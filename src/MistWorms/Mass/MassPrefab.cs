using System;
using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Mass
{
    /// <summary>
    /// Registers the <c>MW_WritheMass</c> prefab (spec §10.1, plan 4B-1 point 1): a clone of a vanilla crawler (SeekerBrood, then Tick),
    /// stripped of everything that would attack, drop, step or show, plus the components of the mass. It must exist before any zone is
    /// loaded on every peer, a dedicated server included: a server that meets an unknown prefab hash destroys the ZDO (decisions.md 1.3).
    /// Same moment as <see cref="Colony.ColonyPrefab"/>: <c>PrefabManager.OnVanillaPrefabsAvailable</c>.
    /// <para>
    /// API: <c>PrefabManager.CreateClonedPrefab</c> + <c>AddPrefab</c>, not <c>CreatureManager</c>/<c>CustomCreature</c>. The creature manager
    /// only adds what a spawned wild creature needs (spawn list entries, the character layer, a parent below its spawn-list container); the mass is
    /// never spawned by the game and has its own layer rules, so the prefab manager alone registers it in the ZNetScene.
    /// </para>
    /// </summary>
    internal static class MassPrefab
    {
        public static void Register()
        {
            GameObject clone = null;
            try
            {
                GameObject source = null;
                string sourceName = null;
                foreach (string candidate in VanillaNames.MassBaseCandidates)
                {
                    GameObject g = PrefabManager.Instance.GetPrefab(candidate);
                    if (g != null && g.GetComponent<Humanoid>() != null) { source = g; sourceName = candidate; break; }
                }
                if (source == null)
                {
                    Plugin.Log.LogError($"[Mass] no clone base found among [{string.Join(", ", VanillaNames.MassBaseCandidates)}]: {MWNames.MassPrefab} is not registered");
                    return;
                }

                clone = PrefabManager.Instance.CreateClonedPrefab(MWNames.MassPrefab, source);
                if (clone == null)
                {
                    Plugin.Log.LogError($"[Mass] could not clone {sourceName}: {MWNames.MassPrefab} is not registered");
                    return;
                }

                Configure(clone, out string summary);
                PrefabManager.Instance.AddPrefab(new CustomPrefab(clone, fixReference: false));
                clone = null;   // owned by Jötunn now
                Plugin.Log.LogInfo($"[Mass] {MWNames.MassPrefab} registered, cloned from {sourceName}: {summary}");
            }
            catch (Exception e)
            {
                Plugin.LogException("MassPrefab.Register", e);
                if (clone != null) UnityEngine.Object.Destroy(clone);
            }
        }

        /// <summary>Strips and tunes the clone. Works on a prefab below an inactive container, so nothing wakes up.</summary>
        private static void Configure(GameObject go, out string summary)
        {
            Character character = go.GetComponent<Character>() ?? throw new InvalidOperationException("the clone base has no Character");
            var humanoid = character as Humanoid ?? throw new InvalidOperationException("the clone base has no Humanoid");

            // 1. Components that attack, drop or step. The AI is replaced below: the prefab needs some BaseAI (EnemyHud), not a MonsterAI.
            int removed = 0;
            removed += Remove<MonsterAI>(go);
            removed += Remove<CharacterDrop>(go);
            removed += Remove<FootStep>(go);
            foreach (BaseAI other in go.GetComponents<BaseAI>()) { UnityEngine.Object.DestroyImmediate(other); removed++; }
            removed += RemoveInChildren<WeakSpot>(go);

            // 2. No equipment at all (the mass never attacks with a weapon).
            humanoid.m_defaultItems = new GameObject[0];
            humanoid.m_randomWeapon = new GameObject[0];
            humanoid.m_randomArmor = new GameObject[0];
            humanoid.m_randomShield = new GameObject[0];
            humanoid.m_randomSets = new Humanoid.ItemSet[0];
            humanoid.m_randomItems = new Humanoid.RandomItem[0];

            // 3. Nothing vanilla is shown. The Animator and its object, and the "Visual" child, must stay active: Character.Awake dereferences
            //    them (Character.cs:667-671). Only the renderers and the LODGroup go.
            int renderers = 0;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) { r.enabled = false; renderers++; }
            foreach (LODGroup lod in go.GetComponentsInChildren<LODGroup>(true)) lod.enabled = false;

            // 4. Only the root capsule is a body; any other collider of the base would be a second hit target.
            CapsuleCollider capsule = go.GetComponent<CapsuleCollider>();
            int colliders = 0;
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
            {
                if (c == capsule) continue;
                c.enabled = false;
                colliders++;
            }

            // 5. Identity, health, mobility, effects (plan 4B-1 point 1). Config dependent values are applied again at instance start
            //    (WritheMassState.ApplyConfig), once the synced config of the server is known.
            character.m_name = MWNames.MassNameToken;
            character.m_group = string.Empty;
            character.m_faction = Character.Faction.PlayerSpawned;
            character.m_health = MWConfig.Worm.Health.Value;
            character.m_staggerDamageFactor = 0f;
            character.m_staggerWhenBlocked = false;
            character.m_deathAnimation = false;
            character.m_canSwim = false;
            character.m_jumpForce = 0f;
            character.m_jumpForceForward = 0f;
            character.m_tolerateFire = false;
            float speed = MWConfig.Mass.MoveSpeed.Value;
            character.m_speed = speed;
            character.m_walkSpeed = speed;
            character.m_runSpeed = speed;
            character.m_damageModifiers = MWConfig.BuildDamageModifiers();
            character.m_deathEffects = new EffectList();   // no ragdoll, no vanilla death effect (the worms' green blood plays on the clients)
            character.m_hitEffects = new EffectList();     // m_critHitEffects and m_backstabHitEffects are kept

            // 6. Our components. The ZNetView and the Character already exist on the clone, so they wake up before these.
            var ai = go.AddComponent<WritheMassAI>();
            ai.m_pathAgentType = Pathfinding.AgentType.Humanoid;   // decisions.md 24 (closed by S08 in phase 4B-2)
            ai.m_avoidWater = true;
            ai.m_aggravatable = false;
            ai.m_spawnMessage = string.Empty;
            ai.m_deathMessage = string.Empty;
            ai.m_idleSound = new EffectList();
            ai.m_alertedEffects = new EffectList();
            go.AddComponent<WritheMassState>();
            go.AddComponent<WritheMassVisual>();

            summary = $"{removed} components removed, {renderers} renderers and {colliders} extra colliders disabled, health {character.m_health}, speed {speed}";
        }

        private static int Remove<T>(GameObject go) where T : Component
        {
            var found = new List<T>(go.GetComponents<T>());
            foreach (T c in found) UnityEngine.Object.DestroyImmediate(c);
            return found.Count;
        }

        private static int RemoveInChildren<T>(GameObject go) where T : Component
        {
            T[] found = go.GetComponentsInChildren<T>(true);
            foreach (T c in found) UnityEngine.Object.DestroyImmediate(c);
            return found.Length;
        }
    }
}
