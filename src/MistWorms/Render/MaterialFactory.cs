using System;
using System.Text;
using Jotunn.Managers;
using MistWorms.Assets;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Render
{
    /// <summary>
    /// Builds the worm / mass / nest materials once, after the vanilla prefabs exist (spec §12.2).
    /// Shader.Find("Custom/Creature") returns null in 1.0.20 (docs/decisions.md, row 22), so the shader is taken from
    /// a vanilla creature material; Performance.ShaderCandidates are then resolved by name (vanilla material, then
    /// Shader.Find, then a scan of the loaded shaders). The vanilla material is cloned first so its Valheim-specific
    /// keywords and defaults are valid, then every visual input is overridden.
    /// </summary>
    internal static class MaterialFactory
    {
        private const string WormAlbedo = "worm_albedo", WormNormal = "worm_normal";
        private const string MassAlbedo = "mass_albedo", MassNormal = "mass_normal";
        private const string NestAlbedo = "nest_albedo", NestNormal = "nest_normal";
        private const string WormGloss = "worm_gloss";

        /// <summary>_Glossiness of the worm (and the fallback value without the lustre map); the map modulates it (dump section 12: vanilla 0.54 to 0.76).</summary>
        public const float WormGlossiness = 0.85f;

        public static Material Worm { get; private set; }
        public static Material Mass { get; private set; }
        public static Material Nest { get; private set; }

        /// <summary>True when the worm material uses the lustre map (_UseGlossmap 1, keyword on); false in the no-map fallback.</summary>
        public static bool WormGlossActive { get; private set; }

        /// <summary>Per material: whether enableInstancing read back true on a platform that supports instancing.</summary>
        public static bool WormInstancing { get; private set; }
        public static bool MassInstancing { get; private set; }
        public static bool NestInstancing { get; private set; }

        public static string ShaderName { get; private set; }
        /// <summary>How the shader was found: "vanilla material", "Shader.Find" or "loaded shader scan".</summary>
        public static string ShaderSource { get; private set; }
        public static string Error { get; private set; }

        public static bool IsReady => Worm != null && Mass != null && Nest != null;

        /// <summary>Builds on first use; cheap once ready.</summary>
        public static bool EnsureBuilt() => IsReady || Build();

        /// <summary>Never throws. Returns false and sets <see cref="Error"/> when no material can be made.</summary>
        public static bool Build()
        {
            Error = null;
            try
            {
                if (!AssetCatalog.IsReady)
                {
                    Error = "AssetCatalog is not loaded: " + AssetCatalog.Error;
                    return false;
                }

                Material vanilla = FindVanillaCreatureMaterial();
                Plugin.Log.LogInfo("[Render] vanilla reference material: " +
                    (vanilla != null
                        ? $"{vanilla.name} ({vanilla.shader.name}), keywords [{string.Join(" ", vanilla.shaderKeywords)}]" +
                          $", _Hue={Prop(vanilla, VanillaNames.PropHue)} _Saturation={Prop(vanilla, VanillaNames.PropSaturation)} _Value={Prop(vanilla, VanillaNames.PropValue)}"
                        : "none (no Tick/SeekerBrood renderer found)"));

                Shader[] loaded = null;
                Shader firstResolved = null;
                string firstSource = null;
                Shader chosen = null;
                string chosenSource = null;

                foreach (string raw in CandidateNames())
                {
                    string name = raw.Trim();
                    if (name.Length == 0) continue;
                    Shader shader = ResolveShader(name, vanilla, ref loaded, out string source);
                    if (shader == null)
                    {
                        Plugin.Log.LogInfo($"[Render] shader candidate '{name}': not found");
                        continue;
                    }
                    bool instancing = ProbeInstancing(shader);
                    Plugin.Log.LogInfo($"[Render] shader candidate '{name}': found via {source}, supported={shader.isSupported}, instancing={instancing}");
                    if (firstResolved == null && shader.isSupported) { firstResolved = shader; firstSource = source; }
                    if (instancing && shader.isSupported) { chosen = shader; chosenSource = source; break; }
                }

                if (chosen == null)
                {
                    // Spec §12.2 step 3: a shader without instancing still renders, through the per-instance fallback.
                    chosen = firstResolved;
                    chosenSource = firstSource;
                    if (chosen != null)
                        Plugin.Log.LogWarning($"[Render] no candidate shader supports instancing; '{chosen.name}' will be drawn one instance at a time");
                }
                if (chosen == null)
                {
                    Error = "none of the shader candidates could be resolved: " + MWConfig.Performance.ShaderCandidates.Value;
                    return false;
                }

                ShaderName = chosen.name;
                ShaderSource = chosenSource;

                Material template = vanilla != null && vanilla.shader == chosen ? vanilla : null;
                Material worm = Create("MW_Worm", chosen, template, WormAlbedo, WormNormal, WormGlossiness, false, WormGloss);
                Material mass = Create("MW_Mass", chosen, template, MassAlbedo, MassNormal, 0.65f, true);
                Material nest = Create("MW_Nest", chosen, template, NestAlbedo, NestNormal, 0.3f, false);

                // Custom/Creature declares no _USEGLOSSMAP_ON keyword (dump section 9): the float drives it, the keyword is only set.
                WormGlossActive = worm.HasProperty(VanillaNames.PropUseGlossmap) && worm.GetFloat(VanillaNames.PropUseGlossmap) > 0.5f
                                  && worm.HasProperty(VanillaNames.PropMetallicGlossMap) && worm.GetTexture(VanillaNames.PropMetallicGlossMap) != null;
                Worm = worm;
                Mass = mass;
                Nest = nest;
                WormInstancing = ReadBackInstancing(worm);
                MassInstancing = ReadBackInstancing(mass);
                NestInstancing = ReadBackInstancing(nest);

                Plugin.Log.LogInfo($"[Render] materials ready: shader '{ShaderName}' via {ShaderSource}; instancing worm={WormInstancing} mass={MassInstancing} nest={NestInstancing}");
                return true;
            }
            catch (Exception e)
            {
                Error = e.Message;
                Plugin.LogException("MaterialFactory.Build", e);
                return false;
            }
        }

        private static string[] CandidateNames()
        {
            string list = MWConfig.Performance.ShaderCandidates.Value;
            if (string.IsNullOrWhiteSpace(list)) list = VanillaNames.ShaderCandidatesDefault;
            return list.Split(',');
        }

        private static Shader ResolveShader(string name, Material vanilla, ref Shader[] loaded, out string source)
        {
            if (vanilla != null && vanilla.shader != null && vanilla.shader.name == name)
            {
                source = "vanilla material";
                return vanilla.shader;
            }

            Shader found = Shader.Find(name);
            if (found != null)
            {
                source = "Shader.Find";
                return found;
            }

            if (loaded == null) loaded = Resources.FindObjectsOfTypeAll<Shader>();
            foreach (Shader s in loaded)
            {
                if (s != null && s.name == name)
                {
                    source = "loaded shader scan";
                    return s;
                }
            }

            source = null;
            return null;
        }

        private static bool ProbeInstancing(Shader shader)
        {
            if (!SystemInfo.supportsInstancing) return false;
            var probe = new Material(shader) { enableInstancing = true };
            bool ok = probe.enableInstancing;
            UnityEngine.Object.Destroy(probe);
            return ok;
        }

        private static bool ReadBackInstancing(Material m) => SystemInfo.supportsInstancing && m.enableInstancing;

        /// <summary>First Custom/Creature material of a vanilla reference creature, else any material of it.</summary>
        private static Material FindVanillaCreatureMaterial()
        {
            Material anyMaterial = null;
            foreach (string prefabName in new[] { VanillaNames.Tick, VanillaNames.SeekerBrood })
            {
                GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefabName) : null;
                if (prefab == null)
                {
                    try { prefab = PrefabManager.Cache.GetPrefab<GameObject>(prefabName); }
                    catch (Exception e) { Plugin.Log.LogInfo($"[Render] prefab cache lookup of {prefabName} failed: {e.Message}"); }
                }
                if (prefab == null) continue;

                foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    Material m = r.sharedMaterial;
                    if (m == null || m.shader == null) continue;
                    if (m.shader.name == VanillaNames.ShaderCreature) return m;
                    if (anyMaterial == null) anyMaterial = m;
                }
            }
            return anyMaterial;
        }

        private static Material Create(string name, Shader shader, Material template, string albedoName, string normalName,
            float glossiness, bool doubleSided, string glossMapName = null)
        {
            Texture2D albedo = AssetCatalog.Texture(albedoName) ?? throw new InvalidOperationException("missing texture " + albedoName);
            Texture2D normal = AssetCatalog.Texture(normalName) ?? throw new InvalidOperationException("missing texture " + normalName);

            Material m = template != null ? new Material(template) : new Material(shader);
            m.name = name;
            // Survives scene unloads and Resources.UnloadUnusedAssets: the factory owns these for the whole session.
            m.hideFlags = HideFlags.HideAndDontSave;

            // Drop every inherited texture (vanilla emissive, style, noise maps), then bind ours.
            foreach (string tex in m.GetTexturePropertyNames()) m.SetTexture(tex, null);
            m.SetTexture(VanillaNames.PropMainTex, albedo);
            if (m.HasProperty(VanillaNames.PropBumpMap))
            {
                m.SetTexture(VanillaNames.PropBumpMap, normal);
                m.EnableKeyword(VanillaNames.KeywordNormalMap);
            }

            // Neutral colour: no vanilla level tint, no emission, no rain/snow overlay, no style/glow effects.
            SetColor(m, VanillaNames.PropColor, Color.white);
            SetColor(m, VanillaNames.PropEmissionColor, Color.black);
            // _Hue/_Saturation/_Value are HSV OFFSETS in Custom/Creature (vanilla ticks use 0/0/0): neutral is 0, not 1.
            SetFloat(m, VanillaNames.PropHue, 0f);
            SetFloat(m, VanillaNames.PropSaturation, 0f);
            SetFloat(m, VanillaNames.PropValue, 0f);
            // Emission fully off: black colour, no map, no keyword inherited from the vanilla copy.
            if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", null);
            m.DisableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            SetFloat(m, VanillaNames.PropMetallic, 0f);
            SetFloat(m, VanillaNames.PropGlossiness, glossiness);
            SetFloat(m, VanillaNames.PropUseGlossmap, 0f);
            SetFloat(m, VanillaNames.PropBumpScale, 1f);
            SetFloat(m, VanillaNames.PropUseStyles, 0f);
            SetFloat(m, VanillaNames.PropNoiseGlowEnabled, 0f);
            SetFloat(m, VanillaNames.PropAddRain, 0f);
            SetFloat(m, VanillaNames.PropSnowCover, 0f);
            m.DisableKeyword(VanillaNames.KeywordUseGlossmap);
            m.DisableKeyword(VanillaNames.KeywordSnapNoise);

            // Lustre map (alpha = gloss, RGB 0 like the vanilla tick's Feasting_m): optional, falls back to a flat _Glossiness.
            if (glossMapName != null)
            {
                Texture2D gloss = AssetCatalog.Texture(glossMapName);
                if (gloss != null && m.HasProperty(VanillaNames.PropMetallicGlossMap) && m.HasProperty(VanillaNames.PropUseGlossmap))
                {
                    m.SetTexture(VanillaNames.PropMetallicGlossMap, gloss);
                    SetFloat(m, VanillaNames.PropUseGlossmap, 1f);
                    m.EnableKeyword(VanillaNames.KeywordUseGlossmap);
                    SetFloat(m, VanillaNames.PropMetallic, 0f);
                    SetFloat(m, VanillaNames.PropGlossiness, glossiness);
                    Plugin.Log.LogInfo($"[Render] {name}: lustre map {glossMapName} ({gloss.width}x{gloss.height}, filter {gloss.filterMode}), _UseGlossmap=1, _Glossiness={glossiness:0.00}, _Metallic=0");
                }
                else
                {
                    SetFloat(m, VanillaNames.PropUseGlossmap, 0f);
                    SetFloat(m, VanillaNames.PropGlossiness, WormGlossiness);
                    Plugin.Log.LogInfo($"[Render] {name}: no lustre map ({(gloss == null ? "texture " + glossMapName + " missing" : "shader has no _MetallicGlossMap")}), _UseGlossmap=0, _Glossiness={WormGlossiness:0.00}");
                }
            }

            // The dome is seen from inside when the camera dives into it: draw both faces and flip back-face normals.
            SetFloat(m, VanillaNames.PropCull, doubleSided ? 0f : 2f);
            SetFloat(m, VanillaNames.PropTwoSidedNormals, doubleSided ? 1f : 0f);
            if (doubleSided) m.EnableKeyword(VanillaNames.KeywordTwoSidedNormals);
            else m.DisableKeyword(VanillaNames.KeywordTwoSidedNormals);

            m.enableInstancing = true;
            bool readBack = m.enableInstancing;
            var sb = new StringBuilder();
            foreach (string k in m.shaderKeywords) sb.Append(k).Append(' ');
            Plugin.Log.LogInfo($"[Render] material {name}: shader '{m.shader.name}', instancing readback={readBack}, keywords [{sb.ToString().Trim()}], queue={m.renderQueue}");
            return m;
        }

#if MW_DEV
        /// <summary>Dev (mw_gloss): switches the lustre map of the worm material and optionally its _Glossiness, live.</summary>
        public static string DevSetGloss(bool useMap, float? glossiness)
        {
            Material m = Worm;
            if (m == null) return "worm material not built";
            Texture2D map = AssetCatalog.Texture(WormGloss);
            if (useMap && map != null) m.SetTexture(VanillaNames.PropMetallicGlossMap, map);
            SetFloat(m, VanillaNames.PropUseGlossmap, useMap ? 1f : 0f);
            if (useMap) m.EnableKeyword(VanillaNames.KeywordUseGlossmap); else m.DisableKeyword(VanillaNames.KeywordUseGlossmap);
            if (glossiness.HasValue) SetFloat(m, VanillaNames.PropGlossiness, glossiness.Value);
            WormGlossActive = useMap && map != null;
            return $"_UseGlossmap={Prop(m, VanillaNames.PropUseGlossmap)} _Glossiness={Prop(m, VanillaNames.PropGlossiness)} map={(m.GetTexture(VanillaNames.PropMetallicGlossMap) != null ? WormGloss : "none")}";
        }
#endif

        private static void SetFloat(Material m, string prop, float v)
        {
            if (m.HasProperty(prop)) m.SetFloat(prop, v);
        }

        private static void SetColor(Material m, string prop, Color c)
        {
            if (m.HasProperty(prop)) m.SetColor(prop, c);
        }

        private static string Prop(Material m, string prop) => m.HasProperty(prop) ? m.GetFloat(prop).ToString("0.###") : "n/a";
    }
}
