#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Drops;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// Spec §15.5 S01: a lone worm and a bronze sword. Part A: five real melee swings (frozen simulation, worm 1.5 m ahead) each kill
    /// in one hit with exactly one hit received and no item but native drops. Part B: 200 kills by injected hits (35 slash, through the hit
    /// proxy) with a seeded drop generator; each native drop rate within 3 points of its probability and no pool item.
    /// Parameters: reps (200), meleeReps (5).
    /// </summary>
    internal sealed class S01_SingleWorm : IDevScenario
    {
        private const float SpawnDistance = 1.5f;
        private const float SwordSlash = 35f;     // SwordBronze, dump section 4

        public string Name => "S01_SingleWorm";

        // ItemDrops that existed before the scenario created anything: whatever else appears is ours to remove.
        private HashSet<ItemDrop> _baseline;

        public IEnumerator Run(ScenarioContext ctx)
        {
            int meleeReps = ctx.ParamInt("meleeReps", 5);
            int reps = ctx.ParamInt("reps", 200);
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 150f + meleeReps * 8f + reps * 0.2f;

            bool autoPickup = Player.m_enableAutoPickup;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                yield return ctx.WaitFrames(3); // a colony destroyed by mw_kill leaves the registry at the end of the frame

                yield return ctx.MoveToOpenGround(5f);
                if (!ctx.LastOpenGround.Found) yield break;

                Player player = Player.m_localPlayer;
                player.SetGodMode(true);
                player.SetGhostMode(false);
                Player.m_enableAutoPickup = false; // drops stay where they fall: the scenario removes them itself
                ctx.ClearInventory();
                ctx.Give(VanillaNames.SwordBronze, equip: true);
                yield return ctx.Wait(1f);

                ctx.Console("mw_freeze on");
                _baseline = SnapshotItems();
                yield return Melee(ctx, meleeReps);
                yield return Statistics(ctx, reps);
            }
            finally
            {
                Player.m_enableAutoPickup = autoPickup;
                WormDrops.ClearTestSeed();
                ctx.Console("mw_freeze off");
                ctx.Console("mw_kill");
                if (_baseline != null) RemoveNewItems(_baseline);
                ctx.ClearInventory();
            }
        }

        // ------------------------------------------------------------------ part A

        private IEnumerator Melee(ScenarioContext ctx, int count)
        {
            Player player = Player.m_localPlayer;
            int passed = 0;

            for (int rep = 1; rep <= count; rep++)
            {
                yield return ctx.WaitUntil(() => player == null || !player.InAttack(), 4f);

                Vector3 forward = player.transform.forward;
                forward.y = 0f;
                forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
                Vector3 spot = player.transform.position + forward * SpawnDistance;

                int slot = ColonyDev.SpawnAt(spot, WormState.Idle, false, out WormColony colony);
                if (slot < 0)
                {
                    ctx.Assert($"melee_{rep}", false, "worm spawned", "no floor/colony");
                    continue;
                }
                ushort generation = colony.Store.generation[slot];
                yield return ctx.WaitFrames(3); // the proxy attaches in the next LateUpdate

                bool proxied = ProxyManager.Existing != null && ProxyManager.Existing.ProxyFor(colony, slot) != null;
                colony.ResetCounters();
                WormDrops.ResetCounters();


                ctx.ResetPlayerState();
                bool started = ColonyDev.StartAttack(player, VanillaNames.SwordBronze, colony.Store.pos[slot], out _, out string error);
                yield return ctx.Wait(1.2f);

                bool dead = !colony.Store.IsAlive(slot, generation);
                int hits = colony.HitsReceived;
                float damage = colony.LastHitDamage;
                int foreign = RemoveNewItems(_baseline);

                bool ok = proxied && started && dead && hits == 1 && damage >= MWConfig.Worm.Health.Value && foreign == 0;
                ctx.Assert($"melee_{rep}", ok, "proxy, attack started, dead, 1 hit, damage >= Worm.Health, no foreign item",
                    $"proxy={proxied} started={started}{(error != null ? " (" + error + ")" : string.Empty)} dead={dead} hits={hits} damage={damage:0.#} foreignItems={foreign}");
                if (ok) passed++;

                yield return ctx.Wait(0.4f);
            }
            ctx.Assert("melee_kills", passed == count, $"{count}/{count}", $"{passed}/{count}");
        }

        // ------------------------------------------------------------------ part B

        private IEnumerator Statistics(ScenarioContext ctx, int reps)
        {
            Player player = Player.m_localPlayer;
            yield return ctx.WaitUntil(() => player == null || !player.InAttack(), 4f);

            // The colony must exist before seeding its drop generator.
            ColonyDev.FindOrCreateColony(player, out _);
            WormDrops.SeedForTests(NativeDrops.ScenarioSeed);
            WormDrops.ResetCounters();

            int killed = 0, viaProxy = 0, failedSpawns = 0;
            for (int rep = 0; rep < reps; rep++)
            {
                Vector3 forward = player.transform.forward;
                forward.y = 0f;
                forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
                int slot = ColonyDev.SpawnAt(player.transform.position + forward * SpawnDistance, WormState.Idle, false, out WormColony colony);
                if (slot < 0)
                {
                    failedSpawns++;
                    yield return null;
                    continue;
                }
                ushort generation = colony.Store.generation[slot];
                yield return null; // proxy attachment (LateUpdate)

                var hit = new HitData();
                hit.m_damage.m_slash = SwordSlash;
                hit.SetAttacker(player);
                hit.m_hitType = HitData.HitType.PlayerHit;
                hit.m_point = colony.Store.pos[slot] + Vector3.up * 0.1f;
                hit.m_dir = forward;
                if (ColonyDev.HitWorm(colony, slot, hit)) viaProxy++;
                if (!colony.Store.IsAlive(slot, generation)) killed++;

                RemoveNewItems(_baseline); // the native drops of this kill, counted by WormDrops
            }

            float baitRate = WormDrops.BaitRolled / (float)reps;
            float entrailsRate = WormDrops.Entrails / (float)reps;
            float bloodbagRate = WormDrops.Bloodbag / (float)reps;
            ctx.Log($"{reps} kills: {killed} dead, {viaProxy} through a proxy, {failedSpawns} failed spawns; rolls {WormDrops.Rolls}, bait {WormDrops.BaitRolled} " +
                    $"({baitRate:P1}), entrails {WormDrops.Entrails} ({entrailsRate:P1}), bloodbag {WormDrops.Bloodbag} ({bloodbagRate:P1}), items created {WormDrops.ItemsSpawned}");
            ctx.Log("with the fixed seed and an unbroken run the unit test expects 17 / 16 / 8");

            ctx.Assert("stats_all_killed", killed == reps, reps, killed);
            ctx.Assert("stats_through_proxy", viaProxy == reps, reps, viaProxy);
            ctx.Assert("stats_bait_rate", Mathf.Abs(baitRate - MWConfig.Drops.BaitChance.Value) <= 0.03f, $"{MWConfig.Drops.BaitChance.Value:P0} +- 3 points", $"{baitRate:P1}");
            ctx.Assert("stats_entrails_rate", Mathf.Abs(entrailsRate - MWConfig.Drops.EntrailsChance.Value) <= 0.03f, $"{MWConfig.Drops.EntrailsChance.Value:P0} +- 3 points", $"{entrailsRate:P1}");
            ctx.Assert("stats_bloodbag_rate", Mathf.Abs(bloodbagRate - MWConfig.Drops.BloodbagChance.Value) <= 0.03f, $"{MWConfig.Drops.BloodbagChance.Value:P0} +- 3 points", $"{bloodbagRate:P1}");
            ctx.Assert("stats_no_pool_drop", WormDrops.PoolItems == 0, 0, WormDrops.PoolItems);
        }

        // ------------------------------------------------------------------ item bookkeeping

        private static HashSet<ItemDrop> SnapshotItems()
        {
            var set = new HashSet<ItemDrop>();
            foreach (ItemDrop item in ItemDrop.s_instances)
                if (item != null) set.Add(item);
            return set;
        }

        private static bool IsNative(string prefab) =>
            prefab == VanillaNames.Entrails || prefab == VanillaNames.Bloodbag
            || (prefab != null && prefab.StartsWith(VanillaNames.FishingBaitPrefix, StringComparison.Ordinal));

        /// <summary>Destroys the ItemDrops that did not exist in <paramref name="before"/> and returns how many of them were not native worm drops.</summary>
        private static int RemoveNewItems(HashSet<ItemDrop> before)
        {
            var created = new List<ItemDrop>();
            foreach (ItemDrop item in ItemDrop.s_instances)
            {
                if (item == null) continue;
                if (before != null && before.Contains(item)) continue;
                created.Add(item);
            }

            int foreign = 0;
            foreach (ItemDrop item in created)
            {
                if (!IsNative(NameOf(item))) foreign++;
                if (ZNetScene.instance != null) ZNetScene.instance.Destroy(item.gameObject);
                else UnityEngine.Object.Destroy(item.gameObject);
            }
            return foreign;
        }

        private static string NameOf(ItemDrop item) => Utils.GetPrefabName(item.gameObject);
    }
}
#endif
