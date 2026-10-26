#if MW_DEV
using System;
using System.Collections;
using MistWorms.Colony;
using MistWorms.Dev.Preview;
using MistWorms.Mass;
using MistWorms.Render;
using MistWorms.Render.Gpu;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// V15 (visual v2, pass B2, plan A7): the free worms of the colony drawn as ropes of the GPU colony. Open ground, no creature around, the player in god mode. For each state of a
    /// free worm (idle, asleep, hunting = crawling, striking = rearing) a pair of worms of the same state stands side by side, the left one drawn as a rope, the right one as the
    /// flipbook it replaces (the dev switch <see cref="GpuFreeWorms.FlipbookAt"/>), the simulation frozen: captures from 1 m and 3 m (and 10 m), the pair side by side being the
    /// comparison. Then a hunting worm crawls to the player (clip of the crawl, rear and strike). Assertions: the ropes are drawn (counts of the host), nothing of them is drawn by
    /// the flipbook renderer, and with the dev switch off (<see cref="GpuFreeWorms.ForceFlipbook"/>) the flipbooks are back. The look is judged on the captures.
    /// </summary>
    internal sealed class V15_FreeWormsGpu : IDevScenario
    {
        private static readonly WormState[] States = { WormState.Idle, WormState.Sleeping, WormState.Hunting, WormState.Striking };

        public string Name => "V15_FreeWormsGpu";

        public IEnumerator Run(ScenarioContext ctx)
        {
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 300f;
            bool frozen = false, clutterHidden = false, timeForced = false;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;
                bool active = GpuMassBridge.FreeActive;
                ctx.Assert("free_ropes_active", active, "free worms drawn by the colony", active ? "on" : (GpuWormHost.Error ?? GpuWormBundle.Error ?? "off (config, device or pool)"));
                if (!active) yield break;

                ctx.Console("mw_kill");
                DevPreview.Instance.Clear();
                if (ClutterSystem.instance != null)
                {
                    ClutterSystem.instance.enabled = false;
                    ClutterSystem.instance.ClearAll();
                    clutterHidden = true;
                }
                yield return ctx.MoveToOpenGround(6f);
                if (!ctx.LastOpenGround.Found) yield break;
                ctx.Console($"{VanillaNames.CmdTimeOfDay} 0.5");
                timeForced = true;
                Player player = Player.m_localPlayer;
                player.SetGodMode(true);
                Vector3 fwd = player.transform.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
                Vector3 side = Vector3.Cross(Vector3.up, fwd);

                // ---- a pair per state: the rope on the left, the flipbook on the right
                foreach (WormState state in States)
                {
                    string tag = state.ToString().ToLowerInvariant();
                    ctx.Console("mw_kill");
                    ctx.Console("mw_freeze off");
                    yield return ctx.WaitFrames(5);
                    Vector3 mid = player.transform.position + fwd * 3.2f;
                    Vector3 left = mid - side * 0.18f, right = mid + side * 0.18f;
                    int slotL = ColonyDev.SpawnAt(left, state, true, out WormColony colony);
                    int slotR = colony != null ? ColonyDev.SpawnAt(right, state, true, out _) : -1;
                    ctx.Assert($"{tag}_pair_spawned", slotL >= 0 && slotR >= 0, "two worms of the state", $"{slotL}, {slotR}");
                    if (slotL < 0 || slotR < 0) continue;
                    // the state is set, then the simulation is frozen so that the poses hold; the sleeping and the striking ones need a moment to settle into their state
                    yield return ctx.WaitFrames(30);
                    ctx.Console("mw_freeze on");
                    frozen = true;
                    Vector3 pl = colony.Store.pos[slotL], pr = colony.Store.pos[slotR];
                    Vector3 across = pr - pl; across.y = 0f;
                    Vector3 acrossDir = across.sqrMagnitude > 1e-4f ? across.normalized : side;
                    Vector3 centre = 0.5f * (pl + pr);
                    GpuFreeWorms.FlipbookAt = p => Vector3.Dot(p - centre, acrossDir) > 0f;
                    GpuFreeWorms.ResetStats();
                    yield return ctx.WaitFrames(30);
                    int ropes = GpuWormHost.FreeDrawn(0) + GpuWormHost.FreeDrawn(1) + GpuWormHost.FreeDrawn(2);
                    ctx.Log($"{tag}: ropes drawn {ropes}, taken {GpuFreeWorms.Taken} of {GpuFreeWorms.Taken + GpuFreeWorms.Refused} offered; state of the left worm {(WormState)colony.Store.state[slotL]}, of the right {(WormState)colony.Store.state[slotR]}");
                    ctx.Assert($"{tag}_one_rope_one_flipbook", ropes >= 1 && ropes <= 2, "the left worm is a rope, the right one stays a flipbook", ropes);
                    // from the side of the pair, 1 m, 3 m and 10 m
                    Vector3 camSide = Vector3.Cross(Vector3.up, acrossDir);
                    yield return V14_GpuMassSandbox.Shots(ctx, $"{tag}_pair", () => centre, camSide, acrossDir, 0.02f);
                    GpuFreeWorms.FlipbookAt = null;
                    ctx.Console("mw_freeze off");
                    frozen = false;
                }

                // ---- all ropes, all flipbooks: the draw calls
                ctx.Console("mw_kill");
                yield return ctx.WaitFrames(5);
                {
                    Vector3 around = player.transform.position + fwd * 4f;
                    int placed = 0;
                    for (int i = 0; i < 40; i++)
                    {
                        Vector3 p = around + fwd * ((i % 8) * 0.4f - 1.4f) + side * ((i / 8) * 0.4f - 0.8f);
                        if (ColonyDev.SpawnAt(p, i % 3 == 0 ? WormState.Idle : WormState.Hunting, true, out _) >= 0) placed++;
                    }
                    ctx.Console("mw_freeze on");
                    frozen = true;
                    yield return ctx.WaitFrames(40);
                    WormRenderer.FrameStats gpu = WormRenderer.Last;
                    int ropes = GpuWormHost.FreeDrawn(0) + GpuWormHost.FreeDrawn(1) + GpuWormHost.FreeDrawn(2);
                    ctx.Log($"{placed} worms: ropes drawn {ropes}; flipbook renderer {gpu.DrawCalls} calls and {gpu.Instances} instances; the colony {GpuWormHost.DrawCalls} calls");
                    ctx.Assert("many_ropes_drawn", ropes >= placed * 9 / 10, $">= 90 % of the {placed} worms are ropes", ropes);
                    ctx.Assert("no_flipbook_drawn", gpu.Instances == 0, "no free worm drawn by the flipbook renderer", gpu.Instances);
                    ctx.Assert("one_to_four_calls", GpuWormHost.DrawCalls >= 1 && GpuWormHost.DrawCalls <= 4, "1 to 4 calls for all the ropes", GpuWormHost.DrawCalls);
                    GpuFreeWorms.ForceFlipbook = true;
                    yield return ctx.WaitFrames(40);
                    gpu = WormRenderer.Last;
                    ctx.Assert("fallback_flipbooks", gpu.Instances >= placed * 9 / 10 && GpuWormHost.FreeDrawn(0) + GpuWormHost.FreeDrawn(1) + GpuWormHost.FreeDrawn(2) == 0,
                        "with the switch off the worms are flipbooks again", $"{gpu.Instances} flipbook instances");
                    GpuFreeWorms.ForceFlipbook = false;
                    ctx.Console("mw_freeze off");
                    frozen = false;
                }

                // ---- the film of a crawl, a rear and a strike: a hunting worm walks to the player
                ctx.Console("mw_kill");
                yield return ctx.WaitFrames(5);
                {
                    Vector3 start = player.transform.position + fwd * 4f;
                    int slot = ColonyDev.SpawnAt(start, WormState.Hunting, true, out WormColony colony);
                    ctx.Assert("film_worm_spawned", slot >= 0, "a hunting worm 4 m away", slot);
                    if (slot >= 0)
                    {
                        Vector3 camPos = player.transform.position + fwd * 1.8f + side * 0.9f + Vector3.up * 0.35f;
                        DevCamera.Free(camPos, player.transform.position + fwd * 2.6f + Vector3.up * 0.05f);
                        yield return ctx.WaitFrames(20);
                        yield return ctx.Clip("crawl_rear_strike", 6f);
                    }
                }
            }
            finally
            {
                GpuFreeWorms.FlipbookAt = null;
                GpuFreeWorms.ForceFlipbook = false;
                DevCamera.Follow();
                if (frozen) ctx.Console("mw_freeze off");
                if (clutterHidden && ClutterSystem.instance != null) ClutterSystem.instance.enabled = true;
                if (timeForced) ctx.Console($"{VanillaNames.CmdTimeOfDay} -1");
                ctx.Console("mw_kill");
            }
        }
    }
}
#endif
