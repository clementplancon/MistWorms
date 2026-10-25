using System;
using System.Collections.Generic;
using HarmonyLib;
using MistWorms.Drops;
using UnityEngine;

namespace MistWorms.Patches
{
    /// <summary>
    /// Harmony patches of the captured loot (spec §7.4, §10.3, plan 6, A3, A13): what a creature, a piece or a dvergr destructible killed by a mass or a free
    /// worm would have dropped goes into the pool of the killer. Each one is inert unless <see cref="DropCapture.Attribute"/> names a killer of ours (a hit whose
    /// attacker is a mass, or a hit with no attacker on something a worm just struck), never returns false for anything else, and logs and lets the vanilla
    /// code run on an error. No transpiler.
    /// </summary>
    internal static class DropPatches
    {
        /// <summary>
        /// The list a creature drops on death (Character.OnDeath, Ragdoll.Setup and CharacterDrop.OnDeath all call it): captured and emptied in place when a mass or
        /// a worm made the lethal hit, so the ragdoll stores an empty list and nothing falls (A13). A second roll of the same death is emptied too.
        /// </summary>
        [HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
        private static class GenerateDropListPatch
        {
            private static void Postfix(CharacterDrop __instance, ref List<KeyValuePair<GameObject, int>> __result)
            {
                try
                {
                    DropCapture.CaptureCreature(__instance, __result);
                }
                catch (Exception e)
                {
                    Plugin.LogException("DropPatches.GenerateDropList", e);
                }
            }
        }

        /// <summary>The saddle of a tamed animal killed by one of ours goes to the pool (Tameable.OnDeath only spawns the saddle); inert for every other death.</summary>
        [HarmonyPatch(typeof(Tameable), "OnDeath")]
        private static class TameableOnDeathPatch
        {
            private static bool Prefix(Tameable __instance)
            {
                try
                {
                    return !DropCapture.CaptureSaddle(__instance);
                }
                catch (Exception e)
                {
                    Plugin.LogException("DropPatches.TameableOnDeath", e);
                    return true;
                }
            }
        }

        /// <summary>Opens the capture context around the destruction of a piece (WearNTear.Destroy(HitData, bool), private): Piece.DropResources and the DropOnDestroyed callback run inside it.</summary>
        [HarmonyPatch(typeof(WearNTear), "Destroy", typeof(HitData), typeof(bool))]
        private static class WearNTearDestroyPatch
        {
            private static void Prefix(WearNTear __instance, HitData hitData, bool blockDrop)
            {
                try
                {
                    if (!blockDrop) DropCapture.BeginStructure(__instance.gameObject, hitData);
                }
                catch (Exception e)
                {
                    Plugin.LogException("DropPatches.WearNTearDestroy.Prefix", e);
                }
            }

            private static void Postfix()
            {
                DropCapture.EndStructure();
            }
        }

        /// <summary>Same for a Destructible (public Destroy(HitData)): the dvergr props and walls of the mines are Destructibles with a DropOnDestroyed.</summary>
        [HarmonyPatch(typeof(Destructible), nameof(Destructible.Destroy))]
        private static class DestructibleDestroyPatch
        {
            private static void Prefix(Destructible __instance, HitData hit)
            {
                try
                {
                    DropCapture.BeginStructure(__instance.gameObject, hit);
                }
                catch (Exception e)
                {
                    Plugin.LogException("DropPatches.DestructibleDestroy.Prefix", e);
                }
            }

            private static void Postfix()
            {
                DropCapture.EndStructure();
            }
        }

        /// <summary>The resources of a piece destroyed by one of ours go to the pool (÷3 rule kept); a collapse or a deconstruction (no hit) is vanilla.</summary>
        [HarmonyPatch(typeof(Piece), nameof(Piece.DropResources))]
        private static class DropResourcesPatch
        {
            private static bool Prefix(Piece __instance, HitData hitData)
            {
                try
                {
                    return !DropCapture.CapturePiece(__instance, hitData);
                }
                catch (Exception e)
                {
                    Plugin.LogException("DropPatches.DropResources", e);
                    return true;
                }
            }
        }

        /// <summary>The drop table of a dvergr destructible destroyed by one of ours goes to the pool; inert on every other object.</summary>
        [HarmonyPatch(typeof(DropOnDestroyed), "OnDestroyed")]
        private static class DropOnDestroyedPatch
        {
            private static bool Prefix(DropOnDestroyed __instance)
            {
                try
                {
                    return !DropCapture.CaptureDropOnDestroyed(__instance);
                }
                catch (Exception e)
                {
                    Plugin.LogException("DropPatches.DropOnDestroyed", e);
                    return true;
                }
            }
        }
    }
}
