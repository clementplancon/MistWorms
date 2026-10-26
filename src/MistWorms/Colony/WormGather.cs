using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>
    /// Pure helpers of the gathering of worms into a mass (spec §3.3 Gathering, §4.1 formation; phase 4B-4). No game access: unit tested.
    /// </summary>
    internal static class WormGather
    {
        /// <summary>Gatherers at more than this height apart (metres) are not on the same floor and never form a mass together.</summary>
        public const float MaxDy = 2f;

        /// <summary>How many seeds one call tries at most (the cost stays linear in the number of gatherers).</summary>
        public const int MaxSeeds = 12;

        private const int Refinements = 3;

        /// <summary>
        /// A worm on its way to a mass is stalled when it is still outside the absorption reach and moved less than <paramref name="minMoved"/> metres over the window
        /// (3 s): it waits for something that cannot happen. A worm gathering to a mass has no arrival radius of its own: it goes on until it is absorbed.
        /// </summary>
        public static bool IsStalled(float distanceToMassCentre, float massRadius, float movedOverWindow, float minMoved = 0.5f) =>
            distanceToMassCentre > AbsorbReach(massRadius) && movedOverWindow < minMoved;

        /// <summary>Smallest absorption reach (metres): a mass is small (r(40) = 0.43 m) and its body is not the only thing that keeps a worm from the centre (the capsule, the other worms).</summary>
        public const float MinAbsorbReach = 2.2f;

        /// <summary>A worm that made no progress for a whole stuck window is pulled in from this far (metres) when nothing solid lies between it and the mass.</summary>
        public const float StalledAbsorbReach = 10f;

        /// <summary>
        /// Distance from the centre of a mass at which a worm touches it and starts to climb: the visible envelope is wider than r in places (lobes),
        /// so r x 1.3 + 0.5 m (a worm used to be missed at 2.8 m from a mass of 39 that wanders), never under <see cref="MinAbsorbReach"/> (with r(38) = 0.42 m the
        /// first term is 1.05 m and two worms that stood 1.7 m from the centre waited for ever; the old mass of 38 reached 2.2 m).
        /// </summary>
        public static float AbsorbReach(float radius) => Mathf.Max(radius * 1.3f + 0.5f, MinAbsorbReach);

        /// <summary>
        /// The mass a gathering worm goes to: the nearest within <paramref name="radius"/> (xz) and <paramref name="maxDy"/> whose load (count plus worms
        /// already climbing onto it) is under <paramref name="max"/>; -1 when there is none. A mass with room wins over any group of free worms.
        /// </summary>
        public static int NearestMass(Vector3 p, Vector3[] pos, int[] load, int count, int max, float radius, float maxDy)
        {
            float best = radius * radius;
            int bi = -1;
            for (int k = 0; k < count; k++)
            {
                if (load[k] >= max) continue;
                float dx = pos[k].x - p.x, dz = pos[k].z - p.z;
                float d2 = dx * dx + dz * dz;
                if (d2 > best || Mathf.Abs(pos[k].y - p.y) > maxDy) continue;
                best = d2;
                bi = k;
            }
            return bi;
        }

        /// <summary>
        /// Looks for <paramref name="minCount"/> or more worms that lie within <paramref name="radius"/> (xz) of their own centroid.
        /// <paramref name="pts"/> are the first <paramref name="count"/> gatherers; the ones already <paramref name="taken"/> are skipped. A seed gives
        /// a first centroid, then the centroid is refined by re-selecting around it (three rounds). On success the members (at most
        /// <paramref name="maxCount"/>, the first met) are written to <paramref name="members"/> as indices into <paramref name="pts"/>, marked taken,
        /// and their number is returned; otherwise 0 and nothing is marked. <paramref name="seedStart"/> rotates the seeds between calls.
        /// No allocation.
        /// </summary>
        public static int SelectCluster(Vector3[] pts, bool[] taken, int count, float radius, int minCount, int maxCount,
            int[] members, int seedStart, out Vector3 centroid)
        {
            centroid = default;
            if (count < minCount || minCount < 1) return 0;
            float r2 = radius * radius;
            int seeds = count < MaxSeeds ? count : MaxSeeds;

            for (int s = 0; s < seeds; s++)
            {
                int seed = (seedStart + s) % count;
                if (taken[seed]) continue;

                Vector3 c = pts[seed];
                int found = 0;
                for (int round = 0; round < Refinements; round++)
                {
                    float sx = 0f, sy = 0f, sz = 0f;
                    found = 0;
                    for (int k = 0; k < count && found < maxCount; k++)
                    {
                        if (taken[k]) continue;
                        Vector3 p = pts[k];
                        float dx = p.x - c.x, dz = p.z - c.z;
                        if (dx * dx + dz * dz > r2 || Mathf.Abs(p.y - c.y) > MaxDy) continue;
                        members[found++] = k;
                        sx += p.x; sy += p.y; sz += p.z;
                    }
                    if (found < minCount) break;
                    c = new Vector3(sx / found, sy / found, sz / found);
                }
                if (found < minCount) continue;

                // The last selection was made around the previous centroid: keep only those still within the radius of the final one.
                int kept = 0;
                float cx = 0f, cy = 0f, cz = 0f;
                for (int m = 0; m < found; m++)
                {
                    Vector3 p = pts[members[m]];
                    float dx = p.x - c.x, dz = p.z - c.z;
                    if (dx * dx + dz * dz > r2) continue;
                    members[kept++] = members[m];
                    cx += p.x; cy += p.y; cz += p.z;
                }
                if (kept < minCount) continue;

                for (int m = 0; m < kept; m++) taken[members[m]] = true;
                centroid = new Vector3(cx / kept, cy / kept, cz / kept);
                return kept;
            }
            return 0;
        }

        /// <summary>
        /// Goal of a gathering worm from the cached list of active worms: the centroid of those within <paramref name="radius"/> (xz) and of the worm itself and
        /// <paramref name="maxDy"/>, if there are at least <paramref name="minGroup"/> others. <paramref name="self"/> is the index into
        /// <paramref name="indices"/> values to leave out. Returns the number of worms in the group.
        /// </summary>
        public static int GroupCentroid(Vector3[] pos, int[] indices, byte[] states, int count, int self, Vector3 from, float radius,
            float maxDy, bool includeFleeing, byte fleeingState, out Vector3 centroid)
        {
            float r2 = radius * radius;
            float sx = 0f, sy = 0f, sz = 0f;
            int n = 0;
            for (int a = 0; a < count; a++)
            {
                if (indices[a] == self) continue;
                if (!includeFleeing && states[a] == fleeingState) continue;
                Vector3 p = pos[a];
                float dx = p.x - from.x, dz = p.z - from.z;
                if (dx * dx + dz * dz > r2 || Mathf.Abs(p.y - from.y) > maxDy) continue;
                sx += p.x; sy += p.y; sz += p.z;
                n++;
            }
            // The worm counts itself in the centroid: every member of a group then heads for the same point (without itself, each stopped at the
            // centroid of the others, on the far side of the group, and a group of 4 never came within the formation radius).
            int all = n + 1;
            centroid = n > 0 ? new Vector3((sx + from.x) / all, (sy + from.y) / all, (sz + from.z) / all) : from;
            return n;
        }
    }
}
