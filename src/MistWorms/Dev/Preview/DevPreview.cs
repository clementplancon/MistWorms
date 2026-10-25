#if MW_DEV
using System;
using System.Collections.Generic;
using System.Globalization;
using MistWorms.Assets;
using MistWorms.Config;
using MistWorms.Mass;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Dev.Preview
{
    /// <summary>
    /// Static preview of the phase 2 assets next to the local player: worms, a real writhing mass (swarm of worms) and a nest,
    /// optionally beside a non-networked copy of a vanilla creature's model for scale comparison.
    /// Everything is placed once, relative to the player at creation time; only the animation frames change.
    /// </summary>
    internal sealed class DevPreview : IWormRenderSource
    {
        public static readonly DevPreview Instance = new DevPreview();

        private const int WormsPerRow = 6;
        private const float MassVisualHeight = 0.7f;    // height of the swarm over its radius (lobes up to 1.25 x 0.5 r, bodies on top), for framing only

        private struct WormItem
        {
            public WormAnim Anim;
            public float Phase;
            public Matrix4x4 Matrix;
        }

        private readonly List<WormItem> _worms = new List<WormItem>();
        private bool _hasNest;
        private Vector3 _nestPos;
        private Quaternion _nestRot;
        private GameObject _dummy;
        private bool _registered;
        private WritheMassState _realMass;

        public string Kind { get; private set; } = "none";
        public int MassN { get; private set; }
        public float MassRadius { get; private set; }
        public string DummyName { get; private set; }
        public string Warning { get; private set; }

        /// <summary>Subject centre (world) and a radius that contains everything shown, for camera framing.</summary>
        public Vector3 Center { get; private set; }
        public float Radius { get; private set; }
        /// <summary>Direction the player was facing when the preview was created, flattened.</summary>
        public Vector3 Forward { get; private set; } = Vector3.forward;
        public Vector3 Origin { get; private set; }
        public bool Active => Kind != "none";
        /// <summary>Set before Show: the exact floor point of the subject (a mine interior has no terrain height); null = in front of the player.</summary>
        public Vector3? ForcedOrigin { get; set; }
        /// <summary>Distance between worms of a worm preview (m); set before Show.</summary>
        public float WormSpacing { get; set; } = 0.5f;
        /// <summary>The real mass of the current preview (AI frozen, no colony; drawn by <see cref="MassRenderSystem"/>), or null.</summary>
        public WritheMassState RealMassState => _realMass;
        /// <summary>Largest dimension of the subject (swarm diameter, comparison creature, worm row), for camera framing.</summary>
        public float Size => Radius * 2f;

        // ---------------------------------------------------------------- public API

        public void Clear()
        {
            _worms.Clear();
            _hasNest = false;
            MassN = 0;
            MassRadius = 0f;
            DummyName = null;
            Warning = null;
            Kind = "none";
            if (_dummy != null) UnityEngine.Object.Destroy(_dummy);
            _dummy = null;
            if (_realMass != null) MassDev.Destroy(_realMass);
            _realMass = null;
            if (_registered && WormRenderer.Instance != null) WormRenderer.Instance.Unregister(this);
            _registered = false;
        }

        /// <summary>Creates a preview; returns null on success or an error message.</summary>
        public string Show(string kind, int n, string anim, int count, string compare, float distance)
        {
            Player player = Player.m_localPlayer;
            if (player == null) return "no local player (not in game)";
            if (!AssetCatalog.IsReady) return "assets not loaded: " + AssetCatalog.Error;
            if (!MaterialFactory.EnsureBuilt()) return "materials not built: " + MaterialFactory.Error;

            kind = (kind ?? string.Empty).ToLowerInvariant();
            Clear();

            Vector3 fwd = player.transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);

            if (distance <= 0f) distance = DefaultDistance(kind, n);
            Vector3 origin = player.transform.position + fwd * distance;
            origin.y = ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(origin, out float h) ? h : player.transform.position.y;
            if (ForcedOrigin.HasValue) origin = ForcedOrigin.Value;

            Forward = fwd;
            Origin = origin;
            string error = null;
            switch (kind)
            {
                case "worm":
                    error = BuildWorms(origin, fwd, right, anim, count);
                    break;
                case "mass":
                    error = BuildMass(origin, fwd, right, n < 1 ? 10 : n, compare);
                    break;
                case "nest":
                    BuildNest(origin);
                    break;
                default:
                    return $"unknown preview kind '{kind}' (worm, mass, nest, clear)";
            }
            if (error != null)
            {
                Clear();
                return error;
            }

            Kind = kind;
            WormRenderer.Instance.Register(this);
            _registered = true;
            return null;
        }

        /// <summary>Camera pose looking at the subject from the player's side, yawed and raised by the given angles.</summary>
        public void CameraPose(float yawDeg, float elevationDeg, out Vector3 position, out Vector3 lookAt)
        {
            Camera cam = Utils.GetMainCamera();
            float vFov = cam != null ? cam.fieldOfView : 60f;
            float aspect = cam != null && cam.aspect > 0.1f ? cam.aspect : 16f / 9f;
            float hFov = 2f * Mathf.Atan(Mathf.Tan(vFov * 0.5f * Mathf.Deg2Rad) * aspect) * Mathf.Rad2Deg;
            float fov = Mathf.Min(vFov, hFov);
            float dist = Radius / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * 1.15f + 0.3f;

            Vector3 dir = Quaternion.AngleAxis(yawDeg, Vector3.up) * -Forward;
            float el = elevationDeg * Mathf.Deg2Rad;
            position = Center + (dir * Mathf.Cos(el) + Vector3.up * Mathf.Sin(el)) * dist;
            lookAt = Center;
        }

        /// <summary>
        /// Camera pose at an explicit distance: horizontal yaw around the subject, height 0.45 x distance, looking at the centre.
        /// A negative <paramref name="distance"/> is not allowed; yaw 0 is behind the player (looking along Forward).
        /// </summary>
        public void CameraPoseAt(float yawDeg, float distance, float heightFactor, out Vector3 position, out Vector3 lookAt)
        {
            Vector3 dir = Quaternion.AngleAxis(yawDeg, Vector3.up) * -Forward;
            position = Center + dir * distance + Vector3.up * (distance * heightFactor);
            lookAt = Center;
        }

        public Dictionary<string, object> Describe()
        {
            return new Dictionary<string, object>
            {
                { "kind", Kind },
                { "n", MassN },
                { "massRadius", MassRadius },
                { "worms", _worms.Count },
                { "nest", _hasNest },
                { "compare", DummyName },
                { "warning", Warning },
                { "center", Center },
                { "radius", Radius },
                { "origin", Origin },
            };
        }

        // ---------------------------------------------------------------- builders

        public static float DefaultDistance(string kind, int n)
        {
            switch (kind)
            {
                case "mass": return Mathf.Max(2.5f, MassMath.Radius(n < 1 ? 10 : n) + 2f);
                case "nest": return 2f;
                default: return 1f;
            }
        }

        private string BuildWorms(Vector3 origin, Vector3 fwd, Vector3 right, string animSpec, int count)
        {
            if (count < 1) count = 1;
            if (count > 400) count = 400;
            if (string.IsNullOrWhiteSpace(animSpec)) animSpec = "crawl";

            var anims = new List<WormAnim>();
            foreach (string part in animSpec.Split(','))
            {
                string a = part.Trim().ToLowerInvariant();
                if (a == "all") { anims.AddRange(new[] { WormAnim.Crawl, WormAnim.Idle, WormAnim.Rear, WormAnim.Strike, WormAnim.Sleep }); continue; }
                if (!TryParseAnim(a, out WormAnim parsed)) return $"unknown worm animation '{part}' (crawl, idle, rear, strike, sleep, all)";
                anims.Add(parsed);
            }

            // Worms face the player at roughly 3/4 so the profile and the head are both readable.
            Quaternion rot = Quaternion.LookRotation(Quaternion.AngleAxis(-35f, Vector3.up) * -fwd, Vector3.up);
            int perRow = Mathf.Min(count, WormsPerRow);
            int rows = (count + perRow - 1) / perRow;
            for (int i = 0; i < count; i++)
            {
                int col = i % perRow;
                int row = i / perRow;
                Vector3 pos = origin
                    + right * ((col - (perRow - 1) * 0.5f) * WormSpacing)
                    + fwd * (row * WormSpacing);
                _worms.Add(new WormItem
                {
                    Anim = anims[i % anims.Count],
                    Phase = i * (anims[i % anims.Count] == WormAnim.Idle ? 0.9f : 6.1f),
                    Matrix = Matrix4x4.TRS(pos, rot, Vector3.one),
                });
            }

            float halfWidth = (perRow - 1) * 0.5f * WormSpacing + 0.3f;
            float depth = (rows - 1) * 0.5f * WormSpacing;
            Center = origin + fwd * depth + Vector3.up * 0.08f;
            Radius = Mathf.Max(halfWidth, depth + 0.3f);
            return null;
        }

        private string BuildMass(Vector3 origin, Vector3 fwd, Vector3 right, int n, string compare)
        {
            float r = MassMath.Radius(n);
            MassN = n;
            MassRadius = r;

            // Always a real creature: its own visual state feeds MassRenderSystem (swarm of worms), no colony, AI frozen.
            _realMass = MassDev.SpawnAt(origin, n, ZDOID.None, freezeAI: true);
            if (_realMass == null) return "could not create the real mass (MW_WritheMass not registered, or no floor under the preview)";

            float left = -r;
            float rightEdge = r;
            float height = MassVisualHeight * r;
            if (!string.IsNullOrWhiteSpace(compare))
            {
                float ext = SpawnDummy(compare.Trim(), origin, fwd, right, r, out float dummyHeight, out float dummyX);
                if (ext > 0f)
                {
                    rightEdge = dummyX + ext;
                    height = Mathf.Max(height, dummyHeight);
                }
            }

            float mid = (left + rightEdge) * 0.5f;
            Center = origin + right * mid + Vector3.up * (height * 0.4f);
            Radius = Mathf.Max((rightEdge - left) * 0.5f, height * 0.7f);
            return null;
        }

        private void BuildNest(Vector3 origin)
        {
            _hasNest = true;
            _nestPos = origin;
            _nestRot = Quaternion.identity;
            Center = origin + Vector3.up * 0.25f;
            Radius = 0.75f;
        }

        // ---------------------------------------------------------------- comparison dummy

        /// <summary>
        /// Instantiates only the prefab's Visual child, under an inactive parent so no Awake runs, strips every script,
        /// collider and rigidbody, then activates it. No ZNetView, no AI, nothing networked. Returns the horizontal
        /// half-extent (0 when nothing was created) and where it stands along the right axis.
        /// </summary>
        private float SpawnDummy(string prefabName, Vector3 origin, Vector3 fwd, Vector3 right, float massRadius,
            out float height, out float offsetX)
        {
            height = 0f;
            offsetX = 0f;
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefabName) : null;
            if (prefab == null) { Warning = $"compare prefab '{prefabName}' not found"; return 0f; }
            Transform visual = prefab.transform.Find(VanillaNames.VisualChild);
            if (visual == null) { Warning = $"compare prefab '{prefabName}' has no '{VanillaNames.VisualChild}' child"; return 0f; }

            var root = new GameObject("MW_PreviewDummy_" + prefabName) { hideFlags = HideFlags.DontSave };
            root.SetActive(false);
            GameObject clone = UnityEngine.Object.Instantiate(visual.gameObject, root.transform, false);
            clone.name = VanillaNames.VisualChild;

            int stripped = 0;
            foreach (MonoBehaviour mb in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                try { UnityEngine.Object.DestroyImmediate(mb); stripped++; }
                catch (Exception e) { Plugin.Log.LogInfo($"[Preview] could not strip {mb.GetType().Name}: {e.Message}"); }
            }
            foreach (Collider c in clone.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            foreach (Rigidbody rb in clone.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(rb);
            foreach (Animator a in clone.GetComponentsInChildren<Animator>(true)) a.applyRootMotion = false;

            // First guess for the position; refined once the renderer bounds are known.
            root.transform.SetPositionAndRotation(origin + right * (massRadius + 2f), Quaternion.LookRotation(-fwd, Vector3.up));
            root.SetActive(true);
            _dummy = root;
            DummyName = prefabName;

            Bounds bounds = default;
            bool any = false;
            foreach (Renderer rend in root.GetComponentsInChildren<Renderer>(false))
            {
                if (!any) { bounds = rend.bounds; any = true; }
                else bounds.Encapsulate(rend.bounds);
            }
            float ext = any ? Mathf.Max(bounds.extents.x, bounds.extents.z) : 0.6f;
            height = any ? bounds.max.y - origin.y : 1f;

            offsetX = massRadius + ext + 0.5f;
            Vector3 pos = origin + right * offsetX;
            if (any)
            {
                // Keep the model's own offset from its bounds centre so it stays centred on the slot.
                Vector3 centreShift = new Vector3(bounds.center.x - root.transform.position.x, 0f, bounds.center.z - root.transform.position.z);
                pos -= centreShift;
            }
            root.transform.position = pos;
            Plugin.Log.LogInfo($"[Preview] comparison dummy {prefabName}: {stripped} scripts stripped, half extent {ext:0.00} m, height {height:0.00} m");
            return ext;
        }

        // ---------------------------------------------------------------- IWormRenderSource

        public void CollectInstances(WormRenderer.Sink sink)
        {
            float t = Time.time;
            for (int i = 0; i < _worms.Count; i++)
            {
                WormItem w = _worms[i];
                int frame;
                if (sink.IsLoop(w.Anim))
                {
                    frame = sink.FrameAt(w.Anim, t, w.Phase);
                }
                else
                {
                    // One-shots replay with a short rest between passes so the preview keeps moving.
                    float period = sink.Duration(w.Anim) + 0.8f;
                    frame = sink.FrameAt(w.Anim, Mathf.Repeat(t + w.Phase * 0.1f, period));
                }
                sink.AddWorm(w.Anim, frame, w.Matrix);
            }

            if (_hasNest)
            {
                // Slow +-3 % scale pulse (spec §12.1); the mesh itself has a single frame.
                float s = 1f + 0.03f * Mathf.Sin(t * (2f * Mathf.PI / 4f));
                sink.AddNest(Matrix4x4.TRS(_nestPos, _nestRot, new Vector3(s, s, s)));
            }
        }

        // ---------------------------------------------------------------- helpers

        private static bool TryParseAnim(string name, out WormAnim anim)
        {
            switch (name)
            {
                case "crawl": anim = WormAnim.Crawl; return true;
                case "rear": anim = WormAnim.Rear; return true;
                case "strike": anim = WormAnim.Strike; return true;
                case "sleep": anim = WormAnim.Sleep; return true;
                case "idle": anim = WormAnim.Idle; return true;
                default: anim = WormAnim.Crawl; return false;
            }
        }

        /// <summary>Shared by the console command and the bridge: runs a preview request, returns an error or null.</summary>
        public static string Run(string kind, int n, string anim, int count, string compare, float distance)
        {
            kind = (kind ?? string.Empty).ToLowerInvariant();
            if (kind == "clear")
            {
                Instance.Clear();
                return null;
            }
            return Instance.Show(kind, n, anim, count, compare, distance);
        }

        public static string Describe(WormRenderer.FrameStats s) =>
            string.Format(CultureInfo.InvariantCulture, "instances={0} culled={1} drawCalls={2} cpu={3:0.000} ms", s.Instances, s.Culled, s.DrawCalls, s.CpuMs);
    }
}
#endif
