using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using MistWorms.Assets;
using MistWorms.Colony;
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
        public const string ModVersion = "1.0.0";

        internal static Plugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }
        internal static string PluginDir { get; private set; }

        private Harmony _harmony;

        /// <summary>Per-event diagnostics: the call, and the interpolated message with it, does not exist in Release.</summary>
        [System.Diagnostics.Conditional("MW_DEV")]
        internal static void DevInfo(string message) => Log?.LogInfo(message);

        [System.Diagnostics.Conditional("MW_DEV")]
        internal static void DevDebug(string message) => Log?.LogDebug(message);

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

            ZNet.WorldSaveStarted += Colony.WormColony.OnWorldSaveStarted;   // the colonies leave their worms in their ZDOs before the world is copied (spec §11.4)
            PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabsAvailable;
            ItemManager.OnItemsRegistered += Drops.BaitWeightsInit.OnItemsRegistered;
            Bites.InfestedEffect.Register();
            Bites.EngulfedEffect.Register();
            ItemManager.OnItemsRegistered += OnItemsRegistered;

#if MW_DEV
            Dev.DevBootstrap.Init(this);
#endif
            Log.LogInfo($"{ModName} {ModVersion} loaded{(IsDevBuild ? " (dev build)" : string.Empty)}");
        }

        private void Update()
        {
            // The only safe moment to build the effect templates (DestroyImmediate) and to play what the hits and deaths asked for.
            if (!Application.isBatchMode) Colony.WormEffects.Pump();
            Drops.WormDrops.PumpPending();   // the items of a dead pool that did not fit in one frame (spec 7.3)
            MineSeeder.Pump();               // colony of a mine and its lazy seeding (spec 8.3)
        }

        /// <summary>The ObjectDB is ready (every time it is rebuilt): the Wisplight gets its virtual durability and the Infested effect its icon (plan 5-1).</summary>
        private void OnItemsRegistered()
        {
            Bites.Wisplight.Apply();
            Bites.InfestedEffect.ChooseIcon();
        }

        private void OnVanillaPrefabsAvailable()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= OnVanillaPrefabsAvailable;

            // Before any zone loads, on every peer: a server that meets an unknown prefab hash destroys the ZDO.
            ColonyPrefab.Register();
            Mass.MassPrefab.Register();
            NestPrefab.Register();

            try
            {
                // Hit proxies are client side and need no asset: a dedicated server has no local player to hit anything.
                if (!Application.isBatchMode) _ = ProxyManager.Instance;
            }
            catch (Exception e)
            {
                LogException("Plugin.ProxyManager", e);
            }

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
        }

        private void OnDestroy()
        {
            ZNet.WorldSaveStarted -= Colony.WormColony.OnWorldSaveStarted;
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

        /// <summary>
        /// Logs an exception from a patch or simulation step without letting it escape into vanilla code. Once per context and exception type: a catch that sits in an
        /// Update or a Tick would otherwise write to the log every frame. The repeats are only counted (<see cref="SuppressedExceptions"/>).
        /// </summary>
        internal static void LogException(string context, Exception e)
        {
            if (!s_loggedSites.Add(context + "|" + (e != null ? e.GetType().FullName : "null"))) { SuppressedExceptions++; return; }
            Log?.LogError($"[{context}] {e}");
        }

        /// <summary>Warns once per <paramref name="key"/>; the repeats are counted in silence.</summary>
        internal static void WarnOnce(string key, string message)
        {
            if (!s_warnedKeys.Add(key)) { SuppressedWarnings++; return; }
            Log?.LogWarning(message);
        }

        private static readonly System.Collections.Generic.HashSet<string> s_loggedSites = new System.Collections.Generic.HashSet<string>();
        private static readonly System.Collections.Generic.HashSet<string> s_warnedKeys = new System.Collections.Generic.HashSet<string>();
        internal static int SuppressedExceptions { get; private set; }
        internal static int SuppressedWarnings { get; private set; }
    }
}
