#if MW_DEV
using System.Collections;
using System.Collections.Generic;
using MistWorms.Bites;
using MistWorms.Config;
using MistWorms.Drops;
using MistWorms.Mass;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// S05 (spec §15.5, plan 6, A1, A18): the loot pool. A player wears the padded cuirass alone (quality 1); a mass of 40 engulfs it until the cuirass has lost
    /// 50 % of its durability (the mass is then frozen, which ends the engulfing). Assertions: the pool of the mass holds exactly the resources of the
    /// recipe at quality 1 (iron and linen thread, no upgrader token) in the amounts durability lost / Dmax x cost x Drops.EquipmentDropRatio.
    /// The pool is then copied into a second, identical mass 25 m away; the first mass dies in one blow, the second worm by worm (39 single worm hits, then
    /// the last). For EACH resource of the recipe the items handed out equal floor(cost x d / Dmax x ratio), in both cases, and no pool is left.
    /// The dissolution threshold is lowered to 1 during the run so that the second mass is not dissolved on its way down; the health damage of a worm is lowered
    /// as in S04 so that the cuirass, not the health, is eaten. Items are counted at the source (<see cref="WormDrops.PoolUnits"/>): the vanilla physics throws
    /// them around, and the player's auto-pickup would take some.
    /// </summary>
    internal sealed class S05_Pool : IDevScenario
    {
        private const int WormCount = 40;
        private const float TestHealthPerWorm = 0.05f;
        private const float StopFraction = 0.5f;
        private const float SecondMassDistance = 25f;

        public string Name => "S05_Pool";

        public IEnumerator Run(ScenarioContext ctx)
        {
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 180f;
            float savedHealthBite = MWConfig.Worm.HealthDamagePerBite.Value;
            int savedThreshold = MWConfig.Mass.DissolveThreshold.Value;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                yield return ctx.WaitFrames(3);
                MWConfig.Worm.HealthDamagePerBite.Value = TestHealthPerWorm;
                MWConfig.Mass.DissolveThreshold.Value = 1;

                Player player = Player.m_localPlayer;
                player.SetGodMode(true);
                player.SetGhostMode(false);
                yield return ctx.MoveToOpenGround(10f);
                if (!ctx.LastOpenGround.Found) yield break;
                player = Player.m_localPlayer;
                S18_GroupCoherence.ClearCreatures(ctx, player);
                player.SetGodMode(false);

                // ---- the set: the padded cuirass alone, and the cost its recipe asks for at quality 1
                ctx.ClearInventory();
                ItemDrop.ItemData cuirass = ctx.Give(VanillaNames.ArmorPaddedCuirass, equip: true);
                if (!ctx.Assert("cuirass_given", cuirass != null && cuirass.m_shared.m_useDurability, "a cuirass with durability", cuirass == null ? "none" : "ok")) yield break;
                Recipe recipe = ObjectDB.instance.GetRecipe(cuirass);
                if (!ctx.Assert("recipe_found", recipe != null && recipe.m_resources != null, "a recipe", recipe == null ? "none" : "ok")) yield break;

                var hashes = new List<int>();
                var costs = new List<int>();
                foreach (Piece.Requirement req in recipe.m_resources)
                {
                    if (req == null || req.m_resItem == null || req.m_upgraderResource) continue;
                    hashes.Add(Utils.GetPrefabName(req.m_resItem.gameObject).GetStableHashCode());
                    costs.Add(ItemCost.Cumulative(req.m_amount, req.m_amountPerLevel, false, cuirass.m_quality, MWConfig.Drops.IncludeUpgradeMaterials.Value));
                    ctx.Log($"recipe resource {req.m_resItem.gameObject.name}: cost {costs[costs.Count - 1]} at quality {cuirass.m_quality}");
                }
                ctx.Assert("recipe_has_iron_and_thread", hashes.Count >= 2, ">= 2 resources (Iron, LinenThread)", hashes.Count);
                ctx.Assert("quality_1", cuirass.m_quality == 1, 1, cuirass.m_quality);

                // ---- the first mass engulfs the player until the cuirass has lost half of its durability
                float dmax = cuirass.GetMaxDurability(cuirass.m_quality);
                PoolRpc.ResetCounters();
                WormDrops.ResetCounters();
                WormBiteContext.ResetCounters();
                Vector3 fwd = Flat(player.transform.forward);
                float gap = MassMath.Radius(WormCount) + 1.2f;
                WritheMassState first = MassDev.SpawnAt(player.transform.position + fwd * gap, WormCount, ZDOID.None, freezeAI: false);
                if (!ctx.Assert("first_mass_created", first != null && first.Valid, "a valid mass", first != null ? first.ZdoId.ToString() : "none")) yield break;
                first.GetComponent<WritheMassAI>().DevForcedTarget = player;

                float stopAt = dmax * (1f - StopFraction);
                float deadline = Time.time + 90f;
                while (Time.time < deadline && cuirass.m_durability > stopAt)
                {
                    player.m_stamina = player.GetMaxStamina();
                    yield return null;
                }
                first.FreezeAI = true;                       // ends the engulfing (and with it the Infested effect)
                yield return ctx.WaitFrames(5);              // the last RPC of a tick has arrived
                float lost = dmax - cuirass.m_durability;
                ctx.Log($"cuirass {cuirass.m_durability:0.#}/{dmax:0.#}: {lost:0.#} points eaten; RPCs sent {PoolRpc.Sent}, received {PoolRpc.Received}, entries ignored {PoolRpc.IgnoredEntries}");
                ctx.Assert("half_eaten", lost >= dmax * StopFraction - 0.01f && lost < dmax * 0.8f, $"{dmax * StopFraction:0.#} to {dmax * 0.8f:0.#} lost", lost);
                ctx.Assert("rpc_received", PoolRpc.Received > 0 && PoolRpc.IgnoredEntries == 0, "ticks received, none ignored", $"{PoolRpc.Received} / {PoolRpc.IgnoredEntries}");

                // ---- the pool of the mass: exactly the resources of the recipe, in the expected amounts
                var pool = new MassPool();
                first.DevPoolCopy(pool);
                ctx.Assert("pool_resource_count", pool.Count == hashes.Count, hashes.Count, pool.Count);
                float ratio = MWConfig.Drops.EquipmentDropRatio.Value;
                var expected = new int[hashes.Count];
                var list = new ConsumedList();
                for (int r = 0; r < hashes.Count; r++)
                {
                    float want = costs[r] * lost / dmax * ratio;
                    expected[r] = MassPool.FloorTol(want);
                    float have = pool.Total(hashes[r]);
                    ctx.Assert("pool_amount_" + Prefab(hashes[r]), Mathf.Abs(have - want) <= 0.02f + 0.001f * want, $"{want:0.###} (cost {costs[r]} x {lost:0.#}/{dmax:0.#} x {ratio})", have);
                    list.Add(hashes[r], have);
                }
                yield return ctx.Shot("pool_filled");

                // ---- a second, identical mass 25 m away with the same pool
                WritheMassState second = SpawnSecond(ctx, player, first, fwd);
                if (!ctx.Assert("second_mass_created", second != null && second.Valid, "a valid mass", second != null ? second.ZdoId.ToString() : "none")) yield break;
                yield return ctx.WaitFrames(3);
                second.DevAddToPool(AsWhole(pool, hashes));
                var check = new MassPool();
                second.DevPoolCopy(check);
                for (int r = 0; r < hashes.Count; r++)
                    ctx.Assert("second_pool_copied_" + Prefab(hashes[r]), Mathf.Abs(check.Total(hashes[r]) - pool.Total(hashes[r])) <= 0.001f, pool.Total(hashes[r]), check.Total(hashes[r]));

                // ---- case 1: the first mass dies in one blow
                float perWorm = MWConfig.Worm.Health.Value;
                WormDrops.ResetCounters();
                first.Character.Damage(NewHit(first, WormCount * perWorm + 500f));
                yield return ctx.WaitFrames(3);
                var oneBlow = new int[hashes.Count];
                for (int r = 0; r < hashes.Count; r++) WormDrops.PoolUnits.TryGetValue(hashes[r], out oneBlow[r]);
                ctx.Log("one blow: " + Describe(hashes, oneBlow));

                // ---- case 2: the second mass dies worm by worm
                WormDrops.ResetCounters();
                int stuck = 0;
                for (int i = 0; i < WormCount - 1; i++)
                {
                    int before = second.N;
                    second.Character.Damage(NewHit(second, perWorm));
                    yield return null;
                    if (second.N != before - 1) stuck++;
                }
                ctx.Assert("worm_by_worm_each_hit_kills_one", stuck == 0 && second.N == 1, "39 hits, one worm each, 1 left", $"{stuck} off, n={second.N}");
                second.Character.Damage(NewHit(second, perWorm * 3f));
                yield return ctx.WaitFrames(3);
                var oneByOne = new int[hashes.Count];
                for (int r = 0; r < hashes.Count; r++) WormDrops.PoolUnits.TryGetValue(hashes[r], out oneByOne[r]);
                ctx.Log("worm by worm: " + Describe(hashes, oneByOne));

                for (int r = 0; r < hashes.Count; r++)
                {
                    string name = Prefab(hashes[r]);
                    ctx.Assert("one_blow_" + name, oneBlow[r] == expected[r], $"floor = {expected[r]}", oneBlow[r]);
                    ctx.Assert("worm_by_worm_" + name, oneByOne[r] == expected[r], $"floor = {expected[r]}", oneByOne[r]);
                }
                ctx.Assert("no_pending_drops", WormDrops.PendingStacks == 0, 0, WormDrops.PendingStacks);
                bool leftover = false;
                foreach (WritheMassState m in new[] { first, second })
                {
                    if (m == null || !m.Valid) continue;
                    var rest = new MassPool();
                    m.DevPoolCopy(rest);
                    if (!rest.IsEmpty) leftover = true;
                }
                ctx.Assert("no_remainder", !leftover, "both pools empty", leftover ? "a pool still holds something" : "empty");
                yield return ctx.WaitFrames(5);
            }
            finally
            {
                DevInput.Release();
                MWConfig.Worm.HealthDamagePerBite.Value = savedHealthBite;
                MWConfig.Mass.DissolveThreshold.Value = savedThreshold;
                ctx.Console("mw_kill");
                ctx.ClearInventory();
                Player p = Player.m_localPlayer;
                if (p != null)
                {
                    p.SetGodMode(true);
                    p.SetHealth(p.GetMaxHealth());
                }
            }
        }

        private static ConsumedList AsWhole(MassPool pool, List<int> hashes)
        {
            var list = new ConsumedList();
            foreach (int h in hashes) list.Add(h, pool.Total(h));
            return list;
        }

        private static string Prefab(int hash)
        {
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(hash) : null;
            return go != null ? go.name : hash.ToString();
        }

        private static string Describe(List<int> hashes, int[] counts)
        {
            var parts = new List<string>();
            for (int r = 0; r < hashes.Count; r++) parts.Add($"{Prefab(hashes[r])} x{counts[r]}");
            return string.Join(", ", parts);
        }

        /// <summary>
        /// The second mass, for sure: along the wanted direction at SecondMassDistance, then 25, 50, 90 degrees to either side and at 1, 0.8 and 0.6 of the distance; a
        /// spot is kept when it has a floor within 2.5 m of the player's height and is 12 m or more from the first mass. The attempt that worked is logged.
        /// </summary>
        private static WritheMassState SpawnSecond(ScenarioContext ctx, Player player, WritheMassState first, Vector3 fwd)
        {
            float[] scales = { 1f, 0.8f, 0.6f };
            float[] angles = { 0f, 25f, -25f, 50f, -50f, 90f, -90f };
            Vector3 origin = player.transform.position;
            int tried = 0;
            foreach (float k in scales)
                foreach (float a in angles)
                {
                    tried++;
                    Vector3 at = origin + Quaternion.AngleAxis(a, Vector3.up) * fwd * (SecondMassDistance * k);
                    WritheMassState m = MassDev.SpawnAt(at, WormCount, ZDOID.None, freezeAI: true);
                    if (m == null) continue;
                    bool level = Mathf.Abs(m.transform.position.y - origin.y) <= 2.5f;
                    bool apart = first == null || !first.Valid || Utils.DistanceXZ(m.transform.position, first.transform.position) >= 12f;
                    if (!level || !apart)
                    {
                        MassDev.Destroy(m);
                        continue;
                    }
                    ctx.Log($"second mass spawned at attempt {tried} (scale {k}, angle {a})");
                    return m;
                }
            ctx.Log($"second mass: no spot in {tried} attempts around {origin}");
            return null;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward;
        }

        /// <summary>A hit with no attacker (vanilla lets it through), blunt (resistance Normal).</summary>
        private static HitData NewHit(WritheMassState mass, float blunt)
        {
            var hit = new HitData();
            hit.m_damage.m_blunt = blunt;
            hit.m_point = mass.transform.position + Vector3.up * 0.5f;
            hit.m_dir = Vector3.forward;
            return hit;
        }
    }
}
#endif
