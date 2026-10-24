using UnityEngine;

namespace MistWorms.Colony
{
    internal enum StepResult
    {
        Moved,
        Blocked,    // a wall stops the step (after trying to slide along it)
        NoGround,   // no floor within the allowed drop: an edge or a hole
        Steep,      // the floor is steeper than the limit
        Ceiling,    // not enough room above the worm
    }

    /// <summary>
    /// Layer masks and collision queries of the virtual worms (spec §3.3, §11.1). Worms have no collider, so this is the
    /// only thing keeping them out of walls and floors: a position is never accepted without a wall check and a floor hit.
    /// No allocation: single-hit queries only.
    /// </summary>
    internal static class WormPhysics
    {
        public const float StepHeight = 0.4f;          // spec §3.3: one step up
        public const float MaxDrop = 0.6f;
        public const float MaxSlopeDeg = 60f;          // spec §3.3
        public const float WallHeight = StepHeight + GroundProbeMargin; // 0.45: the wall probe clears the tallest climbable step
        public const float WallRadius = 0.05f;
        public const float CeilingClearance = 0.25f;
        public const float GroundProbeMargin = 0.05f;
        public const float FallProbe = 20f;

        private static int s_solid;
        private static int s_viewBlock;

        /// <summary>BaseAI.m_solidRayMask (BaseAI.cs:221).</summary>
        public static int SolidMask
        {
            get
            {
                if (s_solid == 0) Init();
                return s_solid;
            }
        }

        /// <summary>BaseAI.m_viewBlockMask (BaseAI.cs:222).</summary>
        public static int ViewBlockMask
        {
            get
            {
                if (s_viewBlock == 0) Init();
                return s_viewBlock;
            }
        }

        private static void Init()
        {
            // Same lists as vanilla BaseAI, so the worms see the world the way vanilla creatures do.
            s_solid = LayerMask.GetMask(VanillaNames.LayerDefault, VanillaNames.LayerStaticSolid, VanillaNames.LayerDefaultSmall,
                VanillaNames.LayerPiece, VanillaNames.LayerTerrain, VanillaNames.LayerVehicle);
            s_viewBlock = LayerMask.GetMask(VanillaNames.LayerDefault, VanillaNames.LayerStaticSolid, VanillaNames.LayerDefaultSmall,
                VanillaNames.LayerPiece, VanillaNames.LayerTerrain, VanillaNames.LayerViewblock, VanillaNames.LayerVehicle);
        }

        /// <summary>Floor under <paramref name="p"/>: from StepHeight above to MaxDrop below.</summary>
        public static bool ProbeGround(Vector3 p, out RaycastHit hit)
        {
            float up = StepHeight + GroundProbeMargin;
            return Physics.Raycast(p + Vector3.up * up, Vector3.down, out hit, up + MaxDrop, SolidMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Floor far below (a worm whose floor vanished), up to <see cref="FallProbe"/>.</summary>
        public static bool ProbeFall(Vector3 p, out RaycastHit hit)
        {
            return Physics.Raycast(p + Vector3.up * (StepHeight + GroundProbeMargin), Vector3.down, out hit, FallProbe, SolidMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// One crawl step. <paramref name="step"/> is horizontal. Order: wall (with one slide attempt), ceiling, floor.
        /// On <see cref="StepResult.Moved"/> the new position sits on the floor.
        /// </summary>
        public static StepResult TryStep(Vector3 pos, Vector3 step, out Vector3 newPos, out Vector3 newNormal)
        {
            newPos = pos;
            newNormal = Vector3.up;
            step.y = 0f;
            float len = step.magnitude;
            if (len < 1e-5f) return StepResult.Moved;

            // Wall: a thin sphere cast along the step at the worm's body height.
            Vector3 origin = pos + Vector3.up * WallHeight;
            Vector3 dir = step / len;
            if (Physics.SphereCast(origin, WallRadius, dir, out RaycastHit wall, len + 0.1f, SolidMask, QueryTriggerInteraction.Ignore))
            {
                // Slide along the wall: the step projected on the wall plane, kept horizontal.
                Vector3 slide = Vector3.ProjectOnPlane(step, wall.normal);
                slide.y = 0f;
                float slideLen = slide.magnitude;
                if (slideLen < 1e-4f) return StepResult.Blocked;
                Vector3 slideDir = slide / slideLen;
                if (Physics.SphereCast(origin, WallRadius, slideDir, out wall, slideLen + 0.1f, SolidMask, QueryTriggerInteraction.Ignore))
                    return StepResult.Blocked;
                step = slide;
            }

            Vector3 candidate = pos + step;

            // Floor: this is the projection that keeps the worm out of the ground. A rise above one step is a wall.
            if (!ProbeGround(candidate, out RaycastHit ground)) return StepResult.NoGround;
            if (ground.point.y - pos.y > StepHeight) return StepResult.Blocked;
            if (Vector3.Angle(ground.normal, Vector3.up) > MaxSlopeDeg) return StepResult.Steep;

            // Ceiling, measured from the floor actually stepped on: do not squeeze under slabs.
            if (Physics.Raycast(ground.point + Vector3.up * 0.05f, Vector3.up, CeilingClearance, SolidMask, QueryTriggerInteraction.Ignore))
                return StepResult.Ceiling;

            newPos = ground.point;
            newNormal = ground.normal;
            return StepResult.Moved;
        }
    }
}
