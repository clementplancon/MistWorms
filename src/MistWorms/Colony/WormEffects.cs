using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MistWorms.Colony
{
    /// <summary>
    /// Vanilla effects and sounds played for a worm (spec §12.3, §12.4; phase 4A-2): green blood on a hit and on death, plus
    /// a few wet sounds. Everything is a local clone of a vanilla prefab, made once: it is built disabled under a hidden holder,
    /// its ZNetView is removed before any activation (so the game never registers a network object), and each play is an
    /// instance of that template (the prefab's own TimedDestruction removes it). Nothing here is replicated: hits play on the
    /// peer that lands them (see <c>WormColony.ReceiveWormHit</c>), the death and strike sounds on the colony owner until phase 8.
    /// </summary>
    internal static class WormEffects
    {
        /// <summary>At most this many of each effect per second and per colony (spec §12.4).</summary>
        public const int MaxPerSecond = 10;

        /// <summary>
        /// Spatial scale applied to the particle systems when the template is built (the seeker's effects are sized for a 2 m creature, the
        /// target jet is about 0.3 m): sizes, speeds, gravity, shape and forces. The instance transform stays at 1.
        /// </summary>
        public const float HitScale = 0.35f;
        public const float DeathScale = 0.45f;

        /// <summary>Burst and emission counts of a small mob (the death has about 1.5 times the hit).</summary>
        public const float HitCountFactor = 0.3f;
        public const float DeathCountFactor = 0.45f;

        /// <summary>Particle size caps in metres (spec §12.4: 1 to 3 cm particles), the ground decal excepted.</summary>
        public const float HitMaxParticleSize = 0.05f;
        public const float DeathMaxParticleSize = 0.08f;

        /// <summary>Particle caps (maxParticles) and the ground decal size in metres.</summary>
        public const int HitMaxParticles = 30;
        public const int DeathMaxParticles = 60;
        public const float HitDecalSize = 0.25f;
        public const float DeathDecalSize = 0.4f;
        /// <summary>Range bounded by construction (every system but the decals): life time cap in seconds and speed limit in m/s, with its damping.</summary>
        public const float HitMaxLifetime = 0.5f;
        public const float DeathMaxLifetime = 0.8f;
        public const float HitSpeedLimit = 1.0f;
        public const float DeathSpeedLimit = 0.8f;
        public const float HitSpeedDampen = 0.6f;
        public const float DeathSpeedDampen = 1.0f;      // immediate clamp
        public const float HitShapeFactor = 1f;          // extra factor on the emission shape radius and scale
        public const float DeathShapeFactor = 0.5f;

        /// <summary>Systems (by name) switched off in both templates when a measure shows they still fly too far (clouds, smoke...). Empty until V08 names one.</summary>
        private static readonly string[] DisabledSystems = { };

        private static readonly string[] DecalNames = { "blood_decal", "blob_splat (1)" };

        /// <summary>Sounds farther than this from the local player are not instantiated at all.</summary>
        public const float SoundRange = 30f;

        private const int MaxAttempts = 5;           // a prefab missing from ZNetScene is looked for again this many times
        private const float RetryInterval = 1f;
        private const string HolderName = "MW_FxTemplates";

        /// <summary>Acid yellow-green of the worm's blood (spec §12.4): the two ends of the start colour gradient, alpha kept from the original.</summary>
        private static readonly Color BloodGreenA = new Color(0.62f, 0.90f, 0.14f, 1f);
        private static readonly Color BloodGreenB = new Color(0.40f, 0.78f, 0.10f, 1f);

        /// <summary>One template: where it comes from, how it is prepared, and its resolution state.</summary>
        private sealed class Slot
        {
            public readonly string What;
            public readonly string[] Names;            // tried in order; the first one is the wanted prefab, the others the old behaviour
            public readonly Action<GameObject> Prepare;
            public GameObject Template;
            public string Source;
            public int Attempts;
            public bool GaveUp;
            public bool Warmed;
            public float NextTry;

            public Slot(string what, string[] names, Action<GameObject> prepare)
            {
                What = what;
                Names = names;
                Prepare = prepare;
            }

            public void Reset()
            {
                if (Template != null) Object.Destroy(Template);
                Template = null;
                Source = null;
                Attempts = 0;
                GaveUp = false;
                Warmed = false;
                NextTry = 0f;
            }
        }

        private static readonly Slot s_hit = new Slot("hit effect", new[] { VanillaNames.FxSeekerHurt, VanillaNames.FxTickBloodHit }, PrepareHit);
        private static readonly Slot s_death = new Slot("death effect", new[] { VanillaNames.FxSeekerDeath, VanillaNames.VfxWormDeath, VanillaNames.FxBabySeekerDeath }, PrepareDeath);
        private static readonly Slot s_hitSound = new Slot("hit sound", new[] { VanillaNames.SfxTickHurt }, null);
        private static readonly Slot s_deathSound = new Slot("death sound", new[] { VanillaNames.SfxLeechDeath }, null);
        private static readonly Slot s_strikeSound = new Slot("strike sound", new[] { VanillaNames.SfxTickAttackJump }, null);
        private static readonly Slot s_idleSound = new Slot("idle sound", new[] { VanillaNames.SfxTickIdle }, null);
        private static readonly Slot[] s_all = { s_hit, s_death, s_hitSound, s_deathSound, s_strikeSound, s_idleSound };

        private static GameObject s_holder;
        private static ZNetScene s_scene;   // prefabs belong to a session: resolve again when the scene is replaced

        /// <summary>Number of particle systems recoloured in the death template, and the pieces removed (log and dev assertions).</summary>
        public static int DeathRecolored { get; private set; }

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

        /// <summary>
        /// Kept for the colonies' first tick: the templates are no longer built there (a tick can run inside an animation event or a rendering
        /// callback, where DestroyImmediate is refused); <see cref="Pump"/> builds and warms them from the plugin's Update.
        /// </summary>
        public static void Prewarm()
        {
        }

        // ------------------------------------------------------------------ plays

        // Why a queue: an effect is asked for from places where Unity forbids creating or destroying objects (animation events: a melee swing is
        // an event of the animator; rendering callbacks: a worm landing). The Play* calls only note the request; `Pump`, called from the plugin's
        // Update, builds the templates (they need DestroyImmediate) and plays what is pending. A request older than PendingLife is dropped.

        private enum Kind : byte { Hit, Death, Strike, Idle }

        private struct Pending
        {
            public Kind Kind;
            public Vector3 Position;
            public float Expire;
        }

        public const int PendingMax = 64;
        public const float PendingLife = 2f;

        private static readonly Pending[] s_pending = new Pending[PendingMax];
        private static int s_pendingHead, s_pendingCount;
        private static bool s_warnedPlay;

        /// <summary>Requests waiting for the next Pump (dev checks).</summary>
        public static int PendingCount => s_pendingCount;

        private static void Enqueue(Kind kind, Vector3 position)
        {
            if (Application.isBatchMode) return;
            if (s_pendingCount >= PendingMax) return;      // a burst beyond the limiter: drop
            int at = (s_pendingHead + s_pendingCount) % PendingMax;
            s_pending[at] = new Pending { Kind = kind, Position = position, Expire = Time.unscaledTime + PendingLife };
            s_pendingCount++;
        }

        /// <summary>Green splash and hurt sound at <paramref name="position"/>, played by the peer that landed the hit (deferred to the next Update; never throws).</summary>
        public static void PlayHit(ref Limiter limiter, Vector3 position, float now)
        {
            if (limiter.Allow(now)) Enqueue(Kind.Hit, position);
        }

        public static void PlayDeath(ref Limiter limiter, Vector3 position, float now)
        {
            if (limiter.Allow(now)) Enqueue(Kind.Death, position);
        }

        public static void PlayStrike(ref Limiter limiter, Vector3 position, float now)
        {
            if (limiter.Allow(now)) Enqueue(Kind.Strike, position);
        }

        public static void PlayIdle(ref Limiter limiter, Vector3 position, float now)
        {
            if (limiter.Allow(now)) Enqueue(Kind.Idle, position);
        }

        /// <summary>
        /// Called from a normal Update (the plugin's): builds the templates once the ZNetScene is ready (again after a scene change), then plays the
        /// pending effects. Nothing here throws.
        /// </summary>
        public static void Pump()
        {
            if (Application.isBatchMode) return;
            try
            {
                if (ZNetScene.instance == null) return;
                CheckScene();
                foreach (Slot slot in s_all)
                {
                    if (slot.Template == null && !slot.GaveUp) Resolve(slot);
                    if (slot.Template != null && !slot.Warmed)
                    {
                        // Instantiates the template once and destroys the copy, so the first real play has no first-use cost.
                        slot.Warmed = true;
                        Object.Destroy(Object.Instantiate(slot.Template, Vector3.zero, Quaternion.identity));
                    }
                }

                float now = Time.unscaledTime;
                int n = s_pendingCount;
                for (int k = 0; k < n; k++)
                {
                    Pending p = s_pending[s_pendingHead];
                    s_pendingHead = (s_pendingHead + 1) % PendingMax;
                    s_pendingCount--;
                    if (p.Expire < now) continue;
                    PlayNow(p.Kind, p.Position);
                }
            }
            catch (Exception e)
            {
                s_pendingCount = 0;
                if (!s_warnedPlay)
                {
                    s_warnedPlay = true;
                    Plugin.LogException("WormEffects.Pump", e);
                }
            }
        }

        private static void PlayNow(Kind kind, Vector3 position)
        {
            try
            {
                switch (kind)
                {
                    case Kind.Hit: SpawnFx(s_hit, position); SpawnSound(s_hitSound, position); break;
                    case Kind.Death: SpawnFx(s_death, position); SpawnSound(s_deathSound, position); break;
                    case Kind.Strike: SpawnSound(s_strikeSound, position); break;
                    default: SpawnSound(s_idleSound, position); break;
                }
            }
            catch (Exception e)
            {
                if (!s_warnedPlay)
                {
                    s_warnedPlay = true;
                    Plugin.LogException("WormEffects.PlayNow " + kind, e);
                }
            }
        }

        private static void SpawnFx(Slot slot, Vector3 position)
        {
            GameObject template = slot.Template;     // built by Pump, never here
            if (template == null) return;
            GameObject fx = Object.Instantiate(template, position, Quaternion.identity);   // inactive, like its template
            fx.SetActive(true);
#if MW_DEV
            LastFx = fx;
#endif
        }

        private static void SpawnSound(Slot slot, Vector3 position)
        {
            if (Application.isBatchMode) return;
            Player listener = Player.m_localPlayer;
            if (listener == null || (listener.transform.position - position).sqrMagnitude > SoundRange * SoundRange) return;
            GameObject template = slot.Template;
            if (template == null) return;
            Object.Instantiate(template, position, Quaternion.identity).SetActive(true);
        }

        // ------------------------------------------------------------------ templates

        private static void CheckScene()
        {
            if (s_scene == ZNetScene.instance) return;
            s_scene = ZNetScene.instance;
            foreach (Slot slot in s_all) slot.Reset();
        }

        /// <summary>The disabled template of <paramref name="slot"/>, built on first use; null while the prefab is not available (a log line says so once it gives up).</summary>
        private static GameObject Resolve(Slot slot)
        {
            CheckScene();
            if (slot.Template != null) return slot.Template;   // Unity null: a destroyed template is rebuilt
            ZNetScene scene = ZNetScene.instance;
            if (scene == null || slot.GaveUp) return null;     // no scene yet: not an attempt
            float now = Time.unscaledTime;
            if (now < slot.NextTry) return null;
            slot.NextTry = now + RetryInterval;

            for (int k = 0; k < slot.Names.Length; k++)
            {
                string name = slot.Names[k];
                GameObject prefab = scene.GetPrefab(name);
                if (prefab == null) continue;
                if (prefab.GetComponentInChildren<Ragdoll>(true) != null)
                {
                    Plugin.Log.LogWarning($"[Effects] {name} carries a Ragdoll, skipped");
                    continue;
                }
#if MW_DEV
                Snapshot(prefab);
#endif
                try
                {
                    slot.Template = Build(prefab, slot);
                    slot.Source = name;
                    Plugin.Log.LogInfo($"[Effects] {slot.What}: template from {name}{(k > 0 ? " (fallback, " + slot.Names[0] + " missing)" : string.Empty)}, no ZNetView, scale {ScaleOf(slot)}");
                    return slot.Template;
                }
                catch (Exception e)
                {
                    Plugin.LogException("WormEffects.Build " + name, e);
                }
            }

            slot.Attempts++;
            if (slot.Attempts >= MaxAttempts)
            {
                slot.GaveUp = true;
                Plugin.Log.LogWarning($"[Effects] {slot.What}: none of [{string.Join(", ", slot.Names)}] is available after {slot.Attempts} tries; nothing will be played");
            }
            return null;
        }

        private static string ScaleOf(Slot slot) =>
            slot == s_hit ? HitScale.ToString("0.00") : slot == s_death ? DeathScale.ToString("0.00") : "n/a";

        /// <summary>
        /// Clones <paramref name="prefab"/> under a hidden holder, so no Awake runs, removes every ZNetView, applies the slot's
        /// preparation and leaves the clone disabled.
        /// </summary>
        private static GameObject Build(GameObject prefab, Slot slot)
        {
            if (s_holder == null)
            {
                s_holder = new GameObject(HolderName);
                s_holder.SetActive(false);
                Object.DontDestroyOnLoad(s_holder);
            }
            // A template is always a copy that lives under our own holder, never an object of the ZNetScene: everything the preparation changes
            // (pieces removed, sizes, colours) must stay out of the vanilla prefab, whose effect real seekers keep playing.
            GameObject clone = Object.Instantiate(prefab, s_holder.transform);
            if (ReferenceEquals(clone, prefab) || clone.GetInstanceID() == prefab.GetInstanceID() || !clone.transform.IsChildOf(s_holder.transform))
                throw new InvalidOperationException($"template of {prefab.name} is not a copy under the holder");
            clone.name = "MW_" + prefab.name;
            foreach (ZNetView view in clone.GetComponentsInChildren<ZNetView>(true)) Object.DestroyImmediate(view);
            slot.Prepare?.Invoke(clone);
            clone.SetActive(false);

            // Checked, not assumed: a template that still has a network view or the pieces it should have lost is thrown away.
            int views = clone.GetComponentsInChildren<ZNetView>(true).Length;
            if (views != 0 || (slot == s_death && (clone.GetComponentsInChildren<ZSFX>(true).Length != 0 || FindByName(clone, "Carapace") != null)))
            {
                string detail = $"{prefab.name}: znetviews {views}, zsfx {clone.GetComponentsInChildren<ZSFX>(true).Length}, carapace {FindByName(clone, "Carapace") != null}, clone id {clone.GetInstanceID()}, prefab id {prefab.GetInstanceID()}";
                Object.DestroyImmediate(clone);
                throw new InvalidOperationException("template not clean after preparation (" + detail + ")");
            }
            return clone;
        }

        private static Transform FindByName(GameObject root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name == name) return t;
            return null;
        }

        // ---- preparation of the seeker effects

        private static void PrepareHit(GameObject clone)
        {
            RemoveChildren(clone, new[] { VanillaNames.FxChildSkinflakes });
            Tint(clone);
            Shrink(clone, HitScale, HitCountFactor, HitMaxParticleSize, HitMaxParticles, HitDecalSize, HitMaxLifetime, HitSpeedLimit, HitSpeedDampen, HitShapeFactor);
        }

        private static void PrepareDeath(GameObject clone)
        {
            RemoveChildren(clone, VanillaNames.FxSeekerDeathPieces);
            // The seeker's own death sounds are not wanted: the worm plays its own (sfx_leech_death).
            foreach (ZSFX sfx in clone.GetComponentsInChildren<ZSFX>(true)) Object.DestroyImmediate(sfx);
            foreach (AudioSource source in clone.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(source);

            DeathRecolored = Tint(clone);
            Shrink(clone, DeathScale, DeathCountFactor, DeathMaxParticleSize, DeathMaxParticles, DeathDecalSize, DeathMaxLifetime, DeathSpeedLimit, DeathSpeedDampen, DeathShapeFactor);
        }

        // ---- scaling of the particle systems

        /// <summary>
        /// Scales every particle system of <paramref name="root"/> for a small creature: scaling mode Local (the transform is left alone, so
        /// nothing is scaled twice), start size (capped), speed, gravity, shape, the velocity and force modules, burst and rate counts
        /// (<paramref name="countFactor"/>) and maxParticles; ground decals get the fixed <paramref name="decalSize"/>.
        /// </summary>
        private static void Shrink(GameObject root, float k, float countFactor, float maxSize, int maxParticles, float decalSize, float maxLifetime, float speedLimit, float dampen, float shapeFactor)
        {
            List<ParticleSystem> systems = CollectSystems(root);   // sub-emitters included, each processed once
            foreach (ParticleSystem ps in systems)
            {
                bool decal = Array.IndexOf(DecalNames, ps.name) >= 0;

                ParticleSystem.MainModule main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Local;
                if (decal)
                {
                    main.startSize = new ParticleSystem.MinMaxCurve(decalSize);
                }
                else if (main.startSize3D)
                {
                    main.startSizeX = Cap(Mul(main.startSizeX, k), maxSize);
                    main.startSizeY = Cap(Mul(main.startSizeY, k), maxSize);
                    main.startSizeZ = Cap(Mul(main.startSizeZ, k), maxSize);
                }
                else
                {
                    main.startSize = Cap(Mul(main.startSize, k), maxSize);
                }
                main.startSpeed = Mul(main.startSpeed, k);
                if (!decal) main.startLifetime = Cap(main.startLifetime, maxLifetime);
                if (Array.IndexOf(DisabledSystems, ps.name) >= 0) ps.gameObject.SetActive(false);
                main.gravityModifier = Mul(main.gravityModifier, k);

                ParticleSystem.ShapeModule shape = ps.shape;
                if (shape.enabled)
                {
                    shape.radius *= k * shapeFactor;
                    shape.length *= k;
                    shape.scale *= k * shapeFactor;
                    shape.position *= k;
                    shape.randomPositionAmount *= k;
                }

                ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
                if (vel.enabled)
                {
                    vel.x = Mul(vel.x, k);
                    vel.y = Mul(vel.y, k);
                    vel.z = Mul(vel.z, k);
                    vel.radial = Mul(vel.radial, k);
                }
                ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
                if (limit.enabled)
                {
                    limit.limit = Mul(limit.limit, k);
                    limit.limitX = Mul(limit.limitX, k);
                    limit.limitY = Mul(limit.limitY, k);
                    limit.limitZ = Mul(limit.limitZ, k);
                }
                if (!decal)
                {
                    limit.enabled = true;
                    limit.separateAxes = false;
                    limit.limit = new ParticleSystem.MinMaxCurve(speedLimit);
                    limit.dampen = dampen;
                }
                ParticleSystem.ForceOverLifetimeModule force = ps.forceOverLifetime;
                if (force.enabled)
                {
                    force.x = Mul(force.x, k);
                    force.y = Mul(force.y, k);
                    force.z = Mul(force.z, k);
                }

                ParticleSystem.NoiseModule noise = ps.noise;
                if (noise.enabled)
                {
                    noise.strength = Mul(noise.strength, k);
                    if (noise.separateAxes)
                    {
                        noise.strengthX = Mul(noise.strengthX, k);
                        noise.strengthY = Mul(noise.strengthY, k);
                        noise.strengthZ = Mul(noise.strengthZ, k);
                    }
                    noise.positionAmount = Mul(noise.positionAmount, k);
                }
                ParticleSystem.InheritVelocityModule inherit = ps.inheritVelocity;
                if (inherit.enabled) inherit.curve = Mul(inherit.curve, k);
                ParticleSystem.TrailModule trails = ps.trails;
                if (trails.enabled) trails.widthOverTrail = Mul(trails.widthOverTrail, k);
                ParticleSystem.CollisionModule collision = ps.collision;
                if (collision.enabled) collision.bounce = Mul(collision.bounce, k);

                ParticleSystem.EmissionModule emission = ps.emission;
                var bursts = new ParticleSystem.Burst[emission.burstCount];
                emission.GetBursts(bursts);
                for (int b = 0; b < bursts.Length; b++) bursts[b].count = Count(bursts[b].count, countFactor);
                emission.SetBursts(bursts);
                emission.rateOverTime = Mul(emission.rateOverTime, countFactor);
            }

            // One particle budget for the whole effect, sub-emitters included: the systems share maxParticles.
            int emitting = 0;
            foreach (ParticleSystem ps in systems) if (ps.main.maxParticles > 0) emitting++;
            int each = Mathf.Max(1, maxParticles / Mathf.Max(1, emitting));
            foreach (ParticleSystem ps in systems)
            {
                ParticleSystem.MainModule main = ps.main;
                if (main.maxParticles > each) main.maxParticles = each;
            }
        }

        private static ParticleSystem.MinMaxCurve Mul(ParticleSystem.MinMaxCurve c, float k)
        {
            switch (c.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    c.constant *= k;
                    break;
                case ParticleSystemCurveMode.TwoConstants:
                    c.constantMin *= k;
                    c.constantMax *= k;
                    break;
                default:
                    c.curveMultiplier *= k;
                    break;
            }
            return c;
        }

        private static ParticleSystem.MinMaxCurve Cap(ParticleSystem.MinMaxCurve c, float cap)
        {
            switch (c.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    c.constant = Mathf.Min(c.constant, cap);
                    break;
                case ParticleSystemCurveMode.TwoConstants:
                    c.constantMin = Mathf.Min(c.constantMin, cap);
                    c.constantMax = Mathf.Min(c.constantMax, cap);
                    break;
                default:
                    c.curveMultiplier = Mathf.Min(c.curveMultiplier, cap);   // curves are normalised to 0..1 in the seeker effects
                    break;
            }
            return c;
        }

        /// <summary>Burst count times <paramref name="f"/>, at least 1 particle for a constant count.</summary>
        private static ParticleSystem.MinMaxCurve Count(ParticleSystem.MinMaxCurve c, float f)
        {
            c = Mul(c, f);
            switch (c.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    c.constant = Mathf.Max(1f, Mathf.Round(c.constant));
                    break;
                case ParticleSystemCurveMode.TwoConstants:
                    c.constantMin = Mathf.Max(1f, Mathf.Round(c.constantMin));
                    c.constantMax = Mathf.Max(c.constantMin, Mathf.Round(c.constantMax));
                    break;
            }
            return c;
        }

        /// <summary>Destroys every descendant (root excluded) whose name is in <paramref name="names"/>.</summary>
        private static void RemoveChildren(GameObject root, string[] names)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t == root.transform) continue;   // a child of an already destroyed object reads as null
                if (Array.IndexOf(names, t.name) >= 0) Object.DestroyImmediate(t.gameObject);
            }
        }

        /// <summary>Every particle system of <paramref name="root"/> and the sub-emitters they reference, each once.</summary>
        private static List<ParticleSystem> CollectSystems(GameObject root)
        {
            var list = new List<ParticleSystem>(root.GetComponentsInChildren<ParticleSystem>(true));
            var seen = new HashSet<ParticleSystem>(list);
            for (int n = 0; n < list.Count; n++)
            {
                ParticleSystem.SubEmittersModule sub = list[n].subEmitters;
                for (int k = 0; k < sub.subEmittersCount; k++)
                {
                    ParticleSystem child = sub.GetSubEmitterSystem(k);
                    if (child == null || !seen.Add(child)) continue;
                    // A sub-emitter that is not part of the copy would be an object of the vanilla prefab (or of another asset): left alone.
                    if (!child.transform.IsChildOf(root.transform))
                    {
                        Plugin.Log.LogWarning($"[Effects] sub-emitter {child.name} of {list[n].name} is outside the template {root.name}: not modified");
                        continue;
                    }
                    list.Add(child);
                }
            }
            return list;
        }

        /// <summary>Gives every blood system the acid green start colour (once, at build); returns the number of systems recoloured.</summary>
        private static int Tint(GameObject root)
        {
            List<ParticleSystem> systems = CollectSystems(root);
            foreach (ParticleSystem ps in systems)
            {
                ParticleSystem.MainModule main = ps.main;
                ParticleSystem.MinMaxGradient g = main.startColor;
                float alphaA = 1f, alphaB = 1f;
                switch (g.mode)
                {
                    case ParticleSystemGradientMode.Color:
                        alphaA = alphaB = g.color.a;
                        break;
                    case ParticleSystemGradientMode.TwoColors:
                        alphaA = g.colorMin.a;
                        alphaB = g.colorMax.a;
                        break;
                }
                Color a = BloodGreenA; a.a = alphaA;
                Color b = BloodGreenB; b.a = alphaB;
                main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            }
            return systems.Count;
        }

#if MW_DEV
        // What a vanilla effect prefab looks like before any template is made from it, to prove later that nothing of it was changed (V08).
        private sealed class PrefabState
        {
            public int NetViews, Legs, Wings, Carapace;
            public Color RootStartMin, RootStartMax;
            public string Describe() => $"znetviews {NetViews}, Legs {Legs}, Wings {Wings}, Carapace {Carapace}, root start colour {RootStartMin}/{RootStartMax}";
        }

        private static readonly Dictionary<string, PrefabState> s_snapshots = new Dictionary<string, PrefabState>();

        private static PrefabState Read(GameObject prefab)
        {
            var state = new PrefabState { NetViews = prefab.GetComponentsInChildren<ZNetView>(true).Length };
            foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                if (t.name == "Legs") state.Legs++;
                else if (t.name == "Wings") state.Wings++;
                else if (t.name == "Carapace") state.Carapace++;
            }
            ParticleSystem root = prefab.GetComponent<ParticleSystem>() ?? prefab.GetComponentInChildren<ParticleSystem>(true);
            if (root != null)
            {
                ParticleSystem.MinMaxGradient g = root.main.startColor;
                state.RootStartMin = g.mode == ParticleSystemGradientMode.TwoColors ? g.colorMin : g.color;
                state.RootStartMax = g.mode == ParticleSystemGradientMode.TwoColors ? g.colorMax : g.color;
            }
            return state;
        }

        private static void Snapshot(GameObject prefab)
        {
            if (!s_snapshots.ContainsKey(prefab.name)) s_snapshots[prefab.name] = Read(prefab);
        }

        /// <summary>
        /// True when the vanilla prefab <paramref name="name"/> still looks like it did when the first template was made from it (V08): same
        /// network view, same Legs, Wings and Carapace, same start colour on its first particle system.
        /// </summary>
        public static bool VanillaIntact(string name, out string detail)
        {
            detail = null;
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            if (prefab == null) { detail = name + " is not in the ZNetScene"; return false; }
            if (!s_snapshots.TryGetValue(name, out PrefabState before)) { detail = "no snapshot (no template was ever made from it)"; return false; }
            PrefabState now = Read(prefab);
            bool same = now.NetViews == before.NetViews && now.Legs == before.Legs && now.Wings == before.Wings && now.Carapace == before.Carapace
                        && now.RootStartMin == before.RootStartMin && now.RootStartMax == before.RootStartMax;
            detail = same ? "unchanged: " + now.Describe() : $"CHANGED: before [{before.Describe()}], now [{now.Describe()}]";
            return same;
        }

        /// <summary>Current network views and Legs, Wings, Carapace counts of the vanilla prefab (V08 asserts the seeker death effect still has them all).</summary>
        public static bool VanillaCounts(string name, out int views, out int legs, out int wings, out int carapace)
        {
            views = legs = wings = carapace = 0;
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            if (prefab == null) return false;
            PrefabState now = Read(prefab);
            views = now.NetViews; legs = now.Legs; wings = now.Wings; carapace = now.Carapace;
            return true;
        }

        /// <summary>The last visual effect instance played (V08 measures its particles).</summary>
        public static GameObject LastFx { get; private set; }

        /// <summary>Builds the hit and death templates now (dev scenarios); true when both exist.</summary>
        public static bool EnsureTemplates(out GameObject hit, out GameObject death, out string hitSource, out string deathSource)
        {
            s_hit.NextTry = 0f;
            s_death.NextTry = 0f;
            hit = Resolve(s_hit);
            death = Resolve(s_death);
            hitSource = s_hit.Source;
            deathSource = s_death.Source;
            return hit != null && death != null;
        }

        /// <summary>Builds every sound template now and returns the names that could not be resolved.</summary>
        public static string MissingSounds()
        {
            string missing = string.Empty;
            foreach (Slot slot in new[] { s_hitSound, s_deathSound, s_strikeSound, s_idleSound })
            {
                slot.NextTry = 0f;
                if (Resolve(slot) == null) missing += (missing.Length > 0 ? ", " : string.Empty) + slot.Names[0];
            }
            return missing;
        }
#endif
    }
}
