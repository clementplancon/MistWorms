using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using MistWorms.Assets;
using MistWorms.Config;
using MistWorms.Localization;
using MistWorms.Render;
using UnityEngine;

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
            bool assetsOk = AssetCatalog.Load(Path.Combine(PluginDir, "assets"));
            if (!assetsOk) Log.LogError("MistWorms visuals disabled: " + AssetCatalog.Error);
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
            try
            {
                // Visuals are client-only; a dedicated server never renders.
                if (AssetCatalog.IsReady && !Application.isBatchMode)
                {
                    if (!MaterialFactory.Build()) Log.LogError("[Render] " + MaterialFactory.Error);
                    else _ = WormRenderer.Instance; // creates the singleton
                }
            }
            catch (Exception e)
            {
                LogException("Plugin.OnVanillaPrefabsAvailable", e);
            }
            // Phase 3+: prefab registration (MW_WritheMass, MW_WormNest, MW_WormColony), status effects.
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
