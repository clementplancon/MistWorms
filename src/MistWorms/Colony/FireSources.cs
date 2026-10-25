using System;
using System.Collections.Generic;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>Pure rules of the fear of fire (spec §9.1, plan 5, A16).</summary>
    internal static class FireMath
    {
        /// <summary>
        /// A mass is afraid when the distance from its centre to the source, minus its radius (the edge of the mass), is within
        /// <paramref name="fearRadius"/> + <paramref name="margin"/>. The margin is what the mass walks between two checks (0.25 s x 2.4 m/s), so
        /// that it turns before the line and never crosses it.
        /// </summary>
        public static bool MassAfraid(float centreDistance, float massRadius, float fearRadius, float margin) => centreDistance - massRadius <= fearRadius + margin;

        /// <summary>A worm (a point) is afraid within the fear radius.</summary>
        public static bool PointAfraid(float distance, float fearRadius) => distance <= fearRadius;

        /// <summary>Horizontal unit vector from the source to the mass (the direction of the flight); <paramref name="fallback"/> if they coincide.</summary>
        public static Vector3 AwayFrom(Vector3 source, Vector3 mass, Vector3 fallback)
        {
            Vector3 d = mass - source;
            d.y = 0f;
            return d.sqrMagnitude > 1e-4f ? d.normalized : fallback;
        }
    }

    /// <summary>
    /// The sources of fire a mass or a worm flees (spec §9.1, plan 5, A16): a fire <see cref="EffectArea"/> (campfire, brazier, wall torch), or a
    /// <see cref="Humanoid"/>, player included, who holds a lit torch. The torch criterion is the mod's own (the vanilla has none, decisions.md 1.1 line 38):
    /// an equipped item of type Torch with durability left. The Wisplight (a Utility) and the dvergr lamps are never sources. No allocation.
    /// </summary>
    internal static class FireSources
    {
        /// <summary>Sources farther than this from the asker are not looked at.</summary>
        public const float ScanRange = 15f;

        /// <summary>True when <paramref name="h"/> is alive and holds a lit torch in either hand.</summary>
        public static bool HoldsTorch(Humanoid h)
        {
            if (h == null || h.IsDead()) return false;
            return IsLitTorch(h.m_rightItem) || IsLitTorch(h.m_leftItem);
        }

        private static bool IsLitTorch(ItemDrop.ItemData item) =>
            item != null && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch && (!item.m_shared.m_useDurability || item.m_durability > 0f);

        /// <summary>
        /// Looks for a source that frightens a mass of radius <paramref name="massRadius"/> centred at <paramref name="center"/>; returns the closest one
        /// (<paramref name="point"/> is where it is: the closest point of the fire collider, or the position of the torch holder).
        /// </summary>
        public static bool Nearest(Vector3 center, float massRadius, float fearRadius, float margin, out Vector3 point, out bool torch)
        {
            point = default;
            torch = false;
            float best = float.MaxValue;
            bool found = false;

            List<EffectArea> areas = EffectArea.GetAllAreas();
            for (int k = 0; k < areas.Count; k++)
            {
                EffectArea area = areas[k];
                if (area == null || !area.isActiveAndEnabled || (area.m_type & EffectArea.Type.Fire) == 0) continue;
                Collider c = area.m_collider;
                if (c == null || !c.enabled) continue;
                Vector3 closest = c is MeshCollider mc && !mc.convex ? c.bounds.ClosestPoint(center) : c.ClosestPoint(center);
                float d = (closest - center).magnitude;
                if (d > ScanRange || d >= best || !FireMath.MassAfraid(d, massRadius, fearRadius, margin)) continue;
                best = d;
                point = closest;
                torch = false;
                found = true;
            }

            List<Character> all = Character.GetAllCharacters();
            for (int k = 0; k < all.Count; k++)
            {
                if (!(all[k] is Humanoid h) || !HoldsTorch(h)) continue;
                Vector3 p = h.transform.position;
                float d = (p - center).magnitude;
                if (d > ScanRange || d >= best || !FireMath.MassAfraid(d, massRadius, fearRadius, margin)) continue;
                best = d;
                point = p;
                torch = true;
                found = true;
            }
            return found;
        }
    }
}
