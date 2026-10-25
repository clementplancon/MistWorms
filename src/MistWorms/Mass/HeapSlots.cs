using System;

namespace MistWorms.Mass
{
    /// <summary>One lobe of the organic envelope for the current frame (ellipsoid: lateral Rz, forward Rx, height Ry, turned by Psi).</summary>
    internal struct Lobe
    {
        public float Cx, Cz, Rx, Ry, Rz, Psi, C, S, Lt, Ax, Rl;
    }

    /// <summary>
    /// Per-slot and per-lobe constants of a heap (structure of arrays), drawn once from the heap seed (port of `Slots` in
    /// assets/blender/mass_layout.py). Allocated once per heap: <see cref="MassLayoutConst.SlotCount"/> places, the lobes and the output
    /// buffers that <see cref="MassLayout.Evaluate"/> fills every frame. Nothing here is reallocated after the constructor.
    /// </summary>
    internal sealed class HeapSlots
    {
        public readonly uint Seed;
        public readonly int Count;

        public readonly byte[] Role, LobeOf, RateClass, DiveClass, BorderClass, BoilK;
        public readonly bool[] Straggler;
        public readonly int[] BorderRank;
        public readonly float[] Prio, Dx, Dy, Dz, Rho0, DivePh, Psi0, Roll, Pitch, PhiB, Reach, Lam0, AnimPhase, Lane, Lane2, ScaleJit,
            BoilPhi, RollFlux, Thr, Hv, Hr, Hph, ESlide, ESlidePh, EOut, EPit, Thr2, As0, Ahz, Ahy;

        // lobes
        public readonly float[] LBeta, LAspS, LAspI, LHf, LPsi0, LPsiPh, LCatPh, LRPh, LHPh;
        public readonly byte[] LRPer, LHPer;
        public readonly float[] LPh;     // [lobe * 4 + m]
        public readonly byte[] LPer;     // [lobe * 4 + m]

        /// <summary>INPUT kept by the caller: per-lobe treadmill phase, the integral of v / Lt_j (see <see cref="MassLayout.AdvanceLobeTread"/>).</summary>
        public readonly float[] LobeTread;

        /// <summary>Lobes of the last Evaluate (LobeMax entries).</summary>
        public readonly Lobe[] Lobes;
        /// <summary>Output of the last Evaluate (first V entries valid).</summary>
        public readonly SlotOut[] Out;

        /// <summary>Marks a place with no valid cached evaluation in <see cref="EvalFrame"/>.</summary>
        public const int NotEvaluated = int.MinValue;
        /// <summary>Frame of the last evaluation of each place (cache bookkeeping of the select-then-evaluate overload).</summary>
        public readonly int[] EvalFrame;

        /// <summary>Boil line cache: cycle (mod 256) of the cached hashes of each place (-1 = none) and the four hashes.</summary>
        public readonly int[] BoilCm;
        public readonly float[] BoilH;

        public HeapSlots(uint seed) : this(seed, MassLayoutConst.SlotCount) { }

        public HeapSlots(uint seed, int count)
        {
            Seed = seed;
            Count = count;
            Role = new byte[count]; LobeOf = new byte[count]; RateClass = new byte[count]; DiveClass = new byte[count]; BorderClass = new byte[count]; BoilK = new byte[count];
            Straggler = new bool[count];
            BorderRank = new int[count];
            Prio = new float[count]; Dx = new float[count]; Dy = new float[count]; Dz = new float[count]; Rho0 = new float[count];
            DivePh = new float[count]; Psi0 = new float[count]; Roll = new float[count]; Pitch = new float[count]; PhiB = new float[count];
            Reach = new float[count]; Lam0 = new float[count]; AnimPhase = new float[count]; Lane = new float[count]; Lane2 = new float[count];
            ScaleJit = new float[count]; BoilPhi = new float[count]; RollFlux = new float[count];
            Thr = new float[count]; Hv = new float[count]; Hr = new float[count]; Hph = new float[count];
            ESlide = new float[count]; ESlidePh = new float[count]; EOut = new float[count]; EPit = new float[count];
            Thr2 = new float[count]; As0 = new float[count]; Ahz = new float[count]; Ahy = new float[count];
            int lm = MassLayoutConst.LobeMax;
            LBeta = new float[lm]; LAspS = new float[lm]; LAspI = new float[lm]; LHf = new float[lm]; LPsi0 = new float[lm]; LPsiPh = new float[lm];
            LCatPh = new float[lm]; LRPh = new float[lm]; LHPh = new float[lm]; LRPer = new byte[lm]; LHPer = new byte[lm];
            LPh = new float[lm * 4]; LPer = new byte[lm * 4];
            Lobes = new Lobe[lm];
            LobeTread = new float[lm];
            Out = new SlotOut[count];
            EvalFrame = new int[count];
            BoilCm = new int[count];
            BoilH = new float[count * 4];
            for (int i = 0; i < count; i++) BoilCm[i] = -1;
            for (int i = 0; i < count; i++) EvalFrame[i] = NotEvaluated;

            float h0 = MassLayout.Hash01(seed, 0, 0);
            float h1 = MassLayout.Hash01(seed, 0, 1);
            int borderRank = 0;
            for (int i = 0; i < count; i++)
            {
                float q = MassLayout.Frac((float)i * MassLayoutConst.RoleAlpha + h0);
                byte role = q < MassLayoutConst.BodyFraction ? MassLayout.RoleBody : (q < MassLayoutConst.FluxFraction ? MassLayout.RoleFlux : MassLayout.RoleBorder);
                Role[i] = role;
                Prio[i] = MassLayout.Frac((float)i * MassLayoutConst.PrioAlpha + h1);
                BorderRank[i] = borderRank;
                if (role == MassLayout.RoleBorder) borderRank++;

                uint a = (uint)(i + 1);
                float u = MassLayout.Hash01(seed, a, 2);
                float rho = (float)Math.Sqrt(1f - u * u);
                float ph = MassLayout.Hash01(seed, a, 3);
                Dx[i] = rho * MassLayout.CosT(ph);
                Dy[i] = u;
                Dz[i] = rho * MassLayout.SinT(ph);
                float hh = MassLayout.Hash01(seed, a, 30);
                Rho0[i] = 1f - MassLayoutConst.RhoSpan * hh * hh;
                RateClass[i] = (byte)(int)Math.Floor(MassLayout.Hash01(seed, a, 4) * 4f);
                DiveClass[i] = (byte)(int)Math.Floor(MassLayout.Hash01(seed, a, 5) * 4f);
                DivePh[i] = MassLayout.Hash01(seed, a, 8);
                Psi0[i] = MassLayout.Hash01(seed, a, 9);
                Roll[i] = (MassLayout.Hash01(seed, a, 10) * 2f - 1f) * MassLayout.BodyRollMaxT;
                Pitch[i] = (MassLayout.Hash01(seed, a, 11) * 2f - 1f) * MassLayout.BodyPitchMaxT;
                PhiB[i] = MassLayout.Hash01(seed, a, 12);
                Reach[i] = MassLayout.Lerp(MassLayoutConst.BorderReachMin, MassLayoutConst.BorderReachMax, MassLayout.Hash01(seed, a, 13));
                Lam0[i] = MassLayout.Hash01(seed, a, 14);
                BoilK[i] = (byte)Math.Min((int)Math.Floor(MassLayout.Hash01(seed, a, 15) * 5f), 4);
                AnimPhase[i] = MassLayout.Hash01(seed, a, 16);
                Lane[i] = (MassLayout.Hash01(seed, a, 17) * 2f - 1f) * MassLayout.TreadLaneFanT;
                Lane2[i] = Lane[i] * MassLayoutConst.LaneConverge;
                ScaleJit[i] = MassLayout.Hash01(seed, a, 19) * 2f - 1f;
                BorderClass[i] = (byte)Math.Min((int)Math.Floor(MassLayout.Hash01(seed, a, 20) * 2f), 1);
                Straggler[i] = MassLayout.Hash01(seed, a, 21) < MassLayoutConst.StragglerFrac;
                BoilPhi[i] = MassLayout.Hash01(seed, a, 22);
                RollFlux[i] = (MassLayout.Hash01(seed, a, 23) * 2f - 1f) * MassLayout.FluxRollMaxT;
                LobeOf[i] = (byte)Math.Min((int)Math.Floor(MassLayout.Hash01(seed, a, 25) * (float)lm), lm - 1);
                Thr[i] = MassLayout.Hash01(seed, a, 26);
                Hv[i] = MassLayout.Hash01(seed, a, 27);
                Hr[i] = MassLayout.Hash01(seed, a, 28);
                Hph[i] = MassLayout.Hash01(seed, a, 29);
                ESlide[i] = MassLayout.Hash01(seed, a, 31);
                ESlidePh[i] = MassLayout.Hash01(seed, a, 32);
                EOut[i] = MassLayout.Hash01(seed, a, 33);
                EPit[i] = MassLayout.Hash01(seed, a, 34);
                Thr2[i] = MassLayout.Hash01(seed, a, 36);
                As0[i] = MassLayout.Hash01(seed, a, 37);
                Ahz[i] = MassLayout.Hash01(seed, a, 38);
                Ahy[i] = MassLayout.Hash01(seed, a, 39);
            }
            for (int j = 0; j < lm; j++)
            {
                uint a = (uint)(2000 + j);
                LBeta[j] = MassLayout.Frac(MassLayout.Hash01(seed, a, 0) + (float)j * MassLayout.GoldenTurn);
                float asp = MassLayout.Lerp(MassLayoutConst.LobeAspectMin, MassLayoutConst.LobeAspectMax, MassLayout.Hash01(seed, a, 1));
                LAspS[j] = (float)Math.Sqrt(asp);
                LAspI[j] = 1f / (float)Math.Sqrt(asp);
                LHf[j] = MassLayout.Lerp(MassLayoutConst.LobeHeightMin, MassLayoutConst.LobeHeightMax, MassLayout.Hash01(seed, a, 2));
                LPsi0[j] = MassLayout.Hash01(seed, a, 3);
                LPsiPh[j] = MassLayout.Hash01(seed, a, 4);
                LCatPh[j] = MassLayout.Hash01(seed, a, 5);
                LRPh[j] = MassLayout.Hash01(seed, a, 6);
                LHPh[j] = MassLayout.Hash01(seed, a, 7);
                LRPer[j] = (byte)Math.Min((int)Math.Floor(MassLayout.Hash01(seed, a, 8) * 5f), 4);
                LHPer[j] = (byte)Math.Min((int)Math.Floor(MassLayout.Hash01(seed, a, 9) * 5f), 4);
                for (int m = 0; m < 4; m++)
                {
                    LPh[j * 4 + m] = MassLayout.Hash01(seed, a, (uint)(10 + m));
                    LPer[j * 4 + m] = (byte)Math.Min((int)Math.Floor(MassLayout.Hash01(seed, a, (uint)(14 + m)) * 5f), 4);
                }
            }
        }
    }
}
