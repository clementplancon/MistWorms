using System;

namespace MistWorms.Colony
{
    /// <summary>Pure helpers of the mine seeding (spec 8.3). No game access: unit tested.</summary>
    internal static class SeedMath
    {
        /// <summary>
        /// Spreads a budget of lone worms over <paramref name="rooms"/> rooms, round-robin, at most <paramref name="maxPerRoom"/> in each.
        /// The round starts at a room picked from <paramref name="seed"/>, so the same mine always gets the same split while the
        /// remainder does not always favour the first rooms. The sum is min(budget, rooms * maxPerRoom).
        /// </summary>
        public static int[] LoneWormsPerRoom(int budget, int rooms, int maxPerRoom, int seed)
        {
            if (rooms <= 0) return new int[0];
            var counts = new int[rooms];
            if (budget <= 0 || maxPerRoom <= 0) return counts;

            int total = (int)Math.Min((long)budget, (long)rooms * maxPerRoom);
            int start = (int)((uint)unchecked(seed * 2654435761u) % (uint)rooms);
            for (int k = 0; k < total; k++) counts[(start + k) % rooms]++;
            return counts;
        }

        /// <summary>
        /// Seed of the seeding of one mine: the seed of the world and the position of the dungeon root (a mine never moves), so the same world always
        /// gets the same nests and lone worms. Positions are rounded to the metre.
        /// </summary>
        public static int MineSeed(int worldSeed, float x, float y, float z)
        {
            unchecked
            {
                return worldSeed * 31 + (int)Math.Round(x) * 4271 + (int)Math.Round(y) * 9187 - (int)Math.Round(z) * 2134;
            }
        }
    }
}
