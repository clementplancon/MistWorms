#if MW_DEV
using System;
using System.Collections;
using MistWorms.Colony;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// S10 (spec §15.5, §8.2, decision 191): 250 worms asked for without force give at most Colony.MaxAlivePerMine (200); with force the dev build goes over
    /// (it keeps spare slots); a nest lays nothing while the mine is full and lays again once there is room. Open ground, a test colony, sleeping worms.
    /// </summary>
    internal sealed class S10_MineCap : IDevScenario
    {
        private const int Requested = 250;
        private const int ForcedExtra = 10;
        private const int Passes = 12;
        private const float SpawnRadius = 12f;

        public string Name => "S10_MineCap";

        public IEnumerator Run(ScenarioContext ctx)
        {
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 200f;
            bool clutterHidden = false;
            bool gatheringBefore = ColonyDev.GatheringEnabled;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                ColonyDev.GatheringEnabled = false;
                yield return ctx.WaitFrames(3);

                if (ClutterSystem.instance != null)
                {
                    ClutterSystem.instance.enabled = false;
                    ClutterSystem.instance.ClearAll();
                    clutterHidden = true;
                }
                yield return ctx.MoveToOpenGround(14f);
                if (!ctx.LastOpenGround.Found) yield break;
                Player player = Player.m_localPlayer;
                player.SetGodMode(true);
                player.SetGhostMode(false);
                ctx.GuardCreatures = true;

                int cap = MWConfig.Colony.MaxAlivePerMine.Value;

                // ---- 250 requested, no force
                WormColony colony = null;
                int created = 0;
                for (int pass = 0; pass < Passes && (colony == null || colony.TotalAlive < cap); pass++)
                {
                    created += ColonyDev.Spawn(Requested - created, SpawnRadius, WormState.Sleeping, false, out colony, out string how, tries: 30);
                    if (pass == 0) ctx.Log($"colony: {how}");
                    yield return null;
                }
                if (colony == null) yield break;
                ctx.Assert("never_more_than_the_cap", created <= cap && colony.Store.AliveCount <= cap, $"<= {cap}", $"created {created}, alive {colony.Store.AliveCount}");
                ctx.Assert("cap_reached", colony.Store.AliveCount == cap, cap, colony.Store.AliveCount);
                int refused = ColonyDev.Spawn(5, SpawnRadius, WormState.Sleeping, false, out _, out _, tries: 30);
                ctx.Assert("full_mine_refuses_more", refused == 0 && colony.Store.AliveCount == cap, 0, $"created {refused}, alive {colony.Store.AliveCount}");

                // ---- force goes over (debug builds keep spare slots)
                int forced = ColonyDev.Spawn(ForcedExtra, SpawnRadius, WormState.Sleeping, true, out _, out _, tries: 30);
                ctx.Assert("force_goes_over_the_cap", forced == ForcedExtra && colony.Store.AliveCount == cap + ForcedExtra, cap + ForcedExtra, colony.Store.AliveCount);

                // ---- a nest in a full mine lays nothing
                WormColony.DevSpawnInterval = 2f;
                WormNest placed = NestDev.PlaceInFront(3f, out WormColony nestColony, out _);
                ctx.Assert("nest_in_the_same_colony", nestColony == colony, colony.ZdoId.ToString(), nestColony.ZdoId.ToString());
                nestColony.DevWakeGroup(placed.Index);    // a sleeping group lays nothing (phase 7-2): the cap is what this scenario is about
                int laidBefore = colony.EggsLaid;
                yield return ctx.WaitGame(7f);
                ctx.Assert("full_mine_lays_nothing", colony.EggsLaid == laidBefore && colony.Store.AliveCount == cap + ForcedExtra, "no egg, count unchanged", $"eggs {colony.EggsLaid - laidBefore}, alive {colony.Store.AliveCount}");

                // ---- room again: the nest lays, and never above the cap
                int toKill = ForcedExtra + 3;
                int killed = 0;
                for (int i = 0; i < colony.Store.HighWater && killed < toKill; i++)
                {
                    if (!colony.Store.alive[i]) continue;
                    colony.KillWorm(i, false);
                    killed++;
                }
                int low = colony.Store.AliveCount;
                float t0 = Time.time;
                int peak = low;
                while (Time.time - t0 < 14f)
                {
                    peak = Math.Max(peak, colony.TotalAlive);
                    yield return null;
                }
                ctx.Assert("laying_resumes_when_there_is_room", colony.EggsLaid - laidBefore >= 1, ">= 1 egg", colony.EggsLaid - laidBefore);
                ctx.Assert("laying_stops_at_the_cap", peak <= cap && colony.Store.AliveCount == cap, $"filled to {cap}, never above", $"peak {peak}, alive {colony.Store.AliveCount}");
            }
            finally
            {
                WormColony.DevSpawnInterval = 0f;
                ColonyDev.GatheringEnabled = gatheringBefore;
                if (clutterHidden && ClutterSystem.instance != null) ClutterSystem.instance.enabled = true;
                ctx.Console("mw_kill");
                Player p = Player.m_localPlayer;
                if (p != null) p.SetGodMode(false);
            }
        }
    }
}
#endif
