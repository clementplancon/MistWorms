using System;
using MistWorms.Config;

namespace MistWorms.Bites
{
    /// <summary>
    /// Gives the Wisplight (Engulf.WisplightPrefab, the Demister) a virtual durability so that bites can eat it last (spec §6.3 point 4,
    /// plan 5, A8). Done once the ObjectDB is ready, on the shared data of the prefab (the items of the inventories share it: ItemData.Clone is shallow
    /// on m_shared), and only if the vanilla item has no durability of its own. Saved games are untouched: an item saved before the mod keeps the
    /// durability it had (vanilla stores 100 for an item without durability); one found at 0 gets the maximum, once, marked in its custom data.
    /// </summary>
    internal static class Wisplight
    {
        /// <summary>Custom data key marking an item that was looked at (plan 5, A8: the fix applies the first time only).</summary>
        public const string SeenKey = "mw_wisp";

        private static bool s_logged;
        private static ItemDrop.ItemData.SharedData s_virtual;

        /// <summary>Applies the virtual durability to the prefab. Idempotent; called on every ObjectDB registration. Returns whether the item is edible.</summary>
        public static bool Apply()
        {
            try
            {
                ObjectDB db = ObjectDB.instance;
                string prefab = MWConfig.Engulf.WisplightPrefab.Value;
                if (db == null || string.IsNullOrEmpty(prefab)) return false;
                UnityEngine.GameObject go = db.GetItemPrefab(prefab);
                ItemDrop drop = go != null ? go.GetComponent<ItemDrop>() : null;
                if (drop == null)
                {
                    if (!s_logged) Plugin.Log.LogWarning($"[Wisplight] no item prefab named '{prefab}' in the ObjectDB: bites cannot eat it");
                    s_logged = true;
                    return false;
                }
                ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;
                bool changed = false;
                if (!shared.m_useDurability)
                {
                    shared.m_useDurability = true;
                    shared.m_durabilityPerLevel = 0f;
                    s_virtual = shared;
                    changed = true;
                }
                // The maximum follows the config at every registration (a synced config may arrive after the first one), but only on the
                // item this mod made durable: a vanilla item that has its own durability keeps it.
                float max = MWConfig.Engulf.WisplightVirtualDurability.Value;
                if (ReferenceEquals(shared, s_virtual) && shared.m_maxDurability != max)
                {
                    shared.m_maxDurability = max;
                    changed = true;
                }
                if (changed || !s_logged)
                {
                    s_logged = true;
                    Plugin.Log.LogInfo($"[Wisplight] '{prefab}' durability: use={shared.m_useDurability}, max={shared.m_maxDurability}, per level={shared.m_durabilityPerLevel}, virtual={ReferenceEquals(shared, s_virtual)}");
                }
                return true;
            }
            catch (Exception e)
            {
                Plugin.LogException("Wisplight.Apply", e);
                return false;
            }
        }

        /// <summary>True if <paramref name="item"/> is the Wisplight.</summary>
        public static bool Is(ItemDrop.ItemData item) =>
            item?.m_dropPrefab != null && item.m_dropPrefab.name == MWConfig.Engulf.WisplightPrefab.Value;

        /// <summary>
        /// The first time a Wisplight of this inventory is seen: marks it, and gives it the maximum durability if it came with none. Called when the
        /// player's inventory is first used by a bite (cheap; not per frame).
        /// </summary>
        public static void FixInventory(Inventory inventory)
        {
            if (inventory == null) return;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (!Is(item) || item.m_customData.ContainsKey(SeenKey)) continue;
                item.m_customData[SeenKey] = "1";
                if (item.m_shared.m_useDurability && item.m_durability <= 0f) item.m_durability = item.GetMaxDurability();
            }
        }
    }
}
