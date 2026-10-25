using System;
using Jotunn.Entities;
using Jotunn.Managers;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Bites
{
    /// <summary>
    /// The hidden slow of an engulfed creature (spec §6.5, plan 6, A14): a vanilla <see cref="SE_Stats"/> with no icon and no name whose <c>m_speedModifier</c> is
    /// Engulf.SlowFactor - 1, registered in the ObjectDB through Jötunn like <see cref="InfestedEffect"/> and applied the same way: by the owner of the creature,
    /// on the request of the mass (<c>MW_Infest</c>), renewed every <see cref="MassEngulf.InfestRefresh"/> seconds and expiring by itself after <see cref="InfestedEffect.Lifetime"/>.
    /// Players get Infested instead; structures get nothing.
    /// </summary>
    internal static class EngulfedEffect
    {
        public static readonly int Hash = MWNames.StatusEngulfed.GetStableHashCode();

        public static void Register()
        {
            try
            {
                var se = ScriptableObject.CreateInstance<SE_Stats>();
                se.name = MWNames.StatusEngulfed;
                se.m_name = string.Empty;
                se.m_ttl = InfestedEffect.Lifetime;
                se.m_speedModifier = SpeedModifier();
                ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, fixReference: false));
            }
            catch (Exception e)
            {
                Plugin.LogException("EngulfedEffect.Register", e);
            }
        }

        private static float SpeedModifier() => Mathf.Clamp(MWConfig.Engulf.SlowFactor.Value, 0.1f, 1f) - 1f;

        /// <summary>Applies or renews the slow on <paramref name="target"/>, which this peer owns. The slow factor is read from the config each time.</summary>
        public static void Apply(Character target)
        {
            SEMan seman = target != null ? target.GetSEMan() : null;
            if (seman == null) return;
            seman.AddStatusEffect(Hash, resetTime: true);
            if (seman.GetStatusEffect(Hash) is SE_Stats se) se.m_speedModifier = SpeedModifier();
        }

        public static void Remove(Character target)
        {
            SEMan seman = target != null ? target.GetSEMan() : null;
            if (seman != null && seman.HaveStatusEffect(Hash)) seman.RemoveStatusEffect(Hash, quiet: true);
        }

        public static bool IsActive(Character target)
        {
            SEMan seman = target != null ? target.GetSEMan() : null;
            return seman != null && seman.HaveStatusEffect(Hash);
        }
    }
}
