#if MW_DEV
using System;
using System.Collections.Generic;
using MistWorms.Colony;
using MistWorms.Mass;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Dev
{
    /// <summary>Debug-only tooling of the writhing masses (plan 4B-1 point 6): creating, listing and removing masses, shared by the console, the bridge and the scenarios.</summary>
    internal static class MassDev
    {
        private const float FloorProbeUp = 2f;
        private const float FloorProbeDown = 8f;
        private const float SpawnLift = 0.02f;
        private const float MinSpawnDistance = 3f;
        private const float SpawnMargin = 2f;

        /// <summary>
        /// A mass of <paramref name="n"/> worms on the floor under <paramref name="near"/>. <paramref name="colony"/> is the colony the worms came
        /// from (ZDOID.None = none: a visual mass that cannot dissolve into worms). Returns null with no floor or no registered prefab.
        /// </summary>
        public static WritheMassState SpawnAt(Vector3 near, int n, ZDOID colony, bool freezeAI)
        {
            if (!Physics.Raycast(near + Vector3.up * FloorProbeUp, Vector3.down, out RaycastHit hit, FloorProbeUp + FloorProbeDown, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore))
                return null;
            WritheMassState mass = WritheMassState.Create(hit.point + Vector3.up * SpawnLift, n, colony);
            if (mass != null) mass.FreezeAI = freezeAI;
            return mass;
        }

        /// <summary>A mass standing exactly at <paramref name="position"/> (no floor search: the caller knows the floor point).</summary>
        public static WritheMassState SpawnExact(Vector3 position, int n, ZDOID colony, bool freezeAI)
        {
            WritheMassState mass = WritheMassState.Create(position, n, colony);
            if (mass != null) mass.FreezeAI = freezeAI;
            return mass;
        }

        /// <summary>
        /// A mass in front of the local player (far enough for its dome), attached to the colony around the player, created if needed
        /// like <see cref="ColonyDev.FindOrCreateColony"/>. <paramref name="note"/> says where the colony came from.
        /// </summary>
        public static WritheMassState SpawnInFront(int n, bool withColony, out WormColony colony, out string note)
        {
            colony = null;
            note = "no colony";
            Player player = Player.m_localPlayer;
            if (player == null) throw new InvalidOperationException("no local player (not in game)");
            if (!player.IsOwner()) throw new InvalidOperationException("the local player is not owned by this peer");

            if (withColony)
            {
                colony = ColonyDev.FindOrCreateColony(player, out note);
                if (colony == null) throw new InvalidOperationException("could not create a colony (prefab missing?)");
            }

            Vector3 fwd = player.transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            float distance = Mathf.Max(MinSpawnDistance, MassMath.Radius(Mathf.Max(1, n)) + SpawnMargin);
            WritheMassState mass = SpawnAt(player.transform.position + fwd * distance, n, colony != null ? colony.ZdoId : ZDOID.None, freezeAI: false);
            if (mass == null) throw new InvalidOperationException("could not create the mass (no floor in front of the player, or prefab missing)");
            return mass;
        }

        /// <summary>The loaded mass whose ZDOID prints as <paramref name="zdoid"/>, or null.</summary>
        public static WritheMassState Find(string zdoid)
        {
            foreach (WritheMassState m in WritheMassState.All)
                if (m != null && m.ZdoId.ToString() == zdoid) return m;
            return null;
        }

        /// <summary>Destroys every mass owned by this peer (mw_kill); returns how many.</summary>
        public static int KillAll()
        {
            var snapshot = new List<WritheMassState>(WritheMassState.All);
            int n = 0;
            foreach (WritheMassState m in snapshot)
            {
                if (m == null || !m.IsOwner) continue;
                Destroy(m);
                n++;
            }
            return n;
        }

        public static void Destroy(WritheMassState mass)
        {
            if (mass == null) return;
            if (ZNetScene.instance != null) ZNetScene.instance.Destroy(mass.gameObject);
            else UnityEngine.Object.Destroy(mass.gameObject);
        }

        public static Dictionary<string, object> Json(WritheMassState m)
        {
            var visual = m.GetComponent<WritheMassVisual>();
            var d = new Dictionary<string, object>
            {
                { "zdoid", m.ZdoId.ToString() },
                { "owner", m.IsOwner },
                { "n", m.N },
                { "partial", m.Partial },
                { "health", m.Health },
                { "maxHealth", m.Character != null ? m.Character.GetMaxHealth() : 0f },
                { "state", MassStateCodes.Describe(m.StateCode) },
                { "stateCode", (int)m.StateCode },
                { "radius", m.TargetRadius },
                { "shownRadius", visual != null ? visual.ShownRadius : -1f },
                { "pos", m.transform.position },
                { "colony", m.ColonyId.ToString() },
                { "dissolving", m.Dissolving },
                { "frozenAI", m.FreezeAI },
                { "hoverName", m.Character != null ? m.Character.GetHoverName() : null },
                { "stream", m.IsStream },
                { "streamWidth", m.StreamWidth },
                { "ribbonLength", m.IsStream ? m.RibbonLength : 0f },
                { "hitSpheres", m.HitSphereCount },
                { "target", m.TargetId.ToString() },
            };
            var ai = m.GetComponent<WritheMassAI>();
            if (ai != null)
            {
                d["ai"] = new Dictionary<string, object>
                {
                    { "costAvgMs", ai.CostAvgMs }, { "costMaxMs", ai.CostMaxMs },
                    { "moveTicks", ai.MoveTicks }, { "pathTicks", ai.PathTicks }, { "noPathTicks", ai.NoPathTicks },
                    { "agent", ai.PathAgent.ToString() },
                };
            }
            if (m.TryGetHitSphere(out Vector3 center, out float radius))
                d["hitSphere"] = new Dictionary<string, object> { { "center", center }, { "radius", radius }, { "layer", LayerMask.LayerToName(m.HitCollider.gameObject.layer) } };
            return d;
        }

        public static List<object> JsonList()
        {
            var list = new List<object>();
            foreach (WritheMassState m in WritheMassState.All)
                if (m != null && m.Valid) list.Add(Json(m));
            return list;
        }

        /// <summary>One line per mass for mw_state.</summary>
        public static List<string> Summary()
        {
            var lines = new List<string>();
            foreach (WritheMassState m in WritheMassState.All)
            {
                if (m == null || !m.Valid) continue;
                lines.Add($"mass {m.ZdoId}{(m.IsOwner ? " (owner)" : " (remote)")} n={m.N} partial={m.Partial:0.##} hp={m.Health:0.##}/{(m.Character != null ? m.Character.GetMaxHealth() : 0f):0.##} " +
                          $"state={MassStateCodes.Describe(m.StateCode)} r={m.TargetRadius:0.00} at {m.transform.position}");
            }
            return lines;
        }
    }
}
#endif
