using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Entities;
using Jotunn.Managers;
using MistWorms.Localization;

namespace MistWorms
{
    /// <summary>
    /// Console commands (spec §2). Release builds expose only <c>mw_version</c>; everything else is MW_DEV.
    /// Dev command help and output are English-only on purpose: they never reach a release player.
    /// </summary>
    internal static class Commands
    {
        public static void Register()
        {
            CommandManager.Instance.AddConsoleCommand(new VersionCommand());
#if MW_DEV
            CommandManager.Instance.AddConsoleCommand(new StubCommand("mw_spawn", "<n> [radius] - spawn n virtual worms around the player", 3));
            CommandManager.Instance.AddConsoleCommand(new StubCommand("mw_mass", "<n> - spawn a writhing mass of n worms in front of the player", 4));
            CommandManager.Instance.AddConsoleCommand(new StubCommand("mw_nest", "- place a worm nest in front of the player", 7));
            CommandManager.Instance.AddConsoleCommand(new StubCommand("mw_kill", "- kill every virtual worm, mass and nest nearby", 3));
            CommandManager.Instance.AddConsoleCommand(new StubCommand("mw_state", "- print colony, mass and player state", 3, cheat: false));
            CommandManager.Instance.AddConsoleCommand(new StubCommand("mw_tpmine", "- teleport into the closest infested mine", 3));
            CommandManager.Instance.AddConsoleCommand(new StubCommand("mw_wake", "- wake every dormant group of the current mine", 7));
            CommandManager.Instance.AddConsoleCommand(new StubCommand("mw_calm", "- send every active group of the current mine back to dormancy", 7));
            CommandManager.Instance.AddConsoleCommand(new StubCommand("mw_perf", "- print simulation/render/network timings", 8, cheat: false));
            CommandManager.Instance.AddConsoleCommand(new ScenarioCommand());
            CommandManager.Instance.AddConsoleCommand(new ScenariosCommand());
            CommandManager.Instance.AddConsoleCommand(new DumpCommand());
            CommandManager.Instance.AddConsoleCommand(new PreviewCommand());
#endif
        }

        /// <summary>Shared plumbing: output to the calling terminal and no exception ever escapes into vanilla.</summary>
        private abstract class MWCommand : ConsoleCommand
        {
            public sealed override void Run(string[] args, Terminal context)
            {
                try
                {
                    Execute(args ?? Array.Empty<string>(), context);
                }
                catch (Exception e)
                {
                    Plugin.LogException(Name, e);
                    Print(context, MWLocalization.Format("$mw_cmd_error", e.Message));
                }
            }

            protected abstract void Execute(string[] args, Terminal context);

            protected static void Print(Terminal context, string text)
            {
                Terminal target = context != null ? context : global::Console.instance;
                if (target != null) target.AddString(text);
                else Plugin.Log.LogInfo(text);
            }
        }

        private sealed class VersionCommand : MWCommand
        {
            public override string Name => "mw_version";
            public override string Help => MWLocalization.Get("$mw_cmd_version_help");

            protected override void Execute(string[] args, Terminal context)
            {
                string build = MWLocalization.Get(Plugin.IsDevBuild ? "$mw_cmd_build_dev" : "$mw_cmd_build_release");
                Print(context, MWLocalization.Format("$mw_cmd_version_output",
                    Plugin.ModName, Plugin.ModVersion, build, global::Version.GetVersionString()));
            }
        }

#if MW_DEV
        /// <summary>Placeholder for commands whose feature lands in a later phase (spec §16).</summary>
        private sealed class StubCommand : MWCommand
        {
            private readonly string _name;
            private readonly string _help;
            private readonly int _phase;
            private readonly bool _cheat;

            public StubCommand(string name, string help, int phase, bool cheat = true)
            {
                _name = name;
                _help = help;
                _phase = phase;
                _cheat = cheat;
            }

            public override string Name => _name;
            public override string Help => $"[dev] {_help}";
            public override bool IsCheat => _cheat;

            protected override void Execute(string[] args, Terminal context)
            {
                Print(context, $"{_name}: not implemented yet (phase {_phase})");
            }
        }

        private sealed class ScenarioCommand : MWCommand
        {
            public override string Name => "mw_scenario";
            public override string Help => "[dev] <name> [key=value ...] - start an in-game test scenario, prints its run id";
            public override bool IsCheat => true;

            public override List<string> CommandOptionList() => Dev.Scenarios.ScenarioRunner.Names.ToList();

            protected override void Execute(string[] args, Terminal context)
            {
                if (args.Length == 0)
                {
                    Print(context, "usage: mw_scenario <name> [key=value ...] (see mw_scenarios)");
                    return;
                }

                Dictionary<string, string> parameters = null;
                for (int i = 1; i < args.Length; i++)
                {
                    int eq = args[i].IndexOf('=');
                    if (eq <= 0) continue;
                    if (parameters == null) parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    parameters[args[i].Substring(0, eq)] = args[i].Substring(eq + 1);
                }

                string id = Dev.Scenarios.ScenarioRunner.Start(args[0], parameters);
                Print(context, $"mw_scenario: started '{args[0]}', run id {id} (GET /scenario/{id})");
            }
        }

        private sealed class ScenariosCommand : MWCommand
        {
            public override string Name => "mw_scenarios";
            public override string Help => "[dev] - list the available test scenarios";

            protected override void Execute(string[] args, Terminal context)
            {
                List<string> names = Dev.Scenarios.ScenarioRunner.Names.ToList();
                Print(context, names.Count == 0
                    ? "mw_scenarios: none registered"
                    : $"mw_scenarios ({names.Count}): {string.Join(", ", names)}");
            }
        }

        private sealed class PreviewCommand : MWCommand
        {
            public override string Name => "mw_preview";
            public override string Help => "[dev] worm [anim] [count] | mass <n> [compare=<prefab>] | nest | clear - static render preview in front of the player";
            public override bool IsCheat => true;

            public override List<string> CommandOptionList() => new List<string> { "worm", "mass", "nest", "clear" };

            protected override void Execute(string[] args, Terminal context)
            {
                if (args.Length == 0)
                {
                    Print(context, "usage: mw_preview worm [anim] [count] | mass <n> [compare=<prefab>] | nest | clear");
                    return;
                }

                string kind = args[0].ToLowerInvariant();
                string anim = null;
                string compare = null;
                int count = 1;
                int n = 10;
                var positional = new List<string>();
                for (int i = 1; i < args.Length; i++)
                {
                    if (args[i].StartsWith("compare=", StringComparison.OrdinalIgnoreCase)) compare = args[i].Substring("compare=".Length);
                    else positional.Add(args[i]);
                }

                if (kind == "worm")
                {
                    // Either "worm <anim> <count>" or "worm <count>".
                    foreach (string p in positional)
                    {
                        if (int.TryParse(p, out int c)) count = c;
                        else anim = p;
                    }
                }
                else if (kind == "mass" && positional.Count > 0 && !int.TryParse(positional[0], out n))
                {
                    Print(context, "mw_preview mass: <n> must be a number");
                    return;
                }

                string error = Dev.Preview.DevPreview.Run(kind, n, anim, count, compare, 0f);
                Print(context, error != null
                    ? $"mw_preview: {error}"
                    : $"mw_preview {kind}: ok ({Dev.Preview.DevPreview.Instance.Warning ?? "no warning"})");
            }
        }

        private sealed class DumpCommand : MWCommand
        {
            public override string Name => "mw_dump";
            public override string Help => "[dev] [path] - write the vanilla reference dump (spec §16 phase 0)";

            protected override void Execute(string[] args, Terminal context)
            {
                // A path with spaces arrives split; an empty one means VanillaDump.DefaultOutputPath.
                string written = Dev.VanillaDump.Run(string.Join(" ", args));
                Print(context, Dev.VanillaDump.IsError(written) ? $"mw_dump: {written}" : $"mw_dump: written to {written}");
            }
        }
#endif
    }
}
