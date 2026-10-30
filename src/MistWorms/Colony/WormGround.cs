namespace MistWorms.Colony
{
    internal enum GroundVerdict
    {
        Keep,   // keep the position and the state, try again at the next check
        Kill,   // remove the worm
    }

    /// <summary>
    /// Pure decision of what happens to a worm whose floor probe failed (decision 273, spec 11.1). A worm never dies for one failed probe: a zone or a room
    /// that is still loading has no collider yet. No game access: unit tested.
    /// </summary>
    internal static class WormGround
    {
        /// <summary>Value of the per worm "first failure" time meaning "the last probe succeeded".</summary>
        public const float NoFailure = -1f;

        /// <summary>An awake worm in a loaded zone is removed only after this many seconds of continuous failure.</summary>
        public const float FailGrace = 3f;

        /// <summary>
        /// A worm that never had a floor (restored or created above rooms that are still loading) is frozen for this long from its creation or restoration
        /// (decision 274). Longer than <c>RestoreGroundWait</c> (the wait for the first worm) and than the 10.6 s a dungeon takes to stand.
        /// </summary>
        public const float FirstFloorWait = 30f;

        /// <summary>Where a sleeper of a mass that went back to sleep is laid (decision 275): on the ring, else on the floor found under the mass with the long probe, else (no floor at all) at the mass position, kept and traced.</summary>
        public enum DepositSpot
        {
            Ring,
            LongProbe,
            Unsupported,
        }

        public static DepositSpot ChooseDepositSpot(bool ringFound, bool longProbeFound) =>
            ringFound ? DepositSpot.Ring : longProbeFound ? DepositSpot.LongProbe : DepositSpot.Unsupported;

        /// <summary>What a sleeper does when its floor probe fails once its first-floor window is over (decision 275).</summary>
        public enum SleeperAction
        {
            Wait,         // zone not loaded, or still inside the first-floor window: nothing
            Fall,         // a floor exists below (long probe, inside the box): drop onto it
            KeepTraced,   // no floor anywhere: it stays where it is (a sleeper never dies for lack of a floor), traced in dev
        }

        public static SleeperAction OnSleeperNoFloor(bool zoneLoaded, bool hadGround, float now, float waitEnd, bool fallFloorFound)
        {
            if (!zoneLoaded || AwaitingFirstFloor(hadGround, now, waitEnd)) return SleeperAction.Wait;
            return fallFloorFound ? SleeperAction.Fall : SleeperAction.KeepTraced;
        }

        /// <summary>The worm has never had a floor and its wait window (<paramref name="waitEnd"/>) is still open: it must not move, fall or die.</summary>
        public static bool AwaitingFirstFloor(bool hadGround, float now, float waitEnd) => !hadGround && now < waitEnd;

        /// <summary>
        /// <paramref name="failSince"/> is the per worm first-failure time (updated: <see cref="NoFailure"/> when frozen, still awaiting its first floor, or asleep, set to
        /// <paramref name="now"/> on the first failure of an awake worm).
        /// </summary>
        public static GroundVerdict OnProbeFailed(bool zoneLoaded, bool sleeping, float now, ref float failSince, float grace = FailGrace, bool hadGround = true, float waitEnd = 0f)
        {
            if (!zoneLoaded || sleeping || AwaitingFirstFloor(hadGround, now, waitEnd))
            {
                failSince = NoFailure;   // frozen (zone not loaded) or asleep: no death, and a later failure starts its own delay
                return GroundVerdict.Keep;
            }
            if (failSince < 0f) failSince = now;
            return now - failSince >= grace ? GroundVerdict.Kill : GroundVerdict.Keep;
        }
    }
}
