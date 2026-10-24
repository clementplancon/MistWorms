using System;
using MistWorms.Bites;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>
    /// Hits received by the virtual worms and the cobra strike they deal (spec §3.1, §3.2, §6.3, §11.1). Hits arrive from a
    /// <see cref="WormProxy"/> (vanilla weapons) or from the dev bridge; they run on the colony owner.
    /// </summary>
    internal sealed partial class WormColony
    {
        // Cobra strike geometry (spec §3.3 point 5 and §11.1): a 0.5 m sphere just in front of the head. Reach follows CobraRange (it was 0.35 m, which missed a standing target while a worm
        // starts striking from 1.2 m of the target surface): see decisions.md 55.
        private const float StrikeReachMargin = 0.1f;   // decisions.md 55: the sphere covers up to CobraRange + 0.1 m from the head
        private const float StrikeHeight = EyeHeight;   // same origin as the Striking trigger in StepHunting (eye = pos + 0.15 m)
        private const float StrikeRadius = 0.5f;
        private const float MinShownDamage = 0.1f;      // vanilla shows no text and no effect below this (see ApplyWormHit)

#if MW_DEV
        // Debug counters (plan B3, B6), read by the scenarios.
        public int HitsReceived { get; private set; }
        public float LastHitDamage { get; private set; }
        public int StrikesLanded { get; private set; }
        public int StrikesMissed { get; private set; }
        // Hunting path requests (plan 4B uses the vanilla pathfinder too): a route is complete when it ends at the goal (1 m horizontally, 1.5 m vertically).
        public int PathRequests { get; private set; }
        public int PathComplete { get; private set; }
        public int PathPartial { get; private set; }
        public int PathFailed { get; private set; }

        private void CountPath(bool ok, Vector3 end, Vector3 goal)
        {
            PathRequests++;
            if (!ok) { PathFailed++; return; }
            float dx = end.x - goal.x, dz = end.z - goal.z;
            if (dx * dx + dz * dz <= 1f && Mathf.Abs(end.y - goal.y) <= 1.5f) PathComplete++;
            else PathPartial++;
        }
        /// <summary>Distance from the strike sphere centre to the target collider at the last strike (the sphere reaches when it is <= 0.5).</summary>
        public float LastStrikeGap { get; private set; } = -1f;

        public void ResetCounters()
        {
            HitsReceived = 0;
            LastHitDamage = 0f;
            StrikesLanded = 0;
            StrikesMissed = 0;
            PathRequests = PathComplete = PathPartial = PathFailed = 0;
            DeathCauses.Clear();
            LastStrikeGap = -1f;
        }
#endif

        // ------------------------------------------------------------------ receiving a hit

        /// <summary>
        /// Entry point of a hit on worm <paramref name="i"/> as seen when the proxy was attached. A stale <paramref name="generation"/>
        /// (the slot was reused) is ignored. The owner applies it; any other peer sends it to the owner (spec §10.2).
        /// </summary>
        public void ReceiveWormHit(int i, ushort generation, HitData hit)
        {
            if (!_initialized || Store == null || hit == null) return;
            if (!Store.IsAlive(i, generation)) return;

            // The blood plays here, on the peer that lands the hit, once: the owner applying a hit that came by RPC does not play it
            // again, and in solo (attacker = owner) this local path is the only one. A hit too weak to count shows nothing.
            if (!Disabled && hit.GetTotalDamage() > MinShownDamage)
            {
                Vector3 where = hit.m_point == Vector3.zero ? Store.pos[i] + Vector3.up * 0.1f : hit.m_point;
                WormEffects.PlayHit(ref _hitFx, where, Time.time);
            }

            if (_nview.IsValid() && _nview.IsOwner())
            {
                ApplyWormHit(i, hit.Clone());
                return;
            }
            if (!_nview.IsValid()) return;

            var pkg = new ZPackage();
            hit.Serialize(ref pkg);
            _nview.InvokeRPC(MWNames.RpcWormHit, i, (int)generation, pkg);
        }

        private void RPC_WormHit(long sender, int index, int generation, ZPackage pkg)
        {
            try
            {
                if (!_initialized || !_nview.IsValid() || !_nview.IsOwner()) return;
                if (Store == null || generation < 0 || generation > ushort.MaxValue || !Store.IsAlive(index, (ushort)generation)) return;
                var hit = new HitData();
                hit.Deserialize(ref pkg);
                ApplyWormHit(index, hit);
            }
            catch (Exception e)
            {
                Plugin.LogException("WormColony.RPC_WormHit", e);
            }
        }

        /// <summary>
        /// Applies a hit to a living worm the way <c>Character.RPC_Damage</c> / <c>ApplyDamage</c> treat a non player (Character.cs:2241-2470):
        /// attacker difficulty scaling, then the worm's resistances (Resist.*), then the world damage rates, then the damage text and
        /// the health. Not reproduced, on purpose (decisions.md 48): backstab, stagger bonus, weak spots, dodge, PvP, New Game+ armor,
        /// and fire/poison/spirit as damage over time (they hit at once, like every other type, spec S03).
        /// </summary>
        private void ApplyWormHit(int i, HitData hit)
        {
            WormStore s = Store;
            if (Disabled || !s.alive[i]) return;
#if MW_DEV
            HitsReceived++;
#endif
            Character attacker = hit.GetAttacker();
            Vector3 pos = s.pos[i];
            Game game = Game.instance;
            if (attacker != null && !attacker.IsPlayer() && game != null)
            {
                hit.ApplyModifier(game.GetDifficultyDamageScalePlayer(pos));
                hit.ApplyModifier(Game.m_enemyDamageRate);
            }

            hit.ApplyResistance(MWConfig.BuildDamageModifiers(), out HitData.DamageModifier modifier);

            // As ApplyDamage: the text shows the damage before the player-count and world scaling, the health takes the scaled one.
            float shown = hit.GetTotalDamage();
            if (game != null) hit.ApplyModifier(game.GetDifficultyDamageScaleEnemy(pos));
            hit.ApplyModifier(Game.m_playerDamageRate);
            float damage = hit.GetTotalDamage();
#if MW_DEV
            LastHitDamage = damage;
#endif
            if (damage <= 0.1f) return; // vanilla: no text, no health change

            Vector3 point = hit.m_point == Vector3.zero ? pos + Vector3.up * 0.1f : hit.m_point;
            if (DamageText.instance != null) DamageText.instance.ShowText(modifier, point, shown, false);

            s.hp[i] -= damage;
            if (s.hp[i] <= 0f)
            {
#if MW_DEV
                _lastKillCause = $"{(attacker != null ? attacker.name : "no attacker")} (hit type {hit.m_hitType}, {damage:0.#} damage, hp left {s.hp[i]:0.#})";
#endif
                KillWorm(i, true);
                return;
            }

            // A struck active worm turns on its attacker when that is a player it may hunt (spec §3.3).
            if (attacker is Player player && MWConfig.Targets.AttackPlayers.Value && Targetable(player)
                && (s.state[i] == (byte)WormState.Idle || s.state[i] == (byte)WormState.Gathering))
            {
                s.targetKind[i] = (byte)TargetKind.Player;
                s.targetId[i] = player.GetPlayerID();
                s.lastSeen[i] = Time.time;
                SetState(i, WormState.Hunting, Time.time);
            }
        }

        // ------------------------------------------------------------------ the cobra strike

        /// <summary>
        /// The strike lands now (spec §3.3 point 5, §11.1): a 0.5 m sphere in front of the head touches the target when its collider is
        /// within 0.5 m of the sphere centre; a target that stepped back during the windup is missed. A hit on a player is routed to the
        /// peer that owns the player, where the vanilla damage runs as a durability bite (see <see cref="WormBite"/>).
        /// </summary>
        private void OnStrike(int i, float now)
        {
            WormStore s = Store;
#if MW_DEV
            _frameStrikes++;
            if (s_sessionStrikes++ == 0) _frameFirstSessionStrike = true;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            WormEffects.PlayStrike(ref _strikeFx, s.pos[i] + Vector3.up * StrikeHeight, now);   // the lunge, hit or miss
#if MW_DEV
            long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
            _frameStrikeFxMs += (t1 - t0) * 1000f / System.Diagnostics.Stopwatch.Frequency;
#endif
            Player target = (TargetKind)s.targetKind[i] == TargetKind.Player ? FindPlayer(s.targetId[i]) : null;
            // Phase 6: other target kinds (tamed animals, creatures through WormBite.HitCreature, structures).

            bool landed = false;
            if (target != null && Targetable(target))
            {
                float h = s.heading[i];
                var forward = new Vector3(Mathf.Sin(h), 0f, Mathf.Cos(h));
                float reach = Mathf.Max(0f, MWConfig.Worm.CobraRange.Value - StrikeRadius + StrikeReachMargin);
                Vector3 center = s.pos[i] + forward * reach + Vector3.up * StrikeHeight;
                CapsuleCollider collider = target.GetCollider();
                if (collider != null)
                {
                    Vector3 closest = collider.ClosestPoint(center);
#if MW_DEV
                    LastStrikeGap = (closest - center).magnitude;
#endif
                    if ((closest - center).sqrMagnitude <= StrikeRadius * StrikeRadius)
                    {
                        Vector3 dir = closest - s.pos[i];
                        dir.y = 0f;
                        dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : forward;
                        landed = WormBite.SendToPlayer(target, closest, dir);
                    }
                }
            }
#if MW_DEV
            _frameStrikeBiteMs += (System.Diagnostics.Stopwatch.GetTimestamp() - t1) * 1000f / System.Diagnostics.Stopwatch.Frequency;
            if (landed) StrikesLanded++;
            else StrikesMissed++;
#endif
        }
    }
}
