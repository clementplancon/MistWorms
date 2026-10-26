using System;
using System.Diagnostics;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>
    /// The network side of the free worms (spec §11.3, phase 8, decisions 213 and 214). The owner of a colony tells every remote player within <c>Network.SyncRadius</c> of its box what its
    /// worms do, by the routed RPC <c>MW_Delta</c> sent to each of those peers at <c>Network.SyncRate</c>: only the worms the peers could not guess (<see cref="DeltaFilter"/>), the events
    /// to play (blood, death, nest burst), and a keyframe to a peer that enters the set (and to all after a change of owner). With nobody near, nothing is sent. A peer that does not own the
    /// colony applies the packets to its <see cref="WormMirror"/>, which fills the same <see cref="WormStore"/> the renderer, the sounds and the hit proxies read. Counters of bytes sent and
    /// received and the CPU time of the mirror are kept for the dev bridge (<c>/perf</c>).
    /// Allocation: the routed RPC itself allocates (its parameter array and the packet copy, vanilla code), at most a few times per colony per tick; the colony adds none.
    /// </summary>
    internal sealed partial class WormColony : IMirrorGround, IMirrorHost
    {
        private const int MaxRecipients = 32;
        private const float MirrorGroundRange = 40f;      // a mirrored worm farther than this from the local player keeps the height of the packet (no probe)
        private const int MirrorProbesPerPacket = 128;
        private const float MirrorForgetMargin = 30f;     // beyond SyncRadius + this the mirror is dropped altogether

        // ---- owner side
        private DeltaWriter _dw;
        private SentState[] _sent;
        private float[] _nextEval;
        private int[] _kfSlots;
        private ushort _netSeq;
        private float _nextSend;
        private bool _netKeyAll = true;        // the next send is a keyframe to everybody (first send, new owner, overflow)
        private bool _netHadRecipients;
        private readonly long[] _recip = new long[MaxRecipients];
        private readonly Vector3[] _recipPos = new Vector3[MaxRecipients];
        private readonly long[] _recipPrev = new long[MaxRecipients];
        private readonly long[] _entrants = new long[MaxRecipients];
        private readonly long[] _others = new long[MaxRecipients];
        private int _recipCount, _recipPrevCount;
        private ZPackage _sendPkg;
        private readonly DeltaEventType[] _evType = new DeltaEventType[DeltaFormat.MaxEvents];
        private readonly Vector3[] _evPos = new Vector3[DeltaFormat.MaxEvents];
        private readonly long[] _evBy = new long[DeltaFormat.MaxEvents];
        private int _evCount;
        private WormEffects.Limiter _evLimiter;

        // ---- peer side
        private WormMirror _mirror;
        private bool _mirrorFilled;
        private bool _needKeyframe = true;     // the mirror holds nothing it can trust: ask the owner for a keyframe (decision 225)
        private readonly KeyRequestLimiter _keyReqIn = new KeyRequestLimiter();    // owner: requests served
        private readonly KeyRequestLimiter _keyReqOut = new KeyRequestLimiter();   // peer: requests sent
        private bool _mirrorMode;              // this peer does not own the colony: its store is the mirror
        private int _probesLeft;
        private long _mirrorTicks;             // Stopwatch ticks spent on the mirror since the last frame was closed

        // ---- counters (the dev bridge reads them)
        private readonly RateWindow _bytesSent = new RateWindow(10);
        private readonly RateWindow _bytesReceived = new RateWindow(10);
        public long NetBytesSent => _bytesSent.Total;
        public long NetBytesReceived => _bytesReceived.Total;
        public int NetPacketsSent { get; private set; }
        public int NetPacketsReceived { get; private set; }
        public int NetKeyframesSent { get; private set; }
        public int NetEventsSent { get; private set; }
        public int NetEventsPlayed { get; private set; }
        public int NetEventsEchoed { get; private set; }   // events that came back with this peer as the one that played them: skipped (its own blood, played locally once)
        public int NetBadPackets { get; private set; }
        public int NetKeyRequests { get; private set; }        // owner: requests served
        public int NetKeyRequestsSent { get; private set; }    // peer: requests sent
        public bool NeedsKeyframe => _needKeyframe;
        public int NetRecipients => _recipCount;
        public float NetSendAvgMs { get; private set; }
        public float NetSendMaxMs { get; private set; }
        public float MirrorLastMs { get; private set; }
        public float MirrorAvgMs { get; private set; }
        public float MirrorMaxMs { get; private set; }
        public WormMirror Mirror => _mirror;

        /// <summary>Bytes per second sent by this colony (payload of <c>MW_Delta</c> times the number of recipients), average over the last 10 s.</summary>
        public double NetBytesSentPerSecond => _bytesSent.PerSecond(Time.unscaledTimeAsDouble);
        public double NetBytesReceivedPerSecond => _bytesReceived.PerSecond(Time.unscaledTimeAsDouble);

        /// <summary>True on a peer that shows this colony from packets instead of simulating it.</summary>
        public bool IsMirror => _mirrorMode;

        private void InitNet(int capacity)
        {
            _mirror = new WormMirror(Store, this);
            _nview.Register<ZPackage>(MWNames.RpcDelta, RPC_Delta);
            _nview.Register(MWNames.RpcKeyReq, RPC_KeyReq);
        }

        /// <summary><see cref="IMirrorHost"/>: a mirrored slot leaves like any worm (path, idle sound, store), without drop or effect.</summary>
        public void ReleaseSlot(int i)
        {
            ReleasePath(i);
            ResetIdleSound(i);
            Store.Free(i);
        }

        /// <summary>Owner side: a peer asks for a keyframe. It is taken out of the peers already served, so it is an entrant at the next send; one request per peer per second.</summary>
        private void RPC_KeyReq(long sender)
        {
            try
            {
                if (!_initialized || !_nview.IsValid() || !_nview.IsOwner() || !_snapSettled) return;
                if (!_keyReqIn.Allow(sender, Time.time)) return;
                for (int j = 0; j < _recipPrevCount; j++)
                {
                    if (_recipPrev[j] != sender) continue;
                    _recipPrev[j] = _recipPrev[--_recipPrevCount];
                    break;
                }
                NetKeyRequests++;
            }
            catch (Exception e)
            {
                Plugin.LogException("WormColony.RPC_KeyReq", e);
            }
        }

        private void EnsureNetBuffers()
        {
            if (_dw != null) return;
            int capacity = Store.Capacity;
            _dw = new DeltaWriter();
            _sent = new SentState[capacity];
            _nextEval = new float[capacity];
            _kfSlots = new int[capacity];
            _sendPkg = new ZPackage();
        }

        // ------------------------------------------------------------------ owner: events

        /// <summary>
        /// Queues an event for the peers (the next <c>MW_Delta</c>); <paramref name="playedBy"/> is the peer that already played it locally and will ignore it. At most 10 per second
        /// per colony (spec §12.4); dropped when nobody listens.
        /// </summary>
        private void EmitNetEvent(DeltaEventType type, Vector3 position, long playedBy)
        {
            if (_recipCount == 0 || _evCount >= DeltaFormat.MaxEvents) return;
            if (!_evLimiter.Allow(Time.time)) return;
            _evType[_evCount] = type;
            _evPos[_evCount] = position;
            _evBy[_evCount] = playedBy;
            _evCount++;
        }

        private static long SessionUid => ZDOMan.GetSessionID();

        // ------------------------------------------------------------------ owner: send

        /// <summary>Owner side, after the simulation tick: sends what changed to the remote players near the colony.</summary>
        private void TickNet(float now)
        {
            if (!_snapSettled || Store == null || ZNet.instance == null || ZRoutedRpc.instance == null || !_nview.IsValid()) return;
            if (now < _nextSend) return;
            _nextSend = now + 1f / Mathf.Max(1f, MWConfig.Network.SyncRate.Value);

            long t0 = Stopwatch.GetTimestamp();
            WormStore s = Store;
            FindRecipients();
            if (_recipCount == 0)
            {
                // Nobody near: nothing is sent, nothing is kept (the first peer to come gets a keyframe).
                _netHadRecipients = false;
                _recipPrevCount = 0;
                _evCount = 0;
                s.ClearFreed();
                return;
            }
            EnsureNetBuffers();
            if (!_netHadRecipients || s.FreedOverflow) _netKeyAll = true;
            _netHadRecipients = true;

            if (_netKeyAll)
            {
                _netKeyAll = false;
                SendKeyframe(_recip, _recipCount, now, record: true, withEvents: true);   // the deltas of this tick are not sent: the events go in the first packet
            }
            else
            {
                int ne = 0, no = 0;
                for (int k = 0; k < _recipCount; k++)
                {
                    bool known = false;
                    for (int j = 0; j < _recipPrevCount && !known; j++) known = _recipPrev[j] == _recip[k];
                    if (known) _others[no++] = _recip[k];
                    else _entrants[ne++] = _recip[k];
                }
                if (ne > 0) SendKeyframe(_entrants, ne, now, record: no == 0, withEvents: false);
                if (no > 0) SendDeltas(_others, no, now);
                else _evCount = 0;
            }
            s.ClearFreed();
            Array.Copy(_recip, _recipPrev, _recipCount);
            _recipPrevCount = _recipCount;

            float ms = (float)((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency);
            NetSendAvgMs = NetSendAvgMs <= 0f ? ms : NetSendAvgMs + (ms - NetSendAvgMs) * 0.05f;
            if (ms > NetSendMaxMs) NetSendMaxMs = ms;
        }

        /// <summary>The remote players (one per peer) within <c>Network.SyncRadius</c> of the box of the colony.</summary>
        private void FindRecipients()
        {
            _recipCount = 0;
            long self = SessionUid;
            float radius = MWConfig.Network.SyncRadius.Value;
            float radiusSqr = radius * radius;
            var players = Player.GetAllPlayers();
            for (int k = 0; k < players.Count && _recipCount < MaxRecipients; k++)
            {
                Player p = players[k];
                if (p == null || p == Player.m_localPlayer) continue;
                long uid = p.GetOwner();
                if (uid == 0L || uid == self) continue;
                Vector3 at = p.transform.position;
                if (_boxBounds.SqrDistance(at) > radiusSqr) continue;
                bool dup = false;
                for (int j = 0; j < _recipCount && !dup; j++) dup = _recip[j] == uid;
                if (dup) continue;
                _recip[_recipCount] = uid;
                _recipPos[_recipCount] = at;
                _recipCount++;
            }
        }

        private float NearestRecipientSqr(Vector3 p)
        {
            float best = float.MaxValue;
            for (int k = 0; k < _recipCount; k++)
            {
                float d = (_recipPos[k] - p).sqrMagnitude;
                if (d < best) best = d;
            }
            return best;
        }

        private ushort NextSeq() => unchecked(++_netSeq);

        private void SendDeltas(long[] targets, int count, float now)
        {
            WormStore s = Store;
            DeltaWriter w = _dw;
            Vector3 c = BoxCenter;
            w.Begin(0, NextSeq());

            for (int k = 0; k < _evCount; k++)
                if (w.AddEvent(_evType[k], _evPos[k] - c, _evBy[k])) NetEventsSent++;
            _evCount = 0;

            // The worms that are gone (the ids the store freed since the last send; an id nobody knew is ignored by the peers).
            for (int k = 0; k < s.FreedCount; k++)
            {
                if (!w.HasRoomForEntry) { Flush(targets, count, now); w.Begin(0, NextSeq()); }
                w.AddEntry(s.FreedIds[k], Vector3.zero, 0f, 0f, 0, false, true);
            }

            for (int i = 0; i < s.HighWater; i++)
            {
                if (!s.alive[i]) continue;
                var state = (WormState)s.state[i];
                ushort id = s.netId[i];
                if (state == WormState.Absorbed || state == WormState.Dead)
                {
                    if (_sent[i].Valid && _sent[i].NetId == id)
                    {
                        if (!w.HasRoomForEntry) { Flush(targets, count, now); w.Begin(0, NextSeq()); }
                        w.AddEntry(id, Vector3.zero, 0f, 0f, (byte)state, false, true);
                        _sent[i].Valid = false;
                    }
                    continue;
                }
                if (now < _nextEval[i]) continue;

                Vector3 pos = s.pos[i];
                _nextEval[i] = DeltaFilter.NextEvaluation(now, NearestRecipientSqr(pos));
                byte visual = DeltaFormat.VisualCode((byte)state, s.lone[i], false);
                if (!DeltaFilter.NeedsSend(_sent[i], id, pos, s.heading[i], visual, now)) continue;

                float speed = DeltaFilter.SendSpeed(_sent[i], id, pos, s.lastSpeed[i], now);
                if (DeltaFilter.CanDelta(_sent[i], id, pos, visual))
                {
                    // Compact entry: the move from the position the peers last had (the base is the last sent position, not the one shown).
                    if (!w.HasRoomForDelta) { Flush(targets, count, now); w.Begin(0, NextSeq()); }
                    Vector3 d = pos - _sent[i].Pos;
                    sbyte cx = DeltaFormat.DeltaCode(d.x), cy = DeltaFormat.DeltaCode(d.y), cz = DeltaFormat.DeltaCode(d.z);
                    w.AddDelta(id, cx, cy, cz, s.heading[i], speed);
                    DeltaFilter.RecordDelta(ref _sent[i], cx, cy, cz, s.heading[i], speed, now);
                    continue;
                }
                if (!w.HasRoomForEntry) { Flush(targets, count, now); w.Begin(0, NextSeq()); }
                w.AddEntry(id, pos - c, s.heading[i], speed, (byte)state, s.lone[i], false);
                DeltaFilter.Record(ref _sent[i], id, pos, s.heading[i], speed, visual, now);
            }
            if (!w.IsEmpty) Flush(targets, count, now);
        }

        /// <summary>The whole colony, in packets of at most 1 200 bytes, to the peers in <paramref name="targets"/>; with <paramref name="record"/> it becomes what everybody is assumed to know.</summary>
        private void SendKeyframe(long[] targets, int count, float now, bool record, bool withEvents)
        {
            WormStore s = Store;
            DeltaWriter w = _dw;
            Vector3 c = BoxCenter;
            int n = 0;
            for (int i = 0; i < s.HighWater; i++)
            {
                if (!s.alive[i]) continue;
                byte st = s.state[i];
                if (st == (byte)WormState.Absorbed || st == (byte)WormState.Dead) continue;
                _kfSlots[n++] = i;
            }
            int events = withEvents ? _evCount : 0;
            int chunks = DeltaFormat.KeyframeChunks(n, events);
            int at = 0;
            for (int ch = 0; ch < chunks; ch++)
            {
                byte flags = DeltaFormat.KeyframeFlags(ch, chunks);
                w.Begin(flags, NextSeq());
                if (ch == 0)
                {
                    for (int k = 0; k < events; k++)
                        if (w.AddEvent(_evType[k], _evPos[k] - c, _evBy[k])) NetEventsSent++;
                }
                for (; at < n && w.HasRoomForEntry; at++)
                {
                    int i = _kfSlots[at];
                    ushort id = s.netId[i];
                    byte visual = DeltaFormat.VisualCode(s.state[i], s.lone[i], false);
                    w.AddEntry(id, s.pos[i] - c, s.heading[i], s.lastSpeed[i], s.state[i], s.lone[i], false);
                    if (record)
                    {
                        DeltaFilter.Record(ref _sent[i], id, s.pos[i], s.heading[i], s.lastSpeed[i], visual, now);
                        _nextEval[i] = 0f;
                    }
                    else _sent[i].NeedFull = true;   // the entrants hold this position, the others do not: the next send of the worm is a full entry for all
                }
                Flush(targets, count, now);
            }
            if (withEvents) _evCount = 0;
            NetKeyframesSent++;
        }

        private void Flush(long[] targets, int count, float now)
        {
            int len = _dw.Finish();
            _sendPkg.Clear();
            _sendPkg.m_stream.Write(_dw.Buffer, 0, len);
            _sendPkg.m_stream.Position = 0L;
            for (int k = 0; k < count; k++) _nview.InvokeRPC(targets[k], MWNames.RpcDelta, _sendPkg);
            NetPacketsSent += count;
            _bytesSent.Add((long)_sendPkg.Size() * count, Time.unscaledTimeAsDouble);
        }

        // ------------------------------------------------------------------ peer: receive

        private void RPC_Delta(long sender, ZPackage pkg)
        {
            try
            {
                if (!_initialized || !_nview.IsValid() || _nview.IsOwner() || Store == null) return;
                if (sender != OwnerUid) { _needKeyframe = true; return; }   // only the owner of the colony speaks for it; the real one has not told us yet

                long t0 = Stopwatch.GetTimestamp();
                byte[] buf = pkg.m_stream.GetBuffer();
                int len = (int)pkg.m_stream.Length;
                if (!DeltaFormat.TryReadHeader(buf, len, out DeltaFormat.Header h)) { NetBadPackets++; return; }
                NetPacketsReceived++;
                _bytesReceived.Add(len, Time.unscaledTimeAsDouble);

                _probesLeft = MirrorProbesPerPacket;
                int keyframes = _mirror.KeyframesCompleted;
                int unknown = _mirror.UnknownDeltas;
                if (_mirror.Apply(buf, h, BoxCenter, Time.time, this))
                {
                    _mirrorMode = true;   // what is in the store from now on is the copy of the owner's: if this peer becomes the owner it is dropped first
                    _mirrorFilled = Store.AliveCount > 0;
                    if (_mirror.KeyframesCompleted != keyframes) _needKeyframe = false;
                    if (_mirror.UnknownDeltas != unknown) _needKeyframe = true;   // a compact entry of a worm this peer does not have: ask for a keyframe (decision 225)
                    PlayRemoteEvents(buf, h);
                }
                _mirrorTicks += Stopwatch.GetTimestamp() - t0;
            }
            catch (Exception e)
            {
                Plugin.LogException("WormColony.RPC_Delta", e);
            }
        }

        private void PlayRemoteEvents(byte[] buf, DeltaFormat.Header h)
        {
            if (h.Events == 0 || Application.isBatchMode) return;
            long self = SessionUid;
            float now = Time.time;
            Vector3 c = BoxCenter;
            for (int k = 0; k < h.Events; k++)
            {
                DeltaFormat.ReadEvent(buf, h, k, out DeltaEventType type, out Vector3 rel, out long by);
                if (by == self) { NetEventsEchoed++; continue; }   // this peer played it itself (it landed the hit)
                Vector3 at = c + rel;
                switch (type)
                {
                    case DeltaEventType.Hit: WormEffects.PlayHit(ref _hitFx, at, now); break;
                    case DeltaEventType.Death: WormEffects.PlayDeath(ref _deathFx, at, now); break;
                    case DeltaEventType.NestDestroyed: WormEffects.PlayNestBurst(at); break;
                    default: continue;
                }
                NetEventsPlayed++;
            }
        }

        /// <summary><see cref="IMirrorGround"/>: a floor probe for a mirrored worm near the local player, within the budget of the packet in progress.</summary>
        public bool TryGround(Vector3 position, out Vector3 point, out Vector3 normal)
        {
            point = position;
            normal = Vector3.up;
            Player lp = Player.m_localPlayer;
            if (_probesLeft <= 0 || lp == null) return false;
            if ((lp.transform.position - position).sqrMagnitude > MirrorGroundRange * MirrorGroundRange) return false;
            _probesLeft--;
            if (!WormPhysics.ProbeGround(position, out RaycastHit hit)) return false;
            point = hit.point;
            normal = hit.normal;
            return true;
        }

        /// <summary>Once a frame on a peer that does not own the colony: dead reckoning of the mirrored worms, then their sounds.</summary>
        private void TickMirror(float now)
        {
            WormStore s = Store;
            long t0 = Stopwatch.GetTimestamp();
            Player lp = Player.m_localPlayer;
            float keep = MWConfig.Network.SyncRadius.Value + MirrorForgetMargin;
            bool near = lp != null && _boxBounds.SqrDistance(lp.transform.position) <= keep * keep;
            if (!near)
            {
                // Too far to be told anything: what was shown is stale, drop it (the keyframe of the next approach brings it back).
                if (_mirrorFilled) { _mirror.Clear(); _mirrorFilled = false; }
                _needKeyframe = true;
                s.ClearFreed();
                _mirrorTicks = 0;
                return;
            }
            RequestKeyframe(now, lp);
            _mirror.Tick(now, Time.deltaTime, MWConfig.Worm.MoveSpeed.Value);
            UpdateIdleSounds(now);
            s.ClearFreed();

            float ms = (float)((_mirrorTicks + Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency);
            _mirrorTicks = 0;
            MirrorLastMs = ms;
            MirrorAvgMs = MirrorAvgMs <= 0f ? ms : MirrorAvgMs + (ms - MirrorAvgMs) * 0.02f;
            if (ms > MirrorMaxMs) MirrorMaxMs = ms;
        }

        /// <summary>This peer owns the colony now: what the mirror held goes (the worms come back from <c>mw_snap</c>, restored by <see cref="SettleSnapshot"/>), silently.</summary>
        private void DropMirrorForOwnership()
        {
            // Whatever the store holds is the mirror's (its slots are not the owner's): restoring from mw_snap needs it empty, so it goes whether or not a flag says it was filled.
            _mirrorFilled = false;
            _needKeyframe = true;
            if (Store.AliveCount > 0) _mirror.Clear();
            Store.ClearFreed();
        }

        /// <summary>
        /// A peer that has no trustworthy copy asks the owner for a keyframe (<c>MW_KeyReq</c>, no argument), at most once a second, while the owner is known, is another peer and the
        /// local player is within <c>Network.SyncRadius</c> of the box. The flag drops when a keyframe has been applied to its last packet.
        /// </summary>
        private void RequestKeyframe(float now, Player lp)
        {
            if (!_needKeyframe) return;
            long owner = OwnerUid;
            if (owner == 0L || owner == SessionUid) return;
            float radius = MWConfig.Network.SyncRadius.Value;
            if (_boxBounds.SqrDistance(lp.transform.position) > radius * radius) return;
            if (!_keyReqOut.Allow(owner, now)) return;
            _nview.InvokeRPC(MWNames.RpcKeyReq);
            NetKeyRequestsSent++;
        }

        /// <summary>This peer lost the colony: what it had remembered of what it sent is void, the next owner sends a keyframe.</summary>
        private void ResetNetSend()
        {
            if (_sent != null) Array.Clear(_sent, 0, _sent.Length);
            _netKeyAll = true;
            _netHadRecipients = false;
            _recipPrevCount = 0;
            _evCount = 0;
        }

        /// <summary>The colony has just read its snapshot (it is the owner and settled): everybody near gets a keyframe at the next send, the worms have new ids.</summary>
        private void NetSettled()
        {
            ResetNetSend();
        }

        /// <summary>Forgets the counters (dev).</summary>
        public void ResetNetCounters()
        {
            NetPacketsSent = NetPacketsReceived = NetKeyframesSent = NetEventsSent = NetEventsPlayed = NetEventsEchoed = NetBadPackets = 0;
            NetSendMaxMs = MirrorMaxMs = 0f;
        }
    }
}
