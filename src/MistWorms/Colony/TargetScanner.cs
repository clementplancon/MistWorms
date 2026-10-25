using System;
using System.Collections.Generic;
using MistWorms.Config;
using MistWorms.Mass;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>What a scan chose (spec §5.3). The player candidates are handled by the callers, which keep their own hearing rules.</summary>
    internal struct TargetChoice
    {
        public bool Found;
        public TargetKind Kind;
        public ZDOID Id;
        public Component Object;      // the Character of a Creature / Tamed target, the WearNTear or Destructible of a Structure
        public Character Character;   // Creature / Tamed
        public Collider Collider;     // Structure: the collider closest to the scan origin
        public Vector3 Center;
        public float Distance;
        public float Score;
    }

    /// <summary>
    /// Target choice of the free worms and the masses among the creatures, the tamed animals and the structures (spec §5.3, plan 6, A3, A12). 3D distance to
    /// the closest point (a Character: its centre, a structure: its closest collider), score = distance x weight (the living targets among themselves; a structure only as a fallback or an obstacle, decision 164), then a <c>ViewBlockMask</c> line cast from the eye
    /// to the centre of the best candidates in order of score (the target's own colliders do not block). Targets.Attack*, Targets.*Weight and
    /// Targets.ExcludedPrefabs apply to every kind; nests, worms, masses, tombstones, portals and indestructible pieces are never candidates. Vanilla
    /// <c>FindEnemy</c> is not used (no weights). A candidate must also be reachable (vertical gap within the real reach of the attacker) and not blacklisted
    /// (decision 163). No allocation: reused arrays; the structures come from small scan results shared by the worms standing close together (one overlap per
    /// 3 m cell per second instead of one per worm per 0.5 s), each keeping the <see cref="MaxPerScan"/> nearest pieces.
    /// </summary>
    internal sealed class TargetScanner
    {
        private const int MaxCandidates = 112;
        private const int MaxCharacters = 64;
        private const int MaxPerScan = 48;
        private const int ScanSlots = 4;
        private const int MaxColliders = 512;
        private const float ReuseRadius = 3f;       // a scan serves worms standing within this of its centre...
        private const float ReuseSeconds = 1f;      // ...for this long
        private const int MaxLineCasts = 4;
        private const float IndestructibleHealth = 100000f;

        // Class of a prefab, by its ZDO prefab hash: 0 = never a target, 1 = a WearNTear piece, 2 = a dvergr Destructible. Computed once per prefab.
        private static readonly Dictionary<int, byte> s_class = new Dictionary<int, byte>();
        // Prefabs of a mod object (ours MW_, SkyWhale's SW_) by ZDO prefab hash: never targeted (spec §10.4).
        private static readonly Dictionary<int, bool> s_modObject = new Dictionary<int, bool>();
        private static int s_structureMask;

        // The candidates of the call in progress.
        private readonly byte[] _kind = new byte[MaxCandidates];
        private readonly Component[] _obj = new Component[MaxCandidates];
        private readonly Character[] _char = new Character[MaxCandidates];
        private readonly Collider[] _col = new Collider[MaxCandidates];
        private readonly Vector3[] _center = new Vector3[MaxCandidates];
        private readonly Vector3[] _near = new Vector3[MaxCandidates];   // a structure: the point of its collider closest to the attacker
        private readonly float[] _dist = new float[MaxCandidates];
        private readonly float[] _score = new float[MaxCandidates];
        private readonly bool[] _used = new bool[MaxCandidates];

        // Structure scans.
        private static readonly RaycastHit[] s_cast = new RaycastHit[32];
        private static readonly Collider[] s_hits = new Collider[MaxColliders];   // scans never nest: one buffer for every scanner
        private readonly float[] _sDist = new float[ScanSlots * MaxPerScan];
        private readonly Component[] _sObj = new Component[ScanSlots * MaxPerScan];
        private readonly Collider[] _sCol = new Collider[ScanSlots * MaxPerScan];
        private readonly int[] _sCount = new int[ScanSlots];
        private readonly Vector3[] _sCenter = new Vector3[ScanSlots];
        private readonly float[] _sRadius = new float[ScanSlots];
        private readonly float[] _sTime = new float[ScanSlots];
        private int _nextSlot;

#if MW_DEV
        /// <summary>Overlap scans made and uses of an existing scan (dev checks of the cost).</summary>
        public int ScansDone, ScansReused, LineCasts;
        /// <summary>Scans whose collider buffer was full, pieces left out of a scan because 48 nearer ones were kept, candidates dropped by the reach filter and by the blacklist (read by /state).</summary>
        public static int ColliderOverflows, StructureOverflows, Unreachable, Blacklisted;
#endif

        private static int PieceMask => LayerMask.GetMask(VanillaNames.LayerPiece, VanillaNames.LayerPieceNonsolid);

        private static int StructureMask
        {
            get
            {
                if (s_structureMask == 0)
                    s_structureMask = LayerMask.GetMask(VanillaNames.LayerPiece, VanillaNames.LayerPieceNonsolid, VanillaNames.LayerDefault,
                        VanillaNames.LayerStaticSolid, VanillaNames.LayerDefaultSmall, VanillaNames.LayerVehicle);
                return s_structureMask;
            }
        }

        // ------------------------------------------------------------------ choice

        /// <summary>
        /// Chooses among the creatures, tamed animals and structures around <paramref name="origin"/> (distance measured from it, view from <paramref name="eye"/>)
        /// the visible candidate with the lowest score under <paramref name="limit"/> (the best player score, or float.MaxValue). <paramref name="range"/> is the
        /// targeting range; <paramref name="current"/> is kept up to twice the range. <paramref name="self"/> is the mass doing the choice, or null for a worm.
        /// <paramref name="origin"/>.y is the ground of the attacker and <paramref name="verticalReach"/> what it can really reach above or below it; a candidate in
        /// <paramref name="blacklist"/> for <paramref name="owner"/> is skipped. <paramref name="fallbackAllowed"/>: no living target was perceived for the forget time
        /// (decision 166), the only case in which a structure may be chosen.
        /// </summary>
        public bool Pick(Vector3 origin, Vector3 eye, float range, float limit, ZDOID current, Character self, float now, float verticalReach,
            TargetBlacklist blacklist, int owner, bool fallbackAllowed, out TargetChoice choice)
        {
            choice = default;
            bool tamedOk = MWConfig.Targets.AttackTamed.Value;
            bool creaturesOk = MWConfig.Targets.AttackCreatures.Value;
            bool structuresOk = MWConfig.Targets.AttackStructures.Value;
            if (!tamedOk && !creaturesOk && !structuresOk) return false;

            TargetRules.Excluded.Refresh(MWConfig.Targets.ExcludedPrefabs.Value);
            float tamedWeight = MWConfig.Targets.TamedWeight.Value;
            float creatureWeight = MWConfig.Targets.CreatureWeight.Value;
            int living = 0;

            // The living targets are compared among themselves (distance x weight).
            if (tamedOk || creaturesOk) living = CollectCharacters(origin, range, current, self, tamedOk, creaturesOk, tamedWeight, creatureWeight, now, verticalReach, blacklist, owner);
            if (living > 0 && SelectVisible(eye, 0, living, limit, out choice)) return true;

            // A structure is a fallback (decision 164): only when no player is perceived and no living target was chosen.
            if (!structuresOk || !fallbackAllowed || !TargetRules.StructureFallbackAllowed(limit, false)) return false;
            int count = CollectStructures(origin, range, current, living, MWConfig.Targets.StructureWeight.Value, now, verticalReach, blacklist, owner);
            return count > living && SelectVisible(eye, living, count, float.MaxValue, out choice);
        }

        /// <summary>
        /// Obstacle rule (decisions 164, 166): the first targetable structure that a sphere of <paramref name="radius"/> swept from <paramref name="center"/> toward
        /// <paramref name="toward"/> (the living target) touches within <paramref name="reach"/>. A piece whose top is under the attacker's feet
        /// (<paramref name="feetY"/>) + 0.3 m is under it, not in its way; a piece set aside for <paramref name="owner"/> is skipped. Needs Targets.AttackStructures.
        /// </summary>
        public bool PickObstacle(Vector3 center, float radius, float feetY, Vector3 toward, float reach, float now, float verticalReach, TargetBlacklist blacklist, int owner,
            out TargetChoice choice)
        {
            choice = default;
            if (!MWConfig.Targets.AttackStructures.Value || toward.sqrMagnitude < 1e-6f) return false;
            TargetRules.Excluded.Refresh(MWConfig.Targets.ExcludedPrefabs.Value);
            int hits = Physics.SphereCastNonAlloc(center, radius, toward.normalized, s_cast, reach, StructureMask, QueryTriggerInteraction.Ignore);
            if (hits > s_cast.Length) hits = s_cast.Length;
            int best = -1;
            float bestDistance = float.MaxValue;
            Component bestComp = null;
            for (int h = 0; h < hits; h++)
            {
                Collider c = s_cast[h].collider;
                if (c == null || s_cast[h].distance >= bestDistance) continue;
                if (!TargetRules.BlocksTheWay(c.bounds.max.y, feetY)) continue;
                Component comp = StructureOf(c);
                if (comp == null) continue;
                ZDOID id = ZdoIdOf(comp);
                if (id == ZDOID.None) continue;
                if (blacklist != null && blacklist.Contains(owner, TargetReach.Key(id.UserID, id.ID), now))
                {
#if MW_DEV
                    Blacklisted++;
#endif
                    continue;
                }
                float y = s_cast[h].point.y;
                if (!TargetReach.Reachable(y, y, feetY, verticalReach)) continue;
                best = h;
                bestDistance = s_cast[h].distance;
                bestComp = comp;
            }
            if (best < 0) return false;
            Collider col = s_cast[best].collider;
            choice.Found = true;
            choice.Kind = TargetKind.Structure;
            choice.Object = bestComp;
            choice.Collider = col;
            choice.Center = col.bounds.center;
            choice.Distance = bestDistance;
            choice.Score = TargetRules.Score(bestDistance, MWConfig.Targets.StructureWeight.Value);
            choice.Id = ZdoIdOf(bestComp);
            return true;
        }

        /// <summary>The visible candidate of [from, to) with the lowest score under <paramref name="limit"/>, testing the best few in order of score.</summary>
        private bool SelectVisible(Vector3 eye, int from, int to, float limit, out TargetChoice choice)
        {
            choice = default;
            for (int i = from; i < to; i++) _used[i] = false;
            for (int tries = 0; tries < MaxLineCasts; tries++)
            {
                int best = TargetRules.NextBest(_score, _used, from, to, limit);
                if (best < 0) return false;
                _used[best] = true;
                if (!Visible(eye, best)) continue;

                choice.Found = true;
                choice.Kind = (TargetKind)_kind[best];
                choice.Object = _obj[best];
                choice.Character = _char[best];
                choice.Collider = _col[best];
                choice.Center = _center[best];
                choice.Distance = _dist[best];
                choice.Score = _score[best];
                choice.Id = ZdoIdOf(_obj[best]);
                return true;
            }
            return false;
        }

        private int CollectCharacters(Vector3 origin, float range, ZDOID current, Character self, bool tamedOk, bool creaturesOk, float tamedWeight, float creatureWeight,
            float now, float verticalReach, TargetBlacklist blacklist, int owner)
        {
            int count = 0;
            List<Character> all = Character.GetAllCharacters();
            for (int k = 0; k < all.Count && count < MaxCharacters; k++)
            {
                Character c = all[k];
                if (c == null || c == self || c.IsPlayer() || c.IsDead()) continue;
                bool tamed = c.IsTamed();
                TargetKind kind = TargetRules.KindOfCharacter(tamed);
                if (!(tamed ? tamedOk : creaturesOk)) continue;
                if (!tamed && !TargetRules.FactionTargetable(c.GetFaction())) continue;
                if (WritheMassState.TryGet(c, out _)) continue;

                Vector3 center = c.GetCenterPoint();
                float d = Vector3.Distance(origin, center);
                if (d > 2f * range) continue;
                ZNetView view = c.GetComponent<ZNetView>();
                if (view == null || !view.IsValid()) continue;
                ZDOID id = view.GetZDO().m_uid;
                if (!TargetRules.InRange(d, range, id == current)) continue;
                int prefabHash = view.GetZDO().GetPrefab();
                if (TargetRules.Excluded.Contains(prefabHash) || IsModObject(prefabHash, c.gameObject)) continue;
                if (blacklist != null && blacklist.Contains(owner, TargetReach.Key(id.UserID, id.ID), now))
                {
#if MW_DEV
                    Blacklisted++;
#endif
                    continue;
                }

                // Reachable: the vertical span of its collider within the real reach of the attacker (a bat 3 m up, a creature on a ledge, are not).
                Collider body = c.GetCollider();
                float low = center.y, high = center.y;
                if (body != null)
                {
                    Bounds b = body.bounds;
                    low = b.min.y;
                    high = b.max.y;
                }
                if (!TargetReach.Reachable(low, high, origin.y, verticalReach))
                {
#if MW_DEV
                    Unreachable++;
#endif
                    continue;
                }

                _kind[count] = (byte)kind;
                _obj[count] = c;
                _char[count] = c;
                _col[count] = null;
                _center[count] = center;
                _dist[count] = d;
                _score[count] = TargetRules.Score(d, tamed ? tamedWeight : creatureWeight);
                count++;
            }
            return count;
        }

        private int CollectStructures(Vector3 origin, float range, ZDOID current, int count, float weight, float now, float verticalReach, TargetBlacklist blacklist, int owner)
        {
            int slot = FindScan(origin, range, now);
            int baseIndex = slot * MaxPerScan;
            for (int k = 0; k < _sCount[slot] && count < MaxCandidates; k++)
            {
                Component comp = _sObj[baseIndex + k];
                Collider col = _sCol[baseIndex + k];
                if (comp == null || col == null) continue;   // destroyed since the scan
                if (!TryClosestOn(col, origin, out Vector3 closest)) continue;   // no real surface seen from here: nothing to reach
                float d = Vector3.Distance(origin, closest);
                ZDOID id = ZdoIdOf(comp);
                if (!TargetRules.InRange(d, range, id == current)) continue;
                if (blacklist != null && blacklist.Contains(owner, TargetReach.Key(id.UserID, id.ID), now))
                {
#if MW_DEV
                    Blacklisted++;
#endif
                    continue;
                }
                if (!TargetReach.Reachable(closest.y, closest.y, origin.y, verticalReach))
                {
#if MW_DEV
                    Unreachable++;
#endif
                    continue;
                }

                _kind[count] = (byte)TargetKind.Structure;
                _obj[count] = comp;
                _char[count] = null;
                _col[count] = col;
                _center[count] = col.bounds.center;
                _near[count] = closest;
                _dist[count] = d;
                _score[count] = TargetRules.Score(d, weight);
                count++;
            }
            return count;
        }

        /// <summary>A scan around <paramref name="origin"/> that covers <paramref name="range"/> and is recent enough, else a new one.</summary>
        private int FindScan(Vector3 origin, float range, float now)
        {
            for (int s = 0; s < ScanSlots; s++)
            {
                if (_sRadius[s] <= 0f || now - _sTime[s] > ReuseSeconds) continue;
                if ((origin - _sCenter[s]).sqrMagnitude > ReuseRadius * ReuseRadius || _sRadius[s] < range + ReuseRadius * 2f) continue;
#if MW_DEV
                ScansReused++;
#endif
                return s;
            }
            int slot = _nextSlot;
            _nextSlot = (_nextSlot + 1) % ScanSlots;
            Scan(slot, origin, range + ReuseRadius * 2f, now);
            return slot;
        }

        private void Scan(int slot, Vector3 center, float radius, float now)
        {
#if MW_DEV
            ScansDone++;
#endif
            _sCenter[slot] = center;
            _sRadius[slot] = radius;
            _sTime[slot] = now;
            _sCount[slot] = 0;
            int hits = Physics.OverlapSphereNonAlloc(center, radius, s_hits, StructureMask, QueryTriggerInteraction.Ignore);
            AddHits(slot, center, hits);
            if (hits >= MaxColliders)
            {
                // The buffer is full (a forest, a rocky mine): which colliders it holds is arbitrary, so a piece may be missing. A second overlap on the layers of
                // the built pieces only, which are far fewer, brings them back (a piece found twice keeps its nearest collider).
#if MW_DEV
                ColliderOverflows++;
#endif
                hits = Physics.OverlapSphereNonAlloc(center, radius, s_hits, PieceMask, QueryTriggerInteraction.Ignore);
                AddHits(slot, center, hits);
            }
        }

        private void AddHits(int slot, Vector3 center, int hits)
        {
            int baseIndex = slot * MaxPerScan;
            for (int h = 0; h < hits; h++)
            {
                Collider c = s_hits[h];
                s_hits[h] = null;
                if (c == null) continue;
                Component comp = StructureOf(c);
                if (comp == null) continue;
                float d = (ClosestOn(c, center) - center).magnitude;

                int at = -1;
                for (int k = 0; k < _sCount[slot]; k++)
                    if (_sObj[baseIndex + k] == comp) { at = k; break; }
                if (at >= 0)
                {
                    // Several colliders of one piece: the one closest to the centre of the scan stands for it.
                    if (d >= _sDist[baseIndex + at]) continue;
                    RemoveAt(slot, at);
                }
                // The MaxPerScan nearest pieces are kept, sorted by distance: a farther one never pushes out a nearer one.
                int pos = TargetReach.NearestInsertPos(_sDist, baseIndex, _sCount[slot], MaxPerScan, d);
                if (pos < 0)
                {
#if MW_DEV
                    StructureOverflows++;
#endif
                    continue;
                }
                if (_sCount[slot] >= MaxPerScan)
                {
#if MW_DEV
                    StructureOverflows++;   // the farthest one falls off
#endif
                    _sCount[slot]--;
                }
                int move = _sCount[slot] - pos;
                if (move > 0)
                {
                    Array.Copy(_sObj, baseIndex + pos, _sObj, baseIndex + pos + 1, move);
                    Array.Copy(_sCol, baseIndex + pos, _sCol, baseIndex + pos + 1, move);
                    Array.Copy(_sDist, baseIndex + pos, _sDist, baseIndex + pos + 1, move);
                }
                _sObj[baseIndex + pos] = comp;
                _sCol[baseIndex + pos] = c;
                _sDist[baseIndex + pos] = d;
                _sCount[slot]++;
            }
        }

        private void RemoveAt(int slot, int at)
        {
            int baseIndex = slot * MaxPerScan;
            int move = _sCount[slot] - at - 1;
            if (move > 0)
            {
                Array.Copy(_sObj, baseIndex + at + 1, _sObj, baseIndex + at, move);
                Array.Copy(_sCol, baseIndex + at + 1, _sCol, baseIndex + at, move);
                Array.Copy(_sDist, baseIndex + at + 1, _sDist, baseIndex + at, move);
            }
            _sCount[slot]--;
        }

        /// <summary>Targets.ExcludedPrefabs for a character (a player included: its ZDO carries the "Player" prefab hash).</summary>
        public static bool IsExcluded(Character c)
        {
            if (c == null) return false;
            TargetRules.Excluded.Refresh(MWConfig.Targets.ExcludedPrefabs.Value);
            if (TargetRules.Excluded.Count == 0) return false;
            ZNetView view = c.GetComponent<ZNetView>();
            return view != null && view.IsValid() && TargetRules.Excluded.Contains(view.GetZDO().GetPrefab());
        }

        // ------------------------------------------------------------------ what a structure is

        /// <summary>The structure a collider belongs to (a WearNTear piece, or a dvergr Destructible of the mines), or null: spec §5.3.</summary>
        private static Component StructureOf(Collider c)
        {
            WearNTear wear = c.GetComponentInParent<WearNTear>();
            if (wear != null) return IsStructure(wear, wear.gameObject, true) ? wear : null;
            Destructible destructible = c.GetComponentInParent<Destructible>();
            if (destructible != null) return IsStructure(destructible, destructible.gameObject, false) ? destructible : null;
            return null;
        }

        private static bool IsStructure(Component comp, GameObject go, bool wearNTear)
        {
            ZNetView view = go.GetComponent<ZNetView>();
            if (view == null || !view.IsValid()) return false;
            ZDO zdo = view.GetZDO();
            int hash = zdo.GetPrefab();
            if (TargetRules.Excluded.Contains(hash)) return false;

            if (!s_class.TryGetValue(hash, out byte kind))
            {
                string name = Utils.GetPrefabName(go);
                if (IsModName(name) || go.GetComponent<TeleportWorld>() != null || go.GetComponent<TombStone>() != null) kind = 0;
                else if (wearNTear) kind = 1;
                else kind = (byte)(TargetRules.IsDvergrStructure(name) ? 2 : 0);
                s_class[hash] = kind;
            }
            if (kind == 0) return false;
            if (kind == 1 && ((WearNTear)comp).m_health >= IndestructibleHealth) return false;
            return true;
        }

        private static bool IsModName(string name) =>
            name.StartsWith("MW_", System.StringComparison.Ordinal) || name.StartsWith("SW_", System.StringComparison.Ordinal);

        private static bool IsModObject(int prefabHash, GameObject go)
        {
            if (!s_modObject.TryGetValue(prefabHash, out bool mod))
            {
                mod = IsModName(Utils.GetPrefabName(go));
                s_modObject[prefabHash] = mod;
            }
            return mod;
        }

        private static ZDOID ZdoIdOf(Component comp)
        {
            ZNetView view = comp != null ? comp.GetComponent<ZNetView>() : null;
            return view != null && view.IsValid() ? view.GetZDO().m_uid : ZDOID.None;
        }

        // ------------------------------------------------------------------ visibility

        /// <summary>
        /// A character is seen when the line to its centre is clear. A structure (a door has four colliders, a wall stands in a slope) is seen when the line to its
        /// centre, to its closest point or to the top of its collider is clear: one point hidden by the terrain or by another piece does not hide it. Whatever
        /// collider of the target itself the line meets does not count as a blocker.
        /// </summary>
        private bool Visible(Vector3 eye, int i)
        {
            if (_kind[i] != (byte)TargetKind.Structure) return LineClear(eye, _char[i].GetCenterPoint(), _obj[i]);
            if (LineClear(eye, _center[i], _obj[i]) || LineClear(eye, _near[i], _obj[i])) return true;
            Bounds b = _col[i].bounds;
            return LineClear(eye, new Vector3(b.center.x, b.max.y - 0.05f, b.center.z), _obj[i]);
        }

        private bool LineClear(Vector3 eye, Vector3 to, Component target)
        {
#if MW_DEV
            LineCasts++;
#endif
            if (!Physics.Linecast(eye, to, out RaycastHit hit, WormPhysics.ViewBlockMask, QueryTriggerInteraction.Ignore)) return true;
            return hit.collider != null && Owns(target, hit.collider);
        }

        /// <summary>The collider belongs to the target object: under its transform, or under the same <c>ZNetView</c> (a piece whose colliders hang elsewhere in its prefab).</summary>
        private static bool Owns(Component target, Collider c)
        {
            if (target == null || c == null) return false;
            if (c.transform.IsChildOf(target.transform)) return true;
            ZNetView mine = target.GetComponentInParent<ZNetView>();
            return mine != null && c.GetComponentInParent<ZNetView>() == mine;
        }

        // ------------------------------------------------------------------ resolving a stored target

        /// <summary>
        /// The object of a target stored as a ZDOID (a worm keeps its target between two steps): the Character, or the structure with its collider closest to
        /// <paramref name="near"/>. False when it is not loaded here, dead or destroyed.
        /// </summary>
        public static bool Resolve(TargetKind kind, ZDOID id, Vector3 near, out Component obj, out Character character, out Collider collider)
        {
            obj = null;
            character = null;
            collider = null;
            GameObject go = id != ZDOID.None && ZNetScene.instance != null ? ZNetScene.instance.FindInstance(id) : null;
            if (go == null) return false;
            if (kind == TargetKind.Structure)
            {
                obj = go.GetComponent<WearNTear>();
                if (obj == null) obj = go.GetComponent<Destructible>();
                if (obj == null) return false;
                collider = ClosestCollider(go, near);
                return collider != null;
            }
            character = go.GetComponent<Character>();
            obj = character;
            return character != null && !character.IsDead();
        }

        private static readonly List<Collider> s_colliders = new List<Collider>(16);

        private static Collider ClosestCollider(GameObject go, Vector3 near)
        {
            go.GetComponentsInChildren(false, s_colliders);
            Collider best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < s_colliders.Count; i++)
            {
                Collider c = s_colliders[i];
                if (c == null || c.isTrigger) continue;
                float d = (ClosestOn(c, near) - near).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = c; }
            }
            s_colliders.Clear();
            return best;
        }

        /// <summary>
        /// Closest point of a collider to <paramref name="p"/>, and whether it is a real surface point. <c>Collider.ClosestPoint</c> is only valid on convex colliders
        /// (and answers <paramref name="p"/> itself, distance 0, when <paramref name="p"/> is inside the AABB of a concave mesh). A concave mesh is measured on its
        /// actual surface: a ray from <paramref name="p"/> to the centre of its bounds, then to the closest point of its bounds. When neither touches the mesh there
        /// is no contact: false, and <paramref name="point"/> is a bounds based stand-in that is never <paramref name="p"/> itself. Every range decision (the scan, the
        /// strike, the engulf range) uses this one measure.
        /// </summary>
        public static bool TryClosestOn(Collider c, Vector3 p, out Vector3 point)
        {
            if (!(c is MeshCollider mc) || mc.convex)
            {
                point = c.ClosestPoint(p);
                return true;
            }
            Bounds b = c.bounds;
            float span = b.size.magnitude + 0.1f;
            Vector3 toCenter = b.center - p;
            RaycastHit hit;
            if (toCenter.sqrMagnitude > 1e-6f && c.Raycast(new Ray(p, toCenter.normalized), out hit, toCenter.magnitude + span))
            {
                point = hit.point;
                return true;
            }
            Vector3 near = b.ClosestPoint(p);
            Vector3 toNear = near - p;
            if (toNear.sqrMagnitude > 1e-6f)
            {
                if (c.Raycast(new Ray(p, toNear.normalized), out hit, toNear.magnitude + span))
                {
                    point = hit.point;
                    return true;
                }
                point = near;
                return false;
            }
            point = b.center + Vector3.up * Mathf.Max(b.extents.y, 0.1f);   // inside the AABB, no surface seen: not p
            return false;
        }

#if MW_DEV
        /// <summary>Dev: why a given structure is or is not a candidate seen from <paramref name="origin"/> (S13), in one line.</summary>
        public static string Diagnose(GameObject structure, Vector3 origin, Vector3 eye, float range, float verticalReach)
        {
            if (structure == null) return "no object";
            Collider[] cols = structure.GetComponentsInChildren<Collider>(false);
            int hits = Physics.OverlapSphereNonAlloc(origin, range + ReuseRadius * 2f, s_hits, StructureMask, QueryTriggerInteraction.Ignore);
            int found = 0;
            for (int h = 0; h < hits; h++)
            {
                if (s_hits[h] != null && s_hits[h].transform.IsChildOf(structure.transform)) found++;
                s_hits[h] = null;
            }
            string text = $"{cols.Length} collider(s), buffer {hits}/{MaxColliders}, {found} of them in the scan";
            for (int k = 0; k < cols.Length && k < 3; k++)
            {
                Collider c = cols[k];
                bool contact = TryClosestOn(c, origin, out Vector3 closest);
                bool blocked = Physics.Linecast(eye, c.bounds.center, out RaycastHit hit, WormPhysics.ViewBlockMask, QueryTriggerInteraction.Ignore);
                string by = blocked && hit.collider != null ? hit.collider.name : "-";
                text += $"; [{c.GetType().Name} layer {LayerMask.LayerToName(c.gameObject.layer)} trigger {c.isTrigger} contact {contact} d {Vector3.Distance(origin, closest):0.0} " +
                        $"reachable {TargetReach.Reachable(closest.y, closest.y, origin.y, verticalReach)} line blocked by {by}]";
            }
            WearNTear wear = structure.GetComponent<WearNTear>();
            ZNetView view = structure.GetComponent<ZNetView>();
            text += $"; wear {(wear != null ? wear.m_health.ToString("0") : "none")}, view valid {(view != null && view.IsValid())}, class {(view != null && view.IsValid() && s_class.TryGetValue(view.GetZDO().GetPrefab(), out byte k2) ? k2.ToString() : "?")}";
            return text;
        }
#endif

        /// <summary>The point of <see cref="TryClosestOn"/>, for steering and ordering only (it may be a stand-in without contact).</summary>
        public static Vector3 ClosestOn(Collider c, Vector3 p)
        {
            TryClosestOn(c, p, out Vector3 point);
            return point;
        }
    }
}
