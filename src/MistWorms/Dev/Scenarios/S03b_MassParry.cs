#if MW_DEV
using System.Collections;
using MistWorms.Bites;
using MistWorms.Config;
using MistWorms.Mass;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// S03b (spec 15.5, plan 5-2, A11): parry against the bite tick of a mass of 40 that engulfs the player (god mode off, padded set, buckler).
    /// a) simple block (shield raised well before the tick): the loss per tick is lower than without a shield, no parry is counted;
    /// b) perfect parry (shield raised 0.1 to 0.18 s before the tick, under the 0.25 s window of Humanoid.BlockAttack): the tick is cancelled (the wear of the
    ///    items other than the shield, and the health, do not change), the next tick comes 1.0 s +-0.1 after the cancelled one, and the mass is never staggered.
    /// The shield itself wears under any block (vanilla), so it is left out of the durability sums. Health damage per worm is lowered as in S04.
    /// </summary>
    internal sealed class S03b_MassParry : IDevScenario
    {
        private const int WormCount = 40;
        private const string Buckler = "ShieldBronzeBuckler";
        private const float TestHealthPerWorm = 0.05f;

        public string Name => "S03b_MassParry";

        public IEnumerator Run(ScenarioContext ctx)
        {
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 200f;
            float savedHealthBite = MWConfig.Worm.HealthDamagePerBite.Value;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                yield return ctx.WaitFrames(3);
                MWConfig.Worm.HealthDamagePerBite.Value = TestHealthPerWorm;
                yield return ctx.MoveToOpenGround(10f);
                if (!ctx.LastOpenGround.Found) yield break;

                Player player = Player.m_localPlayer;
                player.SetGhostMode(false);
                player.SetGodMode(false);
                S18_GroupCoherence.ClearCreatures(ctx, player);
                ctx.ClearInventory();
                ctx.Give(VanillaNames.HelmetPadded, equip: true);
                ctx.Give(VanillaNames.ArmorPaddedCuirass, equip: true);
                ctx.Give(VanillaNames.ArmorPaddedGreaves, equip: true);
                ItemDrop.ItemData shield = ctx.Give(Buckler, equip: true);
                if (!ctx.Assert("shield_equipped", shield != null && shield.m_equipped && shield.m_shared.m_timedBlockBonus > 1f, "a shield with a timed block bonus",
                        shield == null ? "unknown prefab" : $"equipped={shield.m_equipped} timedBlockBonus={shield.m_shared.m_timedBlockBonus}")) yield break;
                player.SetHealth(player.GetMaxHealth());

                Vector3 fwd = Flat(player.transform.forward);
                MassEngulf.ResetCounters();
                WormBiteContext.ResetCounters();
                WritheMassState mass = MassDev.SpawnAt(player.transform.position + fwd * 6f, WormCount, ZDOID.None, freezeAI: false);
                if (!ctx.Assert("mass_created", mass != null && mass.Valid, "a valid mass", mass != null ? mass.ZdoId.ToString() : "none")) yield break;
                WritheMassAI ai = mass.GetComponent<WritheMassAI>();
                ai.DevForcedTarget = player;
                yield return ctx.WaitUntil(() => mass == null || MassStateCodes.Mode(mass.StateCode) == MassStateCodes.Engulf, 25f);
                if (!ctx.Assert("engulf_entered", mass != null && MassStateCodes.Mode(mass.StateCode) == MassStateCodes.Engulf, "state Engulf", "no")) yield break;
                player.SetLookDir(-ai.BiteDir);

                // ---- reference: no shield raised
                yield return WaitTicks(ctx, player, 1);
                float d0 = Durability(player, shield);
                yield return WaitTicks(ctx, player, 5);
                float baseLoss = (d0 - Durability(player, shield)) / 5f;
                ctx.Log($"without a raised shield: {baseLoss:0.#} points per tick");

                // ---- a) simple block
                int parries0 = MassEngulf.Parries;
                DevInput.Set(Vector3.zero, false, true);
                yield return ctx.Wait(0.7f);                       // the block is well past the 0.25 s window
                yield return WaitTicks(ctx, player, 1);
                float b0 = Durability(player, shield);
                yield return WaitTicks(ctx, player, 5);
                float blocked = (b0 - Durability(player, shield)) / 5f;
                DevInput.Release();
                ctx.Log($"simple block: {blocked:0.#} points per tick");
                ctx.Assert("simple_block_reduces_loss", blocked < 0.9f * baseLoss && baseLoss > 1f, $"< {0.9f * baseLoss:0.#} per tick", blocked);
                ctx.Assert("simple_block_is_not_a_parry", MassEngulf.Parries == parries0, "no parry counted", MassEngulf.Parries - parries0);
                yield return ctx.Wait(0.5f);                       // block released: the timer restarts at the next raise

                // ---- b) perfect parry
                bool parried = false;
                for (int attempt = 0; attempt < 5 && !parried; attempt++)
                {
                    player.m_stamina = player.GetMaxStamina();
                    player.SetLookDir(-ai.BiteDir);
                    float deadline = Time.time + 3f;
                    while (Time.time < deadline && !(ai.NextTickTime - Time.time > 0.10f && ai.NextTickTime - Time.time < 0.18f)) yield return null;

                    float dur0 = Durability(player, shield), health0 = player.GetHealth();
                    int recv0 = MassEngulf.TicksReceived, par0 = MassEngulf.Parries;
                    bool staggered = false;
                    DevInput.Set(Vector3.zero, false, true);
                    float hold = Time.time + 0.45f;
                    float tParried = -1f, tNext = -1f;
                    float end = Time.time + 2.6f;
                    while (Time.time < end)
                    {
                        player.m_stamina = player.GetMaxStamina();
                        if (Time.time > hold) DevInput.Release();
                        if (mass.Character != null && mass.Character.IsStaggering()) staggered = true;
                        if (tParried < 0f && MassEngulf.Parries > par0 && MassEngulf.TicksReceived > recv0) tParried = MassEngulf.LastTickTime;
                        else if (tParried >= 0f && tNext < 0f && MassEngulf.TicksReceived > recv0 + 1) tNext = MassEngulf.LastTickTime;
                        if (tNext >= 0f) break;
                        yield return null;
                    }
                    DevInput.Release();
                    ctx.Log($"parry attempt {attempt + 1}: parries {MassEngulf.Parries - par0}, ticks {MassEngulf.TicksReceived - recv0}, parried tick at {tParried:0.00}, next at {tNext:0.00}");
                    if (tParried < 0f) { yield return ctx.Wait(0.6f); continue; }
                    parried = true;

                    // The durability is read at the next tick's arrival: only the cancelled tick must have cost nothing, so compare with the tick before the next one.
                    ctx.Assert("parry_tick_cancelled", MassEngulf.Parries - par0 >= 1, ">= 1 parry counted", MassEngulf.Parries - par0);
                    float gap = tNext - tParried;
                    ctx.Assert("parry_delays_next_tick", tNext >= 0f && Mathf.Abs(gap - 1.0f) <= 0.1f, "1.0 s +-0.1 after the cancelled tick", tNext >= 0f ? gap.ToString("0.00") : "no next tick");
                    ctx.Assert("parry_no_stagger", !staggered, "the mass is never staggered", staggered);
                    ctx.Assert("parry_health_untouched", Mathf.Abs(player.GetHealth() - health0) < 0.01f, health0, player.GetHealth());
                    float lostBetween = dur0 - Durability(player, shield);
                    // Between the start of the attempt and the next tick the only tick is the cancelled one (the next one has just landed: at most one tick of loss).
                    ctx.Assert("parry_no_wear", lostBetween <= ExpectedTick(player) * 1.25f + 0.5f, $"at most the next tick ({ExpectedTick(player):0.#}), none for the parried one", lostBetween);
                }
                if (!parried) ctx.Assert("parry_tick_cancelled", false, "a perfect parry in 5 attempts", "none");
            }
            finally
            {
                DevInput.Release();
                MWConfig.Worm.HealthDamagePerBite.Value = savedHealthBite;
                ctx.Console("mw_kill");
                ctx.ClearInventory();
                Player p = Player.m_localPlayer;
                if (p != null)
                {
                    p.SetGodMode(true);
                    p.SetHealth(p.GetMaxHealth());
                }
            }
        }

        private static float ExpectedTick(Player player)
        {
            float scale = Game.instance.GetDifficultyDamageScalePlayer(player.transform.position) * Game.m_enemyDamageRate;
            return EngulfMath.TickDurability(WormCount, MWConfig.Worm.DurabilityPerBite.Value, scale, MWConfig.Engulf.ScaleWithWorldModifiers.Value);
        }

        private static float Durability(Player player, ItemDrop.ItemData except)
        {
            float sum = 0f;
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
                if (item != except && item.m_shared.m_useDurability) sum += item.m_durability;
            return sum;
        }

        private static IEnumerator WaitTicks(ScenarioContext ctx, Player player, int count)
        {
            int target = MassEngulf.TicksReceived + count;
            float deadline = Time.time + count * MWConfig.Worm.BiteInterval.Value * 3f + 4f;
            while (MassEngulf.TicksReceived < target && Time.time < deadline)
            {
                player.m_stamina = player.GetMaxStamina();
                yield return null;
            }
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward;
        }
    }
}
#endif
