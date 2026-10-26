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
            CommandManager.Instance.AddConsoleCommand(new SpawnCommand());
            CommandManager.Instance.AddConsoleCommand(new KillCommand());
            CommandManager.Instance.AddConsoleCommand(new StateCommand());
            CommandManager.Instance.AddConsoleCommand(new TpMineCommand());
            CommandManager.Instance.AddConsoleCommand(new FreezeCommand());
            CommandManager.Instance.AddConsoleCommand(new GlossCommand());
            CommandManager.Instance.AddConsoleCommand(new MassCommand());
            CommandManager.Instance.AddConsoleCommand(new MassEngulfCommand());
            CommandManager.Instance.AddConsoleCommand(new GpuWormsCommand());
            CommandManager.Instance.AddConsoleCommand(new GpuMassCommand());
            CommandManager.Instance.AddConsoleCommand(new NestCommand());
            CommandManager.Instance.AddConsoleCommand(new SeedCommand());
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

        private sealed class SpawnCommand : MWCommand
        {
            public override string Name => "mw_spawn";
            public override string Help => "[dev] <n> [radius=4] [state=Idle] [force] - spawn n virtual worms on the floor around the player";
            public override bool IsCheat => true;

            public override List<string> CommandOptionList() => Enum.GetNames(typeof(Colony.WormState)).ToList();

            protected override void Execute(string[] args, Terminal context)
            {
                if (args.Length == 0 || !int.TryParse(args[0], out int n) || n < 1)
                {
                    Print(context, "usage: mw_spawn <n> [radius] [state] [force]");
                    return;
                }

                float radius = 4f;
                var state = Colony.WormState.Idle;
                bool force = false;
                for (int i = 1; i < args.Length; i++)
                {
                    if (string.Equals(args[i], "force", StringComparison.OrdinalIgnoreCase)) force = true;
                    else if (float.TryParse(args[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float r)) radius = Math.Max(0.5f, r);
                    else if (!Enum.TryParse(args[i], true, out state))
                    {
                        Print(context, $"mw_spawn: unknown state '{args[i]}' (known: {string.Join(", ", Enum.GetNames(typeof(Colony.WormState)))})");
                        return;
                    }
                }

                int created = Dev.ColonyDev.Spawn(n, radius, state, force, out Colony.WormColony colony, out string how);
                Print(context, $"mw_spawn: created {created}/{n} {state} worms in {how} colony {colony.ZdoId} (alive {colony.Store.AliveCount})");
            }
        }

        private sealed class NestCommand : MWCommand
        {
            public override string Name => "mw_nest";
            public override string Help => "[dev] [distance=2.5] - place a worm nest on the floor in front of the player, in the local colony (created if needed)";
            public override bool IsCheat => true;

            protected override void Execute(string[] args, Terminal context)
            {
                float distance = 2.5f;
                if (args.Length > 0 && !float.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out distance))
                {
                    Print(context, "usage: mw_nest [distance]");
                    return;
                }

                Colony.WormNest nest = Dev.NestDev.PlaceInFront(UnityEngine.Mathf.Max(0.5f, distance), out Colony.WormColony colony, out string how);
                Print(context, $"mw_nest: nest {nest.Index} at {nest.transform.position} (hp {nest.Health:0.#}) in {how} colony {colony.ZdoId} ({colony.NestsLoaded} nests, {colony.Store.AliveCount} worms)");
            }
        }

        private sealed class SeedCommand : MWCommand
        {
            public override string Name => "mw_seed";
            public override string Help => "[dev] [reset] - seeding state of the mine around the player; reset forgets the seeding (worms and nests removed, flag cleared) so the mine is seeded again";
            public override bool IsCheat => true;

            public override List<string> CommandOptionList() => new List<string> { "reset" };

            protected override void Execute(string[] args, Terminal context)
            {
                Player player = Player.m_localPlayer;
                Colony.WormColony colony = player != null ? Colony.ColonyRegistry.FindFor(player.transform.position) : null;
                if (colony == null || colony.Kind != Colony.WormColony.ColonyKind.Mine)
                {
                    Print(context, "mw_seed: the player is not inside a mine colony");
                    return;
                }
                if (args.Length > 0 && string.Equals(args[0], "reset", StringComparison.OrdinalIgnoreCase))
                {
                    int nests = Colony.MineSeeder.Reset(colony);
                    Print(context, $"mw_seed: seeding forgotten ({nests} nests removed); the mine is seeded again within a second or two");
                    return;
                }
                Print(context, $"mw_seed: seeded {colony.Seeded}, nests {colony.NestsLoaded}, worms {colony.Store.AliveCount}, seedings this session {Colony.MineSeeder.Runs}, planning {Colony.MineSeeder.Planning}, suppressed {Colony.MineSeeder.Suppressed}");
            }
        }

        private sealed class MassCommand : MWCommand
        {
            public override string Name => "mw_mass";
            public override string Help => "[dev] <n> [nocolony] [frozen] - spawn a writhing mass of n worms in front of the player, attached to the local colony (created if needed)";
            public override bool IsCheat => true;

            public override List<string> CommandOptionList() => new List<string> { "nocolony", "frozen" };

            protected override void Execute(string[] args, Terminal context)
            {
                if (args.Length == 0 || !int.TryParse(args[0], out int n) || n < 1)
                {
                    Print(context, "usage: mw_mass <n> [nocolony] [frozen]");
                    return;
                }

                bool withColony = true, frozen = false;
                for (int i = 1; i < args.Length; i++)
                {
                    if (string.Equals(args[i], "nocolony", StringComparison.OrdinalIgnoreCase)) withColony = false;
                    else if (string.Equals(args[i], "frozen", StringComparison.OrdinalIgnoreCase)) frozen = true;
                    else
                    {
                        Print(context, $"mw_mass: unknown option '{args[i]}' (nocolony, frozen)");
                        return;
                    }
                }

                Mass.WritheMassState mass = Dev.MassDev.SpawnInFront(n, withColony, out Colony.WormColony colony, out string how);
                mass.FreezeAI = frozen;
                Print(context, $"mw_mass: mass {mass.ZdoId} of {mass.N} worms (hp {mass.Health:0.#}, radius {mass.TargetRadius:0.00} m), {(colony != null ? how + " colony " + colony.ZdoId : "no colony")}");
            }
        }

        private sealed class GpuWormsCommand : MWCommand
        {
            public override string Name => "mw_gpuworms";
            public override string Help => "[dev] <count> - GPU worm spike: a field of count (0..16384) GPU-deformed worms around the player; 0 clears it";
            public override bool IsCheat => true;

            public override List<string> CommandOptionList() => new List<string> { "0", "1000", "5000", "10000" };

            protected override void Execute(string[] args, Terminal context)
            {
                if (args.Length == 0 || !int.TryParse(args[0], out int n) || n < 0)
                {
                    Print(context, "usage: mw_gpuworms <count>");
                    return;
                }

                Player player = Player.m_localPlayer;
                if (n > 0 && player == null) { Print(context, "mw_gpuworms: no local player"); return; }
                bool ok = Dev.GpuWorms.Spawn(n, player != null ? player.transform.position : UnityEngine.Vector3.zero, out string how);
                Print(context, $"mw_gpuworms: {(ok ? how : "FAILED " + how)}; main thread {Dev.GpuWorms.AvgMs:0.000} ms/frame");
            }
        }

        private sealed class GpuMassCommand : MWCommand
        {
            public override string Name => "mw_gpumass";
            public override string Help => "[dev] <n> [still|walk|engulf|engulfplayer] [distance] - a phantom GPU mass of n game units (10 worms each, 0 clears) in front of the player: still, walking <distance> m (default 6), wrapping a mannequin capsule, or wrapping the player";
            public override bool IsCheat => true;

            public override List<string> CommandOptionList() => new List<string> { "0", "50", "200" };

            protected override void Execute(string[] args, Terminal context)
            {
                if (args.Length == 0 || !int.TryParse(args[0], out int n) || n < 0)
                {
                    Print(context, "usage: mw_gpumass <n> [still|walk|engulf|engulfplayer] [distance]");
                    return;
                }
                Dev.GpuMassDev.GhostMode mode = Dev.GpuMassDev.GhostMode.Still;
                if (args.Length > 1 && !Enum.TryParse(args[1], true, out mode))
                {
                    Print(context, "mw_gpumass: unknown mode '" + args[1] + "' (still, walk, engulf, engulfplayer)");
                    return;
                }
                float distance = 6f;
                if (args.Length > 2) float.TryParse(args[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out distance);
                bool ok = Dev.GpuMassDev.Spawn(n, mode, distance, out string how);
                Print(context, $"mw_gpumass: {(ok ? how : "FAILED " + how)}; main thread {MistWorms.Render.Gpu.GpuWormHost.AvgMs:0.000} ms/frame");
            }
        }

        private sealed class MassEngulfCommand : MWCommand
        {
            public override string Name => "mw_mass_engulf";
            public override string Help => "[dev] <0..1|off> - force the engulf parameter of the closest writhing mass (render only; the local player is the target when the mass has none)";
            public override bool IsCheat => true;

            public override List<string> CommandOptionList() => new List<string> { "0", "0.5", "1", "off" };

            protected override void Execute(string[] args, Terminal context)
            {
                Player player = Player.m_localPlayer;
                if (args.Length == 0 || player == null)
                {
                    Print(context, "usage: mw_mass_engulf <0..1|off> (in game)");
                    return;
                }
                float value;
                if (string.Equals(args[0], "off", StringComparison.OrdinalIgnoreCase)) value = -1f;
                else if (!float.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
                {
                    Print(context, "usage: mw_mass_engulf <0..1|off>");
                    return;
                }
                Mass.WritheMassState mass = Dev.MassDev.Nearest(player.transform.position);
                Mass.WritheMassVisual visual = mass != null ? mass.GetComponent<Mass.WritheMassVisual>() : null;
                if (visual == null)
                {
                    Print(context, "mw_mass_engulf: no mass loaded");
                    return;
                }
                visual.ForcedEngulf = value < 0f ? -1f : Math.Min(1f, value);
                Print(context, $"mw_mass_engulf: mass {mass.ZdoId} engulf {(value < 0f ? "released to the state" : visual.ForcedEngulf.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))}");
            }
        }

        private sealed class KillCommand : MWCommand
        {
            public override string Name => "mw_kill";
            public override string Help => "[dev] - kill every virtual worm without drops, remove the test colonies and every mass";
            public override bool IsCheat => true;

            protected override void Execute(string[] args, Terminal context)
            {
                int worms = Dev.ColonyDev.KillAll(out int colonies, out int masses);
                Print(context, $"mw_kill: {worms} worms removed, {colonies} test colonies and {masses} masses destroyed");
            }
        }

        private sealed class StateCommand : MWCommand
        {
            public override string Name => "mw_state";
            public override string Help => "[dev] - print every loaded colony: owner, worms per state, box, simulation cost";

            protected override void Execute(string[] args, Terminal context)
            {
                List<string> lines = Dev.ColonyDev.Summary();
                if (lines.Count == 0) Print(context, "mw_state: no colony loaded");
                foreach (string line in lines) Print(context, "mw_state: " + line);
            }
        }

        private sealed class FreezeCommand : MWCommand
        {
            public override string Name => "mw_freeze";
            public override string Help => "[dev] [on|off] - stop (or resume) the simulation of every colony; worms stay where they are";
            public override bool IsCheat => true;

            public override List<string> CommandOptionList() => new List<string> { "on", "off" };

            protected override void Execute(string[] args, Terminal context)
            {
                if (args.Length > 0)
                {
                    string a = args[0].ToLowerInvariant();
                    if (a == "on" || a == "1" || a == "true") Colony.WormColony.Frozen = true;
                    else if (a == "off" || a == "0" || a == "false") Colony.WormColony.Frozen = false;
                    else
                    {
                        Print(context, "usage: mw_freeze [on|off]");
                        return;
                    }
                }
                else
                {
                    Colony.WormColony.Frozen = !Colony.WormColony.Frozen;
                }
                Print(context, $"mw_freeze: simulation {(Colony.WormColony.Frozen ? "frozen" : "running")}");
            }
        }

        private sealed class GlossCommand : MWCommand
        {
            public override string Name => "mw_gloss";
            public override string Help => "[dev] <map 0|1> [glossiness] - switch the worm lustre map and _Glossiness live";
            public override bool IsCheat => true;

            public override List<string> CommandOptionList() => new List<string> { "0", "1" };

            protected override void Execute(string[] args, Terminal context)
            {
                float g = 0f;
                bool hasG = args.Length > 1 && float.TryParse(args[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out g);
                if (args.Length == 0 || (args[0] != "0" && args[0] != "1") || (args.Length > 1 && !hasG))
                {
                    Print(context, "usage: mw_gloss <map 0|1> [glossiness]");
                    return;
                }
                Print(context, "mw_gloss: " + Render.MaterialFactory.DevSetGloss(args[0] == "1", hasG ? g : (float?)null));
            }
        }

        private sealed class TpMineCommand : MWCommand
        {
            public override string Name => "mw_tpmine";
            public override string Help => "[dev] - teleport into the closest infested mine (up to 90 s)";
            public override bool IsCheat => true;

            protected override void Execute(string[] args, Terminal context)
            {
                var result = new Dev.TpMineResult();
                Dev.DevBridge.Host.StartCoroutine(Run(result, context));
                Print(context, "mw_tpmine: started");
            }

            private static System.Collections.IEnumerator Run(Dev.TpMineResult result, Terminal context)
            {
                Exception failure = null;
                yield return Dev.SafeCoroutine.Run(Dev.DevWorld.TpMine(result, msg => Print(context, "mw_tpmine: " + msg)), e => failure = e);
                if (failure != null) Plugin.LogException("mw_tpmine", failure);
                Print(context, result.Ok
                    ? $"mw_tpmine: inside {result.Location}, {result.RoomCount} rooms, {result.Seconds:0.0} s"
                    : $"mw_tpmine: failed: {failure?.Message ?? result.Error}");
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
