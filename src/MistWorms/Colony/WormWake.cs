using System;
using System.Collections.Generic;
using MistWorms.Config;
using MistWorms.Mass;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>
    /// Waking, alert, calm and sleep of the nest groups (spec §3.3 Sleeping and Calming, §5.1, §5.2; plan 7-2, decisions 200 and following). A group is the nest
    /// of index k, the worms with <c>nestIndex</c> k (the lone worms attached to it included) and the masses whose majority nest is k; a worm with no nest is a group of
    /// one. The owner of the colony keeps one awake flag per group, publishes them as the bits of <c>mw_awake</c> in the ZDO of the colony (a mass owned by another peer
    /// reads its bit there), and everything here runs on that owner: a player near a sleeping worm or a nest, or a hit dealt by a player, alerts a group, which wakes
    /// its worms (a small delay each) and schedules its neighbours one hop after the other (<see cref="AlertMath"/>); a group with no player within Targets.ActiveRadius
    /// of its members for Targets.CalmDelay goes back to sleep: its free worms walk back to their den (Calming) and its masses to their nest (WritheMassAI, Dormant).
    /// No allocation in this path: fixed tables, the game's own player and character lists, single-hit physics queries.
    /// </summary>
    internal sealed partial class WormColony
    {
        private const float AlertHopSeconds = 0.5f;         // spec §5.1: 2 hops per second
        private const float HitWakeRadius = 5f;             // spec §3.3: the worms this close to a hit wake as well, whatever their group
        private const float NestWakeInterval = 0.5f;        // the sight test of a nest
        private const float CalmInterval = 1f;              // spec §5.2: the groups are evaluated once a second
        private const float DenReach = 0.8f;                // Calming: asleep within this of the den...
        private const float CalmWalkSeconds = 20f;          // ...or after this long, where it stands
        private const float DepositMin = 0.4f, DepositMax = 2f;   // ring of the sleeping worms of a mass that went back to sleep
        private const int DepositTries = 4;
        private const float NestEyeHeight = 0.4f;

        private static readonly int HashAwake = MWNames.ZdoAwake.GetStableHashCode();

#if MW_DEV
        /// <summary>Dev switch: while false nothing wakes by itself (a worm sensing a player, a player near a nest); the hits still do. Set by the runner for every scenario but S14 and S14b.</summary>
        public static bool DevPassiveWake = true;
        private static bool PassiveWake => DevPassiveWake;
#else
        private static bool PassiveWake => true;
#endif

        // One entry per nest group, owner side.
        private bool[] _gAwake;
        private float[] _gAlertAt;                          // when a scheduled alert wakes the group, Never = none
        private float[] _gLastActive;                       // last time a player was within Targets.ActiveRadius of the group
        private Vector3[] _gMin, _gMax;                     // scratch of the calm pass: box of the members
        private bool[] _gHas;
        private Vector3[] _nestPos;                         // scratch of the alert: nest positions
        private bool[] _nestAlive;
        private float[] _alertTimes;
        private int[] _alertQueue;
        private short[] _depositNest;                       // nest of origin of each sleeping worm of a dormant dissolution
        private int _alertsPending;
        private bool _wakeAdopted;
        private float _nextCalm;
        private readonly NestCounts _scanNests = new NestCounts();

        /// <summary>Groups woken, groups sent to sleep, worms laid down asleep by a mass, and worms sent back to their den since this colony was loaded (dev checks).</summary>
        public int GroupsWoken { get; private set; }
        public int GroupsSlept { get; private set; }
        public int SleptWorms { get; private set; }
        public int WormsCalmed { get; private set; }

        private void InitWake()
        {
            _gAwake = new bool[MaxNests];
            _gAlertAt = new float[MaxNests];
            _gLastActive = new float[MaxNests];
            _gMin = new Vector3[MaxNests];
            _gMax = new Vector3[MaxNests];
            _gHas = new bool[MaxNests];
            _nestPos = new Vector3[MaxNests];
            _nestAlive = new bool[MaxNests];
            _alertTimes = new float[MaxNests];
            _alertQueue = new int[MaxNests];
            _depositNest = new short[Math.Max(1, MWConfig.Mass.MaxCount.Value)];
            for (int k = 0; k < MaxNests; k++) _gAlertAt[k] = AlertMath.Never;
        }

        /// <summary>Is group <paramref name="k"/> awake? The owner answers from its table, any other peer from the bits in the ZDO of the colony.</summary>
        public bool IsGroupAwake(int k)
        {
            if (!_initialized || (uint)k >= (uint)MaxNests || !_nview.IsValid()) return false;
            if (_nview.IsOwner()) return _gAwake[k];
            return ((_nview.GetZDO().GetLong(HashAwake, 0L) >> k) & 1L) != 0L;
        }

        /// <summary>Bits of the awake groups as published (dev checks).</summary>
        public long AwakeBits => _initialized && _nview.IsValid() ? _nview.GetZDO().GetLong(HashAwake, 0L) : 0L;

        /// <summary>Is group <paramref name="k"/> waiting for a scheduled alert (dev checks)?</summary>
        public bool IsGroupAlertPending(int k) => _gAlertAt != null && (uint)k < (uint)MaxNests && _gAlertAt[k] != AlertMath.Never;

        private void PublishAwake()
        {
            if (!_nview.IsValid() || !_nview.IsOwner()) return;
            long bits = 0L;
            for (int k = 0; k < MaxNests; k++)
                if (_gAwake[k]) bits |= 1L << k;
            ZDO zdo = _nview.GetZDO();
            if (zdo.GetLong(HashAwake, 0L) != bits) zdo.Set(HashAwake, bits);
        }

        /// <summary>A new owner starts from the bits the previous one published, with a fresh calm timer.</summary>
        private void AdoptAwake(float now)
        {
            _wakeAdopted = true;
            long bits = _nview.GetZDO().GetLong(HashAwake, 0L);
            for (int k = 0; k < MaxNests; k++)
            {
                _gAwake[k] = ((bits >> k) & 1L) != 0L;
                _gLastActive[k] = now;
                _gAlertAt[k] = AlertMath.Never;
            }
            _alertsPending = 0;
            _nextCalm = now + CalmInterval;
        }

        /// <summary>Everyone back to sleep and no alert in flight (mw_kill, a mine reset).</summary>
        private void ResetWake()
        {
            if (_gAwake == null) return;
            for (int k = 0; k < MaxNests; k++)
            {
                _gAwake[k] = false;
                _gAlertAt[k] = AlertMath.Never;
            }
            _alertsPending = 0;
            PublishAwake();
        }

        // ------------------------------------------------------------------ the tick

        /// <summary>Every frame, owner side: adopts the awake bits, runs the scheduled alerts, reads the hits on the nests and tests the sight of the nests of sleeping groups.</summary>
        private void TickWake(float now)
        {
            if (_gAwake == null) return;
            if (!_wakeAdopted) AdoptAwake(now);
            if (!MWConfig.General.Enabled.Value) return;
            if (_alertsPending > 0) PumpAlerts(now);
            if (NestsLoaded <= 0) return;

            for (int k = 0; k < _nests.Length; k++)
            {
                WormNest nest = _nests[k];
                if (nest == null || !nest.IsValid) continue;

                long hit = nest.PlayerHitTicks;
                if (hit != nest.SeenPlayerHit)
                {
                    nest.SeenPlayerHit = hit;
                    if (hit != 0L) OnPlayerHit(nest.transform.position, k, now);
                }

                // Spec §5.1: a player within Nest.WakeRadius of the nest and in its line of sight. A group that is awake needs no test.
                if (!PassiveWake || _gAwake[k] || now < nest.NextWakeCheck) continue;
                nest.NextWakeCheck = now + NestWakeInterval;
                Vector3 np = nest.transform.position;
                if (WakeSourcePerceived(np, np + Vector3.up * NestEyeHeight, MWConfig.Nest.WakeRadius.Value, 0f, false)) Alert(k, now);
            }
        }

        /// <summary>Wakes the groups whose scheduled alert is due.</summary>
        private void PumpAlerts(float now)
        {
            for (int k = 0; k < MaxNests && _alertsPending > 0; k++)
            {
                if (_gAlertAt[k] > now) continue;     // Never is +infinity
                WakeGroup(k, now);                    // clears the schedule
            }
        }

        /// <summary>Hits dealt by a player to the masses of this colony (read from their ZDO every 0.25 s): each group present in a mass is alerted from the mass.</summary>
        private void ScanMassHits(float now)
        {
            if (_gAwake == null || !MWConfig.General.Enabled.Value) return;
            List<WritheMassState> all = WritheMassState.All;
            long me = Key;
            for (int i = 0; me != 0L && i < all.Count; i++)
            {
                WritheMassState m = all[i];
                if (m == null || !m.Valid || m.ColonyKey != me) continue;
                long hit = m.PlayerHitTicks;
                if (m.SeenPlayerHit < 0L) { m.SeenPlayerHit = hit; continue; }
                if (hit == m.SeenPlayerHit) continue;
                m.SeenPlayerHit = hit;

                Vector3 at = m.transform.position;
                m.CopyNestsTo(_scanNests);
                bool any = false;
                for (int k = 0; k < NestCounts.Slots; k++)
                {
                    if (_scanNests[k] <= 0) continue;
                    any = true;
                    OnPlayerHit(at, k, now);
                }
                if (!any) OnPlayerHit(at, -1, now);
            }
        }

        // ------------------------------------------------------------------ alert and waking

        /// <summary>
        /// A player dealt damage at <paramref name="point"/> to a worm, a nest or a mass of group <paramref name="group"/> (-1 = none): the sleeping worms within 5 m wake
        /// whatever their group (spec §3.3), and the group is alerted.
        /// </summary>
        private void OnPlayerHit(Vector3 point, int group, float now)
        {
            if (!MWConfig.General.Enabled.Value || Disabled) return;
            WakeNear(point, HitWakeRadius, now);
            if (group >= 0) Alert(group, now);
        }

        /// <summary>
        /// Alert of group <paramref name="k"/>: it wakes at once, and the groups whose nest is within Colony.AlertRadius of it, then of those, wake one hop later each
        /// (<see cref="AlertMath.Schedule"/>); a group already scheduled earlier keeps its time.
        /// </summary>
        private void Alert(int k, float now)
        {
            if ((uint)k >= (uint)MaxNests) return;
            WakeGroup(k, now);

            for (int j = 0; j < MaxNests; j++)
            {
                WormNest nest = _nests[j];
                _nestAlive[j] = nest != null && nest.IsValid;
                if (_nestAlive[j]) _nestPos[j] = nest.transform.position;
            }
            AlertMath.Schedule(_nestPos, _nestAlive, MaxNests, MWConfig.Colony.AlertRadius.Value, AlertHopSeconds, k, now, _alertTimes, _alertQueue);
            _alertsPending += AlertMerge.Into(_gAlertAt, _alertTimes, _gAwake, MaxNests, k);
        }

        /// <summary>Marks group <paramref name="k"/> awake (publishing it when it was not), drops its scheduled alert and starts its calm timer.</summary>
        private void MarkAwake(int k, float now)
        {
            if ((uint)k >= (uint)MaxNests) return;
            _gLastActive[k] = now;
            if (_gAlertAt[k] != AlertMath.Never)
            {
                _gAlertAt[k] = AlertMath.Never;
                _alertsPending--;
            }
            if (_gAwake[k]) return;
            _gAwake[k] = true;
            GroupsWoken++;
            PublishAwake();
#if MW_DEV
            Plugin.DevInfo($"[Wake] colony {ZdoId}: group {k} awake");
#endif
        }

        /// <summary>Wakes every sleeping worm of group <paramref name="k"/> (a delay of 0 to 0.4 s each), brings the Calming ones back to Idle, and marks the group awake. Returns the worms woken.</summary>
        private int WakeGroup(int k, float now)
        {
            WormStore s = Store;
            int woken = 0;
            for (int i = 0; i < s.HighWater; i++)
            {
                if (!s.alive[i] || s.nestIndex[i] != k) continue;
                byte st = s.state[i];
                if (st == (byte)WormState.Sleeping) { WakeWorm(i, now, true); woken++; }
                else if (st == (byte)WormState.Calming) { WakeWorm(i, now, false); woken++; }
            }
            MarkAwake(k, now);
            return woken;
        }

        /// <summary>Wakes the sleeping worms within <paramref name="radius"/> of <paramref name="point"/>, each one alone (its group is marked awake, its siblings sleep on).</summary>
        private void WakeNear(Vector3 point, float radius, float now)
        {
            WormStore s = Store;
            float r2 = radius * radius;
            for (int i = 0; i < s.HighWater; i++)
            {
                if (!s.alive[i] || s.state[i] != (byte)WormState.Sleeping) continue;
                if ((s.pos[i] - point).sqrMagnitude > r2) continue;
                if (s.nestIndex[i] >= 0) MarkAwake(s.nestIndex[i], now);
                WakeWorm(i, now, true);
            }
        }

        /// <summary>
        /// A sleeping worm becomes Idle where it lies (its den and its home), after a small delay of its own so that a group does not start in one frame; a Calming worm
        /// simply goes back to Idle (its den stays).
        /// </summary>
        private void WakeWorm(int i, float now, bool fromSleep)
        {
            WormStore s = Store;
            Vector3 p = s.pos[i];
            float delay = fromSleep ? WakeMath.Stagger(i, s.generation[i]) : 0f;
            ClearTarget(i);
            SetState(i, WormState.Idle, now);
            s.home[i] = p;
            if (fromSleep) s.den[i] = p;
            s.goalTime[i] = 0f;
            s.pauseUntil[i] = now + delay;
            s.nextSenseTime[i] = now + delay;
            s.nextSimTime[i] = now + delay + SimPhase(i);
            s.lastActive[i] = now;
            s.nextGroundTime[i] = 0f;
        }

        // ------------------------------------------------------------------ perception of the sleeping worms (spec §3.1, §5.1)

        private void StepSleeping(int i, float now)
        {
            GroundMaintain(i, now);
            WormStore s = Store;
            if (!s.alive[i] || !PassiveWake || !MWConfig.General.Enabled.Value) return;

            // At its own 2 Hz tick: a player within DormantViewRange in sight, or heard within DormantHearRange (the noise of the player counts, a crouching one gets closer).
            Vector3 pos = s.pos[i];
            if (!WakeSourcePerceived(pos, pos + Vector3.up * EyeHeight, MWConfig.Worm.DormantViewRange.Value, MWConfig.Worm.DormantHearRange.Value, Character.InInterior(pos))) return;
            int g = s.nestIndex[i];
            if (g >= 0) Alert(g, now);
            else WakeWorm(i, now, true);
        }

        /// <summary>
        /// Does a wake up source perceive the point <paramref name="pos"/>? The sources are the players (Targets.WakeOnlyByPlayers) or, when that is false, every character
        /// that is not a mass. Sight: within <paramref name="viewRange"/> of the eye and not behind a view blocker, the ray is cast only for a source in range. Hearing:
        /// <see cref="WormMath.CanHear"/> with <paramref name="hearRange"/> (0 = deaf).
        /// </summary>
        private bool WakeSourcePerceived(Vector3 pos, Vector3 eye, float viewRange, float hearRange, bool interior)
        {
            for (int k = 0; k < _players.Count; k++)
            {
                Player p = _players[k];
                if (!Targetable(p) || !Perceives(p, pos, eye, viewRange, hearRange, interior) || TargetScanner.IsExcluded(p)) continue;
                return true;
            }
            if (MWConfig.Targets.WakeOnlyByPlayers.Value) return false;

            List<Character> everyone = Character.GetAllCharacters();
            for (int k = 0; k < everyone.Count; k++)
            {
                Character c = everyone[k];
                if (c == null || c is Player || c.IsDead()) continue;
                if (!Perceives(c, pos, eye, viewRange, hearRange, interior) || WritheMassState.TryGet(c, out _) || TargetScanner.IsExcluded(c)) continue;
                return true;
            }
            return false;
        }

        private static bool Perceives(Character c, Vector3 pos, Vector3 eye, float viewRange, float hearRange, bool interior)
        {
            if (hearRange > 0f && WormMath.CanHear(Vector3.Distance(c.transform.position, pos), hearRange, interior, c.GetNoiseRange())) return true;
            if (viewRange <= 0f) return false;
            Vector3 center = c.GetCenterPoint();
            if (Vector3.Distance(eye, center) > viewRange) return false;
            Vector3 target = c is Player player ? player.GetEyePoint() : center;
            return !Physics.Linecast(eye, target, WormPhysics.ViewBlockMask, QueryTriggerInteraction.Ignore);
        }

        // ------------------------------------------------------------------ calm (spec §5.2)

        /// <summary>
        /// Once a second, owner side. Box of the active members of each group (its nest, its free worms that are neither asleep nor walking home, its masses): a player
        /// within Targets.ActiveRadius of the box keeps the group awake; after Targets.CalmDelay without one the group goes to sleep and its free worms are recalled
        /// (Calming). A worm with no nest is a group of one measured from its own position. A free worm that is active in a group that sleeps (it was woken alone by a
        /// hit, or left over) is recalled as well.
        /// </summary>
        private void TickCalm(float now)
        {
            if (_gAwake == null || now < _nextCalm || !MWConfig.General.Enabled.Value) return;
            _nextCalm = now + CalmInterval;

            WormStore s = Store;
            float radius = MWConfig.Targets.ActiveRadius.Value;
            float delay = MWConfig.Targets.CalmDelay.Value;
            Array.Clear(_gHas, 0, _gHas.Length);

            for (int i = 0; i < s.HighWater; i++)
            {
                if (!s.alive[i] || !IsAwakeFreeState(s.state[i])) continue;
                int g = s.nestIndex[i];
                if (g >= 0)
                {
                    Include(g, s.pos[i]);
                    continue;
                }
                // No nest: its own group.
                if (AnyPlayerWithin(s.pos[i], radius)) s.lastActive[i] = now;
                else if (WakeMath.CalmElapsed(now, s.lastActive[i], delay) && Recallable(i)) SetCalming(i, now);
            }
            for (int k = 0; k < _nests.Length; k++)
            {
                WormNest nest = _nests[k];
                if (nest != null && nest.IsValid) Include(k, nest.transform.position);
            }
            List<WritheMassState> masses = WritheMassState.All;
            long me = Key;
            for (int m = 0; me != 0L && m < masses.Count; m++)
            {
                WritheMassState mass = masses[m];
                if (mass == null || !mass.Valid || mass.Dissolving || mass.ColonyKey != me) continue;
                int g = mass.MajorityNest();
                if (g >= 0) Include(g, mass.transform.position);
            }

            bool changed = false;
            for (int k = 0; k < MaxNests; k++)
            {
                if (!_gAwake[k]) continue;
                bool near = _gHas[k] && AnyPlayerNearBox(_gMin[k], _gMax[k], radius);
                if (near) _gLastActive[k] = now;
                if (_gHas[k] && !WakeMath.CalmElapsed(now, _gLastActive[k], delay)) continue;
                _gAwake[k] = false;
                changed = true;
                GroupsSlept++;
#if MW_DEV
                Plugin.DevInfo($"[Wake] colony {ZdoId}: group {k} back to sleep");
#endif
            }
            if (changed) PublishAwake();

            // Recall: the free worms of a group that sleeps.
            for (int i = 0; i < s.HighWater; i++)
            {
                if (!s.alive[i] || s.nestIndex[i] < 0 || _gAwake[s.nestIndex[i]]) continue;
                if (Recallable(i)) SetCalming(i, now);
            }
        }

        private static bool IsAwakeFreeState(byte state) =>
            state != (byte)WormState.Sleeping && state != (byte)WormState.Calming && state != (byte)WormState.Absorbed && state != (byte)WormState.Dead;

        /// <summary>A worm that can be called home now: not asleep or already walking, not in the middle of a strike or of the leap out of a dissolved mass.</summary>
        private bool Recallable(int i)
        {
            WormStore s = Store;
            byte st = s.state[i];
            if (!IsAwakeFreeState(st) || st == (byte)WormState.Striking) return false;
            return !(st == (byte)WormState.Fleeing && s.subPhase[i] == LeapPhase);
        }

        private void Include(int g, Vector3 p)
        {
            if (!_gHas[g])
            {
                _gHas[g] = true;
                _gMin[g] = p;
                _gMax[g] = p;
                return;
            }
            Vector3 lo = _gMin[g], hi = _gMax[g];
            if (p.x < lo.x) lo.x = p.x; else if (p.x > hi.x) hi.x = p.x;
            if (p.y < lo.y) lo.y = p.y; else if (p.y > hi.y) hi.y = p.y;
            if (p.z < lo.z) lo.z = p.z; else if (p.z > hi.z) hi.z = p.z;
            _gMin[g] = lo;
            _gMax[g] = hi;
        }

        private bool AnyPlayerWithin(Vector3 p, float radius)
        {
            float r2 = radius * radius;
            for (int k = 0; k < _players.Count; k++)
            {
                Player pl = _players[k];
                if (Targetable(pl) && (pl.transform.position - p).sqrMagnitude <= r2) return true;
            }
            return false;
        }

        private bool AnyPlayerNearBox(Vector3 min, Vector3 max, float radius)
        {
            var box = new Bounds();
            box.SetMinMax(min, max);
            float r2 = radius * radius;
            for (int k = 0; k < _players.Count; k++)
            {
                Player pl = _players[k];
                if (Targetable(pl) && box.SqrDistance(pl.transform.position) <= r2) return true;
            }
            return false;
        }

        /// <summary>The worm gives up what it was doing and walks back to its den.</summary>
        private void SetCalming(int i, float now)
        {
            WormStore s = Store;
            ClearTarget(i);
            s.massRef[i] = ZDOID.None;
            s.goalTime[i] = 0f;
            s.nextAvoidTime[i] = 0f;
            SetState(i, WormState.Calming, now);
            WormsCalmed++;
        }

        private void StepCalming(int i, float now, float dt)
        {
            WormStore s = Store;
            Vector3 den = s.den[i];
            Vector3 d = den - s.pos[i];
            d.y = 0f;
            if (d.sqrMagnitude <= DenReach * DenReach || s.stateTime[i] > CalmWalkSeconds)
            {
                // Home (or out of patience): asleep where it stands.
                s.home[i] = s.pos[i];
                s.nextGroundTime[i] = 0f;
                SetState(i, WormState.Sleeping, now);
                return;
            }
            if (CheckStuck(i, now)) TryRequestPath(i, den, now);
            Vector3 goal = FollowPath(i, den);
            MoveWorm(i, goal, MWConfig.Worm.MoveSpeed.Value, now, dt, noiseWeight: 0f, stopAtGoal: true);
        }

        // ------------------------------------------------------------------ a mass goes back to sleep (spec §5.2)

        /// <summary>
        /// The sleeping worms of a mass that went home: <paramref name="n"/> worms laid on the ring of 0.4 to 2 m around <paramref name="center"/> (on the floor, no wall
        /// between, in the box; the centre itself when no place is found), their pool in equal shares and their nest of origin exactly as the mass counted them. No leap,
        /// no flight: they are asleep. They already existed, so the mine cap does not apply.
        /// </summary>
        private void LayDownSleepers(ZDOID mass, int n, Vector3 center)
        {
            WormStore s = Store;
            if (_depositNest.Length < n) _depositNest = new short[n];   // Mass.MaxCount was raised since the colony loaded
            _dissolveNests.DrainTo(_depositNest, n);
            float start = (float)_rng.NextDouble() * Mathf.PI * 2f;
            int placed = 0;
            for (int k = 0; k < n; k++)
            {
                Vector3 at = DepositPoint(center, k, n, start);
                Vector3 away = at - center;
                float heading = away.x * away.x + away.z * away.z > 1e-4f ? WormMath.HeadingOf(away.x, away.z) : (float)_rng.NextDouble() * Mathf.PI * 2f - Mathf.PI;
                int slot = AddWorm(at, WormState.Sleeping, heading, force: true);
                if (slot < 0)
                {
                    DissolveLost++;
                    ShareToReserve();   // the worm is lost, its share of the pool is not
                    continue;
                }
                GiveDissolvedShare(slot);
                s.nestIndex[slot] = _depositNest[k];
                s.lone[slot] = false;
                s.den[slot] = s.pos[slot];
                placed++;
            }
            SleptWorms += placed;
            Plugin.DevInfo($"[Colony] mass {mass} went back to sleep: {placed}/{n} sleeping worms laid down around {center}");
        }

        private Vector3 DepositPoint(Vector3 center, int k, int n, float start)
        {
            int solid = WormPhysics.SolidMask;
            for (int attempt = 0; attempt < DepositTries; attempt++)
            {
                WakeMath.RingPoint(k, n, start + attempt * 1.3f, DepositMin, DepositMax - attempt * 0.2f, out float dx, out float dz);
                Vector3 from = center + new Vector3(dx, 1.2f, dz);
                if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, 3f, solid, QueryTriggerInteraction.Ignore)) continue;
                if (Vector3.Angle(hit.normal, Vector3.up) > WormPhysics.MaxSlopeDeg) continue;
                if (Mathf.Abs(hit.point.y - center.y) > 1f || !InBox(hit.point)) continue;
                if (Physics.Linecast(center + Vector3.up * 0.6f, hit.point + Vector3.up * 0.3f, solid, QueryTriggerInteraction.Ignore)) continue;
                return hit.point;
            }
            return center;
        }
    }
}
