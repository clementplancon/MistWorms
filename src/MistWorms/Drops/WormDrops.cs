using System;
using System.Collections.Generic;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Drops
{
    /// <summary>
    /// Game side of the native drops of a killed worm (spec §7.5): rolls with <see cref="NativeDrops"/>, resolves the item prefabs and
    /// creates them with the vanilla <c>CharacterDrop.DropItems</c>. The loot pool (spec §7.3) is handed out by <see cref="DropPoolShare"/> and <see cref="DropPoolRest"/>.
    /// </summary>
    internal static class WormDrops
    {
        private const float DropArea = 0.3f;   // plan B4: items scatter within 0.3 m of the worm

        private static readonly List<KeyValuePair<GameObject, int>> s_list = new List<KeyValuePair<GameObject, int>>(3);

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

        public static void ResetCounters()
        {
            Rolls = BaitRolled = Entrails = Bloodbag = ItemsSpawned = PoolItems = 0;
            PoolUnits.Clear();
        }
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

        // ------------------------------------------------------------------ the loot pool (spec §7.3, plan 6, A5)

        private const int PendingCapacity = 32;
        private const int StacksPerFrame = 3;           // plan 6, A5: past the event limit the last worm's items come out 3 stacks per frame

        private static readonly PoolDrops s_poolDrops = new PoolDrops();
        private static readonly GameObject[] s_pendPrefab = new GameObject[PendingCapacity];
        private static readonly int[] s_pendUnits = new int[PendingCapacity];
        private static readonly int[] s_pendStack = new int[PendingCapacity];
        private static readonly Vector3[] s_pendPos = new Vector3[PendingCapacity];
        private static int s_pendHead, s_pendCount;

#if MW_DEV
        /// <summary>Units handed out of pools by resource (prefab hash), since the last <see cref="ResetCounters"/>: the dev check of S05 next to the items on the ground.</summary>
        public static readonly Dictionary<int, int> PoolUnits = new Dictionary<int, int>();
#endif

        /// <summary>Items still waiting to come out (the last worm of a rich pool).</summary>
        public static int PendingStacks => s_pendCount;

        /// <summary>
        /// <paramref name="k"/> of the <paramref name="n"/> worms of a pool died at <paramref name="where"/>: their share moves to the carry and the whole items of the
        /// carry are dropped, at most Drops.MaxItemsPerDropEvent stacks (the surplus stays in the carry for the next worm).
        /// </summary>
        public static void DropPoolShare(MassPool pool, int k, int n, Vector3 where)
        {
            if (pool == null || pool.IsEmpty) return;
            pool.Share(k, n);
            s_poolDrops.Clear();
            pool.Harvest(s_poolDrops);
            EmitPool(pool, where, false);
        }

        /// <summary>The last worm of a pool died: everything left is dropped (floor), in full stacks, over several frames past the event limit; the fraction is lost.</summary>
        public static void DropPoolRest(MassPool pool, Vector3 where)
        {
            if (pool == null || pool.IsEmpty) return;
            s_poolDrops.Clear();
            pool.HarvestAll(s_poolDrops);
            EmitPool(null, where, true);
        }

        private static void EmitPool(MassPool back, Vector3 where, bool final)
        {
            int budget = MWConfig.Drops.MaxItemsPerDropEvent.Value;
            for (int e = 0; e < s_poolDrops.N; e++)
            {
                int hash = s_poolDrops.Hash[e], units = s_poolDrops.Count[e];
                GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(hash) : null;
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop == null)
                {
                    WarnOnce("poolitem:" + hash, $"[Drops] pool item {hash} is not in the ObjectDB: {units} lost");
                    continue;
                }
                int stack = Math.Max(1, drop.m_itemData.m_shared.m_maxStackSize);
                int stacks = (units + stack - 1) / stack;
                if (stacks <= budget)
                {
                    SpawnStacks(prefab, units, stack, where, hash);
                    budget -= stacks;
                    continue;
                }
                int now = budget * stack;
                if (now > 0) SpawnStacks(prefab, now, stack, where, hash);
                budget = 0;
                if (final) Enqueue(prefab, units - now, stack, where);
                else if (back != null) back.Return(hash, units - now);
            }
            s_poolDrops.Clear();
        }

        private static void SpawnStacks(GameObject prefab, int units, int stack, Vector3 where, int hash)
        {
            while (units > 0)
            {
                int size = Math.Min(units, stack);
                SpawnStack(prefab, size, where);
                units -= size;
#if MW_DEV
                PoolItems++;
                PoolUnits.TryGetValue(hash, out int have);
                PoolUnits[hash] = have + size;
#endif
            }
        }

        private static void SpawnStack(GameObject prefab, int size, Vector3 where)
        {
            Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0, 360), 0f);
            GameObject go = UnityEngine.Object.Instantiate(prefab, where + UnityEngine.Random.insideUnitSphere * DropArea, rotation);
            ItemDrop item = go.GetComponent<ItemDrop>();
            if (item != null)
            {
                item.m_itemData.m_worldLevel = (byte)Game.m_worldLevel;
                if (size > 1) item.SetStack(size);
            }
            Rigidbody body = go.GetComponent<Rigidbody>();
            if (body != null)
            {
                Vector3 impulse = UnityEngine.Random.insideUnitSphere;
                if (impulse.y < 0f) impulse.y = -impulse.y;
                body.AddForce(impulse * 5f, ForceMode.VelocityChange);
            }
        }

        private static void Enqueue(GameObject prefab, int units, int stack, Vector3 where)
        {
            if (units <= 0) return;
            if (s_pendCount >= PendingCapacity)
            {
                // Full (many rich pools dying together): nothing is lost, it all comes out now.
                SpawnStacks(prefab, units, stack, where, prefab.name.GetStableHashCode());
                return;
            }
            int at = (s_pendHead + s_pendCount) % PendingCapacity;
            s_pendPrefab[at] = prefab;
            s_pendUnits[at] = units;
            s_pendStack[at] = stack;
            s_pendPos[at] = where;
            s_pendCount++;
        }

        /// <summary>End of the session (the ZNetScene is destroyed): the queued items are abandoned, so nothing appears in another world. Logs how many stacks.</summary>
        public static void ClearPending()
        {
            if (s_pendCount == 0) return;
            int stacks = 0;
            for (int i = 0; i < PendingCapacity; i++)
            {
                if (s_pendPrefab[i] != null) stacks += (s_pendUnits[i] + s_pendStack[i] - 1) / s_pendStack[i];
                s_pendPrefab[i] = null;
                s_pendUnits[i] = 0;
            }
            s_pendHead = s_pendCount = 0;
            Plugin.DevInfo($"[Drops] session ended with {stacks} pool stacks still queued: abandoned");
        }

        /// <summary>Plugin.Update: lets the queued items of the last worms out, <see cref="StacksPerFrame"/> stacks per frame.</summary>
        public static void PumpPending()
        {
            int budget = StacksPerFrame;
            while (budget > 0 && s_pendCount > 0)
            {
                int h = s_pendHead;
                int size = Math.Min(s_pendUnits[h], s_pendStack[h]);
                try
                {
                    SpawnStacks(s_pendPrefab[h], size, s_pendStack[h], s_pendPos[h], s_pendPrefab[h].name.GetStableHashCode());
                }
                catch (Exception e)
                {
                    Plugin.LogException("WormDrops.PumpPending", e);
                    s_pendUnits[h] = size;   // drop this entry
                }
                s_pendUnits[h] -= size;
                budget--;
                if (s_pendUnits[h] > 0) continue;
                s_pendPrefab[h] = null;
                s_pendHead = (s_pendHead + 1) % PendingCapacity;
                s_pendCount--;
            }
        }

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
            Plugin.WarnOnce(key, message);
        }
    }
}
