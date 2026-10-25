#if MW_DEV
using System;
using System.Collections.Generic;
using MistWorms.Colony;
using MistWorms.Mass;
using UnityEngine;

namespace MistWorms.Dev
{
    /// <summary>
    /// The real <c>BaseAI.IsEnemy</c> matrix between a mass (faction PlayerSpawned, no patch of IsEnemy, spec §5.4) and the creatures around it, with what
    /// the table of <see cref="FactionMatrix"/> (spec §5.4) expects (plan 6, A8). Printed by <c>/state</c> and compared by S19.
    /// </summary>
    internal static class FactionDev
    {
        internal struct Row
        {
            public Character.Faction Faction;
            public string Prefab;
            public bool Tamed;
            public bool MassToOther, OtherToMass;
            public bool SpecMassToOther, SpecOtherToMass;         // the table of spec §5.4 (tamed row included)

            public bool MatchesSpec => MassToOther == SpecMassToOther && OtherToMass == SpecOtherToMass;
        }

        /// <summary>Evaluates both directions of <see cref="BaseAI.IsEnemy(Character, Character)"/> between <paramref name="mass"/> and <paramref name="other"/>, with the expectations.</summary>
        public static Row Evaluate(Character mass, Character other)
        {
            var row = new Row
            {
                Faction = other.GetFaction(),
                Prefab = Utils.GetPrefabName(other.gameObject),
                Tamed = other.IsTamed(),
                MassToOther = BaseAI.IsEnemy(mass, other),
                OtherToMass = BaseAI.IsEnemy(other, mass),
            };
            if (row.Tamed)
            {
                FactionMatrix.Tamed(out row.SpecMassToOther, out row.SpecOtherToMass);
            }
            else
            {
                FactionMatrix.Spec((FactionId)(int)row.Faction, out row.SpecMassToOther, out row.SpecOtherToMass);
            }
            return row;
        }

        /// <summary>One row per (faction, tamed) among the characters loaded now, the mass itself excluded.</summary>
        public static List<Row> Present(Character mass)
        {
            var rows = new List<Row>();
            var seen = new HashSet<int>();
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c == mass) continue;
                int key = (int)c.GetFaction() * 2 + (c.IsTamed() ? 1 : 0);
                if (!seen.Add(key)) continue;
                rows.Add(Evaluate(mass, c));
            }
            return rows;
        }

        public static Dictionary<string, object> Json(Row r) => new Dictionary<string, object>
        {
            { "faction", r.Faction.ToString() },
            { "prefab", r.Prefab },
            { "tamed", r.Tamed },
            { "massToOther", r.MassToOther },
            { "otherToMass", r.OtherToMass },
            { "expectedSpec", new[] { r.SpecMassToOther, r.SpecOtherToMass } },
            { "matchesSpec", r.MatchesSpec },
        };

        /// <summary>The matrix of the mass closest to the player against every faction present around (null without a mass), for GET /state.</summary>
        public static object StateJson()
        {
            Player player = Player.m_localPlayer;
            WritheMassState mass = MassDev.Nearest(player != null ? player.transform.position : Vector3.zero);
            if (mass == null || mass.Character == null) return null;
            var list = new List<object>();
            foreach (Row r in Present(mass.Character)) list.Add(Json(r));
            return new Dictionary<string, object> { { "mass", mass.ZdoId.ToString() }, { "faction", mass.Character.GetFaction().ToString() }, { "rows", list } };
        }
    }
}
#endif
