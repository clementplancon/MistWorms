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
        /// Of the colonies found for one box, the one that stays: the smallest ZDO id. Returns its index in <paramref name="users"/> / <paramref name="ids"/>
        /// (-1 when the lists are empty). The others are destroyed by their owners; the seeded flag is inherited, so a mine is never seeded twice.
        /// </summary>
        public static int Keep(long[] users, uint[] ids, int count)
        {
            int keep = -1;
            for (int i = 0; i < count; i++)
                if (keep < 0 || Compare(users[i], ids[i], users[keep], ids[keep]) < 0) keep = i;
            return keep;
        }

        /// <summary>The kept colony is seeded when it or any duplicate was.</summary>
        public static bool InheritSeeded(bool keptSeeded, bool anyDuplicateSeeded) => keptSeeded || anyDuplicateSeeded;
    }
}
