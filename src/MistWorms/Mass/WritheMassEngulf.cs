using System.Collections.Generic;
using MistWorms.Colony;
using UnityEngine;

namespace MistWorms.Mass
{
    /// <summary>
    /// Engulfing, every peer's part (spec §10.2, plan 5, A2): while <c>mw_state</c> says Engulf, the physical collisions between the mass (its capsule and its
    /// hit spheres) and all the colliders of the engulfed player (or creature, plan 6-2) are ignored, so the mass can walk onto it and it is not pushed out;
    /// a structure keeps its collisions: the mass stays against it;
    /// they come back when the mass lets go, changes target or disappears. Driven by the state, so a peer that is neither owner needs no message.
    /// Checked five times a second, no allocation (reused lists).
    /// </summary>
    internal sealed partial class WritheMassState
    {
        private const float IgnorePeriod = 0.2f;

        private GameObject _ignoredObject;                       // the engulfed player or creature
        private Player _ignoredPlayer => _ignoredObject != null ? _ignoredObject.GetComponent<Player>() : null;
        private ZDOID _ignoredId;
        private float _nextIgnore;
        private readonly List<Collider> _ignoredColliders = new List<Collider>(32);
        private readonly List<Collider> _scratchColliders = new List<Collider>(32);

        /// <summary>The player whose colliders this mass currently ignores (dev checks), or null.</summary>
        public Player IgnoredPlayer => _ignoredPlayer;

        /// <summary>The engulfed creature (or player) whose colliders this mass currently ignores (dev checks), or null.</summary>
        public GameObject IgnoredObject => _ignoredObject;

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
                if (_ignoredColliders.Count > 0 || _ignoredObject != null) RestoreCollisions();
                return;
            }

            if (_ignoredObject == null || _ignoredId != id)
            {
                RestoreCollisions();
                GameObject go = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(id) : null;
                if (go == null || go.GetComponent<Character>() == null) return;   // not loaded here, or a structure: collisions stay
                _ignoredObject = go;
                _ignoredId = id;
            }

            // Sweep: a collider added since (equipment) is ignored too.
            _ignoredObject.GetComponentsInChildren<Collider>(true, _scratchColliders);
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
            bool wasLocal = _ignoredObject != null && _ignoredPlayer != null && _ignoredPlayer == Player.m_localPlayer;
            for (int i = 0; i < _ignoredColliders.Count; i++)
                if (_ignoredColliders[i] != null) SetIgnore(_ignoredColliders[i], false);
            _ignoredColliders.Clear();
            _ignoredObject = null;
            _ignoredId = ZDOID.None;
            // The hit spheres never collide with the local player on a proxy layer that needs it (IgnoreLocalPlayer): put that back.
            if (wasLocal && ProxyLayer.NeedsIgnoreCollision) IgnoreLocalPlayer();
        }
    }
}
