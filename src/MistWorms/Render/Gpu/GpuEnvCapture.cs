using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace MistWorms.Render.Gpu
{
    /// <summary>
    /// Environment of the GPU masses, captured on the main thread at 8 Hz per mass (visual v2, A4):
    /// <list type="bullet">
    /// <item>a height grid of 12 x 12 samples over 2 (r + 0.6) m, by one batch of <see cref="RaycastCommand"/> (144 rays) scheduled in a frame and completed in the next one, so the
    /// main thread never waits for it (the very first capture of a mass is synchronous);</item>
    /// <item>up to 24 primitives from <c>Physics.OverlapSphereNonAlloc</c>: boxes (Box colliders, and the bounds of the other colliders, rocks and pieces), capsules
    /// (Capsule colliders: characters), spheres. The engulfed being (<see cref="GpuMass.AdhesiveCollider"/>) is always the first primitive and is adhesive over the covered
    /// fraction <see cref="GpuMass.Engulf"/> of its height; a creature or a player that overlaps the dome is a weakly adhesive primitive.</item>
    /// </list>
    /// Persistent <see cref="NativeArray{T}"/> and a fixed overlap buffer: nothing is allocated after the construction.
    /// </summary>
    internal sealed class GpuEnvCapture : IGpuEnvironment, IDisposable
    {
        public const float Interval = 0.125f;                       // 8 Hz
        private const float RayMinHeight = 1.0f, RayMaxHeight = 4.0f, RayAbove = 0.3f, RayBelow = 5f;
        private const float CoverableHeight = 1.0f;
        private const float BeingAdhesion = 0.25f, BeingCover = 0.35f;

        private NativeArray<RaycastCommand> _cmds;
        private NativeArray<RaycastHit> _hits;
        private readonly JobHandle[] _pending = new JobHandle[WormSimConst.MaxMasses];
        private readonly bool[] _hasPending = new bool[WormSimConst.MaxMasses];
        private readonly Vector2[] _pendingMin = new Vector2[WormSimConst.MaxMasses];
        private readonly float[] _pendingSpacing = new float[WormSimConst.MaxMasses];
        private readonly float[] _pendingY = new float[WormSimConst.MaxMasses];
        private readonly Collider[] _overlap = new Collider[128];
        private readonly float[] _primDist = new float[WormSimConst.MaxPrims];
        private readonly int _groundMask, _solidMask, _decorMask, _terrainMask;

        /// <summary>Surface of the engulfed target (baked meshes), the preferred model; the capsules of the bones or the single capsule are the fallback.</summary>
        private readonly GpuSkinSampler[] _skins = new GpuSkinSampler[WormSimConst.MaxMasses];
        private readonly Collider[] _skinTargets = new Collider[WormSimConst.MaxMasses];
        private readonly Transform[] _skinRoots = new Transform[WormSimConst.MaxMasses];
        private int _lastSkinSlot;
        /// <summary>The skin sampler of the mass that was sampled last (every mass has its own: they can wrap different targets).</summary>
        public GpuSkinSampler Skin => _skins[_lastSkinSlot] ?? (_skins[_lastSkinSlot] = new GpuSkinSampler());

        public int LastPrimCount { get; private set; }
        public int Captures { get; private set; }

        public GpuEnvCapture()
        {
            int n = WormSimConst.MaxMasses * RaysPerMass;
            _cmds = new NativeArray<RaycastCommand>(n, Allocator.Persistent);
            _hits = new NativeArray<RaycastHit>(n, Allocator.Persistent);
            _groundMask = LayerMask.GetMask(VanillaNames.LayerTerrain, VanillaNames.LayerStaticSolid, VanillaNames.LayerDefault, VanillaNames.LayerDefaultSmall, VanillaNames.LayerPiece);
            _terrainMask = LayerMask.GetMask(VanillaNames.LayerTerrain);
            _decorMask = _groundMask & ~LayerMask.GetMask(VanillaNames.LayerTerrain);
            _solidMask = LayerMask.GetMask(VanillaNames.LayerDefault, VanillaNames.LayerStaticSolid, VanillaNames.LayerDefaultSmall, VanillaNames.LayerPiece,
                VanillaNames.LayerCharacter, VanillaNames.LayerVehicle);
        }

        /// <summary>A mass of this slot is gone: its pending rays are finished and dropped, its skin target forgotten.</summary>
        public void Forget(int slot)
        {
            if (slot < 0 || slot >= _pending.Length) return;
            if (_hasPending[slot]) { _pending[slot].Complete(); _hasPending[slot] = false; }
            _skinTargets[slot] = null;
            _skinRoots[slot] = null;
        }

        public void Dispose()
        {
            for (int i = 0; i < _pending.Length; i++) if (_hasPending[i]) { _pending[i].Complete(); _hasPending[i] = false; }
            if (_cmds.IsCreated) _cmds.Dispose();
            if (_hits.IsCreated) _hits.Dispose();
        }

        /// <summary>The skin of the engulfed target, every frame: the matrices of the bones are uploaded and the anchors are skinned on the GPU. Without a usable mesh the anchors are off (capsules take over).</summary>
        private void SkinFrame(GpuMass m)
        {
            int slot = m.Slot;
            Collider target = m.AdhesiveCollider;
            if (target == null || !target.enabled || !target.gameObject.activeInHierarchy) { m.AnchorCount = 0; return; }
            if (_skinTargets[slot] != target)
            {
                _skinTargets[slot] = target;
                Character character = target.GetComponentInParent<Character>();
                ZNetView view = target.GetComponentInParent<ZNetView>();
                _skinRoots[slot] = character != null ? character.transform : view != null ? view.transform : target.transform.root;
            }
            Bounds tb = target.bounds;
            _lastSkinSlot = slot;
            GpuSkinSampler skin = _skins[slot] ?? (_skins[slot] = new GpuSkinSampler());
            if (!skin.Sample(m, _skinRoots[slot], tb.min.y, tb.size.y)) m.AnchorCount = 0;
        }

        public void Tick(GpuMass m, float now, bool force)
        {
            int slot = m.Slot;
            SkinFrame(m);
            if (force)
            {
                if (_hasPending[slot]) { _pending[slot].Complete(); _hasPending[slot] = false; }
                ScheduleHeights(m);
                _pending[slot].Complete();
                _hasPending[slot] = false;
                Publish(m);
                m.NextEnvTime = now + Interval;
                return;
            }

            if (_hasPending[slot])
            {
                _pending[slot].Complete();
                _hasPending[slot] = false;
                Publish(m);
            }
            else if (now >= m.NextEnvTime)
            {
                ScheduleHeights(m);
                m.NextEnvTime = now + Interval;
            }
        }

        // Rays of one mass, in one batch: [0, 256) at the terrain from 4 m above the mass, [256, 512) at everything from 1 m above (where there is no terrain: the floor of a mine),
        // [512, 768) at the obstacles (not the terrain) from above, [768, 1792) four side maps at the obstacles, from the west, the east, the south and the north.
        private const int Cells = WormSimConst.HeightN * WormSimConst.HeightN;
        private const int RaysPerMass = Cells * 7;

        private void ScheduleHeights(GpuMass m)
        {
            int slot = m.Slot, hn = WormSimConst.HeightN, baseIndex = slot * RaysPerMass;
            float extent = m.GridExtent, spacing = extent / (hn - 1);
            Vector2 min = new Vector2(m.Center.x - extent * 0.5f, m.Center.z - extent * 0.5f);
            float cy = m.Center.y;
            float yTop = cy + RayMaxHeight;
            var qTerrain = new QueryParameters(_terrainMask, false, QueryTriggerInteraction.Ignore, false);
            var qAll = new QueryParameters(_groundMask, false, QueryTriggerInteraction.Ignore, false);
            var qDecor = new QueryParameters(_decorMask, false, QueryTriggerInteraction.Ignore, false);
            float sideY0 = cy - 0.5f;
            m.SideYMin = sideY0;
            m.SideYStep = (RayMaxHeight + 0.8f) / (hn - 1);
            float xMax = min.x + (hn - 1) * spacing, zMax = min.y + (hn - 1) * spacing, reach = extent + 2f;
            for (int a = 0; a < hn; a++)
                for (int b = 0; b < hn; b++)
                {
                    int c = a * hn + b;
                    float u = min.x + b * spacing, v = min.y + a * spacing;             // a = row (z), b = column (x) for the maps seen from above
                    _cmds[baseIndex + c] = new RaycastCommand(new Vector3(u, yTop, v), Vector3.down, qTerrain, RayMaxHeight + RayBelow);
                    _cmds[baseIndex + Cells + c] = new RaycastCommand(new Vector3(u, cy + RayMinHeight, v), Vector3.down, qAll, RayMinHeight + RayBelow);
                    _cmds[baseIndex + 2 * Cells + c] = new RaycastCommand(new Vector3(u, yTop, v), Vector3.down, qDecor, RayMaxHeight + 0.5f);
                    // side maps: a = row (height), b = across
                    float y = sideY0 + a * m.SideYStep, across = min.y + b * spacing, acrossX = min.x + b * spacing;
                    _cmds[baseIndex + 3 * Cells + c] = new RaycastCommand(new Vector3(min.x - 1f, y, across), Vector3.right, qDecor, reach);
                    _cmds[baseIndex + 4 * Cells + c] = new RaycastCommand(new Vector3(xMax + 1f, y, across), Vector3.left, qDecor, reach);
                    _cmds[baseIndex + 5 * Cells + c] = new RaycastCommand(new Vector3(acrossX, y, min.y - 1f), Vector3.forward, qDecor, reach);
                    _cmds[baseIndex + 6 * Cells + c] = new RaycastCommand(new Vector3(acrossX, y, zMax + 1f), Vector3.back, qDecor, reach);
                }
            _pending[slot] = RaycastCommand.ScheduleBatch(_cmds.GetSubArray(baseIndex, RaysPerMass), _hits.GetSubArray(baseIndex, RaysPerMass), 32, 1, default);
            _hasPending[slot] = true;
            _pendingMin[slot] = min;
            _pendingSpacing[slot] = spacing;
            _pendingY[slot] = cy;
        }

        /// <summary>Reads the finished rays (ground heights, depth maps of the obstacles), captures the primitives and uploads everything.</summary>
        private void Publish(GpuMass m)
        {
            int slot = m.Slot, baseIndex = slot * RaysPerMass;
            float cy = _pendingY[slot];
            // ground: the terrain when it is within 3 m of the mass, else the first surface from 1 m above (a floor); a sample that misses keeps the mean of the hits
            float sum = 0f;
            int hit = 0;
            for (int i = 0; i < Cells; i++)
            {
                float h = GroundSample(baseIndex, i, cy, out bool ok);
                m.Heights[i] = h;
                if (ok) { sum += h; hit++; }
            }
            float fallback = hit > 0 ? sum / hit : cy;
            for (int i = 0; i < Cells; i++)
            {
                GroundSample(baseIndex, i, cy, out bool ok);
                m.Heights[i] = (ok ? m.Heights[i] : fallback) - m.Origin.y;
            }

            // depth maps of the obstacles, local frame
            Vector2 min = _pendingMin[slot];
            float ox = m.Origin.x, oy = m.Origin.y, oz = m.Origin.z;
            for (int c = 0; c < Cells; c++)
            {
                RaycastHit t = _hits[baseIndex + 2 * Cells + c];
                m.Sides[4 * Cells + c] = t.colliderInstanceID != 0 ? t.point.y - oy : -1e6f;
                RaycastHit w = _hits[baseIndex + 3 * Cells + c], e = _hits[baseIndex + 4 * Cells + c], s = _hits[baseIndex + 5 * Cells + c], n = _hits[baseIndex + 6 * Cells + c];
                m.Sides[c] = w.colliderInstanceID != 0 ? w.point.x - ox : 1e6f;
                m.Sides[Cells + c] = e.colliderInstanceID != 0 ? e.point.x - ox : -1e6f;
                m.Sides[2 * Cells + c] = s.colliderInstanceID != 0 ? s.point.z - oz : 1e6f;
                m.Sides[3 * Cells + c] = n.colliderInstanceID != 0 ? n.point.z - oz : -1e6f;
            }
            m.SideEnabled = true;

            int prims = CapturePrims(m);
            LastPrimCount = prims;
            Captures++;
            GpuWormHost.UploadEnvironment(m, prims, min, _pendingSpacing[slot]);
        }

        private float GroundSample(int baseIndex, int i, float cy, out bool ok)
        {
            RaycastHit t = _hits[baseIndex + i];
            if (t.colliderInstanceID != 0 && Mathf.Abs(t.point.y - cy) < 3f)
            {
                // the bare ground; an obstacle lower than CoverableHeight above it is part of the ground (the mass flows over a low rock), a higher one is not (it is gone round)
                RaycastHit d = _hits[baseIndex + 2 * Cells + i];
                float over = d.colliderInstanceID != 0 ? d.point.y - t.point.y : 0f;
                ok = true;
                return over > 0.05f && over <= CoverableHeight ? d.point.y : t.point.y;
            }
            RaycastHit a = _hits[baseIndex + Cells + i];
            if (a.colliderInstanceID != 0) { ok = true; return a.point.y; }
            ok = false;
            return cy;
        }

        // ---------------------------------------------------------------- primitives

        private int CapturePrims(GpuMass m)
        {
            int count = 0;
            m.AdhesivePrim = -1;
            m.AdhesiveCount = 1;
            Collider target = m.AdhesiveCollider;
            bool skinned = m.AnchorCount > 0;           // the skin of the target is sampled every frame (SkinFrame)
            if (target != null && !skinned && target.enabled && target.gameObject.activeInHierarchy)
            {
                Animator anim = target.GetComponentInParent<Animator>();
                int body = anim != null && anim.isHuman ? AddBodyCapsules(m, target, anim) : 0;
                if (body > 0)
                {
                    count = body;
                    m.AdhesivePrim = 0;
                    m.AdhesiveCount = body;
                }
                else if (ToPrim(target, m, out GpuPrim tp))
                {
                    tp.D.y = 1f;
                    tp.D.z = Mathf.Clamp01(m.Engulf);
                    m.Prims[0] = tp;
                    m.AdhesivePrim = tp.D.x > 0.5f && tp.D.x < 1.5f ? 0 : -1;
                    count = 1;
                }
                for (int i = 0; i < count; i++) _primDist[i] = -1f;
            }

            Vector3 c = m.Center;
            int n = Physics.OverlapSphereNonAlloc(c + Vector3.up * (m.Radius * 0.5f), m.Radius + 1.5f, _overlap, _solidMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider col = _overlap[i];
                if (col == null || col == target) continue;
                if (m.Owner != null && col.transform.IsChildOf(m.Owner)) continue;        // the body of the mass itself
                // Only real boxes, capsules and spheres (pieces, characters, creatures) become primitives. The bounds of a MeshCollider (a rock, the terrain, the scenery)
                // are not an obstacle: a box would give the worms a flat top to slide on. Those are in the height grid (rays from above).
                if (!ToPrim(col, m, out GpuPrim p)) continue;

                // a being that overlaps the dome: the worms stick to its legs for a moment
                if (col is CapsuleCollider && col.GetComponentInParent<Character>() != null && col.bounds.SqrDistance(c) < m.Radius * m.Radius)
                {
                    p.D.y = BeingAdhesion;
                    p.D.z = BeingCover;
                }
                float d = col.bounds.SqrDistance(c);
                if (count < WormSimConst.MaxPrims)
                {
                    m.Prims[count] = p;
                    _primDist[count] = d;
                    count++;
                }
                else
                {
                    int worst = -1;
                    float wd = d;
                    for (int k = 0; k < count; k++) if (_primDist[k] > wd) { wd = _primDist[k]; worst = k; }
                    if (worst >= 0)
                    {
                        m.Prims[worst] = p;
                        _primDist[worst] = d;
                    }
                }
            }
            return count;
        }

        // The eight bone capsules of a humanoid: torso (hips to chest), neck and head, two thighs, two calves, two upper arms. p0 is the lower end, the covered height goes
        // bottom up: the covered fraction of each capsule follows the engulf of the whole body.
        private static readonly HumanBodyBones[] s_boneA =
        {
            HumanBodyBones.Hips, HumanBodyBones.Chest, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
            HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm,
        };
        private static readonly HumanBodyBones[] s_boneB =
        {
            HumanBodyBones.Chest, HumanBodyBones.Head, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
        };
        private static readonly float[] s_boneR = { 0.17f, 0.10f, 0.09f, 0.09f, 0.07f, 0.07f, 0.055f, 0.055f };

        private static int AddBodyCapsules(GpuMass m, Collider target, Animator anim)
        {
            Bounds cb = target.bounds;
            float foot = cb.min.y, reach = Mathf.Clamp01(m.Engulf) * cb.size.y;
            float scale = Mathf.Max(target.transform.lossyScale.y, 0.1f);
            Vector3 o = m.Origin;
            int count = 0;
            for (int i = 0; i < s_boneA.Length; i++)
            {
                Transform a = anim.GetBoneTransform(s_boneA[i]);
                Transform b = anim.GetBoneTransform(s_boneB[i]);
                if (i == 1 && a == null) a = anim.GetBoneTransform(HumanBodyBones.Spine);
                if (a == null || b == null) continue;
                Vector3 p0 = a.position, p1 = b.position;
                if (i == 1) p1 += Vector3.up * (0.1f * scale);          // the head bone is its centre: the capsule goes to the top of the skull
                if (p0.y > p1.y) { Vector3 t = p0; p0 = p1; p1 = t; }
                float r = s_boneR[i] * scale;
                float len = (p1 - p0).magnitude + 2f * r;
                if (len < 0.05f) continue;
                float cover = Mathf.Clamp01((foot + reach - (p0.y - r)) / len);
                m.Prims[count++] = GpuPrim.Capsule(p0 - o, p1 - o, r, 1f, cover);
            }
            return count;
        }

        /// <summary>The primitive of a collider, in the local frame of the mass; false for a collider that cannot be approximated.</summary>
        private static bool ToPrim(Collider col, GpuMass m, out GpuPrim prim)
        {
            Vector3 o = m.Origin;
            Transform t = col.transform;
            Vector3 s = t.lossyScale;
            Vector3 sa = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            if (col is CapsuleCollider cc)
            {
                Vector3 axisLocal = cc.direction == 0 ? Vector3.right : cc.direction == 1 ? Vector3.up : Vector3.forward;
                float axisScale = cc.direction == 0 ? sa.x : cc.direction == 1 ? sa.y : sa.z;
                float radScale = cc.direction == 0 ? Mathf.Max(sa.y, sa.z) : cc.direction == 1 ? Mathf.Max(sa.x, sa.z) : Mathf.Max(sa.x, sa.y);
                float r = cc.radius * radScale;
                float h = Mathf.Max(cc.height * axisScale, 2f * r);
                Vector3 axis = t.TransformDirection(axisLocal).normalized;
                Vector3 centre = t.TransformPoint(cc.center);
                float half = h * 0.5f - r;
                prim = GpuPrim.Capsule(centre - axis * half - o, centre + axis * half - o, r);
                return true;
            }
            if (col is SphereCollider sc)
            {
                prim = GpuPrim.Sphere(t.TransformPoint(sc.center) - o, sc.radius * Mathf.Max(sa.x, Mathf.Max(sa.y, sa.z)));
                return true;
            }
            if (col is BoxCollider bc)
            {
                prim = GpuPrim.Box(t.TransformPoint(bc.center) - o, new Vector3(bc.size.x * sa.x, bc.size.y * sa.y, bc.size.z * sa.z) * 0.5f, t.rotation);
                return true;
            }
            prim = default;
            return false;
        }
    }
}
