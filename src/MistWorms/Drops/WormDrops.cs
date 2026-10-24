using System;
using System.Collections.Generic;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Drops
{
    /// <summary>
    /// Game side of the native drops of a killed worm (spec §7.5): rolls with <see cref="NativeDrops"/>, resolves the item prefabs and
    /// creates them with the vanilla <c>CharacterDrop.DropItems</c>. The loot pool share of phase 6 is not here yet.
    /// </summary>
    internal static class WormDrops
    {
        private const float DropArea = 0.3f;   // plan B4: items scatter within 0.3 m of the worm

        private static readonly List<KeyValuePair<GameObject, int>> s_list = new List<KeyValuePair<GameObject, int>>(3);
        private static readonly HashSet<string> s_warned = new HashSet<string>();

        // Bait table cache, rebuilt when the config text changes (the entry is read on every kill).
        private static string s_tableText;
        private static BaitTable s_table;

        /// <summary>Seed for the drop generator of a new colony; null = time based.</summary>
        public static int? FixedSeed { get; private set; }

        /// <summary>Generator of a colony (spec §7.5, plan B4): seeded when a test asked for it.</summary>
        public static System.Random NewRng(int salt) => new System.Random(FixedSeed ?? (Environment.TickCount ^ salt));

#if MW_DEV
        /// <summary>Makes the drop generators deterministic (S01 statistics): every loaded colony restarts from the seed, so do new ones.</summary>
        public static void SeedForTests(int seed)
        {
            FixedSeed = seed;
            foreach (Colony.WormColony colony in Colony.ColonyRegistry.All)
                if (colony != null) colony.ReseedDrops(seed);
        }

        public static void ClearTestSeed() => FixedSeed = null;

        // Counters of the rolls and of the items really created (read by S01).
        public static int Rolls, BaitRolled, Entrails, Bloodbag, ItemsSpawned, PoolItems;

        public static void ResetCounters() => Rolls = BaitRolled = Entrails = Bloodbag = ItemsSpawned = PoolItems = 0;
#endif

        /// <summary>Rolls and creates the native drops of a worm that died at <paramref name="where"/>.</summary>
        public static void DropFor(System.Random rng, Vector3 where)
        {
            BaitTable table = CurrentTable();
            NativeDropRoll roll = NativeDrops.Roll(rng, MWConfig.Drops.BaitChance.Value, MWConfig.Drops.EntrailsChance.Value,
                MWConfig.Drops.BloodbagChance.Value, table);
#if MW_DEV
            Rolls++;
            if (roll.BaitRolled) BaitRolled++;
            if (roll.Entrails) Entrails++;
            if (roll.Bloodbag) Bloodbag++;
#endif
            s_list.Clear();
            if (roll.Bait != null) AddItem(roll.Bait);
            if (roll.Entrails) AddItem(VanillaNames.Entrails);
            if (roll.Bloodbag) AddItem(VanillaNames.Bloodbag);
            if (s_list.Count == 0) return;

#if MW_DEV
            ItemsSpawned += s_list.Count;
#endif
            CharacterDrop.DropItems(s_list, where + Vector3.up * 0.3f, DropArea, false);
            s_list.Clear();
        }

        // Phase 6: the share of the loot pool (spec §7.3) is added here, per dying worm.
        // private static void DropPoolShare(int i) { }

        private static void AddItem(string prefabName)
        {
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefabName) : null;
            if (prefab == null)
            {
                WarnOnce("item:" + prefabName, $"[Drops] item prefab '{prefabName}' not found in the ObjectDB: no drop");
                return;
            }
            s_list.Add(new KeyValuePair<GameObject, int>(prefab, 1));
        }

        /// <summary>The bait table of Drops.BaitWeights; when the entry is still empty (first launch before ObjectDB), one generated in memory.</summary>
        private static BaitTable CurrentTable()
        {
            string text = MWConfig.Drops.BaitWeights.Value ?? string.Empty;
            if (s_table != null && string.Equals(text, s_tableText, StringComparison.Ordinal)) return s_table;

            var problems = new List<string>();
            var unknown = new List<string>();
            string source = text;
            if (string.IsNullOrWhiteSpace(source)) source = BaitWeightsInit.Generate() ?? string.Empty;

            List<KeyValuePair<string, float>> entries = BaitWeights.Parse(source, problems);
            BaitTable table = BaitWeights.Build(entries,
                name => ObjectDB.instance != null && ObjectDB.instance.GetItemPrefab(name) != null, unknown);

            foreach (string p in problems) WarnOnce("baitp:" + p, $"[Drops] Drops.BaitWeights: {p}, ignored");
            foreach (string u in unknown) WarnOnce("baitu:" + u, $"[Drops] Drops.BaitWeights: unknown prefab '{u}', ignored");

            // An empty list stays uncached while the ObjectDB is not there yet, so it is read again later.
            if (table.Count > 0 || ObjectDB.instance != null)
            {
                s_tableText = text;
                s_table = table;
            }
            return table;
        }

        private static void WarnOnce(string key, string message)
        {
            if (s_warned.Add(key)) Plugin.Log.LogWarning(message);
        }
    }
}
