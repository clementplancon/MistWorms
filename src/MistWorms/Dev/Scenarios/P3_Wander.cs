#if MW_DEV
using System.Collections;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Dev.Preview;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// Phase 3 criterion (spec §16), not in the §15.5 table: n worms wander for a while without sinking into the floor, crossing
    /// a wall or leaving their box. Parameters: n (100), where = mine | open (mine), seconds (60).
    /// The measurements do not use the simulation's own checks: floor, wall and box are probed independently from the stored
    /// positions every 0.1 s (a 0.12 m chord cannot clip a wall corner the 0.05 m clearance of the worms respects; 0.25 s chords could).
    /// The player is god and ghost, so the worms do not hunt and only wander. Since phase 6 a worm also senses creatures, tamed animals and (as a fallback) the structures of the town, and
    /// a worm that senses something gathers into a mass; the absorbed ones leave the store (counted as deaths) and climb onto the heap (above the measured floor). The targets of the
    /// worms are therefore switched off for the run (restored at the end), and the creatures of the mine are removed (<see cref="ScenarioContext.TpMine"/>).
    /// </summary>
    internal sealed class P3_Wander : IDevScenario
    {
        private const float SampleInterval = 0.1f;
        private const float GroundTolerance = 0.15f;
        private const float MinTravel = 2f;

        public string Name => "P3_Wander";

        public IEnumerator Run(ScenarioContext ctx)
        {
            int n = ctx.ParamInt("n", 100);
            string where = ctx.Param("where", "mine").ToLowerInvariant();
            float seconds = ctx.ParamFloat("seconds", 60f);
            // /tp-mine (90 s) + settling + the measurement + the captures.
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 240f + seconds;

            bool tamedBefore = MWConfig.Targets.AttackTamed.Value;
            bool creaturesBefore = MWConfig.Targets.AttackCreatures.Value;
            bool structuresBefore = MWConfig.Targets.AttackStructures.Value;
            try
            {
                MWConfig.Targets.AttackTamed.Value = false;
                MWConfig.Targets.AttackCreatures.Value = false;
                MWConfig.Targets.AttackStructures.Value = false;

                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                Player.m_localPlayer.SetGodMode(true);
                Player.m_localPlayer.SetGhostMode(true);

                bool placed;
                if (where == "open")
                {
                    yield return ctx.MoveToOpenGround(8f);
                    ctx.GuardCreatures = true;
                    placed = ctx.LastOpenGround.Found;
                }
                else
                {
                    yield return ctx.TpMine();
                    placed = ctx.LastTpMine.Ok;
                }
                if (!placed) yield break;

                Player.m_localPlayer.SetGhostMode(true);
                yield return ctx.Wait(1f);

                // A mine room may offer few valid points within 8 m: widen the disc pass by pass until n worms exist.
                int created = 0;
                WormColony colony = null;
                float[] radii = { 8f, 12f, 16f, 20f, 6f, 8f };
                int zeroStreak = 0;
                for (int pass = 0; pass < 20 && created < n; pass++)
                {
                    // Spawn only makes 10 draws per worm and large discs mostly fall out of line of sight in a corridor; 8 m succeeds ~26% of draws.
                    float radius = radii[pass % radii.Length];
                    int got = ColonyDev.Spawn(n - created, radius, WormState.Idle, false, out WormColony c, out string how, tries: 30);
                    if (c != null) colony = c;
                    created += got;
                    ctx.Log($"spawn pass {pass + 1} radius {radius}: +{got} ({created}/{n}) in a {how} colony {colony?.ZdoId}, box {colony?.BoxCenter} +-{colony?.BoxHalfSize}");
                    zeroStreak = got == 0 ? zeroStreak + 1 : 0;
                    if (zeroStreak >= 6) break;
                }
                ctx.Assert("worms_created", created == n, n, created);
                if (created == 0) yield break;

                WormStore store = colony.Store;
                int mask = WormPhysics.SolidMask;
                var last = new Vector3[store.Capacity];
                var have = new bool[store.Capacity];
                var travelled = new float[store.Capacity];
                int groundViolations = 0, wallViolations = 0, boxViolations = 0, samples = 0, contacts = 0;
                int deathsSeen = 0;

                float end = Time.realtimeSinceStartup + seconds;
                float next = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup < end)
                {
                    if (colony == null) { ctx.Assert("colony_alive", false, "colony exists", "destroyed"); yield break; }
                    if (Time.realtimeSinceStartup >= next)
                    {
                        next += SampleInterval;
                        samples++;
                        for (int i = 0; i < store.HighWater; i++)
                        {
                            if (!store.alive[i]) { have[i] = false; continue; }
                            Vector3 p = store.pos[i];

                            // Floor: a floor must be right under the worm, at its height.
                            if (!Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1f, mask, QueryTriggerInteraction.Ignore)
                                || Mathf.Abs(hit.point.y - p.y) > GroundTolerance)
                            {
                                if (++groundViolations <= 5) ctx.Log($"ground violation worm {i} at {p} (floor {(hit.collider != null ? hit.point.y.ToString("0.00") : "none")})");
                            }

                            // Wall: nothing solid between two consecutive samples at body height.
                            if (have[i])
                            {
                                Vector3 up = Vector3.up * WormPhysics.WallHeight;
                                // A real crossing is seen from both sides; a one-way touch is a probe starting on a surface (contact).
                                bool fwd = Physics.Linecast(last[i] + up, p + up, out RaycastHit wall, mask, QueryTriggerInteraction.Ignore);
                                bool bwd = Physics.Linecast(p + up, last[i] + up, out RaycastHit wallBack, mask, QueryTriggerInteraction.Ignore);
                                if (fwd && bwd)
                                {
                                    if (++wallViolations <= 5)
                                        ctx.Log($"wall violation worm {i} {last[i]} -> {p} state {(WormState)store.state[i]}, step {(p - last[i]).magnitude:0.000} m, home {store.home[i]}, heading {store.heading[i]:0.00}, ground normal {store.groundNormal[i]} through {wall.collider.name} bounds {wall.collider.bounds} hit at {wall.point} (layer {LayerMask.LayerToName(wall.collider.gameObject.layer)}, normal {wall.normal}; back: {wallBack.collider.name}, normal {wallBack.normal})");
                                }
                                else if (fwd || bwd)
                                {
                                    contacts++;
                                }
                                Vector3 d = p - last[i];
                                d.y = 0f;
                                travelled[i] += d.magnitude;
                            }

                            // Box (spec §8.4).
                            if (!WormMath.InsideBox(p, colony.BoxCenter, colony.BoxHalfSize))
                            {
                                if (++boxViolations <= 5) ctx.Log($"box violation worm {i} at {p}");
                            }

                            last[i] = p;
                            have[i] = true;
                        }
                        if (store.AliveCount < created) deathsSeen = created - store.AliveCount;
                    }
                    yield return null;
                }

                int moving = 0;
                for (int i = 0; i < store.HighWater; i++)
                    if (travelled[i] >= MinTravel) moving++;

                ctx.Log($"{samples} samples, {store.AliveCount} alive, {moving} moved >= {MinTravel} m, sim avg {colony.AvgSimMs:0.000} ms / max {colony.MaxSimMs:0.000} ms ({colony.LastSimSteps} steps last frame)");
                ctx.Log($"wall contacts (one direction only, informative): {contacts}");
                ctx.Assert("no_ground_violations", groundViolations == 0, 0, groundViolations);
                ctx.Assert("no_wall_violations", wallViolations == 0, 0, wallViolations);
                ctx.Assert("no_box_violations", boxViolations == 0, 0, boxViolations);
                ctx.Assert("no_deaths", store.AliveCount == created, created, store.AliveCount);
                ctx.Assert("colony_not_disabled", !colony.Disabled, "enabled", colony.Disabled ? colony.DisabledReason : "enabled");
                ctx.Assert("worms_moved", moving >= Mathf.CeilToInt(created * 0.8f), $">= {Mathf.CeilToInt(created * 0.8f)} worms with >= {MinTravel} m", moving);
                // Information only until phase 8 (spec §11 budget: 1.0 ms average).
                ctx.Log($"simulation cost: average {colony.AvgSimMs:0.000} ms, max {colony.MaxSimMs:0.000} ms");
                if (deathsSeen > 0) ctx.Log($"{deathsSeen} worms died during the run");

                yield return Shots(ctx, store);
            }
            finally
            {
                MWConfig.Targets.AttackTamed.Value = tamedBefore;
                MWConfig.Targets.AttackCreatures.Value = creaturesBefore;
                MWConfig.Targets.AttackStructures.Value = structuresBefore;
                DevCamera.Follow();
                ctx.Console("mw_kill");
                if (Player.m_localPlayer != null) Player.m_localPlayer.SetGhostMode(false);
            }
        }

        /// <summary>A free camera 3 m above the group and a close-up of one worm.</summary>
        private static IEnumerator Shots(ScenarioContext ctx, WormStore store)
        {
            Player player = Player.m_localPlayer;
            if (player == null || store.AliveCount == 0) yield break;

            var centroid = Vector3.zero;
            for (int i = 0; i < store.HighWater; i++)
                if (store.alive[i]) centroid += store.pos[i];
            centroid /= store.AliveCount;

            Vector3 back = player.transform.position - centroid;
            back.y = 0f;
            back = back.sqrMagnitude > 1f ? back.normalized : Vector3.back;
            int mask = WormPhysics.SolidMask;

            // 2.5 m above the group's floor, 5 m back toward the player (a ~27 degree look-down). A wall or ceiling between the
            // camera and the group pulls the camera in along that line, so it stays inside the room.
            Vector3 target = centroid + Vector3.up * 0.2f;
            Vector3 cam = centroid + back * 5f + Vector3.up * 2.5f;
            if (Physics.Linecast(target, cam, out RaycastHit block, mask, QueryTriggerInteraction.Ignore))
                cam = block.point + (target - block.point).normalized * 0.3f;
            DevCamera.Free(cam, centroid);
            yield return ctx.WaitFrames(10);
            yield return ctx.Shot("group");

            int closest = -1;
            float best = float.MaxValue;
            for (int i = 0; i < store.HighWater; i++)
            {
                if (!store.alive[i]) continue;
                float d = (store.pos[i] - centroid).sqrMagnitude;
                if (d < best) { best = d; closest = i; }
            }
            if (closest < 0) yield break;

            Vector3 wormPos = store.pos[closest];
            Vector3 close = wormPos + back * 0.9f + Vector3.up * 0.5f;
            if (Physics.Linecast(close, wormPos + Vector3.up * 0.05f, mask, QueryTriggerInteraction.Ignore))
                close = wormPos + Vector3.up * 0.9f;
            DevCamera.Free(close, wormPos + Vector3.up * 0.05f);
            yield return ctx.WaitFrames(10);
            yield return ctx.Shot("close");
        }
    }
}
#endif
