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
    /// S12 (spec §15.5, §7.4, plan 6-2, A2, A13): a creature eaten. In a real mine, a Seeker is spawned next to an active mass (n = 40, the player in ghost mode so that
    /// the Seeker is the only target) WITHOUT removing the creatures of the mine (the explicit exception to the removal rule). The mass engulfs it; the Seeker takes damage
    /// and strikes back (the vanilla retaliation: the partial damage of the mass grows, or the last attacker of the mass is the Seeker). Worm.Health is raised to 1000 and
    /// Worm.CreatureDamagePerBite lowered during the run so that the fight lasts and no worm dies (a dying worm would drop its share of the pool and blur the count).
    /// At the Seeker's death: no item on the ground (the ragdoll is waited for), the loot list captured by the GenerateDropList postfix is logged, and the pool of the
    /// mass grew by exactly that list. Up to <see cref="MaxSeekers"/> Seekers are tried until a non empty list was captured.
    /// Second part: a free worm kills a Seeker with the cobra (attackerless hit, "last worm hit" registry): the captured list lands in the pool of the worm.
    /// Parameters: where (mine, default; open to run in the open when no mine is reachable).
    /// </summary>
    internal sealed class S12_CreatureEaten : IDevScenario
    {
        private const int WormCount = 40;
        private const string Seeker = VanillaNames.Seeker;
        private const int MaxSeekers = 5;
        private const float KillTimeout = 90f;
        internal const float GroundRadius = 12f;

        public string Name => "S12_CreatureEaten";

        public IEnumerator Run(ScenarioContext ctx)
        {
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 420f;
            string where = ctx.Param("where", "mine");
            float savedWormHealth = MWConfig.Worm.Health.Value;
            float savedCreatureBite = MWConfig.Worm.CreatureDamagePerBite.Value;
            int savedThreshold = MWConfig.Mass.DissolveThreshold.Value;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                yield return ctx.WaitFrames(3);
                PatchCheck.CheckDropPatches(ctx);
                GameObject seekerPrefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(Seeker) : null;
                if (!ctx.Assert("seeker_prefab", seekerPrefab != null, Seeker, seekerPrefab != null ? "found" : "missing")) yield break;

                MWConfig.Worm.Health.Value = 1000f;
                MWConfig.Worm.CreatureDamagePerBite.Value = 0.25f;
                MWConfig.Mass.DissolveThreshold.Value = 1;

                Player player = Player.m_localPlayer;
                Vector3 site = default;
                float gap = MassMath.Radius(WormCount) + 2f;
                if (where == "mine")
                {
                    player.SetGodMode(true);
                    yield return ctx.TpMineRetry();
                    if (!ctx.LastTpMine.Ok) yield break;
                    yield return ctx.Wait(1f);
                    float rMax = MassMath.Radius(WormCount);
                    var found = new MineSiteResult();
                    yield return MineSite.Find(Player.m_localPlayer.transform.position, 1.5f * rMax + 2.5f, MineSite.AbsoluteMinClearance, found);
                    ctx.Assert("mine_site_found", found.Found, "flat floor", found.Found ? found.Floor.ToString() : found.Error);
                    if (!found.Found) yield break;
                    site = found.Floor;
                }
                else
                {
                    yield return ctx.MoveToOpenGround(10f);
                    if (!ctx.LastOpenGround.Found) yield break;
                }

                player = Player.m_localPlayer;
                player.SetGodMode(true);
                player.SetGhostMode(true);   // no player to hunt: the Seeker is the only target, and it does not hunt the player either
                Vector3 fwd = Flat(player.transform.forward);
                if (where == "mine")
                {
                    var back = new TeleportOutcome();
                    yield return DevWorld.TeleportPlayer(site - fwd * (0.5f * gap) + Vector3.up * 0.3f, Quaternion.LookRotation(fwd, Vector3.up), false, 30f, back);
                    if (!back.Ok) { ctx.Log("could not place the player in the mine: " + back.Error); yield break; }
                    player = Player.m_localPlayer;
                    player.SetGodMode(true);
                    player.SetGhostMode(true);
                }
                else
                {
                    site = player.transform.position + fwd * 6f;
                }
                // The creatures of the mine stay (explicit exception to the removal rule of spec 15.5): S12 keeps its Seeker, and the mine keeps the others.
                ctx.Log($"context: {(where == "mine" ? "infested mine" : "open ground")}; vanilla creatures near the player are NOT removed");

                MassEngulf.ResetCounters();
                PoolRpc.ResetCounters();
                WormDrops.ResetCounters();
                DropCapture.ResetCounters();

                WritheMassState mass = MassDev.SpawnAt(site, WormCount, ZDOID.None, freezeAI: false);
                if (!ctx.Assert("mass_created", mass != null && mass.Valid, "a valid mass", mass != null ? mass.ZdoId.ToString() : "none")) yield break;
                WritheMassAI ai = mass.GetComponent<WritheMassAI>();

                bool sawRiposte = false;
                bool anyNonEmpty = false;
                for (int k = 1; k <= MaxSeekers && !anyNonEmpty; k++)
                {
                    string tag = "seeker" + k + "_";
                    var before = new MassPool();
                    mass.DevPoolCopy(before);
                    var groundBefore = GroundItemIds(site);
                    DropCapture.ResetCounters();
                    MassEngulf.ResetCounters();

                    Vector3 at = SeekerSpot(site, fwd, 5f, k);
                    Character seeker = SpawnSeeker(seekerPrefab, at);
                    if (!ctx.Assert(tag + "spawned", seeker != null, "a Seeker next to the mass", seeker != null ? at.ToString() : "failed")) yield break;
                    float maxHealth = seeker.GetMaxHealth();
                    float lowest = maxHealth;
                    float partialBefore = mass.Partial;
                    float deadline = Time.time + KillTimeout;
                    Vector3 deathAt = at;
                    while (Time.time < deadline && seeker != null && !seeker.IsDead())
                    {
                        deathAt = seeker.transform.position;
                        lowest = Mathf.Min(lowest, seeker.GetHealth());
                        if (mass != null && mass.Valid) mass.DevPoolCopy(before);   // the last pool seen before the kill: shares that fell with worms killed by the riposte are already out of it
                        if (mass != null && mass.Valid && (mass.Partial > partialBefore + 0.01f || (mass.LastAttacker ?? string.Empty).Contains(Seeker))) sawRiposte = true;
                        yield return null;
                    }
                    bool dead = seeker == null || seeker.IsDead();
                    ctx.Assert(tag + "killed", dead, $"dead within {KillTimeout:0} s", dead ? "dead" : "alive");
                    ctx.Assert(tag + "took_damage", lowest < maxHealth, $"health under {maxHealth:0}", lowest);
                    ctx.Assert(tag + "targeted_by_the_mass", ai.CurrentKind == TargetKind.Creature || DropCapture.CreatureCaptures > 0 || MassEngulf.CreatureTicks > 0, "the mass hunted a creature", $"{ai.CurrentKind}, {MassEngulf.CreatureTicks} creature ticks");
                    if (!dead) yield break;

                    yield return ctx.WaitFrames(5);
                    // The vanilla riposte of the Seeker may kill a few worms (spec 5.4, battlefield): the mass must survive, not keep all 40.
                    ctx.Assert(tag + "mass_survives_the_riposte", mass != null && mass.Valid && mass.N > 0, $"alive (started with {WormCount})", mass != null && mass.Valid ? mass.N : -1);
                    ctx.Assert(tag + "captured_once", DropCapture.CreatureCaptures == 1, 1, DropCapture.CreatureCaptures);
                    ctx.Assert(tag + "captured_for_the_mass", (DropCapture.LastKiller ?? string.Empty).Contains(mass.ZdoId.ToString()), $"mass {mass.ZdoId}", DropCapture.LastKiller);

                    // The pool grew by exactly the list the patch captured (units by item).
                    var after = new MassPool();
                    mass.DevPoolCopy(after);
                    bool exact = PoolGrewBy(before, after, DropCapture.LastUnits, out string detail);
                    ctx.Assert(tag + "pool_grew_by_the_captured_list", exact, Describe(DropCapture.LastUnits), detail);
                    if (DropCapture.LastUnits.Count > 0) anyNonEmpty = true;

                    // Nothing on the ground: the ragdoll is waited for (its loot would come out when it goes), then no new item is within reach of the death.
                    yield return ctx.WaitUntil(() => NoRagdollNear(deathAt, GroundRadius), 40f);
                    yield return ctx.WaitFrames(30);
                    int created = NewGroundItems(site, groundBefore, deathAt);
                    ctx.Assert(tag + "nothing_on_the_ground", created == 0, "no new ItemDrop within 12 m", created);
                    ctx.Assert(tag + "no_pool_item_dropped", WormDrops.PoolItems == 0, 0, WormDrops.PoolItems);
                }
                ctx.Assert("riposte_seen", sawRiposte, "the Seeker hurt the mass (partial damage or last attacker)", sawRiposte ? "yes" : "no");
                ctx.Assert("captured_list_not_empty_once", anyNonEmpty, $"a non empty list within {MaxSeekers} Seekers", anyNonEmpty ? "yes" : "all empty (the drop table rolled nothing)");
                yield return ctx.Shot("pool");

                // ---- part two: the cobra of a free worm
                yield return WormKills(ctx, seekerPrefab, site, fwd);
            }
            finally
            {
                MWConfig.Worm.Health.Value = savedWormHealth;
                MWConfig.Worm.CreatureDamagePerBite.Value = savedCreatureBite;
                MWConfig.Mass.DissolveThreshold.Value = savedThreshold;
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

        // ------------------------------------------------------------------ the worm part

        private static IEnumerator WormKills(ScenarioContext ctx, GameObject seekerPrefab, Vector3 site, Vector3 fwd)
        {
            ctx.Console("mw_kill");
            yield return ctx.WaitFrames(3);
            DropCapture.ResetCounters();
            PoolRpc.ResetCounters();

            Vector3 at = SeekerSpot(site, fwd, 3f, 0);
            Character seeker = SpawnSeeker(seekerPrefab, at);
            if (!ctx.Assert("worm_seeker_spawned", seeker != null, "a Seeker", seeker != null ? at.ToString() : "failed")) yield break;
            seeker.SetHealth(1.5f);   // two cobra strikes (the pierce damage is halved by the Seeker's resistance)
            ZDOID seekerId = seeker.GetZDOID();

            int slot = ColonyDev.SpawnAt(at + Flat(Vector3.Cross(Vector3.up, fwd)) * 1.5f, WormState.Hunting, true, out WormColony colony);
            if (!ctx.Assert("worm_created", slot >= 0 && colony != null, "a worm", slot)) yield break;
            WormStore store = colony.Store;
            ushort generation = store.generation[slot];
            store.SetObjectTarget(slot, TargetKind.Creature, seekerId.UserID, seekerId.ID);
            store.lastSeen[slot] = Time.time;

            float deadline = Time.time + 45f;
            while (Time.time < deadline && seeker != null && !seeker.IsDead())
            {
                if (store.IsAlive(slot, generation) && store.targetKind[slot] == (byte)TargetKind.None)
                {
                    store.SetObjectTarget(slot, TargetKind.Creature, seekerId.UserID, seekerId.ID);   // keeps the hunt going if a sense tick lost the target
                    store.lastSeen[slot] = Time.time;
                }
                yield return null;
            }
            bool dead = seeker == null || seeker.IsDead();
            ctx.Assert("worm_killed_the_seeker", dead, "dead within 45 s", dead ? "dead" : "alive");
            if (!dead) yield break;
            yield return ctx.WaitFrames(10);

            ctx.Assert("worm_captured_once", DropCapture.CreatureCaptures == 1, 1, DropCapture.CreatureCaptures);
            ctx.Assert("worm_captured_for_a_worm", (DropCapture.LastKiller ?? string.Empty).StartsWith("worm ", System.StringComparison.Ordinal), "a worm", DropCapture.LastKiller);
            var pool = new MassPool();
            int index = store.IsAlive(slot, generation) ? store.poolIndex[slot] : -1;
            if (index >= 0) pool.CopyFrom(store.Pools.Get(index));
            bool exact = PoolGrewBy(new MassPool(), pool, DropCapture.LastUnits, out string detail);
            ctx.Assert("worm_pool_grew_by_the_captured_list", exact, Describe(DropCapture.LastUnits), detail);
        }

        // ------------------------------------------------------------------ helpers

        private static Character SpawnSeeker(GameObject prefab, Vector3 at)
        {
            if (Physics.Raycast(at + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore)) at = hit.point + Vector3.up * 0.2f;
            GameObject go = Object.Instantiate(prefab, at, Quaternion.identity);
            return go != null ? go.GetComponent<Character>() : null;
        }

        private static Vector3 SeekerSpot(Vector3 site, Vector3 fwd, float distance, int k) =>
            site + Quaternion.AngleAxis(60f * k, Vector3.up) * fwd * (distance + MassMath.Radius(WormCount) * (k == 0 ? 0f : 1f));

        /// <summary>True when every unit of <paramref name="captured"/> is in <paramref name="after"/> over <paramref name="before"/>, and nothing else changed (integers).</summary>
        internal static bool PoolGrewBy(MassPool before, MassPool after, Dictionary<int, int> captured, out string detail)
        {
            var hashes = new HashSet<int>(captured.Keys);
            for (int i = 0; i < before.Count; i++) hashes.Add(before.HashAt(i));
            for (int i = 0; i < after.Count; i++) hashes.Add(after.HashAt(i));
            var wrong = new List<string>();
            foreach (int h in hashes)
            {
                captured.TryGetValue(h, out int units);
                float delta = after.Total(h) - before.Total(h);
                if (Mathf.Abs(delta - units) > 0.001f) wrong.Add($"{Prefab(h)} +{delta:0.###} instead of +{units}");
            }
            detail = wrong.Count == 0 ? "equal (" + Describe(captured) + ")" : string.Join("; ", wrong);
            return wrong.Count == 0;
        }

        internal static string Describe(Dictionary<int, int> units)
        {
            if (units.Count == 0) return "an empty list";
            var parts = new List<string>();
            foreach (KeyValuePair<int, int> pair in units) parts.Add($"{Prefab(pair.Key)} x{pair.Value}");
            return string.Join(", ", parts);
        }

        internal static string Prefab(int hash)
        {
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(hash) : null;
            return go != null ? go.name : hash.ToString();
        }

        private static bool NoRagdollNear(Vector3 p, float radius)
        {
            foreach (Ragdoll r in Object.FindObjectsOfType<Ragdoll>())
                if (r != null && (r.transform.position - p).sqrMagnitude <= radius * radius) return false;
            return true;
        }

        /// <summary>Instance ids of the items on the ground around the site.</summary>
        internal static HashSet<int> GroundItemIds(Vector3 around)
        {
            var ids = new HashSet<int>();
            foreach (ItemDrop item in Object.FindObjectsOfType<ItemDrop>())
                if (item != null) ids.Add(item.GetInstanceID());
            return ids;
        }

        /// <summary>Items created since <paramref name="before"/> within <see cref="GroundRadius"/> of the site or of the death.</summary>
        internal static int NewGroundItems(Vector3 site, HashSet<int> before, Vector3 deathAt)
        {
            int n = 0;
            float r2 = GroundRadius * GroundRadius;
            foreach (ItemDrop item in Object.FindObjectsOfType<ItemDrop>())
            {
                if (item == null || before.Contains(item.GetInstanceID())) continue;
                Vector3 p = item.transform.position;
                if ((p - site).sqrMagnitude <= r2 || (p - deathAt).sqrMagnitude <= r2) n++;
            }
            return n;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward;
        }
    }
}
#endif
