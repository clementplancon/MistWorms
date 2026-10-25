using System;
using System.Collections.Generic;
using System.Diagnostics;
using MistWorms.Config;
using MistWorms.Render;
using UnityEngine;
using C = MistWorms.Mass.MassLayoutConst;

namespace MistWorms.Mass
{
    /// <summary>
    /// The single render source of every writhing mass (phase 4B-3, pass D): per-mass sphere culling, distance LOD with hysteresis, the
    /// per-mass caps that slide with the distance, the shared LOD0 budget, a stopwatch per mass, and the hand-over to the layout worker (<see cref="MassJob"/>):
    /// each frame the main thread takes the buffers the worker finished, copies them into the renderer lists in blocks, and publishes a new snapshot.
    /// Bodies always use the 240 triangle LOD1 mesh; flux and border worms use LOD0 up to 12 m and LOD1 beyond. No allocation per frame.
    /// </summary>
    internal sealed class MassRenderSystem : IWormRenderSource
    {
        public const float Lod0Dist = 12f;
        public const float Lod1Dist = 30f;
        public const float LodHyst = 1f;
        public const float CapFadeBand = 4f;
        public const float CameraClearRadius = 0.5f;
        public const float VisFracTau = 0.5f;
        public const float MinLod0Budget = 60f;
        public const float CostTau = 0.5f;
        public const float CostLogPeriod = 10f;

        public static readonly MassRenderSystem Instance = new MassRenderSystem();

        private static readonly List<WritheMassVisual> s_masses = new List<WritheMassVisual>();

        /// <summary>Camera of the last collected frame (the visuals read it in their next Update).</summary>
        public static Vector3 CameraPosition { get; private set; }
        public static bool HaveCamera { get; private set; }
        public static float RenderDistanceHint { get; private set; } = 60f;

        /// <summary>Masses registered on this client.</summary>
        public static int Count => s_masses.Count;

        public static List<WritheMassVisual> All => s_masses;

        /// <summary>Dev: draws nothing from the masses while set (render cost comparisons).</summary>
        public static bool DevHidden;

        private static bool s_quitHooked;

        public static void Add(WritheMassVisual v)
        {
            if (v == null || s_masses.Contains(v)) return;
            s_masses.Add(v);
            WormRenderer.Instance.Register(Instance);     // idempotent
            if (!s_quitHooked)
            {
                s_quitHooked = true;
                Application.quitting += MassWorker.Stop;
            }
        }

        public static void Remove(WritheMassVisual v)
        {
            s_masses.Remove(v);
        }

        private struct Entry
        {
            public WritheMassVisual V;
            public SphereVis Vis;
            public float D;       // distance from the camera to the edge of the swarm, 0 inside
            public int Lod;
            public int Cap;
        }

        private Entry[] _entries = new Entry[16];

        // ------------------------------------------------------------------ pure helpers (unit tested)

        /// <summary>Distance LOD with a hysteresis: from the previous LOD and the distance to the edge of the swarm.</summary>
        public static int NextLod(int prev, float d)
        {
            if (prev <= 0) { if (d < Lod0Dist + LodHyst) return 0; }
            else if (d < Lod0Dist - LodHyst) return 0;
            if (prev <= 1) { return d < Lod1Dist + LodHyst ? 1 : 2; }
            return d < Lod1Dist - LodHyst ? 1 : 2;
        }

        /// <summary>
        /// Worms drawn at most for a swarm whose edge is <paramref name="d"/> metres from the camera: the LOD0 cap up to 10 m, sliding to the LOD1 cap
        /// over 10 to 14 m, then to the LOD2 cap over 28 to 32 m, then to zero over the last 4 m before the render distance.
        /// </summary>
        public static int CapAt(float d, float renderDistance, int lod0Cap)
        {
            float c0 = lod0Cap;
            float c1 = Math.Min((float)C.Lod1Cap, c0);
            float c2 = Math.Min((float)C.Lod2Cap, c1);
            float lo = Lod0Dist - CapFadeBand * 0.5f;
            float hi = Lod1Dist - CapFadeBand * 0.5f;
            float cap;
            if (d <= lo) cap = c0;
            else if (d < lo + CapFadeBand) cap = c0 + (c1 - c0) * ((d - lo) / CapFadeBand);
            else if (d <= hi) cap = c1;
            else if (d < hi + CapFadeBand) cap = c1 + (c2 - c1) * ((d - hi) / CapFadeBand);
            else cap = c2;
            float fadeStart = Math.Max(hi + CapFadeBand, renderDistance - CapFadeBand);
            if (d > fadeStart) cap *= Math.Max(0f, 1f - (d - fadeStart) / CapFadeBand);
            return cap < 0f ? 0 : (int)(cap + 0.5f);
        }

        /// <summary>
        /// World instance matrix of a place in one pass: the columns (R s, U s, F s, pos) of the local pose are multiplied by the rotation of the
        /// frame as they are computed (no intermediate local matrix). Same result as <see cref="MassLayout.ComposeWorld"/>.
        /// </summary>
        public static void ComposeFast(in Matrix4x4 f, in SlotOut o, out Matrix4x4 m)
        {
            float ux = o.Up.X, uy = o.Up.Y, uz = o.Up.Z, fx = o.Fwd.X, fy = o.Fwd.Y, fz = o.Fwd.Z;
            float s = o.Scale;
            float rx = (uy * fz - uz * fy) * s, ry = (uz * fx - ux * fz) * s, rz = (ux * fy - uy * fx) * s;
            ux *= s; uy *= s; uz *= s; fx *= s; fy *= s; fz *= s;
            float f00 = f.m00, f01 = f.m01, f02 = f.m02, f10 = f.m10, f11 = f.m11, f12 = f.m12, f20 = f.m20, f21 = f.m21, f22 = f.m22;
            float px = o.Pos.X, py = o.Pos.Y, pz = o.Pos.Z;
            m.m00 = f00 * rx + f01 * ry + f02 * rz; m.m10 = f10 * rx + f11 * ry + f12 * rz; m.m20 = f20 * rx + f21 * ry + f22 * rz; m.m30 = 0f;
            m.m01 = f00 * ux + f01 * uy + f02 * uz; m.m11 = f10 * ux + f11 * uy + f12 * uz; m.m21 = f20 * ux + f21 * uy + f22 * uz; m.m31 = 0f;
            m.m02 = f00 * fx + f01 * fy + f02 * fz; m.m12 = f10 * fx + f11 * fy + f12 * fz; m.m22 = f20 * fx + f21 * fy + f22 * fz; m.m32 = 0f;
            m.m03 = f00 * px + f01 * py + f02 * pz + f.m03; m.m13 = f10 * px + f11 * py + f12 * pz + f.m13; m.m23 = f20 * px + f21 * py + f22 * pz + f.m23; m.m33 = 1f;
        }

        // ------------------------------------------------------------------ collection

        /// <summary>A snapshot older than this many frames is not drawn (the mass was culled in between).</summary>
        public const int MaxOutputAgeFrames = 4;

        private readonly float[] _planes = new float[24];

        public void CollectInstances(WormRenderer.Sink sink)
        {
            Vector3 cam = sink.CameraPosition;
            float rd = sink.RenderDistance;
            CameraPosition = cam;
            HaveCamera = true;
            RenderDistanceHint = rd;
            if (DevHidden) return;

            int count = s_masses.Count;
            if (count == 0) return;
            if (_entries.Length < count) _entries = new Entry[Math.Max(count, _entries.Length * 2)];
            MassTables.InitFrom(sink);

            int ne = 0;
            for (int i = 0; i < count; i++)
            {
                WritheMassVisual v = s_masses[i];
                if (v == null || !v.Active) continue;
                SphereVis vis = sink.ClassifySphere(v.Center, v.BoundRadius);
                if (vis == SphereVis.Outside) { v.CulledLastFrame = true; v.LastDrawn = 0; v.LastCap = 0; continue; }
                v.CulledLastFrame = false;

                float d = Vector3.Distance(cam, v.Center) - v.P.R;
                if (d < 0f) d = 0f;
                v.Lod = NextLod(v.Lod, d);

                // Insertion by distance: the nearest masses get the shared LOD0 budget first.
                int at = ne++;
                while (at > 0 && _entries[at - 1].D > d) { _entries[at] = _entries[at - 1]; at--; }
                _entries[at].V = v; _entries[at].Vis = vis; _entries[at].D = d; _entries[at].Lod = v.Lod; _entries[at].Cap = 0;
            }

            int lod0Cap = MWConfig.Performance.MaxVisibleSurfaceWorms.Value;
            int budget = C.HeapLod0GlobalCap;
            for (int e = 0; e < ne; e++)
            {
                int lod = _entries[e].Lod;
                int cap = CapAt(_entries[e].D, rd, lod0Cap);
                if (lod == 0)
                {
                    if (budget < MinLod0Budget) lod = 1;          // the shared budget is spent: this one is drawn like a mid distance swarm
                    else
                    {
                        if (cap > budget) cap = budget;
                        budget -= cap;
                    }
                }
                if (lod >= 1) cap = Math.Min(cap, lod == 1 ? Math.Min(C.Lod1Cap, lod0Cap) : Math.Min(C.Lod2Cap, Math.Min(C.Lod1Cap, lod0Cap)));
                _entries[e].Lod = lod;
                _entries[e].Cap = cap;
            }

            sink.CopyFrustumPlanes(_planes);
            bool inline = !MWConfig.Performance.MassWorkerThread.Value;
            float dtVis = Mathf.Max(Time.deltaTime, 0.001f);
            for (int e = 0; e < ne; e++)
            {
                WritheMassVisual v = _entries[e].V;
                long t0 = Stopwatch.GetTimestamp();
                int drawn = Step(v, sink, cam, rd, _entries[e].Vis, _entries[e].Lod, _entries[e].Cap, _entries[e].D, dtVis, inline);
                float ms = (float)((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency);
                v.HeapMs += (ms - v.HeapMs) * (1f - (float)Math.Exp(-dtVis / CostTau));
                v.PerfMain += ms; v.PerfFrames += 1.0;
                sink.ReportHeap(ms, drawn, _entries[e].Lod);
#if MW_DEV
                if (Time.realtimeSinceStartup >= v.NextCostLog)
                {
                    v.NextCostLog = Time.realtimeSinceStartup + CostLogPeriod;
                    Plugin.Log.LogInfo(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "[Mass] cost n={0} lod={1} cap={2} drawn={3} under-threshold={4} refreshed={11}: main {5:0.000} ms; worker {6:0.000} ms = evaluate {7:0.000} + draw {8:0.000}; p={10:0.00}{12}",
                        v.P.N, _entries[e].Lod, _entries[e].Cap, v.LastDrawn, v.EvaluatedSlots, v.HeapMs, v.WorkerMs, v.EvalMs, v.DrawMs, 0f, v.LastP, v.RefreshedSlots,
                        inline ? " (inline)" : " (worker thread)"));
                }
#endif
            }
        }

        /// <summary>
        /// One mass, one frame, on the main thread: observe the finished job, publish the next snapshot (and run it here when the worker is off),
        /// then copy the buffer on screen into the renderer lists. Returns the number of worms copied.
        /// </summary>
        private int Step(WritheMassVisual v, WormRenderer.Sink sink, Vector3 cam, float rd, SphereVis vis, int lod, int cap, float d, float dt, bool inline)
        {
            MassJob job = v.Job;
            v.LastLod = lod;
            v.LastCap = cap;
            if (job == null) return 0;
            if (job.Done) Observe(v, job, dt);

            if (cap > 0)
            {
                if (!job.Busy)
                {
                    v.PrepareEmit(cam);
                    FillSnapshot(v, job, cam, rd, vis, lod, cap, d);
                    job.Busy = true;
                    job.ClearDone();
                    job.WriteIdx = job.ReadyIdx < 0 ? 0 : 1 - job.ReadyIdx;
                    if (inline)
                    {
                        job.Run();
                        Observe(v, job, dt);
                    }
                    else if (!MassWorker.Enqueue(job)) job.Busy = false;     // ring full: next frame
                }
            }
            else
            {
                v.LastDrawn = 0;
                Array.Clear(v.DrawnByRole, 0, 3);
                return 0;
            }

            if (job.Error != null)
            {
                Plugin.Log.LogWarning("[Mass] layout job failed: " + job.Error);
                job.Error = null;
            }
            return CopyOut(v, job, sink);
        }

        private void FillSnapshot(WritheMassVisual v, MassJob job, Vector3 cam, float rd, SphereVis vis, int lod, int cap, float d)
        {
            job.P = v.P;
            job.Frame = v.Frame;
            job.Cam = cam;
            Array.Copy(_planes, job.Planes, 24);
            job.RenderDistance = rd;
            job.Unculled = vis == SphereVis.Inside;
            job.FluxLod1 = lod >= 1;
            float clearReach = v.BoundRadius + CameraClearRadius;
            job.ClearTest = (cam - v.Root).sqrMagnitude < clearReach * clearReach;
            job.ClearSqr = CameraClearRadius * CameraClearRadius;
            job.Cap = cap;
            job.FrameCounter = Time.frameCount;
            job.TreadDist = v.TakeTreadDistance();
            job.RootY = v.Root.y;

            // Select: the threshold that draws about `cap` places, from the visible fraction of the previous frames (all places on the first one).
            int slots = MassLayout.VisibleCount(v.P.N, job.Slots.Count);
            float used = v.VisFrac;
            job.PThreshold = used > 0f ? Mathf.Clamp01(cap / (Math.Max(slots, 1) * used)) : 1f;
            RefreshDivisors(lod, d, out job.DivBody, out job.DivFlux);
        }

        /// <summary>The finished buffer becomes the one on screen; the figures of the worker are copied to the visual.</summary>
        private static void Observe(WritheMassVisual v, MassJob job, float dt)
        {
            job.ReadyIdx = job.WriteIdx;
            job.Busy = false;
            job.ClearDone();
            MassOutput o = job.Out[job.ReadyIdx];
            if (!o.Valid) return;

            float k = 1f - (float)Math.Exp(-dt / CostTau);
            v.EvalMs += (o.EvalMs - v.EvalMs) * k;
            v.DrawMs += (o.DrawMs - v.DrawMs) * k;
            v.WorkerMs += (o.WorkerMs - v.WorkerMs) * k;
            v.PerfJobs += 1.0; v.PerfEval += o.EvalMs; v.PerfDraw += o.DrawMs; v.PerfWorker += o.WorkerMs; v.PerfRefreshed += o.Refreshed; v.PerfDrawn += o.Added;

            float frac = o.NEval > 0 ? (float)o.NVis / o.NEval : 0f;
            float used = v.VisFrac;
            v.VisFrac = used < 0f ? frac : used + (frac - used) * (1f - (float)Math.Exp(-dt / VisFracTau));
            v.LastP = o.P;
            v.EvaluatedSlots = o.NEval;
            v.RefreshedSlots = o.Refreshed;
            v.DrawnByRole[0] = o.D0; v.DrawnByRole[1] = o.D1; v.DrawnByRole[2] = o.D2;
            v.VisibleByRole[0] = o.D0; v.VisibleByRole[1] = o.D1; v.VisibleByRole[2] = o.D2;
            v.LastDrawn = o.Added;
            v.LastVisibleCount = o.NVis;
            v.LastSlotCount = o.SlotCount;
            v.ClosestToCam = o.ClosestSqr == float.MaxValue ? float.MaxValue : (float)Math.Sqrt(o.ClosestSqr);
            v.MaxDrawnHeight = o.MaxY == float.MinValue ? 0f : o.MaxY - v.Root.y;
            Array.Copy(o.Lobes, v.LobeSnapshot, v.LobeSnapshot.Length);
            v.LastEmitFrame = o.Frame;
        }

        /// <summary>Block copy of the buffer on screen into the renderer lists (buffers older than a few frames are not shown).</summary>
        private static int CopyOut(WritheMassVisual v, MassJob job, WormRenderer.Sink sink)
        {
            if (job.ReadyIdx < 0) return 0;
            MassOutput o = job.Out[job.ReadyIdx];
            if (!o.Valid || Time.frameCount - o.Frame > MaxOutputAgeFrames) return 0;
            for (int u = 0; u < o.UsedCount; u++)
            {
                int bi = o.Used[u];
                MassBucket b = o.Buckets[bi];
                int frame = bi % MassTables.MaxFrames;
                int rest = bi / MassTables.MaxFrames;
                int anim = rest % MassTables.Anims;
                int lod = rest / MassTables.Anims;
                var bounds = new Bounds();
                bounds.SetMinMax(b.Min, b.Max);
                sink.AddWormsBulk((WormAnim)anim, lod == 0 ? WormLod.Lod0 : WormLod.Lod1, frame, b.M, b.Count, bounds);
            }
            return o.Added;
        }

        /// <summary>
        /// Refresh divisors (frames between two evaluations of a place): LOD0 bodies 6 and flux and border 2 (the worms of the engulfing sheath
        /// and of the pseudopod arm are always recomputed); LOD1 bodies 8 and flux and border 3; LOD2 bodies 16 and flux and border 6.
        /// </summary>
        public static void RefreshDivisors(int lod, float d, out int body, out int flux)
        {
            if (lod >= 2) { body = 16; flux = 6; }
            else if (lod == 1) { body = 8; flux = 3; }
            else { body = 6; flux = 2; }
        }

        public const float NearFluxDistance = 6f;
    }
}
