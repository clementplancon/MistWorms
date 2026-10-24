#if MW_DEV
using System.Collections;
using System.Collections.Generic;
using MistWorms.Assets;
using MistWorms.Config;
using MistWorms.Dev.Preview;
using MistWorms.Mass;
using MistWorms.Render;
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
        private const float MaxSlopeDeg = 12f;
        private static readonly float[] Yaws = { 35f, -40f };

        public abstract string Name { get; }

        /// <summary>Preview request: kind, n, anim, count, compare.</summary>
        protected abstract void Describe(out string kind, out int n, out string anim, out int count, out string compare);

        /// <summary>Camera distance for a subject of the given size (m).</summary>
        protected virtual float CameraDistance(float size) => Mathf.Max(2.5f, 2.2f * size);

        /// <summary>Rough subject size known before the preview exists, to pick a clear spot.</summary>
        protected virtual float EstimateSize(string kind, int n) =>
            kind == "mass" ? 2f * MassMath.Radius(n < 1 ? 10 : n) + 3.5f : 0.6f;

        protected virtual float WormSpacing => 0.5f;

        protected virtual bool TopShot => false;

        /// <summary>A "mass" subject is a real MW_WritheMass (AI frozen, no colony) rather than the static dome of the preview (phase 4B-1).</summary>
        protected virtual bool UseRealMass => true;

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

        private static int SolidMask() =>
            LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "vehicle", "item");

        public IEnumerator Run(ScenarioContext ctx)
        {
            bool keep = ctx.ParamBool("keep", false);
            bool clutterHidden = false;
            bool timeForced = false;
            var hiddenRenderers = new List<Renderer>();
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Assert("assets_loaded", AssetCatalog.IsReady, true, AssetCatalog.IsReady ? "ready" : AssetCatalog.Error);
                bool materials = MaterialFactory.EnsureBuilt();
                ctx.Assert("materials_resolved", materials, true,
                    materials ? $"{MaterialFactory.ShaderName} via {MaterialFactory.ShaderSource}" : MaterialFactory.Error);
                if (!AssetCatalog.IsReady || !materials) yield break;
                ctx.Log($"instancing supported: worm={MaterialFactory.WormInstancing} mass={MaterialFactory.MassInstancing} nest={MaterialFactory.NestInstancing}");

                // Noon so the lighting is the same in every capture; the vanilla console command is the supported way.
                ctx.Console($"{VanillaNames.CmdTimeOfDay} {TimeOfDay.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}");
                timeForced = true;
                DevPreview.Instance.Clear();

                Describe(out string kind, out int n, out string anim, out int count, out string compare);
                n = ctx.ParamInt("n", n);
                count = ctx.ParamInt("count", count);
                compare = ctx.Param("compare", compare);
                float distanceParam = ctx.ParamFloat("distance", 0f);

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

                yield return ctx.MoveToOpenGround(1f, AcceptSpot);
                if (!ctx.LastOpenGround.Found) yield break;
                player = Player.m_localPlayer;

                // Player out of frame: hide its renderers during the shots.
                foreach (Renderer r in player.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.enabled) { r.enabled = false; hiddenRenderers.Add(r); }
                }

                DevPreview.Instance.WormSpacing = WormSpacing;
                DevPreview.Instance.RealMass = kind == "mass" && UseRealMass;
                string error = DevPreview.Run(kind, n, anim, count, compare, distanceParam);
                ctx.Assert("preview_created", error == null, "no error", error ?? "ok");
                if (error != null) yield break;
                DevPreview preview = DevPreview.Instance;
                if (DevPreview.Instance.RealMass) AssertRealMass(ctx, preview, n);
                if (preview.Warning != null) ctx.Log("preview warning: " + preview.Warning);
                if (kind == "mass") ctx.Log($"mass n={preview.MassN} radius={preview.MassRadius:0.00} m, dome height {preview.MassRadius * MassMath.DomeHeightFactor:0.00} m");

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
                    Vector3 top = preview.Center + Vector3.up * 1f - preview.Forward * 0.05f;
                    DevCamera.Free(top, preview.Center);
                    yield return ctx.WaitFrames(10);
                    yield return ctx.Shot("top");
                }

                yield return Extra(ctx, preview);

                WormRenderer.FrameStats stats = WormRenderer.Last;
                ctx.Log("render stats: " + DevPreview.Describe(stats));
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
                DevPreview.Instance.RealMass = false;
                if (!keep)
                {
                    DevCamera.Follow();
                    DevPreview.Instance.Clear();
                }
            }
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
            int mask = SolidMask();
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

        // Worms are small: close camera, wider spacing, and a straight-down shot to judge texture and head orientation.
        // Four worms now (crawl, rear, sleep, idle): a little further back and closer together than with three.
        protected override float CameraDistance(float size) => 1.5f;

        protected override float WormSpacing => 0.5f;

        protected override bool TopShot => true;
    }

    /// <summary>V02: a mass of 10 next to a tick (spec §4.5: about a tick).</summary>
    internal sealed class V02_Mass10 : VisualScenario
    {
        public override string Name => "V02_Mass10";

        protected override void Describe(out string kind, out int n, out string anim, out int count, out string compare)
        {
            kind = "mass"; n = 10; anim = null; count = 1; compare = VanillaNames.Tick;
        }
    }

    /// <summary>V03: a mass of 200 next to a seeker soldier (spec §4.5: a bit smaller than the soldier).</summary>
    internal sealed class V03_Mass200 : VisualScenario
    {
        public override string Name => "V03_Mass200";

        protected override void Describe(out string kind, out int n, out string anim, out int count, out string compare)
        {
            kind = "mass"; n = 200; anim = null; count = 1; compare = VanillaNames.SeekerBrute;
        }
    }

    /// <summary>V04: a mass of 50 next to a seeker brood (bonus; spec table: about hip height).</summary>
    internal sealed class V04_Mass50 : VisualScenario
    {
        public override string Name => "V04_Mass50";

        protected override void Describe(out string kind, out int n, out string anim, out int count, out string compare)
        {
            kind = "mass"; n = 50; anim = null; count = 1; compare = VanillaNames.SeekerBrood;
        }
    }

    /// <summary>
    /// V07: the worm skin under light. At night, three preview worms (crawl, idle, crawl) are shot at 0.5 m then 2 m under a
    /// vanilla ground torch placed 1.5 m away, then under a dvergr lamp (skipped with a log line when none is found), then
    /// four crawl frames 1/12 s apart. Assertions only cover loading (lustre active, Point filtering): the judgement is visual.
    /// </summary>
    internal sealed class V07_WormLight : VisualScenario
    {
        private const float LightSideDistance = 1.0f;     // light 90 degrees to the camera axis, 1 m from the central worm
        private const float NearShot = 0.6f;              // horizontal camera distance, height NearHeight (dive about 34 degrees)
        private const float NearHeight = 0.45f;
        private const float FarShot = 2f;                 // height FarHeight (dive about 30 degrees)
        private const float FarHeight = 1.2f;
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

        protected override float CameraDistance(float size) => 1.2f;

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
                yield return Shoot(ctx, preview, "torch_0_5m", true);
                yield return Shoot(ctx, preview, "torch_2m", false);
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
                yield return Shoot(ctx, preview, "dvergr_0_5m", true);
                yield return Shoot(ctx, preview, "dvergr_2m", false);
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
                PlaceCamera(ctx, wormPos, preview, NearShot, NearHeight);
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
            PlaceCamera(ctx, preview.Center, preview, near ? NearShot : FarShot, near ? NearHeight : FarHeight);
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
