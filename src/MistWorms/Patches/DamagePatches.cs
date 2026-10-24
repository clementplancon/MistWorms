using System;
using HarmonyLib;
using MistWorms.Bites;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Patches
{
    /// <summary>
    /// Harmony patches that turn a worm bite into a durability bite (spec §6.3, §6.4, §10.3). Prefixes only, each in a try/catch that
    /// logs and lets the vanilla code run; every one is inert unless a <see cref="WormBiteContext"/> is active, i.e. while the
    /// synchronous <c>Damage</c> call of a bite is on the stack (decisions.md 1.2 line 15, 1.3).
    /// </summary>
    internal static class DamagePatches
    {
        private static bool BiteTarget(Character c) => WormBiteContext.Active && c != null && c == WormBiteContext.Target;

        /// <summary>The routed RPC of the bites must exist on every peer, even one with no colony loaded: register it with the session.</summary>
        [HarmonyPatch(typeof(Game), "Start")]
        private static class GameStartPatch
        {
            private static void Postfix()
            {
                try { WormBite.Register(); }
                catch (Exception e) { Plugin.LogException("DamagePatches.GameStart", e); }
            }
        }

        /// <summary>Armor never reduces a bite (spec §6.4) unless Engulf.ArmorReducesBites. HitData.ApplyArmor is only called from RPC_Damage (Character.cs:2383, 2388).</summary>
        [HarmonyPatch(typeof(HitData), nameof(HitData.ApplyArmor))]
        private static class ApplyArmorPatch
        {
            private static bool Prefix()
            {
                try
                {
                    return !(WormBiteContext.Active && !MWConfig.Engulf.ArmorReducesBites.Value);
                }
                catch (Exception e)
                {
                    Plugin.LogException("DamagePatches.ApplyArmor", e);
                    return true;
                }
            }
        }

        /// <summary>
        /// Vanilla wears one random armor piece by the hit's damage right after ApplyArmor; a bite already wears items through
        /// <see cref="ApplyDamagePatch"/>, so that second wear is skipped (same rule as the armor).
        /// </summary>
        [HarmonyPatch(typeof(Player), "DamageArmorDurability")]
        private static class DamageArmorDurabilityPatch
        {
            private static bool Prefix(Player __instance)
            {
                try
                {
                    return !(BiteTarget(__instance) && !MWConfig.Engulf.ArmorReducesBites.Value);
                }
                catch (Exception e)
                {
                    Plugin.LogException("DamagePatches.DamageArmorDurability", e);
                    return true;
                }
            }
        }

        /// <summary>
        /// The bite on the player: god and ghost modes take nothing; otherwise the damage that survived blocking becomes durability loss
        /// and the health stays untouched while an item can still be eaten; with nothing left to eat vanilla applies the health damage.
        /// </summary>
        [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
        private static class ApplyDamagePatch
        {
            private static bool Prefix(Character __instance, HitData hit)
            {
                try
                {
                    if (!BiteTarget(__instance) || !(__instance is Player player) || hit == null) return true;
                    if (player.IsDead() || player.IsTeleporting()) return true; // vanilla ignores these anyway

                    if (player.InGodMode() || player.InGhostMode()) return false; // spec §6.4

                    float total = hit.GetTotalDamage();
                    // Ratio that survived blocking. Player resistances enter it too: accepted approximation in phase 3 (phase 5 revisits).
                    float original = WormBiteContext.HpDamageOriginal;
                    float ratio = original > 1e-4f ? Mathf.Clamp01(total / original) : 1f;
                    float p = WormBiteContext.P * (MWConfig.Engulf.BlockReducesBites.Value ? ratio : 1f);
                    if (p <= 1e-4f) return true; // blocked down to nothing: vanilla drops it as well

                    BiteResult result = BiteInventory.Apply(player, p, 1);
                    if (result.HadEligible)
                    {
#if MW_DEV
                        WormBiteContext.Bites++;
                        WormBiteContext.DurabilityEaten += result.Eaten;
#endif
                        if (result.Eaten > 0f) WormBite.ShowEquipmentText(hit.m_point, result.Eaten);
                        return false; // health untouched
                    }
#if MW_DEV
                    WormBiteContext.HealthBites++;
                    WormBiteContext.HealthDamage += total;
#endif
                    return true; // nothing left to devour: the vanilla path applies the health damage
                }
                catch (Exception e)
                {
                    Plugin.LogException("DamagePatches.ApplyDamage", e);
                    return true;
                }
            }
        }
    }
}
