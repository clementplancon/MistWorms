using System;

namespace MistWorms.Mass
{
    /// <summary>
    /// Pure rules of the merging of two masses (spec §4.1, decisions.md 77, 79, 92; phase 4B-4): who touches whom, who is the smaller, how many
    /// worms move. Unit tested.
    /// </summary>
    public static class MassMergeMath
    {
        /// <summary>
        /// Two bodies cannot stand closer than the two physical capsules allow (0.6 m each), and the visual radius of a small mass is under 0.6 m:
        /// without a floor on the contact distance two small masses would never "touch".
        /// </summary>
        public const float MinContactDistance = 1.35f;

        /// <summary>Highest height difference (metres) at which two masses can touch: another floor of the mine is another place.</summary>
        public const float MaxContactDy = 2f;

        /// <summary>Seconds the small mass takes to slide onto the big one (spec §4.1, §12.4).</summary>
        public const float MergeSeconds = 0.5f;

        /// <summary>Seconds the Transferring bit of a partial merge stays on (long enough for every peer to see the count change under it).</summary>
        public const float TransferFlagSeconds = 0.6f;

        /// <summary>Seconds a mass waits before its next partial transfer (the count of a remote big mass takes a moment to come back).</summary>
        public const float TransferCooldown = 1.5f;

        /// <summary>The spheres touch: the horizontal distance is under the sum of the radii (never under <see cref="MinContactDistance"/>).</summary>
        public static bool Touching(float horizontalDistance, float dy, float r1, float r2)
        {
            if (Math.Abs(dy) > MaxContactDy) return false;
            return horizontalDistance < Math.Max(r1 + r2, MinContactDistance);
        }

        /// <summary>
        /// True when mass 1 is the one that slides toward mass 2: fewer worms, and at equal count the lower ZDOID (owner id, then sequence number).
        /// </summary>
        public static bool IsSmaller(int n1, long user1, uint id1, int n2, long user2, uint id2)
        {
            if (n1 != n2) return n1 < n2;
            if (user1 != user2) return user1 < user2;
            return id1 < id2;
        }

        public enum MergePlan { None, Complete, Partial }

        /// <summary>
        /// What a mass does about another one it touches, decided by its own owner and no other: nothing unless it is the smaller of the two (<see cref="IsSmaller"/>)
        /// and the other one has room; then all its worms slide onto the other (<see cref="MergePlan.Complete"/>) or exactly <paramref name="moved"/> are handed over
        /// (<see cref="MergePlan.Partial"/>). Of two masses at most one acts, the smaller.
        /// </summary>
        public static MergePlan Decide(int nMe, long userMe, uint idMe, int nOther, long userOther, uint idOther, int max, out int moved)
        {
            moved = 0;
            if (nMe <= 0 || nOther <= 0 || nOther >= max) return MergePlan.None;
            if (!IsSmaller(nMe, userMe, idMe, nOther, userOther, idOther)) return MergePlan.None;
            moved = Transfer(nMe, nOther, max, out bool complete);
            if (moved <= 0) return MergePlan.None;
            return complete ? MergePlan.Complete : MergePlan.Partial;
        }

        /// <summary>
        /// Worms the small mass gives the big one: all of them when they fit (<paramref name="complete"/>), otherwise exactly
        /// <c>max - nBig</c> (0 when the big one is full) and the small mass keeps the rest.
        /// </summary>
        public static int Transfer(int nSmall, int nBig, int max, out bool complete)
        {
            if (nSmall <= 0) { complete = false; return 0; }
            if (nSmall + nBig <= max) { complete = true; return nSmall; }
            complete = false;
            return Math.Max(0, max - nBig);
        }
    }
}
