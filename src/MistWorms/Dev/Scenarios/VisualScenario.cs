#if MW_DEV
using System.Collections;
using System.Collections.Generic;
using MistWorms.Assets;
using MistWorms.Config;
using MistWorms.Dev.Preview;
using MistWorms.Mass;
using MistWorms.Render;
using MistWorms.Render.Gpu;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// Shared flow of the phase 2 visual scenarios V01-V04 (spec §15.5): noon, clear state, a clear spot without
    /// vegetation or pieces, grass and player hidden, preview, 3/4 camera, screenshots, then assertions on assets,
    /// materials and the renderer. Visual judgement is done by reading the screenshots; the assertions only prove
    /// something was drawn. State is always restored on exit (pass keep=true to leave the preview and camera in place).
    /// </summary>
    internal abstract class VisualScenario : IDevScenario
    {
        private const float ClearRadius = 2.5f;
        protected const float SequenceGap = 3f;        // sequences and clips of the masses: the camera 3 m from the edge of the swarm, 1.4 m up
        protected const float SequenceHeight = 1.4f;
        private const float MaxSlopeDeg = 12f;
        private static readonly float[] Yaws = { 35f, -40f };

        public abstract string Name { get; }

        /// <summary>Preview request: kind, n, anim, count, compare.</summary>
        protected abstract void Describe(out string kind, out int n, out string anim, out int count, out string compare);

        /// <summary>Height of the camera of the standard angles over the centre of the subject, in the share of the horizontal distance (<see cref="DevPreview.CameraPoseAt"/>).</summary>
        private const float PoseRise = 0.45f;

        /// <summary>
        /// Horizontal camera distance of the standard angles for a subject of the given size (its diameter, m): computed from the radius of the subject and the field of view, so that the
        /// subject takes 50 % of the height of the image (<see cref="ScenarioContext.FitSphere"/>; D12), the camera standing <see cref="PoseRise"/> x the distance above it.
        /// </summary>
        protected virtual float CameraDistance(float size) => ScenarioContext.FitSphere(size * 0.5f) / Mathf.Sqrt(1f + PoseRise * PoseRise);

        /// <summary>Rough subject size known before the preview exists, to pick a clear spot.</summary>
        protected virtual float EstimateSize(string kind, int n) =>
            kind == "mass" ? 2f * MassMath.Radius(n < 1 ? 10 : n) + 3.5f : 0.6f;

        protected virtual float WormSpacing => 0.5f;

        protected virtual bool TopShot => false;

        /// <summary>Minimum height of the subject ground above the water level (m).</summary>
        protected virtual float MinHeightAboveWater => 0.5f;

        /// <summary>Extra test on the ground point under the subject, after the shared ones.</summary>
        protected virtual bool AcceptSubject(Vector3 subjectGround) => true;

        /// <summary>Time of day forced for the scenario (0.5 = noon, 0 = midnight); anything but noon is released at the end.</summary>
        protected virtual float TimeOfDay => 0.5f;

        /// <summary>Scenario specific captures and assertions, run after the standard angle shots (the preview exists, the camera is free).</summary>
        protected virtual IEnumerator Extra(ScenarioContext ctx, DevPreview preview)
        {
            yield break;
        }

        /// <summary>True for the scenarios that look at a mass in its real setting, the infested mine (parameter where=open gives the old open-ground setting).</summary>
        protected virtual bool DefaultInMine => false;

        /// <summary>Set once the subject stands in the mine.</summary>
        protected bool InMine { get; private set; }

        /// <summary>Count really used (the mine view may shrink it when no floor is big enough).</summary>
        protected int ActualN { get; private set; }

        /// <summary>What a camera must not be inside of or look through: the solid layers and the terrain (open ground has rocks and slopes as well as the mine walls).</summary>
        internal static int CameraMask() => SolidMask() | LayerMask.GetMask("terrain");

        /// <summary>
        /// A camera position at <paramref name="horizontal"/> metres from <paramref name="root"/> along <paramref name="dir"/> (or along one of the three
        /// other quarter turns) at <paramref name="height"/> (or 1 m, then 2.5 m higher), with nothing solid on it and a clear line to <paramref name="look"/>.
        /// Returns false (and the first candidate) when none is clear.
        /// </summary>
        /// <summary>
        /// Camera of the clips of the masses: three quarters front (35 degrees to the left of the heading, a mirrored try when blocked, then any side),
        /// <paramref name="gap"/> metres from the edge of the swarm, <paramref name="height"/> up, looking at the floor under its centre (a dive of about
        /// 15 to 20 degrees). The clear view check of <see cref="ClearCamera"/> is kept. Returns false when no side is clear (the first candidate is returned).
        /// </summary>
        internal static bool ClipPose(WritheMassVisual v, float gap, float height, out Vector3 pos, out Vector3 look)
        {
            look = v.Root + Vector3.up * 0.1f;
            float horizontal = Mathf.Max(0.3f, v.ShownRadius) + gap;
            Vector3 left = (v.Forward * 0.82f + v.Left * 0.57f).normalized;
            Vector3 right = (v.Forward * 0.82f - v.Left * 0.57f).normalized;
            Vector3 aim = v.Root + Vector3.up * (0.35f * Mathf.Max(0.3f, v.ShownRadius));
            int mask = CameraMask();
            foreach (Vector3 d in new[] { left, right })
            {
                Vector3 c = v.Root + d * horizontal + Vector3.up * height;
                if (!Physics.CheckSphere(c, 0.4f, mask, QueryTriggerInteraction.Ignore) && !Physics.Linecast(c, aim, mask, QueryTriggerInteraction.Ignore)) { pos = c; return true; }
            }
            return ClearCamera(v.Root, left, horizontal, height, aim, out pos);
        }

        internal static bool ClearCamera(Vector3 root, Vector3 dir, float horizontal, float height, Vector3 look, out Vector3 pos)
        {
            int mask = CameraMask();
            pos = root + dir.normalized * horizontal + Vector3.up * height;
            foreach (float lift in new[] { 0f, 1f, 2.5f })
            {
                for (int k = 0; k < 4; k++)
                {
                    Vector3 d = Quaternion.AngleAxis(90f * k, Vector3.up) * dir.normalized;
                    Vector3 c = root + d * horizontal + Vector3.up * (height + lift);
                    if (Physics.CheckSphere(c, 0.4f, mask, QueryTriggerInteraction.Ignore) || Physics.Linecast(c, look, mask, QueryTriggerInteraction.Ignore)) continue;
                    pos = c;
                    return true;
                }
            }
            return false;
        }

        internal static int SolidMask() =>
            LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "vehicle", "item");

        public IEnumerator Run(ScenarioContext ctx)
        {
            bool keep = ctx.ParamBool("keep", false);
            bool flipbookSaved = GpuFreeWorms.ForceFlipbook;
            bool clutterHidden = false;
            bool timeForced = false;
            var hiddenRenderers = new List<Renderer>();
            GameObject torch = null;
            bool movedPlayer = false;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Assert("assets_loaded", AssetCatalog.IsReady, true, AssetCatalog.IsReady ? "ready" : AssetCatalog.Error);
                bool materials = MaterialFactory.EnsureBuilt();
                ctx.Assert("materials_resolved", materials, true,
                    materials ? $"{MaterialFactory.ShaderName} via {MaterialFactory.ShaderSource}" : MaterialFactory.Error);
                if (!AssetCatalog.IsReady || !materials) yield break;
                ctx.Log($"instancing supported: worm={MaterialFactory.WormInstancing} nest={MaterialFactory.NestInstancing}");

                // Noon so the lighting is the same in every capture; the vanilla console command is the supported way.
                ctx.Console($"{VanillaNames.CmdTimeOfDay} {TimeOfDay.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}");
                timeForced = true;
                DevPreview.Instance.Clear();

                Describe(out string kind, out int n, out string anim, out int count, out string compare);
                n = ctx.ParamInt("n", n);
                count = ctx.ParamInt("count", count);
                compare = ctx.Param("compare", compare);
                float distanceParam = ctx.ParamFloat("distance", 0f);
                // D10: the free worms stay flipbooks (S11 measures 2 to 3 draw calls for all of them): the worm previews are judged as flipbooks, and the renderer counts their instances
                if (kind == "worm") GpuFreeWorms.ForceFlipbook = true;
                ctx.Log($"render path of the masses: {(ScenarioContext.GpuMasses ? "GPU colony (the reference)" : "CPU layout (force_cpu=1, no compute or GpuWorms Off)")}");

                // Grass hidden for the whole scenario (restored in finally); ClearAll also sets m_forceRebuild for the restore.
                if (ClutterSystem.instance != null)
                {
                    ClutterSystem.instance.enabled = false;
                    ClutterSystem.instance.ClearAll();
                    clutterHidden = true;
                }

                // Clear spot: no trees, rocks, bushes or pieces around the subject and the camera. The spiral search and the
                // teleport are the shared helper (ScenarioContext.MoveToOpenGround); the subject and camera requirements of
                // this scenario are passed to it as an extra test on the player's ground point.
                Player player = Player.m_localPlayer;
                float subjectDistance = distanceParam > 0f ? distanceParam : DevPreview.DefaultDistance(kind, n);
                float estSize = EstimateSize(kind, n);
                float estCamDist = CameraDistance(estSize);
                Vector3 fwd = player.transform.forward;
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
                int solid = SolidMask();
                int terrain = LayerMask.GetMask("terrain");
                float water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel + MinHeightAboveWater : float.MinValue;
                float subjectRadius = Mathf.Max(ClearRadius, estSize * 0.5f);
                var own = new HashSet<Collider>(player.GetComponentsInChildren<Collider>(true));

                bool AcceptSpot(Vector3 pg)
                {
                    Vector3 subject = pg + fwd * subjectDistance;
                    if (!Ground(subject, terrain, out Vector3 sg) || sg.y <= water) return false;
                    if (!Free(sg, subjectRadius, solid, own)) return false;
                    if (!AcceptSubject(sg)) return false;
                    for (int c = 0; c < Yaws.Length; c++)
                    {
                        Vector3 camXz = sg + Quaternion.AngleAxis(Yaws[c], Vector3.up) * -fwd * estCamDist;
                        if (!Ground(camXz, terrain, out Vector3 cg) || !Free(cg, ClearRadius, solid, own)) return false;
                    }
                    return true;
                }

                string where = ctx.Param("where", DefaultInMine ? "mine" : "open");
                InMine = false;
                if (kind == "mass" && where != "open")
                {
                    float r0 = MassMath.Radius(n < 1 ? 10 : n);
                    player.SetGodMode(true);
                    movedPlayer = true;
                    yield return ctx.TpMine();
                    if (ctx.LastTpMine.Ok)
                    {
                        yield return ctx.Wait(1f);
                        player = Player.m_localPlayer;
                        var site = new MineSiteResult();
                        Vector3 here = player.transform.position;
                        float rSite = r0;
                        // A site where the camera can stand 3.5 m from the edge, 1.6 m up, with a clear view of the swarm (tried first, dropped if nothing passes).
                        yield return MineSite.Find(here, r0 + 4.5f, MineSite.MinimumFor(r0), site,
                            (floor, clearance) => ClearCamera(floor, Vector3.right, rSite + SequenceGap, SequenceHeight, floor + Vector3.up * (0.35f * rSite), out _));
                        if (site.Found && !site.ExtraTestPassed) ctx.Log("mine site: no floor with a clear camera 3.5 m from the edge; using the best floor without that test");
                        if (site.Distribution != null) ctx.Log("mine site: " + site.Distribution);
                        if (site.Found && site.Clearance < MineSite.MinimumFor(r0))
                        {
                            // No room for this swarm: a smaller one for the mine view (logged), the biggest that fits the best floor.
                            int smaller = MineSite.FitCount(site.Clearance - 0.8f);
                            ctx.Log($"mine view: no floor with {MineSite.MinimumFor(r0):0.0} m free radius for n={n} (r {r0:0.00} m); using n={smaller} (r {MassMath.Radius(smaller):0.00} m) on a {site.Clearance:0.0} m clearance");
                            n = smaller;
                            r0 = MassMath.Radius(n);
                        }
                        ctx.Assert("mine_site_found", site.Found, $"flat floor with >= {MineSite.MinimumFor(r0):0.0} m free radius", site.Found ? $"{site.Floor} clearance {site.Clearance:0.0} m after {site.Tested} tests, {site.FloorSamples} floor samples, {site.Rooms} rooms" : site.Error);
                        if (site.Found)
                        {
                            var moved = new TeleportOutcome();
                            yield return DevWorld.TeleportPlayer(site.Floor + Vector3.up * 0.3f, Player.m_localPlayer.transform.rotation, false, 30f, moved);
                            InMine = moved.Ok;
                            if (moved.Ok) DevPreview.Instance.ForcedOrigin = site.Floor;
                            if (moved.Ok && Player.m_localPlayer != null) S18_GroupCoherence.ClearCreatures(ctx, Player.m_localPlayer);   // our masses are hostile to every faction: the creatures of the mine would kill them
                            else ctx.Log("could not move inside the mine: " + moved.Error);
                        }
                    }
                    else ctx.Log($"mine not reachable ({ctx.LastTpMine.Error}): falling back to open ground");
                    player = Player.m_localPlayer;
                }

                if (!InMine)
                {
                    if (movedPlayer) { player.SetGhostMode(false); movedPlayer = false; }
                    yield return ctx.MoveToOpenGround(1f, AcceptSpot);
                    if (!ctx.LastOpenGround.Found) yield break;
                    player = Player.m_localPlayer;
                }
                ctx.Log(InMine ? "context: infested mine" : "context: open ground");

                // Player out of frame: hide its renderers during the shots.
                foreach (Renderer r in player.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.enabled) { r.enabled = false; hiddenRenderers.Add(r); }
                }

                DevPreview.Instance.WormSpacing = WormSpacing;
                string error = DevPreview.Run(kind, n, anim, count, compare, distanceParam);
                ctx.Assert("preview_created", error == null, "no error", error ?? "ok");
                if (error != null) yield break;
                DevPreview preview = DevPreview.Instance;
                ActualN = n;
                if (kind == "mass") AssertRealMass(ctx, preview, n);
                if (preview.Warning != null) ctx.Log("preview warning: " + preview.Warning);
                if (kind == "mass") ctx.Log($"mass n={preview.MassN} radius={preview.MassRadius:0.00} m");
                if (InMine && ctx.ParamBool("torch", true)) torch = SpawnTorch(ctx, preview);

                float size = Mathf.Max(preview.Size, 0.6f);
                float camDist = CameraDistance(size);
                ctx.Log($"framing: subject size {size:0.00} m, camera distance {camDist:0.00} m");

                // Let the dummy's Animator and the renderer settle before the first capture.
                PlaceCamera(ctx, preview, Yaws[0], camDist);
                yield return ctx.Wait(1f);
                yield return ctx.Shot("angle_a");

                PlaceCamera(ctx, preview, Yaws[1], camDist);
                yield return ctx.WaitFrames(10);
                yield return ctx.Shot("angle_b");

                if (TopShot)
                {
                    // straight down, from the height at which the subject takes 50 % of the height of the image
                    Vector3 top = preview.Center + Vector3.up * ScenarioContext.FitSphere(preview.Radius) - preview.Forward * 0.05f;
                    DevCamera.Free(top, preview.Center);
                    yield return ctx.WaitFrames(10);
                    yield return ctx.Shot("top");
                }

                yield return Extra(ctx, preview);

                WormRenderer.FrameStats stats = WormRenderer.Last;
                ctx.Log("render stats: " + DevPreview.Describe(stats));
                WritheMassVisual drawnBy = kind == "mass" && preview.RealMassState != null ? preview.RealMassState.GetComponent<WritheMassVisual>() : null;
                if (drawnBy != null && drawnBy.GpuDriven)
                    ctx.Assert("renderer_drew_instances", GpuWormHost.DrawnWorms > 0, "> 0 worms drawn by the GPU colony (the mass is no flipbook instance there)", GpuWormHost.DrawnWorms);
                else
                    ctx.Assert("renderer_drew_instances", stats.Instances > 0, "> 0", stats.Instances);
                ctx.Assert("renderer_no_dropped_instances", stats.Dropped == 0, 0, stats.Dropped);
            }
            finally
            {
                foreach (Renderer r in hiddenRenderers)
                {
                    if (r != null) r.enabled = true;
                }
                if (clutterHidden && ClutterSystem.instance != null) ClutterSystem.instance.enabled = true;
                if (timeForced && TimeOfDay != 0.5f) ctx.Console($"{VanillaNames.CmdTimeOfDay} -1");
                DevPreview.Instance.WormSpacing = 0.5f;
                DevPreview.Instance.ForcedOrigin = null;
                GpuFreeWorms.ForceFlipbook = flipbookSaved;
                if (torch != null) { if (ZNetScene.instance != null) ZNetScene.instance.Destroy(torch); else Object.Destroy(torch); }
                if (movedPlayer && Player.m_localPlayer != null) { Player.m_localPlayer.SetGhostMode(false); Player.m_localPlayer.SetGodMode(false); }
                if (!keep)
                {
                    DevCamera.Follow();
                    DevPreview.Instance.Clear();
                }
            }
        }

        /// <summary>A vanilla ground torch beside the swarm, on the camera side, for the mine (its own lamps are far apart).</summary>
        private static GameObject SpawnTorch(ScenarioContext ctx, DevPreview preview)
        {
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(VanillaNames.PieceGroundTorch) : null;
            if (prefab == null) { ctx.Log("no ground torch prefab: the mine light only"); return null; }
            Vector3 side = Vector3.Cross(Vector3.up, preview.Forward);
            Vector3 pos = preview.Origin + side * (preview.MassRadius + 1.6f) + preview.Forward * (preview.MassRadius + 0.8f);
            if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 3f, SolidMask(), QueryTriggerInteraction.Ignore)) pos = hit.point;
            else pos.y = preview.Origin.y;
            GameObject go = Object.Instantiate(prefab, pos, Quaternion.identity);
            Fireplace fire = go.GetComponent<Fireplace>();
            if (fire != null) fire.AddFuel(fire.m_maxFuel);
            ctx.Log($"torch at {pos}");
            return go;
        }

        /// <summary>The subject is a real, valid mass of the asked size with exactly n x Worm.Health, and what the camera sees is its own visual.</summary>
        private static void AssertRealMass(ScenarioContext ctx, DevPreview preview, int n)
        {
            WritheMassState mass = preview.RealMassState;
            bool ok = mass != null && mass.Valid;
            ctx.Assert("real_mass_created", ok, "a valid MW_WritheMass", ok ? mass.ZdoId.ToString() : "none");
            if (!ok) return;

            int expectedN = Mathf.Min(n < 1 ? 10 : n, MWConfig.Mass.MaxCount.Value);
            float expectedHp = expectedN * MWConfig.Worm.Health.Value;
            ctx.Assert("real_mass_count", mass.N == expectedN, expectedN, mass.N);
            ctx.Assert("real_mass_health", Mathf.Abs(mass.Health - expectedHp) < 0.01f && Mathf.Abs(mass.Character.GetMaxHealth() - expectedHp) < 0.01f,
                $"{expectedHp:0.#} / {expectedHp:0.#}", $"{mass.Health:0.#} / {mass.Character.GetMaxHealth():0.#}");
            ctx.Assert("real_mass_has_visual", mass.GetComponent<WritheMassVisual>() != null, true, mass.GetComponent<WritheMassVisual>() != null);
            ctx.Log($"real mass {mass.ZdoId}: n={mass.N}, name '{mass.Character.GetHoverName()}', hit sphere {(mass.TryGetHitSphere(out _, out float hr) ? hr.ToString("0.00") : "none")} m");
        }

        /// <summary>Places the camera at the yaw, nudging by +20 degrees (up to 6 tries) while the line to the subject is blocked.</summary>
        private static void PlaceCamera(ScenarioContext ctx, DevPreview preview, float yaw, float dist)
        {
            int mask = CameraMask();
            Vector3 pos, look;
            for (int i = 0; i <= 6; i++)
            {
                float y = yaw + 20f * i;
                preview.CameraPoseAt(y, dist, 0.45f, out pos, out look);
                if (!Physics.Linecast(pos, look, out RaycastHit hit, mask, QueryTriggerInteraction.Ignore))
                {
                    if (i > 0) ctx.Log($"camera yaw nudged {yaw:0} -> {y:0} after {i} blocked tries");
                    DevCamera.Free(pos, look);
                    return;
                }
                ctx.Log($"camera line blocked at yaw {y:0} by {hit.collider.name}");
            }
            preview.CameraPoseAt(yaw, dist, 0.45f, out pos, out look);
            ctx.Log("camera: every nudge was blocked, keeping the original yaw");
            DevCamera.Free(pos, look);
        }

        // ---------------------------------------------------------------- clear spot tests (the search itself is DevWorld.MoveToOpenGround)

        /// <summary>Terrain point under the xz of <paramref name="p"/> when the slope is gentle.</summary>
        private static bool Ground(Vector3 p, int terrainMask, out Vector3 ground)
        {
            ground = default;
            if (!Physics.Raycast(p + Vector3.up * 200f, Vector3.down, out RaycastHit hit, 400f, terrainMask, QueryTriggerInteraction.Ignore)) return false;
            if (Vector3.Angle(hit.normal, Vector3.up) >= MaxSlopeDeg) return false;
            ground = hit.point;
            return true;
        }

        /// <summary>True when nothing but terrain (and the player's own colliders) lies in the 0.3 m to 4 m column above the point.</summary>
        private static bool Free(Vector3 ground, float radius, int solidMask, HashSet<Collider> own)
        {
            Collider[] hits = Physics.OverlapCapsule(ground + Vector3.up * 0.3f, ground + Vector3.up * 4f, radius, solidMask, QueryTriggerInteraction.Ignore);
            foreach (Collider c in hits)
            {
                if (!own.Contains(c)) return false;
            }
            return true;
        }
    }

    /// <summary>V01: a worm up close: crawl, rear and sleep side by side, 1 m in front of the player.</summary>
    internal sealed class V01_Worm : VisualScenario
    {
        public override string Name => "V01_Worm";

        protected override void Describe(out string kind, out int n, out string anim, out int count, out string compare)
        {
            kind = "worm"; n = 0; anim = "crawl,rear,sleep,idle"; count = 4; compare = null;
        }

        // Worms are small: wider spacing, and a straight-down shot to judge texture and head orientation. Four worms (crawl, rear, sleep, idle). The camera distance is the framed one
        // of the base class (the row of four worms takes 50 % of the height of the image).
        protected override float WormSpacing => 0.5f;

        protected override bool TopShot => true;
    }

    /// <summary>
    /// Shared flow of V02 to V04 (phase 4B-3, pass D): a real writhing mass with the AI frozen, drawn as a swarm of worms with no core.
    /// After the two standard angles: a side shot 3 m from the edge, a close-up 1 m from the edge, a 45 degree dive, the camera at the centre of
    /// the swarm (nothing within 0.5 m), a sequence of 4 shots 1 s apart (the shape changes), the cap checks by distance (when
    /// <see cref="CheckCaps"/>) and a clip. The assertions come from the swarm renderer itself (WritheMassVisual / MassRenderSystem), the judgement
    /// of the look is done on the captures and the clip.
    /// </summary>
    internal abstract class MassVisualScenario : VisualScenario
    {
        private const float InsideMinDistance = 0.5f;
        private const float SequenceInterval = 1f;
        private const int SequenceShots = 4;
        private const float ClipSeconds = 5f;

        protected override bool DefaultInMine => true;

        /// <summary>Checks the drawn cap at 8, 20, 45 m and beyond the render distance (only meaningful for a swarm big enough to reach the cap).</summary>
        protected virtual bool CheckCaps => false;

        protected override IEnumerator Extra(ScenarioContext ctx, DevPreview preview)
        {
            WritheMassState mass = preview.RealMassState;
            WritheMassVisual visual = mass != null ? mass.GetComponent<WritheMassVisual>() : null;
            ctx.Assert("mass_visual_present", visual != null, true, visual != null);
            if (visual == null) yield break;

            yield return ctx.WaitFrames(20);
            // D9: the GPU colony draws the mass (its twin is made a few frames after the visual is active); what is specific to the CPU layout (drawn places and their roles, the nearest worm to
            // the camera, the lobes, the caps by distance) is judged only on the CPU path (force_cpu=1); on the GPU path the equivalents below are used, the rest is logged.
            if (ScenarioContext.GpuMasses) yield return ctx.WaitUntil(() => visual.Gpu != null, 5f);
            bool gpu = visual.GpuDriven;
            ctx.Log(gpu ? "render path of this mass: GPU colony (worms drawn = the rope range of its twin)" : "render path of this mass: CPU layout");
            ctx.Assert("mass_visual_active", visual.Active, true, visual.Active);
            ctx.Assert("mass_ground_found", visual.Ground.Have, true, visual.Ground.Have);
            ctx.Assert("mass_root_rests_on_body", Vector3.Distance(visual.Root, mass.transform.position) < 0.15f, "< 0.15 m",
                Vector3.Distance(visual.Root, mass.transform.position));

            float r = Mathf.Max(0.3f, visual.ShownRadius);
            float domeH = 0.45f * r;
            Vector3 fwd = preview.Forward;
            Vector3 side = Vector3.Cross(Vector3.up, fwd);

            // ---- side shot framed at 50 % of the height of the image, close-up at 60 %, 45 degree dive at 45 % (D12: the distance comes from the radius of the mass and the field of view)
            Frame(r, domeH, 20f, 0.5f, out float sideH, out float sideUp);
            yield return Aim(ctx, visual, side, sideH, sideUp, "side_fit");
            AssertDrawn(ctx, visual, mass.N, "side_fit", gpu);
            if (!gpu) AssertRoles(ctx, visual, mass.N, "side_fit");
            Frame(r, domeH, 12f, 0.6f, out float closeH, out float closeUp);
            yield return Aim(ctx, visual, side, closeH, closeUp, "close");
            AssertDrawn(ctx, visual, mass.N, "close", gpu);
            Frame(r, domeH, 45f, 0.45f, out float diveH, out float diveUp);
            yield return Aim(ctx, visual, side, diveH, diveUp, "dive_45");
            AssertDrawn(ctx, visual, mass.N, "dive_45", gpu);
            if (!gpu) ctx.Log($"roles drawn at 45 degrees: {Roles(visual)}");
            else ctx.Log("roles (body, flux, border) are a CPU layout notion: not judged on the GPU path");

            // ---- camera at the centre: nothing within 0.5 m, still some worms around
            DevCamera.Free(visual.Center, visual.Center + fwd);
            yield return ctx.WaitFrames(12);
            yield return ctx.Shot("inside");
            if (!gpu)
            {
                float closest = visual.ClosestToCam;
                ctx.Assert("camera_inside_clear_radius", closest >= InsideMinDistance - 0.01f, $">= {InsideMinDistance} m", closest == float.MaxValue ? "none tracked" : closest.ToString("0.00"));
            }
            else ctx.Log("camera_inside_clear_radius is a CPU layout check (the worm nearest to the camera is tracked by the layout worker): not judged on the GPU path, see the capture inside");
            ctx.Assert("camera_inside_still_draws", visual.LastDrawn > 0, "> 0", visual.LastDrawn);

            // ---- the shape changes: 4 shots 1 s apart, lobes compared between the first and the next ones
            GpuMassDev.NodeSnapshot nodesFirst = null, nodesLast = null;
            var first = new Vector2[MassLayoutConst.LobeMax];
            float maxMove = 0f;
            var firstPose = new Vector3[MassLayoutConst.SlotCount];
            var firstSeen = new bool[MassLayoutConst.SlotCount];
            float wormMove = 0f;
            int wormMatched = 0;
            Frame(r, domeH, 20f, 0.5f, out float seqH, out float seqUp);
            for (int i = 0; i < SequenceShots; i++)
            {
                float t0 = Time.realtimeSinceStartup;
                AimNow(ctx, visual, side, seqH, seqUp);
                yield return ctx.WaitFrames(3);
                if (gpu && (i == 0 || i == SequenceShots - 1))
                {
                    GpuMassDev.NodeSnapshot snap = GpuMassDev.RequestNodes(visual.Gpu);
                    yield return ctx.WaitUntil(() => snap.Done, 5f);
                    if (snap.Done && !snap.Error) { if (i == 0) nodesFirst = snap; else nodesLast = snap; }
                }
                for (int j = 0; j < first.Length; j++)
                {
                    var c = new Vector2(visual.LobeSnapshot[j].Cx, visual.LobeSnapshot[j].Cz);
                    if (i == 0) first[j] = c;
                    else maxMove = Mathf.Max(maxMove, Vector2.Distance(first[j], c));
                }
                // Mean move of the same worms (the candidates the worker keeps: flux, border and the highest places) between the first and this capture.
                MassJob job = visual.Job;
                if (job != null && job.ReadyIdx >= 0)
                {
                    MassOutput o = job.Out[job.ReadyIdx];
                    if (i == 0)
                    {
                        for (int k = 0; k < o.CandCount; k++) { firstPose[o.Cand[k].Slot] = new Vector3(o.Cand[k].M.m03, o.Cand[k].M.m13, o.Cand[k].M.m23); firstSeen[o.Cand[k].Slot] = true; }
                    }
                    else if (i == SequenceShots - 1)
                    {
                        for (int k = 0; k < o.CandCount; k++)
                        {
                            int sl = o.Cand[k].Slot;
                            if (!firstSeen[sl]) continue;
                            wormMove += Vector3.Distance(firstPose[sl], new Vector3(o.Cand[k].M.m03, o.Cand[k].M.m13, o.Cand[k].M.m23));
                            wormMatched++;
                        }
                    }
                }
                yield return ctx.Shot("seq_" + i);
                float took = Time.realtimeSinceStartup - t0;
                if (i < SequenceShots - 1 && took < SequenceInterval) yield return ctx.Wait(SequenceInterval - took);
            }
            if (!gpu)
            {
                float meanMove = wormMatched > 0 ? wormMove / wormMatched : 0f;
                ctx.Log($"shape change over 3 s: largest lobe move {maxMove:0.000} m, mean move of {wormMatched} matched drawn worms {meanMove:0.000} m");
                ctx.Assert("shape_changes_over_the_sequence", wormMatched >= 10 && meanMove >= 0.05f, "the matched drawn worms move >= 0.05 m on average over 3 s", $"{meanMove:0.000} m over {wormMatched} worms");
            }
            else
            {
                // lobes and matched candidates are the CPU layout's; the GPU twin is a rope simulation: the mean move of every node between the first and the last readback is logged
                // (information, no threshold: a rope colony at rest has no spec'd amount of writhing; judge the captures seq_0 to seq_3 and the clip)
                float mean = -1f;
                if (nodesFirst != null && nodesLast != null && nodesFirst.Nodes.Length == nodesLast.Nodes.Length && nodesFirst.Nodes.Length > 0)
                {
                    float sum = 0f;
                    for (int q = 0; q < nodesFirst.Nodes.Length; q++)
                    {
                        Vector3 a = new Vector3(nodesFirst.Nodes[q].x, nodesFirst.Nodes[q].y, nodesFirst.Nodes[q].z) + nodesFirst.Origin;
                        Vector3 b = new Vector3(nodesLast.Nodes[q].x, nodesLast.Nodes[q].y, nodesLast.Nodes[q].z) + nodesLast.Origin;
                        sum += Vector3.Distance(a, b);
                    }
                    mean = sum / nodesFirst.Nodes.Length;
                }
                ctx.Log($"shape change over 3 s (GPU, information only): mean move of the {(nodesFirst != null ? nodesFirst.Nodes.Length : 0)} nodes {(mean >= 0f ? mean.ToString("0.000") + " m" : "not read back")}");
            }

            // ---- caps by distance (the caps are the CPU layout's)
            if (CheckCaps && gpu) ctx.Log("caps by distance (8, 20, 45 m, render distance) are a CPU layout notion: not judged on the GPU path (the LOD distances of the colony are measured by V14 and P4)");
            else if (CheckCaps && mass.N >= 150) yield return Caps(ctx, visual, side, r);
            else if (CheckCaps) ctx.Log($"caps check skipped: n={mass.N} is too small to reach the caps (the mine view shrank it)");

            // ---- clip, framed on the swarm (50 % of the height of the image from three quarters front)
            Frame(r, domeH, 20f, 0.5f, out float clipH, out float clipUp);
            bool clipClear = ClipPose(visual, Mathf.Max(0.4f, clipH - r), clipUp, out Vector3 clipPos, out Vector3 clipLook);
            if (!clipClear) ctx.Log("clip camera: no clear side, using the first");
            DevCamera.Free(clipPos, clipLook);
            if (ctx.ParamBool("light", true)) DevCamera.SetFillLight(true);
            yield return ctx.WaitFrames(10);
            double pf = visual.PerfFrames, pm = visual.PerfMain, pj = visual.PerfJobs, pe = visual.PerfEval, pd = visual.PerfDraw, pk = visual.PerfWorker, pr = visual.PerfRefreshed, pw = visual.PerfDrawn;
            yield return ctx.Clip("loop", ClipSeconds);
            DevCamera.SetFillLight(false);
            double frames = visual.PerfFrames - pf;
            double jobs = visual.PerfJobs - pj;
            if (frames > 0 && jobs > 0)
            {
                // The reference figure of the cost of one swarm: mean over the frames of the clip (build the plugin with -Configuration Profile to compare with the budget).
                ctx.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "PERF mass n={0} lod={1} cap={2}: over {3:0} frames of the clip, MAIN thread {4:0.000} ms per frame (snapshot + block copy; budget 0.05 ms in LOD0); WORKER {5:0.000} ms per job = evaluate {6:0.000} + draw {7:0.000} (budget 1 ms for all masses; {8}); {9:0} of {10} places recomputed per job, {11:0} worms drawn",
                    mass.N, visual.LastLod, visual.LastCap, frames, (visual.PerfMain - pm) / frames, (visual.PerfWorker - pk) / jobs, (visual.PerfEval - pe) / jobs, (visual.PerfDraw - pd) / jobs,
                    MWConfig.Performance.MassWorkerThread.Value ? "worker thread" : "inline on the main thread",
                    (visual.PerfRefreshed - pr) / jobs, visual.LastSlotCount, (visual.PerfDrawn - pw) / jobs));
            }
        }

        /// <summary>Horizontal distance and height over the root of the mass of a camera at <paramref name="elevationDeg"/> for which the mass takes <paramref name="fraction"/> of the height of the image.</summary>
        private static void Frame(float radius, float height, float elevationDeg, float fraction, out float horizontal, out float up)
        {
            ScenarioContext.Orbit(ScenarioContext.FitFlat(radius, height, elevationDeg, fraction), elevationDeg, out horizontal, out up);
        }

        private static string Roles(WritheMassVisual v) =>
            $"body {v.DrawnByRole[0]}, flux {v.DrawnByRole[1]}, border {v.DrawnByRole[2]} (lod {v.LastLod}, cap {v.LastCap})";

        /// <summary>Camera <paramref name="horizontal"/> metres from the centre of the swarm along <paramref name="dir"/>, then a capture.</summary>
        private static IEnumerator Aim(ScenarioContext ctx, WritheMassVisual v, Vector3 dir, float horizontal, float height, string shot)
        {
            AimNow(ctx, v, dir, horizontal, height);
            yield return ctx.WaitFrames(15);
            yield return ctx.Shot(shot);
        }

        private static void AimNow(ScenarioContext ctx, WritheMassVisual v, Vector3 dir, float horizontal, float height)
        {
            Vector3 look = v.Root + Vector3.up * (0.35f * Mathf.Max(0.3f, v.ShownRadius));
            // Closer step by step (1 m at a time, never under 1.5 m from the edge) when no side is clear at the asked distance.
            bool clear = false;
            Vector3 pos = default;
            float minH = Mathf.Min(horizontal, Mathf.Max(1.5f, v.ShownRadius + 1.5f));       // a framed camera can be closer than 1.5 m from the edge: then it is not moved in
            for (float h = horizontal; h >= minH - 1e-3f && !clear; h -= 1f)
            {
                clear = ClearCamera(v.Root, dir, h, height, look, out pos);
                if (clear && h < horizontal) ctx.Log($"camera: {horizontal - h:0.0} m closer than asked to see the swarm");
            }
            if (!clear)
            {
                ClearCamera(v.Root, dir, horizontal, height, look, out pos);
                ctx.Log("camera: no clear position on the four sides, using the first one");
            }
            DevCamera.Free(pos, look);
        }

        private static void AssertDrawn(ScenarioContext ctx, WritheMassVisual v, int n, string tag, bool gpu)
        {
            if (gpu)
            {
                // the GPU twin draws the ropes of its range (every worm of the mass, LOD and frustum aside): something, and never more than the worms of the mass
                int worms = MistWorms.Mass.GpuMassBridge.WormsFor(n);
                ctx.Assert($"drawn_count_{tag}", v.LastDrawn > 0 && v.LastDrawn <= worms, $"1..{worms} worms drawn by the GPU colony", $"{v.LastDrawn}");
                return;
            }
            int slots = MassLayout.VisibleCount(n, MassLayoutConst.SlotCount);
            int cap = v.LastCap;
            int drawn = v.LastDrawn;
            // Occlusion hides the inner and the far side places; the threshold then draws about `cap` of what remains.
            int low = (int)(0.25f * Mathf.Min(slots, cap));
            ctx.Assert($"drawn_count_{tag}", drawn >= low && drawn <= cap, $"{low}..{cap} (places {slots}, visible {v.VisibleTotal})", $"{drawn} ({Roles(v)})");
        }

        private static void AssertRoles(ScenarioContext ctx, WritheMassVisual v, int n, string tag)
        {
            int total = v.DrawnByRole[0] + v.DrawnByRole[1] + v.DrawnByRole[2];
            ctx.Assert($"roles_all_present_{tag}", total > 0 && v.DrawnByRole[0] > 0 && v.DrawnByRole[1] > 0 && (n < MassLayoutConst.BorderMinN || v.DrawnByRole[2] > 0),
                "body, flux (and border from n = 6)", Roles(v));
            if (n < 50 || total <= 0) return;
            float body = v.DrawnByRole[0] / (float)total, flux = v.DrawnByRole[1] / (float)total, border = v.DrawnByRole[2] / (float)total;
            ctx.Assert($"role_fractions_{tag}", body >= 0.30f && body <= 0.80f && flux >= 0.12f && flux <= 0.50f && border >= 0.02f && border <= 0.25f,
                "body 0.30..0.80, flux 0.12..0.50, border 0.02..0.25", $"body {body:0.00}, flux {flux:0.00}, border {border:0.00}");
        }

        private static IEnumerator Caps(ScenarioContext ctx, WritheMassVisual v, Vector3 dir, float r)
        {
            int lod0Cap = MWConfig.Performance.MaxVisibleSurfaceWorms.Value;
            float rd = MWConfig.Performance.RenderDistance.Value;
            foreach (float d in new[] { 8f, 20f, 45f, rd + 10f })
            {
                Vector3 look = v.Root + Vector3.up * (0.35f * r);
                ClearCamera(v.Root, dir, r + d, 1.5f + 0.1f * d, look, out Vector3 camPos);
                DevCamera.Free(camPos, look);
                yield return ctx.WaitFrames(20);
                int expected = MassRenderSystem.CapAt(d, rd, lod0Cap);
                int expectedLod = d < 12f ? 0 : d < 30f ? 1 : 2;
                int drawn = v.LastDrawn;
                if (d >= rd)
                    ctx.Assert($"cap_{d:0}m", drawn == 0 && (v.CulledLastFrame || v.LastCap == 0), "nothing drawn beyond the render distance", $"drawn {drawn}, culled {v.CulledLastFrame}");
                else
                    ctx.Assert($"cap_{d:0}m", v.LastCap == expected && drawn <= expected && drawn >= (int)(0.5f * expected) && v.LastLod == expectedLod,
                        $"cap {expected}, lod {expectedLod}, drawn {(int)(0.5f * expected)}..{expected}", $"cap {v.LastCap}, lod {v.LastLod}, drawn {drawn}");
                if (d == 20f) yield return ctx.Shot("cap_20m");
            }
        }
    }

    /// <summary>V02: a mass of 10 next to a tick (spec §4.5: about a tick).</summary>
    internal sealed class V02_Mass10 : MassVisualScenario
    {
        public override string Name => "V02_Mass10";

        protected override void Describe(out string kind, out int n, out string anim, out int count, out string compare)
        {
            kind = "mass"; n = 10; anim = null; count = 1; compare = VanillaNames.Tick;
        }
    }

    /// <summary>V03: a mass of 200 next to a seeker soldier (spec §4.5: a bit smaller than the soldier).</summary>
    internal sealed class V03_Mass200 : MassVisualScenario
    {
        public override string Name => "V03_Mass200";

        protected override bool CheckCaps => true;

        protected override void Describe(out string kind, out int n, out string anim, out int count, out string compare)
        {
            kind = "mass"; n = 200; anim = null; count = 1; compare = VanillaNames.SeekerBrute;
        }
    }

    /// <summary>V04: a mass of 50 next to a seeker brood (bonus; spec table: about hip height).</summary>
    internal sealed class V04_Mass50 : MassVisualScenario
    {
        public override string Name => "V04_Mass50";

        protected override void Describe(out string kind, out int n, out string anim, out int count, out string compare)
        {
            kind = "mass"; n = 50; anim = null; count = 1; compare = VanillaNames.SeekerBrood;
        }
    }

    /// <summary>
    /// V07: the worm skin under light. At night, three preview worms (crawl, idle, crawl) are shot framed on one worm (near) then on the three (far) under a
    /// vanilla ground torch placed 1.5 m away, then under a dvergr lamp (skipped with a log line when none is found), then
    /// four crawl frames 1/12 s apart. Assertions only cover loading (lustre active, Point filtering): the judgement is visual.
    /// </summary>
    internal sealed class V07_WormLight : VisualScenario
    {
        private const float LightSideDistance = 1.0f;     // light 90 degrees to the camera axis, 1 m from the central worm
        private const float WormRadius = 0.15f;           // a worm is 0.3 m long: the near shot frames one worm (50 % of the height of the image, dive of 35 degrees)
        private const float NearElevation = 35f, FarElevation = 30f;   // the far shot frames the three worms (their bounding radius)

        /// <summary>Horizontal distance and height of the near shot (one worm) and of the far shot (the three of them), from the radius of the subject and the field of view (D12).</summary>
        private static void ShotPose(bool near, DevPreview preview, out float horizontal, out float height)
        {
            ScenarioContext.Orbit(ScenarioContext.FitSphere(near ? WormRadius : Mathf.Max(preview.Radius, WormRadius)), near ? NearElevation : FarElevation, out horizontal, out height);
        }
        private const float RaiseStep = 0.1f;
        private const int MaxRaises = 25;
        private const float FlatTolerance = 0.15f;        // ground height spread allowed at 1.5 m and 3 m around the subject
        private const float CrawlShotInterval = 1f / 12f;
        private const int CrawlShots = 4;
        private const float LightSettle = 3f;     // Fireplace.UpdateFireplace runs every 2 s
        private const float WaterClearance = 6f;
        private const float LampProbe = 0.6f;

        public override string Name => "V07_WormLight";

        protected override float TimeOfDay => 0f;

        protected override void Describe(out string kind, out int n, out string anim, out int count, out string compare)
        {
            kind = "worm"; n = 0; anim = "crawl,idle,crawl"; count = 3; compare = null;
        }

        // Side by side, perpendicular to the camera axis (the camera looks along the preview's forward axis).
        protected override float WormSpacing => 0.35f;

        // Dry ground: at least 1.5 m above the water and no water within WaterClearance of the subject (rings of samples).
        protected override float MinHeightAboveWater => 1.5f;

        protected override bool AcceptSubject(Vector3 subjectGround)
        {
            ZoneSystem zs = ZoneSystem.instance;
            if (zs == null) return true;
            float limit = zs.m_waterLevel + MinHeightAboveWater;
            // Flat: the ground stays within FlatTolerance of the subject on 8 points at 1.5 m and at 3 m.
            for (float r = 1.5f; r <= 3.01f; r += 1.5f)
            {
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI * 0.25f;
                    Vector3 s = subjectGround + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    if (!zs.GetGroundHeight(s, out float h) || Mathf.Abs(h - subjectGround.y) >= FlatTolerance) return false;
                }
            }
            for (float r = 2f; r <= WaterClearance + 0.01f; r += 2f)
            {
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI * 0.25f;
                    Vector3 s = subjectGround + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    if (!zs.GetGroundHeight(s, out float h) || h < limit) return false;
                }
            }
            return true;
        }

        protected override IEnumerator Extra(ScenarioContext ctx, DevPreview preview)
        {
            AssertLoading(ctx);

            Vector3 right = Vector3.Cross(Vector3.up, preview.Forward);
            Vector3 lightPos = preview.Center + right * LightSideDistance;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(lightPos, out float gy)) lightPos.y = gy;

            GameObject torch = Spawn(ctx, FindTorch(ctx), lightPos);
            if (torch != null)
            {
                yield return ctx.Wait(LightSettle);
                yield return Shoot(ctx, preview, "torch_near", true);
                yield return Shoot(ctx, preview, "torch_far", false);
                Despawn(torch);
            }

            Vector3 lampPos = lightPos;
            GameObject lamp = null;
            foreach (GameObject candidate in DvergrLampCandidates(ctx))
            {
                GameObject inst = Spawn(ctx, candidate, lampPos);
                yield return ctx.Wait(LampProbe);
                Light active = null;
                if (inst != null)
                {
                    foreach (Light l in inst.GetComponentsInChildren<Light>(false))
                    {
                        if (l.enabled && l.gameObject.activeInHierarchy) { active = l; break; }
                    }
                }
                ctx.Log($"dvergr lamp candidate {candidate.name}: {(active != null ? "has an active Light (" + active.type + ", range " + active.range.ToString("0.0") + ", intensity " + active.intensity.ToString("0.0") + ")" : "no active Light")}");
                if (active != null) { lamp = inst; break; }
                Despawn(inst);
            }
            if (lamp != null)
            {
                yield return ctx.Wait(LightSettle);
                yield return Shoot(ctx, preview, "dvergr_near", true);
                yield return Shoot(ctx, preview, "dvergr_far", false);
                Despawn(lamp);
            }
            else ctx.Log("no dvergr lamp prefab found: the dvergr captures are skipped");

            // Crawl sequence on the first preview worm, under a torch again so the surface reads.
            GameObject seqLight = Spawn(ctx, FindTorch(ctx), lightPos);
            if (seqLight != null) yield return ctx.Wait(LightSettle);
            Vector3 wormPos = preview.Origin - right * preview.WormSpacing + Vector3.up * 0.04f;
            for (int i = 0; i < CrawlShots; i++)
            {
                float t0 = Time.time;
                ShotPose(true, preview, out float crawlH, out float crawlUp);
                PlaceCamera(ctx, wormPos, preview, crawlH, crawlUp);
                yield return ctx.WaitFrames(2);
                yield return ctx.Shot("crawl_" + i);
                float took = Time.time - t0;
                ctx.Log($"crawl shot {i}: capture took {took * 1000f:0} ms (target spacing {CrawlShotInterval * 1000f:0} ms)");
                if (took < CrawlShotInterval) yield return ctx.Wait(CrawlShotInterval - took);
            }
            if (seqLight != null) Despawn(seqLight);
        }

        private static void AssertLoading(ScenarioContext ctx)
        {
            ctx.Assert("worm_gloss_active", MaterialFactory.WormGlossActive, true, MaterialFactory.WormGlossActive);
            foreach (string tex in new[] { "worm_albedo", "worm_normal", "worm_gloss" })
            {
                Texture2D t = AssetCatalog.Texture(tex);
                ctx.Assert(tex + "_point_filter", t != null && t.filterMode == FilterMode.Point, FilterMode.Point,
                    t != null ? (object)t.filterMode : "missing");
            }
        }

        private static IEnumerator Shoot(ScenarioContext ctx, DevPreview preview, string name, bool near)
        {
            ShotPose(near, preview, out float shotH, out float shotUp);
            PlaceCamera(ctx, preview.Center, preview, shotH, shotUp);
            yield return ctx.WaitFrames(10);
            yield return ctx.Shot(name);
        }

        /// <summary>
        /// Camera behind the worms (along -Forward), <paramref name="height"/> above the ground there, looking at the target;
        /// raised by RaiseStep while terrain or solid geometry lies on the line to the target.
        /// </summary>
        private static void PlaceCamera(ScenarioContext ctx, Vector3 target, DevPreview preview, float horizontal, float height)
        {
            Vector3 pos = target - preview.Forward * horizontal;
            float ground = ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(pos, out float gy) ? gy : target.y;
            pos.y = Mathf.Max(ground, target.y - 0.05f) + height;
            int mask = LayerMask.GetMask("terrain", "static_solid");
            int raises = 0;
            while (raises < MaxRaises && Physics.Linecast(pos, target, mask, QueryTriggerInteraction.Ignore))
            {
                pos.y += RaiseStep;
                raises++;
            }
            if (raises > 0) ctx.Log($"camera raised {raises * RaiseStep:0.0} m to clear the terrain{(raises == MaxRaises ? " (still blocked)" : string.Empty)}");
            DevCamera.Free(pos, target);
        }

        private static GameObject FindTorch(ScenarioContext ctx) =>
            FindPrefab(ctx, "torch", VanillaNames.PieceGroundTorch, new[] { "groundtorch" }, null);

        /// <summary>The named lamps first, then any prefab whose name looks like a dvergr lamp; the caller keeps the first one that really lights.</summary>
        private static List<GameObject> DvergrLampCandidates(ScenarioContext ctx)
        {
            var list = new List<GameObject>();
            ZNetScene scene = ZNetScene.instance;
            if (scene == null) return list;
            foreach (string n in VanillaNames.DvergrLampPrefabs)
            {
                GameObject p = scene.GetPrefab(n);
                if (p != null) list.Add(p);
            }
            foreach (GameObject prefab in scene.m_prefabs)
            {
                if (prefab == null || list.Contains(prefab) || list.Count >= 8) continue;
                string lower = prefab.name.ToLowerInvariant();
                if (!lower.Contains(VanillaNames.DvergrLampSearch) && !lower.Contains("dverger")) continue;
                foreach (string kind in VanillaNames.DvergrLampKinds)
                {
                    if (lower.Contains(kind)) { list.Add(prefab); break; }
                }
            }
            var names = new List<string>();
            foreach (GameObject g in list) names.Add(g.name);
            ctx.Log("dvergr lamp candidates: [" + string.Join(", ", names) + "]");
            return list;
        }

        /// <summary>Exact name first, then the first prefab whose lower case name contains one of <paramref name="words"/> (and one of <paramref name="kinds"/>).</summary>
        private static GameObject FindPrefab(ScenarioContext ctx, string what, string exact, string[] words, string[] kinds)
        {
            ZNetScene scene = ZNetScene.instance;
            if (scene == null) return null;
            if (exact != null)
            {
                GameObject p = scene.GetPrefab(exact);
                if (p != null) { ctx.Log($"{what}: prefab {exact}"); return p; }
            }
            var found = new List<string>();
            GameObject first = null;
            foreach (GameObject prefab in scene.m_prefabs)
            {
                if (prefab == null) continue;
                string lower = prefab.name.ToLowerInvariant();
                bool w = false;
                foreach (string word in words) { if (lower.Contains(word)) { w = true; break; } }
                if (!w) continue;
                bool k = kinds == null;
                if (!k) { foreach (string kind in kinds) { if (lower.Contains(kind)) { k = true; break; } } }
                if (!k) continue;
                found.Add(prefab.name);
                if (first == null) first = prefab;
            }
            ctx.Log($"{what}: {(first != null ? "using " + first.name : "none found")}; candidates [{string.Join(", ", found)}]");
            return first;
        }

        private static GameObject Spawn(ScenarioContext ctx, GameObject prefab, Vector3 pos)
        {
            if (prefab == null) return null;
            GameObject go = Object.Instantiate(prefab, pos, Quaternion.identity);
            Fireplace fire = go.GetComponent<Fireplace>();
            if (fire != null) fire.AddFuel(fire.m_maxFuel);
            ctx.Log($"spawned {prefab.name} at {pos}");
            return go;
        }

        private static void Despawn(GameObject go)
        {
            if (go == null) return;
            if (ZNetScene.instance != null) ZNetScene.instance.Destroy(go);
            else Object.Destroy(go);
        }
    }
}
#endif
