#if MW_DEV
using System.Collections;
using System.Collections.Generic;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Drops;
using MistWorms.Mass;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// S13 (spec §15.5, §7.4, plan 6-2, A3): a structure eaten, in OPEN terrain (a mine may forbid building). A wooden door is placed 6 m from the player (ghost mode,
    /// no creature around); a mass of 40 targets it (kind Structure), engulfs it and destroys it with its blunt ticks. Four runs:
    /// (1) a door not placed by a player: the pool of the mass holds each recoverable resource of the door divided by three (vanilla, at least 1); (2) a door placed by
    /// a player: the full cost; (3) a door destroyed by the cobra of a lone worm (attackerless hit, "last worm hit" registry): the pool of the WORM holds the third;
    /// (4) a collapse (damage with no hit): NOT captured, the vanilla drops are on the ground.
    /// Assertions of (1) to (3): the door is gone, exactly the expected units (read from Piece.m_resources before the destruction) are in the pool and equal what the patch
    /// logged, and no new item lies on the ground. Needs the free build key off (the vanilla method gives nothing otherwise).
    /// </summary>
    internal sealed class S13_StructureEaten : IDevScenario
    {
        private const int WormCount = 40;
        private const float DoorDistance = 6f;
        private const float DestroyTimeout = 120f;

        public string Name => "S13_StructureEaten";

        public IEnumerator Run(ScenarioContext ctx)
        {
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 420f;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                yield return ctx.WaitFrames(3);
                PatchCheck.CheckDropPatches(ctx);
                GameObject doorPrefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(VanillaNames.WoodDoor) : null;
                if (!ctx.Assert("door_prefab", doorPrefab != null, VanillaNames.WoodDoor, doorPrefab != null ? "found" : "missing")) yield break;

                Player player = Player.m_localPlayer;
                player.SetGodMode(true);
                player.SetGhostMode(false);
                yield return ctx.MoveToOpenGround(12f, null, flatRadius: 12f, flatTolerance: 0.8f, lenientTolerance: 1.5f);   // the door stands 6 m + the radius of the mass ahead: level over all of it
                if (!ctx.LastOpenGround.Found) yield break;
                player = Player.m_localPlayer;
                S18_GroupCoherence.ClearCreatures(ctx, player);
                player.SetGhostMode(true);   // the door is the only target

                yield return Run(ctx, doorPrefab, "plain_", placedByPlayer: false, byWorm: false, collapse: false);
                yield return Run(ctx, doorPrefab, "placed_", placedByPlayer: true, byWorm: false, collapse: false);
                yield return Run(ctx, doorPrefab, "worm_", placedByPlayer: false, byWorm: true, collapse: false);
                yield return Run(ctx, doorPrefab, "collapse_", placedByPlayer: false, byWorm: false, collapse: true);
            }
            finally
            {
                ctx.Console("mw_kill");
                Player p = Player.m_localPlayer;
                if (p != null)
                {
                    p.SetGhostMode(false);
                    p.SetGodMode(true);
                    p.SetHealth(p.GetMaxHealth());
                }
            }
        }

        private static IEnumerator Run(ScenarioContext ctx, GameObject doorPrefab, string tag, bool placedByPlayer, bool byWorm, bool collapse)
        {
            Player player = Player.m_localPlayer;
            Vector3 fwd = Flat(player.transform.forward);
            Vector3 spot = player.transform.position + fwd * (DoorDistance + MassMath.Radius(WormCount));
            if (Physics.Raycast(spot + Vector3.up * 5f, Vector3.down, out RaycastHit floor, 12f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore)) spot = floor.point;

            ctx.Console("mw_kill");
            yield return ctx.WaitFrames(3);
            DropCapture.ResetCounters();
            MassEngulf.ResetCounters();
            PoolRpc.ResetCounters();
            WormDrops.ResetCounters();

            GameObject door = Object.Instantiate(doorPrefab, spot, Quaternion.LookRotation(fwd, Vector3.up));
            yield return ctx.WaitFrames(2);
            Piece piece = door != null ? door.GetComponent<Piece>() : null;
            WearNTear wear = door != null ? door.GetComponent<WearNTear>() : null;
            if (!ctx.Assert(tag + "door_placed", piece != null && wear != null, "a Piece with a WearNTear", piece == null ? "no Piece" : wear == null ? "no WearNTear" : "ok")) yield break;
            if (placedByPlayer)
            {
                piece.m_creator = player.GetPlayerID();   // Piece.SetCreator needs a PlatformUserID (Splatform is not referenced): the field is what GetCreator reads
                door.GetComponent<ZNetView>().GetZDO().Set(ZDOVars.s_creator, piece.m_creator);
            }
            if (ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey()))
            {
                ctx.Assert(tag + "no_free_build_key", false, "the free build global key off", "set: the vanilla method drops nothing");
                yield break;
            }

            // What the vanilla DropResources would hand out for this door (Piece.cs:339-400), read before it is destroyed.
            var expected = new Dictionary<int, int>();
            bool placed = piece.IsPlacedByPlayer();
            foreach (Piece.Requirement requirement in piece.m_resources)
            {
                if (requirement.m_resItem == null || !requirement.m_recover || requirement.m_amount <= 0) continue;
                int hash = Utils.GetPrefabName(requirement.m_resItem.name).GetStableHashCode();
                expected.TryGetValue(hash, out int have);
                expected[hash] = have + CaptureMath.PieceUnits(requirement.m_amount, placed);
            }
            ctx.Log($"{tag}door {door.name}: health {wear.m_health:0}, placed by a player {placed}; expected units {S12_CreatureEaten.Describe(expected)}");
            ctx.Assert(tag + "placed_flag", placed == placedByPlayer, placedByPlayer, placed);
            ctx.Assert(tag + "door_has_resources", expected.Count > 0, "a recoverable requirement", expected.Count);
            HashSet<int> groundBefore = S12_CreatureEaten.GroundItemIds(spot);

            ZNetView view = door.GetComponent<ZNetView>();
            ZDOID doorId = view.GetZDO().m_uid;
            WritheMassState mass = null;
            WormColony colony = null;
            int slot = -1;
            ushort generation = 0;

            if (collapse)
            {
                wear.ApplyDamage(wear.m_health + 1f);   // no HitData: a structural collapse (vanilla WearNTear.UpdateWear does the same)
            }
            else if (byWorm)
            {
                wear.ApplyDamage(wear.m_health - 1f);   // one hit point left: the first cobra strike finishes it
                slot = ColonyDev.SpawnAt(spot - fwd * 1.8f, WormState.Hunting, true, out colony);
                if (!ctx.Assert(tag + "worm_created", slot >= 0 && colony != null, "a worm", slot)) { Destroy(door); yield break; }
                generation = colony.Store.generation[slot];
                colony.Store.SetObjectTarget(slot, TargetKind.Structure, doorId.UserID, doorId.ID);
                colony.Store.lastSeen[slot] = Time.time;
            }
            else
            {
                mass = MassDev.SpawnAt(spot - fwd * (MassMath.Radius(WormCount) + 3f), WormCount, ZDOID.None, freezeAI: false);
                if (!ctx.Assert(tag + "mass_created", mass != null && mass.Valid, "a valid mass", mass != null ? mass.ZdoId.ToString() : "none")) { Destroy(door); yield break; }
                WritheMassAI ai = mass.GetComponent<WritheMassAI>();
                yield return ctx.WaitUntil(() => door == null || ai.CurrentKind == TargetKind.Structure, 20f);
                bool targeted = door == null || ai.CurrentKind == TargetKind.Structure;
                if (!targeted)
                {
                    Vector3 from = mass.transform.position;
                    ctx.Log($"{tag}door not targeted: mass at {from:0.0}, door at {door.transform.position:0.0} ({Vector3.Distance(from, door.transform.position):0.0} m); " +
                            TargetScanner.Diagnose(door, from, from + Vector3.up * 0.8f, MWConfig.Mass.TargetRange.Value, EngulfMath.MaxHeightGap));
                }
                if (!ctx.Assert(tag + "door_targeted", targeted, "the mass targets a Structure", ai.CurrentKind)) { Destroy(door); yield break; }
                yield return ctx.WaitUntil(() => door == null || MassStateCodes.Mode(mass.StateCode) == MassStateCodes.Engulf, 30f);
                bool engulfed = door == null || MassStateCodes.Mode(mass.StateCode) == MassStateCodes.Engulf;
                if (!engulfed)
                    ctx.Log($"{tag}door not engulfed: target {ai.CurrentKind}, mass at {mass.transform.position:0.00}, door at {door.transform.position:0.00}; last refusal: {(string.IsNullOrEmpty(ai.EngulfNote) ? "none (TryBeginEngulf was not reached)" : ai.EngulfNote)}");
                if (!ctx.Assert(tag + "door_engulfed", engulfed, "Engulf", MassStateCodes.Describe(mass.StateCode))) { Destroy(door); yield break; }   // no 120 s wait for a door that nobody eats
            }

            float deadline = Time.time + DestroyTimeout;
            while (Time.time < deadline && door != null)
            {
                if (byWorm && colony != null && colony.Store.IsAlive(slot, generation) && colony.Store.targetKind[slot] == (byte)TargetKind.None)
                {
                    colony.Store.SetObjectTarget(slot, TargetKind.Structure, doorId.UserID, doorId.ID);
                    colony.Store.lastSeen[slot] = Time.time;
                }
                yield return null;
            }
            bool destroyed = door == null;
            ctx.Assert(tag + "door_destroyed", destroyed, $"destroyed within {DestroyTimeout:0} s", destroyed ? "destroyed" : "standing");
            if (!destroyed) { Destroy(door); yield break; }
            ctx.Assert(tag + "blunt_ticks", collapse || byWorm || MassEngulf.StructureTicks > 0, "structure ticks sent by the mass", MassEngulf.StructureTicks);
            yield return ctx.WaitFrames(15);

            int created = S12_CreatureEaten.NewGroundItems(spot, groundBefore, spot);
            if (collapse)
            {
                ctx.Assert(tag + "not_captured", DropCapture.StructureCaptures == 0, 0, DropCapture.StructureCaptures);
                ctx.Assert(tag + "vanilla_drops_on_the_ground", created > 0, "the vanilla items near the door", created);
                yield break;
            }

            ctx.Assert(tag + "captured_once", DropCapture.StructureCaptures == 1, 1, DropCapture.StructureCaptures);
            ctx.Assert(tag + "captured_equals_expected", SameUnits(DropCapture.LastUnits, expected), S12_CreatureEaten.Describe(expected), S12_CreatureEaten.Describe(DropCapture.LastUnits));
            string killer = DropCapture.LastKiller ?? string.Empty;
            ctx.Assert(tag + "captured_for_the_killer", byWorm ? killer.StartsWith("worm ", System.StringComparison.Ordinal) : killer.Contains(mass.ZdoId.ToString()), byWorm ? "a worm" : $"mass {mass.ZdoId}", killer);

            var pool = new MassPool();
            if (byWorm)
            {
                int index = colony.Store.IsAlive(slot, generation) ? colony.Store.poolIndex[slot] : -1;
                if (index >= 0) pool.CopyFrom(colony.Store.Pools.Get(index));
            }
            else
            {
                mass.DevPoolCopy(pool);
            }
            bool exact = S12_CreatureEaten.PoolGrewBy(new MassPool(), pool, expected, out string detail);
            ctx.Assert(tag + "pool_holds_the_resources", exact, S12_CreatureEaten.Describe(expected), detail);
            ctx.Assert(tag + "nothing_on_the_ground", created == 0, "no new ItemDrop near the door", created);
            yield return ctx.Shot("pool");
        }

        /// <summary>The units equal, hash by hash.</summary>
        private static bool SameUnits(Dictionary<int, int> a, Dictionary<int, int> b)
        {
            if (a.Count != b.Count) return false;
            foreach (KeyValuePair<int, int> pair in a)
                if (!b.TryGetValue(pair.Key, out int v) || v != pair.Value) return false;
            return true;
        }

        private static void Destroy(GameObject go)
        {
            if (go == null) return;
            if (ZNetScene.instance != null) ZNetScene.instance.Destroy(go);
            else Object.Destroy(go);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward;
        }
    }
}
#endif
