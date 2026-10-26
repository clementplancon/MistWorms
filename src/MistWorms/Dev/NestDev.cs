#if MW_DEV
using System;
using System.Collections.Generic;
using MistWorms.Colony;
using UnityEngine;

namespace MistWorms.Dev
{
    /// <summary>Debug-only tooling of the nests (spec §15.3): placing a nest in front of the player, hitting nests, state dumps. Shared by mw_nest, /nest and the scenarios.</summary>
    internal static class NestDev
    {
        /// <summary>
        /// A nest on the floor <paramref name="distance"/> metres in front of the local player, in the colony around the player (created if there is
        /// none, see <see cref="ColonyDev.FindOrCreateColony"/>). Throws InvalidOperationException with the reason when it cannot.
        /// </summary>
        public static WormNest PlaceInFront(float distance, out WormColony colony, out string how)
        {
            Player player = Player.m_localPlayer;
            if (player == null) throw new InvalidOperationException("no local player (not in game)");
            colony = ColonyDev.FindOrCreateColony(player, out how);
            if (colony == null) throw new InvalidOperationException("could not create a colony (prefab missing?)");

            Vector3 forward = player.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            Vector3 at = player.transform.position + forward * distance;
            if (!Physics.Raycast(at + Vector3.up * 1.5f, Vector3.down, out RaycastHit floor, 4f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException($"no floor under {at}");
            WormNest nest = colony.SpawnNest(floor.point, player.transform.eulerAngles.y);
            if (nest == null) throw new InvalidOperationException("could not create the nest (not the owner of the colony, registry full or prefab missing)");
            return nest;
        }

        /// <summary>The nest of <paramref name="index"/> in the colony around the player (or any colony), or null.</summary>
        public static WormNest Find(int index)
        {
            Player player = Player.m_localPlayer;
            WormColony around = player != null ? ColonyRegistry.FindFor(player.transform.position) : null;
            WormNest nest = around?.NestAt(index);
            if (nest != null) return nest;
            foreach (WormColony c in ColonyRegistry.All)
            {
                nest = c != null ? c.NestAt(index) : null;
                if (nest != null) return nest;
            }
            return null;
        }

        /// <summary>A hit of <paramref name="damage"/> blunt (resisted normally by the nest) on its destructible, as a player's weapon would send it.</summary>
        public static void Hit(WormNest nest, float damage)
        {
            var hit = new HitData();
            hit.m_damage.m_blunt = damage;
            Player player = Player.m_localPlayer;
            if (player != null) hit.SetAttacker(player);
            hit.m_hitType = HitData.HitType.PlayerHit;
            hit.m_point = nest.transform.position + Vector3.up * 0.25f;
            hit.m_dir = nest.transform.forward;
            nest.Destructible.Damage(hit);
        }

        public static Dictionary<string, object> Json(WormNest n)
        {
            float since = n.SecondsSinceHit;
            return new Dictionary<string, object>
            {
                { "index", n.Index },
                { "zdoid", n.ZdoId.ToString() },
                { "pos", n.transform.position },
                { "hp", n.Health },
                { "maxHp", n.MaxHealth },
                { "secondsSinceHit", since > 1e6f ? null : (object)since },
                { "layTimer", n.LayTimer },
                { "laid", n.Laid },
                { "colony", n.Colony != null ? n.Colony.ZdoId.ToString() : null },
                { "alive", n.Colony != null && n.Colony.Store != null && n.Index >= 0 && n.Index < WormColony.MaxNests ? n.Colony.CountFromNest(n.Index) : 0 },
            };
        }

        public static Dictionary<string, object> SeedJson(SeedReport r)
        {
            if (r == null) return null;
            var nests = new List<object>();
            foreach (Vector3 p in r.NestPositions) nests.Add(p);
            return new Dictionary<string, object>
            {
                { "seed", r.Seed },
                { "rooms", r.Rooms },
                { "eligibleRooms", r.EligibleRooms },
                { "nests", r.NestsPlaced },
                { "broodWanted", r.BroodWanted },
                { "broodPlaced", r.BroodPlaced },
                { "loneWanted", r.LoneWanted },
                { "lonePlaced", r.LonePlaced },
                { "entrance", r.Entrance },
                { "nestPositions", nests },
                { "frames", r.Frames },
                { "planMs", r.PlanMs },
            };
        }

        /// <summary>The "nests" block of /state: every nest loaded here, and per colony the seeded flag, the laying counters and the last seeding.</summary>
        public static Dictionary<string, object> StateJson()
        {
            var items = new List<object>();
            foreach (WormNest n in WormNest.All)
                if (n != null && n.IsValid) items.Add(Json(n));

            var colonies = new List<object>();
            foreach (WormColony c in ColonyRegistry.All)
            {
                if (c == null) continue;
                int lone = 0;
                WormStore s = c.Store;
                if (s != null)
                    for (int i = 0; i < s.HighWater; i++)
                        if (s.alive[i] && s.lone[i]) lone++;
                colonies.Add(new Dictionary<string, object>
                {
                    { "zdoid", c.ZdoId.ToString() },
                    { "kind", c.Kind.ToString() },
                    { "seeded", c.Seeded },
                    { "nestsLoaded", c.NestsLoaded },
                    { "eggsLaid", c.EggsLaid },
                    { "nestsDestroyed", c.NestsDestroyed },
                    { "wormsReleased", c.WormsReleased },
                    { "wormsInMasses", c.WormsInMasses },
                    { "totalAlive", c.TotalAlive },
                    { "loneWorms", lone },
                    { "seed", SeedJson(c.LastSeed) },
                });
            }

            return new Dictionary<string, object>
            {
                { "count", items.Count },
                { "items", items },
                { "colonies", colonies },
                { "seeding", new Dictionary<string, object> { { "runs", MineSeeder.Runs }, { "planning", MineSeeder.Planning }, { "suppressed", MineSeeder.Suppressed } } },
                { "spawnIntervalOverride", WormColony.DevSpawnInterval },
            };
        }
    }
}
#endif
