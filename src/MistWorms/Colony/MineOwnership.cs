namespace MistWorms.Colony
{
    /// <summary>What the peer of a player standing in a mine does about the colony of that mine.</summary>
    internal enum MineStep
    {
        Nothing = 0,
        Create = 1,   // there is no colony: create it (a duplicate made by another peer at the same time is removed, see Keep)
        Claim = 2,    // a colony exists, owned by a peer that has nobody in the mine: take its ownership
        Seed = 3,     // this peer owns an unseeded colony: seed the mine
    }

    /// <summary>What decides which of two colonies of one mine stays (see <see cref="MineOwnership.CompareKeeper"/>): data read from the ZDO, the same on every peer.</summary>
    internal struct KeeperCandidate
    {
        public bool Seeded;         // the mine was seeded by this colony (the flag of its ZDO)
        public int Worms;           // free worms in the snapshot of its ZDO
        public long CreatedTicks;   // network time of its creation, 0 = unknown (a colony written before the key existed)
        public long User;           // ZDOID, last resort of the order (same on every peer within a session)
        public uint Id;
    }

    /// <summary>Pure decision rules of the colony of a mine (spec §8.3, decision 199). No game access: unit tested.</summary>
    internal static class MineOwnership
    {
        /// <summary>
        /// The step for the local peer, whose player stands in the box of the mine. An owner that has a player in the box keeps the colony (the
        /// vanilla rule gives it to the nearest peer; phase 8 does the rest); one that has nobody there (it stayed outside, next to the entrance, or
        /// left) loses it to the player inside.
        /// </summary>
        public static MineStep Decide(bool colonyExists, bool ownedHere, bool ownerHasPlayerInside, bool seeded)
        {
            if (!colonyExists) return MineStep.Create;
            if (ownedHere) return seeded ? MineStep.Nothing : MineStep.Seed;
            return ownerHasPlayerInside ? MineStep.Nothing : MineStep.Claim;
        }

        /// <summary>Order of two ZDO ids: by user id, then by id. Negative when the first is the smaller.</summary>
        public static int Compare(long userA, uint idA, long userB, uint idB)
        {
            if (userA != userB) return userA < userB ? -1 : 1;
            if (idA != idB) return idA < idB ? -1 : 1;
            return 0;
        }

        /// <summary>
        /// Of the colonies found for one box, the one with the smallest ZDO id (not used to choose any more, a ZDOID is not stable: see <see cref="PickKeeper"/>). Returns its index in <paramref name="users"/> / <paramref name="ids"/>
        /// (-1 when the lists are empty). The others are destroyed by their owners; the seeded flag is inherited, so a mine is never seeded twice.
        /// </summary>
        public static int Keep(long[] users, uint[] ids, int count)
        {
            int keep = -1;
            for (int i = 0; i < count; i++)
                if (keep < 0 || Compare(users[i], ids[i], users[keep], ids[keep]) < 0) keep = i;
            return keep;
        }

        /// <summary>
        /// Which of two colonies of one mine stays (decision 211): negative when <paramref name="a"/> does. The seeded one first (a mine is never seeded twice), then the one that
        /// holds more worms (it is the one that lost less), then the older one (a creation time of 0 is unknown, so the last), then the smaller ZDOID. Everything but the last
        /// comes from the ZDO, so the answer does not depend on the session; the ZDOID only breaks a tie between two colonies of the same age and content.
        /// </summary>
        public static int CompareKeeper(KeeperCandidate a, KeeperCandidate b)
        {
            if (a.Seeded != b.Seeded) return a.Seeded ? -1 : 1;
            if (a.Worms != b.Worms) return a.Worms > b.Worms ? -1 : 1;
            long ca = a.CreatedTicks > 0 ? a.CreatedTicks : long.MaxValue, cb = b.CreatedTicks > 0 ? b.CreatedTicks : long.MaxValue;
            if (ca != cb) return ca < cb ? -1 : 1;
            return Compare(a.User, a.Id, b.User, b.Id);
        }

        /// <summary>Index of the colony that stays among <paramref name="count"/> candidates (-1 when there are none).</summary>
        public static int PickKeeper(KeeperCandidate[] candidates, int count)
        {
            int keep = -1;
            for (int i = 0; i < count; i++)
                if (keep < 0 || CompareKeeper(candidates[i], candidates[keep]) < 0) keep = i;
            return keep;
        }

        /// <summary>The kept colony is seeded when it or any duplicate was.</summary>
        public static bool InheritSeeded(bool keptSeeded, bool anyDuplicateSeeded) => keptSeeded || anyDuplicateSeeded;
    }
}
