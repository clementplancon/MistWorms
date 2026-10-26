using System.Collections.Generic;
using MistWorms.Colony;
using UnityEngine;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>The network identity of the worms (phase 8, decision 214): the id table and its use by the store.</summary>
    public class NetIdTests
    {
        private static int Add(WormStore s, int forced = 0) => s.Alloc(Vector3.zero, 0f, WormState.Idle, 10f, 0f, forced);

        [Fact]
        public void IdsAreHandedOutInIncreasingOrderFromOne()
        {
            var t = new NetIdTable();
            Assert.Equal(1, t.Assign(0));
            Assert.Equal(2, t.Assign(1));
            Assert.Equal(3, t.Assign(2));
            Assert.Equal(3, t.LiveCount);
        }

        [Fact]
        public void FindReturnsTheSlotAndMinusOneForAnUnknownId()
        {
            var t = new NetIdTable();
            ushort a = t.Assign(7);
            Assert.Equal(7, t.Find(a));
            Assert.Equal(-1, t.Find(a + 1));
            Assert.Equal(-1, t.Find(0));
            Assert.Equal(-1, t.Find(-5));
            Assert.Equal(-1, t.Find(70000));
        }

        [Fact]
        public void ReleaseFreesTheIdButANewWormDoesNotTakeItBackAtOnce()
        {
            var t = new NetIdTable();
            ushort a = t.Assign(0);
            t.Release(a);
            Assert.Equal(-1, t.Find(a));
            Assert.Equal(0, t.LiveCount);
            Assert.NotEqual(a, t.Assign(0));   // monotone: the next one is a + 1, so a stale hit on a is not applied to the new worm
            t.Release(a);                       // releasing twice is harmless
            t.Release(0);
            Assert.Equal(1, t.LiveCount);
        }

        [Fact]
        public void WrappingRoundSkipsZeroAndTheIdsThatAreAlive()
        {
            var t = new NetIdTable();
            ushort first = t.Assign(0);          // id 1, stays alive
            var seen = new HashSet<int> { first };
            ushort last = 0;
            for (int i = 1; i < 65534; i++)       // ids 2 .. 65534
            {
                last = t.Assign(i % 1000);
                Assert.True(seen.Add(last));
                t.Release(last);                   // keep only id 1 alive
            }
            Assert.Equal(65534, last);
            Assert.Equal(65535, t.Assign(5));      // the last id
            t.Release(65535);
            ushort wrapped = t.Assign(6);          // the counter wrapped: 0 is never an id and 1 is alive
            Assert.Equal(2, wrapped);
            Assert.Equal(6, t.Find(wrapped));
            Assert.Equal(0, t.Find(first));
        }

        [Fact]
        public void RegisterTakesTheIdTheOwnerChoseAndRefusesATakenOne()
        {
            var t = new NetIdTable();
            Assert.Equal(500, t.Register(500, 3));
            Assert.Equal(3, t.Find(500));
            Assert.Throws<System.InvalidOperationException>(() => t.Register(500, 4));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => t.Register(0, 4));
        }

        [Fact]
        public void EveryLiveWormOfTheStoreHasADistinctNonZeroId()
        {
            var s = new WormStore(50);
            var ids = new HashSet<int>();
            for (int i = 0; i < 50; i++)
            {
                int slot = Add(s);
                Assert.NotEqual(0, s.netId[slot]);
                Assert.True(ids.Add(s.netId[slot]));
                Assert.Equal(slot, s.FindByNetId(s.netId[slot]));
            }
        }

        [Fact]
        public void FreeingRecordsTheIdForThePeersAndForgetsTheLookup()
        {
            var s = new WormStore(4);
            int a = Add(s), b = Add(s);
            ushort ida = s.netId[a], idb = s.netId[b];
            s.Free(a);
            s.Free(b);
            Assert.Equal(2, s.FreedCount);
            Assert.Equal(ida, s.FreedIds[0]);
            Assert.Equal(idb, s.FreedIds[1]);
            Assert.Equal(-1, s.FindByNetId(ida));
            Assert.Equal(0, s.netId[a]);
            s.Free(a);                               // a second Free is ignored: not recorded twice
            Assert.Equal(2, s.FreedCount);
            s.ClearFreed();
            Assert.Equal(0, s.FreedCount);
            Assert.False(s.FreedOverflow);
        }

        [Fact]
        public void AReusedSlotGetsANewId()
        {
            var s = new WormStore(1);
            int a = Add(s);
            ushort first = s.netId[a];
            s.Free(a);
            int b = Add(s);
            Assert.Equal(a, b);
            Assert.NotEqual(first, s.netId[b]);
            Assert.Equal(-1, s.FindByNetId(first));
        }

        [Fact]
        public void AMirrorGivesItsWormsTheIdsTheOwnerChoseAndRefusesADuplicate()
        {
            var s = new WormStore(4);
            int a = Add(s, forced: 900);
            Assert.True(a >= 0);
            Assert.Equal(900, s.netId[a]);
            Assert.Equal(a, s.FindByNetId(900));
            int before = s.AliveCount;
            Assert.Equal(-1, Add(s, forced: 900));
            Assert.Equal(before, s.AliveCount);      // nothing was taken
        }

        [Fact]
        public void TheFreedListOverflowsLoudlyInsteadOfGrowing()
        {
            var s = new WormStore(2);                 // room for 4 freed ids
            for (int round = 0; round < 3; round++)
            {
                int a = Add(s), b = Add(s);
                s.Free(a);
                s.Free(b);
            }
            Assert.Equal(4, s.FreedCount);
            Assert.True(s.FreedOverflow);
            s.ClearFreed();
            Assert.False(s.FreedOverflow);
        }
    }
}
