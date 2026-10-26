namespace MistWorms
{
    /// <summary>
    /// Identifiers the mod itself defines (prefab names, ZDO keys). Vanilla names live in <see cref="VanillaNames"/>.
    /// A prefab name is hashed into saved ZDOs: never rename one after release (docs/decisions.md 1.3).
    /// </summary>
    internal static class MWNames
    {
        public const string ColonyPrefab = "MW_WormColony";   // spec §2
        public const string MassPrefab = "MW_WritheMass";     // spec §2, §10.1
        public const string NestPrefab = "MW_WormNest";       // spec §2, §10.1

        // Localization tokens of the mass (spec §4.3).
        public const string MassNameToken = "$mw_mass";
        public const string MassCountToken = "$mw_mass_count";   // "{0} ({1})": name, number of worms

        // Status effect applied to an engulfed player (spec §6.2).
        public const string StatusInfested = "MW_SE_Infested";
        public const string StatusInfestedNameToken = "$mw_se_infested_name";
        public const string StatusInfestedDescToken = "$mw_se_infested_desc";

        // Hidden status effect of an engulfed creature (spec §6.5): the slow only, no icon.
        public const string StatusEngulfed = "MW_SE_Engulfed";

        // ZDO keys of the colony (spec §11.3/§11.4). Phase 3 only persists the kind and the box; the snapshot comes in phase 7.
        public const string ZdoKind = "mw_kind";               // int: WormColony.ColonyKind
        public const string ZdoBoxCenter = "mw_box_c";         // Vector3, world space (spec §8.4)
        public const string ZdoBoxExtents = "mw_box_e";        // Vector3, half size
        public const string ZdoSeeded = "mw_seeded";           // int on the colony: 1 once the mine has been seeded, never seeded twice (spec §8.3, plan 7 A7)

        // ZDO keys of a worm nest (spec §10.1, §11.3). The hit points are the vanilla "health" of the Destructible.
        public const string ZdoNestColony = "mw_colony";       // ZDOID of the colony the nest belongs to
        public const string ZdoNestIndex = "mw_nest";          // int: stable index of the nest in its colony (WormStore.nestIndex)
        public const string ZdoNestHit = "mw_nest_hit";        // long: network time (ticks) of the last hit, for the laying pause (Nest.HitPause)

        // ZDO keys of a writhing mass (spec §11.3, plan 4B-1). The vanilla health of the Character is the source of truth of the owner;
        // mw_n and mw_partial are derived from it after every hit and read by every peer (name, render, loss effects).
        public const string ZdoMassN = "mw_n";                 // int: worms in the mass
        public const string ZdoMassPartial = "mw_partial";     // float: damage already dealt toward the next worm (0 <= partial < Worm.Health)
        public const string ZdoMassState = "mw_state";         // byte: MassStateCodes (mode + transition + Stream bit)
        public const string ZdoMassColony = "mw_colony";       // ZDOID of the colony the worms come from and return to
        public const string ZdoMassBorn = "mw_born";           // long: network time (ticks) of the birth, for the growth animation
        public const string ZdoMassPool = "mw_pool";           // byte[]: loot pool (MassPool.Write: version, count, then hash + quantity + carry per entry)
        public const string ZdoMassNests = "mw_nests";         // byte[]: NestCounts.ToBytes, the worms of the mass per nest of origin (spec §8.2); the sum is always n
        public const string ZdoMassTarget = "mw_target";       // ZDOID of the creature the mass hunts (ZDOID.None = none)
        public const string ZdoMassStreamWidth = "mw_sw";      // float: width of the ribbon while the Stream bit is set (spec §4.7)

        // RPC names (spec §11.3). Part of the wire protocol: never rename after release.
        public const string RpcWormHit = "MW_WormHit";         // ZNetView RPC to the colony owner: int slot, int generation, ZPackage HitData
        public const string RpcWormBite = "MW_WormBite";       // routed RPC to the owner of the bitten player: ZDOID, durability, health damage, point, dir
        public const string RpcMassBite = "MW_MassBite";       // routed RPC to the owner of the engulfed player: ZDOID target, ZDOID mass, durability P, health damage, int worms, Vector3 dir
        public const string RpcMassParry = "MW_MassParry";     // routed RPC to the owner of a mass: ZDOID mass (its tick was parried perfectly)
        public const string RpcInfest = "MW_Infest";           // routed RPC to the owner of the engulfed player: ZDOID target, bool on
        public const string RpcMassAdd = "MW_MassAdd";         // ZNetView RPC to the mass owner: int k worms to add, ZPackage pool they bring (MassPool.Write)
        public const string RpcConsumed = "MW_Consumed";       // routed RPC to the mass owner: ZDOID mass, ZPackage (int count <= 16, then int prefab hash + float quantity <= 50 per entry)
        public const string RpcWormConsumed = "MW_WormConsumed"; // routed RPC to the colony owner: ZDOID colony, int slot, int generation, ZPackage (same payload as MW_Consumed)
        public const string RpcLastWormHit = "MW_LastHit";     // routed RPC to the owner of a creature or a structure: ZDOID victim, ZDOID colony, int slot, int generation ("last worm hit" registry, spec §7.4)
        public const string RpcMassDissolve = "MW_Dissolve";   // ZNetView RPC to the colony owner: ZDOID mass, int n, Vector3 position, ZPackage pool
        public const string RpcNestGone = "MW_NestGone";       // ZNetView RPC to the colony owner: int nest index, Vector3 position (the nest was destroyed by a hit: release its worms)
    }
}
