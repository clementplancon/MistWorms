#if MW_DEV
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MistWorms.Colony;
using UnityEngine;

namespace MistWorms.Mass
{
    /// <summary>
    /// Dev probe of the vanilla walk (decision 269 follow-up: why some passes of the mass walk slowly). Armed by a scenario (S06, debug=1) through <see cref="Arm"/>;
    /// WritheMassAI.WalkTo then calls <see cref="Tick"/>. Every 0.25 s one sample goes in a preallocated buffer (no allocation per frame); the scenario drains it.
    /// Observation only: nothing here touches the mass.
    /// </summary>
    internal static class MassPathProbe
    {
        internal struct Sample
        {
            public float T, NodeErrDeg, NodeDist, PathLen, Bird, VelAngle, PlayerMove, GroundNy, OverAngle;
            public int PathCount, FindPaths, Removed;
            public byte State;
        }

        private const float Period = 0.25f;
        private static readonly FieldInfo PathField = AccessTools.Field(typeof(BaseAI), "m_path");
        private static readonly FieldInfo FindTimeField = AccessTools.Field(typeof(BaseAI), "m_lastFindPathTime");

        public static readonly Sample[] Buffer = new Sample[512];
        public static readonly float[] FindTimes = new float[128];
        public static int Count;
        public static int FindPathCalls;
        public static float InitialPathLen = -1f;

        private static WritheMassAI _ai;
        private static Rigidbody _rb;
        private static float _t0, _next, _lastFind, _overAcc;
        private static int _prevCount, _removed, _findAcc;
        private static Vector3 _playerLast;
        private static bool _havePlayer;

        public static bool Armed => _ai != null;

        public static void Arm(WritheMassAI ai)
        {
            _ai = ai;
            _rb = ai != null ? ai.GetComponent<Rigidbody>() : null;
            Count = 0;
            FindPathCalls = 0;
            InitialPathLen = -1f;
            _t0 = Time.time;
            _next = _t0;
            _lastFind = FindTimeField != null && ai != null ? (float)FindTimeField.GetValue(ai) : 0f;
            _prevCount = 0;
            _removed = 0;
            _findAcc = 0;
            _overAcc = 0f;
            _havePlayer = false;
        }

        public static void Disarm() => _ai = null;

        public static void Tick(WritheMassAI ai, float dt, Vector3 goal, float moveMinAngle, byte state)
        {
            if (_ai == null || ai != _ai || PathField == null) return;
            var path = PathField.GetValue(ai) as List<Vector3>;
            int count = path != null ? path.Count : 0;
            float find = (float)FindTimeField.GetValue(ai);
            Vector3 pos = ai.transform.position;
            bool found = find != _lastFind;
            if (found)
            {
                _lastFind = find;
                if (FindPathCalls < FindTimes.Length) FindTimes[FindPathCalls] = Time.time - _t0;
                FindPathCalls++;
                _findAcc++;
                _prevCount = count;
                if (InitialPathLen < 0f && count > 0) InitialPathLen = PathLength(path, pos);
            }
            else
            {
                if (count < _prevCount) _removed += _prevCount - count;
                _prevCount = count;
                if (InitialPathLen < 0f && count > 0) InitialPathLen = PathLength(path, pos);
            }

            Vector3 toPlayer = goal - pos;
            toPlayer.y = 0f;
            Vector3 toNode = count > 0 ? path[0] - pos : toPlayer;
            toNode.y = 0f;
            Vector3 fwd = ai.transform.forward;
            fwd.y = 0f;
            if (toNode.sqrMagnitude > 1e-4f && fwd.sqrMagnitude > 1e-4f && Vector3.Angle(fwd, toNode) > moveMinAngle) _overAcc += dt;

            if (Time.time < _next) return;
            _next = Time.time + Period;
            if (Count >= Buffer.Length) return;

            Vector3 vel = _rb != null ? _rb.linearVelocity : Vector3.zero;
            vel.y = 0f;
            float playerMove = 0f;
            Player p = Player.m_localPlayer;
            if (p != null)
            {
                Vector3 pp = p.transform.position;
                if (_havePlayer) playerMove = Utils.DistanceXZ(pp, _playerLast);
                _playerLast = pp;
                _havePlayer = true;
            }
            float ny = Physics.Raycast(pos + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 4f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore) ? hit.normal.y : -1f;

            Buffer[Count++] = new Sample
            {
                T = Time.time - _t0,
                NodeErrDeg = count > 0 && toPlayer.sqrMagnitude > 1e-4f && toNode.sqrMagnitude > 1e-4f ? Vector3.Angle(toNode, toPlayer) : -1f,
                NodeDist = count > 0 ? Utils.DistanceXZ(path[0], pos) : -1f,
                PathCount = count,
                PathLen = count > 0 ? PathLength(path, pos) : -1f,
                Bird = toPlayer.magnitude,
                VelAngle = vel.sqrMagnitude > 0.01f && toPlayer.sqrMagnitude > 1e-4f ? Vector3.Angle(vel, toPlayer) : -1f,
                FindPaths = _findAcc,
                Removed = _removed,
                OverAngle = _overAcc,
                PlayerMove = playerMove,
                State = state,
                GroundNy = ny,
            };
            _findAcc = 0;
            _removed = 0;
        }

        private static float PathLength(List<Vector3> path, Vector3 from)
        {
            float len = 0f;
            Vector3 prev = from;
            for (int i = 0; i < path.Count; i++)
            {
                len += (path[i] - prev).magnitude;
                prev = path[i];
            }
            return len;
        }
    }
}
#endif
