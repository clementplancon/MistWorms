using MistWorms.Colony;
using UnityEngine;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>Which worms the owner sends (phase 8, decision 213): prediction, thresholds, keep alive, far LOD.</summary>
    public class DeltaFilterTests
    {
        private const byte Hunt = (byte)WormState.Hunting;
        private const byte Sleep = (byte)WormState.Sleeping;

        private static SentState Sent(Vector3 pos, float heading, float speed, byte visual, float time, ushort id = 1)
        {
            var s = new SentState();
            DeltaFilter.Record(ref s, id, pos, heading, speed, visual, time);
            return s;
        }

        [Fact]
        public void PredictionCarriesThePositionOnAtHeadingAndSpeed()
        {
            // Heading 0 is +z, heading pi/2 is +x.
            SentState s = Sent(new Vector3(1f, 2f, 3f), 0f, 1.5f, Hunt, 10f);
            Vector3 p = DeltaFilter.Predict(s, 12f);
            Assert.Equal(1f, p.x, 3);
            Assert.Equal(2f, p.y, 3);
            Assert.Equal(6f, p.z, 2);

            s = Sent(Vector3.zero, Mathf.PI / 2f, 2f, Hunt, 0f);
            p = DeltaFilter.Predict(s, 0.5f);
            Assert.True(Mathf.Abs(p.x - 1f) < 0.02f && Mathf.Abs(p.z) < 0.02f);
        }

        [Fact]
        public void ANewOrChangedWormIsAlwaysSent()
        {
            var empty = new SentState();
            Assert.True(DeltaFilter.NeedsSend(empty, 1, Vector3.zero, 0f, Hunt, 0f));

            SentState s = Sent(Vector3.zero, 0f, 0f, Hunt, 0f, id: 5);
            Assert.True(DeltaFilter.NeedsSend(s, 6, Vector3.zero, 0f, Hunt, 0.1f), "the slot was reused by another worm");
            Assert.True(DeltaFilter.NeedsSend(s, 5, Vector3.zero, 0f, (byte)WormState.Striking, 0.1f), "its state changed");
            Assert.True(DeltaFilter.NeedsSend(s, 5, Vector3.zero, 0f, DeltaFormat.VisualCode(Hunt, true, false), 0.1f), "the lone bit is part of the visual state");
        }

        [Fact]
        public void AWormThatMovesAsPredictedIsNotSent()
        {
            SentState s = Sent(Vector3.zero, 0f, 1.2f, Hunt, 0f);
            for (float t = 0.2f; t < 1.9f; t += 0.2f)
            {
                Vector3 actual = new Vector3(0.1f, 0f, 1.2f * t);   // 10 cm to the side: within the tolerance
                Assert.False(DeltaFilter.NeedsSend(s, 1, actual, 0f, Hunt, t), $"t={t}");
            }
        }

        [Fact]
        public void PositionErrorIsTestedAgainstTheThirtyFiveCentimetreThreshold()
        {
            SentState s = Sent(Vector3.zero, 0f, 1f, Hunt, 0f);
            // After 1 s the prediction is (0, 0, 1).
            Assert.False(DeltaFilter.NeedsSend(s, 1, new Vector3(0.34f, 0f, 1f), 0f, Hunt, 1f));
            Assert.True(DeltaFilter.NeedsSend(s, 1, new Vector3(0.36f, 0f, 1f), 0f, Hunt, 1f));
            Assert.True(DeltaFilter.NeedsSend(s, 1, new Vector3(0f, 0f, 1.36f), 0f, Hunt, 1f), "ahead of the prediction counts too");
            Assert.True(DeltaFilter.NeedsSend(s, 1, new Vector3(0f, 0f, 0.64f), 0f, Hunt, 1f), "a worm that stopped counts too");
        }

        [Fact]
        public void VerticalErrorHasItsOwnThreshold()
        {
            SentState s = Sent(Vector3.zero, 0f, 0f, Hunt, 0f);
            Assert.False(DeltaFilter.NeedsSend(s, 1, new Vector3(0f, 0.39f, 0f), 0f, Hunt, 0.5f));
            Assert.True(DeltaFilter.NeedsSend(s, 1, new Vector3(0f, 0.41f, 0f), 0f, Hunt, 0.5f));
            Assert.True(DeltaFilter.NeedsSend(s, 1, new Vector3(0f, -0.41f, 0f), 0f, Hunt, 0.5f));
        }

        [Fact]
        public void HeadingTurnOfMoreThanThirtyDegreesIsSent()
        {
            SentState s = Sent(Vector3.zero, 0f, 1f, Hunt, 0f);
            Assert.False(DeltaFilter.NeedsSend(s, 1, Vector3.zero, 29f * Mathf.Deg2Rad, Hunt, 0.2f));
            Assert.True(DeltaFilter.NeedsSend(s, 1, Vector3.zero, 31f * Mathf.Deg2Rad, Hunt, 0.2f));
            Assert.True(DeltaFilter.NeedsSend(s, 1, Vector3.zero, -31f * Mathf.Deg2Rad, Hunt, 0.2f));
            // across the wrap: 350 degrees is 10 degrees from 0, not 350
            Assert.False(DeltaFilter.NeedsSend(s, 1, Vector3.zero, 350f * Mathf.Deg2Rad, Hunt, 0.2f));
        }

        [Fact]
        public void ASlowWormMayTurnTwiceAsFarBeforeItIsSent()
        {
            SentState slow = Sent(Vector3.zero, 0f, 0.2f, Hunt, 0f);
            Assert.False(DeltaFilter.NeedsSend(slow, 1, Vector3.zero, 55f * Mathf.Deg2Rad, Hunt, 0.2f));
            Assert.True(DeltaFilter.NeedsSend(slow, 1, Vector3.zero, 65f * Mathf.Deg2Rad, Hunt, 0.2f));
            SentState fast = Sent(Vector3.zero, 0f, 1.5f, Hunt, 0f);
            Assert.True(DeltaFilter.NeedsSend(fast, 1, Vector3.zero, 35f * Mathf.Deg2Rad, Hunt, 0.2f));
        }

        [Fact]
        public void HeadingDeltaIsTheShortWayRound()
        {
            Assert.Equal(0.2f, DeltaFilter.HeadingDelta(0.1f, -0.1f), 4);
            Assert.Equal(0.2f, DeltaFilter.HeadingDelta(Mathf.PI - 0.1f, -Mathf.PI + 0.1f), 3);
            Assert.Equal(0f, DeltaFilter.HeadingDelta(0.5f, 0.5f + 2f * Mathf.PI), 3);
        }

        [Fact]
        public void AnAwakeWormIsSentAtLeastEveryTwoSeconds()
        {
            SentState s = Sent(Vector3.zero, 0f, 0f, Hunt, 0f);
            Assert.False(DeltaFilter.NeedsSend(s, 1, Vector3.zero, 0f, Hunt, 1.99f));
            Assert.True(DeltaFilter.NeedsSend(s, 1, Vector3.zero, 0f, Hunt, 2f));
        }

        [Fact]
        public void ASleepingWormIsOnlySentWhenItsStateChanges()
        {
            SentState s = Sent(Vector3.zero, 0f, 0f, Sleep, 0f);
            Assert.False(DeltaFilter.NeedsSend(s, 1, Vector3.zero, 0f, Sleep, 600f), "no keep alive for a sleeper");
            Assert.False(DeltaFilter.NeedsSend(s, 1, new Vector3(3f, 0f, 0f), 2f, Sleep, 600f), "nor a position or heading threshold");
            Assert.True(DeltaFilter.NeedsSend(s, 1, Vector3.zero, 0f, (byte)WormState.Idle, 600f));
        }

        [Fact]
        public void RecordKeepsWhatThePeerDecodedNotWhatTheOwnerHad()
        {
            var s = new SentState();
            DeltaFilter.Record(ref s, 3, Vector3.one, 0.5f, 1.234f, Hunt, 4f);
            Assert.True(s.Valid);
            Assert.Equal(3, s.NetId);
            Assert.Equal(1.25f, s.Speed, 4);                                                        // 25 steps of 0.05
            Assert.Equal(SnapshotFormat.HeadingFromCode(SnapshotFormat.HeadingCode(0.5f)), s.Heading, 5);
            Assert.Equal(4f, s.Time, 5);
        }

        [Fact]
        public void ACompactEntryNeedsAKnownWormWithTheSameStateAndABaseWithinReach()
        {
            SentState s = Sent(Vector3.zero, 0f, 1f, Hunt, 0f, id: 4);
            Assert.True(DeltaFilter.CanDelta(s, 4, new Vector3(2.4f, -2.4f, 2.4f), Hunt));
            Assert.False(DeltaFilter.CanDelta(s, 4, new Vector3(2.6f, 0f, 0f), Hunt), "too far from the base");
            Assert.False(DeltaFilter.CanDelta(s, 4, new Vector3(0f, 0f, -2.6f), Hunt));
            Assert.False(DeltaFilter.CanDelta(s, 5, Vector3.zero, Hunt), "another worm in the slot");
            Assert.False(DeltaFilter.CanDelta(s, 4, Vector3.zero, (byte)WormState.Striking), "state changed");
            Assert.False(DeltaFilter.CanDelta(new SentState(), 4, Vector3.zero, Hunt), "never sent");
            s.NeedFull = true;
            Assert.False(DeltaFilter.CanDelta(s, 4, Vector3.zero, Hunt), "a peer got it by a keyframe the others did not");
            DeltaFilter.Record(ref s, 4, Vector3.zero, 0f, 1f, Hunt, 1f);
            Assert.False(s.NeedFull, "a full entry clears it");
            Assert.True(DeltaFilter.CanDelta(s, 4, Vector3.zero, Hunt));
        }

        [Fact]
        public void RecordedDeltasKeepTheBaseOfTheOwnerEqualToTheBaseOfThePeer()
        {
            // The owner's base moves by the quantised moves only: after a long chain it is still where the peer, adding the same moves, has it.
            var rng = new System.Random(3);
            Vector3 peer = new Vector3(10.37f, 2f, -4.11f);
            SentState s = Sent(peer, 0f, 1f, Hunt, 0f);
            Vector3 actual = peer;
            for (int k = 1; k <= 500; k++)
            {
                actual += new Vector3((float)(rng.NextDouble() - 0.5) * 0.8f, (float)(rng.NextDouble() - 0.5) * 0.1f, (float)(rng.NextDouble() - 0.5) * 0.8f);
                Assert.True(DeltaFilter.CanDelta(s, 1, actual, Hunt));
                Vector3 d = actual - s.Pos;
                sbyte cx = DeltaFormat.DeltaCode(d.x), cy = DeltaFormat.DeltaCode(d.y), cz = DeltaFormat.DeltaCode(d.z);
                DeltaFilter.RecordDelta(ref s, cx, cy, cz, 0f, 1f, k * 0.2f);
                peer += DeltaFormat.DeltaVector(cx, cy, cz);
                Assert.Equal(peer.x, s.Pos.x);
                Assert.Equal(peer.y, s.Pos.y);
                Assert.Equal(peer.z, s.Pos.z);
                Assert.True((actual - s.Pos).magnitude <= 0.02f, "a delta lands within a centimetre per axis of the worm");
            }
        }

        [Fact]
        public void SendSpeedIsTheDistanceReallyMadeSinceTheLastPacket()
        {
            SentState s = Sent(Vector3.zero, 0f, 2.4f, Hunt, 10f);
            // It made 0.3 m in 0.2 s although its last step ran at 2.4 m/s (crowded): the peers are told 1.5.
            Assert.Equal(1.5f, DeltaFilter.SendSpeed(s, 1, new Vector3(0.3f, 5f, 0f), 2.4f, 10.2f), 3);
            Assert.Equal(2.4f, DeltaFilter.SendSpeed(s, 1, new Vector3(0.3f, 0f, 0f), 2.4f, 10.1f), 3);      // too soon to average
            Assert.Equal(2.4f, DeltaFilter.SendSpeed(new SentState(), 1, new Vector3(5f, 0f, 0f), 2.4f, 10.2f), 3);   // nothing sent before
            Assert.Equal(2.4f, DeltaFilter.SendSpeed(s, 2, new Vector3(5f, 0f, 0f), 2.4f, 10.2f), 3);      // another worm
            Assert.Equal(0f, DeltaFilter.SendSpeed(s, 1, new Vector3(0.3f, 0f, 0f), 0f, 10.2f), 3);         // stopped now: told at once
        }

        [Fact]
        public void FarWormsAreLookedAtTwiceASecondAndNearOnesEveryTick()
        {
            Assert.Equal(0f, DeltaFilter.NextEvaluation(10f, 29f * 29f), 5);
            Assert.Equal(0f, DeltaFilter.NextEvaluation(10f, 30f * 30f), 5);
            Assert.Equal(10.5f, DeltaFilter.NextEvaluation(10f, 31f * 31f), 5);
            Assert.Equal(10.5f, DeltaFilter.NextEvaluation(10f, float.MaxValue), 5);
        }

        [Fact]
        public void AWormWalkingStraightCostsOnePacketEntryPerKeepAliveAndATurningOneMore()
        {
            // Straight line at 1.2 m/s, evaluated at 5 Hz for 10 s: the keep alive is the only reason to send.
            SentState s = Sent(Vector3.zero, 0f, 1.2f, Hunt, 0f);
            int sends = 0;
            for (int k = 1; k <= 50; k++)
            {
                float t = k * 0.2f;
                var actual = new Vector3(0f, 0f, 1.2f * t);
                if (DeltaFilter.NeedsSend(s, 1, actual, 0f, Hunt, t))
                {
                    sends++;
                    DeltaFilter.Record(ref s, 1, actual, 0f, 1.2f, Hunt, t);
                }
            }
            Assert.Equal(5, sends);   // 2.0 s, 4.0 s ... 10.0 s
        }
    }
}
