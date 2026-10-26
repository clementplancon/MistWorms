#if MW_DEV
using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Localization;
using MistWorms.Mass;
using MistWorms.Patches;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// Spec §15.5 S03 (plan 4B-1 point 7): the health model of a writhing mass, deterministic (hits injected through Character.Damage with no
    /// attacker, one player alone so both difficulty scales are 1).
    /// A) a mass of 40 (hp 400, named "Writhing Mass (40)"): 55 blunt -> n 35 partial 5; 22 pierce (11 after the resistance) -> 34 / 6;
    ///    30 fire (60 after the weakness; vanilla turns fire into a burning that ticks through ApplyDamage, so the scenario waits for it) -> 28 / 6;
    ///    then 6 x 10 blunt -> 22.
    /// B) knockback: a mass of 200 gets 0.1 of a push force of 100, a mass of 10 all of it; a hit that asks for a stagger staggers nobody.
    /// C) a real sword swing on a mass standing where only the hit sphere is in reach: exactly one ApplyDamage per swing.
    /// D) dissolution: a hit that leaves 4 recreates 4 fleeing worms in the colony and destroys the mass; 6 and 5 do not dissolve.
    /// The structure of the hit zone (no IDestructible of its own, nothing of ours below the mass) and the four patches are checked too.
    /// Parameters: meleeReps (3).
    /// </summary>
    internal sealed class S03_MassDamage : IDevScenario
    {
        private const int StartCount = 40;
        private const float SpawnDistance = 4.5f;     // inside the 5 m of open ground the scenario asks for
        private const float FlatRadius = 6f, FlatTolerance = 0.5f;     // the terrain around the player is level within this over this radius (0.3 found no site in 32000 candidates)
        private const float LevelTolerance = 0.3f;                      // the floor under the mass against the floor under the player
        private const float HipHeight = 0.9f;
        private const float MeleeDistance = 3.2f;      // capsule edge at 2.6 m (outside the sword reach), hit sphere edge at about 2 m (inside)
        private const float SwordReach = 2.4f;         // SwordBronze, dump section 7
        private const float Tolerance = 0.01f;

        public string Name => "S03_MassDamage";

        public IEnumerator Run(ScenarioContext ctx)
        {
            int meleeReps = ctx.ParamInt("meleeReps", 3);
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 60f + meleeReps * 6f;

            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                yield return ctx.WaitFrames(3);

                yield return ctx.MoveToOpenGround(5f, FlatAround);
                ctx.Log($"site flat within {FlatRadius} m (+-{FlatTolerance} m): {FlatAround(ctx.LastOpenGround.Spot)}");
                if (!ctx.LastOpenGround.Found) yield break;

                Player player = Player.m_localPlayer;
                player.SetGodMode(true);
                player.SetGhostMode(false);
                ctx.ClearInventory();
                ctx.Give(VanillaNames.SwordBronze, equip: true);
                yield return ctx.Wait(0.5f);

                S18_GroupCoherence.ClearCreatures(ctx, player);   // goblins of the Plains hit the player, the worms and the masses
                CheckPatches(ctx);
                if (!CheckPreconditions(ctx, player)) yield break;

                yield return Damage(ctx, player);
                yield return Knockback(ctx, player);
                yield return Melee(ctx, player, meleeReps);
                yield return Dissolution(ctx, player);
            }
            finally
            {
                ctx.Console("mw_kill");
                ctx.ClearInventory();
                Player p = Player.m_localPlayer;
                if (p != null) p.SetGodMode(false);
            }
        }

        // ------------------------------------------------------------------ patches and preconditions

        private static void CheckPatches(ScenarioContext ctx)
        {
            Check(ctx, "patch_hover_name", AccessTools.Method(typeof(Character), nameof(Character.GetHoverName)), postfix: true);
            Check(ctx, "patch_stagger", AccessTools.Method(typeof(Character), nameof(Character.Stagger)), postfix: false);
            Check(ctx, "patch_pushback", AccessTools.Method(typeof(Character), "ApplyPushback", new[] { typeof(HitData) }), postfix: false);
            Check(ctx, "patch_damage_fx_postfix", AccessTools.Method(typeof(Character), nameof(Character.Damage)), postfix: true);
            Check(ctx, "patch_apply_damage_postfix", AccessTools.Method(typeof(Character), nameof(Character.ApplyDamage)), postfix: true);
            // The bite prefix of phase 3 shares the method: it must still be there.
            Check(ctx, "patch_apply_damage_bite_prefix", AccessTools.Method(typeof(Character), nameof(Character.ApplyDamage)), postfix: false);
        }

        private static void Check(ScenarioContext ctx, string name, System.Reflection.MethodBase method, bool postfix)
        {
            HarmonyLib.Patches info = method != null ? Harmony.GetPatchInfo(method) : null;
            bool ours = info != null && (postfix ? info.Postfixes : info.Prefixes).Any(p => p.owner == Plugin.ModGuid);
            ctx.Assert(name, ours, $"a {(postfix ? "postfix" : "prefix")} by {Plugin.ModGuid}", ours ? "patched" : method == null ? "method not found" : "not patched");
        }

        /// <summary>The numbers of the spec assume the default resistances, a one player difficulty and no world damage rate.</summary>
        private static bool CheckPreconditions(ScenarioContext ctx, Player player)
        {
            bool prefab = ZNetScene.instance != null && ZNetScene.instance.GetPrefab(MWNames.MassPrefab) != null;
            ctx.Assert("prefab_registered", prefab, MWNames.MassPrefab, prefab ? "registered" : "missing");
            if (!prefab) return false;

            ctx.Assert("config_worm_health", Near(MWConfig.Worm.Health.Value, 10f), 10f, MWConfig.Worm.Health.Value);
            bool resist = MWConfig.Resist.Blunt.Value == HitData.DamageModifier.Normal && MWConfig.Resist.Pierce.Value == HitData.DamageModifier.Resistant
                          && MWConfig.Resist.Fire.Value == HitData.DamageModifier.VeryWeak;
            ctx.Assert("config_default_resistances", resist, "blunt Normal, pierce Resistant, fire VeryWeak",
                $"{MWConfig.Resist.Blunt.Value}, {MWConfig.Resist.Pierce.Value}, {MWConfig.Resist.Fire.Value}");

            float difficulty = Game.instance.GetDifficultyDamageScaleEnemy(player.transform.position);
            float rate = Game.m_playerDamageRate;
            ctx.Assert("solo_damage_scales_are_1", Near(difficulty, 1f) && Near(rate, 1f), "1 and 1", $"{difficulty} and {rate}");
            return true;
        }

        // ------------------------------------------------------------------ A) the health model

        private IEnumerator Damage(ScenarioContext ctx, Player player)
        {
            float perWorm = MWConfig.Worm.Health.Value;
            WritheMassState mass = MassDev.SpawnAt(player.transform.position + Forward(player) * SpawnDistance, StartCount, ZDOID.None, freezeAI: true);
            ctx.Assert("mass_created", mass != null && mass.Valid, "a valid mass", mass != null ? mass.ZdoId.ToString() : "none");
            if (mass == null) yield break;
            yield return ctx.WaitFrames(3);
            Character c = mass.Character;

            // Start state.
            ctx.Assert("start_count", mass.N == StartCount, StartCount, mass.N);
            ctx.Assert("start_hp", Near(mass.Health, StartCount * perWorm) && Near(c.GetMaxHealth(), StartCount * perWorm),
                $"{StartCount * perWorm:0.#} / {StartCount * perWorm:0.#}", $"{mass.Health:0.#} / {c.GetMaxHealth():0.#}");
            string expectedName = MWLocalization.Format(MWNames.MassCountToken, MWNames.MassNameToken, StartCount);
            ctx.Assert("start_name", c.GetHoverName() == expectedName && expectedName.Contains("(40)"), expectedName, c.GetHoverName());
            ctx.Assert("start_level_and_faction", c.GetLevel() == 1 && c.GetFaction() == Character.Faction.PlayerSpawned, "level 1, PlayerSpawned", $"{c.GetLevel()}, {c.GetFaction()}");
            CheckStructure(ctx, mass);

            // 55 blunt (Normal): 400 - 55 = 345.
            c.Damage(NewHit(mass, blunt: 55f));
            yield return ctx.WaitFrames(2);
            AssertMass(ctx, "blunt_55", mass, 35, 5f, StartCount * perWorm - 55f);
            ctx.Assert("name_follows_count", c.GetHoverName().Contains("(35)"), "contains (35)", c.GetHoverName());

            // 22 pierce (Resistant x 0.5) = 11: 334.
            c.Damage(NewHit(mass, pierce: 22f));
            yield return ctx.WaitFrames(2);
            AssertMass(ctx, "pierce_22", mass, 34, 6f, StartCount * perWorm - 66f);

            // 30 fire (VeryWeak x 2) = 60. Vanilla zeroes the fire of the hit and adds it to a burning status effect that ticks through
            // ApplyDamage (Character.cs:2411, SE_Burning), so the total arrives over a few seconds.
            c.Damage(NewHit(mass, fire: 30f));
            bool burning = c.GetSEMan().HaveStatusEffect(SEMan.s_statusEffectBurning);
            ctx.Log($"burning status effect after the fire hit: {burning}");
            yield return ctx.WaitFrames(2);
            yield return ctx.WaitUntil(() => mass == null || !c.GetSEMan().HaveStatusEffect(SEMan.s_statusEffectBurning), 20f, "burning_finished");
            yield return ctx.WaitFrames(2);
            AssertMass(ctx, "fire_30_x2", mass, 28, 6f, StartCount * perWorm - 126f);

            // Six hits of 10 blunt: 274 - 60 = 214 -> 22 worms (partial 6).
            for (int i = 0; i < 6; i++)
            {
                c.Damage(NewHit(mass, blunt: 10f));
                yield return null;
            }
            yield return ctx.WaitFrames(2);
            AssertMass(ctx, "blunt_10_x6", mass, 22, 6f, StartCount * perWorm - 186f);
            ctx.Assert("name_after_losses", c.GetHoverName().Contains("(22)"), "contains (22)", c.GetHoverName());

            // A stored mw_n is what the other peers read.
            ctx.Assert("hit_sphere_follows_count", HitSphereMatches(mass, 22), $"radius {MassMath.HitRadius(MassMath.Radius(22)):0.00}", HitSphereText(mass));
            MassDev.Destroy(mass);
        }

        private static void AssertMass(ScenarioContext ctx, string name, WritheMassState mass, int n, float partial, float health)
        {
            float perWorm = MWConfig.Worm.Health.Value;
            bool ok = mass.N == n && Near(mass.Partial, partial) && Near(mass.Health, health) && Near(mass.Character.GetMaxHealth(), n * perWorm);
            ctx.Assert(name, ok, $"n={n} partial={partial:0.##} hp={health:0.##} max={n * perWorm:0.##}",
                $"n={mass.N} partial={mass.Partial:0.##} hp={mass.Health:0.##} max={mass.Character.GetMaxHealth():0.##}");
        }

        /// <summary>The hit zone has no IDestructible of its own and nothing of ours (WormProxy) hangs below the mass.</summary>
        private static void CheckStructure(ScenarioContext ctx, WritheMassState mass)
        {
            IDestructible[] destructibles = mass.GetComponentsInChildren<IDestructible>(true);
            bool onlyCharacter = destructibles.Length == 1 && ReferenceEquals(destructibles[0], mass.Character);
            ctx.Assert("hit_zone_has_no_destructible", onlyCharacter, "the Character only",
                string.Join(", ", destructibles.Select(d => d.GetType().Name)));
            ctx.Assert("no_worm_proxy_below_mass", mass.GetComponentsInChildren<WormProxy>(true).Length == 0, 0, mass.GetComponentsInChildren<WormProxy>(true).Length);

            bool hasSphere = mass.TryGetHitSphere(out _, out float radius);
            ctx.Assert("hit_zone_exists", hasSphere, "a sphere on the proxy layer", hasSphere ? mass.HitCollider.gameObject.layer + " " + LayerMask.LayerToName(mass.HitCollider.gameObject.layer) : "none");
            if (!hasSphere) return;
            ctx.Assert("hit_zone_layer", mass.HitCollider.gameObject.layer == ProxyLayer.Layer && !mass.HitCollider.isTrigger, $"{ProxyLayer.LayerName}, not a trigger",
                $"{LayerMask.LayerToName(mass.HitCollider.gameObject.layer)}, trigger={mass.HitCollider.isTrigger}");
            ctx.Assert("hit_zone_size", HitSphereMatches(mass, StartCount), $"radius {MassMath.HitRadius(MassMath.Radius(StartCount)):0.00}", HitSphereText(mass));

            CapsuleCollider capsule = mass.GetComponent<CapsuleCollider>();
            float bound = MWConfig.Mass.MaxColliderRadius.Value;
            ctx.Assert("body_collider_bounded", capsule != null && capsule.radius <= bound + 1e-4f, $"<= {bound}", capsule != null ? capsule.radius : -1f);
        }

        private static bool HitSphereMatches(WritheMassState mass, int n)
        {
            if (!mass.TryGetHitSphere(out Vector3 center, out float radius)) return false;
            float r = MassMath.Radius(n);
            float height = center.y - mass.transform.position.y;
            return Near(radius, MassMath.HitRadius(r)) && Near(height, MassMath.HitCenterHeight(r));
        }

        private static string HitSphereText(WritheMassState mass) =>
            mass.TryGetHitSphere(out Vector3 center, out float radius) ? $"radius {radius:0.00}, height {center.y - mass.transform.position.y:0.00}" : "none";

        // ------------------------------------------------------------------ B) knockback and stagger

        private static readonly System.Reflection.FieldInfo PushForceField = AccessTools.Field(typeof(Character), "m_pushForce");

        private IEnumerator Knockback(ScenarioContext ctx, Player player)
        {
            const float push = 100f;
            float min = MWConfig.Mass.KnockbackMinFactor.Value;
            Vector3 at = player.transform.position + Forward(player) * SpawnDistance;

            // A mass of 200: clamp(10 / 200, min, 1).
            WritheMassState big = MassDev.SpawnAt(at, 200, ZDOID.None, freezeAI: true);
            if (big == null) { ctx.Assert("knockback_mass_created", false, "a mass of 200", "none"); yield break; }
            yield return ctx.WaitFrames(3);
            float bigFactor = PushedFactor(big, push);
            float expected = MassMath.KnockbackFactor(200, min);
            ctx.Assert("knockback_200", Math.Abs(bigFactor - expected) <= 0.02f, $"factor {expected:0.###} of the push force", $"{bigFactor:0.###} (scaled push force {MassPatches.LastScaledPushForce:0.##} of {push})");
            ctx.Assert("knockback_200_scaled_force", Near(MassPatches.LastScaledPushForce, push * expected), push * expected, MassPatches.LastScaledPushForce);

            // Stagger: a hit that asks for one (staggerMultiplier >= 100 calls Stagger directly, Character.cs:2261) staggers nobody.
            int before = MassPatches.StaggerBlocked;
            HitData stagger = NewHit(big, blunt: 1f);
            stagger.m_staggerMultiplier = 100f;
            big.Character.Damage(stagger);
            yield return ctx.Wait(0.4f);
            ctx.Assert("stagger_call_cancelled", MassPatches.StaggerBlocked > before, "Stagger blocked at least once", MassPatches.StaggerBlocked - before);
            ctx.Assert("stagger_not_staggering", !big.Character.IsStaggering(), false, big.Character.IsStaggering());
            ctx.Assert("stagger_damage_factor_zero", big.Character.m_staggerDamageFactor <= 0f && !big.Character.m_staggerWhenBlocked, "factor 0, not staggered when blocked",
                $"{big.Character.m_staggerDamageFactor}, {big.Character.m_staggerWhenBlocked}");
            MassDev.Destroy(big);

            // A mass of 10: the whole force.
            WritheMassState small = MassDev.SpawnAt(at, 10, ZDOID.None, freezeAI: true);
            if (small == null) { ctx.Assert("knockback_small_created", false, "a mass of 10", "none"); yield break; }
            yield return ctx.WaitFrames(3);
            float smallFactor = PushedFactor(small, push);
            ctx.Assert("knockback_10", Math.Abs(smallFactor - 1f) <= 0.02f, "factor 1", smallFactor.ToString("0.###"));
            MassDev.Destroy(small);
        }

        /// <summary>Hits the mass with <paramref name="push"/> and returns the push it received as a fraction of what vanilla would have given.</summary>
        private static float PushedFactor(WritheMassState mass, float push)
        {
            Character c = mass.Character;
            HitData hit = NewHit(mass, blunt: 1f);
            hit.m_pushForce = push;
            hit.m_dir = Vector3.forward;
            c.Damage(hit);
            var pushed = (Vector3)PushForceField.GetValue(c);
            float vanilla = push * Mathf.Clamp01(1f + c.GetEquipmentMovementModifier()) / mass.GetComponent<Rigidbody>().mass * 2.5f;   // Character.ApplyPushback
            return vanilla > 1e-6f ? pushed.magnitude / vanilla : -1f;
        }

        // ------------------------------------------------------------------ C) a real swing on the hit zone

        /// <summary>What could explain a swing that found nothing or a dissolution that created nothing.</summary>
        private static string Diagnose(WritheMassState mass, Vector3 spawnedAt)
        {
            if (mass == null) return "mass destroyed";
            var ai = mass.GetComponent<WritheMassAI>();
            int near = 0;
            foreach (Character c in Character.GetAllCharacters())
                if (c != null && !c.IsPlayer() && c != mass.Character && (c.transform.position - mass.transform.position).sqrMagnitude < 15f * 15f) near++;
            return $"mass moved {Vector3.Distance(mass.transform.position, spawnedAt):0.00} m, AI frozen={mass.DevFrozen}, target={(ai != null && ai.Target != null)}, " +
                   $"hit sphere {HitSphereText(mass)}, creatures within 15 m={near}";
        }

        private static readonly System.Reflection.FieldInfo DistantField = AccessTools.Field(typeof(Player), "m_distantTeleport");
        private static readonly System.Reflection.FieldInfo TeleportingField = AccessTools.Field(typeof(Player), "m_teleporting");

        /// <summary>The state of the player and of the rules a swing goes through (Attack.DoMeleeAttack, Character.RPC_Damage), for a swing that finds nothing.</summary>
        private static string PlayerDiagnostics(Player player, WritheMassState mass)
        {
            Animator anim = player.GetComponentInChildren<Animator>();
            ZDOID id = player.GetZDOID();
            GameObject found = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(id) : null;
            return $"; isEnemy={BaseAI.IsEnemy(player, mass.Character)} pvp={player.IsPVPEnabled()} interior={player.InInterior()} faction={player.GetFaction()} group='{player.GetGroup()}' massGroup='{mass.Character.GetGroup()}' " +
                   $"mteleporting={TeleportingField?.GetValue(player)} distantTeleport={DistantField?.GetValue(player)} zdoid={id} findInstanceIsPlayer={found == player.gameObject} " +
                   $"eyeHeight={player.GetEyePoint().y - player.transform.position.y:0.00} crouch={player.IsCrouching()} sitting={player.IsSitting()} onGround={player.IsOnGround()} swimming={player.IsSwimming()} attached={player.IsAttached()} emote={player.InEmote()} " +
                   $"animator={(anim != null ? $"enabled={anim.enabled} culling={anim.cullingMode} speed={anim.speed} stateHash={anim.GetCurrentAnimatorStateInfo(0).shortNameHash} t={anim.GetCurrentAnimatorStateInfo(0).normalizedTime:0.00}" : "none")}";
        }

        /// <summary>True when the terrain within FlatRadius of <paramref name="g"/> (centre and 12 points on two rings) is level with it within FlatTolerance.</summary>
        private static bool FlatAround(Vector3 g)
        {
            int mask = LayerMask.GetMask(VanillaNames.LayerTerrain);      // terrain only: a tree or a rock on a ring is not a slope
            for (int ring = 1; ring <= 2; ring++)
                for (int k = 0; k < 12; k++)
                {
                    float a = k * Mathf.PI * 2f / 12f;
                    Vector3 p = g + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (FlatRadius * ring / 2f);
                    if (!Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, mask, QueryTriggerInteraction.Ignore)) return false;
                    if (Mathf.Abs(hit.point.y - g.y) > FlatTolerance) return false;
                }
            return true;
        }

        /// <summary>
        /// The mass for a swing: spawned MeleeDistance away, kept only if its floor is level with the player's within FlatTolerance and the line from the
        /// player's hip to the centre of its hit sphere is clear; otherwise another of 8 directions is tried (logged). Falls back on the first one.
        /// </summary>
        private static WritheMassState SpawnLevelMass(ScenarioContext ctx, Player player, Vector3 first)
        {
            WritheMassState firstMass = null;
            for (int k = 0; k < 8; k++)
            {
                Vector3 dir = Quaternion.AngleAxis(k * 45f, Vector3.up) * first;
                WritheMassState mass = MassDev.SpawnAt(player.transform.position + dir * MeleeDistance, StartCount, ZDOID.None, freezeAI: true);
                if (mass == null) continue;
                Vector3 hip = player.transform.position + Vector3.up * HipHeight;
                float dy = mass.transform.position.y - player.transform.position.y;
                bool clear = !mass.TryGetHitSphere(out Vector3 centre, out _) ? true : !Physics.Linecast(hip, new Vector3(centre.x, hip.y, centre.z), WormPhysics.SolidMask, QueryTriggerInteraction.Ignore);
                if (Mathf.Abs(dy) <= LevelTolerance && clear)
                {
                    if (firstMass != null) MassDev.Destroy(firstMass);
                    if (k > 0) ctx.Log($"melee site: direction {k * 45} deg, floor {dy:+0.00;-0.00} m, line clear");
                    return mass;
                }
                ctx.Log($"melee site rejected (direction {k * 45} deg): floor {dy:+0.00;-0.00} m from the player, line to the hit sphere clear={clear}");
                if (firstMass == null) firstMass = mass;
                else MassDev.Destroy(mass);
            }
            return firstMass;
        }

        private IEnumerator Melee(ScenarioContext ctx, Player player, int reps)
        {
            // Vanilla logs what a swing finds ("initiating an attack", "added N hits to set", "hit X for ...") when this test is on: see the BepInEx log.
            bool testsBefore = Terminal.m_showTests;
            Terminal.m_showTests = true;
            Terminal.m_testList["damage"] = "";
            int passed = 0;
            for (int rep = 1; rep <= reps; rep++)
            {
                yield return ctx.WaitUntil(() => player == null || !player.InAttack(), 4f);
                Vector3 fwd = Forward(player);
                S18_GroupCoherence.ClearCreatures(ctx, player);   // before the mass exists: it must not be taken for a vanilla creature
                WritheMassState mass = SpawnLevelMass(ctx, player, fwd);
                if (mass == null) { ctx.Assert($"melee_{rep}", false, "a mass", "none"); continue; }
                Vector3 spawnedAt = mass.transform.position;
                yield return ctx.Wait(0.6f);   // the hit sphere reaches the physics scene at the next fixed step

                Vector3 flat = mass.transform.position - player.transform.position;
                flat.y = 0f;
                float capsuleGap = flat.magnitude - mass.GetComponent<CapsuleCollider>().radius;
                int callsBefore = mass.DamageCalls;
                float hpBefore = mass.Health;
                // The previous swing must be over and the stamina full before a new one (a swing refused with started=False was seen on the third).
                bool started = false;
                string error = null;
                for (int attempt = 0; attempt < 3 && !started; attempt++)
                {
                    yield return ctx.WaitUntil(() => player == null || !player.InAttack(), 4f);
                    player.m_stamina = player.GetMaxStamina();
                    yield return ctx.Wait(0.3f);
                    ctx.ResetPlayerState();
                    started = ColonyDev.StartAttack(player, VanillaNames.SwordBronze, mass.transform.position, out _, out error);
                    if (!started) ctx.Log($"melee_{rep}: swing refused (attempt {attempt + 1}), retrying");
                }
                yield return ctx.Wait(1.4f);

                int calls = mass.DamageCalls - callsBefore;
                string aim = "";
                if (mass.TryGetHitSphere(out Vector3 sc, out float sr))
                {
                    Vector3 toSphere = (sc - player.GetEyePoint()).normalized;
                    var sbq = new System.Text.StringBuilder();
                    foreach (RaycastHit h in Physics.RaycastAll(player.GetEyePoint(), toSphere, 4f, ~0, QueryTriggerInteraction.Collide))
                        sbq.Append($" [{h.collider.name}/{LayerMask.LayerToName(h.collider.gameObject.layer)} d={h.distance:0.00} trig={h.collider.isTrigger} en={h.collider.enabled}]");
                    aim = $"; rays:{sbq}; look {player.GetLookDir()} toSphere {toSphere} angle {Vector3.Angle(player.GetLookDir(), toSphere):0.0} deg, sphere distance from eye {Vector3.Distance(player.GetEyePoint(), sc):0.00}, ghost={player.InGhostMode()} god={player.InGodMode()} debugFly={player.IsDebugFlying()} teleporting={player.IsTeleporting()} inAttack={player.InAttack()} pos={player.transform.position}";
                    aim += PlayerDiagnostics(player, mass);
                }
                bool outsideReach = capsuleGap > SwordReach;
                bool ok = started && outsideReach && calls == 1 && mass.N < StartCount && mass.Health < hpBefore;
                ctx.Log($"melee_{rep} diagnostics{aim}");
                ctx.Assert($"melee_{rep}", ok, "attack started, body capsule out of reach, exactly one ApplyDamage, the mass lost worms",
                    $"started={started}{(error != null ? " (" + error + ")" : string.Empty)} capsuleGap={capsuleGap:0.00} (reach {SwordReach}) applyDamageCalls={calls} n={mass.N} hp={mass.Health:0.#} (was {hpBefore:0.#}); {Diagnose(mass, spawnedAt)}{aim}");
                if (ok) passed++;
                MassDev.Destroy(mass);
                yield return ctx.Wait(0.3f);
            }
            Terminal.m_showTests = testsBefore;
            Terminal.m_testList.Remove("damage");
            ctx.Assert("melee_hits", passed == reps, $"{reps}/{reps}", $"{passed}/{reps}");
        }

        // ------------------------------------------------------------------ D) dissolution

        private IEnumerator Dissolution(ScenarioContext ctx, Player player)
        {
            WormColony colony = ColonyDev.FindOrCreateColony(player, out string how);
            ctx.Assert("dissolve_colony", colony != null, "a colony", how);
            if (colony == null) yield break;
            yield return ctx.WaitFrames(3);
            Vector3 at = player.transform.position + Forward(player) * SpawnDistance;
            float perWorm = MWConfig.Worm.Health.Value;

            // Not under the threshold (5): 8 -> 6 -> 5 keep the mass.
            S18_GroupCoherence.ClearCreatures(ctx, player);
            WritheMassState slow = MassDev.SpawnAt(at, 8, colony.ZdoId, freezeAI: true);
            if (slow == null) { ctx.Assert("dissolve_mass_created", false, "a mass of 8", "none"); yield break; }
            Vector3 slowAt = slow.transform.position;
            yield return ctx.WaitFrames(3);
            int aliveBefore = colony.Store.AliveCount;
            slow.Character.Damage(NewHit(slow, blunt: 2f * perWorm));
            yield return ctx.Wait(0.6f);
            slow.Character.Damage(NewHit(slow, blunt: perWorm));
            yield return ctx.Wait(0.6f);
            ctx.Assert("dissolve_not_above_threshold", slow != null && !slow.Dissolving && slow.N == 5 && colony.Store.AliveCount == aliveBefore,
                "n=5, no dissolution, no worm created", slow != null ? $"n={slow.N} dissolving={slow.Dissolving} worms created={colony.Store.AliveCount - aliveBefore}" : "destroyed");
            // 5 -> 4: under the threshold, by a loss.
            if (slow != null)
            {
                slow.Character.Damage(NewHit(slow, blunt: perWorm));
                yield return ctx.WaitFrames(3);
                ctx.Assert("dissolve_after_last_loss", slow.Dissolving && colony.Store.AliveCount == aliveBefore + 4, "dissolving, 4 worms created",
                    $"dissolving={slow.Dissolving} created={colony.Store.AliveCount - aliveBefore}; colony disabled={colony.Disabled} ({colony.DisabledReason}), dissolve lost={colony.DissolveLost}, " +
                    $"mass inside the colony box={WormMath.InsideBox(slow.transform.position, colony.BoxCenter, colony.BoxHalfSize)}; {Diagnose(slow, slowAt)}");
            }
            yield return ctx.WaitUntil(() => slow == null, 3f);
            yield return KillColonyWorms(colony);

            // The case of the plan: a mass of 8 reduced to 4 by one hit of 40.
            WritheMassState mass = MassDev.SpawnAt(at, 8, colony.ZdoId, freezeAI: true);
            if (mass == null) { ctx.Assert("dissolve_mass_created", false, "a mass of 8", "none"); yield break; }
            yield return ctx.WaitFrames(3);
            int fleeingBefore = CountState(colony, WormState.Fleeing);
            aliveBefore = colony.Store.AliveCount;
            Vector3 massPos = mass.transform.position;

            mass.Character.Damage(NewHit(mass, blunt: 4f * perWorm));
            yield return ctx.WaitFrames(3);
            int created = colony.Store.AliveCount - aliveBefore;
            int fleeing = CountState(colony, WormState.Fleeing) - fleeingBefore;
            ctx.Assert("dissolve_four_fleeing", mass != null && mass.N == 4 && mass.Dissolving && created == 4 && fleeing == 4, "n=4, Dissolving, 4 new worms, all Fleeing",
                mass != null ? $"n={mass.N} dissolving={mass.Dissolving} created={created} fleeing={fleeing} stateBits={MassStateCodes.Describe(mass.StateCode)}" : "destroyed too early");
            ctx.Assert("dissolve_worms_around_mass", WormsAround(colony, massPos, 2.5f) >= 4, ">= 4 worms within 2.5 m of the mass", WormsAround(colony, massPos, 2.5f));
            yield return ctx.WaitUntil(() => mass == null, 2f, "dissolve_mass_destroyed");
            yield return KillColonyWorms(colony);
        }

        private static int CountState(WormColony colony, WormState state)
        {
            int n = 0;
            WormStore s = colony.Store;
            for (int i = 0; i < s.HighWater; i++)
                if (s.alive[i] && s.state[i] == (byte)state) n++;
            return n;
        }

        private static int WormsAround(WormColony colony, Vector3 center, float radius)
        {
            int n = 0;
            WormStore s = colony.Store;
            for (int i = 0; i < s.HighWater; i++)
                if (s.alive[i] && (s.pos[i] - center).sqrMagnitude <= radius * radius) n++;
            return n;
        }

        private static IEnumerator KillColonyWorms(WormColony colony)
        {
            colony.KillAll();
            yield return null;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// The direction the masses are put in: the way the player faces if the floor there is level with his feet from 1.5 to 4.5 m (a sword swings at
        /// the height of the player, and a mass standing 1 m higher on a bump is out of its arc: the result of S03 depended on where the previous scenario
        /// left the player turned), otherwise the nearest of 16 directions that is level.
        /// </summary>
        private static Vector3 Forward(Player player)
        {
            Vector3 f = player.transform.forward;
            f.y = 0f;
            f = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            Vector3 origin = player.transform.position;
            for (int k = 0; k < 16; k++)
            {
                int step = (k + 1) / 2 * (k % 2 == 0 ? 1 : -1);
                Vector3 d = Quaternion.AngleAxis(step * 22.5f, Vector3.up) * f;
                if (LevelAlong(origin, d)) return d;
            }
            return f;
        }

        private static bool LevelAlong(Vector3 origin, Vector3 dir)
        {
            int mask = WormPhysics.SolidMask;
            foreach (float dist in new[] { 1.5f, 2.5f, 3.2f, 4.5f })
            {
                Vector3 p = origin + dir * dist;
                if (!Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f, mask, QueryTriggerInteraction.Ignore)) return false;
                if (Mathf.Abs(hit.point.y - origin.y) > 0.25f || hit.normal.y < 0.95f) return false;
            }
            return true;
        }

        /// <summary>A hit with no attacker (vanilla lets it through; a declared but missing attacker would be thrown away, Character.cs:2273).</summary>
        private static HitData NewHit(WritheMassState mass, float blunt = 0f, float pierce = 0f, float fire = 0f)
        {
            var hit = new HitData();
            hit.m_damage.m_blunt = blunt;
            hit.m_damage.m_pierce = pierce;
            hit.m_damage.m_fire = fire;
            hit.m_point = mass.transform.position + Vector3.up * 0.5f;
            hit.m_dir = Vector3.forward;
            return hit;
        }

        private static bool Near(float a, float b) => Math.Abs(a - b) <= Tolerance;
    }
}
#endif
