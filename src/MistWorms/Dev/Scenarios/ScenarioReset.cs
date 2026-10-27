#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using MistWorms.Colony;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// A scenario that needs the player where the previous one left him (or that places him itself from a position given by its parameters): the central reset does not move the
    /// player for it. Everything else starts at <see cref="DevWorld.FixedSearchOrigin"/>.
    /// </summary>
    internal interface IKeepsPlayerPosition
    {
    }

    /// <summary>
    /// The state every scenario starts from (decision 253): the same alone or in a series. Before each scenario the runner forgets the last good site, takes the player back to the
    /// fixed search origin, kills everything (worms, nests, loot pools, masses, test colonies, pieces placed by a scenario), lifts the forced environment, fixes the time of day,
    /// thaws the simulation, puts the flags back and seeds the random generators from the name of the scenario. The values of MWConfig are saved before and put back after, with a
    /// log line for every difference (a scenario that changes a setting and does not restore it is a leak that would bend the next one).
    /// </summary>
    internal static class ScenarioReset
    {
        /// <summary>Time of day every scenario runs at (0..1, 0.5 = noon).</summary>
        public const float FixedDayFraction = 0.5f;

        private static readonly List<GameObject> s_placed = new List<GameObject>();
        private static Dictionary<ConfigDefinition, object> s_config;

        /// <summary>Registers an object a scenario placed in the world (a door, a fire): the reset removes it if the scenario did not.</summary>
        public static void TrackPlaced(GameObject go)
        {
            if (go != null) s_placed.Add(go);
        }

        /// <summary>Stable seed derived from the name of the scenario (not string.GetHashCode: it is not promised stable).</summary>
        public static int SeedFor(string name)
        {
            unchecked
            {
                uint h = 2166136261u;
                if (name != null)
                    foreach (char c in name) h = (h ^ char.ToLowerInvariant(c)) * 16777619u;
                return (int)(h & 0x7FFFFFFF);
            }
        }

        /// <summary>Everything above, before the scenario runs.</summary>
        public static IEnumerator Begin(IDevScenario scenario, ScenarioContext ctx)
        {
            int seed = SeedFor(scenario.Name);

            // 1. memory of the session
            DevWorld.ResetLastGoodSite();

            // 2. a first kill where the player stands (a mine colony around him, masses of the previous scenario)
            Kill(ctx, "before the move");

            // 3. player: god mode, no ghost, back to the fixed origin on the surface
            Player player = Player.m_localPlayer;
            if (player != null)
            {
                player.SetGhostMode(false);
                player.SetGodMode(true);
                player.SetHealth(player.GetMaxHealth());
            }
            if (player != null && !(scenario is IKeepsPlayerPosition))
            {
                Vector3 origin = DevWorld.FixedSearchOrigin();
                Vector3 here = player.transform.position;
                bool interior = Character.InInterior(here);
                float flatDistance = new Vector2(here.x - origin.x, here.z - origin.z).magnitude;
                if (interior || flatDistance > 3f)
                {
                    float h = WorldGenerator.instance != null ? WorldGenerator.instance.GetHeight(origin.x, origin.z) : origin.y;
                    float water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 0f;
                    var outcome = new TeleportOutcome();
                    yield return DevWorld.TeleportPlayer(new Vector3(origin.x, Mathf.Max(h, water) + 2f, origin.z), player.transform.rotation, true, 60f, outcome);
                    ctx.Log($"reset: player back at the fixed origin {origin.x:0},{origin.z:0} (was {here.x:0},{here.y:0},{here.z:0}{(interior ? ", interior" : string.Empty)}): {(outcome.Ok ? "ok" : outcome.Error)}");
                    if (outcome.Ok) yield return new WaitForSecondsRealtime(0.5f);
                }
                else ctx.Log($"reset: the player already stands at the fixed origin {origin.x:0},{origin.z:0}");
            }
            else if (player != null) ctx.Log("reset: this scenario keeps the position of the player");

            // 4. the complete kill, where the player is now
            Kill(ctx, "after the move");

            // 5. environment and time
            try
            {
                if (EnvMan.instance != null)
                {
                    if (!Character.InInterior(Player.m_localPlayer != null ? Player.m_localPlayer.transform.position : Vector3.zero)) EnvMan.instance.SetForceEnvironment(string.Empty);
                    EnvMan.instance.m_debugTimeOfDay = true;
                    EnvMan.instance.m_debugTime = FixedDayFraction;
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("[Scenario] reset environment: " + e.Message); }

            // 6. thaw and flags back to their base
            WormColony.Frozen = false;
            WormColony.GatheringEnabled = true;
            if (ClutterSystem.instance != null) ClutterSystem.instance.enabled = true;
            DevSpawnBlock.Active = false;
            WormColony.DevSpawnInterval = 0f;
            WormColony.DevMeasureAlloc = false;

            // 7. seeds: the worm colonies (existing ones restart, new ones start from it), the placements of the dev spawns, Unity
            WormColony.DevSeed = seed;
            foreach (WormColony colony in new List<WormColony>(ColonyRegistry.All))
                if (colony != null) colony.ReseedRng(seed ^ (int)(colony.ZdoId.ID & 0xFFFF));
            ColonyDev.Seed(seed);
            UnityEngine.Random.InitState(seed);

            // 8. MWConfig as it is now
            s_config = SnapshotConfig();
            ctx.Log($"reset: seed {seed}, time of day {FixedDayFraction:0.##}, {s_config?.Count ?? 0} settings saved");
        }

        /// <summary>After the scenario: puts the settings back and logs what the scenario changed.</summary>
        public static void End(ScenarioContext ctx)
        {
            try
            {
                if (s_config == null) return;
                ConfigFile cfg = Plugin.Instance != null ? Plugin.Instance.Config : null;
                if (cfg == null) return;
                var report = new StringBuilder();
                int diffs = 0;
                foreach (KeyValuePair<ConfigDefinition, object> kv in s_config)
                {
                    if (!cfg.Keys.Contains(kv.Key)) continue;
                    ConfigEntryBase entry = cfg[kv.Key];
                    if (Equals(entry.BoxedValue, kv.Value)) continue;
                    diffs++;
                    report.Append($" {kv.Key.Section}.{kv.Key.Key}: {kv.Value} -> {entry.BoxedValue};");
                    entry.BoxedValue = kv.Value;
                }
                ctx.Log(diffs == 0 ? "reset: no setting changed by the scenario" : $"reset: the scenario changed {diffs} setting(s), put back:{report}");
            }
            catch (Exception e) { Plugin.Log.LogWarning("[Scenario] reset config restore: " + e.Message); }
            finally { s_config = null; }
        }

        private static Dictionary<ConfigDefinition, object> SnapshotConfig()
        {
            ConfigFile cfg = Plugin.Instance != null ? Plugin.Instance.Config : null;
            if (cfg == null) return null;
            var snap = new Dictionary<ConfigDefinition, object>();
            foreach (ConfigDefinition def in cfg.Keys)
                snap[def] = cfg[def].BoxedValue;
            return snap;
        }

        /// <summary>mw_kill, then what it does not reach: the pieces placed by a scenario, the creatures spawned by the dev spawners. Logs what was found.</summary>
        private static void Kill(ScenarioContext ctx, string when)
        {
            try
            {
                int worms = ColonyDev.KillAll(out int colonies, out int masses);
                int nests = 0;
                foreach (WormColony colony in new List<WormColony>(ColonyRegistry.All))
                    if (colony != null) nests += colony.DestroyAllNests();   // (KillAll already did it; a nest created since is caught here)
                int pieces = 0;
                foreach (GameObject go in s_placed)
                {
                    if (go == null) continue;
                    pieces++;
                    if (ZNetScene.instance != null) ZNetScene.instance.Destroy(go);
                    else UnityEngine.Object.Destroy(go);
                }
                s_placed.Clear();
                if (worms + colonies + masses + nests + pieces > 0)
                    ctx.Log($"reset ({when}): {worms} worms, {colonies} test colonies, {masses} masses, {nests} nests, {pieces} placed objects removed");
            }
            catch (Exception e) { Plugin.Log.LogWarning("[Scenario] reset kill: " + e.Message); }
        }
    }
}
#endif
