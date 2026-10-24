using System;
using System.Globalization;
using MistWorms.Config;
using MistWorms.Localization;
using UnityEngine;

namespace MistWorms.Bites
{
    /// <summary>
    /// The cobra strike of a lone worm on a target (spec §3.1, §6.3, §11.1, decisions.md 1.1 line 20 option a). The colony owner sends
    /// the routed RPC <c>MW_WormBite</c> to the owner of the target; that peer builds an attacker-less HitData and runs it through the
    /// vanilla <c>Damage</c> inside a <see cref="WormBiteContext"/>, where the patches turn the hit into a durability bite.
    /// </summary>
    internal static class WormBite
    {
        /// <summary>A strike hardly pushes (the vanilla push is force / mass * 2.5, so this is a nudge).</summary>
        public const float PushForce = 2f;

        /// <summary>Registers the routed RPC once per game session (called from a Game.Start postfix, like vanilla's own registrations).</summary>
        public static void Register()
        {
            if (ZRoutedRpc.instance == null) return;
            try
            {
                ZRoutedRpc.instance.Register<ZDOID, float, float, Vector3, Vector3>(MWNames.RpcWormBite, OnBite);
            }
            catch (ArgumentException)
            {
                // Already registered on this ZRoutedRpc (Game.Start ran twice on it).
            }
        }

        /// <summary>Sends a strike on <paramref name="target"/> to its owner (possibly this peer: handled synchronously). False if it cannot be routed.</summary>
        public static bool SendToPlayer(Player target, Vector3 point, Vector3 dir)
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null || target == null) return false;
            ZDOID id = target.GetZDOID();
            long owner = target.GetOwner();
            // 0 would mean "everybody" for a routed RPC; a player without owner cannot be bitten.
            if (id.IsNone() || owner == 0L) return false;

            rpc.InvokeRoutedRPC(owner, MWNames.RpcWormBite, id, MWConfig.Worm.CobraDurability.Value,
                MWConfig.Worm.CobraCreatureDamage.Value, point, dir);
            return true;
        }

        /// <summary>
        /// Strike on a creature (phase 6 supplies the targets): plain vanilla damage, no context, pierce CobraCreatureDamage, no attacker
        /// (a colony has no Character, decisions.md 1.1 line 20).
        /// </summary>
        public static void HitCreature(Character target, Vector3 point, Vector3 dir)
        {
            if (target == null) return;
            HitData hit = NewHit(MWConfig.Worm.CobraCreatureDamage.Value, point, dir);
            hit.m_blockable = false;
            hit.m_dodgeable = false;
            target.Damage(hit);
        }

        private static HitData NewHit(float pierce, Vector3 point, Vector3 dir)
        {
            var hit = new HitData();
            hit.m_damage.m_pierce = pierce;
            hit.m_point = point;
            hit.m_dir = dir;
            hit.m_blockable = true;   // vanilla leaves both false: a bite must opt in (decisions.md 1.3)
            hit.m_dodgeable = true;
            hit.m_hitType = HitData.HitType.EnemyHit;
            hit.m_pushForce = PushForce;
            return hit;
        }

        private static void OnBite(long sender, ZDOID targetId, float durability, float hpDamage, Vector3 point, Vector3 dir)
        {
            try
            {
                if (WormBiteContext.Active) return; // never nest
                Player target = FindOwnedPlayer(targetId);
                if (target == null) return;

                HitData hit = NewHit(hpDamage, point, dir);
                WormBiteContext.Begin(target, durability, hpDamage);
                try
                {
                    target.Damage(hit);
                }
                finally
                {
                    WormBiteContext.End();
                }
            }
            catch (Exception e)
            {
                Plugin.LogException("WormBite.OnBite", e);
            }
        }

        private static Player FindOwnedPlayer(ZDOID id)
        {
            var players = Player.GetAllPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                Player p = players[i];
                if (p != null && p.IsOwner() && p.GetZDOID() == id) return p;
            }
            return null;
        }

        /// <summary>Floating orange "Gear -P" over the player (spec §6.6); TextType.Bonus is the orange one, decisions.md 52.</summary>
        public static void ShowEquipmentText(Vector3 point, float eaten)
        {
            if (!MWConfig.Engulf.ShowDurabilityText.Value || DamageText.instance == null) return;
            string amount = eaten.ToString("0.#", CultureInfo.InvariantCulture);
            DamageText.instance.ShowText(DamageText.TextType.Bonus, point, MWLocalization.Format("$mw_text_eq", amount), true);
        }
    }
}
