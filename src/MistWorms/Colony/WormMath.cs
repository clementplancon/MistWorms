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

        /// <summary>A worm that has not moved for this long plays the idle flipbook instead of the crawl (plan 4A-1 point 11).</summary>
        public const float IdleAfterSeconds = 0.3f;
        public const float VariationScaleRange = 0.1f;     // uniform scale 1 +- 10 %
        public const float VariationRollDeg = 8f;          // roll +- 8 degrees around the body axis

        /// <summary>True when the worm should show the idle pose: still for longer than <see cref="IdleAfterSeconds"/>.</summary>
        public static bool UseIdlePose(float now, float stillSince) => now - stillSince > IdleAfterSeconds;

        /// <summary>Integer hash (lowbias32) of a slot and its generation: stable for one worm, different for a reused slot.</summary>
        public static uint VariationHash(int slot, int generation)
        {
            unchecked
            {
                uint x = (uint)slot * 0x9E3779B1u + (uint)generation * 0x85EBCA6Bu + 0x1234567u;
                x ^= x >> 16; x *= 0x7FEB352Du;
                x ^= x >> 15; x *= 0x846CA68Bu;
                x ^= x >> 16;
                return x;
            }
        }

        /// <summary>Per worm look variation, deterministic in (slot, generation): scale in 1 +- 0.1, roll in +- 8 degrees.</summary>
        public static void Variation(int slot, int generation, out float scale, out float rollDeg)
        {
            uint h = VariationHash(slot, generation);
            float a = (h & 0xFFFFu) / 65535f * 2f - 1f;
            float b = (h >> 16) / 65535f * 2f - 1f;
            scale = 1f + a * VariationScaleRange;
            rollDeg = b * VariationRollDeg;
        }

        // ---- group behaviour (plan 4A-3)

        public const float BodyMidOffset = 0.15f;      // the middle of the body is this far behind the head, along the heading
        public const float GoldenAngleDeg = 137.5f;
        public const float FileMaxDistance = 0.6f;     // a worm closer than this ahead, inside the cone, makes the file
        public const float FileConeDeg = 30f;
        public const float FileHeadingDeg = 45f;
        public const float FileSpeedMargin = 1.05f;

        /// <summary>Middle of the body of a worm whose head is at (x, z), looking along <paramref name="heading"/>.</summary>
        public static void BodyMid(float x, float z, float heading, out float mx, out float mz)
        {
            mx = x - (float)Math.Sin(heading) * BodyMidOffset;
            mz = z - (float)Math.Cos(heading) * BodyMidOffset;
        }

        /// <summary>Bearing (rad, same convention as <see cref="HeadingOf"/>) of the approach place of rank <paramref name="rank"/>: base + rank x 137.5 degrees.</summary>
        public static float ApproachAngle(int rank, float baseAngle)
        {
            // rank x 137.5 = rank x 275 / 2: exact in integers, then reduced to one turn.
            double deg = (((long)rank * 275L) % 720L) / 2.0;
            return WrapPi(baseAngle + (float)(deg * Math.PI / 180.0));
        }

        public const float RingStep = 0.4f;            // radius gained by each waiting ring
        public const float RingSpacing = 0.35f;        // arc length given to each worm of a ring

        /// <summary>Number of places of ring <paramref name="ring"/> of inner radius <paramref name="innerRadius"/>: floor(2 pi r / 0.35).</summary>
        public static int RingCapacity(int ring, float innerRadius)
        {
            float r = innerRadius + ring * RingStep;
            return Math.Max(1, (int)Math.Floor(2.0 * Math.PI * r / RingSpacing));
        }

        /// <summary>Step of the golden-ratio permutation of a ring: nearest integer to 0.382 x capacity that is coprime with it.</summary>
        public static int GoldenStep(int capacity)
        {
            if (capacity <= 2) return 1;
            int step = Math.Max(1, (int)Math.Round(capacity * 0.381966));
            while (Gcd(step, capacity) != 1) step++;
            return step;
        }

        private static int Gcd(int a, int b)
        {
            while (b != 0) { int r = a % b; a = b; b = r; }
            return a;
        }

        /// <summary>
        /// Approach place of global rank <paramref name="rank"/> (plan 4A-3, rings of the coordinator): rings of radius inner + k x 0.4 m fill one
        /// after the other (capacity floor(2 pi r / 0.35)); inside a ring the places are visited in golden-ratio order from base, and each ring is
        /// offset by k x 0.5 x 360 / capacity so the rings do not line up. Returns the ring index.
        /// </summary>
        public static int ApproachPlace(int rank, float baseAngle, float innerRadius, out float angle, out float radius)
        {
            int ring = 0, left = Math.Max(0, rank);
            while (true)
            {
                int cap = RingCapacity(ring, innerRadius);
                if (left < cap) break;
                left -= cap;
                ring++;
            }
            int capacity = RingCapacity(ring, innerRadius);
            float slotAngle = (float)(2.0 * Math.PI) / capacity;
            // Golden-ratio spread on equal places: slot = rank x step mod capacity with step ~ 0.382 x capacity and coprime with it, so a
            // partial ring is spread like the golden angle and a full ring is exactly 2 pi r / capacity (>= 0.35 m) apart.
            angle = WrapPi(baseAngle + ring * 0.5f * slotAngle + (left * (long)GoldenStep(capacity) % capacity) * slotAngle);
            radius = innerRadius + ring * RingStep;
            return ring;
        }

        /// <summary>
        /// True when a worm at offset (dx, dz) from this one is ahead in file: within <see cref="FileMaxDistance"/>, inside the cone of
        /// +-30 degrees around <paramref name="heading"/>, and heading the same way (difference under 45 degrees).
        /// </summary>
        public static bool IsAheadInFile(float heading, float dx, float dz, float otherHeading)
        {
            float dist = (float)Math.Sqrt(dx * dx + dz * dz);
            if (dist < 1e-4f || dist >= FileMaxDistance) return false;
            float cos = ((float)Math.Sin(heading) * dx + (float)Math.Cos(heading) * dz) / dist;
            if (cos < (float)Math.Cos(FileConeDeg * Math.PI / 180.0)) return false;
            return Math.Abs(WrapPi(otherHeading - heading)) < FileHeadingDeg * (float)(Math.PI / 180.0);
        }

        /// <summary>Speed limit of a worm following another: the leader's last speed plus 5 %, but never below <paramref name="minSpeed"/>.</summary>
        public static float FileSpeedCap(float leaderSpeed, float minSpeed) => Math.Max(leaderSpeed * FileSpeedMargin, minSpeed);

        /// <summary>Simulation rate in Hz: sleeping worms 2 Hz, others near or far from the closest player (spec §11.1).</summary>
        public static float SimRate(bool sleeping, float nearestPlayerSqr, float nearRate, float farRate)
        {
            if (sleeping) return 2f;
            return nearestPlayerSqr < 30f * 30f ? nearRate : farRate;
        }
    }
}
