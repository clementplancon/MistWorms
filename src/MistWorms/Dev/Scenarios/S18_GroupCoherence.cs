#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Dev.Preview;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// S18 (spec §15.5, phase 4A): group behaviour of free hunting worms.
    /// a) open ground, still non ghost player: 30 worms spawned 8 to 12 m away in Hunting, left for 20 s;
    /// b) infested mine: 20 worms spawned as in P3_Wander (passes of 8, 12, 16, 20 m with a line of sight from the chest), left for 30 s;
    ///    informative: the narrowest passage the worms stand in and the success rate of their hunting paths.
    /// The player is god (strikes land without killing: the bites are ignored by the damage patch) and never moves. Spawns use a fixed seed.
    /// Every 0.25 s: the fraction of neighbouring pairs (body middles under 1 m apart) whose middles are more than 0.2 m apart (steady state:
    /// after 5 s, aggregate >= 95 %); a) the 30 degree sectors occupied by the worms within 2 m of the player at the end (>= 240 degrees);
    /// b) a worm with less than 0.5 m of progress in 6 s while its goal is reachable counts as blocked (0 allowed), and the floor / wall / box
    /// checks of P3_Wander (0 allowed). Hunters are refreshed as "seen" at each sample so that sensing, which is not under test, never
    /// makes them give up. Parameters: part (a, b, both), seconds_a (20), seconds_b (30).
    /// </summary>
    internal sealed class S18_GroupCoherence : IDevScenario
    {
        private const int Seed = 18;
        private const int WormsA = 30;
        private const int WormsB = 20;
        private const float OpenClearRadius = 14f;
        private const float SpawnMinA = 8f, SpawnMaxA = 12f;
        private const float MinSpawnGap = 0.4f;

        private const float SteadyAfter = 5f;
        private const float PairInterval = 0.25f;
        private const float ViolationInterval = 0.1f;
        private const float NeighborDistance = 1f;         // pairs whose middles are closer than this are "neighbouring"
        private const float MinMiddleGap = 0.2f;
        private const float MinPairFraction = 0.95f;
        private const float SectorDegrees = 30f;
        private const float SectorRadius = 2f;
        private const float MinCoverageDeg = 240f;

        private const float BlockWindow = 6f;
        private const float BlockDistance = 0.5f;
        private const float ArrivedDistance = 2.5f;       // a worm this close to the player has arrived: waiting its turn is not being blocked
        private const float GroundTolerance = 0.15f;
        private const float CreatureClearRadius = 40f;    // vanilla creatures of the mine are removed this far around the player before b)

        private const float WidthProbe = 4f;
        private static readonly float[] SpawnRadiiB = { 8f, 12f, 16f, 20f };   // as P3_Wander: widened pass by pass until the worms are placed
        private const int SpawnTriesPerWorm = 10;
        private const int SpawnPassesB = 12;
        private const float SimMaxMs = 3f;                // spec 11: worst frame of the simulation
        private const float SpawnQuietSeconds = 1f;       // the spawn frame and the second after it are left out of the maximum

        public string Name => "S18_GroupCoherence";

        public IEnumerator Run(ScenarioContext ctx)
        {
            bool clutterHidden = false;
            string part = ctx.Param("part", "both").ToLowerInvariant();
            bool runA = part == "a" || part == "both";
            bool runB = part == "b" || part == "both";
            float secondsA = ctx.ParamFloat("seconds_a", 20f);
            float secondsB = ctx.ParamFloat("seconds_b", 30f);
            // Player ready + open ground search (a) + /tp-mine 90 s + corridor search (b) + the runs + captures.
            if (!ctx.Params.ContainsKey("timeout"))
                ctx.TimeoutSeconds = 120f + (runA ? 90f + secondsA : 0f) + (runB ? 200f + secondsB : 0f);

            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                yield return ctx.WaitFrames(3);
                Player player = Player.m_localPlayer;
                player.SetGodMode(true);
                player.SetGhostMode(true);

                if (runA)
                {
                    // Tall grass hides the worms in the overview (as in V08): switched off for part a, restored at the end.
                    if (ClutterSystem.instance != null)
                    {
                        ClutterSystem.instance.enabled = false;
                        ClutterSystem.instance.ClearAll();
                        clutterHidden = true;
                    }
                    yield return ctx.MoveToOpenGround(OpenClearRadius);
                    if (!ctx.LastOpenGround.Found) yield break;
                    player.SetGhostMode(false);
                    player.SetGodMode(true);
                    yield return ctx.Wait(1f);
                    yield return RunPart(ctx, "a", false, secondsA);
                    ctx.Console("mw_kill");
                    yield return ctx.Wait(0.5f);
                }

                if (runB)
                {
                    player.SetGhostMode(true);
                    yield return ctx.TpMine();
                    if (!ctx.LastTpMine.Ok) yield break;
                    player.SetGhostMode(false);
                    player.SetGodMode(true);
                    yield return ctx.Wait(1f);
                    yield return RunPart(ctx, "b", true, secondsB);
                }
            }
            finally
            {
                if (clutterHidden && ClutterSystem.instance != null) ClutterSystem.instance.enabled = true;
                DevCamera.Follow();
                ctx.Console("mw_kill");
                if (Player.m_localPlayer != null)
                {
                    Player.m_localPlayer.SetGodMode(true);
                    Player.m_localPlayer.SetGhostMode(false);
                }
            }
        }

        // ------------------------------------------------------------------ one part

        private static IEnumerator RunPart(ScenarioContext ctx, string tag, bool corridor, float seconds)
        {
            Player player = Player.m_localPlayer;
            Vector3 playerStart = player.transform.position;
            int wanted = corridor ? WormsB : WormsA;
            var positions = new List<Vector3>(wanted);
            WormColony colony = ColonyDev.FindOrCreateColony(player, out string how);
            if (!ctx.Assert(tag + "_colony", colony != null, true, how)) yield break;

            if (corridor) ClearCreatures(ctx, player);
            if (corridor) PickMinePositions(player, colony, wanted, positions);
            else PickOpenPositions(player, colony, wanted, positions);
            ctx.Assert(tag + "_worms_placed", positions.Count == wanted, wanted, positions.Count);
            if (positions.Count == 0) yield break;

            // Hunting worms facing the player, target already chosen.
            WormStore store = colony.Store;
            long playerId = player.GetPlayerID();
            Vector3 chest = player.GetCenterPoint();
            float now = Time.time;
            int created = 0;
            foreach (Vector3 at in positions)
            {
                Vector3 to = player.transform.position - at;
                int slot = colony.AddWorm(at, WormState.Hunting, WormMath.HeadingOf(to.x, to.z));
                if (slot < 0) break;
                store.targetKind[slot] = (byte)TargetKind.Player;
                store.targetId[slot] = playerId;
                store.lastSeen[slot] = now;
                store.hasLos[slot] = !Physics.Linecast(store.pos[slot] + Vector3.up * 0.15f, chest, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore);
                created++;
            }
            ctx.Assert(tag + "_worms_created", created == positions.Count, positions.Count, created);
            if (created == 0) yield break;
            colony.ResetCounters();
            float maxDrift = 0f;
            float narrowest = float.MaxValue;
            Vector3 narrowestAt = Vector3.zero;

            // The spawn frame (30 worms placed in one frame, a first sensing of each) and the second after it do not count in the peak.
            yield return ctx.Wait(SpawnQuietSeconds);
            colony.ResetSimPeaks();
            ctx.Log($"{tag}: simulation peak reset after the spawn frame and the {SpawnQuietSeconds:0} s that follow (they are not measured)");

            // Measurement state.
            int cap = store.Capacity;
            var last = new Vector3[cap];
            var haveLast = new bool[cap];
            var refPos = new Vector3[cap];
            var refTime = new float[cap];
            var refGen = new ushort[cap];
            var tracked = new bool[cap];
            var scratch = new List<Vector3>(64);
            int mask = WormPhysics.SolidMask;
            int sectors = Mathf.CeilToInt(360f / SectorDegrees);
            var occupied = new bool[sectors];

            long pairsTotal = 0, pairsGood = 0;
            float worstSample = 1f;
            int blocked = 0, unreachableStuck = 0, groundViolations = 0, wallViolations = 0, boxViolations = 0;
            float lastCoverage = 0f, bestCoverage = 0f;
            int lastNear = 0;

            float t0 = Time.realtimeSinceStartup;
            float end = t0 + seconds;
            float nextPair = t0, nextViolation = t0, nextLog = t0 + 5f;
            bool shotTaken = false;

            while (Time.realtimeSinceStartup < end)
            {
                if (colony == null) { ctx.Assert(tag + "_colony_alive", false, "colony exists", "destroyed"); yield break; }
                float rt = Time.realtimeSinceStartup;
                float elapsed = rt - t0;

                if (rt >= nextViolation)
                {
                    nextViolation += ViolationInterval;
                    for (int i = 0; i < store.HighWater; i++)
                    {
                        if (!store.alive[i]) { haveLast[i] = false; continue; }
                        Vector3 p = store.pos[i];
                        if (!Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1f, mask, QueryTriggerInteraction.Ignore)
                            || Mathf.Abs(hit.point.y - p.y) > GroundTolerance)
                        {
                            if (++groundViolations <= 5) ctx.Log($"ground violation worm {i} at {p}");
                        }
                        if (haveLast[i])
                        {
                            Vector3 up = Vector3.up * WormPhysics.WallHeight;
                            bool fwd = Physics.Linecast(last[i] + up, p + up, out RaycastHit wall, mask, QueryTriggerInteraction.Ignore);
                            bool bwd = Physics.Linecast(p + up, last[i] + up, mask, QueryTriggerInteraction.Ignore);
                            if (fwd && bwd && ++wallViolations <= 5)
                                ctx.Log($"wall violation worm {i} {last[i]} -> {p} through {wall.collider.name}");
                        }
                        if (!WormMath.InsideBox(p, colony.BoxCenter, colony.BoxHalfSize))
                        {
                            if (++boxViolations <= 5) ctx.Log($"box violation worm {i} at {p}");
                        }
                        last[i] = p;
                        haveLast[i] = true;
                    }
                }

                if (rt >= nextPair)
                {
                    nextPair += PairInterval;
                    float game = Time.time;
                    Vector3 pp = player.transform.position;

                    if (corridor)
                    {
                        // Narrowest passage the worms really stand in (4 axis probe at their positions).
                        for (int i = 0; i < store.HighWater; i++)
                        {
                            if (!store.alive[i]) continue;
                            float w = WidthAt(store.pos[i]);
                            if (w < narrowest) { narrowest = w; narrowestAt = store.pos[i]; }
                        }
                    }

                    // Pin the player: the bites push even a god player back (push 2), so put it back at its start each step.
                    maxDrift = Mathf.Max(maxDrift, (pp - playerStart).magnitude);
                    player.transform.position = playerStart;
                    if (player.m_body != null)
                    {
                        player.m_body.position = playerStart;
                        player.m_body.linearVelocity = Vector3.zero;
                    }

                    // Keep the hunters "seeing" the player (sensing is not under test).
                    for (int i = 0; i < store.HighWater; i++)
                        if (store.alive[i] && store.targetId[i] == playerId) store.lastSeen[i] = game;

                    // Pairs of neighbouring body middles.
                    int pairs = 0, good = 0;
                    for (int i = 0; i < store.HighWater; i++)
                    {
                        if (!store.alive[i]) continue;
                        WormMath.BodyMid(store.pos[i].x, store.pos[i].z, store.heading[i], out float ix, out float iz);
                        for (int j = i + 1; j < store.HighWater; j++)
                        {
                            if (!store.alive[j] || Mathf.Abs(store.pos[i].y - store.pos[j].y) > 1.5f) continue;
                            WormMath.BodyMid(store.pos[j].x, store.pos[j].z, store.heading[j], out float jx, out float jz);
                            float dx = ix - jx, dz = iz - jz;
                            float d = Mathf.Sqrt(dx * dx + dz * dz);
                            if (d >= NeighborDistance) continue;
                            pairs++;
                            if (d > MinMiddleGap) good++;
                        }
                    }
                    if (elapsed >= SteadyAfter && pairs > 0)
                    {
                        pairsTotal += pairs;
                        pairsGood += good;
                        worstSample = Mathf.Min(worstSample, good / (float)pairs);
                    }

                    // a) 30 degree sectors occupied within 2 m of the player.
                    Array.Clear(occupied, 0, occupied.Length);
                    lastNear = 0;
                    for (int i = 0; i < store.HighWater; i++)
                    {
                        if (!store.alive[i]) continue;
                        float dx = store.pos[i].x - pp.x, dz = store.pos[i].z - pp.z;
                        if (dx * dx + dz * dz > SectorRadius * SectorRadius) continue;
                        lastNear++;
                        float deg = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
                        deg = (deg % 360f + 360f) % 360f;
                        occupied[Mathf.Min(sectors - 1, (int)(deg / SectorDegrees))] = true;
                    }
                    int count = 0;
                    for (int k = 0; k < sectors; k++) if (occupied[k]) count++;
                    lastCoverage = count * SectorDegrees;
                    bestCoverage = Mathf.Max(bestCoverage, lastCoverage);

                    // b) progress windows.
                    if (corridor)
                    {
                        for (int i = 0; i < store.HighWater; i++)
                        {
                            if (!store.alive[i]) { tracked[i] = false; continue; }
                            Vector3 p = store.pos[i];
                            Vector3 toPlayer = pp - p;
                            toPlayer.y = 0f;
                            bool arrived = store.state[i] == (byte)WormState.Striking || toPlayer.magnitude <= ArrivedDistance;
                            if (!tracked[i] || refGen[i] != store.generation[i] || arrived)
                            {
                                tracked[i] = !arrived;
                                refGen[i] = store.generation[i];
                                refPos[i] = p;
                                refTime[i] = rt;
                                continue;
                            }
                            if (rt - refTime[i] < BlockWindow) continue;
                            Vector3 moved = p - refPos[i];
                            moved.y = 0f;
                            refPos[i] = p;
                            refTime[i] = rt;
                            if (moved.magnitude >= BlockDistance) continue;
                            if (GoalReachable(p, pp, scratch))
                            {
                                if (++blocked <= 10)
                                    ctx.Log($"blocked worm {i} at {p} ({moved.magnitude:0.00} m in {BlockWindow:0} s), state {(WormState)store.state[i]}, {toPlayer.magnitude:0.0} m from the player");
                            }
                            else
                            {
                                unreachableStuck++;
                            }
                        }
                    }

                    if (!shotTaken && elapsed >= seconds - 1f)
                    {
                        shotTaken = true;
                        yield return Shot(ctx, tag, player, store, corridor);
                    }
                }

                if (rt >= nextLog)
                {
                    nextLog += 5f;
                    ctx.Log($"{tag} +{elapsed:0}s: {store.AliveCount} alive, {lastNear} within {SectorRadius:0} m, coverage {lastCoverage:0} deg, " +
                            $"pairs {pairsGood}/{pairsTotal}, strikes {colony.StrikesLanded + colony.StrikesMissed}, sim avg {colony.AvgSimMs:0.000} ms");
                }
                yield return null;
            }

            // ---- verdicts
            float fraction = pairsTotal > 0 ? pairsGood / (float)pairsTotal : 0f;
            int strikes = colony.StrikesLanded + colony.StrikesMissed;
            Vector3 drift = player.transform.position - playerStart;
            ctx.Log($"{tag}: pairs {pairsGood}/{pairsTotal} = {fraction * 100f:0.0} % (worst sample {worstSample * 100f:0.0} %), coverage final {lastCoverage:0} deg (best {bestCoverage:0}), " +
                    $"{lastNear} worms within {SectorRadius:0} m, strikes {colony.StrikesLanded} landed / {colony.StrikesMissed} missed, " +
                    $"sim avg {colony.AvgSimMs:0.000} ms / max {colony.MaxSimMs:0.000} ms");
            for (int k = 0; k < colony.WorstFrames.Length; k++)
                if (colony.WorstFrames[k] != null) ctx.Log($"{tag}: worst simulation frame #{k + 1}: {colony.WorstFrames[k]}");
            ctx.Log(colony.FirstSessionStrikeMs >= 0f
                ? $"{tag}: frame of the first strike of the session ({colony.FirstSessionStrikeMs:0.00} ms) excluded from the maximum: first use of vanilla Damage() and JIT, not a recurring cost"
                : $"{tag}: the first strike of the session did not happen in this colony (nothing to exclude for it)");
            ctx.Assert(tag + "_sim_max", colony.PeakMsWithoutFirstStrike <= SimMaxMs,
                $"<= {SimMaxMs} ms (spawn frame, the second after it and the session's first strike excluded)", $"{colony.PeakMsWithoutFirstStrike:0.00} ms (raw max {colony.MaxSimMs:0.00} ms)");
            foreach (string cause in colony.DeathCauses) ctx.Log($"{tag}: death cause: {cause}");
            ctx.Assert(tag + "_pairs_apart", pairsTotal > 0 && fraction >= MinPairFraction, $">= {MinPairFraction * 100f:0} % of {pairsTotal} neighbouring pairs > {MinMiddleGap} m apart", $"{fraction * 100f:0.0} %");
            ctx.Assert(tag + "_player_alive", !player.IsDead(), true, !player.IsDead());
            ctx.Log($"{tag}: player pinned every {PairInterval} s; largest drift between two pins {maxDrift:0.00} m (informative), final {drift.magnitude:0.00} m");
            ctx.Assert(tag + "_worms_struck", strikes > 0, "> 0 strikes", strikes);
            ctx.Assert(tag + "_colony_not_disabled", !colony.Disabled, "enabled", colony.Disabled ? colony.DisabledReason : "enabled");
            ctx.Assert(tag + "_no_deaths", store.AliveCount == created, created, store.AliveCount);

            if (!corridor)
            {
                ctx.Assert("a_coverage", lastCoverage >= MinCoverageDeg, $">= {MinCoverageDeg} deg of 30 degree sectors", lastCoverage);
            }
            else
            {
                ctx.Log($"b: narrowest passage crossed by a worm {narrowest:0.00} m at {narrowestAt} (informative)");
                ctx.Log($"b: hunting paths: {colony.PathRequests} requests, {colony.PathComplete} complete, {colony.PathPartial} partial, {colony.PathFailed} failed (informative)");
                ctx.Log($"b: {unreachableStuck} slow windows ignored because the goal was not reachable (informative)");
                ctx.Assert("b_no_blocked_worm", blocked == 0, 0, blocked);
                ctx.Assert("b_no_ground_violations", groundViolations == 0, 0, groundViolations);
                ctx.Assert("b_no_wall_violations", wallViolations == 0, 0, wallViolations);
                ctx.Assert("b_no_box_violations", boxViolations == 0, 0, boxViolations);
            }
            if (!corridor) ctx.Log($"a: violations (informative) ground {groundViolations}, wall {wallViolations}, box {boxViolations}");
        }

        // ------------------------------------------------------------------ placement

        /// <summary>Open ground: worms 8 to 12 m from the player, on a floor of the colony box, at least 0.4 m apart. Fixed seed.</summary>
        private static void PickOpenPositions(Player player, WormColony colony, int wanted, List<Vector3> into)
        {
            var rng = new System.Random(Seed);
            Vector3 origin = player.transform.position;
            for (int tries = 0; tries < wanted * 40 && into.Count < wanted; tries++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = SpawnMinA + (float)rng.NextDouble() * (SpawnMaxA - SpawnMinA);
                var start = new Vector3(origin.x + Mathf.Cos(angle) * r, origin.y + 2f, origin.z + Mathf.Sin(angle) * r);
                if (!Physics.Raycast(start, Vector3.down, out RaycastHit hit, 5f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore)) continue;
                TryAdd(into, hit, colony, MinSpawnGap);
            }
        }

        /// <summary>
        /// Removes the vanilla non player, untamed creatures within 40 m (a mine has some: their attacks reach our hit proxies and kill worms). The
        /// ones this peer does not own are claimed first.
        /// </summary>
        private static void ClearCreatures(ScenarioContext ctx, Player player)
        {
            var found = new List<Character>();
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c.IsPlayer() || c.IsTamed()) continue;
                if ((c.transform.position - player.transform.position).sqrMagnitude > CreatureClearRadius * CreatureClearRadius) continue;
                found.Add(c);
            }
            int removed = 0;
            foreach (Character c in found)
            {
                ZNetView view = c.GetComponent<ZNetView>();
                if (view == null || !view.IsValid()) continue;
                if (!view.IsOwner()) view.ClaimOwnership();
                ctx.Log($"removing creature {c.name} at {c.transform.position}");
                if (ZNetScene.instance != null) ZNetScene.instance.Destroy(c.gameObject);
                removed++;
            }
            ctx.Log($"vanilla creatures within {CreatureClearRadius:0} m removed: {removed} of {found.Count} found");
        }

        /// <summary>
        /// Mine: as P3_Wander, passes of radius 8, 12, 16 and 20 m (then 8 m) around the player with a clear line from the chest, until the
        /// worms are placed. Fixed seed.
        /// </summary>
        private static void PickMinePositions(Player player, WormColony colony, int wanted, List<Vector3> into)
        {
            var rng = new System.Random(Seed);
            Vector3 origin = player.transform.position;
            Vector3 chest = player.GetCenterPoint();
            int mask = WormPhysics.SolidMask;
            for (int pass = 0; pass < SpawnPassesB && into.Count < wanted; pass++)
            {
                float radius = pass < SpawnRadiiB.Length ? SpawnRadiiB[pass] : SpawnRadiiB[0];
                int missing = wanted - into.Count;
                for (int k = 0; k < missing; k++)
                {
                    for (int attempt = 0; attempt < SpawnTriesPerWorm; attempt++)
                    {
                        float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                        float r = Mathf.Sqrt((float)rng.NextDouble()) * radius;
                        var start = new Vector3(origin.x + Mathf.Cos(angle) * r, origin.y + 1.5f, origin.z + Mathf.Sin(angle) * r);
                        if (!Physics.Raycast(start, Vector3.down, out RaycastHit hit, 4f, mask, QueryTriggerInteraction.Ignore)) continue;
                        if (Physics.Linecast(chest, hit.point + Vector3.up * 0.3f, mask, QueryTriggerInteraction.Ignore)) continue;
                        if (TryAdd(into, hit, colony, MinSpawnGap)) break;
                    }
                }
            }
        }

        private static bool TryAdd(List<Vector3> into, RaycastHit hit, WormColony colony, float gap)
        {
            if (hit.normal.y < 0.9f) return false;
            if (!WormMath.InsideBox(hit.point, colony.BoxCenter, colony.BoxHalfSize)) return false;
            foreach (Vector3 q in into)
            {
                Vector3 d = q - hit.point;
                if (d.x * d.x + d.z * d.z < gap * gap) return false;
            }
            into.Add(hit.point);
            return true;
        }

        /// <summary>Narrowest of the four wall-to-wall distances through <paramref name="p"/> at wall height (each side capped at WidthProbe).</summary>
        private static float WidthAt(Vector3 p)
        {
            Vector3 origin = p + Vector3.up * WormPhysics.WallHeight;
            float min = float.MaxValue;
            for (int k = 0; k < 4; k++)
            {
                float ang = k * Mathf.PI * 0.25f;
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                float w = Reach(origin, dir) + Reach(origin, -dir);
                if (w < min) min = w;
            }
            return min;
        }

        private static float Reach(Vector3 origin, Vector3 dir) =>
            Physics.Raycast(origin, dir, out RaycastHit hit, WidthProbe, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore) ? hit.distance : WidthProbe;

        // ------------------------------------------------------------------ reachability and capture

        /// <summary>The player can be reached from <paramref name="from"/>: a clear line at wall height, or a complete route of the vanilla pathfinder.</summary>
        private static bool GoalReachable(Vector3 from, Vector3 to, List<Vector3> scratch)
        {
            Vector3 up = Vector3.up * WormPhysics.WallHeight;
            if (!Physics.Linecast(from + up, to + up, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore)) return true;
            return Pathfinding.instance != null
                && Pathfinding.instance.GetPath(from, to, scratch, Pathfinding.AgentType.Humanoid, requireFullPath: true, cleanup: true);
        }

        /// <summary>Overview: a free camera above and behind the player (b: behind the player, along the corridor toward the worms).</summary>
        private static IEnumerator Shot(ScenarioContext ctx, string tag, Player player, WormStore store, bool corridor)
        {
            Vector3 pp = player.transform.position;
            var centroid = Vector3.zero;
            int n = 0;
            for (int i = 0; i < store.HighWater; i++)
            {
                if (!store.alive[i]) continue;
                centroid += store.pos[i];
                n++;
            }
            if (n == 0) yield break;
            centroid /= n;

            Vector3 toWorms = centroid - pp;
            toWorms.y = 0f;
            toWorms = toWorms.sqrMagnitude > 1e-3f ? toWorms.normalized : Vector3.forward;
            Vector3 target = pp + Vector3.up * 0.5f;
            // a) 4 m above and 2.5 m back (steep look down on the ring); b) 1.8 m above, 2.5 m behind the player (the corridor lies ahead).
            Vector3 cam = corridor ? pp - toWorms * 2.5f + Vector3.up * 1.8f : pp - toWorms * 2.5f + Vector3.up * 4f;
            if (corridor) target = pp + toWorms * 3f + Vector3.up * 0.3f;
            if (Physics.Linecast(target, cam, out RaycastHit block, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore))
                cam = block.point + (target - block.point).normalized * 0.3f;
            DevCamera.Free(cam, target);
            yield return ctx.WaitFrames(10);
            yield return ctx.Shot("overview_" + tag);
            DevCamera.Follow();
        }
    }
}
#endif
