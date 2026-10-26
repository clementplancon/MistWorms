using System;
using System.Collections.Generic;
using MistWorms.Colony;
using UnityEngine;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>Corrections of the review of pass 8-1 (decision 225): reset on a full store, bad states, host release, keyframe with events.</summary>
    public class KeyframeFixTests
    {
        private static readonly Vector3 Center = new Vector3(1000f, 50f, -300f);

        private static DeltaFormat.Header Open(DeltaWriter w, out byte[] b)
        {
            int len = w.Finish();
            b = new byte[len];
            Array.Copy(w.Buffer, b, len);
            Assert.True(DeltaFormat.TryReadHeader(b, len, out DeltaFormat.Header h));
            return h;
        }

        private static void Send(WormMirror mirror, DeltaWriter w, ushort seq, byte flags, float now, WormState state, params ushort[] ids)
        {
            w.Begin(flags, seq);
            foreach (ushort id in ids) w.AddEntry(id, Vector3.zero, 0f, 0f, (byte)state, false, false);
            DeltaFormat.Header h = Open(w, out byte[] b);
            mirror.Apply(b, h, Center, now, null);
        }

        [Fact]
        public void AResetKeyframeMakesRoomInAFullStoreForTheRenumberedWorms()
        {
            var store = new WormStore(3);
            var mirror = new WormMirror(store);
            var w = new DeltaWriter();
            Send(mirror, w, 1, 0, 0f, WormState.Idle, 1, 2, 3);
            Assert.Equal(3, store.AliveCount);
            Send(mirror, w, 2, DeltaFormat.FlagReset | DeltaFormat.FlagEnd, 0.1f, WormState.Idle, 100, 101, 102);
            Assert.Equal(0, mirror.Refused);
            Assert.Equal(3, store.AliveCount);
            Assert.True(store.FindByNetId(100) >= 0);
            Assert.True(store.FindByNetId(1) < 0);
            Assert.Equal(1, mirror.KeyframesCompleted);
        }

        [Fact]
        public void ALaterPacketOfTheSameKeyframeOnlyAdds()
        {
            var store = new WormStore(10);
            var mirror = new WormMirror(store);
            var w = new DeltaWriter();
            Send(mirror, w, 1, DeltaFormat.FlagReset, 0f, WormState.Idle, 10, 11);
            Send(mirror, w, 2, 0, 0f, WormState.Idle, 12, 13);
            Assert.Equal(4, store.AliveCount);
            Assert.Equal(0, mirror.KeyframesCompleted);
            Send(mirror, w, 3, DeltaFormat.FlagEnd, 0f, WormState.Idle, 14);
            Assert.Equal(5, store.AliveCount);
            Assert.Equal(1, mirror.KeyframesCompleted);
        }

        [Fact]
        public void AnEntryWhoseStateIsNotAWormStateIsRefused()
        {
            var store = new WormStore(10);
            var mirror = new WormMirror(store);
            var w = new DeltaWriter();
            w.Begin(0, 1);
            w.AddEntry(1, Vector3.zero, 0f, 0f, 12, false, false);                                  // 12 is not a WormState
            w.AddEntry(2, Vector3.zero, 0f, 0f, (byte)WormState.Absorbed, false, false);            // never an entry: skipped
            w.AddEntry(3, Vector3.zero, 0f, 0f, (byte)WormState.Absorbing, false, false);
            DeltaFormat.Header h = Open(w, out byte[] b);
            Assert.True(mirror.Apply(b, h, Center, 0f, null));
            Assert.Equal(1, mirror.Refused);
            Assert.Equal(1, store.AliveCount);
            Assert.True(store.FindByNetId(1) < 0);
            Assert.True(store.FindByNetId(3) >= 0);
        }

        private sealed class RecordingHost : IMirrorHost
        {
            public readonly List<int> Released = new List<int>();
            public WormStore Store;
            public void ReleaseSlot(int slot) { Released.Add(slot); Store.Free(slot); }
        }

        [Fact]
        public void EveryReleaseOfAMirroredSlotGoesThroughTheHost()
        {
            var store = new WormStore(10);
            var host = new RecordingHost { Store = store };
            var mirror = new WormMirror(store, host);
            var w = new DeltaWriter();
            w.Begin(0, 1);
            w.AddEntry(1, Vector3.zero, 0f, 0f, (byte)WormState.Hunting, false, false);
            w.AddEntry(2, Vector3.zero, 0f, 0f, (byte)WormState.Hunting, false, false);
            w.AddEntry(3, Vector3.zero, 0f, 0f, (byte)WormState.Sleeping, false, false);
            w.AddEntry(4, Vector3.zero, 0f, 0f, (byte)WormState.Sleeping, false, false);
            DeltaFormat.Header h = Open(w, out byte[] b);
            mirror.Apply(b, h, Center, 0f, null);

            w.Begin(0, 2);
            w.AddEntry(1, Vector3.zero, 0f, 0f, 0, false, true);          // removed by a packet
            h = Open(w, out b);
            mirror.Apply(b, h, Center, 0.1f, null);
            Assert.Single(host.Released);

            mirror.Tick(10f, 0.016f, 1.2f);                               // worm 2 went silent
            Assert.Equal(2, host.Released.Count);
            mirror.Clear();                                                // the two sleepers
            Assert.Equal(4, host.Released.Count);
            Assert.Equal(0, store.AliveCount);

            Send(mirror, w, 3, DeltaFormat.FlagReset, 11f, WormState.Idle, 7);
            Send(mirror, w, 4, DeltaFormat.FlagReset, 11.1f, WormState.Idle);   // a reset frees what is there
            Assert.Equal(5, host.Released.Count);
        }

        [Theory]
        [InlineData(100, 0, 1)]
        [InlineData(100, 10, 2)]
        [InlineData(94, 10, 1)]
        [InlineData(95, 10, 2)]
        [InlineData(400, 10, 4)]
        [InlineData(0, 10, 1)]
        public void AKeyframeThatCarriesEventsPutsThemInTheFirstPacketAndCountsTheRoomTheyTake(int worms, int events, int chunks)
        {
            Assert.Equal(chunks, DeltaFormat.KeyframeChunks(worms, events));
            var w = new DeltaWriter();
            int at = 0;
            int n = DeltaFormat.KeyframeChunks(worms, events);
            for (int c = 0; c < n; c++)
            {
                w.Begin(DeltaFormat.KeyframeFlags(c, n), (ushort)c);
                if (c == 0)
                    for (int k = 0; k < events; k++) Assert.True(w.AddEvent(DeltaEventType.Hit, Vector3.zero, 1L));
                for (; at < worms && w.HasRoomForEntry; at++) w.AddEntry((ushort)(at + 1), Vector3.zero, 0f, 0f, 1, false, false);
                int len = w.Finish();
                Assert.True(len <= DeltaFormat.MaxPacketBytes);
                Assert.True(DeltaFormat.TryReadHeader(w.Buffer, len, out DeltaFormat.Header h));
                Assert.Equal(c == 0 ? events : 0, h.Events);
            }
            Assert.Equal(worms, at);
        }
    }
}
