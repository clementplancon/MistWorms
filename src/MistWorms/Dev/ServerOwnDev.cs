#if MW_DEV
using System;
using System.Collections.Generic;
using MistWorms.Colony;
using MistWorms.Mass;
using UnityEngine;

namespace MistWorms.Dev
{
    /// <summary>
    /// The <c>serverown</c> dev mode (phase 8-2, decisions 220 and 221): a client asks the dedicated server, by routed RPCs, to claim the colony of the dev site and simulate it, which makes the
    /// client a real non owner, and to answer queries about it. Dev builds only (<c>MW_DEV</c>); inert in a Release build (the file is not compiled), and the handlers answer only on a
    /// dedicated server.
    ///
    /// Requests, client to server, routed RPC <c>MW_DevServer(int id, string cmd, ZDOID colony, string arg)</c>; the answer is <c>MW_DevState(ZPackage)</c> to the sender:
    /// <list type="bullet">
    /// <item><c>ping</c>: answers (is this a dedicated dev server?).</item>
    /// <item><c>own</c>: <see cref="ColonyRegistry.DevServerOwn"/> on, the server's reference position moved onto the colony and held there (a dedicated server only instantiates the objects of the zones around
    /// its reference position, which its <c>Game.FixedUpdate</c> resets to (1e6, 0, 1e6) at every physics step: <see cref="OverrideReference"/>, a prefix of <c>ZNet.SetReferencePosition</c>, keeps it), the ZDO taken (<c>ZDO.SetOwner</c>), gathering switched off when
    /// <c>arg</c> has <c>gather=0</c>. The release rule of vanilla (<c>ZDOMan.ReleaseNearbyZDOS</c>) is also blocked for that ZDO by <see cref="BlocksRelease"/> (the dev flag).</item>
    /// <item><c>state</c>: the worms (netId, position, state, hp), the masses, the network counters and the simulation time of the colony.</item>
    /// <item><c>mass</c>: forms a mass of <c>count=N</c> of the free worms on the server (the owner of the colony).</item>
    /// <item><c>reset</c>: forgets the counters and the simulation peaks.</item>
    /// <item><c>release</c>: undoes everything (flag, reference position, gathering), removes the masses the server owns and destroys the colony.</item>
    /// </list>
    /// Allocation: dev tool, every request and answer allocates.
    /// </summary>
    internal static class ServerOwnDev
    {
        public const string RpcRequest = "MW_DevServer";
        public const string RpcState = "MW_DevState";

        // ---------------------------------------------------------------- server side state

        private static bool s_active;
        private static ZDOID s_colonyId = ZDOID.None;
        private static Vector3 s_previousReference;
        private static bool s_previousGathering;
        private static Vector3 s_referenceTarget;

        /// <summary>
        /// The dedicated server build of <c>Game.FixedUpdate</c> calls <c>ZNet.SetReferencePosition(new Vector3(1000000, 0, 1000000))</c> on every physics step (verified in the decompiled
        /// <c>valheim_server_Data</c>; the client assembly has no such line), so a reference position set once is overwritten within 20 ms and the server never loads the zone of the colony.
        /// The Harmony prefix of <c>ZNet.SetReferencePosition</c> asks here: while the mode is active every caller gets the colony position. True = <paramref name="pos"/> was replaced.
        /// </summary>
        public static bool OverrideReference(ref Vector3 pos)
        {
            if (!s_active || !ColonyRegistry.DevServerOwn) return false;
            pos = s_referenceTarget;
            return true;
        }

        /// <summary>True on the server while it holds a colony for a client (the dev flag of the release block).</summary>
        public static bool ServerActive => s_active;

        /// <summary>
        /// The release rule of vanilla gives back to a peer a ZDO that the server owns and that is not in the active area of the server (<c>ZDOMan.ReleaseNearbyZDOS</c> calls
        /// <c>ZDO.SetOwner(0)</c>, then <c>SetOwner(peer)</c>): the Harmony prefix of <c>ZDO.SetOwner</c> asks here. True = skip the change. Inert unless the dev mode holds this very ZDO and the
        /// new owner is not the server itself.
        /// </summary>
        public static bool BlocksRelease(ZDO zdo, long newOwner)
        {
            if (!s_active || !ColonyRegistry.DevServerOwn || zdo == null || zdo.m_uid != s_colonyId) return false;
            return newOwner != ZDOMan.GetSessionID();
        }

        public static void Register()
        {
            if (ZRoutedRpc.instance == null) return;
            try
            {
                ZRoutedRpc.instance.Register<int, string, ZDOID, string>(RpcRequest, OnRequest);
                ZRoutedRpc.instance.Register<ZPackage>(RpcState, OnState);
            }
            catch (ArgumentException)
            {
                // Already registered on this ZRoutedRpc (Game.Start ran twice on it).
            }
        }

        // ---------------------------------------------------------------- server: requests

        private static void OnRequest(long sender, int id, string cmd, ZDOID colonyId, string arg)
        {
            var pkg = new ZPackage();
            try
            {
                if (ZNet.instance == null || !ZNet.instance.IsDedicated()) return;   // only a dedicated server answers
                string error = null;
                switch (cmd)
                {
                    case "ping": break;
                    case "own": error = Own(colonyId, arg); break;
                    case "mass": error = FormMass(arg); break;
                    case "reset": ResetCounters(); break;
                    case "release": Release(); break;
                    case "state": break;
                    default: error = "unknown command " + cmd; break;
                }
                WriteState(pkg, id, cmd, error, colonyId == ZDOID.None ? s_colonyId : colonyId, cmd == "state");
            }
            catch (Exception e)
            {
                Plugin.LogException("ServerOwnDev.OnRequest " + cmd, e);
                pkg = new ZPackage();
                WriteState(pkg, id, cmd, e.GetType().Name + ": " + e.Message, ZDOID.None, false);
            }
            ZRoutedRpc.instance?.InvokeRoutedRPC(sender, RpcState, pkg);
        }

        private static string Own(ZDOID colonyId, string arg)
        {
            ZDO zdo = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(colonyId) : null;
            if (zdo == null) return "the server does not know the ZDO " + colonyId + " yet";
            if (!s_active)
            {
                s_previousReference = ZNet.instance.GetReferencePosition();
                s_previousGathering = WormColony.GatheringEnabled;
            }
            s_active = true;
            s_colonyId = colonyId;
            ColonyRegistry.DevServerOwn = true;
            if (HasOption(arg, "gather", "0")) WormColony.GatheringEnabled = false;
            s_referenceTarget = zdo.GetPosition();
            ZNet.instance.SetReferencePosition(s_referenceTarget);   // kept there by OverrideReference against the 1e6 reset of Game.FixedUpdate
            if (zdo.GetOwner() != ZDOMan.GetSessionID()) zdo.SetOwner(ZDOMan.GetSessionID());
            Plugin.Log.LogInfo($"[Dev] serverown: the server takes the colony {colonyId} (reference position {zdo.GetPosition()}, gathering {(WormColony.GatheringEnabled ? "on" : "off")})");
            return null;
        }

        private static string FormMass(string arg)
        {
            WormColony c = ColonyRegistry.FindById(s_colonyId);
            if (c == null || !c.IsOwner) return "the server has no colony object it owns";
            int count = 40;
            string v = Option(arg, "count");
            if (v != null) int.TryParse(v, out count);
            WritheMassState mass = c.DevFormMassFromGroup(-1, count);
            if (mass == null) return "no mass could be formed (no free worm of the group -1, or the prefab is missing)";
            Plugin.Log.LogInfo($"[Dev] serverown: mass of {mass.N} formed on the server at {mass.transform.position}");
            return null;
        }

        private static void ResetCounters()
        {
            WormColony c = ColonyRegistry.FindById(s_colonyId);
            if (c == null) return;
            c.ResetNetCounters();
            c.ResetSimPeaks();
            c.ResetCounters();
        }

        private static void Release()
        {
            if (!s_active) return;
            WormColony c = ColonyRegistry.FindById(s_colonyId);
            // The flag first: from here the ZDO may be released and destroyed like any other.
            s_active = false;
            ColonyRegistry.DevServerOwn = false;
            MassDev.KillAll();
            if (c != null && c.IsOwner)
            {
                c.KillAll();
                c.DestroyAllNests();
                c.DestroyColony();
            }
            else
            {
                ZDO zdo = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(s_colonyId) : null;
                if (zdo != null)
                {
                    zdo.SetOwner(ZDOMan.GetSessionID());
                    ZDOMan.instance.DestroyZDO(zdo);
                }
            }
            if (ZNet.instance != null) ZNet.instance.SetReferencePosition(s_previousReference);
            WormColony.GatheringEnabled = s_previousGathering;
            s_colonyId = ZDOID.None;
            Plugin.Log.LogInfo("[Dev] serverown: released, the colony is destroyed");
        }

        private static string Option(string arg, string key)
        {
            if (string.IsNullOrEmpty(arg)) return null;
            foreach (string part in arg.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq > 0 && string.Equals(part.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase)) return part.Substring(eq + 1).Trim();
            }
            return null;
        }

        private static bool HasOption(string arg, string key, string value) => Option(arg, key) == value;

        // ---------------------------------------------------------------- the answer

        private static void WriteState(ZPackage p, int id, string cmd, string error, ZDOID colonyId, bool withWorms)
        {
            p.Write(id);
            p.Write(cmd ?? string.Empty);
            p.Write(error ?? string.Empty);
            p.Write(ZNet.instance != null && ZNet.instance.IsDedicated());
            p.Write(s_active);
            p.Write(Time.timeAsDouble);

            ZDO zdo = colonyId != ZDOID.None && ZDOMan.instance != null ? ZDOMan.instance.GetZDO(colonyId) : null;
            byte[] snap = zdo != null ? zdo.GetByteArray(MWNames.ZdoSnapshot.GetStableHashCode(), null) : null;
            p.Write(zdo != null);                           // the server knows the ZDO
            p.Write(zdo != null ? zdo.GetOwner() : 0L);
            p.Write(snap != null ? snap.Length : 0);        // mw_snap as the server has it

            WormColony c = colonyId != ZDOID.None ? ColonyRegistry.FindById(colonyId) : null;
            WormStore s = c != null ? c.Store : null;
            p.Write(c != null);                             // the server has the colony object
            p.Write(c != null && c.IsOwner);
            int alive = 0, awake = 0;
            if (s != null)
                for (int i = 0; i < s.HighWater; i++)
                {
                    if (!s.alive[i]) continue;
                    byte st = s.state[i];
                    if (st == (byte)WormState.Absorbed || st == (byte)WormState.Dead) continue;
                    alive++;
                    if (st != (byte)WormState.Sleeping) awake++;
                }
            p.Write(alive);
            p.Write(awake);

            p.Write(c != null ? c.LastSimMs : 0f);
            p.Write(c != null ? c.AvgSimMs : 0f);
            p.Write(c != null ? c.MaxSimMs : 0f);
            p.Write(c != null ? c.PeakMsWithoutFirstStrike : 0f);
            p.Write(c != null ? c.NetBytesSentPerSecond : 0.0);
            p.Write(c != null ? c.NetBytesSent : 0L);
            p.Write(c != null ? c.NetPacketsSent : 0);
            p.Write(c != null ? c.NetKeyframesSent : 0);
            p.Write(c != null ? c.NetEventsSent : 0);
            p.Write(c != null ? c.NetRecipients : 0);
            p.Write(c != null ? c.NetBadPackets : 0);
            p.Write(c != null ? c.NetSendAvgMs : 0f);
            p.Write(c != null ? c.HitsReceived : 0);

            var masses = new List<WritheMassState>();
            foreach (WritheMassState m in WritheMassState.All)
                if (m != null && m.Valid && !m.Dissolving) masses.Add(m);
            p.Write(masses.Count);
            foreach (WritheMassState m in masses)
            {
                p.Write(m.ZdoId);
                p.Write(m.N);
                p.Write(m.Health);
                p.Write(m.IsOwner);
            }

            int n = withWorms ? alive : 0;
            p.Write(n);
            if (n == 0 || s == null) return;
            for (int i = 0; i < s.HighWater; i++)
            {
                if (!s.alive[i]) continue;
                byte st = s.state[i];
                if (st == (byte)WormState.Absorbed || st == (byte)WormState.Dead) continue;
                p.Write((int)s.netId[i]);
                p.Write(s.pos[i]);
                p.Write((int)st);
                p.Write(s.hp[i]);
            }
        }

        // ---------------------------------------------------------------- client side

        /// <summary>What the server said (<see cref="WriteState"/>).</summary>
        public sealed class ServerState
        {
            public int Id;
            public string Cmd, Error;
            public bool Dedicated, Active;
            public double ServerTime;
            public bool KnowsZdo;
            public long ZdoOwner;
            public int SnapshotBytes;
            public bool HasColony, ColonyOwner;
            public int Alive, Awake;
            public float SimLastMs, SimAvgMs, SimMaxMs, SimPeakMs;
            public double SentBytesPerSec;
            public long SentBytes;
            public int SentPackets, Keyframes, EventsSent, Recipients, BadPackets;
            public float SendAvgMs;
            public int HitsReceived;
            public readonly List<MassInfo> Masses = new List<MassInfo>();
            public int[] WormId = new int[0];
            public Vector3[] WormPos = new Vector3[0];
            public int[] WormState = new int[0];
            public float[] WormHp = new float[0];
            public int WormCount;
            /// <summary>Client clock (<c>Time.time</c>) when the answer was handled.</summary>
            public float ArrivedAt;

            public bool Ok => string.IsNullOrEmpty(Error);

            public int IndexOf(int netId)
            {
                for (int i = 0; i < WormCount; i++) if (WormId[i] == netId) return i;
                return -1;
            }
        }

        public struct MassInfo
        {
            public ZDOID Id;
            public int N;
            public float Health;
            public bool Owner;
        }

        private static int s_nextId = 1;
        private static readonly Dictionary<int, ServerState> s_answers = new Dictionary<int, ServerState>();
        private static readonly Dictionary<int, float> s_sentAt = new Dictionary<int, float>();
        private static bool s_clientOwnRequested;

        /// <summary>Client: sends a request to the server and returns its id (the answer comes by <see cref="TryGet"/>), or 0 when this peer cannot send.</summary>
        public static int Request(string cmd, ZDOID colony, string arg = "")
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null || ZNet.instance == null || ZNet.instance.IsServer()) return 0;
            int id = s_nextId++;
            s_sentAt[id] = Time.time;
            if (cmd == "own") s_clientOwnRequested = true;
            if (cmd == "release") s_clientOwnRequested = false;
            rpc.InvokeRoutedRPC(RpcRequest, id, cmd, colony, arg ?? string.Empty);
            return id;
        }

        public static float SentAt(int id) => s_sentAt.TryGetValue(id, out float t) ? t : 0f;

        public static bool TryGet(int id, out ServerState state) => s_answers.TryGetValue(id, out state);

        /// <summary>Client: true while this peer asked the server to own a colony and has not released it.</summary>
        public static bool ClientHoldsRequest => s_clientOwnRequested;

        /// <summary>
        /// Called by the scenario runner when a scenario ends or is cut: a client that left the server holding a colony releases it (the colony is destroyed on the server, which gets its
        /// reference position and gathering back). No-op when nothing was asked.
        /// </summary>
        public static void ReleaseIfActive()
        {
            if (!s_clientOwnRequested) return;
            try { Request("release", ZDOID.None); }
            catch (Exception e) { Plugin.Log.LogWarning("[Dev] serverown release: " + e.Message); }
            s_clientOwnRequested = false;
        }

        private static void OnState(long sender, ZPackage pkg)
        {
            try
            {
                var st = new ServerState
                {
                    Id = pkg.ReadInt(),
                    Cmd = pkg.ReadString(),
                    Error = pkg.ReadString(),
                    Dedicated = pkg.ReadBool(),
                    Active = pkg.ReadBool(),
                    ServerTime = pkg.ReadDouble(),
                    KnowsZdo = pkg.ReadBool(),
                    ZdoOwner = pkg.ReadLong(),
                    SnapshotBytes = pkg.ReadInt(),
                    HasColony = pkg.ReadBool(),
                    ColonyOwner = pkg.ReadBool(),
                    Alive = pkg.ReadInt(),
                    Awake = pkg.ReadInt(),
                    SimLastMs = pkg.ReadSingle(),
                    SimAvgMs = pkg.ReadSingle(),
                    SimMaxMs = pkg.ReadSingle(),
                    SimPeakMs = pkg.ReadSingle(),
                    SentBytesPerSec = pkg.ReadDouble(),
                    SentBytes = pkg.ReadLong(),
                    SentPackets = pkg.ReadInt(),
                    Keyframes = pkg.ReadInt(),
                    EventsSent = pkg.ReadInt(),
                    Recipients = pkg.ReadInt(),
                    BadPackets = pkg.ReadInt(),
                    SendAvgMs = pkg.ReadSingle(),
                    HitsReceived = pkg.ReadInt(),
                    ArrivedAt = Time.time,
                };
                int masses = pkg.ReadInt();
                for (int k = 0; k < masses; k++)
                    st.Masses.Add(new MassInfo { Id = pkg.ReadZDOID(), N = pkg.ReadInt(), Health = pkg.ReadSingle(), Owner = pkg.ReadBool() });
                int n = pkg.ReadInt();
                st.WormCount = n;
                st.WormId = new int[n];
                st.WormPos = new Vector3[n];
                st.WormState = new int[n];
                st.WormHp = new float[n];
                for (int i = 0; i < n; i++)
                {
                    st.WormId[i] = pkg.ReadInt();
                    st.WormPos[i] = pkg.ReadVector3();
                    st.WormState[i] = pkg.ReadInt();
                    st.WormHp[i] = pkg.ReadSingle();
                }
                if (s_answers.Count > 64) s_answers.Clear();
                s_answers[st.Id] = st;
            }
            catch (Exception e)
            {
                Plugin.LogException("ServerOwnDev.OnState", e);
            }
        }
    }
}
#endif
