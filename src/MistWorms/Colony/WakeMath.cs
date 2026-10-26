using System;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>
    /// Pure rules of the alert chain between the nest groups (spec §5.1, plan 7-2, decision 200): which group wakes when. No game access: unit tested.
    /// </summary>
    internal static class AlertMath
    {
        /// <summary>Wake time of a group the alert never reaches.</summary>
        public const float Never = float.PositiveInfinity;

        /// <summary>
        /// Breadth first spread of an alert that starts at group <paramref name="start"/> at <paramref name="startTime"/>: every group whose nest is within
        /// <paramref name="radius"/> of a group that is already reached wakes <paramref name="hop"/> seconds after it (spec §5.1: 2 hops per second). A group
        /// that is not <paramref name="alive"/> (its nest is gone or not loaded) is never reached and passes nothing on. Writes the wake time of the first
        /// <paramref name="count"/> groups into <paramref name="times"/> (<see cref="Never"/> when unreached); <paramref name="queue"/> is scratch of at
        /// least <paramref name="count"/> entries. The start group wakes at once even when it is not alive, and then reaches nobody.
        /// </summary>
        public static void Schedule(Vector3[] positions, bool[] alive, int count, float radius, float hop, int start, float startTime, float[] times, int[] queue)
        {
            for (int j = 0; j < count; j++) times[j] = Never;
            if ((uint)start >= (uint)count) return;
            times[start] = startTime;
            if (!alive[start]) return;

            float r2 = radius * radius;
            int head = 0, tail = 0;
            queue[tail++] = start;
            while (head < tail)
            {
                int u = queue[head++];
                for (int j = 0; j < count; j++)
                {
                    if (!alive[j] || times[j] != Never) continue;
                    if ((positions[j] - positions[u]).sqrMagnitude > r2) continue;
                    times[j] = times[u] + hop;
                    queue[tail++] = j;
                }
            }
        }
    }

    internal static class AlertMerge
    {
        /// <summary>
        /// Folds the wake times of a new alert (<paramref name="times"/>) into the schedule of the groups (<paramref name="alertAt"/>): a group keeps the earlier of its two times,
        /// a group that is awake or the one the alert started from (<paramref name="except"/>) is left alone. Returns how many groups had no scheduled time before.
        /// </summary>
        public static int Into(float[] alertAt, float[] times, bool[] awake, int count, int except)
        {
            int added = 0;
            for (int j = 0; j < count; j++)
            {
                if (j == except || awake[j]) continue;
                float t = times[j];
                if (t == AlertMath.Never || t >= alertAt[j]) continue;
                if (alertAt[j] == AlertMath.Never) added++;
                alertAt[j] = t;
            }
            return added;
        }
    }

    /// <summary>Pure rules of the waking and of the return to sleep (spec §5.1, §5.2, plan 7-2). No game access: unit tested.</summary>
    internal static class WakeMath
    {
        /// <summary>The wake up of a group is spread over this many seconds so that its worms do not all start in the same frame.</summary>
        public const float MaxStagger = 0.4f;

        private const float GoldenAngle = 2.3999632f;

        /// <summary>Wake up delay of worm (<paramref name="slot"/>, <paramref name="generation"/>): 0 to <see cref="MaxStagger"/>, a fixed hash of the pair (no random call).</summary>
        public static float Stagger(int slot, int generation)
        {
            unchecked
            {
                uint h = (uint)(slot * 73856093) ^ (uint)(generation * 19349663);
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (h & 0xffff) / 65535f * MaxStagger;
            }
        }

        /// <summary>Has a group (or a worm with no nest) been without a player near it for longer than <c>Targets.CalmDelay</c>?</summary>
        public static bool CalmElapsed(float now, float lastActive, float delay) => now - lastActive > delay;

        /// <summary>
        /// Place <paramref name="k"/> of <paramref name="n"/> on the ring of sleeping worms laid down when a mass goes back to sleep: golden angle steps from
        /// <paramref name="startAngle"/> and an area uniform radius between <paramref name="rMin"/> and <paramref name="rMax"/>, so the worms spread over the
        /// ring whatever n is. The offset from the centre is returned in <paramref name="dx"/> and <paramref name="dz"/>.
        /// </summary>
        public static void RingPoint(int k, int n, float startAngle, float rMin, float rMax, out float dx, out float dz)
        {
            if (n < 1) n = 1;
            float a = startAngle + k * GoldenAngle;
            float t = (float)Math.Sqrt((k + 0.5f) / n);
            if (t > 1f) t = 1f;
            float r = rMin + (rMax - rMin) * t;
            dx = (float)Math.Cos(a) * r;
            dz = (float)Math.Sin(a) * r;
        }
    }
}
