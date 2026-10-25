using System;

namespace MistWorms.Mass
{
    /// <summary>Why a mass lets go of its target (plan 5, A15). Fire comes with pass 5-2.</summary>
    internal enum DetachReason
    {
        None = 0,
        TargetInvalid,   // dead, ghost, teleported, disconnected
        TooFar,          // beyond r + Mass.DetachDistance for Mass.DetachDelay
        Dissolution,     // the mass is dissolving or dying
        Fire,
    }

    /// <summary>Schedule of the bite ticks: one every interval, a cancelled tick pushes the next one back (spec §4.8, plan 5, A11). Pure.</summary>
    internal struct BiteTicker
    {
        private float _next;
        private bool _running;

        public bool Running => _running;
        public float Next => _next;

        /// <summary>The first tick falls one interval after the start.</summary>
        public void Start(float now, float interval)
        {
            _running = true;
            _next = now + interval;
        }

        public void Stop() => _running = false;

        public bool Due(float now) => _running && now >= _next;

        /// <summary>
        /// Schedules the tick after the one just fired at <paramref name="now"/>: one interval later (keeping the rhythm when the call is a
        /// little late, restarting from now after a long pause); a <paramref name="cancelled"/> tick adds <paramref name="cancelDelay"/>
        /// (0.5 s for a perfect parry: 1.0 s between the cancelled tick and the next one).
        /// </summary>
        public void Advance(float now, float interval, bool cancelled = false, float cancelDelay = 0f)
        {
            _next = now - _next < interval ? _next + interval : now + interval;
            if (cancelled) _next += cancelDelay;
        }

        /// <summary>The next tick is pushed back (a parry reported after the tick was fired).</summary>
        public void Postpone(float seconds) => _next += seconds;
    }

    /// <summary>Counts how long the target has stayed beyond the detach distance (spec §4.9). Pure.</summary>
    internal struct DetachTracker
    {
        private float _since;
        private bool _beyond;

        public void Reset() => _beyond = false;

        /// <summary>True once <paramref name="distance"/> has stayed above <paramref name="limit"/> for at least <paramref name="delay"/> seconds.</summary>
        public bool Update(float now, float distance, float limit, float delay)
        {
            if (distance <= limit)
            {
                _beyond = false;
                return false;
            }
            if (!_beyond)
            {
                _beyond = true;
                _since = now;
            }
            return now - _since >= delay;
        }
    }

    internal static class EngulfMath
    {
        /// <summary>Hunt to Engulf (plan 5, A2): the horizontal distance from the centre of the mass to the target is within r(n) + Mass.EngulfRange.</summary>
        public static bool InEngulfRange(float flatDistance, float radius, float engulfRange) => flatDistance <= radius + engulfRange;

        /// <summary>A cancelled tick delays the next one by this much on top of the interval (spec 4.8: 0.5 s, so 1.0 s between the two).</summary>
        public const float ParryDelay = 0.5f;

        /// <summary>Largest height difference between mass and target for the engulfing to start (a player on a ledge above is out of reach).</summary>
        public const float MaxHeightGap = 2.5f;

        public static float DetachLimit(float radius, float detachDistance) => radius + detachDistance;

        /// <summary>The detach decision, in the order of the plan (A15): a dying mass or an invalid target first, then fire, then the distance.</summary>
        public static DetachReason Decide(bool targetValid, bool massDissolving, bool fire, bool tooFar)
        {
            if (massDissolving) return DetachReason.Dissolution;
            if (!targetValid) return DetachReason.TargetInvalid;
            if (fire) return DetachReason.Fire;
            if (tooFar) return DetachReason.TooFar;
            return DetachReason.None;
        }

        /// <summary>Durability points of one tick: n x Worm.DurabilityPerBite, times the world difficulty factor when Engulf.ScaleWithWorldModifiers.</summary>
        public static float TickDurability(int worms, float perBite, float worldScale, bool scale) => Math.Max(0, worms) * perBite * (scale ? worldScale : 1f);
    }
}
