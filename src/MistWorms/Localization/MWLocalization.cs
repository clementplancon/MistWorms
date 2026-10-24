using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Jotunn.Entities;
using Jotunn.Managers;

namespace MistWorms.Localization
{
    /// <summary>
    /// Registers the mod's JSON translations with Jotunn (spec §2) and wraps the vanilla localizer.
    /// Note: inside the MistWorms namespace, the bare name <c>Localization</c> resolves to this namespace,
    /// so the vanilla class must always be written <c>global::Localization</c>.
    /// </summary>
    internal static class MWLocalization
    {
        public const string FolderName = "Localization";
        public const string FallbackLanguage = "English";

        // File name -> Valheim language id (Localization.LocalizationConstants). English first: it is the fallback.
        private static readonly KeyValuePair<string, string>[] Files =
        {
            new KeyValuePair<string, string>("en.json", FallbackLanguage),
            new KeyValuePair<string, string>("fr.json", "French"),
        };

        public static void Load(string pluginDir)
        {
            CustomLocalization loc = LocalizationManager.Instance.GetLocalization();
            string dir = Path.Combine(pluginDir ?? string.Empty, FolderName);

            foreach (KeyValuePair<string, string> file in Files)
            {
                string path = Path.Combine(dir, file.Key);
                try
                {
                    if (!File.Exists(path))
                    {
                        Plugin.Log.LogError($"Localization file missing: {path} ({file.Value} texts will fall back to tokens)");
                        continue;
                    }
                    // ReadAllText with UTF8 strips a BOM if an editor added one.
                    loc.AddJsonFile(file.Value, File.ReadAllText(path, Encoding.UTF8));
                }
                catch (Exception e)
                {
                    Plugin.LogException($"Localization {file.Key}", e);
                }
            }

            WarnMissingTokens(loc);
        }

        /// <summary>Localizes a token; the leading '$' is optional. Unknown tokens come back as "[token]" (vanilla).</summary>
        public static string Get(string token)
        {
            if (string.IsNullOrEmpty(token)) return token;
            return global::Localization.instance.Localize(token[0] == '$' ? token : "$" + token);
        }

        /// <summary>
        /// Localizes a "{0}"-style template token, then fills it. String arguments that are themselves tokens
        /// (start with '$', e.g. a Character.m_name) are localized too. Vanilla's own "$1" substitution is not used.
        /// </summary>
        public static string Format(string token, params object[] args)
        {
            string template = Get(token);
            if (args == null || args.Length == 0) return template;

            var filled = new object[args.Length];
            for (int i = 0; i < args.Length; i++)
                filled[i] = args[i] is string s && s.Length > 1 && s[0] == '$' ? Get(s) : args[i];

            try
            {
                return string.Format(CultureInfo.InvariantCulture, template, filled);
            }
            catch (FormatException e)
            {
                Plugin.Log.LogWarning($"Bad localization template for {token}: \"{template}\" ({e.Message})");
                return template;
            }
        }

        // Spec §19 requires complete FR and EN texts; surface gaps in the log instead of at the player.
        private static void WarnMissingTokens(CustomLocalization loc)
        {
            IReadOnlyDictionary<string, string> fallback = loc.GetTranslations(FallbackLanguage);
            if (fallback == null || fallback.Count == 0) return;

            foreach (KeyValuePair<string, string> file in Files)
            {
                if (file.Value == FallbackLanguage) continue;
                IReadOnlyDictionary<string, string> other = loc.GetTranslations(file.Value);
                if (other == null || other.Count == 0) continue; // missing file already logged
                foreach (string key in fallback.Keys)
                {
                    if (!other.ContainsKey(key))
                        Plugin.Log.LogWarning($"Token '{key}' missing in {file.Key}");
                }
                foreach (string key in other.Keys)
                {
                    if (!fallback.ContainsKey(key))
                        Plugin.Log.LogWarning($"Token '{key}' in {file.Key} has no English fallback");
                }
            }
        }
    }
}
