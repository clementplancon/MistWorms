using System;
using MistWorms.Bites;
using MistWorms.Colony;
using MistWorms.Mass;
using UnityEngine;

namespace MistWorms.Drops
{
    /// <summary>
    /// The routed RPCs that carry what a bite ate to the pool that receives it (spec §7.2, §11.3, plan 6, A9, decision 141): <c>MW_Consumed</c> to the
    /// owner of a mass, <c>MW_WormConsumed</c> to the owner of the colony of a free worm. Sent by the owner of the bitten player, which alone knows the
    /// inventory. The receiver validates everything (<see cref="ConsumedList.TryRead"/>): a prefab unknown to the ObjectDB, a quantity out of
    /// (0, 50] or more than 16 entries are ignored. In solo a routed RPC to oneself is handled synchronously.
    /// </summary>
    internal static class PoolRpc
    {
        private static readonly ConsumedList s_received = new ConsumedList();
        private static bool s_warnedIgnored;

#if MW_DEV
        public static int Sent, Received, IgnoredEntries, DroppedNoTarget;
        public static void ResetCounters() => Sent = Received = IgnoredEntries = DroppedNoTarget = 0;
#endif

        public static void Register()
        {
            if (ZRoutedRpc.instance == null) return;
            try
            {
                ZRoutedRpc.instance.Register<ZDOID, ZPackage>(MWNames.RpcConsumed, OnConsumed);
                ZRoutedRpc.instance.Register<ZDOID, int, int, ZPackage>(MWNames.RpcWormConsumed, OnWormConsumed);
            }
            catch (ArgumentException)
            {
                // Already registered on this ZRoutedRpc (Game.Start ran twice on it).
            }
        }

        public static bool IsKnownItem(int hash) => ObjectDB.instance != null && ObjectDB.instance.GetItemPrefab(hash) != null;

        // ------------------------------------------------------------------ sending (owner of the bitten player)

        /// <summary>After a bite: sends <see cref="BiteInventory.Consumed"/> to the mass or the worm of the active <see cref="WormBiteContext"/>, if there is one.</summary>
        public static void SendBiteResult()
        {
            ConsumedList list = BiteInventory.Consumed;
            if (list.Count == 0) return;
            if (WormBiteContext.Mass != ZDOID.None) SendConsumed(WormBiteContext.Mass, list);
            else if (WormBiteContext.WormColony != ZDOID.None) SendWormConsumed(WormBiteContext.WormColony, WormBiteContext.WormSlot, WormBiteContext.WormGeneration, list);
        }

        public static bool SendConsumed(ZDOID mass, ConsumedList list)
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            ZDO zdo = mass != ZDOID.None && ZDOMan.instance != null ? ZDOMan.instance.GetZDO(mass) : null;
            if (rpc == null || zdo == null || zdo.GetOwner() == 0L) return false;   // 0 would mean "everybody"
            int entry = 0;
            float done = 0f;
            while (true)
            {
                var pkg = new ZPackage();
                if (!list.WriteNextPacket(pkg, ref entry, ref done)) break;
                rpc.InvokeRoutedRPC(zdo.GetOwner(), MWNames.RpcConsumed, mass, pkg);
#if MW_DEV
                Sent++;
#endif
            }
            return true;
        }

        public static bool SendWormConsumed(ZDOID colony, int slot, int generation, ConsumedList list)
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            ZDO zdo = colony != ZDOID.None && ZDOMan.instance != null ? ZDOMan.instance.GetZDO(colony) : null;
            if (rpc == null || zdo == null || zdo.GetOwner() == 0L) return false;
            int entry = 0;
            float done = 0f;
            while (true)
            {
                var pkg = new ZPackage();
                if (!list.WriteNextPacket(pkg, ref entry, ref done)) break;
                rpc.InvokeRoutedRPC(zdo.GetOwner(), MWNames.RpcWormConsumed, colony, slot, generation, pkg);
#if MW_DEV
                Sent++;
#endif
            }
            return true;
        }

        // ------------------------------------------------------------------ receiving (owner of the mass / of the colony)

        private static void OnConsumed(long sender, ZDOID massId, ZPackage pkg)
        {
            try
            {
                int ignored = ConsumedList.TryRead(pkg, IsKnownItem, s_received);
                NoteIgnored(ignored, sender);
                var all = WritheMassState.All;
                for (int i = 0; i < all.Count; i++)
                {
                    WritheMassState m = all[i];
                    if (m == null || !m.Valid || !m.IsOwner || m.ZdoId != massId) continue;
#if MW_DEV
                    Received++;
#endif
                    m.AddConsumed(s_received);
                    return;
                }
                Forward(sender, massId, RpcKind.Mass, 0, 0, pkg);
            }
            catch (Exception e)
            {
                Plugin.LogException("PoolRpc.OnConsumed", e);
            }
        }

        private static void OnWormConsumed(long sender, ZDOID colonyId, int slot, int generation, ZPackage pkg)
        {
            try
            {
                int ignored = ConsumedList.TryRead(pkg, IsKnownItem, s_received);
                NoteIgnored(ignored, sender);
                WormColony colony = ColonyRegistry.FindById(colonyId);
                if (colony == null || !colony.IsOwner)
                {
                    Forward(sender, colonyId, RpcKind.Worm, slot, generation, pkg);
                    return;
                }
#if MW_DEV
                Received++;
#endif
                colony.AddConsumedToWorm(slot, generation, s_received);
            }
            catch (Exception e)
            {
                Plugin.LogException("PoolRpc.OnWormConsumed", e);
            }
        }

        private enum RpcKind { Mass, Worm }

        /// <summary>
        /// This peer is not (or no longer) the owner: the packet is sent once more to the current owner of the ZDO when it is known and is neither this peer nor the
        /// sender (no bounce back); otherwise it is ignored with a dev log.
        /// </summary>
        private static void Forward(long sender, ZDOID id, RpcKind kind, int slot, int generation, ZPackage pkg)
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            ZDO zdo = ZDOMan.instance != null && id != ZDOID.None ? ZDOMan.instance.GetZDO(id) : null;
            long owner = zdo != null ? zdo.GetOwner() : 0L;
            if (rpc != null && owner != 0L && owner != ZDOMan.GetSessionID() && owner != sender)
            {
                if (kind == RpcKind.Mass) rpc.InvokeRoutedRPC(owner, MWNames.RpcConsumed, id, pkg);
                else rpc.InvokeRoutedRPC(owner, MWNames.RpcWormConsumed, id, slot, generation, pkg);
                return;
            }
#if MW_DEV
            DroppedNoTarget++;
            Plugin.Log.LogInfo($"[Pool] {kind} consumed packet for {id}: not owned here and no other owner to forward to, lost");
#endif
        }

        private static void NoteIgnored(int ignored, long sender)
        {
            if (ignored <= 0) return;
#if MW_DEV
            IgnoredEntries += ignored;
            Plugin.Log.LogInfo($"[Pool] {ignored} invalid consumed entries from peer {sender} ignored");
#endif
            if (!s_warnedIgnored)
            {
                s_warnedIgnored = true;
                Plugin.Log.LogWarning($"[Pool] invalid pool entries received from peer {sender}: ignored (further ones are not logged)");
            }
        }
    }
}
