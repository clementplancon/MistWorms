using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>
    /// Idle sounds of the nearby worms (spec §12.3): at most <c>Performance.MaxSoundSources</c> worms within <see cref="IdleRange"/>
    /// of the local player, the closest ones, each emitting one vanilla idle sound every 4 to 8 s. The selection runs twice a second
    /// on fixed arrays (no allocation); the sound itself is a template instance made by <see cref="WormEffects"/>.
    /// </summary>
    internal sealed partial class WormColony
    {
        private const float IdleRange = 12f;
        private const float IdleScanInterval = 0.5f;
        private const float IdleMinDelay = 4f;
        private const float IdleMaxDelay = 8f;
        private const int IdleMaxSelected = 32;      // upper bound of Performance.MaxSoundSources

        private float _nextIdleScan;
        private float[] _nextIdleSound;              // per slot, 0 = not scheduled yet; local, not synchronised
        private readonly int[] _idleSelected = new int[IdleMaxSelected];
        private readonly float[] _idleSelectedDist = new float[IdleMaxSelected];

        private void ResetIdleSound(int i)
        {
            if (_nextIdleSound != null && (uint)i < (uint)_nextIdleSound.Length) _nextIdleSound[i] = 0f;
        }

        private void UpdateIdleSounds(float now)
        {
            if (now < _nextIdleScan) return;
            _nextIdleScan = now + IdleScanInterval;

            Player listener = Player.m_localPlayer;
            WormStore s = Store;
            int cap = Mathf.Min(MWConfig.Performance.MaxSoundSources.Value, IdleMaxSelected);
            if (listener == null || s == null || cap <= 0 || s.AliveCount == 0 || Application.isBatchMode) return;
            if (_nextIdleSound == null || _nextIdleSound.Length < s.Capacity) _nextIdleSound = new float[s.Capacity];

            Vector3 ear = listener.transform.position;
            float rangeSqr = IdleRange * IdleRange;
            int count = 0;
            for (int i = 0; i < s.HighWater; i++)
            {
                if (!s.alive[i]) continue;
                byte state = s.state[i];
                if (state == (byte)WormState.Sleeping || state == (byte)WormState.Absorbed || state == (byte)WormState.Dead) continue;
                float d = (s.pos[i] - ear).sqrMagnitude;
                if (d > rangeSqr) continue;

                // Insertion into the sorted list of the closest ones.
                int k = count < cap ? count : cap - 1;
                if (count >= cap && d >= _idleSelectedDist[k]) continue;
                while (k > 0 && _idleSelectedDist[k - 1] > d)
                {
                    _idleSelected[k] = _idleSelected[k - 1];
                    _idleSelectedDist[k] = _idleSelectedDist[k - 1];
                    k--;
                }
                _idleSelected[k] = i;
                _idleSelectedDist[k] = d;
                if (count < cap) count++;
            }

            for (int n = 0; n < count; n++)
            {
                int i = _idleSelected[n];
                if (_nextIdleSound[i] <= 0f)
                {
                    _nextIdleSound[i] = now + NextIdleDelay();
                }
                else if (now >= _nextIdleSound[i])
                {
                    _nextIdleSound[i] = now + NextIdleDelay();
                    WormEffects.PlayIdle(ref _idleFx, s.pos[i] + Vector3.up * 0.1f, now);
                }
            }
        }

        private float NextIdleDelay() => IdleMinDelay + (float)_rng.NextDouble() * (IdleMaxDelay - IdleMinDelay);
    }
}
