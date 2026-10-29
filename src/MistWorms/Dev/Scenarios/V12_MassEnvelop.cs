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
    ///  1. a mass of 100 hunts the player standing behind a vanilla rock: it goes round the rock (fewer than 2 % of nodes inside it) and reaches the player; then, for the graze alone, a mass follows a dev goal on a straight line tangent to the flank (edge 0.1 m inside the width of the rock), nodes against the rock by construction (decision 266);
    ///  2. a mass of 100 crosses a low stone (about half of its dome height): nothing inside the stone, nodes over it when it goes over;
    ///  3. a mass of 100 engulfs the player: the sleeve forms on the real body of the player, and the time it takes to reach 80 % of its steady state is logged (the readback of
    ///     the nodes every 0.5 s gives the number of worms settled on the body).
    /// Captures from 1, 3 and 10 m and a clip for each act. Run it with tools/build.ps1 -Configuration Profile. Parameters: n (count, default 100).
    /// </summary>
    internal sealed class V12_MassEnvelop : IDevScenario
    {
        private const float PollSeconds = 0.25f;
        private const float PlayerSideOffset = 1.25f;      // the player's lateral shift from the axis start-rock, 1 to 1.5 m, to impose the side of the detour
        private const float SleeveSample = 0.5f;
        private const float FormedBudget = 5f;          // logged against 2.5 s in the sandbox (the engulf of the game rises over 1 s, the polling is 0.5 s); asserted at twice that

        public string Name => "V12_MassEnvelop";

        // The rock the mass goes round (decision 257): chosen by a criterion, not by whichever rock was closest. Tall enough that the mass cannot go over it (a low rock gave 12 to 16 % of nodes
        // against it because the centre passed over, a rock the mass skirts gave 0 %), wide enough to be in its way, not a cliff. Same rule at the site test and at the use.
        private const float RockMinHeight = 1.8f, RockMaxHeight = 3.5f, RockMinAcross = 2f, RockMaxAcross = 4f, RockMinDistance = 6f, RockMaxDistance = 16f;

        /// <summary>
        /// The closest vanilla rock 6 to 16 m from <paramref name="from"/> that is between 1.8 and 3.5 m high and 2 to 4 m across (at least as high as the dome of a mass of
        /// <paramref name="massRadius"/> times 2); ties on the distance go to the lowest x then z, so that the choice depends on the site and nothing else. Null when none.
        /// </summary>
        internal static Collider EnvelopRock(Vector3 from, float massRadius)
        {
            int mask = LayerMask.GetMask(VanillaNames.LayerDefault, VanillaNames.LayerStaticSolid, VanillaNames.LayerDefaultSmall);
            Collider best = null;
            float bestDist = float.MaxValue;
            float minHeight = Mathf.Max(RockMinHeight, 2f * massRadius);
            foreach (Collider col in Physics.OverlapSphere(from, RockMaxDistance + 4f, mask, QueryTriggerInteraction.Ignore))
            {
                if (col == null || col.GetType().Name == "TerrainCollider") continue;
                string root = col.transform.root.name;
                bool rock = root.IndexOf("rock", StringComparison.OrdinalIgnoreCase) >= 0 || col.GetComponentInParent<MineRock5>() != null || col.GetComponentInParent<MineRock>() != null;
                if (!rock) continue;
                Bounds b = col.bounds;
                float across = Mathf.Max(b.extents.x, b.extents.z) * 2f;
                if (across < RockMinAcross || across > RockMaxAcross || b.size.y < minHeight || b.size.y > RockMaxHeight) continue;
                Vector3 d = b.center - from; d.y = 0f;
                float dist = d.magnitude;
                if (dist < RockMinDistance || dist > RockMaxDistance) continue;
                bool tie = best != null && Mathf.Abs(dist - bestDist) < 0.01f && (b.center.x < best.bounds.center.x || (Mathf.Approximately(b.center.x, best.bounds.center.x) && b.center.z < best.bounds.center.z));
                if (dist < bestDist - 0.01f || tie)
                {
                    best = col;
                    bestDist = dist;
                }
            }
            return best;
        }

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

                GpuWormHost.DevDispMax = ctx.ParamFloat("disp", WormSimConst.DispMax);        // dev A/B of the shader constants
                GpuWormHost.DevEdgePush = ctx.ParamFloat("edge", WormSimConst.SolidEdgePush);
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
                                                               && EnvelopRock(p, MassMath.Radius(n)) != null, site, ctx.Log, new FlatSpec(6f, 0.5f, 1f));
                ctx.Assert("rock_site_found", site.Found, "flat ground with a vanilla rock 6 to 16 m away", site.Found ? site.Spot.ToString() : site.Error);
                if (!site.Found) yield break;
                ctx.Console($"{VanillaNames.CmdTimeOfDay} 0.5");
                timeForced = true;
                player = Player.m_localPlayer;
                player.SetGodMode(true);
                S18_GroupCoherence.ClearCreatures(ctx, player);

                Collider rock = EnvelopRock(site.Spot, MassMath.Radius(n));      // the same call as the site test: the rock judged there is the rock used here
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
                    // the player stands 1.25 m off the axis start-rock: the straight line then runs through the +side half of the rock and the mass has to go round that flank (decision 265)
                    Vector3 playerAt = rb.center + dir * (reach + 3f) + side * PlayerSideOffset;
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
                    float maxNear = 0f, maxInside = 0f, maxTouching = 0f, minRockDistance = float.MaxValue;
                    int snapshots = 0, contactFrames = 0;
                    bool reached = false, passShot = false, contactShot = false;
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
                                Measure(snap, rock, out float near, out float inside, out float touching);
                                if (near > maxNear && near >= 0.1f && !contactShot)
                                {
                                    contactShot = true;
                                    yield return ctx.Shot("round_the_rock_contact");      // the first moment nodes touch the rock
                                }
                                if (near > 0f) contactFrames++;
                                maxNear = Mathf.Max(maxNear, near);
                                maxInside = Mathf.Max(maxInside, inside);
                                maxTouching = Mathf.Max(maxTouching, touching);
                                snapshots++;
                            }
                        }
                        float next = Time.time + PollSeconds;
                        while (Time.time < next) yield return null;
                    }
                    ctx.Log($"[AI act] round the rock: {snapshots} snapshots, nodes within 5 cm of the rock up to {maxNear:0.0} %, inside it (in its box and within 4 mm of its collider) up to {maxInside:0.00} %, within 4 mm of any collider (the rock included) up to {maxTouching:0.00} %, closest approach of the centre {minRockDistance:0.0} m to the rock centre, {contactFrames} of {snapshots} snapshots with nodes within 5 cm (report only), player offset {PlayerSideOffset:0.00} m to the +side of the axis, reached the player {reached}");
                    ctx.Assert("ai_round_snapshots", snapshots >= 3, ">= 3 snapshots of the nodes while it passes the rock", snapshots);
                    ctx.Assert("ai_round_no_node_in_the_rock", maxInside < 2f, "< 2 % of the sampled nodes inside the rock (in the box of its bounds, shrunk by 4 mm, and within 4 mm of its collider; touching another collider does not count)", maxInside.ToString("0.00"));
                    ctx.Assert("ai_round_reaches_the_player", reached, "the mass reaches the player (within its engulf range)", reached);
                    yield return V14_GpuMassSandbox.Shots(ctx, "round_end", () => mass != null ? mass.transform.position : rb.center, side, dir, 0.3f, rock);

                    // ---------------------------------------------------------------- graze act (decision 266): contact by construction. The mass follows a dev goal on a straight line TANGENT to the +side flank of the rock,
                    // its edge (radius r) 0.1 m inside the width of the rock at the height of the mass, so that the rock really pushes it aside (SolidPush) and nodes graze it whatever side the AI would pick.
                    if (mass != null) MassDev.Destroy(mass);
                    mass = null;
                    {
                        float rayY = V14_GpuMassSandbox.GroundOf(rb.center, player.transform.position.y) + 0.45f * r * 0.5f;       // half the dome height of the mass
                        float halfWidth = 0f;
                        for (float along = -reach; along <= reach + 0.001f; along += 0.25f)
                        {
                            Vector3 o = rb.center + dir * along + side * (reach + 2f);
                            o.y = rayY;
                            if (rock.Raycast(new Ray(o, -side), out RaycastHit hit, reach * 2f + 4f))
                                halfWidth = Mathf.Max(halfWidth, Vector3.Dot(hit.point - rb.center, side));
                        }
                        if (halfWidth < 0.3f) halfWidth = reach;      // no hit at that height (should not happen): fall back to the box
                        const float GrazeInset = 0.1f;
                        float lateral = halfWidth + r - GrazeInset;
                        ctx.Log($"[graze act] tangent line at {lateral:0.00} m to the +side of the rock axis: width of the rock at the height of the mass (y {rayY:0.00}) {halfWidth:0.00} m (half), radius of the mass {r:0.00} m, edge of the mass {GrazeInset:0.00} m inside the width");
                        Vector3 gStart = rb.center + side * lateral - dir * (reach + r + 3.5f);
                        gStart.y = V14_GpuMassSandbox.GroundOf(gStart, player.transform.position.y);
                        Vector3 gGoal = rb.center + side * lateral + dir * (reach + r + 1.5f);
                        gGoal.y = V14_GpuMassSandbox.GroundOf(gGoal, gStart.y);
                        mass = MassDev.SpawnAt(gStart, n, ZDOID.None, freezeAI: false);
                        ctx.Assert("graze_mass_spawned", mass != null, "a mass on the tangent line", mass != null);
                        if (mass != null)
                        {
                            visual = mass.GetComponent<WritheMassVisual>();
                            ai = mass.GetComponent<WritheMassAI>();
                            ai.DevGoal = gGoal;
                            yield return ctx.WaitUntil(() => visual.GpuDriven, 10f);
                            DevCamera.Free(rb.center + side * camDist + Vector3.up * 1.6f, rb.center + Vector3.up * 0.5f);
                            DevCamera.Provider = new V14_GpuMassSandbox.FollowCam(() => mass != null ? Vector3.Lerp(mass.transform.position, rb.center, 0.5f) : rb.center, side, camDist, 1.6f, 0.4f).Pose;
                            float g0 = Time.time;
                            float gNear = 0f, gInside = 0f, gClosest = float.MaxValue, gMaxLateral = 0f;
                            int gSnaps = 0, gContact = 0;
                            bool gDone = false, gShot = false;
                            while (Time.time - g0 < 40f && mass != null)
                            {
                                Vector3 mp = mass.transform.position;
                                if (V14_GpuMassSandbox.HorizontalDistance(mp, gGoal) < 0.7f) { gDone = true; break; }
                                float dR = V14_GpuMassSandbox.HorizontalDistance(mp, rb.center);
                                gClosest = Mathf.Min(gClosest, dR);
                                if (Mathf.Abs(Vector3.Dot(mp - rb.center, dir)) < reach) gMaxLateral = Mathf.Max(gMaxLateral, Vector3.Dot(mp - rb.center, side));
                                if (visual.Gpu != null && dR < reach + r + 1.2f)
                                {
                                    GpuMassDev.NodeSnapshot snap = GpuMassDev.RequestNodes(visual.Gpu);
                                    float until = Time.time + 2f;
                                    while (!snap.Done && Time.time < until) yield return null;
                                    if (snap.Done && !snap.Error)
                                    {
                                        Measure(snap, rock, out float near, out float inside, out _);
                                        if (near >= 0.1f && !gShot) { gShot = true; yield return ctx.Shot("graze_the_rock_contact"); }
                                        if (near > 0f) gContact++;
                                        gNear = Mathf.Max(gNear, near);
                                        gInside = Mathf.Max(gInside, inside);
                                        gSnaps++;
                                    }
                                }
                                float next = Time.time + PollSeconds;
                                while (Time.time < next) yield return null;
                            }
                            DevCamera.Provider = null;
                            ctx.Log($"[graze act] {gSnaps} snapshots, nodes within 5 cm of the rock up to {gNear:0.0} %, inside it up to {gInside:0.00} % (report only), closest approach of the centre {gClosest:0.00} m to the rock centre (line at {lateral:0.00} m), furthest lateral position of the centre alongside the rock {gMaxLateral:0.00} m, {gContact} of {gSnaps} snapshots with nodes within 5 cm, reached the goal {gDone}");
                            ctx.Assert("graze_snapshots", gSnaps >= 3, ">= 3 snapshots of the nodes while it grazes the rock", gSnaps);
                            ctx.Assert("graze_nodes_against_the_rock", gNear >= 0.1f, ">= 0.1 % of the sampled nodes within 5 cm of the rock while the mass follows a straight line tangent to its flank (edge of the mass 0.1 m inside the width of the rock at its height, so the contact is by construction; the maximum over the snapshots, not the median: a graze lasts a few frames; 0.1 % is two nodes of about 1100; the log gives the line, the width and the radius)", gNear.ToString("0.0"));
                            ctx.Assert("graze_reaches_the_goal", gDone, "the mass reaches the end of the tangent line", gDone);
                            if (mass != null) MassDev.Destroy(mass);
                            mass = null;
                        }
                    }

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
                    float lowH = 0.5f * 0.45f * r;
                    Vector3 blockAt = LowStoneSite(ctx, player.transform.position, r);
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
                        ai.DevStraight = true;      // straight at the goal: the crossing tests the GPU simulation, not the vanilla walk (decision 267)
                        yield return ctx.WaitUntil(() => visual.GpuDriven, 10f);
                        Vector3 camSide = V14_GpuMassSandbox.ClearSide(new[] { from, sb.center, to }, Vector3.Cross(Vector3.up, along), 3.5f, 1.2f, stone, ctx);
                        DevCamera.Free(sb.center + camSide * 3.5f + Vector3.up * 1.2f, sb.center);
                        DevCamera.Provider = new V14_GpuMassSandbox.FollowCam(() => mass != null ? mass.transform.position : sb.center, camSide, 3.5f, 1.2f, 0.3f).Pose;
                        float t0 = Time.time;
                        float maxOn = 0f, maxInside = 0f, maxInsideAny = 0f, closest = float.MaxValue;
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
                                    Measure(snap, stone, out _, out _, out float insideAny);
                                    float inside = InsideBox(snap, stone);
                                    if (ctx.ParamBool("debug", false) && snap.Sides != null)
                                    {
                                        // debug=1: the nodes more than 4 mm inside the stone: depth in the hull and branch of SolidPush, rows of the side maps (their heights over the bare ground)
                                        Transform st = stone.transform;
                                        Vector3 sh = new Vector3(st.lossyScale.x, st.lossyScale.y, st.lossyScale.z) * 0.5f;
                                        int shown = 0;
                                        for (int i = 0; i < snap.Worms * 8 && shown < 8; i += 7)
                                        {
                                            Vector3 wp = new Vector3(snap.Nodes[i].x + snap.Origin.x, snap.Nodes[i].y + snap.Origin.y, snap.Nodes[i].z + snap.Origin.z);
                                            Vector3 l = st.InverseTransformPoint(wp);
                                            l = new Vector3(l.x * st.lossyScale.x, l.y * st.lossyScale.y, l.z * st.lossyScale.z);
                                            if (!(Mathf.Abs(l.x) < sh.x - 0.004f && Mathf.Abs(l.y) < sh.y - 0.004f && Mathf.Abs(l.z) < sh.z - 0.004f)) continue;
                                            string br = WormSimRef.SolidProbe(snap.Sides, snap.Params, new Vector3(snap.Nodes[i].x, snap.Nodes[i].y, snap.Nodes[i].z), out float hd);
                                            ctx.Log($"stone node {i}: {(sh.y - l.y) * 1000f:0} mm under its top, {hd * 1000f:0} mm deep in the hull, branch '{br}', side rows from {snap.Params.Side.x + snap.Origin.y - sb.min.y:0.00} m step {snap.Params.Side.y:0.00} m (relative to the stone base)");
                                            shown++;
                                        }
                                    }
                                    maxInsideAny = Mathf.Max(maxInsideAny, insideAny);
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
                                    ctx.Log($"low stone snapshot {snapshots}: {Mathf.RoundToInt(inside * ((total + 6) / 7) / 100f)} of {(total + 6) / 7} sampled nodes inside the stone ({inside:0.00} %), centre {d:0.00} m from the stone centre");
                                }
                            }
                            float next = Time.time + PollSeconds;
                            while (Time.time < next) yield return null;
                        }
                        DevCamera.Provider = null;
                        bool crossed = closest < 0.6f;
                        ctx.Log($"low stone ({sb.size.x:0.0} x {sb.size.y:0.00} x {sb.size.z:0.0} m): {snapshots} snapshots, up to {maxOn:0.0} % of the nodes over it, {maxInside:0.00} % deeper than 4 mm inside it ({maxInsideAny:0.00} % touching any collider within 4 mm, the stone included), closest approach of the centre {closest:0.0} m ({(crossed ? "went over" : "went round")})");
                        ctx.Assert("low_no_node_in_the_stone", maxInside < 2f, "< 2 % of the sampled nodes inside the stone", maxInside.ToString("0.00"));
                        if (crossed) ctx.Assert("low_nodes_over_the_stone", maxOn >= 8f, ">= 8 % of the nodes over the stone when the centre goes over it", maxOn.ToString("0.0"));
                        ctx.Assert("low_reaches_the_objective", reachedEnd, "the mass reaches the far side", reachedEnd);
                        yield return V14_GpuMassSandbox.Shots(ctx, "low_after", () => mass != null ? mass.transform.position : sb.center, Vector3.Cross(Vector3.up, along), along, 0.2f, stone);
                        if (mass != null) MassDev.Destroy(mass);
                        mass = MassDev.SpawnAt(from, n, ZDOID.None, freezeAI: false);
                        if (mass != null)
                        {
                            WritheMassAI ai2 = mass.GetComponent<WritheMassAI>();
                            ai2.DevGoal = to;
                            ai2.DevStraight = true;
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
                GpuWormHost.DevDispMax = WormSimConst.DispMax;
                GpuWormHost.DevEdgePush = WormSimConst.SolidEdgePush;
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
        /// <summary>
        /// Where the low stone goes: 7 m from the player in the direction (of 16) whose path, 5.5 m before and 4.5 m beyond the stone, is free of any collider but the terrain and whose ground is the
        /// flattest (a 13 cm stone on a slope is buried on one side, and a rock or a tree on the path is a collider the mass meets and the measures count). Logged.
        /// </summary>
        private static Vector3 LowStoneSite(ScenarioContext ctx, Vector3 from, float r)
        {
            int mask = LayerMask.GetMask(VanillaNames.LayerDefault, VanillaNames.LayerStaticSolid, VanillaNames.LayerDefaultSmall, VanillaNames.LayerPiece);
            float stoneH = 0.5f * 0.45f * r;       // the height of the stone: a slope under 3 m smaller than that does not bury a side of it
            Vector3 best = from + Vector3.forward * 7f;
            float bestDev = float.MaxValue;
            bool bestFree = false;
            for (int k = 0; k < 16; k++)
            {
                Vector3 dir = Quaternion.AngleAxis(k * 22.5f, Vector3.up) * Vector3.forward;
                Vector3 c = from + dir * 7f;
                c.y = V14_GpuMassSandbox.GroundOf(c, from.y);
                Vector3 mid = c + dir * -0.5f + Vector3.up * 0.9f;
                bool free = !Physics.CheckBox(mid, new Vector3(Mathf.Max(1.5f, r + 0.6f), 0.8f, 5f), Quaternion.LookRotation(dir, Vector3.up), mask, QueryTriggerInteraction.Ignore);
                float dev = DevWorld.FlatDeviation(c, 3f);
                // order: free of colliders first, then a ground flatter than the stone is high, then the flattest
                bool flatEnough = dev < stoneH;
                bool bestFlat = bestDev < stoneH;
                bool better = free != bestFree ? free
                    : flatEnough != bestFlat ? flatEnough
                    : dev < bestDev;
                if (better)
                {
                    best = c;
                    bestDev = dev;
                    bestFree = free;
                }
            }
            string dv = bestDev < float.MaxValue ? bestDev.ToString("0.00") : "?";
            ctx.Log($"low stone site at ({best.x:0.0},{best.z:0.0}): path free of colliders {bestFree}, ground level within {dv} m over 3 m (stone height {stoneH:0.00} m)");
            if (!(bestDev < stoneH))
                ctx.Log($"NOTE: no site found with a ground level within the stone height over 3 m: the best one is used, {dv} m over 3 m for a {stoneH:0.00} m stone (a side of the stone is buried or overhangs, the nodes-in-the-stone measure is less reliable)");
            if (!bestFree)
                ctx.Log("NOTE: no site found with a path free of colliders: the best one is used");
            return best;
        }

        /// <summary>Percentage of the sampled nodes more than 4 mm inside the collider of a box (nodes resting on its surface are not inside it).</summary>
        private static float InsideBox(GpuMassDev.NodeSnapshot snap, Collider box)
        {
            Transform t = box.transform;
            Vector3 half = new Vector3(t.lossyScale.x, t.lossyScale.y, t.lossyScale.z) * 0.5f;   // a unit cube primitive
            int sampled = 0, inside = 0;
            for (int i = 0; i < snap.Worms * 8; i += 7)
            {
                Vector3 p = new Vector3(snap.Nodes[i].x + snap.Origin.x, snap.Nodes[i].y + snap.Origin.y, snap.Nodes[i].z + snap.Origin.z);
                sampled++;
                Vector3 l = t.InverseTransformPoint(p);
                l = new Vector3(l.x * t.lossyScale.x, l.y * t.lossyScale.y, l.z * t.lossyScale.z);
                if (Mathf.Abs(l.x) < half.x - 0.004f && Mathf.Abs(l.y) < half.y - 0.004f && Mathf.Abs(l.z) < half.z - 0.004f) inside++;
            }
            return 100f * inside / Mathf.Max(1, sampled);
        }

        /// <summary>
        /// Percentages of the sampled nodes: within 5 cm of <paramref name="col"/> (near), inside it (<paramref name="inside"/>: in the box of its bounds shrunk by 4 mm, as <see cref="InsideBox"/>, and
        /// within 4 mm of its collider, so that the corners of the box of a round rock do not count), and within 4 mm of any collider of the mask, <paramref name="col"/> included (touching).
        /// </summary>
        private static void Measure(GpuMassDev.NodeSnapshot snap, Collider col, out float near, out float inside, out float touching)
        {
            int mask = LayerMask.GetMask(VanillaNames.LayerDefault, VanillaNames.LayerStaticSolid, VanillaNames.LayerDefaultSmall, VanillaNames.LayerPiece);
            Bounds box = col.bounds;
            box.Expand(-0.008f);       // 4 mm on each side
            int sampled = 0, nearCount = 0, insideCount = 0, touchCount = 0;
            for (int i = 0; i < snap.Worms * 8; i += 7)
            {
                Vector3 p = new Vector3(snap.Nodes[i].x + snap.Origin.x, snap.Nodes[i].y + snap.Origin.y, snap.Nodes[i].z + snap.Origin.z);
                sampled++;
                if (Physics.CheckSphere(p, 0.004f, mask, QueryTriggerInteraction.Ignore)) touchCount++;
                foreach (Collider c in Physics.OverlapSphere(p, 0.05f, mask, QueryTriggerInteraction.Ignore))
                    if (c == col) { nearCount++; break; }
                if (!box.Contains(p)) continue;
                foreach (Collider c in Physics.OverlapSphere(p, 0.004f, mask, QueryTriggerInteraction.Ignore))
                    if (c == col) { insideCount++; break; }
            }
            near = 100f * nearCount / Mathf.Max(1, sampled);
            inside = 100f * insideCount / Mathf.Max(1, sampled);
            touching = 100f * touchCount / Mathf.Max(1, sampled);
        }
    }
}
#endif
