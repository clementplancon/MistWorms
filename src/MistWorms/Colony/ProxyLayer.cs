using System.Text;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>
    /// Physics layer of the worm proxies (spec §10.1, decisions.md 1.1 line 17). A proxy must be hit by Attack, Projectile and Aoe, and
    /// must not push the player, creatures or the world. Candidates are tried in order; the first whose layer is in the three hit
    /// masks and ignores every layer of <see cref="MustIgnore"/> in the project's collision matrix wins. If none does, the proxy
    /// stays on hitbox and ignores the local player's colliders one by one (<see cref="NeedsIgnoreCollision"/>).
    /// </summary>
    internal static class ProxyLayer
    {
        private static readonly string[] Candidates = { VanillaNames.LayerHitbox, VanillaNames.LayerCharacterNoenv };

        // Layers a proxy must not collide with; character* bodies are dynamic, the rest static.
        private static readonly string[] MustIgnore =
        {
            VanillaNames.LayerCharacter, "character_net", "character_ghost", VanillaNames.LayerCharacterNoenv,
            VanillaNames.LayerDefault, VanillaNames.LayerStaticSolid, VanillaNames.LayerTerrain,
        };

        public static bool Resolved { get; private set; }
        public static int Layer { get; private set; } = -1;
        public static string LayerName { get; private set; }

        /// <summary>No candidate is clean: proxies get explicit IgnoreCollision pairs with the local player.</summary>
        public static bool NeedsIgnoreCollision { get; private set; }

        /// <summary>One line describing the tested matrix and the choice (also written by mw_dump).</summary>
        public static string Report { get; private set; }

        public static void Resolve()
        {
            if (Resolved) return;
            Resolved = true;

            // Same layer lists as Attack.m_attackMask (Attack.cs:372), Projectile.s_rayMaskSolids (Projectile.cs:238) and the props part
            // of Aoe.m_rayMask (Aoe.cs:218).
            int attackMask = LayerMask.GetMask(VanillaNames.LayerDefault, VanillaNames.LayerStaticSolid, VanillaNames.LayerDefaultSmall,
                VanillaNames.LayerPiece, "piece_nonsolid", VanillaNames.LayerCharacter, "character_net", "character_ghost",
                VanillaNames.LayerHitbox, VanillaNames.LayerCharacterNoenv, VanillaNames.LayerVehicle);
            int projectileMask = attackMask | LayerMask.GetMask(VanillaNames.LayerTerrain);
            int aoeMask = LayerMask.GetMask(VanillaNames.LayerDefault, VanillaNames.LayerStaticSolid, VanillaNames.LayerDefaultSmall,
                VanillaNames.LayerPiece, VanillaNames.LayerHitbox, VanillaNames.LayerCharacterNoenv, VanillaNames.LayerVehicle);

            var sb = new StringBuilder("proxy layer matrix (ignore = no physical collision):");
            string chosen = null;
            foreach (string candidate in Candidates)
            {
                int layer = LayerMask.NameToLayer(candidate);
                if (layer < 0)
                {
                    sb.Append($" {candidate}{{missing layer}}");
                    continue;
                }

                int bit = 1 << layer;
                bool inMasks = (attackMask & bit) != 0 && (projectileMask & bit) != 0 && (aoeMask & bit) != 0;
                bool clean = true;
                sb.Append($" {candidate}({layer}){{");
                if (!inMasks) sb.Append("NOT IN THE HIT MASKS ");
                foreach (string other in MustIgnore)
                {
                    int o = LayerMask.NameToLayer(other);
                    if (o < 0) continue;
                    bool ignored = Physics.GetIgnoreLayerCollision(layer, o);
                    sb.Append(other).Append(ignored ? "=ignore " : "=COLLIDES ");
                    if (!ignored) clean = false;
                }
                sb.Append('}');
                if (chosen == null && inMasks && clean) chosen = candidate;
            }

            if (chosen != null)
            {
                Layer = LayerMask.NameToLayer(chosen);
                LayerName = chosen;
                NeedsIgnoreCollision = false;
                sb.Append($" -> {chosen} (clean)");
                Report = sb.ToString();
                Plugin.Log.LogInfo("[Proxy] " + Report);
                return;
            }

            LayerName = VanillaNames.LayerHitbox;
            Layer = LayerMask.NameToLayer(LayerName);
            if (Layer < 0) Layer = 0;
            NeedsIgnoreCollision = true;
            sb.Append($" -> {LayerName} + IgnoreCollision with the local player (no candidate is clean)");
            Report = sb.ToString();
            Plugin.Log.LogWarning("[Proxy] " + Report);
        }
    }
}
