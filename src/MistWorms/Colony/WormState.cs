namespace MistWorms.Colony
{
    /// <summary>States of a virtual worm (spec §3.3). All are declared; phase 3 implements Idle, Hunting, Striking, Fleeing, Dead.</summary>
    internal enum WormState : byte
    {
        Sleeping = 0,
        Idle = 1,
        Gathering = 2,
        Hunting = 3,
        Striking = 4,
        Fleeing = 5,
        Absorbing = 7,   // climbing onto a mass for 0.5 s (decisions.md 64: Absorbed and Dead move up by one)
        Absorbed = 8,
        Dead = 9,
    }

    /// <summary>Sub-phase of Striking (spec §3.3 point 5).</summary>
    internal enum StrikePhase : byte
    {
        Windup = 0,
        Strike = 1,
        Recovery = 2,
    }

    /// <summary>What a worm hunts (spec §5.3). Only players exist in phase 3; creatures, tamed animals and structures come with phase 6.</summary>
    internal enum TargetKind : byte
    {
        None = 0,
        Player = 1,
        // Phase 6: Tamed, Creature, Structure.
    }
}
