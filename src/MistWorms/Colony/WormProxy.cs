using System;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>
    /// The hit target standing in for a virtual worm (spec §10.1): a pooled object with a small non trigger collider that vanilla
    /// melee, projectiles and area attacks find through <c>Projectile.FindHitObject</c> (an <see cref="IDestructible"/> on the collider's
    /// object). It carries no state of its own: <c>Damage</c> forwards the hit to the colony together with the slot and its generation,
    /// so a hit on a slot that was reused since the attachment is ignored.
    /// </summary>
    internal sealed class WormProxy : MonoBehaviour, IDestructible
    {
        public WormColony colony;
        public int index;
        public ushort generation;
        public int poolIndex;

        /// <summary>
        /// Character, so the weapon skill of the attacker rises on a hit as it does on a creature (Attack.cs:1187 and :1384 are the only
        /// readers of the destructible type; nothing casts on it). decisions.md 51.
        /// </summary>
        public DestructibleType GetDestructibleType() => DestructibleType.Character;

        public void Damage(HitData hit)
        {
            try
            {
                if (colony != null && hit != null) colony.ReceiveWormHit(index, generation, hit);
            }
            catch (Exception e)
            {
                Plugin.LogException("WormProxy.Damage", e);
            }
        }
    }
}
