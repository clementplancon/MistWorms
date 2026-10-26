#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// Discovers <see cref="IDevScenario"/> types and runs them one at a time as coroutines on the bridge host.
    /// A run fails on any failed assertion, on timeout, or when the mod logs an error while it runs.
    /// </summary>
    internal static class ScenarioRunner
    {
        private const int MaxKeptResults = 100;

        private static Dictionary<string, Type> s_registry;
        private static readonly Dictionary<string, ScenarioResult> s_results = new Dictionary<string, ScenarioResult>(StringComparer.OrdinalIgnoreCase);
        private static readonly Queue<string> s_resultOrder = new Queue<string>();
        private static ScenarioResult s_current;
        private static int s_counter;
        private static ErrorLogListener s_errors;

        public static IEnumerable<string> Names
        {
            get
            {
                var names = new List<string>(Registry.Keys);
                names.Sort(StringComparer.OrdinalIgnoreCase);
                return names;
            }
        }

        /// <summary>Id of the run in progress, or null.</summary>
        public static string CurrentId => s_current?.Id;

        /// <summary>Starts a scenario and returns its run id. Throws KeyNotFoundException / InvalidOperationException.</summary>
        public static string Start(string name, Dictionary<string, string> parameters)
        {
            if (s_current != null)
                throw new InvalidOperationException($"scenario '{s_current.Name}' ({s_current.Id}) is still running");

            Type type = Resolve(name) ?? throw new KeyNotFoundException($"unknown scenario '{name}' (known: {string.Join(", ", Names)})");
            var scenario = (IDevScenario)Activator.CreateInstance(type, nonPublic: true);

            var result = new ScenarioResult
            {
                Id = $"{++s_counter:000}-{scenario.Name}",
                Name = scenario.Name,
                Parameters = parameters != null
                    ? new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            };
            Remember(result);
            s_current = result;

            var ctx = new ScenarioContext(result, result.Parameters);
            Coroutine run = DevBridge.Host.StartCoroutine(Execute(scenario, ctx, result));
            DevBridge.Host.StartCoroutine(Watchdog(ctx, result, run, Time.realtimeSinceStartup));
            return result.Id;
        }

        /// <summary>Seconds after the timeout of a scenario at which the watchdog gives up on a run that did not stop by itself.</summary>
        private const float WatchdogGrace = 20f;

        /// <summary>
        /// A scenario can never stay running: the runner aborts it between two steps once its timeout is over, but a step that never returns (a Unity wait under a frozen time scale, a
        /// stopped coroutine) would hold the runner forever and every later start would be refused. Past the timeout plus a grace the run is cut, marked as an error and cleaned up.
        /// </summary>
        private static IEnumerator Watchdog(ScenarioContext ctx, ScenarioResult result, Coroutine run, float began)
        {
            while (s_current == result)
            {
                if (Time.realtimeSinceStartup - began > ctx.TimeoutSeconds + WatchdogGrace)
                {
                    try { DevBridge.Host.StopCoroutine(run); } catch (Exception e) { Plugin.Log.LogWarning("[Scenario] watchdog stop: " + e.Message); }
                    try { DevConsole.Execute("mw_kill"); } catch (Exception e) { Plugin.Log.LogWarning("[Scenario] watchdog cleanup: " + e.Message); }
                    try
                    {
                        Player p = Player.m_localPlayer;
                        if (p != null) { p.SetGhostMode(false); p.SetGodMode(true); }
                    }
                    catch (Exception e) { Plugin.Log.LogWarning("[Scenario] watchdog player: " + e.Message); }
                    MistWorms.Mass.GpuMassBridge.ForceCpu = false;
                    MistWorms.Dev.DevSpawnBlock.Active = false;
                    MistWorms.Colony.MineSeeder.Suppressed = false;
                    MistWorms.Colony.WormColony.DevPassiveWake = true;
                    MistWorms.Colony.WormColony.DevSpawnInterval = 0f;
                    MistWorms.Colony.WormColony.DevMeasureAlloc = false;
                    try { MistWorms.Dev.ServerOwnDev.ReleaseIfActive(); } catch (Exception) { }
                    try { s_errors?.End(); } catch (Exception) { }
                    result.DurationSeconds = Time.realtimeSinceStartup - began;
                    result.Status = ScenarioResult.Errored;
                    result.Error = $"watchdog: the scenario did not stop {WatchdogGrace:0} s after its timeout of {ctx.TimeoutSeconds:0} s; cut and cleaned up";
                    Plugin.Log.LogWarning($"[Scenario] {result.Id} {result.Error}");
                    s_current = null;
                    yield break;
                }
                yield return null;
            }
        }

        public static ScenarioResult Get(string id) =>
            id != null && s_results.TryGetValue(id, out ScenarioResult r) ? r : null;

        private static Dictionary<string, Type> Registry => s_registry ?? (s_registry = Discover());

        private static Dictionary<string, Type> Discover()
        {
            var found = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            Type[] types;
            try { types = typeof(IDevScenario).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types; }

            foreach (Type t in types)
            {
                if (t == null || t.IsAbstract || t.IsInterface || !typeof(IDevScenario).IsAssignableFrom(t)) continue;
                if (t.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null) == null)
                {
                    Plugin.Log.LogWarning($"[Scenario] {t.FullName} has no parameterless constructor; skipped");
                    continue;
                }
                try
                {
                    string name = ((IDevScenario)Activator.CreateInstance(t, nonPublic: true)).Name;
                    if (found.ContainsKey(name)) Plugin.Log.LogWarning($"[Scenario] duplicate name '{name}' ({t.FullName}); skipped");
                    else found[name] = t;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"[Scenario] could not instantiate {t.FullName}: {e.Message}");
                }
            }
            return found;
        }

        /// <summary>Exact (case-insensitive) name, or the id prefix before the first underscore ("S01").</summary>
        private static Type Resolve(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Registry.TryGetValue(name, out Type t)) return t;
            foreach (KeyValuePair<string, Type> kv in Registry)
            {
                int underscore = kv.Key.IndexOf('_');
                if (underscore > 0 && underscore == name.Length
                    && string.Compare(kv.Key, 0, name, 0, underscore, StringComparison.OrdinalIgnoreCase) == 0)
                    return kv.Value;
            }
            return null;
        }

        private static void Remember(ScenarioResult result)
        {
            s_results[result.Id] = result;
            s_resultOrder.Enqueue(result.Id);
            while (s_resultOrder.Count > MaxKeptResults) s_results.Remove(s_resultOrder.Dequeue());
        }

        private static IEnumerator Execute(IDevScenario scenario, ScenarioContext ctx, ScenarioResult result)
        {
            if (s_errors == null)
            {
                // Registered once and gated by a flag: BepInEx iterates Listeners from any thread.
                s_errors = new ErrorLogListener();
                BepInEx.Logging.Logger.Listeners.Add(s_errors);
            }

            float start = Time.realtimeSinceStartup;
            Exception error = null;
            bool timedOut = false;
            Plugin.Log.LogInfo($"[Scenario] start {result.Id}");
            s_errors.Begin();
            try { ctx.ResetPlayerState(); }
            catch (Exception e) { Plugin.Log.LogWarning("[Scenario] ResetPlayerState: " + e.Message); }
            IEnumerator ready = null;
            try { ready = ctx.WaitPlayerSane(5f); }
            catch (Exception e) { Plugin.Log.LogWarning("[Scenario] WaitPlayerSane: " + e.Message); }
            if (ready != null) yield return ready;

            // The scenarios of the CPU layout of the masses (previews, lobes, flipbook instances) look at the fallback path: the GPU twin is off for them.
            MistWorms.Dev.DevSpawnBlock.Active = false;
            MistWorms.Mass.GpuMassBridge.ForceCpu = scenario is VisualScenario || scenario is V09_MassTransitions || scenario is V10_MassMoving || scenario is V11_MassEngulf || scenario is S08_MassStream;
            // The mines the scenarios enter stay as they expect them (no nest, no sleeping worm): only S16 wants the lazy seeding.
            MistWorms.Colony.MineSeeder.Suppressed = !(scenario is S16_MineSeed || scenario is S14_CalmSleep || scenario is S15_Persist);
            // Sleeping worms wake by themselves (a player near them or near a nest) only in the scenarios about the waking; the others place sleepers and expect them to stay so.
            MistWorms.Colony.WormColony.DevPassiveWake = scenario is S14_CalmSleep || scenario is S14b_WakeRules;
            IEnumerator body = null;
            try { body = scenario.Run(ctx); }
            catch (Exception e) { error = e; }

            if (body != null)
            {
                yield return SafeCoroutine.Run(body, e => error = e, () =>
                {
                    ctx.GuardTick();
                    if (Time.realtimeSinceStartup - start <= ctx.TimeoutSeconds) return false;
                    timedOut = true;
                    return true;
                });
            }

            MistWorms.Mass.GpuMassBridge.ForceCpu = false;
            MistWorms.Dev.DevSpawnBlock.Active = false;
            MistWorms.Colony.MineSeeder.Suppressed = false;
            MistWorms.Colony.WormColony.DevPassiveWake = true;
            MistWorms.Colony.WormColony.DevSpawnInterval = 0f;
            MistWorms.Colony.WormColony.DevMeasureAlloc = false;
            List<string> logErrors = s_errors.End();
            result.DurationSeconds = Time.realtimeSinceStartup - start;

            if (timedOut)
                ctx.Assert("timeout", false, $"<= {ctx.TimeoutSeconds:0} s", $"{result.DurationSeconds:0.0} s");
            // Always recorded (passing too): a hit that throws inside the animation event is cancelled silently otherwise.
            ctx.Assert("no_mod_errors_in_log", logErrors.Count == 0, 0, logErrors.Count);
            foreach (string line in logErrors) result.AddLog("log error: " + line);

            try { MistWorms.Dev.ServerOwnDev.ReleaseIfActive(); }   // a serverown request left open by a scenario that was cut goes back to the server
            catch (Exception e) { Plugin.Log.LogWarning("[Scenario] serverown release: " + e.Message); }

            if (error != null)
            {
                result.Status = ScenarioResult.Errored;
                result.Error = error.ToString();
                // Warning, not error: the run is already marked and must not trip the next run's listener.
                Plugin.Log.LogWarning($"[Scenario] {result.Id} threw: {error}");
            }
            else if (result.SkipReason != null && result.FailedCount == 0)
            {
                result.Status = ScenarioResult.Skipped;   // a precondition the session cannot meet: neither passed nor failed
            }
            else
            {
                result.Status = result.FailedCount > 0 ? ScenarioResult.Failed : ScenarioResult.Passed;
            }

            Plugin.Log.LogInfo($"[Scenario] {result.Id} {result.Status} in {result.DurationSeconds:0.00} s " +
                               $"({result.Assertions.Count - result.FailedCount}/{result.Assertions.Count} assertions)");
            s_current = null;
        }

        /// <summary>Collects error/fatal lines coming from the mod while a scenario runs.</summary>
        private sealed class ErrorLogListener : ILogListener
        {
            private readonly object _lock = new object();
            private List<string> _lines;

            public void Begin()
            {
                lock (_lock) _lines = new List<string>();
            }

            public List<string> End()
            {
                lock (_lock)
                {
                    List<string> lines = _lines ?? new List<string>();
                    _lines = null;
                    return lines;
                }
            }

            public void LogEvent(object sender, LogEventArgs e)
            {
                if ((e.Level & (LogLevel.Error | LogLevel.Fatal)) == 0) return;
                string source = e.Source?.SourceName;
                string text = e.Data?.ToString() ?? string.Empty;
                // Our own source, or an unhandled exception whose stack trace runs through mod code.
                if (source != Plugin.ModName && text.IndexOf("MistWorms.", StringComparison.Ordinal) < 0) return;
                lock (_lock)
                {
                    if (_lines != null && _lines.Count < 50) _lines.Add($"[{e.Level}:{source}] {text}");
                }
            }

            public void Dispose() { }
        }
    }
}
#endif
