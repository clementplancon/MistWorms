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
        /// scenario specific) agrees. When nothing is found in the loaded area, the player hops 150 m away (up to 3 times) to load more.
        /// </summary>
        public static IEnumerator MoveToOpenGround(float clearRadius, Func<Vector3, bool> accept, OpenGroundResult result, Action<string> log)
        {
            Player player = Player.m_localPlayer;
            if (player == null || ZoneSystem.instance == null || WorldGenerator.instance == null) { result.Error = "not in game"; yield break; }
            player.SetGodMode(true);

            Vector3 center = player.transform.position;
            Quaternion rot = player.transform.rotation;
            var directions = new[] { Vector3.right, Vector3.forward, Vector3.left, Vector3.back };

            for (int hop = 0; hop <= MaxOpenHops; hop++)
            {
                result.Hops = hop;
                if (Search(center, clearRadius, accept, out Vector3 spot, out int tested))
                {
                    result.Tested += tested;
                    result.Found = true;
                    result.Spot = spot;
                    break;
                }
                result.Tested += tested;
                if (hop == MaxOpenHops) break;

                // Nothing in the loaded area: load another one.
                center += directions[hop] * HopDistance;
                float h = WorldGenerator.instance.GetHeight(center.x, center.z);
                log?.Invoke($"no open ground yet ({result.Tested} candidates), hopping to {center.x:0},{center.z:0}");
                var outcome = new TeleportOutcome();
                yield return TeleportPlayer(new Vector3(center.x, Mathf.Max(h, ZoneSystem.instance.m_waterLevel) + 2f, center.z), rot, true, 60f, outcome);
                if (!outcome.Ok) { result.Error = "hop teleport failed: " + outcome.Error; yield break; }
                center = Player.m_localPlayer.transform.position;
            }
            if (!result.Found)
            {
                result.Error = $"no open ground among {result.Tested} candidates";
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
        }

        private static bool Search(Vector3 center, float clearRadius, Func<Vector3, bool> accept, out Vector3 spot, out int tested)
        {
            spot = default;
            tested = 0;
            int terrain = LayerMask.GetMask(VanillaNames.LayerTerrain);
            // Everything a tree, rock, piece or dropped item could be on; the terrain itself is allowed under the capsule.
            int clutter = LayerMask.GetMask(VanillaNames.LayerDefault, VanillaNames.LayerStaticSolid, VanillaNames.LayerDefaultSmall,
                VanillaNames.LayerPiece, "piece_nonsolid", VanillaNames.LayerVehicle, "item");
            float water = ZoneSystem.instance.m_waterLevel + 1f;

            for (float radius = 0f; radius <= OpenSearchRadius; radius += OpenSearchStep)
            {
                int points = radius <= 0f ? 1 : Mathf.Max(6, (int)(2f * Mathf.PI * radius / OpenSearchStep));
                float phase = radius * 0.37f; // rotate each ring so the points do not line up radially
                for (int k = 0; k < points; k++)
                {
                    float angle = phase + k * Mathf.PI * 2f / points;
                    float x = center.x + Mathf.Cos(angle) * radius;
                    float z = center.z + Mathf.Sin(angle) * radius;
                    tested++;

                    float height = WorldGenerator.instance.GetHeight(x, z);
                    if (height <= water) continue;
                    if (!Physics.Raycast(new Vector3(x, height + 50f, z), Vector3.down, out RaycastHit hit, 100f, terrain, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.normal.y < 0.95f) continue;

                    Vector3 g = hit.point;
                    if (Physics.CheckCapsule(g + Vector3.up * 0.3f, g + Vector3.up * 3f, clearRadius, clutter, QueryTriggerInteraction.Ignore)) continue;
                    if (accept != null && !accept(g)) continue;
                    spot = g;
                    return true;
                }
            }
            return false;
        }
    }
}
#endif
