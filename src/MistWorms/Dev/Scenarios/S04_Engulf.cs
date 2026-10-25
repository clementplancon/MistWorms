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
    /// S04 (spec §15.5, plan 5-1): a mass of 40 engulfs a player who wears the full padded set (quality 1), carries the Wisplight and ten other items, in
    /// god mode off; first in the open, then in a real mine (where only the engulfing, the Infested effect, the bite rate and the destruction order are
    /// checked: the sprint, roll and block need room).
    /// Assertions: Infested active; collisions ignored; speed x0.5 measured over 2 s of forced running (the vanilla controls, through DevInput); health
    /// untouched while an item remains; loss of about n x 2 per second (+-25 %); Wisplight intact while something else remains, then Wisplight, then health;
    /// the floating text; a roll cancels the tick that falls in its invincibility frames and does not free; a raised shield reduces the loss; letting go by
    /// sprint; the effect gone after the detach and after mw_kill.
    /// Detach by sprint (adapted from the plan, which put the player at r + 2.5 m by a teleport): the mass chases at 2.4 m/s, so the gap to r + 1.5 m closes in
    /// 0.42 s, less than Mass.DetachDelay (0.75 s): a teleport never detaches (EngulfMathTests proves it). The test runs the player at the slowed sprint
    /// (about 3.5 m/s) instead: the gap grows by 1.1 m/s and the mass lets go about 3 s later.
    /// The health damage of a worm is lowered during the run (Worm.HealthDamagePerBite = 0.05) so that the health phase can be observed on a 25 HP player.
    /// Parameters: where (both, default, open or mine).
    /// </summary>
    internal sealed class S04_Engulf : IDevScenario
    {
        private const int WormCount = 40;
        private const float TestHealthPerWorm = 0.05f;
        private const float OpenGap = 6f;
        private const float SpeedSeconds = 2f;
        private const float WispLeft = 30f, ItemLeft = 20f;
        private const float RateTicks = 8;

        private static readonly string[] Others =
        {
            "SwordBronze", "MaceBronze", "AxeBronze", "PickaxeBronze", "BowFineWood", "ShieldBronzeBuckler", "KnifeFlint", "SpearBronze", "Iron", "LinenThread",
        };

        public string Name => "S04_Engulf";

        public IEnumerator Run(ScenarioContext ctx)
        {
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 330f;
            string where = ctx.Param("where", "both");
            float savedHealthBite = MWConfig.Worm.HealthDamagePerBite.Value;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                yield return ctx.WaitFrames(3);
                MWConfig.Worm.HealthDamagePerBite.Value = TestHealthPerWorm;
                ctx.Assert("test_health_bite_applied", Mathf.Approximately(MWConfig.Worm.HealthDamagePerBite.Value, TestHealthPerWorm), TestHealthPerWorm, MWConfig.Worm.HealthDamagePerBite.Value);

                if (where != "mine") yield return Context(ctx, false);
                if (where != "open") yield return Context(ctx, true);
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

        // ------------------------------------------------------------------ one context (open ground or mine)

        private static IEnumerator Context(ScenarioContext ctx, bool inMine)
        {
            string tag = inMine ? "mine_" : "open_";
            Player player = Player.m_localPlayer;
            int n = WormCount;
            Vector3 site = default;
            float gap = OpenGap;

            if (inMine)
            {
                player.SetGodMode(true);
                yield return ctx.TpMineRetry();
                if (!ctx.LastTpMine.Ok) yield break;
                yield return ctx.Wait(1f);
                float rMax = MassMath.Radius(WormCount);
                var found = new MineSiteResult();
                yield return MineSite.Find(Player.m_localPlayer.transform.position, 1.5f * rMax + 2.5f, MineSite.AbsoluteMinClearance, found);
                ctx.Assert("mine_site_found", found.Found, "flat floor", found.Found ? found.Floor.ToString() : found.Error);
                if (!found.Found) yield break;
                site = found.Floor;
                if (1.5f * rMax + 0.7f > found.Clearance)
                {
                    n = MineSite.FitCount((found.Clearance - 0.7f) / 1.5f);
                    ctx.Log($"mine: n={WormCount} does not fit a {found.Clearance:0.0} m clearance; using n={n}");
                }
                gap = MassMath.Radius(n) + 1.2f;
            }
            else
            {
                Player.m_localPlayer.SetGhostMode(false);
                yield return ctx.MoveToOpenGround(10f);
                if (!ctx.LastOpenGround.Found) yield break;
            }

            player = Player.m_localPlayer;
            player.SetGhostMode(false);
            player.SetGodMode(false);
            S18_GroupCoherence.ClearCreatures(ctx, player);
            Vector3 fwd = Flat(player.transform.forward, Vector3.forward);
            if (inMine)
            {
                var back = new TeleportOutcome();
                yield return DevWorld.TeleportPlayer(site - fwd * (0.5f * gap) + Vector3.up * 0.3f, Quaternion.LookRotation(fwd, Vector3.up), false, 30f, back);
                if (!back.Ok) { ctx.Log("could not place the player in the mine: " + back.Error); yield break; }
                player = Player.m_localPlayer;
                player.SetGodMode(false);
                yield return ctx.Wait(0.5f);
            }

            ctx.Log($"context: {(inMine ? "infested mine" : "open ground")}, n={n}");
            ItemDrop.ItemData wisp = Equip(ctx, player);
            if (!ctx.Assert(tag + "wisplight_given", wisp != null && wisp.m_shared.m_useDurability, "a Wisplight with durability", wisp == null ? "none" : $"use={wisp.m_shared.m_useDurability} max={wisp.GetMaxDurability()}"))
                yield break;
            player.SetHealth(player.GetMaxHealth());

            // ---- reference speed (open ground only): 2 s of forced running before the mass exists
            float refSpeed = 0f;
            if (!inMine)
            {
                player.SetLookDir(fwd);
                yield return MeasureSpeed(ctx, player, fwd, v => refSpeed = v);
                ctx.Log($"reference sprint speed {refSpeed:0.00} m/s");
                ctx.Assert(tag + "reference_speed", refSpeed > 3f, "> 3 m/s", refSpeed);
                fwd = Flat(player.transform.forward, fwd);
            }

            // ---- the mass
            MassEngulf.ResetCounters();
            WormBiteContext.ResetCounters();
            WritheMassState mass = MassDev.SpawnAt(player.transform.position + fwd * gap, n, ZDOID.None, freezeAI: false);
            if (!ctx.Assert(tag + "mass_created", mass != null && mass.Valid, "a valid mass", mass != null ? mass.ZdoId.ToString() : "none")) yield break;
            WritheMassAI ai = mass.GetComponent<WritheMassAI>();
            ai.DevForcedTarget = player;

            yield return ctx.WaitUntil(() => mass == null || MassStateCodes.Mode(mass.StateCode) == MassStateCodes.Engulf, 25f);
            bool engulfed = mass != null && MassStateCodes.Mode(mass.StateCode) == MassStateCodes.Engulf;
            if (!ctx.Assert(tag + "engulf_entered", engulfed, "state Engulf within 25 s", mass == null ? "mass gone" : MassStateCodes.Describe(mass.StateCode))) yield break;

            yield return ctx.WaitUntil(() => InfestedEffect.IsActive(player), 3f);
            ctx.Assert(tag + "infested_active", InfestedEffect.IsActive(player), true, InfestedEffect.IsActive(player));
            yield return ctx.WaitUntil(() => mass.IgnoredPlayer == player, 2f);
            ctx.Assert(tag + "collisions_ignored", mass.IgnoredPlayer == player, "the player's colliders ignored by the mass", mass.IgnoredPlayer == null ? "none" : $"{mass.IgnoredColliderCount} colliders");

            // ---- bite rate, health untouched, Wisplight intact, text
            float perTick = ExpectedTick(player, n);
            float wispMax = wisp.m_durability;
            float healthBefore = player.GetHealth();
            yield return WaitTicks(ctx, player, 1);
            float d0 = Durability(player, false), t0 = Time.time;
            int texts0 = WormBiteContext.Texts;
            yield return WaitTicks(ctx, player, (int)RateTicks);
            float lost = d0 - Durability(player, false), elapsed = Time.time - t0;
            float rate = lost / Mathf.Max(0.01f, elapsed);
            float expectedRate = perTick / MWConfig.Worm.BiteInterval.Value;
            ctx.Log($"bite rate: {lost:0.#} points in {elapsed:0.00} s = {rate:0.#}/s (expected {expectedRate:0.#}/s), texts {WormBiteContext.Texts - texts0}");
            ctx.Assert(tag + "loss_rate", Mathf.Abs(rate - expectedRate) <= 0.25f * expectedRate, $"{expectedRate:0.#}/s +-25 %", $"{rate:0.#}/s");
            ctx.Assert(tag + "health_untouched", Mathf.Abs(player.GetHealth() - healthBefore) < 0.01f, healthBefore, player.GetHealth());
            ctx.Assert(tag + "wisplight_intact", Mathf.Abs(wisp.m_durability - wispMax) < 0.01f, wispMax, wisp.m_durability);
            ctx.Assert(tag + "text_shown", WormBiteContext.Texts - texts0 >= RateTicks - 1, $">= {RateTicks - 1} texts", WormBiteContext.Texts - texts0);
            float baseLossPerTick = lost / RateTicks;
            yield return ctx.Shot(tag + "engulf");

            if (!inMine)
            {
                // ---- block: a raised shield reduces the loss per tick
                ItemDrop.ItemData shield = ctx.Give("ShieldWood", equip: true);
                if (shield != null)
                {
                    player.SetLookDir(-ai.BiteDir);
                    DevInput.Set(Vector3.zero, false, true);
                    yield return ctx.Wait(0.6f);
                    yield return WaitTicks(ctx, player, 1);
                    float b0 = Durability(player, false);
                    yield return WaitTicks(ctx, player, 6);
                    float blocked = (b0 - Durability(player, false)) / 6f;
                    DevInput.Release();
                    ctx.Log($"block: {blocked:0.#} points per tick against {baseLossPerTick:0.#} without the shield");
                    ctx.Assert(tag + "block_reduces_loss", blocked < 0.9f * baseLossPerTick, $"< {0.9f * baseLossPerTick:0.#} per tick", blocked);
                    player.UnequipItem(shield);
                    player.GetInventory().RemoveItem(shield);
                }
                else ctx.Assert(tag + "block_reduces_loss", false, "ShieldWood given", "unknown prefab");

                // ---- roll: the tick that falls in the invincibility frames is lost, nobody is freed
                bool rolled = false;
                for (int attempt = 0; attempt < 4 && !rolled; attempt++)
                {
                    player.m_stamina = player.GetMaxStamina();
                    float deadline = Time.time + 2f;
                    while (Time.time < deadline && !(ai.NextTickTime - Time.time > 0.08f && ai.NextTickTime - Time.time < 0.2f)) yield return null;
                    float r0 = Durability(player, false);
                    int recv0 = MassEngulf.TicksReceived, inv0 = MassEngulf.TicksInvincible;
                    DevInput.RequestDodge();
                    float end = Time.time + 1.3f;
                    while (Time.time < end) { player.m_stamina = player.GetMaxStamina(); yield return null; }
                    DevInput.Release();
                    int recv = MassEngulf.TicksReceived - recv0, inv = MassEngulf.TicksInvincible - inv0;
                    float rl = r0 - Durability(player, false);
                    ctx.Log($"roll attempt {attempt + 1}: {recv} ticks received, {inv} in the invincibility frames, {rl:0.#} points lost");
                    if (inv < 1) continue;
                    rolled = true;
                    float most = (recv - inv) * perTick * 1.25f + 1f;
                    ctx.Assert(tag + "roll_tick_cancelled", rl <= most, $"<= {most:0.#} (only the {recv - inv} other ticks bite)", rl);
                    ctx.Assert(tag + "roll_does_not_free", MassStateCodes.Mode(mass.StateCode) == MassStateCodes.Engulf && InfestedEffect.IsActive(player),
                        "still Engulf and Infested", $"{MassStateCodes.Describe(mass.StateCode)} infested={InfestedEffect.IsActive(player)}");
                }
                if (!rolled) ctx.Assert(tag + "roll_tick_cancelled", false, "a tick inside the invincibility frames in 4 rolls", "none");
                yield return ctx.Wait(1.5f);   // let the mass catch up

                // ---- speed under Infested, then the sprint that makes the mass let go
                if (MassStateCodes.Mode(mass.StateCode) == MassStateCodes.Engulf)
                {
                    Vector3 run = Flat(ai.BiteDir, fwd);
                    player.SetLookDir(run);
                    float slowed = 0f;
                    float sprintStart = Time.time;
                    yield return MeasureSpeed(ctx, player, run, v => slowed = v);
                    // Vanilla status effects ADD base x modifier to the speed (SEMan.ApplyStatusEffectSpeedMods), so the slowed sprint is
                    // m_runSpeed x SlowFactor, whatever slows the reference run (equipment, ramp, terrain).
                    float factor = Mathf.Clamp(MWConfig.Engulf.SlowFactor.Value, 0.1f, 1f);
                    float expectedSpeed = player.m_runSpeed * factor;
                    float escape = MWConfig.Mass.MoveSpeed.Value * 1.2f;
                    ctx.Log($"sprint under Infested {slowed:0.00} m/s; reference sprint {refSpeed:0.00} m/s; m_runSpeed {player.m_runSpeed:0.00} x {factor:0.00} = {expectedSpeed:0.00}; mass speed {MWConfig.Mass.MoveSpeed.Value:0.0}");
                    ctx.Assert(tag + "speed_slowed", Mathf.Abs(slowed - expectedSpeed) <= 0.12f * expectedSpeed, $"m_runSpeed x SlowFactor = {expectedSpeed:0.00} +-12 %", slowed);
                    ctx.Assert(tag + "speed_can_escape", slowed > escape, $"> Mass.MoveSpeed x 1.2 = {escape:0.00} m/s", slowed);

                    DevInput.Set(new Vector3(0f, 0f, 1f), true, false);
                    float deadline = Time.time + 10f;
                    while (Time.time < deadline && mass != null && MassStateCodes.Mode(mass.StateCode) == MassStateCodes.Engulf)
                    {
                        player.m_stamina = player.GetMaxStamina();
                        yield return null;
                    }
                    DevInput.Release();
                    float sprinted = Time.time - sprintStart;
                    bool let = mass != null && MassStateCodes.Mode(mass.StateCode) != MassStateCodes.Engulf;
                    ctx.Log($"detach by sprint after {sprinted:0.0} s ({ai.LastDetach})");
                    ctx.Assert(tag + "detached_by_sprint", let && ai.LastDetach == DetachReason.TooFar && sprinted < 9f, "Hunt, reason TooFar, within 9 s", $"{(mass == null ? "gone" : MassStateCodes.Describe(mass.StateCode))} {ai.LastDetach} {sprinted:0.0} s");
                    yield return ctx.WaitUntil(() => !InfestedEffect.IsActive(player), 3f);
                    ctx.Assert(tag + "infested_removed", !InfestedEffect.IsActive(player), "effect gone within 3 s", InfestedEffect.IsActive(player) ? "still active" : "gone");
                    yield return ctx.WaitUntil(() => mass.IgnoredPlayer == null, 2f);
                    ctx.Assert(tag + "collisions_restored", mass.IgnoredPlayer == null, "no ignored player", mass.IgnoredPlayer == null ? "none" : "still ignored");
                    yield return ctx.WaitUntil(() => mass != null && MassStateCodes.Mode(mass.StateCode) == MassStateCodes.Engulf, 40f);
                    if (!ctx.Assert(tag + "re_engulfed", mass != null && MassStateCodes.Mode(mass.StateCode) == MassStateCodes.Engulf, "the mass catches up and engulfs again", "state " + (mass == null ? "gone" : MassStateCodes.Describe(mass.StateCode)))) yield break;
                }
            }

            // ---- exhaustion: others first, then the Wisplight, then health
            yield return Exhaustion(ctx, player, wisp, tag);

            // ---- mw_kill releases the player
            ctx.Console("mw_kill");
            yield return ctx.WaitUntil(() => !InfestedEffect.IsActive(player), 3f);
            ctx.Assert(tag + "infested_removed_on_kill", !InfestedEffect.IsActive(player), "effect gone within 3 s of mw_kill", InfestedEffect.IsActive(player) ? "still active" : "gone");
            player.SetGodMode(true);
            player.SetHealth(player.GetMaxHealth());
        }

        private static IEnumerator Exhaustion(ScenarioContext ctx, Player player, ItemDrop.ItemData wisp, string tag)
        {
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                if (!item.m_shared.m_useDurability) continue;
                item.m_durability = item == wisp ? WispLeft : ItemLeft;
            }
            float health0 = player.GetHealth();
            bool wispBeforeOthers = false, healthBeforeWisp = false, sawWisp = false, sawHealth = false;
            float firstHealthDrop = 0f;
            float deadline = Time.time + 45f;
            float lastHealth = health0;
            while (Time.time < deadline)
            {
                player.m_stamina = player.GetMaxStamina();
                float others = Durability(player, true), w = wisp.m_durability, h = player.GetHealth();
                if (w < WispLeft - 0.01f) { sawWisp = true; if (others > 0.01f) wispBeforeOthers = true; }
                if (h < lastHealth - 0.01f)
                {
                    if (!sawHealth) firstHealthDrop = lastHealth - h;
                    sawHealth = true;
                    if (w > 0.01f) healthBeforeWisp = true;
                }
                lastHealth = h;
                if (health0 - h >= 6f) break;
                if (player.IsDead()) break;
                yield return null;
            }
            float tickHealth = WormCount * TestHealthPerWorm;
            ctx.Log($"exhaustion: wisplight {wisp.m_durability:0.#}/{WispLeft}, others {Durability(player, true):0.#}, health {health0:0.#} -> {player.GetHealth():0.#}, first health loss {firstHealthDrop:0.##}");
            ctx.Assert(tag + "others_eaten_first", !wispBeforeOthers, "Wisplight untouched while another item has durability", wispBeforeOthers ? "bitten too early" : "ok");
            ctx.Assert(tag + "wisplight_then_health", sawWisp && wisp.m_durability <= 0.01f, "Wisplight eaten to 0", wisp.m_durability);
            ctx.Assert(tag + "health_only_after_wisplight", sawHealth && !healthBeforeWisp, "health drops only once the Wisplight is gone", sawHealth ? (healthBeforeWisp ? "too early" : "ok") : "never dropped");
            ctx.Assert(tag + "health_bite_prorated", !sawHealth || firstHealthDrop <= tickHealth * 1.3f + 0.1f, $"<= one tick of health damage ({tickHealth:0.##})", firstHealthDrop);
            ctx.Assert(tag + "player_alive", !player.IsDead(), true, !player.IsDead());
        }

        // ------------------------------------------------------------------ helpers

        private static ItemDrop.ItemData Equip(ScenarioContext ctx, Player player)
        {
            ctx.ClearInventory();
            ctx.Give(VanillaNames.HelmetPadded, equip: true);
            ctx.Give(VanillaNames.ArmorPaddedCuirass, equip: true);
            ctx.Give(VanillaNames.ArmorPaddedGreaves, equip: true);
            ItemDrop.ItemData wisp = ctx.Give(VanillaNames.Demister, equip: true);
            foreach (string prefab in Others) ctx.Give(prefab, equip: false);
            return wisp;
        }

        private static float ExpectedTick(Player player, int n)
        {
            float scale = Game.instance.GetDifficultyDamageScalePlayer(player.transform.position) * Game.m_enemyDamageRate;
            return EngulfMath.TickDurability(n, MWConfig.Worm.DurabilityPerBite.Value, scale, MWConfig.Engulf.ScaleWithWorldModifiers.Value);
        }

        /// <summary>Durability of the items that can lose some; <paramref name="withoutWisplight"/> leaves the Wisplight out.</summary>
        private static float Durability(Player player, bool withoutWisplight)
        {
            float sum = 0f;
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                if (!item.m_shared.m_useDurability) continue;
                if (withoutWisplight && Wisplight.Is(item)) continue;
                sum += item.m_durability;
            }
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

        /// <summary>Average horizontal speed over <see cref="SpeedSeconds"/> of forced running along <paramref name="dir"/> (after a 0.7 s ramp).</summary>
        private static IEnumerator MeasureSpeed(ScenarioContext ctx, Player player, Vector3 dir, System.Action<float> result)
        {
            player.SetLookDir(dir);
            DevInput.Set(new Vector3(0f, 0f, 1f), true, false);
            float ramp = Time.time + 0.7f;
            while (Time.time < ramp) { player.m_stamina = player.GetMaxStamina(); yield return null; }
            Vector3 p0 = player.transform.position;
            float t0 = Time.time, end = t0 + SpeedSeconds;
            while (Time.time < end) { player.m_stamina = player.GetMaxStamina(); yield return null; }
            Vector3 d = player.transform.position - p0;
            d.y = 0f;
            DevInput.Release();
            result(d.magnitude / Mathf.Max(0.01f, Time.time - t0));
        }

        private static Vector3 Flat(Vector3 v, Vector3 fallback)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : fallback;
        }
    }
}
#endif
