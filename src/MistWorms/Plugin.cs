using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using MistWorms.Config;
using MistWorms.Localization;

namespace MistWorms
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    [BepInDependency(Jotunn.Main.ModGuid, "2.30.0")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string ModGuid = "dev.pointvirgule.mistworms";
        public const string ModName = "MistWorms";
        public const string ModVersion = "0.1.0";

        internal static Plugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }
        internal static string PluginDir { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            PluginDir = Path.GetDirectoryName(Info.Location);

            MWConfig.Bind(Config);
            MWLocalization.Load(PluginDir);
            Commands.Register();

            _harmony = new Harmony(ModGuid);
            _harmony.PatchAll(typeof(Plugin).Assembly);

            PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabsAvailable;

#if MW_DEV
            Dev.DevBootstrap.Init(this);
#endif
            Log.LogInfo($"{ModName} {ModVersion} loaded{(IsDevBuild ? " (dev build)" : string.Empty)}");
        }

        private void OnVanillaPrefabsAvailable()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= OnVanillaPrefabsAvailable;
            // Phase 2+: AssetCatalog, prefab registration (MW_WritheMass, MW_WormNest, MW_WormColony), status effects.
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        internal static bool IsDevBuild
        {
            get
            {
#if MW_DEV
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>Logs an exception from a patch or simulation step without letting it escape into vanilla code.</summary>
        internal static void LogException(string context, Exception e)
        {
            Log?.LogError($"[{context}] {e}");
        }
    }
}
