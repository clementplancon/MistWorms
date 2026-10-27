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
        // Approach places (plan 4A-3): evaluations, first place invalid, rescued by a later place, none valid (aims at the target).
        public int ApproachEvaluations { get; private set; }
        public int ApproachFirstInvalid { get; private set; }
        public int ApproachRescued { get; private set; }
        public int ApproachNoPlace { get; private set; }
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
            ApproachEvaluations = ApproachFirstInvalid = ApproachRescued = ApproachNoPlace = 0;
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
            // The damage first, the effect after (and only queued: it is played by the next Update, never inside this call chain).
            bool show = !Disabled && hit.GetTotalDamage() > MinShownDamage;
            Vector3 where = hit.m_point == Vector3.zero ? Store.pos[i] + Vector3.up * 0.1f : hit.m_point;

            if (_nview.IsValid() && _nview.IsOwner())
            {
                ApplyWormHit(i, hit.Clone(), SessionUid);
                if (show) WormEffects.PlayHit(ref _hitFx, where, Time.time);
                return;
            }
            if (!_nview.IsValid())
            {
                if (show) WormEffects.PlayHit(ref _hitFx, where, Time.time);
                return;
            }

            // A peer that does not own the colony: the slot is private to this peer (it is a slot of the mirror), the worm is named by its network id (phase 8).
            // The owner applies the hit and tells the other peers by an event; this peer plays the blood itself, below, and ignores that event.
            ushort id = Store.netId[i];
            if (id != 0)
            {
                var pkg = new ZPackage();
                hit.Serialize(ref pkg);
                _nview.InvokeRPC(MWNames.RpcWormHit, (int)id, pkg);
            }
            if (show) WormEffects.PlayHit(ref _hitFx, where, Time.time);
        }

        private void RPC_WormHit(long sender, int netId, ZPackage pkg)
        {
            try
            {
                if (!_initialized || !_nview.IsValid() || !_nview.IsOwner() || Store == null) return;
                int index = Store.FindByNetId(netId);   // no allocation: an indexed table
                if (index < 0) return;                   // the worm died or was absorbed since the peer saw it
                var hit = new HitData();
                hit.Deserialize(ref pkg);
                ApplyWormHit(index, hit, sender);
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
        private void ApplyWormHit(int i, HitData hit, long playedBy)
        {
            WormStore s = Store;
            if (Disabled || !s.alive[i]) return;
#if MW_DEV
            HitsReceived++;
#endif
            Character attacker = hit.GetAttacker();
            Vector3 pos = s.pos[i];
            // Spec §3.3, §5.1: damage dealt by a player wakes the worm, its group and the sleepers within 5 m, killing blow included; any other attacker, or none
            // (a cobra strike, a creature, a trap), wakes nothing.
            if (attacker is Player) OnPlayerHit(pos, s.nestIndex[i], Time.time);
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
            EmitNetEvent(DeltaEventType.Hit, point, playedBy);   // the other peers near play the blood; the one that landed the hit (playedBy) already did

            s.hp[i] -= damage;
            if (s.hp[i] <= 0f)
            {
#if MW_DEV
                _lastKillCause = $"{(attacker != null ? attacker.name : "no attacker")} (hit type {hit.m_hitType}, {damage:0.#} damage, hp left {s.hp[i]:0.#})";
#endif
                KillWorm(i, true);
                return;
            }

            // A struck active worm turns on its attacker when that is a player or a creature it may hunt (spec §3.3).
            if (attacker != null && (s.state[i] == (byte)WormState.Idle || s.state[i] == (byte)WormState.Gathering))
            {
                if (attacker is Player player)
                {
                    if (!MWConfig.Targets.AttackPlayers.Value || !Targetable(player) || TargetScanner.IsExcluded(player)) return;
                    s.SetPlayerTarget(i, player.GetPlayerID());
                }
                else
                {
                    bool tamed = attacker.IsTamed();
                    if (!(tamed ? MWConfig.Targets.AttackTamed.Value : MWConfig.Targets.AttackCreatures.Value) || attacker.IsDead()) return;
                    if (!tamed && !TargetRules.FactionTargetable(attacker.GetFaction())) return;
                    if (Mass.WritheMassState.TryGet(attacker, out _) || TargetScanner.IsExcluded(attacker)) return;
                    ZDOID id = attacker.GetZDOID();
                    if (id == ZDOID.None) return;
                    s.SetObjectTarget(i, TargetRules.KindOfCharacter(tamed), id.UserID, id.ID);
                }
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
            bool landed = false;
            if (ResolveTarget(i, out TargetView target))
            {
                float h = s.heading[i];
                var forward = new Vector3(Mathf.Sin(h), 0f, Mathf.Cos(h));
                float reach = Mathf.Max(0f, MWConfig.Worm.CobraRange.Value - StrikeRadius + StrikeReachMargin);
                Vector3 center = s.pos[i] + forward * reach + Vector3.up * StrikeHeight;
                Collider collider = target.IsStructure ? target.Collider : target.Character.GetCollider();
                if (collider != null)
                {
                    // The same surface measure as the scan (a concave mesh is measured on its real surface; no contact, no hit).
                    bool contact = TargetScanner.TryClosestOn(collider, center, out Vector3 closest);
#if MW_DEV
                    LastStrikeGap = contact ? (closest - center).magnitude : -1f;
#endif
                    if (contact && (closest - center).sqrMagnitude <= StrikeRadius * StrikeRadius)
                    {
                        Vector3 dir = closest - s.pos[i];
                        dir.y = 0f;
                        dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : forward;
                        // A player's owner applies the bite (durability rule); a creature or a structure takes a plain attackerless hit (decision 20)
                        // after the "last worm hit" registry was written on the peer that owns it (spec §7.4).
#if MW_DEV
                        long vanilla0 = DevMeasureAlloc ? UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() : 0L;
                        long vanillaT0 = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
                        switch (target.Kind)
                        {
                            case TargetKind.Player:
                                landed = WormBite.SendToPlayer(target.Player, closest, dir, ZdoId, i, s.generation[i]);
                                break;
                            case TargetKind.Structure:
                                landed = WormBite.HitStructure(target.Object, closest, dir, ZdoId, i, s.generation[i]);
                                break;
                            default:
                                landed = WormBite.HitCreature(target.Character, closest, dir, ZdoId, i, s.generation[i]);
                                break;
                        }
#if MW_DEV
                        _frameVanillaMs += (System.Diagnostics.Stopwatch.GetTimestamp() - vanillaT0) * 1000f / System.Diagnostics.Stopwatch.Frequency;
                        if (DevMeasureAlloc)
                        {
                            long rise = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - vanilla0;
                            if (rise > 0) DevVanillaBytes += rise;
                            DevVanillaCalls++;
                        }
#endif
                    }
                }
            }
#if MW_DEV
            _frameStrikeBiteMs += (System.Diagnostics.Stopwatch.GetTimestamp() - t1) * 1000f / System.Diagnostics.Stopwatch.Frequency;
            if (landed && !DevMeasureAlloc && Store.targetKind[i] != (byte)TargetKind.Player) Plugin.DevInfo($"[Worm] {i} struck its {(TargetKind)Store.targetKind[i]} target");
            if (landed) StrikesLanded++;
            else StrikesMissed++;
            if (landed && !DevMeasureAlloc) DevLogCreatureState(i, "struck");
#endif
        }

#if MW_DEV
        /// <summary>Dev journal (S12): health and position of a creature target at each landed strike and when the worm drops it.</summary>
        private void DevLogCreatureState(int i, string what)
        {
            if (Store.targetKind[i] != (byte)TargetKind.Creature) return;
            if (!ResolveTarget(i, out TargetView t) || t.Character == null)
            {
                Plugin.DevInfo($"[WormTarget] worm {i} {what}: creature target unresolved (gone or dead)");
                return;
            }
            Plugin.DevInfo($"[WormTarget] worm {i} {what} {t.Character.name}: hp {t.Character.GetHealth():0.00}/{t.Character.GetMaxHealth():0.0} at {t.Character.transform.position} (worm at {Store.pos[i]}, dist {(t.Character.transform.position - Store.pos[i]).magnitude:0.0} m)");
        }

        private void DevLogTargetDrop(int i) => DevLogCreatureState(i, "drops the target:");
#endif
    }
}
