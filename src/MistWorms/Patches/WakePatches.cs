using System;
using HarmonyLib;
using MistWorms.Colony;

namespace MistWorms.Patches
{
    /// <summary>
    /// Harmony patch of the waking of a nest group (spec §5.1, plan 7-2). A prefix, in a try/catch that logs and lets the vanilla code run; inert unless the
    /// destructible is a <see cref="WormNest"/>. The hits on the worms and on the masses need no patch: they pass through code of the mod
    /// (<c>WormColony.ApplyWormHit</c>, <see cref="MassPatches"/>).
    /// </summary>
    internal static class WakePatches
    {
        /// <summary>A hit on a nest, on the peer that owns it: when a player dealt it, the colony will alert the group of the nest.</summary>
        [HarmonyPatch(typeof(Destructible), "RPC_Damage")]
        private static class NestDamagePatch
        {
            private static void Prefix(Destructible __instance, HitData hit)
            {
                try
                {
                    WormNest nest = __instance.GetComponent<WormNest>();
                    if (nest != null) nest.NotePlayerHit(hit);
                }
                catch (Exception e)
                {
                    Plugin.LogException("WakePatches.NestDamage", e);
                }
            }
        }
    }
}
