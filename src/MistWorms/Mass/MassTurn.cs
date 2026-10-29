using System;
using UnityEngine;

namespace MistWorms.Mass
{
    /// <summary>
    /// Pure rules of the turn of a mass (decision 269): the vanilla rotation of a non-player character (<c>Character.UpdateRotation</c>) and the step assist gate.
    /// No allocation, unit tested.
    /// </summary>
    internal static class MassTurn
    {
        /// <summary>Angle (degrees) under which the turn speed of a non-player character falls off with the square root of the remaining angle.</summary>
        public const float SlowdownAngle = 90f;

        /// <summary>Degrees per second of <c>UpdateRotation</c>: turnSpeed * sqrt(clamp01(|delta| / 90)).</summary>
        public static float Rate(float turnSpeed, float delta) => turnSpeed * (float)Math.Sqrt(Mathf.Clamp01(Math.Abs(delta) / SlowdownAngle));

        /// <summary>
        /// Time to turn from <paramref name="start"/> to <paramref name="end"/> degrees of error (closed form): at a constant rate above 90 degrees, then
        /// d(delta)/dt = -T sqrt(delta / 90), i.e. t = 2 sqrt(90) (sqrt(from) - sqrt(end)) / T. Ideal time (fixed step, no attack factor).
        /// </summary>
        public static float SecondsToTurn(float turnSpeed, float start, float end)
        {
            if (turnSpeed <= 0f || start <= end) return 0f;
            float t = 0f;
            float from = start;
            if (from > SlowdownAngle)
            {
                if (end >= SlowdownAngle) return (from - end) / turnSpeed;
                t += (from - SlowdownAngle) / turnSpeed;
                from = SlowdownAngle;
            }
            return t + 2f * (float)Math.Sqrt(SlowdownAngle) * ((float)Math.Sqrt(from) - (float)Math.Sqrt(Math.Max(end, 0f))) / turnSpeed;
        }

        /// <summary>
        /// Step assist gate (decision 269): a window without progress counts toward a stall only when the body faces the wanted direction within
        /// <paramref name="minAngle"/> (the BaseAI.MoveTowards dead zone: farther off, it barely moves because it is turning).
        /// </summary>
        public static bool CountsAsStall(Vector3 wanted, Vector3 forward, float minAngle)
        {
            wanted.y = 0f;
            forward.y = 0f;
            return Vector3.Angle(wanted, forward) < minAngle;
        }
    }
}
