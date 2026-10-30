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
    /// starts vanilla attacks; this one only picks a target (a player, a tamed animal, a creature or a structure, plan 6-2), walks to it with the vanilla
    /// pathfinding and engulfs it at contact (phase 5).
    /// The prefab needs some BaseAI because <c>EnemyHud</c> calls <c>GetBaseAI()</c> without a null test.
    /// <list type="bullet">
    /// <item>Hunt: toward the target, stopping at r(n) + Mass.EngulfRange. Idle: wanders within 6 m of the last point of interest at half speed.</item>
    /// <item>Engulf (phase 5): within r(n) + Mass.EngulfRange of the target the mass walks onto it (2.4 m/s, no teleport), applies the Infested effect, and
    /// every Worm.BiteInterval sends one aggregated bite tick (<see cref="MassEngulf"/>); it lets go when the target is invalid or stays beyond
    /// r + Mass.DetachDistance for Mass.DetachDelay, or when the mass dies or dissolves.</item>
    /// <item>Dormant (phase 7-2): its group went back to sleep; the mass walks to the nest of its group, targets nothing, and lays its worms down asleep around it
    /// (spec §5.2). A wake up of the group sends it back to Idle.</item>
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
        private const float HuntStopMargin = 0.25f;
        private const float ObstacleCastRadius = 1.2f;   // the sweep toward a living target (decision 166): the radius of the mass, capped
        private const float ProbeHeight = 0.6f;          // height of the width probes: the chest of a passage
        private const float ProbeSideMargin = 1.5f;      // probes reach r + this on each side
        private const float ProbeSideCap = 3.5f;
        private const float ProbeStep = 0.5f;
        private const float ProbeNearEnd = ProbeDistance;
        private const float ProbeDistance = MassStream.ProbeDistance;
        private const float CostLogPeriod = 10f;
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
        private Player _target;                   // the target when it is a player
        private readonly TargetScanner _scanner = new TargetScanner();
        private readonly TargetBlacklist _blacklist = new TargetBlacklist(1);   // targets set aside after a stall (decision 163)
        private ProgressWatch _watch;

        // The target, whatever its kind (plan 6-2): a player (also _target), a creature or tamed animal (_tChar), or a structure (_tObj + its closest collider).
        private TargetKind _tKind;
        private Character _tChar;
        private Component _tObj;
        private Collider _tCol;
        private ZDOID _tId;
        private bool _obstacle;                   // the structure target stands in the way of a living target (decision 164): kept until destroyed or out of reach

        // Engulfing (owner only, plan 5, A1 to A5, A13, A15).
        private bool _engulfing;
        private Character _engulfed;              // the player or creature the Infested / Engulfed effect was sent for
        private BiteTicker _ticker;
        private DetachTracker _detach;
        private float _nextInfest;
        private Vector3 _biteDir;
        private bool _inTick, _tickParried;
        private float _nextFireCheck, _fleeUntil;
        private float _fearUntil;                 // a mass that fled or keeps its distance from a fire is not stalled: no progress watch, no obstacle search until then
        private Vector3 _fleeFrom;
        private float _targetSeen;
        private float _livingSeen = -1000f;       // last time a player or a creature was perceived (a structure is a fallback only after ForgetTime)
        private float _nextTarget;
        private float _nextProbe;
        private Vector3 _home;
        private bool _homeSet;
        private float _speedFactor = -1f;
        private bool _failed;

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
#if MW_DEV
        public string LastAssistNote { get; private set; }
#endif
        // Conditional: in Release the calls vanish, with their interpolated arguments; the bodies exist only for the compiler.
        [Conditional("MW_DEV")]
        private void AssistNote(string note)
        {
#if MW_DEV
            LastAssistNote = note;
#endif
        }

        [Conditional("MW_DEV")]
        private void AssistNoteAdd(string note)
        {
#if MW_DEV
            LastAssistNote += note;
#endif
        }
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

        /// <summary>The kind, the ZDOID and the object of the current target, whatever it is (dev checks, S12, S13).</summary>
        public TargetKind CurrentKind => _tKind;
        public ZDOID CurrentTargetId => _tId;
        public Component TargetObject => _tKind == TargetKind.Structure ? _tObj : _tChar;

        /// <summary>Dev: walk to this point (stopping 0.4 m from it) instead of toward a target. Used by the stairs test.</summary>
        public Vector3? DevGoal { get; set; }
#if MW_DEV
        /// <summary>Dev: with a <see cref="DevGoal"/>, walk straight at it, with neither the vanilla path nor MoveAndAvoid (the low stone of V12 tests the GPU simulation, not the vanilla walk). The step assist stays.</summary>
        public bool DevStraight { get; set; }
#endif

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
            if (_state.AiFrozen || _state.Dissolving || m_character.IsDead())
            {
                if (_engulfing) EndEngulf(DetachReason.Dissolution, now);
                StopMoving();
                return;
            }

            if (now >= _nextDormancy)
            {
                _nextDormancy = now + DormancyPeriod;
                EvaluateDormancy(now);
                if (_state.Dissolving) { StopMoving(); return; }
            }
            if (MassStateCodes.Mode(_state.StateCode) == MassStateCodes.Dormant)
            {
                StepDormant(dt, now);
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

            // Going to join a bigger mass of the colony comes before the hunt while it lasts (at most MergeSeekMaxSeconds).
            CheckFire(now);
            if (now < _fleeUntil)
            {
                if (_engulfing) EndEngulf(DetachReason.Fire, now);
                Flee(dt);
                return;
            }

            bool seeking = _state.TryGetSeekGoal(out Vector3 seekGoal);
            if (HasTarget) WatchProgress(now, now < _fearUntil || _engulfing || seeking || DevGoal.HasValue || _state.Merging || MassStateCodes.IsTransferring(_state.StateCode));
            if (!_engulfing && !seeking && !DevGoal.HasValue && HasTarget) TryBeginEngulf(now);
            if (_engulfing && StepEngulf(dt, now)) return;
            // A smaller mass of the colony is coming to join this one (at most MergeSeekMaxSeconds): it waits, otherwise a hunt at the same speed keeps the two apart (V09: 7 m after 6 s).
            if (!seeking && !_engulfing && _state.IsSoughtBySmaller) { StopMoving(); return; }
            if (seeking) Hunt(dt, seekGoal);
            else if (HasTarget || DevGoal.HasValue) Hunt(dt);
            else Idle(dt);
        }

        // ------------------------------------------------------------------ dormancy (spec §5.1, §5.2, plan 7-2)

        private const float DormancyPeriod = 1f;
        private const float DormantArrive = 1.5f;        // the mass is home within this of the nest
        private const float DormantPatience = 30f;       // and lays its worms down where it is after this long
        private const float DormantDepositReach = 3f;    // ...around the nest when it is within this of it, else around itself
        private const float DormantStop = 1.0f;

        private float _nextDormancy;
        private float _dormantSince;
        private int _dormantGroup = -1;
        private float _calmLastActive = -1f;

        /// <summary>Times this mass went back to sleep (dev checks).</summary>
        public int DormantRounds { get; private set; }

        /// <summary>
        /// Every second: is the group of this mass asleep? The group is the nest most of its worms come from, and its state is a bit of the colony (any peer reads
        /// it). A mass that belongs to a group that went to sleep, and has no player to hunt, goes home (mode Dormant, a stub of 4B turned into the walk to the nest);
        /// a group that wakes up sends it back to Idle. A mass with no nest has no bit: it keeps its own calm timer (no player within Targets.ActiveRadius of it for
        /// Targets.CalmDelay) and lays its worms down where it stands. A mass that engulfs, merges or is steered by a dev tool does not go to sleep.
        /// </summary>
        private void EvaluateDormancy(float now)
        {
            if (_calmLastActive < 0f) _calmLastActive = now;
            byte mode = MassStateCodes.Mode(_state.StateCode);
            if (_engulfing || DevGoal.HasValue || _state.Merging || MassStateCodes.IsTransferring(_state.StateCode))
            {
                _calmLastActive = now;
                if (mode == MassStateCodes.Dormant) _state.SetMode(MassStateCodes.Idle);
                return;
            }

            WormColony colony = ColonyRegistry.FindByKey(_state.ColonyKey);
            if (colony == null)
            {
                if (mode == MassStateCodes.Dormant) _state.SetMode(MassStateCodes.Idle);   // nobody to lay the worms down: it stays a mass
                return;
            }

            int group = _state.MajorityNest();
            bool asleep;
            if (group >= 0)
            {
                asleep = !colony.IsGroupAwake(group);
                if (asleep && mode != MassStateCodes.Dormant && _tKind == TargetKind.Player && TargetValid()) asleep = false;   // it hunts a player: the bit is not its business
            }
            else
            {
                if (AnyPlayerWithin(MWConfig.Targets.ActiveRadius.Value)) _calmLastActive = now;
                asleep = WakeMath.CalmElapsed(now, _calmLastActive, MWConfig.Targets.CalmDelay.Value);
            }

            if (!asleep)
            {
                if (mode == MassStateCodes.Dormant)
                {
                    _state.SetMode(MassStateCodes.Idle);      // a wake up during the walk home brings the mass back to Idle
                    _nextTarget = 0f;
                }
                return;
            }

            if (mode == MassStateCodes.Dormant) return;
            if (group < 0)
            {
                BeginSleep(transform.position, now);
                return;
            }
            _dormantGroup = group;
            _dormantSince = now;
            SetTarget((Player)null);
            _state.SetMode(MassStateCodes.Dormant);
        }

        private bool AnyPlayerWithin(float radius)
        {
            var all = Player.GetAllPlayers();
            float r2 = radius * radius;
            Vector3 pos = transform.position;
            for (int i = 0; i < all.Count; i++)
            {
                Player p = all[i];
                if (Targetable(p) && (p.transform.position - pos).sqrMagnitude <= r2) return true;
            }
            return false;
        }

        /// <summary>Dormant: walks to the nest of its group, then lays its worms down around it (arrival within 1.5 m, 30 s, or the nest gone).</summary>
        private void StepDormant(float dt, float now)
        {
            WormColony colony = ColonyRegistry.FindByKey(_state.ColonyKey);
            WormNest nest = colony != null ? colony.NestAt(_dormantGroup) : null;
            Vector3 pos = transform.position;
            if (nest == null || !nest.IsValid)
            {
                BeginSleep(pos, now);
                return;
            }

            Vector3 goal = nest.transform.position;
            float flat = Utils.DistanceXZ(goal, pos);
            if (flat <= DormantArrive || now - _dormantSince > DormantPatience)
            {
                BeginSleep(flat <= DormantDepositReach ? goal : pos, now);
                return;
            }
            SetSpeedFactor(1f);
            WalkTo(dt, goal, DormantStop);
        }

        private void BeginSleep(Vector3 deposit, float now)
        {
            DormantRounds++;
            if (_engulfing) EndEngulf(DetachReason.Dissolution, now);
            StopMoving();
            Plugin.DevInfo($"[Mass] {_state.ZdoId} (n={_state.N}) goes back to sleep around {deposit}");
            _state.BeginDormantDissolve(deposit);
        }

        // ------------------------------------------------------------------ target (spec §5.3, plan 6-2)

        private bool HasTarget => _tKind != TargetKind.None;

        /// <summary>The target still exists and may be attacked: a player alive and visible, a creature alive, a structure not destroyed.</summary>
        private bool TargetValid()
        {
            switch (_tKind)
            {
                case TargetKind.Player: return Targetable(_target);
                case TargetKind.Tamed:
                case TargetKind.Creature: return _tChar != null && !_tChar.IsDead();
                case TargetKind.Structure: return _tObj != null && _tCol != null;
                default: return false;
            }
        }

        /// <summary>Where the mass walks to: a character's position, the point of a structure's collider closest to the mass.</summary>
        private Vector3 TargetPos()
        {
            if (_tKind == TargetKind.Structure) return _tCol != null ? TargetScanner.ClosestOn(_tCol, transform.position) : transform.position;
            return _tChar != null ? _tChar.transform.position : transform.position;
        }

        /// <summary>
        /// Progress watch (decisions 163, 164): the distance to the target must shrink by 0.5 m in 4 s while the mass walks to it (not engulfing, not seeking a
        /// merge). When it does not, a structure within engulf reach in the half space toward a living target becomes the target (the mass gnaws the door the
        /// player hides behind); else a creature or a structure is set aside for 10 s and dropped, and a new choice follows at once. A player is never set aside.
        /// </summary>
        private void WatchProgress(float now, bool exempt)
        {
            if (_tKind == TargetKind.None || _tId == ZDOID.None) return;
            long key = TargetReach.Key(_tId.UserID, _tId.ID);
            float distance = TargetValid() ? Vector3.Distance(transform.position, TargetPos()) : 0f;
            if (!_watch.Update(key, distance, now, exempt)) return;
            if (TargetRules.AfraidOfFire(now, _fearUntil)) return;   // never taken for a stall while the fire holds the mass off

            if (_tKind != TargetKind.Structure && TakeObstacle(now)) return;
            if (_tKind == TargetKind.Player) return;
            _blacklist.Add(0, key, now);
#if MW_DEV
            Plugin.DevInfo($"[Target] mass {_state.ZdoId} gives up {_tKind} (no progress, distance {distance:0.00} m)");
#endif
            Plugin.DevDebug($"[Target] mass {_state.ZdoId} gives up {_tKind} {(TargetObject != null ? TargetObject.name : "?")}: no progress for {ProgressWatch.Window:0} s, set aside {TargetBlacklist.DefaultSeconds:0} s");
            SetTarget((Player)null);
            _nextTarget = 0f;
        }

        private float ObstacleReach() => MassMath.Radius(Mathf.Max(1, _state.N)) + MWConfig.Mass.EngulfRange.Value + 0.5f;

        private bool TakeObstacle(float now)
        {
            if (!TargetValid()) return false;
            Vector3 pos = transform.position;
            float radius = Mathf.Min(MassMath.Radius(Mathf.Max(1, _state.N)), ObstacleCastRadius);
            Vector3 center = pos + Vector3.up * radius;
            Vector3 goal = _tChar != null ? _tChar.GetCenterPoint() : TargetPos();
            if (!_scanner.PickObstacle(center, radius, pos.y, goal - center, ObstacleReach(), now, EngulfMath.MaxHeightGap, _blacklist, 0, out TargetChoice pick)) return false;
#if MW_DEV
            Plugin.DevInfo($"[Target] mass {_state.ZdoId} takes {(pick.Object != null ? pick.Object.name : "?")} as an obstacle on the way to its {_tKind} target");
#endif
            Plugin.DevDebug($"[Target] mass {_state.ZdoId} takes {(pick.Object != null ? pick.Object.name : "?")} as an obstacle on the way to its {_tKind} target");
            SetTarget(pick);
            _obstacle = true;
            _targetSeen = now;
            return true;
        }

        private void ChooseTarget(float now)
        {
            // An engulfing mass keeps its target: StepEngulf decides when to let go, then a new choice is made.
            if (_engulfing && HasTarget)
            {
                _targetSeen = now;
                if (_tKind != TargetKind.Structure) _livingSeen = now;
                return;
            }

            bool players = MWConfig.Targets.AttackPlayers.Value;
            bool objects = MWConfig.Targets.AttackTamed.Value || MWConfig.Targets.AttackCreatures.Value || MWConfig.Targets.AttackStructures.Value;
            if (!players && !objects && DevForcedTarget == null)
            {
                SetTarget((Player)null);
                return;
            }

            Player forced = DevForcedTarget;
            if (forced != null && Targetable(forced))
            {
                _targetSeen = now;
                SetTarget(forced);
                return;
            }

            // The structure that blocks the way to a living target is gnawed until it falls or is out of reach, then the hunt goes on (decision 164).
            if (_obstacle)
            {
                if (_tKind == TargetKind.Structure && TargetValid() && Vector3.Distance(transform.position, TargetPos()) <= ObstacleReach() + 2f)
                {
                    _targetSeen = now;
                    return;
                }
                _obstacle = false;
            }

            Vector3 pos = transform.position;
            Vector3 eye = pos + Vector3.up * EyeHeight;
            float range = MWConfig.Mass.TargetRange.Value;
            float weight = MWConfig.Targets.PlayerWeight.Value;
            bool interior = Character.InInterior(pos);

            Player best = null;
            float bestScore = float.MaxValue;
            var all = Player.GetAllPlayers();
            for (int i = 0; players && i < all.Count; i++)
            {
                Player p = all[i];
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

            // A creature or a tamed animal that beats the best player (3D distance x weight, line of sight, Targets.*); a structure only when nobody else is perceived (decision 164).
            if (objects && _scanner.Pick(pos, eye, range, bestScore, _tKind == TargetKind.Player ? ZDOID.None : _tId, m_character, now,
                    EngulfMath.MaxHeightGap, _blacklist, 0, TargetRules.FallbackDelayElapsed(now, _livingSeen, ForgetTime), out TargetChoice pick))
            {
                _targetSeen = now;
                if (pick.Kind != TargetKind.Structure) _livingSeen = now;
                SetTarget(pick);
                return;
            }

            if (best != null)
            {
                _targetSeen = now;
                _livingSeen = now;
                SetTarget(best);
                return;
            }
            SenseNote = SenseSummary(pos, eye, range, interior);

            // Nobody perceived: keep the last target for a while (it may be behind a corner), then forget it.
            if (HasTarget && (!TargetValid() || now - _targetSeen > ForgetTime || Vector3.Distance(pos, TargetPos()) > 2f * range))
                SetTarget((Player)null);
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

        /// <summary>A player (or null: no target).</summary>
        private void SetTarget(Player p)
        {
            if (p == null)
            {
                if (_tKind != TargetKind.None) Switch(TargetKind.None, null, null, null, ZDOID.None);
                return;
            }
            if (_tKind == TargetKind.Player && p == _target) return;
            Switch(TargetKind.Player, p, null, null, p.GetZDOID());
        }

        /// <summary>A creature, a tamed animal or a structure chosen by the scanner.</summary>
        private void SetTarget(in TargetChoice choice)
        {
            if (_tKind == choice.Kind && _tId == choice.Id) return;
            Switch(choice.Kind, choice.Character, choice.Object, choice.Collider, choice.Id);
        }

        private void Switch(TargetKind kind, Character character, Component obj, Collider collider, ZDOID id)
        {
            if (HasTarget && TargetValid()) { _home = TargetPos(); _homeSet = true; }   // the last point of interest (spec §4.6)
            _obstacle = false;
            _tKind = kind;
            _target = character as Player;
            _tChar = character;
            _tObj = obj != null ? obj : character;
            _tCol = collider;
            _tId = id;
            _state.SetTarget(id);
        }

        /// <summary>Alive, visible to monsters and not mid-teleport (same rule as the free worms, spec §5.3).</summary>
        private static bool Targetable(Player p) => p != null && !p.IsDead() && !p.InGhostMode() && !p.IsDebugFlying() && !p.IsTeleporting();

        /// <summary>Targets.ExcludedPrefabs (SkyWhale, spec §10.4): never targeted. A player prefab is excluded only if the list names it.</summary>
        private static bool IsExcluded(Character c) => TargetScanner.IsExcluded(c);

        // ------------------------------------------------------------------ movement

        private const float PlayerBodyRadius = 0.3f;

        /// <summary>
        /// What a target's body adds to the engulf range: the range r + Mass.EngulfRange was calibrated on the capsule of a player (0.3 m). A creature wider than that keeps the mass
        /// farther from its centre (the two bodies collide), by its own radius minus the player's: with r(40) = 0.43 m a Seeker could not be reached any more.
        /// </summary>
        /// <summary>
        /// The radius the engulf range counts from: r(n), but never less than what the bodies allow. The physical capsule of a mass is bounded at Mass.MaxColliderRadius whatever r(n) is
        /// (0.5 m), and the capsule of a player is 0.3 m: the centres of the two cannot come closer than 0.8 m, so a small mass (r(40) = 0.43) whose range was r + 0.6 = 1.03 m stood at
        /// 0.99 m and could never engulf. With this floor the range is at least 1.4 m for a player.
        /// </summary>
        private float EngulfRadius(float r)
        {
            if (_capsule == null) _capsule = GetComponent<CapsuleCollider>();
            float body = _capsule != null ? _capsule.radius : MWConfig.Mass.MaxColliderRadius.Value;
            return Mathf.Max(r, body + PlayerBodyRadius);
        }

        private float TargetBodyExtra()
        {
            if (_tKind != TargetKind.Creature || _tChar == null) return 0f;
            Collider c = _tChar.GetComponent<Collider>();
            if (c == null) return 0f;
            Vector3 e = c.bounds.extents;
            return Mathf.Max(0f, Mathf.Max(e.x, e.z) - PlayerBodyRadius);
        }

        private void Hunt(float dt, Vector3? seek = null)
        {
            _state.SetMode(MassStateCodes.Hunt);
            SetSpeedFactor(1f);

            Vector3 goal = seek ?? DevGoal ?? TargetPos();
            // A little inside the engulf range: the walk helper may stop a few tenths short of its stop distance, and the engulfing needs flat <= r + EngulfRange.
            float stop = seek.HasValue || DevGoal.HasValue ? DevGoalStop : Mathf.Max(0.5f, EngulfRadius(MassMath.Radius(Mathf.Max(1, _state.N))) + MWConfig.Mass.EngulfRange.Value + TargetBodyExtra() - HuntStopMargin);

            WalkTo(dt, goal, stop, pushStraight: seek.HasValue);
        }

        /// <summary>Walks toward <paramref name="goal"/> with the vanilla path, or straight at it without a path; stops at <paramref name="stop"/> (horizontal).</summary>
        private void WalkTo(float dt, Vector3 goal, float stop, bool pushStraight = false)
        {
#if MW_DEV
            if (DevStraight)
            {
                float flatS = Utils.DistanceXZ(goal, transform.position);
                if (flatS > stop)
                {
                    MoveTicks++;
                    NoPathTicks++;
                    Vector3 toS = goal - transform.position;
                    toS.y = 0f;
                    MoveTowards(toS.normalized, false);
                }
                StepAssist(true);
                return;
            }
            if (MassPathProbe.Armed) MassPathProbe.Tick(this, dt, goal, m_moveMinAngle, _state.StateCode);
#endif
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
            // A mass going to join another one (pushStraight) ends its path 2 m short of it (the other mass stands on the navmesh): the last metres are walked straight, as without a path.
            if (flat > stop + 0.3f && (pushStraight || !FoundPath()))
            {
                MoveTicks++;
                NoPathTicks++;
                if (pushStraight)
                {
                    // MoveAndAvoid refuses a step when something solid is within radius + 1 m ahead, and the other mass is: the two stop 2 m apart, short of the 1.3 m contact. Straight at it.
                    Vector3 toGoal = goal - transform.position;
                    toGoal.y = 0f;
                    MoveTowards(toGoal.normalized, false);
                }
                else MoveAndAvoid(dt, goal, stop, false);
                StepAssist(true);
                return;
            }
            // Arrived by the horizontal distance of MoveTo, but the goal is clearly higher (the last riser of a staircase, a player on a ledge): the body
            // is still trying to climb, so a stall here is a stall to assist.
            bool climbing = goal.y - transform.position.y > 0.35f && flat < stop + 1.5f;
            StepAssist(climbing);
        }

        // ------------------------------------------------------------------ engulfing (plan 5, A1 to A5, A13, A15)

        /// <summary>True while this mass engulfs a player (owner side).</summary>
        public bool Engulfing => _engulfing;

        /// <summary>Time of the next bite tick (dev checks), and the horizontal direction mass to target that decides the block angle.</summary>
        public float NextTickTime => _ticker.Next;
        public Vector3 BiteDir => _biteDir;

        /// <summary>Dev counters: engulfings begun, and why the last one ended.</summary>
        public int EngulfCount { get; private set; }
        public DetachReason LastDetach { get; private set; }

        private void TryBeginEngulf(float now)
        {
            if (!HasTarget || !TargetValid() || _state.Merging || MassStateCodes.IsTransferring(_state.StateCode)) return;
            if (_tKind == TargetKind.Player && !MWConfig.Targets.AttackPlayers.Value && DevForcedTarget == null) return;
            Vector3 pos = transform.position;
            Vector3 tp = TargetPos();
            // A structure is engulfed only through a real surface point (a concave mesh may show none from here).
            if (_tKind == TargetKind.Structure && !TargetScanner.TryClosestOn(_tCol, pos, out tp))
            {
                NoteRefusal(now, "no real surface point of the structure seen from the mass", pos, tp);
                return;
            }
            float flat = Utils.DistanceXZ(tp, pos);
            float r = MassMath.Radius(Mathf.Max(1, _state.N));
            if (Mathf.Abs(tp.y - pos.y) > EngulfMath.MaxHeightGap)
            {
                NoteRefusal(now, $"height gap {Mathf.Abs(tp.y - pos.y):0.00} m over {EngulfMath.MaxHeightGap:0.0}", pos, tp);
                return;
            }
            if (!EngulfMath.InEngulfRange(flat, EngulfRadius(r), MWConfig.Mass.EngulfRange.Value + TargetBodyExtra()))
            {
                NoteRefusal(now, $"out of range: flat {flat:0.00} m over r {r:0.00} + {MWConfig.Mass.EngulfRange.Value:0.00} + body {TargetBodyExtra():0.00} = {EngulfRadius(r) + MWConfig.Mass.EngulfRange.Value + TargetBodyExtra():0.00}", pos, tp);
                return;
            }

            _engulfing = true;
            _engulfed = _tKind == TargetKind.Structure ? null : _tChar;
            EngulfCount++;
            _ticker.Start(now, MWConfig.Worm.BiteInterval.Value);
            _detach.Reset();
            Vector3 d = tp - pos;
            d.y = 0f;
            _biteDir = d.sqrMagnitude > 1e-4f ? d.normalized : transform.forward;
            _nextInfest = now;                                  // the effect goes out at once
            _state.SetMode(MassStateCodes.Engulf);
            Plugin.DevDebug($"[Engulf] {_state.ZdoId} (n={_state.N}) engulfs {_tKind} {(TargetObject != null ? TargetObject.name : "?")} at {flat:0.00} m (r {r:0.00})");
        }

        /// <summary>Why the last engulfing was refused while the mass was hunting its target (dev log, S13): reason, distances, vertical gap. Empty in a release build.</summary>
        public string EngulfNote { get; private set; }

        [Conditional("MW_DEV")]
        private void NoteRefusal(float now, string reason, Vector3 pos, Vector3 target)
        {
            if (now < _nextRefusalLog) return;
            _nextRefusalLog = now + 2f;
            EngulfNote = $"engulf refused for {_tKind} {(TargetObject != null ? TargetObject.name : "?")}: {reason}; mass at {pos:0.00}, point {target:0.00}, 3D {Vector3.Distance(pos, target):0.00} m, vertical {target.y - pos.y:0.00} m";
            Plugin.DevInfo($"[Engulf] {_state.ZdoId} {EngulfNote}");
        }

        private float _nextRefusalLog;

        /// <summary>One AI tick of an engulfing. Returns false when the mass let go (the normal hunt or wandering then runs this tick).</summary>
        private bool StepEngulf(float dt, float now)
        {
            bool valid = HasTarget && TargetValid();
            Vector3 pos = transform.position;
            Vector3 tp = valid ? TargetPos() : pos;
            float flat = valid ? Utils.DistanceXZ(tp, pos) : 0f;
            float r = MassMath.Radius(Mathf.Max(1, _state.N));
            bool tooFar = valid && _detach.Update(now, flat, EngulfMath.DetachLimit(r, MWConfig.Mass.DetachDistance.Value), MWConfig.Mass.DetachDelay.Value);
            DetachReason reason = EngulfMath.Decide(valid, _state.Dissolving || m_character.IsDead(), now < _fleeUntil, tooFar);
            if (reason != DetachReason.None)
            {
                EndEngulf(reason, now);
                return false;
            }

            _state.SetMode(MassStateCodes.Engulf);
            SetSpeedFactor(1f);
            float reach = EngulfRadius(r) + MWConfig.Mass.EngulfRange.Value + TargetBodyExtra();
            if (_tKind != TargetKind.Structure) WalkTo(dt, tp, MassEngulf.WalkStop);
            else if (flat > reach) WalkTo(dt, tp, reach);   // a structure is not walked onto: the mass stays against it
            else StopMoving();

            if (flat > 0.4f)
            {
                Vector3 d = tp - pos;
                d.y = 0f;
                _biteDir = d.normalized;
            }

            if (_ticker.Due(now))
            {
                // A perfect parry reports back while the bite is sent (solo: synchronously) or later (another peer owns the player).
                _inTick = true;
                _tickParried = false;
                switch (_tKind)
                {
                    case TargetKind.Player: MassEngulf.SendBite(_state, _target, _biteDir); break;
                    case TargetKind.Structure: MassEngulf.HitStructure(_state, _tObj, tp, _biteDir); break;
                    default: MassEngulf.HitCreature(_state, _tChar, _biteDir); break;
                }
                _inTick = false;
                _ticker.Advance(now, MWConfig.Worm.BiteInterval.Value, _tickParried, EngulfMath.ParryDelay);
            }
            if (_engulfed != null && now >= _nextInfest)
            {
                _nextInfest = now + MassEngulf.InfestRefresh;
                MassEngulf.SendInfest(_engulfed, true);
            }
            return true;
        }

        private void EndEngulf(DetachReason reason, float now)
        {
            _engulfing = false;
            LastDetach = reason;
            _ticker.Stop();
            _detach.Reset();
            Character p = _engulfed;
            _engulfed = null;
            if (p != null) MassEngulf.SendInfest(p, false);
            if (reason == DetachReason.TargetInvalid) _nextTarget = 0f;   // a new target is chosen at once (spec §9.3)
            if (!_state.Dissolving && !m_character.IsDead()) _state.SetMode(MassStateCodes.Hunt);
            Plugin.DevDebug($"[Engulf] {_state.ZdoId} lets go: {reason}");
        }

        /// <summary>The target's owner reports that the tick just sent was parried perfectly: the next one comes ParryDelay later (plan 5, A11).</summary>
        internal void Parried()
        {
            ParriedTicks++;
            if (_inTick) _tickParried = true;
            else if (_engulfing) _ticker.Postpone(EngulfMath.ParryDelay);
        }

        /// <summary>Dev counter: ticks of this mass that were parried.</summary>
        public int ParriedTicks { get; private set; }

        // ------------------------------------------------------------------ fear of fire (spec 9.1, plan 5, A16)

        private const float FireCheckPeriod = 0.25f;
        private const float FearGrace = 4f;           // after the flight: the mass walks back, it must not be taken for a stalled hunt
        private const float FireMargin = 0.6f;       // what the mass walks between two checks: it turns before the fear line

        /// <summary>True while the mass flees a fire (dev checks).</summary>
        public bool Fleeing => Time.time < _fleeUntil;

        /// <summary>Dev: the point the mass flees from (the torch holder or the fire) and whether it was a torch, as the last check saw it.</summary>
        public Vector3 FleeFrom => _fleeFrom;

        /// <summary>Dev: what frightened the mass the last time (kind, path, distance).</summary>
        public string FleeSource { get; private set; }

        /// <summary>Dev: how the last flight step was steered (direction, obstacles).</summary>
        public string FleeNote { get; private set; }

        /// <summary>Times a fire made this mass flee (dev checks).</summary>
        public int FireFlights { get; private set; }

        /// <summary>
        /// Every 0.25 s: a fire area or a lit torch held by a humanoid within Fire.FearRadius of the EDGE of the mass (centre distance minus r(n)), or
        /// the mass burning, starts a flight that lasts Fire.FearDuration after the last time it was frightened.
        /// </summary>
        private void CheckFire(float now)
        {
            if (now < _nextFireCheck) return;
            _nextFireCheck = now + FireCheckPeriod;
            if (!MWConfig.Fire.AfraidOfFire.Value) return;

            Vector3 pos = transform.position;
            float r = MassMath.Radius(Mathf.Max(1, _state.N));
            Vector3 from;
            bool afraid = FireSources.Nearest(pos + Vector3.up * EyeHeight, r, MWConfig.Fire.FearRadius.Value, FireMargin, transform, out from, out _, out string who);
            if (afraid) FleeSource = who;
            if (!afraid && m_character.GetSEMan().HaveStatusEffect(SEMan.s_statusEffectBurning))
            {
                // Burning: no known source, run from the target (or backwards).
                afraid = true;
                from = HasTarget ? TargetPos() : pos - transform.forward;
            }
            if (!afraid) return;
            if (now >= _fleeUntil) FireFlights++;
            _fleeFrom = from;
            _fleeUntil = now + MWConfig.Fire.FearDuration.Value;
            _fearUntil = _fleeUntil + FearGrace;
        }

        private const float FleeReach = 8f;

        /// <summary>Runs away from the fire at Mass.MoveSpeed.</summary>
        private void Flee(float dt)
        {
            _state.SetMode(MassStateCodes.Hunt);
            SetSpeedFactor(1f);
            Vector3 pos = transform.position;
            Vector3 away = FireMath.AwayFrom(_fleeFrom, pos, -transform.forward);
            // Not MoveAndAvoid: when the body is stuck for 1.5 s its "get out of the corner" walks BACKWARDS from the goal for 4 s, which for a flight is toward the fire (S06, a mass
            // that fled to 6.8 m came back to 1.9 m of the torch). Here a heading is taken among those that never lead toward the fire; none free means standing still.
            FleeStep(away);
        }

        private static readonly float[] FleeAngles = { 0f, 30f, -30f, 60f, -60f, 80f, -80f };

        /// <summary>One step of the flight: the first heading, from straight away from the fire then fanning out to 80 degrees, whose path is free of obstacles; stands still when none is.</summary>
        private void FleeStep(Vector3 away)
        {
            float radius = m_character.GetRadius();
            float clear = radius + 1f;
            Vector3 center = m_character.GetCenterPoint();
            Vector3 side = Vector3.Cross(Vector3.up, away) * (radius - 0.1f);
            for (int k = 0; k < FleeAngles.Length; k++)
            {
                Vector3 dir = Quaternion.AngleAxis(FleeAngles[k], Vector3.up) * away;
                if (Raycast(center, dir, clear, 0.1f) < clear) continue;
                if (Raycast(center - side, dir, clear, 0.1f) < clear) continue;
                if (Raycast(center + side, dir, clear, 0.1f) < clear) continue;
#if MW_DEV
                FleeNote = $"heading {FleeAngles[k]:0} deg from straight away {away.ToString("0.00")}";
#endif
                MoveTowards(dir, false);
                return;
            }
#if MW_DEV
            FleeNote = "no free heading away from the fire: standing still";
#endif
            StopMoving();
        }

        /// <summary>Called by the state when the mass is destroyed (BaseAI.OnDestroy is private, so it cannot be overridden): the Infested effect is released at once.</summary>
        internal void ReleaseOnDestroy()
        {
            if (!_engulfing) return;
            _engulfing = false;
            try
            {
                Character p = _engulfed;
                _engulfed = null;
                if (p != null && ZRoutedRpc.instance != null) MassEngulf.SendInfest(p, false);
            }
            catch (Exception e)
            {
                Plugin.LogException("WritheMassAI.ReleaseOnDestroy", e);
            }
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
            // Turning is not stalling (decision 269): while the body faces more than m_moveMinAngle away from where it wants to go, BaseAI.MoveTowards moves it
            // only a little, and a window without progress says nothing about a step.
            Vector3 wantDir = DevGoal.HasValue ? DevGoal.Value - pos : HasTarget ? TargetPos() - pos : transform.forward;
            if (!MassTurn.CountsAsStall(wantDir, transform.forward, m_moveMinAngle))
            {
                _stalledWindows = 0;
                _windowStart = now;
                _windowPos = pos;
                return;
            }
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
            Vector3 fwd = DevGoal.HasValue ? DevGoal.Value - pos : HasTarget ? TargetPos() - pos : transform.forward;
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
#if MW_DEV
            string blockers = null;
#endif
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
#if MW_DEV
                        if (blockers == null)
                        {
                            Collider[] over = Physics.OverlapSphere(centre, radius * 0.95f, mask, QueryTriggerInteraction.Ignore);
                            blockers = over.Length > 0 ? $"{over[0].name}/{LayerMask.LayerToName(over[0].gameObject.layer)} x{over.Length}" : "?";
                        }
#endif
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
#if MW_DEV
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
#endif

                // The staircases of the mines are steeper (about 50 degrees, risers of 0.4 m on treads of 0.3 m) than a sphere of 0.6 m can walk: it
                // rests on the noses. The body is then hopped one stride toward the goal onto the floor found there.
                Vector3 goalPoint = DevGoal ?? (HasTarget ? TargetPos() : pos);
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
                    AssistNote($"hopped {gainToGoal:0.00} m up onto the goal");
                    return;
                }
                if (Utils.DistanceXZ(goalPoint, pos) <= AssistGoalReach && gainToGoal >= 0.1f && gainToGoal <= AssistHopProbe) AssistNoteAdd(" goal hop: no room even for a small probe;");
                Vector3 hop = pos + toGoal / distance * Mathf.Min(distance, AssistHopStride);
                if (!Physics.Raycast(hop + Vector3.up * AssistHopProbe, Vector3.down, out RaycastHit floorHit, 2f * AssistHopProbe, mask, QueryTriggerInteraction.Ignore)) { AssistNoteAdd(" hop: no floor"); return; }
                float gain = floorHit.point.y - pos.y;
                if (floorHit.normal.y < 0.5f || gain < 0.1f || gain > AssistHopProbe) { AssistNoteAdd($" hop: floor not usable (gain {gain:0.00}, n.y {floorHit.normal.y:0.00})"); return; }
                if (Physics.CheckSphere(floorHit.point + Vector3.up * (radius + 0.05f), radius * 0.7f, mask, QueryTriggerInteraction.Ignore)) { AssistNoteAdd(" hop: no room"); return; }
                _rb.position = floorHit.point + Vector3.up * 0.1f;
                StepAssists++;
                AssistNote($"hopped {gain:0.00} m up toward the goal");
                return;
            }

            _rb.position = target;
            StepAssists++;
            AssistNote($"lifted {rise + 0.05f:0.00} m");
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
                    Plugin.DevDebug($"[Mass] AI cost of {m_nview.GetZDO().m_uid}: avg {CostAvgMs:0.000} ms, max {_costMaxMs:0.000} ms over {_costTicks} ticks; " +
                                        $"walking ticks {MoveTicks} (path {PathTicks}, no path {NoPathTicks}), n={_state.N}, stream={_state.IsStream}");
                _nextCostLog = now + CostLogPeriod;
            }
        }
    }
}
