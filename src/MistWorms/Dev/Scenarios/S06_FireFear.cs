#if MW_DEV
using System.Collections;
using MistWorms.Bites;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Mass;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// S06 (spec 15.5, plan 5-2, A16): the fear of fire, in the open, god mode on (nothing here measures damage).
    /// a) a worm hunting a player who holds a lit torch flees (the mod's own "torch held" criterion for free worms);
    /// b) a mass of 40 hunting a player with a torch never engulfs him for 10 s and never comes closer than Fire.FearRadius (4 m) to him, measured from its
    ///    EDGE (centre distance minus r(n), the same 3D measure as the AI);
    /// c) the same mass against a player with a sword engulfs him in 5 s at most;
    /// d) a camp fire built 3 m from an engulfing mass makes it let go (reason Fire), the Infested effect goes, and it flees for Fire.FearDuration (3 s) at least.
    /// </summary>
    internal sealed class S06_FireFear : IDevScenario
    {
        private const int WormCount = 40;
        private const float TorchSeconds = 10f;
        private const float SwordSeconds = 5f;
        private const float StartDistance = 7f;
        private const float EyeHeight = 0.8f;

        public string Name => "S06_FireFear";

        public IEnumerator Run(ScenarioContext ctx)
        {
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 200f;
            GameObject fire = null;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                yield return ctx.WaitFrames(3);
                yield return ctx.MoveToOpenGround(10f);
                if (!ctx.LastOpenGround.Found) yield break;

                Player player = Player.m_localPlayer;
                player.SetGhostMode(false);
                player.SetGodMode(true);
                S18_GroupCoherence.ClearCreatures(ctx, player);
                ctx.ClearInventory();
                ItemDrop.ItemData torch = ctx.Give(VanillaNames.Torch, equip: true);
                if (!ctx.Assert("torch_equipped", torch != null && torch.m_equipped, "a lit torch in hand", torch == null ? "unknown prefab" : "equipped=" + torch.m_equipped)) yield break;
                yield return ctx.Wait(0.5f);
                ctx.Assert("torch_counts_as_fire", FireSources.HoldsTorch(player), true, FireSources.HoldsTorch(player));

                Vector3 fwd = Flat(player.transform.forward);

                // ---- a) a free worm flees the torch
                yield return WormFlees(ctx, player, fwd);

                // ---- b) torch: no engulfing, 4 m from the edge
                MassEngulf.ResetCounters();
                WritheMassState mass = Spawn(ctx, player, fwd, StartDistance);
                if (mass == null) yield break;
                WritheMassAI ai = mass.GetComponent<WritheMassAI>();
                float r = MassMath.Radius(WormCount);

                // Set-up: the mass has 4 s to come by itself. If it is still out of the fear range, the player walks toward it in small steps (2 m/s, no
                // teleport) until its edge is at FearRadius + 0.6 m + 0.6 m, then stops: the flight must start by itself from there.
                float fear = MWConfig.Fire.FearRadius.Value;
                float settle = Time.time + 4f;
                while (Time.time < settle && mass != null) yield return null;
                float stepEnd = Time.time + 12f;
                while (mass != null && Edge(mass, player, r) > fear + 1.2f && Time.time < stepEnd)
                {
                    Vector3 toMass = Flat(mass.transform.position - player.transform.position);
                    Vector3 stepped = player.transform.position + toMass * (2f * Time.deltaTime);
                    player.transform.position = stepped;
                    player.m_body.position = stepped;
                    yield return null;
                }
                ctx.Log($"torch set-up: mass {MassStateCodes.Describe(mass.StateCode)}, target {(mass.TargetId == ZDOID.None ? "none" : "set")}, moved {ai.MoveTicks} ticks (path {ai.PathTicks}, no path {ai.NoPathTicks}), edge {Edge(mass, player, r):0.00} m, flights {ai.FireFlights}");

                float minEdge = float.MaxValue;
                bool engulfed = false;
                float end = Time.time + TorchSeconds;
                float nextLog = Time.time;
                while (Time.time < end)
                {
                    if (mass == null) break;
                    engulfed |= MassStateCodes.Mode(mass.StateCode) == MassStateCodes.Engulf;
                    minEdge = Mathf.Min(minEdge, Edge(mass, player, r));
                    if (Time.time >= nextLog)
                    {
                        nextLog = Time.time + 1f;
                        Rigidbody mb = mass.GetComponent<Rigidbody>();
                        ctx.Log($"torch: edge {Edge(mass, player, r):0.00} m, {MassStateCodes.Describe(mass.StateCode)}, fleeing {ai.Fleeing}, flights {ai.FireFlights}; mass speed {(mb != null ? Utils.LengthXZ(mb.linearVelocity) : -1f):0.00} m/s, pos {mass.transform.position.ToString("0.00")}, player {player.transform.position.ToString("0.00")}, " +
                                $"flees from {ai.FleeFrom.ToString("0.00")} ({(ai.FleeFrom - player.transform.position).magnitude:0.00} m from the player): {ai.FleeSource}, stalled checks {ai.StalledChecks}, assists {ai.StepAssists}, vanilla corner timer {CornerTimer(ai):0.0} s, flee note: {ai.FleeNote}");
                    }
                    yield return null;
                }
                ctx.Log($"torch: closest edge distance {minEdge:0.00} m, flights {ai.FireFlights}");
                ctx.Assert("torch_never_engulfs", !engulfed && mass != null, $"no Engulf for {TorchSeconds:0} s", engulfed ? "engulfed" : "ok");
                ctx.Assert("torch_keeps_4m", minEdge >= MWConfig.Fire.FearRadius.Value - 0.02f, $">= {MWConfig.Fire.FearRadius.Value:0.0} m from the edge", minEdge);
                ctx.Assert("torch_made_it_flee", ai.FireFlights >= 1, ">= 1 flight", ai.FireFlights);
                yield return ctx.Shot("torch_mass_held_off");
                ctx.Console("mw_kill");
                yield return ctx.WaitFrames(3);

                // ---- c) sword: engulfed in 5 s
                ctx.ClearInventory();
                ItemDrop.ItemData sword = ctx.Give(VanillaNames.SwordBronze, equip: true);
                yield return ctx.Wait(0.5f);
                ctx.Assert("sword_is_not_fire", !FireSources.HoldsTorch(player), true, !FireSources.HoldsTorch(player));
                int repeats = Mathf.Max(1, ctx.ParamInt("repeat", 1));   // dev: replay this phase N times in one pass (a new mass each time, same site and spawnyaw)
                for (int rep = 0; rep < repeats; rep++)
                {
                    string repTag = repeats > 1 ? $" [rep {rep + 1}/{repeats}]" : "";
                    if (rep > 0)
                    {
                        ctx.Console("mw_kill");
                        yield return ctx.Wait(1.5f);
                        yield return ctx.WaitUntil(() => !InfestedEffect.IsActive(player), 3f);
                    }
                    mass = Spawn(ctx, player, fwd, 8f);
                    if (mass == null) yield break;
                    ai = mass.GetComponent<WritheMassAI>();
                    if (ctx.Params.TryGetValue("spawnyaw", out string yawText) && float.TryParse(yawText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float yawOff))
                    {
                        // Dev: the mass is turned to face this many degrees away from the player (180 = its back to him), for the worst case of the turn.
                        Quaternion faced = Quaternion.LookRotation(Flat(player.transform.position - mass.transform.position)) * Quaternion.Euler(0f, yawOff, 0f);
                        mass.transform.rotation = faced;
                        Rigidbody turned = mass.GetComponent<Rigidbody>();
                        if (turned != null) turned.rotation = faced;
                    }
                    float t0 = Time.time;
                    // Instrumentation only (slow start diagnosis): same stop condition and timeout as the former WaitUntil.
                    Rigidbody swordRb = mass.GetComponent<Rigidbody>();
                    string groundNote = Physics.Raycast(player.transform.position + Vector3.up * 2f, Vector3.down, out RaycastHit pg, 6f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore)
                        ? $"{pg.point.y:0.00} (player y {player.transform.position.y:0.00}, collider {pg.collider.name}, normal.y {pg.normal.y:0.00})" : "none";
                    ctx.Log($"sword spawn: ground under player {groundNote}; mass y {mass.transform.position.y:0.00}");
                    bool swordDebug = ctx.ParamBool("debug", false);   // debug=1: the 0.25 s detail; else one summary line
                    if (swordDebug) MassPathProbe.Arm(ai);
                    int drained = 0;
                    float nextSwordLog = t0, below = -1f, startErr = Vector3.Angle(Flat(player.transform.position - mass.transform.position), Flat(mass.transform.forward)), firstMove = -1f;
                    while (true)
                    {
                        float el = Time.time - t0;
                        if (mass == null || MassStateCodes.Mode(mass.StateCode) == MassStateCodes.Engulf || el >= SwordSeconds + 1f) break;
                        float flatD = Utils.DistanceXZ(mass.transform.position, player.transform.position);
                        if (below < 0f && flatD < 1.40f)
                        {
                            below = el;
                            ctx.Log($"sword: flat distance under 1.40 m at t={el:0.00} s");
                        }
                        if (firstMove < 0f && swordRb != null && Utils.LengthXZ(swordRb.linearVelocity) > 0.5f) firstMove = el;
                        if (swordDebug && Time.time >= nextSwordLog)
                        {
                            nextSwordLog += el < 2f ? 0.25f : 1f;
                            Vector3 v = swordRb != null ? swordRb.linearVelocity : Vector3.zero;
                            ctx.Log($"sword t={el:0.00}: flat {flatD:0.00} m, body v {Utils.LengthXZ(v):0.00} m/s (y {v.y:0.00}), path {ai.PathTicks}, nopath {ai.NoPathTicks}, move {ai.MoveTicks}, stalled {ai.StalledChecks}, assists {ai.StepAssists}, state {MassStateCodes.Describe(mass.StateCode)}, target {(mass.TargetId == ZDOID.None ? "none" : "set")}; " +
                                    $"heading error {Vector3.Angle(Flat(player.transform.position - mass.transform.position), Flat(mass.transform.forward)):0.0} deg (moveMinAngle {ai.m_moveMinAngle:0}), moveDir {((Vector3)MoveDirField.GetValue(ai.GetComponent<Character>())).magnitude:0.00}, canMove {ai.GetComponent<Character>().CanMove()}, onGround {ai.GetComponent<Character>().IsOnGround()}, kinematic {swordRb.isKinematic}, sleeping {swordRb.IsSleeping()}, corner timer {CornerTimer(ai):0.0}");
                        }
                        if (swordDebug) drained = DrainProbe(ctx, drained, repTag);
                        yield return null;
                    }
                    if (swordDebug) drained = DrainProbe(ctx, drained, repTag);
                    float took = Time.time - t0;
                    ctx.Log($"sword summary{repTag}: heading error at spawn {startErr:0} deg, body above 0.5 m/s from t={firstMove:0.00} s, flat < 1.40 m at t={below:0.00} s, engulf after {took:0.00} s, path {ai.PathTicks}, no path {ai.NoPathTicks}, stalled {ai.StalledChecks}, assists {ai.StepAssists}" +
                        (swordDebug ? $", initial path length {MassPathProbe.InitialPathLen:0.00} m, FindPath calls {MassPathProbe.FindPathCalls}" : ""));
                    bool inEngulf = mass != null && MassStateCodes.Mode(mass.StateCode) == MassStateCodes.Engulf;
                    ctx.Assert("sword_engulfs", inEngulf && took <= SwordSeconds, $"Engulf within {SwordSeconds:0} s", inEngulf ? $"{took:0.0} s" : "never");
                    if (swordDebug) MassPathProbe.Disarm();
                    if (!inEngulf) yield break;
                    if (rep < repeats - 1) continue;
                }
                yield return ctx.WaitUntil(() => InfestedEffect.IsActive(player), 3f);
                ctx.Assert("infested_active_before_fire", InfestedEffect.IsActive(player), true, InfestedEffect.IsActive(player));

                // ---- d) a camp fire 3 m from the engulfing mass
                Vector3 side = Vector3.Cross(Vector3.up, fwd);
                Vector3 spot = mass.transform.position + side * 3f;
                if (Physics.Raycast(spot + Vector3.up * 3f, Vector3.down, out RaycastHit floor, 8f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore)) spot = floor.point;
                GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(VanillaNames.FirePit) : null;
                if (!ctx.Assert("fire_prefab", prefab != null, VanillaNames.FirePit, prefab != null ? "found" : "missing")) yield break;
                fire = Object.Instantiate(prefab, spot, Quaternion.identity);
                ScenarioReset.TrackPlaced(fire);
                yield return ctx.WaitUntil(() => HasFireAreaNear(spot), 4f);
                ctx.Assert("fire_area_lit", HasFireAreaNear(spot), "a fire EffectArea at the camp fire", HasFireAreaNear(spot));

                float lit = Time.time;
                yield return ctx.WaitUntil(() => mass == null || MassStateCodes.Mode(mass.StateCode) != MassStateCodes.Engulf, 3f);
                float letGo = Time.time - lit;
                ctx.Assert("fire_detaches", mass != null && MassStateCodes.Mode(mass.StateCode) != MassStateCodes.Engulf && ai.LastDetach == DetachReason.Fire,
                    "Hunt, reason Fire, within 3 s", mass == null ? "gone" : $"{MassStateCodes.Describe(mass.StateCode)} {ai.LastDetach} {letGo:0.0} s");
                yield return ctx.WaitUntil(() => !InfestedEffect.IsActive(player), 3f);
                ctx.Assert("fire_removes_infested", !InfestedEffect.IsActive(player), "effect gone within 3 s", InfestedEffect.IsActive(player) ? "still active" : "gone");

                float flight = 0f;
                float from = Time.time;
                while (Time.time - from < MWConfig.Fire.FearDuration.Value + 0.5f && mass != null)
                {
                    if (ai.Fleeing) flight = Time.time - from;
                    yield return null;
                }
                ctx.Assert("fire_flight_lasts", flight >= MWConfig.Fire.FearDuration.Value - 0.3f, $">= {MWConfig.Fire.FearDuration.Value:0.0} s fleeing", $"{flight:0.0} s");
            }
            finally
            {
                if (fire != null && ZNetScene.instance != null) ZNetScene.instance.Destroy(fire);
                ctx.Console("mw_kill");
                ctx.ClearInventory();
                if (Player.m_localPlayer != null)
                {
                    Player.m_localPlayer.SetGodMode(true);
                    Player.m_localPlayer.SetHealth(Player.m_localPlayer.GetMaxHealth());
                }
            }
        }

        /// <summary>Writes the probe samples not yet written (one line per 0.25 s step); returns the new count.</summary>
        private static int DrainProbe(ScenarioContext ctx, int from, string tag)
        {
            for (int i = from; i < MassPathProbe.Count; i++)
            {
                MassPathProbe.Sample s = MassPathProbe.Buffer[i];
                ctx.Log($"probe{tag} t={s.T:0.00}: node err {s.NodeErrDeg:0}deg at {s.NodeDist:0.00} m, path {s.PathCount} nodes {s.PathLen:0.00} m (bird {s.Bird:0.00} m), vel-player {s.VelAngle:0}deg, FindPath +{s.FindPaths}, nodes removed +{s.Removed}, over moveMinAngle {s.OverAngle:0.00} s, player moved {s.PlayerMove:0.000} m, state {MassStateCodes.Describe(s.State)}, ground normal.y {s.GroundNy:0.00}");
            }
            return MassPathProbe.Count;
        }

        private static readonly System.Reflection.FieldInfo MoveDirField = HarmonyLib.AccessTools.Field(typeof(Character), "m_moveDir");
        private static readonly System.Reflection.FieldInfo CornerField = HarmonyLib.AccessTools.Field(typeof(BaseAI), "m_getOutOfCornerTimer");

        /// <summary>The "get out of the corner" timer of the vanilla MoveAndAvoid: while it runs the character walks backwards from its goal.</summary>
        private static float CornerTimer(BaseAI ai) => CornerField != null ? (float)CornerField.GetValue(ai) : -1f;

        private static IEnumerator WormFlees(ScenarioContext ctx, Player player, Vector3 fwd)
        {
            int slot = ColonyDev.SpawnAt(player.transform.position + fwd * 3f, WormState.Hunting, false, out WormColony colony);
            if (!ctx.Assert("worm_spawned", slot >= 0, "a Hunting worm 3 m away", slot >= 0 ? "ok" : "no floor/colony")) yield break;
            bool fled = false;
            float end = Time.time + 3f;
            while (Time.time < end && !fled)
            {
                fled = colony.Store.state[slot] == (byte)WormState.Fleeing;
                if (!fled) yield return null;
            }
            ctx.Assert("worm_flees_torch", fled, "Fleeing within 3 s", fled ? "fleeing" : ((WormState)colony.Store.state[slot]).ToString());
            ctx.Console("mw_kill");
            yield return ctx.WaitFrames(3);
        }

        /// <summary>
        /// A mass of 40 about <paramref name="distance"/> m from the player, for sure: along the wanted direction, then 25, 50, 90 degrees to either side, at 1, 0.8 and
        /// 0.6 of the distance; a spot is kept when it has a floor within 2.5 m of the player's height. The first pass also wants a line of sight to the player,
        /// the second takes any floor. The attempt that worked is logged.
        /// </summary>
        private static WritheMassState Spawn(ScenarioContext ctx, Player player, Vector3 fwd, float distance)
        {
            float[] scales = { 1f, 0.8f, 0.6f };
            float[] angles = { 0f, 25f, -25f, 50f, -50f, 90f, -90f };
            Vector3 origin = player.transform.position;
            int tried = 0;
            for (int pass = 0; pass < 2; pass++)
                foreach (float k in scales)
                    foreach (float a in angles)
                    {
                        tried++;
                        Vector3 at = origin + Quaternion.AngleAxis(a, Vector3.up) * fwd * (distance * k);
                        WritheMassState m = MassDev.SpawnAt(at, WormCount, ZDOID.None, freezeAI: false);
                        if (m == null) continue;
                        bool sight = !Physics.Linecast(m.transform.position + Vector3.up * EyeHeight, player.GetCenterPoint(), WormPhysics.ViewBlockMask, QueryTriggerInteraction.Ignore);
                        bool level = Mathf.Abs(m.transform.position.y - origin.y) <= 2.5f;
                        if (!level || (pass == 0 && !sight))
                        {
                            MassDev.Destroy(m);
                            continue;
                        }
                        float d = Utils.DistanceXZ(m.transform.position, origin);
                        ctx.Log($"mass spawned {d:0.#} m away at attempt {tried} (scale {k}, angle {a}), line of sight {sight}");
                        ctx.Assert("mass_created", m.Valid, "a valid mass", m.ZdoId.ToString());
                        m.GetComponent<WritheMassAI>().DevForcedTarget = player;
                        return m;
                    }
            ctx.Assert("mass_created", false, "a mass with a floor near the player's height", $"none in {tried} attempts around {origin}");
            return null;
        }

        /// <summary>Distance from the edge of the mass to the player, with the measure the AI uses (centre at eye height, 3D).</summary>
        private static float Edge(WritheMassState mass, Player player, float radius) =>
            (player.transform.position - (mass.transform.position + Vector3.up * EyeHeight)).magnitude - radius;

        private static bool HasFireAreaNear(Vector3 point)
        {
            var areas = EffectArea.GetAllAreas();
            for (int i = 0; i < areas.Count; i++)
            {
                EffectArea a = areas[i];
                if (a != null && a.isActiveAndEnabled && (a.m_type & EffectArea.Type.Fire) != 0 && (a.transform.position - point).sqrMagnitude < 4f) return true;
            }
            return false;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward;
        }
    }
}
#endif
