using System;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>Finds the floor under a mirrored worm when it is close enough to be worth a raycast (the colony supplies it; tests supply none).</summary>
    internal interface IMirrorGround
    {
        bool TryGround(Vector3 position, out Vector3 point, out Vector3 normal);
    }

    /// <summary>The colony frees a mirrored slot the way it frees any worm, without drop or effect: its path and idle sound too (not just the store slot).</summary>
    internal interface IMirrorHost
    {
        void ReleaseSlot(int slot);
    }

    /// <summary>
    /// The copy of the free worms of a colony on a peer that does not own it (spec §11.3 and §11.4, phase 8, decision 214). It is not a second store: it fills and moves the slots of the
    /// colony's own <see cref="WormStore"/>, the one the renderer, the sounds and the hit proxies already read, so there is one source and no second loop. It applies the packets of
    /// <see cref="DeltaFormat"/> (an entry is the absolute state of a worm; a worm is created by its first entry, removed by an entry marked removed, a keyframe replaces everything), dead
    /// reckons the worms between packets (heading and speed constant) and corrects the error of each new packet over <see cref="CorrectionSeconds"/>. The worm that was awake and is
    /// silent for <see cref="StaleSeconds"/> is dropped; a sleeping one stays (it is only ever sent when it changes). Buffers are allocated once; nothing allocates per packet or per frame.
    /// Pure apart from the optional floor probe: unit tested.
    /// </summary>
    internal sealed class WormMirror
    {
        public const float CorrectionSeconds = 0.2f;
        public const float StaleSeconds = 3f;
        public const float MaxExtrapolation = 3f;
        public const float SnapDistance = 3f;           // a worm farther than this from where the packet puts it jumps instead of sliding
        public const float KeyframeTimeout = 2f;        // a keyframe whose last packet never came is closed this long after its first
        public const float StateClock = 10f;            // simInterval of a mirrored worm: the clock of its strike pose runs on real time (CollectInstances)
        private const float MovingSpeed = 0.05f;

        private readonly WormStore _s;
        private readonly IMirrorHost _host;
        private readonly Vector3[] _anchor;
    private readonly Vector3[] _base;      // the position of the last entry or delta of the worm as sent (not snapped to the floor): what the next delta is relative to
        private readonly Vector3[] _vel;
        private readonly Vector3[] _offset;
        private readonly float[] _rx;
        private readonly float[] _speed;
        private ushort _lastSeq;
        private bool _haveSeq;
        private bool _keyframeOpen;
        private float _keyframeSince;

        /// <summary>Counters since creation (the dev bridge reads them).</summary>
        public int Packets { get; private set; }
        public int Ignored { get; private set; }
        public int Resets { get; private set; }
        public int EntriesApplied { get; private set; }
        public int Created { get; private set; }
        public int RemovedByPacket { get; private set; }
        public int DroppedStale { get; private set; }
        public int KeyframesCompleted { get; private set; }   // keyframes whose last packet was applied (the peer stops asking for one)
        public int UnknownDeltas { get; private set; }   // compact entries of a worm this peer does not have: dropped, the colony asks for a keyframe
        public int Refused { get; private set; }     // entries that found no slot (store full) or a taken id

        public WormMirror(WormStore store, IMirrorHost host = null)
        {
            _host = host;
            _s = store ?? throw new ArgumentNullException(nameof(store));
            int n = store.Capacity;
            _anchor = new Vector3[n];
            _base = new Vector3[n];
            _vel = new Vector3[n];
            _offset = new Vector3[n];
            _rx = new float[n];
            _speed = new float[n];
        }

        /// <summary>Last time a packet was applied, 0 = never (the colony uses it to know the mirror is silent).</summary>
        public float LastPacketTime { get; private set; }

        public bool KeyframeOpen => _keyframeOpen;

        /// <summary>Where worm <paramref name="i"/> is shown, <paramref name="now"/>: the last packet's position carried on at its heading and speed, plus the part of the error left to absorb.</summary>
        public Vector3 DisplayedAt(int i, float now)
        {
            float t = now - _rx[i];
            if (t < 0f) t = 0f;
            else if (t > MaxExtrapolation) t = MaxExtrapolation;
            Vector3 p = _anchor[i] + _vel[i] * t;
            if (t < CorrectionSeconds) p += _offset[i] * (1f - t / CorrectionSeconds);
            return p;
        }

        public Vector3 Anchor(int i) => _anchor[i];
        public float SpeedOf(int i) => _speed[i];

        /// <summary>
        /// Applies the entries of a packet whose header is <paramref name="h"/>. False when the packet is ignored (older than the last one applied). <paramref name="center"/> is the centre of the
        /// box the positions are relative to.
        /// </summary>
        public bool Apply(byte[] b, DeltaFormat.Header h, Vector3 center, float now, IMirrorGround ground)
        {
            if (!h.Reset && _haveSeq && !DeltaFormat.IsNewer(h.Seq, _lastSeq)) { Ignored++; return false; }
            _lastSeq = h.Seq;
            _haveSeq = true;
            Packets++;
            LastPacketTime = now;

            if (h.Reset)
            {
                Resets++;
                // The first packet of a keyframe: the new owner renumbered its worms, so what is here is void and would fill the store. Everything goes now, the entries of
                // this packet and of the next ones of the keyframe only add.
                for (int i = 0; i < _s.HighWater; i++)
                    if (_s.alive[i]) Release(i);
                _keyframeOpen = true;
                _keyframeSince = now;
            }

            for (int k = 0; k < h.Entries; k++)
            {
                DeltaFormat.ReadEntry(b, k, out ushort id, out Vector3 rel, out float heading, out float speed, out WormState state, out bool lone, out bool removed);
                int slot = _s.FindByNetId(id);
                if (removed)
                {
                    if (slot >= 0) { Release(slot); RemovedByPacket++; }
                    continue;
                }
                if (state == WormState.Absorbed || state == WormState.Dead) continue;   // never sent as an entry
                if (!IsActiveState(state)) { Refused++; continue; }                       // not a state of the enum: a bad packet

                Vector3 pos = center + rel;
                bool known = slot >= 0;
                if (!known)
                {
                    slot = _s.Alloc(pos, heading, state, 1f, now, id);
                    if (slot < 0) { Refused++; continue; }
                    Created++;
                }
                ApplyWorm(slot, known, pos, heading, speed, state, lone, now, ground);
            }

            for (int k = 0; k < h.Deltas; k++)
            {
                DeltaFormat.ReadDelta(b, h, k, out ushort id, out Vector3 move, out float heading, out float speed);
                int slot = _s.FindByNetId(id);
                if (slot < 0) { UnknownDeltas++; continue; }
                ApplyWorm(slot, true, _base[slot] + move, heading, speed, (WormState)_s.state[slot], _s.lone[slot], now, ground);
            }

            if (h.End)
            {
                if (_keyframeOpen) KeyframesCompleted++;
                _keyframeOpen = false;
            }
            return true;
        }

        /// <summary>Puts the state of an entry or a delta on slot <paramref name="slot"/>: <paramref name="pos"/> is the position as sent.</summary>
        private void ApplyWorm(int slot, bool known, Vector3 pos, float heading, float speed, WormState state, bool lone, float now, IMirrorGround ground)
        {
            _base[slot] = pos;
            Vector3 shown = pos;
            if (known) shown = DisplayedAt(slot, now);

            Vector3 normal = Vector3.up;
            if (ground != null && ground.TryGround(pos, out Vector3 gp, out Vector3 gn) && Mathf.Abs(gp.y - pos.y) < 1f)
            {
                pos = gp;
                normal = gn;
            }

            Vector3 dir = new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading));
            Vector3 vel = dir * speed;
            // Along a slope the floor rises or falls under the worm: carry the vertical part so it follows it between two packets.
            if (normal.y > 0.3f) vel.y = Mathf.Clamp(-(normal.x * vel.x + normal.z * vel.z) / normal.y, -2f * speed, 2f * speed);

            bool sameState = known && _s.state[slot] == (byte)state;
            if (sameState) _s.stateTime[slot] += Mathf.Max(0f, now - _s.lastSimTime[slot]);
            else { _s.stateTime[slot] = 0f; _s.subPhase[slot] = 0; }

            _s.state[slot] = (byte)state;
            _s.lone[slot] = lone;
            _s.heading[slot] = heading;
            _s.groundNormal[slot] = normal;
            _s.lastSimTime[slot] = now;
            _s.simInterval[slot] = StateClock;

            _anchor[slot] = pos;
            _vel[slot] = vel;
            _speed[slot] = speed;
            _rx[slot] = now;
            Vector3 error = shown - pos;
            _offset[slot] = known && error.sqrMagnitude < SnapDistance * SnapDistance ? error : Vector3.zero;
            if (!known)
            {
                _s.pos[slot] = pos;
                _s.prevPos[slot] = pos;
                _s.stillSince[slot] = now;
            }
            EntriesApplied++;
        }

        /// <summary>The keyframe is over (its last packet came, or it never finished: <see cref="KeyframeTimeout"/>).</summary>
        private void CloseKeyframe()
        {
            _keyframeOpen = false;
        }

        private static bool IsActiveState(WormState s) => (byte)s <= (byte)WormState.Absorbing;

        private void Release(int i)
        {
            if (_host != null) _host.ReleaseSlot(i);
            else _s.Free(i);
        }

        /// <summary>
        /// Once a frame: moves every worm to where it is shown now (so the renderer, the sounds and the proxies, which read the store, see it), advances the crawl phase of the worms that
        /// move, and drops the awake ones that went silent. <paramref name="moveSpeed"/> is <c>Worm.MoveSpeed</c> (the crawl animation advances by distance, as on the owner).
        /// </summary>
        public void Tick(float now, float dt, float moveSpeed)
        {
            if (_keyframeOpen && now - _keyframeSince > KeyframeTimeout) CloseKeyframe();
            float inv = 1f / Mathf.Max(0.1f, moveSpeed);
            for (int i = 0; i < _s.HighWater; i++)
            {
                if (!_s.alive[i]) continue;
                if (_s.state[i] != (byte)WormState.Sleeping && now - _rx[i] > StaleSeconds)
                {
                    Release(i);
                    DroppedStale++;
                    continue;
                }
                _s.prevPos[i] = _s.pos[i];
                _s.pos[i] = DisplayedAt(i, now);
                if (_speed[i] > MovingSpeed && _s.state[i] != (byte)WormState.Sleeping && now - _rx[i] <= StaleSeconds)
                {
                    _s.animTime[i] += _speed[i] * dt * inv;
                    _s.stillSince[i] = now;
                }
            }
        }

        /// <summary>Forgets every worm and the sequence (the peer owns the colony now, or it is far away).</summary>
        public void Clear()
        {
            for (int i = 0; i < _s.HighWater; i++)
                if (_s.alive[i]) Release(i);
            _haveSeq = false;
            _keyframeOpen = false;
            LastPacketTime = 0f;
        }
    }
}
