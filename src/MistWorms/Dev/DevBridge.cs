#if MW_DEV
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using HarmonyLib;
using MistWorms.Dev.Scenarios;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MistWorms.Dev
{
    /// <summary>
    /// Local HTTP/JSON control bridge for automated testing (spec §15.3). Listens on 127.0.0.1 only;
    /// an accept thread parses requests and queues them, the Unity main thread runs them one at a time.
    /// </summary>
    internal static class DevBridge
    {
        private const int MaxQueuedRequests = 64;

        // Routes the spec defines whose behaviour depends on systems built in later phases (§16).
        private static readonly Dictionary<string, int> PlannedRoutes = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "/mass", 4 },
            { "/nest", 7 },
            { "/perf", 8 },
        };

        private static readonly ConcurrentQueue<PendingRequest> s_queue = new ConcurrentQueue<PendingRequest>();
        private static HttpListener s_listener;
        private static Thread s_thread;
        private static volatile bool s_running;
        private static DevBridgeHost s_host;

        public static int Port { get; private set; }
        public static bool IsRunning => s_running;

        /// <summary>Hidden DontDestroyOnLoad behaviour that drains the queue; also hosts scenario coroutines.</summary>
        internal static DevBridgeHost Host
        {
            get
            {
                if (s_host == null)
                {
                    var go = new GameObject("MistWorms.DevBridge") { hideFlags = HideFlags.HideAndDontSave };
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    s_host = go.AddComponent<DevBridgeHost>();
                }
                return s_host;
            }
        }

        public static void Start(int port)
        {
            if (s_running) return;
            _ = Host; // created on the main thread before any request can arrive

            try
            {
                var listener = new HttpListener();
                // Literal loopback address: the bridge must never be reachable from another machine.
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                listener.Start();
                s_listener = listener;
                Port = port;
                s_running = true;
                s_thread = new Thread(AcceptLoop) { IsBackground = true, Name = "MistWorms.DevBridge" };
                s_thread.Start();
                Plugin.Log.LogInfo($"[DevBridge] listening on http://127.0.0.1:{port}/");
            }
            catch (Exception e)
            {
                s_running = false;
                Plugin.Log.LogError($"[DevBridge] could not listen on 127.0.0.1:{port}: {e.Message}");
            }
        }

        public static void Stop()
        {
            if (!s_running) return;
            s_running = false;
            try { s_listener?.Stop(); s_listener?.Close(); }
            catch (Exception) { /* listener already torn down */ }
            s_listener = null;
            if (s_thread != null && s_thread.IsAlive) s_thread.Join(500);
            s_thread = null;
            while (s_queue.TryDequeue(out PendingRequest p))
                WriteResponse(p.Context, 503, MiniJson.Serialize(Error("bridge stopped")));
            Plugin.Log.LogInfo("[DevBridge] stopped");
        }

        internal static bool TryDequeue(out PendingRequest request) => s_queue.TryDequeue(out request);

        // ---------------------------------------------------------------- accept thread

        private static void AcceptLoop()
        {
            while (s_running)
            {
                HttpListenerContext ctx;
                try { ctx = s_listener.GetContext(); }
                catch (Exception)
                {
                    if (!s_running) return; // Stop() unblocks GetContext with an exception
                    continue;
                }

                try
                {
                    IPEndPoint remote = ctx.Request.RemoteEndPoint;
                    if (remote == null || !IPAddress.IsLoopback(remote.Address))
                    {
                        WriteResponse(ctx, 403, MiniJson.Serialize(Error("loopback only")));
                        continue;
                    }
                    if (s_queue.Count >= MaxQueuedRequests)
                    {
                        WriteResponse(ctx, 503, MiniJson.Serialize(Error("request queue full")));
                        continue;
                    }
                    s_queue.Enqueue(new PendingRequest(ctx, ReadRequest(ctx)));
                }
                catch (BridgeException be)
                {
                    WriteResponse(ctx, be.Status, MiniJson.Serialize(Error(be.Message)));
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"[DevBridge] failed to read request: {e}");
                    WriteResponse(ctx, 500, MiniJson.Serialize(Error(e.Message)));
                }
            }
        }

        private static BridgeRequest ReadRequest(HttpListenerContext ctx)
        {
            HttpListenerRequest r = ctx.Request;
            var req = new BridgeRequest
            {
                Method = r.HttpMethod.ToUpperInvariant(),
                Path = NormalizePath(r.Url.AbsolutePath),
                Query = r.QueryString,
                Body = new Dictionary<string, object>(StringComparer.Ordinal),
            };

            if (r.HasEntityBody)
            {
                string text;
                using (var reader = new StreamReader(r.InputStream, r.ContentEncoding ?? Encoding.UTF8))
                    text = reader.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    object parsed;
                    try { parsed = MiniJson.Parse(text); }
                    catch (FormatException fe) { throw new BridgeException(400, fe.Message); }
                    req.Body = parsed as Dictionary<string, object> ?? throw new BridgeException(400, "request body must be a JSON object");
                }
            }
            return req;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "/";
            path = Uri.UnescapeDataString(path);
            if (path.Length > 1 && path.EndsWith("/", StringComparison.Ordinal)) path = path.TrimEnd('/');
            return path;
        }

        internal static void Send(HttpListenerContext ctx, BridgeReply reply, bool synchronous)
        {
            string json;
            try { json = MiniJson.Serialize(reply.Body ?? new Dictionary<string, object>()); }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[DevBridge] response serialization failed: {e}");
                reply.Status = 500;
                json = MiniJson.Serialize(Error("response serialization failed: " + e.Message));
            }

            // Socket writes stay off the main thread unless the process is about to exit.
            if (synchronous) WriteResponse(ctx, reply.Status, json);
            else ThreadPool.QueueUserWorkItem(_ => WriteResponse(ctx, reply.Status, json));
        }

        private static void WriteResponse(HttpListenerContext ctx, int status, string json)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                HttpListenerResponse res = ctx.Response;
                res.StatusCode = status;
                res.ContentType = "application/json; charset=utf-8";
                res.ContentLength64 = bytes.Length;
                res.OutputStream.Write(bytes, 0, bytes.Length);
                res.OutputStream.Close();
                res.Close();
            }
            catch (Exception)
            {
                // The client went away; nothing useful to do.
            }
        }

        internal static Dictionary<string, object> Error(string message) =>
            new Dictionary<string, object> { { "error", message } };

        // ---------------------------------------------------------------- dispatch (main thread)

        /// <summary>Returns a coroutine for asynchronous routes, or null when the reply is already filled.</summary>
        internal static IEnumerator Route(BridgeRequest req, BridgeReply reply)
        {
            string path = req.Path;

            if (path.StartsWith("/scenario/", StringComparison.Ordinal))
            {
                req.RequireMethod("GET");
                string id = path.Substring("/scenario/".Length);
                ScenarioResult result = ScenarioRunner.Get(id) ?? throw new BridgeException(404, $"unknown scenario run '{id}'");
                reply.Body = result.ToJson();
                return null;
            }

            switch (path)
            {
                case "/status":
                    req.RequireMethod("GET");
                    reply.Body = Status();
                    return null;
                case "/console":
                    req.RequireMethod("POST");
                    return ConsoleRoute(req, reply);
                case "/tp":
                    req.RequireMethod("POST");
                    return TeleportRoute(req, reply);
                case "/player":
                    req.RequireMethod("POST");
                    PlayerRoute(req, reply);
                    return null;
                case "/spawn":
                    req.RequireMethod("POST");
                    SpawnRoute(req, reply);
                    return null;
                case "/state":
                    req.RequireMethod("GET");
                    StateRoute(req, reply);
                    return null;
                case "/hit":
                    req.RequireMethod("POST");
                    HitRoute(req, reply);
                    return null;
                case "/attack":
                    req.RequireMethod("POST");
                    AttackRoute(req, reply);
                    return null;
                case "/tp-mine":
                    req.RequireMethod("POST");
                    return TpMineRoute(reply);
                case "/time":
                    req.RequireMethod("POST");
                    TimeRoute(req, reply);
                    return null;
                case "/wait":
                    req.RequireMethod("POST");
                    return WaitRoute(req, reply);
                case "/screenshot":
                    req.RequireMethod("GET", "POST");
                    return ScreenshotRoute(req, reply);
                case "/scenario":
                    req.RequireMethod("POST");
                    ScenarioRoute(req, reply);
                    return null;
                case "/scenarios":
                    req.RequireMethod("GET");
                    reply.Body = new Dictionary<string, object>
                    {
                        { "names", new List<string>(ScenarioRunner.Names) },
                        { "running", ScenarioRunner.CurrentId },
                    };
                    return null;
                case "/dump":
                    req.RequireMethod("POST");
                    DumpRoute(req, reply);
                    return null;
                case "/preview":
                    req.RequireMethod("POST");
                    PreviewRoute(req, reply);
                    return null;
                case "/camera":
                    req.RequireMethod("POST");
                    CameraRoute(req, reply);
                    return null;
                case "/quit":
                    req.RequireMethod("POST");
                    reply.Body = new Dictionary<string, object> { { "ok", true }, { "quitting", true } };
                    reply.QuitAfter = true;
                    return null;
            }

            if (PlannedRoutes.TryGetValue(path, out int phase))
                throw new BridgeException(501, $"not implemented until phase {phase}");

            throw new BridgeException(404, $"unknown route {req.Method} {path}");
        }

        internal static void Fail(BridgeRequest req, BridgeReply reply, Exception e)
        {
            if (e is BridgeException be)
            {
                reply.Status = be.Status;
                reply.Body = Error(be.Message);
                return;
            }
            Plugin.Log.LogError($"[DevBridge] {req.Method} {req.Path} failed: {e}");
            reply.Status = 500;
            reply.Body = new Dictionary<string, object> { { "error", e.Message }, { "type", e.GetType().FullName } };
        }

        // ---------------------------------------------------------------- routes

        private static Dictionary<string, object> Status()
        {
            ZNet znet = ZNet.instance;
            Player player = Player.m_localPlayer;
            bool isServer = znet != null && znet.IsServer();

            return new Dictionary<string, object>
            {
                { "scene", SceneManager.GetActiveScene().name },
                { "inGame", Game.instance != null },
                { "world", znet != null ? znet.GetWorldName() : null },
                { "player", player != null ? PlayerJson(player, includeInventory: false) : null },
                { "isServer", isServer },
                { "isDedicated", znet != null && znet.IsDedicated() },
                // Colony ownership arrives in phase 3; until then the host owns everything.
                { "owner", isServer },
                { "peers", znet != null ? znet.GetPeerConnections() : 0 },
                { "colonies", Colony.ColonyRegistry.All.Count },
                { "fps", Host.AverageFps },
                { "gameTime", znet != null ? (object)znet.GetTimeSeconds() : null },
                { "day", EnvMan.instance != null ? (object)EnvMan.instance.GetDay() : null },
                { "dayFraction", EnvMan.instance != null ? (object)EnvMan.instance.GetDayFraction() : null },
                { "timeScale", Time.timeScale },
                { "realtime", Time.realtimeSinceStartup },
                { "pluginVersion", Plugin.ModVersion },
                { "devBuild", Plugin.IsDevBuild },
                { "devcommands", Terminal.m_cheat },
                { "scenario", ScenarioRunner.CurrentId },
                { "ready", IsReady(player) },
            };
        }

        /// <summary>Local player spawned, not teleporting or in the intro, and the zone around them is loaded.</summary>
        internal static bool IsReady(Player player)
        {
            if (player == null || player.IsTeleporting() || player.InIntro()) return false;
            Vector3 pos = player.transform.position;
            return ZoneSystem.instance != null && ZoneSystem.instance.IsZoneLoaded(pos)
                && ZNetScene.instance != null && ZNetScene.instance.IsAreaReady(pos);
        }

        internal static Dictionary<string, object> PlayerJson(Player p, bool includeInventory)
        {
            var d = new Dictionary<string, object>
            {
                { "name", p.GetPlayerName() },
                { "x", p.transform.position.x },
                { "y", p.transform.position.y },
                { "z", p.transform.position.z },
                { "hp", p.GetHealth() },
                { "maxHp", p.GetMaxHealth() },
                { "stamina", p.GetStamina() },
                { "maxStamina", p.GetMaxStamina() },
                { "god", p.InGodMode() },
                { "ghost", p.InGhostMode() },
                { "dead", p.IsDead() },
                { "teleporting", p.IsTeleporting() },
            };
            if (includeInventory)
            {
                var items = new List<object>();
                foreach (ItemDrop.ItemData item in p.GetInventory().GetAllItems())
                {
                    items.Add(new Dictionary<string, object>
                    {
                        { "prefab", item.m_dropPrefab != null ? item.m_dropPrefab.name : null },
                        { "name", item.m_shared.m_name },
                        { "quality", item.m_quality },
                        { "stack", item.m_stack },
                        { "durability", item.m_durability },
                        { "maxDurability", item.GetMaxDurability() },
                        { "equipped", item.m_equipped },
                    });
                }
                d["inventory"] = items;
            }
            return d;
        }

        private static Player RequirePlayer() =>
            Player.m_localPlayer ?? throw new BridgeException(409, "no local player (not in game)");

        private static IEnumerator ConsoleRoute(BridgeRequest req, BridgeReply reply)
        {
            string cmd = req.String("cmd");
            if (string.IsNullOrWhiteSpace(cmd)) throw new BridgeException(400, "missing 'cmd'");
            // Some commands print from later frames (coroutines, RPC replies); callers may ask to keep listening.
            float extra = Mathf.Clamp(MiniJson.GetFloat(req.Body, "waitSeconds", 0f), 0f, 30f);

            List<string> sink = DevConsole.BeginCapture();
            try { DevConsole.Execute(cmd); }
            catch { DevConsole.EndCapture(sink); throw; }

            if (extra > 0f) yield return new WaitForSecondsRealtime(extra);
            DevConsole.EndCapture(sink);
            reply.Body = new Dictionary<string, object> { { "cmd", cmd }, { "lines", sink } };
        }

        private static IEnumerator TeleportRoute(BridgeRequest req, BridgeReply reply)
        {
            Player player = RequirePlayer();
            float x = req.Float("x");
            float z = req.Float("z");
            float y = MiniJson.Has(req.Body, "y")
                ? req.Float("y")
                : (WorldGenerator.instance != null ? WorldGenerator.instance.GetHeight(x, z) + 0.5f : player.transform.position.y);
            var target = new Vector3(x, y, z);
            bool distant = MiniJson.GetBool(req.Body, "distant", Vector3.Distance(player.transform.position, target) > 64f);

            // Player.TeleportTo refuses a second teleport within 2 s; scripted runs chain them.
            player.m_teleportCooldown = 10f;
            if (!player.TeleportTo(target, player.transform.rotation, distant))
                throw new BridgeException(409, "teleport refused (already teleporting)");

            float deadline = Time.realtimeSinceStartup + 60f;
            while (player != null && player.IsTeleporting() && Time.realtimeSinceStartup < deadline)
                yield return null;

            reply.Body = new Dictionary<string, object>
            {
                { "ok", player != null && !player.IsTeleporting() },
                { "target", target },
                { "position", player != null ? (object)player.transform.position : null },
                { "ready", IsReady(player) },
            };
        }

        /// <summary>{n, radius = 4, state = Idle, force}: virtual worms on the floor around the player (see ColonyDev.Spawn).</summary>
        private static void SpawnRoute(BridgeRequest req, BridgeReply reply)
        {
            RequirePlayer();
            int n = MiniJson.GetInt(req.Body, "n", 1);
            if (n < 1) throw new BridgeException(400, "'n' must be at least 1");
            float radius = Mathf.Max(0.5f, MiniJson.GetFloat(req.Body, "radius", 4f));
            string stateName = MiniJson.GetString(req.Body, "state") ?? "Idle";
            if (!Enum.TryParse(stateName, true, out Colony.WormState state))
                throw new BridgeException(400, $"unknown state '{stateName}' (known: {string.Join(", ", Enum.GetNames(typeof(Colony.WormState)))})");
            bool force = MiniJson.GetBool(req.Body, "force");

            int created;
            Colony.WormColony colony;
            string how;
            try { created = ColonyDev.Spawn(n, radius, state, force, out colony, out how); }
            catch (InvalidOperationException e) { throw new BridgeException(409, e.Message); }

            reply.Body = new Dictionary<string, object>
            {
                { "requested", n },
                { "created", created },
                { "state", state.ToString() },
                { "colony", colony.ZdoId.ToString() },
                { "colonyKind", colony.Kind.ToString() },
                { "colonySource", how },
                { "alive", colony.Store.AliveCount },
            };
        }

        /// <summary>GET /state[?worms=1]: colonies (and optionally every worm) plus the local player's health and gear.</summary>
        private static void StateRoute(BridgeRequest req, BridgeReply reply)
        {
            string worms = req.Query?["worms"];
            bool includeWorms = worms == "1" || string.Equals(worms, "true", StringComparison.OrdinalIgnoreCase);

            var colonies = new List<object>();
            foreach (Colony.WormColony c in Colony.ColonyRegistry.All)
                if (c != null) colonies.Add(ColonyDev.ColonyJson(c, includeWorms));

            object playerJson = null;
            Player player = Player.m_localPlayer;
            if (player != null)
            {
                var inventory = new List<object>();
                foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
                {
                    inventory.Add(new Dictionary<string, object>
                    {
                        { "prefab", item.m_dropPrefab != null ? item.m_dropPrefab.name : null },
                        { "durability", item.m_durability },
                        { "max", item.GetMaxDurability() },
                        { "equipped", item.m_equipped },
                    });
                }
                playerJson = new Dictionary<string, object>
                {
                    { "pos", player.transform.position },
                    { "hp", player.GetHealth() },
                    { "maxHp", player.GetMaxHealth() },
                    { "god", player.InGodMode() },
                    { "ghost", player.InGhostMode() },
                    { "inventory", inventory },
                };
            }

            reply.Body = new Dictionary<string, object>
            {
                { "colonies", colonies },
                { "frozen", Colony.WormColony.Frozen },
                { "proxyLayer", Colony.ProxyLayer.LayerName },
                { "bites", BitesJson() },
                { "drops", DropsJson() },
                { "player", playerJson },
            };
        }

        private static Dictionary<string, object> BitesJson() => new Dictionary<string, object>
        {
            { "bites", Bites.WormBiteContext.Bites },
            { "durabilityEaten", Bites.WormBiteContext.DurabilityEaten },
            { "healthBites", Bites.WormBiteContext.HealthBites },
            { "healthDamage", Bites.WormBiteContext.HealthDamage },
        };

        private static Dictionary<string, object> DropsJson() => new Dictionary<string, object>
        {
            { "rolls", Drops.WormDrops.Rolls },
            { "bait", Drops.WormDrops.BaitRolled },
            { "entrails", Drops.WormDrops.Entrails },
            { "bloodbag", Drops.WormDrops.Bloodbag },
            { "itemsSpawned", Drops.WormDrops.ItemsSpawned },
            { "poolItems", Drops.WormDrops.PoolItems },
        };

        /// <summary>
        /// POST /hit {worm, colony?, damage:{slash,blunt,pierce,fire,...}, point?} injects a vanilla HitData (attacker = the local player) on a
        /// worm, through its hit proxy when it has one; {targetZdoid, damage} hits any vanilla object by its ZDOID (plain Damage).
        /// </summary>
        private static void HitRoute(BridgeRequest req, BridgeReply reply)
        {
            Player player = RequirePlayer();
            Dictionary<string, object> dmg = MiniJson.GetObject(req.Body, "damage") ?? throw new BridgeException(400, "missing 'damage' object");

            var hit = new HitData();
            hit.m_damage.m_damage = MiniJson.GetFloat(dmg, "damage");
            hit.m_damage.m_blunt = MiniJson.GetFloat(dmg, "blunt");
            hit.m_damage.m_slash = MiniJson.GetFloat(dmg, "slash");
            hit.m_damage.m_pierce = MiniJson.GetFloat(dmg, "pierce");
            hit.m_damage.m_chop = MiniJson.GetFloat(dmg, "chop");
            hit.m_damage.m_pickaxe = MiniJson.GetFloat(dmg, "pickaxe");
            hit.m_damage.m_fire = MiniJson.GetFloat(dmg, "fire");
            hit.m_damage.m_frost = MiniJson.GetFloat(dmg, "frost");
            hit.m_damage.m_lightning = MiniJson.GetFloat(dmg, "lightning");
            hit.m_damage.m_poison = MiniJson.GetFloat(dmg, "poison");
            hit.m_damage.m_spirit = MiniJson.GetFloat(dmg, "spirit");
            hit.SetAttacker(player);
            hit.m_hitType = HitData.HitType.PlayerHit;
            List<object> point = MiniJson.GetList(req.Body, "point");
            if (point != null && point.Count == 3) hit.m_point = ToVector(point);
            hit.m_dir = player.transform.forward;

            string targetZdoid = MiniJson.GetString(req.Body, "targetZdoid");
            if (!string.IsNullOrEmpty(targetZdoid))
            {
                string[] parts = targetZdoid.Split(':');
                if (parts.Length != 2 || !long.TryParse(parts[0], out long user) || !uint.TryParse(parts[1], out uint id))
                    throw new BridgeException(400, "'targetZdoid' must look like user:id");
                GameObject go = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(new ZDOID(user, id)) : null;
                IDestructible target = go != null ? go.GetComponent<IDestructible>() : null;
                if (target == null) throw new BridgeException(404, $"no destructible instance for {targetZdoid}");
                if (hit.m_point == Vector3.zero) hit.m_point = go.transform.position;
                target.Damage(hit);
                reply.Body = new Dictionary<string, object> { { "ok", true }, { "target", targetZdoid } };
                return;
            }

            if (!MiniJson.Has(req.Body, "worm")) throw new BridgeException(400, "missing 'worm' (slot index) or 'targetZdoid'");
            int index = MiniJson.GetInt(req.Body, "worm");
            Colony.WormColony colony = ColonyDev.ColonyOfWorm(MiniJson.GetString(req.Body, "colony"), index)
                ?? throw new BridgeException(404, $"no living worm {index} in a loaded colony");
            if (hit.m_point == Vector3.zero) hit.m_point = colony.Store.pos[index] + Vector3.up * 0.1f;

            bool viaProxy = ColonyDev.HitWorm(colony, index, hit);
            reply.Body = new Dictionary<string, object>
            {
                { "colony", colony.ZdoId.ToString() },
                { "worm", index },
                { "viaProxy", viaProxy },
                { "hp", colony.Store.hp[index] },
                { "dead", !colony.Store.alive[index] },
                { "hitsReceived", colony.HitsReceived },
                { "lastHitDamage", colony.LastHitDamage },
            };
        }

        /// <summary>
        /// POST /attack {weapon, lookAt?} gives and equips the weapon, turns the player toward lookAt ([x,y,z], default straight ahead) and
        /// starts a real vanilla primary attack (Humanoid.StartAttack). The swing's hit comes later, from the animation.
        /// </summary>
        private static void AttackRoute(BridgeRequest req, BridgeReply reply)
        {
            Player player = RequirePlayer();
            string weapon = req.String("weapon");
            if (string.IsNullOrWhiteSpace(weapon)) throw new BridgeException(400, "missing 'weapon'");

            List<object> look = MiniJson.GetList(req.Body, "lookAt");
            Vector3? lookAt = look != null && look.Count == 3 ? ToVector(look) : (Vector3?)null;

            bool started = ColonyDev.StartAttack(player, weapon, lookAt, out ItemDrop.ItemData item, out string error);
            if (error != null) throw new BridgeException(422, error);
            reply.Body = new Dictionary<string, object>
            {
                { "weapon", weapon },
                { "started", started },
                { "durability", item.m_durability },
            };
        }

        /// <summary>Enters the closest infested mine (spec §15.3). The reply is sent when the dungeon is loaded or the 90 s budget ends.</summary>
        private static IEnumerator TpMineRoute(BridgeReply reply)
        {
            RequirePlayer();
            var result = new TpMineResult();
            yield return DevWorld.TpMine(result, msg => Plugin.Log.LogInfo($"[tp-mine] {msg}"));
            reply.Status = result.Ok ? 200 : 504;
            reply.Body = result.ToJson();
        }

        private static void PlayerRoute(BridgeRequest req, BridgeReply reply)
        {
            Player player = RequirePlayer();
            Dictionary<string, object> b = req.Body;
            Inventory inv = player.GetInventory();
            var errors = new List<string>();

            if (MiniJson.GetBool(b, "clear"))
            {
                player.UnequipAllItems();
                inv.RemoveAll();
            }
            if (MiniJson.Has(b, "god")) player.SetGodMode(MiniJson.GetBool(b, "god"));
            if (MiniJson.Has(b, "ghost")) player.SetGhostMode(MiniJson.GetBool(b, "ghost"));
            // Floor at 1 so a setup call never kills the player as a side effect.
            if (MiniJson.Has(b, "hp")) player.SetHealth(Mathf.Clamp(MiniJson.GetFloat(b, "hp"), 1f, player.GetMaxHealth()));
            if (MiniJson.Has(b, "stamina")) player.m_stamina = Mathf.Clamp(MiniJson.GetFloat(b, "stamina"), 0f, player.GetMaxStamina());

            List<object> inventory = MiniJson.GetList(b, "inventory");
            if (inventory != null)
            {
                foreach (object entry in inventory)
                {
                    var spec = entry as Dictionary<string, object>;
                    string prefab = spec != null ? MiniJson.GetString(spec, "prefab") : entry as string;
                    if (string.IsNullOrEmpty(prefab)) { errors.Add("inventory entry without 'prefab'"); continue; }
                    int quality = spec != null ? Math.Max(1, MiniJson.GetInt(spec, "quality", 1)) : 1;
                    int stack = spec != null ? Math.Max(1, MiniJson.GetInt(spec, "stack", 1)) : 1;

                    ItemDrop.ItemData item = inv.AddItem(prefab, stack, quality, 0, player.GetPlayerID(), player.GetPlayerName(), true);
                    if (item == null) { errors.Add($"could not add '{prefab}' (unknown prefab or inventory full)"); continue; }

                    // 'durability' is absolute; 'durabilityPct' is a 0..1 fraction of the max.
                    float max = item.GetMaxDurability();
                    if (MiniJson.Has(spec, "durability")) item.m_durability = Mathf.Clamp(MiniJson.GetFloat(spec, "durability"), 0f, max);
                    else if (MiniJson.Has(spec, "durabilityPct")) item.m_durability = Mathf.Clamp01(MiniJson.GetFloat(spec, "durabilityPct")) * max;
                }
            }

            List<object> equip = MiniJson.GetList(b, "equip");
            if (equip != null)
            {
                foreach (object entry in equip)
                {
                    string prefab = entry as string;
                    if (string.IsNullOrEmpty(prefab)) { errors.Add("equip entries must be prefab names"); continue; }
                    ItemDrop.ItemData item = FindByPrefab(inv, prefab)
                        ?? inv.AddItem(prefab, 1, 1, 0, player.GetPlayerID(), player.GetPlayerName(), true);
                    if (item == null) { errors.Add($"could not add '{prefab}' to equip it"); continue; }
                    if (!player.IsItemEquiped(item) && !player.EquipItem(item)) errors.Add($"could not equip '{prefab}'");
                }
            }

            reply.Status = errors.Count == 0 ? 200 : 422;
            reply.Body = new Dictionary<string, object>
            {
                { "ok", errors.Count == 0 },
                { "errors", errors },
                { "player", PlayerJson(player, includeInventory: true) },
            };
        }

        private static ItemDrop.ItemData FindByPrefab(Inventory inv, string prefab)
        {
            foreach (ItemDrop.ItemData item in inv.GetAllItems())
                if (item.m_dropPrefab != null && item.m_dropPrefab.name == prefab) return item;
            return null;
        }

        private static void TimeRoute(BridgeRequest req, BridgeReply reply)
        {
            float requested = req.Float("scale");
            float scale = Mathf.Clamp(requested, 0f, 4f);
            // Game.UpdatePause rewrites Time.timeScale every frame from Game.m_timeScale, so set the source.
            if (Game.instance != null) Game.m_timeScale = scale;
            Time.timeScale = scale;
            reply.Body = new Dictionary<string, object>
            {
                { "requested", requested },
                { "scale", scale },
                // Vanilla forces 1 while peers are connected.
                { "forcedByPeers", ZNet.instance != null && ZNet.instance.GetPeerConnections() > 0 },
            };
        }

        private static IEnumerator WaitRoute(BridgeRequest req, BridgeReply reply)
        {
            float seconds = Mathf.Clamp(req.Float("seconds"), 0f, 600f);
            bool gameTime = MiniJson.GetBool(req.Body, "game");
            float start = Time.realtimeSinceStartup;
            if (gameTime)
            {
                float waited = 0f;
                while (waited < seconds) { yield return null; waited += Time.deltaTime; }
            }
            else
            {
                yield return new WaitForSecondsRealtime(seconds);
            }
            reply.Body = new Dictionary<string, object>
            {
                { "seconds", seconds },
                { "game", gameTime },
                { "realSeconds", Time.realtimeSinceStartup - start },
            };
        }

        private static IEnumerator ScreenshotRoute(BridgeRequest req, BridgeReply reply)
        {
            string name = req.Query?["name"] ?? MiniJson.GetString(req.Body, "name");
            if (string.IsNullOrWhiteSpace(name)) name = "shot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

            string path = null, error = null;
            yield return DevShots.Capture(name, (p, e) => { path = p; error = e; });
            if (error != null) throw new BridgeException(504, error);
            reply.Body = new Dictionary<string, object> { { "path", path } };
        }

        private static void ScenarioRoute(BridgeRequest req, BridgeReply reply)
        {
            string name = req.String("name");
            if (string.IsNullOrWhiteSpace(name)) throw new BridgeException(400, "missing 'name'");

            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, object> raw = MiniJson.GetObject(req.Body, "params");
            if (raw != null)
                foreach (KeyValuePair<string, object> kv in raw)
                    parameters[kv.Key] = kv.Value is string s ? s : kv.Value is bool bv ? (bv ? "true" : "false") : MiniJson.Serialize(kv.Value);

            string id;
            try { id = ScenarioRunner.Start(name, parameters); }
            catch (KeyNotFoundException e) { throw new BridgeException(404, e.Message); }
            catch (InvalidOperationException e) { throw new BridgeException(409, e.Message); }

            reply.Body = new Dictionary<string, object> { { "id", id }, { "name", ScenarioRunner.Get(id)?.Name ?? name }, { "status", "running" } };
        }

        private static void PreviewRoute(BridgeRequest req, BridgeReply reply)
        {
            string kind = req.String("kind");
            if (string.IsNullOrWhiteSpace(kind)) throw new BridgeException(400, "missing 'kind' (worm, mass, nest, clear)");

            string error = Preview.DevPreview.Run(kind, MiniJson.GetInt(req.Body, "n", 10), req.String("anim"),
                MiniJson.GetInt(req.Body, "count", 1), req.String("compare"), MiniJson.GetFloat(req.Body, "distance", 0f));
            if (error != null) throw new BridgeException(409, error);

            var body = Preview.DevPreview.Instance.Describe();
            body["stats"] = Preview.DevPreview.Describe(Render.WormRenderer.Last);
            reply.Body = body;
        }

        /// <summary>mode "free": pos + lookAt (or neither to freeze the current pose); mode "follow": back to vanilla.</summary>
        private static void CameraRoute(BridgeRequest req, BridgeReply reply)
        {
            string mode = (req.String("mode") ?? "free").ToLowerInvariant();
            if (mode == "follow")
            {
                Preview.DevCamera.Follow();
            }
            else if (mode == "free")
            {
                List<object> pos = MiniJson.GetList(req.Body, "pos");
                List<object> look = MiniJson.GetList(req.Body, "lookAt");
                if (pos == null && look == null) Preview.DevCamera.FreezeHere();
                else if (pos == null || look == null || pos.Count != 3 || look.Count != 3)
                    throw new BridgeException(400, "'pos' and 'lookAt' must both be [x,y,z]");
                else Preview.DevCamera.Free(ToVector(pos), ToVector(look));
            }
            else
            {
                throw new BridgeException(400, $"unknown camera mode '{mode}' (free, follow)");
            }

            reply.Body = new Dictionary<string, object>
            {
                { "mode", Preview.DevCamera.Active ? "free" : "follow" },
                { "position", Preview.DevCamera.Position },
            };
        }

        private static Vector3 ToVector(List<object> v)
        {
            float F(object o)
            {
                try { return Convert.ToSingle(o, CultureInfo.InvariantCulture); }
                catch (Exception) { throw new BridgeException(400, "vector components must be numbers"); }
            }
            return new Vector3(F(v[0]), F(v[1]), F(v[2]));
        }

        private static void DumpRoute(BridgeRequest req, BridgeReply reply)
        {
            string written = VanillaDump.Run(MiniJson.GetString(req.Body, "path"));
            if (VanillaDump.IsError(written)) throw new BridgeException(409, written);
            reply.Body = new Dictionary<string, object> { { "path", written } };
        }
    }

    internal sealed class BridgeException : Exception
    {
        public readonly int Status;
        public BridgeException(int status, string message) : base(message) { Status = status; }
    }

    internal sealed class BridgeRequest
    {
        public string Method;
        public string Path;
        public NameValueCollection Query;
        public Dictionary<string, object> Body;

        public void RequireMethod(params string[] allowed)
        {
            foreach (string m in allowed)
                if (m == Method) return;
            throw new BridgeException(405, $"{Method} not allowed on {Path} (use {string.Join("/", allowed)})");
        }

        public string String(string key) => MiniJson.GetString(Body, key);

        public float Float(string key)
        {
            if (!MiniJson.Has(Body, key)) throw new BridgeException(400, $"missing '{key}'");
            double v = MiniJson.GetDouble(Body, key, double.NaN);
            if (double.IsNaN(v)) throw new BridgeException(400, $"'{key}' must be a number");
            return (float)v;
        }
    }

    internal sealed class BridgeReply
    {
        public int Status = 200;
        public object Body;
        public bool QuitAfter;
    }

    internal readonly struct PendingRequest
    {
        public readonly HttpListenerContext Context;
        public readonly BridgeRequest Request;
        public PendingRequest(HttpListenerContext context, BridgeRequest request) { Context = context; Request = request; }
    }

    /// <summary>Main-thread side of the bridge: processes queued requests strictly one after another.</summary>
    internal sealed class DevBridgeHost : MonoBehaviour
    {
        private readonly float[] _frameTimes = new float[120];
        private int _frameIndex;
        private int _frameCount;
        private float _frameSum;
        private bool _busy;

        public float AverageFps => _frameSum > 0f ? _frameCount / _frameSum : 0f;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_frameCount == _frameTimes.Length) _frameSum -= _frameTimes[_frameIndex];
            else _frameCount++;
            _frameTimes[_frameIndex] = dt;
            _frameSum += dt;
            _frameIndex = (_frameIndex + 1) % _frameTimes.Length;

            if (!_busy && DevBridge.TryDequeue(out PendingRequest pending))
            {
                _busy = true;
                StartCoroutine(Process(pending));
            }
        }

        private IEnumerator Process(PendingRequest pending)
        {
            var reply = new BridgeReply();
            IEnumerator handler = null;
            try { handler = DevBridge.Route(pending.Request, reply); }
            catch (Exception e) { DevBridge.Fail(pending.Request, reply, e); }

            if (handler != null)
                yield return SafeCoroutine.Run(handler, e => DevBridge.Fail(pending.Request, reply, e));

            DevBridge.Send(pending.Context, reply, synchronous: reply.QuitAfter);
            _busy = false;

            if (reply.QuitAfter)
            {
                Plugin.Log.LogInfo("[DevBridge] quit requested");
                DevBridge.Stop();
                yield return null;
                Application.Quit();
            }
        }

        private void OnApplicationQuit() => DevBridge.Stop();
    }

    /// <summary>
    /// Steps an iterator tree by hand so an exception at any depth is caught, and so the caller can abort
    /// between frames (scenario timeouts). Unity yield instructions are passed through unchanged.
    /// </summary>
    internal static class SafeCoroutine
    {
        public static IEnumerator Run(IEnumerator root, Action<Exception> onError, Func<bool> abort = null)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                if (abort != null && abort())
                {
                    DisposeAll(stack);
                    yield break;
                }

                IEnumerator top = stack.Peek();
                bool more;
                try { more = top.MoveNext(); }
                catch (Exception e)
                {
                    DisposeAll(stack);
                    onError(e);
                    yield break;
                }

                if (!more) { stack.Pop(); continue; }
                object current = top.Current;
                // CustomYieldInstruction (WaitForSecondsRealtime, WaitUntil...) is an IEnumerator too.
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return current;
            }
        }

        private static void DisposeAll(Stack<IEnumerator> stack)
        {
            // Runs the finally blocks of abandoned iterators (scenario cleanup).
            while (stack.Count > 0)
            {
                try { (stack.Pop() as IDisposable)?.Dispose(); }
                catch (Exception e) { Plugin.Log.LogWarning($"[DevBridge] iterator cleanup failed: {e.Message}"); }
            }
        }
    }

    /// <summary>Runs console commands and captures the lines they print to the terminal.</summary>
    internal static class DevConsole
    {
        private static readonly List<List<string>> s_sinks = new List<List<string>>();

        public static List<string> BeginCapture()
        {
            var sink = new List<string>();
            s_sinks.Add(sink);
            return sink;
        }

        public static void EndCapture(List<string> sink) => s_sinks.Remove(sink);

        public static void Execute(string cmd)
        {
            Terminal terminal = global::Console.instance;
            if (terminal == null) throw new BridgeException(503, "console not available");
            // skipAllowedCheck: the bridge acts as the local user; cheat gating still applies through IsCheatsEnabled.
            terminal.TryRunCommand(cmd.Trim(), silentFail: false, skipAllowedCheck: true);
        }

        /// <summary>Runs a command synchronously and returns what it printed during the call.</summary>
        public static List<string> Run(string cmd)
        {
            List<string> sink = BeginCapture();
            try { Execute(cmd); }
            finally { EndCapture(sink); }
            return sink;
        }

        [HarmonyPatch(typeof(Terminal), nameof(Terminal.AddString), typeof(string))]
        private static class TerminalAddStringPatch
        {
            private static void Prefix(Terminal __instance, string text)
            {
                if (s_sinks.Count == 0 || __instance != global::Console.instance) return;
                foreach (List<string> sink in s_sinks) sink.Add(text);
            }
        }
    }

    /// <summary>Screenshot capture into &lt;repo&gt;/docs/shots.</summary>
    internal static class DevShots
    {
        /// <summary>MW_REPO_ROOT when set (vh.py exports it), otherwise the plugin directory.</summary>
        public static string RepoRoot
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("MW_REPO_ROOT");
                return !string.IsNullOrEmpty(env) && Directory.Exists(env) ? env : Plugin.PluginDir;
            }
        }

        public static string ShotsDir => Path.Combine(RepoRoot, Path.Combine("docs", "shots"));

        public static string Sanitize(string name)
        {
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
                sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.' ? c : '_');
            string s = sb.ToString().Trim('.');
            if (s.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - 4);
            return s.Length == 0 ? "shot" : s;
        }

        /// <summary>Never throws; reports (path, null) on success or (null, error).</summary>
        public static IEnumerator Capture(string name, Action<string, string> onDone)
        {
            string path = Path.Combine(ShotsDir, Sanitize(name) + ".png");
            try
            {
                Directory.CreateDirectory(ShotsDir);
                if (File.Exists(path)) File.Delete(path);
                ScreenCapture.CaptureScreenshot(path);
            }
            catch (Exception e)
            {
                onDone(null, $"screenshot failed: {e.Message}");
                yield break;
            }

            // CaptureScreenshot writes at the end of a later frame; wait until the file size stops changing.
            float deadline = Time.realtimeSinceStartup + 15f;
            long lastLength = -1;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                long length = -1;
                try { if (File.Exists(path)) length = new FileInfo(path).Length; }
                catch (IOException) { /* still being written */ }
                if (length > 0 && length == lastLength)
                {
                    onDone(path, null);
                    yield break;
                }
                lastLength = length;
            }
            onDone(null, $"screenshot '{path}' was not written within 15 s (is the window minimized?)");
        }
    }
}
#endif
