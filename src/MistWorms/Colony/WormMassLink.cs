using System;
using System.Collections.Generic;
using MistWorms.Config;
using MistWorms.Drops;
using MistWorms.Mass;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>
    /// Colony side of the link with the writhing masses (spec §3.3 Gathering / Absorbing / Fleeing, §4.1 formation and absorption, §4.4 dissolution,
    /// §11.3; phase 4B-4). Everything runs on the owner of the colony, every 0.25 s for the scans:
    /// - a worm that senses a player gathers toward the nearest mass that is not full, else toward the centroid of at least 2 other active worms
    ///   within Mass.GatherRadius, else it hunts alone (Gathering is switched off by <see cref="GatheringEnabled"/>);
    /// - Mass.FormThreshold or more gathering worms within Mass.FormRadius of their centroid become a mass there (they leave the store and glide onto it);
    /// - an active worm within r + 0.5 m of a mass that is not full climbs onto it in 0.5 s (Absorbing) and joins it (MW_MassAdd when the mass is elsewhere);
    /// - a dissolved mass gives its worms back: they leap from visible places of the heap, flee for Worm.FleeDuration, then gather again if a group is near.
    /// </summary>
    internal sealed partial class WormColony
    {
        /// <summary>Dev switch (<c>ColonyDev.GatheringEnabled</c>): while false a worm that senses a player hunts alone, and nothing forms or absorbs.</summary>
        public static bool GatheringEnabled = true;

        private const float MassTickInterval = 0.25f;       // spec §4.1: scans, formation
        private const float AbsorbSeconds = 0.5f;            // spec §3.3 Absorbing
        private const float AbsorbSpeed = 4.5f;              // m/s: a worm pulled in from afar (a stalled one) takes longer than AbsorbSeconds
        private const float AbsorbMargin = 0.5f;             // r + 0.5 m
        private const float AbsorbMaxDy = 1.5f;
        private const float AbsorbRise = 0.15f;              // the arc of the climb, metres
        private const float AbsorbInside = 0.35f;            // it ends this share of r from the centre, on the heap
        private const float GatherMaxDy = 4f;
        private const int GatherMinGroup = 2;                // spec §3.3: at least 2 other active worms
        private const float GatherGoalInterval = 0.25f;
        private const float GatherGrace = 0.6f;              // a worm that just began to gather keeps going while the scan tables catch up
        private const float GatherStuckWindow = 1.5f;       // a worm that runs for a mass at 5.6 m/s and gains less than 0.5 m in this long is blocked (the 3 s window of §11.1 cost a straggler 6 to 9 s)
        private const float GatherOverObstacleSeconds = 4f;   // a gatherer still stalled this long after it started to gather is pulled in over whatever lies between it and the mass (r8: a prop or a steep rock on the way kept a straggler out for 45 s)
        private const float GatherDirectSeconds = 2f;       // a stalled gatherer steers by its goal alone this long
        private const float GatherPatience = 12f;            // a gathering worm of a group that never forms hunts after this long
        private const int MaxFormPerTick = 2;
        private const byte GatherNone = 0, GatherMass = 1, GatherGroup = 2;

        // Dissolution (spec §4.4, §12.4): the leap from the heap.
        private const float LeapSeconds = 0.35f;
        private const float LeapHeightMin = 0.3f, LeapHeightMax = 0.6f;
        private const float LeapDistMin = 0.5f, LeapDistMax = 1.5f;
        private const float LeapWallHeight = 0.3f;
        private const byte LeapPhase = 1;                    // WormStore.subPhase while Fleeing

        /// <summary>Number of worms the last dissolution could not place (store full or colony disabled), for the dev tools.</summary>
        public int DissolveLost { get; private set; }

        /// <summary>Worms created by dissolutions on this colony since it was loaded.</summary>
        public int DissolvedWorms { get; private set; }

        /// <summary>Masses this colony formed, and worms it let masses absorb (dev checks).</summary>
        public int MassesFormed { get; private set; }
        public int WormsAbsorbed { get; private set; }
        public int WormsFormed { get; private set; }
        /// <summary>Times a worm on its way to a mass made no progress outside its reach and was set straight at it (dev checks).</summary>
        public int GatherStalls { get; private set; }

        private struct MassRef
        {
            public WritheMassState State;
            public ZDOID Id;
            public Vector3 Pos;
            public float R;
            public int N;
            public int Pending;      // worms climbing onto it right now
        }

        private MassRef[] _masses = new MassRef[8];
        private Vector3[] _massPos = new Vector3[8];     // live positions of the masses (every tick) and their load (count + climbing), for WormGather.NearestMass
        private int[] _massLoad = new int[8];
        private int _massCount;
        private float _nextMassTick;
        private bool _massPrefabMissing;

        // Active worms (cached every 0.25 s): where a gatherer looks for its group.
        private Vector3[] _actPos;
        private int[] _actIdx;
        private byte[] _actState;
        private int _actCount;

        // Formation scratch.
        private Vector3[] _gatherPos;
        private int[] _gatherIdx;
        private bool[] _gatherTaken;
        private int[] _cluster;
        private Vector3[] _seedBuf;
        private int _seedStart;

        private Vector3[] _releaseBuf;

        /// <summary>The masses of this colony that exist on this peer (updated every 0.25 s); a colony with a mass does not vanish when it has no worm.</summary>
        public int MassCount => _massCount;

        private void InitMassLink(int capacity)
        {
#if MW_DEV
            _histState = new byte[capacity * HistoryLength];
            _histTime = new float[capacity * HistoryLength];
            _histCount = new int[capacity];
            _dbgStep = new StepResult[capacity];
            _dbgTravel = new float[capacity];
            _dbgSteps = new int[capacity];
#endif
            _actPos = new Vector3[capacity];
            _actIdx = new int[capacity];
            _actState = new byte[capacity];
            _gatherPos = new Vector3[capacity];
            _gatherIdx = new int[capacity];
            _gatherTaken = new bool[capacity];
            _cluster = new int[capacity];
            _seedBuf = new Vector3[capacity];
        }

        // ------------------------------------------------------------------ scans (every 0.25 s)

        private void MassTick(float now)
        {
            if (_actPos == null) return;
            if (now < _nextMassTick) { SyncMassPositions(); return; }
            _nextMassTick = now + MassTickInterval;
            RefreshMasses();
            SyncMassPositions();
            ScanMassHits(now);
            if (!GatheringEnabled) return;
            AbsorbScan(now);
            TryForm(now);
        }

        /// <summary>Positions and loads of the known masses as of this tick: a mass wanders at 2.4 m/s, a goal taken from the last scan is up to 0.6 m old.</summary>
        private void SyncMassPositions()
        {
            if (_massPos.Length < _massCount) { _massPos = new Vector3[_masses.Length]; _massLoad = new int[_masses.Length]; }
            for (int k = 0; k < _massCount; k++)
            {
                WritheMassState m = _masses[k].State;
                if (m != null && m.Valid) _masses[k].Pos = m.transform.position;
                _massPos[k] = _masses[k].Pos;
                _massLoad[k] = _masses[k].N + _masses[k].Pending;
            }
        }

        private void RefreshMasses()
        {
            WormStore s = Store;
            _massCount = 0;
            List<WritheMassState> all = WritheMassState.All;
            long me = Key;
            for (int k = 0; me != 0L && k < all.Count; k++)
            {
                WritheMassState m = all[k];
                if (m == null || !m.Valid || m.ColonyKey != me) continue;
                byte code = m.StateCode;
                if (m.Dissolving || MassStateCodes.IsDissolving(code) || MassStateCodes.IsMerging(code)) continue;
                int n = m.N;
                if (n <= 0) continue;
                if (_massCount == _masses.Length) Array.Resize(ref _masses, _masses.Length * 2);
                ref MassRef e = ref _masses[_massCount++];
                e.State = m;
                e.Id = m.ZdoId;
                e.Pos = m.transform.position;
                e.R = MassMath.Radius(n);
                e.N = n;
                e.Pending = 0;
            }

            _actCount = 0;
            for (int i = 0; i < s.HighWater; i++)
            {
                if (!s.alive[i]) continue;
                var state = (WormState)s.state[i];
                if (state == WormState.Sleeping || state == WormState.Calming || state == WormState.Absorbed || state == WormState.Dead) continue;   // a worm walking home joins nothing
                if (state == WormState.Absorbing)
                {
                    for (int k = 0; k < _massCount; k++)
                        if (_masses[k].Id == s.massRef[i]) { _masses[k].Pending++; break; }
                    continue;
                }
                _actPos[_actCount] = s.pos[i];
                _actIdx[_actCount] = i;
                _actState[_actCount] = s.state[i];
                _actCount++;
            }
        }

        private int FindMass(ZDOID id)
        {
            for (int k = 0; k < _massCount; k++)
                if (_masses[k].Id == id) return k;
            return -1;
        }

        // ------------------------------------------------------------------ gathering (spec §3.3)

        /// <summary>
        /// Where worm <paramref name="i"/> gathers: the nearest mass with room within <paramref name="radius"/> (<see cref="GatherMass"/>), else the centroid
        /// of at least 2 other active worms (<see cref="GatherGroup"/>), else nothing. Reads the tables of the last scan.
        /// </summary>
        private byte FindGatherGoal(int i, float radius, out Vector3 goal)
        {
            Vector3 p = Store.pos[i];
            int max = MWConfig.Mass.MaxCount.Value;
            int bi = WormGather.NearestMass(p, _massPos, _massLoad, _massCount, max, radius, GatherMaxDy);
            if (bi >= 0)
            {
                goal = _masses[bi].Pos;
                return GatherMass;
            }

            int group = WormGather.GroupCentroid(_actPos, _actIdx, _actState, _actCount, i, p, radius, GatherMaxDy, true, (byte)WormState.Fleeing, out goal);
            if (group >= GatherMinGroup) return GatherGroup;
            goal = p;
            return GatherNone;
        }

        /// <summary>A worm that just sensed a player: gather when there is something to join within Mass.GatherRadius, else hunt alone.</summary>
        private void BeginPursuit(int i, float now)
        {
            if (GatheringEnabled && _actPos != null)
            {
                byte kind = FindGatherGoal(i, MWConfig.Mass.GatherRadius.Value, out Vector3 goal);
                if (kind != GatherNone)
                {
                    EnterGathering(i, now, kind, goal);
                    return;
                }
            }
            SetState(i, WormState.Hunting, now);
        }

        private void EnterGathering(int i, float now, byte kind, Vector3 goal)
        {
            WormStore s = Store;
            SetState(i, WormState.Gathering, now);
            s.subPhase[i] = kind;
            s.wanderGoal[i] = goal;
            s.approachNext[i] = now + GatherGoalInterval;
        }

        private void StepGathering(int i, float now, float dt)
        {
            WormStore s = Store;
            if (now >= s.nextSenseTime[i])
            {
                s.nextSenseTime[i] = now + SenseInterval;
                SenseTarget(i, now);
            }
            if (FireCheck(i, false)) { SetState(i, WormState.Fleeing, now); return; }

            bool targeted = ResolveTarget(i, out _);
            if (!GatheringEnabled)
            {
                if (targeted) SetState(i, WormState.Hunting, now);
                else LoseTarget(i, now);
                return;
            }

            // A mass with room within reach beats any group, and its goal is its position of this tick, at every step.
            float gatherRadius = Mathf.Max(MWConfig.Mass.GatherRadius.Value, MWConfig.Mass.RegroupRadius.Value);
            int mk = WormGather.NearestMass(s.pos[i], _massPos, _massLoad, _massCount, MWConfig.Mass.MaxCount.Value, gatherRadius, GatherMaxDy);
            if (mk >= 0)
            {
                s.subPhase[i] = GatherMass;
                s.wanderGoal[i] = _massPos[mk];
                if (TryBeginAbsorb(i, mk, now)) return;
            }
            else if (now >= s.approachNext[i])
            {
                s.approachNext[i] = now + GatherGoalInterval;
                float radius = gatherRadius;
                byte kind = FindGatherGoal(i, radius, out Vector3 goal);
                if (kind == GatherNone && s.stateTime[i] < GatherGrace) return;   // the tables of the scan may predate this worm and its neighbours
                if (kind == GatherNone)
                {
                    // Nothing to join any more: hunt alone if a player is sensed, otherwise back to wandering.
                    if (targeted) SetState(i, WormState.Hunting, now);
                    else LoseTarget(i, now);
                    return;
                }
                s.subPhase[i] = kind;
                s.wanderGoal[i] = goal;
            }

            if (s.subPhase[i] == GatherGroup && s.stateTime[i] > GatherPatience)
            {
                // The group never formed a mass (the others are not gathering): do not wait for ever.
                if (targeted) SetState(i, WormState.Hunting, now);
                else LoseTarget(i, now);
                return;
            }

            Vector3 target = s.wanderGoal[i];
            Vector3 toGoal = target - s.pos[i];
            toGoal.y = 0f;
            bool toMass = s.subPhase[i] == GatherMass;
            bool stuck = CheckStuck(i, now, toMass ? GatherStuckWindow : StuckWindow);      // true once per window (1.5 s to a mass, 3 s otherwise) without 0.5 m of progress
            if (stuck && toGoal.sqrMagnitude > 4f) TryRequestPath(i, target, now);
            if (stuck && toMass && mk >= 0 && WormGather.IsStalled(toGoal.magnitude, _masses[mk].R, 0f))
            {
                // Still stalled after a 2 s try by the goal alone (directUntil was set): the worm is stuck for a reason of the terrain (a ledge, a prop it stands on, a pit). With a clear
                // line to the mass, within StalledAbsorbReach, it is pulled in over the obstacle rather than waiting for ever.
                // After GatherOverObstacleSeconds the line need not be clear: a prop that the worm cannot climb (a wall, a steep rock) would keep it out for ever.
                if (s.directUntil[i] > 0f && TryBeginAbsorb(i, mk, now, WormGather.StalledAbsorbReach, s.stateTime[i] >= GatherOverObstacleSeconds)) return;
                // Waiting outside the reach of the mass for something that cannot come: for the next 2 s only the goal steers (avoidance and separation may cancel it).
                s.directUntil[i] = now + GatherDirectSeconds;
                GatherStalls++;
            }
            target = FollowPath(i, target);
            MoveWorm(i, toMass && now < s.directUntil[i] ? s.wanderGoal[i] : target, MWConfig.Worm.MoveSpeed.Value, now, dt, noiseWeight: 0f, stopAtGoal: !toMass, direct: toMass && now < s.directUntil[i]);
        }

        // ------------------------------------------------------------------ formation (spec §4.1)

        private void TryForm(float now)
        {
            if (_massPrefabMissing) return;
            WormStore s = Store;
            int g = 0;
            int max = MWConfig.Mass.MaxCount.Value;
            float gatherRadius = Mathf.Max(MWConfig.Mass.GatherRadius.Value, MWConfig.Mass.RegroupRadius.Value);   // as StepGathering looks
            for (int i = 0; i < s.HighWater; i++)
            {
                if (!s.alive[i] || s.state[i] != (byte)WormState.Gathering || s.subPhase[i] != GatherGroup) continue;
                if (MassWithRoomWithin(s.pos[i], gatherRadius, max)) continue;      // it joins that mass; it does not start another
                _gatherPos[g] = s.pos[i];
                _gatherIdx[g] = i;
                _gatherTaken[g] = false;
                g++;
            }
            int threshold = MWConfig.Mass.FormThreshold.Value;
            if (g < threshold) return;

            float radius = MWConfig.Mass.FormRadius.Value;
            for (int formed = 0; formed < MaxFormPerTick; formed++)
            {
                int k = WormGather.SelectCluster(_gatherPos, _gatherTaken, g, radius, threshold, max, _cluster, _seedStart, out Vector3 centroid);
                _seedStart = (_seedStart + 1) % g;
                if (k < threshold) break;
                if (!FormMass(k, centroid, now)) break;
            }
        }

        private bool MassWithRoomWithin(Vector3 p, float radius, int max)
        {
            float r2 = radius * radius;
            for (int k = 0; k < _massCount; k++)
            {
                if (_masses[k].N + _masses[k].Pending >= max) continue;
                Vector3 q = _masses[k].Pos;
                float dx = q.x - p.x, dz = q.z - p.z;
                if (dx * dx + dz * dz <= r2 && Mathf.Abs(q.y - p.y) <= GatherMaxDy) return true;
            }
            return false;
        }

        private bool FormMass(int k, Vector3 centroid, float now)
        {
            WormStore s = Store;
            WritheMassState mass = WritheMassState.Create(centroid, k, Key, newborn: true);
            if (mass == null)
            {
                _massPrefabMissing = true;     // logged by Create; do not retry every 0.25 s
                return false;
            }

            // The worms become the first of the heap: they leave the store and glide onto it (spec §12.4).
            _formPool.Clear();
            _formNests.Clear();
            for (int j = 0; j < k; j++)
            {
                int wi = _gatherIdx[_cluster[j]];
                _seedBuf[j] = s.pos[wi];
                if (s.poolIndex[wi] >= 0) _formPool.Merge(s.Pools.Get(s.poolIndex[wi]));
                _formNests.Add(MassNestOf(wi), 1);
                RemoveWorm(wi);
            }
            mass.GiveInitialPool(_formPool);
            mass.GiveInitialNests(_formNests);
            _formNests.Clear();
            _formPool.Clear();
            WritheMassVisual visual = mass.GetComponent<WritheMassVisual>();
            if (visual != null) visual.SeedArrivals(_seedBuf, k);

            if (_massCount == _masses.Length) Array.Resize(ref _masses, _masses.Length * 2);
            ref MassRef e = ref _masses[_massCount++];
            e.State = mass;
            e.Id = mass.ZdoId;
            e.Pos = mass.transform.position;
            e.R = MassMath.Radius(k);
            e.N = k;
            e.Pending = 0;
            SyncMassPositions();

            MassesFormed++;
            WormsFormed += k;
            Plugin.Log.LogInfo($"[Colony] {k} worms formed mass {mass.ZdoId} at {e.Pos}");
            return true;
        }

        /// <summary>Takes a worm out of the store without effect or drop (it joined a mass).</summary>
        private void RemoveWorm(int i)
        {
            ReleasePath(i);
            ResetIdleSound(i);
            Store.Free(i);
        }

        // ------------------------------------------------------------------ absorption (spec §4.1, §3.3 Absorbing)

        private void AbsorbScan(float now)
        {
            if (_massCount == 0) return;
            WormStore s = Store;
            int max = MWConfig.Mass.MaxCount.Value;
            for (int i = 0; i < s.HighWater; i++)
            {
                if (!s.alive[i]) continue;
                byte st = s.state[i];
                if (st != (byte)WormState.Idle && st != (byte)WormState.Gathering && st != (byte)WormState.Hunting) continue;
                Vector3 p = s.pos[i];
                for (int k = 0; k < _massCount; k++)
                    if (TryBeginAbsorb(i, k, now)) break;
            }
        }

        /// <summary>
        /// The worm starts to climb onto mass <paramref name="k"/> when it touches its visible envelope (<see cref="WormGather.AbsorbReach"/>) and the mass has room.
        /// With <paramref name="farReach"/> (a stalled worm) the reach is that distance instead, and nothing solid may lie on the straight line to the mass unless
        /// <paramref name="overObstacles"/> (a worm stalled for good: it is pulled in over what blocks it).
        /// </summary>
        private bool TryBeginAbsorb(int i, int k, float now, float farReach = 0f, bool overObstacles = false)
        {
            WormStore s = Store;
            if (_masses[k].N + _masses[k].Pending >= MWConfig.Mass.MaxCount.Value) return false;
            Vector3 p = s.pos[i];
            Vector3 q = _masses[k].Pos;
            float dx = q.x - p.x, dz = q.z - p.z;
            float reach = farReach > 0f ? farReach : WormGather.AbsorbReach(_masses[k].R);
            if (dx * dx + dz * dz > reach * reach || Mathf.Abs(q.y - p.y) > (overObstacles ? GatherMaxDy : AbsorbMaxDy)) return false;   // a worm stalled for good at the foot of a slope is pulled in up to the height the gathering allows
            if (farReach > 0f && !overObstacles)
            {
                Vector3 lift = Vector3.up * WormPhysics.WallHeight;
                if (Physics.Linecast(p + lift, q + lift, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore)) return false;
            }

            s.massRef[i] = _masses[k].Id;
            s.absorbFrom[i] = p;
            SetState(i, WormState.Absorbing, now);
            s.nextSimTime[i] = now;
            _masses[k].Pending++;
            _massLoad[k]++;
            return true;
        }

        private void StepAbsorbing(int i, float now, float dt)
        {
            WormStore s = Store;
            int mi = FindMass(s.massRef[i]);
            WritheMassState mass = mi >= 0 ? _masses[mi].State : null;
            if (mass == null || !mass.Valid)
            {
                // The mass went away (merged, dissolved, destroyed): the worm is a free worm again where it stands.
                EndAbsorb(i, now);
                return;
            }

            Vector3 mp = mass.transform.position;
            float r = _masses[mi].R;
            Vector3 from = s.absorbFrom[i];
            Vector3 inward = from - mp;
            inward.y = 0f;
            float run = Mathf.Max(AbsorbSeconds, inward.magnitude / AbsorbSpeed);      // 0.5 s within 2.2 m; longer for a worm pulled in from afar
            float t = Mathf.Clamp01(s.stateTime[i] / run);
            float e = t * t * (3f - 2f * t);
            inward = inward.sqrMagnitude > 1e-4f ? inward.normalized : Vector3.forward;
            Vector3 top = mp + inward * (AbsorbInside * r) + Vector3.up * (0.3f * r + 0.05f);
            Vector3 p = Vector3.Lerp(from, top, e);
            p.y += AbsorbRise * Mathf.Sin(Mathf.PI * t);

            Vector3 prev = s.pos[i];
            s.pos[i] = p;
            float moved = (p - prev).magnitude;
            s.heading[i] = WormMath.HeadingOf(-inward.x, -inward.z);
            s.animTime[i] += moved / Mathf.Max(0.1f, MWConfig.Worm.MoveSpeed.Value);
            s.lastSpeed[i] = dt > 1e-4f ? moved / dt : 0f;
            s.stillSince[i] = now;
            s.nextGroundTime[i] = now + GroundInterval;

            if (t < 1f) return;
            MassPool donor = DonorPool(i);   // what the worm ate (and the reserve of the colony) goes into the mass with it
            _oneNest.Clear();
            _oneNest.Add(MassNestOf(i), 1);
            int accepted = mass.AddWorms(1, donor, _oneNest);
            if (accepted == 0)
            {
                EndAbsorb(i, now);     // the mass filled up or started to merge in the meantime
                return;
            }
            WritheMassVisual visual = mass.GetComponent<WritheMassVisual>();
            if (visual != null) visual.NotifyArrival(p);
            WormsAbsorbed++;
            RemoveWorm(i);
        }

        /// <summary>The absorption did not happen: the worm wanders on from the floor under it.</summary>
        private void EndAbsorb(int i, float now)
        {
            WormStore s = Store;
            s.massRef[i] = ZDOID.None;
            s.home[i] = s.pos[i];
            s.goalTime[i] = 0f;
            s.pauseUntil[i] = now;
            s.nextGroundTime[i] = 0f;
            SetState(i, WormState.Idle, now);
        }

        // ------------------------------------------------------------------ dissolution (spec §4.4)

        /// <summary>
        /// Asks the owner of this colony to recreate <paramref name="n"/> fleeing worms where the mass stood <paramref name="position"/>. The owner of the
        /// colony applies it directly; any other peer sends the routed RPC <c>MW_Dissolve</c> (the pool of the mass travels in <paramref name="pool"/>, phase 6).
        /// </summary>
        public void RequestDissolve(ZDOID mass, int n, Vector3 position, ZPackage pool, bool sleep = false)
        {
            if (!_initialized || !_nview.IsValid()) return;
            if (_nview.IsOwner())
            {
                ReceiveDissolve(mass, n, position, pool, sleep);
                return;
            }
            _nview.InvokeRPC(MWNames.RpcMassDissolve, mass, n, position, pool ?? new ZPackage(), sleep);
        }

        private void RPC_MassDissolve(long sender, ZDOID mass, int n, Vector3 position, ZPackage pool, bool sleep)
        {
            try
            {
                if (!_initialized || !_nview.IsValid() || !_nview.IsOwner()) return;
                ReceiveDissolve(mass, n, position, pool, sleep);
            }
            catch (Exception e)
            {
                Plugin.LogException("WormColony.RPC_MassDissolve", e);
            }
        }

#if MW_DEV
        // State history of each worm (the last HistoryLength changes), for the dev journals of stragglers.
        private const int HistoryLength = 8;
        private byte[] _histState;
        private float[] _histTime;
        private int[] _histCount;

        private void RecordHistory(int i, WormState state, float now)
        {
            if (_histCount == null) return;
            int at = i * HistoryLength + (_histCount[i] % HistoryLength);
            _histState[at] = (byte)state;
            _histTime[at] = now;
            _histCount[i]++;
        }

        /// <summary>"Idle.0 > Hunting.5 > Gathering.0" (times in seconds before now), the last states of worm <paramref name="i"/>.</summary>
        public string DevStateHistory(int i)
        {
            if (_histCount == null || _histCount[i] == 0) return "(none)";
            int total = _histCount[i];
            int first = total > HistoryLength ? total - HistoryLength : 0;
            var sb = new System.Text.StringBuilder();
            for (int k = first; k < total; k++)
            {
                int at = i * HistoryLength + (k % HistoryLength);
                if (sb.Length > 0) sb.Append(" > ");
                sb.Append((WormState)_histState[at]).Append('@').Append((Time.time - _histTime[at]).ToString("0.0")).Append(" s ago");
            }
            return sb.ToString();
        }

        /// <summary>The masses this colony knows, as its worms see them: count, load, colony match, position of this tick against the real one (dev checks, S02 stragglers).</summary>
        private StepResult[] _dbgStep;
        private float[] _dbgTravel;
        private int[] _dbgSteps;

        /// <summary>What the last steps of worm <paramref name="i"/> did: result of the last physical step, the distance it tried, steps taken, speed, avoidance direction (S02 stragglers).</summary>
        public string DevStepInfo(int i)
        {
            if (_dbgStep == null) return "(no step info)";
            WormStore s = Store;
            string result = (int)_dbgStep[i] < 0 ? "no travel (speed capped or turning)" : _dbgStep[i].ToString();
            return $"last step {result}, tried {_dbgTravel[i]:0.000} m, {_dbgSteps[i]} steps in all, speed {s.lastSpeed[i]:0.00} m/s, heading {s.heading[i]:0.00} rad, avoid {s.avoidDir[i]}, direct until {s.directUntil[i]:0.0} (now {Time.time:0.0}), sim interval {s.simInterval[i]:0.000} s, path slot {s.pathSlot[i]}, progress window start {s.progressTime[i]:0.0}";
        }

        public string DevMassTable()
        {
            var sb = new System.Text.StringBuilder();
            for (int k = 0; k < _massCount; k++)
            {
                WritheMassState m = _masses[k].State;
                sb.Append($" [mass {_masses[k].Id} n={_masses[k].N} pending={_masses[k].Pending} colonyMatch={(m != null && Key != 0L && m.ColonyKey == Key)} synced={_massPos[k]} real={(m != null ? m.transform.position : Vector3.zero)} load={_massLoad[k]}]");
            }
            foreach (WritheMassState m in WritheMassState.All)
            {
                if (m == null || !m.Valid) continue;
                bool known = false;
                for (int k = 0; k < _massCount; k++) if (_masses[k].Id == m.ZdoId) known = true;
                if (!known) sb.Append($" [UNKNOWN mass {m.ZdoId} n={m.N} colonyMatch={Key != 0L && m.ColonyKey == Key} state={MassStateCodes.Describe(m.StateCode)} dissolving={m.Dissolving} at {m.transform.position}]");
            }
            return sb.Length > 0 ? sb.ToString() : " (no mass)";
        }

        /// <summary>Where the worms of the last dissolution started their leap (dev checks), and how many of those places were worms the layout had drawn.</summary>
        public readonly List<Vector3> DissolveStarts = new List<Vector3>();
        /// <summary>The apex height (m) of the parabola of each leap of the last dissolution, known when the leap is emitted (the observed height depends on when the frames are looked at).</summary>
        public readonly List<float> DissolveApexes = new List<float>();
        public int DissolveFromDrawn { get; private set; }
#endif

        /// <summary>
        /// Creates <paramref name="n"/> worms in the Fleeing state, each leaping (0.3 to 0.6 m high, 0.5 to 1.5 m away from the centre) from the place of a
        /// visible worm of the heap, then fleeing for Worm.FleeDuration. They are worms that already existed, so the population cap does not apply.
        /// </summary>
        private void ReceiveDissolve(ZDOID mass, int n, Vector3 position, ZPackage pool, bool sleep = false)
        {
            DissolveLost = 0;
#if MW_DEV
            DissolveStarts.Clear();
            DissolveApexes.Clear();
            DissolveFromDrawn = 0;
#endif
            if (Store == null || Disabled || n <= 0 || n > MWConfig.Mass.MaxCount.Value)
            {
                DissolveLost = Math.Max(0, n);
                DropRefusedDissolvePool(pool, position);
                Plugin.Log.LogWarning($"[Colony] dissolution of mass {mass} ({n} worms) refused (store {(Store != null ? "ok" : "missing")}, disabled {Disabled})");
                return;
            }

            PrepareDissolvePool(pool, n);
            _dissolveNests.ReadTail(pool);        // the nests of origin of the worms of the heap: each released worm takes one back
            _dissolveNests.FitTo(n);
            if (sleep)
            {
                // The mass went back to sleep (spec §5.2): the position is where it lays its worms down.
                LayDownSleepers(mass, n, position);
                return;
            }
            if (_releaseBuf == null || _releaseBuf.Length < n) _releaseBuf = new Vector3[n];
            int drawn = 0;
            WritheMassVisual visual = null;
            if (ZNetScene.instance != null)
            {
                GameObject go = ZNetScene.instance.FindInstance(mass);
                if (go != null) visual = go.GetComponent<WritheMassVisual>();
            }
            if (visual != null) drawn = visual.GetReleasePoints(_releaseBuf, n);
            else MassLayout.ReleasePoints(MassLayout.SeedFromIds(mass.UserID, mass.ID), n, MassMath.Radius(n), position, Vector3.forward, Vector3.left, _releaseBuf);
#if MW_DEV
            DissolveFromDrawn = drawn;
#endif

            WormStore s = Store;
            float now = Time.time;
            int mask = WormPhysics.SolidMask;
            for (int k = 0; k < n; k++)
            {
                Vector3 pt = _releaseBuf[k];
                Vector3 away = pt - position;
                away.y = 0f;
                if (away.sqrMagnitude < 1e-3f)
                {
                    float a = (float)_rng.NextDouble() * Mathf.PI * 2f;
                    away = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                }
                away.Normalize();

                var start = new Vector3(pt.x, position.y, pt.z);
                int slot = AddWorm(start, WormState.Fleeing, WormMath.HeadingOf(away.x, away.z), force: true);
                if (slot < 0)
                {
                    DissolveLost++;
                    ShareToReserve();   // the worm is lost, its share of the pool is not
                    continue;
                }

                // The landing: 0.5 to 1.5 m away, on a floor, with no wall on the way; shorter, then in place, when it does not fit.
                float dist = LeapDistMin + (float)_rng.NextDouble() * (LeapDistMax - LeapDistMin);
                Vector3 from = s.pos[slot];
                Vector3 landing = from;
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    Vector3 c = from + away * (dist * (1f - 0.5f * attempt));
                    c.y = from.y;
                    if (Physics.Linecast(from + Vector3.up * LeapWallHeight, c + Vector3.up * LeapWallHeight, mask, QueryTriggerInteraction.Ignore)) continue;
                    if (!WormPhysics.ProbeGround(c, out RaycastHit floor) || Mathf.Abs(floor.point.y - from.y) > WormPhysics.StepHeight) continue;
                    landing = floor.point;
                    break;
                }

                GiveDissolvedShare(slot);
                int origin = _dissolveNests.TakeOne();
                s.nestIndex[slot] = origin >= 0 && origin < NestCounts.None ? (short)origin : (short)-1;
                s.fleeFrom[slot] = position;
                s.subPhase[slot] = LeapPhase;
                s.absorbFrom[slot] = from;
                s.wanderGoal[slot] = landing;
                s.leapHeight[slot] = LeapHeightMin + (float)_rng.NextDouble() * (LeapHeightMax - LeapHeightMin);
                s.stateTime[slot] = 0f;
                s.nextSimTime[slot] = now;
                DissolvedWorms++;
#if MW_DEV
                DissolveStarts.Add(from);
                DissolveApexes.Add(s.leapHeight[slot]);
#endif
            }
            Plugin.Log.LogInfo($"[Colony] mass {mass} dissolved: {n - DissolveLost}/{n} fleeing worms created at {position} ({drawn} from drawn worms of the heap)");
        }

        /// <summary>One step of the leap of a dissolved worm: a straight line to the landing with a parabola of the chosen height. True while it lasts.</summary>
        private bool StepLeap(int i, float now, float dt)
        {
            WormStore s = Store;
            float t = Mathf.Clamp01(s.stateTime[i] / LeapSeconds);
            Vector3 p = Vector3.Lerp(s.absorbFrom[i], s.wanderGoal[i], t);
            p.y += s.leapHeight[i] * 4f * t * (1f - t);
            Vector3 prev = s.pos[i];
            s.pos[i] = p;
            float moved = (p - prev).magnitude;
            s.animTime[i] += moved / Mathf.Max(0.1f, MWConfig.Worm.MoveSpeed.Value);
            s.lastSpeed[i] = dt > 1e-4f ? moved / dt : 0f;
            s.stillSince[i] = now;
            s.nextGroundTime[i] = now + GroundInterval;
            if (t < 1f) return true;

            // Landed: the flee timer starts now.
            s.subPhase[i] = 0;
            s.stateTime[i] = 0f;
            s.nextGroundTime[i] = 0f;
            return false;
        }

        /// <summary>End of the flight (spec §3.3 Fleeing): Gathering when a mass or a group is within Mass.RegroupRadius, otherwise Idle where it stands.</summary>
        private void EndFlee(int i, float now)
        {
            WormStore s = Store;
            s.home[i] = s.pos[i];
            s.goalTime[i] = 0f;
            s.pauseUntil[i] = now;
            if (GatheringEnabled && _actPos != null)
            {
                byte kind = FindGatherGoal(i, MWConfig.Mass.RegroupRadius.Value, out Vector3 goal);
                if (kind != GatherNone)
                {
                    EnterGathering(i, now, kind, goal);
                    return;
                }
            }
            SetState(i, WormState.Idle, now);
        }
    }
}
