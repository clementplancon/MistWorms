using System;

namespace MistWorms.Mass
{
    /// <summary>
    /// Health model of a writhing mass (spec §4.2, plan 4B-1). The vanilla health of the Character is the truth of the owner: after every
    /// hit the number of worms is the ceiling of health / Worm.Health and the partial damage is what is missing to the next whole worm.
    /// Pure functions, so the rounding rules are tested out of game.
    /// </summary>
    public static class MassHealth
    {
        /// <summary>Guards the ceiling against float noise: 340 / 10 may come back as 34.000004.</summary>
        public const float CountEpsilon = 1e-4f;

        /// <summary>Tolerance when comparing a stored health with the expected one.</summary>
        public const float HealthTolerance = 1e-3f;

        /// <summary>Worms left for a given health: ceil(health / perWorm - 1e-4). Zero or less health gives 0.</summary>
        public static int CountFor(float health, float perWorm)
        {
            if (health <= 0f || perWorm <= 0f) return 0;
            return (int)Math.Ceiling(health / perWorm - CountEpsilon);
        }

        /// <summary>Damage already dealt toward the next worm: n * perWorm - health, kept in [0, perWorm).</summary>
        public static float PartialFor(int n, float health, float perWorm)
        {
            float partial = n * perWorm - health;
            if (partial < 0f) return 0f;
            return partial >= perWorm ? 0f : partial;
        }

        /// <summary>Health of a mass holding <paramref name="n"/> worms with <paramref name="partial"/> damage already taken.</summary>
        public static float HealthFor(int n, float partial, float perWorm) => n * perWorm - partial;

        public static float MaxHealthFor(int n, float perWorm) => n * perWorm;
    }

    /// <summary>
    /// Layout of <c>mw_state</c> (spec §4.10, §11.3): the low nibble is the mode, 0x10 and 0x20 are the transient states of the
    /// merge and dissolve transitions (render only, phase 4B-4), and 0x80 is the Stream bit (a shape, not a state).
    /// </summary>
    public static class MassStateCodes
    {
        public const byte Idle = 0;
        public const byte Hunt = 1;
        public const byte Engulf = 2;
        public const byte Dormant = 3;

        public const byte ModeMask = 0x0F;
        public const byte Merging = 0x10;
        public const byte Dissolving = 0x20;
        public const byte Stream = 0x80;

        public static byte Mode(byte code) => (byte)(code & ModeMask);

        public static bool IsStream(byte code) => (code & Stream) != 0;

        public static bool IsMerging(byte code) => (code & Merging) != 0;

        public static bool IsDissolving(byte code) => (code & Dissolving) != 0;

        public static byte With(byte code, byte flag, bool on) => on ? (byte)(code | flag) : (byte)(code & ~flag);

        public static byte WithMode(byte code, byte mode) => (byte)((code & ~ModeMask) | (mode & ModeMask));

        public static string Describe(byte code)
        {
            string mode;
            switch (Mode(code))
            {
                case Idle: mode = "Idle"; break;
                case Hunt: mode = "Hunt"; break;
                case Engulf: mode = "Engulf"; break;
                case Dormant: mode = "Dormant"; break;
                default: mode = "Mode" + Mode(code); break;
            }
            if (IsMerging(code)) mode += "+Merging";
            if (IsDissolving(code)) mode += "+Dissolving";
            if (IsStream(code)) mode += "+Stream";
            return mode;
        }
    }
}
