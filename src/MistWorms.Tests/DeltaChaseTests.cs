using System;
using System.Collections.Generic;
using MistWorms.Colony;
using UnityEngine;
using Xunit;
using Xunit.Abstractions;

namespace MistWorms.Tests
{
    /// <summary>
    /// A crowd of worms chasing a moving player, sent through the real filter and codec (the same decisions as <c>WormColony.SendDeltas</c>: full entry or compact entry, speed really made) and read by a
    /// mirror (phase 8, decision 228). The noise of the model (every step a worm is pushed sideways by its neighbours, its step length varies) is set so that with the thresholds of decision 213
    /// (0.25 m, 20 degrees, instant speed, 11-byte entries) the model sends about 4.3 entries per worm and second, which is what S17 measured in game (9 629 bytes/s for 200 worms).
    /// </summary>
    public class DeltaChaseTests
    {
        private readonly ITestOutputHelper _out;
        public DeltaChaseTests(ITestOutputHelper output) { _out = output; }

        private static readonly Vector3 Center = new Vector3(1000f, 50f, -300f);
        private const int Worms = 200;

        private struct Result
        {
            public double BytesPerSecond, EntriesPerWormSecond, DeltaShare, Mean, P95, Worst;
        }

        private static double Gauss(System.Random r) => Math.Sqrt(-2.0 * Math.Log(1.0 - r.NextDouble())) * Math.Cos(2.0 * Math.PI * r.NextDouble());

        private static Result Run(int seed, double turnNoise, double speedNoise, bool compact, float seconds = 30f, bool average = true, double wobble = 0.0)
        {
            var rng = new System.Random(seed);
            var owner = new WormStore(Worms + 10);
            var sent = new SentState[Worms + 10];
            var wob = new float[Worms + 10];   // what the worm shows of its heading beyond where it goes: avoidance and separation make it flicker
            for (int i = 0; i < Worms; i++)
            {
                Vector3 at = Center + new Vector3((float)(rng.NextDouble() * 30 - 15), 0f, (float)(rng.NextDouble() * 30 - 15));
                int s = owner.Alloc(at, (float)(rng.NextDouble() * 6.28), WormState.Hunting, 10f, 0f);
                owner.lastSpeed[s] = 2.4f;
            }
            var store = new WormStore(Worms + 10);
            var mirror = new WormMirror(store);
            var w = new DeltaWriter();
            ushort seq = 0;
            long bytes = 0;
            long entries = 0, deltas = 0;
            var errors = new List<float>(20000);
            float dt = 1f / 60f;
            float nextSend = 0f;
            bool first = true;
            for (int frame = 0; frame < (int)(seconds * 60); frame++)
            {
                float now = frame * dt;
                // the player runs in a circle; every worm steps at 15 Hz, staggered, towards it
                Vector3 player = Center + new Vector3(Mathf.Cos(now * 0.05f) * 6f, 0f, Mathf.Sin(now * 0.05f) * 6f);   // walks slowly: the crowd catches up and mills around it
                for (int i = 0; i < Worms; i++)
                {
                    if ((frame + i) % 4 != 0) continue;
                    float step = 4 * dt;
                    Vector3 to = player - owner.pos[i];
                    float want = Mathf.Atan2(to.x, to.z);
                    float diff = (float)Math.IEEERemainder(want - owner.heading[i], 2 * Math.PI);
                    owner.heading[i] += Mathf.Clamp(diff, -3f * step, 3f * step) + (float)(Gauss(rng) * turnNoise);
                    float v = 2.4f * (float)Math.Max(0.0, 1.0 + Gauss(rng) * speedNoise);
                    if (to.sqrMagnitude < 2f * 2f) v *= 0.05f;   // in contact: it strikes more than it walks
                    float h = owner.heading[i];
                    owner.pos[i] += new Vector3(Mathf.Sin(h), 0f, Mathf.Cos(h)) * v * step;
                    owner.lastSpeed[i] = v;
                    wob[i] = (float)(Gauss(rng) * wobble);
                }
                if (now >= nextSend)
                {
                    nextSend = now + 0.2f;
                    var packets = new List<(byte[] b, int len)>();
                    void Flush()
                    {
                        int len = w.Finish();
                        var b = new byte[len];
                        Array.Copy(w.Buffer, b, len);
                        packets.Add((b, len));
                        bytes += len;
                    }
                    w.Begin(first ? (byte)(DeltaFormat.FlagReset | DeltaFormat.FlagEnd) : (byte)0, ++seq);
                    for (int i = 0; i < Worms; i++)
                    {
                        ushort id = owner.netId[i];
                        byte visual = DeltaFormat.VisualCode(owner.state[i], false, false);
                        Vector3 pos = owner.pos[i];
                        if (!first && !DeltaFilter.NeedsSend(sent[i], id, pos, (owner.heading[i] + wob[i]), visual, now)) continue;
                        float speed = first || !average ? owner.lastSpeed[i] : DeltaFilter.SendSpeed(sent[i], id, pos, owner.lastSpeed[i], now);
                        if (!first && compact && DeltaFilter.CanDelta(sent[i], id, pos, visual))
                        {
                            if (!w.HasRoomForDelta) { Flush(); w.Begin(0, ++seq); }
                            Vector3 d = pos - sent[i].Pos;
                            sbyte cx = DeltaFormat.DeltaCode(d.x), cy = DeltaFormat.DeltaCode(d.y), cz = DeltaFormat.DeltaCode(d.z);
                            w.AddDelta(id, cx, cy, cz, (owner.heading[i] + wob[i]), speed);
                            DeltaFilter.RecordDelta(ref sent[i], cx, cy, cz, (owner.heading[i] + wob[i]), speed, now);
                            deltas++;
                        }
                        else
                        {
                            if (!w.HasRoomForEntry) { Flush(); w.Begin(0, ++seq); }
                            w.AddEntry(id, pos - Center, (owner.heading[i] + wob[i]), speed, owner.state[i], false, false);
                            DeltaFilter.Record(ref sent[i], id, pos, (owner.heading[i] + wob[i]), speed, visual, now);
                            entries++;
                        }
                    }
                    if (!w.IsEmpty) Flush();
                    first = false;
                    foreach ((byte[] b, int len) in packets)
                    {
                        Assert.True(DeltaFormat.TryReadHeader(b, len, out DeltaFormat.Header hd));
                        mirror.Apply(b, hd, Center, now, null);
                    }
                }
                mirror.Tick(now, dt, 2.4f);
                if (now > 2f && frame % 6 == 0)
                    for (int i = 0; i < Worms; i++)
                    {
                        int slot = store.FindByNetId(owner.netId[i]);
                        Assert.True(slot >= 0, "every worm of the owner is in the mirror");
                        errors.Add((store.pos[slot] - owner.pos[i]).magnitude);
                    }
            }
            Assert.Equal(0, mirror.UnknownDeltas);
            errors.Sort();
            double sum = 0;
            foreach (float e in errors) sum += e;
            long all = entries + deltas;
            return new Result
            {
                BytesPerSecond = bytes / (double)seconds,
                EntriesPerWormSecond = all / (double)Worms / seconds,
                DeltaShare = all == 0 ? 0 : deltas / (double)all,
                Mean = sum / errors.Count,
                P95 = errors[(int)(errors.Count * 0.95)],
                Worst = errors[errors.Count - 1],
            };
        }

        [Fact]
        public void ACrowdInAChaseFitsTheBandwidthBudgetAtTheErrorTheSpecAsks()
        {
            Result r = Run(21, TurnNoise, SpeedNoise, compact: true, wobble: Wobble);
            _out.WriteLine($"chase: {r.BytesPerSecond:0} B/s, {r.EntriesPerWormSecond:0.00} sends per worm and second, {r.DeltaShare:P0} compact, error mean {r.Mean:0.000} m, p95 {r.P95:0.000} m, worst {r.Worst:0.00} m");
            Assert.True(r.BytesPerSecond <= 4500.0, $"{r.BytesPerSecond:0} bytes/s for 200 worms");
            Assert.True(r.Mean <= 0.3, $"mean error {r.Mean:0.000} m");
            Assert.True(r.P95 <= 1.0, $"p95 error {r.P95:0.000} m");
            Assert.True(r.DeltaShare > 0.8, $"{r.DeltaShare:P0} of the sends are compact");
        }

        [Fact]
        public void CompactEntriesAloneSaveAboutAThirdOfTheBytes()
        {
            Result full = Run(21, TurnNoise, SpeedNoise, compact: false, wobble: Wobble);
            Result compact = Run(21, TurnNoise, SpeedNoise, compact: true, wobble: Wobble);
            Assert.Equal(full.EntriesPerWormSecond, compact.EntriesPerWormSecond, 1);   // same decisions (the quantised base moves them by a hair), only the size differs
            Assert.True(compact.BytesPerSecond < full.BytesPerSecond * 0.7, $"{compact.BytesPerSecond:0} against {full.BytesPerSecond:0}");
        }

        [Fact]
        public void TheMirrorStaysExactWhenADeltaChainRunsForAMinute()
        {
            Result r = Run(5, TurnNoise, SpeedNoise, compact: true, seconds: 60f, wobble: Wobble);
            Assert.True(r.Worst < 2.0, $"worst error {r.Worst:0.00} m");
            Assert.True(r.Mean <= 0.3, $"mean error {r.Mean:0.000} m");
        }

        private const double TurnNoise = 0.05;
        private const double SpeedNoise = 0.2;
        private const double Wobble = 1.0;
    }
}
