using System;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>
    /// Registers the invisible <c>MW_WormColony</c> prefab (spec §2, §10.1). It must exist before any zone is loaded: a server
    /// that meets an unknown prefab hash destroys the ZDO (decisions.md 1.3). Registered on every peer, dedicated servers included.
    /// </summary>
    internal static class ColonyPrefab
    {
        public static void Register()
        {
            // PrefabManager.CreateEmptyPrefab builds a primitive cube (mesh, renderer, collider), so the prefab is built by hand.
            // It is assembled below an inactive holder so ZNetView.Awake (which destroys the component without a ZDOMan) never runs;
            // AddPrefab then moves it under Jötunn's own inactive container, like any other custom prefab.
            var holder = new GameObject("MW_ColonyPrefabHolder");
            holder.SetActive(false);
            try
            {
                var go = new GameObject(MWNames.ColonyPrefab);
                go.transform.SetParent(holder.transform, false);

                // ZNetView first, so its Awake (creating the ZDO) runs before WormColony.Awake on an instance.
                var view = go.AddComponent<ZNetView>();
                view.m_persistent = true;
                view.m_type = ZDO.ObjectType.Default;
                view.m_distant = false;
                go.AddComponent<WormColony>();

                PrefabManager.Instance.AddPrefab(new CustomPrefab(go, fixReference: false));
            }
            catch (Exception e)
            {
                Plugin.LogException("ColonyPrefab.Register", e);
            }
            finally
            {
                UnityEngine.Object.Destroy(holder);
            }
        }
    }
}
