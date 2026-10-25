using System;
using System.Threading;

namespace MistWorms.Mass
{
    /// <summary>
    /// The single background thread that runs <see cref="MassJob"/>s (phase 4B-3): the layout of every mass is pure computation, so it is evaluated
    /// beside the main thread and delivered one frame later. A fixed ring (one producer, the main thread; one consumer, this thread) and an
    /// AutoResetEvent: no lock, no ThreadPool task, no allocation per frame. The thread is a background one and is stopped at quit.
    /// </summary>
    internal static class MassWorker
    {
        private const int Capacity = 256;

        private static readonly MassJob[] s_ring = new MassJob[Capacity];
        private static int s_head;      // next job to run (the worker moves it)
        private static int s_tail;      // next free place (the main thread moves it)
        private static readonly AutoResetEvent s_signal = new AutoResetEvent(false);
        private static Thread s_thread;
        private static volatile bool s_quit;
        private static long s_jobs;

        /// <summary>Jobs run so far (dev).</summary>
        public static long JobsRun => Interlocked.Read(ref s_jobs);

        public static bool Running => s_thread != null && s_thread.IsAlive;

        /// <summary>Queues a job. False when the ring is full (the caller keeps the job idle and tries again next frame). Main thread only.</summary>
        public static bool Enqueue(MassJob job)
        {
            EnsureStarted();
            int t = s_tail;
            int next = (t + 1) % Capacity;
            if (next == Volatile.Read(ref s_head)) return false;
            s_ring[t] = job;
            Volatile.Write(ref s_tail, next);
            s_signal.Set();
            return true;
        }

        private static void EnsureStarted()
        {
            if (s_thread != null && s_thread.IsAlive) return;
            s_quit = false;
            s_thread = new Thread(Loop) { IsBackground = true, Name = "MistWorms.MassWorker", Priority = ThreadPriority.BelowNormal };
            s_thread.Start();
        }

        private static void Loop()
        {
            while (!s_quit)
            {
                s_signal.WaitOne(250);
                while (!s_quit)
                {
                    int h = s_head;
                    if (h == Volatile.Read(ref s_tail)) break;
                    MassJob job = s_ring[h];
                    s_ring[h] = null;
                    Volatile.Write(ref s_head, (h + 1) % Capacity);
                    if (job == null) continue;
                    job.Run();
                    Interlocked.Increment(ref s_jobs);
                }
            }
        }

        /// <summary>Stops the thread (waits a moment for the job in progress). Called when the application quits and by tests.</summary>
        public static void Stop()
        {
            Thread t = s_thread;
            if (t == null) return;
            s_quit = true;
            s_signal.Set();
            try { t.Join(1000); } catch (Exception) { /* shutting down */ }
            s_thread = null;
        }
    }
}
