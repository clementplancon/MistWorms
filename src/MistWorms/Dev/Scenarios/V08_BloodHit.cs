#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Dev.Preview;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// V08: green blood (phase 4A-2), in the infested mine (tp-mine; open ground when no mine can be reached, the log says so). A frozen
    /// colony worm about 1.2 m from the player takes a non lethal hit through its hit proxy (the path of a vanilla weapon), then a lethal
    /// one; each is captured at +0.05, +0.1 and +0.2 s. The live particles of the effect are measured at +0.1 s (largest size, count) and
    /// at +0.2 s and +0.5 s (farthest distance from the origin). Other assertions: the templates exist without any ZNetView, the sounds
    /// resolve, the hit was received once, and no error reached the Unity log. The look of the captures is judged by eye.
    /// Parameter: where (mine, open).
    /// </summary>
    internal sealed class V08_BloodHit : IDevScenario
    {
        private const float SpawnDistance = 1.2f;
        // Camera beside the worm, from its radius and the field of view (D12): the worm (0.3 m) and the burst around it take 50 % of the height of the image, dive of 30 degrees.
        private const float SubjectRadius = 0.35f;
        private static float CameraDistance { get { ScenarioContext.Orbit(ScenarioContext.FitSphere(SubjectRadius), 30f, out float h, out _); return h; } }
        private static float CameraHeight { get { ScenarioContext.Orbit(ScenarioContext.FitSphere(SubjectRadius), 30f, out _, out float u); return u; } }
        private const float RaiseStep = 0.1f;
        private const int MaxRaises = 25;
        private const float FlatTolerance = 0.15f;      // open ground only: height spread allowed at 1.5 m and 3 m around the spot
        private const int SpotDirections = 16;
        private const float CameraClearance = 0.15f;

        private const int MaxLiveCountHit = 30;
        private const int MaxLiveCountDeath = 60;
        private const float MaxLiveSizeHit = 0.06f;     // spec §12.4: 1 to 3 cm particles (the ground decal is not counted)
        private const float MaxLiveSizeDeath = 0.1f;
        private const float MaxReachHit = 0.6f;         // (raised from 0.4 by the coordinator, decision to record: the jet varies from 0.25 to 0.58 m between runs; spec: about 0.3 m) farthest live particle from the origin at +0.2 s and +0.5 s
        private const float MaxReachDeath = 1.0f;

        private static readonly float[] ShotTimes = { 0.05f, 0.1f, 0.2f };
        private const float MeasureLate = 0.5f;
        private const float SettleTime = 1.5f;          // let the first jet end before the lethal hit

        public string Name => "V08_BloodHit";

        private int _errors;

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _errors++;
                Plugin.Log.LogWarning($"[V08] Unity {type}: {condition}");
            }
        }

        public IEnumerator Run(ScenarioContext ctx)
        {
            string where = ctx.Param("where", "mine").ToLowerInvariant();
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 200f;

            Application.logMessageReceived += OnLog;
            bool clutterHidden = false;
            GameObject visual = null;
            bool visualWasActive = false;
            DevShots.HideUi();   // once for the whole run: the captures keep their timing
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                yield return ctx.WaitFrames(3);

                Player.m_localPlayer.SetGodMode(true);
                bool inMine = false;
                if (where != "open")
                {
                    Player.m_localPlayer.SetGhostMode(true);
                    yield return ctx.TpMine();
                    inMine = ctx.LastTpMine.Ok;
                    if (!inMine) ctx.Log($"mine not reachable ({ctx.LastTpMine.Error}): falling back to open ground");
                    else yield return ctx.Wait(1f);
                }
                if (!inMine)
                {
                    if (ClutterSystem.instance != null)
                    {
                        ClutterSystem.instance.enabled = false;
                        ClutterSystem.instance.ClearAll();
                        clutterHidden = true;
                    }
                    yield return ctx.MoveToOpenGround(3f, IsFlat);
                    if (!ctx.LastOpenGround.Found) yield break;
                    Player.m_localPlayer.SetGhostMode(false);
                }
                ctx.Log(inMine ? "context: infested mine" : "context: open ground");

                Player player = Player.m_localPlayer;
                // Out of the frame: the whole visual (body and equipment) is switched off, restored at the end.
                visual = player.m_visual;
                if (visual != null)
                {
                    visualWasActive = visual.activeSelf;
                    visual.SetActive(false);
                }

                // Templates: green, built disabled, without ZNetView.
                bool ready = WormEffects.EnsureTemplates(out GameObject hit, out GameObject death, out string hitSource, out string deathSource);
                ctx.Assert("templates_built", ready, true, $"hit={hitSource} death={deathSource}");
                if (!ready) yield break;
                ctx.Assert("hit_from_seeker_hurt", hitSource == VanillaNames.FxSeekerHurt, VanillaNames.FxSeekerHurt, hitSource);
                ctx.Assert("death_from_seeker_death", deathSource == VanillaNames.FxSeekerDeath, VanillaNames.FxSeekerDeath, deathSource);
                ctx.Assert("hit_template_disabled_no_znetview", !hit.activeSelf && hit.GetComponentsInChildren<ZNetView>(true).Length == 0, true,
                    $"active={hit.activeSelf} znetviews={hit.GetComponentsInChildren<ZNetView>(true).Length}");
                ctx.Assert("death_template_disabled_no_znetview", !death.activeSelf && death.GetComponentsInChildren<ZNetView>(true).Length == 0, true,
                    $"active={death.activeSelf} znetviews={death.GetComponentsInChildren<ZNetView>(true).Length}");
                ctx.Assert("death_recolored", WormEffects.DeathRecolored > 0, "> 0 systems", WormEffects.DeathRecolored);
                ctx.Assert("death_pieces_removed", death.GetComponentsInChildren<ZSFX>(true).Length == 0 && FindChild(death, "Carapace") == null, true,
                    $"zsfx={death.GetComponentsInChildren<ZSFX>(true).Length} carapace={(FindChild(death, "Carapace") != null)}");
                // The vanilla effect that real seekers play must be untouched by our templates (nothing of the ZNetScene is ever modified).
                foreach (string vanillaFx in new[] { VanillaNames.FxSeekerDeath, VanillaNames.FxSeekerHurt })
                {
                    bool intact = WormEffects.VanillaIntact(vanillaFx, out string intactDetail);
                    ctx.Assert("vanilla_prefab_untouched_" + vanillaFx, intact, "same network view, Legs/Wings/Carapace and start colour as before the first template", intactDetail);
                }
                bool counted = WormEffects.VanillaCounts(VanillaNames.FxSeekerDeath, out int views, out int legs, out int wings, out int carapace);
                ctx.Assert("vanilla_death_prefab_keeps_its_pieces", counted && views > 0 && legs > 0 && wings > 0 && carapace > 0, "ZNetView, Legs, Wings and Carapace all present",
                    $"znetviews {views}, Legs {legs}, Wings {wings}, Carapace {carapace}");
                string missing = WormEffects.MissingSounds();
                ctx.Assert("sounds_resolved", missing.Length == 0, "none missing", missing.Length == 0 ? "ok" : missing);

                // A frozen worm on the floor near the player, a camera beside it.
                ChooseSpot(ctx, player, out Vector3 forward, out Vector3 side, out Vector3 spot);
                int slot = ColonyDev.SpawnAt(spot, WormState.Idle, false, out WormColony colony);
                if (!ctx.Assert("worm_spawned", slot >= 0, true, slot)) yield break;
                ctx.Console("mw_freeze on");
                ushort generation = colony.Store.generation[slot];
                yield return ctx.WaitFrames(3); // the proxy attaches in the next LateUpdate

                Vector3 wormPos = colony.Store.pos[slot];
                PlaceCamera(ctx, wormPos + Vector3.up * 0.1f, side, CameraDistance, CameraHeight);
                yield return ctx.WaitFrames(5);
                colony.ResetCounters();

                float health = MWConfig.Worm.Health.Value;

                // Non lethal hit.
                yield return Burst(ctx, "hit", colony, slot, MakeHit(player, wormPos, forward, health * 0.3f), MaxLiveSizeHit, MaxLiveCountHit, MaxReachHit);
                ctx.Assert("hit_received_once", colony.HitsReceived == 1 && colony.Store.IsAlive(slot, generation), "1 hit, worm alive",
                    $"hits={colony.HitsReceived} alive={colony.Store.IsAlive(slot, generation)}");

                yield return ctx.Wait(SettleTime);

                // Lethal hit.
                yield return Burst(ctx, "death", colony, slot, MakeHit(player, wormPos, forward, health * 2f), MaxLiveSizeDeath, MaxLiveCountDeath, MaxReachDeath);
                ctx.Assert("worm_died", !colony.Store.IsAlive(slot, generation), true, !colony.Store.IsAlive(slot, generation));

                yield return ctx.Wait(1f);
                ctx.Assert("no_unity_errors", _errors == 0, 0, _errors);
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                DevShots.RestoreUi();
                if (visual != null) visual.SetActive(visualWasActive);
                if (clutterHidden && ClutterSystem.instance != null) ClutterSystem.instance.enabled = true;
                ctx.Console("mw_freeze off");
                ctx.Console("mw_kill");
                DevCamera.Follow();
            }
        }

        /// <summary>
        /// Sends the hit, then captures at ShotTimes after it. At +0.1 s the largest live particle and the live count are measured, at +0.2 s and
        /// +0.5 s the farthest distance from the origin (measures are taken before the capture of the same time).
        /// </summary>
        private static IEnumerator Burst(ScenarioContext ctx, string what, WormColony colony, int slot, HitData hit, float maxSize, int maxCount, float maxReach)
        {
            // The effect is queued by the hit and played by the next Update: wait for it, then measure from where and when it was really spawned
            // (the recorded impact point and instant), not from the call of the hit.
            int before = WormEffects.FxSpawned;
            float asked = Time.realtimeSinceStartup;
            ColonyDev.HitWorm(colony, slot, hit);
            while (WormEffects.FxSpawned == before && Time.realtimeSinceStartup - asked < 2f) yield return null;
            yield return null;      // a lethal hit spawns the death effect in the same Update as the hit effect: take the last one
            GameObject fx = WormEffects.LastFx;
            Vector3 origin = WormEffects.FxSpawned > before ? WormEffects.LastFxPosition : Vector3.zero;
            float t0 = WormEffects.FxSpawned > before ? WormEffects.LastFxRealtime : asked;
            ctx.Log($"{what}: effect spawned {(t0 - asked) * 1000f:0} ms after the hit at {origin}, impact point {hit.m_point}, {(origin - hit.m_point).magnitude:0.000} m apart");
            ctx.Assert($"{what}_effect_at_impact_point", WormEffects.FxSpawned > before && (origin - hit.m_point).magnitude < 0.15f, "spawned within 0.15 m of the impact point", $"{(origin - hit.m_point).magnitude:0.000} m");
            float reach = 0f;

            for (int i = 0; i < ShotTimes.Length; i++)
            {
                float wait = ShotTimes[i] - (Time.realtimeSinceStartup - t0);
                if (wait > 0f) yield return ctx.Wait(wait);
                float at = Time.realtimeSinceStartup - t0;
                if (Mathf.Approximately(ShotTimes[i], 0.1f)) MeasureSizeAndCount(ctx, what, fx, at, maxSize, maxCount);
                if (Mathf.Approximately(ShotTimes[i], 0.2f))
                {
                    reach = Mathf.Max(reach, Reach(fx, origin));
                    LogSystems(ctx, what, fx, origin, at);
                }
                yield return ctx.Shot($"{what}_{ShotTimes[i].ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', '_')}");
            }

            float late = MeasureLate - (Time.realtimeSinceStartup - t0);
            if (late > 0f) yield return ctx.Wait(late);
            reach = Mathf.Max(reach, Reach(fx, origin));
            ctx.Log($"{what}: farthest live particle {reach:0.00} m from the origin (at +0.2 s and +{Time.realtimeSinceStartup - t0:0.00} s)");
            ctx.Assert(what + "_max_reach", reach <= maxReach, $"<= {maxReach} m", reach);
        }

        // ---------------------------------------------------------------- placement

        /// <summary>
        /// A spot for the worm about SpawnDistance from the player, the direction nearest to the player's facing among SpotDirections where there is a
        /// floor, no wall between the player and the worm, and a free camera position on one side (open air at CameraDistance, CameraHeight).
        /// Falls back to the player's facing with a log line.
        /// </summary>
        private static void ChooseSpot(ScenarioContext ctx, Player player, out Vector3 forward, out Vector3 side, out Vector3 spot)
        {
            Vector3 face = player.transform.forward;
            face.y = 0f;
            face = face.sqrMagnitude > 1e-4f ? face.normalized : Vector3.forward;
            int mask = LayerMask.GetMask("terrain", "static_solid", "Default", "Default_small", "piece");
            Vector3 origin = player.transform.position;

            for (int k = 0; k < SpotDirections; k++)
            {
                // 0, +step, -step, +2 steps, ...: the nearest directions to the facing first.
                int step = (k + 1) / 2 * (k % 2 == 1 ? 1 : -1);
                Vector3 dir = Quaternion.Euler(0f, step * 360f / SpotDirections, 0f) * face;
                Vector3 probe = origin + dir * SpawnDistance;
                if (!Physics.Raycast(probe + Vector3.up * 0.5f, Vector3.down, out RaycastHit floor, 1f, mask, QueryTriggerInteraction.Ignore) || floor.normal.y < 0.9f) continue;
                Vector3 target = floor.point + Vector3.up * 0.1f;
                if (Physics.Linecast(origin + Vector3.up * 0.5f, target + Vector3.up * 0.2f, mask, QueryTriggerInteraction.Ignore)) continue;
                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    Vector3 sideDir = Vector3.Cross(Vector3.up, dir) * sign;
                    Vector3 cam = floor.point + sideDir * CameraDistance + Vector3.up * CameraHeight;
                    if (Physics.Linecast(target, cam, mask, QueryTriggerInteraction.Ignore) || Physics.CheckSphere(cam, CameraClearance, mask, QueryTriggerInteraction.Ignore)) continue;
                    forward = dir;
                    side = sideDir;
                    spot = floor.point;
                    ctx.Log($"spot: direction {step * 360f / SpotDirections:0} deg from the facing, worm at {spot}");
                    return;
                }
            }
            ctx.Log("no direction with a free camera position: using the player's facing");
            forward = face;
            side = Vector3.Cross(Vector3.up, face);
            spot = origin + face * SpawnDistance;
        }

        /// <summary>Open ground only: flat around the player's spot (the ground stays within FlatTolerance on 8 points at 1.5 m and at 3 m).</summary>
        private static bool IsFlat(Vector3 spot)
        {
            ZoneSystem zs = ZoneSystem.instance;
            if (zs == null || !zs.GetGroundHeight(spot, out float baseHeight)) return false;
            for (float r = 1.5f; r <= 3.01f; r += 1.5f)
            {
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI * 0.25f;
                    Vector3 p = spot + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    if (!zs.GetGroundHeight(p, out float h) || Mathf.Abs(h - baseHeight) >= FlatTolerance) return false;
                }
            }
            return true;
        }

        /// <summary>Camera <paramref name="horizontal"/> metres from the target along <paramref name="side"/>, <paramref name="height"/> above the floor there, raised by steps while the view is blocked.</summary>
        private static void PlaceCamera(ScenarioContext ctx, Vector3 target, Vector3 side, float horizontal, float height)
        {
            Vector3 pos = target + side * horizontal;
            int mask = LayerMask.GetMask("terrain", "static_solid", "Default", "Default_small", "piece");
            float ground = Physics.Raycast(pos + Vector3.up * 1f, Vector3.down, out RaycastHit floor, 3f, mask, QueryTriggerInteraction.Ignore) ? floor.point.y : target.y - 0.1f;
            pos.y = Mathf.Max(ground, target.y - 0.1f) + height;
            int raises = 0;
            while (raises < MaxRaises && Physics.Linecast(pos, target, mask, QueryTriggerInteraction.Ignore))
            {
                pos.y += RaiseStep;
                raises++;
            }
            if (raises > 0) ctx.Log($"camera raised {raises * RaiseStep:0.0} m to clear the view{(raises == MaxRaises ? " (still blocked)" : string.Empty)}");
            DevCamera.Free(pos, target);
        }

        // ---------------------------------------------------------------- measures

        private static bool IsDecal(ParticleSystem ps) =>
            ps.name.IndexOf("decal", StringComparison.OrdinalIgnoreCase) >= 0 || ps.name.StartsWith("blob_splat", StringComparison.Ordinal);

        /// <summary>Largest live particle (ground decal excluded) and live count of the effect instance.</summary>
        private static void MeasureSizeAndCount(ScenarioContext ctx, string what, GameObject fx, float at, float maxSize, int maxCount)
        {
            if (!ctx.Assert(what + "_fx_instance", fx != null, true, fx != null)) return;
            float biggest = 0f;
            int alive = 0;
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                alive += ps.particleCount;
                if (IsDecal(ps)) continue;
                var buffer = new ParticleSystem.Particle[Mathf.Max(1, ps.particleCount)];
                int n = ps.GetParticles(buffer);
                for (int i = 0; i < n; i++) biggest = Mathf.Max(biggest, buffer[i].GetCurrentSize(ps));
            }
            ctx.Log($"{what} particles at +{at:0.00} s: {alive} alive, largest {biggest * 100f:0.0} cm (decals excluded)");
            ctx.Assert(what + "_max_particle_size", biggest < maxSize, $"< {maxSize} m", biggest);
            ctx.Assert(what + "_live_particles", alive <= maxCount, $"<= {maxCount}", alive);
        }

        /// <summary>One log line per particle system of the instance: name, live particles, farthest distance from the origin.</summary>
        private static void LogSystems(ScenarioContext ctx, string what, GameObject fx, Vector3 origin, float at)
        {
            if (fx == null) return;
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                int alive = ps.particleCount;
                ctx.Log($"{what} +{at:0.00} s system {ps.name}: active={ps.gameObject.activeInHierarchy} alive={alive} maxDist={ReachOf(ps, origin):0.00} m lifetime<={ps.main.startLifetime.constantMax:0.00}");
            }
        }

        /// <summary>Distance from <paramref name="origin"/> of the farthest live particle of the effect (world positions).</summary>
        private static float Reach(GameObject fx, Vector3 origin)
        {
            if (fx == null) return 0f;
            float far = 0f;
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true)) far = Mathf.Max(far, ReachOf(ps, origin));
            return far;
        }

        private static float ReachOf(ParticleSystem ps, Vector3 origin)
        {
            float far = 0f;
            var buffer = new ParticleSystem.Particle[Mathf.Max(1, ps.particleCount)];
            int n = ps.GetParticles(buffer);
            bool local = ps.main.simulationSpace == ParticleSystemSimulationSpace.Local;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = local ? ps.transform.TransformPoint(buffer[i].position) : buffer[i].position;
                far = Mathf.Max(far, (p - origin).magnitude);
            }
            return far;
        }

        private static HitData MakeHit(Player player, Vector3 wormPos, Vector3 forward, float slash)
        {
            var hit = new HitData();
            hit.m_damage.m_slash = slash;
            hit.SetAttacker(player);
            hit.m_hitType = HitData.HitType.PlayerHit;
            hit.m_point = wormPos + Vector3.up * 0.1f;
            hit.m_dir = forward;
            return hit;
        }

        private static Transform FindChild(GameObject root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
#endif
