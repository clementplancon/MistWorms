using System.Runtime.InteropServices;
using UnityEngine;

namespace MistWorms.Render.Gpu
{
    /// <summary>
    /// Constants of the worm rope simulation. They are the same numbers as the <c>static const</c> of WormSim.compute (the compute shader and the CPU reference
    /// <see cref="WormSimRef"/> run the same formulas in the same order); a change here must be made there too.
    /// </summary>
    internal static class WormSimConst
    {
        public const int Nodes = 8;                       // K: nodes per rope
        public const int Points = 15;                     // collision points per rope: 8 nodes and the 7 segment midpoints
        public const int PointStride = 16;                // ids of the grid are worm * 16 + point
        public const int GridCells = 1 << 19;             // hashed cells (all masses share the table)
        public const int GridSlots = 8;                   // ids kept per cell (the 8 smallest, deterministic)
        public const uint Empty = 0xFFFFFFFFu;
        public const int MaxPrims = 24;                   // primitives per mass
        public const int HeightN = 16;                    // height grid, HeightN x HeightN samples per mass
        public const int MaxMasses = 8;
        public const int FreeSlot = MaxMasses;            // the slot of the pool of free ropes (not a mass: its worms are written by the CPU each frame)
        public const int FreeCapPerLod = 256;             // free ropes per level of detail
        public const int MaxAnchors = 512;                // skin anchors per mass (points of the baked surface of the engulfed target)
        public const int MassStride = 192;                // bytes of GpuMassParams
        public const int SideMapSize = 256;               // cells of one depth map (HeightN x HeightN)
        public const int SideMaps = 5;                    // west, east, south, north surfaces and the top of the obstacles
        public const int MaxBones = 128;                  // bone matrices per mass (the bones the anchors are skinned with: about 53 for a player)
        public const int NeighbourCount = 8;              // k-NN of an anchor
        public const int HopWords = MaxAnchors / 4;       // words of the table of paths per target anchor (a byte per source anchor)

        public const int MaxWorms = 8192;

        public const float Dt = 1f / 60f;
        public const int MaxSubSteps = 3;
        public const float WormLength = 0.30f;
        public const float SegLen = 0.30f / 7f;           // 4.29 cm between nodes
        public const float Radius = 0.009f;               // collision radius of a node (body diameter 1.8 cm)
        public const float SepDist = 0.024f;              // contact distance between points of different worms
        public const float CellInv = 40f;                 // cells of 2.5 cm (at least the contact distance, so that 27 cells hold every neighbour)
        public const float Damp = 0.985f;                 // velocity kept per step
        public const float Gravity = 3f;                  // m/s2, soft: the mass is a viscous fluid more than a pile
        public const float SepK = 0.9f;
        public const float MaxSep = 0.01f;
        public const float BendMin = 1.35f * SegLen;      // minimum chord of three consecutive nodes (curvature stiffness)
        public const float BendK = 0.3f;
        public const float ContainK = 0.5f;
        public const float AdhesionRange = 0.05f;         // nodes closer to an adhesive primitive than this are attracted to it
        public const float AttractK = 0.4f;
        public const float Climb = 0.05f;                  // m/s along the axis of an adhesive capsule at adhesion 1
        public const float BodyPullMax = 0.03f;           // largest pull of a worm toward its anchor per step (a worm on a moving body keeps up with a walk)
        public const float HomeMax = 0.01f;               // largest pull toward the home point per step
        public const int Iterations = 4;

        public const float BodyDamp = 0.7f;               // velocity kept by an attached node

        // skin engulf (see SkinStep in WormSim.compute)
        public const float SkinOffset = 0.011f;           // distance of a node of the sleeve from the skin: the radius of the worm and 2 mm
        public const float LayerGap = 0.016f;             // the worms of the second layer lie on the first one
        public const float ProjectRadius = 0.12f;         // a node is projected on the plane of its anchor within this distance
        public const float OrientK = 0.10f;               // pull of the nodes toward the line of the worm (tangent to the surface, up or diagonal)
        public const float LeashK = 0.07f;                // pull of the worm toward its place on the body
        public const float TransitK = 0.7f;               // pull of the head toward the next anchor of its path
        public const float AdvanceRadius = 0.12f;         // the head is at the anchor of the path when it is this close (1.5 anchor spacings: a head that must touch each anchor lost most of its time hopping)
        public const float FallTime = 2.5f;               // a worm that lets go falls for this long at most
        public const float SlideGain = 1.0f;              // sideways slide of a node pushed out of a face of an obstacle, over its depth of penetration
        public const float HopTime = 0.2f;                // a head that has not reached the anchor of its path in this time goes on to the next one
        public const float BirthTime = 0.5f;              // a newborn worm grows from nothing in this time, a dying one shrinks to nothing in it
        public const float PendantShare = 0.08f;          // share of the worms of the sleeve that hang by one end
    }

    public enum GpuPrimType { Sphere = 0, Capsule = 1, Box = 2 }

    public enum GpuMassMode { Dome = 0, Engulf = 1 }

    /// <summary>Config Performance.GpuWorms: Auto uses the GPU ropes when the device supports them, Off keeps the CPU flipbooks.</summary>
    public enum GpuWormsMode { Auto = 0, Off = 1 }

    /// <summary>One worm (16 bytes): the layout of <c>WormData</c> in WormSim.compute.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuWormData
    {
        public uint Mass;          // mass slot
        public uint Seed;          // home point, heading, creep frequency
        public float Speed;        // creep speed (m/s)
        public float Phase;        // creep phase (cycles)
    }

    /// <summary>
    /// Parameters of one mass (128 bytes): the layout of <c>MassParams</c> in WormSim.compute. Every position is in the local frame of the mass
    /// (world position minus <c>Origin</c>), which keeps the floats precise far from the world origin.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuMassParams
    {
        public Vector4 Origin;     // xyz world origin of the local frame, w = 1 when the mass is simulated
        public Vector4 Center;     // xyz centre of the base of the dome (local), w = rest radius r
        public Vector4 Heading;    // xyz horizontal unit forward, w = speed (m/s)
        public Vector4 Shape;      // x axis along the heading, y up, z across, w forward shift of the dome centre
        public Vector4 Motion;     // x conveyor 0..1, y creep scale, z home pull per step, w engulf 0..1
        public Vector4 Sleeve;     // x number of adhesive capsules (0 = dome, k = engulf the k capsules starting at index z), y shell thickness of the sleeve, z index of the first adhesive capsule, w ground friction 0..1
        public uint FirstWorm, WormCount, PrimCount, MassIndex;
        public Vector4 GridRect;   // x, y = minimum x and z of the height grid (local), z = sample spacing, w = 1 / spacing
        public Vector4 Skin;       // x number of skin anchors (0 = none), y engulf 0..1, z height of the target, w area of its surface (m2)
        public Vector4 Side;       // depth maps of the obstacles: x lowest height of the side maps (local), y their vertical spacing, z 1 / spacing, w 1 when the maps are valid
        public Vector4 Plan;       // skin sleeve (see GpuSleeve): x worms of the sleeve S, y worms per anchor, z height (rel) of the top of the covered surface, w anchors covered
        public Vector4 Plan2;      // x anchors of the base of the body (where a worm enters it), y worms alive (0 = all; the others shrink to nothing)
    }

    /// <summary>An anchor on the surface of the engulfed body (32 bytes), the layout of <c>Anchor</c> in WormSim.compute. Local frame of the mass.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuAnchor
    {
        public Vector4 A;          // xyz position (w unused)
        public Vector4 B;          // xyz outward normal, w = height of the anchor over the height of the target (0 foot, 1 top; fixed at the selection)
    }

    /// <summary>
    /// A vertex of the surface of the engulfed body that serves as an anchor (80 bytes), the layout of <c>SkinVert</c> in WormSim.compute. Uploaded once, at the selection of the
    /// anchors; the anchor is then skinned on the GPU from the matrices of the bones.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuSkinVertex
    {
        public Vector4 PosRel;     // xyz rest position (mesh space), w = height of the anchor over the height of the target at the selection (0 foot, 1 top)
        public Vector4 Normal;     // xyz rest normal (mesh space)
        public uint Bone0, Bone1, Bone2, Bone3;      // indices among the bones used by the anchors of the mass (less than MaxBones)
        public Vector4 Weights;    // weights of the four bones
        public Vector4 Body;       // position on the body at the selection (m): x, z from the centre, y over the lowest point; the noise of the sleeve is glued to it
    }

    /// <summary>A collision primitive (64 bytes), the layout of <c>Prim</c> in WormSim.compute. Local frame of the mass.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuPrim
    {
        public Vector4 A;          // sphere: centre, radius. capsule: first end, radius. box: centre
        public Vector4 B;          // capsule: second end. box: half extents
        public Vector4 C;          // box: orientation (xyzw)
        public Vector4 D;          // x type, y adhesion 0..1, z covered fraction of the height (adhesive capsule), w unused

        public static GpuPrim Sphere(Vector3 c, float r, float adhesion = 0f) =>
            new GpuPrim { A = new Vector4(c.x, c.y, c.z, r), D = new Vector4((float)GpuPrimType.Sphere, adhesion, 1f, 0f) };

        public static GpuPrim Capsule(Vector3 p0, Vector3 p1, float r, float adhesion = 0f, float cover = 1f) =>
            new GpuPrim { A = new Vector4(p0.x, p0.y, p0.z, r), B = new Vector4(p1.x, p1.y, p1.z, 0f), D = new Vector4((float)GpuPrimType.Capsule, adhesion, cover, 0f) };

        public static GpuPrim Box(Vector3 c, Vector3 half, Quaternion q, float adhesion = 0f) =>
            new GpuPrim
            {
                A = new Vector4(c.x, c.y, c.z, 0f), B = new Vector4(half.x, half.y, half.z, 0f), C = new Vector4(q.x, q.y, q.z, q.w),
                D = new Vector4((float)GpuPrimType.Box, adhesion, 1f, 0f),
            };
    }
}
