using System;
using UnityEngine;

namespace MistWorms.Mass
{
    /// <summary>
    /// Visual root of a mass (plan 4B-3, "décalage résorbé"): <c>root = body + offset</c>. Only the displacement that exceeds a plausible
    /// speed feeds the offset, which then decays, so a normal walk has no lag while the teleports of StepAssist and of the fall safety net
    /// are hidden. Above <see cref="Snap"/> the root is put on the body at once. Pure maths (no physics), unit tested.
    /// </summary>
    internal sealed class MassRoot
    {
        public const float OffsetHalfLife = 0.12f;
        public const float Snap = 4f;
        public const float JumpSlack = 0.05f;
        /// <summary>The root never trails the body by more than this horizontally: the part of a hop beyond it is absorbed at once, the rest is resorbed.</summary>
        public const float MaxOffset = 0.4f;
        /// <summary>
        /// The body moves in steps of the physics tick (50 Hz) while the frames come at another rate: the position seen from the frames jitters by up
        /// to a tick of walking (5 cm at full speed), which makes anything anchored to the world look like it trembles (the poured worms, 0.6 m/s).
        /// The root therefore integrates the speed measured between two steps of the body and is pulled to the body with this half-life.
        /// </summary>
        public const float CorrectionHalfLife = 0.08f;
        public const float VelocityHalfLife = 0.06f;
        public const float UpHalfLife = 0.12f;
        public const float DownHalfLife = 0.08f;

        /// <summary>Smoothed root (world).</summary>
        public Vector3 Root;
        /// <summary>Horizontal offset still to resorb (root - body, xz).</summary>
        public Vector3 Offset;
        /// <summary>True for the step that put the root on the body at once (first step or snap); the ground must be sampled again.</summary>
        public bool Snapped;
        public bool Started => _has;

        private Vector3 _prevBody;
        private bool _has;
        private float _sx, _sz, _vx, _vz, _since;     // smoothed body position, its measured speed, time since the body last moved

        public void Reset() { _has = false; }

        /// <param name="body">transform position of the mass</param>
        /// <param name="groundY">height of the ground plane under the root (when <paramref name="hasGround"/>), else the body height is used</param>
        public void Step(Vector3 body, float dt, float moveSpeed, float groundY, bool hasGround)
        {
            Snapped = false;
            if (!_has)
            {
                _has = true;
                Root = body;
                Offset = Vector3.zero;
                _prevBody = body;
                _sx = body.x; _sz = body.z; _vx = _vz = 0f; _since = 0f;
                Snapped = true;
                return;
            }

            Vector3 d = body - _prevBody;
            _prevBody = body;
            if (d.sqrMagnitude > Snap * Snap)
            {
                Root = body;
                Offset = Vector3.zero;
                _sx = body.x; _sz = body.z; _vx = _vz = 0f; _since = 0f;
                Snapped = true;
                return;
            }

            float dh = (float)Math.Sqrt(d.x * d.x + d.z * d.z);
            float slack = JumpSlack + 1.5f * moveSpeed * dt;
            float excess = dh - slack;
            bool jump = excess > 0f && dh > 1e-6f;
            if (!jump && dt > 0f)
            {
                // Speed of the body: the per-frame displacement averaged over a short window (a hop is not a speed).
                float a = 1f - (float)Math.Pow(2.0, -dt / VelocityHalfLife);
                _vx += (d.x / dt - _vx) * a;
                _vz += (d.z / dt - _vz) * a;
            }

            if (jump)
            {
                float k = excess / dh;
                Offset.x -= d.x * k;
                Offset.z -= d.z * k;
                _sx += d.x; _sz += d.z;                 // the smoothed body follows a hop at once; the offset hides it
            }
            else
            {
                _sx += _vx * dt; _sz += _vz * dt;
                float c = dt > 0f ? 1f - (float)Math.Pow(2.0, -dt / CorrectionHalfLife) : 0f;
                _sx += (body.x - _sx) * c;
                _sz += (body.z - _sz) * c;
            }
            float decay = dt > 0f ? (float)Math.Pow(2.0, -dt / OffsetHalfLife) : 1f;
            Offset.x *= decay;
            Offset.z *= decay;
            float om = (float)Math.Sqrt(Offset.x * Offset.x + Offset.z * Offset.z);
            if (om > MaxOffset)
            {
                float k = MaxOffset / om;
                Offset.x *= k;
                Offset.z *= k;
            }
            Root.x = _sx + Offset.x;
            Root.z = _sz + Offset.z;

            float target = hasGround ? groundY : body.y;
            float hl = target > Root.y ? UpHalfLife : DownHalfLife;
            float f = dt > 0f ? (float)Math.Pow(2.0, -dt / hl) : 1f;
            Root.y = target + (Root.y - target) * f;
        }
    }

    /// <summary>
    /// Ground plane under a mass from five rays every <see cref="Period"/> seconds (centre and four diagonal corners at 0.8 (r + 0.3)):
    /// a height at the centre, two slopes, and one flag per quadrant blocked by a wall. Quadrants follow the layout numbering: angle in turns
    /// from forward toward left, 0 = front-left, 1 = back-left, 2 = back-right, 3 = front-right.
    /// </summary>
    internal sealed class MassGround
    {
        public const float Period = 0.2f;
        public const float ProbeUp = 0.7f;
        public const float ProbeDown = 2.5f;
        public const float Outlier = 0.6f;
        public const float TiltMaxDeg = 25f;
        public const float TiltBlend = 0.6f;
        public const float PlaneHalfLife = 0.1f;
        public const float CornerReach = 0.8f;
        public const float CornerPad = 0.3f;

        public bool Have;
        public float H0, X0, Z0;          // target plane: height H0 at (X0, Z0)
        public float Gx, Gz;              // smoothed slopes (d height / d x, d height / d z)
        public bool Blocked0, Blocked1, Blocked2, Blocked3;
        public int Hits;                  // rays that hit at the last sample (0..5)
        public int Samples;

        private float _tgx, _tgz;
        private float _next;

        /// <summary>Ask for a sample at the next <see cref="Due"/> check (after a snap).</summary>
        public void Force() { _next = 0f; }

        public bool Due(float now) { return now >= _next; }

        public void Schedule(float now, float phase01) { _next = now + Period * (0.85f + 0.3f * phase01); }

        public float HeightAt(float x, float z) { return H0 + Gx * (x - X0) + Gz * (z - Z0); }

        /// <summary>Upward unit normal of the smoothed plane.</summary>
        public Vector3 Normal()
        {
            float l = (float)Math.Sqrt(Gx * Gx + 1f + Gz * Gz);
            return new Vector3(-Gx / l, 1f / l, -Gz / l);
        }

        /// <summary>Slopes follow their target with the plane half-life.</summary>
        public void Smooth(float dt)
        {
            if (!Have) return;
            float f = dt > 0f ? 1f - (float)Math.Pow(2.0, -dt / PlaneHalfLife) : 0f;
            Gx += (_tgx - Gx) * f;
            Gz += (_tgz - Gz) * f;
        }

        /// <summary>
        /// Five rays from <paramref name="center"/> (the root, flat) along the heap axes <paramref name="fwd"/> and <paramref name="left"/> (flat unit vectors).
        /// Heights above the centre by more than <see cref="Outlier"/> flag their quadrant as blocked; they and misses count as flat.
        /// </summary>
        public void Sample(Vector3 center, float rayY, Vector3 fwd, Vector3 left, float radius, int mask)
        {
            Samples++;
            float reach = CornerReach * (radius + CornerPad);
            float d = reach * 0.70710678f;
            float up = rayY + ProbeUp;
            float len = ProbeUp + ProbeDown;

            float hC;
            bool centerHit = Cast(center.x, up, center.z, len, mask, out hC);
            if (!centerHit)
            {
                if (!Have) return;
                hC = HeightAt(center.x, center.z);
            }
            int hits = centerHit ? 1 : 0;

            float hFL = Corner(center, fwd, left, d, d, up, len, mask, hC, 0, ref hits);
            float hBL = Corner(center, fwd, left, -d, d, up, len, mask, hC, 1, ref hits);
            float hBR = Corner(center, fwd, left, -d, -d, up, len, mask, hC, 2, ref hits);
            float hFR = Corner(center, fwd, left, d, -d, up, len, mask, hC, 3, ref hits);

            float inv = 1f / (4f * d);
            float gF = ((hFL + hFR) - (hBL + hBR)) * inv;
            float gL = ((hFL + hBL) - (hFR + hBR)) * inv;
            _tgx = gF * fwd.x + gL * left.x;
            _tgz = gF * fwd.z + gL * left.z;
            H0 = hC; X0 = center.x; Z0 = center.z;
            Hits = hits;
            if (!Have)
            {
                Have = true;
                Gx = _tgx; Gz = _tgz;
            }
        }

        private float Corner(Vector3 c, Vector3 f, Vector3 l, float af, float al, float up, float len, int mask, float hC, int quad, ref int hits)
        {
            float x = c.x + f.x * af + l.x * al;
            float z = c.z + f.z * af + l.z * al;
            bool blocked = false;
            float h = hC;
            if (Cast(x, up, z, len, mask, out float hit))
            {
                hits++;
                if (hit - hC > Outlier) blocked = true;          // a wall or a block in that quadrant: flat
                else if (hit - hC >= -Outlier) h = hit;           // a drop deeper than the outlier is an edge: flat as well
            }
            switch (quad)
            {
                case 0: Blocked0 = blocked; break;
                case 1: Blocked1 = blocked; break;
                case 2: Blocked2 = blocked; break;
                default: Blocked3 = blocked; break;
            }
            return h;
        }

        private static bool Cast(float x, float y, float z, float len, int mask, out float height)
        {
            if (Physics.Raycast(new Vector3(x, y, z), Vector3.down, out RaycastHit hit, len, mask, QueryTriggerInteraction.Ignore))
            {
                height = hit.point.y;
                return true;
            }
            height = 0f;
            return false;
        }

        /// <summary>Up axis of the heap: the world up blended toward the ground normal (0.6) and limited to 25 degrees.</summary>
        public Vector3 UpAxis()
        {
            if (!Have) return Vector3.up;
            Vector3 n = Normal();
            Vector3 u = new Vector3(n.x * TiltBlend, 1f - TiltBlend + n.y * TiltBlend, n.z * TiltBlend);
            float hx = u.x, hz = u.z;
            float hh = (float)Math.Sqrt(hx * hx + hz * hz);
            float tan = hh / Math.Max(u.y, 1e-4f);
            float maxTan = (float)Math.Tan(TiltMaxDeg * Mathf.Deg2Rad);
            if (tan > maxTan)
            {
                float k = maxTan / tan;
                hx *= k; hz *= k;
            }
            float uy = Math.Max(u.y, 1e-4f);
            float l = (float)Math.Sqrt(hx * hx + uy * uy + hz * hz);
            return new Vector3(hx / l, uy / l, hz / l);
        }
    }
}
