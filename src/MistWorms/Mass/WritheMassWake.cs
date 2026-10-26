using MistWorms.Colony;
using UnityEngine;

namespace MistWorms.Mass
{
    /// <summary>
    /// What a mass shares with the waking and the return to sleep of its groups (spec §5.1, §5.2, plan 7-2): the time of the last hit by a player, which the
    /// owner of the colony reads to alert the groups (the mass may belong to another peer), the group the mass belongs to, and its dissolution into sleeping worms.
    /// </summary>
    internal sealed partial class WritheMassState
    {
        private static readonly int HashPlayerHit = MWNames.ZdoMassPlayerHit.GetStableHashCode();

        /// <summary>The last hit time this peer's colony has acted on (-1 = not looked at yet). Local to the owner of the colony.</summary>
        public long SeenPlayerHit = -1;

        /// <summary>Network time (ticks) of the last hit by a player, 0 = never.</summary>
        public long PlayerHitTicks => Valid ? _nview.GetZDO().GetLong(HashPlayerHit, 0L) : 0L;

        /// <summary>Postfix of <c>Character.ApplyDamage</c> on the owner: a hit with a player as attacker is written to the ZDO. Anything else changes nothing.</summary>
        public void NotePlayerHit(HitData hit)
        {
            if (!IsOwner || hit == null || ZNet.instance == null) return;
            if (!(hit.GetAttacker() is Player)) return;
            _nview.GetZDO().Set(HashPlayerHit, ZNet.instance.GetTime().Ticks);
        }

        /// <summary>The nest most of the worms of this mass come from, or -1: the group the mass belongs to (see <see cref="NestCounts.Majority"/>). Any peer.</summary>
        public int MajorityNest()
        {
            if (!Valid) return -1;
            if (IsOwner) return OwnerNests().Majority();
            s_nestRead.ReadBytes(_nview.GetZDO().GetByteArray(MWNames.ZdoMassNests, null));
            return s_nestRead.Majority();
        }

        /// <summary>Copies the nests of origin of the worms of this mass into <paramref name="into"/>. Any peer.</summary>
        public void CopyNestsTo(NestCounts into)
        {
            into.Clear();
            if (!Valid) return;
            if (IsOwner) into.CopyFrom(OwnerNests());
            else into.ReadBytes(_nview.GetZDO().GetByteArray(MWNames.ZdoMassNests, null));
        }

        /// <summary>
        /// The group went back to sleep and the mass reached its nest (or has none to reach): the colony lays its worms down asleep around
        /// <paramref name="deposit"/> and the mass goes away like a dissolved one. Owner only.
        /// </summary>
        public void BeginDormantDissolve(Vector3 deposit)
        {
            if (!IsOwner || _dissolving) return;
            BeginDissolve(Mathf.Max(1, N), sleep: true, deposit);
        }
    }
}
