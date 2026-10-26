#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using MistWorms.Assets;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Dev.Preview;
using MistWorms.Mass;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// V10 (phase 4B-3, pass E; spec §15.5): the movement of the swarm as a colony, in the infested mine. A mass of 50, then of 200, walks 10 to
    /// 15 m along the longest straight free run found around a flat site (AI on, DevGoal toward the motionless player, mw_target set so the arm
    /// reaches for him), is captured from the side and from the front, and walks back with the camera following it at 4 m (clip).
    /// Measured from the worker's probe (world positions of the drawn worms) once the mass walks at full speed and in its free form:
    ///  - TreadRatio: median world speed of the flux worms on top / speed of the mass, in [1.6, 2.4];
    ///  - poured worms (idle flux at speed): median world speed &lt;= 0.25 x the speed of the mass;
    ///  - border worms: at least twice as many in the front sector (+-60 degrees) as in the rear one (beyond 120 degrees);
    ///  - arm: with the player within r + 4 m, a drawn worm lies in the direction of the target, at least 0.3 m beyond the footprint.
    /// The corridor (stream) and the stairs (root without a jump) are checked in S08. Parameters: where=mine (default) or open, torch (1).
    /// </summary>
    internal sealed class V10_MassMoving : IDevScenario
    {
        private static readonly int[] Counts = { 50, 200 };
        private const float MinRun = 6f;               // mine: the longest straight run is short
        private const float MaxRun = 15f;
        private const float OpenMinRun = 20f;          // open ground: the movement criteria need a long run (cruising speed for several seconds)
        private const float OpenMaxRun = 30f;
        private const float RunStep = 0.5f;
        private const float SteadyFraction = 0.9f;       // walking at this part of MoveSpeed counts as full speed
        private const float MeasureSeconds = 5f;           // twice the former window: steady-state samples only (see Snap.Steady)
        private const float ApproachAfterSeconds = 35f;
        private const float ClipSeconds = 5f;
        private const float FollowGap = 3f, FollowHeight = 1.4f;       // clips: 3 m from the edge, 1.4 m up, three quarters front

        public string Name => "V10_MassMoving";

        public IEnumerator Run(ScenarioContext ctx)
        {
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 300f;
            string where = ctx.Param("where", "open");      // the movement is measured on flat open ground; where=mine keeps the old setting
            GameObject torch = null;
            bool movedPlayer = false, clutterHidden = false, timeForced = false;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;
                ctx.Assert("assets_loaded", AssetCatalog.IsReady, true, AssetCatalog.IsReady ? "ready" : AssetCatalog.Error);
                if (!AssetCatalog.IsReady || !MaterialFactory.EnsureBuilt()) yield break;
                if (!MWConfig.Performance.MassWorkerThread.Value) ctx.Log("note: the worker thread is off, results are computed inline");

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                DevPreview.Instance.Clear();
                if (where == "open")
                {
                    ctx.Console($"{VanillaNames.CmdTimeOfDay} 0.35");
                    timeForced = true;
                    if (ClutterSystem.instance != null)
                    {
                        ClutterSystem.instance.enabled = false;
                        ClutterSystem.instance.ClearAll();
                        clutterHidden = true;
                    }
                }
                yield return ctx.WaitFrames(3);

                Player player = Player.m_localPlayer;
                player.SetGodMode(true);
                float rMax = MassMath.Radius(Counts[Counts.Length - 1]);
                Vector3 start = default, dir = Vector3.forward;
                float runLength = 0f;
                bool inMine = false;
                if (where != "open")
                {
                    movedPlayer = true;
                    yield return ctx.TpMine();
                    if (ctx.LastTpMine.Ok)
                    {
                        if (Player.m_localPlayer != null) S18_GroupCoherence.ClearCreatures(ctx, Player.m_localPlayer);   // the creatures of the mine would kill the masses (hostile to every faction)
                        yield return ctx.Wait(1f);
                        var site = new MineSiteResult();
                        yield return MineSite.Find(Player.m_localPlayer.transform.position, rMax + 3f, MineSite.AbsoluteMinClearance, site);
                        if (site.Distribution != null) ctx.Log("mine site: " + site.Distribution);
                        ctx.Assert("mine_site_found", site.Found, "a flat free floor", site.Found ? $"{site.Floor} clearance {site.Clearance:0.0} m" : site.Error);
                        if (site.Found)
                        {
                            BestRun(site.Floor, MaxRun, out start, out dir, out runLength);
                            ctx.Log($"straight run from {start}: {runLength:0.0} m toward {dir}");
                            inMine = runLength >= MinRun;
                            if (!inMine) ctx.Log($"the longest straight run is {runLength:0.0} m, under {MinRun} m: falling back to open ground");
                        }
                    }
                }
                if (!inMine)
                {
                    player.SetGhostMode(false);
                    movedPlayer = false;
                    // A spot with a straight flat run of 25 m or more (a cheap probe per candidate), else the plain open ground search.
                    yield return ctx.MoveToOpenGround(8f, spot => QuickRun(spot) >= 25f);
                    if (!ctx.LastOpenGround.Found)
                    {
                        ctx.Log("no spot with a 25 m straight flat run: plain open ground");
                        yield return ctx.MoveToOpenGround(14f);
                        if (!ctx.LastOpenGround.Found) yield break;
                    }
                    player = Player.m_localPlayer;
                    BestRun(player.transform.position, OpenMaxRun, out start, out dir, out runLength, 2.8f);
                    // A longer run from a few neighbouring points when the first is short.
                    for (int k = 0; k < 8 && runLength < OpenMinRun; k++)
                    {
                        float a = k * Mathf.PI * 0.25f;
                        BestRun(player.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 6f, OpenMaxRun, out Vector3 s2, out Vector3 d2, out float l2, 2.8f);
                        if (l2 > runLength) { start = s2; dir = d2; runLength = l2; }
                    }
                    ctx.Log($"context: open ground, straight flat run of {runLength:0.0} m from {start} toward {dir}");
                    ctx.Assert("open_run_long_enough", runLength >= OpenMinRun, $">= {OpenMinRun} m", runLength.ToString("0.0"));
                }
                else ctx.Log("context: infested mine");
                if (inMine && ctx.ParamBool("torch", true)) torch = SpawnTorch(ctx, start + dir * (0.5f * runLength));

                foreach (int n in Counts)
                {
                    yield return OneMass(ctx, n, start, dir, Mathf.Min(runLength, inMine ? MaxRun : OpenMaxRun));
                    if (Player.m_localPlayer == null) yield break;
                }

                WormRenderer.FrameStats stats = WormRenderer.Last;
                ctx.Assert("renderer_no_dropped_instances", stats.Dropped == 0, 0, stats.Dropped);
            }
            finally
            {
                DevCamera.Provider = null;
                DevCamera.Follow();
                ctx.Console("mw_kill");
                if (clutterHidden && ClutterSystem.instance != null) ClutterSystem.instance.enabled = true;
                if (timeForced) ctx.Console($"{VanillaNames.CmdTimeOfDay} -1");
                Player p = Player.m_localPlayer;
                if (p != null) { p.SetGodMode(false); p.SetGhostMode(false); }
                if (torch != null) { if (ZNetScene.instance != null) ZNetScene.instance.Destroy(torch); else UnityEngine.Object.Destroy(torch); }
            }
        }

        private IEnumerator OneMass(ScenarioContext ctx, int n, Vector3 start, Vector3 dir, float run)
        {
            string tag = "n" + n;
            float r = MassMath.Radius(n);
            float moveSpeed = MWConfig.Mass.MoveSpeed.Value;
            Vector3 far = start + dir * run;
            float stopBefore = r + 0.8f;
            Vector3 goalFar = far - dir * stopBefore;

            // The player stands motionless at the far end; the mass starts at the near end and hunts him.
            var tp = new TeleportOutcome();
            yield return DevWorld.TeleportPlayer(far + Vector3.up * 0.3f, Quaternion.LookRotation(-dir, Vector3.up), false, 30f, tp);
            ctx.Assert(tag + "_player_placed", tp.Ok, "teleported", tp.Ok ? "ok" : tp.Error);
            if (!tp.Ok) yield break;
            Player player = Player.m_localPlayer;
            player.SetGodMode(true);
            yield return ctx.Wait(0.5f);

            WritheMassState mass = MassDev.SpawnExact(start, n, ZDOID.None, freezeAI: true);
            ctx.Assert(tag + "_mass_created", mass != null && mass.Valid, "a valid mass", mass != null ? mass.ZdoId.ToString() : "none");
            if (mass == null) yield break;
            WritheMassVisual visual = mass.GetComponent<WritheMassVisual>();
            WritheMassAI ai = mass.GetComponent<WritheMassAI>();
            if (visual == null || ai == null) yield break;
            mass.SetTarget(player.GetZDOID());
            visual.ProbeEnabled = true;
            yield return ctx.Wait(0.7f);                 // settles on the floor, the layout of the first frames
            mass.DevFrozen = false;
            ai.DevGoal = goalFar;
            float distance0 = Vector3.Distance(start, far);
            ctx.Log($"{tag}: r {r:0.00} m, run {run:0.0} m, goal {goalFar}");

            // ---- wait for full speed in the free form
            float t0 = Time.time;
            while (Time.time - t0 < 8f && mass != null && !(visual.Speed >= SteadyFraction * moveSpeed && visual.StreamK < 0.1f)) yield return null;
            if (mass == null) yield break;
            ctx.Assert(tag + "_reaches_full_speed", visual.Speed >= SteadyFraction * moveSpeed, $">= {SteadyFraction * moveSpeed:0.0} m/s in the free form", $"{visual.Speed:0.00} m/s, stream {visual.StreamK:0.00}");
            if (visual.StreamK > 0.3f) ctx.Log($"{tag}: the mass is in its stream form here, the treadmill criteria are not meaningful");

            // ---- measure over MeasureSeconds from the worker's snapshots. Speeds are taken over a baseline of at least 0.2 s: the flux and border places are
            // recomputed every 2nd frame and the bodies every 6th, so two consecutive snapshots alternate between "moves with the heap" and "jumps ahead".
            // Everything is expressed in the frame of the snapshot itself (its root and heading), not the current one.
            const float Baseline = 0.2f;
            // The walk goes back and forth between the two ends (the run is short) and only the snapshots at full speed in the free form are classified.
            bool toFar = true;
            Vector3 goalNear = start + dir * 0.5f;
            int fluxTotal = 0, fluxTop = 0, fluxIdle = 0, steadySnapshots = 0, pairsSteady = 0, pairsAnimMismatch = 0, pairsTooFast = 0;
            var tread = new List<float>(512);
            var poured = new List<float>(256);
            var history = new List<Snap>(32);
            float lastProcessed = -1f;
            int lastFrame = -1, front = 0, rear = 0, snapshots = 0;
            float speedSum = 0f, sigmaSum = 0f;
            float armExtension = 0f;
            bool armSeen = false;
            float armRampNear = 0f;
            int pouredPairs = 0;
            var secFront = new List<int>(16);
            var secRear = new List<int>(16);
            int bucketFront = 0, bucketRear = 0;
            float bucketStart = -1f;
            const float ShortBaseline = 0.05f;        // a poured worm lies still for only a quarter of a second: its pairs are taken closer
            float tm = Time.time;
            // Back and forth until the treadmill and the poured worms are sampled, then straight to the player: the arm is measured on the approach.
            float armSeenAt = -1f, steadySpeedSum = 0f;
            bool approaching = false;
            while (Time.time - tm < 100f && mass != null && !(approaching && armSeenAt >= 0f && Time.time - armSeenAt > 1f))
            {
                bool enough = (Time.time - tm >= MeasureSeconds && tread.Count >= 300 && poured.Count >= 30) || Time.time - tm >= ApproachAfterSeconds;   // the arm is measured with the player within r + 4 m: the approach starts at the latest after this long
                if (enough && !approaching)
                {
                    approaching = true;
                    toFar = true;
                    ai.DevGoal = goalFar;
                }
                else if (!approaching && Utils.DistanceXZ(mass.transform.position, toFar ? goalFar : goalNear) < 1.5f)
                {
                    toFar = !toFar;
                    ai.DevGoal = toFar ? goalFar : goalNear;
                }
                MassJob job = visual.Job;
                if (job != null && job.ReadyIdx >= 0)
                {
                    MassOutput o = job.Out[job.ReadyIdx];
                    if (o.Valid && o.Probe != null && o.Frame != lastFrame && o.Time - lastProcessed >= 0.015f)     // at most ~60 snapshots a second (the frame rate can be several hundred)
                    {
                        lastFrame = o.Frame;
                        lastProcessed = o.Time;
                        snapshots++;
                        float v = visual.Speed;
                        speedSum += v;
                        sigmaSum += o.Sigma;
                        Vector3 root = o.Root, fwd = o.Forward;
                        Vector3 toTarget = player.transform.position - root; toTarget.y = 0f;
                        float tDist = toTarget.magnitude;
                        Vector3 tDir = tDist > 0.01f ? toTarget / tDist : fwd;
                        bool near = tDist < r + 4f;
                        if (near) armRampNear = Mathf.Max(armRampNear, visual.PseudoRamp);

                        bool steady = v >= SteadyFraction * moveSpeed && visual.StreamK < 0.1f;
                        if (steady) { steadySnapshots++; steadySpeedSum += v; }
                        if (steady)
                        {
                            if (bucketStart < 0f) bucketStart = o.Time;
                            else if (o.Time - bucketStart >= 1f)
                            {
                                secFront.Add(bucketFront);
                                secRear.Add(bucketRear);
                                bucketFront = bucketRear = 0;
                                bucketStart = o.Time;
                            }
                        }
                        if (near && visual.PseudoRamp > 0.5f && armSeenAt < 0f) armSeenAt = Time.time;
                        var snap = new Snap { Steady = steady, Time = o.Time, Pos = new Vector3[MassLayoutConst.SlotCount], Ok = new bool[MassLayoutConst.SlotCount], Anim = new byte[MassLayoutConst.SlotCount], Height = new float[MassLayoutConst.SlotCount] };
                        for (int k = 0; k < o.ProbeCount; k++)
                        {
                            MassProbe pr = o.Probe[k];
                            Vector3 rel = pr.Pos - root; float height = rel.y; rel.y = 0f;
                            float len = rel.magnitude;
                            if (steady && pr.Role == MassLayout.RoleFlux)
                            {
                                fluxTotal++;
                                if (pr.Anim == (byte)WormAnim.Crawl && height > 0.25f) fluxTop++;
                                else if (pr.Anim == (byte)WormAnim.Idle) fluxIdle++;
                            }
                            snap.Pos[pr.Slot] = pr.Pos; snap.Ok[pr.Slot] = pr.Role == MassLayout.RoleFlux; snap.Anim[pr.Slot] = pr.Anim; snap.Height[pr.Slot] = height;
                            if (steady && pr.Role == MassLayout.RoleBorder && len > 0.1f)
                            {
                                float c = Vector3.Dot(rel / len, fwd);
                                if (c > 0.5f) { front++; bucketFront++; } else if (c < -0.5f) { rear++; bucketRear++; }
                            }
                            if (near && len > 0.1f && Vector3.Dot(rel / len, tDir) > 0.866f)
                            {
                                float beyond = len - r * 1.2f;
                                if (beyond > armExtension) armExtension = beyond;
                                armSeen = true;
                            }
                        }

                        // The snapshot at least Baseline seconds older (the nearest one).
                        Snap old = null;
                        for (int h = history.Count - 1; h >= 0; h--) if (o.Time - history[h].Time >= Baseline) { old = history[h]; break; }
                        Snap oldShort = null;
                        for (int h = history.Count - 1; h >= 0; h--) if (o.Time - history[h].Time >= ShortBaseline) { oldShort = history[h]; break; }
                        if (oldShort != null && snap.Steady && oldShort.Steady)
                        {
                            float dts = o.Time - oldShort.Time;
                            for (int k = 0; k < o.ProbeCount; k++)
                            {
                                int sl = o.Probe[k].Slot;
                                if (!snap.Ok[sl] || !oldShort.Ok[sl] || snap.Anim[sl] != (byte)WormAnim.Idle || oldShort.Anim[sl] != (byte)WormAnim.Idle) continue;
                                Vector3 d = snap.Pos[sl] - oldShort.Pos[sl]; d.y = 0f;
                                float sp = d.magnitude / dts;
                                if (sp > 4f * moveSpeed) continue;
                                poured.Add(sp);
                                pouredPairs++;
                            }
                        }
                        if (old != null && snap.Steady && old.Steady)
                        {
                            float dt = o.Time - old.Time;
                            for (int k = 0; k < o.ProbeCount; k++)
                            {
                                int sl = o.Probe[k].Slot;
                                if (!snap.Ok[sl] || !old.Ok[sl]) continue;
                                if (snap.Anim[sl] != old.Anim[sl]) { pairsAnimMismatch++; continue; }
                                Vector3 d = snap.Pos[sl] - old.Pos[sl]; d.y = 0f;
                                float sp = d.magnitude / dt;
                                if (sp > 4f * moveSpeed) { pairsTooFast++; continue; }                    // a worm that went round its cycle
                                pairsSteady++;
                                if (snap.Anim[sl] == (byte)WormAnim.Crawl && snap.Height[sl] > 0.25f && old.Height[sl] > 0.25f) tread.Add(sp);
                            }
                        }
                        history.Add(snap);
                        while (history.Count > 1 && o.Time - history[0].Time > 0.6f) history.RemoveAt(0);
                    }
                }
                yield return null;
            }
            if (mass == null) yield break;

            ctx.Log($"{tag}: classification over {steadySnapshots} of {snapshots} snapshots at full speed in the free form: flux places {fluxTotal}, on top (crawl, > 0.25 m up) {fluxTop}, poured (idle) {fluxIdle}; " +
                    $"pairs 0.2 s apart: matched {pairsSteady}, animation changed {pairsAnimMismatch}, too fast (went round its cycle) {pairsTooFast}; poured pairs {ShortBaseline} s apart {pouredPairs}");
            float vMean = steadySnapshots > 0 ? steadySpeedSum / steadySnapshots : (snapshots > 0 ? speedSum / snapshots : visual.Speed);      // cruising speed: the steady snapshots
            float sigmaMean = snapshots > 0 ? sigmaSum / snapshots : 0f;
            if (secFront.Count > 0)
            {
                var f = new List<int>(secFront); f.Sort();
                var rr = new List<int>(secRear); rr.Sort();
                ctx.Log($"{tag}: border worms per second of steady state ({secFront.Count} s): front min {f[0]}, median {f[f.Count / 2]}, max {f[f.Count - 1]}; rear min {rr[0]}, median {rr[rr.Count / 2]}, max {rr[rr.Count - 1]}");
            }
            float treadMedian = Median(tread), pourMedian = Median(poured);
            float ratio = vMean > 0.1f ? treadMedian / vMean : 0f;
            ctx.Log($"{tag}: over {snapshots} snapshots at {vMean:0.00} m/s (sigma {sigmaMean:0.00}): tread {tread.Count} samples median {treadMedian:0.00} m/s (ratio {ratio:0.00}), poured {poured.Count} samples median {pourMedian:0.00} m/s, border front {front} / rear {rear}, arm reach {(armSeen ? armExtension.ToString("0.00") + " m beyond the footprint" : "none seen")}, stream {visual.StreamK:0.00}");
            if (visual.StreamK < 0.3f)
            {
                ctx.Assert(tag + "_tread_ratio", tread.Count >= 20 && ratio >= 1.6f && ratio <= 2.4f, "median top flux speed / mass speed in [1.6, 2.4]", $"{ratio:0.00} ({tread.Count} samples)");
                ctx.Assert(tag + "_poured_worms_still", poured.Count >= 5 && pourMedian <= 0.25f * vMean, "median poured worm speed <= 0.25 x the mass speed", $"{pourMedian:0.00} m/s ({poured.Count} samples)");
                // Factor 1.5 up to n50, 2 beyond: an isotropic border gives 1.0, the per-second median measured 1.8 to 1.9 at n50 (4.5 at n200), decision 165.
                float factor = n <= 50 ? 1.5f : 2f;
                ctx.Assert(tag + "_border_pulled_forward", front + rear > 0 && front >= factor * Math.Max(rear, 1) - 1, $"front border >= {factor:0.#} x rear border (isotropic = 1.0, measured per-second median 1.8 to 1.9 at n50)", $"front {front}, rear {rear}");
            }
            ctx.Assert(tag + "_arm_toward_target", armSeen && armExtension >= 0.3f && armRampNear > 0.5f, "a drawn worm >= 0.3 m beyond the footprint toward the player within r + 4 m", $"{(armSeen ? armExtension.ToString("0.00") : "none")} m, ramp {armRampNear:0.00} while near, distance now {Vector3.Distance(visual.Root, player.transform.position):0.0} m");

            // ---- captures: side, then front (the camera ahead of the mass, looking back at it)
            Vector3 side = Vector3.Cross(Vector3.up, visual.Forward);
            AimAt(ctx, visual, side, r + FollowGap, FollowHeight);
            yield return ctx.WaitFrames(5);
            yield return ctx.Shot(tag + "_side");
            AimAt(ctx, visual, visual.Forward, r + FollowGap, FollowHeight);
            yield return ctx.WaitFrames(5);
            yield return ctx.Shot(tag + "_front");

            // ---- clip: the mass walks to the farther end, the camera follows at 3 m from its edge, 1.4 m up, three quarters front (clear line every frame)
            Vector3 here = mass.transform.position;
            Vector3 goalNearEnd = start + dir * 0.5f;
            ai.DevGoal = Utils.DistanceXZ(here, goalFar) > Utils.DistanceXZ(here, goalNearEnd) ? goalFar : goalNearEnd;
            Vector3 lastPos = default;
            bool havePos = false;
            DevCamera.Provider = (out Vector3 p, out Vector3 look) =>
            {
                // Three quarters front, 3 m from the edge, 1.4 m up, a clear view every frame (the last good position is kept when none is).
                if (VisualScenario.ClipPose(visual, FollowGap, FollowHeight, out p, out look)) { lastPos = p; havePos = true; }
                else if (havePos) p = lastPos;
            };
            DevCamera.Free(visual.Root + Vector3.up * 3f, visual.Root);
            if (ctx.ParamBool("light", true)) DevCamera.SetFillLight(true);
            yield return ctx.WaitFrames(3);
            float wf = Time.time;
            while (Time.time - wf < 4f && mass != null && visual.Speed < SteadyFraction * moveSpeed) yield return null;       // the clip starts at cruising speed
            ctx.Log($"{tag}: clip starts at {visual.Speed:0.00} m/s, camera {Vector3.Distance(Camera.main != null ? Camera.main.transform.position : Vector3.zero, visual.Root):0.0} m from the centre");
            yield return ctx.Clip(tag, ClipSeconds);
            DevCamera.SetFillLight(false);
            DevCamera.Provider = null;
            DevCamera.Follow();
            ctx.Assert(tag + "_no_fall_rescue", mass == null || mass.FallRescues == 0, 0, mass != null ? mass.FallRescues : -1);
            ctx.Log($"{tag}: travelled {distance0:0.0} m run, AI cost avg {ai.CostAvgMs:0.000} ms");
            visual.ProbeEnabled = false;
            MassDev.Destroy(mass);
            yield return ctx.Wait(0.5f);
        }

        private sealed class Snap
        {
            public float Time;
            public bool Steady;
            public Vector3[] Pos;
            public bool[] Ok;
            public byte[] Anim;
            public float[] Height;
        }

        private static float Median(List<float> v)
        {
            if (v.Count == 0) return 0f;
            v.Sort();
            return v[v.Count / 2];
        }

        private static void AimAt(ScenarioContext ctx, WritheMassVisual v, Vector3 dir, float horizontal, float height)
        {
            Vector3 look = v.Root + Vector3.up * (0.35f * Mathf.Max(0.3f, v.ShownRadius));
            if (!VisualScenario.ClearCamera(v.Root, dir, horizontal, height, look, out Vector3 pos)) ctx.Log("camera: no clear side, using the first");
            DevCamera.Free(pos, look);
        }

        /// <summary>The longest straight run of flat free floor from <paramref name="from"/>, over 24 directions: floor continuity, no wall, free radius.</summary>
        private static void BestRun(Vector3 from, float maxRun, out Vector3 start, out Vector3 dir, out float length, float minFree = 1.2f)
        {
            start = from; dir = Vector3.forward; length = 0f;
            int mask = WormPhysics.SolidMask;
            for (int k = 0; k < 48; k++)
            {
                float a = k * Mathf.PI * 2f / 48f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 prev = from;
                float len = 0f;
                while (len < maxRun)
                {
                    Vector3 q = from + d * (len + RunStep);
                    if (!Physics.Raycast(q + Vector3.up * 0.8f, Vector3.down, out RaycastHit floor, 1.6f, mask, QueryTriggerInteraction.Ignore)) break;
                    if (floor.normal.y < 0.9f || Mathf.Abs(floor.point.y - prev.y) > 0.25f) break;
                    if (Physics.Linecast(prev + Vector3.up * 0.6f, floor.point + Vector3.up * 0.6f, mask, QueryTriggerInteraction.Ignore)) break;
                    if (MineSite.FreeRadius(floor.point, minFree) < minFree) break;
                    prev = floor.point;
                    len += RunStep;
                }
                if (len > length) { length = len; dir = d; }
            }
        }

        /// <summary>Longest straight run of continuous floor with no wall from <paramref name="from"/> over 16 directions, 1 m steps, up to 30 m (a cheap probe).</summary>
        private static float QuickRun(Vector3 from)
        {
            int mask = WormPhysics.SolidMask;
            float best = 0f;
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI * 2f / 16f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 prev = from;
                float len = 0f;
                while (len < 30f)
                {
                    Vector3 q = from + d * (len + 1f);
                    if (!Physics.Raycast(q + Vector3.up * 0.8f, Vector3.down, out RaycastHit floor, 1.6f, mask, QueryTriggerInteraction.Ignore)) break;
                    if (floor.normal.y < 0.9f || Mathf.Abs(floor.point.y - prev.y) > 0.25f) break;
                    if (Physics.Linecast(prev + Vector3.up * 0.6f, floor.point + Vector3.up * 0.6f, mask, QueryTriggerInteraction.Ignore)) break;
                    if ((int)len % 3 == 0 && MineSite.FreeRadius(floor.point, 2.8f) < 2.8f) break;      // room for the biggest swarm (r 2.4 m) to walk
                    prev = floor.point;
                    len += 1f;
                }
                if (len > best) best = len;
                if (best >= 30f) break;
            }
            return best;
        }

        private static GameObject SpawnTorch(ScenarioContext ctx, Vector3 at)
        {
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(VanillaNames.PieceGroundTorch) : null;
            if (prefab == null) { ctx.Log("no ground torch prefab: the mine light only"); return null; }
            Vector3 pos = at;
            if (Physics.Raycast(at + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 3f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore)) pos = hit.point;
            GameObject go = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
            Fireplace fire = go.GetComponent<Fireplace>();
            if (fire != null) fire.AddFuel(fire.m_maxFuel);
            ctx.Log($"torch at {pos}");
            return go;
        }
    }
}
#endif
