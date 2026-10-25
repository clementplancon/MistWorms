using System;
using System.Globalization;
using MistWorms.Config;
using MistWorms.Drops;
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
                ZRoutedRpc.instance.Register<ZDOID, float, float, Vector3, Vector3, ZPackage>(MWNames.RpcWormBite, OnBite);
                ZRoutedRpc.instance.Register<ZDOID, ZDOID, int, int>(MWNames.RpcLastWormHit, OnLastWormHit);
            }
            catch (ArgumentException)
            {
                // Already registered on this ZRoutedRpc (Game.Start ran twice on it).
            }
        }

        /// <summary>Sends a strike on <paramref name="target"/> to its owner (possibly this peer: handled synchronously). False if it cannot be routed.</summary>
        public static bool SendToPlayer(Player target, Vector3 point, Vector3 dir, ZDOID colony, int slot, int generation)
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null || target == null) return false;
            ZDOID id = target.GetZDOID();
            long owner = target.GetOwner();
            // 0 would mean "everybody" for a routed RPC; a player without owner cannot be bitten.
            if (id.IsNone() || owner == 0L) return false;

            rpc.InvokeRoutedRPC(owner, MWNames.RpcWormBite, id, MWConfig.Worm.CobraDurability.Value,
                MWConfig.Worm.CobraCreatureDamage.Value, point, dir, WormTag(colony, slot, generation));
            return true;
        }

        /// <summary>
        /// Strike on a creature or a tamed animal: plain vanilla damage, no context, pierce CobraCreatureDamage, no attacker (a colony has no Character,
        /// decisions.md 1.1 line 20). The "last worm hit" registry is written first, on the peer that owns the victim (spec §7.4), so that the loot of a creature
        /// this strike kills goes to the pool of the worm. Returns false if the target has no usable ZDO.
        /// </summary>
        public static bool HitCreature(Character target, Vector3 point, Vector3 dir, ZDOID colony, int slot, int generation)
        {
            if (target == null) return false;
            NoteLastHit(target.GetZDOID(), target.GetOwner(), colony, slot, generation);
            HitData hit = NewHit(MWConfig.Worm.CobraCreatureDamage.Value, point, dir);
            hit.m_blockable = false;
            hit.m_dodgeable = false;
            hit.m_skillRaiseAmount = CobraMark.Value;   // recognised as a cobra hit by the loot capture (decision 163)
            target.Damage(hit);
            return true;
        }

        /// <summary>
        /// Strike on a structure (a WearNTear piece or a dvergr Destructible): the same attackerless hit, blunt, with the damage of a creature strike
        /// (Worm.CobraCreatureDamage). The vanilla resistances, tool tier and ward rules apply when the victim receives it. The registry is written first.
        /// </summary>
        public static bool HitStructure(Component target, Vector3 point, Vector3 dir, ZDOID colony, int slot, int generation)
        {
            IDestructible destructible = target != null ? target.GetComponent<IDestructible>() : null;
            ZNetView view = target != null ? target.GetComponent<ZNetView>() : null;
            if (destructible == null || view == null || !view.IsValid()) return false;
            NoteLastHit(view.GetZDO().m_uid, view.GetZDO().GetOwner(), colony, slot, generation);
            HitData hit = NewHit(0f, point, dir, 0f);
            hit.m_damage.m_blunt = MWConfig.Worm.CobraCreatureDamage.Value;
            hit.m_blockable = false;
            hit.m_dodgeable = false;
            hit.m_skillRaiseAmount = CobraMark.Value;
            destructible.Damage(hit);
            return true;
        }

        /// <summary>Writes the "last worm hit" registry on the peer that owns the victim: here when this peer owns it, by a routed RPC otherwise (it is sent before the damage, so it is there when the victim dies).</summary>
        private static void NoteLastHit(ZDOID victim, long owner, ZDOID colony, int slot, int generation)
        {
            if (victim == ZDOID.None) return;
            if (owner == 0L || owner == ZDOMan.GetSessionID())
            {
                DropCapture.Registry.Record(victim, colony, slot, generation, Time.time);
                return;
            }
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc != null) rpc.InvokeRoutedRPC(owner, MWNames.RpcLastWormHit, victim, colony, slot, generation);
        }

        private static void OnLastWormHit(long sender, ZDOID victim, ZDOID colony, int slot, int generation)
        {
            try
            {
                if (slot < 0 || generation < 0 || generation > ushort.MaxValue) return;
                DropCapture.Registry.Record(victim, colony, slot, generation, Time.time);
            }
            catch (Exception e)
            {
                Plugin.LogException("WormBite.OnLastWormHit", e);
            }
        }

        internal static HitData NewHit(float pierce, Vector3 point, Vector3 dir, float pushForce = PushForce)
        {
            var hit = new HitData();
            hit.m_damage.m_pierce = pierce;
            hit.m_point = point;
            hit.m_dir = dir;
            hit.m_blockable = true;   // vanilla leaves both false: a bite must opt in (decisions.md 1.3)
            hit.m_dodgeable = true;
            hit.m_hitType = HitData.HitType.EnemyHit;
            hit.m_pushForce = pushForce;
            return hit;
        }

        private static void OnBite(long sender, ZDOID targetId, float durability, float hpDamage, Vector3 point, Vector3 dir, ZPackage worm)
        {
            try
            {
                if (WormBiteContext.Active) return; // never nest
                Player target = FindOwnedPlayer(targetId);
                if (target == null) return;

                HitData hit = NewHit(hpDamage, point, dir);
                WormBiteContext.Begin(target, durability, hpDamage);
                ReadWormTag(worm);
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

        /// <summary>The worm behind a strike (colony, slot, generation) as a package: the routed RPC is at its six parameter limit (decision 141).</summary>
        private static ZPackage WormTag(ZDOID colony, int slot, int generation)
        {
            var pkg = new ZPackage();
            pkg.Write(colony);
            pkg.Write(slot);
            pkg.Write(generation);
            return pkg;
        }

        private static void ReadWormTag(ZPackage pkg)
        {
            try
            {
                if (pkg == null || pkg.Size() == 0) return;
                pkg.SetPos(0);
                ZDOID colony = pkg.ReadZDOID();
                int slot = pkg.ReadInt();
                int generation = pkg.ReadInt();
                WormBiteContext.SetWorm(colony, slot, generation);
            }
            catch (Exception)
            {
                // A malformed tag: the strike still bites, what it eats just belongs to nobody.
            }
        }

        /// <summary>The character <paramref name="id"/> (a player, a creature, a tamed animal) when this peer owns it, otherwise null.</summary>
        internal static Character FindOwnedCharacter(ZDOID id)
        {
            if (id == ZDOID.None) return null;
            var all = Character.GetAllCharacters();
            for (int i = 0; i < all.Count; i++)
            {
                Character c = all[i];
                if (c != null && c.IsOwner() && c.GetZDOID() == id) return c;
            }
            return null;
        }

        internal static Player FindOwnedPlayer(ZDOID id)
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
#if MW_DEV
            WormBiteContext.Texts++;
#endif
            string amount = eaten.ToString("0.#", CultureInfo.InvariantCulture);
            DamageText.instance.ShowText(DamageText.TextType.Bonus, point, MWLocalization.Format("$mw_text_eq", amount), true);
        }
    }
}
