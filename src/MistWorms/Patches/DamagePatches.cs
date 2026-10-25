using System;
using HarmonyLib;
using MistWorms.Bites;
using MistWorms.Config;
using MistWorms.Mass;
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
        private const float HealthShareEpsilon = 1e-3f;

        private static bool BiteTarget(Character c) => WormBiteContext.Active && c != null && c == WormBiteContext.Target;

        /// <summary>The routed RPC of the bites must exist on every peer, even one with no colony loaded: register it with the session.</summary>
        [HarmonyPatch(typeof(Game), "Start")]
        private static class GameStartPatch
        {
            private static void Postfix()
            {
                try
                {
                    WormBite.Register();
                    Mass.MassEngulf.Register();
                }
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
        /// Perfect parry (plan 5, A11; spec 4.8, 6.4): the vanilla block of a bite is perfect when the shield has a timed block bonus and the block was raised
        /// less than 0.25 s ago (Humanoid.BlockAttack: m_blockTimer != -1 and m_blockTimer below 0.25). The test is a local of the vanilla method, so it is made
        /// in the prefix, and the postfix keeps it if the block went through. Vanilla does not cancel the hit; the mod does, for the tick of a mass.
        /// Inert outside a mass bite context.
        /// </summary>
        [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
        private static class BlockAttackPatch
        {
            private const float PerfectWindow = 0.25f;   // Humanoid.BlockAttack

            private static void Prefix(Humanoid __instance, out bool __state)
            {
                __state = false;
                try
                {
                    if (!WormBiteContext.Active || __instance != WormBiteContext.Target || WormBiteContext.Mass == ZDOID.None) return;
                    ItemDrop.ItemData blocker = __instance.GetCurrentBlocker();
                    __state = blocker != null && blocker.m_shared.m_timedBlockBonus > 1f && __instance.m_blockTimer != -1f && __instance.m_blockTimer < PerfectWindow;
                }
                catch (Exception e)
                {
                    Plugin.LogException("DamagePatches.BlockAttack.Prefix", e);
                }
            }

            private static void Postfix(Humanoid __instance, bool __result, bool __state)
            {
                try
                {
                    if (__state && __result && WormBiteContext.Active && __instance == WormBiteContext.Target) WormBiteContext.MarkPerfectParry();
                }
                catch (Exception e)
                {
                    Plugin.LogException("DamagePatches.BlockAttack.Postfix", e);
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

                    if (WormBiteContext.PerfectParry)
                    {
                        // The tick is cancelled: no wear, no health, no text; the mass delays its next tick.
                        MassEngulf.SendParry(WormBiteContext.Mass);
                        return false;
                    }

                    if (player.InGodMode() || player.InGhostMode()) return false; // spec §6.4

                    float total = hit.GetTotalDamage();
                    // Ratio that survived blocking. Player resistances enter it too: accepted approximation in phase 3 (phase 5 revisits).
                    float original = WormBiteContext.HpDamageOriginal;
                    float ratio = original > 1e-4f ? Mathf.Clamp01(total / original) : 1f;
                    float p = WormBiteContext.P * (MWConfig.Engulf.BlockReducesBites.Value ? ratio : 1f);
                    if (p <= 1e-4f) return true; // blocked down to nothing: vanilla drops it as well

                    BiteResult result = BiteInventory.Apply(player, p, WormBiteContext.WormCount);
#if MW_DEV
                    if (result.Eaten > 0f)
                    {
                        WormBiteContext.Bites++;
                        WormBiteContext.DurabilityEaten += result.Eaten;
                    }
#endif
                    if (result.Eaten > 0f) WormBite.ShowEquipmentText(hit.m_point, result.Eaten);

                    // What no item could absorb goes to health in the same tick, prorated (plan 5, A7): nothing left to eat = all of it.
                    float healthShare = Mathf.Clamp01(result.Remaining / p);
                    if (healthShare <= HealthShareEpsilon) return false; // health untouched
                    hit.ApplyModifier(healthShare);
#if MW_DEV
                    WormBiteContext.HealthBites++;
                    WormBiteContext.HealthDamage += hit.GetTotalDamage();
#endif
                    return true; // the vanilla path applies the (reduced) health damage, and shows the red number
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
