#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Mass;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// S14 (spec §15.5, §5.1, §5.2, plan 7-2): the waking, the alert chain and the return to sleep of a real infested mine (/tp-mine, seeded by the plugin itself),
    /// with Targets.CalmDelay forced to 5 s (restored at the end) and nobody hunting (the Targets.Attack* keys are off, restored). The player hits a nest through the real
    /// HitData path (attacker = the local player): its group wakes, the groups within Colony.AlertRadius follow one hop (0.5 s) at a time as the pure rule predicts, the ones
    /// out of the chain sleep on. A mass is formed from the woken group (the dev handle that gives it its nest counts) and a few worms get a pool. The player then leaves the
    /// mine (the door on the surface, 5000 m away): within 5 + 30 + 20 s every worm is asleep and no mass is left, the total of worms is the same, the loot pool is the same
    /// (1e-3), the sleepers of the mass lie within 2.5 m of their nest and no nest laid an egg. The player comes back to the nest: the group wakes in under 1 s with the same
    /// total of worms and the same pool.
    /// </summary>
    internal sealed class S14_CalmSleep : IDevScenario
    {
        private const float CalmDelaySeconds = 5f;
        private const float HopSeconds = 0.5f;
        private const float AsleepWithin = 5f + 30f + 20f;
        private const float SleeperMax = 2.5f;
        private const float PoolTolerance = 1e-3f;

        public string Name => "S14_CalmSleep";

        public IEnumerator Run(ScenarioContext ctx)
        {
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 420f;
            float calmBefore = MWConfig.Targets.CalmDelay.Value;
            bool gatheringBefore = ColonyDev.GatheringEnabled;
            bool attackPlayers = MWConfig.Targets.AttackPlayers.Value, attackTamed = MWConfig.Targets.AttackTamed.Value;
            bool attackCreatures = MWConfig.Targets.AttackCreatures.Value, attackStructures = MWConfig.Targets.AttackStructures.Value;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                ColonyDev.GatheringEnabled = false;
                MWConfig.Targets.CalmDelay.Value = CalmDelaySeconds;
                MWConfig.Targets.AttackPlayers.Value = false;
                MWConfig.Targets.AttackTamed.Value = false;
                MWConfig.Targets.AttackCreatures.Value = false;
                MWConfig.Targets.AttackStructures.Value = false;
                WormColony.DevPassiveWake = false;          // switched on for the return of the player
                yield return ctx.WaitFrames(3);

                // ---- 1. a real mine, seeded by the plugin
                Player.m_localPlayer.SetGodMode(true);
                yield return ctx.TpMineRetry(3, keepCreatures: true);     // the clearing of the mine would wipe the seeding this scenario needs
                if (!ctx.LastTpMine.Ok) yield break;
                Player player = Player.m_localPlayer;
                MistWorms.Dev.DevSpawnBlock.Active = true;
                S18_GroupCoherence.ClearCreatures(ctx, player);
                ctx.GuardCreatures = true;
                player.SetGodMode(true);
                player.SetGhostMode(false);                // a ghost is nobody for the worms

                yield return ctx.WaitUntil(() => ColonyRegistry.FindFor(Player.m_localPlayer.transform.position) != null, 30f, "colony_created_for_the_mine");
                if (ctx.LastWaitTimedOut) yield break;
                WormColony colony = ColonyRegistry.FindFor(Player.m_localPlayer.transform.position);
                if (colony.Seeded)
                {
                    int forgot = MineSeeder.Reset(colony);
                    ctx.Log($"the mine was already seeded: forgotten ({forgot} nests removed, worms killed, flag cleared)");
                    yield return ctx.WaitFrames(3);
                }
                yield return ctx.WaitUntil(() => colony != null && colony.Seeded && !MineSeeder.Planning, 90f, "mine_seeded");
                if (ctx.LastWaitTimedOut) yield break;
                yield return ctx.WaitFrames(5);

                var nests = new List<WormNest>();
                foreach (WormNest n in WormNest.All)
                    if (n != null && n.IsValid && n.Colony == colony) nests.Add(n);
                if (!ctx.Assert("seeded_with_nests", nests.Count >= 1, ">= 1 nest", nests.Count)) yield break;
                ctx.Assert("everything_sleeps_after_the_seeding", colony.DevCountState(WormState.Sleeping) == colony.Store.AliveCount && colony.AwakeBits == 0L,
                    "all asleep, no group awake", $"{colony.DevCountState(WormState.Sleeping)} of {colony.Store.AliveCount} asleep, bits {colony.AwakeBits}");

                // ---- 2. the alert: the nest with the longest chain, the chain the pure rule predicts
                var positions = new Vector3[WormColony.MaxNests];
                var alive = new bool[WormColony.MaxNests];
                foreach (WormNest n in nests) { positions[n.Index] = n.transform.position; alive[n.Index] = true; }
                var predicted = new float[WormColony.MaxNests];
                var queue = new int[WormColony.MaxNests];
                // The nests by the length of their chain, the longest first: the first one with a floor point in sight is the one hit (a cramped nest is skipped, with the reasons logged).
                var order = new List<WormNest>(nests);
                var reachOf = new Dictionary<WormNest, int>();
                foreach (WormNest n in nests)
                {
                    AlertMath.Schedule(positions, alive, WormColony.MaxNests, MWConfig.Colony.AlertRadius.Value, HopSeconds, n.Index, 0f, predicted, queue);
                    int reach = 0;
                    foreach (WormNest o in nests)
                        if (o != n && predicted[o.Index] != AlertMath.Never) reach++;
                    reachOf[n] = reach;
                }
                order.Sort((x, y) => reachOf[y] != reachOf[x] ? reachOf[y].CompareTo(reachOf[x]) : x.Index.CompareTo(y.Index));
                WormNest start = null;
                Vector3 spot = default;
                foreach (WormNest n in order)
                {
                    if (SpotNear(n.transform.position, out spot, out string why)) { start = n; break; }
                    ctx.Log($"nest {n.Index} ({reachOf[n]} nests in its chain): no floor point in sight ({why})");
                }
                if (start == null) { ctx.Assert("spot_near_the_nest", false, "a floor point in sight of a nest", $"none for any of {nests.Count} nests"); yield break; }
                int bestReach = reachOf[start];
                AlertMath.Schedule(positions, alive, WormColony.MaxNests, MWConfig.Colony.AlertRadius.Value, HopSeconds, start.Index, 0f, predicted, queue);
                float horizon = 0f;
                foreach (WormNest n in nests)
                    if (predicted[n.Index] != AlertMath.Never) horizon = Mathf.Max(horizon, predicted[n.Index]);
                int a = start.Index;
                ctx.Log($"{nests.Count} nests; alert from nest {a}, {bestReach} nests in its chain over {horizon / HopSeconds:0} hop(s)");

                var near = new TeleportOutcome();
                yield return DevWorld.TeleportPlayer(spot + Vector3.up * 0.1f, Quaternion.LookRotation(start.transform.position - spot), false, 30f, near);
                ctx.Assert("teleport_next_to_the_nest", near.Ok, true, near.Ok ? "arrived" : near.Error);
                if (!near.Ok) yield break;
                yield return ctx.WaitGame(1.5f);
                ctx.Assert("the_group_sleeps_until_hit", !colony.IsGroupAwake(a), "asleep", colony.IsGroupAwake(a));

                var wokeAt = new float[WormColony.MaxNests];
                for (int k = 0; k < wokeAt.Length; k++) wokeAt[k] = -1f;
                float t0 = Time.time;
                NestDev.Hit(start, 5f);          // the real path: a HitData with the local player as attacker, Destructible.Damage, the prefix of RPC_Damage
                while (Time.time - t0 < horizon + 2f)
                {
                    yield return null;
                    foreach (WormNest n in nests)
                        if (wokeAt[n.Index] < 0f && colony.IsGroupAwake(n.Index)) wokeAt[n.Index] = Time.time - t0;
                }
                ctx.Assert("the_hit_group_wakes_at_once", wokeAt[a] >= 0f && wokeAt[a] <= 0.3f, "<= 0.3 s", wokeAt[a]);
                int late = 0, early = 0, unreached = 0, woke = 0;
                foreach (WormNest n in nests)
                {
                    if (n.Index == a) continue;
                    float p = predicted[n.Index];
                    if (p == AlertMath.Never)
                    {
                        unreached++;
                        if (wokeAt[n.Index] >= 0f) woke++;
                        continue;
                    }
                    if (wokeAt[n.Index] < 0f || wokeAt[n.Index] > p + HopSeconds * 0.8f) late++;
                    else if (wokeAt[n.Index] < p - 0.2f) early++;
                    ctx.Log($"nest {n.Index}: predicted {p:0.00} s, woke at {wokeAt[n.Index]:0.00} s");
                }
                ctx.Assert("neighbours_wake_one_hop_at_a_time", late == 0 && early == 0, "each group at its hop x 0.5 s (-0.2, +0.4)", $"{late} late, {early} early");
                ctx.Assert("groups_out_of_the_chain_sleep_on", woke == 0, $"{unreached} groups asleep", $"{woke} woke");
                yield return ctx.WaitGame(0.8f);
                int stillAsleep = 0;
                WormStore s = colony.Store;
                for (int i = 0; i < s.HighWater; i++)
                    if (s.alive[i] && s.nestIndex[i] == a && s.state[i] == (byte)WormState.Sleeping) stillAsleep++;
                ctx.Assert("the_worms_of_the_group_are_up", stillAsleep == 0, 0, stillAsleep);

                // ---- 3. a mass in the group, and some pool
                int hashA = "Resin".GetStableHashCode(), hashB = "Coal".GetStableHashCode();
                int given = 0;
                for (int i = 0; i < s.HighWater && given < 4; i++)
                {
                    if (!s.alive[i]) continue;
                    if (colony.DevGivePool(i, given % 2 == 0 ? hashA : hashB, 3.25f + given)) given++;
                }
                WritheMassState mass = colony.DevFormMassFromGroup(a, 1000);
                if (!ctx.Assert("mass_formed_in_the_group", mass != null && mass.N >= 1, ">= 1 worm", mass != null ? mass.N : 0)) yield break;
                int massWorms = mass.N;
                yield return ctx.WaitGame(1f);
                colony.DevTotals(out int n0, out float pool0);
                ctx.Log($"before the calm: {n0} worms ({massWorms} in the mass of group {a}), pool {pool0:0.###}");
                ctx.Assert("pool_to_follow", pool0 > 0f, "> 0", pool0);
                int eggs0 = colony.EggsLaid;
                yield return ctx.Shot("awake");

                // ---- 4. the player leaves the mine: the door on the surface, 5000 m under or above every box of the mine
                Vector3 door = ctx.LastTpMine.Entrance;
                bool found = SurfacePoint(ctx, door, nests, out Vector3 outside);
                if (!ctx.Assert("surface_point_found", found, "a floor 6-30 m from the door, out of every group's range", found ? outside.ToString() : "none")) yield break;
                var leave = new TeleportOutcome();
                yield return DevWorld.TeleportPlayer(outside + Vector3.up * 0.2f, Quaternion.identity, true, 60f, leave);
                ctx.Assert("teleport_out_of_the_mine", leave.Ok, true, leave.Ok ? "arrived" : leave.Error);
                if (!leave.Ok) yield break;
                float tLeft = Time.time;

                bool settled = false;
                while (Time.time - tLeft < AsleepWithin + 10f && !settled)
                {
                    yield return ctx.WaitGame(0.5f);
                    settled = colony.DevCountState(WormState.Sleeping) == colony.Store.AliveCount && ActiveMasses(colony) == 0;
                }
                float took = Time.time - tLeft;
                ctx.Assert("everything_asleep_in_time", settled && took <= AsleepWithin, $"all asleep, no mass, within {AsleepWithin:0} s", settled ? $"{took:0.0} s" : $"{colony.DevCountState(WormState.Sleeping)} of {colony.Store.AliveCount} asleep, {ActiveMasses(colony)} masses");
                ctx.Assert("no_mass_left", ActiveMasses(colony) == 0, 0, ActiveMasses(colony));
                ctx.Assert("no_group_awake", colony.AwakeBits == 0L, 0L, colony.AwakeBits);
                colony.DevTotals(out int n1, out float pool1);
                ctx.Assert("same_number_of_worms", n1 == n0, n0, n1);
                ctx.Assert("same_pool", Mathf.Abs(pool1 - pool0) <= PoolTolerance, $"{pool0:0.####} +- {PoolTolerance}", pool1);

                // the sleepers of the mass lie around their nest
                int far = 0, around = 0;
                float farthest = 0f;
                for (int i = 0; i < s.HighWater; i++)
                {
                    if (!s.alive[i] || s.lone[i] || s.nestIndex[i] != a) continue;
                    around++;
                    Vector3 d = s.pos[i] - start.transform.position;
                    d.y = 0f;
                    farthest = Mathf.Max(farthest, d.magnitude);
                    if (d.magnitude > SleeperMax) far++;
                }
                ctx.Assert("sleepers_of_the_mass_near_their_nest", around >= massWorms && far == 0, $"{massWorms}+ worms within {SleeperMax} m", $"{around} worms, {far} farther, farthest {farthest:0.00} m");
                ctx.Assert("no_egg_during_the_calm", colony.EggsLaid == eggs0, eggs0, colony.EggsLaid);
                ctx.Log($"calm: {colony.GroupsSlept} groups slept, {colony.WormsCalmed} worms walked home, {colony.SleptWorms} worms laid down by masses, {took:0.0} s");

                // ---- 5. the player comes back: the group wakes in under a second, same worms, same pool
                // Decision 272 trace: every living slot before the return, to name the worm that goes missing (S14 116/117 in p9h).
                var slotsBefore = new System.Text.StringBuilder();
                for (int i = 0; i < s.HighWater; i++)
                    if (s.alive[i]) slotsBefore.Append($"[{i} {(WormState)s.state[i]} {s.pos[i]} nest {s.nestIndex[i]}] ");
                ctx.Log($"slots alive before the return ({s.AliveCount}): {slotsBefore}");
                WormColony.DevPassiveWake = true;
                var again = new TpMineResult();
                yield return DevWorld.TpMine(again, ctx.Log);
                ctx.Assert("tp_mine_again", again.Ok, "back inside", again.Ok ? again.Location : again.Error);
                if (!again.Ok) yield break;
                player = Player.m_localPlayer;
                player.SetGhostMode(false);
                player.SetGodMode(true);
                var back = new TeleportOutcome();
                yield return DevWorld.TeleportPlayer(spot + Vector3.up * 0.1f, Quaternion.LookRotation(start.transform.position - spot), false, 30f, back);
                ctx.Assert("teleport_back_to_the_nest", back.Ok, true, back.Ok ? "arrived" : back.Error);
                if (!back.Ok) yield break;
                float tBack = Time.time;
                float wokeBack = -1f;
                while (Time.time - tBack < 3f && wokeBack < 0f)
                {
                    if (colony.IsGroupAwake(a)) wokeBack = Time.time - tBack;
                    else yield return null;
                }
                ctx.Assert("the_group_wakes_when_the_player_returns", wokeBack >= 0f && wokeBack <= 1f, "< 1 s", wokeBack < 0f ? "never" : $"{wokeBack:0.00} s");
                yield return ctx.WaitGame(1f);
                colony.DevTotals(out int n2, out float pool2);
                ctx.Log($"after the return: {n2} worms, {s.AliveCount} alive slots, death causes: {(colony.DeathCauses.Count == 0 ? "none" : string.Join(" | ", colony.DeathCauses))}");
                var slotsAfter = new System.Text.StringBuilder();
                for (int i = 0; i < s.HighWater; i++)
                    if (s.alive[i]) slotsAfter.Append($"[{i} {(WormState)s.state[i]} {s.pos[i]} nest {s.nestIndex[i]}] ");
                ctx.Log($"slots alive after the return: {slotsAfter}");
                ctx.Assert("same_number_of_worms_after_the_return", n2 == n0, n0, n2);
                ctx.Assert("same_pool_after_the_return", Mathf.Abs(pool2 - pool0) <= PoolTolerance, $"{pool0:0.####} +- {PoolTolerance}", pool2);
                yield return ctx.Shot("awake_again");
            }
            finally
            {
                WormColony.DevPassiveWake = true;
                MWConfig.Targets.CalmDelay.Value = calmBefore;
                MWConfig.Targets.AttackPlayers.Value = attackPlayers;
                MWConfig.Targets.AttackTamed.Value = attackTamed;
                MWConfig.Targets.AttackCreatures.Value = attackCreatures;
                MWConfig.Targets.AttackStructures.Value = attackStructures;
                ColonyDev.GatheringEnabled = gatheringBefore;
                Player p = Player.m_localPlayer;
                if (p != null) { p.SetGodMode(false); p.SetGhostMode(false); }
            }
        }

        private static int ActiveMasses(WormColony colony)
        {
            int n = 0;
            ZDOID me = colony.ZdoId;
            foreach (WritheMassState m in WritheMassState.All)
                if (m != null && m.Valid && m.ColonyId == me) n++;
            return n;
        }

        /// <summary>A floor point 2.5 to 5 m from the nest, level with it and in sight of it.</summary>
        private static bool SpotNear(Vector3 nest, out Vector3 spot, out string why)
        {
            int mask = WormPhysics.SolidMask;
            int noFloor = 0, tooSteep = 0, notFlat = 0, blockedView = 0, crowded = 0;
            for (float distance = 1.5f; distance <= 6f; distance += 0.5f)
            {
                for (int turn = 0; turn < 16; turn++)
                {
                    Vector3 dir = Quaternion.AngleAxis(22.5f * turn, Vector3.up) * Vector3.forward;
                    Vector3 p = nest + dir * distance;
                    if (!Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 7f, mask, QueryTriggerInteraction.Ignore)) { noFloor++; continue; }
                    if (Vector3.Angle(hit.normal, Vector3.up) > 40f) { tooSteep++; continue; }
                    if (Mathf.Abs(hit.point.y - nest.y) > 1.2f) { notFlat++; continue; }
                    // The view: from just outside the mound toward the spot, at chest height both ways (the line from inside the mound would hit its own collider).
                    Vector3 from = nest + dir * 0.9f + Vector3.up * 0.9f;
                    Vector3 to = hit.point + Vector3.up * 1.0f;
                    if (Physics.Linecast(from, to, mask, QueryTriggerInteraction.Ignore)) { blockedView++; continue; }
                    if (Physics.CheckCapsule(hit.point + Vector3.up * 0.4f, hit.point + Vector3.up * 1.6f, 0.3f, mask, QueryTriggerInteraction.Ignore)) { crowded++; continue; }
                    spot = hit.point;
                    why = null;
                    return true;
                }
            }
            spot = default;
            why = $"rejected: {noFloor} without floor, {tooSteep} too steep, {notFlat} off the nest's level, {blockedView} without a view, {crowded} too crowded";
            return false;
        }

        /// <summary>
        /// A floor point in the open world around <paramref name="door"/>: rings of 6 to 30 m, 16 directions, a ray from 50 m above down to the terrain or the solid world,
        /// a slope up to 45 degrees, above the water, and at least ActiveRadius + 10 m from every box of a group (the nests). The rejects are logged.
        /// </summary>
        private static bool SurfacePoint(ScenarioContext ctx, Vector3 door, List<WormNest> nests, out Vector3 point)
        {
            point = default;
            int mask = WormPhysics.SolidMask | LayerMask.GetMask("terrain");
            float water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
            float keep = MWConfig.Targets.ActiveRadius.Value + 10f;
            int noHit = 0, steep = 0, wet = 0, farLevel = 0, nearGroup = 0;
            for (float ring = 6f; ring <= 30f; ring += 3f)
            {
                for (int turn = 0; turn < 16; turn++)
                {
                    Vector3 p = door + Quaternion.AngleAxis(22.5f * turn, Vector3.up) * Vector3.forward * ring;
                    if (!Physics.Raycast(new Vector3(p.x, door.y + 50f, p.z), Vector3.down, out RaycastHit hit, 150f, mask, QueryTriggerInteraction.Ignore)) { noHit++; continue; }
                    if (Vector3.Angle(hit.normal, Vector3.up) > 45f) { steep++; continue; }
                    if (hit.point.y < water + 0.3f) { wet++; continue; }
                    if (Mathf.Abs(hit.point.y - door.y) > 25f) { farLevel++; continue; }
                    bool close = false;
                    foreach (WormNest n in nests)
                        if (n != null && (n.transform.position - hit.point).magnitude < keep) { close = true; break; }
                    if (close) { nearGroup++; continue; }
                    point = hit.point;
                    return true;
                }
            }
            ctx.Log($"surface point around {door}: rejected {noHit} without ground, {steep} too steep, {wet} in the water, {farLevel} off the door's level, {nearGroup} within {keep:0} m of a group");
            return false;
        }
    }
}
#endif
