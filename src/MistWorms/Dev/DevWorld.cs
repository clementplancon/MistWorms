#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using MistWorms.Colony;
using UnityEngine;

namespace MistWorms.Dev
{
    internal sealed class TeleportOutcome
    {
        public bool Started;
        public bool Ok;
        public string Error;
    }

    /// <summary>Outcome of <see cref="DevWorld.TpMine"/>, also the body of the <c>/tp-mine</c> reply.</summary>
    internal sealed class TpMineResult : IJsonValue
    {
        public bool Ok;
        public string Error;
        public string Location;
        public Vector3 Entrance;
        public Vector3 InteriorPos;
        public int RoomCount;
        public float Seconds;

        public object ToJson() => new Dictionary<string, object>
        {
            { "ok", Ok },
            { "error", Error },
            { "location", Location },
            { "entrance", Entrance },
            { "interiorPos", InteriorPos },
            { "roomCount", RoomCount },
            { "seconds", Seconds },
            // Waiting for the colony is moot before phase 7: colonies are created on demand (mw_spawn) and not by seeding yet.
            { "colony", ColonyRegistry.FindFor(InteriorPos) != null },
        };
    }

    internal sealed class OpenGroundResult
    {
        public bool Found;
        public Vector3 Spot;
        public int Tested;
        public int Hops;
        public string Error;
        /// <summary>Largest height difference (m) found within the flat radius at the spot kept; -1 when no flatness was asked.</summary>
        public float Deviation = -1f;
        /// <summary>Why candidates were rejected, summed over the search (for the log of a failure).</summary>
        public readonly GroundStats Stats = new GroundStats();
    }

    /// <summary>Counters of a ground search: how many candidates each criterion rejected and the flattest candidate that passed the criteria before flatness.</summary>
    internal sealed class GroundStats
    {
        public int Water, Normal, Clutter, Flat, Accept, Passed;
        public float BestDeviation = float.MaxValue;
        public Vector3 BestDeviationSpot;
        public override string ToString() =>
            $"rejected: water {Water}, slope {Normal}, clutter {Clutter}, not flat {Flat}, scenario {Accept}; accepted {Passed}; flattest clear candidate: " +
            (BestDeviation < float.MaxValue ? $"{BestDeviation:0.00} m off level at ({BestDeviationSpot.x:0},{BestDeviationSpot.z:0})" : "none");
    }

    /// <summary>
    /// Flatness wanted of a site: the terrain within <see cref="Radius"/> is level with the centre within <see cref="Strict"/> (preferred) or, failing that,
    /// within <see cref="Lenient"/> (the flattest such site of the spiral is kept; the choice is logged).
    /// </summary>
    internal sealed class FlatSpec
    {
        public float Radius, Strict, Lenient;
        public FlatSpec(float radius, float strict, float lenient) { Radius = radius; Strict = strict; Lenient = Mathf.Max(strict, lenient); }
    }

    /// <summary>
    /// World navigation for tests: teleporting the player and waiting for the world, entering the closest infested mine
    /// (spec §15.3) and finding clear ground (phase 2 open point). Coroutines are driven by the bridge or a scenario.
    /// </summary>
    internal static class DevWorld
    {
        private const float MineBudgetSeconds = 90f;      // spec §15.3: /tp-mine takes at most 90 s
        private const float OpenSearchRadius = 400f;
        private const float OpenSearchStep = 8f;
        private const int MaxOpenHops = 3;
        private const float HopDistance = 150f;

        // ---------------------------------------------------------------- teleport

        /// <summary>
        /// Teleports the local player and waits until the teleport ends and the zone around the arrival is ready. Player.TeleportTo
        /// refuses a second teleport within 2 s, so the cooldown is bypassed like the /tp route does.
        /// </summary>
        public static IEnumerator TeleportPlayer(Vector3 pos, Quaternion rot, bool distant, float timeoutSeconds, TeleportOutcome outcome)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            Player player = Player.m_localPlayer;
            while (player != null && !outcome.Started && Time.realtimeSinceStartup < deadline)
            {
                player.m_teleportCooldown = 10f;
                outcome.Started = player.TeleportTo(pos, rot, distant);
                if (!outcome.Started) yield return null;
                player = Player.m_localPlayer;
            }
            if (!outcome.Started)
            {
                outcome.Error = player == null ? "no local player" : "teleport refused (busy)";
                yield break;
            }

            yield return null;
            while (Player.m_localPlayer != null && Player.m_localPlayer.IsTeleporting() && Time.realtimeSinceStartup < deadline)
                yield return null;
            while (Player.m_localPlayer != null && !DevBridge.IsReady(Player.m_localPlayer) && Time.realtimeSinceStartup < deadline)
                yield return null;

            outcome.Ok = Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer);
            if (!outcome.Ok) outcome.Error = "teleport did not finish in time";
        }

        // ---------------------------------------------------------------- tp-mine

        /// <summary>
        /// Enters the closest infested mine: finds the nearest <c>Mistlands_DvergrTownEntrance1/2</c>, teleports next to it, takes
        /// the vanilla entrance Teleport (whose target is an interior point, y &gt; 3000) and waits for the dungeon to be loaded.
        /// Leaves the player in god and ghost mode. Never throws: failures are reported in <paramref name="result"/>.
        /// </summary>
        public static IEnumerator TpMine(TpMineResult result, Action<string> log)
        {
            float start = Time.realtimeSinceStartup;
            float Left() => MineBudgetSeconds - (Time.realtimeSinceStartup - start);

            Player player = Player.m_localPlayer;
            if (player == null || ZoneSystem.instance == null) { result.Error = "not in game"; yield break; }

            // 1. closest entrance of either kind (the boss citadel is not a mine, VanillaNames.MineLocations excludes it)
            bool found = false;
            ZoneSystem.LocationInstance best = default;
            float bestDistance = float.MaxValue;
            Vector3 from = player.transform.position;
            foreach (string name in VanillaNames.MineLocations)
            {
                if (!ZoneSystem.instance.FindClosestLocation(name, from, out ZoneSystem.LocationInstance candidate)) continue;
                float d = Vector3.Distance(candidate.m_position, from);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = candidate;
                    found = true;
                    result.Location = name;
                }
            }
            if (!found)
            {
                result.Error = "no infested mine entrance (Mistlands_DvergrTownEntrance1/2) is known in this world";
                yield break;
            }
            log?.Invoke($"closest mine entrance {result.Location} at {best.m_position} ({bestDistance:0} m away)");

            player.SetGodMode(true);
            player.SetGhostMode(true);

            // 2. next to the entrance, outside
            var outside = new TeleportOutcome();
            yield return TeleportPlayer(best.m_position + Vector3.up * 2f, player.transform.rotation, true, Mathf.Min(60f, Left()), outside);
            if (!outside.Ok) { result.Error = "teleport to the entrance failed: " + outside.Error; yield break; }

            // 3. the vanilla Teleport at the door whose target lies inside
            Teleport door = null;
            while (door == null && Left() > 0f)
            {
                float closest = 40f;
                Vector3 here = Player.m_localPlayer.transform.position;
                foreach (Teleport t in UnityEngine.Object.FindObjectsByType<Teleport>(FindObjectsSortMode.None))
                {
                    if (t.m_targetPoint == null || !Character.InInterior(t.m_targetPoint.transform.position)) continue;
                    float d = Vector3.Distance(t.transform.position, here);
                    if (d < closest)
                    {
                        closest = d;
                        door = t;
                    }
                }
                if (door == null) yield return new WaitForSecondsRealtime(0.5f);
            }
            if (door == null) { result.Error = "no entrance Teleport with an interior target within 40 m of the entrance"; yield break; }

            result.Entrance = door.transform.position;
            Vector3 inside = door.m_targetPoint.GetTeleportPoint();
            result.InteriorPos = inside;
            log?.Invoke($"entrance door at {result.Entrance}, interior point {inside}");

            // 4. inside, then wait for rooms and a floor under the player
            var enter = new TeleportOutcome();
            yield return TeleportPlayer(inside, door.m_targetPoint.transform.rotation, false, Mathf.Min(60f, Left()), enter);
            if (!enter.Ok) { result.Error = "teleport inside failed: " + enter.Error; yield break; }

            int stable = 0;
            while (Left() > 0f)
            {
                player = Player.m_localPlayer;
                DungeonGenerator gen = NearestGenerator(player.transform.position);
                int rooms = gen != null ? gen.GetComponentsInChildren<Room>().Length : 0;
                bool floor = Physics.Raycast(player.transform.position + Vector3.up, Vector3.down, 4f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore);
                stable = rooms > 0 && floor ? stable + 1 : 0;
                result.RoomCount = rooms;
                if (stable >= 2) break;
                yield return new WaitForSecondsRealtime(0.5f);
            }

            result.Seconds = Time.realtimeSinceStartup - start;
            result.Ok = stable >= 2;
            if (!result.Ok) result.Error = $"dungeon not loaded in time ({result.RoomCount} rooms)";
            else log?.Invoke($"inside the mine: {result.RoomCount} rooms loaded after {result.Seconds:0.0} s");
        }

        /// <summary>The dungeon generator closest to the position in the xz plane (interiors sit 5000 m above their entrance).</summary>
        public static DungeonGenerator NearestGenerator(Vector3 pos)
        {
            DungeonGenerator nearest = null;
            float best = float.MaxValue;
            foreach (DungeonGenerator gen in UnityEngine.Object.FindObjectsByType<DungeonGenerator>(FindObjectsSortMode.None))
            {
                Vector3 d = gen.transform.position - pos;
                d.y = 0f;
                if (d.sqrMagnitude < best)
                {
                    best = d.sqrMagnitude;
                    nearest = gen;
                }
            }
            return nearest;
        }

        // ---------------------------------------------------------------- open ground

        /// <summary>
        /// Teleports the player (god mode) onto flat open ground: a spiral around the player up to 400 m in 8 m steps; a point fits when
        /// terrain is found under it, its normal is at least 0.95, it is more than 1 m above the water, no collider other than terrain
        /// lies in a capsule of radius <paramref name="clearRadius"/> from 0.3 to 3 m above it, and <paramref name="accept"/> (optional,
        /// scenario specific) agrees. When nothing is found in the loaded area, the player hops 150 m away (up to 3 times) to load more. Fallbacks: the last good site of the session is tried first (and always from an interior); after a failed search the player goes to the start temple (the Meadows) and searches there.
        /// </summary>
        public static IEnumerator MoveToOpenGround(float clearRadius, Func<Vector3, bool> accept, OpenGroundResult result, Action<string> log, FlatSpec flat = null, Func<Vector3, bool> scenarioAccept = null)
        {
            Player player = Player.m_localPlayer;
            if (player == null || ZoneSystem.instance == null || WorldGenerator.instance == null) { result.Error = "not in game"; yield break; }
            player.SetGodMode(true);
            Quaternion rot = player.transform.rotation;

            // 1. the last good site of this session, tried first (a mine, the Mistlands or any place without flat ground is no use to search from)
            bool inInterior = Character.InInterior(player.transform.position);
            if (_lastGoodSite.HasValue && (inInterior || (_lastGoodSite.Value - player.transform.position).sqrMagnitude > 50f * 50f))
            {
                Vector3 site = _lastGoodSite.Value;
                log?.Invoke($"trying the last good open ground {site.x:0},{site.z:0} first");
                var outcome = new TeleportOutcome();
                yield return TeleportPlayer(site + Vector3.up * 2f, rot, true, 60f, outcome);
                // Only the remembered point itself, judged by the criteria of THIS search (clear radius, flatness, calm biome...): a site found for another scenario may not fit.
                if (outcome.Ok) yield return SearchWithHops(site, 0, clearRadius, accept, flat, result, log, onlyCenter: true);
                else log?.Invoke("the teleport to the last good site failed: " + outcome.Error);
                if (!result.Found && outcome.Ok) log?.Invoke("the last good site does not fit this scenario: searching elsewhere");
                inInterior = false;
            }

            // 2. around the player (not in an interior: there is no terrain there)
            if (!result.Found && !inInterior)
                yield return SearchWithHops(player.transform.position, MaxOpenHops, clearRadius, accept, flat, result, log);

            // 3. the start temple (Meadows, flat and clear)
            if (!result.Found && ZoneSystem.instance.FindClosestLocation("StartTemple", Player.m_localPlayer.transform.position, out ZoneSystem.LocationInstance temple))
            {
                log?.Invoke($"no open ground found: falling back to the start temple at {temple.m_position.x:0},{temple.m_position.z:0}");
                float h = WorldGenerator.instance.GetHeight(temple.m_position.x, temple.m_position.z);
                var outcome = new TeleportOutcome();
                yield return TeleportPlayer(new Vector3(temple.m_position.x, Mathf.Max(h, ZoneSystem.instance.m_waterLevel) + 2f, temple.m_position.z), rot, true, 60f, outcome);
                if (outcome.Ok) yield return SearchWithHops(Player.m_localPlayer.transform.position, 1, clearRadius, accept, flat, result, log);
                else
                {
                    result.Error = "teleport to the start temple failed: " + outcome.Error;
                    yield break;
                }
            }
            if (!result.Found)
            {
                result.Error = $"no open ground among {result.Tested} candidates ({result.Stats})";
                log?.Invoke("best attempt: " + result.Stats);
                yield break;
            }

            player = Player.m_localPlayer;
            if ((result.Spot - player.transform.position).sqrMagnitude > 0.25f)
            {
                var arrive = new TeleportOutcome();
                bool distant = Vector3.Distance(result.Spot, player.transform.position) > 64f;
                yield return TeleportPlayer(result.Spot + Vector3.up * 0.1f, rot, distant, 60f, arrive);
                if (!arrive.Ok)
                {
                    result.Found = false;
                    result.Error = "teleport to the spot failed: " + arrive.Error;
                    yield break;
                }
                // Let physics and the player settle on the floor before anything is measured.
                yield return new WaitForSecondsRealtime(1f);
            }
            _lastGoodSite = result.Spot;
            // Decision 278 (amended): only the scenario's own criterion is judged again; without one, no Reverify.
            if (scenarioAccept != null) yield return Reverify(clearRadius, accept, scenarioAccept, result, log, flat);
        }

        private const float ReverifyRadius = 60f;
        private const int ReverifyRounds = 12;

        /// <summary>The clutter mask of the open ground search: everything a tree, rock, piece or dropped item could be on (the terrain itself is allowed under the capsule).</summary>
        private static int ClutterMask() => LayerMask.GetMask(VanillaNames.LayerDefault, VanillaNames.LayerStaticSolid, VanillaNames.LayerDefaultSmall,
            VanillaNames.LayerPiece, "piece_nonsolid", VanillaNames.LayerVehicle, "item");

        /// <summary>
        /// Decision 278. The spiral measures ground up to 400 m away, but the trees, logs and rocks exist only around the player (the active area): a site far from where the search started
        /// is judged without its objects (p9j: a PineTree_log 1 m from the site, invisible to the capsule and to the acceptance). Once the player stands on the site and the area is loaded,
        /// the scenario's own criterion (not the general clearance capsule: a tree always stands within 14 m in a forest) is judged again; a site that fails is replaced by a search around the player (where everything is loaded), at most
        /// <see cref="ReverifyRounds"/> times (the loaded areas where nothing fits are not searched again). Public: also run after an imposed site (param site=).
        /// </summary>
        public static IEnumerator Reverify(float clearRadius, Func<Vector3, bool> accept, Func<Vector3, bool> scenarioAccept, OpenGroundResult result, Action<string> log, FlatSpec flat)
        {
            var explored = new System.Collections.Generic.List<Vector3>();   // centres of the loaded areas where the search found nothing: their candidates are not tried again
            Func<Vector3, bool> fresh = p =>
            {
                foreach (Vector3 c in explored) if (Utils.DistanceXZ(p, c) < ReverifyRadius) return false;
                return accept == null || accept(p);
            };
            for (int round = 0; round <= ReverifyRounds && result.Found; round++)
            {
                Vector3 g = result.Spot;
                if (scenarioAccept == null || scenarioAccept(g)) yield break;
                if (round == ReverifyRounds) { result.Found = false; result.Error = $"site {g.ToString("0.00")} still refused after {ReverifyRounds} rounds"; yield break; }
                log?.Invoke($"site {g.ToString("0.00")} refused once its surroundings are loaded (scenario criterion), round {round + 1}: searching around the player");
                Vector3 centre = Player.m_localPlayer.transform.position;
                result.Found = false;
                // First the loaded area around the player, where every object exists; then, that area being exhausted, the far candidates not yet explored (they are judged again on arrival).
                yield return SearchWithHops(centre, 0, clearRadius, fresh, flat, result, log, maxRadius: ReverifyRadius);
                if (!result.Found)
                {
                    explored.Add(centre);
                    yield return SearchWithHops(centre, 0, clearRadius, fresh, flat, result, log);
                }
                if (!result.Found) { result.Error = $"no open ground once the surroundings are loaded ({result.Tested} candidates)"; yield break; }
                Vector3 spot = result.Spot;
                var arrive = new TeleportOutcome();
                yield return TeleportPlayer(spot + Vector3.up * 0.1f, Player.m_localPlayer.transform.rotation, Vector3.Distance(spot, Player.m_localPlayer.transform.position) > 64f, 60f, arrive);
                if (!arrive.Ok) { result.Found = false; result.Error = "teleport to the spot failed: " + arrive.Error; yield break; }
                yield return new WaitForSecondsRealtime(1f);
                _lastGoodSite = spot;
            }
        }

        /// <summary>The last open ground found in this game session (statics survive the scenarios); tried first by the next search.</summary>
        private static Vector3? _lastGoodSite;

        /// <summary>
        /// Forgets the last good open ground: the next search starts from where the player is, whatever an earlier scenario of the session found. A scenario that must be reproducible
        /// (V14) calls it, then goes to a fixed point first (<see cref="FixedSearchOrigin"/>), so that the site is the same on every run in the same world.
        /// </summary>
        public static void ResetLastGoodSite() => _lastGoodSite = null;

        /// <summary>
        /// The point every reproducible open ground search starts from: the start temple of the world (flat, in the Meadows, the same for a given world seed), else the world origin.
        /// </summary>
        public static Vector3 FixedSearchOrigin()
        {
            Vector3 origin = Vector3.zero;
            if (ZoneSystem.instance != null && ZoneSystem.instance.FindClosestLocation("StartTemple", Vector3.zero, out ZoneSystem.LocationInstance temple)) origin = temple.m_position;
            return origin;
        }

        /// <summary>The spiral search around <paramref name="center"/>, then up to <paramref name="maxHops"/> hops of 150 m to load more ground; fills <paramref name="result"/> when found.</summary>
        private static IEnumerator SearchWithHops(Vector3 center, int maxHops, float clearRadius, Func<Vector3, bool> accept, FlatSpec flat, OpenGroundResult result, Action<string> log, bool onlyCenter = false, float maxRadius = OpenSearchRadius)
        {
            Quaternion rot = Player.m_localPlayer.transform.rotation;
            var directions = new[] { Vector3.right, Vector3.forward, Vector3.left, Vector3.back };
            for (int hop = 0; hop <= maxHops; hop++)
            {
                result.Hops += hop > 0 ? 1 : 0;
                bool found = Search(center, clearRadius, accept, flat, result.Stats, out Vector3 spot, out float deviation, out int tested, onlyCenter ? 0f : maxRadius);
                result.Tested += tested;
                if (found)
                {
                    result.Found = true;
                    result.Spot = spot;
                    result.Deviation = deviation;
                    if (flat != null) log?.Invoke($"site level within {deviation:0.00} m over {flat.Radius:0} m (wanted {flat.Strict:0.0}, accepted up to {flat.Lenient:0.0})");
                    yield break;
                }
                if (hop == maxHops) yield break;

                // Nothing in the loaded area: load another one.
                center += directions[hop % directions.Length] * HopDistance;
                float h = WorldGenerator.instance.GetHeight(center.x, center.z);
                log?.Invoke($"no open ground yet ({result.Tested} candidates), hopping to {center.x:0},{center.z:0}");
                var outcome = new TeleportOutcome();
                yield return TeleportPlayer(new Vector3(center.x, Mathf.Max(h, ZoneSystem.instance.m_waterLevel) + 2f, center.z), rot, true, 60f, outcome);
                if (!outcome.Ok) { result.Error = "hop teleport failed: " + outcome.Error; yield break; }
                center = Player.m_localPlayer.transform.position;
            }
        }

        /// <summary>
        /// True when the terrain within <paramref name="radius"/> of <paramref name="g"/> (the centre and 12 points on each of two rings, half the radius and the radius) is level with
        /// it within <paramref name="tolerance"/>. Terrain only: a tree or a rock on a ring is not a slope.
        /// </summary>
        public static bool IsFlat(Vector3 g, float radius, float tolerance) => FlatDeviation(g, radius) <= tolerance;

        /// <summary>Largest height difference between <paramref name="g"/> and the terrain on the two rings (see <see cref="IsFlat"/>); <see cref="float.MaxValue"/> when a ring point has no terrain within reach.</summary>
        public static float FlatDeviation(Vector3 g, float radius)
        {
            int mask = LayerMask.GetMask(VanillaNames.LayerTerrain);
            float worst = 0f;
            for (int ring = 1; ring <= 2; ring++)
                for (int k = 0; k < 12; k++)
                {
                    float a = k * Mathf.PI * 2f / 12f;
                    Vector3 p = g + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (radius * ring / 2f);
                    if (!Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 12f, mask, QueryTriggerInteraction.Ignore)) return float.MaxValue;
                    worst = Mathf.Max(worst, Mathf.Abs(hit.point.y - g.y));
                }
            return worst;
        }

        /// <summary>After a site only level within the lenient tolerance, how far further (m) the spiral looks for a strictly level one.</summary>
        private const float FallbackLookahead = 80f;

        /// <summary>
        /// One spiral. A candidate level within the strict tolerance ends it at once; one within the lenient tolerance is remembered (the flattest) and kept when no strict one turns up
        /// in the next <see cref="FallbackLookahead"/> metres.
        /// </summary>
        private static bool Search(Vector3 center, float clearRadius, Func<Vector3, bool> accept, FlatSpec flat, GroundStats stats, out Vector3 spot, out float deviation, out int tested, float maxRadius)
        {
            spot = default;
            deviation = -1f;
            tested = 0;
            bool haveFallback = false;
            float fallbackRadius = 0f;
            int terrain = LayerMask.GetMask(VanillaNames.LayerTerrain);
            // Everything a tree, rock, piece or dropped item could be on; the terrain itself is allowed under the capsule.
            int clutter = ClutterMask();
            float water = ZoneSystem.instance.m_waterLevel + 1f;

            for (float radius = 0f; radius <= maxRadius; radius += OpenSearchStep)
            {
                if (haveFallback && radius > fallbackRadius + FallbackLookahead) break;
                int points = radius <= 0f ? 1 : Mathf.Max(6, (int)(2f * Mathf.PI * radius / OpenSearchStep));
                float phase = radius * 0.37f; // rotate each ring so the points do not line up radially
                for (int k = 0; k < points; k++)
                {
                    float angle = phase + k * Mathf.PI * 2f / points;
                    float x = center.x + Mathf.Cos(angle) * radius;
                    float z = center.z + Mathf.Sin(angle) * radius;
                    tested++;

                    float height = WorldGenerator.instance.GetHeight(x, z);
                    if (height <= water) { stats.Water++; continue; }
                    if (!Physics.Raycast(new Vector3(x, height + 50f, z), Vector3.down, out RaycastHit hit, 100f, terrain, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.normal.y < 0.95f) { stats.Normal++; continue; }

                    Vector3 g = hit.point;
                    if (Physics.CheckCapsule(g + Vector3.up * 0.3f, g + Vector3.up * 3f, clearRadius, clutter, QueryTriggerInteraction.Ignore)) { stats.Clutter++; continue; }

                    float dev = 0f;
                    if (flat != null)
                    {
                        dev = FlatDeviation(g, flat.Radius);
                        if (dev < stats.BestDeviation) { stats.BestDeviation = dev; stats.BestDeviationSpot = g; }
                        if (dev > flat.Lenient) { stats.Flat++; continue; }
                    }
                    if (accept != null && !accept(g)) { stats.Accept++; continue; }
                    stats.Passed++;

                    if (flat == null || dev <= flat.Strict)
                    {
                        spot = g;
                        deviation = flat == null ? -1f : dev;
                        return true;
                    }
                    if (!haveFallback || dev < deviation)
                    {
                        if (!haveFallback) fallbackRadius = radius;
                        haveFallback = true;
                        spot = g;
                        deviation = dev;
                    }
                }
            }
            return haveFallback;
        }
    }
}
#endif
