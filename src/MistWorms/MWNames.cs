namespace MistWorms
{
    /// <summary>
    /// Identifiers the mod itself defines (prefab names, ZDO keys). Vanilla names live in <see cref="VanillaNames"/>.
    /// A prefab name is hashed into saved ZDOs: never rename one after release (docs/decisions.md 1.3).
    /// </summary>
    internal static class MWNames
    {
        public const string ColonyPrefab = "MW_WormColony";   // spec §2

        // ZDO keys of the colony (spec §11.3/§11.4). Phase 3 only persists the kind and the box; the snapshot comes in phase 7.
        public const string ZdoKind = "mw_kind";               // int: WormColony.ColonyKind
        public const string ZdoBoxCenter = "mw_box_c";         // Vector3, world space (spec §8.4)
        public const string ZdoBoxExtents = "mw_box_e";        // Vector3, half size

        // RPC names (spec §11.3). Part of the wire protocol: never rename after release.
        public const string RpcWormHit = "MW_WormHit";         // ZNetView RPC to the colony owner: int slot, int generation, ZPackage HitData
        public const string RpcWormBite = "MW_WormBite";       // routed RPC to the owner of the bitten player: ZDOID, durability, health damage, point, dir
    }
}
