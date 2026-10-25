using System;

namespace MistWorms.Colony
{
    /// <summary>
    /// Pure rules that keep a worm or a mass from choosing, or sticking to, something out of reach (spec §5.3, decision 163): the vertical reach filter of the scan,
    /// the nearest-first selection of the scan buffer, and the key of a target in the blacklist. No allocation, unit tested.
    /// </summary>
    internal static class TargetReach
    {
        /// <summary>Vertical reach of a free worm: its eye above the ground plus the cobra range.</summary>
        public static float WormVerticalReach(float eyeHeight, float cobraRange) => eyeHeight + cobraRange;

        /// <summary>
        /// Vertical distance between the ground of the attacker (<paramref name="groundY"/>) and the target, which spans [<paramref name="low"/>, <paramref name="high"/>]
        /// (a point: both equal; a character: the bounds of its collider). 0 when the ground level is inside the span.
        /// </summary>
        public static float VerticalGap(float low, float high, float groundY)
        {
            if (low > high) { float t = low; low = high; high = t; }
            if (groundY < low) return low - groundY;
            if (groundY > high) return groundY - high;
            return 0f;
        }

        /// <summary>True when the target's vertical gap does not exceed the real vertical reach of the attacker.</summary>
        public static bool Reachable(float low, float high, float groundY, float verticalReach) =>
            VerticalGap(low, high, groundY) <= verticalReach;

        /// <summary>
        /// Position at which a candidate of distance <paramref name="d"/> enters a list of <paramref name="count"/> entries sorted by increasing distance
        /// (<paramref name="dist"/> from <paramref name="offset"/>, at most <paramref name="capacity"/>), or -1 when the list is full and the candidate is not nearer
        /// than its last entry. The caller moves the entries from that position one place up (the last one falls off when the list is full) and writes the new one.
        /// </summary>
        public static int NearestInsertPos(float[] dist, int offset, int count, int capacity, float d)
        {
            if (capacity <= 0) return -1;
            int pos = count;
            while (pos > 0 && dist[offset + pos - 1] > d) pos--;
            return pos >= capacity ? -1 : pos;
        }

        /// <summary>Key of a target object in the blacklist (never zero).</summary>
        public static long Key(long zdoUser, uint zdoId) => TargetRules.ObjectKey(zdoUser, zdoId);
    }

    /// <summary>
    /// Targets set aside for a while (decision 163): a fixed table of <c>PerOwner</c> entries (key, end time) for each owner (a worm slot, or the single mass).
    /// When an owner's entries are all live, the one that ends first is replaced. No allocation. Pure.
    /// </summary>
    internal sealed class TargetBlacklist
    {
        public const float DefaultSeconds = 10f;
        public const int PerOwner = 4;

        private readonly long[] _key;
        private readonly float[] _until;
        private readonly int _owners;

        public TargetBlacklist(int owners)
        {
            _owners = Math.Max(1, owners);
            _key = new long[_owners * PerOwner];
            _until = new float[_owners * PerOwner];
        }

        /// <summary>Forgets everything the owner had set aside (a worm slot that is reused).</summary>
        public void Clear(int owner)
        {
            if ((uint)owner >= (uint)_owners) return;
            for (int k = 0; k < PerOwner; k++) _key[owner * PerOwner + k] = 0L;
        }

        public void Add(int owner, long key, float now, float seconds = DefaultSeconds)
        {
            if ((uint)owner >= (uint)_owners || key == 0L) return;
            int baseIndex = owner * PerOwner;
            int slot = -1;
            for (int k = 0; k < PerOwner; k++)
            {
                int at = baseIndex + k;
                if (_key[at] == key) { slot = at; break; }
                if (_key[at] == 0L || _until[at] <= now) { if (slot < 0) slot = at; }
            }
            if (slot < 0)
            {
                slot = baseIndex;
                for (int k = 1; k < PerOwner; k++)
                    if (_until[baseIndex + k] < _until[slot]) slot = baseIndex + k;
            }
            _key[slot] = key;
            _until[slot] = now + seconds;
        }

        public bool Contains(int owner, long key, float now)
        {
            if ((uint)owner >= (uint)_owners || key == 0L) return false;
            int baseIndex = owner * PerOwner;
            for (int k = 0; k < PerOwner; k++)
                if (_key[baseIndex + k] == key && _until[baseIndex + k] > now) return true;
            return false;
        }
    }

    /// <summary>
    /// Safety net against a target that cannot be reached (decision 163): the distance to it must shrink by <see cref="MinProgress"/> within <see cref="Window"/>
    /// seconds, else the caller blacklists it. The clock restarts whenever the target changes, the attacker is exempt (striking, engulfing, in strike range,
    /// merging) or the watch was not fed for <see cref="MaxGap"/> seconds. A struct, kept in a plain array per worm. Pure.
    /// </summary>
    internal struct ProgressWatch
    {
        public const float MinProgress = 0.5f;
        public const float Window = 4f;
        public const float MaxGap = 1.5f;

        private long _key;
        private float _best;
        private float _since;
        private float _last;

        public void Reset() { _key = 0L; }

        /// <summary>Feeds one measure; true when the target made no progress for <see cref="Window"/> seconds (the watch then restarts).</summary>
        public bool Update(long key, float distance, float now, bool exempt)
        {
            if (key == 0L || exempt || key != _key || now - _last > MaxGap)
            {
                _key = exempt ? 0L : key;
                _best = distance;
                _since = now;
                _last = now;
                return false;
            }
            _last = now;
            if (distance < _best - MinProgress)
            {
                _best = distance;
                _since = now;
                return false;
            }
            if (now - _since < Window) return false;
            _key = 0L;
            return true;
        }
    }
}
