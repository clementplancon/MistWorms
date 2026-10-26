using System;
using HarmonyLib;
using MistWorms.Colony;

namespace MistWorms.Patches
{
    /// <summary>
    /// Harmony patch of the infested mines (spec §8.3, §10.3). Inert unless the generator is the dungeon of an infested mine (theme DvergerTown, not the
    /// citadel of the Queen, not a SW_ object of SkyWhale): it only remembers the generator, never changes a result, and logs and lets the vanilla code run on an error.
    /// </summary>
    internal static class MinePatches
    {
        /// <summary>
        /// A mine that was generated before (or after) the mod was installed is read back by <c>Load</c> when its zone loads. The rooms are not placed
        /// yet at this point (they load asynchronously, decisions.md 1.2 line 9), so nothing is created here: <see cref="MineSeeder.Pump"/> does it
        /// once the rooms stand and a player is inside.
        /// </summary>
        [HarmonyPatch(typeof(DungeonGenerator), "Load")]
        private static class LoadPatch
        {
            private static void Postfix(DungeonGenerator __instance)
            {
                try
                {
                    MineSeeder.Note(__instance);
                }
                catch (Exception e)
                {
                    Plugin.LogException("MinePatches.Load", e);
                }
            }
        }
    }
}
