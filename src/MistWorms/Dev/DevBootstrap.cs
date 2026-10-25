#if MW_DEV
using System;
using UnityEngine;

namespace MistWorms.Dev
{
    /// <summary>Entry point of the dev harness, called from <see cref="Plugin"/>.Awake in Debug builds.</summary>
    internal static class DevBootstrap
    {
        internal static DevArgs Args { get; private set; }

        public static void Init(Plugin plugin)
        {
            try
            {
                Args = DevArgs.Parse(Environment.GetCommandLineArgs());
                DevArgs a = Args;
                string join = a.JoinHost != null ? $"{a.JoinHost}:{a.JoinPort}" : "-";
                Plugin.Log.LogInfo($"[Dev] args: mwdev={a.Enabled} world={a.World} character={a.Character} seed={a.Seed} " +
                                   $"join={join} bridge={a.BridgePort} autoload={a.AutoLoad} autojoin={a.AutoJoin}");

                if (a.Enabled)
                {
                    // Automation drives the game while another window has focus.
                    Application.runInBackground = true;
                    if (a.BridgePort > 0) DevBridge.Start(a.BridgePort);
                }

                GpuWorms.Load();      // the GPU worm spike: bundle, mesh, compute test (logged; never fatal)

                // QuickStart also owns Dev.AutoDevCommands (first spawn, -mwdev only), so it is not repeated here.
                QuickStart.Init(a);
            }
            catch (Exception e)
            {
                Plugin.LogException("DevBootstrap", e);
            }
        }
    }
}
#endif
