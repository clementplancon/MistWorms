using System;
using MistWorms.Colony;
using UnityEngine;

namespace MistWorms.Mass
{
    /// <summary>
    /// Sounds of the masses (spec §12.3, phase 4B-5). Each audible mass makes a "loop" of three layers of vanilla idle sounds (<c>sfx_tick_idle</c>,
    /// <c>sfx_leech_idle</c>, <c>sfx_blob_idle</c>), each layer drawn again every 0.3 to 1.2 s with a random pitch, layers out of phase, the volume
    /// following the logarithm of the count. At most <see cref="MaxAudible"/> masses are audible: the closest ones to the local player, chosen twice a second.
    /// A sound is a queued instance of <see cref="WormEffects"/> (no allocation here); a newborn mass and the start of an engulfing add one more sound each.
    /// </summary>
    internal static class MassAudio
    {
        public const int MaxAudible = 3;
        public const float Range = 30f;
        public const float ScanInterval = 0.5f;
        public const float MinDelay = 0.3f, MaxDelay = 1.2f;
        public const int Layers = 3;

        private static readonly WritheMassVisual[] s_audible = new WritheMassVisual[MaxAudible];
        private static readonly float[] s_audibleDist = new float[MaxAudible];
        private static float s_nextScan;
        private static WormEffects.Limiter s_layerLimit, s_eventLimit;

        /// <summary>Volume factor of a mass of <paramref name="n"/> worms: grows with ln(n), 0.3 to 1.2.</summary>
        public static float VolumeFor(int n) => Mathf.Clamp(0.25f * (1f + Mathf.Log(Math.Max(1, n))), 0.3f, 1.2f);

        /// <summary>Pitch factor: random in [0.85, 1.2], a bigger mass a little lower.</summary>
        public static float PitchFor(int n) => UnityEngine.Random.Range(0.85f, 1.2f) * Mathf.Lerp(1.1f, 0.85f, Mathf.Clamp01(n / 200f));

        private static void Scan(float now)
        {
            s_nextScan = now + ScanInterval;
            Array.Clear(s_audible, 0, MaxAudible);
            Player listener = Player.m_localPlayer;
            if (listener == null || Application.isBatchMode) return;
            Vector3 ear = listener.transform.position;
            float range2 = Range * Range;
            int count = 0;
            var all = MassRenderSystem.All;
            for (int i = 0; i < all.Count; i++)
            {
                WritheMassVisual v = all[i];
                if (v == null || !v.Active) continue;
                float d = (v.Root - ear).sqrMagnitude;
                if (d > range2) continue;
                int k = count < MaxAudible ? count : MaxAudible - 1;
                if (count >= MaxAudible && d >= s_audibleDist[k]) continue;
                while (k > 0 && s_audibleDist[k - 1] > d)
                {
                    s_audible[k] = s_audible[k - 1];
                    s_audibleDist[k] = s_audibleDist[k - 1];
                    k--;
                }
                s_audible[k] = v;
                s_audibleDist[k] = d;
                if (count < MaxAudible) count++;
            }
        }

        /// <summary>Called by the visual every frame: plays the layers of this mass that are due when it is one of the closest.</summary>
        public static void Update(WritheMassVisual v, float now, int n, float[] next)
        {
            if (now >= s_nextScan) Scan(now);
            bool audible = false;
            for (int i = 0; i < MaxAudible; i++) if (ReferenceEquals(s_audible[i], v)) { audible = true; break; }
            if (!audible) return;

            for (int layer = 0; layer < Layers; layer++)
            {
                if (next[layer] <= 0f)
                {
                    // Out of phase: layer l starts after a share of the delay range.
                    next[layer] = now + layer * 0.35f + UnityEngine.Random.Range(0f, MinDelay);
                    continue;
                }
                if (now < next[layer]) continue;
                next[layer] = now + UnityEngine.Random.Range(MinDelay, MaxDelay);
                WormEffects.PlayMassLayer(ref s_layerLimit, layer, v.Root + Vector3.up * 0.3f, VolumeFor(n), PitchFor(n), now);
            }
        }

        public static void PlayBirth(Vector3 where, float now) => WormEffects.PlayForm(ref s_eventLimit, where, now);

        public static void PlayEngulfStart(Vector3 where, float now) => WormEffects.PlayAlert(ref s_eventLimit, where, now);
    }
}
