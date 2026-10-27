using System;
using MistWorms.Config;
using MistWorms.Render;
using MistWorms.Render.Gpu;
using UnityEngine;

namespace MistWorms.Mass
{
    /// <summary>
    /// Visual v2, pass B2: the link between a <see cref="WritheMassVisual"/> (the game side, on every client: count, smoothed root, speed, heading, shown radius, target, engulf, stream)
    /// and a mass of the GPU rope colony (<see cref="GpuWormHost"/>). The CPU keeps the game (n, r(n), health, engulf, flow, merge, dissolution); the GPU only gets small parameters per mass and
    /// the environment, and nothing comes back. A mass that cannot have a GPU twin (no compute shader, no bundle, <c>Performance.GpuWorms = Off</c>, pool full, far away) is drawn by the CPU path
    /// as before (<see cref="MassRenderSystem"/> runs the layout job for it): the CPU path is kept whole.
    /// </summary>
    internal static class GpuMassBridge
    {
#if MW_DEV
        /// <summary>Dev A/B (S08 param nopseudo=1): the ramp of the pseudopod net given to the GPU stays at 0.</summary>
        internal static bool DevNoPseudo;
#endif
        public const int WormsPerUnit = 10;                  // visual worms per game unit
        public const int MaxWormsPerMass = 2400;
        public const float CreateDistance = 85f;             // a mass farther than this from the camera has no GPU twin (it is not drawn there anyway)
        public const float ReleaseDistance = 110f;
        public const float StartRadiusShare = 0.5f;          // a newborn or dissolving mass keeps this share of its radius for the simulation (the worms are born / absorbed instead)

        /// <summary>Dev (scenarios of the CPU layout): true keeps every mass on the CPU path, whatever the config.</summary>
        public static bool ForceCpu;

        private static bool s_tried, s_ok;
        private static GpuEnvCapture s_env;

        public static GpuEnvCapture Environment => s_env;

        /// <summary>True when masses can be drawn by the GPU (config, device, bundle). Loads the bundle and the host once.</summary>
        public static bool Available
        {
            get
            {
                if (ForceCpu || MWConfig.Performance.GpuWorms.Value == GpuWormsMode.Off) return false;
                if (s_tried) return s_ok && GpuWormHost.Ready;
                s_tried = true;
                try
                {
                    if (Application.isBatchMode || !GpuWormBundle.InitHost()) { s_ok = false; return false; }
                    s_env = GpuWormHost.Environment as GpuEnvCapture ?? new GpuEnvCapture();
                    GpuWormHost.Environment = s_env;
                    GpuWormHost.EnsureFree();
                    s_ok = true;
                }
                catch (Exception e)
                {
                    s_ok = false;
                    Plugin.LogException("GpuMassBridge.Init", e);
                }
                return s_ok;
            }
        }

        /// <summary>True when the free worms are ropes of the colony (the same conditions as the masses).</summary>
        public static bool FreeActive => Available && GpuFreeWorms.Enabled;

        /// <summary>Visual worms for the count of the game (10 per unit), capped.</summary>
        public static int WormsFor(int n) => Mathf.Clamp(n * WormsPerUnit, 1, MaxWormsPerMass);

        /// <summary>Capacity of the segment of the pool for a mass of <paramref name="worms"/> worms: room to grow by absorption without a new segment.</summary>
        public static int CapacityFor(int worms) => Mathf.Clamp(Mathf.Max(worms * 2, 300), worms, MaxWormsPerMass);

        public static void Release(WritheMassVisual v)
        {
            if (v == null || v.Gpu == null) return;
            if (s_env != null) s_env.Forget(v.Gpu.Slot);
            if (GpuWormHost.Ready) GpuWormHost.RemoveMass(v.Gpu);
            v.Gpu = null;
            v.GpuTargetTf = null;
            v.GpuTargetCol = null;
        }

        /// <summary>
        /// Once a frame for a registered visual: creates, updates or releases its GPU twin. True when the GPU draws the mass this frame (the caller skips the CPU layout).
        /// </summary>
        public static bool Drive(WritheMassVisual v, Vector3 cam, bool haveCamera)
        {
            if (v == null) return false;
            if (!v.Active || !Available)
            {
                if (v.Gpu != null) Release(v);
                return false;
            }
            float now = Time.realtimeSinceStartup;
            GpuMass m = v.Gpu;
            if (m != null && !m.Active) { v.Gpu = null; m = null; }

            float dist = haveCamera ? (cam - v.Root).magnitude : 0f;
            if (m != null && dist > ReleaseDistance) { Release(v); return false; }

            int n = v.N;
            float target = MassMath.Radius(Math.Max(n, 1));
            float shown = v.P.R;
            float ratio = target > 1e-4f ? Mathf.Clamp01(shown / target) : 1f;
            int want = WormsFor(n);
            // a newborn mass grows and a dissolving or merging one shrinks: the radius of the simulation holds, the worms are born or absorbed instead
            float radius = Mathf.Max(shown, StartRadiusShare * target);
            if (ratio < 0.9f) want = Mathf.Clamp(Mathf.CeilToInt(want * ratio * ratio), 1, want);

            if (m != null && want > m.WormCount) { Release(v); m = null; }
            if (m == null)
            {
                if (dist > CreateDistance || now < v.GpuRefusedUntil) return false;
                uint seed = v.Slots != null ? v.Slots.Seed : 1u;
                m = GpuWormHost.CreateMass(want, CapacityFor(want), v.Root, radius, seed) ?? GpuWormHost.CreateMass(want, want, v.Root, radius, seed);
                if (m == null)
                {
                    v.GpuRefusedUntil = now + 2f;      // the pool or the slots are full: the CPU path draws this one
                    return false;
                }
                v.Gpu = m;
                m.Owner = v.transform;
            }
            else GpuWormHost.SetActive(m, want);

            // ---- target and engulf
            Transform tf = v.TargetTransform;
            if (tf != v.GpuTargetTf)
            {
                v.GpuTargetTf = tf;
                v.GpuTargetCol = tf != null ? ColliderOf(tf) : null;
            }
            Collider col = v.GpuTargetCol;
            float engulf = v.EngulfShown;
            bool wrap = col != null && engulf > 0.001f;

            m.Radius = radius;
            m.Heading = v.Forward;
            m.Speed = v.Speed;
            m.Frozen = false;
            if (wrap)
            {
                Bounds b = col.bounds;
                m.Mode = GpuMassMode.Engulf;
                m.AdhesiveCollider = col;
                m.Engulf = engulf;
                m.Center = new Vector3(b.center.x, b.min.y, b.center.z);
                m.Height = b.size.y;
                m.ShellThickness = 0.02f;
            }
            else
            {
                m.Mode = GpuMassMode.Dome;
                m.AdhesiveCollider = null;
                m.Engulf = 0f;
                m.Center = v.Root;
                m.Height = 0f;
            }

            // ---- pseudopod: the net of worms toward the target (the CPU Arm()), not while the mass wraps its target; no allocation
            // The ramp given to the GPU follows the one of the visual when it grows and falls slowly (WormSimRef.SmoothRamp), along the last direction and length when the target is gone.
            float armWant = 0f;
            if (!wrap && v.PseudoRamp > 0f && v.P.TargetDist > 0f)
            {
                float ang = v.P.TargetTurns * (2f * Mathf.PI);
                float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                Vector3 f = v.Forward, l = v.Left;
                float dx = f.x * ca + l.x * sa, dz = f.z * ca + l.z * sa;
                float dl = Mathf.Sqrt(dx * dx + dz * dz);
                if (dl > 1e-4f)
                {
                    m.PseudoDirX = dx / dl; m.PseudoDirZ = dz / dl;
                    m.PseudoFull = WormSimRef.PseudoParams(m.PseudoDirX, m.PseudoDirZ, v.P.TargetDist, 1f, radius).z;
                    armWant = v.PseudoRamp;
                }
            }
#if MW_DEV
            if (DevNoPseudo) armWant = 0f;
#endif
            m.PseudoRamp = wrap ? 0f : WormSimRef.SmoothRamp(m.PseudoRamp, armWant, Time.deltaTime);
            m.Pseudo = m.PseudoRamp > 0f && m.PseudoFull > 0f ? new Vector4(m.PseudoDirX, m.PseudoDirZ, m.PseudoFull * m.PseudoRamp, m.PseudoRamp) : Vector4.zero;

            // ---- flow: the dome stretches into a ribbon along the passage
            float k = v.StreamK;
            if (k > 0.01f)
            {
                float smooth = k * k * (3f - 2f * k);
                m.ShapeAlong = Mathf.Lerp(radius, Mathf.Max(radius, 0.5f * v.RibbonLength), smooth);
                m.ShapeAcross = Mathf.Lerp(radius, Mathf.Max(0.15f, 0.5f * v.RibbonWidth), smooth);
                m.DomeHeight = 0.45f * Mathf.Lerp(1f, MWConfig.Mass.StreamHeightFactor.Value, smooth);
            }
            else
            {
                m.ShapeAlong = m.ShapeAcross = 0f;
                m.DomeHeight = 0.45f;
            }
            return true;
        }

        /// <summary>The collider the adhesive primitive and the skin of a target hang from: the body capsule of a character, else the first collider of the object.</summary>
        private static Collider ColliderOf(Transform tf)
        {
            Character c = tf.GetComponentInParent<Character>();
            if (c != null)
            {
                var cap = c.GetComponent<CapsuleCollider>();
                if (cap != null) return cap;
                Collider cc = c.GetComponentInChildren<Collider>();
                if (cc != null) return cc;
            }
            return tf.GetComponentInChildren<Collider>();
        }
    }
}
