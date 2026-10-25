namespace MistWorms.Drops
{
    /// <summary>
    /// The pools of the free worms of one colony (plan 6, A10): slots allocated once, handed out by <see cref="Acquire"/> (the worm's
    /// <c>poolIndex</c>, -1 while it has eaten nothing) and returned by <see cref="Release"/>. A pool object is created the first time its slot
    /// is used and then reused. Pure data.
    /// </summary>
    internal sealed class PoolTable
    {
        private readonly MassPool[] _pools;
        private readonly int[] _free;
        private int _freeCount;

        public readonly int Capacity;

        /// <summary>Slots in use.</summary>
        public int InUse => Capacity - _freeCount;

        public PoolTable(int capacity)
        {
            Capacity = capacity;
            _pools = new MassPool[capacity];
            _free = new int[capacity];
            for (int i = 0; i < capacity; i++) _free[i] = capacity - 1 - i;
            _freeCount = capacity;
        }

        /// <summary>A cleared pool and its index; -1 when the table is full.</summary>
        public int Acquire()
        {
            if (_freeCount == 0) return -1;
            int i = _free[--_freeCount];
            if (_pools[i] == null) _pools[i] = new MassPool();
            else _pools[i].Clear();
            return i;
        }

        public void Release(int index)
        {
            if ((uint)index >= (uint)Capacity) return;
            if (_pools[index] != null) _pools[index].Clear();
            _free[_freeCount++] = index;
        }

        public MassPool Get(int index) => (uint)index < (uint)Capacity ? _pools[index] : null;
    }
}
