using System;

namespace MistWorms.Mass
{
    /// <summary>
    /// Geometry and decisions of the "stream" form of a mass in a narrow passage (spec §4.7, plan 4B-2). Pure, so the thresholds are tested out of game.
    /// The timings and margins are named constants, not config keys (spec §4.7).
    /// </summary>
    public static class MassStream
    {
        public const float ProbePeriod = 0.25f;       // seconds between two width probes
        public const float ProbeDistance = 2f;        // how far ahead the width is measured to enter the stream
        public const float FreeDistance = 3f;         // free path needed to get back to the dome
        public const float EnterMargin = 0.5f;        // stream when the free width < 2 r + this: the dome of radius r is edged by worms 30 cm long (15 cm beyond it each side) and 20 cm of margin
        public const float WidthMargin = 0.1f;        // ribbon width = free width - this
        public const float MinRibbonWidth = 0.5f;
        public const float TransitionSeconds = 0.5f;  // dome <-> ribbon (render)
        public const int MaxChainSpheres = 6;         // preallocated hit spheres along the ribbon
        public const float SphereWidthFactor = 0.9f;  // chain sphere radius = this x half the ribbon width (as the dome sphere is 0.9 r)

        /// <summary>The passage is too narrow for the dome: free width under 2 r + 0.5 m.</summary>
        public static bool TooNarrow(float freeWidth, float radius) => freeWidth < 2f * radius + EnterMargin;

        /// <summary>
        /// Next form. In the dome, the narrowest width measured up to <see cref="ProbeDistance"/> ahead decides. In the stream the mass
        /// returns to the dome only when the passage is wide enough over the next <see cref="FreeDistance"/> metres as well.
        /// </summary>
        public static bool NextStream(bool streaming, float narrowestNear, float narrowestFar, float radius)
        {
            if (!streaming) return TooNarrow(narrowestNear, radius);
            return TooNarrow(Math.Min(narrowestNear, narrowestFar), radius);
        }

        /// <summary>Length of the ribbon: min(n x StreamLengthPerWorm, StreamMaxLength), and never shorter than its width.</summary>
        public static float RibbonLength(int n, float perWorm, float maxLength, float width) =>
            Math.Max(Math.Min(n * perWorm, maxLength), width);

        public static float RibbonWidth(float freeWidth) => Math.Max(MinRibbonWidth, freeWidth - WidthMargin);

        public static float SphereRadius(float ribbonWidth) => SphereWidthFactor * 0.5f * ribbonWidth;

        /// <summary>Number of hit spheres of the chain: enough to cover the ribbon with overlapping spheres, at least 1, at most <see cref="MaxChainSpheres"/>.</summary>
        public static int ChainCount(float length, float sphereRadius)
        {
            if (sphereRadius <= 1e-3f) return 1;
            int k = (int)Math.Ceiling(length / (1.6f * sphereRadius));
            return Math.Max(1, Math.Min(MaxChainSpheres, k));
        }

        /// <summary>Position along the ribbon axis (centred on 0) of sphere <paramref name="index"/> of <paramref name="count"/>.</summary>
        public static float ChainOffset(int index, int count, float length) =>
            count <= 1 ? 0f : (-0.5f + (index + 0.5f) / count) * length;
    }
}
