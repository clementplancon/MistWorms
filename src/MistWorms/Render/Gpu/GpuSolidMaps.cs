using UnityEngine;

namespace MistWorms.Render.Gpu
{
    /// <summary>
    /// Depth maps of an axis aligned box, as the environment capture would measure them with its rays (for the tests and the editor check of <c>SolidPush</c>): in the layout of
    /// <see cref="GpuMass.Sides"/>, local frame. West, east, south and north maps: the x or z of the first surface met, none = +-1e6; top map: the y of the top, none = -1e6.
    /// </summary>
    internal static class GpuSolidMaps
    {
        public static void FillBox(float[] dst, int offset, in GpuMassParams m, Vector3 min, Vector3 max)
        {
            int hn = WormSimConst.HeightN, cells = hn * hn;
            float x0 = m.GridRect.x, z0 = m.GridRect.y, sp = m.GridRect.z, y0 = m.Side.x, sy = m.Side.y;
            for (int a = 0; a < hn; a++)
                for (int b = 0; b < hn; b++)
                {
                    int c = a * hn + b;
                    float y = y0 + a * sy, across = z0 + b * sp, acrossX = x0 + b * sp;
                    bool yIn = y >= min.y && y <= max.y;
                    bool zIn = across >= min.z && across <= max.z, xIn = acrossX >= min.x && acrossX <= max.x;
                    dst[offset + c] = yIn && zIn ? min.x : 1e6f;
                    dst[offset + cells + c] = yIn && zIn ? max.x : -1e6f;
                    dst[offset + 2 * cells + c] = yIn && xIn ? min.z : 1e6f;
                    dst[offset + 3 * cells + c] = yIn && xIn ? max.z : -1e6f;
                    // top map: a = row (z), b = column (x)
                    float zt = z0 + a * sp, xt = x0 + b * sp;
                    dst[offset + 4 * cells + c] = xt >= min.x && xt <= max.x && zt >= min.z && zt <= max.z ? max.y : -1e6f;
                }
        }
    }
}
