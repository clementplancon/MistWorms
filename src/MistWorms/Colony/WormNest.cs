using System;
using System.Collections.Generic;
using MistWorms.Assets;
using MistWorms.Config;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>
    /// A worm nest (spec §8.1, §10.1): a world object with a persistent ZDO, a vanilla <see cref="Destructible"/> for the hit points and the
    /// hits, and this component. The laying is driven by the colony (it owns the worms): the nest holds its index, the time of its last hit
    /// (a ZDO value, read by the colony owner even when another peer owns the nest), the laying timer and its mesh. It draws itself through the
    /// worm renderer (<c>nest_idle</c>). It is never a target (its prefab name starts with MW_, TargetScanner).
    /// </summary>
    internal sealed class WormNest : MonoBehaviour, IWormRenderSource
    {
        /// <summary>Slow +-3 % pulse of the mound (spec §8.1), period in seconds.</summary>
        private const float PulsePeriod = 4f;
        private const float PulseAmount = 0.03f;
        private const float ColonyLookupInterval = 1f;

        private static readonly List<WormNest> s_all = new List<WormNest>();

        /// <summary>Nests loaded on this peer (no iteration while one may be destroyed: copy first).</summary>
        public static List<WormNest> All => s_all;

        private ZNetView _nview;
        private Destructible _destructible;
        private WormColony _colony;
        private bool _renderRegistered;
        private float _nextLookup;
        private WormEffects.Limiter _hitFx;

        /// <summary>Stable index in the colony (the <c>nestIndex</c> of its worms), -1 until known.</summary>
        public int Index { get; private set; } = -1;

        public WormColony Colony => _colony;
        public bool IsValid => _nview != null && _nview.IsValid();
        public bool IsOwner => IsValid && _nview.IsOwner();
        public ZDOID ZdoId => IsValid ? _nview.GetZDO().m_uid : ZDOID.None;
        public Destructible Destructible => _destructible;

        // Laying state, local to the peer that owns the colony (the colony drives it).
        public float LayTimer;
        public float NextCapCheck;
        public int Laid;

        /// <summary>Hit points left: the vanilla "health" of the ZDO, the configured maximum before the first hit.</summary>
        public float Health => IsValid ? _nview.GetZDO().GetFloat(ZDOVars.s_health, MaxHealth) : 0f;
        public float MaxHealth => _destructible != null ? _destructible.m_health : MWConfig.Nest.Health.Value;

        /// <summary>Seconds since the last hit, as seen on network time; a large number when never hit.</summary>
        public float SecondsSinceHit
        {
            get
            {
                if (!IsValid || ZNet.instance == null) return float.MaxValue;
                long ticks = _nview.GetZDO().GetLong(MWNames.ZdoNestHit, 0L);
                if (ticks == 0L) return float.MaxValue;
                return (float)((ZNet.instance.GetTime().Ticks - ticks) / (double)TimeSpan.TicksPerSecond);
            }
        }

        // ------------------------------------------------------------------ life cycle

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            if (_nview == null || !_nview.IsValid())
            {
                // A prefab copy, not a world object (no ZDOMan): nothing to run.
                enabled = false;
                return;
            }
            _destructible = GetComponent<Destructible>();
            if (_destructible != null)
            {
                _destructible.m_health = MWConfig.Nest.Health.Value;
                _destructible.m_damages = MWConfig.BuildDamageModifiers();
                _destructible.m_onDamaged += OnDamaged;
                _destructible.m_onDestroyed += OnDestroyed;
            }

            ZDO zdo = _nview.GetZDO();
            Index = zdo.GetInt(MWNames.ZdoNestIndex, -1);
            s_all.Add(this);
        }

        private void Start()
        {
            // Rendering is client only; a dedicated server has no renderer and no assets.
            if (!enabled || Application.isBatchMode || !AssetCatalog.IsReady) return;
            WormRenderer.Instance.Register(this);
            _renderRegistered = true;
        }

        private void OnDestroy()
        {
            s_all.Remove(this);
            if (_colony != null) _colony.UnregisterNest(this);
            if (_renderRegistered && WormRenderer.Existing != null) WormRenderer.Existing.Unregister(this);
            _renderRegistered = false;
        }

        private void Update()
        {
            if (_colony != null || !IsValid || Time.time < _nextLookup) return;
            _nextLookup = Time.time + ColonyLookupInterval;

            // The colony of a reloaded nest: found by the id the nest carries, once the colony object exists on this peer.
            ZDOID id = _nview.GetZDO().GetZDOID(MWNames.ZdoNestColony);
            WormColony colony = ColonyRegistry.FindById(id);
            if (colony == null || !colony.IsConfigured) return;
            Attach(colony);
            colony.RegisterNest(this);
        }

        private void Attach(WormColony colony)
        {
            _colony = colony;
        }

        // ------------------------------------------------------------------ creation

        /// <summary>Sets the colony and the index of a nest this peer has just created (writes the ZDO).</summary>
        public void Setup(WormColony colony, int index)
        {
            Index = index;
            Attach(colony);
            if (!IsValid || !_nview.IsOwner()) return;
            ZDO zdo = _nview.GetZDO();
            zdo.Set(MWNames.ZdoNestColony, colony.ZdoId);
            zdo.Set(MWNames.ZdoNestIndex, index);
        }

        // ------------------------------------------------------------------ hits (owner of the nest)

        private void OnDamaged()
        {
            if (!IsValid || !_nview.IsOwner()) return;
            if (ZNet.instance != null) _nview.GetZDO().Set(MWNames.ZdoNestHit, ZNet.instance.GetTime().Ticks);
            WormEffects.PlayHit(ref _hitFx, transform.position + Vector3.up * 0.25f, Time.time);
        }

        /// <summary>Called by the Destructible just before the network object is destroyed (owner only): the effect, then the release by the colony.</summary>
        private void OnDestroyed()
        {
            Vector3 where = transform.position;
            try
            {
                WormEffects.PlayNestBurst(where + Vector3.up * 0.2f);
                WormColony colony = _colony ?? ColonyRegistry.FindById(_nview.GetZDO().GetZDOID(MWNames.ZdoNestColony));
                if (colony != null) colony.RequestNestGone(Index, where);
                else Plugin.Log.LogWarning($"[Nest] nest {Index} destroyed at {where} but its colony is not loaded on this peer: no worm released");
            }
            catch (Exception e)
            {
                Plugin.LogException("WormNest.OnDestroyed", e);
            }
        }

        /// <summary>Removes the nest and its ZDO with no effect and no release (dev reset); takes the ownership first.</summary>
        public void DestroyQuiet()
        {
            if (!IsValid) return;
            if (!_nview.IsOwner()) _nview.ClaimOwnership();
            if (ZNetScene.instance != null) ZNetScene.instance.Destroy(gameObject);
            else Destroy(gameObject);
        }

        // ------------------------------------------------------------------ rendering (spec §12.1)

        public void CollectInstances(WormRenderer.Sink sink)
        {
            float t = Time.time + (GetInstanceID() & 0xff) * 0.1f;
            float s = 1f + PulseAmount * Mathf.Sin(t * (2f * Mathf.PI / PulsePeriod));
            sink.AddNest(Matrix4x4.TRS(transform.position, transform.rotation, new Vector3(s, s, s)));
        }
    }
}
