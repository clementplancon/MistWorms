#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Mass;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// S07 (spec §15.5, phase 4B-4): the dissolution of a mass. A mass of 8 (AI frozen, drawn for a moment so the layout has worms to leap from) takes
    /// one injected hit of 40 blunt: 4 worms die, 4 are left, under Mass.DissolveThreshold, so the mass is dissolved:
    ///  - the mass is Dissolving and then destroyed, and 4 free worms exist, all Fleeing;
    ///  - they leave from places of the heap (within its radius), and leap: each rises at least 0.2 m and lands 0.4 m or more from where it started
    ///    (spec §4.4: 0.3 to 0.6 m high, 0.5 to 1.5 m away; a wall may shorten the leap);
    ///  - after Worm.FleeDuration, with 3 fugitives or more close enough, they gather again and form a mass within 15 s of the hit.
    /// Open ground, god player who never moves. Parameter: regroup_seconds (15).
    /// </summary>
    internal sealed class S07_MassDissolve : IDevScenario
    {
        private const int StartCount = 8;
        private const float SpawnDistance = 4.5f;
        private const float LeapObserveSeconds = 0.7f;
        private const float MinRise = 0.2f;
        private const float MinTravel = 0.4f;

        public string Name => "S07_MassDissolve";

        public IEnumerator Run(ScenarioContext ctx)
        {
            float regroupSeconds = ctx.ParamFloat("regroup_seconds", 15f);
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 120f + regroupSeconds;
            bool gatheringBefore = ColonyDev.GatheringEnabled;
            bool clutterHidden = false;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                ColonyDev.GatheringEnabled = true;
                yield return ctx.WaitFrames(3);
                if (ZNetScene.instance == null || ZNetScene.instance.GetPrefab(MWNames.MassPrefab) == null)
                {
                    ctx.Assert("prefab_registered", false, MWNames.MassPrefab, "missing");
                    yield break;
                }

                if (ClutterSystem.instance != null)
                {
                    ClutterSystem.instance.enabled = false;
                    ClutterSystem.instance.ClearAll();
                    clutterHidden = true;
                }
                yield return ctx.MoveToOpenGround(8f);
                if (!ctx.LastOpenGround.Found) yield break;

                Player player = Player.m_localPlayer;
                player.SetGodMode(true);
                player.SetGhostMode(false);
                ctx.ClearInventory();
                S18_GroupCoherence.ClearCreatures(ctx, player);

                WormColony colony = ColonyDev.FindOrCreateColony(player, out string how);
                ctx.Assert("colony_ready", colony != null, "a colony", how);
                if (colony == null) yield break;
                yield return ctx.WaitFrames(3);

                float perWorm = MWConfig.Worm.Health.Value;
                Vector3 fwd = Forward(player);
                WritheMassState mass = MassDev.SpawnAt(player.transform.position + fwd * SpawnDistance, StartCount, colony.ZdoId, freezeAI: true);
                ctx.Assert("mass_created", mass != null && mass.Valid, $"a mass of {StartCount}", mass != null ? mass.ZdoId.ToString() : "none");
                if (mass == null) yield break;

                // Let the swarm renderer draw it, so the layout has drawn worms to leap from.
                WritheMassVisual visual = mass.GetComponent<WritheMassVisual>();
                float waited = 0f;
                while (waited < 3f && (visual == null || visual.LastDrawn < StartCount / 2)) { yield return null; waited += Time.deltaTime; }
                ctx.Log($"heap drawn: {(visual != null ? visual.LastDrawn : -1)} worms after {waited:0.0} s");
                Vector3 massPos = mass.transform.position;
                float heapRadius = MassMath.Radius(StartCount);

                // ---- the hit: 40 blunt kill 4 of 8
                int aliveBefore = colony.Store.AliveCount;
                float t0 = Time.time;
                mass.Character.Damage(NewHit(mass, blunt: 4f * perWorm));
                var fugitives = new List<int>();
                WormStore s = colony.Store;
                for (int frame = 0; frame < 30 && fugitives.Count < 4; frame++)
                {
                    yield return null;
                    fugitives.Clear();
                    for (int i = 0; i < s.HighWater; i++)
                        if (s.alive[i] && s.state[i] == (byte)WormState.Fleeing) fugitives.Add(i);
                }
                int created = colony.Store.AliveCount - aliveBefore;
                ctx.Assert("dissolve_four_fleeing", mass != null && mass.N == 4 && mass.Dissolving && created == 4 && fugitives.Count == 4,
                    "n=4, Dissolving, 4 new worms, all Fleeing",
                    mass != null ? $"n={mass.N} dissolving={mass.Dissolving} created={created} fleeing={fugitives.Count}" : "mass destroyed too early");

                // ---- where they start, and the leap
                int farStarts = 0;
                foreach (Vector3 start in colony.DissolveStarts)
                    if (Utils.DistanceXZ(start, massPos) > heapRadius + MassLayoutConst.BorderReachMax + 0.3f) farStarts++;
                ctx.Assert("start_on_the_heap", colony.DissolveStarts.Count == 4 && farStarts == 0, $"4 places within {heapRadius + MassLayoutConst.BorderReachMax + 0.3f:0.00} m of the mass",
                    $"{colony.DissolveStarts.Count} places, {farStarts} too far; {colony.DissolveFromDrawn} came from drawn worms of the heap");

                var y0 = new float[fugitives.Count];
                var p0 = new Vector3[fugitives.Count];
                var rise = new float[fugitives.Count];
                var travel = new float[fugitives.Count];
                for (int k = 0; k < fugitives.Count; k++)
                {
                    // The place of the heap this worm left from: the nearest recorded start (the worm may already be in the air).
                    Vector3 here = s.pos[fugitives[k]];
                    Vector3 best = here;
                    float bestD = float.MaxValue;
                    foreach (Vector3 start in colony.DissolveStarts)
                    {
                        float d = Utils.DistanceXZ(start, here);
                        if (d < bestD) { bestD = d; best = start; }
                    }
                    p0[k] = best;
                    y0[k] = best.y;
                }
                float watch0 = Time.time;
                while (Time.time - watch0 < LeapObserveSeconds)
                {
                    for (int k = 0; k < fugitives.Count; k++)
                    {
                        int i = fugitives[k];
                        if (!s.alive[i]) continue;
                        rise[k] = Mathf.Max(rise[k], s.pos[i].y - y0[k]);
                        travel[k] = Mathf.Max(travel[k], Utils.DistanceXZ(s.pos[i], p0[k]));
                    }
                    yield return null;
                }
                int tooLow = 0, tooShort = 0;
                float maxRise = 0f;
                for (int k = 0; k < fugitives.Count; k++)
                {
                    if (rise[k] < MinRise) tooLow++;
                    if (travel[k] < MinTravel) tooShort++;
                    maxRise = Mathf.Max(maxRise, rise[k]);
                }
                ctx.Log($"leaps: rise [{string.Join(", ", Array.ConvertAll(rise, v => v.ToString("0.00")))}] m, travel [{string.Join(", ", Array.ConvertAll(travel, v => v.ToString("0.00")))}] m");
                ctx.Assert("leap_rises", fugitives.Count == 4 && tooLow == 0, $"each worm rises >= {MinRise} m", $"{tooLow} too low, highest {maxRise:0.00} m");
                ctx.Assert("leap_travels", fugitives.Count == 4 && tooShort == 0, $"each worm lands >= {MinTravel} m from its start", $"{tooShort} too short");
                ctx.Assert("mass_destroyed", mass == null, "destroyed after the dome shrank", mass == null ? "destroyed" : "still there");
                yield return ctx.Shot("fleeing");

                // ---- regroup: four fugitives (3 are enough) become a mass again within 15 s of the hit
                float deadline = t0 + regroupSeconds;
                WritheMassState again = null;
                int fleeingNow = 0;
                while (Time.time < deadline)
                {
                    fleeingNow = 0;
                    for (int i = 0; i < s.HighWater; i++)
                        if (s.alive[i] && s.state[i] == (byte)WormState.Fleeing) fleeingNow++;
                    foreach (WritheMassState m in WritheMassState.All)
                        if (m != null && m.Valid && m.ColonyId == colony.ZdoId && !m.Dissolving && m.N >= 3) { again = m; break; }
                    if (again != null) break;
                    yield return null;
                }
                ctx.Assert("fugitives_regroup_into_a_mass", again != null, $"a mass of >= 3 within {regroupSeconds:0} s of the hit",
                    again != null ? $"mass of {again.N} after {Time.time - t0:0.0} s" : $"none after {regroupSeconds:0} s ({fleeingNow} still fleeing, {colony.Store.AliveCount} free worms)");
                if (again != null) yield return ctx.Shot("regrouped");
            }
            finally
            {
                ColonyDev.GatheringEnabled = gatheringBefore;
                if (clutterHidden && ClutterSystem.instance != null) ClutterSystem.instance.enabled = true;
                ctx.Console("mw_kill");
                ctx.ClearInventory();
                Player p = Player.m_localPlayer;
                if (p != null) p.SetGodMode(false);
            }
        }

        private static Vector3 Forward(Player player)
        {
            Vector3 f = player.transform.forward;
            f.y = 0f;
            return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
        }

        /// <summary>A hit with no attacker (vanilla lets it through; a declared but missing attacker would be thrown away, Character.cs:2273).</summary>
        private static HitData NewHit(WritheMassState mass, float blunt)
        {
            var hit = new HitData();
            hit.m_damage.m_blunt = blunt;
            hit.m_point = mass.transform.position + Vector3.up * 0.5f;
            hit.m_dir = Vector3.forward;
            return hit;
        }
    }
}
#endif
