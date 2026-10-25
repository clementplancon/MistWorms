using System;
using System.Collections.Generic;
using MistWorms.Config;

namespace MistWorms.Bites
{
    /// <summary>Adapter between <see cref="BiteMath"/> and a player's inventory (spec §6.3). Main thread; buffers are reused.</summary>
    internal static class BiteInventory
    {
        private static readonly List<BiteItem> s_items = new List<BiteItem>(32);
        private static readonly List<ItemDrop.ItemData> s_source = new List<ItemDrop.ItemData>(32);
        private static readonly List<BiteLoss> s_losses = new List<BiteLoss>(8);
        private static readonly Random s_rng = new Random();

        /// <summary>
        /// Eats <paramref name="p"/> durability points from the player's items (<paramref name="wormCount"/> = 1 for a cobra strike) and
        /// applies the losses to the inventory. Eligible: m_useDurability and durability left, equipped only when
        /// Engulf.IncludeUnequippedInventory is off; the Wisplight (Engulf.WisplightPrefab) goes last.
        /// </summary>
        public static BiteResult Apply(Player player, float p, int wormCount)
        {
            s_items.Clear();
            s_source.Clear();

            Wisplight.FixInventory(player.GetInventory());
            bool includeAll = MWConfig.Engulf.IncludeUnequippedInventory.Value;
            string wisplight = MWConfig.Engulf.WisplightPrefab.Value;
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                if (item?.m_shared == null || !item.m_shared.m_useDurability || item.m_durability <= 0f) continue;
                if (!includeAll && !item.m_equipped) continue;
                s_items.Add(new BiteItem
                {
                    Id = s_source.Count,
                    Durability = item.m_durability,
                    IsWisplight = item.m_dropPrefab != null && item.m_dropPrefab.name == wisplight,
                });
                s_source.Add(item);
            }

            BiteResult result = BiteMath.Select(s_items, p, wormCount, MWConfig.Engulf.ItemsPerTenWorms.Value,
                MWConfig.Engulf.WisplightLast.Value, s_rng, s_losses);

            for (int i = 0; i < s_losses.Count; i++)
            {
                ItemDrop.ItemData item = s_source[s_losses[i].Id];
                item.m_durability = Math.Max(0f, item.m_durability - s_losses[i].Amount);
            }
            s_items.Clear();
            s_source.Clear();
            return result;
        }
    }
}
