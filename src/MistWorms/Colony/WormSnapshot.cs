using System;
using System.Collections.Generic;
using MistWorms.Config;
using MistWorms.Drops;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>
    /// Persistence of the free worms of a colony (spec §8.4, §11.3, §11.4; phase 7-3, decision 209). The worms are virtual, so the owner writes them to the ZDO of the colony
    /// as <c>mw_snap</c> (<see cref="SnapshotFormat"/>) every Network.SnapshotInterval when something changed, when the world is saved (<c>ZNet.WorldSaveStarted</c>) and when
    /// the colony object goes away (zone unload). The first owner tick of a colony whose store is empty reads it back before anything else runs: no laying, no seeding
    /// (<see cref="SnapshotSettled"/>), no simulation. Masses and nests persist by their own ZDOs. Not stored: the laying timer of the nests (it restarts), and what happened
    /// since the last write when the owner changes or the game crashes (up to Network.SnapshotInterval).
    /// </summary>
    internal sealed partial class WormColony
    {
        private const float RestoreGroundWait = 20f;   // the rooms of a reloaded mine may not stand yet: wait for a floor under the first worm, this long at most

        private static readonly int HashSnapshot = MWNames.ZdoSnapshot.GetStableHashCode();

        private ZDO _zdo;                          // kept: the zone unload resets the ZNetView before OnDestroy, the ZDO itself stays valid
        private SnapshotWriter _snapWriter;
        private byte[] _snapLast;                  // the array handed to the ZDO last (never written to again: ZDO.Set keeps the reference)
        private MassPool _snapPool;                // scratch of the restore
        private int[] _snapSlots = Array.Empty<int>();
        private bool _snapSettled;
        private bool _snapClosed;
        private float _snapNext;
        private float _snapFirstTick = -1f;
        private readonly List<KeyValuePair<byte[], Vector3>> _absorbQueue = new List<KeyValuePair<byte[], Vector3>>();   // snapshots of duplicate colonies waiting for this one to settle

        /// <summary>Snapshots written to the ZDO and worms restored from one since this colony object was created.</summary>
        public int SnapshotWrites { get; private set; }
        public int SnapshotRestored { get; private set; }

        /// <summary>Hit points of the worms restored and quantity of loot (their pools and the reserve) read from a snapshot, as they were at the moment of the restore (the dev scenarios compare them before the worms gather).</summary>
        public float SnapshotRestoredHp { get; private set; }
        public float SnapshotRestoredPool { get; private set; }

        /// <summary>Worms of the last restore that were left out (outside the box, no slot, nothing to restore) and what the last restore found wrong, or null.</summary>
        public int SnapshotSkipped { get; private set; }
        public string SnapshotProblem { get; private set; }

        /// <summary>The owner has decided what to do with <c>mw_snap</c> (restored, or nothing to restore): laying and seeding may start. False on a peer that is not the owner.</summary>
        public bool SnapshotSettled => _snapSettled;

        // ------------------------------------------------------------------ restore

        /// <summary>First owner ticks: restores the worms from the ZDO when the store is empty. False while waiting for the floor of the mine.</summary>
        private bool SettleSnapshot(float now)
        {
            if (_snapFirstTick < 0f) _snapFirstTick = now;
            byte[] data = _zdo.GetByteArray(HashSnapshot, null);
            if (data == null || data.Length == 0 || Store.AliveCount > 0)
            {
                Settled(now);
                return true;
            }
            if (!MWConfig.General.Enabled.Value) return false;   // a disabled mod must not overwrite what is saved with an empty colony: wait

            if (_snapPool == null) _snapPool = new MassPool();
            if (!SnapshotFormat.TryOpen(data, _snapPool, out int n, out string problem))
            {
                // An unknown version or a broken array empties the colony; the mine is seeded again only when its seeded flag is missing (spec §11.4).
                SnapshotProblem = problem;
                Plugin.Log.LogWarning($"[Snapshot] colony {ZdoId}: mw_snap ({data.Length} bytes) is {problem}, the colony starts empty");
                Settled(now);
                return true;
            }

            if (n > 0 && now - _snapFirstTick < RestoreGroundWait)
            {
                SnapshotFormat.ReadWorm(data, 0, out Vector3 rel, out _, out _, out _, out _, out _);
                if (!WormPhysics.ProbeGround(BoxCenter + rel, out _)) return false;
            }
            RestoreSnapshot(data, n, now, BoxCenter, merge: false);
            Settled(now);
            return true;
        }

        private void Settled(float now)
        {
            _snapSettled = true;
            _snapNext = now + MWConfig.Network.SnapshotInterval.Value;
        }

        /// <summary>
        /// Puts the worms of <paramref name="data"/> into the store. <paramref name="center"/> is the centre of the box its positions are relative to; <paramref name="merge"/>: it is the snapshot of
        /// a duplicate colony of this mine, added to the worms that are already here (the restore of a reloaded colony is not).
        /// </summary>
        private void RestoreSnapshot(byte[] data, int n, float now, Vector3 center, bool merge)
        {
            WormStore s = Store;
            if (_snapSlots.Length < n) _snapSlots = new int[n];
            int restored = 0, skipped = 0, asleep = 0;

            for (int w = 0; w < n; w++)
            {
                _snapSlots[w] = -1;
                SnapshotFormat.ReadWorm(data, w, out Vector3 rel, out float heading, out byte stateCode, out bool lone, out float hp, out int nest);
                Vector3 p = center + rel;
                // Spec §8.4: a worm outside the box is destroyed without a drop (its pool goes to the reserve below); and Absorbed / Dead are not restored.
                if (!SnapshotFormat.RestoredState(stateCode, out WormState state) || !InBox(p)) { skipped++; continue; }

                int slot = AddWorm(p, state, heading, force: true);   // restored worms already existed: the cap of the mine does not apply
                if (slot < 0) { skipped++; continue; }
                s.hp[slot] = Mathf.Max(0.1f, hp);
                SnapshotRestoredHp += s.hp[slot];
                s.nestIndex[slot] = (short)nest;
                s.lone[slot] = lone;
                s.home[slot] = s.pos[slot];
                s.den[slot] = s.pos[slot];
                _snapSlots[w] = slot;
                restored++;
                if (state == WormState.Sleeping) asleep++;
            }

            int at = SnapshotFormat.PoolSectionStart(n);
            int pools = ByteIO.GetUShort(data, at);
            at += 2;
            for (int k = 0; k < pools; k++)
            {
                int w = ByteIO.GetUShort(data, at);
                at += 2;
                _snapPool.ReadFrom(data, ref at);          // validated by TryOpen
                SnapshotRestoredPool += _snapPool.TotalAll();
                MassPool pool = _snapSlots[w] >= 0 ? EnsureWormPool(_snapSlots[w]) : null;
                if (pool != null) pool.CopyFrom(_snapPool);
                else ReserveMerge(_snapPool);              // the worm is not there any more: what it carried is not lost
            }
            _snapPool.ReadFrom(data, ref at);
            SnapshotRestoredPool += _snapPool.TotalAll();
            ReserveMerge(_snapPool);
            _snapPool.Clear();

            if (!merge) _snapLast = data;
            SnapshotRestored += restored;
            SnapshotSkipped += skipped;
            Plugin.Log.LogInfo($"[Snapshot] colony {ZdoId}: {restored}/{n} worms {(merge ? "taken over from a duplicate colony" : "restored")} ({asleep} asleep, {restored - asleep} idle), {skipped} left out, {pools} pools, reserve {(_reserve != null ? _reserve.TotalAll() : 0f):0.###}");
        }

        private void ReserveMerge(MassPool pool)
        {
            if (pool.IsEmpty) return;
            if (_reserve == null) _reserve = new MassPool();
            _reserve.Merge(pool);
        }

        // ------------------------------------------------------------------ write

        /// <summary>Every Network.SnapshotInterval, owner side.</summary>
        private void TickSnapshot(float now)
        {
            if (_absorbQueue.Count > 0) DrainAbsorb(now);
            if (!_snapSettled || now < _snapNext) return;
            _snapNext = now + MWConfig.Network.SnapshotInterval.Value;
            WriteSnapshot();
        }

        /// <summary>
        /// Writes the free worms, their pools and the reserve to <c>mw_snap</c> when they differ from what was written last; the owner of a settled colony only. Safe when the
        /// network object is gone (it works on the ZDO alone). Returns true when the ZDO was changed.
        /// </summary>
        public bool WriteSnapshot()
        {
            ZDO zdo = _zdo;
            if (!_snapSettled || _snapClosed || !_configured || Store == null || zdo == null || ZDOMan.instance == null) return false;   // no ZDOMan: the game is shutting down
            if (zdo.m_uid == ZDOID.None || !zdo.IsValid() || !zdo.IsOwner()) return false;

            SnapshotWriter w = BuildSnapshot();

            if (w.SameAs(_snapLast)) return false;
            _snapLast = w.ToArray();
            zdo.Set(HashSnapshot, _snapLast);
            SnapshotWrites++;
            return true;
        }

        /// <summary>The free worms, their pools and the reserve as they are now, in the reused writer of this colony.</summary>
        private SnapshotWriter BuildSnapshot()
        {
            WormStore s = Store;
            if (_snapWriter == null) _snapWriter = new SnapshotWriter(s.Capacity);
            SnapshotWriter w = _snapWriter;
            Vector3 center = BoxCenter;

            w.Begin();
            for (int i = 0; i < s.HighWater; i++)
            {
                if (!SnapshotKeeps(i)) continue;
                w.AddWorm(s.pos[i] - center, s.heading[i], s.state[i], s.lone[i], s.hp[i], s.nestIndex[i]);
            }
            w.BeginPools();
            int index = 0;
            for (int i = 0; i < s.HighWater && index < w.WormCount; i++)
            {
                if (!SnapshotKeeps(i)) continue;
                if (s.poolIndex[i] >= 0)
                {
                    MassPool pool = s.Pools.Get(s.poolIndex[i]);
                    if (!pool.IsEmpty) w.AddPool(index, pool);
                }
                index++;
            }
            w.Finish(_reserve);
            return w;
        }

        /// <summary>A worm that is in the snapshot: alive and neither absorbed nor dead.</summary>
        internal bool SnapshotKeeps(int i)
        {
            WormStore s = Store;
            if (!s.alive[i]) return false;
            byte st = s.state[i];
            return st != (byte)WormState.Absorbed && st != (byte)WormState.Dead;
        }

        /// <summary><c>ZNet.WorldSaveStarted</c>: every colony this peer owns writes its snapshot before the world is copied for the save.</summary>
        public static void OnWorldSaveStarted()
        {
            try
            {
                var all = ColonyRegistry.All;
                for (int k = 0; k < all.Count; k++)
                    if (all[k] != null) all[k].WriteSnapshot();
            }
            catch (Exception e)
            {
                Plugin.LogException("WormColony.OnWorldSaveStarted", e);
            }
        }

        /// <summary>The colony object is going away: a zone unload keeps the ZDO, so the owner leaves its last state in it.</summary>
        private void WriteSnapshotOnDestroy()
        {
            try
            {
                WriteSnapshot();
            }
            catch (Exception e)
            {
                Plugin.LogException("WormColony.WriteSnapshotOnDestroy", e);
            }
        }

        // ------------------------------------------------------------------ ownership

        /// <summary>
        /// This peer was the owner and is not any more: its worms are the next owner's (it restores them from the last snapshot, up to Network.SnapshotInterval old),
        /// so they leave the store silently and the next time this peer owns the colony it settles again.
        /// </summary>
        private void HandOffSnapshot()
        {
            _snapSettled = false;
            _snapFirstTick = -1f;
            _snapLast = null;
            WormStore s = Store;
            if (s == null) return;
            for (int i = 0; i < s.HighWater; i++)
                if (s.alive[i]) KillWorm(i, false);
            if (_reserve != null) _reserve.Clear();
            ResetWake();
        }

        // ------------------------------------------------------------------ a duplicate colony of the mine (decision 211)

        /// <summary>
        /// This colony (owned here) is a duplicate of <paramref name="keep"/>, the colony of the same mine that stays: everything it holds goes to it before it is destroyed, so that no worm, no pool
        /// and no reserve is lost. Its nests and its masses carry the key of the mine, which is also the key of <paramref name="keep"/>: they follow by themselves.
        /// </summary>
        public void HandOverTo(WormColony keep)
        {
            if (keep == null || keep == this || !_initialized || Store == null || _zdo == null || !_zdo.IsValid()) return;
            // A colony that has read its snapshot gives what is alive now; one that has not gives what its ZDO holds.
            byte[] data = _snapSettled && IsOwner && _configured ? BuildSnapshot().ToArray() : _zdo.GetByteArray(HashSnapshot, null);
            if (data == null || data.Length < SnapshotFormat.HeaderBytes) return;
            Plugin.Log.LogInfo($"[Snapshot] duplicate colony {ZdoId}: its {SnapshotFormat.PeekCount(data)} worms, pools and reserve go to colony {keep.ZdoId}");
            if (keep.IsOwner)
            {
                keep.ReceiveAbsorb(data, BoxCenter);
                return;
            }
            var pkg = new ZPackage();
            pkg.Write(data);
            keep.NView.InvokeRPC(MWNames.RpcSnapAbsorb, pkg, BoxCenter);
        }

        private void RPC_SnapAbsorb(long sender, ZPackage pkg, Vector3 center)
        {
            try
            {
                if (!_initialized || !_nview.IsValid() || !_nview.IsOwner()) return;
                ReceiveAbsorb(pkg.ReadByteArray(), center);
            }
            catch (Exception e)
            {
                Plugin.LogException("WormColony.RPC_SnapAbsorb", e);
            }
        }

        /// <summary>Owner side: the snapshot of a duplicate colony is merged as soon as this colony has settled (its own worms are back before anything is added).</summary>
        public void ReceiveAbsorb(byte[] data, Vector3 center)
        {
            if (data == null) return;
            _absorbQueue.Add(new KeyValuePair<byte[], Vector3>(data, center));
        }

        /// <summary>This peer is not the owner (any more) and holds snapshots of duplicate colonies that were not merged yet: they go to the owner.</summary>
        private void ForwardAbsorbQueue()
        {
            for (int k = 0; k < _absorbQueue.Count; k++)
            {
                var pkg = new ZPackage();
                pkg.Write(_absorbQueue[k].Key);
                _nview.InvokeRPC(MWNames.RpcSnapAbsorb, pkg, _absorbQueue[k].Value);
            }
            _absorbQueue.Clear();
        }

        private void DrainAbsorb(float now)
        {
            if (!_snapSettled) return;
            if (_snapPool == null) _snapPool = new MassPool();
            for (int k = 0; k < _absorbQueue.Count; k++)
            {
                byte[] data = _absorbQueue[k].Key;
                if (!SnapshotFormat.TryOpen(data, _snapPool, out int n, out string problem))
                {
                    Plugin.Log.LogWarning($"[Snapshot] colony {ZdoId}: the snapshot of a duplicate colony is {problem}: ignored");
                    continue;
                }
                RestoreSnapshot(data, n, now, _absorbQueue[k].Value, merge: true);
            }
            _absorbQueue.Clear();
            WriteSnapshot();
        }
    }
}
