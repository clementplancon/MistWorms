#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using MistWorms.Assets;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Dev.Preview;
using MistWorms.Mass;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// V09 (spec §12.4, §15.5; phase 4B-4): the visible transitions of the masses, in the infested mine (parameter where=open for open ground),
    /// each recorded as a clip (docs/shots/clips/V09_MassTransitions_*, assemble with `vh.py assemble`) from the camera of the other clips of the
    /// masses (<see cref="VisualScenario.ClipPose"/>) with the fill light, plus a capture before and after:
    ///  - formation: 12 worms in the Gathering state around a spot gather, become one mass of 12 and slide onto it (the heap grows from nothing);
    ///  - absorption: a worm gathers to a mass of 20, climbs onto it (Absorbing) and the mass counts 21;
    /// - fusion: a mass of 10 that touches a mass of 15 slides onto it and is gone, the big one counts 25;
    ///  - dissolution: a hit that leaves 4 of 8 worms dissolves the mass; the 4 worms leap out and flee.
    /// The player is in god and ghost mode (the worms are not after him), the masses have their AI frozen. A transition is triggered from a second
    /// coroutine a moment after the clip started, so the clip shows the whole of it.
    /// </summary>
    internal sealed class V09_MassTransitions : IDevScenario
    {
        private const float ClipSeconds = 4.5f;
        private const float FormationClipSeconds = 5f;
        private const float TriggerDelay = 0.6f;
        private const int FormationWorms = 12;
        private const int BigCount = 15, SmallCount = 10;
        private const float FormationRing = 2.2f;

        public string Name => "V09_MassTransitions";

        public IEnumerator Run(ScenarioContext ctx)
        {
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 400f;
            string where = ctx.Param("where", "mine");
            bool gatheringBefore = ColonyDev.GatheringEnabled;
            bool mergeBefore = WritheMassState.MergeEnabled;
            bool clutterHidden = false;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;
                ctx.Assert("assets_loaded", AssetCatalog.IsReady, true, AssetCatalog.IsReady ? "ready" : AssetCatalog.Error);
                if (!AssetCatalog.IsReady || !MaterialFactory.EnsureBuilt()) yield break;
                if (ZNetScene.instance == null || ZNetScene.instance.GetPrefab(MWNames.MassPrefab) == null)
                {
                    ctx.Assert("prefab_registered", false, MWNames.MassPrefab, "missing");
                    yield break;
                }

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                DevPreview.Instance.Clear();
                ColonyDev.GatheringEnabled = true;
                WritheMassState.MergeEnabled = true;
                yield return ctx.WaitFrames(3);

                Player player = Player.m_localPlayer;
                player.SetGodMode(true);

                // ---- the place: a free flat floor of the mine, else open ground
                Vector3 anchor = default;
                bool placed = false;
                if (where != "open")
                {
                    player.SetGhostMode(true);
                    yield return ctx.TpMine();
                    if (ctx.LastTpMine.Ok)
                    {
                        yield return ctx.Wait(1f);
                        var site = new MineSiteResult();
                        yield return MineSite.Find(Player.m_localPlayer.transform.position, MassMath.Radius(15) + 6f, MineSite.AbsoluteMinClearance, site, (floor, clearance) => FlatAround(floor));
                        ctx.Assert("mine_site_found", site.Found, "a flat free floor", site.Found ? $"{site.Floor} clearance {site.Clearance:0.0} m" : site.Error);
                        if (site.Found) { anchor = site.Floor; placed = true; }
                    }
                    if (!placed) ctx.Log("no site in the mine: falling back to open ground");
                }
                if (!placed)
                {
                    player.SetGhostMode(false);
                    if (ClutterSystem.instance != null)
                    {
                        ClutterSystem.instance.enabled = false;
                        ClutterSystem.instance.ClearAll();
                        clutterHidden = true;
                    }
                    yield return ctx.MoveToOpenGround(8f, FlatAround);
                    if (!ctx.LastOpenGround.Found) yield break;
                    player = Player.m_localPlayer;
                    anchor = player.transform.position + Forward(player) * 5f;
                    player.SetGhostMode(true);
                }
                player = Player.m_localPlayer;
                player.SetGodMode(true);
                ctx.Log($"anchor {anchor}");

                WormColony colony = ColonyDev.FindOrCreateColony(player, out string how);
                ctx.Assert("colony_ready", colony != null, "a colony", how);
                if (colony == null) yield break;
                yield return ctx.WaitFrames(3);

                yield return Formation(ctx, colony, anchor);
                yield return Absorption(ctx, colony, anchor);
                yield return Fusion(ctx, colony, anchor);
                yield return Dissolution(ctx, colony, anchor);

                WormRenderer.FrameStats stats = WormRenderer.Last;
                ctx.Assert("renderer_no_dropped_instances", stats.Dropped == 0, 0, stats.Dropped);
            }
            finally
            {
                DevCamera.Provider = null;
                DevCamera.Follow();
                DevCamera.SetFillLight(false);
                ColonyDev.GatheringEnabled = gatheringBefore;
                WritheMassState.MergeEnabled = mergeBefore;
                ctx.Console("mw_kill");
                if (clutterHidden && ClutterSystem.instance != null) ClutterSystem.instance.enabled = true;
                Player p = Player.m_localPlayer;
                if (p != null) { p.SetGodMode(false); p.SetGhostMode(false); }
            }
        }

        // ------------------------------------------------------------------ formation

        private IEnumerator Formation(ScenarioContext ctx, WormColony colony, Vector3 anchor)
        {
            ClearMineCreatures(ctx);
            Frame(ctx, anchor, 0.75f * FormationRing, Vector3.forward);       // the worms start on a ring of 2.2 m and slide in: the inner three quarters of it are framed
            yield return ctx.WaitFrames(10);
            yield return ctx.Shot("formation_0_before");

            int glides0 = MassTransients.GlideSpawned;
            int formed0 = colony.MassesFormed;
            int spawned = 0;
            DevBridge.Host.StartCoroutine(Later(TriggerDelay, () =>
            {
                for (int k = 0; k < FormationWorms; k++)
                {
                    float a = k * 2f * Mathf.PI / FormationWorms;
                    Vector3 near = anchor + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * FormationRing;
                    if (ColonyDev.SpawnAt(near, WormState.Gathering, true, out _) >= 0) spawned++;
                }
            }));
            yield return ctx.Clip("formation", FormationClipSeconds);
            DevCamera.SetFillLight(false);

            // The end state counts: two groups may form at the same time and then merge, so one mass of all the worms is awaited (formations and merges logged).
            int merges1 = WritheMassState.MergesCompleted;
            WritheMassState mass = null;
            yield return ctx.WaitUntil(() => (mass = MassOf(colony)) != null && mass.N == spawned && CountMasses(colony) == 1 && colony.Store.AliveCount == 0, 8f);
            ctx.Log($"formation: {colony.MassesFormed - formed0} mass(es) formed, {WritheMassState.MergesCompleted - merges1} merge(s), now {CountMasses(colony)} mass(es)");
            ctx.Assert("formation_worms_placed", spawned >= 3, ">= 3 worms around the spot", spawned);
            ctx.Assert("formation_one_mass", mass != null && mass.N == spawned && CountMasses(colony) == 1, $"one mass of {spawned}",
                mass != null ? $"n={mass.N}, {CountMasses(colony)} mass(es), {colony.MassesFormed - formed0} formed, {WritheMassState.MergesCompleted - merges1} merged" : "none");
            ctx.Assert("formation_worms_left_the_store", colony.Store.AliveCount == 0, 0, colony.Store.AliveCount);
            ctx.Assert("formation_glides_started", MassTransients.GlideSpawned - glides0 >= spawned, $">= {spawned} sliding worms", MassTransients.GlideSpawned - glides0);
            if (mass != null)
            {
                yield return ctx.WaitFrames(30);
                yield return ctx.Shot("formation_1_after");
                MassDev.Destroy(mass);
            }
            yield return ctx.Wait(0.5f);
        }

        // ------------------------------------------------------------------ absorption

        private IEnumerator Absorption(ScenarioContext ctx, WormColony colony, Vector3 anchor)
        {
            ClearMineCreatures(ctx);
            WritheMassState mass = MassDev.SpawnAt(anchor, 20, colony.ZdoId, freezeAI: true);
            ctx.Assert("absorption_mass_created", mass != null && mass.Valid, "a mass of 20", mass != null ? mass.ZdoId.ToString() : "none");
            if (mass == null) yield break;
            WritheMassVisual visual = mass.GetComponent<WritheMassVisual>();
            yield return ctx.WaitUntil(() => visual != null && visual.Active, 5f);
            yield return ctx.WaitFrames(20);
            if (visual == null || !visual) { ctx.Assert("mass_alive_before_the_clip", false, "the mass still stands", "gone (see the mass death log)"); yield break; }
            // The action runs from the far side of the mass to where the worm starts (3 m past its edge): the camera looks at the middle of it, from the side.
            float ra = Mathf.Max(0.3f, visual.ShownRadius);
            Frame(ctx, visual.Root + visual.Left * 1.5f, ra + 1.5f, visual.Forward);
            yield return ctx.Shot("absorption_0_before");

            int absorbed0 = colony.WormsAbsorbed;
            int slot = -1;
            bool sawAbsorbing = false;
            DevBridge.Host.StartCoroutine(Later(TriggerDelay, () =>
            {
                if (visual == null || !visual) { ctx.Log("absorption: the mass is gone before the worm is spawned"); return; }
                Vector3 near = visual.Root + visual.Left * (visual.ShownRadius + 3f);
                slot = ColonyDev.SpawnAt(near, WormState.Gathering, true, out _);
            }));
            DevBridge.Host.StartCoroutine(Watch(ClipSeconds + 2f, () =>
            {
                if (slot >= 0 && colony.Store.alive[slot] && colony.Store.state[slot] == (byte)WormState.Absorbing) sawAbsorbing = true;
            }));
            yield return ctx.Clip("absorption", ClipSeconds);
            DevCamera.SetFillLight(false);
            yield return ctx.Wait(1f);

            ctx.Assert("absorption_worm_spawned", slot >= 0, "a gathering worm near the mass", slot);
            ctx.Assert("absorption_state_seen", sawAbsorbing, "the worm went through Absorbing", sawAbsorbing);
            ctx.Assert("absorption_mass_grew", mass != null && mass.N == 21 && colony.WormsAbsorbed == absorbed0 + 1, "n=21, one worm absorbed",
                mass != null ? $"n={mass.N}, absorbed {colony.WormsAbsorbed - absorbed0}" : "mass gone");
            ctx.Assert("absorption_worm_gone", slot < 0 || !colony.Store.alive[slot] || colony.Store.state[slot] != (byte)WormState.Absorbing, "the worm joined the heap", colony.Store.AliveCount);
            if (mass != null)
            {
                yield return ctx.Shot("absorption_1_after");
                MassDev.Destroy(mass);
            }
            yield return ctx.Wait(0.5f);
        }

        // ------------------------------------------------------------------ fusion

        private IEnumerator Fusion(ScenarioContext ctx, WormColony colony, Vector3 anchor)
        {
            ClearMineCreatures(ctx);
            // Nothing else of the colony may stand near: a stray mass bigger than the 15 would swallow it. What is there is logged and removed.
            foreach (WritheMassState stray in new List<WritheMassState>(WritheMassState.All))
            {
                if (stray == null || !stray.Valid) continue;
                ctx.Log($"fusion: removing a mass left over from an earlier part: {stray.ZdoId} n={stray.N} at {stray.transform.position}");
                MassDev.Destroy(stray);
            }
            yield return ctx.WaitFrames(3);
            WritheMassState big = MassDev.SpawnAt(anchor, BigCount, colony.ZdoId, freezeAI: true);
            ctx.Assert("fusion_mass_created", big != null && big.Valid, $"a mass of {BigCount}", big != null ? big.ZdoId.ToString() : "none");
            if (big == null) yield break;
            WritheMassVisual visual = big.GetComponent<WritheMassVisual>();
            yield return ctx.WaitUntil(() => visual != null && visual.Active, 5f);
            yield return ctx.WaitFrames(20);
            if (visual == null || !visual) { ctx.Assert("mass_alive_before_the_clip", false, "the mass still stands", "gone (see the mass death log)"); yield break; }
            int merges0 = WritheMassState.MergesCompleted;
            WritheMassState small = null;
            float rBig = MassMath.Radius(BigCount), rSmall = MassMath.Radius(SmallCount);
            float touching = 0.93f * (rBig + rSmall);
            // Framed on the point where the two heaps meet, wide enough for both.
            Frame(ctx, visual.Root - visual.Left * (0.5f * touching), 0.5f * (rBig + touching + rSmall), visual.Forward);
            yield return ctx.Shot("fusion_0_before");
            DevBridge.Host.StartCoroutine(Later(TriggerDelay, () =>
            {
                if (visual == null || !visual) { ctx.Log("fusion: the big mass is gone before the small one is spawned"); return; }
                Vector3 near = visual.Root - visual.Left * touching;
                small = MassDev.SpawnAt(near, SmallCount, colony.ZdoId, freezeAI: true);
            }));
            yield return ctx.Clip("fusion", ClipSeconds);
            DevCamera.SetFillLight(false);
            yield return ctx.WaitUntil(() => small == null || !small.Valid, 4f);

            foreach (WritheMassState m in WritheMassState.All)
                if (m != null && m.Valid) ctx.Log($"fusion: after the clip mass {m.ZdoId} n={m.N} state {MassStateCodes.Describe(m.StateCode)} at {m.transform.position}");
            ctx.Assert("fusion_small_created", small != null || WritheMassState.MergesCompleted > merges0, "a mass of 12 touching the big one", small != null ? "yes" : "no");
            ctx.Assert("fusion_one_mass_left", big != null && big.N == BigCount + SmallCount && WritheMassState.MergesCompleted == merges0 + 1, $"the big mass counts {BigCount + SmallCount}, one merge",
                big != null ? $"n={big.N}, merges {WritheMassState.MergesCompleted - merges0}" : "big mass gone");
            ctx.Assert("fusion_small_destroyed", small == null || !small, "the small mass is gone", small == null || !small ? "gone" : "still there");
            if (big != null)
            {
                yield return ctx.Shot("fusion_1_after");
                MassDev.Destroy(big);
            }
            yield return ctx.Wait(0.5f);
        }

        // ------------------------------------------------------------------ dissolution

        private IEnumerator Dissolution(ScenarioContext ctx, WormColony colony, Vector3 anchor)
        {
            ClearMineCreatures(ctx);
            WritheMassState mass = MassDev.SpawnAt(anchor, 8, colony.ZdoId, freezeAI: true);
            ctx.Assert("dissolution_mass_created", mass != null && mass.Valid, "a mass of 8", mass != null ? mass.ZdoId.ToString() : "none");
            if (mass == null) yield break;
            WritheMassVisual visual = mass.GetComponent<WritheMassVisual>();
            yield return ctx.WaitUntil(() => visual != null && visual.Active, 5f);
            yield return ctx.WaitFrames(30);
            if (visual == null || !visual) { ctx.Assert("mass_alive_before_the_clip", false, "the mass still stands", "gone (see the mass death log)"); yield break; }
            // Framed before the hit: the heap and the 1.5 m the worms leap.
            Frame(ctx, visual.Root, Mathf.Max(0.3f, visual.ShownRadius) + 1.5f, visual.Forward);
            yield return ctx.Shot("dissolution_0_before");

            int dissolved0 = colony.DissolvedWorms;
            float perWorm = MWConfig.Worm.Health.Value;
            DevBridge.Host.StartCoroutine(Later(TriggerDelay + 0.2f, () =>
            {
                if (mass == null || !mass || mass.Character == null) { ctx.Log("dissolution: the mass is gone before the hit (see the mass death log)"); return; }
                mass.Character.Damage(NewHit(mass, 4f * perWorm));
            }));
            yield return ctx.Clip("dissolution", ClipSeconds);
            DevCamera.SetFillLight(false);

            ctx.Assert("dissolution_worms_created", colony.DissolvedWorms - dissolved0 == 4, "4 free worms", colony.DissolvedWorms - dissolved0);
            ctx.Assert("dissolution_mass_destroyed", mass == null || !mass, "the mass is gone", mass == null || !mass ? "gone" : "still there");
            yield return ctx.Shot("dissolution_1_after");
            yield return ctx.Wait(0.5f);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Camera along <paramref name="dir"/> from <paramref name="center"/> (another side when blocked), 20 degrees above it, looking at the centre of the action; fill light on. The
        /// distance comes from the radius of the action <paramref name="zone"/> (the mass and what comes to it) and the field of view (D12): the action takes <paramref name="fraction"/>
        /// (60 % by default: a wide action in a 16:9 image) of the height of the image.
        /// </summary>
        private static void Frame(ScenarioContext ctx, Vector3 center, float zone, Vector3 dir, float fraction = 0.6f)
        {
            Vector3 look = center + Vector3.up * 0.3f;
            int mask = VisualScenario.CameraMask();
            zone = Mathf.Max(0.5f, zone);
            ScenarioContext.Orbit(ScenarioContext.FitFlat(zone, 0.5f, 20f, fraction), 20f, out float horizontal, out float up);
            Vector3 flat = new Vector3(dir.x, 0f, dir.z).normalized;
            Vector3 pos = center + flat * horizontal + Vector3.up * up;
            bool found = false;
            // A side qualifies when the camera is in the open and sees the centre of the action AND the edge of the zone all around it (a bump of the
            // ground between the camera and the action hid a whole clip): 8 sides, then 0.6 m higher.
            for (int lift = 0; lift < 2 && !found; lift++)
                for (int k = 0; k < 8 && !found; k++)
                {
                    int step = (k + 1) / 2 * (k % 2 == 0 ? 1 : -1);
                    Vector3 side = Quaternion.AngleAxis(step * 45f, Vector3.up) * flat;
                    Vector3 c = center + side * horizontal + Vector3.up * (up + lift * 0.6f);
                    if (Physics.CheckSphere(c, 0.4f, mask, QueryTriggerInteraction.Ignore) || Physics.Linecast(c, look, mask, QueryTriggerInteraction.Ignore)) continue;
                    bool seesZone = true;
                    for (int e = 0; e < 4 && seesZone; e++)
                    {
                        Vector3 edge = center + Quaternion.AngleAxis(e * 90f, Vector3.up) * side * zone + Vector3.up * 0.15f;
                        if (Physics.Linecast(c, edge, mask, QueryTriggerInteraction.Ignore)) seesZone = false;
                    }
                    if (!seesZone) continue;
                    pos = c;
                    found = true;
                }
            if (!found) ctx.Log($"camera: no side sees the whole action at {horizontal:0.0} m, using the first");
            DevCamera.Free(pos, look);
            DevCamera.SetFillLight(true);
        }

        /// <summary>The ground within 6 m of <paramref name="g"/> (centre and 24 points on two rings) is level with it within 0.3 m.</summary>
        private static bool FlatAround(Vector3 g)
        {
            int mask = WormPhysics.SolidMask;
            for (int ring = 1; ring <= 2; ring++)
                for (int k = 0; k < 12; k++)
                {
                    float a = k * Mathf.PI * 2f / 12f;
                    Vector3 p = g + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (3f * ring);
                    if (!Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, mask, QueryTriggerInteraction.Ignore)) return false;
                    if (Mathf.Abs(hit.point.y - g.y) > 0.3f) return false;
                }
            return true;
        }

        /// <summary>The mine has creatures that kill a mass (it is hostile to every faction): the 40 m around the player are emptied before every part.</summary>
        private static void ClearMineCreatures(ScenarioContext ctx)
        {
            if (Player.m_localPlayer != null) S18_GroupCoherence.ClearCreatures(ctx, Player.m_localPlayer);
        }

        private static int CountMasses(WormColony colony)
        {
            int n = 0;
            foreach (WritheMassState m in WritheMassState.All)
                if (m != null && m.Valid && m.ColonyId == colony.ZdoId && !m.Dissolving) n++;
            return n;
        }

        private static WritheMassState MassOf(WormColony colony)
        {
            foreach (WritheMassState m in WritheMassState.All)
                if (m != null && m.Valid && m.ColonyId == colony.ZdoId && !m.Dissolving) return m;
            return null;
        }

        /// <summary>Runs <paramref name="act"/> after <paramref name="delay"/> real seconds, on the bridge host (a clip blocks the scenario coroutine).</summary>
        private static IEnumerator Later(float delay, Action act)
        {
            float end = Time.realtimeSinceStartup + delay;
            while (Time.realtimeSinceStartup < end) yield return null;
            try { act(); }
            catch (Exception e) { Plugin.LogException("V09 delayed action", e); }
        }

        /// <summary>Calls <paramref name="probe"/> every frame for <paramref name="seconds"/> real seconds.</summary>
        private static IEnumerator Watch(float seconds, Action probe)
        {
            float end = Time.realtimeSinceStartup + seconds;
            bool failed = false;
            while (!failed && Time.realtimeSinceStartup < end)
            {
                try { probe(); }
                catch (Exception e) { Plugin.LogException("V09 watch", e); failed = true; }
                yield return null;
            }
        }

        private static Vector3 Forward(Player player)
        {
            Vector3 f = player.transform.forward;
            f.y = 0f;
            return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
        }

        private static HitData NewHit(WritheMassState mass, float blunt)
        {
            var hit = new HitData();
            hit.m_damage.m_blunt = blunt;
            hit.m_point = mass.transform.position + Vector3.up * 0.5f;
            hit.m_dir = Vector3.forward;
            return hit;
        }
    }
}
#endif
