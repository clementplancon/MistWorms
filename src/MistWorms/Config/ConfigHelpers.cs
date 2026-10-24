using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx.Configuration;

namespace MistWorms.Config
{
    internal static class ConfigHelpers
    {
        /// <summary>
        /// Reads "section/key -> raw value" straight from the .cfg file, using the same line rules as
        /// BepInEx's ConfigFile.Reload. BepInEx clamps silently on load, so the raw text is the only
        /// way to tell that a value was out of range.
        /// </summary>
        internal static Dictionary<string, string> ReadRawValues(string path)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return result;

            string section = string.Empty;
            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;

                if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                {
                    section = line.Substring(1, line.Length - 2);
                    continue;
                }

                string[] split = line.Split(new[] { '=' }, 2);
                if (split.Length != 2) continue;
                result[RawKey(section, split[0].Trim())] = split[1].Trim();
            }
            return result;
        }

        internal static string RawKey(string section, string key) => section + "\u0001" + key;

        /// <summary>Logs a warning when the file holds a parsable value that the entry's acceptable values reject.</summary>
        internal static void WarnIfOutOfRange(ConfigEntryBase entry, Dictionary<string, string> raw)
        {
            AcceptableValueBase acceptable = entry.Description?.AcceptableValues;
            if (acceptable == null || raw == null) return;

            ConfigDefinition def = entry.Definition;
            if (!raw.TryGetValue(RawKey(def.Section, def.Key), out string text)) return;

            object parsed;
            try
            {
                parsed = TomlTypeConverter.ConvertToValue(text, entry.SettingType);
            }
            catch (Exception)
            {
                // Unparsable values are already reported by BepInEx itself.
                return;
            }

            if (!acceptable.IsValid(parsed))
            {
                Plugin.Log?.LogWarning(
                    $"Config {def.Section}.{def.Key} = {text} is out of range; clamped to {entry.GetSerializedValue()}.");
            }
        }

        /// <summary>Splits a comma separated list, trimming entries and dropping empty ones.</summary>
        internal static List<string> SplitList(string value)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(value)) return result;
            foreach (string part in value.Split(','))
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0) result.Add(trimmed);
            }
            return result;
        }

        /// <summary>
        /// Parses a "prefab:weight,prefab:weight" list (Drops.BaitWeights). Malformed or non-positive
        /// entries are skipped with a warning naming <paramref name="keyName"/>; unknown prefabs are the caller's concern.
        /// </summary>
        internal static List<KeyValuePair<string, float>> ParseWeights(string value, string keyName)
        {
            var result = new List<KeyValuePair<string, float>>();
            foreach (string item in SplitList(value))
            {
                int colon = item.LastIndexOf(':');
                if (colon <= 0 || colon == item.Length - 1
                    || !float.TryParse(item.Substring(colon + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float weight)
                    || weight <= 0f)
                {
                    Plugin.Log?.LogWarning($"Config {keyName}: ignoring malformed entry '{item}' (expected prefab:weight).");
                    continue;
                }
                result.Add(new KeyValuePair<string, float>(item.Substring(0, colon).Trim(), weight));
            }
            return result;
        }
    }
}
