using System;
using HarmonyLib;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Mass;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Patches
{
    /// <summary>
    /// Harmony patches of the writhing mass (spec §4.3, §4.8, §10.3, plan 4B-1 point 3). Every one is inert unless the Character is a
    /// <see cref="WritheMassState"/> (spec §10.4: a patch only changes the result when a creature of the mod is involved, and a prefix never
    /// returns false for anything else), and every one logs and lets the vanilla code run on an error.
    /// </summary>
    internal static class MassPatches
    {
#if MW_DEV
        /// <summary>Stagger calls cancelled for a mass since the game started (S03 checks it moved when a hit asks for a stagger).</summary>
        public static int StaggerBlocked;

        /// <summary>The push force the last mass hit carried after scaling (S03 compares it with the expectation).</summary>
        public static float LastScaledPushForce;
        public static int LastPushCount;
#endif

        /// <summary>Name at the cursor and in the enemy bar: "Writhing Mass (n)", recomputed when n changes (spec §4.3). EnemyHud reads it every frame.</summary>
        [HarmonyPatch(typeof(Character), nameof(Character.GetHoverName))]
        private static class HoverNamePatch
        {
            private static void Postfix(Character __instance, ref string __result)
            {
                try
                {
                    if (WritheMassState.TryGet(__instance, out WritheMassState mass)) __result = mass.HoverName();
                }
                catch (Exception e)
                {
                    Plugin.LogException("MassPatches.GetHoverName", e);
                }
            }
        }

        /// <summary>A mass is never staggered (spec §4.8). AddStaggerDamage is already inert: the prefab has m_staggerDamageFactor = 0 (Character.cs:2615).</summary>
        [HarmonyPatch(typeof(Character), nameof(Character.Stagger))]
        private static class StaggerPatch
        {
            private static bool Prefix(Character __instance)
            {
                try
                {
                    if (!WritheMassState.TryGet(__instance, out _)) return true;
#if MW_DEV
                    StaggerBlocked++;
#endif
                    return false;
                }
                catch (Exception e)
                {
                    Plugin.LogException("MassPatches.Stagger", e);
                    return true;
                }
            }
        }

        /// <summary>
        /// Knockback of a hit on a mass: m_pushForce x clamp(10 / n, Mass.KnockbackMinFactor, 1), just before the vanilla pushback
        /// (spec §4.8). The private overload that RPC_Damage calls at Character.cs:2355; the public (Vector3, float) one is used by other code
        /// that must not be scaled.
        /// </summary>
        [HarmonyPatch(typeof(Character), "ApplyPushback", typeof(HitData))]
        private static class PushbackPatch
        {
            private static void Prefix(Character __instance, HitData hit)
            {
                try
                {
                    if (hit == null || !WritheMassState.TryGet(__instance, out WritheMassState mass)) return;
                    hit.m_pushForce *= MassMath.KnockbackFactor(mass.N, MWConfig.Mass.KnockbackMinFactor.Value);
#if MW_DEV
                    LastScaledPushForce = hit.m_pushForce;
                    LastPushCount++;
#endif
                }
                catch (Exception e)
                {
                    Plugin.LogException("MassPatches.ApplyPushback", e);
                }
            }
        }

        private static WormEffects.Limiter s_hitFx;

        /// <summary>
        /// Green splash and hurt sound at the impact point of a hit on a mass, on the peer that lands it (the attacker is the local player, or a hit with
        /// no attacker made here), whatever the hit kills: a hit that costs no worm is seen as well (spec 12.4). Same 10 per second limiter as the worms.
        /// </summary>
        [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
        private static class DamageFxPatch
        {
            private static void Postfix(Character __instance, HitData hit)
            {
                try
                {
                    if (hit == null || !WritheMassState.TryGet(__instance, out WritheMassState mass)) return;
                    if (hit.HaveAttacker() && hit.GetAttacker() != Player.m_localPlayer) return;
                    if (hit.GetTotalDamage() <= 0.1f) return;
                    Vector3 where = hit.m_point == Vector3.zero ? mass.transform.position + Vector3.up * 0.4f : hit.m_point;
                    WormEffects.PlayHit(ref s_hitFx, where, Time.time);
                }
                catch (Exception e)
                {
                    Plugin.LogException("MassPatches.Damage", e);
                }
            }
        }

        /// <summary>
        /// After a hit (or a burning tick, which calls ApplyDamage directly): n and partial follow the new health, and a loss under
        /// Mass.DissolveThreshold dissolves the mass (spec §4.2, §4.4). The bite prefix in <see cref="DamagePatches"/> patches the same
        /// method; the two do not interact (that one needs an active bite context, this one needs a mass).
        /// </summary>
        [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
        private static class ApplyDamagePostfix
        {
            private static void Postfix(Character __instance)
            {
                try
                {
                    if (WritheMassState.TryGet(__instance, out WritheMassState mass) && mass.IsOwner) mass.OnDamageApplied();
                }
                catch (Exception e)
                {
                    Plugin.LogException("MassPatches.ApplyDamage", e);
                }
            }
        }
    }
}
