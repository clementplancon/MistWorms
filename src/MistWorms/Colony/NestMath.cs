using System;
using System.Collections.Generic;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>Pure rules of the nests (spec 8.1 to 8.3): choice of the rooms that get one, caps and laying timer. No game access: unit tested.</summary>
    internal static class NestMath
    {
        /// <summary>Nests closer than this to each other are never both chosen (S16 checks it on the nests it finds).</summary>
        public const float MinSpacing = 6f;

        /// <summary>Nests start at least this far from the entrance of the mine (plan 7, A1).</summary>
        public const float MinEntranceDistance = 20f;

        /// <summary>Two candidates whose distance to the chosen nests differs by less than this are equal: the larger room wins.</summary>
        public const float TieDistance = 0.5f;

        /// <summary>
        /// Picks at most <paramref name="max"/> candidates by farthest point sampling (decision 192). Only candidates at least
        /// <paramref name="minEntranceDistance"/> from <paramref name="entrance"/> take part. The first is the one with the biggest area; each next one
        /// is the farthest from the nests already chosen (the larger area wins a tie within <see cref="TieDistance"/>, then a hash of
        /// <paramref name="seed"/> and the index), as long as it is at least <paramref name="minSpacing"/> from them. Returns the indices in the order
        /// they were chosen (that order is the nest index); fewer than <paramref name="max"/> when the candidates run out.
        /// </summary>
        public static int[] ChooseNests(Vector3[] points, float[] areas, Vector3 entrance, int max, float minEntranceDistance, float minSpacing, int seed)
        {
            if (points == null || areas == null || points.Length != areas.Length || max <= 0) return new int[0];
            var eligible = new List<int>(points.Length);
            for (int i = 0; i < points.Length; i++)
                if (Vector3.Distance(points[i], entrance) >= minEntranceDistance) eligible.Add(i);
            if (eligible.Count == 0) return new int[0];

            var chosen = new List<int>(Math.Min(max, eligible.Count));
            int first = eligible[0];
            for (int k = 1; k < eligible.Count; k++)
            {
                int c = eligible[k];
                if (areas[c] > areas[first] || (areas[c] == areas[first] && Hash(seed, c) < Hash(seed, first))) first = c;
            }
            chosen.Add(first);

            var distance = new float[points.Length];   // distance to the nearest chosen nest, updated as nests are added
            for (int k = 0; k < eligible.Count; k++) distance[eligible[k]] = Vector3.Distance(points[eligible[k]], points[first]);

            while (chosen.Count < max)
            {
                float best = -1f;
                for (int k = 0; k < eligible.Count; k++)
                {
                    int c = eligible[k];
                    if (!chosen.Contains(c) && distance[c] > best) best = distance[c];
                }
                if (best < minSpacing) break;

                int pick = -1;
                for (int k = 0; k < eligible.Count; k++)
                {
                    int c = eligible[k];
                    if (chosen.Contains(c) || distance[c] < best - TieDistance || distance[c] < minSpacing) continue;
                    if (pick < 0 || areas[c] > areas[pick] || (areas[c] == areas[pick] && Hash(seed, c) < Hash(seed, pick))) pick = c;
                }
                if (pick < 0) break;
                chosen.Add(pick);
                for (int k = 0; k < eligible.Count; k++)
                {
                    int c = eligible[k];
                    float d = Vector3.Distance(points[c], points[pick]);
                    if (d < distance[c]) distance[c] = d;
                }
            }
            return chosen.ToArray();
        }

        private static uint Hash(int seed, int index) => unchecked((uint)(seed * 2654435761u) ^ (uint)(index * 40503 + 12345)) * 2246822519u;

        /// <summary>
        /// May a nest lay? Its own living worms (free or inside a mass, lone worms excluded) are below the cap of the nest and the whole mine
        /// (free and inside masses) is below the cap of the mine (spec 8.2).
        /// </summary>
        public static bool CanLay(int fromThisNest, int maxPerNest, int mineAlive, int maxPerMine) => fromThisNest < maxPerNest && mineAlive < maxPerMine;

        /// <summary>
        /// One step of the laying timer. While <paramref name="paused"/> (the nest was hit, nobody in the mine) the timer stands still. Once it reaches
        /// <paramref name="interval"/> it waits at that value while <paramref name="canLay"/> is false, so the egg comes as soon as there is room.
        /// Returns true when an egg is due now (the timer is then back to 0: no catching up).
        /// </summary>
        public static bool LayStep(ref float timer, float dt, float interval, bool paused, bool canLay)
        {
            if (paused) return false;
            timer += dt;
            if (timer < interval) return false;
            if (!canLay)
            {
                timer = interval;
                return false;
            }
            timer = 0f;
            return true;
        }
    }
}
