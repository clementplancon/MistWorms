// Layout of one mass (208 bytes), shared by WormSim.compute and WormRope.shader. Mirrors GpuMassParams (GpuWormTypes.cs, WormSimConst.MassStride).
#ifndef MW_WORM_MASS_PARAMS
#define MW_WORM_MASS_PARAMS
struct MassParams
{
    float4 origin;      // xyz world origin, w = simulated
    float4 center;      // xyz centre of the dome base, w = r
    float4 heading;     // xyz horizontal forward, w = speed
    float4 shape;       // x along the heading, y up, z across, w forward shift
    float4 motion;      // x conveyor, y creep scale, z home pull, w engulf
    float4 sleeve;      // x mode (0 dome, 1 engulf), y shell thickness, z adhesive capsule index, w ground friction
    uint4 ranges;       // x first worm, y worm count, z prim count, w mass index
    float4 gridRect;    // x, y min x and z of the height grid, z spacing, w 1 / spacing
    float4 skin;        // x anchors (0 = none), y engulf, z height of the target, w area of its surface
    float4 side;        // x lowest height of the side maps (local), y spacing, z 1 / spacing, w valid
    float4 plan;        // skin sleeve: x worms of the sleeve S, y worms per anchor, z height (rel) of the top of the covered surface, w anchors covered
    float4 plan2;       // x anchors of the base (where a worm enters the body), y worms alive (0 = all): the others shrink to nothing
    float4 pseudo;      // pseudopod: xy horizontal unit direction to the target, z length of the net past its start (times the ramp, 0 = none), w ramp
};
#endif
