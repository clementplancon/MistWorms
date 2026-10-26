using System;

namespace MistWorms.Colony
{
    /// <summary>
    /// The stable identity of a colony (spec §11.3, decision 211). A ZDOID cannot be that: Valheim hands out new ids to every ZDO it loads from the world file
    /// (<c>ZDO.Load</c>: <c>m_uid.SetID(++ZDOID.m_loadID)</c>) and only remaps its own connection types, so a ZDOID stored in another ZDO is wrong after the next restart.
    /// The key of a mine is a hash of the position of its dungeon generator (one dungeon per place, fixed by the world), so it is the same in every session, on every peer, and
    /// two colonies created for the same mine by two peers at once get the same key. A test colony (dev, outside any dungeon) gets a random one. 0 means "none". Pure: unit tested.
    /// </summary>
    internal static class MineKey
    {
        /// <summary>Metres are quantised to this fraction before hashing, so that the float noise of two reads of one position cannot split a mine in two.</summary>
        private const float StepsPerMetre = 2f;

        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        /// <summary>The key of the mine whose dungeon sits at this position: positive, never 0.</summary>
        public static long FromPosition(float x, float y, float z)
        {
            ulong h = FnvOffset;
            h = Mix(h, Quantise(x));
            h = Mix(h, Quantise(y));
            h = Mix(h, Quantise(z));
            long key = (long)(h & 0x7FFFFFFFFFFFFFFFUL);
            return key == 0L ? 1L : key;
        }

        /// <summary>A random positive key, for a colony that is not a mine.</summary>
        public static long NewRandom()
        {
            long key = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0) & 0x7FFFFFFFFFFFFFFFL;
            return key == 0L ? 1L : key;
        }

        /// <summary>Short text for the logs.</summary>
        public static string Describe(long key) => key == 0L ? "none" : key.ToString("x");

        private static int Quantise(float metres) => (int)Math.Round(metres * StepsPerMetre, MidpointRounding.AwayFromZero);

        private static ulong Mix(ulong h, int value)
        {
            uint v = unchecked((uint)value);
            for (int b = 0; b < 4; b++)
            {
                h ^= (v >> (8 * b)) & 0xFFu;
                h = unchecked(h * FnvPrime);
            }
            return h;
        }
    }
}
