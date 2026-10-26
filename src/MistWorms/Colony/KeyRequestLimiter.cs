namespace MistWorms.Colony
{
    /// <summary>
    /// At most one keyframe request per peer and per second (phase 8, decision 225): the owner uses it to answer <c>MW_KeyReq</c>, a peer uses it (with the owner's uid as key) to
    /// ask at most once a second. A fixed table, no allocation; a peer that is not in it takes the oldest entry. Pure, unit tested.
    /// </summary>
    internal sealed class KeyRequestLimiter
    {
        public const float Interval = 1f;
        private const int Slots = 32;

        private readonly long[] _uid = new long[Slots];
        private readonly float[] _last = new float[Slots];
        private int _count;

        /// <summary>True when <paramref name="uid"/> may be served at <paramref name="now"/>; remembers it then.</summary>
        public bool Allow(long uid, float now)
        {
            int at = -1, oldest = 0;
            for (int k = 0; k < _count; k++)
            {
                if (_uid[k] == uid) { at = k; break; }
                if (_last[k] < _last[oldest]) oldest = k;
            }
            if (at < 0)
            {
                at = _count < Slots ? _count++ : oldest;
                _uid[at] = uid;
                _last[at] = now;
                return true;
            }
            if (now - _last[at] < Interval) return false;
            _last[at] = now;
            return true;
        }

        public void Clear() => _count = 0;
    }
}
