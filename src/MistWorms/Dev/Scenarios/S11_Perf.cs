#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using MistWorms.Assets;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Dev.Preview;
using MistWorms.Mass;
using MistWorms.Render;
using MistWorms.Render.Gpu;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// S11_Perf (phase 8-2, decision 222; spec §15.5 S11): the worst case on a single peer. 400 free worms (forced: a load test at twice the game cap of 200 per mine, decision 191, for which the
    /// scenario raises <c>Colony.MaxAlivePerMine</c> to 400 while it runs, because the capacity of a colony is read when it is created) hunting the god mode player, plus 2 masses of 200, for 30 s
    /// with the camera on the melee. Gathering is off so that the 400 stay free. Budgets of the spec (§11, §19): simulation mean &lt;= 1.0 ms and worst frame &lt;= 3 ms (the frame of the first strike
    /// of the session left out, as everywhere), render &lt;= 0.5 ms (decision 218), instanced calls &lt;= 64 in all, managed allocation of the update of the colony &lt;= 0.5 MB over the 30 s
    /// (<see cref="WormColony.DevMeasureAlloc"/>). The render figures are those of P4_MassPerf (flipbook renderer plus the GPU rope colony). Network: measured by S17, not here. Run it with the
    /// <c>Profile</c> build.
    /// </summary>
    internal sealed class S11_Perf : IDevScenario
    {
        private const int FreeWorms = 400;
        private const int MassCount = 200;
        private const float WarmSeconds = 8f, MeasureSeconds = 30f;
        private const float SimMeanBudget = 1.0f, SimMaxBudget = 3.0f, RenderBudget = 0.5f;
        private const int CallBudget = 64;
        private const long AllocBudget = 512 * 1024;

        public string Name => "S11_Perf";

        public IEnumerator Run(ScenarioContext ctx)
        {
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 300f;
            int capBefore = MWConfig.Colony.MaxAlivePerMine.Value;
            bool gatherBefore = WormColony.GatheringEnabled;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;
                ctx.Assert("assets_loaded", AssetCatalog.IsReady, true, AssetCatalog.IsReady ? "ready" : AssetCatalog.Error);
                if (!AssetCatalog.IsReady || !MaterialFactory.EnsureBuilt()) yield break;
                ctx.Log(P4Optimized()
                    ? "build: optimized (Profile or Release): the timings are valid for the budgets"
                    : "WARNING: this build is NOT optimized (Debug): the timings below are not valid for the budgets; build with tools/build.ps1 -Configuration Profile");

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                DevPreview.Instance.Clear();
                MWConfig.Colony.MaxAlivePerMine.Value = FreeWorms;   // capacity 400 + the spare slots of the dev builds, read when the colony is created below
                WormColony.GatheringEnabled = false;
                Player player = Player.m_localPlayer;
                player.SetGodMode(true);
                yield return ctx.WaitFrames(3);
                yield return ctx.MoveToOpenGround(14f);
                if (!ctx.LastOpenGround.Found) yield break;
                player = Player.m_localPlayer;
                player.SetGodMode(true);
                ctx.ClearInventory();

                Vector3 fwd = player.transform.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
                Vector3 perp = Vector3.Cross(Vector3.up, fwd);
                Vector3 eye = player.transform.position + Vector3.up * 1.7f;
                DevCamera.Free(eye, eye + fwd * 8f);   // on the melee: the masses in front, the worms all around

                // ---- the scene: 2 masses of 200 in front, 400 free worms around that see the player
                WritheMassState a = P4_MassPerf.SpawnRobust(ctx, "A", player, player.transform.position + fwd * 9f - perp * 4f);
                WritheMassState b = P4_MassPerf.SpawnRobust(ctx, "B", player, player.transform.position + fwd * 12f + perp * 5f);
                ctx.Assert("masses_created", a != null && b != null, "two masses of 200", $"{(a != null)} {(b != null)}");
                if (a == null || b == null) yield break;

                int created = 0;
                for (int pass = 0; pass < 6 && created < FreeWorms; pass++)
                    created += ColonyDev.Spawn(FreeWorms - created, 11f, WormState.Idle, true, out _, out _, sight: true);
                WormColony colony = ColonyRegistry.FindFor(player.transform.position);
                ctx.Assert("free_worms_created", colony != null && created >= FreeWorms * 9 / 10, $">= {FreeWorms * 9 / 10} forced free worms", created);
                if (colony == null) yield break;
                ctx.Log($"{created} free worms (capacity {colony.Store.Capacity}), 2 masses of {a.N} and {b.N}, gathering off");

                // ---- warm-up: the first strikes, the paths, the JIT; then the window
                yield return ctx.Wait(WarmSeconds);
                colony.ResetSimPeaks();
                MistWorms.Bites.WormBite.DevResetBites();
                colony.ResetNetCounters();
                WormColony.DevResetAlloc();
                WormColony.DevMeasureAlloc = true;
                int gc0 = GC.CollectionCount(0);

                var sim = new List<float>(4096);
                var render = new List<float>(4096);
                var frame = new List<float>(4096);
                int callsMax = 0, trisMax = 0, droppedMax = 0, lastFrame = -1, hunting = 0;
                float end = Time.realtimeSinceStartup + MeasureSeconds;
                while (Time.realtimeSinceStartup < end)
                {
                    yield return null;
                    sim.Add(colony.LastSimMs);
                    frame.Add(Time.unscaledDeltaTime * 1000f);
                    if (WormRenderer.LastFrame == lastFrame) continue;
                    lastFrame = WormRenderer.LastFrame;
                    WormRenderer.FrameStats st = WormRenderer.Last;
                    render.Add(st.CpuMs + (float)GpuWormHost.LastMs);
                    callsMax = Math.Max(callsMax, st.DrawCalls + GpuWormHost.DrawCalls);
                    trisMax = (int)Math.Min(int.MaxValue / 2, Math.Max(trisMax, st.Triangles + GpuWormHost.Triangles));
                    droppedMax = Math.Max(droppedMax, st.Dropped);
                }
                WormColony.DevMeasureAlloc = false;
                int collections = GC.CollectionCount(0) - gc0;
                for (int i = 0; i < colony.Store.HighWater; i++)
                    if (colony.Store.alive[i] && (colony.Store.state[i] == (byte)WormState.Hunting || colony.Store.state[i] == (byte)WormState.Striking)) hunting++;
                yield return ctx.Shot("melee");

                double simMean = ErrorStats.Mean(sim), renderMean = ErrorStats.Mean(render);
                ctx.Log($"{sim.Count} frames over {MeasureSeconds:0} s: simulation mean {simMean:0.000} ms, p95 {ErrorStats.Percentile(sim, 95):0.000}, max {ErrorStats.Max(sim):0.00}, worst without the first strike {colony.PeakMsWithoutFirstStrike:0.00}; " +
                        $"render mean {renderMean:0.000} ms, p95 {ErrorStats.Percentile(render, 95):0.000}, max {ErrorStats.Max(render):0.000} ({render.Count} renderer frames); frame {ErrorStats.Mean(frame):0.00} ms, " +
                        $"calls max {callsMax}, triangles max {trisMax}, dropped max {droppedMax}");
                ctx.Log($"managed allocation of the colony's update, mod code: {WormColony.DevAllocBytes} bytes, vanilla damage calls apart: {WormColony.DevVanillaBytes} bytes over {WormColony.DevVanillaCalls} calls (reported, not asserted), over {WormColony.DevAllocFrames} frames ({collections} collections of generation 0 in the window, which hide part of a rise); " +
                        $"alive {colony.Store.AliveCount} free worms, {hunting} hunting or striking; masses {a.N} and {b.N}; strikes landed {colony.StrikesLanded}, missed {colony.StrikesMissed}");
                foreach (string line in colony.WorstFrames) if (line != null) ctx.Log("worst frame: " + line);

                ctx.Assert("samples", sim.Count >= 100 && render.Count >= 100, ">= 100 frames and renderer frames", $"{sim.Count} / {render.Count}");
                ctx.Assert("free_worms_still_alive", colony.Store.AliveCount >= FreeWorms * 9 / 10, $">= {FreeWorms * 9 / 10} free worms at the end", colony.Store.AliveCount);
                ctx.Assert("melee_engaged", hunting >= FreeWorms / 2, $">= {FreeWorms / 2} worms hunting or striking at the end", hunting);
                ctx.Assert("sim_mean", simMean <= SimMeanBudget, $"<= {SimMeanBudget} ms per frame (mean)", simMean.ToString("0.000"));
                // Decision 270: the clock of a tick is the wall clock, a garbage collection inside the tick lands in it without being a simulation cost. The peak is judged on the ticks without a collection;
                // the raw peak and the number of excluded ticks are reported, and more than 1 % excluded fails anyway (a chronic allocation is not masked).
                int excluded = colony.TicksWithGc, measured = Math.Max(1, colony.TicksMeasured);
                ctx.Log($"sim max: {colony.PeakMsWithoutGc:0.00} ms without GC ticks, raw {colony.PeakMsWithoutFirstStrike:0.00} ms, {excluded} of {measured} ticks excluded for a collection of generation 0 ({100.0 * excluded / measured:0.00} %)");
                // Decision 272: as S18 since decision 255, the peak is judged without the time inside the strike's call on the player (vanilla Damage), still without the GC ticks. Everything is reported.
                ctx.Log($"sim max detail: judged {colony.PeakMsWithoutVanillaWithoutGc:0.00} ms (no GC tick, no vanilla damage time); without GC ticks but with the vanilla call {colony.PeakMsWithoutGc:0.00} ms; raw {colony.PeakMsWithoutFirstStrike:0.00} ms; " +
                        $"worst time inside the vanilla call in one frame {colony.WorstVanillaMs:0.00} ms; vanilla time of the worst frame {colony.WorstFrameVanillaMs:0.00} ms");
                ctx.Log($"bites on the player: {MistWorms.Bites.WormBite.DevBites} calls, total {MistWorms.Bites.WormBite.DevSumCallMs:0.00} ms (handler of the mod {MistWorms.Bites.WormBite.DevSumHandlerMs:0.00} ms, of which Character.Damage {MistWorms.Bites.WormBite.DevSumDamageMs:0.00} ms); " +
                        $"slowest call {MistWorms.Bites.WormBite.DevWorstCallMs:0.00} ms = routed RPC without the handler {MistWorms.Bites.WormBite.DevWorstRpcOnlyMs:0.00} + handler {MistWorms.Bites.WormBite.DevWorstHandlerMs:0.00} (Character.Damage {MistWorms.Bites.WormBite.DevWorstDamageMs:0.00}, the rest of the handler is the mod's context and tag {MistWorms.Bites.WormBite.DevWorstHandlerMs - MistWorms.Bites.WormBite.DevWorstDamageMs:0.00})");
                if (MistWorms.Bites.WormBite.DevWorstCallMs - MistWorms.Bites.WormBite.DevWorstDamageMs > 1f)
                    ctx.Log($"WARNING: {MistWorms.Bites.WormBite.DevWorstCallMs - MistWorms.Bites.WormBite.DevWorstDamageMs:0.00} ms of the slowest bite is outside Character.Damage (routed RPC or mod handler): a cost of the mod to look at");
                ctx.Assert("sim_gc_ticks", excluded * 100 <= measured, "<= 1 % of the ticks hit by a GC collection", $"{excluded} / {measured}");
                ctx.Assert("sim_max", colony.PeakMsWithoutVanillaWithoutGc <= SimMaxBudget, $"<= {SimMaxBudget} ms (worst tick without a GC collection and without the vanilla damage call of a strike, the first strike of the session left out; with the vanilla call {colony.PeakMsWithoutGc:0.00} ms, raw max {colony.PeakMsWithoutFirstStrike:0.00} ms, {excluded} ticks excluded)", colony.PeakMsWithoutVanillaWithoutGc.ToString("0.00"));
                ctx.Assert("render_mean", renderMean <= RenderBudget, $"<= {RenderBudget} ms (main thread, flipbook renderer + GPU colony; decision 218)", renderMean.ToString("0.000"));
                ctx.Assert("draw_calls_max", callsMax <= CallBudget, $"<= {CallBudget} instanced calls in all", callsMax);
                ctx.Assert("dropped", droppedMax == 0, 0, droppedMax);
                ctx.Assert("gc_alloc", WormColony.DevAllocBytes <= AllocBudget, $"<= {AllocBudget} bytes over {MeasureSeconds:0} s (mod code; the vanilla damage call is reported apart: {WormColony.DevVanillaBytes} bytes)", WormColony.DevAllocBytes);
            }
            finally
            {
                WormColony.DevMeasureAlloc = false;
                WormColony.GatheringEnabled = gatherBefore;
                DevCamera.Follow();
                ctx.Console("mw_kill");
                MWConfig.Colony.MaxAlivePerMine.Value = capBefore;
                Player p = Player.m_localPlayer;
                if (p != null) p.SetGodMode(false);
            }
        }

        private static bool P4Optimized()
        {
            object[] attrs = typeof(S11_Perf).Assembly.GetCustomAttributes(typeof(System.Diagnostics.DebuggableAttribute), false);
            return !(attrs.Length > 0 && ((System.Diagnostics.DebuggableAttribute)attrs[0]).IsJITOptimizerDisabled);
        }
    }
}
#endif
