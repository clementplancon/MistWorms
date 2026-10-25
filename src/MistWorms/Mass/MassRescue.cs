namespace MistWorms.Mass
{
    /// <summary>
    /// The fall safety net of a mass (spec §4.6, decision 102), as a pure rule. A mass is put back on its last stable ground only when it really left the
    /// world: no floor at all under it, or (outdoors) under the terrain, or falling faster than <see cref="MaxFallSpeed"/>. A fall to a real floor, however deep
    /// (a mass that jumps off a ledge), is left to the physics: the old rule "3 m under the last ground" held it at the edge for ever.
    /// </summary>
    public static class MassRescue
    {
        /// <summary>Downward speed (m/s) over which the mass is put back whatever lies under it.</summary>
        public const float MaxFallSpeed = 30f;

        /// <summary>Metres under the heightmap, outdoors, from which the mass is under the terrain.</summary>
        public const float TerrainMargin = 1f;

        /// <summary>Length (m) of the ray that looks for a floor under the mass.</summary>
        public const float FloorProbe = 50f;

        public static bool Needed(bool floorBelow, bool outdoors, bool hasTerrainHeight, float y, float terrainY, float verticalSpeed)
        {
            if (!floorBelow) return true;
            if (outdoors && hasTerrainHeight && y < terrainY - TerrainMargin) return true;
            return verticalSpeed < -MaxFallSpeed;
        }
    }
}
