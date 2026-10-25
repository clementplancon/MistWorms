#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using MistWorms.Colony;
using MistWorms.Dev.Preview;
using MistWorms.Config;
using MistWorms.Mass;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// Spec §15.5 S08 (plan 4B-2): a mass of 200 in a mine corridor of about 2 m.
    /// The corridor is found by geometry, not by paths: the floor of every room of the dungeon is sampled on a 1 m grid, and a floor point whose cross
    /// width is between 1.5 and 3.2 m on a straight axis, with a flat floor and no wall for at least 14 m, is a corridor (the longest, narrowest wins).
    /// The player waits at the far end (about 15 m), the mass is created at the other end with the normal AI.
    /// Criteria: progress of at least 10 m (less if the corridor is short, see the log) in 20 s; the Stream form observed (a chain of hit spheres);
    /// a hit injected on the farthest sphere of the chain, outside the body capsule, reaches the mass (one ApplyDamage).
    /// Stairs part: the mass must climb at least 1.5 m of stairs found by geometry and checked with the vanilla pathfinder, within 20 s; if the
    /// Humanoid agent fails, HumanoidNoSwim then GoblinBruteSize are tried and the one that works is reported (decisions.md 24).
    /// Path rate of the hunt and AI cost are logged. Parameters: n (200), seconds (20).
    /// </summary>
    internal sealed class S08_MassStream : IDevScenario
    {
        private const float GridStep = 1f;
        private const float CorridorMinWidth = 1.5f, CorridorMaxWidth = 3.2f;
        private const float WalkMinWidth = 1.4f, WalkMaxWidth = 3.5f;
        private const float WalkStep = 0.5f;
        private const float MaxRun = 30f;
        private const float MinRun = 14f;
        private const float GoodRun = 24f;
        private const float FloorFlat = 0.25f;
        private const float StairMinRise = 1.5f, StairMaxRise = 4.5f, StairMinRun = 2.5f, StairMaxRun = 12f;
        private const int StairCandidates = 40;
        private const float WideMinRise = 1.2f;
        private const int WideTests = 1500, WideCandidates = 80;
        private const float StartTolerance = 0.45f;     // the floor under the spawn must be this close to the foot of the stairs
        private const int MaxStartOffset = 3;          // metres, outward (away from the top)
        private const float FrameBudgetMs = 8f;
        private const float StartInset = 1.2f;
        private const float TargetDistance = 15f;
        private const float ForceTargetAfter = 3f;

        public string Name => "S08_MassStream";

        private sealed class Corridor
        {
            public Vector3 A, B, Dir;      // Dir goes from A to B
            public float Length, Width;
        }

        private readonly List<Vector3> _floors = new List<Vector3>(8192);
        private readonly RaycastHit[] _hits = new RaycastHit[12];
        private readonly List<GameObject> _walls = new List<GameObject>();

        public IEnumerator Run(ScenarioContext ctx)
        {
            int n = ctx.ParamInt("n", 200);
            _wallInner = 2f * MassMath.Radius(n) + 0.1f;
            float seconds = ctx.ParamFloat("seconds", 20f);
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 420f;

            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                yield return ctx.WaitFrames(3);

                yield return ctx.TpMine();
                if (!ctx.LastTpMine.Ok) yield break;

                Player player = Player.m_localPlayer;
                player.SetGodMode(true);
                player.SetGhostMode(false);   // a ghost is invisible to monsters
                yield return ctx.Wait(1f);

                WormColony colony = ColonyDev.FindOrCreateColony(player, out string how);
                ctx.Assert("colony", colony != null, "a colony", how);
                if (colony == null) yield break;

                DungeonGenerator generator = DevWorld.NearestGenerator(player.transform.position);
                ctx.Assert("dungeon_found", generator != null, "a dungeon generator", generator != null ? generator.name : "none");
                if (generator == null) yield break;

                // In the mine: the stairs (the geometry of the mines gives stairs but no straight corridor to rely on), and a corridor search that
                // is informative only.
                yield return Scan(ctx, generator);
                yield return StairsPart(ctx, colony, seconds);
                if (ctx.ParamBool("mineCorridor", true))
                {
                    Corridor mineCorridor = null;
                    yield return FindCorridor(ctx, c => mineCorridor = c);
                    ctx.Log(mineCorridor != null
                        ? $"(information) the mine has a straight corridor: width {mineCorridor.Width:0.00} m, length {mineCorridor.Length:0.0} m from {mineCorridor.A}"
                        : "(information) the mine has no straight corridor of the wanted size");
                }

                // The corridor itself is built on open flat ground out of vanilla wall pieces (decisions: the geometry of the mines does not guarantee one).
                yield return ctx.MoveToOpenGround(3f, AcceptCorridorSpot);
                if (!ctx.LastOpenGround.Found) yield break;
                player = Player.m_localPlayer;
                player.SetGodMode(true);
                player.SetGhostMode(false);
                S18_GroupCoherence.ClearCreatures(ctx, player);
                WormColony openColony = ColonyDev.FindOrCreateColony(player, out string openHow);
                ctx.Assert("open_colony", openColony != null, "a colony", openHow);
                if (openColony == null) yield break;

                Corridor built = null;
                yield return BuildCorridor(ctx, c => built = c);
                if (built == null) yield break;
                yield return RunCorridor(ctx, openColony, n, seconds, built);
                yield return FallRescuePart(ctx, openColony);
            }
            finally
            {
                foreach (GameObject wall in _walls)
                {
                    if (wall == null) continue;
                    if (ZNetScene.instance != null) ZNetScene.instance.Destroy(wall);
                    else UnityEngine.Object.Destroy(wall);
                }
                _walls.Clear();
                MassDev.KillAll();
                ctx.Console("mw_kill");
                Player p = Player.m_localPlayer;
                if (p != null) { p.SetGodMode(false); p.SetGhostMode(false); }
            }
        }

        // ------------------------------------------------------------------ sampling the floors of the dungeon

        private IEnumerator Scan(ScenarioContext ctx, DungeonGenerator generator)
        {
            _floors.Clear();
            Room[] rooms = generator.GetComponentsInChildren<Room>();
            var watch = Stopwatch.StartNew();
            int columns = 0;
            foreach (Room room in rooms)
            {
                Vector3 size = new Vector3(room.m_size.x, room.m_size.y, room.m_size.z);
                Transform t = room.transform;
                for (float x = -size.x * 0.5f; x <= size.x * 0.5f; x += GridStep)
                {
                    for (float z = -size.z * 0.5f; z <= size.z * 0.5f; z += GridStep)
                    {
                        Vector3 top = t.TransformPoint(new Vector3(x, size.y * 0.5f, z));
                        int count = Physics.RaycastNonAlloc(top, Vector3.down, _hits, size.y + 4f, WormPhysics.CharacterSolidMask, QueryTriggerInteraction.Ignore);
                        for (int k = 0; k < count; k++)
                            if (_hits[k].normal.y >= 0.85f) _floors.Add(_hits[k].point);
                        columns++;
                    }
                    if (watch.ElapsedMilliseconds > FrameBudgetMs)
                    {
                        yield return null;
                        watch.Restart();
                    }
                }
            }
            ctx.Log($"floor scan: {rooms.Length} rooms, {columns} columns, {_floors.Count} floor samples");
        }

        // ------------------------------------------------------------------ corridor

        private float CrossWidth(Vector3 p, Vector3 dir)
        {
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 o = p + Vector3.up * 0.6f;
            if (!Physics.Raycast(o, side, out RaycastHit a, 4f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore)) return 99f;
            if (!Physics.Raycast(o, -side, out RaycastHit b, 4f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore)) return 99f;
            return a.distance + b.distance;
        }

        /// <summary>Length (m) of flat, narrow, wall free floor from <paramref name="p"/> along <paramref name="dir"/>; <paramref name="end"/> is the last floor point.</summary>
        private float WalkRun(Vector3 p, Vector3 dir, out Vector3 end)
        {
            end = p;
            Vector3 prev = p;
            float len = 0f;
            while (len < MaxRun)
            {
                Vector3 q = p + dir * (len + WalkStep);
                if (!Physics.Raycast(q + Vector3.up * 0.8f, Vector3.down, out RaycastHit floor, 1.6f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore)) break;
                if (floor.normal.y < 0.85f || Mathf.Abs(floor.point.y - prev.y) > FloorFlat) break;
                if (Physics.Linecast(prev + Vector3.up * 0.6f, floor.point + Vector3.up * 0.6f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore)) break;
                float w = CrossWidth(floor.point, dir);
                if (w < WalkMinWidth || w > WalkMaxWidth) break;
                prev = floor.point;
                len += WalkStep;
            }
            end = prev;
            return len;
        }

        private IEnumerator FindCorridor(ScenarioContext ctx, Action<Corridor> done)
        {
            Corridor best = null;
            float bestScore = float.MinValue;
            var found = new List<Corridor>();
            var watch = Stopwatch.StartNew();
            int evaluated = 0;
            Vector3[] axes =
            {
                new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 0f),
                new Vector3(0.7071f, 0f, 0.7071f), new Vector3(0.7071f, 0f, -0.7071f),
            };

            for (int i = 0; i < _floors.Count; i++)
            {
                Vector3 p = _floors[i];
                if (InsideFound(found, p)) continue;
                foreach (Vector3 axis in axes)
                {
                    float w = CrossWidth(p, axis);
                    if (w < CorridorMinWidth || w > CorridorMaxWidth) continue;
                    float back = WalkRun(p, -axis, out Vector3 a);
                    float fwd = WalkRun(p, axis, out Vector3 b);
                    float len = back + fwd;
                    evaluated++;
                    if (len < MinRun) continue;
                    var c = new Corridor { A = a, B = b, Dir = axis, Length = len, Width = w };
                    found.Add(c);
                    float score = Mathf.Min(len, GoodRun) - 3f * w;
                    if (score > bestScore) { bestScore = score; best = c; }
                    break;
                }
                if (watch.ElapsedMilliseconds > FrameBudgetMs)
                {
                    yield return null;
                    watch.Restart();
                }
            }
            ctx.Log($"corridor search: {evaluated} candidate axes walked, {found.Count} corridors of at least {MinRun} m");
            done(best);
        }

        private static bool InsideFound(List<Corridor> found, Vector3 p)
        {
            foreach (Corridor c in found)
            {
                Vector3 ap = p - c.A;
                float along = Vector3.Dot(ap, c.Dir);
                if (along < -1f || along > c.Length + 1f) continue;
                Vector3 off = ap - c.Dir * along;
                off.y = 0f;
                if (off.sqrMagnitude < 2.25f) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ the built corridor

        private float _wallInner = 2f;                 // inner width of the corridor: 2 r(n) + 0.1 m of the mass under test (set in Run), narrower than the passage a mass of that size needs (2 r + 0.2)
        private const float WallSink = 0.1f;
        private const float CorridorLength = 18f;      // at least; a whole number of wall pieces
        private const float FlatTolerance = 0.5f;
        private const float PathRetries = 12;
        private const float SearchRing = 150f;
        private const int MaxSearchHops = 3;
        private const float HopMeters = 150f;
        private static readonly string[] WallPrefabs = { "stone_wall_4x2", "woodwall" };

        /// <summary>Height of the terrain collider under p (a ray from 30 m above, 80 m long); the heightmap if no collider is loaded there; NaN if neither.</summary>
        private static float TerrainY(Vector3 p)
        {
            if (Physics.Raycast(new Vector3(p.x, p.y + 30f, p.z), Vector3.down, out RaycastHit hit, 80f, LayerMask.GetMask(VanillaNames.LayerTerrain), QueryTriggerInteraction.Ignore)) return hit.point.y;
            return ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(p, out float h) ? h : float.NaN;
        }

        private static float GroundY(Vector3 p)
        {
            float y = TerrainY(p);
            return float.IsNaN(y) ? p.y : y;
        }

        /// <summary>Flat, free ground for a corridor starting 3 m ahead (+Z) of the player's ground point: 6 m wide, 26 m long, nothing solid but terrain.</summary>
        private static float s_flatTolerance = FlatTolerance;
        private static readonly float[] FlatTolerances = { 0.5f, 0.8f, 1.2f };   // the flattest strip within reach first

        private static bool AcceptCorridorSpot(Vector3 ground) => AcceptCorridorSpot(ground, false);

        /// <summary>Strict: unknown terrain (no collider and no heightmap loaded yet) is not flat. Lenient: it passes (candidates far from the player are not loaded).</summary>
        private static bool AcceptCorridorSpot(Vector3 ground, bool strict)
        {
            Vector3 start = ground + Vector3.forward * 3f;
            for (float z = 0f; z <= CorridorLength + 6f; z += 1f)
            {
                for (float x = -3f; x <= 3f; x += 1.5f)
                {
                    float y = TerrainY(start + new Vector3(x, 0f, z));
                    if (float.IsNaN(y)) { if (strict) return false; continue; }
                    if (Mathf.Abs(y - ground.y) > s_flatTolerance) return false;
                }
            }
            int mask = WormPhysics.SolidMask & ~LayerMask.GetMask(VanillaNames.LayerTerrain);
            Vector3 center = start + new Vector3(0f, 0.4f + 1.5f, (CorridorLength + 6f) * 0.5f);
            return !Physics.CheckBox(center, new Vector3(3f, 1.5f, (CorridorLength + 6f) * 0.5f), Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Two rows of vanilla wall pieces 2.0 m apart (inner faces), no roof, about 18 m long, along +Z on the ground in front of the player.
        /// The size of a piece is read from its colliders, so the scenario does not assume which way the prefab runs. The vanilla pathfinder
        /// rebuilds a navmesh tile when it is asked for a path after the tile was last built, so the path is requested until it fits the corridor.
        /// </summary>
        private IEnumerator BuildCorridor(ScenarioContext ctx, Action<Corridor> done)
        {
            ZNetScene scene = ZNetScene.instance;
            GameObject prefab = null;
            foreach (string name in WallPrefabs)
            {
                prefab = scene != null ? scene.GetPrefab(name) : null;
                if (prefab != null) { ctx.Log($"wall piece: {name}"); break; }
            }
            ctx.Assert("wall_prefab", prefab != null, string.Join(" or ", WallPrefabs), prefab != null ? prefab.name : "none");
            if (prefab == null) yield break;

            Player player = Player.m_localPlayer;
            // The spot MoveToOpenGround kept is not always the one the flatness test saw (the player settles after the teleport): check it again, and
            // look for a flat strip around if needed. The player is moved there.
            Vector3 basePoint = player.transform.position;
            basePoint.y = GroundY(basePoint);
            s_flatTolerance = FlatTolerances[0];
            bool playerSpotFlat = AcceptCorridorSpot(basePoint, true);
            s_flatTolerance = FlatTolerance;
            if (!playerSpotFlat)
            {
                bool found = false;
                int tested = 0;
                Vector3 searchCenter = player.transform.position;
                var hopDirections = new[] { Vector3.right, Vector3.forward, Vector3.left, Vector3.back };
                // 150 m around the player, then up to 3 hops of 150 m (as MoveToOpenGround) to load other ground: strips are searched up to about 300 m from the start.
                for (int hop = 0; hop <= MaxSearchHops && !found; hop++)
                {
                    foreach (float tolerance in FlatTolerances)
                    {
                        s_flatTolerance = tolerance;
                        for (float ring = 0f; ring <= SearchRing && !found; ring += 4f)
                        {
                            int steps = ring < 1f ? 1 : Mathf.Max(8, Mathf.CeilToInt(ring * 0.8f));
                            for (int k = 0; k < steps && !found; k++)
                            {
                                if (++tested % 12 == 0) yield return null;
                                float a = k * Mathf.PI * 2f / steps;
                                Vector3 c = searchCenter + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * ring;
                                c.y = GroundY(c);
                                if (ZoneSystem.instance != null && c.y < ZoneSystem.instance.m_waterLevel + 1.5f) continue;
                                if (!AcceptCorridorSpot(c, true)) continue;
                                basePoint = c;
                                found = true;
                            }
                        }
                        if (found) { ctx.Log($"flat strip found with a tolerance of {tolerance} m after {tested} candidates, {hop} hop(s)"); break; }
                    }
                    if (found || hop == MaxSearchHops) break;
                    searchCenter += hopDirections[hop % hopDirections.Length] * HopMeters;
                    float h = WorldGenerator.instance != null ? WorldGenerator.instance.GetHeight(searchCenter.x, searchCenter.z) : player.transform.position.y;
                    var hopped = new TeleportOutcome();
                    ctx.Log($"no flat strip yet ({tested} candidates): hopping to {searchCenter.x:0},{searchCenter.z:0} to load other ground");
                    yield return DevWorld.TeleportPlayer(new Vector3(searchCenter.x, Mathf.Max(h, ZoneSystem.instance.m_waterLevel) + 2f, searchCenter.z), Quaternion.identity, true, 60f, hopped);
                    if (!hopped.Ok) { ctx.Log("hop failed: " + hopped.Error); break; }
                    player = Player.m_localPlayer;
                    searchCenter = player.transform.position;
                    yield return ctx.Wait(1.5f);
                }
                s_flatTolerance = FlatTolerance;
                ctx.Log(found ? $"the player's spot was not flat enough: the corridor is built at {basePoint}" : "no flat strip found around the player: the corridor goes where the player stands");
                ctx.Assert("corridor_site_flat", found, $"a strip 6 x {CorridorLength + 6:0} m flat within {FlatTolerance} m near the player", found ? basePoint.ToString() : "none within about 300 m");
                if (found)
                {
                    var move = new TeleportOutcome();
                    yield return DevWorld.TeleportPlayer(basePoint + Vector3.up * 0.2f, Quaternion.identity, false, 30f, move);
                    player = Player.m_localPlayer;
                    player.SetGodMode(true);
                    player.SetGhostMode(false);
                    S18_GroupCoherence.ClearCreatures(ctx, player);
                    yield return ctx.Wait(1f);
                }
            }
            Vector3 start = basePoint + Vector3.forward * 3f;
            start.y = GroundY(start);

            // Size and offset of a piece, from a probe at identity rotation.
            GameObject probe = UnityEngine.Object.Instantiate(prefab, start, Quaternion.identity);
            Physics.SyncTransforms();
            if (!Extent(probe, out Bounds b0))
            {
                Destroy(probe);
                ctx.Assert("wall_has_colliders", false, "a piece with colliders", "none");
                yield break;
            }
            Vector3 size0 = b0.size, off0 = b0.center - start;
            float bottom0 = b0.min.y - start.y;
            Destroy(probe);
            yield return null;

            bool turn = size0.x >= size0.z;                       // make the long side run along Z
            Quaternion rot = turn ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            float length = turn ? size0.x : size0.z, thickness = turn ? size0.z : size0.x;
            Vector3 off = rot * off0;
            int segments = Mathf.Max(1, Mathf.CeilToInt(CorridorLength / length));
            float total = segments * length;
            ctx.Log($"wall piece size {size0.x:0.00} x {size0.y:0.00} x {size0.z:0.00} m, rotated={turn}: {segments} pieces per side, corridor {_wallInner:0.0} m wide and {total:0.0} m long");

            ctx.Assert("wall_size_sane", length >= 1f && length <= 8f && thickness > 0.05f && thickness <= 1.5f && size0.y >= 1f && size0.y <= 4f,
                "length 1-8 m, thickness under 1.5 m, height 1-4 m", $"{size0.x:0.00} x {size0.y:0.00} x {size0.z:0.00}");

            var wanted = new List<Vector3>();
            for (int side = -1; side <= 1; side += 2)
            {
                for (int j = 0; j < segments; j++)
                {
                    float cx = start.x + side * (_wallInner * 0.5f + thickness * 0.5f);
                    float cz = start.z + length * (j + 0.5f);
                    // The foot of a piece is the lowest ground under it (5 points), so no gap is left under the wall on a slope.
                    float groundY = float.MaxValue;
                    for (int q = 0; q < 5; q++)
                        groundY = Mathf.Min(groundY, GroundY(new Vector3(cx + (q % 2 == 0 ? -thickness * 0.5f : thickness * 0.5f), start.y, cz + (q - 2) * length * 0.25f)));
                    var pivot = new Vector3(cx - off.x, groundY - bottom0 - WallSink, cz - off.z);
                    GameObject wallObject = UnityEngine.Object.Instantiate(prefab, pivot, rot);
                    // WearNTear destroys a piece it finds unsupported (the terrain collider under it may not be there yet): the test walls are props.
                    foreach (WearNTear wear in wallObject.GetComponentsInChildren<WearNTear>()) wear.enabled = false;
                    _walls.Add(wallObject);
                    wanted.Add(new Vector3(cx, groundY, cz));
                }
            }
            yield return ctx.WaitFrames(3);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();

            // Each wall is checked in the world with its own colliders: long side along the corridor axis (within 5 degrees), centre at 1.0 m plus
            // half its thickness from the axis. A wall that lies across is turned by 90 degrees and put back at its place, then checked again.
            int aligned = 0, turned = 0;
            var notes = new List<string>();
            for (int w = 0; w < _walls.Count; w++)
            {
                if (FixWall(_walls[w], wanted[w], out bool wasTurned, out string note)) aligned++;
                if (wasTurned) turned++;
                if (note != null && notes.Count < 4) notes.Add(note);
            }
            yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
            ctx.Assert("walls_aligned", aligned == _walls.Count, $"{_walls.Count} walls parallel to the corridor, at 1.0 m + half thickness from the axis",
                $"{aligned}/{_walls.Count} ({turned} had to be turned){(notes.Count > 0 ? "; " + string.Join("; ", notes) : string.Empty)}");

            var corridor = new Corridor { A = start, B = start + Vector3.forward * total, Dir = Vector3.forward, Length = total, Width = _wallInner };
            yield return ctx.Wait(1f);   // a wall that is going to be destroyed or to fall must have done so before the measure
            Physics.SyncTransforms();
            int alive = 0;
            foreach (GameObject w in _walls) if (w != null && w.activeInHierarchy) alive++;
            Vector3 mid = start + Vector3.forward * (total * 0.5f);
            // On a slope the walls stand higher than the middle of the floor: measure at 0.6 m above the foot of the walls around the middle.
            float footY = float.MaxValue;
            foreach (GameObject w in _walls)
                if (w != null && Extent(w, out Bounds wb) && Mathf.Abs(wb.center.z - mid.z) <= length * 0.51f) footY = Mathf.Min(footY, wb.min.y);
            if (footY < float.MaxValue) mid.y = Mathf.Max(mid.y, footY);
            float measured = CrossWidth(mid, Vector3.forward);
            string detail = string.Empty;
            if (Mathf.Abs(measured - _wallInner) > 0.15f)
            {
                // Which side is missing, and what the rays see: the terrain and the pieces at this spot.
                Vector3 o = mid + Vector3.up * 0.6f;
                bool r = Physics.Raycast(o, Vector3.right, out RaycastHit hr, 4f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore);
                bool l = Physics.Raycast(o, Vector3.left, out RaycastHit hl, 4f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore);
                detail = $"; right ray {(r ? hr.collider.name + " at " + hr.distance.ToString("0.00") : "nothing")}, left ray {(l ? hl.collider.name + " at " + hl.distance.ToString("0.00") : "nothing")}, " +
                         $"ground at the spot {GroundY(mid):0.00} (ray origin {o.y:0.00})";
                for (int w = 0; w < _walls.Count && w < 4; w++)
                    detail += _walls[w] != null && Extent(_walls[w], out Bounds wb) ? $"; wall {w} centre {wb.center} size {wb.size}" : $"; wall {w} gone";
            }
            ctx.Assert("corridor_built", alive == 2 * segments && Mathf.Abs(measured - _wallInner) <= 0.15f,
                $"{2 * segments} pieces, inner width {_wallInner:0.0} m", $"{alive}/{2 * segments} pieces alive, measured width {measured:0.00} m{detail}");

            // The path through the corridor (lazy navmesh, pieces included once the tile is rebuilt).
            var path = new List<Vector3>(32);
            bool pathOk = false;
            Vector3 from = start + Vector3.forward * StartInset, to = start + Vector3.forward * (total - 1.5f);
            for (int attempt = 0; attempt < PathRetries && !pathOk; attempt++)
            {
                if (Pathfinding.instance != null && Pathfinding.instance.GetPath(from, to, path, Pathfinding.AgentType.Humanoid) && path.Count > 0)
                {
                    Vector3 end = path[path.Count - 1];
                    float maxSide = 0f;
                    foreach (Vector3 p in path) maxSide = Mathf.Max(maxSide, Mathf.Abs(p.x - start.x));
                    pathOk = Utils.DistanceXZ(end, to) <= 1.5f && maxSide <= _wallInner * 0.5f + 0.1f;
                }
                if (!pathOk) yield return ctx.Wait(1f);
            }
            ctx.Assert("corridor_path", pathOk, "a vanilla path along the corridor, inside the walls", pathOk ? $"{path.Count} points" : "none after retries");
            done(corridor);
        }

        /// <summary>
        /// Puts a wall piece where it should be by its collider bounds: long side along Z, centre at <paramref name="center"/> (xz), foot on the ground.
        /// Returns whether it is correct afterwards (aligned within 5 degrees of the axis, centre within 0.1 m of its place).
        /// </summary>
        private static bool FixWall(GameObject wall, Vector3 center, out bool turned, out string note)
        {
            turned = false;
            note = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Physics.SyncTransforms();
                if (!Extent(wall, out Bounds b)) { note = wall.name + ": no colliders"; return false; }
                if (b.size.z >= b.size.x) break;
                wall.transform.RotateAround(b.center, Vector3.up, 90f);
                turned = true;
            }
            Physics.SyncTransforms();
            if (!Extent(wall, out Bounds fixedBounds)) { note = wall.name + ": no colliders"; return false; }
            var delta = new Vector3(center.x - fixedBounds.center.x, center.y - WallSink - fixedBounds.min.y, center.z - fixedBounds.center.z);
            wall.transform.position += delta;
            Physics.SyncTransforms();
            Extent(wall, out Bounds b2);

            float yaw = wall.transform.eulerAngles.y;
            float off90 = Mathf.Abs(Mathf.DeltaAngle(Mathf.Repeat(yaw, 90f), 0f));
            bool parallel = b2.size.z > b2.size.x && (off90 < 5f || off90 > 85f);
            bool placed = Mathf.Abs(b2.center.x - center.x) <= 0.1f && Mathf.Abs(b2.center.z - center.z) <= 0.1f;
            if (!parallel || !placed)
                note = $"{wall.name}: bounds {b2.size.x:0.00} x {b2.size.z:0.00}, centre {b2.center}, wanted {center}, yaw {yaw:0}";
            return parallel && placed;
        }

        private static bool Extent(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Collider c in go.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger) continue;
                if (!any) { bounds = c.bounds; any = true; }
                else bounds.Encapsulate(c.bounds);
            }
            return any;
        }

        private static void Destroy(GameObject go)
        {
            if (go == null) return;
            if (ZNetScene.instance != null) ZNetScene.instance.Destroy(go);
            else UnityEngine.Object.Destroy(go);
        }

        private IEnumerator RunCorridor(ScenarioContext ctx, WormColony colony, int n, float seconds, Corridor corridor)
        {
            ctx.Log($"corridor under test: width {corridor.Width:0.00} m, length {corridor.Length:0.0} m from {corridor.A} to {corridor.B}");

            // The player waits at the far end, the mass starts at the near end.
            Vector3 dir = corridor.Dir;
            Vector3 start = corridor.A + dir * StartInset;
            float distance = Mathf.Min(TargetDistance, corridor.Length - StartInset - 0.5f);
            Vector3 playerSpot = start + dir * distance;
            float radius = MassMath.Radius(n);
            float stop = radius + MWConfig.Mass.EngulfRange.Value;
            float required = Mathf.Min(10f, distance - stop - 1f);
            ctx.Log($"corridor: width {corridor.Width:0.00} m, length {corridor.Length:0.0} m; mass at {start}, player {distance:0.0} m away; mass radius {radius:0.00} m, stops at {stop:0.00} m; required progress {required:0.0} m");

            Player player = Player.m_localPlayer;
            var tp = new TeleportOutcome();
            yield return DevWorld.TeleportPlayer(playerSpot + Vector3.up * 0.2f, Quaternion.LookRotation(-dir), false, 30f, tp);
            ctx.Assert("player_in_corridor", tp.Ok, "teleported", tp.Ok ? "ok" : tp.Error);
            if (!tp.Ok) yield break;
            player = Player.m_localPlayer;
            player.SetGodMode(true);
            player.SetGhostMode(false);
            yield return ctx.Wait(1f);

            WritheMassState mass = MassDev.SpawnAt(start, n, colony.ZdoId, freezeAI: true);   // frozen for the overview shot
            ctx.Assert("mass_created", mass != null, $"a mass of {n}", mass != null ? mass.ZdoId.ToString() : "none");
            if (mass == null) yield break;
            var ai = mass.GetComponent<WritheMassAI>();
            Vector3 origin = mass.transform.position;

            // Overview from above for the human check of the walls and of the mass.
            Vector3 mid = corridor.A + dir * (corridor.Length * 0.5f);
            DevCamera.Free(mid + Vector3.up * 26f + Vector3.back * 4f, mid);
            yield return ctx.WaitFrames(15);
            yield return ctx.Shot("overview_corridor");
            DevCamera.Follow();
            mass.FreezeAI = false;

            float t0 = Time.time;
            float progress = 0f, maxProgress = 0f, timeToRequired = -1f, firstStream = -1f, maxSpheres = 0f, ribbon = 0f, ribbonLength = 0f;
            bool natural = false, forced = false, hitDone = false, hitRouted = false, hitOnce = false, outsideCapsule = false;
            string hitNote = "no stream chain seen";
            int domeAfter = 0;
            // Pass E: while streaming, where the drawn worms lie across the corridor (world positions recorded by the layout worker).
            WritheMassVisual look = mass.GetComponent<WritheMassVisual>();
            if (look != null) look.ProbeEnabled = true;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            int lastProbeFrame = -1, fluxSeen = 0, fluxCentred = 0, probeFrames = 0;
            // Samples are taken at full speed only (steady state). Once the required progress is reached the mass is sent back and forth along the corridor
            // (DevGoal, no engulfing) so that the ribbon is sampled for the rest of the run: about twice the samples of a single pass. The per-second share of
            // centred flux worms is logged (min, median, max).
            var secSeen = new List<int>(32);
            var secCentred = new List<int>(32);
            int bucket = -1, bucketSeen = 0, bucketCentred = 0;
            bool bouncing = false, toFar = false;
            Vector3 goalFar = playerSpot - dir * (stop + 0.5f), goalNear = start;
            float maxHeadingError = 0f, maxLateralWorld = 0f;
            float maxLateral = 0f, maxLateralFlux = 0f, maxLateralBorder = 0f, sampledWidth = 0f;
            while (Time.time - t0 < seconds && mass != null)
            {
                float elapsed = Time.time - t0;
                if (ai.Target != null) natural |= !forced;
                if (!forced && ai.Target == null && elapsed > ForceTargetAfter)
                {
                    forced = true;
                    ai.DevForcedTarget = player;
                    ctx.Log($"no target after {ForceTargetAfter} s: the player is forced as the target for the rest of the run");
                }

                if (timeToRequired >= 0f && !bouncing)
                {
                    bouncing = true;
                    toFar = false;
                    ai.DevGoal = goalNear;
                }
                else if (bouncing && Utils.DistanceXZ(mass.transform.position, toFar ? goalFar : goalNear) < 1.5f)
                {
                    toFar = !toFar;
                    ai.DevGoal = toFar ? goalFar : goalNear;
                }

                progress = Vector3.Dot(mass.transform.position - origin, dir);
                if (progress > maxProgress) maxProgress = progress;
                if (timeToRequired < 0f && progress >= required) timeToRequired = elapsed;
                if (mass.IsStream)
                {
                    if (firstStream < 0f) firstStream = elapsed;
                    maxSpheres = Mathf.Max(maxSpheres, mass.HitSphereCount);
                    ribbon = mass.StreamWidth;
                    ribbonLength = mass.RibbonLength;
                    MassJob probeJob = look != null ? look.Job : null;
                    if (probeJob != null && probeJob.ReadyIdx >= 0 && elapsed - firstStream > 1.5f && look.StreamK >= 0.999f && look.Speed >= 0.9f * MWConfig.Mass.MoveSpeed.Value)
                    {
                        MassOutput po = probeJob.Out[probeJob.ReadyIdx];
                        if (po.Valid && po.Probe != null && po.Frame != lastProbeFrame)
                        {
                            lastProbeFrame = po.Frame;
                            probeFrames++;
                            if ((int)elapsed != bucket)
                            {
                                if (bucket >= 0 && bucketSeen > 0) { secSeen.Add(bucketSeen); secCentred.Add(bucketCentred); }
                                bucket = (int)elapsed;
                                bucketSeen = bucketCentred = 0;
                            }
                            sampledWidth = mass.StreamWidth;
                            for (int k = 0; k < po.ProbeCount; k++)
                            {
                                maxHeadingError = Mathf.Max(maxHeadingError, Mathf.Min(Vector3.Angle(po.Forward, dir), Vector3.Angle(po.Forward, -dir)));
                                float latWorld = Mathf.Abs(Vector3.Dot(po.Probe[k].Pos - po.Root, side));       // against the corridor axis (informative: includes the heading error)
                                maxLateralWorld = Mathf.Max(maxLateralWorld, latWorld);
                                float lat = Mathf.Abs(Vector3.Dot(po.Probe[k].Pos - po.Root, po.Left));          // across the ribbon of the snapshot: the axis the layout clamps in
                                maxLateral = Mathf.Max(maxLateral, lat);
                                if (po.Probe[k].Role == MassLayout.RoleFlux)
                                {
                                    fluxSeen++;
                                    bucketSeen++;
                                    if (lat < 0.25f * sampledWidth) { fluxCentred++; bucketCentred++; }
                                    maxLateralFlux = Mathf.Max(maxLateralFlux, lat);
                                }
                                else if (po.Probe[k].Role == MassLayout.RoleBorder) maxLateralBorder = Mathf.Max(maxLateralBorder, lat);
                            }
                        }
                    }
                    if (!hitDone && elapsed - firstStream > 1f && mass.HitSphereCount >= 2)
                    {
                        hitDone = true;
                        HitChain(mass, out hitRouted, out hitOnce, out outsideCapsule, out hitNote);
                    }
                }
                else if (firstStream >= 0f)
                {
                    domeAfter++;
                }
                yield return null;
            }

            if (mass == null) { ctx.Assert("mass_alive", false, "the mass survives the run", "destroyed"); yield break; }
            ctx.Log($"after {Time.time - t0:0.0} s: progress {maxProgress:0.0} m (now {progress:0.0}), first stream at {(firstStream >= 0f ? firstStream.ToString("0.0") + " s" : "never")}, " +
                    $"ribbon {ribbon:0.00} x {ribbonLength:0.0} m, up to {maxSpheres} hit spheres, frames in dome after the stream {domeAfter}");
            ctx.Log($"target: sensed by the AI itself = {natural}, forced = {forced}; mode {MassStateCodes.Describe(mass.StateCode)}; sense: {ai.SenseNote ?? "(a target was found)"}; " +
                    $"player at {Player.m_localPlayer.transform.position}, mass started at {origin}, corridor {corridor.A} -> {corridor.B}");
            ctx.Log($"hunt path rate: {ai.PathTicks} ticks with a path, {ai.NoPathTicks} without (straight push), of {ai.MoveTicks} walking ticks; AI cost avg {ai.CostAvgMs:0.000} ms, max {ai.CostMaxMs:0.000} ms");
            ctx.Log($"colony path requests: {colony.PathRequests}, complete {colony.PathComplete}, partial {colony.PathPartial}, failed {colony.PathFailed}");

            if (look != null) look.ProbeEnabled = false;
            if (probeFrames > 0 && sampledWidth > 0f)
            {
                float centred = fluxSeen > 0 ? fluxCentred / (float)fluxSeen : 0f;
                if (bucketSeen > 0) { secSeen.Add(bucketSeen); secCentred.Add(bucketCentred); }
                var shares = new List<float>(secSeen.Count);
                for (int q = 0; q < secSeen.Count; q++) shares.Add(secCentred[q] / (float)secSeen[q]);
                shares.Sort();
                if (shares.Count > 0)
                    ctx.Log($"stream centred flux share per second over {shares.Count} s of steady state ({fluxSeen} flux samples): min {shares[0] * 100f:0} %, median {shares[shares.Count / 2] * 100f:0} %, max {shares[shares.Count - 1] * 100f:0} %");
                ctx.Log($"stream layout over {probeFrames} frames, ribbon {sampledWidth:0.00} m: flux within 0.25 w of the axis {centred * 100f:0} % of {fluxSeen}; largest lateral offset {maxLateral:0.00} m (flux {maxLateralFlux:0.00}, border {maxLateralBorder:0.00}), limit w/2 + 0.1 = {sampledWidth * 0.5f + 0.1f:0.00} m; largest heading error of the heap against the corridor {maxHeadingError:0.0} degrees, which puts the largest offset against the corridor axis at {maxLateralWorld:0.00} m");
                ctx.Assert("stream_flux_centred", centred >= 0.9f, ">= 90 % of the flux worms within 0.25 x width of the axis", $"{centred * 100f:0} %");
                ctx.Assert("stream_nothing_outside_the_ribbon", maxLateral <= sampledWidth * 0.5f + 0.1f, $"no drawn worm beyond w/2 + 0.1 = {sampledWidth * 0.5f + 0.1f:0.00} m", $"{maxLateral:0.00} m");
            }
            else ctx.Assert("stream_layout_sampled", false, "frames of the ribbon sampled", "none");
            ctx.Assert("target_sensed_by_the_ai", natural && !forced, "the mass acquires the player by itself (view or hearing)", forced ? "forced after 3 s" : natural.ToString());
            ctx.Assert("progress", maxProgress >= required, $">= {required:0.0} m in {seconds:0} s", $"{maxProgress:0.0} m{(timeToRequired >= 0f ? " in " + timeToRequired.ToString("0.0") + " s" : string.Empty)}");
            ctx.Assert("stream_observed", firstStream >= 0f && maxSpheres >= 2f, "Stream bit set with a chain of at least 2 hit spheres", firstStream >= 0f ? $"{maxSpheres} spheres, ribbon {ribbon:0.00} x {ribbonLength:0.0} m" : "never");
            ctx.Assert("hit_chain_outside_body", hitDone && outsideCapsule, "the tested sphere lies outside the body capsule", hitNote);
            ctx.Assert("hit_chain_routes_to_mass", hitDone && hitRouted && hitOnce, "an injected hit on a ribbon sphere reaches the mass once", hitNote);
            ctx.Assert("ai_cost_small", ai.CostAvgMs <= 0.5f, "average AI cost <= 0.5 ms", ai.CostAvgMs.ToString("0.000"));
            ctx.Assert("fall_rescues_corridor", mass.FallRescues == 0, 0, mass.FallRescues);
            MassDev.Destroy(mass);
            yield return ctx.Wait(0.3f);
        }

        /// <summary>
        /// Injects a hit the way a projectile reaches its target: the colliders at the centre of the farthest chain sphere are resolved with
        /// Projectile.FindHitObject, and Damage is called on the IDestructible found there.
        /// </summary>
        private static void HitChain(WritheMassState mass, out bool routed, out bool once, out bool outsideCapsule, out string note)
        {
            routed = once = outsideCapsule = false;
            int best = -1;
            float bestDistance = -1f;
            Vector3 bestCenter = default;
            float bestRadius = 0f;
            for (int i = 0; i < mass.HitSphereCount; i++)
            {
                if (!mass.TryGetHitSphere(i, out Vector3 c, out float r)) continue;
                Vector3 flat = c - mass.transform.position;
                flat.y = 0f;
                if (flat.magnitude > bestDistance) { bestDistance = flat.magnitude; best = i; bestCenter = c; bestRadius = r; }
            }
            CapsuleCollider capsule = mass.GetComponent<CapsuleCollider>();
            outsideCapsule = best >= 0 && bestDistance > capsule.radius + 0.1f;
            if (best < 0) { note = "no chain sphere"; return; }

            Physics.SyncTransforms();
            Collider[] colliders = Physics.OverlapSphere(bestCenter, 0.05f, 1 << ProxyLayer.Layer, QueryTriggerInteraction.Ignore);
            Collider expected = mass.HitColliderAt(best);
            bool contains = false, allMass = colliders.Length > 0;
            foreach (Collider col in colliders)
            {
                if (col == expected) contains = true;
                if (Projectile.FindHitObject(col) != mass.gameObject) allMass = false;
            }
            routed = contains && allMass;
            if (!routed)
            {
                note = $"sphere {best} at distance {bestDistance:0.00} m (radius {bestRadius:0.00}): overlap found {colliders.Length} colliders, contains the sphere {contains}, all resolve to the mass {allMass}";
                return;
            }

            IDestructible target = mass.gameObject.GetComponent<IDestructible>();
            var hit = new HitData();
            hit.m_damage.m_blunt = 5f;
            hit.m_point = bestCenter;
            hit.m_dir = Vector3.forward;
            hit.m_hitCollider = expected;
            int before = mass.DamageCalls;
            float hp = mass.Health;
            target.Damage(hit);
            once = mass.DamageCalls - before == 1 && mass.Health < hp;
            note = $"sphere {best} of {mass.HitSphereCount} at {bestDistance:0.00} m from the root (radius {bestRadius:0.00}), {colliders.Length} collider(s) there; ApplyDamage calls +{mass.DamageCalls - before}, hp {hp:0.#} -> {mass.Health:0.#}";
        }

        /// <summary>A mass sunk 0.5 m under the floor falls out of the world; the safety net must put it back on its last ground within 2 s.</summary>
        private IEnumerator FallRescuePart(ScenarioContext ctx, WormColony colony)
        {
            Player player = Player.m_localPlayer;
            if (player == null) yield break;
            Vector3 at = player.transform.position + Vector3.back * 6f;
            WritheMassState mass = MassDev.SpawnAt(at, 10, colony.ZdoId, freezeAI: true);
            ctx.Assert("fall_test_mass", mass != null, "a mass of 10", mass != null ? mass.ZdoId.ToString() : "none");
            if (mass == null) yield break;
            yield return ctx.Wait(1.5f);   // the ground must be stable for 0.5 s
            float groundY = mass.transform.position.y;
            ctx.Assert("fall_test_grounded", mass.Character.IsOnGround(), "on the ground", mass.Character.IsOnGround());

            // 0.5 m under open terrain is only pushed back by the heightmap collider: go 4 m down, under it, where the body falls freely.
            mass.DevSink(4f);
            float t0 = Time.time;
            while (mass != null && mass.FallRescues == 0 && Time.time - t0 < 3f) yield return null;
            float took = Time.time - t0;
            bool rescued = mass != null && mass.FallRescues == 1;
            yield return ctx.Wait(0.3f);
            float y = mass != null ? mass.transform.position.y : -9999f;
            ctx.Assert("fall_rescue", rescued && took <= 2f && y >= groundY - 0.3f, "rescued once within 2 s, back at the ground height",
                $"rescues {(mass != null ? mass.FallRescues : -1)} after {took:0.00} s, height {y - groundY:0.00} m from the ground");
            MassDev.Destroy(mass);
        }

        // ------------------------------------------------------------------ stairs

        private struct Stair
        {
            public Vector3 Low, High;
            public float Path;
            public Vector3 Start;      // validated spawn point at the foot of the stairs
            public int Offset;         // metres moved outward from Low to get a valid start
            public Vector3 Landing;    // where the player stands: on the upper level, well past the top
        }

        private IEnumerator StairsPart(ScenarioContext ctx, WormColony colony, float seconds)
        {
            Stair stair = default;
            bool have = false;
            // The navmesh of a zone is built lazily: the first requests may fail, so the search is repeated a few times.
            yield return ctx.Wait(4f);          // the rooms of the mine are still spawning asynchronously when the player arrives, and their navmesh comes after
            for (int attempt = 0; attempt < 5 && !have; attempt++)
            {
                // Attempts after the first widen the search: more pairs, more candidates, a rise from 1.2 m.
                yield return FindStairs(ctx, (s, ok) => { stair = s; have = ok; }, wide: attempt > 0);
                if (!have) yield return ctx.Wait(4f);
            }
            ctx.Assert("stairs_found", have, $"a rise of {StairMinRise}-{StairMaxRise} m within {StairMaxRun} m with a vanilla path",
                have ? $"{stair.Low} -> {stair.High}: rise {stair.High.y - stair.Low.y:0.00} m, path {stair.Path:0.0} m, start {stair.Start} ({stair.Offset} m outward)" : "none");
            if (!have) yield break;

            // The ranked candidates, best first: the first one where a mass really settles at the foot of the stairs.
            have = false;
            yield return PickStart(ctx, colony, (s, ok) => { stair = s; have = ok; });
            ctx.Assert("stairs_start_valid", have, "a candidate where the mass settles within " + StartTolerance + " m of the foot of the stairs", have ? $"start {stair.Start}" : "none of the first 8");
            if (!have) yield break;

            // The hunt stops at r(n) + EngulfRange from the player: with the player right at the top the mass could stop half way up the stairs.
            // The player therefore stands on the upper landing, at least 4 m beyond the top of the stairs.
            Vector3 landing = stair.Landing;
            ctx.Log($"landing for the player: {landing}, {Utils.DistanceXZ(landing, stair.High):0.0} m from the top of the stairs");
            var tp = new TeleportOutcome();
            yield return DevWorld.TeleportPlayer(landing + Vector3.up * 0.2f, Quaternion.identity, false, 30f, tp);
            ctx.Assert("player_on_stairs_top", tp.Ok, "teleported", tp.Ok ? "ok" : tp.Error);
            if (!tp.Ok) yield break;
            Player player = Player.m_localPlayer;
            player.SetGodMode(true);
            player.SetGhostMode(false);
            yield return ctx.Wait(1f);

            Pathfinding.AgentType[] agents = { Pathfinding.AgentType.Humanoid, Pathfinding.AgentType.HumanoidNoSwim, Pathfinding.AgentType.GoblinBruteSize };
            string worked = null;
            foreach (Pathfinding.AgentType agent in agents)
            {
                // Created exactly on the sampled floor point, then checked: it must stand within 0.3 m of the foot of the stairs, otherwise it is moved one
                // step (1 m) away from the top and tried again (a floor search from above could land on the upper level).
                // The start was validated during the search with the same calculation as the creation (floor within 0.3 m of the foot).
                WritheMassState mass = MassDev.SpawnExact(stair.Start, 50, colony.ZdoId, freezeAI: true);   // frozen while it settles
                if (mass != null)
                {
                    yield return ctx.Wait(0.5f);
                    float settled = mass != null ? mass.transform.position.y - stair.Low.y : 0f;
                    ctx.Log($"stairs start {stair.Start} ({stair.Offset} m outward): the mass settled {settled:0.00} m from the foot of the stairs");
                    if (mass == null || Mathf.Abs(settled) > StartTolerance) { MassDev.Destroy(mass); mass = null; }
                }
                if (mass == null) { ctx.Assert($"stairs_{agent}", false, "a mass starting within 0.3 m of the foot of the stairs", "none"); continue; }
                mass.FreezeAI = false;
                var ai = mass.GetComponent<WritheMassAI>();
                ai.PathAgent = agent;
                ai.DevForcedTarget = player;   // the stairs test is about the path, not about the senses (they are covered by the corridor part)
                ai.DevGoal = stair.High;       // walk to the top of the stairs itself: the hunt would stop 2 m short of a player standing beyond it
                float t0 = Time.time;
                float reached = -1f, bestY = stair.Low.y, nextSample = 0f;
                float stop = MassMath.Radius(50) + MWConfig.Mass.EngulfRange.Value;
                float rise = stair.High.y - stair.Low.y;
                float needed = Mathf.Min(StairMinRise, rise - 0.15f);
                string how = null;
                WritheMassVisual climbVisual = mass.GetComponent<WritheMassVisual>();
                float prevRootY = float.NaN, worstRootStep = 0f, worstRootGap = 0f, worstRootGapY = 0f;
                int rootSamples = 0, gapLogs = 0;
                while (Time.time - t0 < seconds && mass != null)
                {
                    if (climbVisual != null && climbVisual.Active && Time.deltaTime < 1f / 30f)
                    {
                        if (!float.IsNaN(prevRootY) && !climbVisual.RootState.Snapped) { worstRootStep = Mathf.Max(worstRootStep, Mathf.Abs(climbVisual.Root.y - prevRootY)); rootSamples++; }
                        Vector3 bodyPos = mass.transform.position;
                        float gapXZ = Utils.DistanceXZ(climbVisual.Root, bodyPos);
                        worstRootGap = Mathf.Max(worstRootGap, gapXZ);
                        worstRootGapY = Mathf.Max(worstRootGapY, Mathf.Abs(climbVisual.Root.y - bodyPos.y));
                        if ((gapXZ > 0.4f + 0.02f || Mathf.Abs(climbVisual.Root.y - bodyPos.y) > 0.8f) && gapLogs++ < 8)
                            ctx.Log($"  root diagnosis t={Time.time - t0:0.00}: root {climbVisual.Root}, body {bodyPos}, horizontal gap {gapXZ:0.00} m, vertical gap {climbVisual.Root.y - bodyPos.y:0.00} m, " +
                                    $"offset {climbVisual.RootState.Offset}, ground plane height at root {(climbVisual.Ground.Have ? climbVisual.Ground.HeightAt(climbVisual.Root.x, climbVisual.Root.z).ToString("0.00") : "none")} " +
                                    $"(samples {climbVisual.Ground.Samples}, hits {climbVisual.Ground.Hits}), step assists {ai.StepAssists} ({ai.LastAssistNote ?? "-"})");
                    }
                    if (climbVisual != null) prevRootY = climbVisual.Root.y;
                    Vector3 p = mass.transform.position;
                    if (p.y > bestY) bestY = p.y;
                    // Success only counts after a second of real walking: AI ticks toward the target (with or without a path).
                    bool walked = Time.time - t0 >= 1f && ai.MoveTicks > 0;
                    // Success: climbed the wanted rise, or stopped at contact distance from the player on the landing (floor within 0.5 m of its height).
                    if (walked && p.y - stair.Low.y >= needed) { reached = Time.time - t0; how = $"climbed {p.y - stair.Low.y:0.00} m"; break; }
                    if (walked && Utils.DistanceXZ(p, stair.High) <= 1.5f && Mathf.Abs(p.y - stair.High.y) < 0.6f) { reached = Time.time - t0; how = "at contact distance on the landing"; break; }
                    if (Time.time - t0 >= nextSample)
                    {
                        nextSample += 2f;
                        Rigidbody body = mass.GetComponent<Rigidbody>();
                        ctx.Log($"  {agent} t={Time.time - t0:0.0}: at {p} ({p.y - stair.Low.y:0.00} m up, {Utils.DistanceXZ(p, landing):0.0} m from the player), speed {(body != null ? body.linearVelocity.magnitude : 0f):0.00} m/s, " +
                                $"path ticks {ai.PathTicks}, no path {ai.NoPathTicks}, step assists {ai.StepAssists} (stalled checks {ai.StalledChecks}, last: {ai.LastAssistNote ?? "-"}), on ground {mass.Character.IsOnGround()}");
                    }
                    yield return null;
                }
                bool ok = reached >= 0f;
                ctx.Log($"stairs with {agent}: visual root: {rootSamples} frames sampled, largest vertical step {worstRootStep:0.000} m per frame, largest horizontal root-to-body gap {worstRootGap:0.00} m (vertical {worstRootGapY:0.00} m; a hop of the body is absorbed up to {MassRoot.MaxOffset:0.0} m at once, the rest in about 0.5 s)");
                if (rootSamples > 10)
                {
                    ctx.Assert($"stairs_root_smooth_{agent}", worstRootStep <= 0.08f, "<= 0.08 m per frame (StepAssist hides in the smoothed root)", worstRootStep.ToString("0.000"));
                    ctx.Assert($"stairs_root_near_body_{agent}", worstRootGap < 0.4f + 0.02f, "horizontal root to body gap < 0.4 m", worstRootGap.ToString("0.00"));
                }
                ctx.Log($"stairs with {agent}: {(ok ? how + " in " + reached.ToString("0.0") + " s" : "not reached")}, highest {bestY - stair.Low.y:0.00} m of {rise:0.00} m (needs {needed:0.00}), " +
                        (mass != null ? $"path ticks {ai.PathTicks}, no path {ai.NoPathTicks}, step assists {ai.StepAssists}, AI avg {ai.CostAvgMs:0.000} ms" : "mass lost"));
                if (mass != null) ctx.Assert($"fall_rescues_stairs_{agent}", mass.FallRescues == 0, 0, mass.FallRescues);
                MassDev.Destroy(mass);
                yield return ctx.Wait(0.3f);
                if (ok) { worked = agent.ToString(); break; }
            }
            ctx.Assert("stairs_climbed", worked != null, $"the mass climbs {StairMinRise} m or more in {seconds:0} s with one of Humanoid, HumanoidNoSwim, GoblinBruteSize",
                worked != null ? "agent " + worked : "none worked");
            if (worked != null) ctx.Log($"decisions.md 24: agent type that passes the stairs of the mines: {worked}");
        }

        /// <summary>A floor point on the upper level, 4 to 8 m beyond the top of the stairs (away from the bottom) and reachable from it; the top itself if none.</summary>
        private const float LandingMin = 4f, LandingMax = 10f;

        /// <summary>
        /// A floor point on the upper level, 4 to 10 m from the top of the stairs, preferably straight on beyond it, and reachable from the top by the
        /// vanilla pathfinder. The mass stops at r + 0.6 m from the player, so the player must stand well past the top for the mass to climb.
        /// </summary>
        private bool TryLanding(Vector3 low, Vector3 high, out Vector3 landing)
        {
            landing = high;
            Vector3 away = high - low;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) return false;
            away.Normalize();

            // The best aligned few, then the pathfinder on those.
            const int Keep = 6;
            var best = new Vector3[Keep];
            var score = new float[Keep];
            int kept = 0;
            for (int i = 0; i < _floors.Count; i++)
            {
                Vector3 p = _floors[i];
                if (Mathf.Abs(p.y - high.y) > 0.3f) continue;
                Vector3 d = p - high;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist < LandingMin || dist > LandingMax) continue;
                float dot = Vector3.Dot(d / dist, away);
                if (dot < 0f) continue;
                int slot = kept < Keep ? kept : -1;
                if (slot < 0)
                {
                    int worst = 0;
                    for (int k = 1; k < Keep; k++) if (score[k] < score[worst]) worst = k;
                    if (score[worst] < dot) slot = worst;
                }
                if (slot < 0) continue;
                if (slot == kept) kept++;
                best[slot] = p;
                score[slot] = dot;
            }

            var path = new List<Vector3>(32);
            for (int round = 0; round < kept; round++)
            {
                int top = 0;
                for (int k = 1; k < kept; k++) if (score[k] > score[top]) top = k;
                Vector3 p = best[top];
                score[top] = float.MinValue;
                // From the foot, not from the top: the mass must be able to walk the whole way, not reach a lower room next to the landing.
                if (Pathfinding.instance == null || !Pathfinding.instance.GetPath(low, p, path, Pathfinding.AgentType.Humanoid) || path.Count == 0) continue;
                Vector3 end = path[path.Count - 1];
                if (Utils.DistanceXZ(end, p) > 1.5f || Mathf.Abs(end.y - p.y) > 0.8f) continue;
                landing = p;
                return true;
            }
            return false;
        }

        /// <summary>
        /// The spawn point of a mass at the foot of the stairs, computed like the mass itself does (WritheMassState.SafeSpawnPosition) but without creating
        /// one: the floor it finds must be within 0.3 m of <paramref name="low"/>. Tries the point, then 1 to 3 m outward (away from the top).
        /// </summary>
        private static bool ValidStart(Vector3 low, Vector3 high, out Vector3 start, out int offset)
        {
            Vector3 away = low - high;
            away.y = 0f;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.back;
            for (offset = 0; offset <= MaxStartOffset; offset++)
            {
                Vector3 q = low + away * offset;
                float floor = WritheMassState.SafeSpawnPosition(q).y - 0.1f;
                if (Mathf.Abs(floor - low.y) <= StartTolerance) { start = q; return true; }   // q itself: the mass applies the same calculation on creation
            }
            start = default;
            offset = 0;
            return false;
        }

        private readonly List<Stair> _ranked = new List<Stair>();

        private IEnumerator PickStart(ScenarioContext ctx, WormColony colony, Action<Stair, bool> done)
        {
            for (int i = 0; i < _ranked.Count && i < 8; i++)
            {
                Stair s = _ranked[i];
                WritheMassState m = MassDev.SpawnExact(s.Start, 50, colony.ZdoId, freezeAI: true);
                if (m == null) continue;
                yield return ctx.Wait(0.6f);
                float settled = m != null ? m.transform.position.y - s.Low.y : float.NaN;
                bool ok = m != null && Mathf.Abs(settled) <= StartTolerance;
                if (!ok && m != null)
                {
                    // What is under the start: every collider a ray crosses, to see which one the sample came from and what the body fell through.
                    RaycastHit[] hits = Physics.RaycastAll(s.Start + Vector3.up * 2f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Collide);
                    var sb = new System.Text.StringBuilder();
                    foreach (RaycastHit h in hits)
                        sb.Append($" [{h.collider.name} layer {LayerMask.LayerToName(h.collider.gameObject.layer)} {(h.collider.isTrigger ? "trigger " : string.Empty)}{(h.collider is MeshCollider ? "mesh " : string.Empty)}y {h.point.y - s.Low.y:0.00}]");
                    ctx.Log($"stairs candidate {i} ({s.Low}): the mass settled {settled:0.00} m from the foot, colliders under the start:{sb}");
                }
                MassDev.Destroy(m);
                if (ok) { ctx.Log($"stairs candidate {i} kept: the mass settles {settled:0.00} m from the foot"); done(s, true); yield break; }
                yield return null;
            }
            done(default, false);
        }

        private IEnumerator FindStairs(ScenarioContext ctx, Action<Stair, bool> done, bool wide)
        {
            float minRise = wide ? WideMinRise : StairMinRise;
            int maxTests = wide ? WideTests : 400;
            int maxCandidates = wide ? WideCandidates : StairCandidates;
            int badStarts = 0, noLanding = 0;
            // Bucket the floor samples by 2 m cells.
            var cells = new Dictionary<long, List<int>>();
            for (int i = 0; i < _floors.Count; i++)
            {
                long key = Key(_floors[i]);
                if (!cells.TryGetValue(key, out List<int> list)) cells[key] = list = new List<int>(4);
                list.Add(i);
            }

            var candidates = new List<Stair>();
            var watch = Stopwatch.StartNew();
            var path = new List<Vector3>(64);
            int tested = 0, failedPaths = 0;
            for (int i = 0; i < _floors.Count && candidates.Count < maxCandidates && tested < maxTests; i += wide ? 1 : 3)
            {
                Vector3 low = _floors[i];
                // Look at the cells around, at 2.5 to 12 m, for a floor 1.5 to 4.5 m higher.
                for (int dx = -6; dx <= 6; dx++)
                {
                    for (int dz = -6; dz <= 6; dz++)
                    {
                        if (!cells.TryGetValue(Key(low, dx, dz), out List<int> list)) continue;
                        foreach (int j in list)
                        {
                            Vector3 high = _floors[j];
                            float rise = high.y - low.y;
                            float run = Utils.DistanceXZ(high, low);
                            if (rise < minRise || rise > StairMaxRise || run < StairMinRun || run > StairMaxRun) continue;
                            if (candidates.Count >= maxCandidates || (candidates.Count > 0 && (candidates[candidates.Count - 1].Low - low).sqrMagnitude < 4f)) continue;
                            if (!ValidStart(low, high, out Vector3 start, out int startOffset)) { badStarts++; continue; }
                            tested++;
                            if (Pathfinding.instance == null || !Pathfinding.instance.GetPath(low, high, path, Pathfinding.AgentType.Humanoid)) { failedPaths++; continue; }
                            if (path.Count == 0 || Utils.DistanceXZ(path[path.Count - 1], high) > 1.5f || Mathf.Abs(path[path.Count - 1].y - high.y) > 1f) { failedPaths++; continue; }
                            float length = 0f;
                            for (int k = 1; k < path.Count; k++) length += Vector3.Distance(path[k - 1], path[k]);
                            if (length > 3f * run + 6f) continue;   // a long detour is no staircase
                            if (!TryLanding(low, high, out Vector3 landing)) { noLanding++; continue; }
                            candidates.Add(new Stair { Low = low, High = high, Path = length, Start = start, Offset = startOffset, Landing = landing });
                        }
                    }
                }
                if (watch.ElapsedMilliseconds > FrameBudgetMs)
                {
                    yield return null;
                    watch.Restart();
                }
            }
            ctx.Log($"stairs search{(wide ? " (wide: rise from " + WideMinRise + " m)" : string.Empty)}: {tested} pairs checked with the pathfinder, {failedPaths} without a usable path, " +
                    $"{badStarts} rejected because no valid start (floor within {StartTolerance} m of the foot, up to {MaxStartOffset} m outward), " +
                    $"{noLanding} without a landing {LandingMin}-{LandingMax} m past the top, {candidates.Count} candidates");
            if (candidates.Count == 0) { done(default, false); yield break; }

            // The rise closest to 2.5 m, then the shortest path; the whole ranking is kept for PickStart.
            candidates.Sort((x, y) =>
            {
                float sx = Mathf.Abs(x.High.y - x.Low.y - 2.5f) * 4f + x.Path * 0.1f, sy = Mathf.Abs(y.High.y - y.Low.y - 2.5f) * 4f + y.Path * 0.1f;
                if (Mathf.Abs(sx - sy) > 1e-4f) return sx.CompareTo(sy);
                return LessThan(x.Low, y.Low) ? -1 : LessThan(y.Low, x.Low) ? 1 : 0;
            });
            _ranked.Clear();
            _ranked.AddRange(candidates);
            Stair best = candidates[0];
            float bestScore = float.MaxValue;
            foreach (Stair s in candidates)
            {
                float score = Mathf.Abs(s.High.y - s.Low.y - 2.5f) * 4f + s.Path * 0.1f;
                // Deterministic: the best score, ties broken by position.
                if (score < bestScore - 1e-4f || (Mathf.Abs(score - bestScore) <= 1e-4f && LessThan(s.Low, best.Low))) { bestScore = score; best = s; }
            }
            ctx.Log($"stairs candidate kept: {best.Low} -> {best.High}, rise {best.High.y - best.Low.y:0.00} m, path {best.Path:0.0} m, start {best.Start}, moved {best.Offset} m outward ({candidates.Count} candidates)");
            done(best, true);
        }

        private static bool LessThan(Vector3 a, Vector3 b) => a.x != b.x ? a.x < b.x : a.y != b.y ? a.y < b.y : a.z < b.z;

        private static long Key(Vector3 p, int dx = 0, int dz = 0) => ((long)(Mathf.FloorToInt(p.x / 2f) + dx) << 32) ^ (uint)(Mathf.FloorToInt(p.z / 2f) + dz);
    }
}
#endif
