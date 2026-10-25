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

        /// <summary>The loaded mass closest to <paramref name="from"/>, or null.</summary>
        public static WritheMassState Nearest(Vector3 from)
        {
            WritheMassState best = null;
            float bestSqr = float.MaxValue;
            foreach (WritheMassState m in WritheMassState.All)
            {
                if (m == null || !m.Valid) continue;
                float d = (m.transform.position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = m; }
            }
            return best;
        }

        private static Dictionary<string, object> Roles(int a, int b, int c) =>
            new Dictionary<string, object> { { "body", a }, { "flux", b }, { "border", c } };

        private static Dictionary<string, object> Fractions(int a, int b, int c)
        {
            float t = a + b + c;
            return new Dictionary<string, object> { { "body", t > 0f ? a / t : 0f }, { "flux", t > 0f ? b / t : 0f }, { "border", t > 0f ? c / t : 0f } };
        }

        /// <summary>
        /// What the swarm renderer did for this mass in the last frame it was drawn (route /mass-visual): visible and drawn roles, LOD, cap,
        /// engulf, the closest drawn worm to the camera, the highest drawn worm, smoothed root against body, cost.
        /// </summary>
        public static Dictionary<string, object> VisualJson(WritheMassState m)
        {
            var v = m.GetComponent<WritheMassVisual>();
            var d = new Dictionary<string, object> { { "zdoid", m.ZdoId.ToString() }, { "n", m.N } };
            if (v == null) { d["visual"] = false; return d; }
            d["visual"] = true;
            d["active"] = v.Active;
            d["culled"] = v.CulledLastFrame;
            d["lod"] = v.LastLod;
            d["cap"] = v.LastCap;
            d["slotsEvaluated"] = v.LastSlotCount;
            d["visibleSlots"] = v.VisibleTotal;
            d["drawn"] = v.LastDrawn;
            d["visibleRoles"] = Roles(v.VisibleByRole[0], v.VisibleByRole[1], v.VisibleByRole[2]);
            d["visibleRoleFraction"] = Fractions(v.VisibleByRole[0], v.VisibleByRole[1], v.VisibleByRole[2]);
            d["drawnRoles"] = Roles(v.DrawnByRole[0], v.DrawnByRole[1], v.DrawnByRole[2]);
            d["drawnRoleFraction"] = Fractions(v.DrawnByRole[0], v.DrawnByRole[1], v.DrawnByRole[2]);
            d["visFrac"] = v.VisFrac;
            d["threshold"] = v.LastP;
            d["closestDrawnToCamera"] = v.ClosestToCam == float.MaxValue ? -1f : v.ClosestToCam;
            d["maxDrawnHeight"] = v.MaxDrawnHeight;
            d["heapMs"] = v.HeapMs;
            d["cost"] = new Dictionary<string, object> { { "evaluateMs", v.EvalMs }, { "drawMs", v.DrawMs }, { "composeMs", v.ComposeMs }, { "rendererAddMs", v.AddMs }, { "evaluatedSlots", v.EvaluatedSlots } };
            d["engulf"] = v.EngulfShown;
            d["engulfForced"] = v.ForcedEngulf;
            d["hasTarget"] = v.HasTarget;
            d["radius"] = v.P.R;
            d["sigma"] = v.Sigma;
            d["speed"] = v.Speed;
            d["streamK"] = v.StreamK;
            d["pseudoRamp"] = v.PseudoRamp;
            d["root"] = v.Root;
            d["body"] = m.transform.position;
            d["rootToBody"] = Vector3.Distance(v.Root, m.transform.position);
            d["center"] = v.Center;
            d["boundRadius"] = v.BoundRadius;
            d["groundHave"] = v.Ground.Have;
            d["groundSamples"] = v.Ground.Samples;
            d["groundHits"] = v.Ground.Hits;
            d["groundTiltDeg"] = Vector3.Angle(Vector3.up, v.Up);
            d["blocked"] = new List<object> { v.Ground.Blocked0, v.Ground.Blocked1, v.Ground.Blocked2, v.Ground.Blocked3 };
            d["targetDist"] = v.P.TargetDist;
            d["targetTurns"] = v.P.TargetTurns;
            d["worker"] = new Dictionary<string, object>
            {
                { "enabled", MistWorms.Config.MWConfig.Performance.MassWorkerThread.Value }, { "running", MassWorker.Running }, { "jobsRun", MassWorker.JobsRun },
                { "mainMs", v.HeapMs }, { "workerMs", v.WorkerMs }, { "busy", v.Job != null && v.Job.Busy },
            };
            if (v.Slots != null)
            {
                Lobe e = v.LobeSnapshot[MassLayoutConst.EngulfLobe];
                d["engulfLobe"] = new Dictionary<string, object> { { "cx", e.Cx }, { "cz", e.Cz }, { "rx", e.Rx }, { "ry", e.Ry }, { "rz", e.Rz } };
                if (v.P.TargetDist >= 0f)
                {
                    V3 t = v.P.TargetLocal();
                    d["targetLocal"] = new Dictionary<string, object> { { "x", t.X }, { "z", t.Z } };
                }
            }
            d["fallsInProgress"] = MassTransients.LiveCount;
            d["fallsSpawned"] = MassTransients.Spawned;
            return d;
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
