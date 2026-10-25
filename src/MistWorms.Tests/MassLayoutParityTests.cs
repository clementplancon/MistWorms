using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MistWorms.Assets;
using MistWorms.Mass;
using Xunit;
using Xunit.Abstractions;

namespace MistWorms.Tests
{
    /// <summary>
    /// Parity of MassLayout.cs with the Python reference (assets/blender/mass_layout.py) through assets/build/mass_layout_golden.json:
    /// seed 12345, n in {10, 50, 200}, t in {0, 1.7}, sigma in {0, 1}, the first 40 slots (role, anim, frame, visible, pos, fwd, up), 1e-3 m.
    /// The tests return silently when the golden file is absent (a checkout without a built assets/build).
    /// </summary>
    public class MassLayoutParityTests
    {
        private const double Tolerance = 1e-3;
        private readonly ITestOutputHelper _out;

        public MassLayoutParityTests(ITestOutputHelper output) { _out = output; }

        private static object LoadGolden()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 8 && dir != null; i++, dir = Path.GetDirectoryName(dir))
            {
                string p = Path.Combine(dir, "assets", "build", "mass_layout_golden.json");
                if (File.Exists(p)) return JsonReader.Parse(File.ReadAllText(p));
            }
            return null;
        }

        private static double D(object o) { return (double)o; }

        [Fact]
        public void ConstantsMatchTheMassLayoutCopyInTheGoldenFile()
        {
            object g = LoadGolden();
            if (g == null) return;
            var ml = JsonReader.Obj(g, "massLayout");
            Assert.NotNull(ml);
            var fields = typeof(MassLayoutConst).GetFields(BindingFlags.Public | BindingFlags.Static);
            Assert.Equal(ml.Count, fields.Length);
            foreach (var f in fields)
            {
                object v;
                Assert.True(ml.TryGetValue(f.Name, out v), "missing in golden: " + f.Name);
                object c = f.GetValue(null);
                if (v is List<object>)
                {
                    var list = (List<object>)v;
                    var arr = (Array)c;
                    Assert.Equal(list.Count, arr.Length);
                    for (int i = 0; i < list.Count; i++)
                        Assert.Equal(D(list[i]), Convert.ToDouble(arr.GetValue(i)), 6);
                }
                else if (v is bool) Assert.Equal((bool)v, (bool)c);
                else Assert.Equal(D(v), Convert.ToDouble(c), 6);
            }
        }

        [Fact]
        public void HashAndTablesMatch()
        {
            object g = LoadGolden();
            if (g == null) return;
            foreach (object o in JsonReader.Arr(g, "lowbias32"))
                Assert.Equal((uint)JsonReader.Num(o, "h", -1), MassLayout.LowBias32((uint)JsonReader.Num(o, "x", 0)));
            foreach (object o in JsonReader.Arr(g, "hashVectors"))
            {
                uint s = (uint)JsonReader.Num(o, "seed", 0), a = (uint)JsonReader.Num(o, "a", 0), b = (uint)JsonReader.Num(o, "b", 0);
                Assert.Equal((uint)JsonReader.Num(o, "h", -1), MassLayout.HashU32(s, a, b));
                Assert.Equal(JsonReader.Num(o, "h01", -1), MassLayout.Hash01(s, a, b), 7);
            }
            Assert.Equal(JsonReader.Num(g, "sinLutSum", 0), MassLayout.SinLutSum(), 4);
        }

        [Fact]
        public void SlotsDrawnFromTheSeedMatch()
        {
            object g = LoadGolden();
            if (g == null) return;
            var s = new HeapSlots((uint)JsonReader.Num(g, "seed", 0));
            var smp = JsonReader.Obj(g, "slotSample");
            var role = JsonReader.Arr(smp, "role");
            var prio = JsonReader.Arr(smp, "prio");
            var lobe = JsonReader.Arr(smp, "lobe");
            var rho0 = JsonReader.Arr(smp, "rho0");
            var x0 = JsonReader.Arr(smp, "dx");
            var z0 = JsonReader.Arr(smp, "dz");
            for (int i = 0; i < role.Count; i++)
            {
                Assert.Equal((int)D(role[i]), (int)s.Role[i]);
                Assert.InRange(s.Prio[i], D(prio[i]) - 2e-6, D(prio[i]) + 2e-6);     // golden values are rounded to 6 decimals
                Assert.Equal((int)D(lobe[i]), (int)s.LobeOf[i]);
                Assert.InRange(s.Rho0[i], D(rho0[i]) - 2e-6, D(rho0[i]) + 2e-6);
                Assert.InRange(s.Dx[i], D(x0[i]) - 2e-6, D(x0[i]) + 2e-6);
                Assert.InRange(s.Dz[i], D(z0[i]) - 2e-6, D(z0[i]) + 2e-6);
            }
        }

        private static MassFrameParams ParamsOf(object p)
        {
            var q = MassFrameParams.Default((float)JsonReader.Num(p, "r", 0), (int)JsonReader.Num(p, "n", 0));
            q.Sigma = (float)JsonReader.Num(p, "sigma", 0);
            q.T = (float)JsonReader.Num(p, "t", 0);
            q.Tread = (float)JsonReader.Num(p, "tread", 0);
            q.Boil = (float)JsonReader.Num(p, "boil", 0);
            q.OdoFlux = (float)JsonReader.Num(p, "odoFlux", 0);
            q.OdoBorder = (float)JsonReader.Num(p, "odoBorder", 0);
            q.TargetTurns = (float)JsonReader.Num(p, "targetTurns", 0);
            q.TargetDist = (float)JsonReader.Num(p, "targetDist", -1);
            q.PseudoRamp = (float)JsonReader.Num(p, "pseudoRamp", 0);
            q.Engulf = (float)JsonReader.Num(p, "engulf", 0);
            var bl = JsonReader.Arr(p, "blocked");
            if (bl != null) { q.Blocked0 = (bool)bl[0]; q.Blocked1 = (bool)bl[1]; q.Blocked2 = (bool)bl[2]; q.Blocked3 = (bool)bl[3]; }
            var cam = JsonReader.Arr(p, "camL");
            if (cam != null) { q.HasCam = true; q.CamL = new V3((float)D(cam[0]), (float)D(cam[1]), (float)D(cam[2])); }
            return q;
        }

        private static double Dist(List<object> want, V3 got)
        {
            double dx = D(want[0]) - got.X, dy = D(want[1]) - got.Y, dz = D(want[2]) - got.Z;
            return Math.Max(Math.Abs(dx), Math.Max(Math.Abs(dy), Math.Abs(dz)));
        }

        [Fact]
        public void EvaluateMatchesTheGoldenCases()
        {
            object g = LoadGolden();
            if (g == null) return;
            var slots = new HeapSlots((uint)JsonReader.Num(g, "seed", 0));
            double max = 0;
            int compared = 0;
            foreach (object c in JsonReader.Arr(g, "cases"))
            {
                var P = ParamsOf(c is Dictionary<string, object> ? ((Dictionary<string, object>)c)["params"] : null);
                string tag = "n=" + P.N + " t=" + P.T + " sigma=" + P.Sigma;
                var lt = JsonReader.Arr(((Dictionary<string, object>)c)["params"], "lobeTread");
                for (int j = 0; j < lt.Count; j++) slots.LobeTread[j] = (float)D(lt[j]);
                int count = MassLayout.Evaluate(P, slots);
                Assert.True(count >= 40, tag);
                var want = JsonReader.Arr(c, "slots");
                for (int k = 0; k < want.Count; k++)
                {
                    object w = want[k];
                    SlotOut o = slots.Out[k];
                    string id = tag + " slot " + k;
                    Assert.Equal((int)JsonReader.Num(w, "i", -1), o.I);
                    Assert.Equal((int)JsonReader.Num(w, "role", -1), (int)o.Role);
                    Assert.Equal((int)JsonReader.Num(w, "anim", -1), (int)o.Anim);
                    Assert.Equal((int)JsonReader.Num(w, "lobe", -2), o.Lobe);
                    Assert.Equal(JsonReader.Num(w, "scale", 0), o.Scale, 4);
                    Assert.Equal((int)JsonReader.Num(w, "frame", -1), o.Frame);
                    Assert.Equal(JsonReader.Bool(w, "visible", false), o.Visible);
                    Assert.Equal(JsonReader.Bool(w, "lod0", false), o.Lod0);
                    double e = Math.Max(Dist(JsonReader.Arr(w, "pos"), o.Pos), Math.Max(Dist(JsonReader.Arr(w, "fwd"), o.Fwd), Dist(JsonReader.Arr(w, "up"), o.Up)));
                    Assert.True(e <= Tolerance, id + " error " + e);
                    if (e > max) max = e;
                    compared++;
                }
                Assert.Equal((int)JsonReader.Num(c, "visibleCount", -1), count);
                Assert.Equal(JsonReader.Num(c, "rPlace", 0), MassLayout.RPlace(P.N, P.R), 4);
                var lw = JsonReader.Arr(c, "lobes");
                for (int j = 0; j < lw.Count; j++)
                {
                    var lv = (List<object>)lw[j];
                    Lobe L = slots.Lobes[j];
                    double[] got = { L.Cx, L.Cz, L.Rx, L.Ry, L.Rz, L.Psi };
                    for (int m = 0; m < 6; m++)
                    {
                        // psi is in turns and wraps: compare modulo 1
                        double d = Math.Abs(D(lv[m]) - got[m]);
                        if (m == 5) d = Math.Min(d, Math.Abs(d - 1.0));
                        Assert.True(d <= 1e-3, tag + " lobe " + j + " field " + m + " error " + d);
                        if (m < 5 && d > max) max = d;
                    }
                }
            }
            _out.WriteLine("golden parity: " + compared + " slots, max component error " + max.ToString("0.0e+0") + " m (tolerance 1e-3)");
        }
    }
}
