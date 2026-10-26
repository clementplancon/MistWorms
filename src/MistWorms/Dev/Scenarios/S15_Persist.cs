#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MistWorms.Colony;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// S15 (spec §15.5, §11.4, plan 7-3): the persistence of a colony, in a real seeded mine (/tp-mine). Param <c>step</c>:
    /// <list type="bullet">
    /// <item><c>reload</c> (default): wakes the biggest group, gives pools to a few worms and the reserve, lets the mine live 10 s, forces a snapshot and notes the free worms,
    /// their hit points, the pools, the reserve and the nests. The player goes to the start temple (at least 1 000 m away); the scenario waits for the colony to be unloaded
    /// (the zone is gone), checks that the unload left the snapshot in the ZDO, comes back through the door and compares: the same colony (ZDO), the worms restored from
    /// <c>mw_snap</c> by the new colony object (+-1), the pools (1e-3) and the reserve, the hit points, the nests, nothing outside the box, no second seeding.</item>
    /// <item><c>save</c>: the same preparation, then the notes go to BepInEx/config/mistworms_s15_expect.json and the world is saved (ZNet). The coordinator then restarts the game.</item>
    /// <item><c>check</c>: after the restart, reads that file, goes back to the mine through its door and compares as above; no seeding may have run in the session.</item>
    /// </list>
    /// The attacks are off and the gathering too (nothing hunts, no mass forms): the totals can only change by the persistence.
    /// </summary>
    internal sealed class S15_Persist : IDevScenario
    {
        private const float PoolTolerance = 1e-3f;
        private const float HpTolerancePerWorm = 0.06f;      // hit points are kept to a tenth
        private const float NestPositionTolerance = 0.05f;
        private const float NestHealthTolerance = 0.01f;
        private const float FarEnough = 1000f;
        private const float LiveSeconds = 10f;
        private const int WormTolerance = 1;

        private sealed class Expect
        {
            public string User;     // the ZDOID of the colony when the notes were taken: informative only, Valheim gives the ZDOs it loads new ids
            public string Id;
            public string Key;      // the stable identity of the colony (MineKey), what "the same colony" means after a restart
            public int Free, Total, RunsBefore;
            public float Hp, FreePool, Reserve;
            public Vector3 Door;
            public readonly List<float[]> Nests = new List<float[]>();   // index, x, y, z, health
        }

        private WormColony _colony;
        private Expect _expect;

        public string Name => "S15_Persist";

        private static string ExpectPath => Path.Combine(BepInEx.Paths.ConfigPath, "mistworms_s15_expect.json");

        public IEnumerator Run(ScenarioContext ctx)
        {
            string step = ctx.Param("step", "reload").ToLowerInvariant();
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 420f;
            if (step != "reload" && step != "save" && step != "check")
            {
                ctx.Assert("known_step", false, "reload, save or check", step);
                yield break;
            }

            bool gatheringBefore = ColonyDev.GatheringEnabled;
            bool attackPlayers = MWConfig.Targets.AttackPlayers.Value, attackTamed = MWConfig.Targets.AttackTamed.Value;
            bool attackCreatures = MWConfig.Targets.AttackCreatures.Value, attackStructures = MWConfig.Targets.AttackStructures.Value;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                // Not mw_kill: it would empty the very mine this scenario is about.
                ctx.Console("mw_freeze off");
                ColonyDev.GatheringEnabled = false;
                MWConfig.Targets.AttackPlayers.Value = false;
                MWConfig.Targets.AttackTamed.Value = false;
                MWConfig.Targets.AttackCreatures.Value = false;
                MWConfig.Targets.AttackStructures.Value = false;
                yield return ctx.WaitFrames(3);

                if (step == "check")
                {
                    _expect = ReadExpect(ctx);
                    if (_expect == null) yield break;
                    ctx.Log($"expecting {_expect.Free} free worms ({_expect.Total} with the masses), pool {_expect.FreePool:0.####} + reserve {_expect.Reserve:0.####}, {_expect.Nests.Count} nests; colony {MineKey.Describe(long.Parse(_expect.Key))} (was ZDOID {_expect.User}:{_expect.Id} before the restart)");
                    yield return GoBackInside(ctx);
                    if (_colony == null) yield break;
                    Verify(ctx, expectedRuns: 0);
                    yield break;
                }

                yield return Prepare(ctx);
                if (_colony == null) yield break;

                if (step == "save")
                {
                    WriteExpect(ctx);
                    float t = Time.realtimeSinceStartup;
                    Game.instance.m_saveTimer = 100f;     // the console save refuses to run within a minute of the last one
                    ZNet.instance.SaveWorldAndPlayerProfiles();
                    yield return ctx.WaitFrames(5);
                    yield return ctx.WaitUntil(() => !ZNet.instance.IsSaving(), 60f, "world_saved");
                    // The colonies stop here: whatever the worms do before the game is closed (they gather into masses, they hit the structures) would be in the save that closing the game makes,
                    // which replaces this one. mw_freeze off in the check step (and at the start of every scenario) lets them live again.
                    ctx.Console("mw_freeze on");
                    ctx.Log($"world saved in {Time.realtimeSinceStartup - t:0.0} s; colonies frozen so that the save made when the game closes holds the same state; quit the game, start it again and run S15 with step=check");
                    yield break;
                }

                // ---- reload: out of the zone and back
                Vector3 mine = _expect.Door;
                var far = new Vector3();
                bool temple = ZoneSystem.instance.FindClosestLocation("StartTemple", mine, out ZoneSystem.LocationInstance loc);
                if (temple)
                {
                    float h = WorldGenerator.instance.GetHeight(loc.m_position.x, loc.m_position.z);
                    far = new Vector3(loc.m_position.x, Mathf.Max(h, ZoneSystem.instance.m_waterLevel) + 2f, loc.m_position.z);
                }
                float away = temple ? Vector2.Distance(new Vector2(far.x, far.z), new Vector2(mine.x, mine.z)) : 0f;
                if (!ctx.Assert("far_place_found", temple && away >= FarEnough, $">= {FarEnough:0} m from the mine", temple ? $"{away:0} m" : "no start temple")) yield break;

                ZDOID id = _colony.ZdoId;
                int sizeBefore = _colony.DevSnapshotSize();
                var leave = new TeleportOutcome();
                yield return DevWorld.TeleportPlayer(far, Quaternion.identity, true, 60f, leave);
                ctx.Assert("teleport_far", leave.Ok, true, leave.Ok ? $"{away:0} m away" : leave.Error);
                if (!leave.Ok) yield break;

                yield return ctx.WaitUntil(() => ColonyRegistry.FindById(id) == null, 90f, "colony_unloaded");
                if (ctx.LastWaitTimedOut) yield break;
                ZDO zdo = ZDOMan.instance.GetZDO(id);
                byte[] left = zdo != null ? zdo.GetByteArray(MWNames.ZdoSnapshot, null) : null;
                ctx.Assert("snapshot_left_in_the_zdo", left != null && left.Length >= SnapshotFormat.HeaderBytes, "mw_snap present", left != null ? left.Length + " bytes" : "none");
                ctx.Log($"colony {id} unloaded; mw_snap {sizeBefore} bytes before, {(left != null ? left.Length : 0)} in the ZDO");

                yield return GoBackInside(ctx);
                if (_colony == null) yield break;
                ctx.Assert("same_colony_object_of_the_zdo", _colony.ZdoId == id, id.ToString(), _colony.ZdoId.ToString());
                Verify(ctx, expectedRuns: _expect.RunsBefore);
            }
            finally
            {
                ColonyDev.GatheringEnabled = gatheringBefore;
                MWConfig.Targets.AttackPlayers.Value = attackPlayers;
                MWConfig.Targets.AttackTamed.Value = attackTamed;
                MWConfig.Targets.AttackCreatures.Value = attackCreatures;
                MWConfig.Targets.AttackStructures.Value = attackStructures;
                Player p = Player.m_localPlayer;
                if (p != null) { p.SetGodMode(false); p.SetGhostMode(false); }
            }
        }

        // ------------------------------------------------------------------ preparation

        /// <summary>In the mine: seeded colony, one group awake, pools, reserve, damaged worms, ten seconds of life, a forced snapshot, the notes.</summary>
        private IEnumerator Prepare(ScenarioContext ctx)
        {
            _colony = null;
            Player.m_localPlayer.SetGodMode(true);
            yield return ctx.TpMineRetry(3, keepCreatures: true);     // the clearing of the mine would wipe the seeding this scenario needs
            if (!ctx.LastTpMine.Ok) yield break;
            Vector3 door = ctx.LastTpMine.Entrance;
            Player player = Player.m_localPlayer;
            player.SetGodMode(true);
            player.SetGhostMode(false);

            yield return ctx.WaitUntil(() => ColonyRegistry.FindFor(Player.m_localPlayer.transform.position) != null, 30f, "colony_created_for_the_mine");
            if (ctx.LastWaitTimedOut) yield break;
            WormColony colony = ColonyRegistry.FindFor(Player.m_localPlayer.transform.position);
            yield return ctx.WaitUntil(() => colony.SnapshotSettled, 30f, "colony_settled");
            if (ctx.LastWaitTimedOut) yield break;

            // A mine with next to nothing in it (seeded before the persistence existed, or emptied by an earlier scenario) is seeded again, so there is something to keep.
            if (colony.Seeded && colony.Store.AliveCount < 20)
            {
                int forgot = MineSeeder.Reset(colony);
                ctx.Log($"the mine held {colony.Store.AliveCount} worms: forgotten ({forgot} nests removed, worms killed, flag cleared) to be seeded again");
                yield return ctx.WaitFrames(3);
            }
            yield return ctx.WaitUntil(() => colony.Seeded && !MineSeeder.Planning, 90f, "mine_seeded");
            if (ctx.LastWaitTimedOut) yield break;
            yield return ctx.WaitFrames(5);

            WormStore s = colony.Store;
            if (!ctx.Assert("worms_to_keep", s.AliveCount >= 20, ">= 20 free worms", s.AliveCount)) yield break;

            // The biggest group that still has sleepers wakes (a mine seeded earlier may already have an awake group, whose worms are not asleep); nothing else does (no passive wake in this scenario).
            var perGroup = new int[WormColony.MaxNests];
            for (int i = 0; i < s.HighWater; i++)
                if (s.alive[i] && !s.lone[i] && s.nestIndex[i] >= 0 && (s.state[i] == (byte)WormState.Sleeping || s.state[i] == (byte)WormState.Calming)) perGroup[s.nestIndex[i]]++;
            int group = 0;
            for (int k = 1; k < perGroup.Length; k++)
                if (perGroup[k] > perGroup[group]) group = k;
            int woken = colony.DevWakeGroup(group);
            ctx.Assert("a_group_wakes", woken > 0, "> 0", woken);

            // Pools, the reserve, and hit points that are not the full ones.
            int hashA = "Resin".GetStableHashCode(), hashB = "Coal".GetStableHashCode();
            int given = 0, hurt = 0;
            for (int i = 0; i < s.HighWater; i++)
            {
                if (!s.alive[i]) continue;
                if (given < 4 && colony.DevGivePool(i, given % 2 == 0 ? hashA : hashB, 3.25f + given)) given++;
                else if (hurt < 5) s.hp[i] = hurt++ % 2 == 0 ? 4.35f : 7.2f;
            }
            colony.DevAddReserve(hashA, 1.75f);
            yield return ctx.WaitGame(LiveSeconds);

            int bytes = colony.DevWriteSnapshot();
            ctx.Assert("snapshot_written", bytes > 0 && bytes == colony.DevSnapshotSize(), "mw_snap in the ZDO", $"{bytes} bytes, ZDO {colony.DevSnapshotSize()}");
            _colony = colony;
            _expect = Capture(colony, door);
            ctx.Log($"noted: {_expect.Free} free worms ({_expect.Total} with the masses), hit points {_expect.Hp:0.##}, pool {_expect.FreePool:0.####} + reserve {_expect.Reserve:0.####}, {_expect.Nests.Count} nests, " +
                    $"{colony.DevCountState(WormState.Sleeping)} asleep, group {group} awake ({woken} woken), snapshot {bytes} bytes");
        }

        private static int KeptCount(WormColony colony)
        {
            WormStore s = colony.Store;
            int n = 0;
            for (int i = 0; i < s.HighWater; i++)
                if (colony.SnapshotKeeps(i)) n++;
            return n;
        }

        private static Expect Capture(WormColony colony, Vector3 door)
        {
            WormStore s = colony.Store;
            // The worms the snapshot keeps: a worm being absorbed (or dying) is alive in the store but is not saved, nor is it restored.
            var e = new Expect { Door = door, RunsBefore = MineSeeder.Runs, Free = KeptCount(colony) };
            colony.DevTotals(out e.Total, out _);
            e.Total += e.Free - s.AliveCount;
            for (int i = 0; i < s.HighWater; i++)
            {
                if (!colony.SnapshotKeeps(i)) continue;
                e.Hp += s.hp[i];
                if (s.poolIndex[i] >= 0) e.FreePool += s.Pools.Get(s.poolIndex[i]).TotalAll();
            }
            e.Reserve = colony.DevReserveTotal();
            ZDOID id = colony.ZdoId;
            e.User = id.UserID.ToString();
            e.Id = id.ID.ToString();
            e.Key = colony.Key.ToString();
            foreach (WormNest n in WormNest.All)
            {
                if (n == null || !n.IsValid || n.Colony != colony) continue;
                Vector3 p = n.transform.position;
                e.Nests.Add(new[] { n.Index, p.x, p.y, p.z, n.Health });
            }
            return e;
        }

        // ------------------------------------------------------------------ out of the zone and back

        /// <summary>To the door of the mine, then through it (the colony may need to load again), and waits for it to be settled with its nests loaded.</summary>
        private IEnumerator GoBackInside(ScenarioContext ctx)
        {
            _colony = null;
            var outside = new TeleportOutcome();
            yield return DevWorld.TeleportPlayer(_expect.Door + Vector3.up * 2f, Quaternion.identity, true, 60f, outside);
            ctx.Assert("teleport_to_the_door", outside.Ok, true, outside.Ok ? "arrived" : outside.Error);
            if (!outside.Ok) yield break;
            Player.m_localPlayer.SetGodMode(true);
            yield return ctx.TpMineRetry(3, keepCreatures: true);
            if (!ctx.LastTpMine.Ok) yield break;
            Player player = Player.m_localPlayer;
            player.SetGodMode(true);
            player.SetGhostMode(false);

            yield return ctx.WaitUntil(() => ColonyRegistry.FindFor(Player.m_localPlayer.transform.position) != null, 60f, "colony_loaded_again");
            if (ctx.LastWaitTimedOut) yield break;
            WormColony colony = ColonyRegistry.FindFor(Player.m_localPlayer.transform.position);
            yield return ctx.WaitUntil(() => colony.SnapshotSettled, 40f, "colony_settled_again");
            if (ctx.LastWaitTimedOut) yield break;
            yield return ctx.WaitUntil(() => colony.NestsLoaded >= _expect.Nests.Count, 20f, "nests_loaded_again");
            yield return ctx.WaitGame(1f);
            _colony = colony;
        }

        // ------------------------------------------------------------------ comparison

        private void Verify(ScenarioContext ctx, int expectedRuns)
        {
            WormColony colony = _colony;
            Expect e = _expect;
            WormStore s = colony.Store;
            // The identity is the key of the mine, not the ZDOID: Valheim renumbers the ZDOs it loads, so the colony has another ZDOID after a restart.
            ctx.Assert("same_colony", colony.Key.ToString() == e.Key, MineKey.Describe(long.Parse(e.Key)), MineKey.Describe(colony.Key));
            int sameKey = 0;
            foreach (WormColony c in ColonyRegistry.All)
                if (c != null && c.Key == colony.Key) sameKey++;
            ctx.Assert("one_colony_for_the_mine", sameKey == 1, 1, sameKey);
            ctx.Assert("worms_come_from_the_snapshot", colony.SnapshotRestored >= e.Free - WormTolerance, $">= {e.Free - WormTolerance} restored", $"{colony.SnapshotRestored} restored, {colony.SnapshotSkipped} left out, problem {colony.SnapshotProblem ?? "none"}");

            float hp = 0f, pool = 0f;
            int outside = 0;
            for (int i = 0; i < s.HighWater; i++)
            {
                if (!colony.SnapshotKeeps(i)) continue;
                hp += s.hp[i];
                if (s.poolIndex[i] >= 0) pool += s.Pools.Get(s.poolIndex[i]).TotalAll();
                if (!WormMath.InsideBox(s.pos[i], colony.BoxCenter, colony.BoxHalfSize)) outside++;
            }
            colony.DevTotals(out int total, out _);
            int kept = KeptCount(colony);
            total += kept - s.AliveCount;
            ctx.Assert("free_worms_same", Mathf.Abs(kept - e.Free) <= WormTolerance, $"{e.Free} +- {WormTolerance}", kept);
            ctx.Assert("total_worms_same", Mathf.Abs(total - e.Total) <= WormTolerance, $"{e.Total} +- {WormTolerance}", total);
            ctx.Assert("hit_points_same", Mathf.Abs(hp - e.Hp) <= HpTolerancePerWorm * e.Free, $"{e.Hp:0.##} +- {HpTolerancePerWorm} per worm", hp);
            ctx.Assert("worm_pools_same", Mathf.Abs(pool - e.FreePool) <= PoolTolerance, $"{e.FreePool:0.####} +- {PoolTolerance}", pool);
            ctx.Assert("reserve_same", Mathf.Abs(colony.DevReserveTotal() - e.Reserve) <= PoolTolerance, $"{e.Reserve:0.####} +- {PoolTolerance}", colony.DevReserveTotal());
            ctx.Assert("nothing_outside_the_box", outside == 0, 0, outside);

            int bad = 0;
            var found = new List<WormNest>();
            foreach (WormNest n in WormNest.All)
                if (n != null && n.IsValid && n.Colony == colony) found.Add(n);
            foreach (float[] want in e.Nests)
            {
                WormNest match = null;
                foreach (WormNest n in found)
                    if (n.Index == (int)want[0]) match = n;
                if (match == null) { bad++; continue; }
                Vector3 p = match.transform.position;
                if (Vector3.Distance(p, new Vector3(want[1], want[2], want[3])) > NestPositionTolerance || Mathf.Abs(match.Health - want[4]) > NestHealthTolerance) bad++;
            }
            ctx.Assert("nests_same", found.Count == e.Nests.Count && bad == 0, $"{e.Nests.Count} nests, same place and health", $"{found.Count} nests, {bad} different");
            ctx.Assert("not_seeded_twice", colony.Seeded && MineSeeder.Runs == expectedRuns, $"seeded, {expectedRuns} seeding(s) this session", $"seeded {colony.Seeded}, {MineSeeder.Runs} seeding(s)");
            ctx.Log($"after: {s.AliveCount} free worms ({total} with the masses), {colony.DevCountState(WormState.Sleeping)} asleep, hit points {hp:0.##}, pool {pool:0.####} + reserve {colony.DevReserveTotal():0.####}, {found.Count} nests");
        }

        // ------------------------------------------------------------------ the notes of the save step

        private void WriteExpect(ScenarioContext ctx)
        {
            Expect e = _expect;
            var nests = new List<object>();
            foreach (float[] n in e.Nests)
                nests.Add(new Dictionary<string, object> { ["index"] = (int)n[0], ["x"] = n[1], ["y"] = n[2], ["z"] = n[3], ["health"] = n[4] });
            var doc = new Dictionary<string, object>
            {
                ["user"] = e.User, ["id"] = e.Id, ["key"] = e.Key, ["free"] = e.Free, ["total"] = e.Total,
                ["hp"] = e.Hp, ["pool"] = e.FreePool, ["reserve"] = e.Reserve,
                ["door"] = new Dictionary<string, object> { ["x"] = e.Door.x, ["y"] = e.Door.y, ["z"] = e.Door.z },
                ["nests"] = nests,
            };
            File.WriteAllText(ExpectPath, MiniJson.Serialize(doc));
            ctx.Log("expectations written to " + ExpectPath);
        }

        private static Expect ReadExpect(ScenarioContext ctx)
        {
            if (!File.Exists(ExpectPath))
            {
                ctx.Assert("expectations_file", false, ExpectPath, "missing: run step=save first");
                return null;
            }
            Dictionary<string, object> doc = MiniJson.AsObject(MiniJson.Parse(File.ReadAllText(ExpectPath)));
            if (!long.TryParse(MiniJson.GetString(doc, "key") ?? "", out _))
            {
                ctx.Assert("expectations_file", false, "notes with a colony key (from this build)", "notes written by an older build: run step=save again");
                return null;
            }
            var e = new Expect
            {
                User = MiniJson.GetString(doc, "user"), Id = MiniJson.GetString(doc, "id"), Key = MiniJson.GetString(doc, "key"),
                Free = MiniJson.GetInt(doc, "free"), Total = MiniJson.GetInt(doc, "total"), RunsBefore = 0,
                Hp = MiniJson.GetFloat(doc, "hp"), FreePool = MiniJson.GetFloat(doc, "pool"), Reserve = MiniJson.GetFloat(doc, "reserve"),
            };
            Dictionary<string, object> door = MiniJson.GetObject(doc, "door");
            e.Door = new Vector3(MiniJson.GetFloat(door, "x"), MiniJson.GetFloat(door, "y"), MiniJson.GetFloat(door, "z"));
            foreach (object o in MiniJson.GetList(doc, "nests") ?? new List<object>())
            {
                Dictionary<string, object> n = MiniJson.AsObject(o);
                e.Nests.Add(new[] { MiniJson.GetInt(n, "index"), MiniJson.GetFloat(n, "x"), MiniJson.GetFloat(n, "y"), MiniJson.GetFloat(n, "z"), MiniJson.GetFloat(n, "health") });
            }
            return e;
        }
    }
}
#endif
