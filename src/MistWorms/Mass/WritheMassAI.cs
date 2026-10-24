using System;
using System.Diagnostics;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Mass
{
    /// <summary>
    /// AI of a writhing mass (spec §4.6, §4.7, §5.3, §11.2, plan 4B-2). A <see cref="BaseAI"/>, not a MonsterAI: the MonsterAI chase needs a weapon and
    /// starts vanilla attacks; this one only picks a player, walks to it with the vanilla pathfinding and stops at contact (no engulfing before phase 5).
    /// The prefab needs some BaseAI because <c>EnemyHud</c> calls <c>GetBaseAI()</c> without a null test.
    /// <list type="bullet">
    /// <item>Hunt: toward the target, stopping at r(n) + Mass.EngulfRange. Idle: wanders within 6 m of the last point of interest at half speed.</item>
    /// <item>Dormant: a stub, the mass stands still (phase 7).</item>
    /// <item>Stream: every 0.25 s the free width ahead decides between the dome and the ribbon form (<see cref="MassStream"/>).</item>
    /// </list>
    /// Runs on the owner only. BaseAI.UpdateAI is deliberately not called: its regeneration would put the health out of step with mw_n / mw_partial.
    /// </summary>
    internal sealed class WritheMassAI : BaseAI
    {
        private const float TargetPeriod = 0.5f;         // spec §5.3
        private const float ForgetTime = 5f;             // a target no longer perceived is dropped after this long
        private const float IdleSpeedFactor = 0.5f;      // spec plan 4B-2: wandering at half speed
        private const float WanderRange = 6f;            // spec §4.6
        private const float EyeHeight = 0.8f;
        private const float ProbeHeight = 0.6f;          // height of the width probes: the chest of a passage
        private const float ProbeSideMargin = 1.5f;      // probes reach r + this on each side
        private const float ProbeSideCap = 3.5f;
        private const float ProbeStep = 0.5f;
        private const float ProbeNearEnd = ProbeDistance;
        private const float ProbeDistance = MassStream.ProbeDistance;
        private const float CostLogPeriod = 10f;
        private const string PlayerPrefab = "Player";
        private const float DevGoalStop = 0.4f;

        // Step assist (stairs of the mines): the capsule (r 0.6, a sphere resting on the floor) can stall against a step that a taller
        // capsule would roll over. When the mass wants to walk, makes no headway for two windows, and a low obstacle ahead has free space
        // above it, the body is lifted by a small step. Limited to this component; nothing vanilla is patched.
        private const float AssistWindow = 0.4f;
        private const float AssistStalledFraction = 0.2f;     // moved less than this fraction of the wanted distance
        private const int AssistStalledWindows = 2;
        private const float AssistFootHeight = 0.1f;
        private const float AssistMaxStep = 0.65f;
        private static readonly float[] AssistNudges = { 0.35f, 0.7f, 1.0f };
        private static readonly float[] AssistLifts = { 0.25f, 0.45f, 0.65f, 0.85f, 1.05f };
        private const float AssistHopStride = 1.0f;
        private const float AssistGoalReach = 1.6f;
        private const float AssistHopProbe = 1.2f;
        private const float AssistReach = 0.5f;               // beyond the capsule radius

        private WritheMassState _state;
        private Player _target;
        private float _targetSeen;
        private float _nextTarget;
        private float _nextProbe;
        private Vector3 _home;
        private bool _homeSet;
        private float _speedFactor = -1f;
        private bool _failed;
        private string _excludedRaw;
        private string[] _excluded = new string[0];

        // Cost and path statistics (read by the dev tools; a few integer increments per AI tick).
        private long _costTicks;
        private double _costSumMs;
        private float _costMaxMs;
        private float _nextCostLog;

        /// <summary>AI ticks spent walking toward a target, and how many of them had a vanilla path / had none (then the mass pushes straight at it).</summary>
        public int MoveTicks { get; private set; }
        public int PathTicks { get; private set; }
        public int NoPathTicks { get; private set; }

        /// <summary>Times the step assist lifted the mass over a low obstacle, and what the last stalled check found (dev log).</summary>
        public int StepAssists { get; private set; }
        public string LastAssistNote { get; private set; }
        public int StalledChecks { get; private set; }
        private Rigidbody _rb;
        private CapsuleCollider _capsule;
        private float _windowStart;
        private Vector3 _windowPos;
        private int _stalledWindows;

        public float CostAvgMs => _costTicks > 0 ? (float)(_costSumMs / _costTicks) : 0f;
        public float CostMaxMs => _costMaxMs;

        /// <summary>Dev: this player is the target whatever the senses say (stairs test of S08). Null = the normal rules.</summary>
        public Player DevForcedTarget { get; set; }

        public Player Target => _target;

        /// <summary>Dev: walk to this point (stopping 0.4 m from it) instead of toward a target. Used by the stairs test.</summary>
        public Vector3? DevGoal { get; set; }

        /// <summary>Why the last target choice found nobody (dev log): players seen in the list, the closest one and what the senses said.</summary>
        public string SenseNote { get; private set; }

        /// <summary>Agent type of the vanilla pathfinder, settable so the stairs test can try the next size (decisions.md 24).</summary>
        public Pathfinding.AgentType PathAgent
        {
            get => m_pathAgentType;
            set => m_pathAgentType = value;
        }

        public override void Awake()
        {
            base.Awake();
            _state = GetComponent<WritheMassState>();
            // Config dependent, so read per instance (the prefab is built before the synced config is known).
            m_viewRange = MWConfig.Mass.TargetRange.Value;
            m_viewAngle = 180f;                      // half angle: 360 degrees in all
            m_hearRange = MWConfig.Worm.HearRange.Value;
            m_avoidWater = true;
            m_afraidOfFire = true;                   // the fear itself comes with phase 5
            m_randomMoveRange = WanderRange;
        }

        public override bool UpdateAI(float dt)
        {
            if (m_nview == null || !m_nview.IsValid() || !m_nview.IsOwner() || _state == null) return false;

            long start = Stopwatch.GetTimestamp();
            try
            {
                if (!_failed) Step(dt);
            }
            catch (Exception e)
            {
                // One bad frame must not reach the game: stop this AI and say so once.
                _failed = true;
                StopMoving();
                Plugin.LogException("WritheMassAI.Step (AI stopped)", e);
            }
            RecordCost(start);
            return false;
        }

        private void Step(float dt)
        {
            float now = Time.time;
            byte mode = MassStateCodes.Mode(_state.StateCode);
            if (_state.FreezeAI || _state.Dissolving || mode == MassStateCodes.Dormant)
            {
                StopMoving();
                return;
            }

            if (now >= _nextTarget)
            {
                _nextTarget = now + TargetPeriod;
                ChooseTarget(now);
            }
            if (now >= _nextProbe)
            {
                _nextProbe = now + MassStream.ProbePeriod;
                ProbeStream();
            }

            if (_target != null || DevGoal.HasValue) Hunt(dt);
            else Idle(dt);
        }

        // ------------------------------------------------------------------ target (spec §5.3, players only in phase 4B)

        private void ChooseTarget(float now)
        {
            if (!MWConfig.Targets.AttackPlayers.Value && DevForcedTarget == null)
            {
                SetTarget(null);
                return;
            }

            Player forced = DevForcedTarget;
            if (forced != null && Targetable(forced))
            {
                _targetSeen = now;
                SetTarget(forced);
                return;
            }

            Vector3 pos = transform.position;
            Vector3 eye = pos + Vector3.up * EyeHeight;
            float range = MWConfig.Mass.TargetRange.Value;
            float weight = MWConfig.Targets.PlayerWeight.Value;
            bool interior = Character.InInterior(pos);

            Player best = null;
            float bestScore = float.MaxValue;
            var players = Player.GetAllPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                Player p = players[i];
                if (!Targetable(p) || IsExcluded(p)) continue;

                float d = Vector3.Distance(pos, p.transform.position);
                bool heard = WormMath.CanHear(d, m_hearRange, interior, p.GetNoiseRange());
                bool seen = !heard && d <= range && !Physics.Linecast(eye, p.GetCenterPoint(), WormPhysics.ViewBlockMask, QueryTriggerInteraction.Ignore);
                if (!heard && !seen) continue;
                if (p != _target && d > range) continue;

                float score = d * weight;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }

            if (best != null)
            {
                _targetSeen = now;
                SetTarget(best);
                return;
            }
            SenseNote = SenseSummary(pos, eye, range, interior);

            // Nobody perceived: keep the last target for a while (it may be behind a corner), then forget it.
            if (_target != null && (!Targetable(_target) || now - _targetSeen > ForgetTime || Vector3.Distance(pos, _target.transform.position) > 2f * range))
                SetTarget(null);
        }

        private string SenseSummary(Vector3 pos, Vector3 eye, float range, bool interior)
        {
            var players = Player.GetAllPlayers();
            if (players.Count == 0) return "no player";
            Player p = players[0];
            float d = Vector3.Distance(pos, p.transform.position);
            return $"{players.Count} player(s), first at {d:0.0} m: targetable={Targetable(p)}, hear range {m_hearRange}, noise {p.GetNoiseRange():0.0}, interior={interior}, " +
                   $"view range {range}, line blocked={Physics.Linecast(eye, p.GetCenterPoint(), WormPhysics.ViewBlockMask, QueryTriggerInteraction.Ignore)}, attack players={MWConfig.Targets.AttackPlayers.Value}";
        }

        private void SetTarget(Player p)
        {
            if (p == _target) return;
            if (_target != null) { _home = _target.transform.position; _homeSet = true; }   // the last point of interest (spec §4.6)
            _target = p;
            _state.SetTarget(p != null ? p.GetZDOID() : ZDOID.None);
        }

        /// <summary>Alive, visible to monsters and not mid-teleport (same rule as the free worms, spec §5.3).</summary>
        private static bool Targetable(Player p) => p != null && !p.IsDead() && !p.InGhostMode() && !p.IsDebugFlying() && !p.IsTeleporting();

        /// <summary>Targets.ExcludedPrefabs (SkyWhale, spec §10.4): never targeted. A player prefab is excluded only if the list names it.</summary>
        private bool IsExcluded(Character c)
        {
            string raw = MWConfig.Targets.ExcludedPrefabs.Value ?? string.Empty;
            if (!string.Equals(raw, _excludedRaw, StringComparison.Ordinal))
            {
                _excludedRaw = raw;
                _excluded = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < _excluded.Length; i++) _excluded[i] = _excluded[i].Trim();
            }
            if (_excluded.Length == 0) return false;
            string name = Utils.GetPrefabName(c.gameObject);
            if (string.IsNullOrEmpty(name)) name = PlayerPrefab;
            for (int i = 0; i < _excluded.Length; i++)
                if (string.Equals(_excluded[i], name, StringComparison.Ordinal)) return true;
            return false;
        }

        // ------------------------------------------------------------------ movement

        private void Hunt(float dt)
        {
            _state.SetMode(MassStateCodes.Hunt);
            SetSpeedFactor(1f);

            Vector3 goal = DevGoal ?? _target.transform.position;
            float stop = DevGoal.HasValue ? DevGoalStop : MassMath.Radius(Mathf.Max(1, _state.N)) + MWConfig.Mass.EngulfRange.Value;

            bool done = MoveTo(dt, goal, stop, false);
            if (!done)
            {
                MoveTicks++;
                PathTicks++;
                StepAssist(true);
                return;
            }

            // MoveTo also answers "done" when no path exists. If the target is not reached, push straight at it (corridors of a mine are open
            // enough for that) and count the tick, so the path rate shows in the logs.
            float flat = Utils.DistanceXZ(goal, transform.position);
            if (flat > stop + 0.3f && !FoundPath())
            {
                MoveTicks++;
                NoPathTicks++;
                MoveAndAvoid(dt, goal, stop, false);
                StepAssist(true);
                return;
            }
            // Arrived by the horizontal distance of MoveTo, but the goal is clearly higher (the last riser of a staircase, a player on a ledge): the body
            // is still trying to climb, so a stall here is a stall to assist.
            bool climbing = goal.y - transform.position.y > 0.35f && flat < stop + 1.5f;
            StepAssist(climbing);
        }

        /// <summary>See the constants above. <paramref name="wantsToMove"/> is false when the mass is where it wanted to be (no stall to detect).</summary>
        private void StepAssist(bool wantsToMove)
        {
            float now = Time.time;
            if (!wantsToMove)
            {
                _stalledWindows = 0;
                _windowStart = now;
                _windowPos = transform.position;
                return;
            }
            if (now - _windowStart < AssistWindow) return;

            Vector3 pos = transform.position;
            float wanted = m_character.m_walkSpeed * (now - _windowStart);
            bool stalled = Utils.DistanceXZ(pos, _windowPos) < AssistStalledFraction * wanted;
            _windowStart = now;
            _windowPos = pos;
            _stalledWindows = stalled ? _stalledWindows + 1 : 0;
            if (_stalledWindows < AssistStalledWindows) return;
            _stalledWindows = 0;
            StalledChecks++;

            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (_capsule == null) _capsule = GetComponent<CapsuleCollider>();
            if (_rb == null || _capsule == null) return;

            // Direction of the wanted move (the body may face another way while it turns): toward the target if any, else its forward.
            Vector3 fwd = DevGoal.HasValue ? DevGoal.Value - pos : _target != null ? _target.transform.position - pos : transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) return;
            fwd.Normalize();
            float radius = _capsule.radius;
            int mask = WormPhysics.SolidMask;

            // A staircase has no gap above the riser: instead of looking for free air, look for a floor just ahead that is higher than the feet
            // by at most a step, with room for the body above it.
            // Try the body itself: for each lift (smallest first), would a sphere of the body's size, moved a little toward the goal and raised, be free
            // of geometry and have a floor right under it? No analysis of the surface ahead (a ray cast from above can start inside a slab or miss
            // an overhanging edge); the position is validated the way the body will use it.
            Vector3 target = default;
            bool found = false;
            float rise = 0f;
            int noRoom = 0, noFloor = 0, tried = 0;
            string blockers = null;
            for (int n = 0; n < AssistNudges.Length && !found; n++)
            {
                for (int k = 0; k < AssistLifts.Length && !found; k++)
                {
                    tried++;
                    float lift = AssistLifts[k];
                    Vector3 centre = pos + fwd * AssistNudges[n] + Vector3.up * (lift + radius);
                    if (Physics.CheckSphere(centre, radius * 0.95f, mask, QueryTriggerInteraction.Ignore))
                    {
                        noRoom++;
                        if (blockers == null)
                        {
                            Collider[] over = Physics.OverlapSphere(centre, radius * 0.95f, mask, QueryTriggerInteraction.Ignore);
                            blockers = over.Length > 0 ? $"{over[0].name}/{LayerMask.LayerToName(over[0].gameObject.layer)} x{over.Length}" : "?";
                        }
                        continue;
                    }
                    if (!Physics.Raycast(centre, Vector3.down, out RaycastHit h, radius + 0.35f, mask, QueryTriggerInteraction.Ignore) || h.normal.y < 0.5f) { noFloor++; continue; }
                    rise = h.point.y - pos.y;
                    if (rise < 0.05f) { noFloor++; continue; }
                    target = new Vector3(pos.x + fwd.x * AssistNudges[n], h.point.y + 0.05f, pos.z + fwd.z * AssistNudges[n]);
                    found = true;
                }
            }
            if (!found)
            {
                // Geometry around the stall, for the log: headroom above the body, what is straight ahead at three heights, the floor ahead.
                string around = string.Empty;
                if (Physics.Raycast(pos + Vector3.up * (2f * radius), Vector3.up, out RaycastHit ceiling, 4f, mask, QueryTriggerInteraction.Ignore)) around += $" headroom above the body top {ceiling.distance:0.00} ({ceiling.collider.name});";
                else around += " no ceiling within 4 m;";
                for (int hgt = 0; hgt < 3; hgt++)
                {
                    float y = 0.1f + hgt * 0.4f;
                    around += Physics.Raycast(pos + Vector3.up * y, fwd, out RaycastHit wall, 2f, mask, QueryTriggerInteraction.Ignore) ? $" wall at +{y:0.0} m: {wall.distance:0.00} m ({wall.collider.name}/{LayerMask.LayerToName(wall.collider.gameObject.layer)});" : $" nothing ahead at +{y:0.0};";
                }
                Vector3 up3 = pos + fwd * 0.8f + Vector3.up * 3f;
                RaycastHit[] downs = Physics.RaycastAll(up3, Vector3.down, 5f, mask, QueryTriggerInteraction.Ignore);
                foreach (RaycastHit d in downs) around += $" down@0.8 ahead: {d.collider.name} at {d.point.y - pos.y:0.00} (n.y {d.normal.y:0.00});";
                LastAssistNote = $"nothing fits: {noRoom}/{tried} blocked (first {blockers}), {noFloor}/{tried} without a floor under;{around}";

                // The staircases of the mines are steeper (about 50 degrees, risers of 0.4 m on treads of 0.3 m) than a sphere of 0.6 m can walk: it
                // rests on the noses. The body is then hopped one stride toward the goal onto the floor found there.
                Vector3 goalPoint = DevGoal ?? (_target != null ? _target.transform.position : pos);
                Vector3 toGoal = goalPoint - pos;
                float distance = toGoal.magnitude;
                if (distance < 0.3f) return;
                // The goal itself when it is within reach, higher, and the body fits there (a goal is a point on a floor): otherwise one stride toward it.
                float gainToGoal = goalPoint.y - pos.y;
                if (Utils.DistanceXZ(goalPoint, pos) <= AssistGoalReach && gainToGoal >= 0.1f && gainToGoal <= AssistHopProbe
                    && !Physics.CheckSphere(goalPoint + Vector3.up * (radius + 0.1f), radius * 0.35f, mask, QueryTriggerInteraction.Ignore))   // small probe: the body pushes itself clear of the walls
                {
                    _rb.position = goalPoint + Vector3.up * 0.1f;
                    StepAssists++;
                    LastAssistNote = $"hopped {gainToGoal:0.00} m up onto the goal";
                    return;
                }
                if (Utils.DistanceXZ(goalPoint, pos) <= AssistGoalReach && gainToGoal >= 0.1f && gainToGoal <= AssistHopProbe) LastAssistNote += " goal hop: no room even for a small probe;";
                Vector3 hop = pos + toGoal / distance * Mathf.Min(distance, AssistHopStride);
                if (!Physics.Raycast(hop + Vector3.up * AssistHopProbe, Vector3.down, out RaycastHit floorHit, 2f * AssistHopProbe, mask, QueryTriggerInteraction.Ignore)) { LastAssistNote += " hop: no floor"; return; }
                float gain = floorHit.point.y - pos.y;
                if (floorHit.normal.y < 0.5f || gain < 0.1f || gain > AssistHopProbe) { LastAssistNote += $" hop: floor not usable (gain {gain:0.00}, n.y {floorHit.normal.y:0.00})"; return; }
                if (Physics.CheckSphere(floorHit.point + Vector3.up * (radius + 0.05f), radius * 0.7f, mask, QueryTriggerInteraction.Ignore)) { LastAssistNote += " hop: no room"; return; }
                _rb.position = floorHit.point + Vector3.up * 0.1f;
                StepAssists++;
                LastAssistNote = $"hopped {gain:0.00} m up toward the goal";
                return;
            }

            _rb.position = target;
            StepAssists++;
            LastAssistNote = $"lifted {rise + 0.05f:0.00} m";
        }

        private void Idle(float dt)
        {
            _state.SetMode(MassStateCodes.Idle);
            SetSpeedFactor(IdleSpeedFactor);
            if (!_homeSet)
            {
                _home = transform.position;
                _homeSet = true;
            }
            RandomMovement(dt, _home);
        }

        private void SetSpeedFactor(float factor)
        {
            if (Mathf.Approximately(factor, _speedFactor)) return;
            _speedFactor = factor;
            float speed = MWConfig.Mass.MoveSpeed.Value * factor;
            m_character.m_speed = speed;
            m_character.m_walkSpeed = speed;
            m_character.m_runSpeed = speed;
        }

        // ------------------------------------------------------------------ stream (spec §4.7)

        /// <summary>
        /// Measures the free width of the passage ahead with side raycasts at chest height, every <see cref="ProbeStep"/> metres, up to
        /// 2 m (entering) or 3 m (leaving) ahead, and picks the form. A wall right ahead ends the measurement there.
        /// </summary>
        private void ProbeStream()
        {
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) return;
            fwd.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, fwd);

            int n = Mathf.Max(1, _state.N);
            float radius = MassMath.Radius(n);
            bool streaming = _state.IsStream;
            float reach = Mathf.Min(ProbeSideCap, radius + ProbeSideMargin);
            float lastDistance = streaming ? MassStream.FreeDistance : ProbeDistance;
            int mask = WormPhysics.SolidMask;

            Vector3 origin = transform.position + Vector3.up * ProbeHeight;
            float near = float.MaxValue, far = float.MaxValue;
            for (float d = 0f; d <= lastDistance + 1e-3f; d += ProbeStep)
            {
                Vector3 p = origin + fwd * d;
                if (d > 0f && Physics.Raycast(p - fwd * ProbeStep, fwd, ProbeStep, mask, QueryTriggerInteraction.Ignore)) break;   // dead end: nothing further to cross

                float right = Physics.Raycast(p, side, out RaycastHit hr, reach, mask, QueryTriggerInteraction.Ignore) ? hr.distance : reach;
                float left = Physics.Raycast(p, -side, out RaycastHit hl, reach, mask, QueryTriggerInteraction.Ignore) ? hl.distance : reach;
                float width = left + right;
                if (d <= ProbeNearEnd + 1e-3f) { if (width < near) near = width; }
                else if (width < far) far = width;
            }
            if (near == float.MaxValue) return;
            if (far == float.MaxValue) far = near;

            bool want = MassStream.NextStream(streaming, near, far, radius);
            if (!want)
            {
                if (streaming) _state.SetStream(false, 0f);
                return;
            }
            float ribbon = Mathf.Min(MassStream.RibbonWidth(Mathf.Min(near, far)), 2f * radius);
            _state.SetStream(true, ribbon);
        }

        // ------------------------------------------------------------------ cost

        private void RecordCost(long start)
        {
            float ms = (float)((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
            _costTicks++;
            _costSumMs += ms;
            if (ms > _costMaxMs) _costMaxMs = ms;

            float now = Time.time;
            if (now >= _nextCostLog)
            {
                if (_nextCostLog > 0f)
                    Plugin.Log.LogDebug($"[Mass] AI cost of {m_nview.GetZDO().m_uid}: avg {CostAvgMs:0.000} ms, max {_costMaxMs:0.000} ms over {_costTicks} ticks; " +
                                        $"walking ticks {MoveTicks} (path {PathTicks}, no path {NoPathTicks}), n={_state.N}, stream={_state.IsStream}");
                _nextCostLog = now + CostLogPeriod;
            }
        }
    }
}
