using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>Checks src/MistWorms/Localization/{en,fr}.json (Jotunn/Crowdin flat format) against spec §2.</summary>
    public class LocalizationTests
    {
        // Every player-visible token listed in spec §2, without the leading '$'.
        private static readonly string[] SpecTokens =
        {
            "mw_mass", "mw_mass_count", "mw_nest",
            "mw_se_infested_name", "mw_se_infested_desc", "mw_text_eq",
        };

        // Pure format strings ("{0} ({1})") are legitimately identical in both languages.
        private static readonly string[] DisplayTokens =
        {
            "mw_mass", "mw_nest", "mw_se_infested_name", "mw_se_infested_desc", "mw_text_eq",
        };

        // string.Format style ({0}) and vanilla Localization.Localize(text, words) style ($1).
        private static readonly Regex Placeholder = new Regex(@"\{\d+\}|\$\d+", RegexOptions.Compiled);

        public static IEnumerable<object[]> Languages => new[] { new object[] { "en" }, new object[] { "fr" } };

        [Theory]
        [MemberData(nameof(Languages))]
        public void FileParsesAsFlatStringMap(string lang)
        {
            Dictionary<string, string> map = Load(lang);
            Assert.NotEmpty(map);
        }

        [Fact]
        public void LanguagesHaveIdenticalKeySets()
        {
            var en = Load("en").Keys.ToList();
            var fr = Load("fr").Keys.ToList();
            Assert.Empty(en.Except(fr));
            Assert.Empty(fr.Except(en));
        }

        [Theory]
        [MemberData(nameof(Languages))]
        public void ContainsEverySpecToken(string lang)
        {
            var map = Load(lang);
            var missing = SpecTokens.Where(t => !map.ContainsKey(t)).ToList();
            Assert.Empty(missing);
        }

        [Theory]
        [MemberData(nameof(Languages))]
        public void NoEmptyValues(string lang)
        {
            var map = Load(lang);
            var empty = map.Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key).ToList();
            Assert.Empty(empty);
        }

        [Fact]
        public void FrenchDisplayNamesDifferFromEnglish()
        {
            var en = Load("en");
            var fr = Load("fr");
            var untranslated = DisplayTokens.Where(t => en.ContainsKey(t) && fr.ContainsKey(t) && en[t] == fr[t]).ToList();
            Assert.Empty(untranslated);
        }

        [Fact]
        public void PlaceholdersMatchAcrossLanguages()
        {
            var en = Load("en");
            var fr = Load("fr");
            var mismatched = en.Keys.Where(fr.ContainsKey)
                .Where(k => PlaceholderSet(en[k]) != PlaceholderSet(fr[k]))
                .ToList();
            Assert.Empty(mismatched);
        }

        [Fact]
        public void EveryTokenUsedInSourceExistsInBothLanguages()
        {
            var used = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string file in Directory.EnumerateFiles(SourceDir(), "*.cs", SearchOption.AllDirectories))
            {
                string rel = file.Replace('/', '.').Replace((char)92, '.');
                if (rel.Contains(".obj.") || rel.Contains(".bin.")) continue;
                foreach (Match m in SourceToken.Matches(File.ReadAllText(file, Encoding.UTF8)))
                    used.Add(m.Groups[1].Value);
            }
            Assert.NotEmpty(used);
            foreach (string lang in new[] { "en", "fr" })
            {
                var map = Load(lang);
                var missing = used.Where(t => !map.ContainsKey(t)).ToList();
                Assert.True(missing.Count == 0, lang + ".json misses: " + string.Join(", ", missing));
            }
        }

        // A token literal in the code: "$mw_something" (ZDO keys and command names carry no '$').
        private static readonly Regex SourceToken = new Regex(@"\$(mw_[a-z0-9_]+)", RegexOptions.Compiled);

        private static string SourceDir()
        {
            for (var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "src", "MistWorms");
                if (Directory.Exists(candidate)) return candidate;
            }
            throw new DirectoryNotFoundException("src/MistWorms not found above " + AppDomain.CurrentDomain.BaseDirectory);
        }

        private static string PlaceholderSet(string s) =>
            string.Join(",", Placeholder.Matches(s).Cast<Match>().Select(m => m.Value).Distinct().OrderBy(v => v, StringComparer.Ordinal));

        private static Dictionary<string, string> Load(string lang)
        {
            string path = Path.Combine(LocalizationDir(), lang + ".json");
            Assert.True(File.Exists(path), $"Missing {path}");
            var serializer = new DataContractJsonSerializer(
                typeof(Dictionary<string, string>),
                new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText(path, Encoding.UTF8))))
            {
                var raw = (Dictionary<string, string>)serializer.ReadObject(stream);
                // Jotunn accepts keys with or without '$'; normalize so the checks do not care.
                return raw.ToDictionary(kv => kv.Key.TrimStart('$'), kv => kv.Value);
            }
        }

        // Prefer the source tree so tests always see the files being edited, then the copy next to the plugin.
        private static string LocalizationDir()
        {
            for (var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "src", "MistWorms", "Localization");
                if (Directory.Exists(candidate)) return candidate;
            }
            string copied = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Localization");
            if (Directory.Exists(copied)) return copied;
            throw new DirectoryNotFoundException("src/MistWorms/Localization not found above " + AppDomain.CurrentDomain.BaseDirectory);
        }
    }
}
