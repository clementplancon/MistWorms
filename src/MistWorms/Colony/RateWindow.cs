using System;

namespace MistWorms.Colony
{
    /// <summary>
    /// A quantity per second averaged over a sliding window (default 10 s) in one-second buckets: bytes sent or received by a colony (phase 8, plan A9). Adding costs nothing and nothing
    /// is allocated after construction. The clock is passed in, so it is unit tested. The average is over the time the window has really been running, up to its length, so the first
    /// seconds are not diluted.
    /// </summary>
    internal sealed class RateWindow
    {
        private readonly long[] _buckets;
        private readonly int _seconds;
        private long _currentSecond = long.MinValue;
        private double _firstTime = double.NaN;
        private long _total;

        public RateWindow(int seconds = 10)
        {
            if (seconds < 1) throw new ArgumentOutOfRangeException(nameof(seconds));
            _seconds = seconds;
            _buckets = new long[seconds];
        }

        /// <summary>Everything ever added.</summary>
        public long Total => _total;

        private void Advance(double now)
        {
            long sec = (long)Math.Floor(now);
            if (double.IsNaN(_firstTime)) _firstTime = now;
            if (_currentSecond == long.MinValue) { _currentSecond = sec; return; }
            if (sec <= _currentSecond) return;
            long gap = sec - _currentSecond;
            if (gap >= _seconds) Array.Clear(_buckets, 0, _seconds);
            else
                for (long s = _currentSecond + 1; s <= sec; s++) _buckets[(int)(s % _seconds)] = 0;
            _currentSecond = sec;
        }

        public void Add(long amount, double now)
        {
            Advance(now);
            _buckets[(int)(_currentSecond % _seconds)] += amount;
            _total += amount;
        }

        /// <summary>Average per second over the last <c>seconds</c> (the current, unfinished second counts for the part that has elapsed).</summary>
        public double PerSecond(double now)
        {
            if (_currentSecond == long.MinValue) return 0.0;
            Advance(now);
            long sum = 0;
            for (int k = 0; k < _seconds; k++) sum += _buckets[k];
            double span = Math.Min(_seconds, Math.Max(1.0, now - _firstTime));
            return sum / span;
        }
    }
}
