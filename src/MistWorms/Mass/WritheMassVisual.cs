using System;
using MistWorms.Assets;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Render;
using UnityEngine;
using C = MistWorms.Mass.MassLayoutConst;

namespace MistWorms.Mass
{
    /// <summary>
    /// Client side state of a writhing mass for the swarm renderer (phase 4B-3, pass D): reads the ZDO (count, Stream, Dissolving, target),
    /// keeps the smoothed root and the ground plane, the speed, the heading and the integrators, and builds the per-frame inputs of
    /// <see cref="MassLayout"/>. Nothing is drawn here: <see cref="MassRenderSystem"/> collects every mass in one place (culling, LOD, caps).
    /// Runs on every client from the ZDO, with no RPC; a dedicated server skips it entirely.
    /// </summary>
    internal sealed class WritheMassVisual : MonoBehaviour
    {
        private const float MinRadius = 0.1f;
        private const float VelTau = 0.25f;
        private const float SigmaTau = 0.3f;
        private const float MaxVisualSpeedFactor = 1.5f;
        private const float HeadingMinSpeed = 0.25f;
        private const float HeadingSlew = 180f;        // deg/s while moving
        private const float TargetTurnIdle = 60f;      // deg/s toward the target at rest
        private const float TargetRefresh = C.TargetRefresh;
        private const float DissolveSeconds = WritheMassState.DissolveDestroyDelay;
        private const float EngulfRate = 1f;           // engulf units per second (smooth, never a pop)
        private const float OdoFold = 4096f;           // multiple of every flipbook length: folding changes no frame
        private const float DetachWindowFrames = 4;    // the detached worm is taken from an evaluation at most this many frames old
        private const int DetachPerLoss = 10;          // MassTransients limiter: the same 10 per second as the colony effects

        private WritheMassState _state;
        private bool _registered;
        private HeapSlots _slots;
        private float _treadDistance;     // distance walked since the last snapshot of the worker (treadmill phase of the lobes)
        private readonly MassRoot _root = new MassRoot();
        private readonly MassGround _ground = new MassGround();
        private float _groundPhase;

        private float _shownRadius = -1f;
        private int _lastN = -1;
        private float _streamK;
        private float _ribbonWidth, _ribbonLength;
        private float _dissolve;

        private Vector3 _prevRoot;
        private Vector3 _vel;
        private float _yaw;
        private bool _yawSet;
        private float _sigma;
        private float _speed;

        private float _tread, _boil, _odoFlux, _odoBorder;
        private float _pseudoRamp;
        private float _engulf;
        private float _forcedEngulf = -1f;
        private Transform _targetTf;
        private float _nextTargetRefresh;
        private ZDOID _targetId;
        private bool _hasTarget;
        private Vector3 _targetPos;

        private WormEffects.Limiter _detachLimit;

        // Published to MassRenderSystem and to the dev tools.
        /// <summary>Per-frame inputs of the layout. Filled by Update, completed with the camera by <see cref="PrepareEmit"/>.</summary>
        internal MassFrameParams P;
        /// <summary>Local -> world frame of the heap (forward, up, left, root).</summary>
        internal Matrix4x4 Frame;
        internal Vector3 Root, Forward, Up, Left;
        internal Vector3 Center;
        internal float BoundRadius;
        internal bool Active;

        /// <summary>The places belong to the layout job (the worker thread runs them): the main thread reads only the output buffers.</summary>
        internal HeapSlots Slots => _slots;
        /// <summary>Hand-over to the layout worker (snapshot in, double buffered output out).</summary>
        internal MassJob Job { get; private set; }
        /// <summary>Copy of the lobes of the buffer on screen (dev tools and scenarios).</summary>
        internal readonly Lobe[] LobeSnapshot = new Lobe[MassLayoutConst.LobeMax];
        internal float Sigma => _sigma;
        internal float Speed => _speed;
        internal float StreamK => _streamK;
        internal float EngulfShown => _engulf;
        internal float PseudoRamp => _pseudoRamp;
        internal bool HasTarget => _hasTarget;
        internal MassGround Ground => _ground;
        internal MassRoot RootState => _root;
        internal WritheMassState State => _state;
        internal int N => _state != null ? _state.N : 0;

        // Collected by MassRenderSystem.
        internal int Lod;                 // hysteresis state of the distance LOD (0, 1, 2)
        internal int LastLod, LastCap, LastDrawn, LastVisibleCount, LastSlotCount;
        internal readonly int[] DrawnByRole = new int[3];
        internal readonly int[] VisibleByRole = new int[3];
        internal float VisFrac = -1f;
        internal float LastP = 1f;
        internal float HeapMs;
        internal float EvalMs, DrawMs, ComposeMs, AddMs;   // EMA of the parts of the per-mass cost (Compose and Add are sampled)
        internal int EvaluatedSlots;     // places under the priority threshold
        internal int RefreshedSlots;     // places really recomputed this frame (the rest came from the cache)
        // Raw per-frame sums for a measurement window (a scenario takes two readings and divides the difference).
        internal double PerfFrames, PerfMain, PerfJobs, PerfEval, PerfDraw, PerfWorker, PerfRefreshed, PerfDrawn;
        /// <summary>Dev: the worker records every drawn worm (see <see cref="MassProbe"/>).</summary>
        internal bool ProbeEnabled;
        internal float WorkerMs;        // EMA of the worker time of one job of this mass
        internal float NextCostLog;
        internal float ClosestToCam = float.MaxValue;
        internal float MaxDrawnHeight;
        internal bool CulledLastFrame;
        internal int LastEmitFrame = -100;

        /// <summary>Radius the swarm is drawn with right now (smoothed toward r(n); the dissolve shrinks it).</summary>
        public float ShownRadius => _shownRadius;

        /// <summary>Dev (mw_mass_engulf): forces <c>engulf</c> in [0, 1] until phase 5 drives it; a negative value gives it back to the state.</summary>
        public float ForcedEngulf { get => _forcedEngulf; set => _forcedEngulf = value; }

        private void Start()
        {
            _state = GetComponent<WritheMassState>();
            _groundPhase = (GetInstanceID() & 0xFF) / 255f;
            // Rendering is client only; a dedicated server has no renderer and no assets.
            if (Application.isBatchMode || !AssetCatalog.IsReady) return;
            MassRenderSystem.Add(this);
            _registered = true;
        }

        private void OnDestroy()
        {
            if (_registered) MassRenderSystem.Remove(this);
            _registered = false;
            Active = false;
        }

        private static float ClockSeconds()
        {
            double t = ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : Time.timeAsDouble;
            return (float)(t % C.ClockPeriod);
        }

        private void EnsureSlots()
        {
            if (_slots != null) return;
            uint seed;
            if (_state.Valid)
            {
                ZDOID id = _state.ZdoId;
                seed = MassLayout.SeedFromIds(id.UserID, id.ID);
            }
            else seed = MassLayout.LowBias32((uint)GetInstanceID());
            _slots = new HeapSlots(seed);
            Job = new MassJob(_slots);
        }

        private void Update()
        {
            Active = false;
            if (_state == null || !_state.Valid || Application.isBatchMode || !_registered) return;
            int n = _state.N;
            if (n <= 0) return;
            float dt = Time.deltaTime;
            if (dt <= 1e-5f) { Active = _slots != null && _root.Started; return; }
            EnsureSlots();

            float now = Time.time;
            bool dissolving = _state.Dissolving || MassStateCodes.IsDissolving(_state.StateCode);

            // ---- radius (spec 4.5: smoothed on change), loss detection
            float target = MassMath.Radius(n);
            if (_shownRadius < 0f) _shownRadius = target;
            else _shownRadius = MassMath.SmoothRadius(_shownRadius, target, dt);
            if (_lastN >= 0 && n < _lastN && !dissolving) DetachLost(_lastN, n);
            _lastN = n;
            _dissolve = Mathf.MoveTowards(_dissolve, dissolving ? 1f : 0f, dt / DissolveSeconds);
            float shrink = 1f - _dissolve * _dissolve * (3f - 2f * _dissolve);
            float r = Mathf.Max(MinRadius, _shownRadius * shrink);

            // ---- dome <-> ribbon (Stream bit)
            bool stream = _state.IsStream;
            if (stream)
            {
                _ribbonWidth = Mathf.Min(Mathf.Max(MassStream.MinRibbonWidth, _state.StreamWidth), 2f * target);
                _ribbonLength = _state.RibbonLength;
            }
            _streamK = Mathf.MoveTowards(_streamK, stream ? 1f : 0f, dt / MassStream.TransitionSeconds);

            // ---- root (smoothed offset), ground plane
            Vector3 body = transform.position;
            float moveSpeed = MWConfig.Mass.MoveSpeed.Value;
            float gy = _ground.Have ? _ground.HeightAt(_root.Root.x, _root.Root.z) : body.y;
            _root.Step(body, dt, moveSpeed, gy, _ground.Have);
            if (_root.Snapped)
            {
                _ground.Force();
                _prevRoot = _root.Root;
                _vel = Vector3.zero;
            }
            Vector3 root = _root.Root;

            // ---- velocity, speed ratio
            Vector3 raw = (root - _prevRoot) / dt;
            _prevRoot = root;
            raw.y = 0f;
            float lp = 1f - (float)Math.Exp(-dt / VelTau);
            _vel += (raw - _vel) * lp;
            float vMax = MaxVisualSpeedFactor * moveSpeed;
            float v = _vel.magnitude;
            if (v > vMax) { _vel *= vMax / v; v = vMax; }
            _speed = v;
            float sigmaTarget = moveSpeed > 1e-3f ? Mathf.Clamp01(v / moveSpeed) : 0f;
            _sigma += (sigmaTarget - _sigma) * (1f - (float)Math.Exp(-dt / SigmaTau));

            // ---- target (read every TargetRefresh seconds)
            UpdateTarget(now);

            // ---- heading
            if (!_yawSet) { _yaw = transform.eulerAngles.y; _yawSet = true; }
            if (v >= HeadingMinSpeed)
                _yaw = Mathf.MoveTowardsAngle(_yaw, Mathf.Atan2(_vel.x, _vel.z) * Mathf.Rad2Deg, HeadingSlew * dt);
            else if (_hasTarget)
            {
                Vector3 to = _targetPos - root;
                if (to.x * to.x + to.z * to.z > 0.01f)
                    _yaw = Mathf.MoveTowardsAngle(_yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, TargetTurnIdle * dt);
            }
            float yaw = _yaw;
            if (_streamK > 0f)
            {
                float k = _streamK * _streamK * (3f - 2f * _streamK);
                yaw = Mathf.LerpAngle(_yaw, transform.eulerAngles.y, k);
            }
            float yr = yaw * Mathf.Deg2Rad;
            Vector3 flat = new Vector3(Mathf.Sin(yr), 0f, Mathf.Cos(yr));

            // ---- ground rays (only when the heap can be seen)
            if (_ground.Due(now))
            {
                float reach = MassRenderSystem.RenderDistanceHint + r + 2f;
                Vector3 cam = MassRenderSystem.CameraPosition;
                bool near = !MassRenderSystem.HaveCamera || (cam - root).sqrMagnitude < reach * reach;
                if (near)
                {
                    Vector3 fl = flat;
                    Vector3 lf = Vector3.Cross(fl, Vector3.up);
                    _ground.Sample(root, body.y, fl, lf, r, WormPhysics.CharacterSolidMask);
                }
                _ground.Schedule(now, _groundPhase);
            }
            _ground.Smooth(dt);

            Vector3 up = _ground.UpAxis();
            Vector3 fwd = flat - up * Vector3.Dot(flat, up);
            fwd = fwd.sqrMagnitude > 1e-8f ? fwd.normalized : flat;
            Vector3 left = Vector3.Cross(fwd, up);
            Root = root; Forward = fwd; Up = up; Left = left;
            Frame = MassLayout.MakeFrame(root, fwd, up);

            // ---- integrators (increments only: no division of a cumulative distance by a changing length)
            float rp = MassLayout.RPlace(n, r);
            float lt = C.TreadPerimeter * rp * (1f + C.TreadStretch * _sigma) / C.TreadSurfaceFrac;
            _tread += v * dt / lt;
            if (_tread > 2048f) _tread -= 2048f;                      // CatRate 0.5: a multiple of 2 changes nothing
            _boil += (1f - _sigma) * dt / C.BoilTref;
            _odoFlux += (v + C.BoilSpeed * (1f - _sigma)) * dt * C.CrawlFramesPerM;
            _odoBorder += (v + C.BorderSpeed * (1f - _sigma)) * dt * C.CrawlFramesPerM;
            if (_odoFlux > OdoFold) _odoFlux -= OdoFold;
            if (_odoBorder > OdoFold) _odoBorder -= OdoFold;
            _treadDistance += v * dt;       // applied by the job (the worker owns the places): AdvanceLobeTread(S, distance, 1)

            // ---- target in the local frame, pseudopod, engulf
            float targetDist = -1f, targetTurns = 0f;
            if (_hasTarget)
            {
                Vector3 d = _targetPos - root;
                float lx = d.x * fwd.x + d.y * fwd.y + d.z * fwd.z;
                float lz = d.x * left.x + d.y * left.y + d.z * left.z;
                targetDist = Mathf.Sqrt(lx * lx + lz * lz);
                targetTurns = Mathf.Atan2(lz, lx) / (2f * Mathf.PI);
            }
            bool wantPseudo = _hasTarget ? targetDist < r + C.PseudoRange : _sigma > C.PseudoMoveMin;
            _pseudoRamp = Mathf.MoveTowards(_pseudoRamp, wantPseudo ? 1f : 0f, dt / C.PseudoRamp);

            float engulfGoal = _forcedEngulf >= 0f ? _forcedEngulf : (MassStateCodes.Mode(_state.StateCode) == MassStateCodes.Engulf ? 1f : 0f);
            if (!_hasTarget) engulfGoal = 0f;
            _engulf = Mathf.MoveTowards(_engulf, Mathf.Clamp01(engulfGoal), EngulfRate * dt);

            // ---- frame parameters
            P.R = r;
            P.N = n;
            P.Sigma = _sigma;
            P.StreamK = _streamK;
            P.RibbonW = _streamK > 0f ? _ribbonWidth : 0f;
            P.RibbonL = _streamK > 0f ? _ribbonLength : 0f;
            P.T = ClockSeconds();
            P.Tread = _tread;
            P.Boil = _boil;
            P.OdoFlux = _odoFlux;
            P.OdoBorder = _odoBorder;
            P.TargetDist = targetDist;
            P.TargetTurns = targetTurns;
            P.PseudoRamp = _pseudoRamp;
            P.Engulf = _engulf;
            P.Blocked0 = _ground.Blocked0; P.Blocked1 = _ground.Blocked1; P.Blocked2 = _ground.Blocked2; P.Blocked3 = _ground.Blocked3;

            // ---- bounding sphere of everything the swarm can reach this frame
            float bound = r * (1f + C.StretchFront + 0.35f) + C.BorderReachMax + 0.9f;
            bound += _pseudoRamp * (C.ArmMax + C.ArmMargin);       // the arm toward the target reaches at most ArmMax past the footprint
            if (_engulf > 0f && targetDist >= 0f) bound = Mathf.Max(bound, targetDist + Mathf.Max(C.BaseRadius, C.EngulfRBottom) + 0.5f);
            if (_streamK > 0f) bound = Mathf.Max(bound, 0.5f * _ribbonLength + 0.5f);
            BoundRadius = bound;
            Center = root + up * (0.25f * r + 0.2f);
            Active = true;
        }

        private void UpdateTarget(float now)
        {
            if (now >= _nextTargetRefresh)
            {
                _nextTargetRefresh = now + TargetRefresh;
                ZDOID id = _state.TargetId;
                if (id != _targetId || _targetTf == null)
                {
                    _targetId = id;
                    _targetTf = null;
                    if (id != ZDOID.None && ZNetScene.instance != null)
                    {
                        GameObject go = ZNetScene.instance.FindInstance(id);
                        if (go != null) _targetTf = go.transform;
                    }
                }
                // Dev: a forced engulf with no hunted creature uses the local player, so the render can be judged without the AI.
                if (_targetTf == null && _forcedEngulf > 0f && Player.m_localPlayer != null) _targetTf = Player.m_localPlayer.transform;
            }
            _hasTarget = _targetTf != null;
            if (_hasTarget) _targetPos = _targetTf.position;
        }

        /// <summary>Completes the per-frame inputs with the camera (direction toward it in the heap frame, for the inner place culling).</summary>
        internal void PrepareEmit(Vector3 cam)
        {
            Vector3 d = cam - Root;
            float dl = d.magnitude;
            if (dl > 0.6f * P.R + 0.3f)
            {
                d /= dl;
                P.HasCam = true;
                P.CamL = new V3(Vector3.Dot(d, Forward), Vector3.Dot(d, Up), Vector3.Dot(d, Left));
            }
            else
            {
                P.HasCam = false;      // inside the swarm: no outside direction, the nearest worms are dropped by distance instead
            }
        }

        // ------------------------------------------------------------------ loss of worms (pass F, base)

        /// <summary>
        /// n went down: a visible worm detaches and falls (<see cref="MassTransients"/>). The removed places [V(new), V(old)) of the last evaluation
        /// are taken first; if none was drawn, a drawn flux or border worm is copied out instead (its place keeps drawing: no "hidden until its
        /// next cycle" API in the layout, reported). At most DetachPerLoss per second, like the other colony effects.
        /// </summary>
        internal float TakeTreadDistance()
        {
            float d = _treadDistance;
            _treadDistance = 0f;
            return d;
        }

        private void DetachLost(int oldN, int newN)
        {
            if (Job == null || Job.ReadyIdx < 0 || Time.frameCount - LastEmitFrame > DetachWindowFrames) return;
            MassOutput o = Job.Out[Job.ReadyIdx];
            if (!o.Valid) return;
            int v0 = Math.Min(MassLayout.VisibleCount(oldN, _slots.Count), LastSlotCount);
            int v1 = MassLayout.VisibleCount(newN, _slots.Count);
            int want = Math.Min(oldN - newN, DetachPerLoss);
            float now = Time.time;
            int done = 0;
            // The drawn worms the worker kept: the removed places [V(new), V(old)) first, then any flux or border worm.
            for (int i = 0; i < o.CandCount && done < want; i++)
                if (o.Cand[i].Slot >= v1 && o.Cand[i].Slot < v0 && TryDetach(o.Cand[i], now)) done++;
            for (int i = 0; i < o.CandCount && done < want; i++)
                if (o.Cand[i].Slot < v1 && o.Cand[i].Role != MassLayout.RoleBody && TryDetach(o.Cand[i], now)) done++;
        }

        private bool TryDetach(in MassCandidate c, float now)
        {
            if (!_detachLimit.Allow(now)) return false;
            float gy = _ground.Have ? _ground.HeightAt(c.M.m03, c.M.m23) : Root.y;
            MassTransients.Spawn(c.M, (WormAnim)c.Anim, c.Lod1 ? WormLod.Lod1 : WormLod.Lod0, c.Frame, gy, now);
            return true;
        }

        /// <summary>Places that last evaluation could show and that are currently shown (dev tools).</summary>
        internal int VisibleTotal => VisibleByRole[0] + VisibleByRole[1] + VisibleByRole[2];
    }
}
