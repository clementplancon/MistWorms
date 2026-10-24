#if MW_DEV
using System.Collections;
using HarmonyLib;
using MistWorms.Bites;
using MistWorms.Colony;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// Phase 3 scenario (not in the §15.5 table): the cobra strike of a hunting worm on a player who is not in god mode.
    /// a) full padded set worn plus two spare items: strikes only wear items (total loss = strikes x Worm.CobraDurability), health untouched;
    /// b) nothing left to eat: strikes hurt (health damage counted by the patch); c) a player who steps back during the windup is missed.
    /// Also checks that the three damage patches are applied. The worm is a lone Hunting worm 3 m away; the player stands still.
    /// </summary>
    internal sealed class P3_Cobra : IDevScenario
    {
        private const float HuntDistance = 3f;

        public string Name => "P3_Cobra";

        public IEnumerator Run(ScenarioContext ctx)
        {
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 200f;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                CheckPatches(ctx);

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                yield return ctx.WaitFrames(3);

                yield return ctx.MoveToOpenGround(5f);
                if (!ctx.LastOpenGround.Found) yield break;

                Player player = Player.m_localPlayer;
                player.SetGhostMode(false);
                player.SetGodMode(false);
                yield return Padded(ctx, player);
                yield return Bare(ctx, player);
                yield return Dodge(ctx, player);
            }
            finally
            {
                ctx.Console("mw_kill");
                ctx.ClearInventory();
                if (Player.m_localPlayer != null)
                {
                    Player.m_localPlayer.SetGodMode(true);
                    Player.m_localPlayer.SetHealth(Player.m_localPlayer.GetMaxHealth());
                }
            }
        }

        private static void CheckPatches(ScenarioContext ctx)
        {
            Check(ctx, "patch_apply_damage", AccessTools.Method(typeof(Character), nameof(Character.ApplyDamage)));
            Check(ctx, "patch_apply_armor", AccessTools.Method(typeof(HitData), nameof(HitData.ApplyArmor)));
            Check(ctx, "patch_armor_durability", AccessTools.Method(typeof(Player), "DamageArmorDurability"));
        }

        private static void Check(ScenarioContext ctx, string name, System.Reflection.MethodBase method)
        {
            HarmonyLib.Patches info = method != null ? Harmony.GetPatchInfo(method) : null;
            bool ours = info != null && info.Owners.Contains(Plugin.ModGuid);
            ctx.Assert(name, ours, "patched by " + Plugin.ModGuid, ours ? "patched" : "not patched");
        }

        // ------------------------------------------------------------------ a) full padded set

        private static IEnumerator Padded(ScenarioContext ctx, Player player)
        {
            ctx.ClearInventory();
            ctx.Give(VanillaNames.HelmetPadded, equip: true);
            ctx.Give(VanillaNames.ArmorPaddedCuirass, equip: true);
            ctx.Give(VanillaNames.ArmorPaddedGreaves, equip: true);
            ctx.Give(VanillaNames.SwordBronze, equip: false);
            ctx.Give(VanillaNames.MaceBronze, equip: false);
            yield return ctx.Wait(1f);
            player.SetHealth(player.GetMaxHealth());

            float durabilityBefore = Durability(player);
            float healthBefore = player.GetHealth();
            WormBiteContext.ResetCounters();

            WormColony colony = null;
            yield return Hunter(ctx, player, c => colony = c);
            if (colony == null) yield break;
            yield return ctx.Wait(12f);

            int landed = colony.StrikesLanded;
            float lost = durabilityBefore - Durability(player);
            float expected = landed * MWConfig.Worm.CobraDurability.Value;
            ctx.Log($"padded set: {landed} landed, {colony.StrikesMissed} missed, last gap {colony.LastStrikeGap:0.00} m (reach when <= 0.5), bites {WormBiteContext.Bites}, " +
                    $"durability lost {lost:0.##}, health {healthBefore:0.#} -> {player.GetHealth():0.#}");
            ctx.Assert("padded_strikes_landed", landed >= 3, ">= 3", landed);
            ctx.Assert("padded_health_untouched", WormBiteContext.HealthBites == 0 && player.GetHealth() >= Mathf.Min(healthBefore, player.GetMaxHealth()) - 0.01f,
                $"no health bite, health >= min(start {healthBefore:0.#}, current max)", $"healthBites={WormBiteContext.HealthBites} health={player.GetHealth():0.#}");
            ctx.Assert("padded_durability_loss", Mathf.Abs(lost - expected) <= 0.01f, $"{expected:0.##} (strikes x CobraDurability)", $"{lost:0.##}");
            ctx.Console("mw_kill");
            yield return ctx.WaitFrames(3);
        }

        // ------------------------------------------------------------------ b) nothing to eat

        private static IEnumerator Bare(ScenarioContext ctx, Player player)
        {
            ctx.ClearInventory();
            yield return ctx.Wait(0.5f);
            player.SetHealth(player.GetMaxHealth());
            float healthBefore = player.GetHealth();
            WormBiteContext.ResetCounters();

            WormColony colony = null;
            yield return Hunter(ctx, player, c => colony = c);
            if (colony == null) yield break;
            yield return ctx.Wait(8f);

            int landed = colony.StrikesLanded;
            float taken = healthBefore - player.GetHealth();
            float perStrike = MWConfig.Worm.CobraCreatureDamage.Value;
            ctx.Log($"bare player: {landed} landed, {colony.StrikesMissed} missed, last gap {colony.LastStrikeGap:0.00} m, health {healthBefore:0.#} -> {player.GetHealth():0.#} " +
                    $"(taken {taken:0.##}), health bites {WormBiteContext.HealthBites} for {WormBiteContext.HealthDamage:0.##}");
            ctx.Assert("bare_strikes_landed", landed >= 2, ">= 2", landed);
            ctx.Assert("bare_health_damage", WormBiteContext.HealthDamage >= landed * perStrike * 0.9f && landed > 0,
                $">= {landed * perStrike * 0.9f:0.##} health damage through the patch", $"{WormBiteContext.HealthDamage:0.##}");
            ctx.Console("mw_kill");
            yield return ctx.WaitFrames(3);
        }

        // ------------------------------------------------------------------ c) dodge

        private static IEnumerator Dodge(ScenarioContext ctx, Player player)
        {
            ctx.ClearInventory();
            player.SetHealth(player.GetMaxHealth());
            WormColony colony = null;
            yield return Hunter(ctx, player, c => colony = c);
            if (colony == null) yield break;

            // Wait for a windup, then step back 3 m away from the worm (a plain move, not a teleport: a teleporting player is untargetable).
            WormStore s = colony.Store;
            int slot = -1;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline && slot < 0)
            {
                for (int i = 0; i < s.HighWater && slot < 0; i++)
                    if (s.alive[i] && s.state[i] == (byte)WormState.Striking && s.subPhase[i] == (byte)StrikePhase.Windup) slot = i;
                if (slot < 0) yield return null;
            }
            if (!ctx.Assert("dodge_windup_seen", slot >= 0, "a worm in Striking/Windup within 10 s", slot >= 0 ? "seen" : "never")) yield break;

            Vector3 away = player.transform.position - s.pos[slot];
            away.y = 0f;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.back;
            Vector3 target = player.transform.position + away * 3f;
            if (Physics.Raycast(target + Vector3.up * 2f, Vector3.down, out RaycastHit ground, 5f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore))
                target.y = ground.point.y + 0.05f;
            player.transform.position = target;
            player.m_body.position = target;

            int landedBefore = colony.StrikesLanded;
            yield return ctx.Wait(1.5f);
            ctx.Log($"dodge: {colony.StrikesLanded - landedBefore} landed, {colony.StrikesMissed} missed, last gap {colony.LastStrikeGap:0.00} m");
            ctx.Assert("dodge_strike_missed", colony.StrikesMissed >= 1 && colony.StrikesLanded == landedBefore,
                "a strike missed, none landed", $"missed={colony.StrikesMissed} landed={colony.StrikesLanded - landedBefore}");
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Starts one Hunting worm 3 m in front of the player and hands back its colony (counters reset).</summary>
        private static IEnumerator Hunter(ScenarioContext ctx, Player player, System.Action<WormColony> done)
        {
            Vector3 forward = player.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            int slot = ColonyDev.SpawnAt(player.transform.position + forward * HuntDistance, WormState.Hunting, false, out WormColony colony);
            if (!ctx.Assert("hunter_spawned", slot >= 0, "a Hunting worm 3 m away", slot >= 0 ? "ok" : "no floor/colony"))
                yield break;
            colony.ResetCounters();
            done(colony);
            yield return null;
        }

        private static float Durability(Player player)
        {
            float sum = 0f;
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
                if (item.m_shared.m_useDurability) sum += item.m_durability;
            return sum;
        }
    }
}
#endif
