#if MW_DEV
using System;
using MistWorms.Config;

namespace MistWorms.Dev
{
    /// <summary>
    /// Parses the dev command line (spec §15.2):
    /// <c>-mwdev autoload=&lt;world&gt;/&lt;character&gt; character=&lt;name&gt; bridge=&lt;port&gt; seed=&lt;seed&gt;</c> or
    /// <c>-mwdev join=host:port character=&lt;name&gt; password=&lt;pw&gt;</c>. A later <c>character=</c> overrides the one given by <c>autoload=</c>.
    /// Missing values fall back to the <c>Dev</c> config section.
    /// </summary>
    internal sealed class DevArgs
    {
        public bool Enabled;
        public string World;
        public string Character;
        public string Seed;
        public string JoinHost;
        public int JoinPort = 2456;
        public string JoinPassword;
        public int BridgePort;

        public bool AutoLoad => Enabled && !string.IsNullOrEmpty(World) && !string.IsNullOrEmpty(Character) && JoinHost == null;
        public bool AutoJoin => Enabled && JoinHost != null;

        public static DevArgs Parse(string[] argv)
        {
            var a = new DevArgs
            {
                World = MWConfig.Dev.AutoLoadWorld.Value,
                Character = MWConfig.Dev.AutoLoadCharacter.Value,
                Seed = MWConfig.Dev.WorldSeed.Value,
                BridgePort = MWConfig.Dev.BridgePort.Value,
            };

            int start = Array.IndexOf(argv, "-mwdev");
            if (start < 0) return a;
            a.Enabled = true;

            for (int i = start + 1; i < argv.Length && !argv[i].StartsWith("-", StringComparison.Ordinal); i++)
            {
                int eq = argv[i].IndexOf('=');
                if (eq <= 0) continue;
                string key = argv[i].Substring(0, eq).ToLowerInvariant();
                string value = argv[i].Substring(eq + 1);
                switch (key)
                {
                    case "autoload":
                        int slash = value.IndexOf('/');
                        if (slash > 0) { a.World = value.Substring(0, slash); a.Character = value.Substring(slash + 1); }
                        else a.World = value;
                        break;
                    case "character":
                        a.Character = value;
                        break;
                    case "bridge":
                        if (int.TryParse(value, out int port)) a.BridgePort = port;
                        break;
                    case "seed":
                        a.Seed = value;
                        break;
                    case "join":
                        int colon = value.LastIndexOf(':');
                        if (colon > 0 && int.TryParse(value.Substring(colon + 1), out int jp)) { a.JoinHost = value.Substring(0, colon); a.JoinPort = jp; }
                        else a.JoinHost = value;
                        break;
                    case "password":
                        a.JoinPassword = value;
                        break;
                }
            }
            return a;
        }
    }
}
#endif
