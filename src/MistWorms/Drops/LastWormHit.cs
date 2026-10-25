using System;
using System.Collections.Generic;

namespace MistWorms.Drops
{
    /// <summary>
    /// The mark of a cobra hit on a creature or a structure (decision 163). The cobra has no attacker (decision 20), so it is recognised by an unused field that
    /// travels with the hit through the vanilla RPC: <c>m_skillRaiseAmount</c> (default 1, read by vanilla only to raise the skill of a player attacker; a cobra hit
    /// has none). The "last worm hit" registry is credited only when the lethal hit, the last one applied to the victim, carries this mark.
    /// </summary>
    internal static class CobraMark
    {
        public const float Value = 0.0625f;

        public static bool Is(float skillRaiseAmount) => Math.Abs(skillRaiseAmount - Value) < 1e-4f;
    }

    /// <summary>
    /// The "last worm hit" registry (spec §7.4, §10.3, plan 6, A2): a cobra strike has no attacker (decision 20), so what a free worm hit last is remembered
    /// here, by the ZDOID of the creature or the structure: which colony, which slot and generation, and when. The patches of the drops consult it when the
    /// victim dies or is destroyed by a hit with no attacker. An entry expires <see cref="Expiry"/> seconds after the strike (each new strike renews it).
    /// Fixed arrays and linear scans over <see cref="Capacity"/> entries, no allocation. Pure: unit tested.
    /// </summary>
    internal sealed class LastWormHitRegistry
    {
        public const float Expiry = 2f;
        public const int Capacity = 64;

        private readonly ZDOID[] _key = new ZDOID[Capacity];
        private readonly ZDOID[] _colony = new ZDOID[Capacity];
        private readonly int[] _slot = new int[Capacity];
        private readonly int[] _generation = new int[Capacity];
        private readonly float[] _time = new float[Capacity];
        private readonly bool[] _used = new bool[Capacity];

        public int Count
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Capacity; i++) if (_used[i]) n++;
                return n;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < Capacity; i++) _used[i] = false;
        }

        /// <summary>Notes that worm (<paramref name="slot"/>, <paramref name="generation"/>) of <paramref name="colony"/> struck <paramref name="victim"/> at <paramref name="now"/>.</summary>
        public void Record(ZDOID victim, ZDOID colony, int slot, int generation, float now)
        {
            if (victim == ZDOID.None) return;
            int at = -1, free = -1, oldest = -1;
            for (int i = 0; i < Capacity; i++)
            {
                if (!_used[i] || now - _time[i] > Expiry)
                {
                    if (free < 0) free = i;
                    _used[i] = false;
                    continue;
                }
                if (_key[i] == victim) at = i;
                if (oldest < 0 || _time[i] < _time[oldest]) oldest = i;
            }
            if (at < 0) at = free >= 0 ? free : oldest;   // full of live entries: the oldest one is replaced
            _used[at] = true;
            _key[at] = victim;
            _colony[at] = colony;
            _slot[at] = slot;
            _generation[at] = generation;
            _time[at] = now;
        }

        /// <summary>The worm that last struck <paramref name="victim"/> less than <see cref="Expiry"/> seconds before <paramref name="now"/>.</summary>
        public bool TryGet(ZDOID victim, float now, out ZDOID colony, out int slot, out int generation)
        {
            colony = ZDOID.None;
            slot = -1;
            generation = 0;
            if (victim == ZDOID.None) return false;
            for (int i = 0; i < Capacity; i++)
            {
                if (!_used[i] || _key[i] != victim) continue;
                if (now - _time[i] > Expiry)
                {
                    _used[i] = false;
                    return false;
                }
                colony = _colony[i];
                slot = _slot[i];
                generation = _generation[i];
                return true;
            }
            return false;
        }

        /// <summary>Forgets the entry of <paramref name="victim"/> (consumed by a capture).</summary>
        public void Remove(ZDOID victim)
        {
            for (int i = 0; i < Capacity; i++)
                if (_used[i] && _key[i] == victim) _used[i] = false;
        }
    }

    /// <summary>
    /// Creatures whose loot was just captured (plan 6, A13): a second <c>GenerateDropList</c> for the same creature (the ragdoll rolls the list, and a creature
    /// whose drops stay enabled rolls it once more) is emptied too, without being credited a second time. A small ring with a time window. Pure.
    /// </summary>
    internal sealed class CaptureBook
    {
        public const float Window = 10f;
        public const int Capacity = 16;

        private readonly ZDOID[] _key = new ZDOID[Capacity];
        private readonly float[] _time = new float[Capacity];
        private int _next;

        public void Clear()
        {
            for (int i = 0; i < Capacity; i++) _key[i] = ZDOID.None;
            _next = 0;
        }

        public void Mark(ZDOID victim, float now)
        {
            _key[_next] = victim;
            _time[_next] = now;
            _next = (_next + 1) % Capacity;
        }

        public bool Seen(ZDOID victim, float now)
        {
            if (victim == ZDOID.None) return false;
            for (int i = 0; i < Capacity; i++)
                if (_key[i] == victim && now - _time[i] <= Window) return true;
            return false;
        }
    }

    /// <summary>Counting rules of the captured loot (spec §7.4), pure so they are unit tested.</summary>
    internal static class CaptureMath
    {
        /// <summary>
        /// Whether a lethal hit may be credited to the worm of the "last worm hit" registry (decision 163): it has no attacker, it is a cobra hit (so the last hit
        /// applied to the victim is the cobra: a burn, a poison, a fall or a drowning that comes after carries no mark) and the registry has a live entry.
        /// </summary>
        public static bool CreditsRegistry(bool hasAttacker, bool lethalHitIsCobra, bool registryHasEntry) =>
            !hasAttacker && lethalHitIsCobra && registryHasEntry;

        /// <summary>Removes in place (order kept, no allocation) the entries that satisfy <paramref name="match"/>; returns how many were removed.</summary>
        public static int RemoveWhere<T>(List<T> list, Func<T, bool> match)
        {
            if (list == null) return 0;
            int write = 0;
            for (int read = 0; read < list.Count; read++)
            {
                T item = list[read];
                if (match(item)) continue;
                if (write != read) list[write] = item;
                write++;
            }
            int removed = list.Count - write;
            if (removed > 0) list.RemoveRange(write, removed);
            return removed;
        }

        /// <summary>
        /// What <c>Piece.DropResources</c> gives of a requirement of <paramref name="amount"/>: the vanilla "not placed by a player" rule keeps a third, at
        /// least 1 (Piece.cs:365-368). Feast scaling and drop conversion are applied before, by the caller.
        /// </summary>
        public static int PieceUnits(int amount, bool placedByPlayer)
        {
            if (amount <= 0) return 0;
            return placedByPlayer ? amount : Math.Max(1, amount / 3);
        }

        /// <summary>Adds the units of a drop (prefab hash, count) to a list of captured items; false if nothing was added.</summary>
        public static bool AddUnits(ConsumedList list, int hash, int units)
        {
            if (list == null || units <= 0) return false;
            return list.Add(hash, units);
        }

        /// <summary>Whole units of a captured list, for the checks and the log.</summary>
        public static int TotalUnits(ConsumedList list)
        {
            int n = 0;
            for (int i = 0; list != null && i < list.Count; i++) n += (int)Math.Round(list.Qty[i]);
            return n;
        }
    }
}
