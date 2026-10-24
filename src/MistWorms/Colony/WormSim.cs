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
        private const float WanderRadius = 6f;             // spec §3.3 Idle
        private const float WanderSpeedFactor = 0.5f;      // decisions.md 34: strolling at half speed
        private const float ArriveDistance = 0.3f;
        private const float WanderGoalMaxTime = 8f;
        private const float FleeDuration = 4f;             // spec §3.3 Fleeing
        private const float FleeGoalDistance = 6f;
        private const float GroundInterval = 0.25f;        // spec §11.1: floor check cadence of a resting worm
        private const float AvoidInterval = 0.25f;
        private const float AvoidLength = 1f;
        private const float AvoidAngle = 35f * Mathf.Deg2Rad;
        private const float AvoidWeight = 1.5f;
        private const float SeparationRadius = 0.5f;
        private const int SeparationNeighbors = 4;
        private const float SeparationWeight = 0.6f;
        private const float GridInterval = 0.1f;
        private const float StuckWindow = 3f;              // spec §11.1: no 0.5 m of progress in 3 s
        private const float StuckDistance = 0.5f;
        private const float MaxStepDt = 0.5f;
        private const float EyeHeight = 0.15f;

        private Bounds _boxBounds;
        private float _nextGridTime;
        private readonly int[] _neighborIdx = new int[SeparationNeighbors];
        private readonly float[] _neighborSqr = new float[SeparationNeighbors];

        // ------------------------------------------------------------------ colony tick

        private void Tick()
        {
            long start = Stopwatch.GetTimestamp();
            float now = Time.time;
            WormStore s = Store;

            _players = Player.GetAllPlayers();
            RefreshFire(now);
            RefreshPathBudget(now);
            if (now >= _nextGridTime) RebuildGrid(now);

            float nearRate = MWConfig.Performance.SimRateNear.Value;
            float farRate = MWConfig.Performance.SimRateFar.Value;
            int steps = 0;

            for (int i = 0; i < s.HighWater; i++)
            {
                if (!s.alive[i] || now < s.nextSimTime[i]) continue;
                var state = (WormState)s.state[i];
                if (state == WormState.Absorbed) continue;

                float dt = Mathf.Min(now - s.lastSimTime[i], MaxStepDt);
                float rate = WormMath.SimRate(state == WormState.Sleeping, NearestPlayerSqr(s.pos[i]), nearRate, farRate);
                float interval = 1f / Mathf.Max(0.1f, rate);
                s.lastSimTime[i] = now;
                s.nextSimTime[i] = now + interval;
                s.simInterval[i] = interval;

                StepWorm(i, now, dt);
                steps++;
            }

            // Phase 7 adds nests and the snapshot: a colony will then also wait for its nests before vanishing.
            if (s.AliveCount == 0)
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

            switch ((WormState)s.state[i])
            {
                case WormState.Sleeping:
                    // Phase 7: dormant worms wake up through the nest and DormantViewRange/DormantHearRange.
                    GroundMaintain(i, now);
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
                default:
                    // Idle. Gathering joins the same behaviour until phase 4 adds masses and groups.
                    StepIdle(i, now, dt);
                    break;
            }

            // Spec §8.4: a worm that ends up outside the box (fell, was pushed) is destroyed without drop.
            if (s.alive[i] && !InBox(s.pos[i])) KillWorm(i, false);
        }

        private void SetState(int i, WormState state, float now)
        {
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
                    // Phase 4: look for a mass or a group to join (Gathering) before hunting alone.
                    SetState(i, WormState.Hunting, now);
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
            }

            Player target = FindPlayer(s.targetId[i]);
            if (target == null || !Targetable(target)) { LoseTarget(i, now); return; }
            if (FireCheck(i, false)) { SetState(i, WormState.Fleeing, now); return; }

            Vector3 pos = s.pos[i];
            // Distance to the target's surface: eye to capsule centre minus its radius (spec §3.3).
            float surface = Vector3.Distance(pos + Vector3.up * EyeHeight, target.GetCenterPoint()) - target.GetRadius();
            if (surface <= MWConfig.Worm.CobraRange.Value)
            {
                SetState(i, WormState.Striking, now);
                return;
            }

            Vector3 goal = target.transform.position;
            bool stuck = CheckStuck(i, now);
            if (stuck || !s.hasLos[i]) TryRequestPath(i, goal, now);
            goal = FollowPath(i, goal);
            MoveWorm(i, goal, MWConfig.Worm.MoveSpeed.Value, now, dt, noiseWeight: 0f, stopAtGoal: false);
        }

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
                s.subPhase[i] = (byte)phase;
                if (phase == StrikePhase.Strike) OnStrike(i, now);
            }

            Player target = FindPlayer(s.targetId[i]);
            if (phase == StrikePhase.Windup && target != null)
            {
                // Immobile but facing the target.
                Vector3 d = target.transform.position - s.pos[i];
                s.heading[i] = WormMath.TurnToward(s.heading[i], WormMath.HeadingOf(d.x, d.z),
                    MWConfig.Worm.TurnSpeed.Value * Mathf.Deg2Rad * dt);
            }

            if (FireCheck(i, false)) { SetState(i, WormState.Fleeing, now); return; }
            Rest(i, now);

            if (t >= windup + StrikeDuration + cooldown)
            {
                // Phase 4: re-evaluate "somebody to join?" after each recovery.
                if (target != null && Targetable(target)) SetState(i, WormState.Hunting, now);
                else LoseTarget(i, now);
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
            Store.targetKind[i] = (byte)TargetKind.None;
            Store.targetId[i] = 0;
            Store.hasLos[i] = false;
        }

        // ---- Fleeing

        private void StepFleeing(int i, float now, float dt)
        {
            WormStore s = Store;
            if (s.stateTime[i] >= FleeDuration)
            {
                // Phase 4: Gathering when a group exists within 30 m. Phase 3: back to wandering where it ended up.
                s.home[i] = s.pos[i];
                s.goalTime[i] = 0f;
                s.pauseUntil[i] = now;
                SetState(i, WormState.Idle, now);
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
        private bool MoveWorm(int i, Vector3 goal, float speed, float now, float dt, float noiseWeight, bool stopAtGoal)
        {
            WormStore s = Store;
            Vector3 pos = s.pos[i];
            float h = s.heading[i];

            Vector3 toGoal = goal - pos;
            toGoal.y = 0f;
            float dist = toGoal.magnitude;
            Vector3 desired = dist > 1e-4f ? toGoal / dist : Vector3.zero;

            if (now >= s.nextAvoidTime[i]) EvaluateAvoidance(i, now);
            desired += s.avoidDir[i] * AvoidWeight;
            desired += Separation(i) * SeparationWeight;
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
            if (stopAtGoal && travel > dist) travel = dist;
            if (travel < 1e-4f)
            {
                GroundMaintain(i, now);
                return false;
            }

            StepResult result = WormPhysics.TryStep(pos, new Vector3(fx, 0f, fz) * travel, out Vector3 newPos, out Vector3 newNormal);
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

        /// <summary>Push away from the four closest neighbours within 0.5 m, stronger when closer.</summary>
        private Vector3 Separation(int i)
        {
            WormStore s = Store;
            Vector3 p = s.pos[i];
            int n = _grid.QueryNearest(p.x, p.y, p.z, SeparationRadius, 1.5f, i, s.pos, SeparationNeighbors, _neighborIdx, _neighborSqr);
            Vector3 push = Vector3.zero;
            for (int k = 0; k < n; k++)
            {
                int j = _neighborIdx[k];
                Vector3 d = p - s.pos[j];
                d.y = 0f;
                float m = Mathf.Sqrt(_neighborSqr[k]);
                if (m < 1e-3f)
                {
                    // Exactly stacked: split along an arbitrary but opposite axis for the two worms.
                    d = new Vector3(i > j ? 1f : -1f, 0f, 0f);
                    m = 1f;
                }
                push += d / m * (1f - Mathf.Min(1f, m / SeparationRadius));
            }
            return push;
        }

        /// <summary>No 0.5 m of progress in 3 s while it wants to move (spec §11.1). Resets its window every time it is evaluated.</summary>
        private bool CheckStuck(int i, float now)
        {
            WormStore s = Store;
            if (now - s.progressTime[i] < StuckWindow) return false;
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
