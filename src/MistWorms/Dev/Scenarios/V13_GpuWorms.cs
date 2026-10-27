#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Profiling;
using MistWorms.Colony;
using MistWorms.Dev.Preview;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// V13 (spike of the GPU worm path, visual v2): 1000, 5000 then 10000 GPU worms (bundle shader, procedural instancing) on open ground around the player;
    /// three captures each, from 1 m, 3 m and 10 m. Assertions: the bundle is loaded, the shader is supported, the compute kernel returned the right values,
    /// the main thread cost of the per frame path with 10000 worms is at most 0.2 ms (mean over 300 frames, upload of the whole buffer included), and no
    /// managed allocation in steady state (the differential check of P4, drawn against hidden, plus the heap rises seen around our own Update, which must be nil).
    /// The visual judgement (slimy tube, crawl wave, peristalsis, shadows, fog) is made on the captures. Run it with tools/build.ps1 -Configuration Profile.
    /// Parameters: frames (scale of the measured windows, default 1).
    /// </summary>
    internal sealed class V13_GpuWorms : IDevScenario
    {
        private static readonly int[] Counts = { 1000, 5000, 10000 };
        private static readonly float[] Distances = { 1f, 3f, 10f };
        private static readonly float[] Heights = { 0.25f, 0.7f, 2.2f };
        private const int Warm = 120, Measured = 300, Window = 300;
        private const double CostBudgetMs = 0.2;
        private const long DifferentialGuard = 256 * 1024;      // as P4: the heap noise of the game is 20 to 80 KB per window of 300 frames

        public string Name => "V13_GpuWorms";

        public IEnumerator Run(ScenarioContext ctx)
        {
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 300f;
            float scale = ctx.ParamFloat("frames", 1f);
            bool timeForced = false;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Assert("bundle_loaded", GpuWorms.BundleLoaded, true, GpuWorms.BundleLoaded ? "loaded" : GpuWorms.Error);
                ctx.Assert("shader_supported", GpuWorms.ShaderSupported, true, GpuWorms.ShaderSupported);
                ctx.Assert("compute_supported", GpuWorms.ComputeSupported, true, GpuWorms.ComputeSupported);
                ctx.Assert("compute_result_correct", GpuWorms.ComputeOk, true, GpuWorms.ComputeResult);
                if (!GpuWorms.Loaded) yield break;

                ctx.Log(IsOptimized()
                    ? "build: optimized (Profile or Release): the timings are valid for the budgets"
                    : "WARNING: this build is NOT optimized (Debug): the timings are not valid for the budget; build with tools/build.ps1 -Configuration Profile");

                ctx.Console("mw_kill");
                ctx.Console("mw_gpuworms 0");
                DevPreview.Instance.Clear();
                Player player = Player.m_localPlayer;
                player.SetGodMode(true);
                player.SetGhostMode(true);
                yield return ctx.WaitFrames(3);
                yield return ctx.MoveToOpenGround(30f);
                if (!ctx.LastOpenGround.Found) yield break;
                player = Player.m_localPlayer;
                player.SetGodMode(true);
                player.SetGhostMode(true);
                ctx.Console($"{VanillaNames.CmdTimeOfDay} 0.5");
                timeForced = true;

                Vector3 center = player.transform.position;
                Vector3 fwd = player.transform.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
                Vector3 side = Vector3.Cross(Vector3.up, fwd);

                double cost10k = -1;
                foreach (int count in Counts)
                {
                    ctx.Console($"mw_gpuworms {count}");
                    ctx.Assert($"spawn_{count}", GpuWorms.Count == count, count, GpuWorms.Count);
                    for (int k = 0; k < Distances.Length; k++)
                    {
                        Vector3 cam = center + side * Distances[k] + Vector3.up * Heights[k];
                        Vector3 look = center + fwd * (0.3f * Distances[k]) + Vector3.up * 0.0f;
                        DevCamera.Free(cam, look);
                        yield return ctx.WaitFrames(Warm / 4);
                        yield return ctx.Shot($"n{count}_{Distances[k]:0}m");
                    }

                    // cost of the per frame path, camera at 3 m
                    DevCamera.Free(center + side * 3f + Vector3.up * 0.7f, center + fwd * 1f);
                    yield return ctx.WaitFrames(Math.Max(10, (int)(Warm * scale)));
                    GpuWorms.ResetStats();
                    yield return ctx.WaitFrames(Math.Max(20, (int)(Measured * scale)));
                    ctx.Log($"{count} worms: main thread {GpuWorms.AvgMs:0.0000} ms/frame mean, max {GpuWorms.MaxMs:0.000} ms over {GpuWorms.Frames} frames; {GpuWorms.Count * (GpuWorms.IndexCount / 3)} triangles per pass; frame {Time.unscaledDeltaTime * 1000f:0.0} ms");
                    if (count == 10000) cost10k = GpuWorms.AvgMs;
                }

                ctx.Assert("main_thread_10000_under_budget", cost10k >= 0 && cost10k <= CostBudgetMs, $"<= {CostBudgetMs} ms/frame (mean)", cost10k.ToString("0.0000"));

                // ---- zero allocation in steady state: P4's differential (drawn against hidden, ABAB, median of the windows after the first two)
                long probe0 = Profiler.GetMonoUsedSizeLong();
                byte[] junk = new byte[4096];
                bool operative = Profiler.GetMonoUsedSizeLong() > probe0;
                GC.KeepAlive(junk);
                int window = Math.Max(20, (int)(Window * scale));
                var drawn = new List<long>();
                var hidden = new List<long>();
                for (int w = 0; w < 12; w++)
                {
                    bool hide = (w & 1) == 1;
                    GpuWorms.Hidden = hide;
                    yield return ctx.WaitFrames(20);
                    long previous = Profiler.GetMonoUsedSizeLong();
                    long rises = 0;
                    for (int f = 0; f < window; f++)
                    {
                        yield return null;
                        long used = Profiler.GetMonoUsedSizeLong();
                        if (used > previous) rises += used - previous;
                        previous = used;
                    }
                    if (w >= 2) (hide ? hidden : drawn).Add(rises);
                }
                GpuWorms.Hidden = false;
                long perDrawn = Median(drawn), perHidden = Median(hidden);
                ctx.Log($"allocation per window of {window} frames, median: drawn {perDrawn} bytes, hidden {perHidden} bytes, difference {perDrawn - perHidden}; drawn [{string.Join(", ", drawn)}], hidden [{string.Join(", ", hidden)}] (probe {(operative ? "sees a 4 KB allocation" : "DID NOT see a 4 KB allocation: inoperative")})");
                // The probe reads the heap of the whole process (the network, the log and the bridge allocate in other threads: 49 KB then 98 KB in one window of 300 frames, a differential of
                // 438 KB then 1.15 MB, while the code of the free worms is the one of 6c0c75b where this was green). As P4: the lowest windows are compared, not the medians.
                long minDrawn = long.MaxValue, minHidden = long.MaxValue;
                foreach (long v in drawn) minDrawn = Math.Min(minDrawn, v);
                foreach (long v in hidden) minHidden = Math.Min(minHidden, v);
                ctx.Log($"lowest windows: drawn {minDrawn} bytes, hidden {minHidden} bytes (difference {minDrawn - minHidden})");
                if (operative) ctx.Assert("no_allocation_differential", minDrawn - minHidden <= DifferentialGuard, $"<= {DifferentialGuard / 1024} KB more than hidden per window (lowest windows, the floor of the noise of the process; as P4)", minDrawn - minHidden);

                // The heap rises around our own Update: the direct proof. Several windows (the first absorbs what the runtime does once); a real allocation rises in nearly every frame of
                // every window, the noise of other threads in a few frames of some windows: the median window must be exactly 0 bytes, and every window is logged.
                var ownBytes = new List<long>();
                var ownFrames = new List<long>();
                for (int w = 0; w < 6; w++)
                {
                    GpuWorms.ResetStats();
                    GpuWorms.MeasureAlloc = true;
                    yield return ctx.WaitFrames(window);
                    GpuWorms.MeasureAlloc = false;
                    if (w >= 1) { ownBytes.Add(GpuWorms.OwnAllocBytes); ownFrames.Add(GpuWorms.OwnAllocFrames); }
                }
                long medianOwn = Median(ownBytes);
                ctx.Log($"heap rise around our own Frame per window of {window} frames: bytes [{string.Join(", ", ownBytes)}], frames with a rise [{string.Join(", ", ownFrames)}]");
                ctx.Assert("no_allocation_own_update", medianOwn == 0 && Median(ownFrames) == 0, "0 bytes in the median window of 5 (repeated windows, first dropped)", $"median {medianOwn} bytes, windows [{string.Join(", ", ownBytes)}]");

                // ---- lighting comparison: one GPU worm and one current worm (flipbook, Custom/Creature) side by side at 1 m, same light, same frame.
                // Both lie along the same line (the flipbook worm faces the player, who stands 1 m behind them); the camera looks from the side.
                ctx.Console("mw_gpuworms 0");
                ctx.Console("mw_freeze on");
                Vector3 mid = center + fwd * 1.5f;
                Vector3 posFlip = mid - fwd * 0.25f, posGpu = mid + fwd * 0.25f;
                int slot = ColonyDev.SpawnAt(posFlip, WormState.Idle, false, out WormColony colony);
                ctx.Assert("compare_flipbook_spawned", slot >= 0, "a flipbook worm on the ground", slot);
                if (slot >= 0)
                {
                    Vector3 flipPos = colony.Store.pos[slot];
                    float ground = flipPos.y;
                    Vector3 gpuPos = new Vector3(posGpu.x, ground, posGpu.z);
                    ctx.Assert("compare_gpu_placed", GpuWorms.PlaceSingle(gpuPos, -fwd, out string how), true, how);
                    yield return ctx.WaitFrames(Warm / 4);
                    Vector3 target = (flipPos + gpuPos) * 0.5f + Vector3.up * 0.01f;
                    DevCamera.Free(target + side * 1.0f + Vector3.up * 0.25f, target);
                    yield return ctx.WaitFrames(Warm / 4);
                    yield return ctx.Shot("compare_gpu_right_flipbook_left_1m");
                    DevCamera.Free(target + side * 0.5f + fwd * 0.5f + Vector3.up * 0.5f, target);
                    yield return ctx.WaitFrames(Warm / 4);
                    yield return ctx.Shot("compare_gpu_right_flipbook_left_34");
                }
            }
            finally
            {
                GpuWorms.MeasureAlloc = false;
                GpuWorms.Hidden = false;
                ctx.Console("mw_gpuworms 0");
                ctx.Console("mw_freeze off");
                ctx.Console("mw_kill");
                DevCamera.Follow();
                if (timeForced) ctx.Console($"{VanillaNames.CmdTimeOfDay} -1");
                Player p = Player.m_localPlayer;
                if (p != null) { p.SetGodMode(false); p.SetGhostMode(false); }
            }
        }

        private static long Median(List<long> v)
        {
            if (v.Count == 0) return 0;
            var c = new List<long>(v);
            c.Sort();
            return c[c.Count / 2];
        }

        private static bool IsOptimized()
        {
            object[] attrs = typeof(V13_GpuWorms).Assembly.GetCustomAttributes(typeof(DebuggableAttribute), false);
            return !(attrs.Length > 0 && ((DebuggableAttribute)attrs[0]).IsJITOptimizerDisabled);
        }
    }
}
#endif
