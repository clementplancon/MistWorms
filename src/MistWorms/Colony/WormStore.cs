using System;
using MistWorms.Drops;
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

        /// <summary>The pools of the worms that ate something; a worm points to its pool with <see cref="poolIndex"/>. Freed with the slot.</summary>
        public readonly PoolTable Pools;

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
        public readonly short[] nestIndex;        // -1 = none; the nest a worm was laid or seeded at, or the nearest one for a lone worm
        public readonly bool[] lone;              // a lone worm of the seeding: attached to its nearest nest for the alert only, never counted in the cap of that nest (spec 8.1)
        public readonly byte[] targetKind;        // TargetKind
        public readonly long[] targetId;          // player id for TargetKind.Player, 0 = none; any other kind: a non zero key of the object (see SetObjectTarget)
        public readonly long[] targetZdoUser;     // ZDOID.UserID of a Tamed / Creature / Structure target (spec §11.3), 0 otherwise
        public readonly uint[] targetZdoId;       // ZDOID.ID of it
        public readonly int[] poolIndex;          // index in Pools, -1 while the worm has eaten nothing (spec 7.1)
        public readonly bool[] alive;
        public readonly ushort[] generation;      // bumped on every reuse of the slot (proxies, phase 3B)
        public readonly int[] proxyIndex;         // pool index of the hit proxy attached to the slot, -1 = none (owned by ProxyManager)

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
        public readonly float[] stillSince;       // last time the worm really moved (> 1 mm in a step); local, never synchronized (idle pose)

        // ---- group behaviour (local, never synchronized)
        public readonly float[] lastSpeed;        // m/s over the last step (file: the worm behind never outruns it)
        public readonly int[] approachRank;       // rank among the hunters of approachTarget, -1 = none yet
        public readonly long[] approachTarget;
        public readonly float[] approachBase;     // bearing from the target at which the first hunter of the wave arrived (rad)
        public readonly Vector3[] approachOffset; // approach point relative to the target, zero = aim at the target itself
        public readonly float[] approachNext;     // when the approach point is evaluated next
        public readonly byte[] approachMode;      // 0 = not evaluated yet, 1 = a validated place, 2 = no valid place (aims at the target itself)

        // ---- masses (phase 4B-4, local, never synchronized)
        public readonly ZDOID[] massRef;          // the mass an Absorbing worm climbs onto
        public readonly Vector3[] absorbFrom;     // where the absorption (or the leap of a dissolved worm) started
        public readonly float[] leapHeight;       // apex of the leap of a dissolved worm, metres (subPhase 1 while Fleeing)
        public readonly float[] directUntil;      // until then the worm steers by its goal only (a gatherer that made no progress)

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
            Pools = new PoolTable(capacity);

            pos = new Vector3[capacity];
            prevPos = new Vector3[capacity];
            heading = new float[capacity];
            groundNormal = new Vector3[capacity];
            state = new byte[capacity];
            stateTime = new float[capacity];
            subPhase = new byte[capacity];
            hp = new float[capacity];
            nestIndex = new short[capacity];
            lone = new bool[capacity];
            targetKind = new byte[capacity];
            targetId = new long[capacity];
            targetZdoUser = new long[capacity];
            targetZdoId = new uint[capacity];
            poolIndex = new int[capacity];
            alive = new bool[capacity];
            generation = new ushort[capacity];
            proxyIndex = new int[capacity];

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
            stillSince = new float[capacity];

            lastSpeed = new float[capacity];
            approachRank = new int[capacity];
            approachTarget = new long[capacity];
            approachBase = new float[capacity];
            approachOffset = new Vector3[capacity];
            approachNext = new float[capacity];
            approachMode = new byte[capacity];

            massRef = new ZDOID[capacity];
            absorbFrom = new Vector3[capacity];
            leapHeight = new float[capacity];
            directUntil = new float[capacity];

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
            lone[i] = false;
            targetKind[i] = (byte)TargetKind.None;
            targetId[i] = 0;
            targetZdoUser[i] = 0;
            targetZdoId[i] = 0;
            poolIndex[i] = -1;
            proxyIndex[i] = -1;

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
            stillSince[i] = now;

            lastSpeed[i] = 0f;
            approachRank[i] = -1;
            approachTarget[i] = 0;
            approachBase[i] = 0f;
            approachOffset[i] = Vector3.zero;
            approachNext[i] = 0f;
            approachMode[i] = 0;

            massRef[i] = ZDOID.None;
            absorbFrom[i] = position;
            leapHeight[i] = 0f;
            directUntil[i] = 0f;

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

        /// <summary>Hunts a player (spec §5.3): the player id is the target key.</summary>
        public void SetPlayerTarget(int i, long playerId)
        {
            targetKind[i] = (byte)TargetKind.Player;
            targetId[i] = playerId;
            targetZdoUser[i] = 0;
            targetZdoId[i] = 0;
        }

        /// <summary>Hunts a creature, a tamed animal or a structure (spec §11.3): the ZDOID is stored as (user, id) and <see cref="targetId"/> gets a non zero key of it.</summary>
        public void SetObjectTarget(int i, TargetKind kind, long zdoUser, uint zdoId)
        {
            targetKind[i] = (byte)kind;
            targetZdoUser[i] = zdoUser;
            targetZdoId[i] = zdoId;
            targetId[i] = TargetRules.ObjectKey(zdoUser, zdoId);
        }

        /// <summary>No target.</summary>
        public void ClearTarget(int i)
        {
            targetKind[i] = (byte)TargetKind.None;
            targetId[i] = 0;
            targetZdoUser[i] = 0;
            targetZdoId[i] = 0;
        }

        /// <summary>Releases a slot. A dead or already free slot is ignored.</summary>
        public void Free(int i)
        {
            if ((uint)i >= (uint)Capacity || !alive[i]) return;
            if (poolIndex[i] >= 0) { Pools.Release(poolIndex[i]); poolIndex[i] = -1; }
            alive[i] = false;
            state[i] = (byte)WormState.Dead;
            AliveCount--;
            _free[_freeCount++] = i;
        }

        public bool IsAlive(int i, ushort gen) => (uint)i < (uint)Capacity && alive[i] && generation[i] == gen;
    }
}
