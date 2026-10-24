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

        /// <summary>Enters the closest infested mine (spec §15.3) and records the "tp_mine" assertion; the player ends in god and ghost mode.</summary>
        public IEnumerator TpMine()
        {
            var result = new TpMineResult();
            LastTpMine = result;
            yield return DevWorld.TpMine(result, Log);
            Assert("tp_mine", result.Ok, $"inside a mine within 90 s", result.Ok
                ? (object)$"{result.Location}, {result.RoomCount} rooms, {result.Seconds:0.0} s"
                : result.Error);
        }

        /// <summary>
        /// Teleports the player onto flat open ground with <paramref name="clearRadius"/> metres of nothing around (see
        /// <see cref="DevWorld.MoveToOpenGround"/>) and records the "open_ground_found" assertion. <paramref name="accept"/> adds a
        /// scenario specific test on the candidate ground point.
        /// </summary>
        public IEnumerator MoveToOpenGround(float clearRadius = 5f, Func<Vector3, bool> accept = null)
        {
            var result = new OpenGroundResult();
            LastOpenGround = result;
            yield return DevWorld.MoveToOpenGround(clearRadius, accept, result, Log);
            Assert("open_ground_found", result.Found, true,
                result.Found ? (object)$"{result.Spot} after {result.Tested} candidates, {result.Hops} hops" : result.Error);
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
