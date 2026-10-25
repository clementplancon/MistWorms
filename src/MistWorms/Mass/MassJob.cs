using System;
using System.Diagnostics;
using System.Threading;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Mass
{
    /// <summary>
    /// Read-only tables of the mesh buckets (lod, anim, frame), filled once on the main thread from the renderer and read by the worker:
    /// frames per (lod, anim) and the largest bounding radius of the meshes of the animation (centre offset included).
    /// </summary>
    internal static class MassTables
    {
        public const int Anims = 6, Lods = 2, MaxFrames = 16;
        public const int BucketCount = Lods * Anims * MaxFrames;

        public static readonly int[] FrameCount = new int[Lods * Anims];
        public static readonly float[] Radius = new float[Lods * Anims];
        public static volatile bool Ready;

        public static int Bucket(int lod, int anim, int frame) => (lod * Anims + anim) * MaxFrames + frame;

        public static void Set(int lod, int anim, int frames, float radius)
        {
            FrameCount[lod * Anims + anim] = frames;
            Radius[lod * Anims + anim] = radius;
        }

        /// <summary>Reads the tables from the renderer (main thread).</summary>
        public static void InitFrom(WormRenderer.Sink sink)
        {
            if (Ready) return;
            WormAnim[] anims = { WormAnim.Crawl, WormAnim.Idle, WormAnim.Coil };
            foreach (WormAnim a in anims)
                for (int lod = 0; lod < Lods; lod++)
                {
                    sink.BucketInfo(a, lod == 0 ? WormLod.Lod0 : WormLod.Lod1, out int frames, out float radius);
                    Set(lod, (int)a, frames, radius);
                }
            Ready = true;
        }
    }

    /// <summary>Matrices of one mesh bucket (lod, anim, frame), ready to be block copied into the renderer list.</summary>
    internal sealed class MassBucket
    {
        public Matrix4x4[] M;
        public int Count;
        public Vector3 Min, Max;
        public bool Used;
    }

    /// <summary>A drawn worm kept for the main thread (the detaching worm of a loss): world pose and mesh choice.</summary>
    internal struct MassCandidate
    {
        public int Slot;
        public byte Role, Anim;
        public bool Lod1;
        public int Frame;
        public Matrix4x4 M;
    }

    /// <summary>Everything the worker produced for one snapshot: buckets of world matrices plus the figures the main thread displays.</summary>
    internal sealed class MassOutput
    {
        public const int CandidateMax = 128;
        public const int CandidateSlotSpan = 96;     // the highest places are the ones a loss of worms removes

        public readonly MassBucket[] Buckets = new MassBucket[MassTables.BucketCount];
        public readonly int[] Used = new int[MassTables.BucketCount];
        public int UsedCount;
        public int Added, D0, D1, D2, NEval, NVis, Refreshed, SlotCount;
        public float ClosestSqr, MaxY, EvalMs, DrawMs, WorkerMs;
        public readonly Lobe[] Lobes = new Lobe[MassLayoutConst.LobeMax];
        public readonly MassCandidate[] Cand = new MassCandidate[CandidateMax];
        public int CandCount;
        public int Frame;            // frame counter of the snapshot
        public float P;              // priority threshold used
        public int RosterV;          // places evaluated (V)
        public bool Valid;

        public MassOutput()
        {
            for (int i = 0; i < Buckets.Length; i++) Buckets[i] = new MassBucket();
        }

        public void Reset()
        {
            for (int k = 0; k < UsedCount; k++)
            {
                MassBucket b = Buckets[Used[k]];
                b.Count = 0; b.Used = false;
            }
            UsedCount = 0;
            Added = D0 = D1 = D2 = NEval = NVis = Refreshed = SlotCount = CandCount = 0;
            ClosestSqr = float.MaxValue;
            MaxY = float.MinValue;
            Valid = false;
        }
    }

    /// <summary>
    /// One mass for the layout worker: the input snapshot written by the main thread, the places (owned by whoever runs the job), and two output
    /// buffers. Protocol, no lock: the main thread writes the input only while <see cref="Busy"/> is false, then sets it and hands the job to the
    /// worker; the worker fills <c>Out[WriteIdx]</c> (never the buffer the main thread reads) and sets <see cref="Done"/>; the main thread sees it, makes
    /// that buffer the one it reads, and clears Busy. A job that is not finished keeps the previous buffer on screen. The job touches no Unity object.
    /// </summary>
    internal sealed class MassJob
    {
        public readonly HeapSlots Slots;
        public readonly MassOutput[] Out = { new MassOutput(), new MassOutput() };

        // ---- input snapshot (main thread, while !Busy)
        public MassFrameParams P;
        public Matrix4x4 Frame;
        public Vector3 Cam;
        public readonly float[] Planes = new float[24];
        public float RenderDistance;
        public bool Unculled;
        public bool FluxLod1;
        public bool ClearTest;
        public float ClearSqr;
        public int Cap;
        public float PThreshold;
        public int FrameCounter;
        public int DivBody = 1, DivFlux = 1;
        public float TreadDist;       // distance walked since the last snapshot (treadmill phase of the lobes)
        public float RootY;

        // ---- protocol
        public bool Busy;                         // main thread only
        public int WriteIdx;                      // main thread sets, worker reads
        public int ReadyIdx = -1;                 // main thread only: buffer on screen
        private int _done;
        public bool Done => Volatile.Read(ref _done) != 0;
        public void ClearDone() { Volatile.Write(ref _done, 0); }
        public string Error;                      // worker writes before Done, main reads after
        public long Runs;

        public MassJob(HeapSlots slots) { Slots = slots; }

        /// <summary>Runs the snapshot: evaluation, selection, world matrices, per-instance culling, buckets. On the worker thread or inline.</summary>
        public void Run()
        {
            long t0 = Stopwatch.GetTimestamp();
            MassOutput o = Out[WriteIdx];
            try
            {
                o.Reset();
                o.Frame = FrameCounter;
                o.P = PThreshold;
                if (TreadDist > 0f) MassLayout.AdvanceLobeTread(Slots, TreadDist, 1f);
                long t1 = Stopwatch.GetTimestamp();
                int count = MassLayout.Evaluate(P, Slots, PThreshold, FrameCounter, DivBody, DivFlux);
                long t2 = Stopwatch.GetTimestamp();
                var sink = new WorkerSink { Job = this, O = o, Frame = Frame, Cam = Cam, ClosestSqr = float.MaxValue, MaxY = float.MinValue, SlotLow = count - MassOutput.CandidateSlotSpan };
                sink.RD = RenderDistance;
                MassLayout.Draw(ref sink, Slots, count, Cap, PThreshold, out int ne, out int nv);
                long t3 = Stopwatch.GetTimestamp();

                int[] stamps = Slots.EvalFrame;
                int refreshed = 0;
                for (int i = 0; i < count; i++) if (stamps[i] == FrameCounter) refreshed++;
                o.Added = sink.Added; o.D0 = sink.D0; o.D1 = sink.D1; o.D2 = sink.D2;
                o.NEval = ne; o.NVis = nv; o.Refreshed = refreshed; o.SlotCount = count; o.RosterV = count;
                o.ClosestSqr = sink.ClosestSqr; o.MaxY = sink.MaxY;
                Array.Copy(Slots.Lobes, o.Lobes, o.Lobes.Length);
                double ms = 1000.0 / Stopwatch.Frequency;
                o.EvalMs = (float)((t2 - t1) * ms);
                o.DrawMs = (float)((t3 - t2) * ms);
                o.Valid = true;
            }
            catch (Exception e)
            {
                Error = e.GetType().Name + ": " + e.Message;
                o.Valid = false;
            }
            o.WorkerMs = (float)((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency);
            Runs++;
            Volatile.Write(ref _done, 1);
        }

        /// <summary>Instance sink of the worker: world matrix, camera clearing, per-instance culling, bucket and candidate bookkeeping.</summary>
        private struct WorkerSink : IHeapSink
        {
            public MassJob Job;
            public MassOutput O;
            public Matrix4x4 Frame;
            public Vector3 Cam;
            public float RD;
            public int Added, D0, D1, D2;
            public float ClosestSqr, MaxY;
            public int SlotLow;

            public void AddWorm(in SlotOut s)
            {
                Matrix4x4 m;
                MassRenderSystem.ComposeFast(Frame, s, out m);
                float px = m.m03, py = m.m13, pz = m.m23;
                float dx = px - Cam.x, dy = py - Cam.y, dz = pz - Cam.z;
                float sq = dx * dx + dy * dy + dz * dz;
                if (Job.ClearTest)
                {
                    if (sq < Job.ClearSqr) return;
                    if (sq < ClosestSqr) ClosestSqr = sq;
                }

                bool lod1 = Job.FluxLod1 || !s.Lod0;
                int lod = lod1 ? 1 : 0;
                int anim = s.Anim;
                int tb = lod * MassTables.Anims + anim;
                int fc = MassTables.FrameCount[tb];
                if (fc <= 0) return;
                int frame = lod1 ? s.Frame >> 1 : s.Frame;
                frame = frame % fc;
                if (frame < 0) frame += fc;
                if (frame >= MassTables.MaxFrames) frame %= MassTables.MaxFrames;
                float rad = MassTables.Radius[tb] * s.Scale;

                if (!Job.Unculled)
                {
                    float reach = RD + rad;
                    if (sq > reach * reach) return;
                    float[] pl = Job.Planes;
                    for (int k = 0; k < 24; k += 4)
                        if (pl[k] * px + pl[k + 1] * py + pl[k + 2] * pz + pl[k + 3] < -rad) return;
                }

                MassBucket b = O.Buckets[MassTables.Bucket(lod, anim, frame)];
                if (!b.Used)
                {
                    b.Used = true;
                    b.Count = 0;
                    b.Min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                    b.Max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                    O.Used[O.UsedCount++] = MassTables.Bucket(lod, anim, frame);
                    if (b.M == null) b.M = new Matrix4x4[32];
                }
                if (b.Count == b.M.Length) Array.Resize(ref b.M, b.Count * 2);    // amortised: only while the working set grows
                b.M[b.Count++] = m;
                if (px - rad < b.Min.x) b.Min.x = px - rad;
                if (py - rad < b.Min.y) b.Min.y = py - rad;
                if (pz - rad < b.Min.z) b.Min.z = pz - rad;
                if (px + rad > b.Max.x) b.Max.x = px + rad;
                if (py + rad > b.Max.y) b.Max.y = py + rad;
                if (pz + rad > b.Max.z) b.Max.z = pz + rad;

                if (py > MaxY) MaxY = py;
                Added++;
                if (s.Role == MassLayout.RoleBody) D0++; else if (s.Role == MassLayout.RoleFlux) D1++; else D2++;

                // The detaching worm of a loss is taken from the highest places, or from any flux or border worm.
                if (O.CandCount < MassOutput.CandidateMax && (s.I >= SlotLow || (s.Role != MassLayout.RoleBody && O.CandCount < MassOutput.CandidateMax / 2)))
                {
                    ref MassCandidate c = ref O.Cand[O.CandCount++];
                    c.Slot = s.I; c.Role = s.Role; c.Anim = (byte)anim; c.Lod1 = lod1; c.Frame = frame; c.M = m;
                }
            }
        }
    }
}
