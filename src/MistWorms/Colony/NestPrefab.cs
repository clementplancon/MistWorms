using System;
using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>
    /// Registers the <c>MW_WormNest</c> prefab (spec §10.1, decision 195): a clone of the vanilla egg <c>SeekerEgg</c> (<c>Destructible</c> and a persistent
    /// <c>ZNetView</c>) stripped of the hatching, the drop, the LOD group and every vanilla renderer and child, with a 0.9 x 0.5 m box collider and the
    /// <see cref="WormNest"/> component. Its visual is the worm renderer's <c>nest_idle</c> mesh, drawn by the component. It must exist before any zone is
    /// loaded on every peer, a dedicated server included: a server that meets an unknown prefab hash destroys the ZDO (decisions.md 1.3).
    /// </summary>
    internal static class NestPrefab
    {
        /// <summary>Size of the hit box of the mound (spec §8.1): 0.9 m wide, 0.5 m high.</summary>
        public static readonly Vector3 ColliderSize = new Vector3(0.9f, 0.5f, 0.9f);

        public static void Register()
        {
            GameObject clone = null;
            try
            {
                GameObject source = null;
                string sourceName = null;
                foreach (string candidate in new[] { VanillaNames.SeekerEgg, VanillaNames.CreepPropEggHanging })
                {
                    GameObject g = PrefabManager.Instance.GetPrefab(candidate);
                    if (g != null && g.GetComponent<Destructible>() != null) { source = g; sourceName = candidate; break; }
                }
                if (source == null)
                {
                    Plugin.Log.LogError($"[Nest] no clone base found ({VanillaNames.SeekerEgg}, {VanillaNames.CreepPropEggHanging}): {MWNames.NestPrefab} is not registered");
                    return;
                }

                clone = PrefabManager.Instance.CreateClonedPrefab(MWNames.NestPrefab, source);
                if (clone == null)
                {
                    Plugin.Log.LogError($"[Nest] could not clone {sourceName}: {MWNames.NestPrefab} is not registered");
                    return;
                }

                Configure(clone, out string summary);
                PrefabManager.Instance.AddPrefab(new CustomPrefab(clone, fixReference: false));
                clone = null;   // owned by Jötunn now
                Plugin.Log.LogInfo($"[Nest] {MWNames.NestPrefab} registered, cloned from {sourceName}: {summary}");
            }
            catch (Exception e)
            {
                Plugin.LogException("NestPrefab.Register", e);
                if (clone != null) UnityEngine.Object.Destroy(clone);
            }
        }

        /// <summary>Strips and tunes the clone. Works on a prefab below an inactive container, so nothing wakes up.</summary>
        private static void Configure(GameObject go, out string summary)
        {
            // 1. No hatching, no drop, no LOD group.
            int removed = RemoveInChildren<EggHatch>(go) + RemoveInChildren<DropOnDestroyed>(go) + RemoveInChildren<LODGroup>(go);

            // 2. Nothing vanilla is shown or hit: the children (meshes, effects, lights) and any renderer or collider of the root go.
            int children = 0;
            for (int i = go.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
                children++;
            }
            int visuals = RemoveInChildren<Renderer>(go) + RemoveInChildren<MeshFilter>(go);
            int colliders = RemoveInChildren<Collider>(go);

            // 3. One box for the mound, on the layer of the clone (Default): the player's attacks reach it like any destructible.
            var box = go.AddComponent<BoxCollider>();
            box.size = ColliderSize;
            box.center = new Vector3(0f, ColliderSize.y * 0.5f, 0f);
            box.isTrigger = false;

            // 4. The Destructible: the nest's hit points and resistances (re-applied at instance start for the synced config), no vanilla effect
            //    (the destruction effect is a local copy without ZNetView, WormEffects, so no network object is created by it) and no fragments.
            Destructible d = go.GetComponent<Destructible>() ?? throw new InvalidOperationException("the clone base has no Destructible");
            d.m_destructibleType = DestructibleType.Default;
            d.m_health = MWConfig.Nest.Health.Value;
            d.m_damages = MWConfig.BuildDamageModifiers();
            d.m_minDamageTreshold = 0f;
            d.m_minToolTier = 0;
            d.m_hitNoise = 0f;
            d.m_destroyNoise = 0f;
            d.m_triggerPrivateArea = false;
            d.m_ttl = 0f;
            d.m_spawnWhenDamaged = null;
            d.m_spawnWhenDestroyed = null;
            d.m_autoCreateFragments = false;
            d.m_destroyedEffect = new EffectList();
            d.m_hitEffect = new EffectList();
            d.m_hitEffectBig = new EffectList();
            d.m_hitEffectBuildup = new EffectList();
            d.m_hitEffectBigThreshold = 0f;
            d.m_hitEffectBuildUpThreshold = 0f;

            // 5. A persistent network object: the nests survive a save (spec §11.4).
            ZNetView view = go.GetComponent<ZNetView>() ?? throw new InvalidOperationException("the clone base has no ZNetView");
            view.m_persistent = true;
            view.m_distant = false;
            view.m_type = ZDO.ObjectType.Default;

            // 6. The name shown on hover: Destructible is not Hoverable, so a HoverText carries the token (the vanilla egg has none).
            HoverText hover = go.GetComponent<HoverText>() ?? go.AddComponent<HoverText>();
            hover.m_text = MWNames.NestNameToken;

            go.AddComponent<WormNest>();
            summary = $"{removed} components, {children} children, {visuals} visuals and {colliders} colliders removed, box {ColliderSize}, health {d.m_health}";
        }

        private static int RemoveInChildren<T>(GameObject go) where T : Component
        {
            T[] found = go.GetComponentsInChildren<T>(true);
            var distinct = new List<T>(found);
            foreach (T c in distinct)
                if (c != null) UnityEngine.Object.DestroyImmediate(c);
            return distinct.Count;
        }
    }
}
