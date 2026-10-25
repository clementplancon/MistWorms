using UnityEngine;

namespace MistWorms.Render.Gpu
{
    /// <summary>
    /// How well the sleeve wraps the body, measured on what the GPU holds (nodes, modes of the worms, anchors read back): shared by the unit tests, the editor check (SimCheck) and the
    /// scenario V14. A worm is of the sleeve when it is settled on the body (mode 2); it is on the skin when at least 6 of its 8 nodes are within 4 cm of the surface of their
    /// nearest anchor (the plane distance) and within 8 cm of that anchor.
    /// </summary>
    internal static class GpuSkinMetrics
    {
        public const int NearNodes = 6;
        public const float NearPlane = 0.04f, NearAnchorDistance = 0.08f;

        public sealed class Result
        {
            public int Worms, Pile, Transit, Settled, Falling, Near;
            /// <summary>Worms on the skin per region of the body: left leg, right leg, left arm, right arm, torso and head.</summary>
            public readonly int[] NearRegion = new int[5];
            public float NearShare => Settled > 0 ? Near / (float)Settled : 0f;
        }

        /// <summary>The region of an anchor: legs under half the height, arms between half and 0.88 of it and farther than 19 cm from the axis of the body, the rest torso and head.</summary>
        public static void Regions(GpuAnchor[] anchors, int count, Vector3 right, Vector3 bodyCenter, int[] region)
        {
            for (int a = 0; a < count; a++)
            {
                float lat = Vector3.Dot(new Vector3(anchors[a].A.x, anchors[a].A.y, anchors[a].A.z) - bodyCenter, right), rel = anchors[a].B.w;
                region[a] = rel < 0.5f ? (lat < 0f ? 0 : 1) : (rel < 0.88f && Mathf.Abs(lat) > 0.19f ? (lat < 0f ? 2 : 3) : 4);
            }
        }

        /// <summary>The region of a limb of the synthetic body (<see cref="GpuSkinAnchors.SyntheticLimbs"/>).</summary>
        public static int RegionOfLimb(int limb)
        {
            switch (limb)
            {
                case 2: case 4: return 0;
                case 3: case 5: return 1;
                case 6: return 2;
                case 7: return 3;
                default: return 4;
            }
        }

        public static Result Measure(Vector4[] nodes, Vector4[] state, GpuAnchor[] anchors, int anchorCount, int[] region, int worms, float anchorSpacing = 0f)
        {
            var r = new Result { Worms = worms };
            float reach = Mathf.Max(NearAnchorDistance, 1.2f * anchorSpacing);       // the anchors of a real body are 8 cm apart: a node between two is at most that from the nearest
            for (int w = 0; w < worms; w++)
            {
                int mode = (int)(state[w].x + 0.5f);
                if (mode == 0) r.Pile++;
                else if (mode == 1) r.Transit++;
                else if (mode == 3) r.Falling++;
                else
                {
                    r.Settled++;
                    int close = 0, centre = -1;
                    for (int j = 0; j < 8; j++)
                    {
                        Vector4 n = nodes[w * 8 + j];
                        int best = -1;
                        float bestD = float.MaxValue;
                        for (int a = 0; a < anchorCount; a++)
                        {
                            float dx = n.x - anchors[a].A.x, dy = n.y - anchors[a].A.y, dz = n.z - anchors[a].A.z;
                            float d = dx * dx + dy * dy + dz * dz;
                            if (d < bestD) { bestD = d; best = a; }
                        }
                        if (best < 0) continue;
                        if (j == 3) centre = best;
                        float plane = (n.x - anchors[best].A.x) * anchors[best].B.x + (n.y - anchors[best].A.y) * anchors[best].B.y + (n.z - anchors[best].A.z) * anchors[best].B.z;
                        if (Mathf.Sqrt(bestD) <= reach && Mathf.Abs(plane) <= NearPlane) close++;
                    }
                    if (close >= NearNodes && centre >= 0)
                    {
                        r.Near++;
                        r.NearRegion[region[centre]]++;
                    }
                }
            }
            return r;
        }
    }
}
