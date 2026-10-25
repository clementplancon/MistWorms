using System;

namespace MistWorms.Colony
{
    /// <summary>
    /// Targets.ExcludedPrefabs as a set of prefab hashes (spec §5.3, §10.4), shared by the worms and the masses: a ZDO carries the hash of its prefab, so
    /// testing a creature or a piece allocates nothing. The list is parsed again only when the config text changes. Pure: unit tested.
    /// </summary>
    internal sealed class ExcludedSet
    {
        private string _raw;
        private int[] _hashes = new int[0];

        /// <summary>Parses <paramref name="raw"/> (comma separated prefab names) unless it is the text already parsed.</summary>
        public void Refresh(string raw)
        {
            raw = raw ?? string.Empty;
            if (string.Equals(raw, _raw, StringComparison.Ordinal)) return;
            _raw = raw;
            _hashes = Parse(raw);
        }

        public static int[] Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new int[0];
            string[] parts = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var hashes = new int[parts.Length];
            int n = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                string name = parts[i].Trim();
                if (name.Length > 0) hashes[n++] = name.GetStableHashCode();
            }
            if (n != hashes.Length) Array.Resize(ref hashes, n);
            return hashes;
        }

        public int Count => _hashes.Length;

        public bool Contains(int prefabHash)
        {
            for (int i = 0; i < _hashes.Length; i++)
                if (_hashes[i] == prefabHash) return true;
            return false;
        }

        public bool ContainsName(string prefabName) => !string.IsNullOrEmpty(prefabName) && Contains(prefabName.GetStableHashCode());
    }

    /// <summary>
    /// <c>Character.Faction</c> by value (Character.cs:8-24), so that the pure rules and their tests never load the game's Character type. The dev scenario S19
    /// checks that the names and values still match.
    /// </summary>
    internal enum FactionId
    {
        Players = 0,
        AnimalsVeg = 1,
        ForestMonsters = 2,
        Undead = 3,
        Demon = 4,
        MountainMonsters = 5,
        SeaMonsters = 6,
        PlainsMonsters = 7,
        Boss = 8,
        MistlandsMonsters = 9,
        Dverger = 10,
        PlayerSpawned = 11,
        TrainingDummy = 12,
        DeepNorth = 13,
    }

    /// <summary>Pure rules of the target choice shared by the free worms and the masses (spec §5.3, §5.4, plan 6, A8, A11, A12).</summary>
    internal static class TargetRules
    {
        /// <summary>One shared set, refreshed by whoever reads it (main thread only).</summary>
        public static readonly ExcludedSet Excluded = new ExcludedSet();

        /// <summary>Prefix test of the dvergr structures of the mines (spec §5.3): their destructible props and walls. Never a nest, never another Destructible.</summary>
        public static bool IsDvergrStructure(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return false;
            for (int i = 0; i < VanillaNames.DvergrStructurePrefixes.Length; i++)
                if (prefabName.StartsWith(VanillaNames.DvergrStructurePrefixes[i], StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>Score of a candidate: distance x weight, the lowest wins (spec §5.3).</summary>
        public static float Score(float distance, float weight) => distance * weight;

        /// <summary>Distance weight of a kind (spec §5.3); None and unknown kinds have none (infinite).</summary>
        public static float Weight(TargetKind kind, float player, float tamed, float creature, float structure)
        {
            switch (kind)
            {
                case TargetKind.Player: return player;
                case TargetKind.Tamed: return tamed;
                case TargetKind.Creature: return creature;
                case TargetKind.Structure: return structure;
                default: return float.MaxValue;
            }
        }

        /// <summary>Targets.Attack* of a kind.</summary>
        public static bool Allowed(TargetKind kind, bool players, bool tamed, bool creatures, bool structures)
        {
            switch (kind)
            {
                case TargetKind.Player: return players;
                case TargetKind.Tamed: return tamed;
                case TargetKind.Creature: return creatures;
                case TargetKind.Structure: return structures;
                default: return false;
            }
        }

        /// <summary>A character that is not a player is Tamed or Creature.</summary>
        public static TargetKind KindOfCharacter(bool tamed) => tamed ? TargetKind.Tamed : TargetKind.Creature;

        /// <summary>
        /// The factions that are never targeted: other masses and everything player spawned (PlayerSpawned, spec §5.4) and the training dummy (the vanilla
        /// exception). A tamed animal is tested by <see cref="KindOfCharacter"/> before this, whatever its faction.
        /// </summary>
        public static bool FactionTargetable(FactionId faction) => faction != FactionId.PlayerSpawned && faction != FactionId.TrainingDummy;

        public static bool FactionTargetable(Character.Faction faction) => FactionTargetable((FactionId)(int)faction);

        /// <summary>A non zero key of an object ZDOID for the places that identify a target by a long (approach ranks); never collides with a small player id by construction of the high bit.</summary>
        public static long ObjectKey(long zdoUser, uint zdoId) => unchecked((zdoUser * 1000003L + zdoId) | (1L << 62));

        /// <summary>
        /// Index of the unused candidate with the lowest score under <paramref name="limit"/>, or -1. The caller tests the visibility of the best one
        /// and marks it used when it is hidden, so only a few line casts are made per choice (spec §5.3, plan 6, A12).
        /// </summary>
        public static int NextBest(float[] scores, bool[] used, int count, float limit) => NextBest(scores, used, 0, count, limit);

        /// <summary>Same over the candidates [<paramref name="from"/>, <paramref name="to"/>): the living ones come first in the array, the structures after them.</summary>
        public static int NextBest(float[] scores, bool[] used, int from, int to, float limit)
        {
            int best = -1;
            float bestScore = limit;
            for (int i = from; i < to; i++)
            {
                if (used[i] || !(scores[i] < bestScore)) continue;
                bestScore = scores[i];
                best = i;
            }
            return best;
        }

        /// <summary>
        /// A structure is a fallback target (decision 164): it is considered only when no player is perceived (<paramref name="playerLimit"/> is still float.MaxValue,
        /// the best player score of the caller) and no living candidate was chosen. Living targets are compared among themselves by distance x weight.
        /// </summary>
        public static bool StructureFallbackAllowed(float playerLimit, bool livingChosen) => !livingChosen && playerLimit >= float.MaxValue;

        /// <summary>
        /// Decision 166: the structure fallback waits until no living target was perceived for <paramref name="forgetTime"/> seconds, so that a target hidden for one
        /// tick by a corner does not make the attacker run to a wall and back.
        /// </summary>
        public static bool FallbackDelayElapsed(float now, float livingSeen, float forgetTime) => now - livingSeen > forgetTime;

        /// <summary>Decision 167: while the fear of a fire lasts (<paramref name="fearUntil"/>), a stalled hunt is the fire's doing: no progress verdict, no obstacle.</summary>
        public static bool AfraidOfFire(float now, float fearUntil) => now < fearUntil;

        /// <summary>Height above the attacker's feet under which a piece is never an obstacle (floors, foundations under it): its top must reach feet + this.</summary>
        public const float ObstacleMinHeight = 0.3f;

        /// <summary>Decision 166: a piece whose top (<paramref name="topY"/>) is lower than the attacker's feet + <see cref="ObstacleMinHeight"/> is under it, not in its way.</summary>
        public static bool BlocksTheWay(float topY, float feetY) => topY >= feetY + ObstacleMinHeight;

        /// <summary>
        /// Index of the nearest hit of a sweep (<paramref name="distance"/>, <paramref name="top"/> per hit) that is a targetable structure (<paramref name="candidate"/>)
        /// and blocks the way (<see cref="BlocksTheWay"/>), or -1. Other hits (a rock, a tree) are skipped. Pure: the geometry is the caller's.
        /// </summary>
        public static int FirstObstacle(float[] distance, float[] top, bool[] candidate, int count, float feetY)
        {
            int best = -1;
            for (int i = 0; i < count; i++)
            {
                if (!candidate[i] || !BlocksTheWay(top[i], feetY)) continue;
                if (best < 0 || distance[i] < distance[best]) best = i;
            }
            return best;
        }

        /// <summary>Range gate of a candidate: the current target is kept up to twice the range, any other must be within it.</summary>
        public static bool InRange(float distance, float range, bool isCurrent) => distance <= (isCurrent ? 2f * range : range);
    }

    /// <summary>
    /// What <c>BaseAI.IsEnemy</c> answers between a mass (faction PlayerSpawned) and a creature of another faction (spec §5.4, plan 6, A8): the table of the
    /// spec, as read in the decompiled vanilla method (BaseAI.cs:1230-1288). Pure.
    /// </summary>
    internal static class FactionMatrix
    {
        /// <summary>
        /// Spec §5.4 table for a creature of faction <paramref name="other"/>, not tamed: (mass to other, other to mass). Another PlayerSpawned: false both ways
        /// (same faction). Training dummy: true from the mass (the PlayerSpawned case of the switch has no exception, BaseAI.cs:1232) and false back (the dummy only
        /// fights players, BaseAI.cs:1285-1286). Every other faction: true both ways.
        /// </summary>
        public static void Spec(FactionId other, out bool massToOther, out bool otherToMass)
        {
            massToOther = other != FactionId.PlayerSpawned;
            otherToMass = other != FactionId.PlayerSpawned && other != FactionId.TrainingDummy;
        }

        /// <summary>A tamed creature of any faction is an enemy of the mass both ways (BaseAI.cs:1214-1221: the exceptions concern tamed against Players, Dverger or another tamed one); vanilla true / true.</summary>
        public static void Tamed(out bool massToOther, out bool otherToMass)
        {
            massToOther = true;
            otherToMass = true;
        }
    }
}
