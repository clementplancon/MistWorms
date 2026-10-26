#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using MistWorms.Colony;
using MistWorms.Dev.Preview;
using MistWorms.Render.Gpu;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// V14 (visual v2, pass B1): sandbox of the GPU worm masses, with the phantom mass (<see cref="GpuMassDev"/>) and nothing of the game logic. Open ground, vanilla creatures
    /// removed within 40 m, the player in god mode. First the parity of the compute simulation with the CPU reference (64 worms, 30 steps, 1 mm). Then, for a mass of 50 and
    /// one of 200 game units: at rest, walking 6 m, in contact with a vanilla rock (or a stone piece when none is near), wrapping the player. Captures from 1, 3 and 10 m
    /// for each, and a short clip of the walking mass (50). Assertions: main thread budget 0.25 ms/frame (mean over 300 frames, environment capture, upload and draw
    /// submission included), no managed allocation in steady state, no node under the ground the simulation used, no node inside the obstacle, parity within a millimetre,
    /// and the wrapped capsule covered over at least 60 % of its height times the engulf parameter. The visual judgement is made on the captures.
    /// Run it with tools/build.ps1 -Configuration Profile. Parameters: frames (scale of the measured windows, default 1), engulf (target fraction, default 1).
    ///
    /// Reproducibility (D-V14, no threshold was relaxed to get it). Three runs of the same code failed on three different assertions, each at a limit value; what varied from run to run was
    /// the setting, not the code. So: (1) one fixed site: the last good site of the session is forgotten and the search starts from the start temple of the world, the player facing a
    /// fixed way, and the compare and rock sites are searched again from that same site; (2) the block of the low obstacle is turned toward the approach and the mass is walked exactly
    /// to its middle; (3) every wait of the simulation is a number of steps (<see cref="GpuWormHost.StepIndex"/>, 1/60 s each) and not seconds or frames; (4) the measures that one window
    /// leaves noisy (volume of the mass at rest, nodes on the flanks of an obstacle, first selection of the anchors) are the median of several windows, the safety checks (no node under the
    /// ground or inside an obstacle) the worst of them; (5) the first selection of the anchors is measured after a warm-up pass: the very first pass of a session pays the JIT of the
    /// selection code and the first dispatch of the skinning kernel, which is neither the cost of an engulf nor what the 4 ms budget is about (the budget is the main thread cost of reading
    /// the meshes of a target seen for the first time and of taking the result); the warm-up pass is logged, not judged. Captures are framed on the subject (40 to 60 % of the height of the
    /// image, <see cref="ScenarioContext.FitFlat"/>), not from fixed distances.
    /// </summary>
    internal sealed class V14_GpuMassSandbox : IDevScenario
    {
        private static readonly int[] Counts = { 50, 200 };
        // legacy captures (V12, V15 still call Shots with a distance list): 1, 3 and 10 m
        private static readonly float[] Distances = { 1f, 3f, 10f };
        private static readonly float[] Heights = { 0.35f, 0.9f, 2.4f };
        // framed captures: close (60 % of the height of the image, low), fit (50 %) and wide (40 %, high)
        private static readonly float[] ShotFractions = { 0.6f, 0.5f, 0.4f };
        private static readonly float[] ShotElevations = { 12f, 25f, 40f };
        private static readonly string[] ShotNames = { "close", "fit", "wide" };
        private const int Warm = 120;                      // simulation steps (2 s of the colony): what a mass needs to settle after it is created or stopped
        private const int Window = 300;                    // frames, for the main thread cost (a cost per frame is measured in frames)
        private const int RestSettleSteps = 240;           // a mass at rest: 4 s of simulation before the first look
        private const int Windows = 5, WindowGapSteps = 30;   // median of 5 snapshots, half a second of simulation apart
        private const int SpikeRuns = 3;                   // measured passes of the first selection (after one warm-up pass)
        private static readonly uint[] FormationMarks = { 60, 90, 120, 150, 180, 210 };      // steps (1 s to 3.5 s) at which the sleeve is read back
        private const float FormedLimit = 3.5f;            // seconds: 80 % of the steady state in game (2.5 s in the editor check, SimCheck)
        private const double BudgetMs = 0.25;
        private const float WalkDistance = 6f;
        internal const float WaterClearance = 5f;           // no water (sea, lake, river) within this distance of a site
        internal const float BuiltClearance = 15f;          // no built piece (WearNTear) this close to a site: the captures are not made through beams

        public string Name => "V14_GpuMassSandbox";

        public IEnumerator Run(ScenarioContext ctx)
        {
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 900f;
            float scale = ctx.ParamFloat("frames", 1f);
            float engulf = Mathf.Clamp01(ctx.ParamFloat("engulf", 1f));
            int window = Math.Max(30, (int)(Window * scale));
            bool timeForced = false, clutterHidden = false, frozen = false;
            GameObject placed = null;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                bool host = GpuWormBundle.InitHost();
                ctx.Assert("gpu_host_ready", host, true, host ? "ready" : (GpuWormHost.Error ?? GpuWormBundle.Error));
                if (!host) yield break;
                ctx.Log(IsOptimized()
                    ? "build: optimized (Profile or Release): the timings are valid for the budgets"
                    : "WARNING: this build is NOT optimized (Debug): the timings are not valid for the budget; build with tools/build.ps1 -Configuration Profile");

                ctx.Console("mw_kill");
                GpuMassDev.Clear();
                DevPreview.Instance.Clear();
                // no tall grass to hide the worms
                if (ClutterSystem.instance != null)
                {
                    ClutterSystem.instance.enabled = false;
                    ClutterSystem.instance.ClearAll();
                    clutterHidden = true;
                }
                Player player = Player.m_localPlayer;
                player.SetGodMode(true);
                yield return ctx.WaitFrames(3);
                // flat, open ground, more than 1 m above the water, nothing but terrain within 25 m. The same site on every run: the last good site of the session is forgotten and the
                // search starts from the start temple, the player facing north (the heading of every mass and of the approach to the obstacles comes from it)
                DevWorld.ResetLastGoodSite();
                yield return GoToFixedOrigin(ctx);
                yield return ctx.MoveToOpenGround(25f, p => !NearBuilt(p, BuiltClearance) && !NearWater(p, WaterClearance));
                if (!ctx.LastOpenGround.Found) yield break;
                Vector3 fixedSite = ctx.LastOpenGround.Spot;
                ctx.Log($"fixed site {fixedSite.ToString("0.0")} (search from the start temple, level within {ctx.LastOpenGround.Deviation:0.00} m)");
                player = Player.m_localPlayer;
                player.SetGodMode(true);
                ctx.Console($"{VanillaNames.CmdTimeOfDay} 0.5");
                timeForced = true;
                S18_GroupCoherence.ClearCreatures(ctx, player);

                // ---------------------------------------------------------------- parity with the CPU reference (in the real scenery)
                var parity = new GpuMassDev.ParityResult();
                yield return GpuMassDev.RunParity(parity);
                ctx.Assert("parity_ran", parity.Error == null, "no error", parity.Error ?? "ok");
                if (parity.Error == null)
                {
                    ctx.Log($"parity stages: {parity.Stages}; environment: {parity.Env}");
                    ctx.Log($"parity: {parity.Worms} worms, {parity.Steps} steps: init {parity.InitMm:0.0000} mm, steps {parity.MaxMm:0.0000} mm");
                    ctx.Assert("parity_init_within_1mm", parity.InitMm <= 1f, "<= 1 mm", parity.InitMm.ToString("0.0000"));
                    ctx.Assert("parity_30_steps_within_1mm", parity.MaxMm <= 1f, "<= 1 mm", parity.MaxMm.ToString("0.0000"));
                }

                Vector3 fwd = player.transform.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
                Vector3 side = Vector3.Cross(Vector3.up, fwd);
                ctx.Log($"heading {fwd.ToString("0.00")}");

                // ---------------------------------------------------------------- first selection of the anchors, median of SpikeRuns passes after a warm-up pass
                yield return SelectionSpike(ctx);
                player = Player.m_localPlayer;
                if (player == null) yield break;

                foreach (int n in Counts)
                {
                    string tag = "n" + n;
                    float r = GpuMassDev.RadiusOf(n);
                    float domeH = 0.45f * r;

                    // ---------------------------------------------------------------- at rest
                    S18_GroupCoherence.ClearCreatures(ctx, player);
                    ctx.Console($"mw_gpumass {n} still");
                    ctx.Assert($"{tag}_rest_spawned", GpuMassDev.Mass != null, "a phantom mass", GpuMassDev.Mass != null);
                    if (GpuMassDev.Mass == null) yield break;
                    yield return ctx.WaitSteps(RestSettleSteps);
                    yield return FramedShots(ctx, $"{tag}_rest", () => GpuMassDev.Mass.Center, side, fwd, 0.3f * r, r, domeH);
                    yield return Measure(ctx, $"{tag}_rest", window);
                    yield return CheckGround(ctx, $"{tag}_rest");
                    yield return CheckVolume(ctx, $"{tag}_rest", r);

                    // ---------------------------------------------------------------- walking 6 m (the clip films the walk, the camera follows the mass)
                    ctx.Console($"mw_gpumass {n} walk {WalkDistance.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}");
                    GpuWormHost.ResetStats();
                    // the camera follows the mass framed on it (the radius of the subject is the mass, the walk is 6 m: the mass stays the subject)
                    float walkD = ScenarioContext.FitFlat(r, domeH, 20f);
                    ScenarioContext.Orbit(walkD, 20f, out float walkH, out float walkUp);
                    Vector3 walkSide = ClearSide(new[] { GpuMassDev.Mass.Center, GpuMassDev.WalkTarget }, side, walkH, walkUp, null, ctx);
                    DevCamera.Free(GpuMassDev.Mass.Center + walkSide * walkH + Vector3.up * walkUp, GpuMassDev.Mass.Center);
                    DevCamera.Provider = new FollowCam(() => GpuMassDev.Mass != null ? GpuMassDev.Mass.Center : Vector3.zero, walkSide, walkH, walkUp, 0.25f).Pose;
                    if (n == 50 || n == 200) yield return ctx.Clip($"walk_{tag}", 5f);
                    yield return ctx.WaitUntil(() => GpuMassDev.Arrived, 30f);
                    ctx.Assert($"{tag}_walk_arrived", GpuMassDev.Arrived, "arrived within 30 s", GpuMassDev.Arrived ? "yes" : "no");
                    double walkMs = GpuWormHost.AvgMs;
                    ctx.Log($"{tag} walking: main thread {walkMs:0.0000} ms/frame mean over {GpuWormHost.Frames} frames, max {GpuWormHost.MaxMs:0.000}; sub-steps last frame {GpuWormHost.LastSubSteps}");
                    ctx.Assert($"{tag}_walk_main_thread_budget", walkMs <= BudgetMs, $"<= {BudgetMs} ms/frame (mean)", walkMs.ToString("0.0000"));
                    DevCamera.Provider = null;
                    yield return ctx.WaitSteps(Warm);
                    yield return FramedShots(ctx, $"{tag}_walk", () => GpuMassDev.Mass.Center, side, fwd, 0.3f * r, r, domeH);
                    yield return CheckGround(ctx, $"{tag}_walk");

                    // ---------------------------------------------------------------- wrapping the player
                    DevCamera.Follow();
                    S18_GroupCoherence.ClearCreatures(ctx, player);
                    GpuMassDev.EngulfTarget = engulf;
                    // the selection of the anchors starts again at every engulf, from the cache the warm-up filled: its spike is the one of an engulf of a target already known
                    if (GpuMassDev.Env != null) GpuMassDev.Env.DevForgetSkins();
                    ctx.Console($"mw_gpumass {n} engulfplayer");
                    ctx.Assert($"{tag}_engulf_spawned", GpuMassDev.Mass != null && GpuMassDev.Target != null, "a phantom mass and the player capsule", GpuMassDev.Mass != null);
                    if (GpuMassDev.Mass == null || GpuMassDev.Target == null) yield break;
                    uint step0 = GpuWormHost.StepIndex;
                    var formTimes = new List<float>();
                    var formSettled = new List<int>();
                    GpuMassDev.NodeSnapshot early = null;
                    foreach (uint mark in FormationMarks)
                    {
                        yield return ctx.WaitUntil(() => GpuWormHost.StepIndex - step0 >= mark, 20f);       // the sleeve is read back at 1, 1.5, 2, 2.5, 3 and 3.5 s (of simulation) after the start of the engulf
                        early = GpuMassDev.RequestNodes(GpuMassDev.Mass);
                        yield return ctx.WaitUntil(() => early.Done, 10f);
                        if (early.Done && !early.Error && early.AnchorCount > 0) { formTimes.Add((GpuWormHost.StepIndex - step0) * WormSimConst.Dt); formSettled.Add(early.SkinSleeve().Settled); }
                    }
                    ctx.Log($"{tag} engulf: {GpuMassDev.Mass.WormCount} worms around the player; skin anchors {GpuMassDev.Mass.AnchorCount} ({GpuMassDev.Env.Skin.Description})");
                    ctx.Assert($"{tag}_skin_anchors_active", GpuMassDev.Mass.AnchorCount > 0, "anchors on the baked surface of the player", GpuMassDev.Mass.AnchorCount);
                    ctx.Assert($"{tag}_skin_anchor_count", GpuMassDev.Mass.AnchorCount >= 300, ">= 300 anchors on a player", GpuMassDev.Mass.AnchorCount);
                    // the player walks slowly, in the look direction, once the formation is measured: the anchors follow the animation. Not before: a body that walks away at 1.5 m/s leaves the pile behind
                    // (the pile follows its centre at 0.6 m/s), which slowed the formation to 67 % of the editor's in game; the moving body is checked by SimCheck and from here on
                    DevInput.Set(new Vector3(0f, 0f, 0.3f));
                    int formedSettled = formSettled.Count > 0 ? formSettled[formSettled.Count - 1] : 0;
                    {
                        // the anchors are skinned on the GPU: read back to be measured
                        Bounds cap = GpuMassDev.Target.bounds;
                        early.AnchorExtent(out float aMinY, out float aMaxY, out Vector2 aCentre);
                        float anchorHeight = aMaxY - aMinY;
                        float offCentre = Vector2.Distance(aCentre, new Vector2(cap.center.x, cap.center.z));
                        ctx.Log($"{tag} anchors: height {anchorHeight:0.00} m for a body of {cap.size.y:0.00} m, from {aMinY:0.00} to {aMaxY:0.00} (capsule {cap.min.y:0.00} to {cap.max.y:0.00}), centre {offCentre * 100f:0} cm from the centre of the capsule");
                        ctx.Assert($"{tag}_anchors_height_is_the_height_of_the_player", Mathf.Abs(anchorHeight - cap.size.y) <= 0.1f * cap.size.y, $"height of the anchors = {cap.size.y:0.00} m +- 10%", anchorHeight.ToString("0.00"));
                        ctx.Assert($"{tag}_anchors_centred_on_the_player", offCentre <= 0.15f, "centre of the anchors within 15 cm of the centre of the capsule", $"{offCentre * 100f:0} cm");
                    }
                    ctx.Assert($"{tag}_skin_area", GpuMassDev.Env.Skin.MeshArea >= 1f && GpuMassDev.Mass.SurfaceArea >= 1f, "baked mesh area and surface the anchors stand for >= 1 m2", $"{GpuMassDev.Env.Skin.MeshArea:0.00} m2 / {GpuMassDev.Mass.SurfaceArea:0.00} m2");
                    // the subject of the captures of an engulf is the player and the sleeve of worms on him: a body of 1.8 m with the pile of the mass at its feet
                    float bodyR = Mathf.Max(r, 0.35f);
                    float bodyH = Mathf.Max(GpuMassDev.Target.bounds.size.y, 1.2f);
                    if (n == 200)
                    {
                        // the clip starts with the mass: it films the rise around the player
                        float engulfD = ScenarioContext.FitFlat(bodyR, bodyH, 10f);
                        ScenarioContext.Orbit(engulfD, 10f, out float engulfH, out float engulfUp);
                        Vector3 engulfSide = ClearSide(new[] { GpuMassDev.Target.bounds.center }, side, engulfH, engulfUp, null, ctx);
                        DevCamera.Free(GpuMassDev.Target.bounds.center + engulfSide * engulfH + Vector3.up * engulfUp, GpuMassDev.Target.bounds.center);
                        DevCamera.Provider = new FollowCam(() => GpuMassDev.Target != null ? GpuMassDev.Target.bounds.center : Vector3.zero, engulfSide, engulfH, engulfUp, 0f).Pose;
                        yield return ctx.Clip($"engulf_{tag}", 5f);
                        DevCamera.Provider = null;
                        yield return ctx.WaitSteps(3 * 60);
                    }
                    else yield return ctx.WaitSteps((int)((GpuMassDev.EngulfRamp + 4f) * 60f));
                    yield return FramedShots(ctx, $"{tag}_engulf", () => GpuMassDev.Target.bounds.center, side, fwd, 0f, bodyR, bodyH);
                    // the cost is measured with the camera at 1.5 m (not a capture: a fixed distance, LOD0 and the shadows are what is paid for)
                    {
                        Vector3 cc = GpuMassDev.Target.bounds.center;
                        DevCamera.Free(cc + ClearSide(new[] { cc }, side, 1.5f, 0.5f, null, ctx) * 1.5f + Vector3.up * 0.5f, cc);
                        DevCamera.Provider = new FollowCam(() => GpuMassDev.Target != null ? GpuMassDev.Target.bounds.center : Vector3.zero, side, 1.5f, 0.5f, 0f).Pose;
                    }
                    yield return ctx.WaitFrames(20);
                    GpuMassDev.Env.Skin.ResetStats();
                    GpuWormHost.ResetStats();
                    yield return ctx.WaitFrames(window);
                    CheckLod(ctx, $"{tag}_engulf_measure");
                    {
                        double skinMs = GpuMassDev.Env.Skin.TotalMs / Math.Max(1, GpuWormHost.Frames);
                        ctx.Log($"{tag} skin sampling: {GpuMassDev.Env.Skin.Samples} samples, {skinMs:0.0000} ms/frame mean, {GpuMassDev.Env.Skin.MaxMs:0.000} ms at most for one sample");
                        ctx.Assert($"{tag}_skin_sampling_budget", skinMs <= 0.05, "<= 0.05 ms/frame (mean)", skinMs.ToString("0.0000"));
                        {
                            GpuSkinSampler sk = GpuMassDev.Env.Skin;
                            ctx.Log($"{tag} selection of the anchors: {(sk.LastFromCache ? "from the cache" : "chosen on a worker thread")}; {sk.SpikeBreakdown}; meshes cached {GpuSkinSampler.CachedMeshes}, sets cached {GpuSkinSampler.CachedSets}");
                            // two distinct budgets: the first engulf of a mesh (meshes read, anchors chosen on the worker, result taken) 4 ms, judged once, on the median of the passes of SelectionSpike
                            // (the thresholds are the same as before); an engulf with the anchors in the cache 2 ms, judged here at every engulf (the warm-up filled the cache, the sampler was made to forget)
                            ctx.Assert($"{tag}_skin_selection_from_the_cache", sk.LastFromCache, "the selection of an engulf after the warm-up comes from the cache", sk.LastFromCache ? "from the cache" : "chosen again");
                            ctx.Assert($"{tag}_skin_selection_cached_spike", sk.SpikeCachedMs <= 2.0, "<= 2 ms at the start of an engulf whose anchors are in the cache", $"{sk.SpikeCachedMs:0.00} ms");
                        }
                    }
                    double engulfMs = GpuWormHost.AvgMs;
                    ctx.Log($"{tag} wrapping the player: main thread {engulfMs:0.0000} ms/frame mean, max {GpuWormHost.MaxMs:0.000}; {GpuWormHost.DrawnWorms} worms drawn, {GpuWormHost.Triangles} triangles (+{GpuWormHost.ShadowTriangles} shadow), {GpuWormHost.DrawCalls} draw calls");
                    ctx.Assert($"{tag}_engulf_main_thread_budget", engulfMs <= BudgetMs, $"<= {BudgetMs} ms/frame (mean)", engulfMs.ToString("0.0000"));

                    // zero allocation in steady state: the heap rises seen around the host's own Tick (environment capture included)
                    // A first window absorbs what the runtime does once (first call of a generic, a lazily built table): it is logged. The assertion is on the second window,
                    // and the heap is read around the host's own Tick only, so what other threads allocate in between still counts: the window is long enough to show a per frame leak.
                    GpuWormHost.ResetStats();
                    GpuWormHost.MeasureAlloc = true;
                    yield return ctx.WaitFrames(120);
                    GpuWormHost.MeasureAlloc = false;
                    ctx.Log($"{tag} allocation, first window (one-off costs included): {GpuWormHost.OwnAllocBytes} bytes over {GpuWormHost.Frames} frames");
                    GpuWormHost.ResetStats();
                    GpuWormHost.MeasureAlloc = true;
                    yield return ctx.WaitFrames(window);
                    GpuWormHost.MeasureAlloc = false;
                    ctx.Assert($"{tag}_no_allocation_in_steady_state", GpuWormHost.OwnAllocBytes == 0, "0 bytes over the window", $"{GpuWormHost.OwnAllocBytes} bytes over {GpuWormHost.Frames} frames");

                    // The player stops and the sleeve settles for 2 s before the final readback: a worm within 4 cm of a leg that walks is a matter of the phase of the stride and of the speed the
                    // ground allows (r7 71 %, r8 52 % of the n50 sleeve on two sites, 81 % earlier; the left leg alone held 101 of 246 worms in one and 59 of 242 in the other).
                    DevInput.Release();
                    yield return ctx.WaitSteps(Warm);
                    GpuMassDev.NodeSnapshot nodes = GpuMassDev.RequestNodes(GpuMassDev.Mass);
                    yield return ctx.WaitUntil(() => nodes.Done, 10f);
                    ctx.Assert($"{tag}_readback_ok", nodes.Done && !nodes.Error, "nodes read back", nodes.Done ? (nodes.Error ? "error" : "ok") : "timed out");
                    if (nodes.Done && !nodes.Error)
                    {
                        Bounds cb = GpuMassDev.Target.bounds;
                        bool skin = nodes.AnchorCount > 0;
                        float coverage = skin ? nodes.CoverageSkin(cb.min.y, cb.size.y, out int attached) : nodes.Coverage(cb.min.y, cb.size.y, out attached);
                        // the sleeve fills from the bottom up: when the worms run short the covered height is lower than engulf x h
                        float covered = skin ? Mathf.Min(GpuMassDev.Mass.Engulf, nodes.Plan.TopRel) : GpuMassDev.Mass.Engulf;
                        float required = 0.6f * covered;
                        ctx.Log($"{tag} wrapped body {cb.size.y:0.00} m: coverage {coverage * 100f:0}% of the height, {attached} nodes on the {(skin ? "baked surface (" + nodes.AnchorCount + " anchors)" : "adhesive capsules")}, engulf {GpuMassDev.Mass.Engulf:0.00}, covered height {covered:0.00}");
                        ctx.Assert($"{tag}_body_coverage", coverage >= required, $">= {required * 100f:0}% of the height (60% x the covered height)", $"{coverage * 100f:0}%");
                        if (skin)
                        {
                            GpuSleevePlan plan = nodes.Plan;
                            float planShare = plan.Count / nodes.Worms;
                            GpuSkinMetrics.Result sl = nodes.SkinSleeve();
                            ctx.Log($"{tag} sleeve: S = {plan.Count:0} of {nodes.Worms} worms ({planShare * 100f:0}%) over {plan.Cover} of {nodes.AnchorCount} anchors up to {plan.TopRel * 100f:0}% of the height ({plan.PerAnchor:0.00} worms per anchor, anchors stand for {GpuMassDev.Mass.SurfaceArea:0.00} m2); " +
                                    $"now pile {sl.Pile}, transit {sl.Transit}, settled {sl.Settled}, falling {sl.Falling}");
                            ctx.Log($"{tag} sleeve worms: {sl.Settled} settled on the body, {sl.Near} with {GpuSkinMetrics.NearNodes} of 8 nodes within {GpuSkinMetrics.NearPlane * 100f:0} cm of the surface ({sl.NearShare * 100f:0}%); " +
                                    $"left leg {sl.NearRegion[0]}, right leg {sl.NearRegion[1]}, left arm {sl.NearRegion[2]}, right arm {sl.NearRegion[3]}, torso and head {sl.NearRegion[4]}");
                            if (n == 200 && engulf >= 0.99f)
                                ctx.Assert($"{tag}_sleeve_size", planShare >= 0.4f, ">= 40% of the worms in the sleeve (S >= 0.4 V)", $"{planShare * 100f:0}% ({plan.Count:0} of {nodes.Worms})");
                            {
                                float formedAt = -1f;
                                var line = new System.Text.StringBuilder();
                                for (int f = 0; f < formTimes.Count; f++)
                                {
                                    line.Append($" {formTimes[f]:0.0}s:{formSettled[f]}");
                                    if (formedAt < 0f && sl.Settled > 0 && formSettled[f] >= 0.8f * sl.Settled) formedAt = formTimes[f];
                                }
                                ctx.Log($"{tag} sleeve formation (worms settled, from the start of the engulf):{line}; steady state {sl.Settled}; 80 % reached at {(formedAt >= 0f ? formedAt.ToString("0.0") + " s" : "never within " + FormedLimit + " s")}");
                                ctx.Assert($"{tag}_sleeve_formed_in_3.5s", formedAt >= 0f && formedAt <= FormedLimit + 0.15f, $">= 80% of the settled worms of the steady state are settled {FormedLimit} s after the start of the engulf", $"{formedSettled} against {sl.Settled}, 80 % at {(formedAt >= 0f ? formedAt.ToString("0.0") : "never")} s");
                            }
                            ctx.Assert($"{tag}_sleeve_filled", sl.Settled + sl.Transit >= 0.5f * plan.Count, ">= 50% of the planned worms are on the body (holes, falls and the ragged edge take the rest)", $"{sl.Settled + sl.Transit} of {plan.Count:0}");
                            ctx.Assert($"{tag}_sleeve_worms_on_the_surface", sl.Settled > 0 && sl.NearShare >= 0.7f, ">= 70% of the sleeve worms within 4 cm of the skin along their length", $"{sl.NearShare * 100f:0}% of {sl.Settled}");
                            // legs: 3% each. Arms (n200 only: 50 units do not reach them): 2% each. An arm is 3 to 6% of the surface of a body, and only the part of it more than 19 cm from the axis
                            // counts as an arm (the rest is the torso), which leaves about 60% of its anchors.
                            string[] names = { "left leg", "right leg", "left arm", "right arm" };
                            for (int g = 0; g < 4; g++)
                            {
                                if (g >= 2 && n < 200) continue;
                                float share = g < 2 ? 0.03f : 0.02f;
                                ctx.Assert($"{tag}_worms_on_{names[g].Replace(' ', '_')}", sl.Near > 0 && sl.NearRegion[g] >= share * sl.Near, $">= {share * 100f:0}% of the worms on the skin", $"{sl.NearRegion[g]} of {sl.Near}");
                            }
                        }
                        int below;
                        float lowest = nodes.LowestAboveGround(out below);
                        ctx.Assert($"{tag}_engulf_no_node_under_ground", below == 0, "no node under the ground", $"lowest margin {lowest * 1000f:0.0} mm, {below} below");
                    }
                    DevInput.Release();
                    DevCamera.Provider = null;
                    GpuMassDev.Clear();
                    yield return ctx.WaitFrames(10);
                }

                // ---------------------------------------------------------------- GPU worms next to a flipbook worm, same light, same frame (white balance of the rope shader)
                // The player has walked while wrapped (and may stand on a slope, in the water or against a tree): back to open ground first, then the first free direction.
                DevCamera.Follow();
                // back to the fixed site (the player has walked while wrapped: where he stands is not the same on every run)
                yield return ReturnToSite(ctx, fixedSite);
                ctx.Log("compare site: the fixed site " + fixedSite.ToString("0.0"));
                player = Player.m_localPlayer;
                if (player == null) yield break;
                player.SetGodMode(true);
                S18_GroupCoherence.ClearCreatures(ctx, player);
                {
                    int slot = -1;
                    WormColony colony = null;
                    Vector3 mid = Vector3.zero, cmpFwd = fwd, cmpSide = side;
                    var reasons = new System.Text.StringBuilder();
                    foreach (float angle in Angles)
                    {
                        Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * fwd;
                        Vector3 sd = Vector3.Cross(Vector3.up, dir);
                        mid = player.transform.position + dir * 3f;
                        Vector3 spot = mid - sd * 0.35f;
                        bool ground = Physics.Raycast(spot + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore);
                        slot = ground ? ColonyDev.SpawnAt(spot, WormState.Idle, true, out colony) : -1;
                        if (slot >= 0) { cmpFwd = dir; cmpSide = sd; break; }
                        reasons.Append(ground ? $" [{angle:0}: ground {hit.collider.name} but no slot (colony {(colony != null ? colony.Kind.ToString() : "null")}, store {(colony != null && colony.Store != null ? colony.Store.AliveCount.ToString() : "none")})]" : $" [{angle:0}: no ground within 4 m under {spot.ToString("0.0")}]");
                    }
                    if (slot < 0) ctx.Log("compare_flipbook: no worm could be placed:" + reasons);
                    ctx.Assert("compare_flipbook_spawned", slot >= 0, "a flipbook worm on the ground", slot);
                    if (slot >= 0)
                    {
                        fwd = cmpFwd;
                        side = cmpSide;
                        ctx.Console("mw_freeze on");
                        frozen = true;
                        Vector3 flipPos = colony.Store.pos[slot];
                        GpuMassDev.Spawn(1, GpuMassDev.GhostMode.Still, 0f, out string how, new Vector3(mid.x + side.x * 0.35f, flipPos.y, mid.z + side.z * 0.35f));
                        yield return ctx.WaitSteps(Warm * 2);
                        Vector3 target = flipPos + side * 0.35f + Vector3.up * 0.02f;
                        // the camera looks across the pair, from the side the line of the pair is seen at 90 degrees; another angle when something is in the way.
                        // The pair is 0.7 m wide and low: framed at 50 % then 60 % of the height of the image (a worm is 0.3 m long)
                        float pairD = ScenarioContext.FitFlat(0.5f, 0.1f, 25f, 0.5f);
                        ScenarioContext.Orbit(pairD, 25f, out float pairH, out float pairUp);
                        Vector3 across = ClearSide(new[] { target }, -fwd, pairH, pairUp, null, ctx);
                        DevCamera.Free(target + across * pairH + Vector3.up * pairUp, target);
                        yield return ctx.WaitFrames(20);
                        yield return ctx.Shot("compare_flipbook_left_gpu_right_fit");
                        float pairD2 = ScenarioContext.FitFlat(0.5f, 0.1f, 15f, 0.6f);
                        ScenarioContext.Orbit(pairD2, 15f, out float pairH2, out float pairUp2);
                        DevCamera.Free(target + across * pairH2 + Vector3.up * pairUp2, target);
                        yield return ctx.WaitFrames(20);
                        yield return ctx.Shot("compare_flipbook_left_gpu_right_close");
                        ctx.Console("mw_freeze off");
                        frozen = false;
                        ctx.Console("mw_kill");
                        GpuMassDev.Clear();
                    }
                }
                DevCamera.Follow();
                bool onRockSite = false;
                var rockSite = new OpenGroundResult();
                // the rock site is searched from the fixed site, with nothing remembered: the same rock on every run
                yield return ReturnToSite(ctx, fixedSite);
                DevWorld.ResetLastGoodSite();
                yield return DevWorld.MoveToOpenGround(3f, p => !NearBuilt(p, BuiltClearance) && !NearWater(p, WaterClearance) && FindRock(p, 6f, 16f) != null, rockSite, ctx.Log);
                ctx.Assert("rock_site_found", rockSite.Found, "flat ground with a vanilla rock 6 to 16 m away", rockSite.Found ? rockSite.Spot.ToString() : rockSite.Error);
                onRockSite = rockSite.Found;
                player = Player.m_localPlayer;
                if (player == null) yield break;
                player.SetGodMode(true);
                S18_GroupCoherence.ClearCreatures(ctx, player);
                fwd = player.transform.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;

                foreach (int n in Counts)
                {
                    string tag = "n" + n;
                    float r = GpuMassDev.RadiusOf(n);

                    // a low obstacle: a flat block lower than 0.6 x the height of the dome of the mass, which the mass covers. The block is turned toward the approach (its 1.6 m face
                    // across the way of the mass, its 1 m depth along it), so that the crossing does not depend on how a block aligned on the axes of the world meets the heading
                    float lowH = 0.5f * 0.45f * r;
                    placed = PlaceBlock(player.transform.position + fwd * 7f, new Vector3(1.6f, lowH, 1.0f), "MW_V14_Low", Quaternion.LookRotation(fwd, Vector3.up));
                    yield return CrossObstacle(ctx, player, fwd, tag + "_low", n, placed.GetComponent<Collider>(), lowH, false, n == 200);
                    DestroyPlaced(placed);
                    placed = null;
                    DevCamera.Follow();
                    GpuMassDev.Clear();

                    // a high obstacle: a vanilla rock, higher than the dome, which the mass goes round
                    Collider rock = onRockSite ? FindRock(player.transform.position, 4f, 20f) : null;
                    string kind = rock != null ? "vanilla rock " + rock.transform.root.name : null;
                    if (rock == null)
                    {
                        placed = PlaceBlock(player.transform.position + fwd * 8f, new Vector3(1.6f, 1.5f, 1.0f), "MW_V14_High", Quaternion.LookRotation(fwd, Vector3.up));
                        rock = placed.GetComponent<Collider>();
                        kind = "solid block 1.6 x 1.5 x 1 m";
                    }
                    ctx.Log($"{tag} high obstacle: {kind}, {rock.bounds.size.ToString("0.00")} m");
                    yield return CrossObstacle(ctx, player, fwd, tag + "_high", n, rock, rock.bounds.size.y, true, n == 200);
                    if (placed != null) { DestroyPlaced(placed); placed = null; }
                    DevCamera.Follow();
                    GpuMassDev.Clear();
                    yield return ctx.WaitFrames(10);
                }
            }
            finally
            {
                GpuWormHost.MeasureAlloc = false;
                GpuWormHost.AutoStep = true;
                GpuMassDev.EngulfTarget = 1f;
                DevInput.Release();
                GpuMassDev.Clear();
                if (placed != null) DestroyPlaced(placed);
                DevCamera.Follow();
                if (frozen) ctx.Console("mw_freeze off");
                if (clutterHidden && ClutterSystem.instance != null) ClutterSystem.instance.enabled = true;
                if (timeForced) ctx.Console($"{VanillaNames.CmdTimeOfDay} -1");
                Player p = Player.m_localPlayer;
                if (p != null) p.SetGodMode(false);
            }
        }

        /// <summary>
        /// A camera that follows the mass for a clip. Every frame the line from the camera to the mass is checked against the scenery (<see cref="Blocked"/>, the rock
        /// included: a camera behind the rock does not film the mass); when it is blocked the camera goes round the mass to the nearest clear angle, at 120 degrees per
        /// second, before it takes up the shot again.
        /// </summary>
        internal sealed class FollowCam
        {
            private readonly Func<Vector3> _target;
            private readonly Vector3 _side;
            private readonly float _distance, _height, _lift;
            private float _angle, _want;
            private static readonly float[] Offsets = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f, 150f, -150f, 180f };

            public FollowCam(Func<Vector3> target, Vector3 side, float distance, float height, float lift)
            {
                _target = target; _side = side; _distance = distance; _height = height; _lift = lift;
            }

            private Vector3 At(Vector3 c, float angle) => c + Quaternion.AngleAxis(angle, Vector3.up) * _side * _distance + Vector3.up * _height;

            public void Pose(out Vector3 pos, out Vector3 look)
            {
                Vector3 c = _target();
                Vector3 aim = c + Vector3.up * Mathf.Max(_lift, 0.3f);
                if (Blocked(At(c, _angle), aim, null) || Blocked(At(c, _want), aim, null))
                {
                    foreach (float o in Offsets)
                    {
                        float a = _angle + o;
                        if (Blocked(At(c, a), aim, null)) continue;
                        _want = a;
                        break;
                    }
                }
                else _want = _angle;
                _angle = Mathf.MoveTowardsAngle(_angle, _want, 120f * Time.unscaledDeltaTime);
                pos = At(c, _angle);
                look = c + Vector3.up * _lift;
            }
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Captures from 1, 3 and 10 m (from the side, rising with the distance), looking at <paramref name="look"/> raised by <paramref name="lift"/>. Before each one the line
        /// from the camera to the mass is checked against the colliders of the scenery (<paramref name="ignore"/> excepted: the rock the mass drapes); when something is in
        /// the way the camera goes round the mass to the next angle.
        /// </summary>
        internal static IEnumerator Shots(ScenarioContext ctx, string name, Func<Vector3> look, Vector3 side, Vector3 fwd, float lift, Collider ignore = null)
        {
            for (int k = 0; k < Distances.Length; k++)
            {
                Vector3 c = look();
                Vector3 aim = c + Vector3.up * Mathf.Max(lift, 0.3f);
                Vector3 cam = c + side * Distances[k] - fwd * (0.25f * Distances[k]) + Vector3.up * Heights[k];
                bool clear = false;
                foreach (float angle in Angles)
                {
                    Quaternion q = Quaternion.AngleAxis(angle, Vector3.up);
                    Vector3 candidate = c + q * side * Distances[k] - q * fwd * (0.25f * Distances[k]) + Vector3.up * Heights[k];
                    if (Blocked(candidate, aim, ignore)) continue;
                    cam = candidate;
                    clear = true;
                    break;
                }
                if (!clear) ctx.Log($"capture {name} {Distances[k]:0} m: the view is blocked from every angle, shot taken anyway");
                DevCamera.Free(cam, c + Vector3.up * lift);
                yield return ctx.WaitFrames(20);
                CheckLod(ctx, $"{name}_{Distances[k]:0}m");
                yield return ctx.Shot($"{name}_{Distances[k]:0}m");
            }
        }

        /// <summary>
        /// Three captures framed on the subject (a flat mass, a worm pair, a player wrapped by the sleeve): <c>close</c> (60 % of the height of the image, low), <c>fit</c> (50 %) and
        /// <c>wide</c> (40 %, from higher up), each at the distance <see cref="ScenarioContext.FitFlat"/> gives for the footprint <paramref name="radius"/> and the height
        /// <paramref name="height"/> of the subject and the field of view of the game camera. The line to the subject is checked against the scenery like <see cref="Shots"/> does.
        /// </summary>
        internal static IEnumerator FramedShots(ScenarioContext ctx, string name, Func<Vector3> look, Vector3 side, Vector3 fwd, float lift, float radius, float height, Collider ignore = null)
        {
            for (int k = 0; k < ShotNames.Length; k++)
            {
                float d = ScenarioContext.FitFlat(radius, height, ShotElevations[k], ShotFractions[k]);
                ScenarioContext.Orbit(d, ShotElevations[k], out float horizontal, out float up);
                Vector3 c = look();
                Vector3 target = c + Vector3.up * lift;
                Vector3 aim = c + Vector3.up * Mathf.Max(lift, 0.3f);
                Vector3 away = (side - 0.25f * fwd).normalized;
                Vector3 cam = target + away * horizontal + Vector3.up * up;
                bool clear = false;
                foreach (float angle in Angles)
                {
                    Vector3 candidate = target + Quaternion.AngleAxis(angle, Vector3.up) * away * horizontal + Vector3.up * up;
                    if (Blocked(candidate, aim, ignore)) continue;
                    cam = candidate;
                    clear = true;
                    break;
                }
                if (!clear) ctx.Log($"capture {name} {ShotNames[k]}: the view is blocked from every angle, shot taken anyway");
                DevCamera.Free(cam, target);
                yield return ctx.WaitFrames(20);
                CheckLod(ctx, $"{name}_{ShotNames[k]}");
                ctx.Log($"capture {name}_{ShotNames[k]}: subject r {radius:0.00} m x {height:0.00} m, camera {d:0.00} m away at {ShotElevations[k]:0} degrees, aimed for {ShotFractions[k] * 100f:0} % of the height of the image");
                yield return ctx.Shot($"{name}_{ShotNames[k]}");
            }
        }

        /// <summary>The same player teleport to the point every run starts from: the start temple of the world, facing north, high enough to land.</summary>
        private static IEnumerator GoToFixedOrigin(ScenarioContext ctx)
        {
            Vector3 o = DevWorld.FixedSearchOrigin();
            float h = WorldGenerator.instance != null ? WorldGenerator.instance.GetHeight(o.x, o.z) : o.y;
            float water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 0f;
            var outcome = new TeleportOutcome();
            yield return DevWorld.TeleportPlayer(new Vector3(o.x, Mathf.Max(h, water) + 2f, o.z), Quaternion.identity, true, 60f, outcome);
            ctx.Log(outcome.Ok ? $"fixed origin of the site search: {o.x:0},{o.z:0}" : "the teleport to the fixed origin failed (" + outcome.Error + "): the search starts where the player is");
        }

        /// <summary>The player back on the fixed site (the heading of the player is kept: it is the one the site was found with).</summary>
        private static IEnumerator ReturnToSite(ScenarioContext ctx, Vector3 site)
        {
            Player p = Player.m_localPlayer;
            if (p == null) yield break;
            var outcome = new TeleportOutcome();
            yield return DevWorld.TeleportPlayer(site + Vector3.up * 0.1f, Quaternion.identity, Vector3.Distance(p.transform.position, site) > 64f, 60f, outcome);
            if (!outcome.Ok) { ctx.Log("could not return to the fixed site: " + outcome.Error); yield break; }
            yield return new WaitForSecondsRealtime(1f);          // physics and the player settle on the floor (as DevWorld.MoveToOpenGround does)
            Player q = Player.m_localPlayer;
            if (q != null) q.SetGodMode(true);
            if (q != null) S18_GroupCoherence.ClearCreatures(ctx, q);
        }

        /// <summary>
        /// The first selection of the anchors of a mesh, measured. Each pass wraps the player with a phantom mass of 10 units after the caches of the meshes and of the sets were emptied
        /// and every sampler made to forget its selection: the main thread reads the meshes, starts the worker, then takes its result. Pass 0 is the warm-up: the first pass of a session also
        /// pays the JIT of the selection code and the first dispatch of the skinning kernel (shader), which is not the cost of reading the meshes of a new target; it is logged and not judged.
        /// The budget (4 ms, unchanged) is judged on the median of the <see cref="SpikeRuns"/> passes that follow. Leaves the caches filled (the engulfs after it select from the cache).
        /// </summary>
        private static IEnumerator SelectionSpike(ScenarioContext ctx)
        {
            var spikes = new List<float>();
            string warm = "none";
            for (int pass = 0; pass <= SpikeRuns; pass++)
            {
                if (!GpuMassDev.Spawn(10, GpuMassDev.GhostMode.EngulfPlayer, 0f, out string how))
                {
                    ctx.Assert("skin_selection_warm_pass_spawned", false, "a phantom mass wrapping the player", how);
                    yield break;
                }
                GpuSkinSampler.DevClearCaches();
                GpuMassDev.Env.DevForgetSkins();
                yield return ctx.WaitUntil(() => GpuMassDev.Env.Skin.Selected && !GpuMassDev.Env.Skin.DevSelectionPending, 10f);
                yield return ctx.WaitSteps(30);
                GpuSkinSampler sk = GpuMassDev.Env.Skin;
                string line = $"{sk.SpikeUncachedMs:0.00} ms ({(sk.LastFromCache ? "from the cache: not a first selection" : "first selection")}; {sk.SpikeBreakdown})";
                if (pass == 0) { warm = line; ctx.Log("skin selection, warm-up pass (JIT and first dispatch included, not judged): " + line); }
                else
                {
                    ctx.Log($"skin selection, pass {pass} of {SpikeRuns}: {line}");
                    if (!sk.LastFromCache && sk.Selected) spikes.Add((float)sk.SpikeUncachedMs);
                }
                GpuMassDev.Clear();
                yield return ctx.WaitSteps(10);
            }
            float median = spikes.Count > 0 ? Median(spikes) : float.MaxValue;
            ctx.Assert("skin_selection_first_spike_median", spikes.Count == SpikeRuns && median <= 4.0, $"<= 4 ms at the first engulf of a mesh, median of {SpikeRuns} passes after a warm-up pass", spikes.Count == SpikeRuns ? $"{median:0.00} ms (passes {Join(spikes)}; warm-up {warm})" : $"only {spikes.Count} of {SpikeRuns} passes were first selections");
        }

        private static readonly float[] Angles = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };
        private static int s_viewMask;

        /// <summary>True when a collider of the scenery (not <paramref name="ignore"/>) lies on the line from <paramref name="from"/> to <paramref name="to"/>.</summary>
        internal static bool Blocked(Vector3 from, Vector3 to, Collider ignore)
        {
            if (s_viewMask == 0)
                s_viewMask = LayerMask.GetMask(VanillaNames.LayerDefault, VanillaNames.LayerStaticSolid, VanillaNames.LayerDefaultSmall, VanillaNames.LayerPiece, VanillaNames.LayerTerrain, VanillaNames.LayerVehicle);
            Vector3 dir = to - from;
            float len = dir.magnitude;
            if (len < 0.01f) return false;
            dir /= len;
            Vector3 origin = from;
            for (int i = 0; i < 4; i++)
            {
                float rest = len - (origin - from).magnitude;
                if (rest <= 0.02f || !Physics.Raycast(origin, dir, out RaycastHit hit, rest, s_viewMask, QueryTriggerInteraction.Ignore)) return false;
                if (ignore == null || hit.collider != ignore) return true;
                origin = hit.point + dir * 0.02f;
            }
            return true;
        }

        /// <summary>The direction (the given one, else turned by 45 degree steps) from which a camera at <paramref name="distance"/> and <paramref name="height"/> sees every one of <paramref name="targets"/>.</summary>
        internal static Vector3 ClearSide(Vector3[] targets, Vector3 side, float distance, float height, Collider ignore, ScenarioContext ctx)
        {
            foreach (float angle in Angles)
            {
                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * side;
                bool ok = true;
                foreach (Vector3 t in targets)
                {
                    Vector3 aim = t + Vector3.up * 0.3f;
                    if (Blocked(aim + dir * distance + Vector3.up * height, aim, ignore)) { ok = false; break; }
                }
                if (ok) return dir;
            }
            ctx.Log("no clear angle for the camera: the first one is kept");
            return side;
        }

        /// <summary>Water within <paramref name="radius"/> of <paramref name="p"/>: the ground (centre, then two rings of eight points) lower than 0.5 m over the sea level, or a collider of the water layer.</summary>
        internal static bool NearWater(Vector3 p, float radius)
        {
            ZoneSystem zones = ZoneSystem.instance;
            if (zones == null) return false;
            float level = zones.m_waterLevel + 0.5f;
            if (zones.GetGroundHeight(p) < level) return true;
            for (int ring = 1; ring <= 2; ring++)
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI * 0.25f;
                    Vector3 q = p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (radius * ring * 0.5f);
                    if (zones.GetGroundHeight(q) < level) return true;
                }
            int water = LayerMask.GetMask("water");
            return water != 0 && Physics.CheckSphere(p, radius, water, QueryTriggerInteraction.Collide);
        }

        internal static bool NearBuilt(Vector3 p, float radius)
        {
            foreach (WearNTear w in WearNTear.s_allInstances)
                if (w != null && (w.transform.position - p).sqrMagnitude < radius * radius) return true;
            return false;
        }

        private static IEnumerator Measure(ScenarioContext ctx, string tag, int window)
        {
            DevCamera.Free(GpuMassDev.Mass.Center + Vector3.right * 3f + Vector3.up * 1f, GpuMassDev.Mass.Center + Vector3.up * 0.2f);
            yield return ctx.WaitFrames(20);
            GpuWormHost.ResetStats();
            yield return ctx.WaitFrames(window);
            double ms = GpuWormHost.AvgMs;
            ctx.Log($"{tag}: main thread {ms:0.0000} ms/frame mean, max {GpuWormHost.MaxMs:0.000} over {GpuWormHost.Frames} frames; {GpuWormHost.DrawnWorms} worms drawn, {GpuWormHost.Triangles} triangles (+{GpuWormHost.ShadowTriangles} shadow), {GpuWormHost.DrawCalls} draw calls; frame {Time.unscaledDeltaTime * 1000f:0.0} ms");
            ctx.Assert($"{tag}_main_thread_budget", ms <= BudgetMs, $"<= {BudgetMs} ms/frame (mean)", ms.ToString("0.0000"));
        }

        /// <summary>
        /// Logs, for the mass in front of the camera, the distance kept for the LOD (camera to the nearest point of the volume of the mass), the LOD, the volume and the cameras
        /// (the one the host used and the scenario's); with the camera within 2 m of the volume the LOD0 and the shadows must be there (triangles = worms x 304).
        /// </summary>
        private static void CheckLod(ScenarioContext ctx, string tag)
        {
            GpuMass m = GpuMassDev.Mass;
            if (m == null) return;
            ctx.Log($"{tag}: LOD {m.Lod} at {m.LodDistance:0.00} m from the volume (centre {m.VolumeCenter.ToString("0.00")}, half sizes {m.VolumeExtents.ToString("0.00")}); host camera {GpuWormHost.LastCameraPosition.ToString("0.00")}, " +
                    $"scenario camera {(DevCamera.Active ? DevCamera.Position.ToString("0.00") : "off")}; {GpuWormHost.DrawnWorms} worms drawn, {GpuWormHost.Triangles} triangles (+{GpuWormHost.ShadowTriangles} shadow)");
            if (DevCamera.Active)
                ctx.Assert($"{tag}_host_uses_the_scenario_camera", Vector3.Distance(GpuWormHost.LastCameraPosition, DevCamera.Position) < 1f, "the camera of the host is the one of the scenario (< 1 m)", Vector3.Distance(GpuWormHost.LastCameraPosition, DevCamera.Position).ToString("0.00"));
            if (m.LodDistance <= 2f)
            {
                long expected = (long)GpuWormHost.DrawnWorms * GpuWormTube.Triangles(0);
                bool ok = m.Lod == 0 && GpuWormHost.ShadowTriangles > 0 && Math.Abs(GpuWormHost.Triangles - expected) <= expected / 50 + 1;
                ctx.Assert($"{tag}_lod0_with_shadows_when_close", ok, $"LOD0 and shadows within 2 m (triangles = worms x {GpuWormTube.Triangles(0)} = {expected})", $"LOD {m.Lod}, {GpuWormHost.Triangles} triangles, +{GpuWormHost.ShadowTriangles} shadow");
            }
        }

        /// <summary>The mass at rest keeps its volume: a dome on the grid, not a carpet.</summary>
        private static IEnumerator CheckVolume(ScenarioContext ctx, string tag, float r)
        {
            // the median of Windows snapshots half a second of simulation apart: one snapshot of a mass still shifting gave an extent of 0.96 m (2.06 r) on one run and 0.41 m on the next
            var tops = new List<float>();
            var r90s = new List<float>();
            var medians = new List<float>();
            for (int w = 0; w < Windows; w++)
            {
                if (w > 0) yield return ctx.WaitSteps(WindowGapSteps);
                GpuMassDev.NodeSnapshot snap = GpuMassDev.RequestNodes(GpuMassDev.Mass);
                yield return ctx.WaitUntil(() => snap.Done, 10f);
                if (!snap.Done || snap.Error) continue;
                var heights = new List<float>();
                var radii = new List<float>();
                Vector3 c = GpuMassDev.Mass.Center - snap.Origin;
                foreach (Vector4 n in snap.Nodes)
                {
                    heights.Add(n.y - snap.GroundAt(n) - WormSimConst.Radius);
                    radii.Add(new Vector2(n.x - c.x, n.z - c.z).magnitude);
                }
                heights.Sort();
                radii.Sort();
                tops.Add(heights[heights.Count - 1]);
                medians.Add(heights[heights.Count / 2]);
                r90s.Add(radii[(int)(radii.Count * 0.9f)]);
            }
            if (tops.Count == 0) { ctx.Assert($"{tag}_volume", false, "nodes read back", "failed"); yield break; }
            float top = Median(tops), median = Median(medians), r90 = Median(r90s);
            ctx.Log($"{tag} volume, median of {tops.Count} snapshots: highest node {top:0.00} m ({top / r:0.00} r) above the ground (each {Join(tops)}), median node {median:0.00} m, 90% of the nodes within {r90:0.00} m ({r90 / r:0.00} r) (each {Join(r90s)})");
            ctx.Assert($"{tag}_volume_height", top >= 0.30f * r, $">= {0.30f * r:0.00} m (0.30 r) above the ground, median of {tops.Count} snapshots", top.ToString("0.00"));
            ctx.Assert($"{tag}_volume_extent", r90 <= 1.3f * r, $"90% of the nodes within {1.3f * r:0.00} m (1.3 r), median of {r90s.Count} snapshots", r90.ToString("0.00"));
        }

        internal static float HorizontalDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        private static IEnumerator CheckGround(ScenarioContext ctx, string tag)
        {
            GpuMassDev.NodeSnapshot snap = GpuMassDev.RequestNodes(GpuMassDev.Mass);
            yield return ctx.WaitUntil(() => snap.Done, 10f);
            if (!snap.Done || snap.Error) { ctx.Assert($"{tag}_no_node_under_ground", false, "nodes read back", snap.Done ? "error" : "timed out"); yield break; }
            int below;
            float lowest = snap.LowestAboveGround(out below);
            ctx.Assert($"{tag}_no_node_under_ground", below == 0, "no node under the ground the simulation used", $"lowest margin {lowest * 1000f:0.0} mm, {below} of {snap.Nodes.Length} below");
        }

        internal static float GroundOf(Vector3 p, float fallback)
        {
            ZoneSystem zones = ZoneSystem.instance;
            return zones != null ? zones.GetGroundHeight(p) : fallback;
        }

        internal static bool IsOptimized()
        {
            object[] attrs = typeof(V14_GpuMassSandbox).Assembly.GetCustomAttributes(typeof(DebuggableAttribute), false);
            return !(attrs.Length > 0 && ((DebuggableAttribute)attrs[0]).IsJITOptimizerDisabled);
        }

        /// <summary>The closest vanilla rock between <paramref name="min"/> and <paramref name="max"/> metres from <paramref name="from"/>: a solid collider of a rock prefab, 1 to 6 m across.</summary>
        internal static Collider FindRock(Vector3 from, float min, float max)
        {
            int mask = LayerMask.GetMask(VanillaNames.LayerDefault, VanillaNames.LayerStaticSolid, VanillaNames.LayerDefaultSmall);
            Collider best = null;
            float bestDist = float.MaxValue;
            foreach (Collider col in Physics.OverlapSphere(from, max, mask, QueryTriggerInteraction.Ignore))
            {
                if (col == null || col.GetType().Name == "TerrainCollider") continue;
                string root = col.transform.root.name;
                bool rock = root.IndexOf("rock", StringComparison.OrdinalIgnoreCase) >= 0 || col.GetComponentInParent<MineRock5>() != null || col.GetComponentInParent<MineRock>() != null;
                if (!rock) continue;
                Vector3 e = col.bounds.extents;
                float across = Mathf.Max(e.x, e.z) * 2f;
                if (across < 1f || across > 6f || e.y * 2f < 0.6f) continue;
                Vector3 d = col.bounds.center - from; d.y = 0f;
                float dist = d.magnitude;
                if (dist < min || dist >= bestDist) continue;
                best = col;
                bestDist = dist;
            }
            return best;
        }

        /// <summary>A stone piece (or a plain solid block when the prefab is not there) of 2 x 1 x 0.6 m standing on the ground at <paramref name="pos"/>.</summary>
        /// <summary>A solid block of the given size standing on the ground at <paramref name="pos"/> (the low and the fallback high obstacle).</summary>
        internal static GameObject PlaceBlock(Vector3 pos, Vector3 size, string name, Quaternion? rotation = null)
        {
            pos.y = GroundOf(pos, pos.y);
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.localScale = size;
            cube.transform.rotation = rotation ?? Quaternion.identity;
            cube.transform.position = pos + Vector3.up * (size.y * 0.5f);
            int layer = LayerMask.NameToLayer(VanillaNames.LayerStaticSolid);
            if (layer >= 0) cube.layer = layer;
            Physics.SyncTransforms();           // a collider created this frame keeps the bounds of a unit cube at the origin until the physics see its transform
            return cube;
        }

        /// <summary>
        /// The mass crosses an obstacle: it starts 2.5 m before it, walks to 2 m beyond it, and is stopped with its centre over the obstacle for the measures, then goes on.
        /// A low obstacle (lower than the dome) is covered: nodes on it, on its flanks, at its foot. A high one is gone round: nodes against its surface (within 5 cm), on both
        /// sides, at its foot. In both cases almost no node inside it and the objective beyond is reached. A clip films the crossing (the camera follows the mass).
        /// </summary>
        private static IEnumerator CrossObstacle(ScenarioContext ctx, Player player, Vector3 fwd, string tag, int n, Collider obst, float obstH, bool high, bool clip)
        {
            float r = GpuMassDev.RadiusOf(n);
            Bounds b = obst.bounds;
            Vector3 toPlayer = player.transform.position - b.center; toPlayer.y = 0f;
            toPlayer = toPlayer.sqrMagnitude > 1e-3f ? toPlayer.normalized : -fwd;
            // a block of this scenario is turned toward the approach: its measures are taken in its own frame (face across the way, depth along it), a rock keeps its box of the world
            bool oriented = obst.gameObject.name.StartsWith("MW_V14_", StringComparison.Ordinal);
            Vector3 half = oriented ? obst.transform.localScale * 0.5f : b.extents;
            float reach = oriented ? half.z : Mathf.Abs(toPlayer.x) * b.extents.x + Mathf.Abs(toPlayer.z) * b.extents.z;
            Vector3 start = b.center + toPlayer * (reach + r + 2.5f);
            start.y = GroundOf(start, player.transform.position.y);
            float bare = GroundOf(b.center, start.y);                // the bare ground under the obstacle (the site can slope: the ground at the start is not the one at the obstacle)
            Vector3 far = b.center - toPlayer * (reach + 2f);
            far.y = GroundOf(far, bare);
            // framed on the mass (and on the flank of the rock it goes round: the camera stays clear of it), 40 to 60 % of the height of the image
            float subjectR = r + (high ? 0.6f * reach : 0f), subjectH = Mathf.Max(0.45f * r, high ? 0.5f * obstH : 0f);
            float crossD = ScenarioContext.FitFlat(subjectR, subjectH, 25f);
            ScenarioContext.Orbit(crossD, 25f, out float camDist, out float camUp);
            Vector3 camSide = ClearSide(new[] { start, b.center, far }, Vector3.Cross(Vector3.up, toPlayer), camDist, camUp, obst, ctx);
            // the centre is over the obstacle when it is this close to its middle (the block: walked to the middle itself and stopped there; a rock cannot be reached, the mass is held against it)
            float over = Mathf.Max(0.35f * Mathf.Min(half.x, half.z), 0.4f);
            ctx.Log($"{tag} crossing: bounds {b.size.x:0.0} x {b.size.y:0.0} x {b.size.z:0.0} m{(oriented ? " (block turned toward the approach)" : string.Empty)}, from {start.ToString("0.0")} to {far.ToString("0.0")}; camera {crossD:0.0} m");

            if (clip)
            {
                GpuMassDev.Spawn(n, GpuMassDev.GhostMode.Still, 0f, out _, start);
                yield return ctx.WaitSteps(Warm);
                DevCamera.Free(start + camSide * camDist + Vector3.up * camUp, start + Vector3.up * 0.4f);
                DevCamera.Provider = new FollowCam(() => GpuMassDev.Mass != null ? GpuMassDev.Mass.Center : b.center, camSide, camDist, camUp, 0.4f).Pose;
                GpuMassDev.WalkTo(far);
                yield return ctx.WaitUntil(() => HorizontalDistance(GpuMassDev.Mass.Center, b.center) < reach + 3f, 40f);
                yield return ctx.Clip("cross_" + tag, 6f);
                yield return ctx.WaitUntil(() => GpuMassDev.Arrived, 40f);
                DevCamera.Provider = null;
            }

            // the measured crossing
            GpuMassDev.Spawn(n, GpuMassDev.GhostMode.Still, 0f, out _, start);
            yield return ctx.WaitSteps(Warm);
            if (!high)
            {
                // a block is walked to its middle and the mass stops exactly there (the arrival is the stop of the ghost): the same position on every run, not wherever the mass coasts to
                GpuMassDev.WalkTo(new Vector3(b.center.x, bare, b.center.z));
                yield return ctx.WaitUntil(() => GpuMassDev.Arrived, 60f, $"{tag}_centre_over_the_obstacle");
            }
            else
            {
                GpuMassDev.WalkTo(far);
                yield return ctx.WaitUntil(() => HorizontalDistance(GpuMassDev.Mass.Center, b.center) < over, 60f, $"{tag}_centre_over_the_obstacle");
                GpuMassDev.WalkTo(GpuMassDev.Mass.Center);                 // stop where it is
            }
            yield return ctx.WaitSteps(Warm * 2);
            Vector3 look2 = Vector3.Lerp(GpuMassDev.Mass.Center, b.center, 0.5f) + Vector3.up * 0.3f;
            yield return FramedShots(ctx, $"{tag}_over", () => look2, camSide, toPlayer, 0f, subjectR, subjectH, obst);

            // median of Windows snapshots half a second of simulation apart (one snapshot of a mass that is still shifting gave 12.7 % then 20 % of nodes on the flanks)
            int mask = LayerMask.GetMask(VanillaNames.LayerDefault, VanillaNames.LayerStaticSolid, VanillaNames.LayerDefaultSmall, VanillaNames.LayerPiece);
            float upOn = Mathf.Min(0.3f, 0.5f * obstH), upFlank = Mathf.Min(0.1f, 0.25f * obstH);
            var pTops = new List<float>(); var pFlanks = new List<float>(); var pFoots = new List<float>(); var pNears = new List<float>();
            float pInWorst = 0f, lowestOverall = float.MaxValue, highestNode = float.MinValue;
            int belowTotal = 0, taken = 0, sampledLast = 0, sideLast = 0, farLast = 0;
            for (int w = 0; w < Windows; w++)
            {
                if (w > 0) yield return ctx.WaitSteps(WindowGapSteps);
                GpuMassDev.NodeSnapshot snap = GpuMassDev.RequestNodes(GpuMassDev.Mass);
                yield return ctx.WaitUntil(() => snap.Done, 10f);
                if (!snap.Done || snap.Error) continue;
                int total = snap.Nodes.Length, onTop = 0, flank = 0, foot = 0, inside = 0, near = 0, sampled = 0, sideCount = 0, farCount = 0;
                for (int i = 0; i < total; i++)
                {
                    Vector3 p = new Vector3(snap.Nodes[i].x + snap.Origin.x, snap.Nodes[i].y + snap.Origin.y, snap.Nodes[i].z + snap.Origin.z);
                    highestNode = Mathf.Max(highestNode, p.y);
                    Vector3 d = p - b.center;
                    float e = oriented
                        ? Mathf.Max(Mathf.Abs(Vector3.Dot(d, obst.transform.right)) / Mathf.Max(half.x, 0.1f), Mathf.Abs(Vector3.Dot(d, obst.transform.forward)) / Mathf.Max(half.z, 0.1f))
                        : Mathf.Max(Mathf.Abs(d.x) / Mathf.Max(b.extents.x, 0.1f), Mathf.Abs(d.z) / Mathf.Max(b.extents.z, 0.1f));
                    float up = p.y - bare;
                    if (e < 1f && up > upOn) onTop++;
                    if (e >= 0.6f && e < 1.2f && up > upFlank) flank++;
                    if (e >= 1f && e < 1.8f && up < 0.15f) foot++;
                    if (i % 7 != 0) continue;
                    sampled++;
                    if (Physics.CheckSphere(p, 0.004f, mask, QueryTriggerInteraction.Ignore)) inside++;
                    if (Vector3.Dot(d, toPlayer) > 0f) sideCount++; else farCount++;
                    bool close = false;
                    foreach (Collider c in Physics.OverlapSphere(p, 0.05f, mask, QueryTriggerInteraction.Ignore)) if (c == obst) { close = true; break; }
                    if (close) near++;
                }
                pTops.Add(100f * onTop / total); pFlanks.Add(100f * flank / total); pFoots.Add(100f * foot / total); pNears.Add(100f * near / Mathf.Max(1, sampled));
                pInWorst = Mathf.Max(pInWorst, 100f * inside / Mathf.Max(1, sampled));
                int below;
                lowestOverall = Mathf.Min(lowestOverall, snap.LowestAboveGround(out below));
                belowTotal += below;
                taken++;
                sampledLast = sampled; sideLast = sideCount; farLast = farCount;
            }
            if (taken > 0)
            {
                float pTop = Median(pTops), pFlank = Median(pFlanks), pFoot = Median(pFoots), pNear = Median(pNears);
                ctx.Log($"{tag} mid crossing, median of {taken} snapshots: {pTop:0.0}% over the obstacle (each {Join(pTops)}), {pFlank:0.0}% on the flanks (each {Join(pFlanks)}), {pFoot:0.0}% at the foot, {pNear:0.0}% within 5 cm of its surface (each {Join(pNears)}), " +
                        $"{100f * sideLast / Mathf.Max(1, sampledLast):0}% on the near side and {100f * farLast / Mathf.Max(1, sampledLast):0}% on the far side (last), worst {pInWorst:0.00}% of the sampled nodes touching a collider; highest node {highestNode:0.00} m, obstacle {b.max.y:0.00} m");
                if (!high)
                {
                    ctx.Assert($"{tag}_cross_nodes_on_the_obstacle", pTop >= 25f, $">= 25% of the nodes over the obstacle (above {upOn:0.00} m over the bare ground), median of {taken} snapshots", pTop.ToString("0.0"));
                    ctx.Assert($"{tag}_cross_nodes_on_the_flanks", pFlank >= 15f, $">= 15% of the nodes on the flanks, median of {taken} snapshots", pFlank.ToString("0.0"));
                    // no "at the foot" assertion: stopped with its centre over the block, a mass about as wide as the block (n50: 0.9 m for 1.6 x 1.0) is entirely on it; the foot is not a place to measure here
                }
                else
                {
                    // going round a high obstacle is the work of the AI of the mass (V12_MassEnvelop); the ghost walks straight at it and is only held against it
                    // 10 % before the nodes slid along the face; measured 2.5 % (n200) once they do: the mass is held against the rock and flows past its flank, the leading nodes only touch it.
                    // 1 % keeps a margin of 2.5 on the measure and still fails a mass that goes through (0 %) or keeps away; judge the captures {tag}_high_over_*.
                    ctx.Assert($"{tag}_cross_nodes_against_the_surface", pNear >= 1f, $">= 1% of the nodes within 5 cm of the obstacle, median of {taken} snapshots", pNear.ToString("0.0"));
                }
                ctx.Assert($"{tag}_cross_no_node_in_the_obstacle", pInWorst < 2f, $"< 2% of the sampled nodes touching the obstacle, worst of {taken} snapshots", pInWorst.ToString("0.00"));
                ctx.Assert($"{tag}_cross_no_node_under_ground", belowTotal == 0, $"no node under the ground the simulation used, {taken} snapshots", $"lowest margin {lowestOverall * 1000f:0.0} mm, {belowTotal} below");
            }
            else ctx.Assert($"{tag}_cross_readback_ok", false, "nodes read back", "none of the snapshots");

            // down the other side: the objective beyond the obstacle is reached
            GpuMassDev.WalkTo(far);
            yield return ctx.WaitUntil(() => GpuMassDev.Arrived, 40f);
            yield return ctx.WaitSteps(Warm * 2);
            float miss = HorizontalDistance(GpuMassDev.Mass.Center, far);
            ctx.Assert($"{tag}_cross_reaches_the_objective", GpuMassDev.Arrived && miss < 0.6f, "the centre reaches the objective 2 m beyond the obstacle (< 0.6 m)", $"{miss:0.00} m");
            Vector3 look3 = Vector3.Lerp(GpuMassDev.Mass.Center, b.center, 0.5f) + Vector3.up * 0.3f;
            yield return FramedShots(ctx, $"{tag}_after", () => look3, camSide, toPlayer, 0f, subjectR, subjectH, obst);
        }

        private static float Median(List<float> v)
        {
            if (v.Count == 0) return 0f;
            var sorted = new List<float>(v);
            sorted.Sort();
            int m = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[m] : 0.5f * (sorted[m - 1] + sorted[m]);
        }

        private static string Join(List<float> v)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < v.Count; i++) sb.Append(i > 0 ? "/" : "").Append(v[i].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        private static GameObject PlaceStone(Vector3 pos, ref string kind)
        {
            pos.y = GroundOf(pos, pos.y);
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("stone_wall_2x1") : null;
            if (prefab != null)
            {
                GameObject go = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
                if (go.GetComponentInChildren<Collider>() != null)
                {
                    kind = "stone piece stone_wall_2x1";
                    return go;
                }
                UnityEngine.Object.Destroy(go);
            }
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "MW_V14_Block";
            cube.transform.localScale = new Vector3(2f, 1f, 0.6f);
            cube.transform.position = pos + Vector3.up * 0.5f;
            int layer = LayerMask.NameToLayer(VanillaNames.LayerStaticSolid);
            if (layer >= 0) cube.layer = layer;
            kind = "solid block 2 x 1 x 0.6 m";
            return cube;
        }

        internal static void DestroyPlaced(GameObject go)
        {
            if (go == null) return;
            ZNetView view = go.GetComponent<ZNetView>();
            if (view != null && view.IsValid() && ZNetScene.instance != null) ZNetScene.instance.Destroy(go);
            else UnityEngine.Object.Destroy(go);
        }
    }
}
#endif
