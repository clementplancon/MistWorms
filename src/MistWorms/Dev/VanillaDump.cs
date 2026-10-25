#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using MistWorms.Colony;
using MistWorms.Config;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace MistWorms.Dev
{
    /// <summary>
    /// Phase 0 reconnaissance (spec §16, §20.1): dumps the vanilla data the design depends on into a markdown
    /// report that is then curated into docs/vanilla-reference.md. Must run in game, on the main thread.
    /// Names that exist in VanillaNames are read from it (single source of truth); names not covered there stay literals.
    /// </summary>
    internal static class VanillaDump
    {
        private static readonly string[] ReferenceCreatures = { VanillaNames.Tick, VanillaNames.Seeker, VanillaNames.SeekerBrood, VanillaNames.SeekerBrute, VanillaNames.Leech, VanillaNames.Blob };
        private static readonly string[] FxPrefixes = { "sfx_", "vfx_", "fx_" };
        private static readonly string[] FxKeywords = { "tick", "seeker", "brood", "egg", "bug", "insect", "crawl", "squish", "death", "blood",
            "hit", "hurt", "leech", "blob", "boar", "ooze", "goo", "slime", "green", "puke", "guck", "poison" };
        private static readonly string[] DestructibleKeywords = { "egg", "sac", "seeker", "dvergr", "nest", "cocoon" };
        private static readonly string[] RequiredShaders = { VanillaNames.ShaderCreature, VanillaNames.ShaderPiece, "Custom/Vegetation", "Custom/Player" };

        private static readonly string[] ProbeWeapons =
        {
            "KnifeCopper", VanillaNames.SwordBronze, "AxeBronze", "MaceBronze", "SpearBronze", "AtgeirBronze", "PickaxeBronze", "ShieldBronzeBuckler",
            "SwordIron", "AxeIron", "MaceIron", "SledgeIron", "AtgeirIron", "Battleaxe", "PickaxeIron", "ShieldBanded", "BowFineWood",
        };

        private static readonly string[] ProbeAttackItems = { VanillaNames.SwordBronze, "AtgeirBronze", "SpearBronze", "SledgeIron", "BowFineWood", "ArrowWood", "ArrowFire", "StaffFireball" };

        // Shaders seen on any material during the dump, so section 6 can test shaders Shader.Find cannot resolve.
        private static readonly Dictionary<string, Shader> SeenShaders = new Dictionary<string, Shader>(StringComparer.Ordinal);
        private static readonly HashSet<string> ShaderPropsPrinted = new HashSet<string>(StringComparer.Ordinal);

        // Kept alive until the deferred render check has finished drawing with them.
        private static readonly List<UnityEngine.Object> TempObjects = new List<UnityEngine.Object>();
        private static bool _deferredCheckRunning;

        /// <summary>Prefix of every failure message <see cref="Run"/> returns instead of a path.</summary>
        public const string ErrorPrefix = "ERROR: ";

        public static bool IsError(string result) => result == null || result.StartsWith(ErrorPrefix, StringComparison.Ordinal);

        /// <summary>
        /// Raw dump next to, never over, the curated docs/vanilla-reference.md. MW_REPO_ROOT (set by
        /// tools/run-game.ps1) puts it in the repo; otherwise it stays in the BepInEx folder.
        /// </summary>
        public static string DefaultOutputPath
        {
            get
            {
                string root = Environment.GetEnvironmentVariable("MW_REPO_ROOT");
                return !string.IsNullOrEmpty(root) && Directory.Exists(root)
                    ? Path.Combine(Path.Combine(root, "docs"), "vanilla-reference-dump.md")
                    : Path.Combine(Paths.BepInExRootPath, "mistworms-vanilla-dump.md");
            }
        }

        public static string Run(string outputPath)
        {
            if (ZNetScene.instance == null || ObjectDB.instance == null || ObjectDB.instance.m_items.Count == 0)
                return ErrorPrefix + "ZNetScene/ObjectDB not ready (load a world first)";

            string path = Path.GetFullPath(string.IsNullOrWhiteSpace(outputPath) ? DefaultOutputPath : outputPath);

            SeenShaders.Clear();
            ShaderPropsPrinted.Clear();
            List<RenderProbe> probes = null;

            var sb = new StringBuilder(256 * 1024);
            sb.AppendLine("# MistWorms vanilla dump").AppendLine();
            sb.AppendLine($"Generated {DateTime.Now:yyyy-MM-dd HH:mm:ss} by `VanillaDump` (MistWorms {Plugin.ModVersion}). " +
                          $"Valheim `{SafeGameVersion()}`, Unity `{Application.unityVersion}`. Raw output, to be curated into `docs/vanilla-reference.md`.");
            sb.AppendLine();

            List<GameObject> prefabs = AllPrefabs();

            Section(sb, "1. Reference creatures", s => DumpCreatures(s, prefabs));
            Section(sb, "1b. World-space sizes", DumpWorldSizes);
            Section(sb, "2. Effect prefabs (sfx_ / vfx_ / fx_)", s => DumpFx(s, prefabs));
            Section(sb, "3. Destructible candidates (eggs, sacs, nests)", s => DumpDestructibles(s, prefabs));
            Section(sb, "4. Items", DumpItems);
            Section(sb, "5. Room themes and Dvergr / Mistlands locations", DumpLocations);
            Section(sb, "6. Shaders and instancing", s => probes = DumpShaders(s, prefabs));
            Section(sb, "7. Layers and hit masks", s => DumpLayers(s, prefabs));
            Section(sb, "8. Engine and game versions", DumpVersions);
            Section(sb, "9. Custom/Creature shader: keywords, defaults, ranges", DumpCreatureShader);
            Section(sb, "10. Particle systems of priority and hit / hurt / blood effects", s => DumpParticles(s, prefabs));
            Section(sb, "11. Effects referenced by the dumped creatures", s => DumpReferencedEffects(s, prefabs));
            Section(sb, "12. Albedo and gloss texture statistics", s => DumpTextureStats(s, prefabs));
            Section(sb, "13. Status effects (ObjectDB.m_StatusEffects, Infested icon candidates)", DumpStatusEffects);

            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                return $"{ErrorPrefix}cannot write {path}: {e.Message}";
            }

            // Render-time instancing errors only surface during the following frames; they are appended later.
            if (probes != null && probes.Count > 0 && Plugin.Instance != null && !_deferredCheckRunning)
                Plugin.Instance.StartCoroutine(DeferredRenderCheck(path, probes));

            Plugin.Log.LogInfo($"[VanillaDump] wrote {path}");
            return path;
        }

        // ------------------------------------------------------------------ 1. creatures

        private static void DumpCreatures(StringBuilder sb, List<GameObject> prefabs)
        {
            var names = new SortedSet<string>(ReferenceCreatures, StringComparer.Ordinal);
            foreach (GameObject p in prefabs)
                if (IsCreatureCandidate(p.name)) names.Add(p.name);

            var creatures = new List<GameObject>();
            var others = new List<object[]>();
            foreach (string n in names)
            {
                GameObject p = FindPrefab(prefabs, n);
                if (p == null) others.Add(new object[] { n, "**missing from ZNetScene**" });
                else if (p.GetComponent<Character>() != null) creatures.Add(p);
                else others.Add(new object[] { n, ComponentList(p) });
            }

            sb.AppendLine("### Stats").AppendLine();
            Table(sb, new[] { "Prefab", "m_name", "Health", "Speed", "Walk", "Run", "Turn", "RunTurn", "Faction", "Group", "Flying", "Boss", "TolerateFire" },
                creatures.Select(p =>
                {
                    Character c = p.GetComponent<Character>();
                    return new object[] { p.name, Token(c.m_name), c.m_health, c.m_speed, c.m_walkSpeed, c.m_runSpeed, c.m_turnSpeed, c.m_runTurnSpeed,
                        c.m_faction, c.m_group, c.m_flying, c.m_boss, c.m_tolerateFire };
                }));

            sb.AppendLine("### Perception and AI").AppendLine();
            Table(sb, new[] { "Prefab", "AI", "ViewRange", "ViewAngle", "HearRange", "AlertRange", "AfraidOfFire", "AvoidFire", "PathAgentType", "AttackPlayerObjects", "IdleSound (interval, chance)" },
                creatures.Select(p =>
                {
                    BaseAI ai = p.GetComponent<BaseAI>();
                    if (ai == null) return new object[] { p.name, "none" };
                    MonsterAI mai = ai as MonsterAI;
                    return new object[] { p.name, ai.GetType().Name, ai.m_viewRange, ai.m_viewAngle, ai.m_hearRange, mai != null ? (object)mai.m_alertRange : "-",
                        ai.m_afraidOfFire, ai.m_avoidFire, ai.m_pathAgentType, mai != null ? (object)mai.m_attackPlayerObjects : "-",
                        $"{Effects(ai.m_idleSound)} ({F(ai.m_idleSoundInterval)} s, {F(ai.m_idleSoundChance)})" };
                }));

            sb.AppendLine("### Damage modifiers").AppendLine();
            FieldInfo[] modFields = typeof(HitData.DamageModifiers).GetFields(BindingFlags.Public | BindingFlags.Instance);
            Table(sb, new[] { "Prefab" }.Concat(modFields.Select(f => f.Name.Replace("m_", ""))).ToArray(),
                creatures.Select(p =>
                {
                    object mods = p.GetComponent<Character>().m_damageModifiers;
                    return new object[] { p.name }.Concat(modFields.Select(f => f.GetValue(mods))).ToArray();
                }));

            sb.AppendLine("### Size").AppendLine();
            sb.AppendLine("Mesh AABB is computed from mesh/skin local bounds in root space (LOD0 only, inactive children skipped) because prefab assets report empty `Renderer.bounds`; both are shown.").AppendLine();
            Table(sb, new[] { "Prefab", "Root scale", "Mesh AABB size (root space)", "Renderer.bounds size (as-is)", "Colliders" },
                creatures.Select(p => new object[] { p.name, V(p.transform.localScale), MeshAabb(p), RendererBoundsAsIs(p), Colliders(p) }));

            foreach (GameObject p in creatures)
            {
                try
                {
                    DumpCreatureDetails(sb, p);
                }
                catch (Exception e)
                {
                    sb.AppendLine($"> `{p.name}` details failed: {Esc(e.Message)}").AppendLine();
                }
            }

            sb.AppendLine("### Other prefabs matching Seeker / Tick / Leech / Blob").AppendLine();
            Table(sb, new[] { "Prefab", "Components" }, others);

            sb.AppendLine("### Shader properties (creature materials)").AppendLine();
            foreach (Shader sh in SeenShaders.Values.ToList()) ShaderProperties(sb, sh);
        }

        private static void DumpCreatureDetails(StringBuilder sb, GameObject p)
        {
            Character c = p.GetComponent<Character>();
            sb.AppendLine($"### {p.name}").AppendLine();
            sb.AppendLine($"Components: {ComponentList(p)}").AppendLine();

            if (c is Humanoid h)
            {
                var items = new List<KeyValuePair<string, GameObject>>();
                void Add(string src, IEnumerable<GameObject> gos)
                {
                    if (gos == null) return;
                    foreach (GameObject go in gos) if (go != null) items.Add(new KeyValuePair<string, GameObject>(src, go));
                }
                Add("default", h.m_defaultItems);
                Add("randomWeapon", h.m_randomWeapon);
                Add("randomArmor", h.m_randomArmor);
                Add("randomShield", h.m_randomShield);
                if (h.m_randomSets != null) foreach (Humanoid.ItemSet set in h.m_randomSets) Add("set " + set.m_name, set.m_items);
                if (h.m_randomItems != null) Add("randomItem", h.m_randomItems.Select(r => r.m_prefab));
                if (h.m_unarmedWeapon != null) Add("unarmed", new[] { h.m_unarmedWeapon.gameObject });

                sb.AppendLine("Attacks (Humanoid items):").AppendLine();
                Table(sb, new[] { "Source", "Item", "Type", "Damages", "AttackType", "Anim", "Range", "Height", "Angle", "RayWidth", "AI range", "AI interval", "Force", "Projectile", "Hit effects" },
                    items.Select(kv =>
                    {
                        ItemDrop id = kv.Value.GetComponent<ItemDrop>();
                        if (id == null) return new object[] { kv.Key, kv.Value.name, "no ItemDrop" };
                        ItemDrop.ItemData.SharedData s = id.m_itemData.m_shared;
                        Attack a = s.m_attack;
                        return new object[] { kv.Key, kv.Value.name, s.m_itemType, Damages(s.m_damages), a?.m_attackType, a?.m_attackAnimation, a?.m_attackRange, a?.m_attackHeight,
                            a?.m_attackAngle, a?.m_attackRayWidth, s.m_aiAttackRange, s.m_aiAttackInterval, s.m_attackForce, a?.m_attackProjectile ? a.m_attackProjectile.name : "-",
                            JoinNonEmpty(Effects(s.m_hitEffect), a != null ? Effects(a.m_hitEffect) : null, a != null ? Effects(a.m_triggerEffect) : null) };
                    }));
            }

            CharacterDrop drop = p.GetComponent<CharacterDrop>();
            sb.AppendLine("Drops (CharacterDrop):").AppendLine();
            Table(sb, new[] { "Item", "Min", "Max", "Chance", "OnePerPlayer", "LevelMultiplier", "DontScale" },
                drop == null ? Enumerable.Empty<object[]>() : drop.m_drops.Select(d => new object[]
                    { d.m_prefab ? d.m_prefab.name : "null", d.m_amountMin, d.m_amountMax, d.m_chance, d.m_onePerPlayer, d.m_levelMultiplier, d.m_dontScale }));

            BaseAI ai = p.GetComponent<BaseAI>();
            MonsterAI mai = ai as MonsterAI;
            sb.AppendLine("Effects:").AppendLine();
            Table(sb, new[] { "List", "Prefabs" }, new[]
            {
                new object[] { "m_hitEffects", Effects(c.m_hitEffects) },
                new object[] { "m_critHitEffects", Effects(c.m_critHitEffects) },
                new object[] { "m_backstabHitEffects", Effects(c.m_backstabHitEffects) },
                new object[] { "m_deathEffects", Effects(c.m_deathEffects) },
                new object[] { "BaseAI.m_alertedEffects", ai != null ? Effects(ai.m_alertedEffects) : "-" },
                new object[] { "BaseAI.m_idleSound", ai != null ? Effects(ai.m_idleSound) : "-" },
                new object[] { "MonsterAI.m_wakeupEffects", mai != null ? Effects(mai.m_wakeupEffects) : "-" },
                new object[] { "MonsterAI.m_sleepEffects", mai != null ? Effects(mai.m_sleepEffects) : "-" },
            });

            sb.AppendLine("Renderers and materials:").AppendLine();
            var rows = new List<object[]>();
            foreach (Renderer r in p.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                Mesh mesh = r is SkinnedMeshRenderer smr ? smr.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    if (m.shader != null) SeenShaders[m.shader.name] = m.shader;
                    rows.Add(new object[] { RelPath(r.transform, p.transform), r.GetType().Name, mesh ? $"{mesh.name} ({mesh.vertexCount} v)" : "-", m.name,
                        m.shader ? m.shader.name : "null", Textures(m), string.Join(" ", m.shaderKeywords ?? new string[0]), m.enableInstancing, m.renderQueue });
                }
            }
            Table(sb, new[] { "Renderer", "Type", "Mesh", "Material", "Shader", "Textures", "Keywords", "Instancing", "Queue" }, rows);

            var seenMats = new HashSet<Material>();
            foreach (Renderer r in p.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                foreach (Material m in r.sharedMaterials)
                    if (m != null && seenMats.Add(m))
                    {
                        try { MaterialValues(sb, m, RelPath(r.transform, p.transform)); }
                        catch (Exception e) { sb.AppendLine($"> material `{m.name}` values failed: {Esc(e.Message)}").AppendLine(); }
                    }
            }
        }

        // ------------------------------------------------------------------ 1b. world-space sizes

        private static readonly string[] WorldSizePrefabs =
            { VanillaNames.Player, VanillaNames.Tick, VanillaNames.Seeker, VanillaNames.SeekerBrood, VanillaNames.SeekerBrute };

        private sealed class AabbAccum
        {
            public bool Any;
            public Bounds B;

            public void Add(Vector3 p)
            {
                if (!Any) { B = new Bounds(p, Vector3.zero); Any = true; }
                else B.Encapsulate(p);
            }

            public void AddCorners(Bounds local, Matrix4x4 m)
            {
                for (int i = 0; i < 8; i++)
                    Add(m.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            }

            public Vector3 Size => Any ? B.size : Vector3.zero;
        }

        private static void DumpWorldSizes(StringBuilder sb)
        {
            sb.AppendLine("Measured on the prefab assets (nothing instantiated). Every point is taken to the prefab root space with `root.worldToLocalMatrix` (root position/rotation dropped) " +
                          "and then multiplied by `root.lossyScale`, so the root scale is applied. Renderers: active `SkinnedMeshRenderer` / `MeshRenderer` only (LOD0 when a LODGroup exists, " +
                          "disabled renderers and inactive children skipped). Skinned: `localBounds` in rootBone space (renderer transform if no rootBone). " +
                          "Alt AABB: all used renderers re-measured with `sharedMesh.bounds` in renderer space, as a cross-check. Collider sizes use the collider transform `lossyScale`.").AppendLine();

            var summary = new List<object[]>();
            var details = new StringBuilder();
            float playerHeight = float.NaN;
            foreach (string name in WorldSizePrefabs)
            {
                GameObject p = ZNetScene.instance.GetPrefab(name);
                if (p == null)
                {
                    summary.Add(new object[] { name, "**missing from ZNetScene**" });
                    continue;
                }

                try
                {
                    var part = new StringBuilder();
                    object[] row = MeasureWorldSize(p, part, out Vector3 size);
                    summary.Add(row);
                    details.Append(part);
                    if (name == VanillaNames.Player) playerHeight = size.y;
                }
                catch (Exception e)
                {
                    summary.Add(new object[] { name, "error: " + e.Message });
                }
            }

            sb.AppendLine("### Summary").AppendLine();
            Table(sb, new[] { "Prefab", "Width X (m)", "Height Y (m)", "Length Z (m)", "Footprint D = sqrt(X*Z) (m)", "Main collider (world)", "Root scale", "Renderers used" }, summary);
            sb.AppendLine(float.IsNaN(playerHeight)
                ? "Player sanity check: **Player not measured**."
                : $"Player sanity check: measured player height = {F(playerHeight)} m (expected about 1.8 - 2.0 m).").AppendLine();
            sb.Append(details);
        }

        private static object[] MeasureWorldSize(GameObject p, StringBuilder sb, out Vector3 size)
        {
            Transform root = p.transform;
            Vector3 rootScale = root.lossyScale;
            Matrix4x4 toRoot = Matrix4x4.Scale(rootScale) * root.worldToLocalMatrix;

            HashSet<Renderer> excluded = NonLod0Renderers(p);
            LODGroup[] lodGroups = p.GetComponentsInChildren<LODGroup>(true);
            var total = new AabbAccum();
            var alt = new AabbAccum();
            var rows = new List<object[]>();
            int used = 0, skipped = 0;
            foreach (Renderer r in p.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is SkinnedMeshRenderer || r is MeshRenderer)) continue;
                if (excluded.Contains(r) || !r.enabled || !ActiveUnder(r.transform, root)) { skipped++; continue; }

                Bounds local;
                Transform space = r.transform;
                string kind;
                if (r is SkinnedMeshRenderer smr)
                {
                    local = smr.localBounds;
                    if (smr.rootBone != null) space = smr.rootBone;
                    kind = "skinned" + (smr.rootBone != null ? " (rootBone " + smr.rootBone.name + ")" : " (renderer space)");
                    if (smr.sharedMesh != null) alt.AddCorners(smr.sharedMesh.bounds, toRoot * r.transform.localToWorldMatrix);
                }
                else
                {
                    MeshFilter mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) { skipped++; continue; }
                    local = mf.sharedMesh.bounds;
                    kind = "mesh";
                    alt.AddCorners(local, toRoot * r.transform.localToWorldMatrix);
                }

                Matrix4x4 m = toRoot * space.localToWorldMatrix;
                var one = new AabbAccum();
                one.AddCorners(local, m);
                total.AddCorners(local, m);
                used++;
                rows.Add(new object[] { RelPath(r.transform, root), kind, V(local.size), V(one.Size), $"{F(one.B.min.y)} .. {F(one.B.max.y)}" });
            }

            size = total.Size;
            string lodText = lodGroups.Length == 0 ? "no LODGroup" : $"{lodGroups.Length} LODGroup (LOD0 only, {excluded.Count} LOD1+ renderers excluded)";

            sb.AppendLine($"### {p.name} (world size)").AppendLine();
            sb.AppendLine($"Root localScale {V(root.localScale)}, lossyScale {V(rootScale)}; {lodText}; {used} renderers used, {skipped} skipped. " +
                          (total.Any ? $"Combined AABB {V(size)}, y range {F(total.B.min.y)} .. {F(total.B.max.y)}, center {V(total.B.center)}. " : "No renderer. ") +
                          (alt.Any ? $"Alt AABB (mesh.bounds, renderer space) {V(alt.Size)}." : "")).AppendLine();
            Table(sb, new[] { "Renderer", "Kind", "Local bounds size", "Root-space AABB size", "Y range (root space)" }, rows);

            var colRows = new List<object[]>();
            var candidates = new List<KeyValuePair<Collider, string>>();
            foreach (Collider c in p.GetComponentsInChildren<Collider>(true))
            {
                string type = c.GetType().Name, dims;
                Vector3 sc = c.transform.lossyScale;
                var a = new Vector3(Mathf.Abs(sc.x), Mathf.Abs(sc.y), Mathf.Abs(sc.z));
                Vector3 center = Vector3.zero;
                switch (c)
                {
                    case CapsuleCollider cc:
                    {
                        int axis = cc.direction; // 0 X, 1 Y, 2 Z
                        float along = axis == 0 ? a.x : axis == 1 ? a.y : a.z;
                        float radial = axis == 0 ? Mathf.Max(a.y, a.z) : axis == 1 ? Mathf.Max(a.x, a.z) : Mathf.Max(a.x, a.y);
                        dims = $"r {F(cc.radius * radial)} h {F(cc.height * along)} dir {(axis == 0 ? "X" : axis == 1 ? "Y" : "Z")}";
                        center = cc.center;
                        break;
                    }
                    case SphereCollider sph:
                        dims = $"r {F(sph.radius * Mathf.Max(a.x, Mathf.Max(a.y, a.z)))}";
                        center = sph.center;
                        break;
                    case BoxCollider bc:
                        dims = $"size {V(Vector3.Scale(bc.size, a))}";
                        center = bc.center;
                        break;
                    case MeshCollider mc:
                        dims = mc.sharedMesh ? $"mesh {mc.sharedMesh.name} bounds {V(Vector3.Scale(mc.sharedMesh.bounds.size, a))}" : "mesh null";
                        break;
                    default:
                        dims = "";
                        break;
                }

                Vector3 centerRoot = (toRoot * c.transform.localToWorldMatrix).MultiplyPoint3x4(center);
                colRows.Add(new object[] { RelPath(c.transform, root), type, LayerMask.LayerToName(c.gameObject.layer), c.isTrigger, dims, V(centerRoot),
                    (c.enabled ? "" : "disabled ") + (ActiveUnder(c.transform, root) ? "" : "inactive") });
                if (!c.isTrigger) candidates.Add(new KeyValuePair<Collider, string>(c, $"{type} {dims}"));
            }
            Table(sb, new[] { "Collider", "Type", "Layer", "IsTrigger", "World dims", "Center (root space)", "State" }, colRows);

            // Main collider: first non-trigger on the root object, else first non-trigger on the character layer, else first non-trigger.
            string mainText = "none";
            if (candidates.Count > 0)
            {
                KeyValuePair<Collider, string> pick = candidates.FirstOrDefault(kv => kv.Key.transform == root);
                if (pick.Key == null) pick = candidates.FirstOrDefault(kv => kv.Key.gameObject.layer == LayerMask.NameToLayer(VanillaNames.LayerCharacter));
                if (pick.Key == null) pick = candidates[0];
                mainText = pick.Value;
            }

            float d = Mathf.Sqrt(Mathf.Max(0f, size.x * size.z));
            return new object[] { p.name, F(size.x), F(size.y), F(size.z), F(d), mainText, V(root.localScale), $"{used} (+{skipped} skipped)" };
        }

        // ------------------------------------------------------------------ 2. effects

        private static void DumpFx(StringBuilder sb, List<GameObject> prefabs)
        {
            var byPrefix = FxPrefixes.ToDictionary(x => x, x => new List<string>());
            var matched = new List<object[]>();
            foreach (GameObject p in prefabs)
            {
                string lower = p.name.ToLowerInvariant();
                string prefix = FxPrefixes.FirstOrDefault(x => lower.StartsWith(x, StringComparison.Ordinal));
                if (prefix == null) continue;
                byPrefix[prefix].Add(p.name);
                if (!FxKeywords.Any(k => lower.Contains(k))) continue;

                string detail;
                try { detail = FxDetail(p); }
                catch (Exception e) { detail = "error: " + e.Message; }
                matched.Add(new object[] { p.name, ZNetScene.instance.m_prefabs.Contains(p) ? "net" : "non-net", detail });
            }

            Table(sb, new[] { "Prefix", "Count" }, byPrefix.Select(kv => new object[] { kv.Key, kv.Value.Count }));
            sb.AppendLine($"Keyword filter: {string.Join(", ", FxKeywords)}.").AppendLine();
            Table(sb, new[] { "Prefab", "List", "Details" }, matched.OrderBy(r => (string)r[0], StringComparer.Ordinal));

            foreach (KeyValuePair<string, List<string>> kv in byPrefix)
            {
                kv.Value.Sort(StringComparer.Ordinal);
                sb.AppendLine($"<details><summary>All {kv.Key}* ({kv.Value.Count})</summary>").AppendLine();
                sb.AppendLine(string.Join(", ", kv.Value)).AppendLine();
                sb.AppendLine("</details>").AppendLine();
            }
        }

        private static string FxDetail(GameObject p)
        {
            var parts = new List<string>();
            foreach (ZSFX sfx in p.GetComponentsInChildren<ZSFX>(true))
            {
                string clips = sfx.m_audioClips == null ? "" : string.Join(", ", sfx.m_audioClips.Where(a => a != null).Select(a => a.name));
                parts.Add($"ZSFX clips [{clips}] pitch {F(sfx.m_minPitch)}-{F(sfx.m_maxPitch)} vol {F(sfx.m_minVol)}-{F(sfx.m_maxVol)}");
            }
            int ps = p.GetComponentsInChildren<ParticleSystem>(true).Length;
            if (ps > 0) parts.Add($"{ps} ParticleSystem");
            if (p.GetComponentInChildren<Ragdoll>(true) != null) parts.Add("Ragdoll");
            if (p.GetComponentInChildren<TimedDestruction>(true) != null) parts.Add("TimedDestruction");
            return parts.Count > 0 ? string.Join("; ", parts) : ComponentList(p);
        }

        // ------------------------------------------------------------------ 3. destructibles

        private static void DumpDestructibles(StringBuilder sb, List<GameObject> prefabs)
        {
            var rows = new List<object[]>();
            foreach (GameObject p in prefabs)
            {
                string lower = p.name.ToLowerInvariant();
                if (!DestructibleKeywords.Any(k => lower.Contains(k))) continue;
                Destructible d = p.GetComponent<Destructible>();
                if (d == null || p.GetComponent<Character>() != null || p.GetComponent<WearNTear>() != null) continue;
                try
                {
                    DropOnDestroyed dod = p.GetComponent<DropOnDestroyed>();
                    string drops = dod == null ? "-" : $"{dod.m_dropWhenDestroyed.m_dropMin}-{dod.m_dropWhenDestroyed.m_dropMax} @ {F(dod.m_dropWhenDestroyed.m_dropChance)}: " +
                        string.Join(", ", dod.m_dropWhenDestroyed.m_drops.Select(x => $"{(x.m_item ? x.m_item.name : "null")} {x.m_stackMin}-{x.m_stackMax} w{F(x.m_weight)}"));
                    rows.Add(new object[] { p.name, d.m_health, d.m_destructibleType, d.m_minToolTier, NonNormalModifiers(d.m_damages), Effects(d.m_destroyedEffect),
                        Effects(d.m_hitEffect), d.m_spawnWhenDestroyed ? d.m_spawnWhenDestroyed.name : "-", drops, MeshAabb(p) });
                }
                catch (Exception e)
                {
                    rows.Add(new object[] { p.name, "error: " + e.Message });
                }
            }
            Table(sb, new[] { "Prefab", "Health", "Type", "MinToolTier", "Modifiers (non-Normal)", "m_destroyedEffect", "m_hitEffect", "SpawnWhenDestroyed", "DropOnDestroyed", "Mesh AABB" }, rows);
        }

        // ------------------------------------------------------------------ 4. items

        private static void DumpItems(StringBuilder sb)
        {
            List<ItemDrop> items = ObjectDB.instance.m_items.Where(go => go != null).Select(go => go.GetComponent<ItemDrop>()).Where(i => i != null).ToList();
            sb.AppendLine($"ObjectDB: {items.Count} items, {ObjectDB.instance.m_recipes.Count} recipes, {items.Count(i => i.m_itemData.m_shared.m_useDurability)} with durability.").AppendLine();

            sb.AppendLine("### Wisplight (Demister and tokens containing demister / wisp)").AppendLine();
            var wisp = items.Where(i => i.name == VanillaNames.Demister || ContainsCi(i.m_itemData.m_shared.m_name, "demister") || ContainsCi(i.m_itemData.m_shared.m_name, "wisp")).ToList();
            if (ObjectDB.instance.GetItemPrefab(VanillaNames.Demister) == null) sb.AppendLine("**No item prefab named `Demister`.**").AppendLine();
            Table(sb, new[] { "Prefab", "m_name", "Type", "UseDurability", "MaxDurability", "DurabilityPerLevel", "MaxQuality", "Recipe" },
                wisp.Select(i =>
                {
                    ItemDrop.ItemData.SharedData s = i.m_itemData.m_shared;
                    return new object[] { i.name, Token(s.m_name), s.m_itemType, s.m_useDurability, s.m_maxDurability, s.m_durabilityPerLevel, s.m_maxQuality, RecipeText(i) };
                }));

            sb.AppendLine("### Entrails and Bloodbag").AppendLine();
            Table(sb, new[] { "Prefab", "Found", "m_name", "Type", "Weight", "MaxStack", "Value", "Teleportable" },
                new[] { VanillaNames.Entrails, VanillaNames.Bloodbag }.Select(n =>
                {
                    ItemDrop i = items.FirstOrDefault(x => x.name == n);
                    if (i == null) return new object[] { n, false };
                    ItemDrop.ItemData.SharedData s = i.m_itemData.m_shared;
                    return new object[] { n, true, Token(s.m_name), s.m_itemType, s.m_weight, s.m_maxStackSize, s.m_value, s.m_teleportable };
                }));

            sb.AppendLine("### Fishing baits").AppendLine();
            Table(sb, new[] { "Prefab", "m_name", "MaxStack", "Recipe output", "Station", "Resources", "Trophy ingredients" },
                items.Where(i => i.name.StartsWith(VanillaNames.FishingBaitPrefix, StringComparison.Ordinal)).OrderBy(i => i.name, StringComparer.Ordinal).Select(i =>
                {
                    Recipe r = ObjectDB.instance.GetRecipe(i.m_itemData);
                    int trophies = r == null ? 0 : r.m_resources.Count(x => x.m_resItem != null && x.m_resItem.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy);
                    return new object[] { i.name, Token(i.m_itemData.m_shared.m_name), i.m_itemData.m_shared.m_maxStackSize, r != null ? (object)r.m_amount : "no recipe",
                        StationText(r), ResourcesText(r), r != null ? (object)trophies : "-" };
                }));

            sb.AppendLine("### Padded armor").AppendLine();
            DurabilityTable(sb, items.Where(i => i.name.Contains("Padded")).OrderBy(i => i.name, StringComparer.Ordinal).ToList());

            sb.AppendLine("### Bronze / iron weapons and shields").AppendLine();
            var weapons = new List<ItemDrop>();
            var missing = new List<string>();
            foreach (string n in ProbeWeapons)
            {
                ItemDrop i = items.FirstOrDefault(x => x.name == n);
                if (i != null) weapons.Add(i); else missing.Add(n);
            }
            if (missing.Count > 0) sb.AppendLine($"Missing: {string.Join(", ", missing)}").AppendLine();
            DurabilityTable(sb, weapons);
        }

        private static void DurabilityTable(StringBuilder sb, List<ItemDrop> list)
        {
            Table(sb, new[] { "Prefab", "m_name", "Type", "UseDurability", "Max durability per quality", "DurabilityPerLevel", "Armor (+/lvl)", "Damages", "Station", "Resources (amount, +perLevel)" },
                list.Select(i =>
                {
                    ItemDrop.ItemData.SharedData s = i.m_itemData.m_shared;
                    int maxQ = Math.Max(1, s.m_maxQuality);
                    string perQ = string.Join(" / ", Enumerable.Range(1, maxQ).Select(q => F(i.m_itemData.GetMaxDurability(q))));
                    Recipe r = ObjectDB.instance.GetRecipe(i.m_itemData);
                    return new object[] { i.name, Token(s.m_name), s.m_itemType, s.m_useDurability, perQ, s.m_durabilityPerLevel, $"{F(s.m_armor)} (+{F(s.m_armorPerLevel)})",
                        Damages(s.m_damages), StationText(r), ResourcesText(r) };
                }));
        }

        private static string RecipeText(ItemDrop i)
        {
            Recipe r = ObjectDB.instance.GetRecipe(i.m_itemData);
            return r == null ? "no recipe" : $"{StationText(r)}: {ResourcesText(r)}";
        }

        private static string StationText(Recipe r)
        {
            if (r == null) return "-";
            string station = r.m_craftingStation ? $"{r.m_craftingStation.name} ({r.m_craftingStation.m_name}) lvl {r.m_minStationLevel}" : "inventory";
            if (r.m_repairStation) station += $", repair {r.m_repairStation.name}";
            return station;
        }

        private static string ResourcesText(Recipe r)
        {
            if (r == null) return "-";
            return string.Join(", ", r.m_resources.Select(x =>
            {
                if (x.m_resItem == null) return "null";
                bool trophy = x.m_resItem.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy;
                return $"{x.m_resItem.name} x{x.m_amount} (+{x.m_amountPerLevel}/lvl){(trophy ? " [trophy]" : "")}";
            }));
        }

        // ------------------------------------------------------------------ 5. themes, locations

        private static void DumpLocations(StringBuilder sb)
        {
            sb.AppendLine("### Room.Theme").AppendLine();
            Dictionary<Room.Theme, int> roomsPerTheme = new Dictionary<Room.Theme, int>();
            var dvergrRooms = new List<string>();
            string roomsNote = null;
            try
            {
                FieldInfo roomPrefab = typeof(DungeonDB.RoomData).GetField("m_prefab");
                foreach (DungeonDB.RoomData rd in DungeonDB.GetRooms())
                {
                    roomsPerTheme.TryGetValue(rd.m_theme, out int n);
                    roomsPerTheme[rd.m_theme] = n + 1;
                    if (rd.m_theme == Room.Theme.DvergerTown || rd.m_theme == Room.Theme.DvergerBoss)
                        dvergrRooms.Add($"{SoftRefName(roomPrefab?.GetValue(rd))} ({rd.m_theme}{(rd.m_enabled ? "" : ", disabled")})");
                }
            }
            catch (Exception e)
            {
                roomsNote = "DungeonDB rooms unavailable: " + e.Message;
            }

            Table(sb, new[] { "Name", "Value", "Hex", "Rooms in DungeonDB" },
                Enum.GetValues(typeof(Room.Theme)).Cast<Room.Theme>().Select(t =>
                    new object[] { t, (int)t, "0x" + ((int)t).ToString("X", CultureInfo.InvariantCulture), roomsPerTheme.TryGetValue(t, out int n) ? n : 0 }));
            if (roomsNote != null) sb.AppendLine(roomsNote).AppendLine();
            if (dvergrRooms.Count > 0)
            {
                dvergrRooms.Sort(StringComparer.Ordinal);
                sb.AppendLine($"<details><summary>DvergerTown / DvergerBoss rooms ({dvergrRooms.Count})</summary>").AppendLine();
                sb.AppendLine(string.Join(", ", dvergrRooms)).AppendLine();
                sb.AppendLine("</details>").AppendLine();
            }

            sb.AppendLine("### Locations").AppendLine();
            if (ZoneSystem.instance == null)
            {
                sb.AppendLine("ZoneSystem.instance is null.").AppendLine();
                return;
            }

            // ZoneLocation.m_prefab is a SoftReference<GameObject> from an assembly the project does not reference.
            FieldInfo prefabField = typeof(ZoneSystem.ZoneLocation).GetField("m_prefab");
            sb.AppendLine($"ZoneSystem.m_locations: {ZoneSystem.instance.m_locations.Count} entries.").AppendLine();
            var rows = new List<object[]>();
            foreach (ZoneSystem.ZoneLocation loc in ZoneSystem.instance.m_locations)
            {
                object softRef = prefabField?.GetValue(loc);
                string prefabName = !string.IsNullOrEmpty(loc.m_prefabName) ? loc.m_prefabName : SoftRefName(softRef);
                if (!(ContainsCi(prefabName, "dvergr") || ContainsCi(prefabName, "mistlands") || ContainsCi(loc.m_name, "dvergr") || ContainsCi(loc.m_name, "mistlands"))) continue;
                string dungeon;
                try { dungeon = InspectLocationPrefab(softRef); }
                catch (Exception e) { dungeon = "error: " + e.Message; }
                rows.Add(new object[] { loc.m_name, prefabName, loc.m_enable, loc.m_biome, loc.m_quantity, loc.m_unique, loc.m_interiorRadius, loc.m_exteriorRadius, dungeon });
            }
            Table(sb, new[] { "m_name", "m_prefabName", "Enabled", "Biome", "Quantity", "Unique", "InteriorRadius", "ExteriorRadius", "DungeonGenerator" }, rows);
        }

        private static string InspectLocationPrefab(object softRef)
        {
            if (softRef == null) return "no m_prefab";
            Type t = softRef.GetType();
            t.GetMethod("Load", Type.EmptyTypes)?.Invoke(softRef, null);
            try
            {
                GameObject asset = t.GetProperty("Asset")?.GetValue(softRef, null) as GameObject;
                if (asset == null) return "prefab did not load";
                DungeonGenerator[] dgs = asset.GetComponentsInChildren<DungeonGenerator>(true);
                if (dgs.Length == 0) return "none";
                return string.Join("; ", dgs.Select(dg =>
                    $"{RelPath(dg.transform, asset.transform)}: themes={dg.m_themes}, algorithm={dg.m_algorithm}, rooms {dg.m_minRooms}-{dg.m_maxRooms}, " +
                    $"zoneSize={V(dg.m_zoneSize)}, tile={F(dg.m_tileWidth)}, alternative={dg.m_alternativeFunctionality}"));
            }
            finally
            {
                t.GetMethod("Release", Type.EmptyTypes)?.Invoke(softRef, null);
            }
        }

        private static string SoftRefName(object softRef)
        {
            if (softRef == null) return "?";
            try { return softRef.GetType().GetProperty("Name")?.GetValue(softRef, null) as string ?? "?"; }
            catch { return "?"; }
        }

        // ------------------------------------------------------------------ 6. shaders

        private sealed class RenderProbe
        {
            public string Name;
            public Material Material;
            public Mesh Mesh;
            public string ImmediateError;
        }

        private static List<RenderProbe> DumpShaders(StringBuilder sb, List<GameObject> prefabs)
        {
            // Usage census first so prefab-only shaders can still be instancing-tested below.
            var slots = new Dictionary<string, int>(StringComparer.Ordinal);
            var users = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (GameObject p in prefabs)
            {
                foreach (Renderer r in p.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material m in r.sharedMaterials)
                    {
                        if (m == null || m.shader == null) continue;
                        string n = m.shader.name;
                        SeenShaders[n] = m.shader;
                        slots.TryGetValue(n, out int c);
                        slots[n] = c + 1;
                        if (!users.TryGetValue(n, out HashSet<string> set)) users[n] = set = new HashSet<string>(StringComparer.Ordinal);
                        set.Add(p.name);
                    }
                }
            }

            string configured = MWConfig.Performance.ShaderCandidates.BoxedValue as string ?? "";
            List<string> candidates = configured.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).Concat(RequiredShaders).Distinct(StringComparer.Ordinal).ToList();

            sb.AppendLine($"`SystemInfo.supportsInstancing` = {SystemInfo.supportsInstancing}. Candidates: `Performance.ShaderCandidates` (`{configured}`) + {string.Join(", ", RequiredShaders)}.").AppendLine();
            sb.AppendLine("Immediate test: `new Material(shader) { enableInstancing = true }`, read back, then one `Graphics.RenderMeshInstanced` of a quad 2 m in front of the camera; " +
                          "warnings/errors logged during the call are captured. A deferred check over the next frames is appended at the end of this file.").AppendLine();

            Mesh quad = BuildQuad();
            TempObjects.Add(quad);
            var probes = new List<RenderProbe>();
            var rows = new List<object[]>();
            foreach (string name in candidates)
            {
                Shader found = Shader.Find(name);
                Shader shader = found;
                if (shader == null) SeenShaders.TryGetValue(name, out shader);
                if (shader == null)
                {
                    rows.Add(new object[] { name, "null", "no", "-", "-", "-", "-", "-", "-" });
                    continue;
                }

                var probe = new RenderProbe { Name = name, Mesh = quad };
                string readBack = "-", render = "-";
                var logs = new List<string>();
                Application.LogCallback cb = (msg, stack, type) => { if (type != LogType.Log) logs.Add($"{type}: {msg}"); };
                Application.logMessageReceived += cb;
                try
                {
                    probe.Material = new Material(shader) { name = "MW_InstancingProbe_" + name, enableInstancing = true };
                    TempObjects.Add(probe.Material);
                    readBack = probe.Material.enableInstancing.ToString();
                    RenderProbeOnce(probe);
                    render = "no exception";
                    probes.Add(probe);
                }
                catch (Exception e)
                {
                    probe.ImmediateError = $"{e.GetType().Name}: {e.Message}";
                    render = probe.ImmediateError;
                }
                finally
                {
                    Application.logMessageReceived -= cb;
                }

                bool instancingKeyword = false;
                try { instancingKeyword = shader.keywordSpace.keywordNames.Any(k => k.Contains("INSTANCING")); }
                catch { /* keywordSpace missing on some builds */ }

                rows.Add(new object[] { name, found != null ? "found" : "null", found == null ? "yes (from prefab material)" : (SeenShaders.ContainsKey(name) ? "yes" : "no"),
                    shader.isSupported, shader.renderQueue, shader.passCount, instancingKeyword, readBack, render + (logs.Count > 0 ? " / logs: " + string.Join(" ; ", logs) : "") });
            }
            Table(sb, new[] { "Shader", "Shader.Find", "Used by prefabs", "isSupported", "Queue", "Passes", "INSTANCING* keyword", "enableInstancing readback", "RenderMeshInstanced" }, rows);

            sb.AppendLine("### Candidate shader properties").AppendLine();
            foreach (string name in candidates)
                if (SeenShaders.TryGetValue(name, out Shader sh) || (sh = Shader.Find(name)) != null) ShaderProperties(sb, sh);

            sb.AppendLine("### Shader usage across all prefabs").AppendLine();
            Table(sb, new[] { "Shader", "Material slots", "Prefabs", "isSupported", "Sample prefabs" },
                slots.OrderByDescending(kv => kv.Value).Select(kv => new object[]
                {
                    kv.Key, kv.Value, users[kv.Key].Count, SeenShaders[kv.Key].isSupported,
                    string.Join(", ", users[kv.Key].OrderBy(x => x, StringComparer.Ordinal).Take(5)),
                }));
            return probes;
        }

        private static void RenderProbeOnce(RenderProbe probe)
        {
            Camera cam = Camera.main;
            Vector3 pos = cam ? cam.transform.position + cam.transform.forward * 2f : Vector3.zero;
            Quaternion rot = cam ? Quaternion.LookRotation(-cam.transform.forward) : Quaternion.identity;
            var rp = new RenderParams(probe.Material)
            {
                worldBounds = new Bounds(pos, Vector3.one * 2f),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };
            Graphics.RenderMeshInstanced(rp, probe.Mesh, 0, new[] { Matrix4x4.TRS(pos, rot, Vector3.one * 0.5f) });
        }

        private static IEnumerator DeferredRenderCheck(string path, List<RenderProbe> probes)
        {
            _deferredCheckRunning = true;
            var sb = new StringBuilder();
            sb.AppendLine().AppendLine("## 6b. Deferred render check").AppendLine();
            sb.AppendLine("Each probe material is drawn alone for 6 frames; warnings and errors logged from any thread during its window are attributed to it.").AppendLine();
            sb.AppendLine("| Shader | Result |").AppendLine("| --- | --- |");

            var logs = new List<string>();
            Application.LogCallback cb = (msg, stack, type) => { if (type != LogType.Log) lock (logs) logs.Add($"{type}: {msg}"); };
            Application.logMessageReceivedThreaded += cb;
            try
            {
                foreach (RenderProbe probe in probes)
                {
                    lock (logs) logs.Clear();
                    string error = null;
                    for (int frame = 0; frame < 6; frame++)
                    {
                        if (frame < 5 && error == null && probe.Material != null)
                        {
                            try { RenderProbeOnce(probe); }
                            catch (Exception e) { error = $"{e.GetType().Name}: {e.Message}"; }
                        }
                        yield return null;
                    }
                    string captured;
                    lock (logs) captured = string.Join(" ; ", logs.Distinct());
                    string result = error ?? (captured.Length == 0 ? "ok (no warnings/errors)" : "logged: " + captured);
                    sb.AppendLine($"| {Esc(probe.Name)} | {Esc(result)} |");
                }
            }
            finally
            {
                Application.logMessageReceivedThreaded -= cb;
                foreach (UnityEngine.Object o in TempObjects) if (o != null) UnityEngine.Object.Destroy(o);
                TempObjects.Clear();
                _deferredCheckRunning = false;
            }

            try
            {
                File.AppendAllText(path, sb.ToString(), new UTF8Encoding(false));
                Plugin.Log.LogInfo($"[VanillaDump] deferred render check appended to {path}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[VanillaDump] cannot append deferred render check: {e.Message}");
            }
        }

        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "MW_InstancingProbeQuad" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void ShaderProperties(StringBuilder sb, Shader sh)
        {
            if (sh == null || !ShaderPropsPrinted.Add(sh.name)) return;
            int count = sh.GetPropertyCount();
            sb.AppendLine($"`{sh.name}` ({count} properties): " +
                          string.Join(", ", Enumerable.Range(0, count).Select(i => $"`{sh.GetPropertyName(i)}` {sh.GetPropertyType(i)}"))).AppendLine();
        }

        // ------------------------------------------------------------------ 7. layers

        private static void DumpLayers(StringBuilder sb, List<GameObject> prefabs)
        {
            Table(sb, new[] { "Index", "Name" }, Enumerable.Range(0, 32).Select(i => new object[] { i, LayerMask.LayerToName(i) is string n && n.Length > 0 ? n : "-" }));

            sb.AppendLine($"`Physics.queriesHitTriggers` = {Physics.queriesHitTriggers}. Per decompiled Attack.cs: `DoAreaAttack` uses OverlapSphere with `QueryTriggerInteraction.UseGlobal`, " +
                          "`DoMeleeAttack` uses SphereCast/Raycast with `QueryTriggerInteraction.Ignore` (melee sweeps never hit triggers). " +
                          "Aoe uses OverlapBox/OverlapSphere (UseGlobal) plus OnTriggerEnter/Stay when `m_useTriggers`. Projectile uses SphereCastAll/RaycastAll (UseGlobal).").AppendLine();

            sb.AppendLine("### Masks").AppendLine();
            sb.AppendLine("Static masks are lazily initialised by vanilla (0 until the first attack/projectile); the reconstructed value repeats the layer list from the decompiled source.").AppendLine();
            Table(sb, new[] { "Mask", "Current value", "Reconstructed", "Layers" }, new[]
            {
                MaskRow("Attack.m_attackMask", Attack.m_attackMask, VanillaNames.LayerDefault, VanillaNames.LayerStaticSolid, "Default_small", VanillaNames.LayerPiece, "piece_nonsolid", VanillaNames.LayerCharacter, "character_net", "character_ghost", VanillaNames.LayerHitbox, VanillaNames.LayerCharacterNoenv, "vehicle"),
                MaskRow("Attack.m_attackMaskTerrain", Attack.m_attackMaskTerrain, VanillaNames.LayerDefault, VanillaNames.LayerStaticSolid, "Default_small", VanillaNames.LayerPiece, "piece_nonsolid", VanillaNames.LayerTerrain, VanillaNames.LayerCharacter, "character_net", "character_ghost", VanillaNames.LayerHitbox, VanillaNames.LayerCharacterNoenv, "vehicle"),
                MaskRow("Attack.m_attackMaskCharacters", Attack.m_attackMaskCharacters, VanillaNames.LayerCharacter, "character_net", "character_ghost", VanillaNames.LayerHitbox, VanillaNames.LayerCharacterNoenv, "vehicle"),
                MaskRow("Projectile.s_rayMaskSolids", Projectile.s_rayMaskSolids, VanillaNames.LayerDefault, VanillaNames.LayerStaticSolid, "Default_small", VanillaNames.LayerPiece, "piece_nonsolid", VanillaNames.LayerTerrain, VanillaNames.LayerCharacter, "character_net", "character_ghost", VanillaNames.LayerHitbox, VanillaNames.LayerCharacterNoenv, "vehicle"),
                MaskRow("Aoe.m_rayMask (hitCharacters part)", -1, VanillaNames.LayerCharacter, "character_net", "character_ghost"),
                MaskRow("Aoe.m_rayMask (hitProps part)", -1, VanillaNames.LayerDefault, VanillaNames.LayerStaticSolid, "Default_small", VanillaNames.LayerPiece, VanillaNames.LayerHitbox, VanillaNames.LayerCharacterNoenv, "vehicle"),
                MaskRow("Aoe.m_rayMask (hitTerrain part)", -1, VanillaNames.LayerTerrain),
            });

            ProxyLayer.Resolve();
            sb.AppendLine("### Worm proxy layer (phase 3)").AppendLine();
            sb.AppendLine($"{ProxyLayer.Report}. `Physics.GetIgnoreLayerCollision` read at runtime; see decisions.md 47.").AppendLine();

            sb.AppendLine("### Attack setup of a few vanilla items").AppendLine();
            var rows = new List<object[]>();
            foreach (string n in ProbeAttackItems)
            {
                GameObject go = ObjectDB.instance.GetItemPrefab(n);
                ItemDrop id = go ? go.GetComponent<ItemDrop>() : null;
                if (id == null) { rows.Add(new object[] { n, "missing" }); continue; }
                ItemDrop.ItemData.SharedData s = id.m_itemData.m_shared;
                foreach (KeyValuePair<string, Attack> kv in new[] { new KeyValuePair<string, Attack>("primary", s.m_attack), new KeyValuePair<string, Attack>("secondary", s.m_secondaryAttack) })
                {
                    Attack a = kv.Value;
                    if (a == null || (a.m_attackType == Attack.AttackType.None && string.IsNullOrEmpty(a.m_attackAnimation))) continue;
                    rows.Add(new object[] { n, kv.Key, a.m_attackType, a.m_hitTerrain, a.m_attackRange, a.m_attackRayWidth, a.m_attackProjectile ? a.m_attackProjectile.name : "-",
                        a.m_attackProjectile ? ProjectileText(a.m_attackProjectile) : "-" });
                }
            }
            Table(sb, new[] { "Item", "Attack", "Type", "HitTerrain", "Range", "RayWidth", "Projectile", "Projectile setup" }, rows);

            sb.AppendLine("### Aoe prefabs (first 15)").AppendLine();
            Table(sb, new[] { "Prefab", "UseTriggers", "HitCharacters", "HitProps", "HitTerrain", "Radius", "Collider", "Mask layers" },
                prefabs.Select(p => new KeyValuePair<GameObject, Aoe>(p, p.GetComponentInChildren<Aoe>(true))).Where(kv => kv.Value != null).Take(15).Select(kv =>
                {
                    Aoe a = kv.Value;
                    int mask = (a.m_hitCharacters ? LayerMask.GetMask(VanillaNames.LayerCharacter, "character_net", "character_ghost") : 0)
                             | (a.m_hitProps ? LayerMask.GetMask(VanillaNames.LayerDefault, VanillaNames.LayerStaticSolid, "Default_small", VanillaNames.LayerPiece, VanillaNames.LayerHitbox, VanillaNames.LayerCharacterNoenv, "vehicle") : 0)
                             | (a.m_hitTerrain ? LayerMask.GetMask(VanillaNames.LayerTerrain) : 0);
                    Collider col = a.GetComponent<Collider>();
                    return new object[] { kv.Key.name, a.m_useTriggers, a.m_hitCharacters, a.m_hitProps, a.m_hitTerrain, a.m_radius,
                        col ? $"{col.GetType().Name}{(col.isTrigger ? " trigger" : "")}" : "-", MaskLayers(mask) };
                }));
        }

        private static string ProjectileText(GameObject go)
        {
            Projectile pr = go.GetComponent<Projectile>();
            if (pr == null) return ComponentList(go);
            return $"rayRadius {F(pr.m_rayRadius)}, aoe {F(pr.m_aoe)}, hitFriendly {pr.m_hitFriendly}, ttl {F(pr.m_ttl)}, layer {LayerMask.LayerToName(go.layer)}, " +
                   $"spawnOnHit {(pr.m_spawnOnHit ? pr.m_spawnOnHit.name : "-")}";
        }

        private static object[] MaskRow(string label, int current, params string[] layers)
        {
            int rebuilt = LayerMask.GetMask(layers);
            return new object[] { label, current < 0 ? "per instance" : current.ToString(CultureInfo.InvariantCulture), rebuilt, MaskLayers(rebuilt) };
        }

        private static string MaskLayers(int mask)
        {
            return string.Join(", ", Enumerable.Range(0, 32).Where(i => (mask & (1 << i)) != 0).Select(i => $"{i}:{LayerMask.LayerToName(i)}"));
        }

        // ------------------------------------------------------------------ 8. versions

        private static void DumpVersions(StringBuilder sb)
        {
            string pipeline;
            try { pipeline = GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.name : "built-in"; }
            catch (Exception e) { pipeline = "error: " + e.Message; }

            Table(sb, new[] { "Key", "Value" }, new[]
            {
                new object[] { "Application.unityVersion", Application.unityVersion },
                new object[] { "Application.version", Application.version },
                new object[] { "Version.CurrentVersion", SafeGameVersion() },
                new object[] { "Version.GetVersionString(true)", Safe(() => global::Version.GetVersionString(true)) },
                new object[] { "Jotunn", Jotunn.Main.Version },
                new object[] { "BepInEx", typeof(BaseUnityPlugin).Assembly.GetName().Version },
                new object[] { "SystemInfo.supportsInstancing", SystemInfo.supportsInstancing },
                new object[] { "SystemInfo.graphicsDeviceType", SystemInfo.graphicsDeviceType },
                new object[] { "SystemInfo.graphicsDeviceName", SystemInfo.graphicsDeviceName },
                new object[] { "SystemInfo.graphicsDeviceVersion", SystemInfo.graphicsDeviceVersion },
                new object[] { "SystemInfo.operatingSystem", SystemInfo.operatingSystem },
                new object[] { "Render pipeline", pipeline },
                new object[] { "Graphics.RenderMeshInstanced available", typeof(Graphics).GetMethods().Any(m => m.Name == "RenderMeshInstanced") },
                new object[] { "Dedicated server", ZNet.instance != null && ZNet.instance.IsDedicated() },
            });
        }

        private static string SafeGameVersion() => Safe(() => global::Version.CurrentVersion.ToString());

        // ------------------------------------------------------------------ 1 (extra). material values

        private static bool IsCreatureCandidate(string name) =>
            name.Contains(VanillaNames.Seeker) || name.Contains(VanillaNames.Tick) || name.Contains(VanillaNames.Leech) || name.Contains(VanillaNames.Blob);

        private static string C(Color c) => $"RGBA({F(c.r)}, {F(c.g)}, {F(c.b)}, {F(c.a)})";

        private static string TextureInfo(Texture t)
        {
            string fmt;
            try { fmt = t.graphicsFormat.ToString(); }
            catch (Exception e) { fmt = "error: " + e.Message; }
            return $"{t.name} ({t.width}x{t.height}, filter {t.filterMode}, wrap {t.wrapMode}, aniso {t.anisoLevel}, mips {t.mipmapCount}, format {fmt})";
        }

        private static void MaterialValues(StringBuilder sb, Material m, string rendererPath)
        {
            Shader sh = m.shader;
            sb.AppendLine($"Material values `{m.name}` (renderer {rendererPath}, shader `{(sh ? sh.name : "null")}`). " +
                          $"Active keywords: [{string.Join(" ", m.shaderKeywords ?? new string[0])}].").AppendLine();
            if (sh == null) return;
            var rows = new List<object[]>();
            int count = sh.GetPropertyCount();
            for (int i = 0; i < count; i++)
            {
                string name = sh.GetPropertyName(i);
                ShaderPropertyType type = sh.GetPropertyType(i);
                string value;
                try
                {
                    switch (type)
                    {
                        case ShaderPropertyType.Float:
                            value = F(m.GetFloat(name));
                            break;
                        case ShaderPropertyType.Range:
                        {
                            Vector2 lim = sh.GetPropertyRangeLimits(i);
                            value = $"{F(m.GetFloat(name))} (range {F(lim.x)}..{F(lim.y)})";
                            break;
                        }
                        case ShaderPropertyType.Color:
                            value = C(m.GetColor(name));
                            break;
                        case ShaderPropertyType.Vector:
                            value = C(m.GetVector(name));
                            break;
                        case ShaderPropertyType.Int:
                            value = m.GetInteger(name).ToString(CultureInfo.InvariantCulture);
                            break;
                        case ShaderPropertyType.Texture:
                        {
                            Texture t = m.GetTexture(name);
                            value = (t != null ? TextureInfo(t) : "none") + $"; scale {m.GetTextureScale(name)}, offset {m.GetTextureOffset(name)}";
                            break;
                        }
                        default:
                            value = "?";
                            break;
                    }
                }
                catch (Exception e)
                {
                    value = "error: " + e.Message;
                }
                rows.Add(new object[] { name, type, value });
            }
            Table(sb, new[] { "Property", "Type", "Value" }, rows);
        }

        // ------------------------------------------------------------------ 9. Custom/Creature shader

        private static void DumpCreatureShader(StringBuilder sb)
        {
            if (!SeenShaders.TryGetValue(VanillaNames.ShaderCreature, out Shader sh) || sh == null)
            {
                sb.AppendLine($"**`{VanillaNames.ShaderCreature}` was not seen on any material.**").AppendLine();
                return;
            }

            sb.AppendLine("### Keyword space").AppendLine();
            try
            {
                string[] kws = sh.keywordSpace.keywordNames;
                sb.AppendLine($"{kws.Length} keywords: {string.Join(", ", kws.Select(k => "`" + k + "`"))}").AppendLine();
            }
            catch (Exception e)
            {
                sb.AppendLine("keywordSpace unavailable: " + e.Message).AppendLine();
            }

            sb.AppendLine("### Property defaults and range limits").AppendLine();
            var rows = new List<object[]>();
            foreach (string name in new[] { "_Cutoff", "_Glossiness", "_MetalGloss", "_NoiseGlowEdge", "_BumpScale" })
            {
                int idx = sh.FindPropertyIndex(name);
                if (idx < 0) { rows.Add(new object[] { name, "absent", "-", "-" }); continue; }
                string def, lim;
                try { def = F(sh.GetPropertyDefaultFloatValue(idx)); } catch (Exception e) { def = "error: " + e.Message; }
                try { Vector2 l = sh.GetPropertyRangeLimits(idx); lim = $"{F(l.x)} .. {F(l.y)}"; } catch (Exception e) { lim = "error: " + e.Message; }
                rows.Add(new object[] { name, sh.GetPropertyType(idx), def, lim });
            }
            Table(sb, new[] { "Property", "Type", "Default", "Range limits" }, rows);
        }

        // ------------------------------------------------------------------ 10. particles

        private static readonly string[] PriorityFx =
        {
            "fx_TickBloodHit", "fx_tick_death", "fx_seeker_hurt", "fx_seeker_death", "fx_babyseeker_hurt", "fx_babyseeker_death",
            "vfx_leech_hit", "vfx_leech_death", "vfx_blob_hit", "vfx_blob_death", "vfx_boar_hit", "vfx_BloodHit", "vfx_BloodDeath",
            "fx_bloodweapon_hit", "vfx_Poison", "vfx_Slimed", "fx_Puke", "fx_Eat_Green", "vfx_GuckSackHit",
        };

        private static string Curve(ParticleSystem.MinMaxCurve c)
        {
            switch (c.mode)
            {
                case ParticleSystemCurveMode.Constant: return $"Constant {F(c.constant)}";
                case ParticleSystemCurveMode.TwoConstants: return $"TwoConstants {F(c.constantMin)} .. {F(c.constantMax)}";
                case ParticleSystemCurveMode.Curve:
                    return $"Curve x{F(c.curveMultiplier)} [{CurveKeys(c.curve)}]";
                default:
                    return $"TwoCurves x{F(c.curveMultiplier)} min [{CurveKeys(c.curveMin)}] max [{CurveKeys(c.curveMax)}]";
            }
        }

        private static string CurveKeys(AnimationCurve a) =>
            a == null ? "" : string.Join(" ", a.keys.Select(k => $"({F(k.time)}:{F(k.value)})"));

        private static string GradientKeys(Gradient g) =>
            g == null ? "null" : $"mode {g.mode} color [{string.Join(" ", g.colorKeys.Select(k => $"{F(k.time)}:{C(k.color)}"))}] alpha [{string.Join(" ", g.alphaKeys.Select(k => $"{F(k.time)}:{F(k.alpha)}"))}]";

        private static string StartColor(ParticleSystem.MinMaxGradient g)
        {
            switch (g.mode)
            {
                case ParticleSystemGradientMode.Color: return $"Color {C(g.color)}";
                case ParticleSystemGradientMode.TwoColors: return $"TwoColors {C(g.colorMin)} .. {C(g.colorMax)}";
                case ParticleSystemGradientMode.Gradient: return $"Gradient {GradientKeys(g.gradient)}";
                case ParticleSystemGradientMode.TwoGradients: return $"TwoGradients min {GradientKeys(g.gradientMin)} max {GradientKeys(g.gradientMax)}";
                default: return $"{g.mode} {GradientKeys(g.gradient)}";
            }
        }

        private static string ParticleRenderer(ParticleSystem ps)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (r == null) return "no renderer";
            Material m = r.sharedMaterial;
            if (m == null) return $"{r.renderMode}, no sharedMaterial";
            var parts = new List<string> { $"mode {r.renderMode}", $"shader {(m.shader ? m.shader.name : "null")}", $"material {m.name}" };
            if (m.HasProperty("_MainTex"))
            {
                Texture t = m.GetTexture("_MainTex");
                parts.Add("_MainTex " + (t != null ? $"{t.name} ({t.width}x{t.height})" : "none"));
            }
            if (m.HasProperty("_Color")) parts.Add("_Color " + C(m.GetColor("_Color")));
            if (m.HasProperty("_TintColor")) parts.Add("_TintColor " + C(m.GetColor("_TintColor")));
            return string.Join("; ", parts);
        }

        private static void DumpParticles(StringBuilder sb, List<GameObject> prefabs)
        {
            var wanted = new List<GameObject>();
            var missing = new List<string>();
            foreach (string n in PriorityFx)
            {
                GameObject p = FindPrefab(prefabs, n);
                if (p == null) missing.Add(n); else wanted.Add(p);
            }
            foreach (GameObject p in prefabs)
            {
                string lower = p.name.ToLowerInvariant();
                if (!FxPrefixes.Any(x => lower.StartsWith(x, StringComparison.Ordinal))) continue;
                if (!(lower.Contains("blood") || lower.Contains("hurt") || (lower.Contains("hit") && !lower.Contains("white")))) continue;
                if (!wanted.Contains(p)) wanted.Add(p);
            }
            wanted.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            sb.AppendLine($"Detailed: the priority list ({PriorityFx.Length}) plus every sfx_/vfx_/fx_ prefab whose name contains blood, hurt or hit ({wanted.Count} prefabs). " +
                          $"Missing priority prefabs: {(missing.Count == 0 ? "none" : string.Join(", ", missing))}.").AppendLine();

            var rows = new List<object[]>();
            foreach (GameObject p in wanted)
            {
                bool priority = PriorityFx.Contains(p.name);
                ParticleSystem[] systems = p.GetComponentsInChildren<ParticleSystem>(true);
                if (systems.Length == 0) { rows.Add(new object[] { p.name, priority ? "yes" : "", "-", "no ParticleSystem" }); continue; }
                foreach (ParticleSystem ps in systems)
                {
                    string detail;
                    try
                    {
                        ParticleSystem.MainModule main = ps.main;
                        detail = $"startColor: {StartColor(main.startColor)} | startSize: {Curve(main.startSize)} | startLifetime: {Curve(main.startLifetime)} | " +
                                 $"maxParticles {main.maxParticles} | simulationSpace {main.simulationSpace} | renderer: {ParticleRenderer(ps)}";
                    }
                    catch (Exception e) { detail = "error: " + e.Message; }
                    rows.Add(new object[] { p.name, priority ? "yes" : "", RelPath(ps.transform, p.transform), detail });
                }
            }
            Table(sb, new[] { "Prefab", "Priority", "ParticleSystem", "Details" }, rows);
        }

        // ------------------------------------------------------------------ 11. referenced effects

        private static IEnumerable<GameObject> HumanoidItems(Humanoid h)
        {
            var list = new List<GameObject>();
            void Add(IEnumerable<GameObject> gos) { if (gos != null) list.AddRange(gos.Where(g => g != null)); }
            Add(h.m_defaultItems);
            Add(h.m_randomWeapon);
            Add(h.m_randomArmor);
            Add(h.m_randomShield);
            if (h.m_randomSets != null) foreach (Humanoid.ItemSet set in h.m_randomSets) Add(set.m_items);
            if (h.m_randomItems != null) Add(h.m_randomItems.Select(r => r.m_prefab));
            if (h.m_unarmedWeapon != null) list.Add(h.m_unarmedWeapon.gameObject);
            return list;
        }

        private static void CollectEffects(object o, string label, Dictionary<GameObject, HashSet<string>> map, int depth)
        {
            if (o == null || depth > 3) return;
            foreach (FieldInfo f in o.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.FieldType == typeof(EffectList))
                {
                    var list = f.GetValue(o) as EffectList;
                    if (list?.m_effectPrefabs == null) continue;
                    foreach (EffectList.EffectData e in list.m_effectPrefabs)
                    {
                        if (e == null || e.m_prefab == null) continue;
                        if (!map.TryGetValue(e.m_prefab, out HashSet<string> set)) map[e.m_prefab] = set = new HashSet<string>(StringComparer.Ordinal);
                        set.Add(label + "." + f.Name);
                    }
                }
                else if (f.FieldType == typeof(Attack))
                {
                    CollectEffects(f.GetValue(o), label + "." + f.Name, map, depth + 1);
                }
            }
        }

        private static void DumpReferencedEffects(StringBuilder sb, List<GameObject> prefabs)
        {
            var map = new Dictionary<GameObject, HashSet<string>>();
            var creatureNames = new List<string>();
            foreach (GameObject p in prefabs)
            {
                if (!IsCreatureCandidate(p.name) || p.GetComponent<Character>() == null) continue;
                creatureNames.Add(p.name);
                foreach (MonoBehaviour mb in p.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb != null) CollectEffects(mb, p.name + ":" + mb.GetType().Name, map, 0);
                if (p.GetComponent<Character>() is Humanoid h)
                    foreach (GameObject item in HumanoidItems(h))
                    {
                        ItemDrop id = item.GetComponent<ItemDrop>();
                        if (id != null) CollectEffects(id.m_itemData.m_shared, p.name + ":item " + item.name, map, 0);
                    }
            }

            creatureNames.Sort(StringComparer.Ordinal);
            sb.AppendLine($"Creatures scanned (reflection over every public `EffectList` field of their components and of their Humanoid items SharedData / Attack): {string.Join(", ", creatureNames)}. " +
                          "Only sfx_ / vfx_ / fx_ prefabs are listed, whether or not they are in ZNetScene.").AppendLine();

            var rows = new List<object[]>();
            foreach (KeyValuePair<GameObject, HashSet<string>> kv in map.OrderBy(k => k.Key.name, StringComparer.Ordinal))
            {
                GameObject p = kv.Key;
                string lower = p.name.ToLowerInvariant();
                if (!FxPrefixes.Any(x => lower.StartsWith(x, StringComparison.Ordinal))) continue;
                string clips = string.Join(", ", p.GetComponentsInChildren<ZSFX>(true)
                    .Select(z => "[" + (z.m_audioClips == null ? "" : string.Join(", ", z.m_audioClips.Where(a => a != null).Select(a => a.name))) + "]"));
                rows.Add(new object[] { p.name, string.Join("; ", kv.Value.OrderBy(x => x, StringComparer.Ordinal)),
                    ZNetScene.instance.m_prefabs.Contains(p) ? "net" : (ZNetScene.instance.m_nonNetViewPrefabs.Contains(p) ? "non-net" : "NOT in ZNetScene"),
                    ComponentList(p), clips.Length > 0 ? clips : "-" });
            }
            Table(sb, new[] { "Prefab", "Referenced by", "ZNetScene", "Root components", "ZSFX clips" }, rows);
        }

        // ------------------------------------------------------------------ 12. texture statistics

        private static readonly string[] StatTextureNames = { "babyseeker_d", "Feasting_d", "Feasting_m" };

        private static void DumpTextureStats(StringBuilder sb, List<GameObject> prefabs)
        {
            var targets = new List<KeyValuePair<string, Texture>>();
            var seen = new HashSet<Texture>();
            foreach (Texture t in Resources.FindObjectsOfTypeAll<Texture>())
                if (t != null && StatTextureNames.Any(n => string.Equals(n, t.name, StringComparison.OrdinalIgnoreCase)) && seen.Add(t))
                    targets.Add(new KeyValuePair<string, Texture>("by name", t));
            foreach (string cn in new[] { VanillaNames.Leech, VanillaNames.Blob })
            {
                GameObject p = FindPrefab(prefabs, cn);
                if (p == null) { sb.AppendLine($"`{cn}` missing from ZNetScene.").AppendLine(); continue; }
                foreach (Renderer r in p.GetComponentsInChildren<Renderer>(true))
                    foreach (Material m in r.sharedMaterials)
                    {
                        Texture t = m != null && m.HasProperty(VanillaNames.PropMainTex) ? m.GetTexture(VanillaNames.PropMainTex) : null;
                        if (t != null && seen.Add(t)) targets.Add(new KeyValuePair<string, Texture>(cn + " _MainTex", t));
                    }
            }
            foreach (string n in StatTextureNames)
                if (!targets.Any(kv => string.Equals(kv.Value.name, n, StringComparison.OrdinalIgnoreCase)))
                    sb.AppendLine($"Texture `{n}` not found among loaded textures.").AppendLine();

            sb.AppendLine("Each texture is blitted to a temporary ARGB32 RenderTexture (sRGB read/write matched to the source format so the bytes are the stored values), " +
                          "read back with ReadPixels; values are 0..255. Temporary objects are released.").AppendLine();
            var rows = new List<object[]>();
            foreach (KeyValuePair<string, Texture> kv in targets)
            {
                try
                {
                    foreach (object[] row in ChannelStats(kv.Key, kv.Value)) rows.Add(row);
                }
                catch (Exception e)
                {
                    rows.Add(new object[] { kv.Key, kv.Value.name, "error: " + e.Message });
                }
            }
            Table(sb, new[] { "Source", "Texture", "Size / sRGB", "Channel", "Mean", "Min", "Max" }, rows);
        }

        private static IEnumerable<object[]> ChannelStats(string source, Texture t)
        {
            int w = t.width, h = t.height;
            bool srgb = GraphicsFormatUtility.IsSRGBFormat(t.graphicsFormat);
            RenderTexture prev = RenderTexture.active;
            RenderTexture rt = null;
            Texture2D readable = null;
            Color32[] px;
            try
            {
                rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
                Graphics.Blit(t, rt);
                RenderTexture.active = rt;
                readable = new Texture2D(w, h, TextureFormat.RGBA32, false, !srgb);
                readable.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                px = readable.GetPixels32();
            }
            finally
            {
                RenderTexture.active = prev;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (readable != null) UnityEngine.Object.Destroy(readable);
            }

            var sum = new double[4];
            var min = new int[] { 255, 255, 255, 255 };
            var max = new int[4];
            foreach (Color32 c in px)
            {
                int[] v = { c.r, c.g, c.b, c.a };
                for (int i = 0; i < 4; i++)
                {
                    sum[i] += v[i];
                    if (v[i] < min[i]) min[i] = v[i];
                    if (v[i] > max[i]) max[i] = v[i];
                }
            }
            string[] names = { "R", "G", "B", "A" };
            var rows = new List<object[]>();
            for (int i = 0; i < 4; i++)
                rows.Add(new object[] { source, t.name, $"{w}x{h} / {(srgb ? "sRGB" : "linear")}", names[i], px.Length > 0 ? sum[i] / px.Length : 0.0, min[i], max[i] });
            return rows;
        }

        // ------------------------------------------------------------------ 13. status effects

        private static void DumpStatusEffects(StringBuilder sb)
        {
            List<StatusEffect> effects = ObjectDB.instance.m_StatusEffects.Where(e => e != null).ToList();
            sb.AppendLine($"ObjectDB: {effects.Count} status effects, {effects.Count(e => e.m_icon != null)} with an icon. " +
                          $"Candidates for the Infested icon: `{VanillaNames.InfestedIconPreferred}`, else `{VanillaNames.InfestedIconFallback}`, else none. " +
                          $"Choice made at runtime: {Bites.InfestedEffect.IconNote}.").AppendLine();
            Table(sb, new[] { "Name", "m_name", "Type", "Icon (sprite)", "m_ttl", "Category", "Hidden", "Candidate" },
                effects.OrderBy(e => e.name, StringComparer.Ordinal).Select(e => new object[]
                {
                    e.name, Token(e.m_name), e.GetType().Name, e.m_icon != null ? e.m_icon.name : "-", e.m_ttl, e.m_category, e.m_hidden,
                    e.name == VanillaNames.InfestedIconPreferred ? "preferred" : (e.name == VanillaNames.InfestedIconFallback ? "fallback" : string.Empty),
                }));
        }

        // ------------------------------------------------------------------ helpers

        private static void Section(StringBuilder sb, string title, Action<StringBuilder> body)
        {
            sb.AppendLine("## " + title).AppendLine();
            var part = new StringBuilder();
            try
            {
                body(part);
                sb.Append(part);
            }
            catch (Exception e)
            {
                sb.Append(part);
                sb.AppendLine($"> **Section failed:** `{e.GetType().Name}: {Esc(e.Message)}`").AppendLine();
                sb.AppendLine("```").AppendLine(e.ToString()).AppendLine("```").AppendLine();
                Plugin.Log.LogWarning($"[VanillaDump] section '{title}' failed: {e}");
            }
        }

        private static void Table(StringBuilder sb, string[] headers, IEnumerable<object[]> rows)
        {
            sb.Append("| ").Append(string.Join(" | ", headers.Select(Esc))).AppendLine(" |");
            sb.Append('|').Append(string.Concat(headers.Select(_ => " --- |"))).AppendLine();
            int count = 0;
            foreach (object[] row in rows)
            {
                var cells = new string[headers.Length];
                for (int i = 0; i < cells.Length; i++) cells[i] = Esc(Cell(row != null && i < row.Length ? row[i] : null));
                sb.Append("| ").Append(string.Join(" | ", cells)).AppendLine(" |");
                count++;
            }
            if (count == 0) sb.Append("| _(none)_ |").Append(string.Concat(Enumerable.Repeat(" |", headers.Length - 1))).AppendLine();
            sb.AppendLine();
        }

        private static string Cell(object o)
        {
            switch (o)
            {
                case null: return "";
                case float f: return F(f);
                case double d: return d.ToString("0.###", CultureInfo.InvariantCulture);
                case string s: return s;
                default: return Convert.ToString(o, CultureInfo.InvariantCulture);
            }
        }

        private static string Esc(string s) => (s ?? "").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

        private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private static string V(Vector3 v) => $"{F(v.x)} x {F(v.y)} x {F(v.z)}";

        private static string Safe(Func<string> f)
        {
            try { return f(); }
            catch (Exception e) { return "error: " + e.Message; }
        }

        private static bool ContainsCi(string s, string part) => s != null && s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string JoinNonEmpty(params string[] parts) => string.Join(" / ", parts.Where(x => !string.IsNullOrEmpty(x) && x != "-"));

        private static List<GameObject> AllPrefabs()
        {
            var seen = new HashSet<GameObject>();
            var list = new List<GameObject>();
            foreach (GameObject p in ZNetScene.instance.m_prefabs.Concat(ZNetScene.instance.m_nonNetViewPrefabs))
                if (p != null && seen.Add(p)) list.Add(p);
            return list;
        }

        private static GameObject FindPrefab(List<GameObject> prefabs, string name) => prefabs.FirstOrDefault(p => p.name == name);

        private static string Token(string token)
        {
            if (string.IsNullOrEmpty(token)) return "";
            string localized = Safe(() => global::Localization.instance != null ? global::Localization.instance.Localize(token) : token);
            return localized == token ? token : $"{token} ({localized})";
        }

        private static string ComponentList(GameObject go)
        {
            return string.Join(", ", go.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name).Distinct());
        }

        private static string Effects(EffectList list)
        {
            if (list?.m_effectPrefabs == null || list.m_effectPrefabs.Length == 0) return "-";
            return string.Join(", ", list.m_effectPrefabs.Where(e => e != null && e.m_prefab != null)
                .Select(e => e.m_prefab.name + (e.m_enabled ? "" : " (disabled)") + (e.m_variant >= 0 ? $" (variant {e.m_variant})" : "") + (e.m_attach ? " (attach)" : "")));
        }

        private static string Damages(HitData.DamageTypes d)
        {
            object boxed = d;
            var parts = typeof(HitData.DamageTypes).GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => f.FieldType == typeof(float))
                .Select(f => new KeyValuePair<string, float>(f.Name.Replace("m_", ""), (float)f.GetValue(boxed)))
                .Where(kv => kv.Value != 0f)
                .Select(kv => $"{kv.Key} {F(kv.Value)}");
            string s = string.Join(", ", parts);
            return s.Length > 0 ? s : "none";
        }

        private static string NonNormalModifiers(HitData.DamageModifiers mods)
        {
            object boxed = mods;
            var parts = typeof(HitData.DamageModifiers).GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => new KeyValuePair<string, object>(f.Name.Replace("m_", ""), f.GetValue(boxed)))
                .Where(kv => !(kv.Value is HitData.DamageModifier m) || m != HitData.DamageModifier.Normal)
                .Select(kv => $"{kv.Key} {kv.Value}");
            string s = string.Join(", ", parts);
            return s.Length > 0 ? s : "all Normal";
        }

        private static string Textures(Material m)
        {
            var parts = new List<string>();
            foreach (string prop in m.GetTexturePropertyNames())
            {
                Texture t = m.GetTexture(prop);
                if (t != null) parts.Add($"{prop}={TextureInfo(t)}");
            }
            return string.Join(", ", parts);
        }

        private static string RelPath(Transform t, Transform root)
        {
            if (t == root) return root.name;
            var names = new List<string>();
            for (Transform x = t; x != null && x != root; x = x.parent) names.Add(x.name);
            names.Reverse();
            return string.Join("/", names);
        }

        private static bool ActiveUnder(Transform t, Transform root)
        {
            for (Transform x = t; x != null; x = x.parent)
            {
                if (!x.gameObject.activeSelf && x != root) return false;
                if (x == root) break;
            }
            return true;
        }

        private static string Colliders(GameObject p)
        {
            var parts = new List<string>();
            foreach (Collider c in p.GetComponentsInChildren<Collider>(true))
            {
                string dims;
                switch (c)
                {
                    case CapsuleCollider cc: dims = $"r {F(cc.radius)} h {F(cc.height)} dir {cc.direction} center {V(cc.center)}"; break;
                    case SphereCollider sc: dims = $"r {F(sc.radius)} center {V(sc.center)}"; break;
                    case BoxCollider bc: dims = $"size {V(bc.size)} center {V(bc.center)}"; break;
                    case MeshCollider mc: dims = $"mesh {(mc.sharedMesh ? mc.sharedMesh.name : "null")} convex {mc.convex}"; break;
                    default: dims = ""; break;
                }
                parts.Add($"{RelPath(c.transform, p.transform)}: {c.GetType().Name} {dims}{(c.isTrigger ? " trigger" : "")} layer {LayerMask.LayerToName(c.gameObject.layer)} " +
                          $"scale {V(c.transform.lossyScale)}{(c.enabled ? "" : " (disabled)")}");
            }
            return parts.Count > 0 ? string.Join("; ", parts) : "none";
        }

        /// <summary>Renderers belonging only to LOD1+ of any LODGroup under the prefab (LOD0 is kept).</summary>
        private static HashSet<Renderer> NonLod0Renderers(GameObject p)
        {
            var excluded = new HashSet<Renderer>();
            foreach (LODGroup g in p.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = g.GetLODs();
                for (int i = 1; i < lods.Length; i++)
                    foreach (Renderer r in lods[i].renderers) if (r != null) excluded.Add(r);
                if (lods.Length > 0)
                    foreach (Renderer r in lods[0].renderers) if (r != null) excluded.Remove(r);
            }
            return excluded;
        }

        private static string MeshAabb(GameObject p)
        {
            HashSet<Renderer> excluded = NonLod0Renderers(p);

            // Root space keeps the root scale (that is what the world sees) but drops its position/rotation.
            Matrix4x4 toRoot = Matrix4x4.TRS(p.transform.position, p.transform.rotation, Vector3.one).inverse;
            bool any = false;
            Bounds total = default;
            foreach (Renderer r in p.GetComponentsInChildren<Renderer>(true))
            {
                if (excluded.Contains(r) || !r.enabled || !ActiveUnder(r.transform, p.transform)) continue;
                Bounds local;
                Transform space = r.transform;
                if (r is SkinnedMeshRenderer smr)
                {
                    local = smr.localBounds;
                    if (smr.rootBone != null) space = smr.rootBone;
                }
                else if (r is MeshRenderer)
                {
                    MeshFilter mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    local = mf.sharedMesh.bounds;
                }
                else continue;

                Matrix4x4 m = toRoot * space.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 w = m.MultiplyPoint3x4(corner);
                    if (!any) { total = new Bounds(w, Vector3.zero); any = true; }
                    else total.Encapsulate(w);
                }
            }
            return any ? $"{V(total.size)} (min y {F(total.min.y)})" : "no mesh";
        }

        private static string RendererBoundsAsIs(GameObject p)
        {
            bool any = false;
            Bounds total = default;
            foreach (Renderer r in p.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                if (!any) { total = r.bounds; any = true; }
                else total.Encapsulate(r.bounds);
            }
            return any ? V(total.size) : "-";
        }
    }
}
#endif
