#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Dev
{
    /// <summary>
    /// Clickless boot into a local solo game or a dedicated server join (spec §15.2).
    /// Drives the same FejdStartup methods the menu buttons call, so the vanilla 1.0 start path
    /// (Game.SetProfile, ZNet.SetServer, scene transition) stays authoritative.
    /// </summary>
    internal static class QuickStart
    {
        private const string Tag = "[QuickStart] ";

        private static DevArgs s_args;
        private static bool s_triggered;
        private static bool s_introSkipped;

        private static bool Armed => s_args != null && s_args.Enabled && (s_args.AutoLoad || s_args.AutoJoin);

        /// <summary>True until the first FejdStartup of this process has been taken over.</summary>
        private static bool Pending => Armed && !s_triggered;

        public static void Init(DevArgs args)
        {
            s_args = args;
            if (args == null || !args.Enabled) return;

            Game.m_playerInitialSpawn += OnPlayerInitialSpawn;

            if (args.AutoJoin)
                Info($"armed: join {args.JoinHost}:{args.JoinPort} as '{args.Character}'");
            else if (args.AutoLoad)
                Info($"armed: local world '{args.World}' (seed '{args.Seed}') as '{args.Character}'");
            else
                Info("no autoload/join target, menu left to the user");

            // Plugins normally load before the start scene's FejdStartup.Start; cover the opposite order too.
            if (FejdStartup.instance != null) Trigger();
        }

        private static void Trigger()
        {
            if (!Pending) return;
            s_triggered = true;
            Plugin.Instance.StartCoroutine(Run());
        }

        private static IEnumerator Run()
        {
            Info("main menu detected, waiting for it to settle");
            // FejdStartup.Start still has same-frame work (profile list, world cache, intro coroutine).
            yield return null;
            yield return new WaitForSecondsRealtime(0.5f);

            for (int i = 0; i < 30; i++)
            {
                bool dismissed;
                try { dismissed = DismissBlockers(); }
                catch (Exception e) { Plugin.LogException("QuickStart", e); yield break; }
                if (!dismissed) break;
                yield return null;
            }

            try { Execute(); }
            catch (Exception e) { Plugin.LogException("QuickStart", e); }
        }

        /// <summary>Closes anything that would sit on top of the menu. Returns true if something was closed.</summary>
        private static bool DismissBlockers()
        {
            FejdStartup fejd = FejdStartup.instance;
            if (fejd == null) return false;
            bool any = false;

            if (CinematicsManager.s_instance != null && CinematicsManager.IsStartedPlaying())
            {
                Info("stopping intro cinematic");
                CinematicsManager.Stop();
                any = true;
            }
            // Beta-branch NDA only; hidden without persisting acceptance.
            if (fejd.m_ndaPanel != null && fejd.m_ndaPanel.activeSelf)
            {
                Info("hiding NDA panel");
                fejd.m_ndaPanel.SetActive(false);
                fejd.m_mainMenu.SetActive(true);
                any = true;
            }
            if (fejd.m_connectionFailedPanel != null && fejd.m_connectionFailedPanel.activeSelf)
            {
                Info("dismissing connection error panel: " + fejd.m_connectionFailedError?.text);
                fejd.OnConnectionFailedOk();
                any = true;
            }
            if (fejd.m_changeLog != null && fejd.m_changeLog.activeSelf)
            {
                Info("closing changelog");
                fejd.m_changeLog.SetActive(false);
                any = true;
            }
            // Startup warnings (Vulkan/AMD, PlayFab, privileges...) all go through UnifiedPopup.
            // Vanilla shows no "modded game" popup in 1.0, only the m_moddedText label.
            for (int n = 0; n < 8 && UnifiedPopup.IsVisible(); n++)
            {
                Info("dismissing popup");
                UnifiedPopup.Pop();
                any = true;
            }
            return any;
        }

        private static void Execute()
        {
            FejdStartup fejd = FejdStartup.instance;
            if (fejd == null)
            {
                Error("FejdStartup is gone, aborting");
                return;
            }
            if (string.IsNullOrEmpty(s_args.Character))
            {
                Error("no character name (autoload=<world>/<character> or Dev.AutoLoadCharacter)");
                return;
            }

            string profileFile = EnsureProfile(fejd, s_args.Character);
            if (profileFile == null) return;

            if (s_args.AutoJoin)
            {
                Join(fejd, profileFile);
                return;
            }

            if (!EnsureWorld(s_args.World, s_args.Seed)) return;
            StartLocal(fejd, profileFile, s_args.World);
        }

        // ---- Profile -------------------------------------------------------------------------

        private static string EnsureProfile(FejdStartup fejd, string name)
        {
            // OnNewCharacterDone stores the profile under the lower-cased typed name.
            string filename = name.ToLowerInvariant();

            foreach (PlayerProfile p in SaveSystem.GetAllPlayerProfiles())
            {
                if (string.Equals(p.GetFilename(), filename, StringComparison.OrdinalIgnoreCase))
                {
                    Info($"profile '{p.GetName()}' found (file '{p.GetFilename()}', source {p.m_fileSource})");
                    return p.GetFilename();
                }
            }
            if (PlayerProfile.HaveProfile(filename))
            {
                Error($"profile file '{filename}' exists but failed to load; fix or delete it");
                return null;
            }
            if (name.Length < 3) Warn($"character name '{name}' is shorter than the menu allows (3)");

            Info($"creating profile '{name}' (local)");
            CreateProfile(fejd, name, filename);
            if (!PlayerProfile.HaveProfile(filename))
            {
                Error($"profile '{filename}' still missing after save");
                return null;
            }
            return filename;
        }

        /// <summary>Mirrors FejdStartup.OnNewCharacterDone(forceLocal: true) without the name input field.</summary>
        private static void CreateProfile(FejdStartup fejd, string name, string filename)
        {
            bool mounted = MountCloud();
            try
            {
                var profile = new PlayerProfile(filename, FileHelpers.FileSource.Local);
                // Fresh preview player, exactly what OnCharacterNew gives OnNewCharacterDone.
                fejd.SetupCharacterPreview(null);
                Player player = fejd.GetPreviewPlayer();
                player.GiveDefaultItems();
                profile.SetName(name);
                profile.SavePlayerData(player);
                // Skips the Valkyrie intro so automated runs land on the ground immediately.
                profile.m_firstSpawn = false;
                if (!profile.Save()) Error($"PlayerProfile.Save returned false for '{filename}'");
            }
            finally
            {
                if (mounted) UnmountCloud();
            }
            // Force the menu to rescan profiles.
            fejd.m_profiles = null;
        }

        // ---- World ---------------------------------------------------------------------------

        private static bool EnsureWorld(string worldName, string seed)
        {
            if (World.HaveWorld(worldName))
            {
                Info($"world '{worldName}' found");
                return true;
            }
            if (worldName.Length < 5) Warn($"world name '{worldName}' is shorter than the menu allows (5)");
            if (string.IsNullOrEmpty(seed)) seed = World.GenerateSeed();

            Info($"creating world '{worldName}' with seed '{seed}' (local)");
            // Mirrors FejdStartup.OnNewWorldDone(forceLocal: true) without the input fields.
            var world = new World(worldName, seed)
            {
                m_fileSource = FileHelpers.FileSource.Local,
                m_needsDB = false,
            };
            bool mounted = MountCloud();
            try
            {
                AltBiomeWorldData.RemoveCache(worldName);
                SaveSystem.SetSaveNumber(0u);
                world.SaveWorldFWLData(DateTime.Now);
                SaveSystem.InvalidateCache(SaveDataType.World);
            }
            finally
            {
                if (mounted) UnmountCloud();
            }

            if (!World.HaveWorld(worldName))
            {
                Error($"world '{worldName}' still missing after save");
                return false;
            }
            return true;
        }

        // ---- Start paths ---------------------------------------------------------------------

        /// <summary>Main menu Start -> character Start -> world Start, with server opening off.</summary>
        private static void StartLocal(FejdStartup fejd, string profileFile, string worldName)
        {
            if (!SelectProfile(fejd, profileFile)) return;

            // Same flag -joinserverwithcharacter uses; only drops the 1.5 s menu fade delay.
            fejd.m_instantStart = true;
            fejd.OnCharacterStart(); // Game.SetProfile + ShowStartGame (refreshes the world list)

            World world = fejd.FindWorld(worldName);
            if (world == null)
            {
                Error($"world '{worldName}' not in the menu world list");
                return;
            }
            fejd.m_world = world;
            fejd.UpdateWorldList(centerSelection: true);
            world = fejd.m_world;

            if (world.m_dataError != World.SaveDataError.None)
            {
                // Vanilla would open a restore-backup prompt here; that needs a human decision.
                Error($"world '{worldName}' has data error {world.m_dataError}, not starting");
                return;
            }
            if (!string.IsNullOrEmpty(s_args.Seed) && world.m_seedName != s_args.Seed)
                Warn($"existing world seed '{world.m_seedName}' differs from requested '{s_args.Seed}' (world not recreated)");

            // Local solo: no open server, no public listing, so no password is ever needed.
            fejd.m_openServerToggle.SetIsOnWithoutNotify(false);
            fejd.m_publicServerToggle.SetIsOnWithoutNotify(false);
            if (!fejd.CanStartServer()) Warn("CanStartServer() is false, calling OnWorldStart anyway");

            Info($"starting local game: world '{world.m_name}' seed '{world.m_seedName}', profile '{profileFile}'");
            fejd.OnWorldStart();

            if (fejd.m_startingWorld)
                Info("world start accepted, loading main scene");
            else if (fejd.m_cloudStorageWarningNextSave != null && fejd.m_cloudStorageWarningNextSave.activeSelf)
                Error("blocked by the cloud storage quota warning (profile or world on cloud storage)");
            else
                Error("OnWorldStart did not start the world (check the vanilla log above)");
        }

        /// <summary>Same path as a Steam +connect invite: queued join consumed by the character Start button.</summary>
        private static void Join(FejdStartup fejd, string profileFile)
        {
            if (s_args.JoinPort <= 0 || s_args.JoinPort > ushort.MaxValue)
            {
                Error($"invalid join port {s_args.JoinPort}");
                return;
            }
            if (!SelectProfile(fejd, profileFile)) return;

            if (!string.IsNullOrEmpty(s_args.JoinPassword))
                FejdStartup.ServerPassword = s_args.JoinPassword; // ZNet.RPC_ClientHandshake submits it automatically

            fejd.m_queuedJoinServer = new ServerJoinData(new ServerJoinDataDedicated(s_args.JoinHost, (ushort)s_args.JoinPort));
            fejd.m_instantStart = true;

            Info($"joining {s_args.JoinHost}:{s_args.JoinPort} with profile '{profileFile}'");
            // TODO(phase1-verify): JoinServer resolves the dedicated backend asynchronously (PlayFab lookup when
            // logged in, else Steamworks) and polls LoadMainSceneIfBackendSelected up to 50 times; check the log for
            // "Determined backend of dedicated server" and that a -crossplay-off local server is reached via Steamworks.
            fejd.OnCharacterStart();
        }

        /// <summary>Main menu Start button, then select the profile in the character carousel.</summary>
        private static bool SelectProfile(FejdStartup fejd, string profileFile)
        {
            fejd.m_profiles = null;
            fejd.OnStartGame();
            fejd.SetSelectedProfile(profileFile);

            List<PlayerProfile> profiles = fejd.m_profiles;
            int index = fejd.m_profileIndex;
            if (profiles == null || index < 0 || index >= profiles.Count || profiles[index].GetFilename() != profileFile)
            {
                Error($"could not select profile '{profileFile}' in the character screen");
                return false;
            }
            PlayerProfile selected = profiles[index];
            Info($"profile selected: '{selected.GetName()}' (source {selected.m_fileSource})");
            return true;
        }

        // ---- In game -------------------------------------------------------------------------

        private static void OnPlayerInitialSpawn()
        {
            try
            {
                string world = ZNet.instance != null ? ZNet.instance.GetWorldName() : "?";
                string player = Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName() : "?";
                Info($"in game: world '{world}', player '{player}', server={ZNet.instance != null && ZNet.instance.IsServer()}");

                // The vanilla command toggles, so only send it while cheats are off; on a dedicated
                // server it is forwarded and only takes effect for admins.
                if (MWConfig.Dev.AutoDevCommands.Value && !Terminal.m_cheat && global::Console.instance != null)
                {
                    Info("enabling devcommands");
                    global::Console.instance.TryRunCommand("devcommands");
                }
            }
            catch (Exception e)
            {
                Plugin.LogException("QuickStart", e);
            }
        }

        // ---- Cloud mount ---------------------------------------------------------------------
        // FileHelpers.Mount/Unmount take Splatform enums and Splatform.dll is not referenced, so they are
        // reached by reflection. Vanilla wraps every save write in them when a cloud provider exists.

        private static bool MountCloud()
        {
            if (!FileHelpers.CloudStorageSupported) return false;
            MethodInfo mount = AccessTools.Method(typeof(FileHelpers), "Mount");
            object readWrite = Enum.Parse(mount.GetParameters()[0].ParameterType, "ReadWrite");
            if (!(bool)mount.Invoke(null, new[] { readWrite }))
                throw new InvalidOperationException("Failed to mount!");
            return true;
        }

        private static void UnmountCloud()
        {
            MethodInfo unmount = AccessTools.Method(typeof(FileHelpers), "Unmount");
            ParameterInfo[] ps = unmount.GetParameters();
            object[] args = ps.Length == 0 ? new object[0] : new[] { Enum.Parse(ps[0].ParameterType, "Commit") };
            unmount.Invoke(null, args);
        }

        // ---- Logging -------------------------------------------------------------------------

        private static void Info(string msg) => Plugin.Log.LogInfo(Tag + msg);
        private static void Warn(string msg) => Plugin.Log.LogWarning(Tag + msg);
        private static void Error(string msg) => Plugin.Log.LogError(Tag + msg);

        // ---- Patches -------------------------------------------------------------------------

        [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Start))]
        private static class FejdStartupStartPatch
        {
            private static void Postfix()
            {
                try { Trigger(); }
                catch (Exception e) { Plugin.LogException("QuickStart", e); }
            }
        }

        /// <summary>
        /// The first-launch intro video would hide the menu for ~1 min; replace it with the
        /// vanilla "SkipIntroCinematic" branch (fade the menu in) without touching the pref.
        /// Only the first menu of the process; later menus (after logout) behave as vanilla.
        /// </summary>
        [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.TryPlayIntroCinematic))]
        private static class FejdStartupIntroPatch
        {
            private static bool Prefix(FejdStartup __instance, ref IEnumerator __result)
            {
                try
                {
                    if (!Armed || s_introSkipped) return true;
                    s_introSkipped = true;
                    Info("skipping intro cinematic");
                    __result = FadeInMenu(__instance);
                    return false;
                }
                catch (Exception e)
                {
                    Plugin.LogException("QuickStart", e);
                    return true;
                }
            }

            private static IEnumerator FadeInMenu(FejdStartup fejd)
            {
                fejd.m_menuAnimator.SetTrigger("FadeIn");
                yield break;
            }
        }
    }
}
#endif
