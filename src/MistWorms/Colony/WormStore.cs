using System;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>
    /// Structure-of-arrays storage of the virtual worms of one colony (spec §10.1, §11.1). Everything is allocated
    /// once at the colony's capacity; <see cref="Alloc"/> and <see cref="Free"/> never allocate. Pure data, no game
    /// access, so it is unit tested. A slot index is only meaningful together with its <see cref="generation"/>.
    /// </summary>
    internal sealed class WormStore
    {
        public readonly int Capacity;

        /// <summary>1 + the highest slot ever handed out; loops over the store stop here.</summary>
        public int HighWater { get; private set; }
        public int AliveCount { get; private set; }

        // ---- transform and state
        public readonly Vector3[] pos;
        public readonly Vector3[] prevPos;        // position at the start of the last simulation step (render interpolation)
        public readonly float[] heading;          // yaw in radians, forward = (sin h, 0, cos h)
        public readonly Vector3[] groundNormal;
        public readonly byte[] state;             // WormState
        public readonly float[] stateTime;        // seconds in the current state, as of lastSimTime
        public readonly byte[] subPhase;          // StrikePhase while Striking
        public readonly float[] hp;
        public readonly short[] nestIndex;        // -1 = none (nests arrive in phase 7)
        public readonly byte[] targetKind;        // TargetKind
        public readonly long[] targetId;          // player id for TargetKind.Player, 0 = none
        public readonly int[] poolIndex;          // -1 in phase 3 (loot pool arrives in phase 6)
        public readonly bool[] alive;
        public readonly ushort[] generation;      // bumped on every reuse of the slot (proxies, phase 3B)

        // ---- movement and wandering
        public readonly Vector3[] home;
        public readonly Vector3[] wanderGoal;
        public readonly float[] goalTime;         // when wanderGoal was chosen, 0 = no goal
        public readonly float[] pauseUntil;
        public readonly Vector3[] avoidDir;
        public readonly float[] nextAvoidTime;
        public readonly float[] nextGroundTime;
        public readonly Vector3[] progressPos;    // stuck detection: where the worm was progressTime ago
        public readonly float[] progressTime;
        public readonly Vector3[] fleeFrom;
        public readonly float[] animTime;         // local, never synchronized

        // ---- scheduling and perception
        public readonly float[] nextSimTime;
        public readonly float[] lastSimTime;
        public readonly float[] simInterval;      // seconds between two steps, for render interpolation
        public readonly float[] nextSenseTime;
        public readonly float[] lastSeen;
        public readonly bool[] hasLos;            // line of sight to the target at the last sense

        // ---- paths
        public readonly int[] pathSlot;           // -1 = none
        public readonly int[] pathCursor;
        public readonly float[] pathExpire;
        public readonly float[] nextPathTime;

        private readonly int[] _free;
        private int _freeCount;

        public WormStore(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;

            pos = new Vector3[capacity];
            prevPos = new Vector3[capacity];
            heading = new float[capacity];
            groundNormal = new Vector3[capacity];
            state = new byte[capacity];
            stateTime = new float[capacity];
            subPhase = new byte[capacity];
            hp = new float[capacity];
            nestIndex = new short[capacity];
            targetKind = new byte[capacity];
            targetId = new long[capacity];
            poolIndex = new int[capacity];
            alive = new bool[capacity];
            generation = new ushort[capacity];

            home = new Vector3[capacity];
            wanderGoal = new Vector3[capacity];
            goalTime = new float[capacity];
            pauseUntil = new float[capacity];
            avoidDir = new Vector3[capacity];
            nextAvoidTime = new float[capacity];
            nextGroundTime = new float[capacity];
            progressPos = new Vector3[capacity];
            progressTime = new float[capacity];
            fleeFrom = new Vector3[capacity];
            animTime = new float[capacity];

            nextSimTime = new float[capacity];
            lastSimTime = new float[capacity];
            simInterval = new float[capacity];
            nextSenseTime = new float[capacity];
            lastSeen = new float[capacity];
            hasLos = new bool[capacity];

            pathSlot = new int[capacity];
            pathCursor = new int[capacity];
            pathExpire = new float[capacity];
            nextPathTime = new float[capacity];

            // Lowest slot on top so a fresh store fills 0, 1, 2...
            _free = new int[capacity];
            for (int i = 0; i < capacity; i++) _free[i] = capacity - 1 - i;
            _freeCount = capacity;
        }

        /// <summary>Takes a free slot and resets it; returns -1 when the store is full.</summary>
        public int Alloc(Vector3 position, float headingRad, WormState initialState, float health, float now)
        {
            if (_freeCount == 0) return -1;
            int i = _free[--_freeCount];

            unchecked { generation[i]++; }
            alive[i] = true;
            AliveCount++;
            if (i >= HighWater) HighWater = i + 1;

            pos[i] = position;
            prevPos[i] = position;
            heading[i] = headingRad;
            groundNormal[i] = Vector3.up;
            state[i] = (byte)initialState;
            stateTime[i] = 0f;
            subPhase[i] = 0;
            hp[i] = health;
            nestIndex[i] = -1;
            targetKind[i] = (byte)TargetKind.None;
            targetId[i] = 0;
            poolIndex[i] = -1;

            home[i] = position;
            wanderGoal[i] = position;
            goalTime[i] = 0f;
            pauseUntil[i] = 0f;
            avoidDir[i] = Vector3.zero;
            nextAvoidTime[i] = 0f;
            nextGroundTime[i] = now;
            progressPos[i] = position;
            progressTime[i] = now;
            fleeFrom[i] = position;
            animTime[i] = 0f;

            lastSimTime[i] = now;
            nextSimTime[i] = now;
            simInterval[i] = 0.05f;
            nextSenseTime[i] = now;
            lastSeen[i] = now;
            hasLos[i] = false;

            pathSlot[i] = -1;
            pathCursor[i] = 0;
            pathExpire[i] = 0f;
            nextPathTime[i] = 0f;
            return i;
        }

        /// <summary>Releases a slot. A dead or already free slot is ignored.</summary>
        public void Free(int i)
        {
            if ((uint)i >= (uint)Capacity || !alive[i]) return;
            alive[i] = false;
            state[i] = (byte)WormState.Dead;
            AliveCount--;
            _free[_freeCount++] = i;
        }

        public bool IsAlive(int i, ushort gen) => (uint)i < (uint)Capacity && alive[i] && generation[i] == gen;
    }
}
