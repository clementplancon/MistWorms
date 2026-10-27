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
        public const int MassStride = 208;                // bytes of GpuMassParams
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
        public const float SolidEdgePush = 0.3f;         // a node in the hull of the obstacles but not under a solid column (the nearest sample of the top map is open) is pushed out only when a vertical face is this close: the edge of a rock yes, the passage between two parallel walls (faces 1 m away) no
        public const float SolidRowGap = 0.35f;          // a node outside the hull of the obstacles but under a solid column of the top map by no more than this (about one row of the side maps, 0.32 m) is set on top of it: a stone lower than the spacing of the rows falls between two rows of the side maps and the hull never sees it (V12)
        public const int SolidOpenCells = 3;              // a node in the thickness of an obstacle (closed column of the top map, more than SolidRowGap under its top) looks for the nearest open column within this many cells, in 4 directions, and leaves on that side (S08: a wall has no inner face in the hull)
        public const float DispMax = 0.25f;               // largest displacement of a node of the dome in one step (15 m/s): a push, a containment or a pull can no longer throw a node tens of metres (S08)
        public const float SlideGain = 1.0f;              // sideways slide of a node pushed out of a face of an obstacle, over its depth of penetration
        public const float HopTime = 0.2f;                // a head that has not reached the anchor of its path in this time goes on to the next one
        public const float BirthTime = 0.5f;              // a newborn worm grows from nothing in this time, a dying one shrinks to nothing in it
        public const float PendantShare = 0.08f;          // share of the worms of the sleeve that hang by one end

        // pseudopod: the net of worms of a mass that stretches toward its target (port of Arm() in MassLayout.cs; the values of the first block are the ones of MassLayoutConst)
        public const float ArmFrac = 0.25f;               // share of the worms of the net at full ramp
        public const float ArmSoft = 0.1f;                // soft recruitment band (MassLayoutConst.RecruitSoft): a worm is of the net when its threshold is under ArmFrac * ramp - ArmSoft / 2
        public const float ArmStart = 0.6f;               // the net starts at this share of the radius of the mass
        public const float ArmMax = 2.0f;                 // longest net past its start
        public const float ArmMargin = 0.3f;              // the tip stops this far from the target
        public const float ArmWidth = 0.4f;               // width at the root
        public const float ArmTaper = 0.6f;               // width at the tip over width at the root
        public const float ArmThick = 0.14f;              // thickness of the layer at the root
        public const float ArmWave = 0.18f;               // lateral undulation (m)
        public const float ArmWaveK = 1.3f;               // waves along the net
        public const float ArmWavePer = 3.0f;             // period of the undulation (s)
        public const float ArmFlow = 0.12f;               // places run along the net at this rate per second (0 -> 1 toward the tip)
        public const float ArmTipFrac = 0.015f;           // worms that rear up at the tip
        public const float ArmTipPitch = 70f * 0.017453292f;     // pitch of a rearing worm, rad
        // what the GPU adds: the net is a kinematic pull of the nodes toward their place, and the size of a worm fades near the two ends of the conveyor (no pop at the wrap)
        public const float ArmFade = 0.08f;               // share of the length over which a worm grows from nothing at the root and shrinks to nothing at the tip
        public const float ArmK = 0.15f;                  // share of the gap to its place a node closes per step
        public const float ArmPullMax = 0.03f;            // largest pull of a node per step (1.8 m/s)
        public const float ArmRetractRate = 0.7f;         // the ramp of the net given to the GPU falls at this rate per second at most (the visual ramp falls in 0.4 s: the worms, pulled at 1.8 m/s, would be left far outside the dome and thrown back by the containment)
        public const float ArmOut = 1.15f;                // a worm whose centre is further than this (in footprint radii) from the centre of the dome has left the net
        public const float ArmLeaveTime = 0.15f;          // a worm that left the net shrinks to nothing in this time before the containment takes it back
        public const float ArmGone = 0.02f;               // size under which a worm that left the net is gone
        public const float ArmNetBack = 0.5f;             // the net corridor (where a worm of the net, or one that left it, is not contained): from this far behind the centre of the dome ...
        public const float ArmNetSlack = 1.0f;            // ... to ArmStart r + ArmMax + this ahead of it, along the direction of the net,
        public const float ArmNetLat = 0.6f;              // and across it, within this (a worm thrown sideways, through a wall, is not of the net: the containment takes it back)
        public const float ArmSnap = 0.02f;             // a worm whose fade is under this (invisible) goes straight to its place
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
    /// Parameters of one mass (208 bytes): the layout of <c>MassParams</c> in WormSim.compute. Every position is in the local frame of the mass
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
        public Vector4 Pseudo;     // pseudopod toward the target: xy horizontal unit direction (local frame), z length of the net past its start (already times the ramp; 0 = no net), w ramp 0..1
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
