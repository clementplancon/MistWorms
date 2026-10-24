using System;
using MistWorms.Colony;
using UnityEngine;
using Xunit;

namespace MistWorms.Tests
{
    public class WormStoreTests
    {
        private static int Add(WormStore s, float x = 0f) => s.Alloc(new Vector3(x, 0f, 0f), 0f, WormState.Idle, 10f, 0f);

        [Fact]
        public void AllocFillsSlotsInOrderAndTracksCounts()
        {
            var s = new WormStore(4);
            Assert.Equal(0, Add(s));
            Assert.Equal(1, Add(s));
            Assert.Equal(2, s.HighWater);
            Assert.Equal(2, s.AliveCount);
            Assert.True(s.alive[0] && s.alive[1] && !s.alive[2]);
        }

        [Fact]
        public void CapacityIsRespected()
        {
            var s = new WormStore(3);
            for (int i = 0; i < 3; i++) Assert.True(Add(s) >= 0);
            Assert.Equal(-1, Add(s));
            Assert.Equal(3, s.AliveCount);
            Assert.Equal(3, s.HighWater);
        }

        [Fact]
        public void FreeReleasesTheSlotForReuse()
        {
            var s = new WormStore(2);
            int a = Add(s), b = Add(s);
            Assert.Equal(-1, Add(s));

            s.Free(a);
            Assert.False(s.alive[a]);
            Assert.Equal((byte)WormState.Dead, s.state[a]);
            Assert.Equal(1, s.AliveCount);

            Assert.Equal(a, Add(s)); // the freed slot comes back
            Assert.Equal(2, s.AliveCount);
            Assert.True(s.alive[b]);
        }

        [Fact]
        public void GenerationChangesOnEveryReuse()
        {
            var s = new WormStore(1);
            int i = Add(s);
            ushort first = s.generation[i];
            Assert.True(s.IsAlive(i, first));

            s.Free(i);
            Assert.False(s.IsAlive(i, first)); // dead
            int again = Add(s);
            Assert.Equal(i, again);
            Assert.NotEqual(first, s.generation[i]);
            Assert.False(s.IsAlive(i, first));  // a stale proxy reference no longer matches
            Assert.True(s.IsAlive(i, s.generation[i]));
        }

        [Fact]
        public void GenerationWrapsWithoutThrowing()
        {
            var s = new WormStore(1);
            for (int n = 0; n < 70000; n++)
            {
                int i = Add(s);
                s.Free(i);
            }
            Assert.Equal(0, s.AliveCount);
        }

        [Fact]
        public void DoubleFreeAndBadIndexAreIgnored()
        {
            var s = new WormStore(2);
            int i = Add(s);
            s.Free(i);
            s.Free(i);
            s.Free(-1);
            s.Free(99);
            Assert.Equal(0, s.AliveCount);
            // Still exactly two slots available, not three.
            Assert.True(Add(s) >= 0);
            Assert.True(Add(s) >= 0);
            Assert.Equal(-1, Add(s));
        }

        [Fact]
        public void AllocResetsTheSlot()
        {
            var s = new WormStore(1);
            int i = Add(s, 5f);
            s.hp[i] = 1f;
            s.targetId[i] = 42;
            s.pathSlot[i] = 7;
            s.animTime[i] = 9f;
            s.Free(i);

            Add(s, 2f);
            Assert.Equal(new Vector3(2f, 0f, 0f), s.pos[i]);
            Assert.Equal(10f, s.hp[i]);
            Assert.Equal(0L, s.targetId[i]);
            Assert.Equal(-1, s.pathSlot[i]);
            Assert.Equal(-1, s.nestIndex[i]);
            Assert.Equal(-1, s.poolIndex[i]);
            Assert.Equal(-1, s.proxyIndex[i]);
            Assert.Equal(0f, s.animTime[i]);
            Assert.Equal((byte)WormState.Idle, s.state[i]);
        }

        [Fact]
        public void RejectsZeroCapacity()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WormStore(0));
        }
    }
}
