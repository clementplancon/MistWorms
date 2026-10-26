using System;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>What happened that a peer that did not cause it has to play (spec §12.4, decision 213).</summary>
    internal enum DeltaEventType : byte
    {
        Hit = 1,            // a worm took a hit: the blood
        Death = 2,          // a worm died: the death effect
        NestDestroyed = 3,  // a nest was destroyed: the egg splash
    }

    /// <summary>
    /// The wire format of <c>MW_Delta</c>, the RPC by which the owner of a colony shows its free worms to the peers near it (spec §11.3, phase 8, decision 213). Little-endian. Header of 8
    /// bytes: version (1), flags (<see cref="FlagReset"/>, <see cref="FlagEnd"/>), seq (ushort), entry count (ushort), compact entry count (byte), event count (byte). Then the entries, 11 bytes each: net id (ushort),
    /// position (3 x int16 in cm, relative to the centre of the box of the colony), heading (int8, 256 steps of a turn), speed (uint8, 0.05 m/s steps), visual state (uint8: bits 0 to 3 =
    /// <see cref="WormState"/>, bit 6 = lone, bit 7 = removed: dead, absorbed or out). Then the compact entries ("deltas"), 7 bytes each: net id (ushort), the move from the position of the last entry or delta of that worm
    /// (3 x int8, <see cref="DeltaStep"/> = 2 cm, so at most <see cref="MaxDeltaMetres"/> per axis), heading (int8), speed (uint8); the state and the lone bit are those of that last entry (a
    /// change of either is always a full entry). A delta whose worm the peer does not know is dropped and a keyframe is asked for. Version 2 (decision 228). Then the events, 15 bytes each: type (byte), position (3 x int16, same frame), the uid of the peer that
    /// already played it (long). A packet never exceeds <see cref="MaxPacketBytes"/>. Pure data, no game access: unit tested.
    /// </summary>
    internal static class DeltaFormat
    {
        public const byte Version = 2;
        public const int HeaderBytes = 8;
        public const int EntryBytes = 11;
        public const int DeltaBytes = 7;
        public const int MaxDeltas = 255;           // the count is one byte
        public const float DeltaStep = 0.02f;
        public const int MaxDeltaCode = 125;
        public const float MaxDeltaMetres = MaxDeltaCode * DeltaStep;   // 2.5 m
        public const int EventBytes = 15;
        public const int MaxPacketBytes = 1200;
        public const int MaxEvents = 10;            // spec §12.4: at most 10 per second and per colony, so never more than that in one packet

        /// <summary>First packet of a keyframe: the receiver forgets what it knew (marks every worm stale) before it applies the entries.</summary>
        public const byte FlagReset = 1;
        /// <summary>Last packet of a keyframe: the worms that no entry of the keyframe refreshed are dropped.</summary>
        public const byte FlagEnd = 2;

        public const byte VisualStateMask = 0x0F;
        public const byte VisualLone = 0x40;
        public const byte VisualRemoved = 0x80;

        public const float SpeedStep = 0.05f;
        public const float MaxSpeed = 255 * SpeedStep;

        public static byte SpeedCode(float metresPerSecond)
        {
            if (!(metresPerSecond > 0f)) return 0;
            double q = Math.Round(metresPerSecond / SpeedStep);
            return q >= 255.0 ? (byte)255 : (byte)q;
        }

        public static float SpeedFromCode(byte code) => code * SpeedStep;

        public static sbyte DeltaCode(float metres)
        {
            double q = Math.Round(metres / DeltaStep);
            return q >= MaxDeltaCode ? (sbyte)MaxDeltaCode : q <= -MaxDeltaCode ? (sbyte)-MaxDeltaCode : (sbyte)q;
        }

        /// <summary>The move a delta carries, as owner and peer both compute it (the same float operations, so both bases stay identical).</summary>
        public static Vector3 DeltaVector(sbyte cx, sbyte cy, sbyte cz) => new Vector3(cx * DeltaStep, cy * DeltaStep, cz * DeltaStep);

        public static byte VisualCode(byte state, bool lone, bool removed) => (byte)((state & VisualStateMask) | (lone ? VisualLone : 0) | (removed ? VisualRemoved : 0));

        /// <summary>Entries that fit in a packet already holding <paramref name="events"/> events.</summary>
        public static int MaxEntries(int events) => (MaxPacketBytes - HeaderBytes - events * EventBytes) / EntryBytes;

        /// <summary>Packets of a keyframe of <paramref name="entries"/> worms (at least one: an empty colony still sends the reset that clears the mirror).</summary>
        public static int KeyframeChunks(int entries)
        {
            int per = MaxEntries(0);
            return Math.Max(1, (entries + per - 1) / per);
        }

        /// <summary>
        /// Packets of a keyframe of <paramref name="entries"/> worms whose first packet also carries <paramref name="events"/> pending events (a keyframe to everybody takes them along: the
        /// deltas of that tick are not sent): the events take room from the entries of the first packet only.
        /// </summary>
        public static int KeyframeChunks(int entries, int events)
        {
            int first = MaxEntries(events);
            if (entries <= first) return 1;
            int per = MaxEntries(0);
            return 1 + (entries - first + per - 1) / per;
        }
        /// <summary>Flags of packet <paramref name="chunk"/> (0 based) of a keyframe of <paramref name="chunks"/> packets: the first resets, the last ends.</summary>
        public static byte KeyframeFlags(int chunk, int chunks) => (byte)((chunk == 0 ? FlagReset : 0) | (chunk == chunks - 1 ? FlagEnd : 0));

        /// <summary>Sequence comparison with wrap round: true when <paramref name="seq"/> is newer than <paramref name="last"/>.</summary>
        public static bool IsNewer(ushort seq, ushort last) => seq != last && (ushort)(seq - last) < 32768;

        // ------------------------------------------------------------------ reading

        public struct Header
        {
            public byte Flags;
            public ushort Seq;
            public int Entries;
            public int Deltas;
            public int Events;

            public bool Reset => (Flags & FlagReset) != 0;
            public bool End => (Flags & FlagEnd) != 0;
        }

        /// <summary>Validates the header and the total size (an unknown version or a truncated packet is refused: nothing in it is trusted).</summary>
        public static bool TryReadHeader(byte[] b, int length, out Header h)
        {
            h = default;
            if (b == null || length < HeaderBytes || length > b.Length || b[0] != Version) return false;
            h.Flags = b[1];
            h.Seq = (ushort)ByteIO.GetUShort(b, 2);
            h.Entries = ByteIO.GetUShort(b, 4);
            h.Deltas = b[6];
            h.Events = b[7];
            return length == HeaderBytes + h.Entries * EntryBytes + h.Deltas * DeltaBytes + h.Events * EventBytes;
        }

        public static void ReadEntry(byte[] b, int k, out ushort netId, out Vector3 rel, out float heading, out float speed, out WormState state, out bool lone, out bool removed)
        {
            int at = HeaderBytes + k * EntryBytes;
            netId = (ushort)ByteIO.GetUShort(b, at);
            rel = new Vector3(ByteIO.GetShort(b, at + 2) / SnapshotFormat.PositionUnitsPerMetre,
                              ByteIO.GetShort(b, at + 4) / SnapshotFormat.PositionUnitsPerMetre,
                              ByteIO.GetShort(b, at + 6) / SnapshotFormat.PositionUnitsPerMetre);
            heading = SnapshotFormat.HeadingFromCode(unchecked((sbyte)b[at + 8]));
            speed = SpeedFromCode(b[at + 9]);
            byte v = b[at + 10];
            state = (WormState)(v & VisualStateMask);
            lone = (v & VisualLone) != 0;
            removed = (v & VisualRemoved) != 0;
        }

        public static void ReadDelta(byte[] b, Header h, int k, out ushort netId, out Vector3 move, out float heading, out float speed)
        {
            int at = HeaderBytes + h.Entries * EntryBytes + k * DeltaBytes;
            netId = (ushort)ByteIO.GetUShort(b, at);
            move = DeltaVector(unchecked((sbyte)b[at + 2]), unchecked((sbyte)b[at + 3]), unchecked((sbyte)b[at + 4]));
            heading = SnapshotFormat.HeadingFromCode(unchecked((sbyte)b[at + 5]));
            speed = SpeedFromCode(b[at + 6]);
        }

        public static void ReadEvent(byte[] b, Header h, int k, out DeltaEventType type, out Vector3 rel, out long by)
        {
            int at = HeaderBytes + h.Entries * EntryBytes + h.Deltas * DeltaBytes + k * EventBytes;
            type = (DeltaEventType)b[at];
            rel = new Vector3(ByteIO.GetShort(b, at + 1) / SnapshotFormat.PositionUnitsPerMetre,
                              ByteIO.GetShort(b, at + 3) / SnapshotFormat.PositionUnitsPerMetre,
                              ByteIO.GetShort(b, at + 5) / SnapshotFormat.PositionUnitsPerMetre);
            by = ByteIO.GetLong(b, at + 7);
        }
    }

    /// <summary>
    /// Builds one packet of <see cref="DeltaFormat"/> in a buffer that is reused (no allocation). Events first or last as the caller likes: they are written after the entries by
    /// <see cref="Finish"/>, and the room for them is counted as soon as they are added.
    /// </summary>
    internal sealed class DeltaWriter
    {
        public readonly byte[] Buffer = new byte[DeltaFormat.MaxPacketBytes];

        private byte _flags;
        private ushort _seq;
        private int _entries;
        private int _deltas;
        private int _events;
        private readonly byte[] _entryBytes = new byte[DeltaFormat.MaxPacketBytes];
        private readonly byte[] _deltaBytes = new byte[DeltaFormat.MaxPacketBytes];
        private readonly byte[] _eventBytes = new byte[DeltaFormat.MaxEvents * DeltaFormat.EventBytes];

        public int EntryCount => _entries;
        public int DeltaCount => _deltas;
        public int EventCount => _events;

        /// <summary>Length of the packet as it stands, events included.</summary>
        public int Length => DeltaFormat.HeaderBytes + _entries * DeltaFormat.EntryBytes + _deltas * DeltaFormat.DeltaBytes + _events * DeltaFormat.EventBytes;

        public bool IsEmpty => _entries == 0 && _deltas == 0 && _events == 0;

        public void Begin(byte flags, ushort seq)
        {
            _flags = flags;
            _seq = seq;
            _entries = 0;
            _deltas = 0;
            _events = 0;
        }

        public bool HasRoomForEntry => Length + DeltaFormat.EntryBytes <= DeltaFormat.MaxPacketBytes;

        public bool HasRoomForDelta => _deltas < DeltaFormat.MaxDeltas && Length + DeltaFormat.DeltaBytes <= DeltaFormat.MaxPacketBytes;

        /// <summary>Adds an entry; false when the packet is full (nothing written). <paramref name="rel"/> is relative to the centre of the box.</summary>
        public bool AddEntry(ushort netId, Vector3 rel, float headingRad, float speed, byte state, bool lone, bool removed)
        {
            if (!HasRoomForEntry) return false;
            int at = _entries * DeltaFormat.EntryBytes;
            byte[] b = _entryBytes;
            ByteIO.PutUShort(b, at, netId);
            ByteIO.PutShort(b, at + 2, SnapshotFormat.PositionCode(rel.x));
            ByteIO.PutShort(b, at + 4, SnapshotFormat.PositionCode(rel.y));
            ByteIO.PutShort(b, at + 6, SnapshotFormat.PositionCode(rel.z));
            b[at + 8] = unchecked((byte)SnapshotFormat.HeadingCode(headingRad));
            b[at + 9] = DeltaFormat.SpeedCode(speed);
            b[at + 10] = DeltaFormat.VisualCode(state, lone, removed);
            _entries++;
            return true;
        }

        /// <summary>Adds a compact entry (the move in <see cref="DeltaFormat.DeltaStep"/> units); false when the packet is full (nothing written).</summary>
        public bool AddDelta(ushort netId, sbyte cx, sbyte cy, sbyte cz, float headingRad, float speed)
        {
            if (!HasRoomForDelta) return false;
            int at = _deltas * DeltaFormat.DeltaBytes;
            byte[] b = _deltaBytes;
            ByteIO.PutUShort(b, at, netId);
            b[at + 2] = unchecked((byte)cx);
            b[at + 3] = unchecked((byte)cy);
            b[at + 4] = unchecked((byte)cz);
            b[at + 5] = unchecked((byte)SnapshotFormat.HeadingCode(headingRad));
            b[at + 6] = DeltaFormat.SpeedCode(speed);
            _deltas++;
            return true;
        }

        /// <summary>Adds an event; false when there are already <see cref="DeltaFormat.MaxEvents"/> or the packet is full.</summary>
        public bool AddEvent(DeltaEventType type, Vector3 rel, long by)
        {
            if (_events >= DeltaFormat.MaxEvents || Length + DeltaFormat.EventBytes > DeltaFormat.MaxPacketBytes) return false;
            int at = _events * DeltaFormat.EventBytes;
            byte[] e = _eventBytes;
            e[at] = (byte)type;
            ByteIO.PutShort(e, at + 1, SnapshotFormat.PositionCode(rel.x));
            ByteIO.PutShort(e, at + 3, SnapshotFormat.PositionCode(rel.y));
            ByteIO.PutShort(e, at + 5, SnapshotFormat.PositionCode(rel.z));
            ByteIO.PutLong(e, at + 7, by);
            _events++;
            return true;
        }

        /// <summary>Writes the header and the events; returns the length of the packet in <see cref="Buffer"/>.</summary>
        public int Finish()
        {
            byte[] b = Buffer;
            b[0] = DeltaFormat.Version;
            b[1] = _flags;
            ByteIO.PutUShort(b, 2, _seq);
            ByteIO.PutUShort(b, 4, _entries);
            b[6] = (byte)_deltas;
            b[7] = (byte)_events;
            int at = DeltaFormat.HeaderBytes;
            Array.Copy(_entryBytes, 0, b, at, _entries * DeltaFormat.EntryBytes);
            at += _entries * DeltaFormat.EntryBytes;
            Array.Copy(_deltaBytes, 0, b, at, _deltas * DeltaFormat.DeltaBytes);
            at += _deltas * DeltaFormat.DeltaBytes;
            Array.Copy(_eventBytes, 0, b, at, _events * DeltaFormat.EventBytes);
            return at + _events * DeltaFormat.EventBytes;
        }
    }
}
