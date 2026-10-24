using System;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Render
{
    /// <summary>Visual size of a writhing mass (spec §4.5): r(n) = R10 * (n / 10)^e, dome height 0.8 * r.</summary>
    public static class MassMath
    {
        public const float DomeHeightFactor = 0.8f;

        /// <summary>Dome radius in metres for a mass of n worms, from the live config.</summary>
        public static float Radius(int n) =>
            Radius(n, MWConfig.Mass.BaseRadius.Value, MWConfig.Mass.RadiusExponent.Value);

        /// <summary>Pure overload (no config access) so tests and tools can evaluate the formula.</summary>
        public static float Radius(int n, float baseRadius, float exponent)
        {
            // A mass always holds at least one worm; guards the pow against n <= 0.
            if (n < 1) n = 1;
            return baseRadius * (float)Math.Pow(n / 10.0, exponent);
        }

        /// <summary>Scale applied to the unit dome mesh (radius 1, height 1): (r, 0.8 r, r).</summary>
        public static Vector3 DomeScale(int n) => DomeScale(Radius(n));

        public static Vector3 DomeScale(float radius) =>
            new Vector3(radius, DomeHeightFactor * radius, radius);

        /// <summary>Hit sphere radius of a mass in dome form, as a fraction of r(n) (spec §4.5, decisions.md 78).</summary>
        public const float HitRadiusFactor = 0.9f;

        /// <summary>Height of the hit sphere centre above the mass root, as a fraction of r(n).</summary>
        public const float HitCenterHeightFactor = 0.4f;

        /// <summary>Time constant of the displayed radius (spec §4.5): about 95 % of a change is shown after this long.</summary>
        public const float RadiusSmoothSeconds = 0.3f;

        public static float HitRadius(float radius) => HitRadiusFactor * radius;

        public static float HitCenterHeight(float radius) => HitCenterHeightFactor * radius;

        /// <summary>
        /// One frame of the radius smoothing: exponential approach of <paramref name="shown"/> to <paramref name="target"/>, 95 % done
        /// after <see cref="RadiusSmoothSeconds"/>. Never overshoots; a non positive dt changes nothing.
        /// </summary>
        public static float SmoothRadius(float shown, float target, float dt)
        {
            if (dt <= 0f) return shown;
            float k = 1f - (float)Math.Exp(-3.0 * dt / RadiusSmoothSeconds);
            return shown + (target - shown) * k;
        }

        /// <summary>Knockback multiplier of a mass of <paramref name="n"/> worms: clamp(10 / n, min, 1) (spec §4.8).</summary>
        public static float KnockbackFactor(int n, float min)
        {
            float f = 10f / (n < 1 ? 1 : n);
            if (f < min) f = min;
            return f > 1f ? 1f : f;
        }
    }

    /// <summary>Flipbook frame selection (spec §12.1), pure so it can be tested.</summary>
    public static class Flipbook
    {
        public static int Wrap(int index, int count)
        {
            if (count <= 1) return 0;
            index %= count;
            return index < 0 ? index + count : index;
        }

        /// <summary>
        /// Frame at <paramref name="time"/> seconds: (int)(time * fps + phase) wrapped for loops, clamped for
        /// one-shots (then <paramref name="time"/> is the time elapsed since the animation started).
        /// </summary>
        public static int FrameAt(int frames, float fps, bool loop, float time, float phase)
        {
            if (frames <= 1) return 0;
            int f = (int)Math.Floor(time * fps + phase);
            if (loop) return Wrap(f, frames);
            return f < 0 ? 0 : f >= frames ? frames - 1 : f;
        }
    }
}
