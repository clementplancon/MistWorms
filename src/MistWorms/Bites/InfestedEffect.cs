using System;
using Jotunn.Entities;
using Jotunn.Managers;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Bites
{
    /// <summary>
    /// The Infested status effect of an engulfed player (spec §6.2, plan 5, A13). A vanilla <see cref="SE_Stats"/> registered in the ObjectDB
    /// through Jötunn: its <c>m_speedModifier</c> (SlowFactor - 1) goes through <c>SEMan.ApplyStatusEffectSpeedMods</c>, which vanilla calls for the
    /// walk, the run (<c>Character.UpdateWalking</c> passes the already chosen m_runSpeed through it) and the swim, so the sprint is slowed
    /// too. Applied and removed on the owner of the player only (<see cref="MassEngulf"/> routes the request); no damage of its own.
    /// The mass renews it every <see cref="MassEngulf.InfestRefresh"/> seconds and it expires by itself after <see cref="Lifetime"/> if the mass goes.
    /// </summary>
    internal static class InfestedEffect
    {
        /// <summary>Seconds the effect lasts without renewal (three refresh periods: a lost RPC or two do not release the player).</summary>
        public const float Lifetime = 1.5f;

        public static readonly int Hash = MWNames.StatusInfested.GetStableHashCode();

        private static SE_Stats s_template;
        private static bool s_iconChosen;
        private static string s_iconNote = "icon not chosen yet";

        /// <summary>What the icon choice found (dev log / state).</summary>
        public static string IconNote => s_iconNote;

        /// <summary>Creates the effect and hands it to Jötunn, which adds it to every ObjectDB. Called once, from the plugin Awake.</summary>
        public static void Register()
        {
            try
            {
                var se = ScriptableObject.CreateInstance<SE_Stats>();
                se.name = MWNames.StatusInfested;
                se.m_name = MWNames.StatusInfestedNameToken;
                se.m_tooltip = MWNames.StatusInfestedDescToken;
                se.m_ttl = Lifetime;
                se.m_speedModifier = SpeedModifier();
                s_template = se;
                ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, fixReference: false));
            }
            catch (Exception e)
            {
                Plugin.LogException("InfestedEffect.Register", e);
            }
        }

        private static float SpeedModifier() => Mathf.Clamp(MWConfig.Engulf.SlowFactor.Value, 0.1f, 1f) - 1f;

        /// <summary>
        /// Picks the icon once the vanilla ObjectDB is there: the effect named "Slimed", else "Poison", else none (needs an icon).
        /// The choice is logged. Safe to call again (a new ObjectDB after a scene change re-applies the same sprite).
        /// </summary>
        public static void ChooseIcon()
        {
            try
            {
                ObjectDB db = ObjectDB.instance;
                if (db == null || s_template == null) return;

                StatusEffect preferred = null, fallback = null;
                foreach (StatusEffect e in db.m_StatusEffects)
                {
                    if (e == null || e.m_icon == null || e == s_template || e.name == MWNames.StatusInfested) continue;
                    if (preferred == null && string.Equals(e.name, VanillaNames.InfestedIconPreferred, StringComparison.Ordinal)) preferred = e;
                    if (fallback == null && string.Equals(e.name, VanillaNames.InfestedIconFallback, StringComparison.Ordinal)) fallback = e;
                }
                StatusEffect chosen = preferred ?? fallback;
                Sprite icon = chosen != null ? chosen.m_icon : null;
                s_template.m_icon = icon;
                StatusEffect registered = db.GetStatusEffect(Hash);   // Jötunn may have registered a copy
                if (registered != null) registered.m_icon = icon;

                s_iconNote = chosen != null
                    ? $"icon of '{chosen.name}' ({(preferred != null ? "preferred" : "fallback")}), sprite '{icon.name}'"
                    : $"no status effect with an icon is named '{VanillaNames.InfestedIconPreferred}' or '{VanillaNames.InfestedIconFallback}': no icon";
                if (!s_iconChosen)
                {
                    s_iconChosen = true;
                    Plugin.Log.LogInfo("[Infested] " + s_iconNote + $"; registered in the ObjectDB: {registered != null}");
                }
            }
            catch (Exception e)
            {
                Plugin.LogException("InfestedEffect.ChooseIcon", e);
            }
        }

        /// <summary>Applies or renews the effect on <paramref name="player"/>, which this peer owns. The slow factor is read from the config each time.</summary>
        public static void Apply(Player player)
        {
            if (player == null) return;
            SEMan seman = player.GetSEMan();
            if (seman == null) return;
            seman.AddStatusEffect(Hash, resetTime: true);
            if (seman.GetStatusEffect(Hash) is SE_Stats se) se.m_speedModifier = SpeedModifier();
        }

        public static void Remove(Player player)
        {
            SEMan seman = player != null ? player.GetSEMan() : null;
            if (seman != null && seman.HaveStatusEffect(Hash)) seman.RemoveStatusEffect(Hash, quiet: true);
        }

        public static bool IsActive(Player player)
        {
            SEMan seman = player != null ? player.GetSEMan() : null;
            return seman != null && seman.HaveStatusEffect(Hash);
        }
    }
}
