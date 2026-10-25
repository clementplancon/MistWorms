using System;
using MistWorms.Bites;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Mass
{
    /// <summary>
    /// The network side of the engulfing (spec §6.1, §10.2, plan 5, A5 and A13). The owner of the mass decides (<see cref="WritheMassAI"/>) and sends two
    /// routed RPCs to the owner of the engulfed player, which applies them:
    /// <list type="bullet">
    /// <item><c>MW_MassBite</c>: one aggregated tick. The target's owner builds one HitData attributed to the mass (a real Character, so the vanilla
    /// block, riposte and dodge frames work) and runs it through the vanilla <c>Damage</c> inside a <see cref="WormBiteContext"/>, exactly like the
    /// cobra: the damage patches then turn it into a durability bite (rest of P to health).</item>
    /// <item><c>MW_Infest</c>: applies (or renews) / removes the Infested effect (a player) or the hidden slow <see cref="EngulfedEffect"/> (a creature).</item>
    /// </list>
    /// A creature or a structure needs no RPC for the tick (plan 6, pass 6-2): the mass owner builds the same aggregated HitData, attributed to the mass, and
    /// sends it with the plain vanilla <c>Damage</c>, which routes it to the owner of the victim; armor, resistances and the retaliation are then vanilla's.
    /// No <see cref="WormBiteContext"/> is opened for them, so the durability patches stay inert.
    /// In solo both owners are the same peer; the code goes through the RPCs all the same (a routed RPC to oneself is handled synchronously).
    /// No <c>byte[]</c> travels in a routed RPC.
    /// </summary>
    internal static class MassEngulf
    {
        /// <summary>Seconds between two renewals of the Infested effect while a mass engulfs a player (the effect lasts <see cref="InfestedEffect.Lifetime"/>).</summary>
        public const float InfestRefresh = 0.5f;

        /// <summary>The mass walks up to this distance of the target (plan 5, A1: stop at 0 m; the walk helper never stops closer than 0.5 m).</summary>
        public const float WalkStop = 0.3f;

        /// <summary>The tick pushes nothing: the mass sits on its target, a shove in a random direction would only be noise (the knockback of a deflected hit is vanilla's).</summary>
        public const float TickPushForce = 0f;

        private static bool s_warnedNoAttacker;

#if MW_DEV
        // Debug counters read by the scenarios (S04).
        public static float LastTickTime;
        public static int TicksSent, TicksReceived, TicksDroppedNoAttacker, TicksInvincible, Parries, InfestSent, InfestReceived;
        public static int CreatureTicks, StructureTicks;
        public static float LastCreatureTickDamage, LastStructureTickDamage;

        public static void ResetCounters()
        {
            TicksSent = TicksReceived = TicksDroppedNoAttacker = TicksInvincible = Parries = InfestSent = InfestReceived = 0;
            CreatureTicks = StructureTicks = 0;
            LastCreatureTickDamage = LastStructureTickDamage = 0f;
        }
#endif

        /// <summary>Registers the routed RPCs once per game session (Game.Start postfix, like <see cref="WormBite.Register"/>).</summary>
        public static void Register()
        {
            if (ZRoutedRpc.instance == null) return;
            try
            {
                ZRoutedRpc.instance.Register<ZDOID, ZDOID, float, float, int, Vector3>(MWNames.RpcMassBite, OnMassBite);
                ZRoutedRpc.instance.Register<ZDOID, bool>(MWNames.RpcInfest, OnInfest);
                ZRoutedRpc.instance.Register<ZDOID>(MWNames.RpcMassParry, OnParry);
            }
            catch (ArgumentException)
            {
                // Already registered on this ZRoutedRpc (Game.Start ran twice on it).
            }
        }

        // ------------------------------------------------------------------ sending (owner of the mass)

        /// <summary>
        /// One aggregated tick of <paramref name="mass"/> on <paramref name="target"/>: P = n x Worm.DurabilityPerBite points, n x
        /// Worm.HealthDamagePerBite piercing health damage in case nothing is left to eat. <paramref name="dir"/> is the horizontal direction of the
        /// attack (mass toward target), which decides whether the block angle of a raised shield covers it. The world difficulty factor is applied
        /// by the receiver, which knows the position of the player.
        /// </summary>
        public static bool SendBite(WritheMassState mass, Player target, Vector3 dir)
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null || mass == null || target == null) return false;
            ZDOID targetId = target.GetZDOID();
            long owner = target.GetOwner();
            if (targetId.IsNone() || owner == 0L) return false;   // 0 would mean "everybody" for a routed RPC

            int n = Math.Max(1, mass.N);
            float p = EngulfMath.TickDurability(n, MWConfig.Worm.DurabilityPerBite.Value, 1f, false);
            float hp = n * MWConfig.Worm.HealthDamagePerBite.Value;
            rpc.InvokeRoutedRPC(owner, MWNames.RpcMassBite, targetId, mass.ZdoId, p, hp, n, dir);
#if MW_DEV
            TicksSent++;
#endif
            return true;
        }

        /// <summary>
        /// One aggregated tick on a creature or a tamed animal: n x Worm.CreatureDamagePerBite piercing damage, blockable and dodgeable like any monster
        /// attack, attributed to the mass (a real Character: the creature's AI answers it, and the loot of a kill goes to the mass, spec §7.4). Called by the owner of the mass.
        /// </summary>
        public static bool HitCreature(WritheMassState mass, Character target, Vector3 dir)
        {
            if (mass == null || target == null || target.IsDead()) return false;
            int n = Math.Max(1, mass.N);
            HitData hit = WormBite.NewHit(n * MWConfig.Worm.CreatureDamagePerBite.Value, target.GetCenterPoint(), dir, TickPushForce);
            hit.m_attacker = mass.ZdoId;
            hit.m_hitType = HitData.HitType.EnemyHit;
            target.Damage(hit);
#if MW_DEV
            TicksSent++;
            CreatureTicks++;
            LastCreatureTickDamage = hit.m_damage.m_pierce;
#endif
            return true;
        }

        /// <summary>One aggregated tick on a structure: n x Worm.StructureDamagePerBite blunt damage at <paramref name="point"/>, attributed to the mass.</summary>
        public static bool HitStructure(WritheMassState mass, Component target, Vector3 point, Vector3 dir)
        {
            IDestructible destructible = mass != null && target != null ? target.GetComponent<IDestructible>() : null;
            if (destructible == null) return false;
            int n = Math.Max(1, mass.N);
            HitData hit = WormBite.NewHit(0f, point, dir, 0f);
            hit.m_damage.m_blunt = n * MWConfig.Worm.StructureDamagePerBite.Value;
            hit.m_blockable = false;
            hit.m_dodgeable = false;
            hit.m_attacker = mass.ZdoId;
            hit.m_hitType = HitData.HitType.EnemyHit;
            destructible.Damage(hit);
#if MW_DEV
            TicksSent++;
            StructureTicks++;
            LastStructureTickDamage = hit.m_damage.m_blunt;
#endif
            return true;
        }

        /// <summary>Applies (<paramref name="on"/>) or removes the Infested effect (player) or the hidden slow (creature) on <paramref name="target"/>, wherever its owner is.</summary>
        public static bool SendInfest(Character target, bool on)
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null || target == null) return false;
            ZDOID targetId = target.GetZDOID();
            long owner = target.GetOwner();
            if (targetId.IsNone() || owner == 0L) return false;
            rpc.InvokeRoutedRPC(owner, MWNames.RpcInfest, targetId, on);
#if MW_DEV
            InfestSent++;
#endif
            return true;
        }

        /// <summary>The target's owner tells the owner of the mass that its tick was parried: the next one is delayed (plan 5, A11).</summary>
        public static void SendParry(ZDOID massId)
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            ZDO zdo = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(massId) : null;
            if (rpc == null || zdo == null || zdo.GetOwner() == 0L) return;
            rpc.InvokeRoutedRPC(zdo.GetOwner(), MWNames.RpcMassParry, massId);
        }

        private static void OnParry(long sender, ZDOID massId)
        {
            try
            {
                var all = WritheMassState.All;
                for (int i = 0; i < all.Count; i++)
                {
                    WritheMassState m = all[i];
                    if (m == null || !m.Valid || !m.IsOwner || m.ZdoId != massId) continue;
#if MW_DEV
                    Parries++;
#endif
                    if (m.Ai != null) m.Ai.Parried();
                    return;
                }
            }
            catch (Exception e)
            {
                Plugin.LogException("MassEngulf.OnParry", e);
            }
        }

        // ------------------------------------------------------------------ receiving (owner of the player)

        private static void OnMassBite(long sender, ZDOID targetId, ZDOID massId, float durability, float hpDamage, int worms, Vector3 dir)
        {
            try
            {
                if (WormBiteContext.Active) return; // never nest
                Player target = WormBite.FindOwnedPlayer(targetId);
                if (target == null) return;
#if MW_DEV
                TicksReceived++;
                LastTickTime = Time.time;
                if (target.IsDodgeInvincible()) TicksInvincible++;   // the vanilla dodge frames cancel this tick (plan 5, A12)
#endif
                // The vanilla RPC_Damage throws away a hit whose attacker is unknown here: the mass must be loaded on this peer (it sits on the player).
                GameObject massGo = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(massId) : null;
                Character attacker = massGo != null ? massGo.GetComponent<Character>() : null;
                if (attacker == null)
                {
#if MW_DEV
                    TicksDroppedNoAttacker++;
#endif
                    if (!s_warnedNoAttacker)
                    {
                        s_warnedNoAttacker = true;
                        Plugin.Log.LogWarning($"[Engulf] a bite tick of mass {massId} reached the owner of its target but the mass is not loaded here: the tick is lost");
                    }
                    return;
                }

                // RPC_Damage scales the damage of a non-player attacker by the difficulty of the area and the world's enemy damage rate; the same
                // factor applies to P when Engulf.ScaleWithWorldModifiers (spec §9.2). The ratio the patch measures (block, armor) is taken against the scaled health damage.
                float scale = Game.instance != null ? Game.instance.GetDifficultyDamageScalePlayer(target.transform.position) * Game.m_enemyDamageRate : 1f;
                float p = EngulfMath.TickDurability(1, durability, scale, MWConfig.Engulf.ScaleWithWorldModifiers.Value);

                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-4f) dir = -target.transform.forward;   // no known direction: from the front, so a raised shield covers it
                dir.Normalize();

                HitData hit = WormBite.NewHit(hpDamage, target.GetCenterPoint(), dir, TickPushForce);
                hit.m_attacker = massId;
                hit.m_hitType = HitData.HitType.EnemyHit;

                WormBiteContext.Begin(target, p, hpDamage * scale, worms, massId);
                try
                {
                    target.Damage(hit);
                }
                finally
                {
                    WormBiteContext.End();
                }
                MassAudio.PlayBiteTick(target.GetCenterPoint(), Time.time);
            }
            catch (Exception e)
            {
                Plugin.LogException("MassEngulf.OnMassBite", e);
            }
        }

        private static void OnInfest(long sender, ZDOID targetId, bool on)
        {
            try
            {
                Character target = WormBite.FindOwnedCharacter(targetId);
                if (target == null) return;
#if MW_DEV
                InfestReceived++;
#endif
                if (target is Player player)
                {
                    if (on)
                    {
                        if (player.IsDead()) return;
                        InfestedEffect.Apply(player);
                    }
                    else
                    {
                        InfestedEffect.Remove(player);
                    }
                    return;
                }
                if (on)
                {
                    if (!target.IsDead()) EngulfedEffect.Apply(target);
                }
                else
                {
                    EngulfedEffect.Remove(target);
                }
            }
            catch (Exception e)
            {
                Plugin.LogException("MassEngulf.OnInfest", e);
            }
        }
    }
}
