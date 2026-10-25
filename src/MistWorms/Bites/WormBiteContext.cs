namespace MistWorms.Bites
{
    /// <summary>
    /// Marks the synchronous <c>player.Damage(hit)</c> call of a worm bite so the damage patches can recognise it (spec §6.4, §10.3):
    /// <c>Character.Damage</c> to <c>RPC_Damage</c> to <c>ApplyDamage</c> all run inside the call when the target is the local player
    /// (an RPC to oneself is handled synchronously). Outside such a call every patch stays inert. Main thread only.
    /// </summary>
    internal static class WormBiteContext
    {
        public static bool Active { get; private set; }
        public static Character Target { get; private set; }

        /// <summary>Durability points the bite asks for before blocking (Worm.CobraDurability for a cobra strike).</summary>
        public static float P { get; private set; }

        /// <summary>Health damage the bite carries before blocking and resistances; the block ratio is measured against it.</summary>
        public static float HpDamageOriginal { get; private set; }

        /// <summary>Worms behind the bite (1 for a cobra strike, n for the aggregated tick of a mass): drives the number of pieces bitten.</summary>
        public static int WormCount { get; private set; } = 1;

        /// <summary>The mass that sent the tick (ZDOID.None for a cobra strike).</summary>
        public static ZDOID Mass { get; private set; }

        /// <summary>The bite was parried perfectly (a postfix of the vanilla block, plan 5, A11): the tick is cancelled and the next one delayed.</summary>
        public static bool PerfectParry { get; private set; }

        public static void MarkPerfectParry() => PerfectParry = true;

#if MW_DEV
        // Debug counters read by the scenarios (P3_Cobra).
        public static int Bites;
        public static float DurabilityEaten;
        public static float HealthDamage;
        public static int HealthBites;
        public static int Texts;

        public static void ResetCounters()
        {
            Texts = 0;
            Bites = 0;
            DurabilityEaten = 0f;
            HealthDamage = 0f;
            HealthBites = 0;
        }
#endif

        public static void Begin(Character target, float p, float hpDamageOriginal, int wormCount = 1, ZDOID mass = default)
        {
            Active = true;
            Target = target;
            P = p;
            HpDamageOriginal = hpDamageOriginal;
            WormCount = System.Math.Max(1, wormCount);
            Mass = mass;
            PerfectParry = false;
        }

        public static void End()
        {
            Active = false;
            Target = null;
            P = 0f;
            HpDamageOriginal = 0f;
            WormCount = 1;
            Mass = ZDOID.None;
            PerfectParry = false;
        }
    }
}
