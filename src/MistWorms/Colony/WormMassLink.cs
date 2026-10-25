using System;
using System.Collections.Generic;
using MistWorms.Config;
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
        private const float AbsorbMargin = 0.5f;             // r + 0.5 m
        private const float AbsorbMaxDy = 1.5f;
        private const float AbsorbRise = 0.15f;              // the arc of the climb, metres
        private const float AbsorbInside = 0.35f;            // it ends this share of r from the centre, on the heap
        private const float GatherMaxDy = 4f;
        private const int GatherMinGroup = 2;                // spec §3.3: at least 2 other active worms
        private const float GatherGoalInterval = 0.25f;
        private const float GatherGrace = 0.6f;              // a worm that just began to gather keeps going while the scan tables catch up
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
            if (now < _nextMassTick || _actPos == null) return;
            _nextMassTick = now + MassTickInterval;
            RefreshMasses();
            if (!GatheringEnabled) return;
            AbsorbScan(now);
            TryForm(now);
        }

        private void RefreshMasses()
        {
            WormStore s = Store;
            _massCount = 0;
            List<WritheMassState> all = WritheMassState.All;
            ZDOID me = ZdoId;
            for (int k = 0; k < all.Count; k++)
            {
                WritheMassState m = all[k];
                if (m == null || !m.Valid || m.ColonyId != me) continue;
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
                if (state == WormState.Sleeping || state == WormState.Absorbed || state == WormState.Dead) continue;
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
            float best = radius * radius;
            int bi = -1;
            for (int k = 0; k < _massCount; k++)
            {
                if (_masses[k].N + _masses[k].Pending >= max) continue;
                Vector3 q = _masses[k].Pos;
                float dx = q.x - p.x, dz = q.z - p.z;
                float d2 = dx * dx + dz * dz;
                if (d2 > best || Mathf.Abs(q.y - p.y) > GatherMaxDy) continue;
                best = d2;
                bi = k;
            }
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

            bool targeted = s.targetId[i] != 0 && Targetable(FindPlayer(s.targetId[i]));
            if (!GatheringEnabled)
            {
                if (targeted) SetState(i, WormState.Hunting, now);
                else LoseTarget(i, now);
                return;
            }

            if (now >= s.approachNext[i])
            {
                s.approachNext[i] = now + GatherGoalInterval;
                float radius = Mathf.Max(MWConfig.Mass.GatherRadius.Value, MWConfig.Mass.RegroupRadius.Value);
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
            if (CheckStuck(i, now) && toGoal.sqrMagnitude > 4f) TryRequestPath(i, target, now);
            target = FollowPath(i, target);
            MoveWorm(i, target, MWConfig.Worm.MoveSpeed.Value, now, dt, noiseWeight: 0f, stopAtGoal: !toMass);
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
            WritheMassState mass = WritheMassState.Create(centroid, k, ZdoId, newborn: true);
            if (mass == null)
            {
                _massPrefabMissing = true;     // logged by Create; do not retry every 0.25 s
                return false;
            }

            // The worms become the first of the heap: they leave the store and glide onto it (spec §12.4).
            for (int j = 0; j < k; j++)
            {
                int wi = _gatherIdx[_cluster[j]];
                _seedBuf[j] = s.pos[wi];
                RemoveWorm(wi);
            }
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
                {
                    if (_masses[k].N + _masses[k].Pending >= max) continue;
                    Vector3 q = _masses[k].Pos;
                    float dx = q.x - p.x, dz = q.z - p.z;
                    float reach = _masses[k].R + AbsorbMargin;
                    if (dx * dx + dz * dz > reach * reach || Mathf.Abs(q.y - p.y) > AbsorbMaxDy) continue;

                    s.massRef[i] = _masses[k].Id;
                    s.absorbFrom[i] = p;
                    SetState(i, WormState.Absorbing, now);
                    s.nextSimTime[i] = now;
                    _masses[k].Pending++;
                    break;
                }
            }
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
            float t = Mathf.Clamp01(s.stateTime[i] / AbsorbSeconds);
            float e = t * t * (3f - 2f * t);
            Vector3 from = s.absorbFrom[i];
            Vector3 inward = from - mp;
            inward.y = 0f;
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
            int accepted = mass.AddWorms(1);
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
        public void RequestDissolve(ZDOID mass, int n, Vector3 position, ZPackage pool)
        {
            if (!_initialized || !_nview.IsValid()) return;
            if (_nview.IsOwner())
            {
                ReceiveDissolve(mass, n, position);
                return;
            }
            _nview.InvokeRPC(MWNames.RpcMassDissolve, mass, n, position, pool ?? new ZPackage());
        }

        private void RPC_MassDissolve(long sender, ZDOID mass, int n, Vector3 position, ZPackage pool)
        {
            try
            {
                if (!_initialized || !_nview.IsValid() || !_nview.IsOwner()) return;
                ReceiveDissolve(mass, n, position);
            }
            catch (Exception e)
            {
                Plugin.LogException("WormColony.RPC_MassDissolve", e);
            }
        }

#if MW_DEV
        /// <summary>Where the worms of the last dissolution started their leap (dev checks), and how many of those places were worms the layout had drawn.</summary>
        public readonly List<Vector3> DissolveStarts = new List<Vector3>();
        public int DissolveFromDrawn { get; private set; }
#endif

        /// <summary>
        /// Creates <paramref name="n"/> worms in the Fleeing state, each leaping (0.3 to 0.6 m high, 0.5 to 1.5 m away from the centre) from the place of a
        /// visible worm of the heap, then fleeing for Worm.FleeDuration. They are worms that already existed, so the population cap does not apply.
        /// </summary>
        private void ReceiveDissolve(ZDOID mass, int n, Vector3 position)
        {
            DissolveLost = 0;
#if MW_DEV
            DissolveStarts.Clear();
            DissolveFromDrawn = 0;
#endif
            if (Store == null || Disabled || n <= 0 || n > MWConfig.Mass.MaxCount.Value)
            {
                DissolveLost = Math.Max(0, n);
                Plugin.Log.LogWarning($"[Colony] dissolution of mass {mass} ({n} worms) refused (store {(Store != null ? "ok" : "missing")}, disabled {Disabled})");
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
