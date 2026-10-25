using System.Collections.Generic;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>
    /// Perception, fire fear and path requests of the virtual worms (spec §3.1, §5.3, §9.1, §11.1). Same allocation rules
    /// as WormSim.cs: the player and area lists are the game's own, the path buffers are fixed.
    /// </summary>
    internal sealed partial class WormColony
    {
        private const float SenseInterval = 0.5f;        // spec §11.1: one test per worm per 0.5 s
        private const float HuntRange = 12f;             // spec §5.3: target choice range of a lone worm
        private const float ForgetTime = 5f;             // target unseen for this long is dropped
        private const float FireInterval = 0.5f;         // spec §9.1 reading of EffectArea.GetAllAreas
        private const int MaxFireColliders = 16;
        private const int MaxTorchHolders = 8;

        private const int PathSlots = 32;                // pool of 32 paths of 24 points (plan A3)
        private const int PathPoints = 24;
        private const float PathBudgetPerSecond = 8f;    // spec §11.1: 8 requests/s per colony
        private const float PathCacheSeconds = 10f;
        private const float PathRetrySeconds = 2f;       // a failed request (navmesh not built yet) is retried after 2 s
        private const float WaypointReach = 0.5f;

        private List<Player> _players = new List<Player>();
        private readonly TargetScanner _scanner = new TargetScanner();
        private readonly Collider[] _fire = new Collider[MaxFireColliders];
        private int _fireCount;
        private float _nextFireTime;
        private readonly Humanoid[] _torchHolders = new Humanoid[MaxTorchHolders];   // humanoids with a lit torch near the box (plan 5, A16)
        private int _torchCount;

        private Vector3[] _pathPoints;
        private int[] _pathLength;
        private int[] _pathFree;
        private int _pathFreeCount;
        private float _pathTokens;
        private int _pathsThisTick;
        private const int PathsPerTick = 2;              // a burst of requests never lands in one frame
        private readonly List<Vector3> _pathScratch = new List<Vector3>(64);

        // ------------------------------------------------------------------ targets

        /// <summary>Alive, visible to monsters and not mid-teleport (spec §5.3, decisions 18). Ghost and fly mode are ignored.</summary>
        private static bool Targetable(Player p)
        {
            return p != null && !p.IsDead() && !p.InGhostMode() && !p.IsDebugFlying() && !p.IsTeleporting();
        }

        private Player FindPlayer(long id)
        {
            if (id == 0) return null;
            for (int k = 0; k < _players.Count; k++)
            {
                Player p = _players[k];
                if (p != null && p.GetPlayerID() == id) return p;
            }
            return null;
        }

        /// <summary>The current target of worm <paramref name="i"/> as a ZDOID, for a creature, a tamed animal or a structure; ZDOID.None otherwise.</summary>
        private ZDOID ObjectTargetId(int i)
        {
            WormStore s = Store;
            TargetKind kind = (TargetKind)s.targetKind[i];
            return kind == TargetKind.Tamed || kind == TargetKind.Creature || kind == TargetKind.Structure ? new ZDOID(s.targetZdoUser[i], s.targetZdoId[i]) : ZDOID.None;
        }

        /// <summary>
        /// Perception of a worm (spec §3.1, §5.3): a player is sensed when heard (BaseAI.CanHearTarget rule) or seen (range, angle,
        /// no view blocker). Creatures, tamed animals and structures are chosen by <see cref="TargetScanner"/> (3D distance, line of sight, Targets.*). The
        /// lowest distance x weight within the hunt range becomes the target; the current target is kept while perceived at any range, and forgotten after
        /// 5 s unseen or beyond twice the view range. Targets.ExcludedPrefabs applies to every kind.
        /// Returns whether the worm has a target afterwards.
        /// </summary>
        private bool SenseTarget(int i, float now)
        {
            WormStore s = Store;
            bool players = MWConfig.Targets.AttackPlayers.Value;
            bool objects = MWConfig.Targets.AttackTamed.Value || MWConfig.Targets.AttackCreatures.Value || MWConfig.Targets.AttackStructures.Value;
            if (!players && !objects)
            {
                if (s.targetKind[i] != (byte)TargetKind.None) ClearTarget(i);
                return false;
            }
            TargetRules.Excluded.Refresh(MWConfig.Targets.ExcludedPrefabs.Value);
            if (KeepObstacle(i, now)) return true;   // the door a living target hides behind is gnawed until it falls (decision 164)

            Vector3 pos = s.pos[i];
            Vector3 eye = pos + Vector3.up * EyeHeight;
            TargetKind currentKind = (TargetKind)s.targetKind[i];
            long current = currentKind == TargetKind.Player ? s.targetId[i] : 0;
            float viewRange = MWConfig.Worm.ViewRange.Value;
            float viewAngle = MWConfig.Worm.ViewAngle.Value;
            float hearRange = MWConfig.Worm.HearRange.Value;
            float weight = MWConfig.Targets.PlayerWeight.Value;
            bool interior = Character.InInterior(pos);

            Player best = null;
            float bestScore = float.MaxValue;
            Vector3 bestCenter = default;

            for (int k = 0; players && k < _players.Count; k++)
            {
                Player p = _players[k];
                if (!Targetable(p) || TargetScanner.IsExcluded(p)) continue;

                bool isCurrent = current != 0 && p.GetPlayerID() == current;
                Vector3 center = p.GetCenterPoint();
                float d = Vector3.Distance(eye, center);

                bool heard = WormMath.CanHear(Vector3.Distance(p.transform.position, pos), hearRange, interior, p.GetNoiseRange());
                bool seen = false;
                if (!heard && d <= viewRange
                    && WormMath.InViewAngle(s.heading[i], center.x - pos.x, center.z - pos.z, viewAngle))
                {
                    seen = !Physics.Linecast(eye, p.GetEyePoint(), WormPhysics.ViewBlockMask, QueryTriggerInteraction.Ignore);
                }
                if (!heard && !seen) continue;
                if (!isCurrent && d > HuntRange) continue;

                float score = d * weight;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = p;
                    bestCenter = center;
                }
            }

            // A creature or a tamed animal that beats the best player; a structure only when no player and no creature is perceived (decision 164).
            if (objects && _scanner.Pick(pos, eye, Mathf.Min(HuntRange, viewRange), bestScore, ObjectTargetId(i), null, now,
                    TargetReach.WormVerticalReach(EyeHeight, MWConfig.Worm.CobraRange.Value), _blacklist, i,
                    TargetRules.FallbackDelayElapsed(now, _livingSeen[i], ForgetTime), out TargetChoice pick))
            {
                ZDOID id = pick.Id;
                if (id != ZDOID.None)
                {
                    s.SetObjectTarget(i, pick.Kind, id.UserID, id.ID);
                    s.lastSeen[i] = now;
                    if (pick.Kind != TargetKind.Structure) _livingSeen[i] = now;
                    s.hasLos[i] = true;   // the pick is a line of sight
                    return true;
                }
            }

            if (best != null)
            {
                s.SetPlayerTarget(i, best.GetPlayerID());
                s.lastSeen[i] = now;
                _livingSeen[i] = now;
                s.hasLos[i] = !Physics.Linecast(eye, bestCenter, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore);
                return true;
            }

            if (currentKind != TargetKind.None)
            {
                if (!TargetStillValid(i, currentKind, now, viewRange))
                {
                    ClearTarget(i);
                    return false;
                }
                return true; // not perceived this time, but not forgotten yet
            }
            return false;
        }

        /// <summary>The current target still exists, is alive and was perceived less than ForgetTime ago, within twice the view range.</summary>
        private bool TargetStillValid(int i, TargetKind kind, float now, float viewRange)
        {
            WormStore s = Store;
            if (now - s.lastSeen[i] > ForgetTime) return false;
            if (kind == TargetKind.Player)
            {
                Player held = FindPlayer(s.targetId[i]);
                return held != null && Targetable(held) && Vector3.Distance(held.transform.position, s.pos[i]) <= 2f * viewRange;
            }
            if (!ResolveTarget(i, out TargetView view)) return false;
            return Vector3.Distance(view.Position, s.pos[i]) <= 2f * viewRange;
        }

        // ------------------------------------------------------------------ fire (spec §9.1, decisions 16)

        /// <summary>
        /// Reads the fire areas once per 0.5 s and keeps the active ones whose collider is near the box, and the humanoids (players included) that hold
        /// a lit torch near it (the mod's own criterion, <see cref="FireSources.HoldsTorch"/>; their position is read when a worm is tested).
        /// </summary>
        private void RefreshFire(float now)
        {
            if (now < _nextFireTime) return;
            _nextFireTime = now + FireInterval;
            _fireCount = 0;
            _torchCount = 0;
            if (!MWConfig.Fire.AfraidOfFire.Value) return;

            float radius = MWConfig.Fire.FearRadius.Value;
            Bounds torchBox = _boxBounds;
            torchBox.Expand(radius * 2f);
            List<Character> everyone = Character.GetAllCharacters();
            for (int k = 0; k < everyone.Count && _torchCount < MaxTorchHolders; k++)
            {
                if (everyone[k] is Humanoid h && FireSources.HoldsTorch(h) && torchBox.Contains(h.transform.position)) _torchHolders[_torchCount++] = h;
            }
            List<EffectArea> areas = EffectArea.GetAllAreas();
            for (int k = 0; k < areas.Count && _fireCount < MaxFireColliders; k++)
            {
                EffectArea area = areas[k];
                // GetAllAreas also lists inactive zones: filter on the live object.
                if (area == null || !area.isActiveAndEnabled || (area.m_type & EffectArea.Type.Fire) == 0) continue;
                Collider c = area.m_collider;
                if (c == null || !c.enabled) continue;
                Bounds b = c.bounds;
                b.Expand(radius * 2f);
                if (!_boxBounds.Intersects(b)) continue;
                _fire[_fireCount++] = c;
            }
        }

        /// <summary>
        /// True when a fire area is within Fire.FearRadius of the worm (distance to the collider surface); records the closest point as
        /// the flee origin. Unless <paramref name="alreadyFleeing"/>, the target is dropped: the caller then switches to Fleeing.
        /// </summary>
        private bool FireCheck(int i, bool alreadyFleeing)
        {
            if (_fireCount == 0 && _torchCount == 0) return false;
            WormStore s = Store;
            Vector3 p = s.pos[i];
            float radius = MWConfig.Fire.FearRadius.Value;
            float r2 = radius * radius;

            for (int k = 0; k < _torchCount; k++)
            {
                Humanoid h = _torchHolders[k];
                if (h == null || !FireSources.HoldsTorch(h)) continue;
                Vector3 hp = h.transform.position;
                if ((hp - p).sqrMagnitude > r2) continue;
                s.fleeFrom[i] = hp;
                if (!alreadyFleeing) ClearTarget(i);
                return true;
            }

            for (int k = 0; k < _fireCount; k++)
            {
                Collider c = _fire[k];
                if (c == null) continue;
                // ClosestPoint is only valid on convex colliders; fall back to the bounds for a concave mesh.
                Vector3 closest = c is MeshCollider mc && !mc.convex ? c.bounds.ClosestPoint(p) : c.ClosestPoint(p);
                if ((closest - p).sqrMagnitude > r2) continue;
                s.fleeFrom[i] = closest;
                if (!alreadyFleeing) ClearTarget(i);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ paths (spec §11.1)

        private void InitPaths()
        {
            _pathPoints = new Vector3[PathSlots * PathPoints];
            _pathLength = new int[PathSlots];
            _pathFree = new int[PathSlots];
            for (int k = 0; k < PathSlots; k++) _pathFree[k] = PathSlots - 1 - k;
            _pathFreeCount = PathSlots;
            _pathTokens = PathBudgetPerSecond;
        }

        private void RefreshPathBudget(float now)
        {
            _pathTokens = Mathf.Min(PathBudgetPerSecond, _pathTokens + (now - _lastTickTime) * PathBudgetPerSecond);
        }

        private void ReleasePath(int i)
        {
            WormStore s = Store;
            int slot = s.pathSlot[i];
            if (slot < 0) return;
            s.pathSlot[i] = -1;
            _pathFree[_pathFreeCount++] = slot;
        }

        /// <summary>
        /// Asks the vanilla pathfinder for a route (Humanoid agent, the smallest walker) within the colony budget and the per-worm
        /// cache. Failures (no budget, no free slot, navmesh not built yet) simply leave the worm steering straight.
        /// </summary>
        private void TryRequestPath(int i, Vector3 target, float now)
        {
            WormStore s = Store;
            if (s.pathSlot[i] >= 0 && now < s.pathExpire[i]) return;   // cached
            ReleasePath(i);
            if (now < s.nextPathTime[i] || _pathTokens < 1f || _pathFreeCount == 0 || _pathsThisTick >= PathsPerTick) return;
            if (Pathfinding.instance == null) return;

            _pathTokens -= 1f;
            _pathsThisTick++;
#if MW_DEV
            _framePaths++;
#endif
            bool ok = Pathfinding.instance.GetPath(s.pos[i], target, _pathScratch, Pathfinding.AgentType.Humanoid,
                requireFullPath: false, cleanup: true);
#if MW_DEV
            CountPath(ok && _pathScratch.Count >= 2, ok && _pathScratch.Count > 0 ? _pathScratch[_pathScratch.Count - 1] : target, target);
#endif
            if (!ok || _pathScratch.Count < 2)
            {
                s.nextPathTime[i] = now + PathRetrySeconds;
                return;
            }

            int slot = _pathFree[--_pathFreeCount];
            int count = Mathf.Min(_pathScratch.Count, PathPoints); // a longer route is followed up to its 24th point, then re-requested
            int baseIndex = slot * PathPoints;
            for (int k = 0; k < count; k++) _pathPoints[baseIndex + k] = _pathScratch[k];
            _pathLength[slot] = count;
            s.pathSlot[i] = slot;
            s.pathCursor[i] = 1; // point 0 is where the worm already stands
            s.pathExpire[i] = now + PathCacheSeconds;
        }

        /// <summary>The waypoint to steer to while a cached path lasts, otherwise <paramref name="goal"/>.</summary>
        private Vector3 FollowPath(int i, Vector3 goal)
        {
            WormStore s = Store;
            int slot = s.pathSlot[i];
            if (slot < 0) return goal;
            if (Time.time >= s.pathExpire[i])
            {
                ReleasePath(i);
                return goal;
            }

            int count = _pathLength[slot];
            int baseIndex = slot * PathPoints;
            int cursor = s.pathCursor[i];
            Vector3 p = s.pos[i];
            while (cursor < count)
            {
                Vector3 d = _pathPoints[baseIndex + cursor] - p;
                d.y = 0f;
                if (d.sqrMagnitude >= WaypointReach * WaypointReach) break;
                cursor++;
            }
            s.pathCursor[i] = cursor;
            if (cursor >= count)
            {
                ReleasePath(i);
                return goal;
            }
            return _pathPoints[baseIndex + cursor];
        }
    }
}
