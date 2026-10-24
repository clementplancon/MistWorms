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
            DevBridge.Host.StartCoroutine(Execute(scenario, ctx, result));
            return result.Id;
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

            IEnumerator body = null;
            try { body = scenario.Run(ctx); }
            catch (Exception e) { error = e; }

            if (body != null)
            {
                yield return SafeCoroutine.Run(body, e => error = e, () =>
                {
                    if (Time.realtimeSinceStartup - start <= ctx.TimeoutSeconds) return false;
                    timedOut = true;
                    return true;
                });
            }

            List<string> logErrors = s_errors.End();
            result.DurationSeconds = Time.realtimeSinceStartup - start;

            if (timedOut)
                ctx.Assert("timeout", false, $"<= {ctx.TimeoutSeconds:0} s", $"{result.DurationSeconds:0.0} s");
            if (logErrors.Count > 0)
            {
                ctx.Assert("no_mod_errors_in_log", false, 0, logErrors.Count);
                foreach (string line in logErrors) result.AddLog("log error: " + line);
            }

            if (error != null)
            {
                result.Status = ScenarioResult.Errored;
                result.Error = error.ToString();
                // Warning, not error: the run is already marked and must not trip the next run's listener.
                Plugin.Log.LogWarning($"[Scenario] {result.Id} threw: {error}");
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
