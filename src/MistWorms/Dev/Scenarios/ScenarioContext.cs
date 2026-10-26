#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>What a running scenario sees: parameters, assertions, log, screenshots and wait helpers.</summary>
    internal sealed class ScenarioContext
    {
        public const float DefaultTimeoutSeconds = 120f;

        private readonly ScenarioResult _result;
        private readonly float _startRealtime;

        internal ScenarioContext(ScenarioResult result, Dictionary<string, string> parameters)
        {
            _result = result;
            _startRealtime = Time.realtimeSinceStartup;
            Params = parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            TimeoutSeconds = ParamFloat("timeout", DefaultTimeoutSeconds);
        }

        public string Name => _result.Name;
        public Dictionary<string, string> Params { get; }

        /// <summary>Real seconds before the runner fails the scenario; overridable with the "timeout" param.</summary>
        public float TimeoutSeconds { get; set; }

        /// <summary>Real (unscaled) seconds since the scenario started.</summary>
        public float Elapsed => Time.realtimeSinceStartup - _startRealtime;

        public bool InGame => Game.instance != null;
        public Player Player => Player.m_localPlayer;

        /// <summary>True when the last <see cref="WaitUntil"/> gave up before its condition held.</summary>
        public bool LastWaitTimedOut { get; private set; }

        // ---------------------------------------------------------------- parameters

        public string Param(string key, string def = null) =>
            Params.TryGetValue(key, out string v) && v != null ? v : def;

        public float ParamFloat(string key, float def) =>
            float.TryParse(Param(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : def;

        public int ParamInt(string key, int def) =>
            int.TryParse(Param(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : def;

        public bool ParamBool(string key, bool def)
        {
            string s = Param(key);
            if (s == null) return def;
            if (bool.TryParse(s, out bool b)) return b;
            return s == "1" ? true : s == "0" ? false : def;
        }

        /// <summary>
        /// Gives the player back to a known state, so that the order of the scenarios never matters: the dev camera released, debug flight off, the
        /// look straight ahead (a pitch inherited from a mine would send a sword swing over a low hit sphere), full stamina. Called by the runner
        /// before every scenario and by the melee ones before each swing.
        /// </summary>
        public void ResetPlayerState()
        {
            Preview.DevCamera.Follow();
            Player p = Player.m_localPlayer;
            if (p == null) return;
            if (p.IsDebugFlying()) p.ToggleDebugFly();
            p.SetMouseLookForward(true);
            p.m_stamina = p.GetMaxStamina();
            p.GetSEMan().RemoveAllStatusEffects();   // a status left by the previous scenario (Infested, Engulfed...) must not follow
        }

        /// <summary>Why the player cannot equip an item right now (the conditions of Humanoid.EquipItem and the states around them), or null when it can.</summary>
        public static string EquipBlock(Player p)
        {
            if (p == null) return "no local player";
            if (p.IsDead()) return "IsDead";
            if (p.IsTeleporting()) return "IsTeleporting";
            if (p.InAttack()) return "InAttack";
            if (p.InDodge()) return "InDodge";
            if (p.IsSwimming() && !p.IsOnGround()) return "swimming off the ground";
            return null;
        }

        /// <summary>Waits up to <paramref name="seconds"/> for a player who can equip items (a scenario that ended mid-attack or mid-dodge leaves one that cannot); logs the reason if it never comes.</summary>
        public IEnumerator WaitPlayerSane(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            string reason = EquipBlock(Player.m_localPlayer);
            while (reason != null && Time.realtimeSinceStartup < end)
            {
                yield return null;
                reason = EquipBlock(Player.m_localPlayer);
            }
            if (reason != null) Log($"the player is not ready after {seconds:0} s: {reason}");
        }

        // ---------------------------------------------------------------- results

        /// <summary>Records an assertion; returns <paramref name="ok"/> so callers can bail out early.</summary>
        public bool Assert(string name, bool ok, object expected = null, object actual = null)
        {
            _result.Assertions.Add(new ScenarioAssertion { Name = name, Ok = ok, Expected = expected, Actual = actual });
            Log($"{(ok ? "PASS" : "FAIL")} {name}: expected={Format(expected)} actual={Format(actual)}");
            return ok;
        }

        public void Log(string message)
        {
            string line = $"[{Elapsed,7:0.00}s] {message}";
            _result.AddLog(line);
            Plugin.Log.LogInfo($"[Scenario {Name}] {message}");
        }

        /// <summary>Captures a screenshot named "&lt;scenario&gt;_&lt;name&gt;"; a failed capture is logged, not fatal.</summary>
        public IEnumerator Shot(string name)
        {
            string path = null, error = null;
            yield return DevShots.Capture($"{Name}_{name}", (p, e) => { path = p; error = e; });
            if (path != null)
            {
                _result.Shots.Add(path);
                Log($"shot {path}");
            }
            else
            {
                Log($"shot '{name}' failed: {error}");
            }
        }

        /// <summary>Records a short clip of JPG frames into docs/shots/clips/&lt;scenario&gt;_&lt;name&gt; (assemble with `vh.py assemble`); failures are logged, not fatal.</summary>
        public IEnumerator Clip(string name, float seconds = 4f, float fps = 15f, float scale = 0.5f)
        {
            Dictionary<string, object> info = null;
            string error = null;
            yield return DevShots.Clip($"{Name}_{name}", seconds, fps, scale, true, (i, e) => { info = i; error = e; });
            if (info != null) Log($"clip {info["dir"]} frames={info["frames"]} fps={info["fps"]}");
            else Log($"clip '{name}' failed: {error}");
        }

        /// <summary>Runs a console command and returns the lines it printed.</summary>
        public List<string> Console(string cmd)
        {
            List<string> lines = DevConsole.Run(cmd);
            Log($"> {cmd}{(lines.Count > 0 ? " -> " + string.Join(" | ", lines) : string.Empty)}");
            return lines;
        }

        // ---------------------------------------------------------------- world navigation

        /// <summary>Outcome of the last <see cref="TpMine"/>.</summary>
        public TpMineResult LastTpMine { get; private set; }

        /// <summary>Outcome of the last <see cref="MoveToOpenGround"/>.</summary>
        public OpenGroundResult LastOpenGround { get; private set; }

        /// <summary>
        /// Enters the closest infested mine (spec §15.3) and records the "tp_mine" assertion; the player ends in god and ghost mode. The mine is generated with its own creatures
        /// (spawners on while it loads); once inside, unless <paramref name="keepCreatures"/> (S12 feeds a Seeker to the masses), the creatures within 40 m are removed, the vanilla
        /// spawners are silent and <see cref="GuardCreatures"/> keeps the area clear for the rest of the scenario: a Seeker or a Dvergr kills worms and masses, and a worm that senses one
        /// (or a door of the town) gathers into a mass.
        /// </summary>
        public IEnumerator TpMine(bool keepCreatures = false)
        {
            var result = new TpMineResult();
            LastTpMine = result;
            MistWorms.Dev.DevSpawnBlock.Active = false;       // the mines keep their own creatures while they load
            GuardCreatures = false;
            yield return DevWorld.TpMine(result, Log);
            Assert("tp_mine", result.Ok, $"inside a mine within 90 s", result.Ok
                ? (object)$"{result.Location}, {result.RoomCount} rooms, {result.Seconds:0.0} s"
                : result.Error);
            if (result.Ok && !keepCreatures) ClearMineCreatures();
        }

        private void ClearMineCreatures()
        {
            Player player = Player.m_localPlayer;
            if (player == null) return;
            MistWorms.Dev.DevSpawnBlock.Active = true;        // until the end of the scenario (the runner switches it off)
            S18_GroupCoherence.ClearCreatures(this, player);
            int wiped = ColonyDev.ClearMine(player);
            if (wiped > 0) Log($"mine colony wiped: {wiped} worms and nests removed (an earlier seeding)");
            GuardCreatures = true;
        }

        /// <summary>
        /// <see cref="TpMine"/> tried up to <paramref name="attempts"/> times (a teleport that does not finish in time is not final: the player is waited for and the door tried again);
        /// the single "tp_mine" assertion is recorded at the end.
        /// </summary>
        public IEnumerator TpMineRetry(int attempts = 3, bool keepCreatures = false)
        {
            TpMineResult result = null;
            for (int k = 1; k <= attempts; k++)
            {
                result = new TpMineResult();
                yield return DevWorld.TpMine(result, Log);
                if (result.Ok) break;
                Log($"tp_mine attempt {k} of {attempts} failed: {result.Error}");
                yield return WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f);
                yield return Wait(1f);
            }
            LastTpMine = result;
            Assert("tp_mine", result.Ok, $"inside a mine within 90 s (up to {attempts} attempts)", result.Ok
                ? (object)$"{result.Location}, {result.RoomCount} rooms, {result.Seconds:0.0} s"
                : result.Error);
            if (result.Ok && !keepCreatures) ClearMineCreatures();
        }

        /// <summary>
        /// While true the vanilla creatures within 40 m of the player are removed every half second (the runner calls <see cref="GuardTick"/> each frame). For the scenarios that count worms:
        /// a poisonous blob that wanders in from beyond the one-off clearing killed 6 worms of S02b. Off by default: S12 and S19 spawn creatures of their own.
        /// </summary>
        public bool GuardCreatures;
        private float _nextGuard;

        public void GuardTick()
        {
            if (!GuardCreatures || Time.time < _nextGuard) return;
            _nextGuard = Time.time + 0.5f;
            Player p = Player.m_localPlayer;
            if (p != null) S18_GroupCoherence.ClearCreatures(this, p, quiet: true);
        }

        private static bool CalmBiome(Vector3 p)
        {
            WorldGenerator wg = WorldGenerator.instance;
            if (wg == null) return true;
            Heightmap.Biome b = wg.GetBiome(p);
            return b == Heightmap.Biome.Meadows;
        }

        /// <summary>
        /// Teleports the player onto flat open ground with <paramref name="clearRadius"/> metres of nothing around (see
        /// <see cref="DevWorld.MoveToOpenGround"/>) and records the "open_ground_found" assertion. <paramref name="accept"/> adds a
        /// scenario specific test on the candidate ground point.
        /// </summary>
        public const float DefaultFlatRadius = 6f, DefaultFlatTolerance = 0.5f, DefaultLenientTolerance = 1f;

        public IEnumerator MoveToOpenGround(float clearRadius = 5f, Func<Vector3, bool> accept = null, float flatRadius = DefaultFlatRadius, float flatTolerance = DefaultFlatTolerance, float lenientTolerance = DefaultLenientTolerance)
        {
            var result = new OpenGroundResult();
            LastOpenGround = result;
            // Hardening of the open ground scenarios: never a site in or beside water (the rings of water on the captures, the sea level under the masses), and once there the vanilla
            // creatures within 40 m are removed and the player is protected (a P3_Cobra was spoiled once by the death of the player in the Plains).
            // The site is level within flatTolerance over flatRadius: a condition of acceptance, not a remark (a door 1.3 m above the mass, a mass that climbs, a speed that is not the flat one).
            // Strict first (flatTolerance), else the flattest site within lenientTolerance of the spiral (logged with its measured deviation): the rolling Meadows rarely offer 0.5 m over 6 m next to a clear radius of 10 m or more.
            var flat = new FlatSpec(flatRadius, flatTolerance, lenientTolerance);
            Func<Vector3, bool> dry = p => !V14_GpuMassSandbox.NearWater(p, V14_GpuMassSandbox.WaterClearance) && (accept == null || accept(p));
            // Meadows first: the Plains have tar blobs and fulings that kill the worms of a test (S18 a: 24 of 30 dead), the Black Forest has trees that block a fleeing mass (S06) and greydwarves
            // that eat the pool (S13). Any biome only when the Meadows have no flat clear spot within reach (logged).
            Func<Vector3, bool> calm = p => dry(p) && CalmBiome(p);
            yield return DevWorld.MoveToOpenGround(clearRadius, calm, result, Log, flat);
            if (!result.Found)
            {
                Log("no calm biome (Meadows, Black Forest) found: any biome");
                result = new OpenGroundResult();
                LastOpenGround = result;
                yield return DevWorld.MoveToOpenGround(clearRadius, dry, result, Log, flat);
            }
            Assert("open_ground_found", result.Found, true,
                result.Found ? (object)$"{result.Spot} after {result.Tested} candidates, {result.Hops} hops" : result.Error);
            if (result.Found && Player.m_localPlayer != null)
            {
                Player.m_localPlayer.SetGodMode(true);
                MistWorms.Dev.DevSpawnBlock.Active = true;        // the vanilla spawners are silent until the end of the scenario
                S18_GroupCoherence.ClearCreatures(this, Player.m_localPlayer);
            }
        }

        // ---------------------------------------------------------------- player setup

        /// <summary>Unequips everything and empties the inventory.</summary>
        public void ClearInventory()
        {
            Player player = Player.m_localPlayer;
            if (player == null) return;
            player.UnequipAllItems();
            player.GetInventory().RemoveAll();
        }

        /// <summary>Adds a quality 1 item (equipped or not) and returns it; null with a log line when the prefab is unknown or the inventory full.</summary>
        public ItemDrop.ItemData Give(string prefab, bool equip)
        {
            Player player = Player.m_localPlayer;
            if (player == null) return null;
            ItemDrop.ItemData item = player.GetInventory().AddItem(prefab, 1, 1, 0, player.GetPlayerID(), player.GetPlayerName(), true);
            if (item == null)
            {
                Log($"could not add '{prefab}'");
                return null;
            }
            if (equip && !player.EquipItem(item))
                Log($"could not equip '{prefab}': {EquipBlock(player) ?? "no blocking state found (inventory, durability, DLC?)"}; staggering {player.IsStaggering()}, knocked back {player.IsKnockedBack()}, " +
                    $"place mode {player.InPlaceMode()}, minor action {player.InMinorAction()}, on ground {player.IsOnGround()}, can move {player.CanMove()}");
            return item;
        }

        // ---------------------------------------------------------------- waiting

        /// <summary>Waits real seconds (unaffected by time scale or pause).</summary>
        public IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        /// <summary>Waits scaled game seconds.</summary>
        public IEnumerator WaitGame(float seconds)
        {
            float waited = 0f;
            while (waited < seconds)
            {
                yield return null;
                waited += Time.deltaTime;
            }
        }

        public IEnumerator WaitFrames(int frames)
        {
            for (int i = 0; i < frames; i++) yield return null;
        }

        /// <summary>
        /// Polls <paramref name="condition"/> every frame for up to <paramref name="timeoutSeconds"/> real seconds.
        /// With <paramref name="assertName"/>, the outcome is also recorded as an assertion.
        /// </summary>
        public IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string assertName = null)
        {
            float start = Time.realtimeSinceStartup;
            LastWaitTimedOut = false;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - start >= timeoutSeconds)
                {
                    LastWaitTimedOut = true;
                    break;
                }
                yield return null;
            }
            if (assertName != null)
                Assert(assertName, !LastWaitTimedOut, $"within {timeoutSeconds:0.##} s", LastWaitTimedOut ? "timed out" : $"{Time.realtimeSinceStartup - start:0.##} s");
        }

        private static string Format(object v) => v == null ? "null" : v is string s ? s : MiniJson.Serialize(v);
    }
}
#endif
