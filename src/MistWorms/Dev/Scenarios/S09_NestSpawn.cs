#if MW_DEV
using System;
using System.Collections;
using MistWorms.Colony;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// S09 (spec §15.5, §8.1, §8.2): a nest laying with SpawnInterval forced to 2 s. Open ground, a test colony around a nest placed in front of the player.
    /// Assertions: the prefab and the nest object are as specified; +3 worms in 6 s (+-1), laid asleep 1 to 2 m from the nest; laying stops at
    /// Nest.MaxAlivePerNest (lowered for the test, restored at the end) and resumes when it is raised; no egg during Nest.HitPause after a hit, then
    /// laying goes on; a nest that is destroyed releases Nest.ReleaseOnDestroy active worms and drops nothing.
    /// </summary>
    internal sealed class S09_NestSpawn : IDevScenario
    {
        private const float Interval = 2f;
        private const float SettleSeconds = 6f;
        private const float MinEggDistance = 0.9f, MaxEggDistance = 2.3f;

        public string Name => "S09_NestSpawn";

        public IEnumerator Run(ScenarioContext ctx)
        {
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 240f;
            int savedMaxPerNest = MWConfig.Nest.MaxAlivePerNest.Value;
            bool clutterHidden = false;
            bool gatheringBefore = ColonyDev.GatheringEnabled;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                ColonyDev.GatheringEnabled = false;      // the released worms must stay free worms to be counted
                yield return ctx.WaitFrames(3);

                bool prefab = ZNetScene.instance != null && ZNetScene.instance.GetPrefab(MWNames.NestPrefab) != null;
                ctx.Assert("prefab_registered", prefab, MWNames.NestPrefab, prefab ? "registered" : "missing");
                if (!prefab) yield break;

                if (ClutterSystem.instance != null)
                {
                    ClutterSystem.instance.enabled = false;
                    ClutterSystem.instance.ClearAll();
                    clutterHidden = true;
                }
                yield return ctx.MoveToOpenGround(6f);
                if (!ctx.LastOpenGround.Found) yield break;
                Player player = Player.m_localPlayer;
                player.SetGodMode(true);
                player.SetGhostMode(false);
                ctx.GuardCreatures = true;

                WormColony.DevSpawnInterval = Interval;
                WormNest nest = NestDev.PlaceInFront(3f, out WormColony colony, out string how);
                ctx.Log($"nest {nest.Index} placed at {nest.transform.position} in {how} colony {colony.ZdoId}");
                yield return ctx.WaitFrames(5);

                // ---- the object
                ctx.Assert("nest_registered_with_its_colony", nest.Colony == colony && colony.NestAt(nest.Index) == nest && colony.NestsLoaded == 1, "the colony knows the nest",
                    $"colony {(nest.Colony == colony)}, loaded {colony.NestsLoaded}");
                float hp = MWConfig.Nest.Health.Value;
                ctx.Assert("nest_health", Mathf.Abs(nest.MaxHealth - hp) < 0.01f && Mathf.Abs(nest.Health - hp) < 0.01f, hp, $"{nest.Health:0.#} / {nest.MaxHealth:0.#}");
                var box = nest.GetComponent<BoxCollider>();
                ctx.Assert("nest_collider", box != null && !box.isTrigger && Mathf.Abs(box.size.x - 0.9f) < 0.01f && Mathf.Abs(box.size.y - 0.5f) < 0.01f,
                    "a solid 0.9 x 0.5 box", box != null ? box.size.ToString() : "none");
                bool stripped = nest.GetComponent<EggHatch>() == null && nest.GetComponent<DropOnDestroyed>() == null && nest.GetComponentInChildren<LODGroup>(true) == null
                                && nest.GetComponentsInChildren<Renderer>(true).Length == 0;
                ctx.Assert("nest_without_vanilla_egg_parts", stripped, "no EggHatch, DropOnDestroyed, LODGroup or renderer", stripped);
                var view = nest.GetComponent<ZNetView>();
                ctx.Assert("nest_persistent", view != null && view.m_persistent, true, view != null && view.m_persistent);
                Destructible d = nest.Destructible;
                ctx.Assert("nest_destructible_without_vanilla_effects",
                    d != null && (d.m_destroyedEffect == null || d.m_destroyedEffect.m_effectPrefabs == null || d.m_destroyedEffect.m_effectPrefabs.Length == 0), "no networked destruction effect",
                    d != null && d.m_destroyedEffect != null && d.m_destroyedEffect.m_effectPrefabs != null ? d.m_destroyedEffect.m_effectPrefabs.Length : -1);

                // ---- (a) +3 worms in 6 s
                int baseline = colony.Store.AliveCount;
                float t0 = Time.time;
                while (Time.time - t0 < SettleSeconds) yield return null;
                int laid = colony.Store.AliveCount - baseline;
                ctx.Assert("three_worms_in_six_seconds", laid >= 2 && laid <= 4, "3 +- 1", laid);
                CheckEggs(ctx, colony, nest);

                // ---- (b) the cap of the nest
                int cap = colony.CountFromNest(nest.Index) + 2;
                MWConfig.Nest.MaxAlivePerNest.Value = cap;
                ctx.Log($"Nest.MaxAlivePerNest lowered to {cap}");
                yield return ctx.WaitGame(4f * Interval + 1f);
                int atCap = colony.CountFromNest(nest.Index);
                ctx.Assert("laying_stops_at_the_cap", atCap == cap, cap, atCap);
                yield return ctx.WaitGame(3f * Interval);
                int later = colony.CountFromNest(nest.Index);
                ctx.Assert("no_egg_above_the_cap", later == cap, cap, later);

                // ---- (c) the pause after a hit
                MWConfig.Nest.MaxAlivePerNest.Value = savedMaxPerNest;
                int beforeResume = colony.Store.AliveCount;
                yield return ctx.WaitUntil(() => colony.Store.AliveCount > beforeResume, 3f * Interval + 2f, "laying_resumes_when_the_cap_is_raised");
                float pause = MWConfig.Nest.HitPause.Value;
                int count0 = colony.Store.AliveCount;
                float healthBefore = nest.Health;
                float hitAt = Time.time;
                NestDev.Hit(nest, 5f);
                yield return ctx.WaitFrames(3);
                ctx.Assert("hit_lowers_the_health", nest.Health < healthBefore - 0.5f, $"< {healthBefore:0.#}", nest.Health);
                ctx.Assert("hit_is_recorded", nest.SecondsSinceHit < 1.5f, "< 1.5 s", nest.SecondsSinceHit);
                while (Time.time - hitAt < pause - 0.25f) yield return null;
                ctx.Assert("no_egg_during_the_hit_pause", colony.Store.AliveCount == count0, count0, colony.Store.AliveCount);
                float resumedAt = -1f;
                float deadline = hitAt + pause + 2f * Interval + 2f;
                while (Time.time < deadline && resumedAt < 0f)
                {
                    if (colony.Store.AliveCount > count0) resumedAt = Time.time;
                    yield return null;
                }
                ctx.Assert("laying_resumes_after_the_pause", resumedAt >= 0f && resumedAt - hitAt >= pause - 0.1f, $">= {pause:0.##} s after the hit", resumedAt < 0f ? "no egg" : $"{resumedAt - hitAt:0.00} s");

                // ---- (d) destruction
                Vector3 where = nest.transform.position;
                int itemsBefore = ItemsNear(where);
                int released = colony.WormsReleased;
                int aliveBefore = colony.Store.AliveCount;
                NestDev.Hit(nest, MWConfig.Nest.Health.Value * 2f);
                yield return ctx.WaitFrames(4);
                bool gone = nest == null;
                ctx.Assert("nest_destroyed_by_lethal_hit", gone, true, gone);
                int wanted = MWConfig.Nest.ReleaseOnDestroy.Value;
                ctx.Assert("worms_released", colony.WormsReleased - released == wanted, wanted, colony.WormsReleased - released);
                int active = 0;
                for (int i = 0; i < colony.Store.HighWater; i++)
                    if (colony.Store.alive[i] && colony.Store.state[i] != (byte)WormState.Sleeping) active++;
                ctx.Assert("released_worms_are_active", active >= wanted, $">= {wanted} not asleep", active);
                ctx.Assert("alive_grew_by_the_release", colony.Store.AliveCount == aliveBefore + wanted, aliveBefore + wanted, colony.Store.AliveCount);
                ctx.Assert("nothing_dropped", ItemsNear(where) == itemsBefore, itemsBefore, ItemsNear(where));
                ctx.Assert("colony_forgot_the_nest", colony.NestsLoaded == 0, 0, colony.NestsLoaded);
            }
            finally
            {
                MWConfig.Nest.MaxAlivePerNest.Value = savedMaxPerNest;
                WormColony.DevSpawnInterval = 0f;
                ColonyDev.GatheringEnabled = gatheringBefore;
                if (clutterHidden && ClutterSystem.instance != null) ClutterSystem.instance.enabled = true;
                ctx.Console("mw_kill");
                Player p = Player.m_localPlayer;
                if (p != null) p.SetGodMode(false);
            }
        }

        private static int ItemsNear(Vector3 where)
        {
            int n = 0;
            foreach (ItemDrop item in UnityEngine.Object.FindObjectsByType<ItemDrop>(FindObjectsSortMode.None))
                if (item != null && (item.transform.position - where).sqrMagnitude < 9f) n++;
            return n;
        }

        /// <summary>The worms laid so far: asleep, attached to the nest, not lone, 1 to 2 m (and a little floor slack) from it.</summary>
        private static void CheckEggs(ScenarioContext ctx, WormColony colony, WormNest nest)
        {
            WormStore s = colony.Store;
            int eggs = 0, bad = 0;
            float farthest = 0f, nearest = float.MaxValue;
            for (int i = 0; i < s.HighWater; i++)
            {
                if (!s.alive[i]) continue;
                eggs++;
                Vector3 d = s.pos[i] - nest.transform.position;
                d.y = 0f;
                float h = d.magnitude;
                farthest = Mathf.Max(farthest, h);
                nearest = Mathf.Min(nearest, h);
                if (s.state[i] != (byte)WormState.Sleeping || s.nestIndex[i] != nest.Index || s.lone[i] || h < MinEggDistance || h > MaxEggDistance) bad++;
            }
            ctx.Assert("eggs_asleep_at_the_nest", eggs > 0 && bad == 0, $"{eggs} worms, all Sleeping, nestIndex {nest.Index}, not lone, {MinEggDistance}-{MaxEggDistance} m away",
                $"{bad} of {eggs} wrong; distance {nearest:0.00} to {farthest:0.00} m");
        }
    }
}
#endif
