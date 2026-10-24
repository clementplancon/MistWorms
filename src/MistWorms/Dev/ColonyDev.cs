#if MW_DEV
using System;
using System.Collections.Generic;
using MistWorms.Colony;
using UnityEngine;

namespace MistWorms.Dev
{
    /// <summary>
    /// Debug-only colony tooling (spec §15.3): creating test colonies, spawning worms around the player, mw_kill, state dumps.
    /// Shared by the console commands, the bridge routes and the scenarios.
    /// </summary>
    internal static class ColonyDev
    {
        private const int SpawnTries = 10;
        private const float MaxSpawnSlopeDeg = 60f;

        private static readonly System.Random s_rng = new System.Random();

        // ---------------------------------------------------------------- colony lookup / creation

        /// <summary>
        /// The colony around the player; otherwise a mine colony for the closest dungeon when the player is inside one,
        /// otherwise a test colony at the player. <paramref name="how"/> says which.
        /// </summary>
        public static WormColony FindOrCreateColony(Player player, out string how)
        {
            Vector3 pos = player.transform.position;
            WormColony colony = ColonyRegistry.FindFor(pos);
            if (colony != null)
            {
                how = "existing";
                return colony;
            }

            if (Character.InInterior(pos))
            {
                DungeonGenerator nearest = DevWorld.NearestGenerator(pos);
                if (nearest != null)
                {
                    ColonyBox.ForDungeon(nearest, out Vector3 center, out Vector3 half);
                    if (WormMath.InsideBox(pos, center, half))
                    {
                        how = "mine";
                        return WormColony.Create(nearest.transform.position, WormColony.ColonyKind.Mine, center, half);
                    }
                }
            }

            ColonyBox.ForTest(pos, out Vector3 testCenter, out Vector3 testHalf);
            how = "test";
            return WormColony.Create(pos, WormColony.ColonyKind.Test, testCenter, testHalf);
        }

        // ---------------------------------------------------------------- spawn / kill

        /// <summary>
        /// Spawns up to <paramref name="n"/> worms on the floor within <paramref name="radius"/> of the local player, in view of
        /// the player's chest (never behind a wall or inside rock). Respects Colony.MaxAlivePerMine unless <paramref name="force"/>.
        /// Returns how many were created.
        /// </summary>
        public static int Spawn(int n, float radius, WormState state, bool force, out WormColony colony, out string note)
        {
            colony = null;
            note = null;
            Player player = Player.m_localPlayer;
            if (player == null) throw new InvalidOperationException("no local player (not in game)");
            if (!player.IsOwner()) throw new InvalidOperationException("the local player is not owned by this peer");

            colony = FindOrCreateColony(player, out string how);
            if (colony == null) throw new InvalidOperationException("could not create a colony (prefab missing?)");
            note = how;

            Vector3 origin = player.transform.position;
            Vector3 chest = player.GetCenterPoint();
            int mask = WormPhysics.SolidMask;
            int created = 0;

            for (int k = 0; k < n; k++)
            {
                for (int attempt = 0; attempt < SpawnTries; attempt++)
                {
                    float angle = (float)s_rng.NextDouble() * Mathf.PI * 2f;
                    float r = Mathf.Sqrt((float)s_rng.NextDouble()) * radius;
                    var start = new Vector3(origin.x + Mathf.Cos(angle) * r, origin.y + 1.5f, origin.z + Mathf.Sin(angle) * r);

                    if (!Physics.Raycast(start, Vector3.down, out RaycastHit hit, 4f, mask, QueryTriggerInteraction.Ignore)) continue;
                    if (Vector3.Angle(hit.normal, Vector3.up) > MaxSpawnSlopeDeg) continue;
                    if (!WormMath.InsideBox(hit.point, colony.BoxCenter, colony.BoxHalfSize)) continue;
                    if (Physics.Linecast(chest, hit.point + Vector3.up * 0.3f, mask, QueryTriggerInteraction.Ignore)) continue;

                    int slot = colony.AddWorm(hit.point, state, (float)s_rng.NextDouble() * Mathf.PI * 2f - Mathf.PI, force);
                    if (slot < 0) return created; // cap reached or store full
                    created++;
                    break;
                }
            }
            return created;
        }

        /// <summary>mw_kill: removes every worm without drops and destroys the test colonies. Returns (worms, colonies destroyed).</summary>
        public static int KillAll(out int coloniesDestroyed)
        {
            coloniesDestroyed = 0;
            int worms = 0;
            var snapshot = new List<WormColony>(ColonyRegistry.All);
            foreach (WormColony colony in snapshot)
            {
                if (colony == null) continue;
                worms += colony.KillAll();
                if (colony.Kind == WormColony.ColonyKind.Test && colony.IsOwner)
                {
                    colony.DestroyColony();
                    coloniesDestroyed++;
                }
            }
            return worms;
        }

        // ---------------------------------------------------------------- state

        public static Dictionary<string, object> ColonyJson(WormColony c, bool includeWorms)
        {
            WormStore s = c.Store;
            var byState = new Dictionary<string, object>();
            var worms = includeWorms ? new List<object>() : null;

            if (s != null)
            {
                var counts = new int[Enum.GetValues(typeof(WormState)).Length];
                for (int i = 0; i < s.HighWater; i++)
                {
                    if (!s.alive[i]) continue;
                    counts[s.state[i]]++;
                    if (includeWorms)
                    {
                        worms.Add(new Dictionary<string, object>
                        {
                            { "i", i },
                            { "pos", s.pos[i] },
                            { "state", ((WormState)s.state[i]).ToString() },
                            { "hp", s.hp[i] },
                        });
                    }
                }
                for (int k = 0; k < counts.Length; k++)
                    if (counts[k] > 0) byState[((WormState)k).ToString()] = counts[k];
            }

            var d = new Dictionary<string, object>
            {
                { "zdoid", c.ZdoId.ToString() },
                { "owner", c.IsOwner },
                { "kind", c.Kind.ToString() },
                { "configured", c.IsConfigured },
                { "disabled", c.Disabled ? c.DisabledReason : null },
                { "box", new Dictionary<string, object> { { "center", c.BoxCenter }, { "half", c.BoxHalfSize } } },
                { "alive", s != null ? s.AliveCount : 0 },
                { "capacity", s != null ? s.Capacity : 0 },
                { "byState", byState },
                { "simMs", new Dictionary<string, object> { { "last", c.LastSimMs }, { "avg", c.AvgSimMs }, { "max", c.MaxSimMs }, { "steps", c.LastSimSteps } } },
            };
            if (includeWorms) d["worms"] = worms;
            return d;
        }

        /// <summary>One line per colony for mw_state.</summary>
        public static List<string> Summary()
        {
            var lines = new List<string>();
            foreach (WormColony c in ColonyRegistry.All)
            {
                if (c == null) continue;
                WormStore s = c.Store;
                var sb = new System.Text.StringBuilder();
                sb.Append(c.ZdoId).Append(' ').Append(c.Kind).Append(c.IsOwner ? " (owner)" : " (remote)");
                if (c.Disabled) sb.Append(" DISABLED: ").Append(c.DisabledReason);
                sb.Append(" alive=").Append(s != null ? s.AliveCount : 0).Append('/').Append(s != null ? s.Capacity : 0);
                if (s != null)
                {
                    var counts = new int[Enum.GetValues(typeof(WormState)).Length];
                    for (int i = 0; i < s.HighWater; i++)
                        if (s.alive[i]) counts[s.state[i]]++;
                    for (int k = 0; k < counts.Length; k++)
                        if (counts[k] > 0) sb.Append(' ').Append((WormState)k).Append('=').Append(counts[k]);
                }
                sb.Append($" box={c.BoxCenter} +-{c.BoxHalfSize} sim={c.AvgSimMs:0.00}ms (max {c.MaxSimMs:0.00})");
                lines.Add(sb.ToString());
            }
            return lines;
        }
    }
}
#endif
