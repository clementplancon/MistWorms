#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Dev.Preview;
using MistWorms.Localization;
using MistWorms.Mass;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// Shared body of S02 and S02b (spec §15.5, phase 4B-4): free worms spawned around a visible player gather and form masses on their own.
    /// Open ground, a player in god mode who never moves, worms spawned Idle 4 to 12 m away with a clear line from the chest. The scenario watches every
    /// frame for the largest mass, counts the masses and the free worms, and ends when the expected masses stand and no worm is left (or at the deadline).
    /// Parameters: seconds (the deadline), timeout.
    /// </summary>
    internal abstract class MassFormScenario : IDevScenario
    {
        private const float SpawnRadius = 10f;      // inside the 12 m at which a lone worm picks a target (WormSense.HuntRange): every worm can sense the player at once
        private const int SpawnPasses = 30;
        private const float StableSeconds = 1f;      // the expected masses must stand this long (they could still be merging)

        public abstract string Name { get; }

        /// <summary>Worms spawned.</summary>
        protected abstract int Worms { get; }

        /// <summary>Seconds the masses have to stand (spec §15.5).</summary>
        protected abstract float DefaultSeconds { get; }

        /// <summary>The counts of the expected masses, largest first.</summary>
        protected abstract int[] Expected(int cap);

        public IEnumerator Run(ScenarioContext ctx)
        {
            float seconds = ctx.ParamFloat("seconds", DefaultSeconds);
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 150f + seconds;
            bool gatheringBefore = ColonyDev.GatheringEnabled;
            bool mergeBefore = WritheMassState.MergeEnabled;
            bool clutterHidden = false;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                ColonyDev.GatheringEnabled = true;
                WritheMassState.MergeEnabled = true;
                yield return ctx.WaitFrames(3);

                bool prefab = ZNetScene.instance != null && ZNetScene.instance.GetPrefab(MWNames.MassPrefab) != null;
                ctx.Assert("prefab_registered", prefab, MWNames.MassPrefab, prefab ? "registered" : "missing");
                if (!prefab) yield break;

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
                ctx.ClearInventory();
                S18_GroupCoherence.ClearCreatures(ctx, player);
                yield return ctx.Wait(0.5f);

                // ---- spawn: widening passes until the wanted number is placed (some draws fall out of the line of sight)
                WormColony colony = null;
                int created = 0;
                for (int pass = 0; pass < SpawnPasses && created < Worms; pass++)
                {
                    float radius = SpawnRadius;
                    created += ColonyDev.Spawn(Worms - created, radius, WormState.Idle, true, out colony, out string how, tries: 30, sight: true);
                    if (pass == 0) ctx.Log($"colony: {how}");
                }
                ctx.Assert("worms_spawned", created == Worms, Worms, created);
                if (colony == null || created == 0) yield break;
                yield return ctx.WaitFrames(2);
                ctx.Assert("start_all_free_idle", colony.Store.AliveCount == created, created, colony.Store.AliveCount);

                // ---- watch
                int cap = MWConfig.Mass.MaxCount.Value;
                int[] expected = Expected(cap);
                float t0 = Time.time;
                float stableSince = -1f;
                float doneAt = -1f;
                int maxSeen = 0, peakMasses = 0;
                float nextLog = 0f;
                var counts = new List<int>();
                while (Time.time - t0 < seconds + StableSeconds)
                {
                    SortedCounts(colony, counts);
                    for (int k = 0; k < counts.Count; k++) maxSeen = Math.Max(maxSeen, counts[k]);
                    peakMasses = Math.Max(peakMasses, counts.Count);
                    bool match = colony.Store.AliveCount == 0 && Matches(counts, expected);
                    if (match) { if (stableSince < 0f) stableSince = Time.time; }
                    else stableSince = -1f;
                    if (Time.time - t0 >= nextLog)
                    {
                        nextLog += 2f;
                        ctx.Log($"t={Time.time - t0:0.0}s masses [{string.Join(", ", counts)}] free worms {colony.Store.AliveCount} (formed {colony.MassesFormed}, absorbed {colony.WormsAbsorbed}, merges {WritheMassState.MergesCompleted}, transferred {WritheMassState.WormsTransferred})");
                    }
                    if (stableSince >= 0f && Time.time - stableSince >= StableSeconds) { doneAt = stableSince - t0; break; }
                    yield return null;
                }

                SortedCounts(colony, counts);
                for (int k = 0; k < counts.Count; k++) maxSeen = Math.Max(maxSeen, counts[k]);
                if (colony.Store.AliveCount > 0) LogStragglers(ctx, colony, player);
                string want = "[" + string.Join(", ", expected) + "]";
                ctx.Assert("expected_masses_within_time", doneAt >= 0f && doneAt <= seconds, $"{want} and no free worm within {seconds:0} s",
                    doneAt >= 0f ? $"{doneAt:0.0} s" : $"[{string.Join(", ", counts)}] and {colony.Store.AliveCount} free worms after {seconds:0} s");
                ctx.Assert("never_more_than_the_cap", maxSeen <= cap, $"<= {cap}", maxSeen);
                int sum = 0;
                foreach (int c in counts) sum += c;
                ctx.Assert("worms_conserved", sum + colony.Store.AliveCount == created, created, $"{sum} in masses + {colony.Store.AliveCount} free");
                ctx.Log($"peak number of masses at once: {peakMasses}; formed {colony.MassesFormed} masses from {colony.WormsFormed} worms, absorbed {colony.WormsAbsorbed}, complete merges {WritheMassState.MergesCompleted}, worms handed over {WritheMassState.WormsTransferred}");

                // ---- the largest mass: health, name, state
                WritheMassState biggest = Biggest(colony);
                if (biggest != null)
                {
                    float perWorm = MWConfig.Worm.Health.Value;
                    int n = biggest.N;
                    ctx.Assert("hp_is_n_times_worm_health", Math.Abs(biggest.Health - n * perWorm) < 0.01f && Math.Abs(biggest.Character.GetMaxHealth() - n * perWorm) < 0.01f,
                        $"{n * perWorm:0.#} / {n * perWorm:0.#}", $"{biggest.Health:0.#} / {biggest.Character.GetMaxHealth():0.#}");
                    string expectedName = MWLocalization.Format(MWNames.MassCountToken, MWNames.MassNameToken, n);
                    ctx.Assert("name_shows_the_count", biggest.Character.GetHoverName() == expectedName && expectedName.Contains($"({n})"), expectedName, biggest.Character.GetHoverName());
                    ctx.Assert("mass_belongs_to_the_colony", biggest.ColonyId == colony.ZdoId, colony.ZdoId.ToString(), biggest.ColonyId.ToString());
                }
                yield return ctx.Shot("masses");
            }
            finally
            {
                ColonyDev.GatheringEnabled = gatheringBefore;
                WritheMassState.MergeEnabled = mergeBefore;
                if (clutterHidden && ClutterSystem.instance != null) ClutterSystem.instance.enabled = true;
                DevCamera.Follow();
                ctx.Console("mw_kill");
                ctx.ClearInventory();
                Player p = Player.m_localPlayer;
                if (p != null) p.SetGodMode(false);
            }
        }

        /// <summary>What lies between a worm and its goal at the height of the wall probe of the simulation: the first solid thing a straight line meets.</summary>
        private static string Blocker(Vector3 from, Vector3 to)
        {
            Vector3 lift = Vector3.up * WormPhysics.WallHeight;
            if (Physics.Linecast(from + lift, to + lift, out RaycastHit hit, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore))
                return $"blocked by {hit.collider.name} (layer {LayerMask.LayerToName(hit.collider.gameObject.layer)}) at {hit.distance:0.0} m of {Vector3.Distance(from, to):0.0} m, normal {hit.normal}";
            return $"straight line to the goal ({Vector3.Distance(from, to):0.0} m) is clear";
        }

        /// <summary>The worms still free at the end: state, distance to the player and to the nearest mass, target, line of sight, time in the state.</summary>
        private static void LogStragglers(ScenarioContext ctx, WormColony colony, Player player)
        {
            WormStore s = colony.Store;
            for (int i = 0; i < s.HighWater; i++)
            {
                if (!s.alive[i]) continue;
                float toMass = float.MaxValue;
                foreach (WritheMassState m in WritheMassState.All)
                    if (m != null && m.Valid) toMass = Math.Min(toMass, Vector3.Distance(m.transform.position, s.pos[i]));
                ctx.Log($"straggler {i}: {(WormState)s.state[i]} for {s.stateTime[i]:0.0} s (history: {colony.DevStateHistory(i)}), {Vector3.Distance(player.transform.position, s.pos[i]):0.0} m from the player, {(toMass == float.MaxValue ? -1f : toMass):0.0} m from the nearest mass, target {s.targetId[i]}, line of sight {s.hasLos[i]}, goal {s.wanderGoal[i]}, gather kind {s.subPhase[i]} (1 = a mass, 2 = a group), {colony.DevStepInfo(i)}, stalls on the way to a mass {colony.GatherStalls}, goal to the nearest mass {(toMass == float.MaxValue ? -1f : 0f):0}; colony masses:{colony.DevMassTable()}; {Blocker(s.pos[i], s.wanderGoal[i])}");
            }
        }

        /// <summary>The counts of the valid masses of the colony, largest first.</summary>
        private static void SortedCounts(WormColony colony, List<int> into)
        {
            into.Clear();
            ZDOID id = colony.ZdoId;
            foreach (WritheMassState m in WritheMassState.All)
                if (m != null && m.Valid && m.ColonyId == id && !m.Dissolving) into.Add(m.N);
            into.Sort((a, b) => b.CompareTo(a));
        }

        private static bool Matches(List<int> counts, int[] expected)
        {
            if (counts.Count != expected.Length) return false;
            for (int k = 0; k < expected.Length; k++)
                if (counts[k] != expected[k]) return false;
            return true;
        }

        private static WritheMassState Biggest(WormColony colony)
        {
            WritheMassState best = null;
            foreach (WritheMassState m in WritheMassState.All)
                if (m != null && m.Valid && m.ColonyId == colony.ZdoId && (best == null || m.N > best.N)) best = m;
            return best;
        }
    }

    /// <summary>S02 (spec §15.5): 40 worms spawned around a visible player become one mass of 40 within 10 s (hp 400, named "Writhing Mass (40)").</summary>
    internal sealed class S02_MassForm : MassFormScenario
    {
        public override string Name => "S02_MassForm";
        protected override int Worms => 40;
        protected override float DefaultSeconds => 10f;
        protected override int[] Expected(int cap) => new[] { Worms };
    }

    /// <summary>S02b (spec §15.5, decision 65): 240 worms become a mass of 200 and a mass of 40, never more than 200 in one.</summary>
    internal sealed class S02b_MassCap : MassFormScenario
    {
        public override string Name => "S02b_MassCap";
        protected override int Worms => 240;
        protected override float DefaultSeconds => 45f;
        protected override int[] Expected(int cap) => new[] { cap, Worms - cap };
    }
}
#endif
