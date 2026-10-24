#if MW_DEV
using System;
using System.Collections.Generic;

namespace MistWorms.Dev.Scenarios
{
    internal sealed class ScenarioAssertion : IJsonValue
    {
        public string Name;
        public bool Ok;
        public object Expected;
        public object Actual;

        public object ToJson() => new Dictionary<string, object>
        {
            { "name", Name },
            { "ok", Ok },
            { "expected", Expected },
            { "actual", Actual },
        };
    }

    /// <summary>Outcome of one scenario run, as returned by <c>GET /scenario/{id}</c>.</summary>
    internal sealed class ScenarioResult : IJsonValue
    {
        public const string Running = "running";
        public const string Passed = "passed";
        public const string Failed = "failed";
        public const string Errored = "error";

        private const int MaxLogLines = 500;

        public string Id;
        public string Name;
        public string Status = Running;
        public Dictionary<string, string> Parameters = new Dictionary<string, string>();
        public readonly List<ScenarioAssertion> Assertions = new List<ScenarioAssertion>();
        public readonly List<string> Shots = new List<string>();
        public readonly List<string> Log = new List<string>();
        public string Error;
        public DateTime StartedAt = DateTime.Now;
        public float DurationSeconds;

        public bool Finished => Status != Running;

        public int FailedCount
        {
            get
            {
                int n = 0;
                foreach (ScenarioAssertion a in Assertions) if (!a.Ok) n++;
                return n;
            }
        }

        internal void AddLog(string line)
        {
            if (Log.Count < MaxLogLines) Log.Add(line);
            else if (Log.Count == MaxLogLines) Log.Add("... log truncated");
        }

        public object ToJson() => new Dictionary<string, object>
        {
            { "id", Id },
            { "name", Name },
            { "status", Status },
            { "params", Parameters },
            { "assertions", Assertions },
            { "assertionsPassed", Assertions.Count - FailedCount },
            { "assertionsFailed", FailedCount },
            { "shots", Shots },
            { "log", Log },
            { "error", Error },
            { "startedAt", StartedAt },
            { "durationSeconds", DurationSeconds },
        };
    }
}
#endif
