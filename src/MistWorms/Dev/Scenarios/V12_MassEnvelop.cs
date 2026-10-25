#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using MistWorms.Colony;
using MistWorms.Dev.Preview;
using MistWorms.Mass;
using MistWorms.Render;
using MistWorms.Render.Gpu;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// V12 (visual v2, pass B2): the GPU rope colony on real masses, driven by the AI of the game (not the phantom of V14). Open ground, no creature within 40 m, the player in god
    /// mode, no water within 5 m of the site. Three acts:
    ///  1. a mass of 100 hunts the player standing behind a vanilla rock: it goes round the rock (nodes against the rock while it passes, fewer than 2 % inside it) and reaches the player;
    ///  2. a mass of 100 crosses a low stone (about half of its dome height): nothing inside the stone, nodes over it when it goes over;
    ///  3. a mass of 100 engulfs the player: the sleeve forms on the real body of the player, and the time it takes to reach 80 % of its steady state is logged (the readback of
    ///     the nodes every 0.5 s gives the number of worms settled on the body).
    /// Captures from 1, 3 and 10 m and a clip for each act. Run it with tools/build.ps1 -Configuration Profile. Parameters: n (count, default 100).
    /// </summary>
    internal sealed class V12_MassEnvelop : IDevScenario
    {
        private const float PollSeconds = 0.25f;
        private const float SleeveSample = 0.5f;
        private const float FormedBudget = 5f;          // logged against 2.5 s in the sandbox (the engulf of the game rises over 1 s, the polling is 0.5 s); asserted at twice that

        public string Name => "V12_MassEnvelop";

        public IEnumerator Run(ScenarioContext ctx)
        {
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 600f;
            int n = Mathf.Clamp((int)ctx.ParamFloat("n", 100f), 10, 200);
            bool timeForced = false, clutterHidden = false;
            GameObject placed = null;
            WritheMassState mass = null;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;
                bool gpu = GpuMassBridge.Available;
                ctx.Assert("gpu_available", gpu, true, gpu ? "ready" : (GpuWormHost.Error ?? GpuWormBundle.Error ?? "off"));
                if (!gpu) yield break;

                ctx.Console("mw_kill");
                DevPreview.Instance.Clear();
                if (ClutterSystem.instance != null)
                {
                    ClutterSystem.instance.enabled = false;
                    ClutterSystem.instance.ClearAll();
                    clutterHidden = true;
                }
                Player player = Player.m_localPlayer;
                player.SetGodMode(true);
                yield return ctx.WaitFrames(3);

                // open ground with a vanilla rock 6 to 16 m away, no built piece, no water
                var site = new OpenGroundResult();
                yield return DevWorld.MoveToOpenGround(3f, p => !V14_GpuMassSandbox.NearBuilt(p, V14_GpuMassSandbox.BuiltClearance) && !V14_GpuMassSandbox.NearWater(p, V14_GpuMassSandbox.WaterClearance)
                                                               && V14_GpuMassSandbox.FindRock(p, 6f, 16f) != null, site, ctx.Log);
                ctx.Assert("rock_site_found", site.Found, "flat ground with a vanilla rock 6 to 16 m away", site.Found ? site.Spot.ToString() : site.Error);
                if (!site.Found) yield break;
                ctx.Console($"{VanillaNames.CmdTimeOfDay} 0.5");
                timeForced = true;
                player = Player.m_localPlayer;
                player.SetGodMode(true);
                S18_GroupCoherence.ClearCreatures(ctx, player);

                Collider rock = V14_GpuMassSandbox.FindRock(player.transform.position, 4f, 20f);
                ctx.Assert("rock_found", rock != null, "a vanilla rock", rock != null ? rock.transform.root.name : "none");
                if (rock == null) yield break;
                Bounds rb = rock.bounds;
                float r = MassMath.Radius(n);
                float reach = Mathf.Max(rb.extents.x, rb.extents.z);
                ctx.Log($"rock {rock.transform.root.name}: {rb.size.x:0.0} x {rb.size.y:0.0} x {rb.size.z:0.0} m; mass of {n} units, radius {r:0.00} m, {GpuMassBridge.WormsFor(n)} worms");

                Vector3 dir = player.transform.position - rb.center; dir.y = 0f;
                dir = dir.sqrMagnitude > 1e-3f ? dir.normalized : player.transform.forward;
                Vector3 side = Vector3.Cross(Vector3.up, dir);

                // ================================================================ 1. round the rock
                {
                    Vector3 playerAt = rb.center + dir * (reach + 3f);
                    playerAt.y = V14_GpuMassSandbox.GroundOf(playerAt, player.transform.position.y);
                    var tp = new TeleportOutcome();
                    yield return DevWorld.TeleportPlayer(playerAt + Vector3.up * 0.1f, Quaternion.LookRotation(-dir, Vector3.up), false, 30f, tp);
                    ctx.Assert("player_placed", tp.Ok, true, tp.Ok ? "ok" : tp.Error);
                    if (!tp.Ok) yield break;
                    player = Player.m_localPlayer;
                    player.SetGodMode(true);
                    S18_GroupCoherence.ClearCreatures(ctx, player);
                    yield return ctx.Wait(1f);

                    Vector3 start = rb.center - dir * (reach + r + 3.5f);
                    start.y = V14_GpuMassSandbox.GroundOf(start, player.transform.position.y);
                    mass = MassDev.SpawnAt(start, n, ZDOID.None, freezeAI: false);
                    ctx.Assert("mass_spawned", mass != null, "a mass behind the rock", mass != null);
                    if (mass == null) yield break;
                    WritheMassVisual visual = mass.GetComponent<WritheMassVisual>();
                    WritheMassAI ai = mass.GetComponent<WritheMassAI>();
                    yield return ctx.WaitUntil(() => visual.GpuDriven, 10f);
                    ctx.Assert("mass_drawn_by_gpu", visual.GpuDriven, "a GPU twin", visual.GpuDriven);
                    if (!visual.GpuDriven) yield break;

                    Vector3 camSide = V14_GpuMassSandbox.ClearSide(new[] { start, rb.center, playerAt }, side, Mathf.Max(4f, reach + 3f), 1.6f, rock, ctx);
                    float camDist = Mathf.Max(4f, reach + 3f);
                    DevCamera.Free(rb.center + camSide * camDist + Vector3.up * 1.6f, rb.center + Vector3.up * 0.5f);
                    DevCamera.Provider = new V14_GpuMassSandbox.FollowCam(() => mass != null ? Vector3.Lerp(mass.transform.position, rb.center, 0.5f) : rb.center, camSide, camDist, 1.6f, 0.4f).Pose;

                    // the AI hunts the player; when the rock hides him the dev goal (his position) takes over after 3 s so that the test measures the crossing, not the sight
                    float t0 = Time.time;
                    bool devGoal = false;
                    float maxNear = 0f, maxInside = 0f, minRockDistance = float.MaxValue;
                    int snapshots = 0;
                    bool reached = false, passShot = false;
                    float flatStop = r + Mathf.Max(0.3f, 0.6f);
                    while (Time.time - t0 < 60f && mass != null)
                    {
                        if (!devGoal && Time.time - t0 > 3f && !visual.HasTarget) { ai.DevGoal = player.transform.position; devGoal = true; ctx.Log("the rock hides the player: dev goal set on him"); }
                        float dRock = V14_GpuMassSandbox.HorizontalDistance(mass.transform.position, rb.center);
                        minRockDistance = Mathf.Min(minRockDistance, dRock);
                        float toPlayer = V14_GpuMassSandbox.HorizontalDistance(mass.transform.position, player.transform.position);
                        if (toPlayer <= flatStop + 0.4f || MassStateCodes.Mode(mass.StateCode) == MassStateCodes.Engulf) { reached = true; break; }
                        if (!passShot && dRock < reach + 0.6f * r)
                        {
                            passShot = true;
                            yield return ctx.Shot("round_the_rock_pass");      // the camera follows the mass: the mass against the flank of the rock
                        }
                        if (visual.Gpu != null && dRock < reach + r + 1.2f)
                        {
                            GpuMassDev.NodeSnapshot snap = GpuMassDev.RequestNodes(visual.Gpu);
                            float until = Time.time + 2f;
                            while (!snap.Done && Time.time < until) yield return null;
                            if (snap.Done && !snap.Error)
                            {
                                Measure(snap, rock, out float near, out float inside);
                                maxNear = Mathf.Max(maxNear, near);
                                maxInside = Mathf.Max(maxInside, inside);
                                snapshots++;
                            }
                        }
                        float next = Time.time + PollSeconds;
                        while (Time.time < next) yield return null;
                    }
                    ctx.Log($"round the rock: {snapshots} snapshots, nodes within 5 cm of the rock up to {maxNear:0.0} %, inside it up to {maxInside:0.00} %, closest approach of the centre {minRockDistance:0.0} m, reached the player {reached}");
                    ctx.Assert("round_snapshots", snapshots >= 3, ">= 3 snapshots of the nodes while it passes the rock", snapshots);
                    ctx.Assert("round_no_node_in_the_rock", maxInside < 2f, "< 2 % of the sampled nodes inside the rock", maxInside.ToString("0.00"));
                    ctx.Assert("round_nodes_against_the_rock", maxNear >= 1f, ">= 1 % of the sampled nodes within 5 cm of the rock while it goes round (measured 1.3 % in game: the mass flows past the flank, it is not pressed against it)", maxNear.ToString("0.0"));
                    ctx.Assert("round_reaches_the_player", reached, "the mass reaches the player (within its engulf range)", reached);
                    yield return V14_GpuMassSandbox.Shots(ctx, "round_end", () => mass != null ? mass.transform.position : rb.center, side, dir, 0.3f, rock);

                    // the film of the crossing: the same walk again, the camera follows the mass (the measures above could not run while the clip recorded)
                    if (mass != null) MassDev.Destroy(mass);
                    mass = MassDev.SpawnAt(start, n, ZDOID.None, freezeAI: false);
                    if (mass != null)
                    {
                        visual = mass.GetComponent<WritheMassVisual>();
                        ai = mass.GetComponent<WritheMassAI>();
                        ai.DevGoal = player.transform.position;
                        DevCamera.Free(rb.center + camSide * camDist + Vector3.up * 1.6f, rb.center + Vector3.up * 0.5f);
                        DevCamera.Provider = new V14_GpuMassSandbox.FollowCam(() => mass != null ? Vector3.Lerp(mass.transform.position, rb.center, 0.5f) : rb.center, camSide, camDist, 1.6f, 0.4f).Pose;
                        yield return ctx.WaitFrames(30);
                        yield return ctx.Clip("round_the_rock", 8f);
                    }
                    DevCamera.Provider = null;

                    // ================================================================ 3. engulfing the player (the mass is on him now)
                    // the engulf needs a mass that has reached the player: a fresh one, hunting him from where the rock was (the film above used the first)
                    if (mass != null) MassDev.Destroy(mass);
                    mass = MassDev.SpawnAt(player.transform.position + dir * (r + 1.0f), n, ZDOID.None, freezeAI: false);
                    if (mass != null)
                    {
                        visual = mass.GetComponent<WritheMassVisual>();
                        ai = mass.GetComponent<WritheMassAI>();
                        yield return Engulf(ctx, n, mass, visual, ai, player, side, dir);
                    }
                    if (mass != null) MassDev.Destroy(mass);
                    mass = null;
                }

                // ================================================================ 2. a low stone
                {
                    DevCamera.Follow();
                    player = Player.m_localPlayer;
                    S18_GroupCoherence.ClearCreatures(ctx, player);
                    Vector3 fwd = player.transform.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
                    float lowH = 0.5f * 0.45f * r;
                    Vector3 blockAt = player.transform.position + fwd * 7f;
                    placed = V14_GpuMassSandbox.PlaceBlock(blockAt, new Vector3(1.6f, lowH, 1.0f), "MW_V12_Low");
                    Collider stone = placed.GetComponent<Collider>();
                    Bounds sb = stone.bounds;
                    Vector3 along = (player.transform.position - sb.center); along.y = 0f; along = along.normalized;
                    Vector3 from = sb.center + along * (sb.extents.magnitude + r + 2.5f);
                    from.y = V14_GpuMassSandbox.GroundOf(from, player.transform.position.y);
                    Vector3 to = sb.center - along * (sb.extents.magnitude + 2f);
                    to.y = V14_GpuMassSandbox.GroundOf(to, from.y);
                    mass = MassDev.SpawnAt(from, n, ZDOID.None, freezeAI: false);
                    ctx.Assert("low_mass_spawned", mass != null, "a mass before the low stone", mass != null);
                    if (mass != null)
                    {
                        WritheMassVisual visual = mass.GetComponent<WritheMassVisual>();
                        WritheMassAI ai = mass.GetComponent<WritheMassAI>();
                        ai.DevGoal = to;
                        yield return ctx.WaitUntil(() => visual.GpuDriven, 10f);
                        Vector3 camSide = V14_GpuMassSandbox.ClearSide(new[] { from, sb.center, to }, Vector3.Cross(Vector3.up, along), 3.5f, 1.2f, stone, ctx);
                        DevCamera.Free(sb.center + camSide * 3.5f + Vector3.up * 1.2f, sb.center);
                        DevCamera.Provider = new V14_GpuMassSandbox.FollowCam(() => mass != null ? mass.transform.position : sb.center, camSide, 3.5f, 1.2f, 0.3f).Pose;
                        float t0 = Time.time;
                        float maxOn = 0f, maxInside = 0f, closest = float.MaxValue;
                        int snapshots = 0;
                        bool reachedEnd = false;
                        while (Time.time - t0 < 40f && mass != null)
                        {
                            float d = V14_GpuMassSandbox.HorizontalDistance(mass.transform.position, sb.center);
                            closest = Mathf.Min(closest, d);
                            if (V14_GpuMassSandbox.HorizontalDistance(mass.transform.position, to) < 0.7f) { reachedEnd = true; break; }
                            if (visual.Gpu != null && d < sb.extents.magnitude + r)
                            {
                                GpuMassDev.NodeSnapshot snap = GpuMassDev.RequestNodes(visual.Gpu);
                                float until = Time.time + 2f;
                                while (!snap.Done && Time.time < until) yield return null;
                                if (snap.Done && !snap.Error)
                                {
                                    Measure(snap, stone, out _, out float inside);
                                    int on = 0, total = 0;
                                    for (int i = 0; i < snap.Worms * 8; i++)
                                    {
                                        Vector3 p = new Vector3(snap.Nodes[i].x + snap.Origin.x, snap.Nodes[i].y + snap.Origin.y, snap.Nodes[i].z + snap.Origin.z);
                                        total++;
                                        if (p.x > sb.min.x && p.x < sb.max.x && p.z > sb.min.z && p.z < sb.max.z && p.y > sb.max.y - 0.002f) on++;
                                    }
                                    maxOn = Mathf.Max(maxOn, 100f * on / Mathf.Max(1, total));
                                    maxInside = Mathf.Max(maxInside, inside);
                                    snapshots++;
                                }
                            }
                            float next = Time.time + PollSeconds;
                            while (Time.time < next) yield return null;
                        }
                        DevCamera.Provider = null;
                        bool crossed = closest < 0.6f;
                        ctx.Log($"low stone ({sb.size.x:0.0} x {sb.size.y:0.00} x {sb.size.z:0.0} m): {snapshots} snapshots, up to {maxOn:0.0} % of the nodes over it, {maxInside:0.00} % inside it, closest approach of the centre {closest:0.0} m ({(crossed ? "went over" : "went round")})");
                        ctx.Assert("low_no_node_in_the_stone", maxInside < 2f, "< 2 % of the sampled nodes inside the stone", maxInside.ToString("0.00"));
                        if (crossed) ctx.Assert("low_nodes_over_the_stone", maxOn >= 8f, ">= 8 % of the nodes over the stone when the centre goes over it", maxOn.ToString("0.0"));
                        ctx.Assert("low_reaches_the_objective", reachedEnd, "the mass reaches the far side", reachedEnd);
                        yield return V14_GpuMassSandbox.Shots(ctx, "low_after", () => mass != null ? mass.transform.position : sb.center, Vector3.Cross(Vector3.up, along), along, 0.2f, stone);
                        if (mass != null) MassDev.Destroy(mass);
                        mass = MassDev.SpawnAt(from, n, ZDOID.None, freezeAI: false);
                        if (mass != null)
                        {
                            mass.GetComponent<WritheMassAI>().DevGoal = to;
                            DevCamera.Free(sb.center + camSide * 3.5f + Vector3.up * 1.2f, sb.center);
                            DevCamera.Provider = new V14_GpuMassSandbox.FollowCam(() => mass != null ? mass.transform.position : sb.center, camSide, 3.5f, 1.2f, 0.3f).Pose;
                            yield return ctx.WaitFrames(30);
                            yield return ctx.Clip("over_the_stone", 6f);
                            DevCamera.Provider = null;
                        }
                    }
                }
            }
            finally
            {
                DevCamera.Provider = null;
                DevCamera.Follow();
                if (mass != null) MassDev.Destroy(mass);
                if (placed != null) V14_GpuMassSandbox.DestroyPlaced(placed);
                if (clutterHidden && ClutterSystem.instance != null) ClutterSystem.instance.enabled = true;
                if (timeForced) ctx.Console($"{VanillaNames.CmdTimeOfDay} -1");
                ctx.Console("mw_kill");
            }
        }

        /// <summary>The mass has reached the player: it engulfs him, and the sleeve is followed every 0.5 s until it is steady.</summary>
        private static IEnumerator Engulf(ScenarioContext ctx, int n, WritheMassState mass, WritheMassVisual visual, WritheMassAI ai, Player player, Vector3 side, Vector3 fwd)
        {
            ai.DevGoal = null;
            yield return ctx.WaitUntil(() => mass == null || MassStateCodes.Mode(mass.StateCode) == MassStateCodes.Engulf, 30f, "engulf_begins");
            if (mass == null || MassStateCodes.Mode(mass.StateCode) != MassStateCodes.Engulf) yield break;
            float t0 = Time.time;
            yield return ctx.WaitUntil(() => visual.EngulfShown > 0.01f, 5f);
            t0 = Time.time;
            ctx.Assert("engulf_gpu_twin", visual.GpuDriven && visual.Gpu.Mode == GpuMassMode.Engulf, "the GPU twin wraps the player", visual.GpuDriven ? visual.Gpu.Mode.ToString() : "none");
            if (!visual.GpuDriven) yield break;
            Vector3 center = player.transform.position + Vector3.up * 0.9f;
            DevCamera.Free(center + side * 2.6f + Vector3.up * 0.4f, center);
            DevCamera.Provider = new V14_GpuMassSandbox.FollowCam(() => Player.m_localPlayer != null ? Player.m_localPlayer.transform.position + Vector3.up * 0.9f : center, side, 2.6f, 0.4f, 0f).Pose;

            var times = new List<float>();
            var settled = new List<int>();
            var anchors = 0;
            float end = t0 + 9f;
            while (Time.time < end && visual.Gpu != null)
            {
                GpuMassDev.NodeSnapshot snap = GpuMassDev.RequestNodes(visual.Gpu);
                float until = Time.time + 2f;
                while (!snap.Done && Time.time < until) yield return null;
                if (snap.Done && !snap.Error && snap.AnchorCount > 0)
                {
                    GpuSkinMetrics.Result sl = snap.SkinSleeve();
                    times.Add(Time.time - t0);
                    settled.Add(sl.Settled);
                    anchors = snap.AnchorCount;
                }
                float next = Time.time + SleeveSample;
                while (Time.time < next) yield return null;
            }
            ctx.Assert("engulf_anchors_on_the_player", anchors >= 300, ">= 300 skin anchors on the player", anchors);
            if (GpuMassBridge.Environment != null)
            {
                GpuSkinSampler skin = GpuMassBridge.Environment.Skin;
                ctx.Log($"skin: {skin.Description}");
                ctx.Log($"skin selection: {(skin.LastFromCache ? "from the cache" : "chosen on a worker thread")}; {skin.SpikeBreakdown}");
                if (skin.LastFromCache) ctx.Assert("engulf_cached_selection_spike", skin.SpikeCachedMs <= 2.0, "<= 2 ms at the start of an engulf whose anchors are in the cache", $"{skin.SpikeCachedMs:0.00} ms");
                else ctx.Assert("engulf_first_selection_spike", skin.SpikeUncachedMs <= 4.0, "<= 4 ms at the first engulf of a mesh", $"{skin.SpikeUncachedMs:0.00} ms");
            }
            if (times.Count >= 4)
            {
                int steady = 0;
                for (int i = times.Count / 2; i < times.Count; i++) steady = Mathf.Max(steady, settled[i]);
                float formed = -1f;
                for (int i = 0; i < times.Count; i++) if (settled[i] >= 0.8f * steady) { formed = times[i]; break; }
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < times.Count; i++) sb.Append($" t={times[i]:0.0}s:{settled[i]}");
                ctx.Log($"sleeve formation (worms settled on the body, from the start of the engulf):{sb}; steady state {steady}; 80 % reached at {formed:0.0} s");
                ctx.Assert("engulf_sleeve_has_worms", steady >= 0.15f * GpuMassBridge.WormsFor(n), ">= 15 % of the worms settled on the body", $"{steady} of {GpuMassBridge.WormsFor(n)}");
                ctx.Assert("engulf_sleeve_formed_in_time", formed >= 0f && formed <= FormedBudget, $"80 % of the steady state within {FormedBudget} s of the start of the engulf", formed.ToString("0.0"));
            }
            else ctx.Assert("engulf_sleeve_samples", false, ">= 4 snapshots of the sleeve", times.Count);
            yield return ctx.Clip("engulf_player", 5f);       // the steady sleeve (the formation is measured above, the readbacks cannot run while a clip records)
            DevCamera.Provider = null;
            yield return V14_GpuMassSandbox.Shots(ctx, "engulf", () => Player.m_localPlayer != null ? Player.m_localPlayer.transform.position + Vector3.up * 0.9f : center, side, fwd, 0f);
        }

        /// <summary>Share (percent) of the nodes of a snapshot within 5 cm of a collider and inside it (every 7th node).</summary>
        private static void Measure(GpuMassDev.NodeSnapshot snap, Collider col, out float near, out float inside)
        {
            int mask = LayerMask.GetMask(VanillaNames.LayerDefault, VanillaNames.LayerStaticSolid, VanillaNames.LayerDefaultSmall, VanillaNames.LayerPiece);
            int sampled = 0, nearCount = 0, insideCount = 0;
            for (int i = 0; i < snap.Worms * 8; i += 7)
            {
                Vector3 p = new Vector3(snap.Nodes[i].x + snap.Origin.x, snap.Nodes[i].y + snap.Origin.y, snap.Nodes[i].z + snap.Origin.z);
                sampled++;
                if (Physics.CheckSphere(p, 0.004f, mask, QueryTriggerInteraction.Ignore)) insideCount++;
                foreach (Collider c in Physics.OverlapSphere(p, 0.05f, mask, QueryTriggerInteraction.Ignore))
                    if (c == col) { nearCount++; break; }
            }
            near = 100f * nearCount / Mathf.Max(1, sampled);
            inside = 100f * insideCount / Mathf.Max(1, sampled);
        }
    }
}
#endif
