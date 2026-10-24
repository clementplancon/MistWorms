using System;
using System.Collections.Generic;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Drops
{
    /// <summary>
    /// Fills Drops.BaitWeights on first launch (spec §7.5, decisions.md 12): enumerates the ObjectDB items whose prefab starts with
    /// "FishingBait", counts the trophy ingredients of their recipe and writes the "prefab:weight" list to the config. Runs on every
    /// peer when the ObjectDB is ready; the server's value reaches the clients through the config sync.
    /// </summary>
    internal static class BaitWeightsInit
    {
        /// <summary>Hooked to Jötunn's ItemManager.OnItemsRegistered (fires each time a new ObjectDB is set up).</summary>
        public static void OnItemsRegistered()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(MWConfig.Drops.BaitWeights.Value)) return;
                string text = Generate();
                if (string.IsNullOrEmpty(text)) return;
                MWConfig.Drops.BaitWeights.Value = text;
                Plugin.Log.LogInfo($"[Drops] Drops.BaitWeights generated: {text}");
            }
            catch (Exception e)
            {
                Plugin.LogException("BaitWeightsInit.OnItemsRegistered", e);
            }
        }

        /// <summary>The weight list for the baits currently in the ObjectDB, or null when it is not ready or holds no bait.</summary>
        public static string Generate()
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null || db.m_items == null || db.m_items.Count == 0) return null;

            var baits = new List<BaitCandidate>();
            foreach (GameObject go in db.m_items)
            {
                if (go == null || !go.name.StartsWith(VanillaNames.FishingBaitPrefix, StringComparison.Ordinal)) continue;
                ItemDrop drop = go.GetComponent<ItemDrop>();
                if (drop == null) continue;
                baits.Add(new BaitCandidate(go.name, CountTrophies(db.GetRecipe(drop.m_itemData))));
            }
            if (baits.Count == 0) return null;
            return BaitWeights.Compute(baits, VanillaNames.FishingBaitBase, VanillaNames.FishingBaitMistlands);
        }

        private static int CountTrophies(Recipe recipe)
        {
            if (recipe == null || recipe.m_resources == null) return 0;
            int n = 0;
            foreach (Piece.Requirement req in recipe.m_resources)
            {
                ItemDrop item = req?.m_resItem;
                if (item != null && item.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy) n++;
            }
            return n;
        }
    }
}
