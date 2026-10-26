using System;

namespace MistWorms.Colony
{
    /// <summary>
    /// Network identity of the worms of a colony (phase 8, decision 214): a <c>ushort</c> that the owner hands out in increasing order, wrapping round and skipping 0 and the ids that are
    /// alive, and the lookup <c>id -> slot</c> as an indexed array of 65 536 entries (slot + 1, 0 = free): no hashing, no allocation after construction. The slot of a worm is private to
    /// each peer (a mirror fills its own slots), the id is what travels. Pure data, unit tested.
    /// </summary>
    internal sealed class NetIdTable
    {
        public const int IdCount = 65536;

        private readonly ushort[] _slotPlusOne = new ushort[IdCount];
        private int _next = 1;
        private int _live;

        /// <summary>Slots up to this are representable (the table stores slot + 1 in a ushort).</summary>
        public const int MaxSlots = ushort.MaxValue - 1;

        public int LiveCount => _live;

        /// <summary>The next free id, bound to <paramref name="slot"/>. Throws when all 65 535 ids are alive (the store never holds that many).</summary>
        public ushort Assign(int slot)
        {
            if ((uint)slot >= MaxSlots) throw new ArgumentOutOfRangeException(nameof(slot));
            if (_live >= IdCount - 1) throw new InvalidOperationException("no free network id");
            while (true)
            {
                int id = _next;
                _next = _next == ushort.MaxValue ? 1 : _next + 1;
                if (_slotPlusOne[id] != 0) continue;
                _slotPlusOne[id] = (ushort)(slot + 1);
                _live++;
                return (ushort)id;
            }
        }

        /// <summary>Binds a given id (chosen by the owner) to <paramref name="slot"/>; the id must be 1 to 65 535 and free.</summary>
        public ushort Register(int id, int slot)
        {
            if (id < 1 || id > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(id));
            if ((uint)slot >= MaxSlots) throw new ArgumentOutOfRangeException(nameof(slot));
            if (_slotPlusOne[id] != 0) throw new InvalidOperationException("network id already alive");
            _slotPlusOne[id] = (ushort)(slot + 1);
            _live++;
            return (ushort)id;
        }

        /// <summary>The slot bound to the id, or -1.</summary>
        public int Find(int id) => (uint)(id - 1) < ushort.MaxValue ? _slotPlusOne[id] - 1 : -1;

        public void Release(int id)
        {
            if ((uint)(id - 1) >= ushort.MaxValue || _slotPlusOne[id] == 0) return;
            _slotPlusOne[id] = 0;
            _live--;
        }
    }
}
