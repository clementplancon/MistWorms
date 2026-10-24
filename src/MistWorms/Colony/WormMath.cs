using System;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>Pure helpers of the worm simulation (angles, boxes, perception rules). No game access: unit tested.</summary>
    internal static class WormMath
    {
        private const float TwoPi = (float)(Math.PI * 2.0);

        /// <summary>Wraps an angle into (-pi, pi].</summary>
        public static float WrapPi(float a)
        {
            a %= TwoPi;
            if (a > (float)Math.PI) a -= TwoPi;
            else if (a <= -(float)Math.PI) a += TwoPi;
            return a;
        }

        /// <summary>Yaw whose forward vector (sin h, 0, cos h) points along (dx, dz).</summary>
        public static float HeadingOf(float dx, float dz) => (float)Math.Atan2(dx, dz);

        /// <summary>Turns <paramref name="heading"/> toward <paramref name="desired"/> by at most <paramref name="maxStep"/> radians (shortest way).</summary>
        public static float TurnToward(float heading, float desired, float maxStep)
        {
            float d = WrapPi(desired - heading);
            if (Math.Abs(d) <= maxStep) return WrapPi(desired);
            return WrapPi(heading + (d > 0f ? maxStep : -maxStep));
        }

        /// <summary>True when (px, pz) is inside the axis aligned box (centre, half size), ignoring nothing: the three axes count.</summary>
        public static bool InsideBox(Vector3 p, Vector3 center, Vector3 halfSize)
        {
            return Math.Abs(p.x - center.x) <= halfSize.x
                && Math.Abs(p.y - center.y) <= halfSize.y
                && Math.Abs(p.z - center.z) <= halfSize.z;
        }

        /// <summary>Grows [min, max] to contain the oriented box (centre, rotation, half size).</summary>
        public static void ExpandByObb(Vector3 center, Quaternion rotation, Vector3 halfSize, ref Vector3 min, ref Vector3 max)
        {
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? -halfSize.x : halfSize.x,
                    (i & 2) == 0 ? -halfSize.y : halfSize.y,
                    (i & 4) == 0 ? -halfSize.z : halfSize.z);
                Vector3 w = center + rotation * corner;
                min = Vector3.Min(min, w);
                max = Vector3.Max(max, w);
            }
        }

        /// <summary>Hearing as in BaseAI.CanHearTarget: range capped at 12 m inside, and the target must be closer than its noise range.</summary>
        public static bool CanHear(float distance, float hearRange, bool interior, float noiseRange)
        {
            if (interior) hearRange = Math.Min(12f, hearRange);
            if (distance > hearRange) return false;
            return distance < noiseRange;
        }

        /// <summary>
        /// Field of view test. <paramref name="fullAngleDeg"/> is the full angle (360 = all around); vanilla's m_viewAngle is a half angle,
        /// hence the division by 2 (decisions.md 1.1 line 4).
        /// </summary>
        public static bool InViewAngle(float headingRad, float dx, float dz, float fullAngleDeg)
        {
            if (fullAngleDeg >= 359.5f) return true;
            float diff = Math.Abs(WrapPi(HeadingOf(dx, dz) - headingRad)) * (180f / (float)Math.PI);
            return diff <= fullAngleDeg * 0.5f;
        }

        /// <summary>Simulation rate in Hz: sleeping worms 2 Hz, others near or far from the closest player (spec §11.1).</summary>
        public static float SimRate(bool sleeping, float nearestPlayerSqr, float nearRate, float farRate)
        {
            if (sleeping) return 2f;
            return nearestPlayerSqr < 30f * 30f ? nearRate : farRate;
        }
    }
}
