using System;
using System.Runtime.InteropServices;
using MistWorms.Drops;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>Little-endian reads and writes in a byte array, without allocation (the ZDO form of the snapshot, pools included).</summary>
    internal static class ByteIO
    {
        [StructLayout(LayoutKind.Explicit)]
        private struct FloatBits
        {
            [FieldOffset(0)] public float F;
            [FieldOffset(0)] public int I;
        }

        public static void PutUShort(byte[] b, int at, int v)
        {
            b[at] = (byte)v;
            b[at + 1] = (byte)(v >> 8);
        }

        public static int GetUShort(byte[] b, int at) => b[at] | (b[at + 1] << 8);

        public static void PutShort(byte[] b, int at, int v) => PutUShort(b, at, v & 0xFFFF);

        public static int GetShort(byte[] b, int at) => (short)GetUShort(b, at);

        public static void PutInt(byte[] b, int at, int v)
        {
            b[at] = (byte)v;
            b[at + 1] = (byte)(v >> 8);
            b[at + 2] = (byte)(v >> 16);
            b[at + 3] = (byte)(v >> 24);
        }

        public static int GetInt(byte[] b, int at) => b[at] | (b[at + 1] << 8) | (b[at + 2] << 16) | (b[at + 3] << 24);

        public static void PutFloat(byte[] b, int at, float v) => PutInt(b, at, new FloatBits { F = v }.I);

        public static float GetFloat(byte[] b, int at) => new FloatBits { I = GetInt(b, at) }.F;

        public static void PutLong(byte[] b, int at, long v)
        {
            PutInt(b, at, (int)v);
            PutInt(b, at + 4, (int)(v >> 32));
        }

        public static long GetLong(byte[] b, int at) => (uint)GetInt(b, at) | ((long)GetInt(b, at + 4) << 32);
    }

    /// <summary>
    /// The <c>mw_snap</c> byte array of the ZDO of a colony (spec §11.4, phase 7-3, decision 209), little-endian: version (1 byte), worm count (ushort); per worm 11 bytes
    /// (3 x int16 position in cm relative to the centre of the box, int8 heading in 256 steps, int8 state with bit 7 = lone, uint16 hit points in hundredths (0 to 655.35, the
    /// config caps a worm at 200), int8 nest index -1 to 63). Version 1 (hit points x 10 in an int8, capped at 12.7) is no longer read: it falls under the unknown version rule; the pools of the worms that have one (ushort count, then worm index in the snapshot order + a <see cref="MassPool"/> in its own byte format);
    /// the reserve of the colony (a pool). Pure data, no game access: unit tested.
    /// </summary>
    internal static class SnapshotFormat
    {
        public const byte Version = 2;
        public const int HeaderBytes = 3;
        public const int WormBytes = 11;

        public const float PositionUnitsPerMetre = 100f;
        public const int MaxHpCode = ushort.MaxValue;
        public const float HpPerCode = 0.01f;
        public const byte LoneBit = 0x80;
        public const int MaxNestIndex = 63;

        private const double TwoPi = Math.PI * 2.0;

        // ------------------------------------------------------------------ one worm

        public static int PositionCode(float metres)
        {
            double cm = Math.Round(metres * PositionUnitsPerMetre);
            if (!(cm > short.MinValue)) return short.MinValue;   // also NaN
            return cm > short.MaxValue ? short.MaxValue : (int)cm;
        }

        /// <summary>The heading (any angle, radians) in 256 steps of a turn, as the int8 of the format.</summary>
        public static sbyte HeadingCode(float radians)
        {
            double turns = radians / TwoPi;
            turns -= Math.Floor(turns);
            int q = (int)Math.Round(turns * 256.0) & 255;
            return unchecked((sbyte)(byte)q);
        }

        public static float HeadingFromCode(sbyte code) => (float)(code * (TwoPi / 256.0));

        /// <summary>Hit points in hundredths rounded, 0 to 65535 (above 655.35 hit points the value is capped, below 0.005 it reads as 0).</summary>
        public static int HpCode(float hp)
        {
            if (!(hp > 0f)) return 0;
            double q = Math.Round(hp * 100.0);
            return q > MaxHpCode ? MaxHpCode : (int)q;
        }

        public static float HpFromCode(int code) => code * HpPerCode;

        public static sbyte NestCode(int nestIndex) => (sbyte)(nestIndex < 0 || nestIndex > MaxNestIndex ? -1 : nestIndex);

        public static byte StateCode(byte state, bool lone) => (byte)((state & 0x7F) | (lone ? LoneBit : 0));

        /// <summary>
        /// The state a worm restarts in: asleep stays asleep, walking home becomes asleep where it stands, every other active state becomes Idle (the worm then wanders around
        /// where it was read). Absorbed, Dead and anything unknown give false (nothing to restore).
        /// </summary>
        public static bool RestoredState(byte stateCode, out WormState state)
        {
            switch ((WormState)(stateCode & 0x7F))
            {
                case WormState.Sleeping:
                case WormState.Calming:
                    state = WormState.Sleeping;
                    return true;
                case WormState.Idle:
                case WormState.Gathering:
                case WormState.Hunting:
                case WormState.Striking:
                case WormState.Fleeing:
                case WormState.Absorbing:
                    state = WormState.Idle;
                    return true;
                default:
                    state = WormState.Dead;
                    return false;
            }
        }

        public static void WriteWorm(byte[] b, int at, Vector3 rel, float heading, byte state, bool lone, float hp, int nestIndex)
        {
            ByteIO.PutShort(b, at, PositionCode(rel.x));
            ByteIO.PutShort(b, at + 2, PositionCode(rel.y));
            ByteIO.PutShort(b, at + 4, PositionCode(rel.z));
            b[at + 6] = unchecked((byte)HeadingCode(heading));
            b[at + 7] = StateCode(state, lone);
            ByteIO.PutUShort(b, at + 8, HpCode(hp));
            b[at + 10] = unchecked((byte)NestCode(nestIndex));
        }

        /// <summary>Worm <paramref name="index"/> of a snapshot that <see cref="TryOpen"/> accepted.</summary>
        public static void ReadWorm(byte[] data, int index, out Vector3 rel, out float heading, out byte stateCode, out bool lone, out float hp, out int nestIndex)
        {
            int at = HeaderBytes + index * WormBytes;
            rel = new Vector3(ByteIO.GetShort(data, at) / PositionUnitsPerMetre, ByteIO.GetShort(data, at + 2) / PositionUnitsPerMetre, ByteIO.GetShort(data, at + 4) / PositionUnitsPerMetre);
            heading = HeadingFromCode(unchecked((sbyte)data[at + 6]));
            byte raw = data[at + 7];
            lone = (raw & LoneBit) != 0;
            stateCode = (byte)(raw & 0x7F);
            hp = HpFromCode(ByteIO.GetUShort(data, at + 8));
            nestIndex = unchecked((sbyte)data[at + 10]);
        }

        // ------------------------------------------------------------------ the whole array

        /// <summary>The worm count in the header of a snapshot, 0 for no array, a short one or an unknown version (no validation of the rest: see <see cref="TryOpen"/>).</summary>
        public static int PeekCount(byte[] data) => data != null && data.Length >= HeaderBytes && data[0] == Version ? ByteIO.GetUShort(data, 1) : 0;

        /// <summary>Where the pool section starts for a snapshot of <paramref name="wormCount"/> worms.</summary>
        public static int PoolSectionStart(int wormCount) => HeaderBytes + wormCount * WormBytes;

        /// <summary>
        /// Checks the whole array (version, lengths, every pool, the reserve) so that a restore never starts on a broken one. <paramref name="scratch"/> receives the pools while
        /// they are read. False with a short reason otherwise; trailing bytes are tolerated.
        /// </summary>
        public static bool TryOpen(byte[] data, MassPool scratch, out int wormCount, out string problem)
        {
            wormCount = 0;
            problem = null;
            if (data == null || data.Length < HeaderBytes) { problem = "shorter than its header"; return false; }
            if (data[0] != Version) { problem = "unknown version " + data[0]; return false; }
            int n = ByteIO.GetUShort(data, 1);
            int at = PoolSectionStart(n);
            if (at + 2 > data.Length) { problem = "truncated in the worms"; return false; }

            int pools = ByteIO.GetUShort(data, at);
            at += 2;
            for (int k = 0; k < pools; k++)
            {
                if (at + 2 > data.Length) { problem = "truncated in the pools"; return false; }
                int worm = ByteIO.GetUShort(data, at);
                at += 2;
                if (worm >= n) { problem = "a pool of a worm that is not there"; return false; }
                if (!scratch.ReadFrom(data, ref at)) { problem = "unreadable or truncated pool"; return false; }
            }
            if (!scratch.ReadFrom(data, ref at)) { problem = "unreadable or truncated reserve"; return false; }
            wormCount = n;
            return true;
        }
    }

    /// <summary>
    /// Builds a snapshot in a buffer that is reused (grown when needed): <see cref="Begin"/>, one <see cref="AddWorm"/> per worm, <see cref="BeginPools"/>, one
    /// <see cref="AddPool"/> per pool, <see cref="Finish"/>. Nothing is allocated until the changed result is copied out with <see cref="ToArray"/> (ZDO.Set keeps the
    /// reference it is given, so what was handed over is never written to again).
    /// </summary>
    internal sealed class SnapshotWriter
    {
        private byte[] _buf;
        private int _len;
        private int _worms;
        private int _poolCountAt;
        private int _pools;

        public SnapshotWriter(int wormCapacity)
        {
            _buf = new byte[SnapshotFormat.HeaderBytes + Math.Max(1, wormCapacity) * SnapshotFormat.WormBytes + 512];
        }

        public int Length => _len;
        public int WormCount => _worms;
        public int PoolCount => _pools;

        private void Ensure(int extra)
        {
            if (_len + extra > _buf.Length) Array.Resize(ref _buf, Math.Max(_buf.Length * 2, _len + extra));
        }

        public void Begin()
        {
            _len = SnapshotFormat.HeaderBytes;
            _worms = 0;
            _pools = 0;
            _buf[0] = SnapshotFormat.Version;
        }

        /// <summary>Appends a worm; returns its index in the snapshot, or -1 when the 65535 of the count field are taken.</summary>
        public int AddWorm(Vector3 rel, float heading, byte state, bool lone, float hp, int nestIndex)
        {
            if (_worms >= ushort.MaxValue) return -1;
            Ensure(SnapshotFormat.WormBytes);
            SnapshotFormat.WriteWorm(_buf, _len, rel, heading, state, lone, hp, nestIndex);
            _len += SnapshotFormat.WormBytes;
            return _worms++;
        }

        public void BeginPools()
        {
            Ensure(2);
            _poolCountAt = _len;
            _len += 2;
            _pools = 0;
        }

        public void AddPool(int wormIndex, MassPool pool)
        {
            Ensure(4 + pool.SerializedSize);
            ByteIO.PutUShort(_buf, _len, wormIndex);
            _len = pool.WriteTo(_buf, _len + 2);
            _pools++;
        }

        /// <summary>Writes the reserve (null = empty) and the two counts.</summary>
        public void Finish(MassPool reserve)
        {
            if (reserve != null)
            {
                Ensure(reserve.SerializedSize);
                _len = reserve.WriteTo(_buf, _len);
            }
            else
            {
                Ensure(2);
                _buf[_len++] = MassPool.FormatVersion;
                _buf[_len++] = 0;
            }
            ByteIO.PutUShort(_buf, 1, _worms);
            ByteIO.PutUShort(_buf, _poolCountAt, _pools);
        }

        /// <summary>Is the snapshot just built the same bytes as <paramref name="other"/>?</summary>
        public bool SameAs(byte[] other)
        {
            if (other == null || other.Length != _len) return false;
            for (int i = 0; i < _len; i++)
                if (_buf[i] != other[i]) return false;
            return true;
        }

        /// <summary>An exact size copy: the one allocation of a write that changed something.</summary>
        public byte[] ToArray()
        {
            var copy = new byte[_len];
            Buffer.BlockCopy(_buf, 0, copy, 0, _len);
            return copy;
        }
    }
}
