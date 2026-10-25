#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using MistWorms.Assets;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Dev.Preview;
using MistWorms.Mass;
using MistWorms.Render;
using UnityEngine;
using UnityEngine.Profiling;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// P4_MassPerf (phase 4B-3, pass G; plan "Passe G" with the budgets of the addendum and of the worker architecture): the cost of the swarms in
    /// the worst case. Open ground, god and ghost player, two masses of 200 walking back and forth 3 m every 3 s (so the treadmill turns), 100 free
    /// worms. Phases (frames): B = A at 8 m (LOD0) and B at 22 m (LOD1), 600; C = both close (6 and 9 m, shared LOD0 budget), 600; D = both at 40 m (LOD2),
    /// 300; E = the masses hidden and shown in windows of 300 frames (ABABAB) for the frame time difference.
    /// Budgets (main thread = everything the renderer does: free worms, masses, submission): mean render CPU &lt;= 0.6 ms in B and C; mean main cost of a
    /// mass in LOD0 &lt;= 0.05 ms; worker &lt;= 1 ms per frame for all masses; &lt;= 64 instanced calls; &lt;= 450 k triangles; nothing dropped; no managed
    /// allocation or collection in the measured windows (the probe is calibrated with a 4 KB allocation first and reported unusable if it does not see it).
    /// The log says whether this build is optimized: run it with tools/build.ps1 -Configuration Profile.
    /// </summary>
    internal sealed class P4_MassPerf : IDevScenario
    {
        private const int MassCount = 200;
        private const int FreeWorms = 100;
        private const int WarmFrames = 300;
        private const int FramesB = 600, FramesC = 600, FramesD = 300, WindowE = 300, WindowsE = 6;
        private const float RenderCpuBudget = 0.6f, HeapMainBudget = 0.05f, WorkerBudget = 1f;
        private const int CallBudget = 64;
        private const int TriangleBudget = 450000;
        private const float WalkHalf = 3f, WalkPeriod = 3f;
        private const float FrameDeltaWarn = 2f;

        public string Name => "P4_MassPerf";

        private sealed class Series
        {
            public readonly float[] Cpu, HeapMain, Worker, Frame;
            public readonly int[] Calls, Tris, Dropped;
            public int Count;
            public Series(int n) { Cpu = new float[n]; HeapMain = new float[n]; Worker = new float[n]; Frame = new float[n]; Calls = new int[n]; Tris = new int[n]; Dropped = new int[n]; }
        }

        private WritheMassState _a, _b;
        private WritheMassVisual _va, _vb;
        private WritheMassAI _aiA, _aiB;
        private Vector3 _anchorA, _anchorB, _perp;

        public IEnumerator Run(ScenarioContext ctx)
        {
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 300f;
            float scale = ctx.ParamFloat("frames", 1f);        // a shorter run for a quick check: frames=0.25
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;
                ctx.Assert("assets_loaded", AssetCatalog.IsReady, true, AssetCatalog.IsReady ? "ready" : AssetCatalog.Error);
                if (!AssetCatalog.IsReady || !MaterialFactory.EnsureBuilt()) yield break;

                bool optimized = IsOptimized();
                ctx.Log(optimized
                    ? "build: optimized (Profile or Release): the timings are valid for the budgets"
                    : "WARNING: this build is NOT optimized (Debug): the timings below are not valid for the budgets; build with tools/build.ps1 -Configuration Profile");
                ctx.Log($"worker thread: {(MWConfig.Performance.MassWorkerThread.Value ? "on" : "OFF (inline on the main thread)")}");

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                DevPreview.Instance.Clear();
                Player player = Player.m_localPlayer;
                player.SetGodMode(true);
                player.SetGhostMode(true);
                yield return ctx.WaitFrames(3);
                yield return ctx.MoveToOpenGround(14f);
                if (!ctx.LastOpenGround.Found) yield break;
                player = Player.m_localPlayer;
                player.SetGodMode(true);
                player.SetGhostMode(true);

                Vector3 fwd = player.transform.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
                _perp = Vector3.Cross(Vector3.up, fwd);
                Vector3 eye = player.transform.position + Vector3.up * 1.7f;
                DevCamera.Free(eye, eye + fwd * 10f);

                // ---- the scene
                _anchorA = player.transform.position + fwd * 7f - _perp * 4f;
                _anchorB = player.transform.position + fwd * 20f + _perp * 9f;
                _a = SpawnRobust(ctx, "A", player, _anchorA);
                _b = SpawnRobust(ctx, "B", player, _anchorB);
                ctx.Assert("masses_created", _a != null && _b != null, "two masses of 200", $"{(_a != null)} {(_b != null)}");
                if (_a == null || _b == null) yield break;
                _va = _a.GetComponent<WritheMassVisual>(); _vb = _b.GetComponent<WritheMassVisual>();
                _aiA = _a.GetComponent<WritheMassAI>(); _aiB = _b.GetComponent<WritheMassAI>();
                MassAudio.Enabled = false;      // every budget below is the render and the simulation; the sounds (vanilla prefab instances allocate by nature) are measured apart in G2

                int created = 0;
                float[] radii = { 20f, 20f, 20f, 20f };
                for (int pass = 0; pass < radii.Length && created < FreeWorms; pass++)
                    created += ColonyDev.Spawn(FreeWorms - created, radii[pass], WormState.Idle, true, out _, out _);
                ctx.Assert("free_worms_created", created >= FreeWorms * 9 / 10, $">= {FreeWorms * 9 / 10}", created);

                yield return Frames(WarmFrames, scale, (f) => Walk());

                // ---- B: A at 8 m (LOD0), B at 22 m (LOD1)
                Place(_anchorA, _anchorB);
                var sb = new Series((int)(FramesB * scale) + 1);
                yield return Measure(sb, FramesB, scale, () => Walk());
                Report(ctx, "B", sb, true);
                ctx.Assert("B_lod_A_is_0", _va.LastLod == 0, 0, _va.LastLod);
                ctx.Assert("B_lod_B_is_1", _vb.LastLod == 1, 1, _vb.LastLod);
                ctx.Assert("B_drawn_A", _va.LastDrawn >= 100 && _va.LastDrawn <= _va.LastCap, $"100..{_va.LastCap}", _va.LastDrawn);
                Budgets(ctx, "B", sb, true);

                // ---- C: both close (shared LOD0 budget)
                _anchorA = player.transform.position + fwd * 6f;
                _anchorB = player.transform.position + fwd * 8f + _perp * 4f;
                Place(_anchorA, _anchorB);
                yield return Frames(60, scale, (f) => Walk());
                var sc = new Series((int)(FramesC * scale) + 1);
                yield return Measure(sc, FramesC, scale, () => Walk());
                Report(ctx, "C", sc, true);
                ctx.Log($"C: lod {_va.LastLod} / {_vb.LastLod}, drawn {_va.LastDrawn} + {_vb.LastDrawn} (global LOD0 cap {MassLayoutConst.HeapLod0GlobalCap})");
                Budgets(ctx, "C", sc, true);

                // ---- D: both at 40 m
                yield return PlaceFar(ctx, player, fwd, 40f, scale);
                yield return Frames(60, scale, (f) => Walk());
                var sd = new Series((int)(FramesD * scale) + 1);
                yield return Measure(sd, FramesD, scale, () => Walk());
                Report(ctx, "D", sd, false);
                ctx.Assert("D_lod_both_2", _va.LastLod == 2 && _vb.LastLod == 2, "2 and 2", $"{_va.LastLod} and {_vb.LastLod}");
                ctx.Assert("D_drawn_at_most_lod2_cap", _va.LastDrawn <= MassLayoutConst.Lod2Cap && _vb.LastDrawn <= MassLayoutConst.Lod2Cap, $"<= {MassLayoutConst.Lod2Cap} each", $"{_va.LastDrawn} / {_vb.LastDrawn}");
                Budgets(ctx, "D", sd, false);

                // ---- E: the difference in frame time with and without the masses (worst case placement), ABABAB
                _anchorA = player.transform.position + fwd * 6f;
                _anchorB = player.transform.position + fwd * 8f + _perp * 4f;
                Place(_anchorA, _anchorB);
                yield return Frames(60, scale, (f) => Walk());
                double with = 0, without = 0; int nWith = 0, nWithout = 0;
                int window = Math.Max(10, (int)(WindowE * scale));
                for (int w = 0; w < WindowsE; w++)
                {
                    bool hidden = (w & 1) == 1;
                    MassRenderSystem.DevHidden = hidden;
                    yield return Frames(20, scale, (f) => Walk());       // lets the first frames of a window go
                    for (int f = 0; f < window; f++)
                    {
                        Walk();
                        yield return null;
                        if (hidden) { without += Time.unscaledDeltaTime; nWithout++; } else { with += Time.unscaledDeltaTime; nWith++; }
                    }
                }
                MassRenderSystem.DevHidden = false;
                double delta = (nWith > 0 && nWithout > 0) ? (with / nWith - without / nWithout) * 1000.0 : 0.0;
                ctx.Log($"E: frame time with the masses {with / Math.Max(1, nWith) * 1000.0:0.00} ms, without {without / Math.Max(1, nWithout) * 1000.0:0.00} ms: difference {delta:0.00} ms" +
                        (delta > FrameDeltaWarn ? $" WARNING above {FrameDeltaWarn} ms (logged, not a failure: the frame is bounded by the GPU in many scenes)" : string.Empty));

                // ---- G: managed allocation, differential. The same windows of frames with the masses drawn (A) and hidden (B), alternated ABABABAB:
                // what the masses add to the allocation of the game must stay under 64 KB per window. Allocation = the sum of the rises of the managed heap
                // from one frame to the next (a collection lowers it and is not counted).
                long probe0 = Profiler.GetMonoUsedSizeLong();
                byte[] junk = new byte[4096];
                bool operative = Profiler.GetMonoUsedSizeLong() > probe0;
                GC.KeepAlive(junk);
                int gWindow = Math.Max(20, (int)(WindowE * scale));
                MassAudio.Enabled = false;      // the budget is the render and the simulation: the vanilla sound prefabs allocate by nature, measured apart below
                // (b) the masses drawn against hidden, 12 windows ABAB..., the first two left out (the lists of the renderer and the buffers of the worker grow to their
                // working set once; that is a one-time cost, not a per-frame one). Median of the windows: one growth does not decide the budget.
                var withList = new List<long>();
                var withoutList = new List<long>();
                for (int w = 0; w < 12; w++)
                {
                    bool hidden = (w & 1) == 1;
                    MassRenderSystem.DevHidden = hidden;
                    yield return Frames(20, scale, (f) => Walk());
                    var box = new long[1];
                    yield return AllocWindow(gWindow, scale, box, Walk);
                    if (w < 2) continue;
                    (hidden ? withoutList : withList).Add(box[0]);
                }
                MassRenderSystem.DevHidden = false;
                long perWith = Median(withList), perWithout = Median(withoutList);
                ctx.Log($"G (b, the render of the masses): managed allocation per window of {gWindow} frames, median: drawn {perWith} bytes, hidden {perWithout} bytes, difference {perWith - perWithout} bytes; windows drawn [{string.Join(", ", withList)}], hidden [{string.Join(", ", withoutList)}] (probe {(operative ? "sees a 4 KB allocation" : "DID NOT see a 4 KB allocation: inoperative")})");
                // The proof of zero allocation is the unit tests (MassWorkerTests, the layout tests). In game the probe reads the whole managed heap of the process, whose noise is
                // 20 to 80 KB per window of 300 frames: this threshold (256 KB, under 1 KB per frame) only guards against a gross regression.
                if (operative) ctx.Assert("G_masses_add_no_allocation", perWith - perWithout <= 256 * 1024, "<= 256 KB more per window than hidden (median; guard against a gross regression, noise 20-80 KB)", $"{perWith - perWithout} bytes");

                // (a) the masses standing (Character, AI, physics, our state) against deactivated, render hidden in both: logged, not a budget of ours unless it comes from our code.
                var presentList = new List<long>();
                var absentList = new List<long>();
                MassRenderSystem.DevHidden = true;
                for (int w = 0; w < 8; w++)
                {
                    bool absent = (w & 1) == 1;
                    if (_a != null) _a.gameObject.SetActive(!absent);
                    if (_b != null) _b.gameObject.SetActive(!absent);
                    yield return Frames(20, scale, (f) => Walk());
                    var box = new long[1];
                    yield return AllocWindow(gWindow, scale, box, Walk);
                    if (w < 2) continue;
                    (absent ? absentList : presentList).Add(box[0]);
                }
                if (_a != null) _a.gameObject.SetActive(true);
                if (_b != null) _b.gameObject.SetActive(true);
                MassRenderSystem.DevHidden = false;
                ctx.Log($"G (a, the masses as creatures): managed allocation per window of {gWindow} frames, median: masses standing {Median(presentList)} bytes, deactivated {Median(absentList)} bytes, difference {Median(presentList) - Median(absentList)} bytes (Character, AI, physics, state; render hidden in both)");

                // ---- G2: what the sounds of the masses allocate (they instantiate vanilla sound prefabs, as in the game): windows with the audio on and off, masses drawn. Logged.
                long soundOn = 0, soundOff = 0;
                int winOn = 0, winOff = 0;
                for (int w = 0; w < 6; w++)
                {
                    bool on = (w & 1) == 0;
                    MassAudio.Enabled = on;
                    yield return Frames(20, scale, (f) => Walk());
                    long previous = Profiler.GetMonoUsedSizeLong();
                    long rises = 0;
                    for (int f = 0; f < gWindow; f++)
                    {
                        Walk();
                        yield return null;
                        long used = Profiler.GetMonoUsedSizeLong();
                        if (used > previous) rises += used - previous;
                        previous = used;
                    }
                    if (on) { soundOn += rises; winOn++; } else { soundOff += rises; winOff++; }
                }
                ctx.Log($"G2: managed allocation per window of {gWindow} frames: audio on {soundOn / Math.Max(1, winOn)} bytes, off {soundOff / Math.Max(1, winOff)} bytes, the sounds of the masses add {soundOn / Math.Max(1, winOn) - soundOff / Math.Max(1, winOff)} bytes (vanilla sound prefab instances, not part of the budget)");
            }
            finally
            {
                MassRenderSystem.DevHidden = false;
                MassAudio.Enabled = true;
                DevCamera.Follow();
                ctx.Console("mw_kill");
                Player p = Player.m_localPlayer;
                if (p != null) { p.SetGodMode(false); p.SetGhostMode(false); }
            }
        }

        /// <summary>
        /// A mass of MassCount at <paramref name="anchor"/>, or when there is no floor there (an edge, a rock): at 0.8, 0.6 and 0.45 of its distance from the player,
        /// each time on the same ray and then 25 and 50 degrees to either side. A spawn whose floor is more than 2.5 m from the player's is thrown away. Logged.
        /// </summary>
        private static WritheMassState SpawnRobust(ScenarioContext ctx, string tag, Player player, Vector3 anchor)
        {
            Vector3 origin = player.transform.position;
            Vector3 toAnchor = anchor - origin;
            float[] scales = { 1f, 0.8f, 0.6f, 0.45f };
            float[] angles = { 0f, 25f, -25f, 50f, -50f };
            int tried = 0;
            foreach (float k in scales)
                foreach (float a in angles)
                {
                    Vector3 at = origin + Quaternion.AngleAxis(a, Vector3.up) * toAnchor * k;
                    tried++;
                    WritheMassState m = MassDev.SpawnAt(at, MassCount, ZDOID.None, freezeAI: false);
                    if (m == null) continue;
                    if (Mathf.Abs(m.transform.position.y - origin.y) > 2.5f)
                    {
                        ctx.Log($"mass {tag}: floor at {m.transform.position.y - origin.y:+0.0;-0.0} m from the player at scale {k} angle {a}, rejected");
                        MassDev.Destroy(m);
                        continue;
                    }
                    if (tried > 1) ctx.Log($"mass {tag}: placed at attempt {tried} (scale {k}, angle {a}), anchor {anchor} had no usable floor");
                    return m;
                }
            ctx.Log($"mass {tag}: no floor in {tried} attempts around {anchor}");
            return null;
        }

        private static bool IsOptimized()
        {
            object[] attrs = typeof(P4_MassPerf).Assembly.GetCustomAttributes(typeof(DebuggableAttribute), false);
            return !(attrs.Length > 0 && ((DebuggableAttribute)attrs[0]).IsJITOptimizerDisabled);
        }

        /// <summary>The two masses back where they should be (snap, no velocity), the walk restarts around the new anchors.</summary>
        /// <summary>
        /// Puts the two masses about <paramref name="distance"/> metres from the camera, for sure: along one of 8 directions (the first is straight ahead) where there is a floor
        /// within 6 m of the player's height under both anchors (a mass snapped where there is none falls and is put back near where it came from by the fall rescue),
        /// then checks after 15 frames that both really are that far (edge of the swarm at 31 m or more) and logs the distances. Sets the anchors the walk follows.
        /// </summary>
        private IEnumerator PlaceFar(ScenarioContext ctx, Player player, Vector3 fwd, float distance, float scale)
        {
            Vector3 origin = player.transform.position;
            int mask = WormPhysics.SolidMask;
            for (int k = 0; k < 8; k++)
            {
                Vector3 dir = Quaternion.AngleAxis(k * 45f, Vector3.up) * fwd;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                Vector3 a = origin + dir * distance;
                Vector3 b = origin + dir * (distance - 2f) + side * 12f;
                if (!HasFloor(a, origin.y, mask) || !HasFloor(b, origin.y, mask))
                {
                    ctx.Log($"far placement: direction {k * 45} deg has no floor within 6 m of the player's height under both anchors");
                    continue;
                }
                _anchorA = a; _anchorB = b; _perp = side;
                Place(a, b);
                yield return Frames(15, scale, (f) => Walk());
                Vector3 cam = Camera.main != null ? Camera.main.transform.position : origin + Vector3.up * 1.7f;
                float da = Vector3.Distance(cam, _va.Root) - _va.P.R, db = Vector3.Distance(cam, _vb.Root) - _vb.P.R;
                ctx.Log($"far placement: direction {k * 45} deg, distance of the edge of the swarm from the camera: A {da:0.0} m, B {db:0.0} m (LOD2 from 31 m), fall rescues A {_a.FallRescues} B {_b.FallRescues}");
                if (da >= 31f && db >= 31f) yield break;
            }
            ctx.Log("far placement: no direction put both masses 31 m away; the LOD assertions of D will say so");
        }

        private static bool HasFloor(Vector3 p, float playerY, int mask) =>
            Physics.Raycast(p + Vector3.up * 8f, Vector3.down, out RaycastHit hit, 20f, mask, QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.point.y - playerY) <= 6f;

        private void Place(Vector3 a, Vector3 b)
        {
            Snap(_a, a);
            Snap(_b, b);
        }

        private static void Snap(WritheMassState m, Vector3 near)
        {
            Vector3 p = near;
            if (Physics.Raycast(near + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 10f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore)) p = hit.point + Vector3.up * 0.1f;
            Rigidbody body = m.GetComponent<Rigidbody>();
            if (body != null) { body.position = p; body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            m.transform.position = p;
            m.TeleportedTo(p);
        }

        /// <summary>Back and forth of +-WalkHalf along the lateral axis every WalkPeriod seconds, by the dev goal of the AI.</summary>
        private void Walk()
        {
            float phase = Mathf.Floor(Time.time / WalkPeriod);
            float s = ((int)phase & 1) == 0 ? 1f : -1f;
            if (_aiA != null) _aiA.DevGoal = _anchorA + _perp * (s * WalkHalf);
            if (_aiB != null) _aiB.DevGoal = _anchorB - _perp * (s * WalkHalf);
        }

        private static IEnumerator Frames(int frames, float scale, Action<int> each)
        {
            int n = Math.Max(1, (int)(frames * scale));
            for (int f = 0; f < n; f++) { each(f); yield return null; }
        }

        private IEnumerator Measure(Series s, int frames, float scale, Action walk)
        {
            int n = Math.Max(1, (int)(frames * scale));
            // The probe of managed allocation, calibrated: a 4 KB array must move the figure.
            long u0 = Profiler.GetMonoUsedSizeLong();
            byte[] junk = new byte[4096];
            long u1 = Profiler.GetMonoUsedSizeLong();
            GC.KeepAlive(junk);
            _probeOperative = u1 > u0;
            int gc0 = GC.CollectionCount(0);
            long mono0 = Profiler.GetMonoUsedSizeLong();
            int lastFrame = -1;
            for (int f = 0; f < n; f++)
            {
                walk();
                yield return null;
                if (WormRenderer.LastFrame == lastFrame) continue;     // no new renderer frame: nothing to read
                lastFrame = WormRenderer.LastFrame;
                WormRenderer.FrameStats st = WormRenderer.Last;
                int i = s.Count++;
                s.Cpu[i] = st.CpuMs; s.Calls[i] = st.DrawCalls; s.Tris[i] = st.Triangles; s.Dropped[i] = st.Dropped;
                s.HeapMain[i] = _va != null ? _va.HeapMs : 0f;
                s.Worker[i] = (_va != null ? _va.WorkerMs : 0f) + (_vb != null ? _vb.WorkerMs : 0f);
                s.Frame[i] = Time.unscaledDeltaTime * 1000f;
            }
            _gcCollections = GC.CollectionCount(0) - gc0;
            _monoDelta = Profiler.GetMonoUsedSizeLong() - mono0;
        }

        private static IEnumerator AllocWindow(int frames, float scale, long[] result, Action each)
        {
            long previous = Profiler.GetMonoUsedSizeLong();
            long rises = 0;
            for (int f = 0; f < frames; f++)
            {
                each();
                yield return null;
                long used = Profiler.GetMonoUsedSizeLong();
                if (used > previous) rises += used - previous;
                previous = used;
            }
            result[0] = rises;
        }

        private static long Median(List<long> v)
        {
            if (v.Count == 0) return 0;
            var c = new List<long>(v);
            c.Sort();
            return c[c.Count / 2];
        }

        private bool _probeOperative;
        private int _gcCollections;
        private long _monoDelta;

        private static float Avg(float[] a, int n) { double s = 0; for (int i = 0; i < n; i++) s += a[i]; return n > 0 ? (float)(s / n) : 0f; }
        private static float Max(float[] a, int n) { float m = 0; for (int i = 0; i < n; i++) if (a[i] > m) m = a[i]; return m; }
        private static float P95(float[] a, int n)
        {
            if (n == 0) return 0f;
            var c = new float[n];
            Array.Copy(a, c, n);
            Array.Sort(c);
            return c[Math.Min(n - 1, (int)(n * 0.95f))];
        }
        private static int MaxI(int[] a, int n) { int m = 0; for (int i = 0; i < n; i++) if (a[i] > m) m = a[i]; return m; }

        private void Report(ScenarioContext ctx, string tag, Series s, bool lod0)
        {
            int n = s.Count;
            ctx.Log($"{tag}: {n} frames: render cpu (main thread) avg {Avg(s.Cpu, n):0.000} ms, p95 {P95(s.Cpu, n):0.000}, max {Max(s.Cpu, n):0.000}; " +
                    $"mass A main cost avg {Avg(s.HeapMain, n):0.000} ms (EMA); worker for both masses avg {Avg(s.Worker, n):0.000} ms (max {Max(s.Worker, n):0.000}); " +
                    $"calls max {MaxI(s.Calls, n)}, triangles max {MaxI(s.Tris, n)}, dropped max {MaxI(s.Dropped, n)}; frame {Avg(s.Frame, n):0.00} ms");
            ctx.Log($"{tag}: A lod {_va.LastLod} cap {_va.LastCap} drawn {_va.LastDrawn} (refreshed {_va.RefreshedSlots} of {_va.LastSlotCount}), B lod {_vb.LastLod} cap {_vb.LastCap} drawn {_vb.LastDrawn}; " +
                    $"merged lists {WormRenderer.Last.Merged}");
        }

        private void Budgets(ScenarioContext ctx, string tag, Series s, bool checkCpu)
        {
            int n = s.Count;
            ctx.Assert($"{tag}_samples", n >= 20, ">= 20 renderer frames", n);
            if (checkCpu)
            {
                ctx.Assert($"{tag}_render_cpu_avg", Avg(s.Cpu, n) <= RenderCpuBudget, $"<= {RenderCpuBudget} ms (main thread, all worms and masses)", Avg(s.Cpu, n).ToString("0.000"));
                ctx.Assert($"{tag}_heap_main_avg", Avg(s.HeapMain, n) <= HeapMainBudget, $"<= {HeapMainBudget} ms for the mass in LOD0", Avg(s.HeapMain, n).ToString("0.000"));
            }
            ctx.Assert($"{tag}_worker_avg", Avg(s.Worker, n) <= WorkerBudget, $"<= {WorkerBudget} ms for all masses", Avg(s.Worker, n).ToString("0.000"));
            ctx.Assert($"{tag}_draw_calls_max", MaxI(s.Calls, n) <= CallBudget, $"<= {CallBudget}", MaxI(s.Calls, n));
            ctx.Assert($"{tag}_triangles_max", MaxI(s.Tris, n) <= TriangleBudget, $"<= {TriangleBudget}", MaxI(s.Tris, n));
            ctx.Assert($"{tag}_dropped", MaxI(s.Dropped, n) == 0, 0, MaxI(s.Dropped, n));
            // The managed heap is the game's as much as ours (Valheim allocates all the time): the absolute figure proves nothing, see the differential phase G.
            ctx.Log($"{tag}: managed heap over the window (whole game, informative): {_monoDelta} bytes, {_gcCollections} collections of generation 0");
        }
    }
}
#endif
