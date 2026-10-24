using System;

namespace MistWorms.Drops
{
    /// <summary>What one killed worm drops by itself (spec §7.5). Empty strings / false = nothing.</summary>
    internal struct NativeDropRoll
    {
        public string Bait;       // prefab name picked by the bait table, null when no bait drops
        public bool BaitRolled;   // the bait chance succeeded (even if the table was empty)
        public bool Entrails;
        public bool Bloodbag;
    }

    /// <summary>
    /// Native drop logic of a killed worm (spec §7.5): three independent rolls, bait picked by weight. Pure (no game access)
    /// so the probabilities are unit tested over 100 000 draws. The random generator is passed in: one per colony in the game.
    /// </summary>
    internal static class NativeDrops
    {
        /// <summary>
        /// Seed of the S01 statistics run. With 200 repetitions at p = 0.08 the standard deviation is 1.9 points, so a free RNG would
        /// fail a +-3 point check about 12 % of the time per category; the scenario therefore seeds the drop RNG with a seed whose
        /// 200-draw outcome is checked by NativeDropsTests (decisions.md 50): 17 bait, 16 entrails, 8 bloodbag rolls, i.e. 8.5 %, 8 %, 4 %.
        /// </summary>
        public const int ScenarioSeed = 20261050;

        /// <summary>
        /// Rolls the three drops in a fixed order (bait, entrails, bloodbag), exactly one NextDouble per roll plus one more for the
        /// bait pick, so a seeded generator gives the same sequence whatever the table holds.
        /// </summary>
        public static NativeDropRoll Roll(Random rng, float baitChance, float entrailsChance, float bloodbagChance, BaitTable table)
        {
            var r = new NativeDropRoll();
            if (rng.NextDouble() < baitChance)
            {
                r.BaitRolled = true;
                double pick = rng.NextDouble();
                r.Bait = table != null ? table.Pick(pick) : null;
            }
            r.Entrails = rng.NextDouble() < entrailsChance;
            r.Bloodbag = rng.NextDouble() < bloodbagChance;
            return r;
        }
    }
}
