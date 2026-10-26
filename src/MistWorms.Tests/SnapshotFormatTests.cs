using System;
using MistWorms.Colony;
using MistWorms.Drops;
using UnityEngine;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>The <c>mw_snap</c> format of a colony (spec §11.4, phase 7-3, decision 209): round trip, quantization, refusals, pools.</summary>
    public class SnapshotFormatTests
    {
        private const int Resin = 1111, Coal = 2222;

        private static byte[] Build(Action<SnapshotWriter> fill, MassPool reserve = null, int capacity = 8)
        {
            var w = new SnapshotWriter(capacity);
            w.Begin();
            fill(w);
            w.BeginPools();
            w.Finish(reserve);
            return w.ToArray();
        }

        private static byte[] BuildWithPools(MassPool[] pools, MassPool reserve)
        {
            var w = new SnapshotWriter(4);
            w.Begin();
            for (int i = 0; i < pools.Length; i++) w.AddWorm(new Vector3(i, 0f, 0f), 0f, (byte)WormState.Idle, false, 10f, 0);
            w.BeginPools();
            for (int i = 0; i < pools.Length; i++)
                if (pools[i] != null) w.AddPool(i, pools[i]);
            w.Finish(reserve);
            return w.ToArray();
        }

        [Fact]
        public void RoundTripKeepsPositionHeadingStateHpAndNest()
        {
            var rng = new System.Random(15);
            var written = new Vector3[40];
            var headings = new float[40];
            var w = new SnapshotWriter(8);   // smaller than the 40 worms: the buffer grows
            w.Begin();
            for (int i = 0; i < written.Length; i++)
            {
                written[i] = new Vector3((float)(rng.NextDouble() * 200 - 100), (float)(rng.NextDouble() * 40 - 20), (float)(rng.NextDouble() * 200 - 100));
                headings[i] = (float)(rng.NextDouble() * 20 - 10);   // any angle, several turns
                w.AddWorm(written[i], headings[i], (byte)(i % 2 == 0 ? WormState.Hunting : WormState.Sleeping), i % 3 == 0, i * 0.25f, i % 5 == 0 ? -1 : i % 64);
            }
            w.BeginPools();
            w.Finish(null);
            byte[] data = w.ToArray();

            Assert.True(SnapshotFormat.TryOpen(data, new MassPool(), out int n, out string problem), problem);
            Assert.Equal(written.Length, n);
            for (int i = 0; i < n; i++)
            {
                SnapshotFormat.ReadWorm(data, i, out Vector3 rel, out float heading, out byte state, out bool lone, out float hp, out int nest);
                Assert.True(Math.Abs(rel.x - written[i].x) <= 0.005f + 1e-4f && Math.Abs(rel.y - written[i].y) <= 0.005f + 1e-4f && Math.Abs(rel.z - written[i].z) <= 0.005f + 1e-4f, $"worm {i}: {rel} vs {written[i]}");
                float diff = Math.Abs(WormMath.WrapPi(heading - headings[i])) * Mathf.Rad2Deg;
                Assert.True(diff <= 1.5f, $"worm {i}: heading off by {diff} degrees");
                Assert.Equal(i % 2 == 0 ? (byte)WormState.Hunting : (byte)WormState.Sleeping, state);
                Assert.Equal(i % 3 == 0, lone);
                Assert.Equal(i % 5 == 0 ? -1 : i % 64, nest);
                Assert.True(Math.Abs(hp - i * 0.25f) <= 0.005f + 1e-4f, $"worm {i}: hp {hp}");
            }
        }

        [Fact]
        public void HeadingErrorIsUnderOneDegree()
        {
            for (float h = -7f; h < 7f; h += 0.0137f)
            {
                float back = SnapshotFormat.HeadingFromCode(SnapshotFormat.HeadingCode(h));
                Assert.True(Math.Abs(WormMath.WrapPi(back - h)) * Mathf.Rad2Deg <= 0.71f, $"{h}");
            }
        }

        [Fact]
        public void LoneIsBitSevenAndTheStateKeepsItsSevenBits()
        {
            Assert.Equal((byte)0x80, SnapshotFormat.LoneBit);
            Assert.Equal((byte)(0x80 | (byte)WormState.Sleeping), SnapshotFormat.StateCode((byte)WormState.Sleeping, true));
            Assert.Equal((byte)WormState.Absorbing, SnapshotFormat.StateCode((byte)WormState.Absorbing, false));
            byte[] data = Build(w => w.AddWorm(Vector3.zero, 0f, (byte)WormState.Calming, true, 10f, 3));
            SnapshotFormat.ReadWorm(data, 0, out _, out _, out byte state, out bool lone, out _, out int nest);
            Assert.True(lone);
            Assert.Equal((byte)WormState.Calming, state);
            Assert.Equal(3, nest);
        }

        [Theory]
        [InlineData(0f, 0)]
        [InlineData(-1f, 0)]
        [InlineData(float.NaN, 0)]
        [InlineData(0.004f, 0)]
        [InlineData(0.01f, 1)]
        [InlineData(10f, 1000)]
        [InlineData(20f, 2000)]
        [InlineData(200f, 20000)]
        [InlineData(655.35f, 65535)]
        [InlineData(5000f, 65535)]
        public void HitPointsAreHundredthsCappedAtTheUShort(float hp, int code)
        {
            Assert.Equal(code, SnapshotFormat.HpCode(hp));
        }

        [Fact]
        public void HitPointsOfAHealthyWormComeBackExactly()
        {
            Assert.Equal(10f, SnapshotFormat.HpFromCode(SnapshotFormat.HpCode(10f)), 4);
            Assert.True(Math.Abs(SnapshotFormat.HpFromCode(SnapshotFormat.HpCode(4.35f)) - 4.35f) <= 0.0051f);   // a hundredth, the error is half of it
            Assert.Equal(200f, SnapshotFormat.HpFromCode(SnapshotFormat.HpCode(200f)), 3);   // the most the config allows for a worm
            Assert.Equal(20f, SnapshotFormat.HpFromCode(SnapshotFormat.HpCode(20f)), 3);
        }

        [Fact]
        public void PositionsOutOfRangeAreClamped()
        {
            Assert.Equal(short.MaxValue, SnapshotFormat.PositionCode(400f));
            Assert.Equal(short.MinValue, SnapshotFormat.PositionCode(-400f));
            Assert.Equal(-123, SnapshotFormat.PositionCode(-1.234f));
            Assert.Equal(-1, SnapshotFormat.NestCode(64));
            Assert.Equal(-1, SnapshotFormat.NestCode(-5));
            Assert.Equal(63, SnapshotFormat.NestCode(63));
        }

        [Theory]
        [InlineData((byte)WormState.Sleeping, (byte)WormState.Sleeping, true)]
        [InlineData((byte)WormState.Calming, (byte)WormState.Sleeping, true)]
        [InlineData((byte)WormState.Idle, (byte)WormState.Idle, true)]
        [InlineData((byte)WormState.Hunting, (byte)WormState.Idle, true)]
        [InlineData((byte)WormState.Striking, (byte)WormState.Idle, true)]
        [InlineData((byte)WormState.Fleeing, (byte)WormState.Idle, true)]
        [InlineData((byte)WormState.Gathering, (byte)WormState.Idle, true)]
        [InlineData((byte)WormState.Absorbing, (byte)WormState.Idle, true)]
        [InlineData((byte)WormState.Absorbed, (byte)WormState.Dead, false)]
        [InlineData((byte)WormState.Dead, (byte)WormState.Dead, false)]
        public void RestoredStates(byte written, byte restored, bool kept)
        {
            Assert.Equal(kept, SnapshotFormat.RestoredState(SnapshotFormat.StateCode(written, true), out WormState state));
            if (kept) Assert.Equal(restored, (byte)state);
        }

        [Fact]
        public void UnknownVersionIsRefused()
        {
            byte[] data = Build(w => w.AddWorm(Vector3.zero, 0f, (byte)WormState.Idle, false, 10f, 0));
            data[0] = 1;   // the previous format (int8 hit points): refused
            Assert.False(SnapshotFormat.TryOpen(data, new MassPool(), out int n, out string problem));
            Assert.Equal(0, n);
            Assert.Contains("version", problem);
        }

        [Fact]
        public void NullAndTinyArraysAreRefused()
        {
            Assert.False(SnapshotFormat.TryOpen(null, new MassPool(), out _, out _));
            Assert.False(SnapshotFormat.TryOpen(new byte[0], new MassPool(), out _, out _));
            Assert.False(SnapshotFormat.TryOpen(new byte[] { 1, 0 }, new MassPool(), out _, out _));
        }

        [Fact]
        public void EveryTruncationIsRefused()
        {
            var pool = new MassPool();
            pool.Add(Resin, 3.5f);
            var reserve = new MassPool();
            reserve.Add(Coal, 1.25f);
            byte[] data = BuildWithPools(new[] { pool, null, pool }, reserve);
            Assert.True(SnapshotFormat.TryOpen(data, new MassPool(), out _, out string whole), whole);
            for (int cut = 0; cut < data.Length; cut++)
            {
                var part = new byte[cut];
                Array.Copy(data, part, cut);
                Assert.False(SnapshotFormat.TryOpen(part, new MassPool(), out _, out _), $"cut at {cut} of {data.Length}");
            }
        }

        [Fact]
        public void APoolOfAWormThatIsNotThereIsRefused()
        {
            var pool = new MassPool();
            pool.Add(Resin, 1f);
            var w = new SnapshotWriter(2);
            w.Begin();
            w.AddWorm(Vector3.zero, 0f, (byte)WormState.Idle, false, 10f, 0);
            w.BeginPools();
            w.AddPool(5, pool);
            w.Finish(null);
            Assert.False(SnapshotFormat.TryOpen(w.ToArray(), new MassPool(), out _, out _));
        }

        [Fact]
        public void PoolsAndReserveComeBack()
        {
            var a = new MassPool();
            a.Add(Resin, 3.25f);
            a.Add(Coal, 0.5f);
            var c = new MassPool();
            c.Add(Coal, 7f);
            c.Share(1, 2);   // some of it in the carry
            var reserve = new MassPool();
            reserve.Add(Resin, 1.75f);
            byte[] data = BuildWithPools(new[] { a, null, c }, reserve);

            var scratch = new MassPool();
            Assert.True(SnapshotFormat.TryOpen(data, scratch, out int n, out string problem), problem);
            Assert.Equal(3, n);
            int at = SnapshotFormat.PoolSectionStart(n);
            Assert.Equal(2, ByteIO.GetUShort(data, at));
            at += 2;

            Assert.Equal(0, ByteIO.GetUShort(data, at));
            at += 2;
            Assert.True(scratch.ReadFrom(data, ref at));
            Assert.Equal(3.25f, scratch.Total(Resin), 5);
            Assert.Equal(0.5f, scratch.Total(Coal), 5);

            Assert.Equal(2, ByteIO.GetUShort(data, at));
            at += 2;
            Assert.True(scratch.ReadFrom(data, ref at));
            Assert.Equal(7f, scratch.Total(Coal), 5);
            Assert.Equal(c.CarryAt(0), scratch.CarryAt(0), 5);

            Assert.True(scratch.ReadFrom(data, ref at));
            Assert.Equal(1.75f, scratch.Total(Resin), 5);
            Assert.Equal(data.Length, at);
        }

        [Fact]
        public void PoolBytesAreTheBytesOfTheExistingFormat()
        {
            var pool = new MassPool();
            pool.Add(Resin, 3.25f);
            pool.Add(Coal, -0f + 0.75f);
            pool.Add(-77, 123.5f);
            pool.Share(1, 3);

            var buf = new byte[pool.SerializedSize + 3];
            Assert.Equal(buf.Length - 1, pool.WriteTo(buf, 2));
            byte[] expected = pool.ToBytes();
            Assert.Equal(expected.Length, pool.SerializedSize);
            for (int i = 0; i < expected.Length; i++) Assert.Equal(expected[i], buf[2 + i]);

            // and the other way: what the ZPackage form wrote reads back from an array
            var back = new MassPool();
            int at = 0;
            Assert.True(back.ReadFrom(expected, ref at));
            Assert.Equal(expected.Length, at);
            Assert.Equal(pool.TotalAll(), back.TotalAll(), 4);
            Assert.Equal(pool.Count, back.Count);
        }

        [Fact]
        public void BrokenPoolsAreRefused()
        {
            var pool = new MassPool();
            pool.Add(Resin, 2f);
            byte[] good = pool.ToBytes();
            int at = 0;
            var read = new MassPool();

            var badVersion = (byte[])good.Clone();
            badVersion[0] = 9;
            Assert.False(read.ReadFrom(badVersion, ref at));

            var tooMany = (byte[])good.Clone();
            tooMany[1] = (byte)(MassPool.MaxEntries + 1);
            at = 0;
            Assert.False(read.ReadFrom(tooMany, ref at));

            var nan = (byte[])good.Clone();
            ByteIO.PutFloat(nan, 6, float.NaN);
            at = 0;
            Assert.False(read.ReadFrom(nan, ref at));
            Assert.True(read.IsEmpty);

            var negative = (byte[])good.Clone();
            ByteIO.PutFloat(negative, 6, -1f);
            at = 0;
            Assert.False(read.ReadFrom(negative, ref at));

            at = 0;
            Assert.False(read.ReadFrom(new byte[] { MassPool.FormatVersion, 1, 0, 0 }, ref at));
        }

        [Fact]
        public void SameAsDetectsAChangeAndToArrayIsACopy()
        {
            var w = new SnapshotWriter(4);
            w.Begin();
            w.AddWorm(new Vector3(1f, 2f, 3f), 0.5f, (byte)WormState.Idle, false, 10f, 1);
            w.BeginPools();
            w.Finish(null);
            byte[] first = w.ToArray();
            Assert.True(w.SameAs(first));

            w.Begin();
            w.AddWorm(new Vector3(1f, 2f, 3.01f), 0.5f, (byte)WormState.Idle, false, 10f, 1);
            w.BeginPools();
            w.Finish(null);
            Assert.False(w.SameAs(first));
            byte[] second = w.ToArray();
            Assert.True(w.SameAs(second));
            Assert.False(ReferenceEquals(first, second));
            Assert.False(w.SameAs(null));

            w.Begin();   // rebuilding does not touch what was handed out
            w.BeginPools();
            w.Finish(null);
            Assert.True(SnapshotFormat.TryOpen(first, new MassPool(), out int n, out _));
            Assert.Equal(1, n);
        }

        [Fact]
        public void AnEmptyColonyIsAValidSnapshot()
        {
            byte[] data = Build(w => { });
            Assert.True(SnapshotFormat.TryOpen(data, new MassPool(), out int n, out string problem), problem);
            Assert.Equal(0, n);
            Assert.Equal(SnapshotFormat.HeaderBytes + 2 + 2, data.Length);
        }

        [Fact]
        public void TwoHundredFiftyWormsFitInAboutTwoAndAHalfKilobytes()
        {
            byte[] data = Build(w => { for (int i = 0; i < 250; i++) w.AddWorm(new Vector3(i * 0.1f, 0f, 0f), 0f, (byte)WormState.Sleeping, false, 10f, i % 8); }, capacity: 250);
            Assert.Equal(SnapshotFormat.HeaderBytes + 250 * SnapshotFormat.WormBytes + 2 + 2, data.Length);
        }
    }
}
