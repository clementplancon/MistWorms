using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using C = MistWorms.Mass.MassLayoutConst;

namespace MistWorms.Mass
{
    /// <summary>Plain float3 (no UnityEngine dependency in the hot code; the layout is pure and testable out of game).</summary>
    internal struct V3
    {
        public float X, Y, Z;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public V3(float x, float y, float z) { X = x; Y = y; Z = z; }
    }

    /// <summary>Inputs of one frame (plan 4B-3, MassFrameParams). Local frame of the heap: +X forward, +Y up, +Z LEFT.</summary>
    internal struct MassFrameParams
    {
        public float R;               // displayed radius r(n), smoothed
        public int N;                 // worms counted (gameplay)
        public float Sigma;           // smoothed speed ratio v / MoveSpeed, 0..1
        public float StreamK;         // 0 free form .. 1 ribbon (lobes aligned in the corridor)
        public float RibbonW, RibbonL;
        public float T;               // clock, seconds in [0, ClockPeriod)
        public float Tread, Boil;     // integrators: treadmill / caterpillar phase (cycles), boil phase (cycles)
        public float OdoFlux, OdoBorder;
        public float TargetTurns;     // target heading in the local frame (turns from +X toward +Z)
        public float TargetDist;      // target distance from the heap centre; negative = no target
        public float PseudoRamp;      // 0..1: PseudoLobe stretches toward the target
        public float Engulf;          // 0..1: EngulfLobe centres on the target and climbs around it
        public bool Blocked0, Blocked1, Blocked2, Blocked3;   // ground quadrants blocked by a wall (border worms stay inside)
        public bool HasCam;           // inner-place culling against CamL
        public V3 CamL;               // unit vector toward the camera, local frame

        /// <summary>Local target position (x forward, z left), valid when TargetDist >= 0.</summary>
        public V3 TargetLocal() { return new V3(TargetDist * MassLayout.CosT(TargetTurns), 0f, TargetDist * MassLayout.SinT(TargetTurns)); }

        public static MassFrameParams Default(float r, int n)
        {
            var p = new MassFrameParams();
            p.R = r; p.N = n; p.TargetDist = -1f;
            return p;
        }
    }

    /// <summary>One evaluated slot. Role 0 body, 1 flux, 2 border. Anim: WormAnim numbering (Crawl 0, Idle 4, Coil 5).</summary>
    internal struct SlotOut
    {
        public int I;
        public bool Visible;
        public byte Role, Anim;
        public int Frame;
        public int Lobe;              // lobe of the slot (-1: border worms)
        public V3 Pos, Fwd, Up;       // local frame
        public float Scale, Prio;
        /// <summary>True when the worm deserves the LOD0 mesh: surface flux facing the camera, border, sheath and arm worms; bodies and hidden-layer flux use LOD1.</summary>
        public bool Lod0;
    }

    /// <summary>Instance sink of <see cref="MassLayout.Emit{T}"/> (a struct implementing this keeps the call allocation free).</summary>
    internal interface IHeapSink
    {
        void AddWorm(in SlotOut o);
    }

    /// <summary>
    /// Port of assets/blender/mass_layout.py, line by line (that file is the reference; assets/build/mass_layout_golden.json is the
    /// parity contract). Floats only, hash `lowbias32`, trigonometry only through the 256 entry sine table (argument in TURNS),
    /// no allocation. Local frame: +X forward, +Y up, +Z left; local -> Unity world is x*F + y*U + z*L (L = left), a proper rotation,
    /// so a worm's right axis is cross(U, F) in both frames.
    /// </summary>
    internal static class MassLayout
    {
        public const byte RoleBody = 0, RoleFlux = 1, RoleBorder = 2;
        public const byte AnimCrawl = 0, AnimIdle = 4, AnimCoil = 5;
        public const int CrawlFrames = 16, IdleFrames = 8, CoilFrames = 12;

        // ------------------------------------------------------------------------------------------ derived constants (turns)
        public static readonly float BodyRollMaxT = C.BodyRollMax / 360f;
        public static readonly float BodyPitchMaxT = C.BodyPitchMax / 360f;
        public static readonly float EmergePitchT = C.EmergePitch / 360f;
        public static readonly float DivePitchT = C.DivePitch / 360f;
        public static readonly float TreadLaneFanT = C.TreadLaneFan / 360f;
        public static readonly float ArmTipPitchT = C.ArmTipPitch / 360f;
        public static readonly float EngulfAcrossPitchT = C.EngulfAcrossPitch / 360f;
        public static readonly float EngulfPitchMinT = C.EngulfPitchMin / 360f;
        public static readonly float EngulfPitchMaxT = C.EngulfPitchMax / 360f;
        public static readonly float FluxRollMaxT = C.FluxRollMax / 360f;
        public static readonly float TwoPi = (float)(2.0 * Math.PI);
        public static readonly float GoldenTurn = 0.3819660112501051f;
        static readonly float[] DriftRate = MakeDriftRate();
        static readonly float[] SinLut = MakeSinLut();

        static float[] MakeDriftRate()
        {
            var d = new float[C.BodyDriftPeriods.Length];
            for (int i = 0; i < d.Length; i++) d[i] = 1f / C.BodyDriftPeriods[i];
            return d;
        }

        static float[] MakeSinLut()
        {
            var t = new float[257];
            for (int i = 0; i < 256; i++) t[i] = (float)Math.Sin(2.0 * Math.PI * i / 256.0);
            t[256] = 0f;
            return t;
        }

        /// <summary>Sum of the table (checked against the golden file).</summary>
        public static double SinLutSum() { double s = 0; for (int i = 0; i < SinLut.Length; i++) s += SinLut[i]; return s; }

        // ------------------------------------------------------------------------------------------ hash
        public static uint LowBias32(uint x)
        {
            unchecked
            {
                x ^= x >> 16;
                x *= 0x7FEB352Du;
                x ^= x >> 15;
                x *= 0x846CA68Bu;
                x ^= x >> 16;
                return x;
            }
        }

        public static uint HashU32(uint seed, uint a, uint b)
        {
            unchecked
            {
                uint h = LowBias32(seed ^ LowBias32(a + 0x9E3779B9u));
                return LowBias32(h ^ LowBias32(b + 0x85EBCA6Bu));
            }
        }

        /// <summary>[0,1) with 24 bits, exact in float.</summary>
        public static float Hash01(uint seed, uint a, uint b) { return (float)(HashU32(seed, a, b) >> 8) * (1f / 16777216f); }

        /// <summary>Seed of a heap: H(uid ^ uid>>32) ^ H(id * 0x9E3779B9).</summary>
        public static uint SeedFromIds(long userId, uint id)
        {
            unchecked
            {
                ulong u = (ulong)userId;
                uint a = (uint)(u ^ (u >> 32));
                return LowBias32(a) ^ LowBias32(id * 0x9E3779B9u);
            }
        }

        // ------------------------------------------------------------------------------------------ scalar helpers
        /// <summary>floor() as an int cast and a correction: Math.Floor is a call (not inlined) on Mono, this is three instructions. Exact below 2^31.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FloorI(float x) { int i = (int)x; return x < (float)i ? i - 1 : i; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FloorF(float x) { int i = (int)x; return x < (float)i ? (float)(i - 1) : (float)i; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Frac(float x) { return x - FloorF(x); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Clamp01(float x) { return x < 0f ? 0f : (x > 1f ? 1f : x); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Smooth01(float x) { x = Clamp01(x); return x * x * (3f - 2f * x); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float Rsqrt(float x) { return 1f / (float)Math.Sqrt(x); }

        /// <summary>sin(2 pi turns) from the 256 entry table, linear interpolation.</summary>
        public static float SinT(float turns)
        {
            float x = Frac(turns) * 256f;
            if (x >= 256f) x -= 256f;
            int i = FloorI(x);
            float fr = x - (float)i;
            float a = SinLut[i];
            return a + (SinLut[i + 1] - a) * fr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CosT(float turns) { return SinT(turns + 0.25f); }

        // ------------------------------------------------------------------------------------------ vectors
        static readonly V3 Up = new V3(0f, 1f, 0f);
        static readonly V3 FwdX = new V3(1f, 0f, 0f);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static V3 Add(V3 a, V3 b) { return new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static V3 Sub(V3 a, V3 b) { return new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static V3 Mul(V3 a, float s) { return new V3(a.X * s, a.Y * s, a.Z * s); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float Dot(V3 a, V3 b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static V3 Cross(V3 a, V3 b) { return new V3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static V3 VLerp(V3 a, V3 b, float t) { return new V3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static V3 Norm(V3 a, V3 fallback)
        {
            float l2 = Dot(a, a);
            if (l2 < 1e-10f) return fallback;
            return Mul(a, Rsqrt(l2));
        }

        // ------------------------------------------------------------------------------------------ envelope (footprint of the heap, border ring)
        struct Env { public float AF, AB, AS, H, Breath, Rp, WScale; }

        public static float RPlace(int n, float r)
        {
            float k = Clamp01((float)(n - C.TangleFullN) / (float)C.TangleSpan);
            float cap = C.TangleR0 + C.TangleK * (float)n;
            float small = r < cap ? r : cap;
            return Lerp(small, r, k);
        }

        static Env Envelope(in MassFrameParams P, float rp)
        {
            Env e;
            float s = P.Sigma;
            e.Rp = rp;
            e.WScale = Lerp(C.HeapScaleR0, C.HeapScaleR1, Clamp01((rp - C.HeapScaleRMin) / (C.HeapScaleRMax - C.HeapScaleRMin)));
            e.AF = rp * (1f + C.StretchFront * s);
            e.AB = rp * (1f + C.StretchBack * s);
            e.AS = rp * (1f - C.SideNarrow * s);
            e.H = C.HeightFrac * rp;
            e.Breath = 1f;
            float k = P.StreamK;
            if (k > 0f)
            {
                if (P.RibbonW > 0f)
                {
                    float half = 0.5f * P.RibbonW;
                    e.AS = Lerp(e.AS, e.AS < half ? e.AS : half, k);
                }
                if (P.RibbonL > 0f)
                {
                    e.AF = Lerp(e.AF, 0.5f * P.RibbonL, k);
                    e.AB = Lerp(e.AB, 0.5f * P.RibbonL, k);
                }
            }
            return e;
        }

        static void Surface(in Env e, in MassFrameParams P, float X, float Z, out V3 pos, out V3 n)
        {
            float ax = X > 0f ? e.AF : e.AB;
            float q = 1f - X * X - Z * Z;
            float Y = (float)Math.Sqrt(q > 0f ? q : 0f);
            float drop = 1f - C.FrontDrop * P.Sigma * (X > 0f ? X : 0f);
            pos = new V3(X * ax, e.H * Y * drop * e.Breath, Z * e.AS);
            n = Norm(new V3(X / ax, Y / e.H, Z / e.AS), Up);
        }

        static float FootprintRadius(in Env e, float cx, float sz)
        {
            float ax = cx > 0f ? e.AF : e.AB;
            float a = cx / ax;
            float b = sz / e.AS;
            return Rsqrt(a * a + b * b);
        }

        static void TangentFrame(V3 n, out V3 az, out V3 em)
        {
            az = Norm(new V3(-n.Z, 0f, n.X), new V3(0f, 0f, 1f));
            em = Cross(n, az);
        }

        static V3 BlendDir(V3 f, V3 n, float k)
        {
            V3 tx = Sub(FwdX, Mul(n, Dot(FwdX, n)));
            tx = Norm(tx, f);
            return Norm(VLerp(f, tx, k), f);
        }

        static void Tilt(V3 n, V3 f, float rollT, float pitchT, out V3 f2, out V3 u2)
        {
            V3 u = n;
            V3 r = Cross(u, f);
            float cr = CosT(rollT), sr = SinT(rollT);
            V3 u1 = Add(Mul(u, cr), Mul(r, sr));
            float cp = CosT(pitchT), sp = SinT(pitchT);
            f2 = Add(Mul(f, cp), Mul(u1, sp));
            u2 = Sub(Mul(u1, cp), Mul(f, sp));
        }

        static float WormScale(in Env e, HeapSlots S, int i, byte role)
        {
            float s = e.WScale * (1f + C.ScaleJitter * S.ScaleJit[i]);
            if (role == RoleBorder && s > C.BorderScaleCap) s = C.BorderScaleCap;
            return s;
        }

        // ------------------------------------------------------------------------------------------ lobes
        static float AngleLerp(float a, float b, float k)
        {
            float d = b - a;
            d = d - FloorF(d + 0.5f);
            return a + d * k;
        }

        /// <summary>
        /// The lobes of the frame (once per frame and per heap): the organic envelope, a metaball-like sum of ellipsoids.
        /// K = clamp(1 + n / LobeDiv, 1, LobeMax), continuous: lobe j separates from lobe 0 with weight clamp01(K - j). Centres drift
        /// (periods 3 to 8 s), radii and heights pulse; in motion the lobes align and a caterpillar phase stretches the front ones.
        /// EngulfLobe becomes the low wide base heap at the target's feet (the arm and the sheath are place overrides, see Evaluate).
        /// </summary>
        static void ComputeLobes(in MassFrameParams P, HeapSlots S, float rp)
        {
            float t = P.T, sg = P.Sigma;
            float kf = 1f + (float)P.N / C.LobeDiv;
            kf = kf < 1f ? 1f : (kf > (float)C.LobeMax ? (float)C.LobeMax : kf);
            float rl = rp * C.LobeRadiusK * Rsqrt(kf);
            float rlCap = rp * C.LobeRadiusCap;
            rl = rl > rlCap ? rlCap : rl;
            float hb = C.HeightFrac * rp * (float)Math.Sqrt(rl / rp);
            float span = rp - rl;
            float[] per = C.LobePeriods;
            for (int j = 0; j < C.LobeMax; j++)
            {
                float sj = Clamp01(kf - (float)j);
                float amp = j == 0 ? C.Lobe0DriftFrac : C.LobeDriftFrac;
                float baseR = C.LobeBaseFrac * span * sj;
                float beta = S.LBeta[j];
                float a = amp * span * (j == 0 ? 1f : sj);
                int o = j * 4;
                float dx = a * (0.7f * SinT(t / per[S.LPer[o]] + S.LPh[o]) + 0.3f * SinT(t / per[S.LPer[o + 1]] + S.LPh[o + 1]));
                float dz = a * (0.7f * SinT(t / per[S.LPer[o + 2]] + S.LPh[o + 2]) + 0.3f * SinT(t / per[S.LPer[o + 3]] + S.LPh[o + 3]));
                float cx = baseR * CosT(beta) + dx;
                float cz = baseR * SinT(beta) + dz;
                float cp = P.Tread * C.CatRate + S.LCatPh[j];
                cx = cx + sg * span * sj * (C.CatBias * CosT(beta) + C.CatAmp * SinT(cp));
                cz = cz * (1f - 0.4f * sg);
                // The slow centre: without the drift and the caterpillar swing (they move a lobe at 0.5 m/s while the poured worms must hold still).
                float sxs = baseR * CosT(beta) + sg * span * sj * C.CatBias * CosT(beta);
                float szs = baseR * SinT(beta) * (1f - 0.4f * sg);
                float stretch = 1f + sg * C.CatStretch * (0.5f + 0.5f * SinT(cp + 0.25f)) * (0.6f + 0.4f * CosT(beta));
                float pulse = 1f + C.LobePulse * SinT(t / per[S.LRPer[j]] + S.LRPh[j]);
                float rx = rl * S.LAspS[j] * pulse * stretch;
                float rz = rl * S.LAspI[j] * pulse;
                float ry = hb * S.LHf[j] * (1f + C.LobeHeightPulse * SinT(t / per[S.LHPer[j]] + S.LHPh[j]));
                float psi = (S.LPsi0[j] + C.LobePsiSwing * SinT(t / C.LobePsiPeriod + S.LPsiPh[j])) * (1f - sg);
                float ltx = rl * S.LAspS[j], lty = hb * S.LHf[j];
                float k = P.StreamK;
                if (k > 0f)
                {
                    float lx = (((float)j + 0.5f) / (float)C.LobeMax - 0.5f) * (P.RibbonL > 0f ? P.RibbonL : 2f * rp);
                    cx = Lerp(cx, lx, k);
                    cz = cz * (1f - k);
                    sxs = Lerp(sxs, lx, k);
                    szs = szs * (1f - k);
                    if (P.RibbonW > 0f)
                    {
                        float half = 0.5f * P.RibbonW;
                        rz = Lerp(rz, rz < half ? rz : half, k);
                    }
                    if (P.RibbonL > 0f)
                    {
                        float seg = P.RibbonL / (float)C.LobeMax;
                        rx = Lerp(rx, rx > seg ? rx : seg, k);
                    }
                    ry = ry * Lerp(1f, C.StreamHeightFactor, k);
                    psi = psi * (1f - k);
                }
                if (j == C.EngulfLobe && P.Engulf > 0f && P.TargetDist >= 0f)
                {
                    float es = Smooth01(P.Engulf);
                    float tx = P.TargetDist * CosT(P.TargetTurns), tz = P.TargetDist * SinT(P.TargetTurns);
                    cx = Lerp(cx, tx, es);
                    cz = Lerp(cz, tz, es);
                    sxs = Lerp(sxs, tx, es);
                    szs = Lerp(szs, tz, es);
                    rx = Lerp(rx, C.BaseRadius, es);
                    rz = Lerp(rz, C.BaseRadius, es);
                    ry = Lerp(ry, C.BaseHeight, es);
                    ltx = rx; lty = ry;
                }
                Lobe L;
                L.Cx = cx; L.Cz = cz; L.Rx = rx; L.Ry = ry; L.Rz = rz; L.Psi = psi;
                L.C = CosT(psi); L.S = SinT(psi);
                L.Lt = (float)Math.PI * (float)Math.Sqrt(0.5f * (ltx * ltx + lty * lty)) * (1f + C.TreadStretch * sg) / C.TreadSurfaceFrac * C.TreadLtScale;
                L.Rl = rl;
                L.Ax = ltx;
                L.Sx = sxs; L.Sz = szs;
                S.Lobes[j] = L;
            }
        }

        static V3 ToHeapVec(in Lobe L, V3 p) { return new V3(L.C * p.X - L.S * p.Z, p.Y, L.S * p.X + L.C * p.Z); }
        static V3 ToHeapPt(in Lobe L, V3 p) { return new V3(L.Cx + L.C * p.X - L.S * p.Z, p.Y, L.Cz + L.S * p.X + L.C * p.Z); }

        static Env LobeEnv(in Lobe L)
        {
            Env e;
            e.AF = L.Rx; e.AB = L.Rx; e.AS = L.Rz; e.H = L.Ry; e.Breath = 1f; e.Rp = L.Rl; e.WScale = 1f;
            return e;
        }

        // ------------------------------------------------------------------------------------------ roles
        static void EvalBody(in MassFrameParams P, HeapSlots S, in Env G, in Lobe L, int j, int i, bool occl, ref SlotOut o)
        {
            float t = P.T;
            int rc = S.RateClass[i];
            float turns = Frac(t * DriftRate[rc]);
            float c = CosT(turns), s = SinT(turns);
            float X = S.Dx[i] * c - S.Dz[i] * s, Z = S.Dx[i] * s + S.Dz[i] * c;
            float dy = S.Dy[i];
            float x = Frac(t / C.BodyDivePeriods[S.DiveClass[i]] + S.DivePh[i]);
            float y01 = 0.5f - 0.5f * CosT(x);
            float pulse = Smooth01((y01 - 0.3f) / 0.5f);
            float rho = S.Rho0[i] * (1f - C.BodyDiveFrac * pulse);
            float wsc = WormScale(G, S, i, RoleBody);
            V3 nl = Norm(new V3(X / L.Rx, dy / L.Ry, Z / L.Rz), Up);
            V3 p = new V3(rho * L.Rx * X, rho * L.Ry * dy, rho * L.Rz * Z);
            p = Sub(p, Mul(nl, C.CoilHalfHeight * wsc));
            p = new V3(p.X, p.Y > 0f ? p.Y : 0f, p.Z);
            V3 pos = ToHeapPt(L, p);
            V3 nn = ToHeapVec(L, nl);
            bool hidden = false;
            if (occl)
            {
                float dd = Dot(nn, P.CamL);
                if (dd < C.OccludeDot || (1f - rho) * (1f + dd) > C.InnerCull) hidden = true;
            }
            V3 az, em;
            TangentFrame(nn, out az, out em);
            float psi = S.Psi0[i] + C.BodyHeadingSwing / TwoPi * SinT(t * (float)(rc + 1) / 12f + S.AnimPhase[i]);
            V3 f = Add(Mul(az, CosT(psi)), Mul(em, SinT(psi)));
            f = BlendDir(f, nn, C.BodyHeadingBias * P.Sigma);
            V3 f2, u2;
            Tilt(nn, f, S.Roll[i], S.Pitch[i], out f2, out u2);
            o.I = i; o.Visible = !hidden; o.Role = RoleBody; o.Anim = AnimCoil; o.Lobe = j; o.Lod0 = false;
            o.Frame = FloorI(t * 10f + (float)i * C.CoilPhaseStep) % CoilFrames;
            o.Pos = pos; o.Fwd = f2; o.Up = u2; o.Prio = S.Prio[i];
        }

        static void BoilLine(HeapSlots S, int i, int c, float rl, out float sx, out float sz, out float ex, out float ez, out float bow)
        {
            // The four hashes of a boil cycle (12 mixes) only change when the cycle does (every few seconds): cached per place.
            int cmi = c & 255;
            float[] bh = S.BoilH;
            int hi = i * 4;
            if (S.BoilCm[i] != cmi)
            {
                uint a = (uint)(i + 1);
                uint cm = (uint)cmi;
                bh[hi] = Hash01(S.Seed, a, 64 + cm * 4);
                bh[hi + 1] = Hash01(S.Seed, a, 65 + cm * 4);
                bh[hi + 2] = Hash01(S.Seed, a, 66 + cm * 4);
                bh[hi + 3] = Hash01(S.Seed, a, 67 + cm * 4);
                S.BoilCm[i] = cmi;
            }
            float hl = bh[hi], hd = bh[hi + 1], hr = bh[hi + 2], hb = bh[hi + 3];
            float az = Frac(S.BoilPhi[i] + (float)c * GoldenTurn);
            float rho = 0.10f + 0.85f * hr;
            sx = rho * CosT(az); sz = rho * SinT(az);
            float length = Lerp(C.BoilLenMin, C.BoilLenMax, hl) / rl;
            ex = sx + length * CosT(hd);
            ez = sz + length * SinT(hd);
            float e2 = ex * ex + ez * ez;
            if (e2 > 0.9025f)
            {
                float k = 0.95f * Rsqrt(e2);
                ex *= k; ez *= k;
            }
            bow = (hb * 2f - 1f) * C.BoilTwistMax * 0.5f;
        }

        static void EvalFlux(in MassFrameParams P, HeapSlots S, in Lobe L, int j, int i, ref SlotOut o)
        {
            float t = P.T;
            float sg = P.Sigma;
            Env e = LobeEnv(L);
            float lam = S.Lam0[i] + S.LobeTread[j] + C.BoilKs[S.BoilK[i]] * P.Boil;
            int cyc = FloorI(lam);
            float mu = lam - (float)cyc;
            float sxR, szR, exR, ezR, bow;
            BoilLine(S, i, cyc, e.Rp, out sxR, out szR, out exR, out ezR, out bow);
            float l1 = S.Lane[i], l2 = S.Lane2[i];
            float sxT = -CosT(l1), szT = SinT(l1) * 0.8f;
            float exT = CosT(l2), ezT = SinT(l2) * 0.8f;
            float sx = Lerp(sxR, sxT, sg), sz = Lerp(szR, szT, sg);
            float ex = Lerp(exR, exT, sg), ez = Lerp(ezR, ezT, sg);
            float sq = 1f - C.StreamFileSqueeze * P.StreamK;
            sz *= sq; ez *= sq;
            float dx = ex - sx, dz = ez - sz;
            V3 headingH = new V3(dx * e.AF, 0f, dz * e.AS);
            byte anim = AnimCrawl;
            float pitchT = 0f;
            float off = C.FluxLift;
            bool exitBlend = false;
            // Anchoring to the slow centre of the lobe, in from mu = 0.5 to 0.84 (the start of the pour): gentle on the surface, complete when the worm lies still.
            float wpour = mu > 0.5f ? sg * Smooth01((mu - 0.5f) / 0.34f) : 0f;
            bool hidden;
            float X, Z;
            float hideD = C.HideFrac * e.H;
            if (mu < C.EmergeEnd)
            {
                float a = Smooth01(mu / C.EmergeEnd);
                X = sx; Z = sz;
                off = Lerp(C.EmergeDepthFrac * e.H, C.FluxLift, a);
                pitchT = Lerp(EmergePitchT, 0f, a);
                hidden = off < -hideD;
            }
            else if (mu < C.SurfaceEnd)
            {
                float a = (mu - C.EmergeEnd) / (C.SurfaceEnd - C.EmergeEnd);
                float sw = bow * (1f - sg) * 4f * a * (1f - a);
                X = Lerp(sx, ex, a) - dz * sw; Z = Lerp(sz, ez, a) + dx * sw;
                hidden = false;
            }
            else
            {
                float a = Smooth01((mu - C.SurfaceEnd) / (1f - C.SurfaceEnd));
                X = ex; Z = ez;
                off = Lerp(C.FluxLift, C.EmergeDepthFrac * e.H, a);
                pitchT = DivePitchT * a;
                hidden = off < -hideD;
                exitBlend = true;
            }
            float r2 = X * X + Z * Z;
            if (r2 > 0.9801f)
            {
                float k = 0.99f * Rsqrt(r2);
                X *= k; Z *= k;
            }
            V3 posS, n;
            Surface(e, P, X, Z, out posS, out n);
            V3 pos = Add(posS, Mul(n, off));
            V3 f = Sub(headingH, Mul(n, Dot(headingH, n)));
            f = Norm(f, FwdX);
            V3 u = n;
            float cp = CosT(pitchT), sp = SinT(pitchT);
            V3 fN = Add(Mul(f, cp), Mul(u, sp));
            V3 uN = Sub(Mul(u, cp), Mul(f, sp));
            f = fN; u = uN;
            if (exitBlend && sg > 0f)
            {
                float xe = ex * L.Ax;
                float px = xe + C.PourAhead - (mu - C.SurfaceEnd) * L.Lt;
                V3 pour = new V3(px, C.FluxLift, ez * e.AS);
                V3 pf = Norm(new V3(dx * e.AF, 0f, dz * e.AS), FwdX);
                float wp = sg * Smooth01((mu - C.SurfaceEnd) / 0.04f);
                pos = VLerp(pos, pour, wp);
                f = Norm(VLerp(f, pf, wp), pf);
                u = Norm(VLerp(u, Up, wp), Up);
                u = Norm(Sub(u, Mul(f, Dot(u, f))), Up);
                bool pourHidden = px < 0.8f * L.Ax - C.PourHideMargin || mu > C.PourEnd;
                if (sg >= 0.5f) hidden = pourHidden;
                if (mu > C.PourIdleMu && sg > 0.5f) anim = AnimIdle;
            }
            V3 rr = Cross(u, f);
            float cr = CosT(S.RollFlux[i]), sr = SinT(S.RollFlux[i]);
            u = Add(Mul(u, cr), Mul(rr, sr));
            bool lod0Phase = (mu >= C.EmergeEnd && mu < C.SurfaceEnd) || (exitBlend && sg > 0.5f && mu >= C.SurfaceEnd + 0.04f);
            V3 nh = ToHeapVec(L, n);
            pos = ToHeapPt(L, pos); f = ToHeapVec(L, f); u = ToHeapVec(L, u);
            if (wpour > 0f)
            {
                // Anchored to the slow centre of the lobe, not to its drifting and swinging one: the poured worm holds still in the world.
                pos.X += wpour * (L.Sx - L.Cx);
                pos.Z += wpour * (L.Sz - L.Cz);
            }
            int frame;
            if (anim == AnimIdle) frame = FloorI(t * 6f + S.AnimPhase[i] * (float)IdleFrames) % IdleFrames;
            else frame = FloorI(P.OdoFlux + (float)i * C.CrawlPhaseStep) % CrawlFrames;
            o.I = i; o.Visible = !hidden; o.Role = RoleFlux; o.Anim = anim; o.Frame = frame; o.Lobe = j;
            o.Lod0 = lod0Phase && (!P.HasCam || Dot(nh, P.CamL) > C.Lod0FaceDot);
            o.Pos = pos; o.Fwd = f; o.Up = u; o.Prio = S.Prio[i];
        }

        static bool Blocked(in MassFrameParams P, int q)
        {
            switch (q) { case 0: return P.Blocked0; case 1: return P.Blocked1; case 2: return P.Blocked2; default: return P.Blocked3; }
        }

        static void EvalBorder(in MassFrameParams P, HeapSlots S, in Env G, int i, ref SlotOut o)
        {
            float t = P.T;
            float sg = P.Sigma;
            o.I = i; o.Role = RoleBorder; o.Prio = S.Prio[i]; o.Lobe = -1;
            float a;
            if (S.Straggler[i]) a = 0.5f + (S.BoilPhi[i] - 0.5f) * 0.15f;
            else
            {
                float a0 = S.PhiB[i];
                a0 = a0 - FloorF(a0 + 0.5f);
                a = a0 * (1f - C.FrontCompress * sg);
            }
            V3 ur = new V3(CosT(a), 0f, SinT(a));
            V3 ut = new V3(-ur.Z, 0f, ur.X);
            float ell = FootprintRadius(G, ur.X, ur.Z);
            float reach = S.Reach[i];
            reach = reach + C.BorderAheadExtra * Smooth01((sg - 0.4f) / 0.4f) * Smooth01((ur.X - 0.3f) / 0.4f);
            float rho0 = C.BorderTuck * ell;
            float rho1 = ell + reach;
            int quad = FloorI(Frac(a) * 4f) & 3;
            if (Blocked(P, quad))
            {
                rho1 = ell;
                rho0 = rho0 < ell ? rho0 : ell;
            }
            float tb = C.BorderPeriods[S.BorderClass[i]];
            float mu = Frac(S.Lam0[i] + t / tb);
            float pf = C.BorderPauseFrac * (1f - sg);
            float seg = (1f - pf) * 0.5f;
            bool paused = false;
            float w;
            if (mu < seg) w = 0.5f * mu / seg;
            else if (mu < seg + pf) { w = 0.5f; paused = true; }
            else w = 0.5f + 0.5f * (mu - seg - pf) / seg;
            float cw = CosT(w), sw = SinT(w);
            float rad = rho0 + (rho1 - rho0) * (0.5f - 0.5f * cw);
            float tan = C.BorderLoopWidth * sw;
            V3 pos = new V3(ur.X * rad + ut.X * tan, 0f, ur.Z * rad + ut.Z * tan);
            float drad = (rho1 - rho0) * (float)Math.PI * sw;
            float dtan = C.BorderLoopWidth * TwoPi * cw;
            V3 hd = new V3(ur.X * drad + ut.X * dtan, 0f, ur.Z * drad + ut.Z * dtan);
            V3 f = Norm(hd, ur);
            if (paused)
            {
                o.Anim = AnimIdle;
                o.Frame = FloorI(t * 6f + S.AnimPhase[i] * (float)IdleFrames) % IdleFrames;
            }
            else
            {
                o.Anim = AnimCrawl;
                o.Frame = FloorI(P.OdoBorder + (float)i * C.CrawlPhaseStep) % CrawlFrames;
            }
            o.Visible = true; o.Pos = pos; o.Fwd = f; o.Up = Up;
            o.Lod0 = !P.HasCam || (pos.X * P.CamL.X + pos.Z * P.CamL.Z) > 0f;     // LOD0 only on the camera side
        }

        /// <summary>
        /// Place on the sheath around the target: height v * H (an uneven top), 2.2 turns over the height, a radius that tapers from RBottom to
        /// RTop with bulges that rise slowly, strays that stick out, a part that slides down and back up (2 to 4 s), mostly vertical worms.
        /// </summary>
        static void Spiral(in MassFrameParams P, HeapSlots S, int i, float tx, float tz, float es, out V3 pos, out V3 f, out V3 u)
        {
            float t = P.T;
            float v = S.Hv[i];
            bool down = false;
            if (S.ESlide[i] < C.EngulfSlideFrac)
            {
                int cls = Math.Min(FloorI(S.ESlidePh[i] * 3f), 2);
                float x = Frac(t / C.EngulfSlidePeriods[cls] + S.ESlidePh[i] * 7f);
                v = v - C.EngulfSlideAmp * v * (0.5f - 0.5f * CosT(x));
                down = x < 0.5f;
            }
            float ang = S.Hph[i] + C.EngulfTurns * v + t * C.EngulfSpin;
            float top = 1f + C.EngulfTopWobble * SinT(ang + t / 6f) * v * v;
            float hgt = v * C.EngulfHeight * es * top;
            float rr = Lerp(C.EngulfRBottom, C.EngulfRTop, v) * (1f + C.EngulfJitter * (S.Hr[i] * 2f - 1f));
            rr = rr + C.EngulfBulgeAmp * (0.5f + 0.5f * SinT(v * C.EngulfBulges - t * C.EngulfRise));
            bool outS = S.EOut[i] < C.EngulfOutFrac;
            if (outS) rr = rr + C.EngulfOutReach * (0.5f + 0.5f * SinT(t / 3f + S.Hph[i]));
            rr = rr < C.EngulfRMinSheath ? C.EngulfRMinSheath : rr;
            float ca = CosT(ang), sa = SinT(ang);
            pos = new V3(tx + rr * ca, hgt + 0.02f, tz + rr * sa);
            V3 tang = new V3(-sa, 0f, ca);
            float p;
            if (S.EPit[i] < C.EngulfAcrossFrac) p = EngulfAcrossPitchT;
            else p = Lerp(EngulfPitchMinT, EngulfPitchMaxT, (S.EPit[i] - C.EngulfAcrossFrac) / (1f - C.EngulfAcrossFrac));
            if (down) p = -p;
            float cp = CosT(p), sp = SinT(p);
            f = Add(Mul(tang, cp), Mul(Up, sp));
            V3 radial = new V3(ca, 0f, sa);
            if (outS) f = Norm(VLerp(f, radial, 0.5f), f);
            u = Norm(Sub(radial, Mul(f, Dot(radial, f))), radial);
        }

        /// <summary>Orientation of the base heap worms at the target's feet: lying down and interlaced, mostly tangential, only a few turned outward.</summary>
        static void BaseOrient(HeapSlots S, int i, V3 pos, in Lobe L2, out V3 f, out V3 u)
        {
            float rx = pos.X - L2.Cx, rz = pos.Z - L2.Cz;
            V3 r = Norm(new V3(rx, 0f, rz), FwdX);
            float sgn = S.Psi0[i] >= 0.5f ? 1f : -1f;
            V3 t = new V3(-r.Z * sgn, 0f, r.X * sgn);
            float eps = (S.PhiB[i] - 0.5f) * C.BaseSpread;
            if (S.EOut[i] < C.BaseOutFrac) eps = 0.25f;
            V3 ff = Norm(Add(Mul(t, CosT(eps)), Mul(r, SinT(eps))), r);
            Tilt(Up, ff, S.Roll[i] * C.BaseTiltK, S.Pitch[i] * C.BaseTiltK, out f, out u);
        }

        /// <summary>
        /// Place on the arm conveyor toward the target: s runs 0 -> 1 (toward the tip) at ArmFlow per second, the arm undulates and narrows
        /// toward the tip; the places with the smallest threshold rear up at the tip, head toward the target.
        /// </summary>
        static void Arm(in MassFrameParams P, HeapSlots S, int i, float rp, float ln, float dcx, float dcz, out V3 pos, out V3 f, out V3 u, out bool hidden)
        {
            float t = P.T;
            float a0 = C.ArmStart * rp;
            bool tip = S.Thr2[i] < C.ArmTipFrac;
            float s = tip ? 1f : Frac(S.As0[i] + t * C.ArmFlow);
            float waveA = s * C.ArmWaveK - t / C.ArmWavePer;
            float wave = C.ArmWave * s * SinT(waveA);
            float wid = Lerp(C.ArmWidth, C.ArmWidth * C.ArmTaper, s);
            float lat = (S.Ahz[i] - 0.5f) * wid;
            float dist = a0 + s * ln;
            float px = -dcz, pz = dcx;
            pos = new V3(dcx * dist + px * (wave + lat), C.ArmThick * S.Ahy[i] * (1f - 0.4f * s) + 0.03f, dcz * dist + pz * (wave + lat));
            float dw = C.ArmWave * (SinT(waveA) + s * C.ArmWaveK * TwoPi * CosT(waveA)) / (ln > 0.2f ? ln : 0.2f);
            f = Norm(new V3(dcx + px * dw, 0f, dcz + pz * dw), new V3(dcx, 0f, dcz));
            u = Up;
            hidden = false;
            if (tip)
            {
                float cp = CosT(ArmTipPitchT), sp = SinT(ArmTipPitchT);
                f = Add(Mul(f, cp), Mul(Up, sp));
                u = Norm(Sub(Mul(Up, cp), Mul(new V3(f.X, 0f, f.Z), sp)), Up);
            }
            else hidden = s < 0.04f || s > 0.96f;
        }

        // ------------------------------------------------------------------------------------------ Evaluate
        /// <summary>
        /// Call once per frame BEFORE Evaluate: advances the per-lobe treadmill phases by v * dt / Lt_j, with the path length Lt_j of the lobes of
        /// the previous Evaluate. (A cumulative phase times a ratio would jump when a lobe pulses, and cannot be folded.)
        /// </summary>
        public static void AdvanceLobeTread(HeapSlots S, float v, float dt)
        {
            for (int j = 0; j < C.LobeMax; j++)
            {
                float lt = S.Lobes[j].Lt;
                if (lt > 1e-4f) S.LobeTread[j] += v * dt / lt;
            }
        }

        /// <summary>V = min(SlotCount, round(n * k(n))): the heap shows more worms than it counts.</summary>
        public static int VisibleCount(int n, int slotCount)
        {
            float k = Lerp(C.DensityK0, C.DensityK1, Clamp01((float)(n - C.DensityN0) / (float)(C.DensityN1 - C.DensityN0)));
            int v = FloorI((float)n * k + 0.5f);
            return v < slotCount ? v : slotCount;
        }

        /// <summary>Evaluates the lobes and the first V slots into <see cref="HeapSlots.Lobes"/> and <see cref="HeapSlots.Out"/>; returns V. No allocation.</summary>
        public static int Evaluate(in MassFrameParams P, HeapSlots S)
        {
            return Evaluate(P, S, float.MaxValue, 0, 1, 1);
        }

        /// <summary>
        /// Select-then-evaluate (additive, the all-slots overload above is unchanged): only the places with prio &lt; <paramref name="pMax"/> are
        /// evaluated (the others are marked invisible and forget their cache), and a place already evaluated is kept for
        /// <paramref name="divBody"/> / <paramref name="divFlux"/> frames (flux and border share the second divisor): it is refreshed when
        /// (frame + i) % div == 0, so the refreshes are staggered. Divisors of 1 evaluate everything every call.
        /// </summary>
        public static int Evaluate(in MassFrameParams P, HeapSlots S, float pMax, int frame, int divBody, int divFlux)
        {
            int count = VisibleCount(P.N, S.Count);
            float rp = RPlace(P.N, P.R);
            Env G = Envelope(P, rp);
            ComputeLobes(P, S, rp);
            bool occl = P.HasCam;
            bool borderOk = P.N >= C.BorderMinN;
            bool eng = P.Engulf > 0f && P.TargetDist >= 0f;
            float tx = 0f, tz = 0f, es = 0f;
            if (eng)
            {
                tx = P.TargetDist * CosT(P.TargetTurns); tz = P.TargetDist * SinT(P.TargetTurns);
                es = Smooth01(P.Engulf);
            }
            bool arm = P.PseudoRamp > 0f && P.TargetDist >= 0f;
            float ln = 0f, dcx = 0f, dcz = 0f;
            if (arm)
            {
                dcx = CosT(P.TargetTurns); dcz = SinT(P.TargetTurns);
                float lim = P.TargetDist - C.ArmMargin - C.ArmStart * rp;
                ln = C.ArmMax < lim ? C.ArmMax : lim;
                ln = (ln > 0f ? ln : 0f) * P.PseudoRamp;
            }
            SlotOut[] outp = S.Out;
            int[] stamp = S.EvalFrame;
            // Places the engulfing or the arm pull are recomputed every frame whatever the divisor (only they: the rest stays cached).
            float engThr = eng ? (C.ColFrac + C.BaseFrac) * P.Engulf + C.RecruitSoft : -1f;
            float armThr = arm ? C.ArmFrac * P.PseudoRamp + C.RecruitSoft : -1f;
            for (int i = 0; i < count; i++)
            {
                byte role = S.Role[i];
                // A place pulled by the sheath or the arm is evaluated whatever its priority: the layout gives it priority 0 (always drawn).
                if (S.Prio[i] >= pMax && !(S.Thr[i] < engThr) && !(S.Thr2[i] < armThr))
                {
                    outp[i].Visible = false;
                    stamp[i] = HeapSlots.NotEvaluated;
                    continue;
                }
                int div = role == RoleBody ? divBody : divFlux;
                if (div > 1 && stamp[i] != HeapSlots.NotEvaluated && (frame + i) % div != 0 && frame - stamp[i] < div && frame >= stamp[i]
                    && !(S.Thr[i] < engThr) && !(S.Thr2[i] < armThr)
                    && !(P.Sigma > 0.5f && role != RoleBody && outp[i].Anim == AnimIdle)) continue;   // cached (poured worms excepted: they hold still in the world)
                stamp[i] = frame;
                if (role == RoleBorder && !borderOk) role = RoleFlux;
                int j = S.LobeOf[i];
                if (role == RoleBody) EvalBody(P, S, G, S.Lobes[j], j, i, occl, ref outp[i]);
                else if (role == RoleFlux) EvalFlux(P, S, S.Lobes[j], j, i, ref outp[i]);
                else EvalBorder(P, S, G, i, ref outp[i]);
                outp[i].Scale = WormScale(G, S, i, outp[i].Role);
                if (arm)
                {
                    float w = Smooth01((C.ArmFrac * P.PseudoRamp - S.Thr2[i]) / C.RecruitSoft);
                    if (w > 0f)
                    {
                        V3 ap, af, au; bool ah;
                        Arm(P, S, i, rp, ln, dcx, dcz, out ap, out af, out au, out ah);
                        outp[i].Pos = VLerp(outp[i].Pos, ap, w);
                        outp[i].Fwd = Norm(VLerp(outp[i].Fwd, af, w), af);
                        outp[i].Up = Norm(VLerp(outp[i].Up, au, w), au);
                        if (w > 0.5f)
                        {
                            outp[i].Visible = !ah;
                            outp[i].Prio = 0f;                       // arm worms are always drawn
                            outp[i].Lod0 = true;
                            outp[i].Anim = AnimCrawl;
                            outp[i].Frame = FloorI(P.OdoFlux + (float)i * C.CrawlPhaseStep) % CrawlFrames;
                        }
                    }
                }
                if (eng)
                {
                    if (role == RoleBody && j == C.EngulfLobe)
                    {
                        V3 nf, nu;
                        BaseOrient(S, i, outp[i].Pos, S.Lobes[C.EngulfLobe], out nf, out nu);
                        outp[i].Fwd = Norm(VLerp(outp[i].Fwd, nf, es), nf);
                        outp[i].Up = Norm(VLerp(outp[i].Up, nu, es), nu);
                    }
                    float wb = Smooth01(((C.ColFrac + C.BaseFrac) * P.Engulf - S.Thr[i]) / C.RecruitSoft);
                    if (wb > 0f)
                    {
                        SlotOut ob = default(SlotOut);
                        EvalBody(P, S, G, S.Lobes[C.EngulfLobe], C.EngulfLobe, i, false, ref ob);   // a place of the base heap at the target's feet
                        V3 bf, bu;
                        BaseOrient(S, i, ob.Pos, S.Lobes[C.EngulfLobe], out bf, out bu);
                        outp[i].Pos = VLerp(outp[i].Pos, ob.Pos, wb);
                        outp[i].Fwd = Norm(VLerp(outp[i].Fwd, bf, wb), bf);
                        outp[i].Up = Norm(VLerp(outp[i].Up, bu, wb), bu);
                        if (wb > 0.5f)
                        {
                            outp[i].Visible = true;
                            outp[i].Anim = ob.Anim; outp[i].Frame = ob.Frame;
                            outp[i].Lod0 = false;                    // base worms are bodies: LOD1 meshes
                        }
                    }
                    float wc = Smooth01((C.ColFrac * P.Engulf - S.Thr[i]) / C.RecruitSoft);
                    if (wc > 0f)
                    {
                        V3 sp, sf, su;
                        Spiral(P, S, i, tx, tz, es, out sp, out sf, out su);
                        outp[i].Pos = VLerp(outp[i].Pos, sp, wc);
                        outp[i].Fwd = Norm(VLerp(outp[i].Fwd, sf, wc), sf);
                        outp[i].Up = Norm(VLerp(outp[i].Up, su, wc), su);
                        if (wc > 0.5f)
                        {
                            outp[i].Visible = true;
                            outp[i].Prio = 0f;                       // sheath worms are always drawn
                            outp[i].Lod0 = true;
                            outp[i].Anim = AnimCrawl;
                            outp[i].Frame = FloorI(P.OdoFlux + (float)i * C.CrawlPhaseStep) % CrawlFrames;
                        }
                    }
                }
                if (P.StreamK > 0f && P.RibbonW > 0f)
                {
                    // In the stream no worm, whatever its role (border and arm included), lies beyond the half width of the ribbon.
                    float zlim = (0.5f * P.RibbonW > 0.28f ? 0.5f * P.RibbonW - 0.08f : 0.2f) + (1f - P.StreamK) * (P.R + 2f);   // 8 cm in: the heading follows the corridor to a few degrees
                    if (outp[i].Pos.Z > zlim) outp[i].Pos.Z = zlim;
                    else if (outp[i].Pos.Z < -zlim) outp[i].Pos.Z = -zlim;
                }
            }
            return count;
        }

        /// <summary>
        /// Evaluate + draw selection: p = clamp01(cap / max(1, V * vhat)); a visible slot with prio &lt; p is added, up to cap.
        /// visFrac &lt; 0 uses the exact visible fraction of this call (the game passes the EMA of the previous frames).
        /// Returns the number of worms added.
        /// </summary>
        public static int Emit<T>(ref T sink, in MassFrameParams P, HeapSlots S, int cap, float visFrac) where T : struct, IHeapSink
        {
            int v = Evaluate(P, S);
            SlotOut[] o = S.Out;
            float vhat = visFrac;
            if (vhat < 0f)
            {
                int nv = 0;
                for (int i = 0; i < v; i++) if (o[i].Visible) nv++;
                vhat = (float)nv / (float)(v > 1 ? v : 1);
            }
            float denom = (float)(v > 1 ? v : 1) * vhat;
            float p = Clamp01((float)cap / (vhat > 0f ? denom : 1f));
            int drawn = 0;
            for (int i = 0; i < v; i++)
            {
                if (!o[i].Visible || o[i].Prio >= p) continue;
                sink.AddWorm(o[i]);
                if (++drawn >= cap) break;
            }
            return drawn;
        }

        /// <summary>
        /// Evaluate only the places that can be drawn, then draw: <paramref name="p"/> is the priority threshold (cap / (V * visible fraction), 1 = all).
        /// Same result as <see cref="Emit{T}"/> for the same threshold, at the cost of the drawn places only. Returns the number added.
        /// </summary>
        public static int EmitSelected<T>(ref T sink, in MassFrameParams P, HeapSlots S, int cap, float p, int frame, int divBody, int divFlux,
            out int evaluated, out int visible) where T : struct, IHeapSink
        {
            int v = Evaluate(P, S, p, frame, divBody, divFlux);
            return Draw(ref sink, S, v, cap, p, out evaluated, out visible);
        }

        /// <summary>The draw loop alone: visible places with prio &lt; p, up to cap; also counts the places under the threshold and the visible ones among them.</summary>
        public static int Draw<T>(ref T sink, HeapSlots S, int v, int cap, float p, out int evaluated, out int visible) where T : struct, IHeapSink
        {
            SlotOut[] o = S.Out;
            int drawn = 0, ne = 0, nv = 0;
            for (int i = 0; i < v; i++)
            {
                if (o[i].Prio >= p) continue;
                ne++;
                if (!o[i].Visible) continue;
                nv++;
                if (drawn < cap) { sink.AddWorm(o[i]); drawn++; }
            }
            evaluated = ne; visible = nv;
            return drawn;
        }

        // ------------------------------------------------------------------------------------------ Compose (no TRS / LookRotation)
        /// <summary>Local instance matrix: columns (R*s, U*s, F*s, pos) with R = cross(U, F).</summary>
        public static void Compose(in SlotOut o, out Matrix4x4 m)
        {
            V3 r = Cross(o.Up, o.Fwd);
            float s = o.Scale;
            m = default(Matrix4x4);
            m.m00 = r.X * s; m.m10 = r.Y * s; m.m20 = r.Z * s;
            m.m01 = o.Up.X * s; m.m11 = o.Up.Y * s; m.m21 = o.Up.Z * s;
            m.m02 = o.Fwd.X * s; m.m12 = o.Fwd.Y * s; m.m22 = o.Fwd.Z * s;
            m.m03 = o.Pos.X; m.m13 = o.Pos.Y; m.m23 = o.Pos.Z; m.m33 = 1f;
        }

        /// <summary>
        /// World matrix = frame * local (36 multiplications, by hand). `frame` maps local to world: columns (forward, up, LEFT, root)
        /// of the heap (see <see cref="MakeFrame"/>).
        /// </summary>
        public static void ComposeWorld(in Matrix4x4 frame, in SlotOut o, out Matrix4x4 m)
        {
            Matrix4x4 l;
            Compose(o, out l);
            m = default(Matrix4x4);
            m.m00 = frame.m00 * l.m00 + frame.m01 * l.m10 + frame.m02 * l.m20;
            m.m10 = frame.m10 * l.m00 + frame.m11 * l.m10 + frame.m12 * l.m20;
            m.m20 = frame.m20 * l.m00 + frame.m21 * l.m10 + frame.m22 * l.m20;
            m.m01 = frame.m00 * l.m01 + frame.m01 * l.m11 + frame.m02 * l.m21;
            m.m11 = frame.m10 * l.m01 + frame.m11 * l.m11 + frame.m12 * l.m21;
            m.m21 = frame.m20 * l.m01 + frame.m21 * l.m11 + frame.m22 * l.m21;
            m.m02 = frame.m00 * l.m02 + frame.m01 * l.m12 + frame.m02 * l.m22;
            m.m12 = frame.m10 * l.m02 + frame.m11 * l.m12 + frame.m12 * l.m22;
            m.m22 = frame.m20 * l.m02 + frame.m21 * l.m12 + frame.m22 * l.m22;
            m.m03 = frame.m00 * l.m03 + frame.m01 * l.m13 + frame.m02 * l.m23 + frame.m03;
            m.m13 = frame.m10 * l.m03 + frame.m11 * l.m13 + frame.m12 * l.m23 + frame.m13;
            m.m23 = frame.m20 * l.m03 + frame.m21 * l.m13 + frame.m22 * l.m23 + frame.m23;
            m.m33 = 1f;
        }

        /// <summary>Share of r(n) at which a release or arrival point lies from the centre, at most (inside the footprint).</summary>
        public const float ReleaseReach = 0.9f;
        /// <summary>Height of a release point on the heap, as a share of r(n) (the visual height of the heap is about half its radius).</summary>
        public const float ReleaseHeight = 0.5f;

        /// <summary>
        /// Pure points on a heap where worms leave it (dissolution) or reach it (formation, transfer): the first <paramref name="count"/> places of
        /// the heap seed, projected on the footprint of radius <paramref name="r"/> (place k lies at the same direction and depth as the worm of
        /// the layout that holds it). <paramref name="forward"/> and <paramref name="left"/> are the horizontal axes of the heap frame, +X and +Z
        /// of the local frame. Fills <paramref name="outPoints"/>[0..count) with world positions; heights are above <paramref name="root"/>. No allocation.
        /// </summary>
        public static void ReleasePoints(uint seed, int count, float r, Vector3 root, Vector3 forward, Vector3 left, Vector3[] outPoints)
        {
            for (int k = 0; k < count; k++)
            {
                uint a = (uint)(k + 1);
                float u = Hash01(seed, a, 2);
                float rho = (float)Math.Sqrt(1f - u * u);
                float ph = Hash01(seed, a, 3);
                float dx = rho * CosT(ph), dz = rho * SinT(ph);
                float hh = Hash01(seed, a, 30);
                float depth = (1f - C.RhoSpan * hh * hh) * ReleaseReach;
                outPoints[k] = root + forward * (dx * depth * r) + left * (dz * depth * r) + Vector3.up * (u * ReleaseHeight * r);
            }
        }

        /// <summary>Frame of a heap in the world: local +X to forward, +Y to up, +Z to LEFT (= -right = cross(forward, up)), origin at root.</summary>
        public static Matrix4x4 MakeFrame(Vector3 root, Vector3 forward, Vector3 up)
        {
            Vector3 left = Vector3.Cross(forward, up);
            Matrix4x4 m = default(Matrix4x4);
            m.m00 = forward.x; m.m10 = forward.y; m.m20 = forward.z;
            m.m01 = up.x; m.m11 = up.y; m.m21 = up.z;
            m.m02 = left.x; m.m12 = left.y; m.m22 = left.z;
            m.m03 = root.x; m.m13 = root.y; m.m23 = root.z; m.m33 = 1f;
            return m;
        }
    }
}
