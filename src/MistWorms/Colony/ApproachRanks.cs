namespace MistWorms.Colony
{
    /// <summary>
    /// Approach places of the hunters (plan 4A-3 point 3): a counter per target in a small fixed table. A worm takes the next rank
    /// when it starts hunting a target and keeps it as long as it hunts that target (it is stored in the worm), so a rank never moves
    /// when other worms arrive or leave. The counter of a target starts again when no hunter was ranked for <see cref="ResetSeconds"/>.
    /// Pure data, no allocation after construction: unit tested.
    /// </summary>
    internal sealed class ApproachRanks
    {
        public const int Slots = 8;
        public const float ResetSeconds = 2f;

        private readonly bool[] _used = new bool[Slots];
        private readonly long[] _target = new long[Slots];
        private readonly int[] _count = new int[Slots];
        private readonly float[] _baseAngle = new float[Slots];
        private readonly float[] _lastAssign = new float[Slots];

        /// <summary>
        /// Next rank for <paramref name="targetId"/>. <paramref name="arrivalBearing"/> (rad, bearing of the worm seen from the target) becomes
        /// the base angle when the counter starts; <paramref name="baseAngle"/> is the one of the running counter.
        /// </summary>
        public int Assign(long targetId, float now, float arrivalBearing, out float baseAngle)
        {
            int slot = -1, free = -1, oldest = -1;
            for (int k = 0; k < Slots; k++)
            {
                if (!_used[k])
                {
                    if (free < 0) free = k;
                    continue;
                }
                if (_target[k] == targetId) slot = k;
                if (oldest < 0 || _lastAssign[k] < _lastAssign[oldest]) oldest = k;
            }

            bool restart = false;
            if (slot < 0)
            {
                slot = free >= 0 ? free : oldest;
                restart = true;
            }
            else if (now - _lastAssign[slot] > ResetSeconds)
            {
                restart = true;
            }

            if (restart)
            {
                _used[slot] = true;
                _target[slot] = targetId;
                _count[slot] = 0;
                _baseAngle[slot] = arrivalBearing;
            }
            _lastAssign[slot] = now;
            baseAngle = _baseAngle[slot];
            return _count[slot]++;
        }

        public void Clear()
        {
            for (int k = 0; k < Slots; k++) _used[k] = false;
        }
    }
}
