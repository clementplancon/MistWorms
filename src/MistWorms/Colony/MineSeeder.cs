using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>What one seeding of a mine planned and placed (spec §8.3); read by the dev state and by S16.</summary>
    internal sealed class SeedReport
    {
        public int Seed;
        public int Rooms;               // rooms of the mine
        public int EligibleRooms;       // big enough for a nest, not the entrance, not a divider, with a valid floor point
        public int LoneRooms;           // not the entrance, not a divider, with a valid floor point for a lone worm (corridors included, any size)
        public int LargestRoom;         // m2, for the log
        public int NestsPlaced;
        public int BroodWanted, BroodPlaced;
        public int LoneWanted, LonePlaced;
        public Vector3 Entrance;
        public Vector3[] NestPositions = new Vector3[0];
        public int[] LonePerRoom = new int[0];    // per lone room, in the order they were handed out (the wish)
        public int[] LonePlacedPerRoom = new int[0];   // per lone room, what was really placed there (never above Nest.MaxLoneWormsPerRoom)
        public int Frames;
        public float PlanMs;
        public string Note;

        public int WormsPlaced => BroodPlaced + LonePlaced;
    }

    /// <summary>
    /// Colony of an infested mine and its lazy seeding (spec §8.3, decisions 192, 193 and 194). A <c>DungeonGenerator.Load</c> postfix only notes the
    /// generator; <see cref="Pump"/> (once per frame from the plugin, a poll at 2 Hz and only while the local player is in an interior) waits for
    /// the rooms to be loaded and stable and the local player to stand in the box of the mine, then creates the colony when there is none (the owner
    /// of the dungeon only) and, on the owner of the colony, seeds the mine once: the nests by farthest point sampling, a brood around each, the lone
    /// worms by <see cref="SeedMath"/>. The plan is sampled in slices of a few ms over several frames; the objects are placed in one frame, after the
    /// seeded flag of the colony is written, so a mine is never seeded twice.
    /// </summary>
    internal static class MineSeeder
    {
        private const float PollInterval = 0.5f;
        private const float BudgetMs = 3f;
        private const int FloorTries = 12;            // spec §8.3: 12 tries for a floor point
        private const float WallClearance = 1.5f;     // at least 1.5 m from the walls
        private const float SpawnerClearance = 2f;    // at least 2 m from the creature spawners
        private const float MaxFloorSlopeDeg = 20f;   // slope under 20 degrees
        private const float LoneNestDistance = 4f;    // lone worms sit at least 4 m from a nest
        private const float LoneSpacing = 1.2f;       // and at least this far from each other
        private const float BroodRingMin = 1f, BroodRingMax = 3f;
        private const int BroodTries = 12;
        private const float RoomMargin = 1.5f;
        private const float LoneWallClearance = 0.6f;   // a lone worm also lives in corridors 2 m wide
        private const float LoneRoomMargin = 0.6f;
        private const int LoneTries = 24;

        /// <summary>Seedings completed in this game session (S16: leaving the mine and coming back must not add one).</summary>
        public static int Runs { get; private set; }

        /// <summary>The report of the last seeding, or null.</summary>
        public static SeedReport Last { get; private set; }

        /// <summary>A seeding is being planned (spread over frames).</summary>
        public static bool Planning => s_routine != null;

#if MW_DEV
        /// <summary>
        /// Dev: no seeding at all. The runner sets it for every scenario but S16, so that the mine the other scenarios enter stays as they expect it
        /// (no nest, no sleeping worm around the player).
        /// </summary>
        public static bool Suppressed;
#endif

        private sealed class Poll
        {
            public int Rooms;
            public int Stable;
        }

        private static readonly List<DungeonGenerator> s_generators = new List<DungeonGenerator>();
        private static readonly Dictionary<DungeonGenerator, Poll> s_polls = new Dictionary<DungeonGenerator, Poll>();
        private static readonly List<Room> s_roomBuffer = new List<Room>();
        private static readonly RaycastHit[] s_hits = new RaycastHit[8];
        private static float s_nextPoll;
        private static IEnumerator s_routine;
        private static WormColony s_routineColony;
        private static DungeonGenerator s_routineGenerator;

        // ------------------------------------------------------------------ hook

        /// <summary>True for the generator of an infested mine of the Mistlands (theme DvergerTown, not the citadel of the Queen).</summary>
        public static bool IsInfestedMine(DungeonGenerator generator)
        {
            if (generator == null || generator.m_algorithm != DungeonGenerator.Algorithm.Dungeon) return false;
            if ((generator.m_themes & VanillaNames.MineTheme) == 0 || (generator.m_themes & VanillaNames.QueenCitadelTheme) != 0) return false;
            // Another mod's object is never ours (SkyWhale's SW_ objects, spec §10.4).
            string name = generator.gameObject.name;
            return !name.StartsWith("SW_", StringComparison.Ordinal);
        }

        /// <summary>Postfix of <c>DungeonGenerator.Load</c>: remembers an infested mine so the poll can look at it. Touches nothing else.</summary>
        public static void Note(DungeonGenerator generator)
        {
            if (!IsInfestedMine(generator) || s_generators.Contains(generator)) return;
            s_generators.Add(generator);
        }

        // ------------------------------------------------------------------ poll

        /// <summary>Called every frame by the plugin; never throws.</summary>
        public static void Pump()
        {
            try
            {
                if (s_routine != null)
                {
                    StepRoutine();
                    return;
                }
                float now = Time.unscaledTime;
                if (now < s_nextPoll) return;
                s_nextPoll = now + PollInterval;

                Player player = Player.m_localPlayer;
                if (player == null || s_generators.Count == 0 || !Character.InInterior(player.transform.position)) return;
                PollPlayer(player);
            }
            catch (Exception e)
            {
                Plugin.LogException("MineSeeder.Pump", e);
                s_routine = null;
                s_routineColony = null;
            }
        }

        private static DungeonGenerator NearestGenerator(Vector3 pos)
        {
            DungeonGenerator best = null;
            float bestSqr = float.MaxValue;
            for (int k = s_generators.Count - 1; k >= 0; k--)
            {
                DungeonGenerator g = s_generators[k];
                if (g == null)
                {
                    s_polls.Remove(g);
                    s_generators.RemoveAt(k);
                    continue;
                }
                Vector3 d = g.transform.position - pos;
                d.y = 0f;
                if (d.sqrMagnitude < bestSqr)
                {
                    bestSqr = d.sqrMagnitude;
                    best = g;
                }
            }
            return best;
        }

        private static readonly List<WormColony> s_sameBox = new List<WormColony>();
        private static readonly int s_colonyHash = MWNames.ColonyPrefab.GetStableHashCode();
        private static float s_loadingSince = -1f;
        private const float LoadingPatience = 30f;

        /// <summary>
        /// True while the zone of <paramref name="where"/> holds a colony ZDO whose object is not there yet (a saved colony that the zone is still instantiating,
        /// or one not configured yet): creating a colony now would make a second one for the same mine, and the nests of the one that is removed would be orphans.
        /// Gives up after <see cref="LoadingPatience"/> seconds (a ZDO that never gets an object).
        /// </summary>
        internal static bool ColonyLoading(Vector3 where)
        {
            bool pending = false;
            ZDOMan man = ZDOMan.instance;
            if (man != null && man.m_objectsBySector != null)
            {
                int sector = (int)ZoneSystem.SectorToIndex(ZoneSystem.GetZone(where)).Sector;
                List<ZDO> list = sector >= 0 && sector < man.m_objectsBySector.Length ? man.m_objectsBySector[sector] : null;
                if (list != null)
                {
                    for (int k = 0; k < list.Count && !pending; k++)
                    {
                        ZDO zdo = list[k];
                        if (zdo == null || !zdo.IsValid() || zdo.GetPrefab() != s_colonyHash) continue;
                        WormColony c = ColonyRegistry.FindById(zdo.m_uid);
                        pending = c == null || !c.IsConfigured;
                    }
                }
            }
            if (!pending) { s_loadingSince = -1f; return false; }
            float now = Time.unscaledTime;
            if (s_loadingSince < 0f) s_loadingSince = now;
            if (now - s_loadingSince < LoadingPatience) return true;
            s_loadingSince = -1f;
            Plugin.Log.LogWarning("[Seed] a colony of this mine is still not there after " + LoadingPatience + " s: creating one");
            return false;
        }

        private static void PollPlayer(Player player)
        {
            if (!MWConfig.General.Enabled.Value) return;
#if MW_DEV
            if (Suppressed) return;
#endif
            Vector3 here = player.transform.position;
            WormColony colony = Deduplicate(here);
            if (colony != null && (colony.Kind != WormColony.ColonyKind.Mine || !colony.IsConfigured)) return;
            if (colony != null && colony.IsOwner && !colony.SnapshotSettled) return;   // a reloaded colony gets its worms back before any seeding (phase 7-3)

            // The common case in a mine that is already handled: nothing to look at, nothing allocated.
            MineStep step = MineOwnership.Decide(colony != null, colony != null && colony.IsOwner, colony != null && !colony.IsOwner && OwnerHasPlayerInside(colony), colony != null && colony.Seeded);
            if (step == MineStep.Nothing) return;
            if (step == MineStep.Claim)
            {
                // The owner stayed outside (next to the entrance) or left: the player inside takes the colony (phase 8 refines this).
                colony.NView.ClaimOwnership();
                Plugin.Log.LogInfo($"[Seed] colony {colony.ZdoId} had no player in the mine: ownership taken by this peer");
                return;
            }

            DungeonGenerator gen = NearestGenerator(here);
            if (gen == null) return;

            if (!s_polls.TryGetValue(gen, out Poll poll)) s_polls[gen] = poll = new Poll();
            s_roomBuffer.Clear();
            gen.GetComponentsInChildren(s_roomBuffer);
            int rooms = s_roomBuffer.Count;
            s_roomBuffer.Clear();
            if (rooms == 0) { poll.Stable = 0; poll.Rooms = 0; return; }
            poll.Stable = rooms == poll.Rooms ? poll.Stable + 1 : 0;
            poll.Rooms = rooms;
            if (poll.Stable < 2) return;     // the rooms are placed in one go; two equal counts in a row mean they are all there

            if (step == MineStep.Create)
            {
                ColonyBox.ForDungeon(gen, out Vector3 center, out Vector3 half);
                if (!WormMath.InsideBox(here, center, half)) return;
                if (ColonyLoading(gen.transform.position)) return;   // the saved colony of this mine is still being instantiated: wait for it

                // Any peer with a player in the box creates the colony (registry by box, spec §8.3); two made at once are reduced to one by Deduplicate.
                colony = WormColony.Create(gen.transform.position, WormColony.ColonyKind.Mine, center, half);
                if (colony != null) Plugin.Log.LogInfo($"[Seed] colony created for the mine at {gen.transform.position}: box {center} +-{half}, {rooms} rooms");
                return;
            }

            s_routineColony = colony;
            s_routineGenerator = gen;
            s_routine = Seed(gen, colony);
        }

        /// <summary>Does the peer that owns <paramref name="colony"/> have a player inside its box? (A player is owned by its own peer.)</summary>
        private static bool OwnerHasPlayerInside(WormColony colony)
        {
            long owner = colony.OwnerUid;
            List<Player> players = Player.GetAllPlayers();
            for (int k = 0; k < players.Count; k++)
            {
                Player p = players[k];
                if (p != null && p.GetOwner() == owner && WormMath.InsideBox(p.transform.position, colony.BoxCenter, colony.BoxHalfSize)) return true;
            }
            return false;
        }

        private static KeeperCandidate[] s_candidates = new KeeperCandidate[4];

        /// <summary>
        /// The colony of the mine around <paramref name="pos"/>. When several mine colonies share the box one stays, chosen by <see cref="MineOwnership.CompareKeeper"/> from what their ZDOs hold
        /// (seeded, more worms, older; never by ZDOID, which is not the same from one session to the next, decision 211). Each duplicate that this peer owns hands its worms, pools and reserve
        /// to the one that stays and is destroyed; its nests and masses carry the key of the mine and follow. The seeded flag is inherited, so the mine is never seeded twice.
        /// Returns the one that stays (null when none).
        /// </summary>
        private static WormColony Deduplicate(Vector3 pos)
        {
            s_sameBox.Clear();
            List<WormColony> all = ColonyRegistry.All;
            for (int k = 0; k < all.Count; k++)
            {
                WormColony c = all[k];
                if (c != null && c.IsConfigured && c.Kind == WormColony.ColonyKind.Mine && WormMath.InsideBox(pos, c.BoxCenter, c.BoxHalfSize)) s_sameBox.Add(c);
            }
            if (s_sameBox.Count == 0) return ColonyRegistry.FindFor(pos);
            if (s_sameBox.Count == 1)
            {
                WormColony only = s_sameBox[0];
                s_sameBox.Clear();
                return only;
            }

            if (s_candidates.Length < s_sameBox.Count) s_candidates = new KeeperCandidate[s_sameBox.Count];
            for (int k = 0; k < s_sameBox.Count; k++) s_candidates[k] = s_sameBox[k].Candidate;
            WormColony keep = s_sameBox[MineOwnership.PickKeeper(s_candidates, s_sameBox.Count)];

            bool anySeeded = false;
            for (int k = 0; k < s_sameBox.Count; k++)
                if (s_sameBox[k] != keep && s_sameBox[k].Seeded) anySeeded = true;
            if (keep.IsOwner && MineOwnership.InheritSeeded(keep.Seeded, anySeeded) && !keep.Seeded) keep.SetSeeded(true);
            for (int k = 0; k < s_sameBox.Count; k++)
            {
                WormColony dup = s_sameBox[k];
                if (dup == keep || !dup.IsOwner) continue;
                Plugin.Log.LogInfo($"[Seed] duplicate colony {dup.ZdoId} (mine {MineKey.Describe(dup.Key)}, {s_candidates[k].Worms} worms, seeded {s_candidates[k].Seeded}) for the mine of colony {keep.ZdoId}: handed over, destroyed");
                dup.HandOverTo(keep);
                dup.DestroyColony();
            }
            s_sameBox.Clear();
            return keep;
        }

        private static void StepRoutine()
        {
            WormColony colony = s_routineColony;
            Player player = Player.m_localPlayer;
            bool alive = colony != null && s_routineGenerator != null && colony.IsOwner && player != null
                         && WormMath.InsideBox(player.transform.position, colony.BoxCenter, colony.BoxHalfSize);
#if MW_DEV
            if (Suppressed) alive = false;
#endif
            if (!alive)
            {
                Plugin.Log.LogInfo("[Seed] seeding abandoned (the player left the mine or the colony went away); it will start again on the next visit");
                s_routine = null;
                s_routineColony = null;
                return;
            }
            if (!s_routine.MoveNext())
            {
                s_routine = null;
                s_routineColony = null;
            }
        }

#if MW_DEV
        /// <summary>
        /// Dev: forgets a seeding. Kills the worms of the colony, destroys its nests and clears the seeded flag, so the next visit seeds again.
        /// Returns the nests destroyed.
        /// </summary>
        public static int Reset(WormColony colony)
        {
            if (colony == null) return 0;
            s_routine = null;
            s_routineColony = null;
            colony.KillAll();
            int nests = colony.DestroyAllNests();
            colony.SetSeeded(false);
            colony.LastSeed = null;
            return nests;
        }
#endif

        // ------------------------------------------------------------------ plan and placement

        private sealed class RoomPick
        {
            public Room Room;
            public Vector3 Point;
            public float Area;
        }

        private static IEnumerator Seed(DungeonGenerator gen, WormColony colony)
        {
            var clock = Stopwatch.StartNew();
            var slice = Stopwatch.StartNew();
            var report = new SeedReport();
            int worldSeed = WorldGenerator.instance != null ? WorldGenerator.instance.GetSeed() : 0;
            Vector3 root = gen.transform.position;
            int seed = SeedMath.MineSeed(worldSeed, root.x, root.y, root.z);
            var rng = new System.Random(seed);
            report.Seed = seed;

            Room[] rooms = gen.GetComponentsInChildren<Room>();
            report.Rooms = rooms.Length;
            Vector3 entrance = root;
            foreach (Room r in rooms)
                if (r.m_entrance) { entrance = r.transform.position; break; }
            report.Entrance = entrance;

            // Creature spawners of the mine: a nest or a worm keeps 2 m away from them.
            CreatureSpawner[] spawnerComponents = gen.GetComponentsInChildren<CreatureSpawner>();
            var spawners = new Vector3[spawnerComponents.Length];
            for (int k = 0; k < spawners.Length; k++) spawners[k] = spawnerComponents[k].transform.position;

            // 1. Every eligible room and a valid floor point in it.
            float minArea = MWConfig.Nest.MinRoomArea.Value;
            var picks = new List<RoomPick>();
            var none = new List<Vector3>(0);
            foreach (Room room in rooms)
            {
                if (room.m_entrance || room.m_divider) continue;
                float area = room.m_size.x * room.m_size.z;
                if (area > report.LargestRoom) report.LargestRoom = Mathf.RoundToInt(area);
                if (area < minArea) continue;
                if (FloorPoint(room, rng, spawners, colony, none, 0f, none, 0f, out Vector3 point)) picks.Add(new RoomPick { Room = room, Point = point, Area = area });
                if (slice.Elapsed.TotalMilliseconds > BudgetMs) { report.Frames++; yield return null; slice.Restart(); }
            }
            report.EligibleRooms = picks.Count;

            // 2. The nests: farthest point sampling over those rooms.
            var points = new Vector3[picks.Count];
            var areas = new float[picks.Count];
            for (int k = 0; k < picks.Count; k++) { points[k] = picks[k].Point; areas[k] = picks[k].Area; }
            int[] chosen = NestMath.ChooseNests(points, areas, entrance, MWConfig.Nest.MaxNestsPerMine.Value, NestMath.MinEntranceDistance, NestMath.MinSpacing, seed);
            var nestPoints = new List<Vector3>(chosen.Length);
            foreach (int c in chosen) nestPoints.Add(picks[c].Point);
            report.NestPositions = nestPoints.ToArray();

            // 3. A brood around each nest.
            int perNest = MWConfig.Nest.InitialWorms.Value;
            var brood = new List<Vector3>[chosen.Length];
            for (int n = 0; n < chosen.Length; n++)
            {
                brood[n] = new List<Vector3>(perNest);
                for (int w = 0; w < perNest; w++)
                {
                    if (BroodPoint(nestPoints[n], rng, colony, brood[n], out Vector3 at)) brood[n].Add(at);
                    report.BroodWanted++;
                }
                if (slice.Elapsed.TotalMilliseconds > BudgetMs) { report.Frames++; yield return null; slice.Restart(); }
            }

            // 4. The lone worms: the budget over every room that is not the entrance or a divider and has a valid floor point (corridors and small rooms included,
            //    decision 207), the biggest rooms first (SeedMath hands the round out from a seeded start).
            var lonePicks = new List<RoomPick>();
            foreach (Room room in rooms)
            {
                if (room.m_entrance || room.m_divider) continue;
                float area = room.m_size.x * room.m_size.z;
                if (FloorPoint(room, rng, spawners, colony, none, 0f, none, 0f, out Vector3 point, LoneWallClearance, LoneRoomMargin, LoneTries))
                    lonePicks.Add(new RoomPick { Room = room, Point = point, Area = area });
                if (slice.Elapsed.TotalMilliseconds > BudgetMs) { report.Frames++; yield return null; slice.Restart(); }
            }
            report.LoneRooms = lonePicks.Count;
            var areaCount = new SortedDictionary<int, int>();
            foreach (Room room in rooms)
            {
                int a = Mathf.RoundToInt(room.m_size.x * room.m_size.z);
                areaCount[a] = areaCount.TryGetValue(a, out int n) ? n + 1 : 1;
            }
            var areaText = new System.Text.StringBuilder();
            foreach (KeyValuePair<int, int> kv in areaCount) areaText.Append(kv.Value).Append('x').Append(kv.Key).Append(" m2 ");
            Plugin.Log.LogInfo($"[Seed] room floor areas: {areaText}");
            lonePicks.Sort((a, b) => b.Area != a.Area ? b.Area.CompareTo(a.Area) : a.Room.transform.position.GetHashCode().CompareTo(b.Room.transform.position.GetHashCode()));
            int loneCap = MWConfig.Nest.MaxLoneWormsPerRoom.Value;
            int[] counts = SeedMath.LoneWormsPerRoom(MWConfig.Nest.LoneWormsPerMine.Value, lonePicks.Count, loneCap, seed);
            report.LonePerRoom = counts;
            var lone = new List<Vector3>();
            var loneByRoom = new List<Vector3>[lonePicks.Count];
            for (int k = 0; k < lonePicks.Count; k++)
            {
                loneByRoom[k] = new List<Vector3>(loneCap);
                for (int w = 0; w < counts[k]; w++)
                {
                    report.LoneWanted++;
                    // At least 4 m from every nest: the nests act as the "others" of the distance test (LoneNestDistance), the lone worms of the room as the second one.
                    if (LonePoint(lonePicks[k].Room, rng, spawners, colony, nestPoints, loneByRoom[k], out Vector3 at))
                    {
                        lone.Add(at);
                        loneByRoom[k].Add(at);
                    }
                }
                if (slice.Elapsed.TotalMilliseconds > BudgetMs) { report.Frames++; yield return null; slice.Restart(); }
            }
            // What a room could not take (a point too close to a nest) goes to the rooms that still have room for one.
            int shortfall = report.LoneWanted - lone.Count;
            for (int pass = 0; pass < 2 && shortfall > 0; pass++)
            {
                for (int k = 0; k < lonePicks.Count && shortfall > 0; k++)
                {
                    if (loneByRoom[k].Count >= loneCap) continue;
                    if (LonePoint(lonePicks[k].Room, rng, spawners, colony, nestPoints, loneByRoom[k], out Vector3 at))
                    {
                        lone.Add(at);
                        loneByRoom[k].Add(at);
                        shortfall--;
                    }
                    if (slice.Elapsed.TotalMilliseconds > BudgetMs) { report.Frames++; yield return null; slice.Restart(); }
                }
            }

            report.LonePlacedPerRoom = new int[lonePicks.Count];
            for (int k = 0; k < lonePicks.Count; k++) report.LonePlacedPerRoom[k] = loneByRoom[k].Count;

            // 5. Placement, in this frame. The flag first: whatever happens next, this mine is not seeded again.
            if (colony == null || !colony.IsOwner || colony.Seeded) yield break;
            colony.SetSeeded(true);

            var placedNests = new List<WormNest>(chosen.Length);
            for (int n = 0; n < chosen.Length; n++)
            {
                WormNest nest = colony.SpawnNest(nestPoints[n], (float)rng.NextDouble() * 360f, n);
                placedNests.Add(nest);
                if (nest == null) continue;
                report.NestsPlaced++;
                foreach (Vector3 at in brood[n])
                {
                    Vector3 away = at - nestPoints[n];
                    int slot = colony.AddWorm(at, WormState.Sleeping, WormMath.HeadingOf(away.x, away.z));
                    if (slot < 0) continue;
                    colony.Store.nestIndex[slot] = (short)n;
                    report.BroodPlaced++;
                }
            }
            foreach (Vector3 at in lone)
            {
                int slot = colony.AddWorm(at, WormState.Sleeping, (float)rng.NextDouble() * Mathf.PI * 2f - Mathf.PI);
                if (slot < 0) continue;
                colony.Store.lone[slot] = true;
                colony.Store.nestIndex[slot] = (short)NearestNest(at, nestPoints, placedNests);   // for the alert only (phase 7-2)
                report.LonePlaced++;
            }

            report.PlanMs = (float)clock.Elapsed.TotalMilliseconds;
            colony.LastSeed = report;
            Last = report;
            Runs++;
            Plugin.Log.LogInfo($"[Seed] mine at {root} seeded (seed {seed}): {report.Rooms} rooms, {report.EligibleRooms} eligible, {report.NestsPlaced} nests, " +
                               $"{report.LoneRooms} lone rooms (largest room {report.LargestRoom} m2, nest minimum {minArea:0} m2), {report.BroodPlaced}/{report.BroodWanted} brood worms, {report.LonePlaced}/{report.LoneWanted} lone worms, {report.Frames + 1} frames, {report.PlanMs:0} ms");
        }

        private static int NearestNest(Vector3 p, List<Vector3> nestPoints, List<WormNest> placed)
        {
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int n = 0; n < nestPoints.Count; n++)
            {
                if (placed[n] == null) continue;
                float d = (nestPoints[n] - p).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = n; }
            }
            return best;
        }

        // ------------------------------------------------------------------ sampling

        /// <summary>
        /// A floor point of <paramref name="room"/>, at most <see cref="FloorTries"/> tries: the lowest surface under a random column of the room that is
        /// flat enough (under 20 degrees) with headroom, at least 1.5 m from the walls and 2 m from the creature spawners, with nothing solid in the way of the
        /// mound, inside the box of the colony, at least <paramref name="distA"/> from every point of <paramref name="avoidA"/> and <paramref name="distB"/> from every
        /// point of <paramref name="avoidB"/>.
        /// </summary>
        private static bool FloorPoint(Room room, System.Random rng, Vector3[] spawners, WormColony colony, List<Vector3> avoidA, float distA, List<Vector3> avoidB, float distB, out Vector3 point,
            float wallClearance = WallClearance, float margin = RoomMargin, int maxTries = FloorTries)
        {
            Transform t = room.transform;
            float hx = room.m_size.x * 0.5f, hz = room.m_size.z * 0.5f, hy = room.m_size.y * 0.5f;
            int mask = WormPhysics.CharacterSolidMask;
            float cosMax = Mathf.Cos(MaxFloorSlopeDeg * Mathf.Deg2Rad);
            for (int tries = 0; tries < maxTries; tries++)
            {
                float lx = Between(rng, -hx + margin, hx - margin);
                float lz = Between(rng, -hz + margin, hz - margin);
                Vector3 top = t.TransformPoint(new Vector3(lx, hy, lz));
                int count = Physics.RaycastNonAlloc(top, Vector3.down, s_hits, room.m_size.y + 4f, mask, QueryTriggerInteraction.Ignore);
                int low = -1;
                for (int k = 0; k < count; k++)
                {
                    if (s_hits[k].normal.y < cosMax) continue;
                    float y = s_hits[k].point.y - t.position.y;
                    if (y < -hy - 0.5f || y > hy - 1f) continue;      // the floor of this room, not the one below it or the top of its ceiling
                    if (low < 0 || s_hits[k].point.y < s_hits[low].point.y) low = k;
                }
                if (low < 0) continue;
                Vector3 p = s_hits[low].point;
                if (Accept(p, spawners, colony, avoidA, distA, avoidB, distB, wallClearance)) { point = p; return true; }
            }
            point = default;
            return false;
        }

        /// <summary>A lone worm: at least 4 m from every nest and 1.2 m from the other lone worms of its room.</summary>
        private static bool LonePoint(Room room, System.Random rng, Vector3[] spawners, WormColony colony, List<Vector3> nests, List<Vector3> others, out Vector3 point) =>
            FloorPoint(room, rng, spawners, colony, nests, LoneNestDistance, others, LoneSpacing, out point, LoneWallClearance, LoneRoomMargin, LoneTries);

        private static bool Accept(Vector3 p, Vector3[] spawners, WormColony colony, List<Vector3> avoidA, float distA, List<Vector3> avoidB, float distB, float wallClearance)
        {
            if (!WormMath.InsideBox(p, colony.BoxCenter, colony.BoxHalfSize)) return false;
            for (int k = 0; k < avoidA.Count; k++)
                if ((avoidA[k] - p).sqrMagnitude < distA * distA) return false;
            for (int k = 0; k < avoidB.Count; k++)
                if ((avoidB[k] - p).sqrMagnitude < distB * distB) return false;
            for (int k = 0; k < spawners.Length; k++)
                if ((spawners[k] - p).sqrMagnitude < SpawnerClearance * SpawnerClearance) return false;
            int solid = WormPhysics.SolidMask;
            if (Physics.Raycast(p + Vector3.up * 0.05f, Vector3.up, 1.2f, solid, QueryTriggerInteraction.Ignore)) return false;    // headroom
            if (Physics.CheckSphere(p + Vector3.up * 0.55f, 0.45f, solid, QueryTriggerInteraction.Ignore)) return false;               // a prop on the spot
            return FreeRadius(p, wallClearance, solid) >= wallClearance;
        }

        /// <summary>Distance to the nearest wall over 16 horizontal rays at knee height, at most <paramref name="max"/>.</summary>
        private static float FreeRadius(Vector3 floor, float max, int mask)
        {
            Vector3 o = floor + Vector3.up * 0.5f;
            float best = max;
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI * 0.125f;
                if (Physics.Raycast(o, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), out RaycastHit hit, best, mask, QueryTriggerInteraction.Ignore) && hit.distance < best) best = hit.distance;
            }
            return best;
        }

        /// <summary>A brood place 1 to 3 m from the nest: floor within a step of it, a clear line from above the mound, inside the box, not on another worm.</summary>
        private static bool BroodPoint(Vector3 nest, System.Random rng, WormColony colony, List<Vector3> placed, out Vector3 point)
        {
            int solid = WormPhysics.SolidMask;
            for (int t = 0; t < BroodTries; t++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = Between(rng, BroodRingMin, BroodRingMax);
                Vector3 from = nest + new Vector3(Mathf.Cos(a) * r, 1.2f, Mathf.Sin(a) * r);
                if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, 3f, solid, QueryTriggerInteraction.Ignore)) continue;
                if (Vector3.Angle(hit.normal, Vector3.up) > WormPhysics.MaxSlopeDeg) continue;
                if (Mathf.Abs(hit.point.y - nest.y) > 1f || !WormMath.InsideBox(hit.point, colony.BoxCenter, colony.BoxHalfSize)) continue;
                if (Physics.Linecast(nest + Vector3.up * 0.6f, hit.point + Vector3.up * 0.3f, solid, QueryTriggerInteraction.Ignore)) continue;
                bool apart = true;
                for (int k = 0; k < placed.Count && apart; k++)
                    if ((placed[k] - hit.point).sqrMagnitude < 0.04f) apart = false;
                if (!apart) continue;
                point = hit.point;
                return true;
            }
            point = nest;
            return false;
        }

        private static float Between(System.Random rng, float min, float max)
        {
            if (max <= min) return (min + max) * 0.5f;
            return min + (float)rng.NextDouble() * (max - min);
        }
    }
}
