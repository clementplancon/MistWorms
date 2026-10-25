#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Dev
{
    /// <summary>Result of <see cref="MineSite.Find"/>.</summary>
    internal sealed class MineSiteResult
    {
        public bool Found;
        public Vector3 Floor;          // floor point of the centre of the free disc
        public float Clearance;        // radius of the free disc actually obtained
        public int Tested;
        public int Rooms;
        public int FloorSamples;
        public string Error;
        /// <summary>Best free radius found over all floor samples: ignoring the other criteria, with the ceiling criterion, with the flatness criterion, with both.</summary>
        public string Distribution;
        /// <summary>True when the optional extra test (clear camera) held at the site found; false when it had to be dropped.</summary>
        public bool ExtraTestPassed;
    }

    /// <summary>
    /// Flat, free floor inside a mine (phase 4B-3, pass D): the visual scenarios of the masses look at a swarm in its real setting. The floors
    /// of every room of the dungeon are sampled (rays down on a <see cref="GridStep"/> grid, like S08), sorted by distance from the player,
    /// and the nearest floor point that is flat (normal, and eight rays at 0.6 of the clearance within <see cref="FlatTol"/>), has no solid in a
    /// capsule of that radius and a ceiling at least <see cref="CeilingMin"/> m up is kept. The clearance goes down by 0.5 m from the wanted
    /// value to the minimum. Without a generator the search walks rings around the player instead.
    /// </summary>
    internal static class MineSite
    {
        public const float Reach = 30f;
        public const float Step = 1.5f;
        public const float GridStep = 1.5f;
        public const float CeilingMin = 2.2f;
        public const float FlatTol = 0.4f;
        public const float KneeLow = 0.4f, KneeHigh = 0.6f;     // the free radius is measured at knee height
        public const float FallbackMinClearance = 1f;
        public const float AbsoluteMinClearance = 2f;
        private const int PerFrame = 120;
        private const float FrameBudgetMs = 4f;

        private static int Mask => WormPhysics.SolidMask;

        /// <summary>Clearance a swarm of radius <paramref name="radius"/> needs for the mine view (with a margin of 0.8 m, never under 2 m).</summary>
        public static float MinimumFor(float radius) => Math.Max(radius + 0.8f, AbsoluteMinClearance);

        /// <summary>The biggest count whose radius is at most <paramref name="radius"/> (inverse of r(n) = R10 (n / 10)^e), at least 2.</summary>
        public static int FitCount(float radius)
        {
            float r10 = MWConfig.Mass.BaseRadius.Value, e = MWConfig.Mass.RadiusExponent.Value;
            if (radius <= r10 * 0.2f || r10 <= 0f || e <= 0f) return 2;
            int n = Mathf.FloorToInt(10f * Mathf.Pow(radius / r10, 1f / e));
            return Mathf.Clamp(n, 2, MWConfig.Mass.MaxCount.Value);
        }

        /// <param name="extra">optional extra test of a candidate (floor point, clearance), e.g. a clear camera; tried first, then dropped (logged in the distribution) when nothing passes</param>
        public static IEnumerator Find(Vector3 from, float wanted, float minimum, MineSiteResult res, Func<Vector3, float, bool> extra = null)
        {
            var floors = new List<Vector3>();
            DungeonGenerator generator = DevWorld.NearestGenerator(from);
            if (generator != null) yield return Scan(generator, floors, res);
            if (floors.Count == 0) Rings(from, floors);
            res.FloorSamples = floors.Count;
            floors.Sort((x, y) => (x - from).sqrMagnitude.CompareTo((y - from).sqrMagnitude));

            // Free radius of every sample once (horizontal rays at knee height: a sphere or a capsule would touch the floor itself), and its ceiling.
            int n = floors.Count;
            var free = new float[n];
            var ceil = new bool[n];
            float bestFree = 0f, bestCeil = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i % 60 == 59) yield return null;
                free[i] = FreeRadius(floors[i], wanted);
                ceil[i] = !Physics.Raycast(floors[i] + Vector3.up * 0.2f, Vector3.up, CeilingMin, Mask, QueryTriggerInteraction.Ignore);
                if (free[i] > bestFree) bestFree = free[i];
                if (ceil[i] && free[i] > bestCeil) bestCeil = free[i];
            }

            // The sample under the player: a sanity check of the measure (the entrance is open floor).
            float here = Physics.Raycast(from + Vector3.up * 1.5f, Vector3.down, out RaycastHit under, 4f, Mask, QueryTriggerInteraction.Ignore) ? FreeRadius(under.point, wanted) : -1f;

            // Nearest sample that is flat and free at the biggest clearance that exists, from the wanted value down to the minimum (1 m at worst).
            float bestFlat = 0f, bestAll = 0f;
            float floorLevel = Math.Min(minimum, FallbackMinClearance);
            int queries = 0;
            bool extraUsed = false;
            for (int pass = 0; pass < (extra != null ? 2 : 1) && !res.Found; pass++)
            {
                Func<Vector3, float, bool> test = pass == 0 ? extra : null;
                extraUsed = test != null;
                for (float clearance = wanted; clearance >= floorLevel - 1e-3f; clearance -= 0.5f)
                {
                    for (int i = 0; i < n; i++)
                    {
                        if (free[i] < clearance || !ceil[i]) continue;
                        res.Tested++;
                        if (++queries % PerFrame == 0) yield return null;
                        if (!IsFlat(floors[i], clearance)) continue;
                        if (test != null && !test(floors[i], clearance)) continue;
                        bestAll = clearance;
                        res.Found = true; res.Floor = floors[i]; res.Clearance = clearance;
                        break;
                    }
                    if (res.Found) break;
                }
            }
            res.ExtraTestPassed = res.Found && extraUsed;
            if (!res.Found)
            {
                for (int i = 0; i < n && i < 400; i++) if (free[i] >= FallbackMinClearance && IsFlat(floors[i], FallbackMinClearance) && free[i] > bestFlat) bestFlat = free[i];
            }
            res.Distribution = $"free radius at knee height (rays) over {n} floor samples of {res.Rooms} rooms: under the player {here:0.0} m; best any {bestFree:0.0} m, with a {CeilingMin:0.0} m ceiling {bestCeil:0.0} m, chosen (flat within {FlatTol:0.00} m + ceiling) {bestAll:0.0} m";
            if (!res.Found) res.Error = $"no usable floor: {res.Distribution}";
        }

        /// <summary>Distance to the nearest wall over 16 horizontal rays at knee height (<see cref="KneeLow"/> + 0.1), at most <paramref name="max"/>.</summary>
        private static float FreeRadius(Vector3 f, float max)
        {
            Vector3 o = f + Vector3.up * (KneeLow + 0.1f);
            float best = max;
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI * 0.125f;
                if (Physics.Raycast(o, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), out RaycastHit hit, best, Mask, QueryTriggerInteraction.Ignore) && hit.distance < best) best = hit.distance;
            }
            return best;
        }

        private static bool IsFlat(Vector3 f, float clearance)
        {
            float r = 0.6f * clearance;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                Vector3 o = f + new Vector3(Mathf.Cos(a) * r, 0.6f, Mathf.Sin(a) * r);
                if (!Physics.Raycast(o, Vector3.down, out RaycastHit h2, 1.6f, Mask, QueryTriggerInteraction.Ignore) || Mathf.Abs(h2.point.y - f.y) > FlatTol) return false;
            }
            return true;
        }

        private static IEnumerator Scan(DungeonGenerator generator, List<Vector3> floors, MineSiteResult res)
        {
            Room[] rooms = generator.GetComponentsInChildren<Room>();
            res.Rooms = rooms.Length;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var hits = new RaycastHit[8];
            foreach (Room room in rooms)
            {
                Vector3 size = new Vector3(room.m_size.x, room.m_size.y, room.m_size.z);
                Transform t = room.transform;
                for (float x = -size.x * 0.5f; x <= size.x * 0.5f; x += GridStep)
                {
                    for (float z = -size.z * 0.5f; z <= size.z * 0.5f; z += GridStep)
                    {
                        Vector3 top = t.TransformPoint(new Vector3(x, size.y * 0.5f, z));
                        int count = Physics.RaycastNonAlloc(top, Vector3.down, hits, size.y + 4f, WormPhysics.CharacterSolidMask, QueryTriggerInteraction.Ignore);
                        for (int k = 0; k < count; k++)
                            if (hits[k].normal.y >= 0.9f) floors.Add(hits[k].point);
                    }
                    if (watch.ElapsedMilliseconds > FrameBudgetMs) { yield return null; watch.Restart(); }
                }
            }
        }

        private static void Rings(Vector3 from, List<Vector3> floors)
        {
            for (float ring = 0f; ring <= Reach; ring += Step)
            {
                int points = ring < 0.1f ? 1 : Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * ring / Step));
                for (int k = 0; k < points; k++)
                {
                    float a = k * 2f * Mathf.PI / points;
                    Vector3 p = from + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ring;
                    if (Physics.Raycast(new Vector3(p.x, from.y + 1.5f, p.z), Vector3.down, out RaycastHit hit, 4f, Mask, QueryTriggerInteraction.Ignore) && hit.normal.y >= 0.9f)
                        floors.Add(hit.point);
                }
            }
        }

    }
}
#endif
