using System;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>What the owner remembers of a worm as the peers last saw it: the basis of the prediction a peer makes (phase 8, decision 213).</summary>
    internal struct SentState
    {
        public bool Valid;
        public ushort NetId;        // a slot reused by another worm invalidates what was remembered of the previous one
        public Vector3 Pos;         // world space
        public float Heading;       // as decoded by the peer (256 steps)
        public float Speed;         // as decoded by the peer (0.05 m/s steps)
        public byte Visual;         // DeltaFormat.VisualCode
        public bool NeedFull;       // a peer was given this worm by a keyframe the others did not get: its base is not Pos, the next send is a full entry (decision 228)
        public float Time;          // when it was sent (the clock of the owner)
    }

    /// <summary>
    /// Which worms the owner sends (spec §11.3, plan 8-1 A4): a worm goes out when its visual state changed, when it is farther than <see cref="PositionTolerance"/> from where a peer that
    /// dead reckons from the last packet (position + heading x speed x elapsed time) thinks it is, when its heading turned by more than <see cref="HeadingTolerance"/>, or when it is awake
    /// and was not sent for <see cref="KeepAlive"/>. A sleeping worm is only sent when its state changes. Worms far from every recipient are only looked at at <see cref="FarInterval"/>.
    /// Pure, unit tested.
    /// </summary>
    internal static class DeltaFilter
    {
        public const float PositionTolerance = 0.35f;                          // metres, on the ground plane
        public const float VerticalTolerance = 0.4f;                           // metres: the peer predicts no vertical motion, it snaps to the floor itself
        public const float HeadingTolerance = 30f * Mathf.Deg2Rad;
        public const float SlowSpeed = 0.5f;                                   // m/s: below it a turn moves the worm by less than the position threshold within a second, only its facing shows
        public const float SlowHeadingTolerance = 60f * Mathf.Deg2Rad;
        public const float KeepAlive = 2f;                                     // seconds, awake worms (a peer drops a worm that is awake and silent for 3 s)
        public const float FarDistance = 30f;                                  // metres from every recipient
        public const float FarInterval = 0.5f;                                 // 2 Hz for the far ones

        /// <summary>Where a peer thinks the worm is, <paramref name="now"/>, from the last packet.</summary>
        public static Vector3 Predict(in SentState s, float now)
        {
            float dt = Mathf.Max(0f, now - s.Time);
            float d = s.Speed * dt;
            return new Vector3(s.Pos.x + Mathf.Sin(s.Heading) * d, s.Pos.y, s.Pos.z + Mathf.Cos(s.Heading) * d);
        }

        /// <summary>Smallest angle between two headings (radians, any range), 0 to pi.</summary>
        public static float HeadingDelta(float a, float b)
        {
            double d = (a - b) % (2.0 * Math.PI);
            if (d > Math.PI) d -= 2.0 * Math.PI;
            else if (d < -Math.PI) d += 2.0 * Math.PI;
            return (float)Math.Abs(d);
        }

        public static bool NeedsSend(in SentState s, ushort netId, Vector3 pos, float heading, byte visual, float now)
        {
            if (!s.Valid || s.NetId != netId || s.Visual != visual) return true;
            bool asleep = (visual & DeltaFormat.VisualStateMask) == (byte)WormState.Sleeping;
            if (asleep) return false;
            if (now - s.Time >= KeepAlive) return true;
            if (HeadingDelta(heading, s.Heading) > (s.Speed < SlowSpeed ? SlowHeadingTolerance : HeadingTolerance)) return true;
            Vector3 p = Predict(s, now);
            float dx = pos.x - p.x, dz = pos.z - p.z;
            if (dx * dx + dz * dz > PositionTolerance * PositionTolerance) return true;
            return Mathf.Abs(pos.y - p.y) > VerticalTolerance;
        }

        /// <summary>Slowest average speed (m/s) that <see cref="SendSpeed"/> still trusts over the instant one.</summary>
        public const float StoppedSpeed = 0.05f;
        public const float MinAverageInterval = 0.15f;

        /// <summary>
        /// The speed to put in a packet: the speed the worm really made since the last packet (distance on the ground over the time), which a peer that carries on at constant heading and
        /// speed predicts better than from the speed of the last simulation step (noisy in a crowd). A worm that is stopped now (or has no earlier packet, or whose last packet is too recent
        /// to average over) sends its instant speed.
        /// </summary>
        public static float SendSpeed(in SentState s, ushort netId, Vector3 pos, float instantSpeed, float now)
        {
            if (instantSpeed < StoppedSpeed || !s.Valid || s.NetId != netId) return instantSpeed;
            float dt = now - s.Time;
            if (dt < MinAverageInterval) return instantSpeed;
            float dx = pos.x - s.Pos.x, dz = pos.z - s.Pos.z;
            return Mathf.Sqrt(dx * dx + dz * dz) / dt;
        }

        /// <summary>True when the worm can go as a compact entry: the peer knows it, with the state it has now, and has the position of <see cref="SentState.Pos"/> as its base, within reach of a delta.</summary>
        public static bool CanDelta(in SentState s, ushort netId, Vector3 pos, byte visual)
        {
            if (!s.Valid || s.NeedFull || s.NetId != netId || s.Visual != visual) return false;
            const float reach = DeltaFormat.MaxDeltaMetres - DeltaFormat.DeltaStep;
            return Mathf.Abs(pos.x - s.Pos.x) <= reach && Mathf.Abs(pos.y - s.Pos.y) <= reach && Mathf.Abs(pos.z - s.Pos.z) <= reach;
        }

        /// <summary>Remembers a compact entry just sent: the base moves by the quantised move, exactly as the peer moves its own.</summary>
        public static void RecordDelta(ref SentState s, sbyte cx, sbyte cy, sbyte cz, float heading, float speed, float now)
        {
            s.Pos += DeltaFormat.DeltaVector(cx, cy, cz);
            s.Heading = SnapshotFormat.HeadingFromCode(SnapshotFormat.HeadingCode(heading));
            s.Speed = DeltaFormat.SpeedFromCode(DeltaFormat.SpeedCode(speed));
            s.Time = now;
        }

        /// <summary>Remembers what was just sent, as the peer decoded it.</summary>
        public static void Record(ref SentState s, ushort netId, Vector3 pos, float heading, float speed, byte visual, float now)
        {
            s.Valid = true;
            s.NetId = netId;
            s.Pos = pos;
            s.Heading = SnapshotFormat.HeadingFromCode(SnapshotFormat.HeadingCode(heading));
            s.Speed = DeltaFormat.SpeedFromCode(DeltaFormat.SpeedCode(speed));
            s.Visual = visual;
            s.NeedFull = false;
            s.Time = now;
        }

        /// <summary>The next time a worm is looked at: every tick near a recipient, <see cref="FarInterval"/> later when it is more than <see cref="FarDistance"/> from all of them.</summary>
        public static float NextEvaluation(float now, float nearestRecipientSqr) =>
            nearestRecipientSqr > FarDistance * FarDistance ? now + FarInterval : 0f;
    }
}
