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
        private const float SightSpawnRange = 11.5f;     // under WormSense.HuntRange (12 m)
        private const float MaxSpawnSlopeDeg = 60f;

        private static readonly System.Random s_rng = new System.Random();

        /// <summary>Dev switch (plan 4B-4): while false a worm that senses a player hunts alone, and nothing gathers, forms or absorbs. S18 tests the free worms with it off.</summary>
        public static bool GatheringEnabled
        {
            get => WormColony.GatheringEnabled;
            set => WormColony.GatheringEnabled = value;
        }

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
        public static int Spawn(int n, float radius, WormState state, bool force, out WormColony colony, out string note, int tries = SpawnTries, bool sight = false)
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
                for (int attempt = 0; attempt < tries; attempt++)
                {
                    float angle = (float)s_rng.NextDouble() * Mathf.PI * 2f;
                    float r = Mathf.Sqrt((float)s_rng.NextDouble()) * radius;
                    var start = new Vector3(origin.x + Mathf.Cos(angle) * r, origin.y + 1.5f, origin.z + Mathf.Sin(angle) * r);

                    if (!Physics.Raycast(start, Vector3.down, out RaycastHit hit, 4f, mask, QueryTriggerInteraction.Ignore)) continue;
                    if (Vector3.Angle(hit.normal, Vector3.up) > MaxSpawnSlopeDeg) continue;
                    if (!WormMath.InsideBox(hit.point, colony.BoxCenter, colony.BoxHalfSize)) continue;
                    if (Physics.Linecast(chest, hit.point + Vector3.up * 0.3f, mask, QueryTriggerInteraction.Ignore)) continue;
                    // sight: the worm itself must see the player (eye 0.15 m up, to the player's eye, view blockers) from within the range at which a lone worm picks a target.
                    if (sight && (Vector3.Distance(hit.point + Vector3.up * 0.15f, player.GetCenterPoint()) > SightSpawnRange
                                  || Physics.Linecast(hit.point + Vector3.up * 0.15f, player.GetEyePoint(), WormPhysics.ViewBlockMask, QueryTriggerInteraction.Ignore))) continue;

                    int slot = colony.AddWorm(hit.point, state, (float)s_rng.NextDouble() * Mathf.PI * 2f - Mathf.PI, force);
                    if (slot < 0) return created; // cap reached or store full
                    created++;
                    break;
                }
            }
            return created;
        }

        /// <summary>
        /// One worm on the floor under <paramref name="near"/>, facing the local player. Returns the slot or -1; <paramref name="colony"/> is the
        /// colony that holds it (see <see cref="FindOrCreateColony"/>).
        /// </summary>
        public static int SpawnAt(Vector3 near, WormState state, bool force, out WormColony colony)
        {
            colony = null;
            Player player = Player.m_localPlayer;
            if (player == null) throw new InvalidOperationException("no local player (not in game)");
            colony = FindOrCreateColony(player, out _);
            if (colony == null) throw new InvalidOperationException("could not create a colony (prefab missing?)");

            if (!Physics.Raycast(near + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore)) return -1;
            Vector3 toPlayer = player.transform.position - hit.point;
            float heading = WormMath.HeadingOf(toPlayer.x, toPlayer.z);
            return colony.AddWorm(hit.point, state, heading, force);
        }

        /// <summary>
        /// Gives and equips <paramref name="weapon"/>, turns the player toward <paramref name="lookAt"/> and starts a real vanilla primary attack.
        /// <paramref name="error"/> is set when the item cannot be added or equipped; the return value tells whether the attack started.
        /// </summary>
        public static bool StartAttack(Player player, string weapon, Vector3? lookAt, out ItemDrop.ItemData item, out string error)
        {
            error = null;
            Inventory inv = player.GetInventory();
            item = null;
            foreach (ItemDrop.ItemData candidate in inv.GetAllItems())
                if (candidate.m_dropPrefab != null && candidate.m_dropPrefab.name == weapon) { item = candidate; break; }
            if (item == null) item = inv.AddItem(weapon, 1, 1, 0, player.GetPlayerID(), player.GetPlayerName(), true);
            if (item == null)
            {
                error = $"could not add '{weapon}' (unknown prefab or inventory full)";
                return false;
            }
            if (!player.IsItemEquiped(item) && !player.EquipItem(item))
            {
                error = $"could not equip '{weapon}'";
                return false;
            }

            if (lookAt.HasValue)
            {
                Vector3 dir = lookAt.Value - player.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 1e-6f)
                {
                    player.transform.rotation = Quaternion.LookRotation(dir);
                    player.SetLookDir(dir.normalized);
                    player.SetMouseLookForward(true);   // yaw from the body, pitch 0: the swing is level with the hit sphere
                }
            }

            player.m_stamina = player.GetMaxStamina();
            return player.StartAttack(null, false);
        }

        /// <summary>mw_kill: removes every worm without drops, destroys the test colonies and every mass. Returns the worms removed.</summary>
        public static int KillAll(out int coloniesDestroyed, out int massesDestroyed)
        {
            coloniesDestroyed = 0;
            massesDestroyed = MassDev.KillAll();
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

        // ---------------------------------------------------------------- hits

        /// <summary>The loaded colony whose ZDOID prints as <paramref name="zdoid"/>, or null.</summary>
        public static WormColony FindColony(string zdoid)
        {
            foreach (WormColony c in ColonyRegistry.All)
                if (c != null && c.ZdoId.ToString() == zdoid) return c;
            return null;
        }

        /// <summary>The colony holding a living worm <paramref name="index"/>: the one named, else the one around the player, else the first that has it.</summary>
        public static WormColony ColonyOfWorm(string zdoid, int index)
        {
            if (!string.IsNullOrEmpty(zdoid)) return FindColony(zdoid);
            Player player = Player.m_localPlayer;
            WormColony around = player != null ? ColonyRegistry.FindFor(player.transform.position) : null;
            if (HasWorm(around, index)) return around;
            foreach (WormColony c in ColonyRegistry.All)
                if (HasWorm(c, index)) return c;
            return null;
        }

        /// <summary>
        /// Sends <paramref name="hit"/> to worm <paramref name="index"/>: through its hit proxy when it has one (the path a vanilla weapon
        /// takes), otherwise straight to the colony. Returns whether a proxy carried it.
        /// </summary>
        public static bool HitWorm(WormColony colony, int index, HitData hit)
        {
            ushort generation = colony.Store.generation[index];
            WormProxy proxy = ProxyManager.Existing != null ? ProxyManager.Existing.ProxyFor(colony, index) : null;
            if (proxy != null)
            {
                proxy.Damage(hit);
                return true;
            }
            colony.ReceiveWormHit(index, generation, hit);
            return false;
        }

        private static bool HasWorm(WormColony c, int index) => c?.Store != null && (uint)index < (uint)c.Store.Capacity && c.Store.alive[index];

        /// <summary>Count of the worms of <paramref name="colony"/> that currently have a hit proxy.</summary>
        public static int ProxiedWorms(WormColony colony)
        {
            ProxyManager manager = ProxyManager.Existing;
            if (manager == null || colony?.Store == null) return 0;
            int n = 0;
            for (int i = 0; i < colony.Store.HighWater; i++)
                if (colony.Store.alive[i] && manager.ProxyFor(colony, i) != null) n++;
            return n;
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
                { "proxies", ProxiedWorms(c) },
                { "masses", new Dictionary<string, object> { { "known", c.MassCount }, { "formed", c.MassesFormed }, { "wormsFormed", c.WormsFormed }, { "wormsAbsorbed", c.WormsAbsorbed }, { "dissolvedWorms", c.DissolvedWorms }, { "gathering", ColonyDev.GatheringEnabled } } },
                {
                    "combat", new Dictionary<string, object>
                    {
                        { "hitsReceived", c.HitsReceived }, { "lastHitDamage", c.LastHitDamage },
                        { "strikesLanded", c.StrikesLanded }, { "strikesMissed", c.StrikesMissed }, { "lastStrikeGap", c.LastStrikeGap },
                    }
                },
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
            lines.AddRange(MassDev.Summary());
            return lines;
        }
    }
}
#endif
