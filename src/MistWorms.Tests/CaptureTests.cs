using MistWorms.Drops;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>Pass 6-2: the "last worm hit" registry (spec §7.4, plan 6, A2) and the bookkeeping of the captured loot (A13).</summary>
    public class CaptureTests
    {
        private static ZDOID Id(long user, uint id) => new ZDOID(user, id);

        // ------------------------------------------------------------------ registry

        [Fact]
        public void AStrikeIsRememberedForTwoSeconds()
        {
            var r = new LastWormHitRegistry();
            ZDOID victim = Id(1, 10), colony = Id(2, 20);
            r.Record(victim, colony, 7, 3, 100f);

            Assert.True(r.TryGet(victim, 100f, out ZDOID c, out int slot, out int gen));
            Assert.Equal(colony, c);
            Assert.Equal(7, slot);
            Assert.Equal(3, gen);
            Assert.True(r.TryGet(victim, 101.9f, out _, out _, out _));
            Assert.True(r.TryGet(victim, 102f, out _, out _, out _));    // exactly the window
            Assert.False(r.TryGet(victim, 102.01f, out _, out _, out _));
        }

        [Fact]
        public void AnExpiredEntryStaysGoneEvenIfTheClockWentBack()
        {
            var r = new LastWormHitRegistry();
            ZDOID victim = Id(1, 10);
            r.Record(victim, Id(2, 20), 0, 1, 10f);
            Assert.False(r.TryGet(victim, 20f, out _, out _, out _));    // expired and dropped
            Assert.False(r.TryGet(victim, 10.5f, out _, out _, out _));
            Assert.Equal(0, r.Count);
        }

        [Fact]
        public void ANewStrikeRenewsTheEntryAndReplacesTheWorm()
        {
            var r = new LastWormHitRegistry();
            ZDOID victim = Id(1, 10);
            r.Record(victim, Id(2, 20), 1, 1, 0f);
            r.Record(victim, Id(2, 20), 5, 9, 1.5f);                      // another worm struck it later
            Assert.Equal(1, r.Count);
            Assert.True(r.TryGet(victim, 3.0f, out _, out int slot, out int gen));
            Assert.Equal(5, slot);
            Assert.Equal(9, gen);
            Assert.False(r.TryGet(victim, 3.6f, out _, out _, out _));
        }

        [Fact]
        public void EntriesAreIndependentAndRemovable()
        {
            var r = new LastWormHitRegistry();
            r.Record(Id(1, 1), Id(2, 20), 1, 1, 0f);
            r.Record(Id(1, 2), Id(2, 20), 2, 1, 0f);
            r.Remove(Id(1, 1));
            Assert.False(r.TryGet(Id(1, 1), 0.5f, out _, out _, out _));
            Assert.True(r.TryGet(Id(1, 2), 0.5f, out _, out int slot, out _));
            Assert.Equal(2, slot);
            Assert.False(r.TryGet(ZDOID.None, 0.5f, out _, out _, out _));
            r.Record(ZDOID.None, Id(2, 20), 3, 1, 0f);                    // a victim without id is ignored
            Assert.Equal(1, r.Count);
        }

        [Fact]
        public void AFullRegistryExpiresThenReplacesTheOldest()
        {
            var r = new LastWormHitRegistry();
            for (uint k = 1; k <= LastWormHitRegistry.Capacity; k++) r.Record(Id(1, k), Id(2, 20), (int)k, 1, k * 0.01f);
            Assert.Equal(LastWormHitRegistry.Capacity, r.Count);

            // Everything alive: the oldest (the first) is replaced.
            r.Record(Id(1, 1000), Id(2, 20), 99, 1, 0.9f);
            Assert.Equal(LastWormHitRegistry.Capacity, r.Count);
            Assert.False(r.TryGet(Id(1, 1), 0.9f, out _, out _, out _));
            Assert.True(r.TryGet(Id(1, 1000), 0.9f, out _, out int slot, out _));
            Assert.Equal(99, slot);

            // Later, the expired ones make room: nothing live is lost.
            r.Record(Id(1, 2000), Id(2, 20), 100, 1, 50f);
            Assert.Equal(1, r.Count);
        }

        [Fact]
        public void ClearForgetsEverything()
        {
            var r = new LastWormHitRegistry();
            r.Record(Id(1, 1), Id(2, 20), 1, 1, 0f);
            r.Clear();
            Assert.Equal(0, r.Count);
            Assert.False(r.TryGet(Id(1, 1), 0f, out _, out _, out _));
        }

        // ------------------------------------------------------------------ one capture per death

        [Fact]
        public void ASecondRollOfTheSameDeathIsRecognised()
        {
            var book = new CaptureBook();
            ZDOID seeker = Id(1, 5);
            Assert.False(book.Seen(seeker, 10f));
            book.Mark(seeker, 10f);
            Assert.True(book.Seen(seeker, 10f));                          // Ragdoll.Setup then CharacterDrop.OnDeath, same frame
            Assert.True(book.Seen(seeker, 19.9f));
            Assert.False(book.Seen(seeker, 21f));                         // another creature of the same id much later is a new death
            Assert.False(book.Seen(Id(1, 6), 10f));
            Assert.False(book.Seen(ZDOID.None, 10f));
        }

        [Fact]
        public void TheBookKeepsTheLastSixteenDeaths()
        {
            var book = new CaptureBook();
            for (uint k = 1; k <= CaptureBook.Capacity + 1; k++) book.Mark(Id(1, k), 0f);
            Assert.False(book.Seen(Id(1, 1), 1f));                        // pushed out by the 17th
            Assert.True(book.Seen(Id(1, 2), 1f));
            Assert.True(book.Seen(Id(1, CaptureBook.Capacity + 1), 1f));
            book.Clear();
            Assert.False(book.Seen(Id(1, 2), 1f));
        }

        // ------------------------------------------------------------------ counting

        [Theory]
        [InlineData(10, true, 10)]
        [InlineData(10, false, 3)]
        [InlineData(3, false, 1)]
        [InlineData(2, false, 1)]       // at least one
        [InlineData(1, false, 1)]
        [InlineData(0, false, 0)]
        [InlineData(-4, true, 0)]
        [InlineData(7, true, 7)]
        public void PiecesGiveAThirdWhenNotPlacedByAPlayer(int amount, bool placed, int expected)
        {
            Assert.Equal(expected, CaptureMath.PieceUnits(amount, placed));
        }

        [Fact]
        public void CapturedUnitsAddUpPerItem()
        {
            var list = new ConsumedList();
            Assert.True(CaptureMath.AddUnits(list, 111, 2));
            Assert.True(CaptureMath.AddUnits(list, 222, 1));
            Assert.True(CaptureMath.AddUnits(list, 111, 3));              // the same item twice in a drop list
            Assert.False(CaptureMath.AddUnits(list, 333, 0));
            Assert.False(CaptureMath.AddUnits(list, 333, -1));
            Assert.False(CaptureMath.AddUnits(null, 333, 1));
            Assert.Equal(2, list.Count);
            Assert.Equal(6, CaptureMath.TotalUnits(list));
            Assert.Equal(5f, list.Qty[0], 3);
        }

        [Fact]
        public void ACapturedListTravelsAsWholeUnitsWithinTheRpcLimits()
        {
            // A big list (more than 16 items, a stack over 50) is split into valid packets and every unit arrives exactly once.
            var sent = new ConsumedList();
            for (int h = 1; h <= 20; h++) CaptureMath.AddUnits(sent, h, h == 3 ? 120 : h);
            var received = new ConsumedList();
            var total = new ConsumedList();
            int entry = 0;
            float done = 0f;
            while (true)
            {
                var pkg = new ZPackage();
                if (!sent.WriteNextPacket(pkg, ref entry, ref done)) break;
                Assert.Equal(0, ConsumedList.TryRead(pkg, h => true, received));
                for (int i = 0; i < received.Count; i++) total.Add(received.Hash[i], received.Qty[i]);
            }
            Assert.Equal(CaptureMath.TotalUnits(sent), CaptureMath.TotalUnits(total));
            for (int h = 1; h <= 20; h++) Assert.Equal(h == 3 ? 120f : h, FindQty(total, h), 3);
        }

        private static float FindQty(ConsumedList list, int hash)
        {
            for (int i = 0; i < list.Count; i++) if (list.Hash[i] == hash) return list.Qty[i];
            return 0f;
        }
    }
}
