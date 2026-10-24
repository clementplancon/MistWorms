#if MW_DEV
using System.Collections;
using BepInEx.Bootstrap;
using MistWorms.Config;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>Harness self-check: plugin, config, localization and (in game) the local player.</summary>
    internal sealed class S00_Smoke : IDevScenario
    {
        public string Name => "S00_Smoke";

        public IEnumerator Run(ScenarioContext ctx)
        {
            ctx.Assert("plugin_loaded",
                Plugin.Instance != null && Chainloader.PluginInfos.ContainsKey(Plugin.ModGuid),
                Plugin.ModGuid, Plugin.Instance != null ? Plugin.ModVersion : null);

            bool bound = MWConfig.Worm.Health != null && MWConfig.Dev.BridgePort != null
                && Plugin.Instance != null && MWConfig.Worm.Health.ConfigFile == Plugin.Instance.Config;
            ctx.Assert("config_bound", bound, "Worm.Health bound to the plugin config",
                MWConfig.Worm.Health != null ? (object)MWConfig.Worm.Health.Value : null);

            // A missing token comes back as "[mw_mass]" (or unchanged) from vanilla Localize.
            string localized = global::Localization.instance != null ? global::Localization.instance.Localize("$mw_mass") : null;
            bool translated = !string.IsNullOrEmpty(localized) && !localized.StartsWith("$") && !localized.StartsWith("[");
            ctx.Assert("localization_mw_mass", translated, "translated text", localized);

            if (ctx.InGame)
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null, 30f);
                ctx.Assert("local_player_exists", Player.m_localPlayer != null, true, Player.m_localPlayer != null);
            }
            else
            {
                ctx.Log("not in game; local player check skipped");
            }
        }
    }
}
#endif
