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

        // Localization tokens of the mass (spec §4.3).
        public const string MassNameToken = "$mw_mass";
        public const string MassCountToken = "$mw_mass_count";   // "{0} ({1})": name, number of worms

        // ZDO keys of the colony (spec §11.3/§11.4). Phase 3 only persists the kind and the box; the snapshot comes in phase 7.
        public const string ZdoKind = "mw_kind";               // int: WormColony.ColonyKind
        public const string ZdoBoxCenter = "mw_box_c";         // Vector3, world space (spec §8.4)
        public const string ZdoBoxExtents = "mw_box_e";        // Vector3, half size

        // ZDO keys of a writhing mass (spec §11.3, plan 4B-1). The vanilla health of the Character is the source of truth of the owner;
        // mw_n and mw_partial are derived from it after every hit and read by every peer (name, render, loss effects).
        public const string ZdoMassN = "mw_n";                 // int: worms in the mass
        public const string ZdoMassPartial = "mw_partial";     // float: damage already dealt toward the next worm (0 <= partial < Worm.Health)
        public const string ZdoMassState = "mw_state";         // byte: MassStateCodes (mode + transition + Stream bit)
        public const string ZdoMassColony = "mw_colony";       // ZDOID of the colony the worms come from and return to
        public const string ZdoMassBorn = "mw_born";           // long: network time (ticks) of the birth, for the growth animation
        public const string ZdoMassPool = "mw_pool";           // byte[]: loot pool, empty until phase 6
        public const string ZdoMassTarget = "mw_target";       // ZDOID of the creature the mass hunts (ZDOID.None = none)
        public const string ZdoMassStreamWidth = "mw_sw";      // float: width of the ribbon while the Stream bit is set (spec §4.7)

        // RPC names (spec §11.3). Part of the wire protocol: never rename after release.
        public const string RpcWormHit = "MW_WormHit";         // ZNetView RPC to the colony owner: int slot, int generation, ZPackage HitData
        public const string RpcWormBite = "MW_WormBite";       // routed RPC to the owner of the bitten player: ZDOID, durability, health damage, point, dir
        public const string RpcMassAdd = "MW_MassAdd";         // ZNetView RPC to the mass owner: int k worms to add
        public const string RpcMassDissolve = "MW_Dissolve";   // ZNetView RPC to the colony owner: ZDOID mass, int n, Vector3 position, ZPackage pool
    }
}
