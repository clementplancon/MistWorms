#if MW_DEV
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using MistWorms.Assets;
using MistWorms.Dev.Preview;
using MistWorms.Mass;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// V11 (phase 4B-3, pass D; spec §15.5): the render of the engulfing, driven by the dev command <c>mw_mass_engulf</c> since the game logic
    /// comes with phase 5. A frozen mass of 50, then of 200, stands against a motionless player (it hunts him: mw_target); engulf 0, 0.5, then 1;
    /// two captures each (from the side, from behind the player) and a clip where the swarm rises around the player. The assertions come from the
    /// swarm renderer: the parameter is applied, the engulf lobe centres on the target at 1, the swarm reaches a column height, worms are drawn.
    /// The judgement (a column of worms around the player, no solid shape in the middle) is done on the captures.
    /// The setting is the infested mine (flat floor found by <see cref="MineSite"/>, a torch on the side); where=open gives the open-ground setting.
    /// Parameters: settle (seconds after each engulf change, default 2.5), where (mine, default, or open), torch (1, default, or 0).
    /// </summary>
    internal sealed class V11_MassEngulf : IDevScenario
    {
        private static readonly int[] Counts = { 50, 200 };
        private static readonly float[] Levels = { 0f, 0.5f, 1f };
        private const float ClipSeconds = 6f;
        private const float MinColumnHeight = 1.0f;       // at engulf 1 a drawn worm is at least this high above the floor (the column is 1.8 m)

        public string Name => "V11_MassEngulf";

        public IEnumerator Run(ScenarioContext ctx)
        {
            // 3.5 s on the GPU path: the sleeve of the colony forms in 3.5 s at most (V14 sleeve_formed_in_3.5s); 2.5 s for the CPU layout
            float settle = ctx.ParamFloat("settle", ScenarioContext.GpuMasses ? 3.5f : 2.5f);
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 240f;
            bool timeForced = false, clutterHidden = false;
            string where = ctx.Param("where", "mine");
            GameObject torch = null;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Assert("assets_loaded", AssetCatalog.IsReady, true, AssetCatalog.IsReady ? "ready" : AssetCatalog.Error);
                bool materials = MaterialFactory.EnsureBuilt();
                ctx.Assert("materials_resolved", materials, true, materials ? $"{MaterialFactory.ShaderName} via {MaterialFactory.ShaderSource}" : MaterialFactory.Error);
                if (!AssetCatalog.IsReady || !materials) yield break;

                ctx.Console("mw_kill");
                ctx.Console($"{VanillaNames.CmdTimeOfDay} 0.5");
                timeForced = true;
                DevPreview.Instance.Clear();
                if (ClutterSystem.instance != null)
                {
                    ClutterSystem.instance.enabled = false;
                    ClutterSystem.instance.ClearAll();
                    clutterHidden = true;
                }
                yield return ctx.WaitFrames(3);

                Player player = Player.m_localPlayer;
                bool inMine = false;
                Vector3 site = default;
                float siteClearance = 0f;
                float rMax = MassMath.Radius(Counts[Counts.Length - 1]);
                if (where != "open")
                {
                    player.SetGodMode(true);
                    yield return ctx.TpMineRetry();
                    if (ctx.LastTpMine.Ok)
                    {
                        yield return ctx.Wait(1f);
                        var found = new MineSiteResult();
                        yield return MineSite.Find(Player.m_localPlayer.transform.position, 1.5f * rMax + 2.5f, MineSite.AbsoluteMinClearance, found);
                        ctx.Assert("mine_site_found", found.Found, $"flat floor with >= {MineSite.AbsoluteMinClearance:0.0} m free radius", found.Found ? $"{found.Floor} clearance {found.Clearance:0.0} m after {found.Tested} tests, {found.FloorSamples} floor samples, {found.Rooms} rooms" : found.Error);
                        if (found.Distribution != null) ctx.Log("mine site: " + found.Distribution);
                        inMine = found.Found;
                        site = found.Floor;
                        siteClearance = found.Clearance;
                    }
                    else ctx.Log($"mine not reachable ({ctx.LastTpMine.Error}): falling back to open ground");
                    if (Player.m_localPlayer == null) { ctx.Assert("player_present_after_teleport", false, "a local player", "none"); yield break; }
                }
                if (!inMine)
                {
                    if (Player.m_localPlayer == null) { ctx.Assert("player_present", false, "a local player", "none"); yield break; }
                    Player.m_localPlayer.SetGhostMode(false);
                    yield return ctx.MoveToOpenGround(7f);
                    if (!ctx.LastOpenGround.Found) yield break;
                }
                ctx.Log(inMine ? "context: infested mine" : "context: open ground");

                player = Player.m_localPlayer;
                if (player == null) { ctx.Assert("player_present", false, "a local player", "none"); yield break; }
                player.SetGodMode(true);
                player.SetGhostMode(inMine);      // the mine teleport leaves the ghost mode on: nothing there hunts the player, and it keeps him out of the way
                S18_GroupCoherence.ClearCreatures(ctx, player);
                yield return ctx.Wait(0.5f);

                // The pair (player and swarm) spans 1.5 r + 0.7 around the site: a swarm too big for the floor found is replaced by the biggest that fits (logged).
                var counts = new List<int>();
                foreach (int wanted in Counts)
                {
                    int c = wanted;
                    if (inMine && 1.5f * MassMath.Radius(c) + 0.7f > siteClearance)
                    {
                        c = MineSite.FitCount((siteClearance - 0.7f) / 1.5f);
                        ctx.Log($"mine view: n={wanted} does not fit a {siteClearance:0.0} m clearance; using n={c} (r {MassMath.Radius(c):0.00} m)");
                    }
                    if (!counts.Contains(c)) counts.Add(c);
                }

                foreach (int n in counts)
                {
                    player = Player.m_localPlayer;
                    Vector3 fwd = player.transform.forward;
                    fwd.y = 0f;
                    fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
                    float r = MassMath.Radius(n);
                    float gap = r + 0.6f;                                   // player to the centre of the swarm
                    if (inMine)
                    {
                        // The pair is centred on the site: the player on one side, the swarm on the other.
                        var back = new TeleportOutcome();
                        yield return DevWorld.TeleportPlayer(site - fwd * (0.5f * gap) + Vector3.up * 0.3f, Quaternion.LookRotation(fwd, Vector3.up), false, 30f, back);
                        if (!back.Ok) { ctx.Log("could not place the player in the mine: " + back.Error); yield break; }
                        player = Player.m_localPlayer;
                        yield return ctx.Wait(0.5f);
                    }
                    Vector3 at = player.transform.position + fwd * gap;
                    if (inMine && torch == null && ctx.ParamBool("torch", true)) torch = SpawnTorch(ctx, at, fwd);

                    WritheMassState mass = MassDev.SpawnAt(at, n, ZDOID.None, freezeAI: true);
                    ctx.Assert($"n{n}_mass_created", mass != null && mass.Valid, "a valid mass", mass != null ? mass.ZdoId.ToString() : "none");
                    if (mass == null) yield break;
                    mass.SetTarget(player.GetZDOID());
                    WritheMassVisual visual = mass.GetComponent<WritheMassVisual>();
                    ctx.Assert($"n{n}_visual_present", visual != null, true, visual != null);
                    if (visual == null) yield break;

                    ctx.Console("mw_mass_engulf 0");
                    yield return ctx.Wait(settle);
                    ctx.Assert($"n{n}_target_seen", visual.HasTarget, true, visual.HasTarget);
                    ctx.Assert($"n{n}_cap_not_stale", visual.LastCap >= 100, ">= 100 (a Performance.MaxVisibleSurfaceWorms of 40 left in an old .cfg gives a sparse swarm)", visual.LastCap);

                    foreach (float level in Levels)
                    {
                        ctx.Console("mw_mass_engulf " + level.ToString("0.##", CultureInfo.InvariantCulture));
                        yield return ctx.Wait(settle);
                        string tag = $"n{n}_e{(int)(level * 100)}";

                        Aim(ctx, player, visual, side: true);
                        yield return ctx.WaitFrames(10);
                        yield return ctx.Shot(tag + "_side");
                        Aim(ctx, player, visual, side: false);
                        yield return ctx.WaitFrames(10);
                        yield return ctx.Shot(tag + "_player");

                        ctx.Assert(tag + "_engulf_applied", Mathf.Abs(visual.EngulfShown - level) < 0.02f, level, visual.EngulfShown);
                        ctx.Assert(tag + "_worms_drawn", visual.LastDrawn > 0, "> 0", visual.LastDrawn);
                        bool gpu = visual.GpuDriven;
                        if (!gpu) ctx.Log($"{tag}: drawn {visual.LastDrawn} (body {visual.DrawnByRole[0]}, flux {visual.DrawnByRole[1]}, border {visual.DrawnByRole[2]}), max height {visual.MaxDrawnHeight:0.00} m, lod {visual.LastLod}");
                        else ctx.Log($"{tag}: GPU colony, {visual.LastDrawn} worms drawn, LOD {visual.Gpu.Lod}");
                        if (level >= 1f && !gpu)
                        {
                            Lobe e = visual.LobeSnapshot[MassLayoutConst.EngulfLobe];
                            V3 t = visual.P.TargetLocal();
                            float err = Mathf.Sqrt((e.Cx - t.X) * (e.Cx - t.X) + (e.Cz - t.Z) * (e.Cz - t.Z));
                            ctx.Assert(tag + "_lobe_centred_on_target", visual.P.TargetDist >= 0f && err < 0.05f, "< 0.05 m", err.ToString("0.000"));
                            ctx.Assert(tag + "_column_height", visual.MaxDrawnHeight >= MinColumnHeight, $">= {MinColumnHeight} m", visual.MaxDrawnHeight.ToString("0.00"));
                        }
                        else if (level >= 1f)
                        {
                            // D9, GPU path: the lobe and the drawn heights are the CPU layout's. The equivalents: the simulated mass is centred on the target (the bridge puts its centre on the foot of
                            // the collider), and the highest rope node stands at the height of a column (read back from the GPU). n50 does not reach as high as n200: its sleeve is a part of the
                            // body (V14 judges the coverage by the planned sleeve), so the column of n50 is asked at 0.6 m.
                            Collider col = visual.GpuTargetCol;
                            float err = col != null ? Mathf.Sqrt(Mathf.Pow(visual.Gpu.Center.x - col.bounds.center.x, 2f) + Mathf.Pow(visual.Gpu.Center.z - col.bounds.center.z, 2f)) : float.MaxValue;
                            ctx.Assert(tag + "_gpu_mass_centred_on_target", col != null && visual.Gpu.Mode == MistWorms.Render.Gpu.GpuMassMode.Engulf && err < 0.05f, "engulf mode, centre within 0.05 m of the target", col != null ? err.ToString("0.000") : "no target collider");
                            GpuMassDev.NodeSnapshot snap = GpuMassDev.RequestNodes(visual.Gpu);
                            yield return ctx.WaitUntil(() => snap.Done, 10f);
                            float need = n >= 100 ? MinColumnHeight : 0.6f;
                            if (snap.Done && !snap.Error && col != null)
                            {
                                float foot = col.bounds.min.y, top = float.MinValue;
                                for (int q = 0; q < snap.Nodes.Length; q++) top = Mathf.Max(top, snap.Nodes[q].y + snap.Origin.y - foot);
                                ctx.Assert(tag + "_gpu_column_height", top >= need, $"highest node >= {need:0.0} m over the foot of the target", top.ToString("0.00"));
                            }
                            else ctx.Assert(tag + "_gpu_column_height", false, "nodes read back", "failed");
                        }
                    }

                    // The clip: back to 0, then up to 1 right before recording; the swarm rises while the camera watches.
                    ctx.Console("mw_mass_engulf 0");
                    yield return ctx.Wait(settle);
                    Aim(ctx, player, visual, side: true);
                    yield return ctx.WaitFrames(5);
                    ctx.Console("mw_mass_engulf 1");
                    if (ctx.ParamBool("light", true)) DevCamera.SetFillLight(true);
                    yield return ctx.Clip($"n{n}", ClipSeconds);
                    DevCamera.SetFillLight(false);

                    MassDev.Destroy(mass);
                    yield return ctx.Wait(0.5f);
                }

                WormRenderer.FrameStats stats = WormRenderer.Last;
                ctx.Assert("renderer_no_dropped_instances", stats.Dropped == 0, 0, stats.Dropped);
            }
            finally
            {
                ctx.Console("mw_kill");
                DevCamera.Follow();
                Player p = Player.m_localPlayer;
                if (p != null) { p.SetGodMode(false); p.SetGhostMode(false); }
                if (torch != null) { if (ZNetScene.instance != null) ZNetScene.instance.Destroy(torch); else Object.Destroy(torch); }
                if (clutterHidden && ClutterSystem.instance != null) ClutterSystem.instance.enabled = true;
                if (timeForced) ctx.Console($"{VanillaNames.CmdTimeOfDay} -1");
            }
        }

        private static GameObject SpawnTorch(ScenarioContext ctx, Vector3 massCentre, Vector3 fwd)
        {
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(VanillaNames.PieceGroundTorch) : null;
            if (prefab == null) { ctx.Log("no ground torch prefab: the mine light only"); return null; }
            Vector3 pos = massCentre + Vector3.Cross(Vector3.up, fwd) * 3f;
            if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 3f, LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain"), QueryTriggerInteraction.Ignore)) pos = hit.point;
            GameObject go = Object.Instantiate(prefab, pos, Quaternion.identity);
            Fireplace fire = go.GetComponent<Fireplace>();
            if (fire != null) fire.AddFuel(fire.m_maxFuel);
            ctx.Log($"torch at {pos}");
            return go;
        }

        /// <summary>
        /// Side: the camera stands perpendicular to the player / mass axis, looking at their middle, at the distance for which the swarm and the player (a body of 1.8 m) take 50 % of the
        /// height of the image (D12; the radius of the subject is the shown radius of the swarm plus 0.4 m). Player: behind the player, looking over his shoulder toward the swarm (what the
        /// player sees), at the distance for which the swarm takes 50 % of the height, never closer than 1.2 m behind the player.
        /// </summary>
        private static void Aim(ScenarioContext ctx, Player player, WritheMassVisual visual, bool side)
        {
            Vector3 p = player.transform.position;
            Vector3 m = visual.Root;
            Vector3 axis = m - p;
            axis.y = 0f;
            axis = axis.sqrMagnitude > 1e-4f ? axis.normalized : Vector3.forward;
            Vector3 perp = Vector3.Cross(Vector3.up, axis);
            Vector3 pos, look;
            float subjectR = Mathf.Max(0.3f, visual.ShownRadius) + 0.4f;
            if (side)
            {
                Vector3 mid = (p + m) * 0.5f;
                ScenarioContext.Orbit(ScenarioContext.FitFlat(subjectR, 1.8f, 15f), 15f, out float d, out float up);
                look = mid + Vector3.up * 0.7f;
                pos = mid + perp * d + Vector3.up * (0.7f + up);
            }
            else
            {
                float gap = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(m.x, 0f, m.z));
                ScenarioContext.Orbit(ScenarioContext.FitFlat(subjectR, 1.0f, 20f), 20f, out float back, out float up);
                float behind = Mathf.Max(back - gap, 1.2f);
                look = p + Vector3.up * 1.0f + axis * 0.8f;
                pos = p - axis * behind + perp * 0.6f + Vector3.up * (0.5f + up);
            }
            int mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "vehicle", "item", "terrain");
            // In a mine a wall is never far: the first of the four rotations of the camera with a clear line wins.
            Vector3 centre = side ? (p + m) * 0.5f : p;
            for (int k = 0; k < 4; k++)
            {
                Vector3 c = centre + Quaternion.AngleAxis(90f * k, Vector3.up) * (pos - centre);
                if (!Physics.CheckSphere(c, 0.3f, mask, QueryTriggerInteraction.Ignore) && !Physics.Linecast(c, look, mask, QueryTriggerInteraction.Ignore)) { pos = c; break; }
            }
            int raises = 0;
            while (raises < 20 && Physics.Linecast(pos, look, mask, QueryTriggerInteraction.Ignore)) { pos.y += 0.15f; raises++; }
            if (raises > 0) ctx.Log($"camera raised {raises * 0.15f:0.0} m");
            DevCamera.Free(pos, look);
        }
    }
}
#endif
