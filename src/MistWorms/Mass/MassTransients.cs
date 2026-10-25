using System;
using MistWorms.Colony;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Mass
{
    /// <summary>
    /// Worms that leave a mass (a loss of worms: phase 4B-3 pass F, base). A ring of <see cref="Max"/> falling worms, static so they survive
    /// the destruction of the mass that dropped them. The path is analytic: a short push along the surface normal, then a fall whose gravity is
    /// chosen to touch the ground plane exactly after <see cref="FallSeconds"/>; the green death effect plays on impact. No allocation.
    /// </summary>
    internal sealed class MassTransients : IWormRenderSource
    {
        public const int Max = 24;
        public const float FallSeconds = 0.6f;
        public const float PushSpeed = 0.8f;
        public const float SpinTurnsPerSecond = 0.9f;
        public const float GroundLift = 0.02f;

        public static readonly MassTransients Instance = new MassTransients();

        private struct Fall
        {
            public bool Live;
            public float T0;
            public Vector3 P0, V0, Axis, X, Y, Z;     // start position, start velocity, spin axis, scaled columns of the start pose
            public float G, GroundY;
            public WormAnim Anim;
            public WormLod Lod;
            public int Frame;
        }

        private readonly Fall[] _ring = new Fall[Max];
        private int _next;
        private WormEffects.Limiter _impactFx;

        /// <summary>Falls in progress (dev checks).</summary>
        public static int LiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Max; i++) if (Instance._ring[i].Live) n++;
                return n;
            }
        }

        /// <summary>Total number of worms ever dropped (dev checks).</summary>
        public static int Spawned { get; private set; }

        /// <summary>Starts a fall from the world pose <paramref name="pose"/> (instance matrix, scale included). Overwrites the oldest one when the ring is full.</summary>
        public static void Spawn(in Matrix4x4 pose, WormAnim anim, WormLod lod, int frame, float groundY, float now)
        {
            MassTransients t = Instance;
            Fall f;
            f.Live = true;
            f.T0 = now;
            f.P0 = new Vector3(pose.m03, pose.m13, pose.m23);
            f.X = new Vector3(pose.m00, pose.m10, pose.m20);
            f.Y = new Vector3(pose.m01, pose.m11, pose.m21);
            f.Z = new Vector3(pose.m02, pose.m12, pose.m22);
            Vector3 up = f.Y.sqrMagnitude > 1e-8f ? f.Y.normalized : Vector3.up;
            f.V0 = up * PushSpeed;
            f.Axis = f.Z.sqrMagnitude > 1e-8f ? f.Z.normalized : Vector3.forward;
            f.GroundY = groundY + GroundLift;
            // y(T) = y0 + vy T - g T^2 / 2 = ground
            float g = 2f * (f.P0.y + f.V0.y * FallSeconds - f.GroundY) / (FallSeconds * FallSeconds);
            f.G = g < 4f ? 4f : g;
            f.Anim = anim; f.Lod = lod; f.Frame = frame;
            t._ring[t._next] = f;
            t._next = (t._next + 1) % Max;
            Spawned++;

            if (!Application.isBatchMode) WormRenderer.Instance.Register(t);   // idempotent
        }

        public void CollectInstances(WormRenderer.Sink sink)
        {
            float now = Time.time;
            for (int i = 0; i < Max; i++)
            {
                if (!_ring[i].Live) continue;
                Fall f = _ring[i];
                float t = now - f.T0;
                if (t >= FallSeconds || t < 0f)
                {
                    _ring[i].Live = false;
                    if (t >= FallSeconds)
                        WormEffects.PlayDeath(ref _impactFx, new Vector3(f.P0.x + f.V0.x * FallSeconds, f.GroundY, f.P0.z + f.V0.z * FallSeconds), now);
                    continue;
                }

                var pos = new Vector3(f.P0.x + f.V0.x * t, f.P0.y + f.V0.y * t - 0.5f * f.G * t * t, f.P0.z + f.V0.z * t);
                Quaternion q = Quaternion.AngleAxis(360f * SpinTurnsPerSecond * t, f.Axis);
                Vector3 x = q * f.X, y = q * f.Y, z = q * f.Z;
                Matrix4x4 m = default;
                m.m00 = x.x; m.m10 = x.y; m.m20 = x.z;
                m.m01 = y.x; m.m11 = y.y; m.m21 = y.z;
                m.m02 = z.x; m.m12 = z.y; m.m22 = z.z;
                m.m03 = pos.x; m.m13 = pos.y; m.m23 = pos.z; m.m33 = 1f;
                sink.AddWorm(f.Anim, f.Lod, f.Frame, m);
            }
        }
    }
}
