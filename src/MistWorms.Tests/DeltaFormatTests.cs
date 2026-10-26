using System;
using MistWorms.Colony;
using UnityEngine;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>The wire format of MW_Delta (phase 8, decision 213): round trip, header, limits, events, keyframe chunks.</summary>
    public class DeltaFormatTests
    {
        private static DeltaFormat.Header Open(DeltaWriter w, out byte[] buf)
        {
            int len = w.Finish();
            buf = new byte[len];
            Array.Copy(w.Buffer, buf, len);
            Assert.True(DeltaFormat.TryReadHeader(buf, len, out DeltaFormat.Header h));
            return h;
        }

        [Fact]
        public void EntryRoundTripsToOneCentimetreAndOneAndAHalfDegree()
        {
            var w = new DeltaWriter();
            w.Begin(0, 7);
            var rng = new System.Random(5);
            const int n = 100;
            var pos = new Vector3[n];
            var head = new float[n];
            for (int i = 0; i < n; i++)
            {
                pos[i] = new Vector3((float)(rng.NextDouble() * 200 - 100), (float)(rng.NextDouble() * 40 - 20), (float)(rng.NextDouble() * 200 - 100));
                head[i] = (float)(rng.NextDouble() * 20 - 10);   // any angle, wraps
                Assert.True(w.AddEntry((ushort)(i + 1), pos[i], head[i], 1.23f, (byte)WormState.Hunting, i % 2 == 0, false));
            }
            DeltaFormat.Header h = Open(w, out byte[] b);
            Assert.Equal(n, h.Entries);
            for (int i = 0; i < n; i++)
            {
                DeltaFormat.ReadEntry(b, i, out ushort id, out Vector3 rel, out float heading, out float speed, out WormState state, out bool lone, out bool removed);
                Assert.Equal(i + 1, id);
                Assert.True(Mathf.Abs(rel.x - pos[i].x) <= 0.0051f && Mathf.Abs(rel.y - pos[i].y) <= 0.0051f && Mathf.Abs(rel.z - pos[i].z) <= 0.0051f);   // half a centimetre
                Assert.True(DeltaFilter.HeadingDelta(heading, head[i]) <= 1.5f * Mathf.Deg2Rad, $"heading {head[i]} read as {heading}");
                Assert.InRange(speed, 1.23f - 0.026f, 1.23f + 0.026f);
                Assert.Equal(WormState.Hunting, state);
                Assert.Equal(i % 2 == 0, lone);
                Assert.False(removed);
            }
        }

        [Theory]
        [InlineData((int)WormState.Sleeping)]
        [InlineData((int)WormState.Idle)]
        [InlineData((int)WormState.Gathering)]
        [InlineData((int)WormState.Hunting)]
        [InlineData((int)WormState.Striking)]
        [InlineData((int)WormState.Fleeing)]
        [InlineData((int)WormState.Calming)]
        [InlineData((int)WormState.Absorbing)]
        public void VisualStateKeepsTheStateTheLoneBitAndTheRemovedBit(int stateCode)
        {
            foreach (bool lone in new[] { false, true })
                foreach (bool removed in new[] { false, true })
                {
                    byte v = DeltaFormat.VisualCode((byte)stateCode, lone, removed);
                    Assert.Equal((byte)stateCode, (byte)(v & DeltaFormat.VisualStateMask));
                    Assert.Equal(lone, (v & DeltaFormat.VisualLone) != 0);
                    Assert.Equal(removed, (v & DeltaFormat.VisualRemoved) != 0);
                }
            Assert.True((byte)WormState.Dead <= DeltaFormat.VisualStateMask, "every WormState fits in four bits");
        }

        [Fact]
        public void SpeedIsInFiveCentimetreStepsAndSaturates()
        {
            Assert.Equal(0, DeltaFormat.SpeedCode(0f));
            Assert.Equal(0, DeltaFormat.SpeedCode(-3f));
            Assert.Equal(0, DeltaFormat.SpeedCode(float.NaN));
            Assert.Equal(24, DeltaFormat.SpeedCode(1.2f));
            Assert.Equal(255, DeltaFormat.SpeedCode(50f));
            Assert.Equal(1.2f, DeltaFormat.SpeedFromCode(24), 4);
        }

        [Fact]
        public void PositionBeyondThePacketRangeSaturates()
        {
            var w = new DeltaWriter();
            w.Begin(0, 1);
            w.AddEntry(1, new Vector3(1000f, -1000f, 0f), 0f, 0f, 1, false, false);
            Open(w, out byte[] b);
            DeltaFormat.ReadEntry(b, 0, out _, out Vector3 rel, out _, out _, out _, out _, out _);
            Assert.Equal(327.67f, rel.x, 2);
            Assert.Equal(-327.68f, rel.y, 2);
        }

        [Fact]
        public void RemovedEntryCarriesTheRemovedBit()
        {
            var w = new DeltaWriter();
            w.Begin(0, 1);
            w.AddEntry(42, Vector3.zero, 0f, 0f, 0, false, true);
            Open(w, out byte[] b);
            DeltaFormat.ReadEntry(b, 0, out ushort id, out _, out _, out _, out _, out _, out bool removed);
            Assert.Equal(42, id);
            Assert.True(removed);
        }

        [Fact]
        public void HeaderCarriesFlagsSequenceAndCounts()
        {
            var w = new DeltaWriter();
            w.Begin(DeltaFormat.FlagReset | DeltaFormat.FlagEnd, 65535);
            w.AddEntry(1, Vector3.zero, 0f, 0f, 1, false, false);
            w.AddEvent(DeltaEventType.Death, Vector3.one, 5L);
            DeltaFormat.Header h = Open(w, out byte[] b);
            Assert.True(h.Reset && h.End);
            Assert.Equal(65535, h.Seq);
            Assert.Equal(1, h.Entries);
            Assert.Equal(1, h.Events);
            Assert.Equal(DeltaFormat.HeaderBytes + DeltaFormat.EntryBytes + DeltaFormat.EventBytes, b.Length);
        }

        [Fact]
        public void EntriesAreElevenBytesDeltasSevenAndEventsFifteen()
        {
            Assert.Equal(11, DeltaFormat.EntryBytes);
            Assert.Equal(15, DeltaFormat.EventBytes);
            Assert.Equal(8, DeltaFormat.HeaderBytes);
            Assert.Equal(7, DeltaFormat.DeltaBytes);
            Assert.Equal(2, DeltaFormat.Version);
        }

        [Fact]
        public void UnknownVersionTruncatedAndPaddedPacketsAreRefused()
        {
            var w = new DeltaWriter();
            w.Begin(0, 1);
            w.AddEntry(1, Vector3.zero, 0f, 0f, 1, false, false);
            int len = w.Finish();

            Assert.False(DeltaFormat.TryReadHeader(w.Buffer, len - 1, out _));                  // truncated
            Assert.False(DeltaFormat.TryReadHeader(w.Buffer, len + 1, out _));                  // trailing byte
            Assert.False(DeltaFormat.TryReadHeader(w.Buffer, 3, out _));                        // shorter than the header
            Assert.False(DeltaFormat.TryReadHeader(null, 0, out _));
            byte saved = w.Buffer[0];
            w.Buffer[0] = (byte)(saved + 1);
            Assert.False(DeltaFormat.TryReadHeader(w.Buffer, len, out _));                      // unknown version
            w.Buffer[0] = saved;
            Assert.True(DeltaFormat.TryReadHeader(w.Buffer, len, out _));
        }

        [Fact]
        public void EventRoundTripsWithItsPositionAndTheUidThatPlayedIt()
        {
            var w = new DeltaWriter();
            w.Begin(0, 3);
            long uid = -7234567890123456L;
            w.AddEvent(DeltaEventType.Hit, new Vector3(1.234f, -2.5f, 30f), uid);
            w.AddEvent(DeltaEventType.NestDestroyed, new Vector3(0f, 0f, 0f), long.MaxValue);
            DeltaFormat.Header h = Open(w, out byte[] b);
            Assert.Equal(2, h.Events);
            DeltaFormat.ReadEvent(b, h, 0, out DeltaEventType type, out Vector3 rel, out long by);
            Assert.Equal(DeltaEventType.Hit, type);
            Assert.Equal(1.23f, rel.x, 2);
            Assert.Equal(-2.5f, rel.y, 2);
            Assert.Equal(30f, rel.z, 2);
            Assert.Equal(uid, by);
            DeltaFormat.ReadEvent(b, h, 1, out type, out _, out by);
            Assert.Equal(DeltaEventType.NestDestroyed, type);
            Assert.Equal(long.MaxValue, by);
        }

        [Fact]
        public void EventsAreCappedAtTenPerPacket()
        {
            var w = new DeltaWriter();
            w.Begin(0, 1);
            for (int i = 0; i < DeltaFormat.MaxEvents; i++) Assert.True(w.AddEvent(DeltaEventType.Hit, Vector3.zero, 1L));
            Assert.False(w.AddEvent(DeltaEventType.Hit, Vector3.zero, 1L));
            Assert.Equal(DeltaFormat.MaxEvents, w.EventCount);
        }

        [Fact]
        public void APacketNeverExceedsTwelveHundredBytesAndEventsTakeRoomFromEntries()
        {
            var w = new DeltaWriter();
            w.Begin(0, 1);
            int n = 0;
            while (w.AddEntry((ushort)(n + 1), Vector3.zero, 0f, 0f, 1, false, false)) n++;
            Assert.Equal(DeltaFormat.MaxEntries(0), n);
            Assert.True(w.Length <= DeltaFormat.MaxPacketBytes);

            w.Begin(0, 2);
            for (int i = 0; i < 10; i++) w.AddEvent(DeltaEventType.Death, Vector3.zero, 1L);
            int m = 0;
            while (w.AddEntry((ushort)(m + 1), Vector3.zero, 0f, 0f, 1, false, false)) m++;
            Assert.Equal(DeltaFormat.MaxEntries(10), m);
            Assert.True(m < n);
            int len = w.Finish();
            Assert.True(len <= DeltaFormat.MaxPacketBytes);
            Assert.True(DeltaFormat.TryReadHeader(w.Buffer, len, out DeltaFormat.Header h));
            Assert.Equal(m, h.Entries);
            Assert.Equal(10, h.Events);
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(108, 1)]
        [InlineData(109, 2)]
        [InlineData(216, 2)]
        [InlineData(217, 3)]
        [InlineData(400, 4)]
        public void KeyframeIsCutIntoPacketsOfAtMostTwelveHundredBytes(int worms, int chunks)
        {
            Assert.Equal(chunks, DeltaFormat.KeyframeChunks(worms));

            // Build it the way the colony does and check every packet.
            var w = new DeltaWriter();
            int at = 0, packets = 0, first = 0;
            int expected = DeltaFormat.KeyframeChunks(worms);
            for (int c = 0; c < expected; c++)
            {
                byte flags = DeltaFormat.KeyframeFlags(c, expected);
                w.Begin(flags, (ushort)c);
                for (; at < worms && w.HasRoomForEntry; at++) w.AddEntry((ushort)(at + 1), Vector3.zero, 0f, 0f, 1, false, false);
                int len = w.Finish();
                Assert.True(len <= DeltaFormat.MaxPacketBytes);
                Assert.True(DeltaFormat.TryReadHeader(w.Buffer, len, out DeltaFormat.Header h));
                Assert.Equal(c == 0, h.Reset);
                Assert.Equal(c == expected - 1, h.End);
                first += h.Entries;
                packets++;
            }
            Assert.Equal(worms, at);          // nothing left over after the last packet
            Assert.Equal(worms, first);
            Assert.Equal(expected, packets);
        }

        [Fact]
        public void DeltaEntryIsSevenBytesAndRoundTripsToTwoCentimetres()
        {
            var w = new DeltaWriter();
            w.Begin(0, 9);
            w.AddEntry(1, Vector3.one, 0f, 1f, (byte)WormState.Hunting, false, false);
            Assert.True(w.AddDelta(77, DeltaFormat.DeltaCode(0.31f), DeltaFormat.DeltaCode(-0.051f), DeltaFormat.DeltaCode(-1.234f), 1.0f, 2.43f));
            w.AddEvent(DeltaEventType.Hit, Vector3.zero, 3L);
            DeltaFormat.Header h = Open(w, out byte[] b);
            Assert.Equal(1, h.Entries);
            Assert.Equal(1, h.Deltas);
            Assert.Equal(1, h.Events);
            Assert.Equal(DeltaFormat.HeaderBytes + 11 + 7 + 15, b.Length);
            DeltaFormat.ReadDelta(b, h, 0, out ushort id, out Vector3 move, out float heading, out float speed);
            Assert.Equal(77, id);
            Assert.Equal(0.32f, move.x, 3);
            Assert.Equal(-0.06f, move.y, 3);
            Assert.Equal(-1.24f, move.z, 3);
            Assert.True(DeltaFilter.HeadingDelta(heading, 1.0f) <= 1.5f * Mathf.Deg2Rad);
            Assert.Equal(2.45f, speed, 3);
            DeltaFormat.ReadEvent(b, h, 0, out DeltaEventType type, out _, out long by);   // the events follow the deltas
            Assert.Equal(DeltaEventType.Hit, type);
            Assert.Equal(3L, by);
        }

        [Fact]
        public void DeltaCodeSaturatesAtTwoAndAHalfMetres()
        {
            Assert.Equal(125, DeltaFormat.DeltaCode(9f));
            Assert.Equal(-125, DeltaFormat.DeltaCode(-9f));
            Assert.Equal(0, DeltaFormat.DeltaCode(0.009f));
            Assert.Equal(2.5f, DeltaFormat.MaxDeltaMetres, 4);
        }

        [Fact]
        public void DeltasTakeRoomAndNeverPassTheLimits()
        {
            var w = new DeltaWriter();
            w.Begin(0, 1);
            int n = 0;
            while (w.AddDelta((ushort)(n + 1), 0, 0, 0, 0f, 0f)) n++;
            Assert.Equal((DeltaFormat.MaxPacketBytes - DeltaFormat.HeaderBytes) / DeltaFormat.DeltaBytes, n);
            Assert.True(w.Length <= DeltaFormat.MaxPacketBytes);
            int len = w.Finish();
            Assert.True(DeltaFormat.TryReadHeader(w.Buffer, len, out DeltaFormat.Header h));
            Assert.Equal(n, h.Deltas);
            Assert.False(w.HasRoomForDelta);
        }

        [Fact]
        public void APacketWithAWrongDeltaCountIsRefused()
        {
            var w = new DeltaWriter();
            w.Begin(0, 1);
            w.AddDelta(1, 0, 0, 0, 0f, 0f);
            int len = w.Finish();
            Assert.True(DeltaFormat.TryReadHeader(w.Buffer, len, out _));
            w.Buffer[6] = 2;
            Assert.False(DeltaFormat.TryReadHeader(w.Buffer, len, out _));
        }

        [Fact]
        public void SequenceComparisonWrapsRound()
        {
            Assert.True(DeltaFormat.IsNewer(5, 4));
            Assert.False(DeltaFormat.IsNewer(4, 5));
            Assert.False(DeltaFormat.IsNewer(7, 7));
            Assert.True(DeltaFormat.IsNewer(2, 65534));        // wrapped
            Assert.False(DeltaFormat.IsNewer(65534, 2));
        }
    }
}
