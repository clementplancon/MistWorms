using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>
    /// Vanilla effects played for a worm (spec §12.4): the death puff and a light hit splash. Both are fire-and-forget prefab instances
    /// created on the colony owner only; the prefabs' own TimedDestruction removes them. Phase 8 decides how other peers see them
    /// (a prefab that carries a ZNetView is replicated by the game by itself, the scale is not).
    /// </summary>
    internal static class WormEffects
    {
        /// <summary>At most this many of each effect per second and per colony (spec §12.4).</summary>
        public const int MaxPerSecond = 10;

        /// <summary>The tick's death effect is sized for a 1.2 m creature; a worm is 0.4 m (decisions.md 53, to judge in game).</summary>
        public const float DeathScale = 0.4f;
        public const float HitScale = 0.4f;

        private static GameObject s_death;
        private static bool s_deathResolved;
        private static GameObject s_hit;
        private static bool s_hitResolved;
        private static ZNetScene s_scene;   // prefabs belong to a session: resolve again when the scene is replaced

        /// <summary>Rate limiter state of one colony and one effect.</summary>
        public struct Limiter
        {
            public float WindowStart;
            public int Count;

            public bool Allow(float now)
            {
                if (now - WindowStart >= 1f)
                {
                    WindowStart = now;
                    Count = 0;
                }
                if (Count >= MaxPerSecond) return false;
                Count++;
                return true;
            }
        }

        private static void CheckScene()
        {
            if (s_scene == ZNetScene.instance) return;
            s_scene = ZNetScene.instance;
            s_deathResolved = false;
            s_hitResolved = false;
            s_death = null;
            s_hit = null;
        }

        public static void PlayDeath(ref Limiter limiter, Vector3 position, float now)
        {
            CheckScene();
            if (!s_deathResolved)
            {
                s_deathResolved = true;
                s_death = ResolveDeath();
            }
            Spawn(s_death, ref limiter, position, DeathScale, now);
        }

        public static void PlayHit(ref Limiter limiter, Vector3 position, float now)
        {
            CheckScene();
            if (!s_hitResolved)
            {
                s_hitResolved = true;
                s_hit = Find(VanillaNames.FxTickBloodHit);
                if (s_hit == null) Plugin.Log.LogWarning($"[Effects] {VanillaNames.FxTickBloodHit} not found: worms show no hit splash");
            }
            Spawn(s_hit, ref limiter, position, HitScale, now);
        }

        private static GameObject ResolveDeath()
        {
            // Preferred tick death; a ragdoll in the effect would leave a corpse, so then the baby seeker's is used (spec §12.4, plan B4).
            foreach (string name in new[] { VanillaNames.VfxWormDeath, VanillaNames.FxBabySeekerDeath })
            {
                GameObject prefab = Find(name);
                if (prefab == null) continue;
                if (prefab.GetComponentInChildren<Ragdoll>(true) != null)
                {
                    Plugin.Log.LogWarning($"[Effects] {name} carries a Ragdoll, skipped");
                    continue;
                }
                Plugin.Log.LogInfo($"[Effects] death effect {name}: ZNetView {(prefab.GetComponent<ZNetView>() != null ? "yes (replicated by the game)" : "no (local only)")}, scale {DeathScale}");
                return prefab;
            }
            Plugin.Log.LogWarning("[Effects] no usable death effect prefab: worms die silently");
            return null;
        }

        private static GameObject Find(string name) => ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;

        private static void Spawn(GameObject prefab, ref Limiter limiter, Vector3 position, float scale, float now)
        {
            if (prefab == null || !limiter.Allow(now)) return;
            GameObject fx = Object.Instantiate(prefab, position, Quaternion.identity);
            if (fx != null) fx.transform.localScale = prefab.transform.localScale * scale;
        }
    }
}
