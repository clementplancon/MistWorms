#if MW_DEV
using HarmonyLib;

namespace MistWorms.Dev
{
    /// <summary>
    /// Dev only: while <see cref="Active"/> the vanilla spawners (the spawn system of the zones and the fixed creature spawners: nests, camps, spawn points) spawn nothing. The open ground
    /// scenarios switch it on once they stand on their site and the runner switches it off at the end of each one: the creatures removed around the player at the start do not come back
    /// (a tar blob killed 24 of the 30 worms of S18 a, greydwarves ate the pool of S13). Creatures a scenario instantiates itself are not spawners and are not affected.
    /// </summary>
    internal static class DevSpawnBlock
    {
        public static bool Active;

        [HarmonyPatch(typeof(SpawnSystem), "UpdateSpawning")]
        private static class SpawnSystemPatch
        {
            private static bool Prefix() => !Active;
        }

        [HarmonyPatch(typeof(CreatureSpawner), "UpdateSpawner")]
        private static class CreatureSpawnerPatch
        {
            private static bool Prefix() => !Active;
        }
    }
}
#endif
