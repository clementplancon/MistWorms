using System.Collections.Generic;
using MistWorms.Colony;
using MistWorms.Drops;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>Corrections of pass 6-2 (decision 163): reach filter, blacklist, progress watch, nearest-first scan buffer, validity of the worm credit, partial loot capture.</summary>
    public class TargetReachTests
    {
        // ------------------------------------------------------------------ vertical reach

        [Fact]
        public void WormReachIsEyePlusCobraRange()
        {
            Assert.Equal(1.35f, TargetReach.WormVerticalReach(0.15f, 1.2f), 4);
        }

        [Fact]
        public void ABeamThreeMetresAboveAWormIsOutOfReach()
        {
            float reach = TargetReach.WormVerticalReach(0.15f, 1.2f);
            Assert.False(TargetReach.Reachable(3f, 3f, 0f, reach));      // closest point of the beam
            Assert.True(TargetReach.Reachable(1.3f, 1.3f, 0f, reach));
            Assert.False(TargetReach.Reachable(-3f, -3f, 0f, reach));    // a pit below is as unreachable as a ledge above
        }

        [Fact]
        public void ACharacterIsReachableWhenItsColliderSpanTouchesTheReach()
        {
            float reach = TargetReach.WormVerticalReach(0.15f, 1.2f);
            Assert.True(TargetReach.Reachable(0f, 2.5f, 0f, reach));         // a lox standing on the floor
            Assert.Equal(0f, TargetReach.VerticalGap(-0.5f, 1f, 0f));         // the ground inside the span
            Assert.False(TargetReach.Reachable(3f, 4f, 0f, reach));          // a bat 3 m up
            Assert.Equal(2f, TargetReach.VerticalGap(3f, 4f, 1f), 4);
            Assert.Equal(1f, TargetReach.VerticalGap(-1f, 0f, 2f) - 1f, 4);   // below: ground - high
            Assert.Equal(TargetReach.VerticalGap(3f, 1f, 0f), TargetReach.VerticalGap(1f, 3f, 0f)); // swapped bounds
        }

        [Fact]
        public void AMassReachesTwoAndAHalfMetres()
        {
            Assert.True(TargetReach.Reachable(2.4f, 2.4f, 0f, Mass.EngulfMath.MaxHeightGap));
            Assert.False(TargetReach.Reachable(2.6f, 2.6f, 0f, Mass.EngulfMath.MaxHeightGap));
        }

        // ------------------------------------------------------------------ blacklist

        [Fact]
        public void ABlacklistedTargetIsSkippedForTenSecondsForThatOwnerOnly()
        {
            var b = new TargetBlacklist(3);
            long key = TargetReach.Key(5, 9);
            Assert.False(b.Contains(1, key, 0f));
            b.Add(1, key, 100f);
            Assert.True(b.Contains(1, key, 100f));
            Assert.True(b.Contains(1, key, 109.9f));
            Assert.False(b.Contains(1, key, 110f));      // over
            Assert.False(b.Contains(0, key, 101f));      // another worm still may hunt it
            Assert.False(b.Contains(1, TargetReach.Key(5, 10), 101f));
        }

        [Fact]
        public void AFullBlacklistReplacesTheEntryThatEndsFirstAndClearForgetsAll()
        {
            var b = new TargetBlacklist(1);
            for (uint k = 1; k <= TargetBlacklist.PerOwner; k++) b.Add(0, TargetReach.Key(1, k), 100f + k);
            b.Add(0, TargetReach.Key(1, 99), 110f);      // all four are live: the first one (ends at 101) goes
            Assert.False(b.Contains(0, TargetReach.Key(1, 1), 102f - 2f));
            Assert.True(b.Contains(0, TargetReach.Key(1, 2), 103f));
            Assert.True(b.Contains(0, TargetReach.Key(1, 99), 103f));
            b.Clear(0);
            Assert.False(b.Contains(0, TargetReach.Key(1, 99), 103f));
            b.Add(0, 0L, 100f);                          // key 0 is never stored
            Assert.False(b.Contains(0, 0L, 101f));
            b.Add(7, TargetReach.Key(1, 1), 100f);       // unknown owner: ignored
            Assert.False(b.Contains(7, TargetReach.Key(1, 1), 101f));
        }

        [Fact]
        public void ReAddingATargetExtendsItsTime()
        {
            var b = new TargetBlacklist(1);
            long key = TargetReach.Key(1, 1);
            b.Add(0, key, 100f);
            b.Add(0, key, 105f);
            Assert.True(b.Contains(0, key, 114f));
            Assert.False(b.Contains(0, key, 115f));
        }

        // ------------------------------------------------------------------ progress watch

        [Fact]
        public void AStalledDistanceIsReportedAfterFourSeconds()
        {
            var w = new ProgressWatch();
            long key = TargetReach.Key(1, 1);
            Assert.False(w.Update(key, 3f, 0f, false));
            for (float t = 0.5f; t < 4f; t += 0.5f) Assert.False(w.Update(key, 3f, t, false));
            Assert.True(w.Update(key, 3f, 4f, false));
            Assert.False(w.Update(key, 3f, 4.5f, false));      // restarted
        }

        [Fact]
        public void ProgressOfHalfAMetreRestartsTheClock()
        {
            var w = new ProgressWatch();
            long key = TargetReach.Key(1, 1);
            w.Update(key, 10f, 0f, false);
            Assert.False(w.Update(key, 9.4f, 1f, false));      // 0.6 m closer: the clock restarts at 1
            Assert.False(w.Update(key, 9.4f, 2f, false));
            Assert.False(w.Update(key, 9.4f, 3f, false));
            Assert.False(w.Update(key, 9.4f, 4f, false));
            Assert.True(w.Update(key, 9.35f, 5f, false));      // 0.05 m is not progress: 4 s since 1
        }

        [Fact]
        public void ExemptionsAChangeOfTargetAndAGapRestartTheWatch()
        {
            var w = new ProgressWatch();
            long a = TargetReach.Key(1, 1), b = TargetReach.Key(1, 2);
            w.Update(a, 3f, 0f, false);
            Assert.False(w.Update(a, 3f, 3f, true));           // striking / engulfing: exempt
            Assert.False(w.Update(a, 3f, 6f, false));          // restarted at 6
            Assert.False(w.Update(b, 3f, 9f, false));          // another target
            Assert.False(w.Update(b, 3f, 10.9f, false));
            Assert.False(w.Update(b, 3f, 20f, false));         // not fed for 9 s: restarted, not reported
            Assert.False(w.Update(0L, 3f, 30f, false));        // no target
        }

        // ------------------------------------------------------------------ the 48 nearest pieces

        private static void Offer(float[] dist, int[] item, ref int count, int capacity, float d, int id)
        {
            int pos = TargetReach.NearestInsertPos(dist, 0, count, capacity, d);
            if (pos < 0) return;
            if (count >= capacity) count--;
            for (int k = count; k > pos; k--) { dist[k] = dist[k - 1]; item[k] = item[k - 1]; }
            dist[pos] = d;
            item[pos] = id;
            count++;
        }

        [Fact]
        public void TheScanKeepsTheNearestPiecesWhateverTheOrderFound()
        {
            const int capacity = 48;
            var dist = new float[capacity];
            var item = new int[capacity];
            int count = 0;
            // 100 pieces found farthest first: the distance of piece n is n.
            for (int n = 100; n >= 1; n--) Offer(dist, item, ref count, capacity, n, n);
            Assert.Equal(capacity, count);
            for (int k = 0; k < capacity; k++)
            {
                Assert.Equal(k + 1, item[k]);
                Assert.Equal(k + 1f, dist[k]);
            }
        }

        [Fact]
        public void AFarPieceNeverPushesOutANearOneAndTiesDoNotOverflow()
        {
            var dist = new float[3];
            var item = new int[3];
            int count = 0;
            Offer(dist, item, ref count, 3, 1f, 1);
            Offer(dist, item, ref count, 3, 2f, 2);
            Offer(dist, item, ref count, 3, 3f, 3);
            Assert.Equal(-1, TargetReach.NearestInsertPos(dist, 0, count, 3, 3f));    // not nearer than the last
            Assert.Equal(-1, TargetReach.NearestInsertPos(dist, 0, count, 3, 9f));
            Assert.Equal(1, TargetReach.NearestInsertPos(dist, 0, count, 3, 1.5f));
            Offer(dist, item, ref count, 3, 0.5f, 4);
            Assert.Equal(new[] { 4, 1, 2 }, item);                                    // the farthest fell off
            Assert.Equal(-1, TargetReach.NearestInsertPos(dist, 0, 0, 0, 1f));
        }

        // ------------------------------------------------------------------ validity of the worm credit

        [Fact]
        public void OnlyAMarkedCobraHitWithoutAttackerCreditsTheRegistry()
        {
            Assert.True(CaptureMath.CreditsRegistry(false, true, true));
            Assert.False(CaptureMath.CreditsRegistry(false, false, true));     // burn, poison, fall, drowning after a cobra hit
            Assert.False(CaptureMath.CreditsRegistry(true, true, true));       // another attacker
            Assert.False(CaptureMath.CreditsRegistry(false, true, false));     // nothing registered
        }

        [Fact]
        public void TheCobraMarkIsNotTheVanillaDefault()
        {
            Assert.True(CobraMark.Is(CobraMark.Value));
            Assert.False(CobraMark.Is(1f));      // HitData.c_SkillRaiseAmountDefault
            Assert.False(CobraMark.Is(0f));
        }

        // ------------------------------------------------------------------ partial capture of a drop list

        [Fact]
        public void OnlyTheCapturedEntriesLeaveTheList()
        {
            // "Blob" x2 (a creature) stays, the items go; the order of what stays is kept.
            var list = new List<string> { "Resin", "Blob", "Coins", "Blob", "Ooze" };
            int removed = CaptureMath.RemoveWhere(list, e => e != "Blob");
            Assert.Equal(3, removed);
            Assert.Equal(new[] { "Blob", "Blob" }, list);
            Assert.Equal(0, CaptureMath.RemoveWhere(list, e => e == "Nothing"));
            Assert.Equal(2, CaptureMath.RemoveWhere(list, e => true));
            Assert.Empty(list);
            Assert.Equal(0, CaptureMath.RemoveWhere<int>(null, e => true));
        }
    }
}
