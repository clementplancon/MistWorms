using System;
using MistWorms.Config;

namespace MistWorms.Drops
{
    /// <summary>
    /// The vanilla cost of an item, cumulative (plan 6, A1): the crafting cost at quality 1 plus the upgrade steps 2 to q, with the amounts of
    /// <c>Piece.Requirement.GetAmount(level)</c>, which is the cost of ONE step (Piece.cs:102-111, consumed by Player.ConsumeResources with the target level).
    /// Only the requirements of an ordinary station are counted (<c>m_upgraderResource</c> false): the upgrader resources are consumed at upgrader
    /// stations only (Player.cs:2751, InventoryGui.cs:1655), a path the pool does not model.
    /// </summary>
    internal static class ItemCost
    {
        /// <summary>Mirror of <c>Piece.Requirement.GetAmount(level)</c> (kept pure for the tests, which compare it with the real method).</summary>
        public static int StepAmount(int amount, int amountPerLevel, bool upgraderResource, int level)
        {
            if (level <= 1) return amount;
            float num = level >= 4 ? 4f + (level - 4) / 2f : level - 1;
            return (int)Math.Floor(num * amountPerLevel + (upgraderResource ? amount : 0));
        }

        /// <summary>Crafting cost plus, when <paramref name="includeUpgrades"/>, the steps 2 to <paramref name="quality"/>.</summary>
        public static int Cumulative(int amount, int amountPerLevel, bool upgraderResource, int quality, bool includeUpgrades)
        {
            int total = StepAmount(amount, amountPerLevel, upgraderResource, 1);
            if (!includeUpgrades) return total;
            for (int l = 2; l <= quality; l++) total += StepAmount(amount, amountPerLevel, upgraderResource, l);
            return total;
        }

        /// <summary>
        /// pool[r] += d / Dmax x cost(r, quality) x ratio for every resource of the recipe of <paramref name="item"/>. Nothing for an item without recipe
        /// or whose recipe has no resource (merchant, quest, drop items), for a single-ingredient recipe, and for a non positive d or Dmax.
        /// </summary>
        public static void AddTo(ConsumedList into, ItemDrop.ItemData item, int quality, float d, float dmax, bool includeUpgrades, float ratio)
        {
            if (item?.m_shared == null || !(d > 0f) || !(dmax > 0f) || !(ratio > 0f) || ObjectDB.instance == null) return;
            Recipe recipe = ObjectDB.instance.GetRecipe(item);
            if (recipe == null || recipe.m_requireOnlyOneIngredient || recipe.m_resources == null) return;
            float weight = d / dmax * ratio;
            foreach (Piece.Requirement req in recipe.m_resources)
            {
                if (req == null || req.m_resItem == null || req.m_upgraderResource) continue;
                int cost = Cumulative(req.m_amount, req.m_amountPerLevel, false, quality, includeUpgrades);
                if (cost <= 0) continue;
                into.Add(Utils.GetPrefabName(req.m_resItem.gameObject).GetStableHashCode(), cost * weight);
            }
        }

        public static void AddTo(ConsumedList into, ItemDrop.ItemData item, float d, float dmax) =>
            AddTo(into, item, item.m_quality, d, dmax, MWConfig.Drops.IncludeUpgradeMaterials.Value, MWConfig.Drops.EquipmentDropRatio.Value);
    }
}
