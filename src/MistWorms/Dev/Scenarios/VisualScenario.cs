#if MW_DEV
using System.Collections;
using System.Collections.Generic;
using MistWorms.Assets;
using MistWorms.Dev.Preview;
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

        private static int SolidMask() =>
            LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "vehicle", "item");

        public IEnumerator Run(ScenarioContext ctx)
        {
            bool keep = ctx.ParamBool("keep", false);
            bool clutterHidden = false;
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
                ctx.Console($"{VanillaNames.CmdTimeOfDay} 0.5");
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
                float water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel + 0.5f : float.MinValue;
                float subjectRadius = Mathf.Max(ClearRadius, estSize * 0.5f);
                var own = new HashSet<Collider>(player.GetComponentsInChildren<Collider>(true));

                bool AcceptSpot(Vector3 pg)
                {
                    Vector3 subject = pg + fwd * subjectDistance;
                    if (!Ground(subject, terrain, out Vector3 sg) || sg.y <= water) return false;
                    if (!Free(sg, subjectRadius, solid, own)) return false;
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
                string error = DevPreview.Run(kind, n, anim, count, compare, distanceParam);
                ctx.Assert("preview_created", error == null, "no error", error ?? "ok");
                if (error != null) yield break;
                DevPreview preview = DevPreview.Instance;
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
                DevPreview.Instance.WormSpacing = 0.5f;
                if (!keep)
                {
                    DevCamera.Follow();
                    DevPreview.Instance.Clear();
                }
            }
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
            kind = "worm"; n = 0; anim = "crawl,rear,sleep"; count = 3; compare = null;
        }

        // Worms are small: close camera, wider spacing, and a straight-down shot to judge texture and head orientation.
        protected override float CameraDistance(float size) => 1.2f;

        protected override float WormSpacing => 0.6f;

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
}
#endif
