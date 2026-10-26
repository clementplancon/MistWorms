using System;
using HarmonyLib;
using MistWorms.Colony;

namespace MistWorms.Patches
{
    /// <summary>
    /// Harmony patch of the change of owner of a colony (spec §11.4, phase 8, decision 216). A prefix, in a try/catch that logs and lets the vanilla code run, inert unless the ZDO is the
    /// colony of a peer that owns it and the new owner is somebody else: the colony writes its snapshot first, so that the planned hand-off (the vanilla release in
    /// <c>ZDOMan.ReleaseNearbyZDOS</c>, which the server and a host run) loses nothing. Only <c>SetOwner</c> is patched: <c>SetOwnerInternal</c> is also how a peer learns, after the fact, that the
    /// server gave its ZDO away (<c>ZDOMan.HandleSyncData</c>), too late to write anything the next owner would receive.
    /// </summary>
    internal static class OwnerPatches
    {
        [HarmonyPatch(typeof(ZDO), nameof(ZDO.SetOwner))]
        private static class SetOwnerPatch
        {
            private static void Prefix(ZDO __instance, long uid)
            {
                try
                {
                    if (uid == ZDOMan.GetSessionID()) return;   // taking or keeping the ownership: nothing to hand over
                    WormColony.OnOwnerLeaving(__instance);
                }
                catch (Exception e)
                {
                    Plugin.LogException("OwnerPatches.SetOwner", e);
                }
            }
        }

#if MW_DEV
        /// <summary>
        /// Dev (the <c>serverown</c> mode of the phase 8-2, decision 221): while a dedicated dev server holds a colony for a client, the vanilla release of that ZDO (<c>ZDOMan.ReleaseNearbyZDOS</c>:
        /// <c>SetOwner(0)</c> when it is outside the active area of the server, then <c>SetOwner(peer)</c>, which would hand it back to the client) is skipped. Inert unless that mode holds this very
        /// ZDO; always inert in a Release build.
        /// </summary>
        [HarmonyPatch(typeof(ZDO), nameof(ZDO.SetOwner))]
        private static class DevServerOwnPatch
        {
            private static bool Prefix(ZDO __instance, long uid)
            {
                try
                {
                    return !Dev.ServerOwnDev.BlocksRelease(__instance, uid);
                }
                catch (Exception e)
                {
                    Plugin.LogException("OwnerPatches.DevServerOwn", e);
                    return true;
                }
            }
        }

        /// <summary>
        /// Dev (decision 221): the dedicated server build of <c>Game.FixedUpdate</c> resets the reference position of the server to (1e6, 0, 1e6) at every physics step, so the zone of the colony
        /// is never loaded and its object never instantiated. While the mode is active the position is replaced by that of the colony. Inert otherwise; absent from a Release build.
        /// </summary>
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.SetReferencePosition))]
        private static class DevServerReferencePatch
        {
            private static void Prefix(ref UnityEngine.Vector3 pos)
            {
                try
                {
                    Dev.ServerOwnDev.OverrideReference(ref pos);
                }
                catch (Exception e)
                {
                    Plugin.LogException("OwnerPatches.DevServerReference", e);
                }
            }
        }
#endif
    }
}
