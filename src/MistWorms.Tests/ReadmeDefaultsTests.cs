using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using MistWorms.Config;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>
    /// The defaults announced in thunderstore/README.md (the "Key | Default" table) must be the ones of MWConfig. A row cites one or more
    /// <c>Section.Key</c> in the first cell and, in the second, either one value for all of them or "a / b / c", one per key (a trailing unit
    /// is ignored). Also guards the 1.0.0 packaging files: manifest and Plugin.ModVersion agree and no draft marker is left.
    /// </summary>
    public class ReadmeDefaultsTests
    {
        private static string FindRepoRoot()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "MistWorms.sln"))) return dir;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        private static string RequireRepoRoot()
        {
            string root = FindRepoRoot();
            Assert.True(root != null, "MistWorms.sln not found above " + AppDomain.CurrentDomain.BaseDirectory);
            return root;
        }

        /// <summary>
        /// MWConfig.Bind is static and returns at once when it already holds a ConfigFile: forget it so that the defaults read here come from this test's own binding,
        /// whatever ran before (the tests do not run in parallel, see the assembly attribute of MassWorkerTests).
        /// </summary>
        private static void ResetMWConfig()
        {
            const BindingFlags f = BindingFlags.NonPublic | BindingFlags.Static;
            Type t = typeof(MWConfig);
            FieldInfo cfg = t.GetField("_cfg", f), entries = t.GetField("Entries", f), order = t.GetField("_order", f);
            Assert.True(cfg != null && entries != null && order != null, "MWConfig fields _cfg / Entries / _order not found");
            cfg.SetValue(null, null);
            entries.FieldType.GetMethod("Clear").Invoke(entries.GetValue(null), null);
            order.SetValue(null, 0);
        }

        private static readonly Regex KeyRx = new Regex(@"`([A-Za-z]+)\.([A-Za-z]+)`");

        [Fact]
        public void ReadmeDefaultsMatchConfigDefaults()
        {
            string root = RequireRepoRoot();
            string readme = Path.Combine(root, "thunderstore", "README.md");
            Assert.True(File.Exists(readme), "thunderstore/README.md not found at " + readme);

            string tmp = Path.Combine(Path.GetTempPath(), "mw-readme-" + Guid.NewGuid().ToString("N") + ".cfg");
            var cfg = new ConfigFile(tmp, false);
            ResetMWConfig();
            MWConfig.Bind(cfg);

            var defaults = new Dictionary<string, object>();
            foreach (ConfigDefinition def in cfg.Keys) defaults[def.Section + "." + def.Key] = cfg[def].DefaultValue;

            int checkedKeys = 0;
            foreach (string line in File.ReadAllLines(readme))
            {
                if (!line.StartsWith("| `", StringComparison.Ordinal)) continue;
                string[] cells = line.Split('|');
                if (cells.Length < 4) continue;
                MatchCollection keys = KeyRx.Matches(cells[1]);
                string[] values = cells[2].Trim().Split(new[] { " / " }, StringSplitOptions.None);
                Assert.True(values.Length == 1 || values.Length == keys.Count, "README row has " + keys.Count + " keys but " + values.Length + " values: " + line);

                for (int i = 0; i < keys.Count; i++)
                {
                    string key = keys[i].Groups[1].Value + "." + keys[i].Groups[2].Value;
                    Assert.True(defaults.TryGetValue(key, out object actual), "README cites unknown config key " + key);
                    string announced = values[values.Length == 1 ? 0 : i].Trim();
                    Assert.True(Same(actual, announced), "README announces " + key + " = " + announced + " but the code default is " + actual);
                    checkedKeys++;
                }
            }
            Assert.True(checkedKeys >= 10, "README table not found or too short (" + checkedKeys + " keys)");
        }

        private static bool Same(object actual, string announced)
        {
            if (actual is bool b) return string.Equals(announced, b ? "true" : "false", StringComparison.OrdinalIgnoreCase);
            string number = announced.Split(' ')[0];
            if (actual is int || actual is float || actual is double)
            {
                if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) return false;
                return Math.Abs(Convert.ToDouble(actual, CultureInfo.InvariantCulture) - v) < 1e-5;
            }
            return string.Equals(Convert.ToString(actual, CultureInfo.InvariantCulture), announced, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ManifestVersionMatchesPluginAndNoDraftMarkerRemains()
        {
            string root = RequireRepoRoot();
            foreach (string need in new[] { "manifest.json", "README.md", "CHANGELOG.md" })
                Assert.True(File.Exists(Path.Combine(root, "thunderstore", need)), "thunderstore/" + need + " not found");
            string manifest = File.ReadAllText(Path.Combine(root, "thunderstore", "manifest.json"));
            Match m = Regex.Match(manifest, "\"version_number\"\\s*:\\s*\"([^\"]+)\"");
            Assert.True(m.Success);
            Assert.Equal(Plugin.ModVersion, m.Groups[1].Value);

            foreach (string f in new[] { "README.md", "CHANGELOG.md" })
            {
                string text = File.ReadAllText(Path.Combine(root, "thunderstore", f));
                Assert.DoesNotContain("DRAFT", text);
                Assert.DoesNotContain("Brouillon", text);
            }
            Assert.Contains("## " + Plugin.ModVersion, File.ReadAllText(Path.Combine(root, "thunderstore", "CHANGELOG.md")));
        }
    }
}
