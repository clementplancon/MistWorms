using System;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>
    /// Colony side of the link with the writhing masses (spec §4.4, §11.3). Phase 4B-1: the dissolution of a mass recreates its worms
    /// fleeing around its position. Formation, absorption and merging come with phase 4B-3, the visible leap with phase 4B-4.
    /// </summary>
    internal sealed partial class WormColony
    {
        // Where a dissolved worm appears, around the centre of the mass (the leap of spec §4.4 comes with the transitions).
        private const float DissolveRingMin = 0.3f;
        private const float DissolveRingSpread = 0.7f;

        /// <summary>Number of worms the last dissolution could not place (store full or colony disabled), for the dev tools.</summary>
        public int DissolveLost { get; private set; }

        /// <summary>Worms created by dissolutions on this colony since it was loaded.</summary>
        public int DissolvedWorms { get; private set; }

        /// <summary>
        /// Asks the owner of this colony to recreate <paramref name="n"/> fleeing worms around <paramref name="position"/>. The owner of the colony
        /// applies it directly; any other peer sends the routed RPC <c>MW_Dissolve</c> (the pool of the mass travels in <paramref name="pool"/>, phase 6).
        /// </summary>
        public void RequestDissolve(ZDOID mass, int n, Vector3 position, ZPackage pool)
        {
            if (!_initialized || !_nview.IsValid()) return;
            if (_nview.IsOwner())
            {
                ReceiveDissolve(mass, n, position);
                return;
            }
            _nview.InvokeRPC(MWNames.RpcMassDissolve, mass, n, position, pool ?? new ZPackage());
        }

        private void RPC_MassDissolve(long sender, ZDOID mass, int n, Vector3 position, ZPackage pool)
        {
            try
            {
                if (!_initialized || !_nview.IsValid() || !_nview.IsOwner()) return;
                ReceiveDissolve(mass, n, position);
            }
            catch (Exception e)
            {
                Plugin.LogException("WormColony.RPC_MassDissolve", e);
            }
        }

        /// <summary>
        /// Creates <paramref name="n"/> worms in the Fleeing state on the floor around <paramref name="position"/>, each facing away from the
        /// centre of the mass (it is also the point they flee from). They are worms that already existed, so the population cap does not apply.
        /// </summary>
        private void ReceiveDissolve(ZDOID mass, int n, Vector3 position)
        {
            DissolveLost = 0;
            if (Store == null || Disabled || n <= 0 || n > MWConfig.Mass.MaxCount.Value)
            {
                DissolveLost = Math.Max(0, n);
                Plugin.Log.LogWarning($"[Colony] dissolution of mass {mass} ({n} worms) refused (store {(Store != null ? "ok" : "missing")}, disabled {Disabled})");
                return;
            }

            for (int k = 0; k < n; k++)
            {
                float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;
                float dist = DissolveRingMin + (float)_rng.NextDouble() * DissolveRingSpread;
                float dx = Mathf.Cos(angle) * dist, dz = Mathf.Sin(angle) * dist;
                var at = new Vector3(position.x + dx, position.y, position.z + dz);
                // A spot without a floor (a wall just behind): fall back on the centre of the mass.
                if (!WormPhysics.ProbeGround(at, out _)) at = position;

                int slot = AddWorm(at, WormState.Fleeing, WormMath.HeadingOf(dx, dz), force: true);
                if (slot < 0)
                {
                    DissolveLost++;
                    continue;
                }
                Store.fleeFrom[slot] = position;
                DissolvedWorms++;
            }
            Plugin.Log.LogInfo($"[Colony] mass {mass} dissolved: {n - DissolveLost}/{n} fleeing worms created at {position}");
        }
    }
}
