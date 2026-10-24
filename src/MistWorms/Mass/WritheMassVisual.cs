using System;
using MistWorms.Assets;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Mass
{
    /// <summary>
    /// Client side look of a writhing mass (spec §4.5, §12.1, plan 4B-1 point 4 and 5): the boiling dome at the smoothed radius and up to
    /// Performance.MaxVisibleSurfaceWorms crawling worms on a Fibonacci spiral over it. Runs on every client from the ZDO count, with no
    /// RPC. Transitions (growth, merge, dissolve, stream) come with phase 4B-4.
    /// </summary>
    internal sealed class WritheMassVisual : MonoBehaviour, IWormRenderSource
    {
        private const float GoldenAngle = 2.39996323f;
        private const float SkirtMargin = 0.95f;        // the spiral stops short of the dome skirt
        private const float SlideSpeed = 0.15f;         // m/s along the dome (spec §12.1: slowly)
        private const float MinSlideRadius = 0.3f;      // keeps the angular speed finite near the apex
        private const float MinRadius = 0.1f;
        private const float CrawlPhaseStep = 6.1f;      // frames between two surface worms of the crawl cycle
        private const float ScaleMin = 0.9f;
        private const float ScaleSpread = 0.2f;
        private const float ScaleGolden = 0.618034f;

        private WritheMassState _state;
        private bool _registered;
        private float _shownRadius = -1f;
        private int _lastN = -1;
        private float _domePhase;
        private WormEffects.Limiter _lossFx;
        private float _streamK;          // 0 = dome, 1 = ribbon (spec 4.7: 0.5 s each way)
        private float _ribbonWidth;      // last width/length seen while streaming, kept while the ribbon relaxes back to the dome
        private float _ribbonLength;

        /// <summary>Radius the dome is drawn with right now (smoothed toward r(n)).</summary>
        public float ShownRadius => _shownRadius;

        private void Start()
        {
            _state = GetComponent<WritheMassState>();
            _domePhase = (GetInstanceID() & 0xFF) * 0.05f;
            // Rendering is client only; a dedicated server has no renderer and no assets.
            if (Application.isBatchMode || !AssetCatalog.IsReady) return;
            WormRenderer.Instance.Register(this);
            _registered = true;
        }

        private void OnDestroy()
        {
            if (_registered && WormRenderer.Existing != null) WormRenderer.Existing.Unregister(this);
            _registered = false;
        }

        private void Update()
        {
            if (_state == null || !_state.Valid || Application.isBatchMode) return;

            int n = _state.N;
            if (n <= 0) return;

            float target = MassMath.Radius(n);
            if (_shownRadius < 0f) _shownRadius = target;    // first sight: no growth, no loss effect (spec §4.5 smoothing only on change)
            else _shownRadius = MassMath.SmoothRadius(_shownRadius, target, Time.deltaTime);

            if (_lastN >= 0 && n < _lastN) PlayLoss(_lastN - n);
            _lastN = n;

            // Dome <-> ribbon: the shape follows the Stream bit over MassStream.TransitionSeconds (fine transitions come with phase 4B-4).
            bool stream = _state.IsStream;
            if (stream)
            {
                _ribbonWidth = Mathf.Min(Mathf.Max(MassStream.MinRibbonWidth, _state.StreamWidth), 2f * target);
                _ribbonLength = _state.RibbonLength;
            }
            _streamK = Mathf.MoveTowards(_streamK, stream ? 1f : 0f, Time.deltaTime / MassStream.TransitionSeconds);
        }

        /// <summary>
        /// Plan 4B-1 point 5: k worms lost, k green death effects at random points of the surface. The limiter caps the colony effects at
        /// WormEffects.MaxPerSecond (the sound shares it); nothing is sent over the network, every client diffs the count it sees.
        /// </summary>
        private void PlayLoss(int lost)
        {
            float now = Time.time;
            Vector3 root = transform.position;
            float r = Mathf.Max(MinRadius, _shownRadius);
            int count = Math.Min(lost, WormEffects.MaxPerSecond);
            for (int i = 0; i < count; i++)
            {
                Vector3 d = UnityEngine.Random.onUnitSphere;
                if (d.y < 0f) d.y = -d.y;
                Vector3 p = root + new Vector3(d.x * r, d.y * r * MassMath.DomeHeightFactor, d.z * r);
                WormEffects.PlayDeath(ref _lossFx, p, now);
            }
        }

        // ------------------------------------------------------------------ IWormRenderSource

        public void CollectInstances(WormRenderer.Sink sink)
        {
            if (_state == null || !_state.Valid) return;
            int n = _state.N;
            if (n <= 0) return;

            float now = Time.time;
            float r = Mathf.Max(MinRadius, _shownRadius < 0f ? MassMath.Radius(n) : _shownRadius);
            Vector3 root = transform.position;

            // Dome scale (r, 0.8 r, r); the ribbon is flat (height x StreamHeightFactor), narrow (the passage) and long, along the forward axis.
            Vector3 scale = MassMath.DomeScale(r);
            Quaternion yaw = Quaternion.identity;
            if (_streamK > 0f)
            {
                var ribbon = new Vector3(0.5f * _ribbonWidth, scale.y * MWConfig.Mass.StreamHeightFactor.Value, 0.5f * _ribbonLength);
                float k = _streamK * _streamK * (3f - 2f * _streamK);     // smoothstep
                scale = Vector3.Lerp(scale, ribbon, k);
                yaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            }

            sink.AddDome(sink.DomeFrameAt(now, _domePhase), Matrix4x4.TRS(root, yaw, scale));

            int visible = Math.Min(n, MWConfig.Performance.MaxVisibleSurfaceWorms.Value);
            for (int i = 0; i < visible; i++)
            {
                Matrix4x4 m = SurfaceMatrix(i, visible, now, scale, yaw, root);
                sink.AddWorm(WormAnim.Crawl, sink.FrameAt(WormAnim.Crawl, now, i * CrawlPhaseStep), m);
            }
        }

        /// <summary>
        /// Pose of surface worm <paramref name="i"/> of <paramref name="count"/>: on a Fibonacci spiral over the dome, sliding slowly along its
        /// parallel (half of them the other way), oriented along the motion and tangent to the surface (spec §12.1).
        /// </summary>
        internal static Matrix4x4 SurfaceMatrix(int i, int count, float time, Vector3 scale, Quaternion yaw, Vector3 root)
        {
            float y = 1f - (i + 0.5f) / count * SkirtMargin;                  // unit hemisphere height
            float rho = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float dir = (i & 1) == 1 ? -1f : 1f;
            float phi = i * GoldenAngle + dir * time * SlideSpeed / Mathf.Max(MinSlideRadius, rho * Mathf.Max(scale.x, scale.z));

            float cos = Mathf.Cos(phi), sin = Mathf.Sin(phi);
            var unit = new Vector3(rho * cos, y, rho * sin);
            Vector3 pos = root + yaw * new Vector3(unit.x * scale.x, unit.y * scale.y, unit.z * scale.z);

            // Ellipsoid normal: gradient of (x/rx)^2 + (y/ry)^2 + (z/rz)^2. On a ribbon the same spiral spreads the worms along its length.
            Vector3 normal = new Vector3(unit.x / scale.x, unit.y / scale.y, unit.z / scale.z).normalized;
            Vector3 tangent = Vector3.ProjectOnPlane(new Vector3(-sin, 0f, cos) * dir, normal);
            if (tangent.sqrMagnitude < 1e-6f) tangent = Vector3.ProjectOnPlane(Vector3.forward, normal);
            Quaternion rot = yaw * Quaternion.LookRotation(tangent.normalized, normal);

            float s = ScaleMin + ScaleSpread * Frac(i * ScaleGolden);
            return Matrix4x4.TRS(pos, rot, new Vector3(s, s, s));
        }

        private static float Frac(float v) => v - Mathf.Floor(v);
    }
}
