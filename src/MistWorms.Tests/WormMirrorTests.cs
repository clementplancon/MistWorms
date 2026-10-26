using System;
using MistWorms.Colony;
using UnityEngine;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>The copy of the free worms on a peer that does not own the colony (phase 8, decision 214): packets in, positions out.</summary>
    public class WormMirrorTests
    {
        private static readonly Vector3 Center = new Vector3(1000f, 50f, -300f);

        private sealed class Fixture
        {
            public readonly WormStore Store = new WormStore(300);
            public readonly WormMirror Mirror;
            public readonly DeltaWriter Writer = new DeltaWriter();
            public ushort Seq;

            public Fixture() { Mirror = new WormMirror(Store); }

            public bool Send(float now, byte flags, IMirrorGround ground, params Entry[] entries)
            {
                Writer.Begin(flags, ++Seq);
                foreach (Entry e in entries) Assert.True(Writer.AddEntry(e.Id, e.Pos - Center, e.Heading, e.Speed, (byte)e.State, e.Lone, e.Removed));
                int len = Writer.Finish();
                var b = new byte[len];
                Array.Copy(Writer.Buffer, b, len);
                Assert.True(DeltaFormat.TryReadHeader(b, len, out DeltaFormat.Header h));
                return Mirror.Apply(b, h, Center, now, ground);
            }

            public bool Resend(float now, ushort seq, byte flags, params Entry[] entries)
            {
                Seq = (ushort)(seq - 1);
                return Send(now, flags, null, entries);
            }

            public int Slot(int id) => Store.FindByNetId(id);
        }

        private struct Entry
        {
            public ushort Id;
            public Vector3 Pos;
            public float Heading;
            public float Speed;
            public WormState State;
            public bool Lone, Removed;

            public static Entry At(int id, Vector3 pos, float heading = 0f, float speed = 0f, WormState state = WormState.Hunting) =>
                new Entry { Id = (ushort)id, Pos = pos, Heading = heading, Speed = speed, State = state };

            public static Entry Gone(int id) => new Entry { Id = (ushort)id, Removed = true };
        }

        private sealed class FlatGround : IMirrorGround
        {
            public int Probes;
            public float Slope;   // dy per metre of x
            public bool TryGround(Vector3 position, out Vector3 point, out Vector3 normal)
            {
                Probes++;
                point = new Vector3(position.x, position.y + 0.02f, position.z);
                normal = new Vector3(-Slope, 1f, 0f).normalized;
                return true;
            }
        }

        private static Vector3 P(float x, float z) => Center + new Vector3(x, 0f, z);

        [Fact]
        public void ANewEntryCreatesTheWormWithItsNetIdWhereThePacketPutsIt()
        {
            var f = new Fixture();
            Assert.True(f.Send(10f, 0, null, Entry.At(77, P(3f, 4f), heading: 1f, speed: 1.2f, state: WormState.Hunting)));
            int s = f.Slot(77);
            Assert.True(s >= 0);
            Assert.True(f.Store.alive[s]);
            Assert.Equal(77, f.Store.netId[s]);
            Assert.Equal((byte)WormState.Hunting, f.Store.state[s]);
            Assert.True((f.Store.pos[s] - P(3f, 4f)).magnitude < 0.01f);
            Assert.True(DeltaFilter.HeadingDelta(f.Store.heading[s], 1f) < 1.5f * Mathf.Deg2Rad);
            Assert.Equal(1, f.Mirror.Created);
        }

        [Fact]
        public void BetweenPacketsAWormCarriesOnAtItsHeadingAndSpeed()
        {
            var f = new Fixture();
            f.Send(0f, 0, null, Entry.At(1, P(0f, 0f), heading: 0f, speed: 2f));
            int s = f.Slot(1);
            Vector3 at1 = f.Mirror.DisplayedAt(s, 1f);
            Assert.True(Mathf.Abs(at1.z - (P(0f, 0f).z + 2f)) < 0.02f && Mathf.Abs(at1.x - P(0f, 0f).x) < 0.02f);

            f.Mirror.Tick(1f, 0.016f, 1.2f);
            Assert.True((f.Store.pos[s] - at1).magnitude < 1e-4f, "the store slot is moved to where the worm is shown");
        }

        [Fact]
        public void TheErrorOfANewPacketIsAbsorbedOverTwoTenthsOfASecond()
        {
            var f = new Fixture();
            f.Send(0f, 0, null, Entry.At(1, P(0f, 0f), heading: 0f, speed: 1f));
            int s = f.Slot(1);
            // At t=1 the worm is shown at z+1; the owner says it is at z+1.5 (it ran faster than predicted).
            Vector3 before = f.Mirror.DisplayedAt(s, 1f);
            f.Send(1f, 0, null, Entry.At(1, P(0f, 1.5f), heading: 0f, speed: 1f));
            Vector3 atReceipt = f.Mirror.DisplayedAt(s, 1f);
            Assert.True((atReceipt - before).magnitude < 0.02f, "no jump at the moment the packet is applied");

            // Half way through the correction the error is half left; after 0.2 s it is gone and the worm is where the packet says it is.
            Vector3 half = f.Mirror.DisplayedAt(s, 1.1f);
            Vector3 truth = P(0f, 1.5f) + new Vector3(0f, 0f, 1f * 0.1f);
            Assert.Equal(0.25f, (half - truth).magnitude, 2);
            Vector3 after = f.Mirror.DisplayedAt(s, 1.2f);
            Vector3 truth2 = P(0f, 1.5f) + new Vector3(0f, 0f, 0.2f);
            Assert.True((after - truth2).magnitude < 0.02f);
        }

        [Fact]
        public void AWormFarFromWherePacketPutsItJumpsInsteadOfSliding()
        {
            var f = new Fixture();
            f.Send(0f, 0, null, Entry.At(1, P(0f, 0f)));
            f.Send(0.5f, 0, null, Entry.At(1, P(20f, 0f)));
            int s = f.Slot(1);
            Assert.True((f.Mirror.DisplayedAt(s, 0.5f) - P(20f, 0f)).magnitude < 0.02f);
        }

        [Fact]
        public void ARemovedEntryFreesTheWormAndAnUnknownOneIsIgnored()
        {
            var f = new Fixture();
            f.Send(0f, 0, null, Entry.At(1, P(0f, 0f)), Entry.At(2, P(1f, 0f)));
            Assert.Equal(2, f.Store.AliveCount);
            f.Send(0.2f, 0, null, Entry.Gone(1), Entry.Gone(999));
            Assert.Equal(1, f.Store.AliveCount);
            Assert.Equal(-1, f.Slot(1));
            Assert.True(f.Slot(2) >= 0);
            Assert.Equal(1, f.Mirror.RemovedByPacket);
        }

        [Fact]
        public void AKeyframeReplacesEverythingAndDropsWhatItDoesNotMention()
        {
            var f = new Fixture();
            f.Send(0f, 0, null, Entry.At(1, P(0f, 0f)), Entry.At(2, P(1f, 0f)), Entry.At(3, P(2f, 0f)));
            Assert.Equal(3, f.Store.AliveCount);
            f.Send(1f, DeltaFormat.FlagReset | DeltaFormat.FlagEnd, null, Entry.At(2, P(5f, 5f)), Entry.At(4, P(6f, 6f)));
            Assert.Equal(2, f.Store.AliveCount);
            Assert.Equal(-1, f.Slot(1));
            Assert.Equal(-1, f.Slot(3));
            Assert.True(f.Slot(2) >= 0 && f.Slot(4) >= 0);
            Assert.False(f.Mirror.KeyframeOpen);
        }

        [Fact]
        public void AKeyframeInSeveralPacketsClearsAtOnceThenOnlyAdds()
        {
            var f = new Fixture();
            f.Send(0f, 0, null, Entry.At(1, P(0f, 0f)), Entry.At(2, P(1f, 0f)), Entry.At(3, P(2f, 0f)));
            f.Send(1f, DeltaFormat.FlagReset, null, Entry.At(1, P(0f, 0f)));
            Assert.Equal(1, f.Store.AliveCount);                 // the first packet cleared everything then added its own worm
            Assert.True(f.Mirror.KeyframeOpen);
            f.Send(1f, DeltaFormat.FlagEnd, null, Entry.At(2, P(1f, 0f)));
            Assert.Equal(2, f.Store.AliveCount);                 // later packets only add
            Assert.Equal(-1, f.Slot(3));
            Assert.False(f.Mirror.KeyframeOpen);
        }

        [Fact]
        public void AKeyframeThatNeverEndsIsClosedAfterItsTimeout()
        {
            var f = new Fixture();
            f.Send(0f, 0, null, Entry.At(1, P(0f, 0f), state: WormState.Sleeping), Entry.At(2, P(1f, 0f), state: WormState.Sleeping));
            f.Send(5f, DeltaFormat.FlagReset, null, Entry.At(1, P(0f, 0f), state: WormState.Sleeping));
            f.Mirror.Tick(5.5f, 0.016f, 1.2f);
            Assert.Equal(1, f.Store.AliveCount);
            f.Mirror.Tick(5f + WormMirror.KeyframeTimeout + 0.1f, 0.016f, 1.2f);
            Assert.Equal(1, f.Store.AliveCount);
            Assert.False(f.Mirror.KeyframeOpen);
        }

        [Fact]
        public void APacketOlderThanTheLastOneIsIgnoredButAResetIsNot()
        {
            var f = new Fixture();
            Assert.True(f.Resend(0f, 10, 0, Entry.At(1, P(0f, 0f))));
            Assert.False(f.Resend(0.1f, 9, 0, Entry.At(1, P(9f, 0f))));        // older
            Assert.False(f.Resend(0.1f, 10, 0, Entry.At(1, P(9f, 0f))));       // same
            Assert.Equal(2, f.Mirror.Ignored);
            Assert.True((f.Store.pos[f.Slot(1)] - P(0f, 0f)).magnitude < 0.02f);
            Assert.True(f.Resend(0.2f, 3, DeltaFormat.FlagReset | DeltaFormat.FlagEnd, Entry.At(1, P(2f, 0f))));   // a new owner restarts its counter
            Assert.True(f.Resend(0.3f, 4, 0, Entry.At(1, P(3f, 0f))));
        }

        [Fact]
        public void AnAwakeWormThatWentSilentIsDroppedAfterThreeSecondsButASleeperStays()
        {
            var f = new Fixture();
            f.Send(0f, 0, null, Entry.At(1, P(0f, 0f), speed: 1f, state: WormState.Hunting), Entry.At(2, P(3f, 0f), state: WormState.Sleeping));
            f.Mirror.Tick(WormMirror.StaleSeconds - 0.1f, 0.016f, 1.2f);
            Assert.Equal(2, f.Store.AliveCount);
            f.Mirror.Tick(WormMirror.StaleSeconds + 0.1f, 0.016f, 1.2f);
            Assert.Equal(1, f.Store.AliveCount);
            Assert.True(f.Slot(2) >= 0);
            Assert.Equal(1, f.Mirror.DroppedStale);
            f.Mirror.Tick(600f, 0.016f, 1.2f);
            Assert.Equal(1, f.Store.AliveCount);                                   // the sleeper is still there
        }

        [Fact]
        public void ADroppedWormIsAlsoToldToTheProxiesThroughTheStore()
        {
            var f = new Fixture();
            f.Send(0f, 0, null, Entry.At(1, P(0f, 0f)));
            int s = f.Slot(1);
            ushort gen = f.Store.generation[s];
            Assert.True(f.Store.IsAlive(s, gen));
            f.Send(0.2f, 0, null, Entry.Gone(1));
            Assert.False(f.Store.IsAlive(s, gen), "a hit proxy attached to it sees it die");
        }

        [Fact]
        public void TheStateClockRestartsOnAChangeAndRunsOnOtherwise()
        {
            var f = new Fixture();
            f.Send(0f, 0, null, Entry.At(1, P(0f, 0f), state: WormState.Hunting));
            int s = f.Slot(1);
            f.Send(1f, 0, null, Entry.At(1, P(0f, 0f), state: WormState.Hunting));
            Assert.Equal(1f, f.Store.stateTime[s], 3);                              // same state: the time it lasted
            f.Send(1.5f, 0, null, Entry.At(1, P(0f, 0f), state: WormState.Striking));
            Assert.Equal(0f, f.Store.stateTime[s], 3);                              // a new state starts at 0
            Assert.Equal((byte)WormState.Striking, f.Store.state[s]);
            Assert.Equal(1.5f, f.Store.lastSimTime[s], 3);
            Assert.True(f.Store.simInterval[s] >= 5f, "CollectInstances adds min(elapsed, simInterval) to stateTime");
        }

        [Fact]
        public void CrawlPhaseAdvancesWithDistanceAndOnlyWhileMoving()
        {
            var f = new Fixture();
            f.Send(0f, 0, null, Entry.At(1, P(0f, 0f), speed: 1.2f), Entry.At(2, P(3f, 0f), speed: 0f, state: WormState.Idle));
            int moving = f.Slot(1), still = f.Slot(2);
            float a0 = f.Store.animTime[moving];
            f.Mirror.Tick(0.5f, 0.5f, 1.2f);
            Assert.Equal(a0 + 1.2f * 0.5f / 1.2f, f.Store.animTime[moving], 3);     // distance / Worm.MoveSpeed, as on the owner
            Assert.Equal(0f, f.Store.animTime[still], 5);
        }

        [Fact]
        public void LoneBitAndGroundNormalComeFromThePacketAndTheProbe()
        {
            var f = new Fixture();
            var ground = new FlatGround { Slope = 0.5f };
            f.Writer.Begin(0, 1);
            f.Writer.AddEntry(5, P(0f, 0f) - Center, Mathf.PI / 2f, 1f, (byte)WormState.Idle, true, false);   // heading +x, up the slope
            int len = f.Writer.Finish();
            var b = new byte[len];
            Array.Copy(f.Writer.Buffer, b, len);
            DeltaFormat.TryReadHeader(b, len, out DeltaFormat.Header h);
            f.Mirror.Apply(b, h, Center, 0f, ground);
            int s = f.Slot(5);
            Assert.True(f.Store.lone[s]);
            Assert.Equal(1, ground.Probes);
            Assert.True(f.Store.groundNormal[s].x < -0.3f);
            // Up the slope of 0.5: after 1 s at 1 m/s the worm has climbed about half a metre.
            float climbed = f.Mirror.DisplayedAt(s, 1f).y - f.Mirror.DisplayedAt(s, 0f).y;
            Assert.InRange(climbed, 0.35f, 0.55f);
        }

        [Fact]
        public void ClearForgetsEveryWormAndTheSequence()
        {
            var f = new Fixture();
            f.Send(0f, 0, null, Entry.At(1, P(0f, 0f)), Entry.At(2, P(1f, 0f)));
            f.Mirror.Clear();
            Assert.Equal(0, f.Store.AliveCount);
            Assert.Equal(-1, f.Slot(1));
            Assert.True(f.Resend(1f, 1, 0, Entry.At(3, P(0f, 0f))), "the sequence starts over");
        }

        [Fact]
        public void ACompactEntryMovesAKnownWormFromItsLastSentPositionAndIsDroppedForAnUnknownOne()
        {
            var f = new Fixture();
            f.Send(0f, 0, null, Entry.At(1, P(5f, 5f), 0f, 1f));
            var ground = new FlatGround();
            // A delta of (+0.5, 0, +1.0) from the sent position, applied with a floor probe that lifts the worm by 2 cm: the next delta is still relative to the sent one.
            for (int k = 1; k <= 2; k++)
            {
                f.Writer.Begin(0, ++f.Seq);
                f.Writer.AddDelta(1, 25, 0, 50, 0f, 1f);
                f.Writer.AddDelta(99, 1, 1, 1, 0f, 1f);
                int len = f.Writer.Finish();
                var b = new byte[len];
                Array.Copy(f.Writer.Buffer, b, len);
                Assert.True(DeltaFormat.TryReadHeader(b, len, out DeltaFormat.Header h));
                Assert.True(f.Mirror.Apply(b, h, Center, k * 0.2f, ground));
                Assert.Equal(k, f.Mirror.UnknownDeltas);
                Assert.Equal(WormState.Hunting, (WormState)f.Store.state[f.Slot(1)]);
                Vector3 a = f.Mirror.Anchor(f.Slot(1));
                Assert.Equal(5f + 0.5f * k, a.x - Center.x, 3);
                Assert.Equal(5f + 1.0f * k, a.z - Center.z, 3);
                Assert.Equal(0.02f, a.y - Center.y, 3);
            }
            Assert.Equal(-1, f.Slot(99));
        }

        [Fact]
        public void AStoreThatIsFullRefusesTheEntryWithoutBreaking()
        {
            var store = new WormStore(2);
            var mirror = new WormMirror(store);
            var w = new DeltaWriter();
            w.Begin(0, 1);
            for (int i = 1; i <= 3; i++) w.AddEntry((ushort)i, Vector3.zero, 0f, 0f, 1, false, false);
            int len = w.Finish();
            var b = new byte[len];
            Array.Copy(w.Buffer, b, len);
            DeltaFormat.TryReadHeader(b, len, out DeltaFormat.Header h);
            Assert.True(mirror.Apply(b, h, Center, 0f, null));
            Assert.Equal(2, store.AliveCount);
            Assert.Equal(1, mirror.Refused);
        }

        [Fact]
        public void OwnerToMirrorPipelineStaysWithinAQuarterMetreForTwentySeconds()
        {
            // An owner store with 50 wandering and chasing worms, filtered and sent at 5 Hz through the real codec, read by a mirror at 60 fps.
            var rng = new System.Random(11);
            var owner = new WormStore(100);
            var sent = new SentState[100];
            var vel = new Vector3[100];
            for (int i = 0; i < 50; i++)
            {
                int s = owner.Alloc(P((float)rng.NextDouble() * 40f, (float)rng.NextDouble() * 40f), (float)(rng.NextDouble() * 6.28), WormState.Hunting, 10f, 0f);
                owner.lastSpeed[s] = 0.6f + (float)rng.NextDouble();
            }
            var f = new Fixture();
            var w = new DeltaWriter();
            ushort seq = 0;
            long bytes = 0;
            double worst = 0, sum = 0;
            int samples = 0;
            float dt = 1f / 60f;
            float nextSend = 0f;
            bool first = true;
            for (int frame = 0; frame < 20 * 60; frame++)
            {
                float now = frame * dt;
                // owner: 20 Hz simulation, a worm turns now and then
                if (frame % 3 == 0)
                    for (int i = 0; i < owner.HighWater; i++)
                    {
                        if (rng.NextDouble() < 0.02) owner.heading[i] += (float)(rng.NextDouble() - 0.5) * 1.5f;
                        float h = owner.heading[i];
                        owner.pos[i] += new Vector3(Mathf.Sin(h), 0f, Mathf.Cos(h)) * owner.lastSpeed[i] * (3 * dt);
                    }
                if (now >= nextSend)
                {
                    nextSend = now + 0.2f;
                    if (first)
                    {
                        // keyframe
                        w.Begin(DeltaFormat.FlagReset | DeltaFormat.FlagEnd, ++seq);
                        for (int i = 0; i < owner.HighWater; i++)
                        {
                            w.AddEntry(owner.netId[i], owner.pos[i] - Center, owner.heading[i], owner.lastSpeed[i], owner.state[i], false, false);
                            DeltaFilter.Record(ref sent[i], owner.netId[i], owner.pos[i], owner.heading[i], owner.lastSpeed[i], owner.state[i], now);
                        }
                        first = false;
                    }
                    else
                    {
                        w.Begin(0, ++seq);
                        for (int i = 0; i < owner.HighWater; i++)
                        {
                            if (!DeltaFilter.NeedsSend(sent[i], owner.netId[i], owner.pos[i], owner.heading[i], owner.state[i], now)) continue;
                            w.AddEntry(owner.netId[i], owner.pos[i] - Center, owner.heading[i], owner.lastSpeed[i], owner.state[i], false, false);
                            DeltaFilter.Record(ref sent[i], owner.netId[i], owner.pos[i], owner.heading[i], owner.lastSpeed[i], owner.state[i], now);
                        }
                    }
                    int len = w.Finish();
                    bytes += len;
                    var b = new byte[len];
                    Array.Copy(w.Buffer, b, len);
                    Assert.True(DeltaFormat.TryReadHeader(b, len, out DeltaFormat.Header hd));
                    f.Mirror.Apply(b, hd, Center, now, null);
                }
                f.Mirror.Tick(now, dt, 1.2f);
                if (now > 2f)
                    for (int i = 0; i < owner.HighWater; i++)
                    {
                        int slot = f.Slot(owner.netId[i]);
                        Assert.True(slot >= 0, "every worm of the owner is in the mirror");
                        double err = (f.Store.pos[slot] - owner.pos[i]).magnitude;
                        worst = Math.Max(worst, err);
                        sum += err;
                        samples++;
                    }
            }
            Assert.Equal(owner.AliveCount, f.Store.AliveCount);
            // Mean error well under the filter threshold plus the latency of a 5 Hz send; the worst case is bounded by the tolerance and one send interval of drift.
            Assert.True(sum / samples < 0.25, $"mean error {sum / samples:0.000} m");
            Assert.True(worst < 1.0, $"worst error {worst:0.000} m");
            // 50 worms turning now and then: far below the 6 KB/s budget for one recipient.
            Assert.True(bytes / 20.0 < 6000.0, $"{bytes / 20.0:0} bytes/s");
        }
    }
}
