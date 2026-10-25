using System.Collections.Generic;
using MistWorms.Colony;
using UnityEngine;

namespace MistWorms.Mass
{
    /// <summary>
    /// Engulfing, every peer's part (spec §10.2, plan 5, A2): while <c>mw_state</c> says Engulf, the physical collisions between the mass (its capsule and its
    /// hit spheres) and all the colliders of the engulfed player are ignored, so the mass can walk onto the player and the player is not pushed out;
    /// they come back when the mass lets go, changes target or disappears. Driven by the state, so a peer that is neither owner needs no message.
    /// Checked five times a second, no allocation (reused lists).
    /// </summary>
    internal sealed partial class WritheMassState
    {
        private const float IgnorePeriod = 0.2f;

        private Player _ignoredPlayer;
        private ZDOID _ignoredId;
        private float _nextIgnore;
        private readonly List<Collider> _ignoredColliders = new List<Collider>(32);
        private readonly List<Collider> _scratchColliders = new List<Collider>(32);

        /// <summary>The player whose colliders this mass currently ignores (dev checks), or null.</summary>
        public Player IgnoredPlayer => _ignoredPlayer;

        /// <summary>Number of the player's colliders currently ignored (dev checks).</summary>
        public int IgnoredColliderCount => _ignoredColliders.Count;

        private void EngulfCollisionTick(float now)
        {
            if (now < _nextIgnore || _capsule == null) return;
            _nextIgnore = now + IgnorePeriod;

            bool engulfing = MassStateCodes.Mode(StateCode) == MassStateCodes.Engulf && !_dissolving;
            ZDOID id = engulfing ? TargetId : ZDOID.None;
            if (id == ZDOID.None)
            {
                if (_ignoredColliders.Count > 0 || _ignoredPlayer != null) RestoreCollisions();
                return;
            }

            if (_ignoredPlayer == null || _ignoredId != id)
            {
                RestoreCollisions();
                var players = Player.GetAllPlayers();
                for (int i = 0; i < players.Count; i++)
                {
                    Player p = players[i];
                    if (p != null && p.GetZDOID() == id) { _ignoredPlayer = p; _ignoredId = id; break; }
                }
                if (_ignoredPlayer == null) return;
            }

            // Sweep: a collider added since (equipment) is ignored too.
            _ignoredPlayer.GetComponentsInChildren<Collider>(true, _scratchColliders);
            for (int i = 0; i < _scratchColliders.Count; i++)
            {
                Collider c = _scratchColliders[i];
                if (c == null || _ignoredColliders.Contains(c)) continue;
                SetIgnore(c, true);
                _ignoredColliders.Add(c);
            }
            _scratchColliders.Clear();
        }

        private void SetIgnore(Collider other, bool ignore)
        {
            Physics.IgnoreCollision(_capsule, other, ignore);
            for (int i = 0; i < _hits.Length; i++)
                if (_hits[i] != null) Physics.IgnoreCollision(_hits[i], other, ignore);
        }

        private void RestoreCollisions()
        {
            bool wasLocal = _ignoredPlayer != null && _ignoredPlayer == Player.m_localPlayer;
            for (int i = 0; i < _ignoredColliders.Count; i++)
                if (_ignoredColliders[i] != null) SetIgnore(_ignoredColliders[i], false);
            _ignoredColliders.Clear();
            _ignoredPlayer = null;
            _ignoredId = ZDOID.None;
            // The hit spheres never collide with the local player on a proxy layer that needs it (IgnoreLocalPlayer): put that back.
            if (wasLocal && ProxyLayer.NeedsIgnoreCollision) IgnoreLocalPlayer();
        }
    }
}
