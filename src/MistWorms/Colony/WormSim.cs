using System;
using System.Diagnostics;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>
    /// Simulation of the virtual worms, owner side (spec §3.3, §11.1): scheduling, states and movement.
    /// No allocation in this path: arrays from <see cref="WormStore"/>, single-hit physics queries, struct math only.
    /// </summary>
    internal sealed partial class WormColony
    {
        // Fixed behaviour numbers of the spec that have no config key.
        private const float LeapStepInterval = 0.05f;
        private const float WanderRadius = 6f;             // spec §3.3 Idle
        private const float WanderSpeedFactor = 0.5f;      // decisions.md 34: strolling at half speed
        private const float ArriveDistance = 0.3f;
        private const float WanderGoalMaxTime = 8f;
        private const float FleeGoalDistance = 6f;
        private const float GroundInterval = 0.25f;        // spec §11.1: floor check cadence of a resting worm
        private const float AvoidInterval = 0.25f;
        private const float AvoidLength = 1f;
        private const float AvoidAngle = 35f * Mathf.Deg2Rad;
        private const float MovedEpsilon = 0.001f;         // a step longer than 1 mm counts as moving (idle pose, WormStore.stillSince)
        private const float AvoidWeight = 1.5f;

        // Group behaviour of free worms (spec §3.3, §11.1; plan 4A-3). One grid query per worm and step feeds all of it.
        private const float SeparationRadius = 0.3f;       // between body middles
        private const int SeparationNeighbors = 6;
        private const float SeparationWeight = 1.5f;
        private const float SeparationFirstMin = 0.2f;   // push (a unit is a worm on top of another) above which Recovery ignores its place
        private const float SeparationQueryRadius = SeparationRadius + 2f * WormMath.BodyMidOffset; // head distance that can still mean middles < 0.3 m apart
        private const int NeighborQueryCount = 8;          // the 6 separation neighbours plus 2 for cohesion and alignment
        private const float NeighborMaxDy = 1.5f;
        private const float CohesionRadius = 3f;           // query radius of a wandering worm
        private const float CohesionMinDistance = 0.3f;
        private const float CohesionWeight = 0.2f;
        private const float AlignRadius = 1.5f;            // query radius of a hunting / gathering worm
        private const float AlignWeight = 0.3f;
        private const float FileMinSpeed = 0.2f;           // a stopped leader still lets the follower creep so that it can steer around it
        private const float ApproachInterval = 0.5f;       // the approach point is re-evaluated this often
        private const float ApproachRangeFactor = 0.9f;    // point = target + direction x CobraRange x 0.9
        private const float ApproachReach = 0.3f;          // a hunter strikes only once this close to its place...
        private const float ApproachPatience = 4f;         // ...or after this long in Hunting without getting there (anti-blocking)
        private const float WindupNudgeSpeed = 0.35f;      // m/s of the sideways slide of a worm in its windup
        private const float WindupNudgeMinPush = 0.05f;    // separation push (a unit is a worm on top of another) under which it stays still
        private const float RecoverSpeedFactor = 0.5f;     // a worm recovering from a strike shuffles to its place at this share of the speed (0.3 left Recovery + Recovery pairs overlapping, S18)
        private const int ApproachTries = 8;              // places tried in order before aiming at the target itself
        private const float ApproachMaxDy = 0.6f;          // floor of the point within this of the target's feet
        private const float GridInterval = 0.1f;
        private const float StuckWindow = 3f;              // spec §11.1: no 0.5 m of progress in 3 s
        private const float StuckDistance = 0.5f;
        private const float MaxStepDt = 0.5f;
        private const float EyeHeight = 0.15f;

        private Bounds _boxBounds;
        private float _nextGridTime;
        private readonly int[] _neighborIdx = new int[NeighborQueryCount];
        private readonly float[] _neighborSqr = new float[NeighborQueryCount];
        private bool _warmed;
        private int _simCursor;   // first slot of the next frame's walk: where the last one stopped when it ran out of budget (no starvation)

        /// <summary>
        /// Steps one frame may run (decision 227). A hitch makes every worm overdue at once and, with next = now + interval, they stayed in lockstep for good:
        /// 400 steps in one frame (7 ms). The rest is carried to the next frames, from where the walk stopped. 160 steps cover 400 near worms at 20 Hz from 50 fps up.
        /// </summary>
        internal const int MaxStepsPerFrame = 160;
        private const int MinStepsPerFrame = 8;           // below this the time budget does not stop the walk
        private const double MaxStepBudgetMs = 2.0;       // time of the walk above which the rest is carried (the cost of a strike is not predictable)

        /// <summary>First step of worm <paramref name="i"/> after <c>now</c>: its own phase inside one near interval (creation, restoration, waking).</summary>
        private static float SimPhase(int i) => WormMath.SimPhase(i, 1f / Mathf.Max(0.1f, MWConfig.Performance.SimRateNear.Value));
        private readonly ApproachRanks _approach = new ApproachRanks();

        // ------------------------------------------------------------------ colony tick

        private void Tick()
        {
#if MW_DEV
            int gcAtStart = GC.CollectionCount(0);
#endif
            long start = Stopwatch.GetTimestamp();
            float now = Time.time;
            WormStore s = Store;

            _pathsThisTick = 0;
            if (!_warmed)
            {
                _warmed = true;
                WormEffects.Prewarm();
            }
            _players = Player.GetAllPlayers();
            RefreshFire(now);
            RefreshPathBudget(now);
            MassTick(now);
            TickWake(now);
            bool gridRebuilt = false;
            if (now >= _nextGridTime) { RebuildGrid(now); gridRebuilt = true; }

            float nearRate = MWConfig.Performance.SimRateNear.Value;
            float farRate = MWConfig.Performance.SimRateFar.Value;
            int steps = 0;
            int highWater = s.HighWater;
            int first = _simCursor < highWater ? _simCursor : 0;
            long budgetEnd = start + (long)(MaxStepBudgetMs * Stopwatch.Frequency / 1000.0);
            _simCursor = 0;

            for (int n = 0; n < highWater; n++)
            {
                int i = first + n;
                if (i >= highWater) i -= highWater;
                if (!s.alive[i] || now < s.nextSimTime[i]) continue;
                var state = (WormState)s.state[i];
                if (state == WormState.Absorbed) continue;
                if (steps >= MaxStepsPerFrame || (steps >= MinStepsPerFrame && Stopwatch.GetTimestamp() > budgetEnd))
                {
                    _simCursor = i;   // due, not stepped: it opens the next frame, which is what keeps the carry fair
                    break;
                }

                float dt = Mathf.Min(now - s.lastSimTime[i], MaxStepDt);
                float rate = WormMath.SimRate(state == WormState.Sleeping, NearestPlayerSqr(s.pos[i]), nearRate, farRate);
                float interval = 1f / Mathf.Max(0.1f, rate);
                if (state == WormState.Fleeing && s.subPhase[i] == LeapPhase && interval > LeapStepInterval) interval = LeapStepInterval;   // a 0.35 s leap is stepped finely whatever the distance
                s.lastSimTime[i] = now;
                s.nextSimTime[i] = now + interval;
                s.simInterval[i] = interval;

                StepWorm(i, now, dt);
                steps++;
            }

            TickNests(now);
            TickCalm(now);

            // A test colony vanishes when it has no worm, no mass and no nest. It also stays while it has a mass (the worms of a dissolving mass come
            // back to it, spec §4.4). A mine colony stays: it carries the seeded flag of the mine, which must outlive its last worm (decision 194).
            if (s.AliveCount == 0 && _massCount == 0 && NestsLoaded == 0 && Kind != ColonyKind.Mine)
            {
                if (_emptySince < 0f) _emptySince = now;
                else if (now - _emptySince >= EmptyDestroyDelay) DestroyColony();
            }
            else
            {
                _emptySince = -1f;
            }

            _lastTickTime = now;
            LastSimSteps = steps;
            LastSimMs = (float)((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
#if MW_DEV
            RecordFrame(now, steps, gridRebuilt, GC.CollectionCount(0) != gcAtStart);
#endif
            AvgSimMs = AvgSimMs <= 0f ? LastSimMs : AvgSimMs + (LastSimMs - AvgSimMs) * 0.02f;
            if (LastSimMs > MaxSimMs) MaxSimMs = LastSimMs;
        }

        private void RebuildGrid(float now)
        {
            _nextGridTime = now + GridInterval;
            WormStore s = Store;
            _grid.Clear();
            for (int i = 0; i < s.HighWater; i++)
            {
                if (!s.alive[i] || s.state[i] == (byte)WormState.Absorbed) continue;
                Vector3 p = s.pos[i];
                _grid.Insert(i, p.x, p.z);
            }
        }

        private float NearestPlayerSqr(Vector3 p)
        {
            float best = float.MaxValue;
            for (int k = 0; k < _players.Count; k++)
            {
                Player pl = _players[k];
                if (pl == null) continue;
                float d = (pl.transform.position - p).sqrMagnitude;
                if (d < best) best = d;
            }
            return best;
        }

        // ------------------------------------------------------------------ one worm

        private void StepWorm(int i, float now, float dt)
        {
            WormStore s = Store;
            s.prevPos[i] = s.pos[i];
            s.stateTime[i] += dt;
            s.lastSpeed[i] = 0f; // MoveWorm sets the real value when the worm moves

            switch ((WormState)s.state[i])
            {
                case WormState.Sleeping:
                    StepSleeping(i, now);   // keeps its floor and watches for a player (spec §3.1, §5.1)
                    break;
                case WormState.Calming:
                    StepCalming(i, now, dt);
                    break;
                case WormState.Striking:
                    StepStriking(i, now, dt);
                    break;
                case WormState.Hunting:
                    StepHunting(i, now, dt);
                    break;
                case WormState.Fleeing:
                    StepFleeing(i, now, dt);
                    break;
                case WormState.Gathering:
                    StepGathering(i, now, dt);
                    break;
                case WormState.Absorbing:
                    StepAbsorbing(i, now, dt);
                    break;
                default:
                    StepIdle(i, now, dt);
                    break;
            }

            // Spec §8.4: a worm that ends up outside the box (fell, was pushed) is destroyed without drop.
            if (s.alive[i] && !InBox(s.pos[i])) KillWorm(i, false);
        }

        private void SetState(int i, WormState state, float now)
        {
#if MW_DEV
            RecordHistory(i, state, now);
#endif
            WormStore s = Store;
            s.state[i] = (byte)state;
            s.stateTime[i] = 0f;
            s.subPhase[i] = 0;
            s.progressTime[i] = now;
            s.progressPos[i] = s.pos[i];
            ReleasePath(i);
        }

        // ---- Idle

        private void StepIdle(int i, float now, float dt)
        {
            WormStore s = Store;
            if (now >= s.nextSenseTime[i])
            {
                s.nextSenseTime[i] = now + SenseInterval;
                if (SenseTarget(i, now))
                {
                    // A mass or a group to join (Gathering) before hunting alone (spec §3.3).
                    BeginPursuit(i, now);
                    return;
                }
            }
            if (FireCheck(i, false)) { SetState(i, WormState.Fleeing, now); return; }

            if (s.pauseUntil[i] > now)
            {
                Rest(i, now);
                return;
            }

            if (s.goalTime[i] <= 0f)
            {
                if (!PickWanderGoal(i, now))
                {
                    s.pauseUntil[i] = now + 1f;
                    Rest(i, now);
                    return;
                }
            }
            else
            {
                Vector3 d = s.wanderGoal[i] - s.pos[i];
                d.y = 0f;
                if (d.sqrMagnitude < ArriveDistance * ArriveDistance || now - s.goalTime[i] > WanderGoalMaxTime)
                {
                    // Arrived (or gave up): pause 1 to 3 s, then pick a new goal.
                    s.goalTime[i] = 0f;
                    s.pauseUntil[i] = now + 1f + (float)_rng.NextDouble() * 2f;
                    Rest(i, now);
                    return;
                }
            }

            MoveWorm(i, s.wanderGoal[i], MWConfig.Worm.MoveSpeed.Value * WanderSpeedFactor, now, dt, noiseWeight: 0.2f, stopAtGoal: true);
            if (CheckStuck(i, now))
            {
                s.goalTime[i] = 0f;      // drop the goal, pick another next step
                s.nextAvoidTime[i] = 0f;
            }
        }

        /// <summary>Random point of the wander disc around home that the worm can see from where it stands (spec §3.3).</summary>
        private bool PickWanderGoal(int i, float now)
        {
            WormStore s = Store;
            Vector3 pos = s.pos[i];
            Vector3 eye = pos + Vector3.up * WormPhysics.WallHeight;
            for (int tries = 0; tries < 4; tries++)
            {
                float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Sqrt((float)_rng.NextDouble()) * WanderRadius;
                Vector3 goal = s.home[i] + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                goal.y = pos.y;
                if (!InBox(goal)) continue;
                if (Physics.Linecast(eye, goal + Vector3.up * WormPhysics.WallHeight, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore)) continue;
                s.wanderGoal[i] = goal;
                s.goalTime[i] = now;
                return true;
            }
            return false;
        }

        // ---- Hunting and Striking

        private void StepHunting(int i, float now, float dt)
        {
            WormStore s = Store;
            if (now >= s.nextSenseTime[i])
            {
                s.nextSenseTime[i] = now + SenseInterval;
                if (!SenseTarget(i, now)) { LoseTarget(i, now); return; }
                // A hunter that sees a mass of its colony with room within Mass.GatherRadius joins it instead (spec 3.3): the stragglers of a group that began to hunt alone.
                if (GatheringEnabled && _actPos != null && FindGatherGoal(i, MWConfig.Mass.GatherRadius.Value, out Vector3 massGoal) == GatherMass)
                {
                    EnterGathering(i, now, GatherMass, massGoal);
                    return;
                }
            }

            if (!ResolveTarget(i, out TargetView target)) { LoseTarget(i, now); return; }
            if (FireCheck(i, false)) { SetState(i, WormState.Fleeing, now); return; }
            if (WatchProgress(i, target, now)) { LoseTarget(i, now); return; }

            Vector3 pos = s.pos[i];
            Vector3 goal = ApproachGoal(i, target, now);
            // The strike needs the target in range AND the worm at its place (or no valid place, or too long waiting), so the hunters
            // spread around the target instead of striking where they stand in a heap.
            if (SurfaceDistance(i, target) <= MWConfig.Worm.CobraRange.Value)
            {
                Vector3 toPlace = goal - pos;
                toPlace.y = 0f;
                bool atPlace = s.approachMode[i] == 2 || toPlace.sqrMagnitude <= ApproachReach * ApproachReach || s.stateTime[i] > ApproachPatience;
                if (s.approachMode[i] != 0 && atPlace)
                {
                    SetState(i, WormState.Striking, now);
                    return;
                }
            }

            bool stuck = CheckStuck(i, now);
            if (stuck || !s.hasLos[i]) TryRequestPath(i, goal, now);
            goal = FollowPath(i, goal);
            MoveWorm(i, goal, MWConfig.Worm.MoveSpeed.Value, now, dt, noiseWeight: 0f, stopAtGoal: s.approachMode[i] == 1);
        }

        /// <summary>
        /// Where a hunter heads (plan 4A-3 point 3): its own place around the target, at CobraRange x 0.9, chosen by rank x golden angle from
        /// the bearing at which the first hunter of that target arrived. The rank is kept as long as the worm hunts the same target. The
        /// point is validated (floor, no wall from the target) every 0.5 s; otherwise the worm aims at the target itself.
        /// </summary>
        private Vector3 ApproachGoal(int i, in TargetView target, float now)
        {
            WormStore s = Store;
            if (target.IsStructure)
            {
                // A structure has no place around it to reserve: the worm walks to the point of its collider closest to it and strikes from where it stands.
                s.approachRank[i] = 0;
                s.approachTarget[i] = s.targetId[i];
                s.approachMode[i] = 2;
                return target.Position;
            }
            Vector3 tp = target.Position;
            if (s.approachRank[i] < 0 || s.approachTarget[i] != s.targetId[i])
            {
                Vector3 from = s.pos[i] - tp;
                s.approachRank[i] = _approach.Assign(s.targetId[i], now, WormMath.HeadingOf(from.x, from.z), out float baseAngle);
                s.approachBase[i] = baseAngle;
                s.approachTarget[i] = s.targetId[i];
                s.approachNext[i] = now + (i % 10) * (ApproachInterval / 10f); // spread the evaluations of a wave over frames
                s.approachMode[i] = 0;
            }
            if (now >= s.approachNext[i])
            {
                s.approachNext[i] = now + ApproachInterval;
                s.approachOffset[i] = Vector3.zero;
                s.approachMode[i] = 2;
                // The place of the rank; when it is not valid (no floor, wall, a step too high on uneven ground) the following places of the
                // order are tried (the rest of the ring, then the next ring), up to ApproachTries, instead of aiming at the target. The rank stays.
                for (int attempt = 0; attempt < ApproachTries; attempt++)
                {
                    WormMath.ApproachPlace(s.approachRank[i] + attempt, s.approachBase[i], MWConfig.Worm.CobraRange.Value * ApproachRangeFactor, out float a, out float radius);
                    Vector3 point = tp + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
#if MW_DEV
                    _frameApproach++;
                    if (attempt == 0) ApproachEvaluations++;
#endif
                    if (Physics.Raycast(point + Vector3.up, Vector3.down, out RaycastHit floor, 2.5f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore)
                        && Mathf.Abs(floor.point.y - tp.y) <= ApproachMaxDy
                        && !Physics.Linecast(tp + Vector3.up * WormPhysics.WallHeight, floor.point + Vector3.up * WormPhysics.WallHeight,
                            WormPhysics.SolidMask, QueryTriggerInteraction.Ignore))
                    {
                        s.approachOffset[i] = floor.point - tp;
                        s.approachMode[i] = 1;
#if MW_DEV
                        if (attempt > 0) ApproachRescued++;
#endif
                        break;
                    }
#if MW_DEV
                    if (attempt == 0) ApproachFirstInvalid++;
#endif
                }
#if MW_DEV
                if (s.approachMode[i] == 2) ApproachNoPlace++;
#endif
            }
            return tp + s.approachOffset[i];
        }

        private const int MaxStrikesPerFrame = 1;
        private int _strikeFrame = -1, _strikesThisFrame;

        private void StepStriking(int i, float now, float dt)
        {
            WormStore s = Store;
            float windup = MWConfig.Worm.CobraWindup.Value;
            float cooldown = MWConfig.Worm.CobraCooldown.Value;
            float t = s.stateTime[i];

            StrikePhase phase = t < windup ? StrikePhase.Windup
                : t < windup + StrikeDuration ? StrikePhase.Strike
                : StrikePhase.Recovery;
            if ((byte)phase != s.subPhase[i])
            {
                if (phase == StrikePhase.Strike)
                {
                    // At most MaxStrikesPerFrame strikes land in a frame: a group that winds up together would otherwise pay for 2 to 4 bites (the vanilla damage and its
                    // effects, 0.6 to 0.8 ms each) in the same frame. The strike window lasts 0.25 s, the others land a frame or two later.
                    int frame = Time.frameCount;
                    if (frame != _strikeFrame) { _strikeFrame = frame; _strikesThisFrame = 0; }
                    if (_strikesThisFrame >= MaxStrikesPerFrame) phase = StrikePhase.Windup;
                    else _strikesThisFrame++;
                }
                if ((byte)phase != s.subPhase[i])
                {
                    s.subPhase[i] = (byte)phase;
                    if (phase == StrikePhase.Strike) OnStrike(i, now);
                }
            }

            bool haveTarget = ResolveTarget(i, out TargetView target);
            if (phase == StrikePhase.Windup && haveTarget)
            {
                // Immobile but facing the target.
                Vector3 d = target.Position - s.pos[i];
                s.heading[i] = WormMath.TurnToward(s.heading[i], WormMath.HeadingOf(d.x, d.z),
                    MWConfig.Worm.TurnSpeed.Value * Mathf.Deg2Rad * dt);
                WindupNudge(i, target, now, dt);
            }

            if (FireCheck(i, false)) { SetState(i, WormState.Fleeing, now); return; }
            if (phase == StrikePhase.Recovery && haveTarget) RecoverMove(i, target, now, dt);
            else Rest(i, now);

            if (t >= windup + StrikeDuration + cooldown)
            {
                // Phase 4: re-evaluate "somebody to join?" after each recovery.
                if (haveTarget) SetState(i, WormState.Hunting, now);
                else LoseTarget(i, now);
            }
        }

        /// <summary>
        /// Recovery of a strike: the worm shuffles toward its place at 0.3 x the speed, steering only by its place and the separation (it
        /// moves off a neighbour it overlaps), and never leaves the cobra range. The windup stays motionless.
        /// </summary>
        /// <summary>
        /// A worm winding up does not crawl, so the separation could not act on it and its neighbours kept overlapping it (S18 a). It may slide
        /// sideways away from the neighbours, slowly, without turning (the heading stays on the target) and never out of cobra range.
        /// </summary>
        private void WindupNudge(int i, in TargetView target, float now, float dt)
        {
            WormStore s = Store;
            Vector3 pos = s.pos[i];
            Vector3 push = GroupSteer(i, pos, s.heading[i], out _);
            push.y = 0f;
            if (push.sqrMagnitude < WindupNudgeMinPush * WindupNudgeMinPush) return;

            StepResult result = WormPhysics.TryStep(pos, push.normalized * (WindupNudgeSpeed * dt), out Vector3 newPos, out Vector3 newNormal);
            if (result != StepResult.Moved || !InBox(newPos)) return;
            s.pos[i] = newPos;
            if (SurfaceDistance(i, target) > MWConfig.Worm.CobraRange.Value)
            {
                s.pos[i] = pos;
                return;
            }
            s.groundNormal[i] = Vector3.Lerp(s.groundNormal[i], newNormal, 0.35f).normalized;
            s.lastSpeed[i] = dt > 1e-4f ? (newPos - pos).magnitude / dt : 0f;
            s.nextGroundTime[i] = now + GroundInterval;
        }

        private void RecoverMove(int i, in TargetView target, float now, float dt)
        {
            WormStore s = Store;
            Vector3 before = s.pos[i];
            Vector3 goal = ApproachGoal(i, target, now);
            MoveWorm(i, goal, MWConfig.Worm.MoveSpeed.Value * RecoverSpeedFactor, now, dt, noiseWeight: 0f, stopAtGoal: true, separationFirst: true);
            if (SurfaceDistance(i, target) > MWConfig.Worm.CobraRange.Value)
            {
                s.pos[i] = before;
                s.lastSpeed[i] = 0f;
            }
        }

        private void LoseTarget(int i, float now)
        {
            ClearTarget(i);
            Store.home[i] = Store.pos[i];
            Store.goalTime[i] = 0f;
            Store.pauseUntil[i] = now;
            SetState(i, WormState.Idle, now);
        }

        private void ClearTarget(int i)
        {
#if MW_DEV
            DevLogTargetDrop(i);
#endif
            Store.ClearTarget(i);
            Store.hasLos[i] = false;
            Store.approachRank[i] = -1;
        }

        // ---- Fleeing

        private void StepFleeing(int i, float now, float dt)
        {
            WormStore s = Store;
            // The leap out of a dissolved mass comes first (the flee timer starts when it lands).
            if (s.subPhase[i] == LeapPhase && StepLeap(i, now, dt)) return;
            if (s.stateTime[i] >= MWConfig.Worm.FleeDuration.Value)
            {
                // Gathering when a mass or a group exists within Mass.RegroupRadius, otherwise back to wandering where it ended up.
                EndFlee(i, now);
                return;
            }

            FireCheck(i, true); // keeps fleeFrom on the closest fire point while one is near

            Vector3 away = s.pos[i] - s.fleeFrom[i];
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = -new Vector3(Mathf.Sin(s.heading[i]), 0f, Mathf.Cos(s.heading[i]));
            Vector3 goal = s.pos[i] + away.normalized * FleeGoalDistance;
            MoveWorm(i, goal, MWConfig.Worm.MoveSpeed.Value * MWConfig.Worm.FleeSpeedMultiplier.Value, now, dt, noiseWeight: 0f, stopAtGoal: false);
        }

        // ------------------------------------------------------------------ movement

        /// <summary>
        /// One crawl step toward <paramref name="goal"/>: steer (goal + obstacle avoidance + separation + wander noise), turn at
        /// Worm.TurnSpeed, then advance along the heading through <see cref="WormPhysics.TryStep"/>. Returns true if the worm moved.
        /// </summary>
        private bool MoveWorm(int i, Vector3 goal, float speed, float now, float dt, float noiseWeight, bool stopAtGoal, bool separationFirst = false, bool direct = false)
        {
            WormStore s = Store;
            Vector3 pos = s.pos[i];
            float h = s.heading[i];

            Vector3 toGoal = goal - pos;
            toGoal.y = 0f;
            float dist = toGoal.magnitude;
            Vector3 desired = dist > 1e-4f ? toGoal / dist : Vector3.zero;

            float fileCap = float.MaxValue;
            bool pushed = false;
            if (!direct)
            {
                if (now >= s.nextAvoidTime[i]) EvaluateAvoidance(i, now);
                desired += s.avoidDir[i] * AvoidWeight;
                Vector3 group = GroupSteer(i, pos, h, out fileCap);
                // Recovering from a strike: a push away from a neighbour comes before the way back to the place (the goal is dropped while it lasts).
                pushed = separationFirst && group.sqrMagnitude > SeparationFirstMin * SeparationFirstMin;
                if (pushed) desired = group + s.avoidDir[i] * AvoidWeight;
                else desired += group;
            }
            // direct: only the goal steers (a worm that made no progress for 3 s on its way to a mass: avoidance and separation may be cancelling the goal).
            if (fileCap < speed) speed = fileCap;
            if (noiseWeight > 0f)
            {
                float a = (Mathf.PerlinNoise(now * 0.6f, i * 0.731f) - 0.5f) * 2.4f; // about +-70 degrees
                desired += new Vector3(Mathf.Sin(h + a), 0f, Mathf.Cos(h + a)) * noiseWeight;
            }
            desired.y = 0f;
            float mag = desired.magnitude;

            if (mag > 1e-4f)
            {
                h = WormMath.TurnToward(h, WormMath.HeadingOf(desired.x, desired.z), MWConfig.Worm.TurnSpeed.Value * Mathf.Deg2Rad * dt);
                s.heading[i] = h;
            }
            float fx = Mathf.Sin(h), fz = Mathf.Cos(h);

            // A worm crawls along its heading and slows down while it is still turning toward the wanted direction.
            float align = mag > 1e-4f ? (fx * desired.x + fz * desired.z) / mag : 1f;
            float travel = speed * dt * Mathf.Clamp(0.25f + 0.75f * align, 0.2f, 1f);
            if (stopAtGoal && !pushed && travel > dist) travel = dist;
            if (travel < 1e-4f)
            {
#if MW_DEV
                if (_dbgStep != null) { _dbgTravel[i] = travel; _dbgSteps[i]++; _dbgStep[i] = (StepResult)(-1); }   // -1: no travel at all (speed capped or turned away)
#endif
                GroundMaintain(i, now);
                return false;
            }

            StepResult result = WormPhysics.TryStep(pos, new Vector3(fx, 0f, fz) * travel, out Vector3 newPos, out Vector3 newNormal);
#if MW_DEV
            if (_dbgStep != null) { _dbgStep[i] = result; _dbgTravel[i] = travel; _dbgSteps[i]++; }
#endif
            if (result == StepResult.Moved && !InBox(newPos)) result = StepResult.Blocked; // clamp to the box (spec §8.4)
            if (result != StepResult.Moved)
            {
                s.nextAvoidTime[i] = 0f; // re-evaluate the surroundings on the next step
                GroundMaintain(i, now);
                return false;
            }

            float moved = (newPos - pos).magnitude;
            s.pos[i] = newPos;
            s.groundNormal[i] = Vector3.Lerp(s.groundNormal[i], newNormal, 0.35f).normalized;
            s.animTime[i] += moved / Mathf.Max(0.1f, MWConfig.Worm.MoveSpeed.Value);
            s.lastSpeed[i] = dt > 1e-4f ? moved / dt : 0f;
            if (moved > MovedEpsilon) s.stillSince[i] = now;
            s.nextGroundTime[i] = now + GroundInterval;
            return true;
        }

        /// <summary>Three horizontal rays (ahead, +-35 degrees); when ahead is blocked, steer toward the clearer side or turn around.</summary>
        private void EvaluateAvoidance(int i, float now)
        {
            WormStore s = Store;
            s.nextAvoidTime[i] = now + AvoidInterval;

            Vector3 origin = s.pos[i] + Vector3.up * WormPhysics.WallHeight;
            float h = s.heading[i];
            var fwd = new Vector3(Mathf.Sin(h), 0f, Mathf.Cos(h));
            if (!Obstructed(origin, fwd, out _))
            {
                s.avoidDir[i] = Vector3.zero;
                return;
            }

            Obstructed(origin, new Vector3(Mathf.Sin(h - AvoidAngle), 0f, Mathf.Cos(h - AvoidAngle)), out float freeLeft);
            Obstructed(origin, new Vector3(Mathf.Sin(h + AvoidAngle), 0f, Mathf.Cos(h + AvoidAngle)), out float freeRight);

            if (Mathf.Max(freeLeft, freeRight) < 0.4f)
                s.avoidDir[i] = -fwd; // boxed in: turn around
            else if (freeLeft > freeRight)
                s.avoidDir[i] = new Vector3(Mathf.Sin(h - Mathf.PI * 0.5f), 0f, Mathf.Cos(h - Mathf.PI * 0.5f));
            else
                s.avoidDir[i] = new Vector3(Mathf.Sin(h + Mathf.PI * 0.5f), 0f, Mathf.Cos(h + Mathf.PI * 0.5f));
        }

        /// <summary>A ray that only meets a walkable slope (normal within 45 degrees of up) does not count as an obstacle.</summary>
        private static bool Obstructed(Vector3 origin, Vector3 dir, out float freeDistance)
        {
            if (Physics.Raycast(origin, dir, out RaycastHit hit, AvoidLength, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore)
                && hit.normal.y < 0.7f)
            {
                freeDistance = hit.distance;
                return true;
            }
            freeDistance = AvoidLength;
            return false;
        }

        /// <summary>
        /// Group steering of one step from a single grid query (plan 4A-3): separation between body middles (all states), cohesion toward the
        /// neighbours within 3 m (wandering), alignment with the neighbours in the same state (hunting, gathering), and the speed cap of the
        /// file (a worm ahead within 0.6 m in a 30 degree cone going the same way: its last speed + 5 %, never below FileMinSpeed).
        /// <paramref name="speedCap"/> is float.MaxValue when there is no cap.
        /// </summary>
        private Vector3 GroupSteer(int i, Vector3 p, float h, out float speedCap)
        {
            speedCap = float.MaxValue;
            WormStore s = Store;
            byte state = s.state[i];
            bool cohesion = state == (byte)WormState.Idle;
            bool align = state == (byte)WormState.Hunting || state == (byte)WormState.Gathering;
            float radius = cohesion ? CohesionRadius : align ? AlignRadius : SeparationQueryRadius;

#if MW_DEV
            _frameQueries++;
#endif
            int n = _grid.QueryNearest(p.x, p.y, p.z, radius, NeighborMaxDy, i, s.pos, NeighborQueryCount, _neighborIdx, _neighborSqr);
            if (n == 0) return Vector3.zero;

            WormMath.BodyMid(p.x, p.z, h, out float mx, out float mz);
            float pushX = 0f, pushZ = 0f, cohX = 0f, cohZ = 0f, alignX = 0f, alignZ = 0f;
            int cohN = 0, alignN = 0;
            bool filed = false;

            for (int k = 0; k < n; k++)
            {
                int j = _neighborIdx[k];
                Vector3 pj = s.pos[j];
                float hj = s.heading[j];
                float dist = Mathf.Sqrt(_neighborSqr[k]);

                if (k < SeparationNeighbors && dist < SeparationQueryRadius)
                {
                    WormMath.BodyMid(pj.x, pj.z, hj, out float jx, out float jz);
                    float dx = mx - jx, dz = mz - jz;
                    float m = Mathf.Sqrt(dx * dx + dz * dz);
                    if (m < 1e-3f)
                    {
                        // Exactly stacked: split along an arbitrary but opposite axis for the two worms.
                        dx = i > j ? 1f : -1f;
                        dz = 0f;
                        m = 1f;
                    }
                    if (m < SeparationRadius)
                    {
                        float scale = (1f - m / SeparationRadius) / m;
                        pushX += dx * scale;
                        pushZ += dz * scale;
                    }
                }

                if (!filed && dist < WormMath.FileMaxDistance && WormMath.IsAheadInFile(h, pj.x - p.x, pj.z - p.z, hj))
                {
                    filed = true;
                    speedCap = WormMath.FileSpeedCap(s.lastSpeed[j], FileMinSpeed);
                }

                if (cohesion && dist > CohesionMinDistance)
                {
                    cohX += pj.x;
                    cohZ += pj.z;
                    cohN++;
                }
                if (align && s.state[j] == state)
                {
                    alignX += Mathf.Sin(hj);
                    alignZ += Mathf.Cos(hj);
                    alignN++;
                }
            }

            var steer = new Vector3(pushX, 0f, pushZ) * SeparationWeight;
            if (cohN > 0)
            {
                float cx = cohX / cohN - p.x, cz = cohZ / cohN - p.z;
                float cm = Mathf.Sqrt(cx * cx + cz * cz);
                if (cm > 1e-3f) steer += new Vector3(cx, 0f, cz) * (CohesionWeight / cm);
            }
            if (alignN > 0)
            {
                float am = Mathf.Sqrt(alignX * alignX + alignZ * alignZ);
                if (am > 0.1f) steer += new Vector3(alignX, 0f, alignZ) * (AlignWeight / am);
            }
            return steer;
        }

        /// <summary>No 0.5 m of progress in 3 s while it wants to move (spec §11.1). Resets its window every time it is evaluated.</summary>
        private bool CheckStuck(int i, float now, float window = StuckWindow)
        {
            WormStore s = Store;
            if (now - s.progressTime[i] < window) return false;
            Vector3 d = s.pos[i] - s.progressPos[i];
            d.y = 0f;
            s.progressPos[i] = s.pos[i];
            s.progressTime[i] = now;
            return d.sqrMagnitude < StuckDistance * StuckDistance;
        }

        /// <summary>A worm that does not want to move (pause, windup): no stuck window, only the periodic floor check.</summary>
        private void Rest(int i, float now)
        {
            WormStore s = Store;
            s.progressTime[i] = now;
            s.progressPos[i] = s.pos[i];
            GroundMaintain(i, now);
        }

        /// <summary>Keeps a stationary worm on its floor (a piece may be destroyed under it); falls to the next floor or dies.</summary>
        private void GroundMaintain(int i, float now)
        {
            WormStore s = Store;
            if (now < s.nextGroundTime[i]) return;
            s.nextGroundTime[i] = now + GroundInterval;

            Vector3 p = s.pos[i];
            if (WormPhysics.ProbeGround(p, out RaycastHit hit))
            {
                s.pos[i] = hit.point;
                s.groundNormal[i] = Vector3.Lerp(s.groundNormal[i], hit.normal, 0.35f).normalized;
                return;
            }

            // The floor is gone: simple fall to the next floor below, or out of the world (spec §11.1).
            if (WormPhysics.ProbeFall(p, out hit))
            {
                s.pos[i] = hit.point;
                s.prevPos[i] = hit.point;
                s.groundNormal[i] = hit.normal;
                return;
            }
            KillWorm(i, false);
        }
    }
}
