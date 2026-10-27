#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Mass;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// S17_Multiplayer (phase 8-2, decisions 220 to 223): this client, joined to the dedicated dev server (<c>tools/vh.py s17</c>), is a real non owner. The server claims and simulates a dev colony
    /// (<see cref="ServerOwnDev"/>); the scenario compares, every 2 s, what the server says (netId, position, state, hp of each worm) with the local mirror, by netId.
    /// Skipped (status <c>skipped</c>, with the reason) when this peer is not joined to a dedicated MistWorms dev server: nothing answers <c>MW_DevServer</c>.
    /// Parameter <c>expect_alert</c>: the value that the orchestration wrote for <c>Colony.AlertRadius</c> in the .cfg of the server.
    /// Steps: (a) the synced key has the server's value; (b) 200 worms wander 20 s, then hunt the walking player 20 s: count gap 0 (2 during a keyframe), mean error &lt;= 0.5 m, p95 &lt;= 1.5 m,
    /// the error being that of the mirror at the instant of the server's snapshot (mid-point between the request and its answer, assuming a symmetric latency); (e) at the end of the hunt, bandwidth
    /// of the server (10 s average) &lt;= 6 KB/s, bytes received by the client reported; (f) CPU of the mirror &lt;= 0.3 ms; (c) a hit of this client on a mirrored worm lowers its hp on the server,
    /// the blood is played locally once and the returned event is not replayed; (d) a mass formed on the server is seen here (Character, <c>mw_n</c>) and a hit lowers its hp; (g) everything undone.
    /// </summary>
    internal sealed class S17_Multiplayer : IDevScenario, IKeepsPlayerPosition
    {
        private const int WormCount = 200;
        private const float ClusterDistance = 26f, ClusterRadius = 9f;
        private const float WanderSeconds = 20f, HuntSeconds = 20f, SampleEvery = 2f;
        private const float MeanBudget = 0.5f, P95Budget = 1.5f;
        private const double BandwidthBudget = 6144.0;      // 6 KB/s
        private const float MirrorCpuBudget = 0.3f;
        private const float AnswerTimeout = 8f;

        public string Name => "S17_Multiplayer";

        private WormColony _colony;
        private ZDOID _colonyId = ZDOID.None;
        private Vector3 _center;
        private bool _walking;
        private float _walkDeadline;
        private bool _reachedCluster;

        private sealed class Sampling
        {
            public string Name;
            public readonly List<float> Dist = new List<float>();
            public readonly List<float> DistH = new List<float>();
            public int Samples, Timeouts, MissingInMirror, ExtraInMirror, MaxGap, MaxGapSteady, KeyframeSamples;
            public int HuntingMax;
        }

        public IEnumerator Run(ScenarioContext ctx)
        {
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 420f;
            bool ran = false;
            bool prevGather = WormColony.GatheringEnabled;
            try
            {
                // ---- the precondition: a client joined to a server that answers the dev RPC
                ZNet znet = ZNet.instance;
                if (znet == null || znet.IsServer() || ZRoutedRpc.instance == null)
                {
                    ctx.Skip("this peer is not a client joined to a dedicated server (it hosts its own world, or is not in game): S17 runs on the client of `tools/vh.py s17`");
                    yield break;
                }
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ServerOwnDev.ServerState ping = null;
                yield return Ask("ping", ZDOID.None, "", AnswerTimeout, s => ping = s);
                if (ping == null || !ping.Dedicated)
                {
                    ctx.Skip("the server does not answer MW_DevServer: not a dedicated server running a MistWorms dev build (Profile or Debug)");
                    yield break;
                }
                ran = true;
                ctx.Log("a dedicated dev server answers");

                // ---- a) the config enforced by the server
                float live = MWConfig.Colony.AlertRadius.Value;
                string expectText = ctx.Param("expect_alert");
                if (expectText != null && float.TryParse(expectText, NumberStyles.Float, CultureInfo.InvariantCulture, out float expect))
                    ctx.Assert("a_config_enforced_Colony_AlertRadius", Mathf.Abs(live - expect) < 1e-3f, $"{expect} (the value of the server's .cfg)", live);
                else
                    ctx.Log($"no expect_alert parameter: Colony.AlertRadius is {live} here, nothing asserted (vh.py s17 passes it)");

                // ---- b) the colony of the dev site near the player
                ctx.Console("mw_kill");
                ctx.ClearInventory();
                yield return ctx.MoveToOpenGround(26f);
                if (!ctx.LastOpenGround.Found) yield break;
                Player player = Player.m_localPlayer;
                player.SetGodMode(true);
                player.SetGhostMode(false);   // the server sees a player that is not a ghost (the ghost flag is not replicated), and so does the worm AI

                _colony = ColonyDev.FindOrCreateColony(player, out string how);
                if (!ctx.Assert("b_colony_created", _colony != null, "a colony around the player", how)) yield break;
                _colonyId = _colony.ZdoId;
                WormColony.GatheringEnabled = false;   // free worms stay free here, and on the server (own gather=0): the comparison is about free worms

                _center = ChooseCluster(player);
                int created = SpawnCluster(_colony, _center, WormCount);
                ctx.Assert("b_worms_created", created >= WormCount * 95 / 100, $">= {WormCount * 95 / 100}", created);
                ctx.Log($"{created} worms awake on a disc of {ClusterRadius} m at {ClusterDistance} m from the player (they cannot sense it: HuntRange 12 m)");
                yield return ctx.Wait(2f);

                // the hand-over carries the worms by mw_snap: written now, and the server must have it before it takes the colony
                int snapSize = -1;
                yield return ctx.WaitUntil(() => (snapSize = _colony.DevWriteSnapshot()) > 0, 10f);
                if (!ctx.Assert("b_snapshot_written", snapSize > 0, "mw_snap written by the owner", snapSize)) yield break;
                ServerOwnDev.ServerState seen = null;
                float giveUp = Time.realtimeSinceStartup + 20f;
                while (Time.realtimeSinceStartup < giveUp)
                {
                    yield return Ask("state", _colonyId, "", AnswerTimeout, s => seen = s);
                    if (seen != null && seen.KnowsZdo && seen.SnapshotBytes >= snapSize) break;
                    yield return ctx.Wait(0.5f);
                }
                if (!ctx.Assert("b_server_has_snapshot", seen != null && seen.KnowsZdo && seen.SnapshotBytes >= snapSize, $"the server knows the colony and holds mw_snap >= {snapSize} bytes",
                        seen == null ? "no answer" : $"knows {seen.KnowsZdo}, {seen.SnapshotBytes} bytes")) yield break;

                // ---- the server takes the colony (serverown)
                int aliveBefore = _colony.Store.AliveCount;
                ServerOwnDev.ServerState own = null;
                yield return Ask("own", _colonyId, "gather=0", AnswerTimeout, s => own = s);
                if (!ctx.Assert("b_serverown_accepted", own != null && own.Ok, "the server accepts the colony", own == null ? "no answer" : own.Error)) yield break;

                ServerOwnDev.ServerState taken = null;
                giveUp = Time.realtimeSinceStartup + 90f;
                while (Time.realtimeSinceStartup < giveUp)
                {
                    yield return Ask("state", _colonyId, "", AnswerTimeout, s => taken = s);
                    if (taken != null && taken.HasColony && taken.ColonyOwner && taken.Alive > 0) break;
                    yield return ctx.Wait(1f);
                }
                if (!ctx.Assert("b_server_simulates", taken != null && taken.HasColony && taken.ColonyOwner && taken.Alive >= aliveBefore * 95 / 100,
                        $"the server owns and simulates the colony with >= {aliveBefore * 95 / 100} of {aliveBefore} worms (restored from mw_snap)",
                        taken == null ? "no answer" : $"colony object {taken.HasColony}, owner {taken.ColonyOwner}, alive {taken.Alive}")) yield break;
                yield return ctx.WaitUntil(() => _colony != null && _colony.IsMirror && !_colony.IsOwner, 20f, "b_client_is_mirror");
                if (ctx.LastWaitTimedOut) yield break;

                // the mirror fills from the keyframe
                _colony.ResetNetCounters();
                yield return ctx.WaitUntil(() => _colony.Store.AliveCount >= taken.Alive - 2, 20f, "b_mirror_filled");
                if (ctx.LastWaitTimedOut) yield break;
                ServerOwnDev.ServerState reset = null;
                yield return Ask("reset", _colonyId, "", AnswerTimeout, s => reset = s);
                ctx.Log($"mirror: {_colony.Store.AliveCount} worms, server {taken.Alive}; keyframes sent {taken.Keyframes}, recipients {taken.Recipients}");

                // ---- b) 20 s of wandering, then 20 s of hunt while the player walks into the group
                var wander = new Sampling { Name = "wander" };
                yield return SampleFor(ctx, wander, WanderSeconds, null);
                yield return ctx.Shot("wander");

                _walking = true;
                _reachedCluster = false;
                _walkDeadline = Time.time + 12f;
                var hunt = new Sampling { Name = "hunt" };
                yield return SampleFor(ctx, hunt, HuntSeconds, Drive);
                DevInput.Release();
                yield return ctx.Shot("hunt");
                Report(ctx, wander);
                Report(ctx, hunt);

                var both = new Sampling { Name = "all" };
                both.Dist.AddRange(wander.Dist); both.Dist.AddRange(hunt.Dist);
                both.DistH.AddRange(wander.DistH); both.DistH.AddRange(hunt.DistH);
                both.Samples = wander.Samples + hunt.Samples;
                both.MaxGap = Math.Max(wander.MaxGap, hunt.MaxGap);
                both.MaxGapSteady = Math.Max(wander.MaxGapSteady, hunt.MaxGapSteady);
                both.Timeouts = wander.Timeouts + hunt.Timeouts;
                double mean = ErrorStats.Mean(both.Dist);
                float p95 = ErrorStats.Percentile(both.Dist, 95);
                ctx.Assert("b_samples", both.Samples >= 16 && both.Timeouts == 0, ">= 16 comparisons, no lost answer", $"{both.Samples} (lost {both.Timeouts})");
                ctx.Assert("b_hunt_engaged", hunt.HuntingMax >= WormCount / 2, $">= {WormCount / 2} worms Hunting or Striking at some point of the hunt", hunt.HuntingMax);
                ctx.Assert("b_count_gap_zero", both.MaxGapSteady == 0 && both.MaxGap <= 2, "0 outside a keyframe, <= 2 during one", $"max {both.MaxGapSteady} outside, {both.MaxGap} overall");
                ctx.Assert("b_mean_error", mean <= MeanBudget, $"<= {MeanBudget} m (mirror at the instant of the server snapshot)", $"{mean:0.000} m over {both.Dist.Count} worm samples (horizontal {ErrorStats.Mean(both.DistH):0.000})");
                ctx.Assert("b_p95_error", p95 <= P95Budget, $"<= {P95Budget} m", $"{p95:0.000} m (max {ErrorStats.Max(both.Dist):0.00})");

                // ---- e) and f), measured at the end of the hunt, before anything else changes the traffic
                ServerOwnDev.ServerState end = null;
                yield return Ask("state", _colonyId, "", AnswerTimeout, s => end = s);
                if (end != null)
                {
                    ctx.Assert("e_awake_worms", end.Awake >= WormCount * 9 / 10, $">= {WormCount * 9 / 10} awake worms on the server", end.Awake);
                    ctx.Assert("e_bandwidth", end.SentBytesPerSec <= BandwidthBudget, $"<= {BandwidthBudget:0} B/s sent by the server (10 s average, {end.Awake} awake worms, {end.Recipients} recipient)",
                        $"{end.SentBytesPerSec:0} B/s; {end.SentPackets} packets, {end.Keyframes} keyframes, {end.EventsSent} events; send cost {end.SendAvgMs:0.000} ms");
                    ctx.Log($"server simulation: last {end.SimLastMs:0.00} ms, average {end.SimAvgMs:0.00}, max {end.SimMaxMs:0.00}, worst without the first strike {end.SimPeakMs:0.00}");
                }
                else ctx.Assert("e_bandwidth", false, "the server answers", "no answer");
                ctx.Log($"client received {_colony.NetBytesReceivedPerSecond:0} B/s (10 s average), {_colony.NetBytesReceived} bytes in {_colony.NetPacketsReceived} packets; bad packets {_colony.NetBadPackets}");
                ctx.Assert("e_no_bad_packet", _colony.NetBadPackets == 0, 0, _colony.NetBadPackets);
                ctx.Assert("f_mirror_cpu", _colony.MirrorAvgMs <= MirrorCpuBudget, $"<= {MirrorCpuBudget} ms per frame (average)", $"{_colony.MirrorAvgMs:0.000} ms (max {_colony.MirrorMaxMs:0.000}, last {_colony.MirrorLastMs:0.000})");

                // ---- c) a hit of this client on a mirrored worm
                yield return HitWorm(ctx, player);

                // ---- d) a mass formed on the server, seen and hit from here
                yield return MassOnServer(ctx, player);
            }
            finally
            {
                DevInput.Release();
                WormColony.GatheringEnabled = prevGather;
                ServerOwnDev.ReleaseIfActive();   // g) the server gives back its flag, its reference position and its gathering, and destroys the colony
            }

            // ---- g) restore everything
            if (ran) yield return Cleanup(ctx);
        }

        // ------------------------------------------------------------------ steps

        private IEnumerator HitWorm(ScenarioContext ctx, Player player)
        {
            WormStore s = _colony.Store;
            int slot = -1;
            float best = float.MaxValue;
            for (int i = 0; i < s.HighWater; i++)
            {
                if (!s.alive[i] || s.netId[i] == 0) continue;
                float d = (s.pos[i] - player.transform.position).sqrMagnitude;
                if (d < best) { best = d; slot = i; }
            }
            if (!ctx.Assert("c_mirror_worm_found", slot >= 0, "a worm in the mirror", slot)) yield break;
            int netId = s.netId[slot];

            ServerOwnDev.ServerState before = null;
            yield return Ask("state", _colonyId, "", AnswerTimeout, st => before = st);
            int at = before != null ? before.IndexOf(netId) : -1;
            if (!ctx.Assert("c_server_knows_worm", at >= 0, $"netId {netId} on the server", at)) yield break;
            float hpBefore = before.WormHp[at];
            int sentBefore = before.EventsSent;
            int hitsBefore = before.HitsReceived;
            int fxBefore = WormEffects.FxSpawned;
            int echoedBefore = _colony.NetEventsEchoed, playedBefore = _colony.NetEventsPlayed;

            var hit = new HitData();
            hit.m_damage.m_slash = 2f;
            hit.SetAttacker(player);
            hit.m_hitType = HitData.HitType.PlayerHit;
            hit.m_point = s.pos[slot] + Vector3.up * 0.1f;
            hit.m_dir = player.transform.forward;
            bool viaProxy = ColonyDev.HitWorm(_colony, slot, hit);
            ctx.Log($"hit of 2 slash on the mirrored worm netId {netId} (slot {slot}, hp {hpBefore:0.00} on the server), {(viaProxy ? "through its hit proxy" : "straight to the colony")}");

            yield return ctx.Wait(1.5f);   // the blood is deferred to the next Update, the RPC and the event take a few ticks
            ServerOwnDev.ServerState after = null;
            yield return Ask("state", _colonyId, "", AnswerTimeout, st => after = st);
            int at2 = after != null ? after.IndexOf(netId) : -1;
            float hpAfter = at2 >= 0 ? after.WormHp[at2] : 0f;
            ctx.Assert("c_hp_lowered_on_server", after != null && (at2 < 0 || hpAfter < hpBefore - 0.05f), $"hp below {hpBefore:0.00} on the server",
                after == null ? "no answer" : at2 < 0 ? "the worm died" : $"{hpAfter:0.00} ({after.HitsReceived - hitsBefore} hit received)");
            ctx.Assert("c_blood_played_once_locally", WormEffects.FxSpawned - fxBefore == 1, "1 effect played here", WormEffects.FxSpawned - fxBefore);
            ctx.Assert("c_event_sent_by_server", after != null && after.EventsSent - sentBefore >= 1, "the server emitted the event", after == null ? "no answer" : after.EventsSent - sentBefore);
            ctx.Assert("c_event_not_replayed", _colony.NetEventsPlayed == playedBefore && _colony.NetEventsEchoed - echoedBefore >= 1,
                "the returned event is recognised as this peer's own and skipped", $"played {_colony.NetEventsPlayed - playedBefore}, echoed {_colony.NetEventsEchoed - echoedBefore}");
        }

        private IEnumerator MassOnServer(ScenarioContext ctx, Player player)
        {
            ServerOwnDev.ServerState formed = null;
            yield return Ask("mass", _colonyId, "count=40", AnswerTimeout, s => formed = s);
            if (!ctx.Assert("d_mass_formed_on_server", formed != null && formed.Ok, "the server forms a mass of 40", formed == null ? "no answer" : formed.Error)) yield break;

            WritheMassState mass = null;
            yield return ctx.WaitUntil(() => (mass = FindRemoteMass()) != null && mass.Character != null && mass.N > 0, 20f);
            if (!ctx.Assert("d_mass_visible_here", mass != null && mass.Character != null && mass.N > 0, "a mass object here, owned by the server, with a Character and mw_n > 0",
                    mass == null ? "none" : $"owner {mass.IsOwner}, Character {mass.Character != null}, n {mass.N}")) yield break;
            ZDOID massId = mass.ZdoId;
            ctx.Assert("d_mass_owned_by_server", !mass.IsOwner, "not owned by this client", mass.IsOwner);
            ctx.Log($"mass {massId} seen here: n {mass.N}, hp {mass.Health:0.0}");
            yield return ctx.Shot("mass");

            yield return ctx.Wait(1f);
            ServerOwnDev.ServerState before = null;
            yield return Ask("state", _colonyId, "", AnswerTimeout, st => before = st);
            float hpBefore = MassHealthOn(before, massId), hereBefore = mass.Health;
            int nBefore = mass.N;

            var hit = new HitData();
            hit.m_damage.m_blunt = 60f;
            hit.SetAttacker(player);
            hit.m_hitType = HitData.HitType.PlayerHit;
            hit.m_point = mass.transform.position + Vector3.up * 0.3f;
            hit.m_dir = player.transform.forward;
            mass.Character.Damage(hit);
            ctx.Log($"hit of 60 blunt on the mass (server hp {hpBefore:0.0}, seen here {hereBefore:0.0}, n {nBefore})");

            ServerOwnDev.ServerState after = null;
            float giveUp = Time.realtimeSinceStartup + 6f;
            float hpAfter = hpBefore;
            while (Time.realtimeSinceStartup < giveUp)
            {
                yield return ctx.Wait(0.75f);
                yield return Ask("state", _colonyId, "", AnswerTimeout, st => after = st);
                hpAfter = MassHealthOn(after, massId);
                if (hpAfter < hpBefore - 1f) break;
            }
            ctx.Assert("d_hp_lowered_on_server", hpAfter >= 0f && hpAfter < hpBefore - 1f, $"hp below {hpBefore:0.0} on the server", $"{hpAfter:0.0}");
            yield return ctx.WaitUntil(() => mass == null || !mass.Valid || mass.Health < hereBefore - 1f || mass.N < nBefore, 5f);
            bool seen = mass == null || !mass.Valid || mass.Health < hereBefore - 1f || mass.N < nBefore;
            ctx.Assert("d_hp_lowered_seen_here", seen, $"hp below {hereBefore:0.0} (or n below {nBefore}) as read here from the ZDO", mass != null && mass.Valid ? $"hp {mass.Health:0.0}, n {mass.N}" : "the mass is gone");
        }

        private IEnumerator Cleanup(ScenarioContext ctx)
        {
            // The release was sent by the finally block (it destroys the colony and its masses on the server); a client that kept the colony (the take-over never happened) removes it itself.
            float end = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < end && (ColonyRegistry.FindById(_colonyId) != null || FindRemoteMass() != null)) yield return ctx.Wait(0.5f);
            bool colonyGone = ColonyRegistry.FindById(_colonyId) == null;
            bool massGone = FindRemoteMass() == null;
            if (!colonyGone || !massGone)
            {
                ctx.Log("the server did not clean up in 30 s: removing what this client owns");
                ctx.Console("mw_kill");
                yield return ctx.Wait(2f);
            }
            ctx.Assert("g_colony_destroyed", colonyGone, "the colony is gone from this client", colonyGone ? "gone" : "still here");
            ctx.Assert("g_masses_destroyed", massGone, "no remote mass left", massGone ? "none" : "still here");
            ctx.Assert("g_flags_restored", !ColonyRegistry.DevServerOwn && !ServerOwnDev.ClientHoldsRequest, "DevServerOwn false, no request held", $"{ColonyRegistry.DevServerOwn}, {ServerOwnDev.ClientHoldsRequest}");
            ServerOwnDev.ServerState last = null;
            yield return Ask("state", ZDOID.None, "", AnswerTimeout, s => last = s);
            ctx.Assert("g_server_flag_restored", last != null && !last.Active, "the server's dev mode is off", last == null ? "no answer" : $"active {last.Active}");
            Player p = Player.m_localPlayer;
            if (p != null) p.SetGodMode(false);
        }

        // ------------------------------------------------------------------ sampling

        private IEnumerator SampleFor(ScenarioContext ctx, Sampling acc, float seconds, Action drive)
        {
            float end = Time.realtimeSinceStartup + seconds;
            float next = Time.realtimeSinceStartup;
            int pending = 0;
            float t0 = 0f;
            while (Time.realtimeSinceStartup < end || pending != 0)
            {
                drive?.Invoke();
                if (pending == 0 && Time.realtimeSinceStartup >= next && Time.realtimeSinceStartup < end)
                {
                    pending = ServerOwnDev.Request("state", _colonyId);
                    t0 = Time.time;
                    next = Time.realtimeSinceStartup + SampleEvery;
                    if (pending == 0) { acc.Timeouts++; break; }
                }
                if (pending != 0)
                {
                    if (ServerOwnDev.TryGet(pending, out ServerOwnDev.ServerState st))
                    {
                        Compare(acc, st, t0);
                        pending = 0;
                    }
                    else if (Time.realtimeSinceStartup > next - SampleEvery + AnswerTimeout)
                    {
                        acc.Timeouts++;
                        pending = 0;
                    }
                }
                yield return null;
            }
        }

        /// <summary>One comparison: the server's worms against the mirror, by netId, the mirror taken at the instant of the server's snapshot.</summary>
        private void Compare(Sampling acc, ServerOwnDev.ServerState st, float requestedAt)
        {
            WormStore s = _colony.Store;
            WormMirror mirror = _colony.Mirror;
            float tMid = 0.5f * (requestedAt + st.ArrivedAt);
            acc.Samples++;

            int gap = Math.Abs(st.WormCount - s.AliveCount);
            bool keyframe = mirror.KeyframeOpen;
            if (keyframe) acc.KeyframeSamples++;
            acc.MaxGap = Math.Max(acc.MaxGap, gap);
            if (!keyframe) acc.MaxGapSteady = Math.Max(acc.MaxGapSteady, gap);

            int hunting = 0;
            for (int k = 0; k < st.WormCount; k++)
            {
                int state = st.WormState[k];
                if (state == (int)WormState.Hunting || state == (int)WormState.Striking) hunting++;
                int slot = s.FindByNetId(st.WormId[k]);
                if (slot < 0) { acc.MissingInMirror++; continue; }
                Vector3 shown = mirror.DisplayedAt(slot, tMid);
                Vector3 truth = st.WormPos[k];
                acc.Dist.Add(Vector3.Distance(shown, truth));
                acc.DistH.Add(new Vector2(shown.x - truth.x, shown.z - truth.z).magnitude);
            }
            acc.HuntingMax = Math.Max(acc.HuntingMax, hunting);
            for (int i = 0; i < s.HighWater; i++)
                if (s.alive[i] && st.IndexOf(s.netId[i]) < 0) acc.ExtraInMirror++;
        }

        private static void Report(ScenarioContext ctx, Sampling a)
        {
            ctx.Log($"{a.Name}: {a.Samples} comparisons, {a.Dist.Count} worm samples: mean {ErrorStats.Mean(a.Dist):0.000} m (horizontal {ErrorStats.Mean(a.DistH):0.000}), p95 {ErrorStats.Percentile(a.Dist, 95):0.000}, " +
                    $"max {ErrorStats.Max(a.Dist):0.00}; count gap max {a.MaxGap} ({a.MaxGapSteady} outside a keyframe, {a.KeyframeSamples} samples in one); missing in the mirror {a.MissingInMirror}, extra {a.ExtraInMirror}; " +
                    $"hunting at most {a.HuntingMax}; lost answers {a.Timeouts}");
        }

        // ------------------------------------------------------------------ helpers

        private static IEnumerator Ask(string cmd, ZDOID colony, string arg, float timeout, Action<ServerOwnDev.ServerState> result)
        {
            int id = ServerOwnDev.Request(cmd, colony, arg);
            if (id == 0) { result(null); yield break; }
            float end = Time.realtimeSinceStartup + timeout;
            ServerOwnDev.ServerState st = null;
            while (!ServerOwnDev.TryGet(id, out st) && Time.realtimeSinceStartup < end) yield return null;
            result(st);
        }

        private static float MassHealthOn(ServerOwnDev.ServerState st, ZDOID id)
        {
            if (st == null) return -1f;
            foreach (ServerOwnDev.MassInfo m in st.Masses)
                if (m.Id == id) return m.Health;
            return -1f;
        }

        private static WritheMassState FindRemoteMass()
        {
            foreach (WritheMassState m in WritheMassState.All)
                if (m != null && m.Valid && !m.IsOwner && !m.Dissolving) return m;
            return null;
        }

        /// <summary>The player walks to the middle of the cluster, running, then stands there (the worms close in).</summary>
        private void Drive()
        {
            Player p = Player.m_localPlayer;
            if (!_walking || p == null) return;
            Vector3 to = _center - p.transform.position;
            to.y = 0f;
            if (to.magnitude < 2f || Time.time > _walkDeadline)
            {
                _walking = false;
                _reachedCluster = to.magnitude < 2f;
                DevInput.Release();
                return;
            }
            p.m_stamina = p.GetMaxStamina();
            p.SetLookDir(to.normalized);
            DevInput.Set(new Vector3(0f, 0f, 1f), true, false);
        }

        /// <summary>The centre of the cluster: <see cref="ClusterDistance"/> from the player along the first of 8 directions with a floor at the player's height and nothing in between.</summary>
        private static Vector3 ChooseCluster(Player player)
        {
            Vector3 origin = player.transform.position;
            Vector3 fwd = player.transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            int mask = WormPhysics.SolidMask;
            for (int k = 0; k < 8; k++)
            {
                Vector3 dir = Quaternion.AngleAxis(k * 45f, Vector3.up) * fwd;
                Vector3 c = origin + dir * ClusterDistance;
                if (!Physics.Raycast(c + Vector3.up * 6f, Vector3.down, out RaycastHit hit, 14f, mask, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(hit.point.y - origin.y) > 3f || Vector3.Angle(hit.normal, Vector3.up) > 30f) continue;
                if (Physics.Linecast(origin + Vector3.up * 1.5f, hit.point + Vector3.up * 1.5f, mask, QueryTriggerInteraction.Ignore)) continue;
                return hit.point;
            }
            return origin + fwd * ClusterDistance;
        }

        private static int SpawnCluster(WormColony colony, Vector3 center, int wanted)
        {
            var rng = new System.Random(1707);
            int made = 0;
            int mask = WormPhysics.SolidMask;
            for (int attempt = 0; attempt < wanted * 12 && made < wanted; attempt++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Sqrt((float)rng.NextDouble()) * ClusterRadius;
                var start = new Vector3(center.x + Mathf.Cos(angle) * r, center.y + 3f, center.z + Mathf.Sin(angle) * r);
                if (!Physics.Raycast(start, Vector3.down, out RaycastHit hit, 8f, mask, QueryTriggerInteraction.Ignore)) continue;
                if (Vector3.Angle(hit.normal, Vector3.up) > 40f) continue;
                if (!WormMath.InsideBox(hit.point, colony.BoxCenter, colony.BoxHalfSize)) continue;
                int slot = colony.AddWorm(hit.point, WormState.Idle, (float)rng.NextDouble() * Mathf.PI * 2f - Mathf.PI, force: true);
                if (slot < 0) break;
                made++;
            }
            return made;
        }
    }
}
#endif
