#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using MistWorms.Colony;
using MistWorms.Mass;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// S19 (spec §15.5, §5.4, plan 6-2, A8): the real <c>BaseAI.IsEnemy</c> matrix between a mass and creatures of every faction, in both directions, compared with
    /// the expected matrix. A mass of 10 (AI frozen, nothing is hunted) stands on open ground; for each faction that has a vanilla creature prefab one creature is
    /// spawned next to it, in view, evaluated in the same frame (its AI never runs) and removed; the factions without a prefab (Boss, TrainingDummy...) and the
    /// tamed case are evaluated on a probe creature whose faction (or tamed flag) is switched. The local player is the Players row; a second mass is the
    /// PlayerSpawned row. The SkyWhale row is not checked: a scenario never touches SW_* objects.
    /// Expected = the table of spec 5.4 (training dummy: true from the mass, false back; tamed: true both ways), compared exactly, with no tolerance.
    /// Also checks that the target rules of the mod skip the training dummy and the PlayerSpawned faction (TargetRules.FactionTargetable).
    /// </summary>
    internal sealed class S19_Factions : IDevScenario
    {
        public string Name => "S19_Factions";

        public IEnumerator Run(ScenarioContext ctx)
        {
            Player player = null;
            WritheMassState mass = null;
            WritheMassState other = null;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                yield return ctx.WaitFrames(3);

                player = Player.m_localPlayer;
                player.SetGodMode(true);
                player.SetGhostMode(false);
                yield return ctx.MoveToOpenGround(10f);
                if (!ctx.LastOpenGround.Found) yield break;
                player = Player.m_localPlayer;
                S18_GroupCoherence.ClearCreatures(ctx, player);

                Vector3 fwd = player.transform.forward;
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
                Vector3 site = player.transform.position + fwd * 8f;
                mass = MassDev.SpawnAt(site, 10, ZDOID.None, freezeAI: true);
                if (!ctx.Assert("mass_created", mass != null && mass.Valid, "a valid mass", mass != null ? mass.ZdoId.ToString() : "none")) yield break;
                other = MassDev.SpawnAt(site + Vector3.Cross(Vector3.up, fwd) * 6f, 10, ZDOID.None, freezeAI: true);
                yield return ctx.WaitFrames(5);

                Character massCharacter = mass.Character;
                // FactionId mirrors Character.Faction by value: a vanilla update that reorders the enum must fail here, not silently.
                bool sameEnum = Enum.GetNames(typeof(Character.Faction)).Length == Enum.GetNames(typeof(FactionId)).Length;
                foreach (Character.Faction f in Enum.GetValues(typeof(Character.Faction)))
                    if (!Enum.IsDefined(typeof(FactionId), (int)f) || Enum.GetName(typeof(FactionId), (int)f) != f.ToString()) sameEnum = false;
                ctx.Assert("faction_enum_mirrored", sameEnum, "FactionId = Character.Faction by name and value", sameEnum ? "identical" : "different");
                ctx.Assert("mass_faction_player_spawned", massCharacter.GetFaction() == Character.Faction.PlayerSpawned, Character.Faction.PlayerSpawned, massCharacter.GetFaction());

                var rows = new List<FactionDev.Row>();
                var probePrefab = (GameObject)null;
                var byFaction = FindPrefabs(out probePrefab);
                ctx.Log($"creature prefabs found for {byFaction.Count} factions: " + Describe(byFaction));

                // ---- a creature of each faction that has a prefab: spawned in view of the mass, evaluated in the same frame, removed
                Vector3 spot = mass.transform.position + Vector3.Cross(Vector3.up, fwd) * -4f;
                foreach (KeyValuePair<Character.Faction, GameObject> pair in byFaction)
                {
                    GameObject go = UnityEngine.Object.Instantiate(pair.Value, spot, Quaternion.identity);
                    Character c = go.GetComponent<Character>();
                    if (c != null) rows.Add(FactionDev.Evaluate(massCharacter, c));
                    if (ZNetScene.instance != null) ZNetScene.instance.Destroy(go); else UnityEngine.Object.Destroy(go);
                }

                // ---- the local player and a second mass
                rows.Add(FactionDev.Evaluate(massCharacter, player));
                if (other != null && other.Valid) rows.Add(FactionDev.Evaluate(massCharacter, other.Character));

                // ---- every faction value, and the tamed case, on a probe creature
                if (probePrefab != null)
                {
                    GameObject go = UnityEngine.Object.Instantiate(probePrefab, spot, Quaternion.identity);
                    Character probe = go.GetComponent<Character>();
                    Character.Faction original = probe.m_faction;
                    var swept = new HashSet<Character.Faction>();
                    foreach (FactionDev.Row r in rows) if (!r.Tamed) swept.Add(r.Faction);
                    foreach (Character.Faction f in Enum.GetValues(typeof(Character.Faction)))
                    {
                        if (swept.Contains(f)) continue;
                        probe.m_faction = f;
                        FactionDev.Row r = FactionDev.Evaluate(massCharacter, probe);
                        r.Prefab = "(probe " + r.Prefab + ")";
                        rows.Add(r);
                    }
                    probe.m_faction = original;
                    probe.m_tamed = true;
                    FactionDev.Row tamed = FactionDev.Evaluate(massCharacter, probe);
                    tamed.Prefab = "(probe " + tamed.Prefab + ", tamed)";
                    rows.Add(tamed);
                    probe.m_tamed = false;
                    if (ZNetScene.instance != null) ZNetScene.instance.Destroy(go); else UnityEngine.Object.Destroy(go);
                }
                else
                {
                    ctx.Log("no creature prefab to use as a probe: only the factions with a prefab are evaluated");
                }

                // ---- the matrix, as /state prints it
                var printed = new List<string>();
                int specOk = 0;
                foreach (FactionDev.Row r in rows)
                {
                    printed.Add($"{r.Faction}{(r.Tamed ? " (tamed)" : string.Empty)} [{r.Prefab}]: mass->other {r.MassToOther}, other->mass {r.OtherToMass}");
                    ctx.Log($"IsEnemy {printed[printed.Count - 1]} | spec {r.SpecMassToOther}/{r.SpecOtherToMass}");
                    if (r.MatchesSpec) specOk++;
                    string rowName = "is_enemy_" + r.Faction + (r.Tamed ? "_tamed" : string.Empty) + (r.Prefab.StartsWith("(probe") ? "_probe" : string.Empty);
                    ctx.Assert(rowName, r.MatchesSpec, $"{r.SpecMassToOther}/{r.SpecOtherToMass}", $"{r.MassToOther}/{r.OtherToMass}");
                }
                ctx.Assert("matrix_rows", rows.Count >= 8, ">= 8 rows (the factions with a prefab, the player, a mass, every faction value, tamed)", rows.Count);
                ctx.Assert("matrix_equals_spec", specOk == rows.Count, "the table of spec 5.4, exactly (" + rows.Count + " rows)", specOk + " rows");
                ctx.Log("SkyWhale row of the spec table: not checked (no SW_* object is touched by a scenario)");

                // ---- the target rules of the mod follow the same exemptions
                ctx.Assert("target_rules_skip_dummy", !TargetRules.FactionTargetable(Character.Faction.TrainingDummy), false, TargetRules.FactionTargetable(Character.Faction.TrainingDummy));
                ctx.Assert("target_rules_skip_player_spawned", !TargetRules.FactionTargetable(Character.Faction.PlayerSpawned), false, TargetRules.FactionTargetable(Character.Faction.PlayerSpawned));
                bool allOthers = true;
                foreach (Character.Faction f in Enum.GetValues(typeof(Character.Faction)))
                    if (f != Character.Faction.TrainingDummy && f != Character.Faction.PlayerSpawned && !TargetRules.FactionTargetable(f)) allOthers = false;
                ctx.Assert("target_rules_accept_every_other_faction", allOthers, true, allOthers);
                yield return ctx.Shot("matrix");
            }
            finally
            {
                ctx.Console("mw_kill");
                Player p = Player.m_localPlayer;
                if (p != null)
                {
                    p.SetGodMode(true);
                    p.SetHealth(p.GetMaxHealth());
                }
            }
        }

        /// <summary>One creature prefab per faction (the lowest health, not a boss, not a mod object), and the cheapest one overall as the probe.</summary>
        private static Dictionary<Character.Faction, GameObject> FindPrefabs(out GameObject probe)
        {
            var best = new Dictionary<Character.Faction, GameObject>();
            probe = null;
            float probeHealth = float.MaxValue;
            if (ZNetScene.instance == null) return best;
            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                if (prefab == null || prefab.name.StartsWith("MW_", StringComparison.Ordinal) || prefab.name.StartsWith("SW_", StringComparison.Ordinal)) continue;
                Character c = prefab.GetComponent<Character>();
                if (c == null || c is Player || c.m_boss || prefab.GetComponent<BaseAI>() == null) continue;
                Character.Faction f = c.GetFaction();
                if (f == Character.Faction.Players || f == Character.Faction.PlayerSpawned || f == Character.Faction.Boss) continue;
                if (!best.TryGetValue(f, out GameObject held) || c.m_health < held.GetComponent<Character>().m_health) best[f] = prefab;
                if (c.m_health < probeHealth && f != Character.Faction.TrainingDummy) { probeHealth = c.m_health; probe = prefab; }
            }
            return best;
        }

        private static string Describe(Dictionary<Character.Faction, GameObject> byFaction)
        {
            var parts = new List<string>();
            foreach (KeyValuePair<Character.Faction, GameObject> pair in byFaction) parts.Add($"{pair.Key}={pair.Value.name}");
            return string.Join(", ", parts);
        }
    }
}
#endif
