#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using MistWorms.Colony;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// S16 (spec §15.5, §8.3, plan 7 A1, A2, A7): the lazy seeding of a real infested mine (/tp-mine). The colony of the mine is created without any dev
    /// tool, the mine is seeded once the rooms stand and the player is inside: between 1 and Nest.MaxNestsPerMine nests, apart from each other and at least
    /// 20 m from the entrance; a brood of Nest.InitialWorms asleep around each; at most Nest.MaxLoneWormsPerRoom lone worms in a room, 4 m from any nest;
    /// a total of about InitialWorms x nests + min(LoneWormsPerMine, MaxLoneWormsPerRoom x lone rooms: every room but the entrance and the dividers with a floor point), within 5 %; everything inside the box of the mine;
    /// the vanilla rooms untouched. Leaving the mine and coming back does not seed it again. The seeded mine is left in place at the end; the runner keeps
    /// the seeding off for every other scenario, and entering a mine in one of them wipes its worms and nests.
    /// </summary>
    internal sealed class S16_MineSeed : IDevScenario
    {
        private const float Tolerance = 0.05f;
        private const float BroodMax = 3.5f;          // 3 m ring plus the floor slack
        private const float BroodMin = 0.8f;
        private const float LoneNestMin = 3.9f;

        public string Name => "S16_MineSeed";

        public IEnumerator Run(ScenarioContext ctx)
        {
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 360f;
            WormColony colony = null;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                yield return ctx.WaitFrames(3);
                bool prefab = ZNetScene.instance != null && ZNetScene.instance.GetPrefab(MWNames.NestPrefab) != null;
                ctx.Assert("prefab_registered", prefab, MWNames.NestPrefab, prefab ? "registered" : "missing");
                if (!prefab) yield break;
                PatchCheck.Check(ctx, "patch_dungeon_generator_load", HarmonyLib.AccessTools.Method(typeof(DungeonGenerator), "Load"), postfix: true);

                Player.m_localPlayer.SetGodMode(true);
                Player.m_localPlayer.SetGhostMode(true);
                yield return ctx.TpMineRetry(3, keepCreatures: true);   // the clearing of the mine would wipe the seeding this scenario is about
                if (!ctx.LastTpMine.Ok) yield break;
                Player player = Player.m_localPlayer;
                DungeonGenerator gen = DevWorld.NearestGenerator(player.transform.position);
                if (!ctx.Assert("generator_found", gen != null, true, gen != null)) yield break;
                ctx.Assert("mine_theme", MineSeeder.IsInfestedMine(gen), "DvergerTown, not the citadel", gen.m_themes.ToString());

                // ---- the colony exists without any dev tool
                yield return ctx.WaitUntil(() => ColonyRegistry.FindFor(Player.m_localPlayer.transform.position) != null, 30f, "colony_created_for_the_mine");
                if (ctx.LastWaitTimedOut) yield break;
                colony = ColonyRegistry.FindFor(Player.m_localPlayer.transform.position);
                ctx.Assert("colony_is_a_mine_colony", colony.Kind == WormColony.ColonyKind.Mine && colony.IsOwner, "Mine, owned here", $"{colony.Kind}, owner {colony.IsOwner}");
                int colonies = 0;
                foreach (WormColony c in ColonyRegistry.All)
                    if (c != null && c.Kind == WormColony.ColonyKind.Mine && WormMath.InsideBox(player.transform.position, c.BoxCenter, c.BoxHalfSize)) colonies++;
                ctx.Assert("one_colony_for_the_mine", colonies == 1, 1, colonies);

                // A mine seeded by an earlier visit of this world (or an earlier run) is forgotten, so the seeding can be seen from the start.
                if (colony.Seeded)
                {
                    int forgot = MineSeeder.Reset(colony);
                    ctx.Log($"the mine was already seeded: forgotten ({forgot} nests removed, worms killed, flag cleared)");
                    yield return ctx.WaitFrames(3);
                }
                int runsBefore = MineSeeder.Runs;

                // ---- the seeding itself
                yield return ctx.WaitUntil(() => colony != null && colony.Seeded && !MineSeeder.Planning, 90f, "mine_seeded");
                if (ctx.LastWaitTimedOut) yield break;
                yield return ctx.WaitFrames(5);
                SeedReport report = colony.LastSeed;
                if (!ctx.Assert("seed_report", report != null, true, report != null)) yield break;
                ctx.Assert("seeded_once_so_far", MineSeeder.Runs == runsBefore + 1, runsBefore + 1, MineSeeder.Runs);
                ctx.Log($"seed {report.Seed}: {report.Rooms} rooms, {report.EligibleRooms} eligible for a nest, {report.LoneRooms} for lone worms (largest {report.LargestRoom} m2), {report.NestsPlaced} nests, brood {report.BroodPlaced}/{report.BroodWanted}, " +
                        $"lone {report.LonePlaced}/{report.LoneWanted}, {report.Frames + 1} frames, {report.PlanMs:0} ms; entrance {report.Entrance}");

                // ---- nests
                var nests = new List<WormNest>();
                foreach (WormNest n in WormNest.All)
                    if (n != null && n.IsValid && n.Colony == colony) nests.Add(n);
                int maxNests = MWConfig.Nest.MaxNestsPerMine.Value;
                ctx.Assert("nests_between_1_and_the_maximum", nests.Count >= 1 && nests.Count <= maxNests, $"1 to {maxNests}", nests.Count);
                ctx.Assert("every_nest_registered", colony.NestsLoaded == nests.Count && nests.Count == report.NestsPlaced, nests.Count, $"loaded {colony.NestsLoaded}, placed {report.NestsPlaced}");
                bool indices = true;
                var seen = new HashSet<int>();
                foreach (WormNest n in nests) indices &= n.Index >= 0 && n.Index < maxNests && seen.Add(n.Index);
                ctx.Assert("nest_indices_unique", indices, "unique indices", indices);
                float closest = float.MaxValue, nearestEntrance = float.MaxValue;
                for (int a = 0; a < nests.Count; a++)
                {
                    nearestEntrance = Mathf.Min(nearestEntrance, Vector3.Distance(nests[a].transform.position, report.Entrance));
                    for (int b = a + 1; b < nests.Count; b++) closest = Mathf.Min(closest, Vector3.Distance(nests[a].transform.position, nests[b].transform.position));
                }
                ctx.Assert("nests_apart", nests.Count < 2 || closest >= NestMath.MinSpacing - 0.01f, $">= {NestMath.MinSpacing} m", nests.Count < 2 ? "one nest" : $"{closest:0.0} m");
                ctx.Assert("nests_far_from_the_entrance", nearestEntrance >= NestMath.MinEntranceDistance - 0.01f, $">= {NestMath.MinEntranceDistance} m", $"{nearestEntrance:0.0} m");
                bool inBox = true;
                foreach (WormNest n in nests) inBox &= WormMath.InsideBox(n.transform.position, colony.BoxCenter, colony.BoxHalfSize);
                ctx.Assert("nests_in_the_box", inBox, true, inBox);
                float hp = MWConfig.Nest.Health.Value;
                bool healthy = true;
                foreach (WormNest n in nests) healthy &= Mathf.Abs(n.Health - hp) < 0.01f;
                ctx.Assert("nests_at_full_health", healthy, hp, healthy);

                // ---- worms
                WormStore s = colony.Store;
                Room[] rooms = gen.GetComponentsInChildren<Room>();
                var lonePerRoom = new Dictionary<Room, int>();
                int alive = 0, awake = 0, lone = 0, outsideBox = 0, loneOutsideRooms = 0, loneNearNest = 0, broodBad = 0, brood = 0, noNest = 0;
                for (int i = 0; i < s.HighWater; i++)
                {
                    if (!s.alive[i]) continue;
                    alive++;
                    if (s.state[i] != (byte)WormState.Sleeping) awake++;
                    if (!WormMath.InsideBox(s.pos[i], colony.BoxCenter, colony.BoxHalfSize)) outsideBox++;
                    if (s.lone[i])
                    {
                        lone++;
                        Room room = RoomAt(rooms, s.pos[i]);
                        if (room == null) loneOutsideRooms++;
                        else lonePerRoom[room] = lonePerRoom.TryGetValue(room, out int c) ? c + 1 : 1;
                        foreach (WormNest n in nests)
                            if (Vector3.Distance(n.transform.position, s.pos[i]) < LoneNestMin) { loneNearNest++; break; }
                        if (nests.Count > 0 && (s.nestIndex[i] < 0 || colony.NestAt(s.nestIndex[i]) == null)) noNest++;
                    }
                    else
                    {
                        brood++;
                        WormNest n = s.nestIndex[i] >= 0 ? colony.NestAt(s.nestIndex[i]) : null;
                        float d = n != null ? Vector3.Distance(n.transform.position, s.pos[i]) : -1f;
                        if (n == null || d < BroodMin || d > BroodMax) broodBad++;
                    }
                }
                // The room of a lone worm is the one the seeder placed it in (the report): the bounds of neighbouring rooms overlap, so a geometric lookup is ambiguous.
                int maxPerRoom = 0, placedInRooms = 0;
                foreach (int c in report.LonePlacedPerRoom) { maxPerRoom = Mathf.Max(maxPerRoom, c); placedInRooms += c; }
                int maxByBounds = 0;
                foreach (KeyValuePair<Room, int> kv in lonePerRoom) maxByBounds = Mathf.Max(maxByBounds, kv.Value);
                int loneCap = MWConfig.Nest.MaxLoneWormsPerRoom.Value;
                ctx.Log($"lone worms per room: max {maxPerRoom} by the seeder's report over {report.LonePlacedPerRoom.Length} rooms ({placedInRooms} worms); max {maxByBounds} by overlapping room bounds (informative)");
                ctx.Assert("all_worms_asleep", awake == 0, 0, awake);
                ctx.Assert("lone_worms_at_most_the_room_maximum", maxPerRoom <= loneCap && placedInRooms == report.LonePlaced && loneOutsideRooms == 0, $"<= {loneCap} per room, all in a room",
                    $"max {maxPerRoom}, {placedInRooms} placed in rooms of {report.LonePlaced}, outside any room {loneOutsideRooms}");
                ctx.Assert("lone_worms_apart_from_nests", loneNearNest == 0, 0, loneNearNest);
                ctx.Assert("lone_worms_attached_to_a_nest", noNest == 0, 0, noNest);
                ctx.Assert("lone_count_matches_report", lone == report.LonePlaced, report.LonePlaced, lone);
                ctx.Assert("brood_around_its_nest", broodBad == 0 && brood == report.BroodPlaced, $"{report.BroodPlaced} worms, {BroodMin}-{BroodMax} m from their nest", $"{brood} worms, {broodBad} wrong");
                int perNest = MWConfig.Nest.InitialWorms.Value;
                bool broods = true;
                foreach (WormNest n in nests) broods &= colony.CountFromNest(n.Index) >= perNest - 1;
                ctx.Assert("each_nest_has_its_brood", broods, $">= {perNest - 1} each", broods);
                ctx.Assert("nothing_outside_the_box", outsideBox == 0, 0, outsideBox);

                int expected = nests.Count * perNest + Math.Min(MWConfig.Nest.LoneWormsPerMine.Value, loneCap * report.LoneRooms);
                int tolerance = Math.Max(1, Mathf.CeilToInt(expected * Tolerance));
                ctx.Assert("initial_total_matches_the_formula", Math.Abs(alive - expected) <= tolerance, $"{expected} +- {tolerance} ({nests.Count} nests x {perNest} + min({MWConfig.Nest.LoneWormsPerMine.Value}, {loneCap} x {report.LoneRooms} rooms))",
                    $"{alive} ({brood} brood + {lone} lone)");
                ctx.Assert("under_the_mine_cap", alive <= MWConfig.Colony.MaxAlivePerMine.Value, $"<= {MWConfig.Colony.MaxAlivePerMine.Value}", alive);
                ctx.Assert("vanilla_rooms_intact", gen.GetComponentsInChildren<Room>().Length == report.Rooms, report.Rooms, gen.GetComponentsInChildren<Room>().Length);

                // ---- one seeding only: out of the mine and back in
                int nestsBefore = nests.Count;
                int aliveBefore = s.AliveCount;
                var again = new TpMineResult();
                yield return DevWorld.TpMine(again, ctx.Log);
                ctx.Assert("tp_mine_again", again.Ok, "back inside", again.Ok ? again.Location : again.Error);
                if (again.Ok)
                {
                    yield return ctx.Wait(8f);   // many polls
                    int nestsAfter = 0;
                    foreach (WormNest n in WormNest.All)
                        if (n != null && n.IsValid && n.Colony == colony) nestsAfter++;
                    ctx.Assert("not_seeded_again", MineSeeder.Runs == runsBefore + 1 && nestsAfter == nestsBefore && colony.Store.AliveCount <= aliveBefore + nestsBefore,
                        $"1 seeding, {nestsBefore} nests, at most {aliveBefore + nestsBefore} worms", $"{MineSeeder.Runs - runsBefore} seedings, {nestsAfter} nests, {colony.Store.AliveCount} worms");
                    ctx.Assert("seeded_flag_stays", colony.Seeded, true, colony.Seeded);
                }
                yield return ctx.Shot("seeded_mine");
            }
            finally
            {
                // The seeded mine is left as it is (it is what a real mine looks like); the other scenarios wipe the mine they enter (ScenarioContext.TpMine),
                // and "mw_seed reset" seeds it again by hand.
                Player p = Player.m_localPlayer;
                if (p != null) { p.SetGodMode(false); p.SetGhostMode(false); }
            }
        }

        private static Room RoomAt(Room[] rooms, Vector3 world)
        {
            foreach (Room room in rooms)
            {
                Vector3 l = room.transform.InverseTransformPoint(world);
                if (Mathf.Abs(l.x) <= room.m_size.x * 0.5f + 0.1f && Mathf.Abs(l.z) <= room.m_size.z * 0.5f + 0.1f && Mathf.Abs(l.y) <= room.m_size.y * 0.5f + 0.6f) return room;
            }
            return null;
        }
    }
}
#endif
