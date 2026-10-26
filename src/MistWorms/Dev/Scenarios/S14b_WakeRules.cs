#if MW_DEV
using System;
using System.Collections;
using MistWorms.Colony;
using MistWorms.Config;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// S14b (spec §15.5, §3.3, §5.1, plan 7-2): the rules of the waking, on open ground. A test colony with four nests (three in line, 15 m apart, and a fourth 60 m
    /// from the first), five sleeping worms at each, and one sleeping worm of no nest. A hit with no attacker, and a hit by a creature, wake nothing; a hit by the
    /// player on nest 1 wakes group 1 at once, group 2 one hop later (0.5 s) and group 3 two hops later (1 s), and group 4 sleeps on; a player within Nest.WakeRadius of
    /// a nest in line of sight wakes it, behind a wall he does not; a player who stands still (or crouches: no footstep noise) 5 m from the lone worm wakes nothing, one
    /// who makes the noise of a walking player wakes it. Nobody hunts: the Targets.Attack* keys are off, restored at the end.
    /// </summary>
    internal sealed class S14b_WakeRules : IDevScenario
    {
        private const int Nests = 4;
        private const int PerNest = 5;
        private const float Spacing = 15f;
        private const float LayoutDy = 8f;            // height difference to the player the nests and the lone worm tolerate
        private const float LayoutAngle = 40f;        // steepest ground for them
        private const float FarMargin = 5f;           // the far nest lies at least AlertRadius + this from the chain nests
        private const float ViewerEyeHeight = 1.6f;      // the eyes of a standing player over his feet
        private const float WalkNoise = 15f;          // Character.AddNoise(15) of a walking player (Character.cs:1857)

        public string Name => "S14b_WakeRules";

        public IEnumerator Run(ScenarioContext ctx)
        {
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 240f;
            bool clutterHidden = false;
            bool gatheringBefore = ColonyDev.GatheringEnabled;
            bool attackPlayers = MWConfig.Targets.AttackPlayers.Value, attackTamed = MWConfig.Targets.AttackTamed.Value;
            bool attackCreatures = MWConfig.Targets.AttackCreatures.Value, attackStructures = MWConfig.Targets.AttackStructures.Value;
            GameObject wall = null;
            GameObject boar = null;
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                ColonyDev.GatheringEnabled = false;
                MWConfig.Targets.AttackPlayers.Value = false;
                MWConfig.Targets.AttackTamed.Value = false;
                MWConfig.Targets.AttackCreatures.Value = false;
                MWConfig.Targets.AttackStructures.Value = false;
                yield return ctx.WaitFrames(3);

                if (ClutterSystem.instance != null)
                {
                    ClutterSystem.instance.enabled = false;
                    ClutterSystem.instance.ClearAll();
                    clutterHidden = true;
                }
                yield return ctx.MoveToOpenGround(8f);
                if (!ctx.LastOpenGround.Found) yield break;
                Player player = Player.m_localPlayer;
                player.SetGodMode(true);
                player.SetGhostMode(false);

                // ---- the layout: a chain of three nests 15 m apart (nests 1, 2, 3) along a direction u, a fourth farther from all of them than Colony.AlertRadius + a margin, and a lone
                // worm. Ground may slope (up to LayoutDy of height difference, 40 degrees) and the directions are tried all around; each failed try says why.
                Vector3 origin = player.transform.position;
                var nestPoints = new Vector3[Nests];
                Vector3 u = Vector3.forward, side = Vector3.right;
                Vector3 lonePoint = default;
                bool laidOut = false;
                float alert = MWConfig.Colony.AlertRadius.Value;
                for (int turn = 0; turn < 8 && !laidOut; turn++)
                {
                    u = Quaternion.AngleAxis(45f * turn, Vector3.up) * Vector3.forward;
                    side = Vector3.Cross(Vector3.up, u);
                    string why = null;
                    for (int c = 0; c < 3 && why == null; c++)
                        if (!FloorAt(origin + u * (-45f + 15f * c), origin.y, out nestPoints[c], LayoutDy, LayoutAngle)) why = $"no floor for chain nest {c + 1}";
                    if (why == null && (Vector3.Distance(nestPoints[0], nestPoints[1]) > alert - 2f || Vector3.Distance(nestPoints[1], nestPoints[2]) > alert - 2f)) why = "the chain nests are too far apart on this slope";
                    if (why == null && Vector3.Distance(nestPoints[0], nestPoints[2]) <= alert + 1f) why = "nest 3 would be a first hop of nest 1";
                    if (why == null && !FarNest(nestPoints, origin, alert, out nestPoints[3])) why = $"no floor for the far nest at {alert + FarMargin:0}+ m from the chain and 14+ m from the player";
                    if (why == null && !LoneSpot(nestPoints, origin, side, out lonePoint)) why = "no floor for the lone worm 14+ m from the nests, 20+ m from the player";
                    if (why != null) ctx.Log($"layout try {turn} (u {u.x:0.0},{u.z:0.0}): {why}");
                    laidOut = why == null;
                }
                if (!ctx.Assert("layout_found", laidOut, "four nests and a lone worm on the ground around the player", laidOut)) yield break;

                WormColony colony = ColonyDev.FindOrCreateColony(player, out string how);
                ctx.Log($"colony {colony.ZdoId} ({how}); nests at {nestPoints[0]}, {nestPoints[1]}, {nestPoints[2]}, {nestPoints[3]}; lone worm at {lonePoint}");
                var nests = new WormNest[Nests];
                for (int k = 0; k < Nests; k++)
                {
                    nests[k] = colony.SpawnNest(nestPoints[k], 0f, k);
                    if (nests[k] == null) { ctx.Assert("nest_created", false, k, "null"); yield break; }
                    for (int w = 0; w < PerNest; w++) SleeperAround(colony, nestPoints[k], k, w);
                }
                int loneSlot = colony.AddWorm(lonePoint, WormState.Sleeping, 0f);
                int total = colony.Store.AliveCount;
                ctx.Assert("sleepers_placed", total == Nests * PerNest + 1 && loneSlot >= 0, Nests * PerNest + 1, total);
                yield return ctx.WaitGame(2f);
                ctx.Log($"asleep per group: {CountAsleep(colony, 0)}, {CountAsleep(colony, 1)}, {CountAsleep(colony, 2)}, {CountAsleep(colony, 3)}, no nest {CountAsleepNoNest(colony)}; removed: {string.Join(" | ", colony.DeathCauses)}");
                ctx.Assert("everything_sleeps_at_the_start", colony.DevCountState(WormState.Sleeping) == total && colony.AwakeBits == 0L, $"{total} asleep, no group awake",
                    $"{colony.DevCountState(WormState.Sleeping)} asleep, bits {colony.AwakeBits}");

                // ---- (a) a hit with no attacker, and a hit by a creature, wake nothing
                int target = FirstOfGroup(colony, 3);
                float hpBefore = colony.Store.hp[target];
                ColonyDev.HitWorm(colony, target, Blunt(1f, colony.Store.pos[target]));
                yield return ctx.WaitGame(1.2f);
                ctx.Assert("hit_without_attacker_wakes_nothing", colony.DevCountState(WormState.Sleeping) == total && !colony.IsGroupAwake(3) && colony.Store.hp[target] < hpBefore - 0.5f,
                    "all asleep, the worm hurt", $"{colony.DevCountState(WormState.Sleeping)} asleep, group 4 awake {colony.IsGroupAwake(3)}, hp {hpBefore:0.#} -> {colony.Store.hp[target]:0.#}");

                GameObject boarPrefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("Boar") : null;
                Character creature = null;
                if (boarPrefab != null)
                {
                    boar = UnityEngine.Object.Instantiate(boarPrefab, nestPoints[3] + side * 10f + Vector3.up * 1f, Quaternion.identity);
                    creature = boar != null ? boar.GetComponent<Character>() : null;
                }
                if (creature != null)
                {
                    yield return ctx.WaitFrames(5);
                    hpBefore = colony.Store.hp[target];
                    HitData byCreature = Blunt(1f, colony.Store.pos[target]);
                    byCreature.SetAttacker(creature);
                    ColonyDev.HitWorm(colony, target, byCreature);
                    yield return ctx.WaitGame(1.2f);
                    ctx.Assert("hit_by_a_creature_wakes_nothing", colony.DevCountState(WormState.Sleeping) == total && !colony.IsGroupAwake(3) && colony.Store.hp[target] < hpBefore - 0.5f,
                        "all asleep, the worm hurt", $"{colony.DevCountState(WormState.Sleeping)} asleep, group 4 awake {colony.IsGroupAwake(3)}, hp {hpBefore:0.#} -> {colony.Store.hp[target]:0.#}");
                }
                else
                {
                    ctx.Log("no Boar prefab or no Character on it: the creature hit is not tested");
                }
                if (boar != null) { if (ZNetScene.instance != null) ZNetScene.instance.Destroy(boar); else UnityEngine.Object.Destroy(boar); boar = null; }

                // ---- (b) a hit by the player on nest 1: groups 1, 2, 3 at 0, 0.5 and 1 s, group 4 sleeps on
                var wokeAt = new float[Nests];
                for (int k = 0; k < Nests; k++) wokeAt[k] = -1f;
                float t0 = Time.time;
                NestDev.Hit(nests[0], 5f);
                while (Time.time - t0 < 4f)
                {
                    yield return null;
                    for (int k = 0; k < Nests; k++)
                        if (wokeAt[k] < 0f && colony.IsGroupAwake(k)) wokeAt[k] = Time.time - t0;
                }
                ctx.Log($"groups awake at {wokeAt[0]:0.00}, {wokeAt[1]:0.00}, {wokeAt[2]:0.00}, {wokeAt[3]:0.00} s (-1 = never)");
                ctx.Assert("nest_1_wakes_at_once", wokeAt[0] >= 0f && wokeAt[0] <= 0.3f, "<= 0.3 s", wokeAt[0]);
                ctx.Assert("nest_2_wakes_one_hop_later", wokeAt[1] >= 0.35f && wokeAt[1] <= 0.85f, "about 0.5 s", wokeAt[1]);
                ctx.Assert("nest_3_wakes_two_hops_later", wokeAt[2] >= 0.85f && wokeAt[2] <= 1.4f, "about 1.0 s", wokeAt[2]);
                int asleepInChain = 0;
                for (int g = 0; g < 3; g++) asleepInChain += CountAsleep(colony, g);
                ctx.Assert("the_worms_of_the_woken_groups_are_up", asleepInChain == 0, 0, asleepInChain);
                ctx.Assert("nest_4_sleeps_on", wokeAt[3] < 0f && CountAsleep(colony, 3) == PerNest, "group 4 and its 5 worms asleep", $"group 4 woke at {wokeAt[3]:0.00}, {CountAsleep(colony, 3)} asleep");

                // ---- (c) the sight of a nest: 9 m from nest 4 in line of sight wakes it, behind a wall it does not
                // The nest looks from NestEyeHeight over its floor point with the view blockers of the worms: on open ground a bump of the terrain between the nest and the viewer hides him
                // (r8: nest 4 on a ridge, no wake in 3 s). The viewer's place is the first of the 12 directions around the nest, 9 m out, where the ray from the nest to his eyes is clear.
                Vector3 towardBase = nestPoints[3] - nestPoints[2];
                towardBase.y = 0f;
                towardBase = towardBase.sqrMagnitude > 1e-4f ? towardBase.normalized : u;
                Vector3 nestEye = nestPoints[3] + Vector3.up * 0.4f;
                Vector3 toward = towardBase, viewerFloor = default;
                bool viewerFound = false;
                string blocked = null;
                for (int k = 0; k < 12 && !viewerFound; k++)
                {
                    float angle = k % 2 == 0 ? k / 2 * 30f : -(k / 2 + 1) * 30f;
                    Vector3 away = Quaternion.AngleAxis(angle, Vector3.up) * -towardBase;      // from the nest to the viewer
                    if (!FloorAt(nestPoints[3] + away * 9f, nestPoints[3].y, out Vector3 floor, LayoutDy, LayoutAngle)) { blocked = $"no floor at {angle:0} degrees"; continue; }
                    if (Physics.Linecast(nestEye, floor + Vector3.up * ViewerEyeHeight, out RaycastHit block, WormPhysics.ViewBlockMask, QueryTriggerInteraction.Ignore))
                    {
                        blocked = $"{angle:0} degrees: {block.collider.name} (layer {LayerMask.LayerToName(block.collider.gameObject.layer)}) at {block.point}";
                        continue;
                    }
                    toward = -away;
                    viewerFloor = floor;
                    viewerFound = true;
                }
                if (viewerFound)
                {
                    Vector3 viewer = viewerFloor;
                    wall = new GameObject("MW_DevWall");
                    wall.transform.position = nestPoints[3] - toward * 3f + Vector3.up * 2f;
                    wall.transform.rotation = Quaternion.LookRotation(toward);
                    wall.AddComponent<BoxCollider>().size = new Vector3(10f, 4f, 0.4f);
                    var behind = new TeleportOutcome();
                    yield return DevWorld.TeleportPlayer(viewerFloor + Vector3.up * 0.1f, Quaternion.LookRotation(toward), false, 30f, behind);
                    ctx.Assert("teleport_behind_the_wall", behind.Ok, true, behind.Ok ? "arrived" : behind.Error);
                    yield return ctx.WaitGame(2.5f);
                    ctx.Assert("a_wall_hides_the_player_from_the_nest", !colony.IsGroupAwake(3), "group 4 asleep", colony.IsGroupAwake(3));

                    UnityEngine.Object.Destroy(wall);
                    wall = null;
                    yield return null;
                    float seen0 = Time.time;
                    float seenAt = -1f;
                    while (Time.time - seen0 < 3f && seenAt < 0f)
                    {
                        yield return null;
                        if (colony.IsGroupAwake(3)) seenAt = Time.time - seen0;
                    }
                    ctx.Assert("a_player_in_sight_9_m_from_the_nest_wakes_it", seenAt >= 0f && seenAt <= 1.2f, "within 1.2 s", seenAt < 0f ? "never" : $"{seenAt:0.00} s");
                    if (seenAt < 0f)
                    {
                        Player seer = Player.m_localPlayer;
                        bool hit = Physics.Linecast(nestEye, seer.GetEyePoint(), out RaycastHit now, WormPhysics.ViewBlockMask, QueryTriggerInteraction.Ignore);
                        ctx.Log($"diagnosis: player eye {Vector3.Distance(nestEye, seer.GetEyePoint()):0.0} m from the nest eye (viewer wanted at {viewer}, is at {seer.transform.position}), ghost {seer.InGhostMode()}, teleporting {seer.IsTeleporting()}, ray {(hit ? $"blocked by {now.collider.name} at {now.point}" : "clear")}, WakeRadius {MWConfig.Nest.WakeRadius.Value:0.#}");
                    }
                }
                else
                {
                    ctx.Log($"no place 9 m from nest 4 with a clear line to the nest: the sight of the nest is not tested (last: {blocked})");
                }

                // ---- (d) the senses of a sleeping worm: standing still (a crouching player makes no noise) 5 m away, then the noise of a walk
                Vector3 listener = lonePoint + (origin - lonePoint).normalized * 5f;
                if (!FloorAt(listener, lonePoint.y, out Vector3 listenerFloor, LayoutDy, LayoutAngle)) listenerFloor = lonePoint + side * 5f;
                var near = new TeleportOutcome();
                yield return DevWorld.TeleportPlayer(listenerFloor + Vector3.up * 0.1f, Quaternion.LookRotation(lonePoint - listenerFloor), false, 30f, near);
                ctx.Assert("teleport_next_to_the_lone_worm", near.Ok, true, near.Ok ? "arrived" : near.Error);
                player = Player.m_localPlayer;
                float distance = Vector3.Distance(player.transform.position, colony.Store.pos[loneSlot]);
                ctx.Log($"the player stands {distance:0.0} m from the lone worm");
                player.SetCrouch(true);
                yield return ctx.WaitGame(3f);
                ctx.Assert("a_crouching_player_at_5_m_wakes_nothing", colony.Store.state[loneSlot] == (byte)WormState.Sleeping, "asleep", (WormState)colony.Store.state[loneSlot]);
                player.SetCrouch(false);

                float walk0 = Time.time;
                float woke = -1f;
                float nextNoise = 0f;
                while (Time.time - walk0 < 3f && woke < 0f)
                {
                    if (Time.time >= nextNoise)
                    {
                        nextNoise = Time.time + 0.25f;
                        player.AddNoise(WalkNoise);
                    }
                    yield return null;
                    if (colony.Store.state[loneSlot] != (byte)WormState.Sleeping) woke = Time.time - walk0;
                }
                ctx.Assert("a_walking_player_at_5_m_wakes_it", woke >= 0f && woke <= 2.5f, $"awake within 2.5 s (noise {WalkNoise})", woke < 0f ? "still asleep" : $"{woke:0.00} s");
                yield return ctx.Shot("woken_chain");
            }
            finally
            {
                if (wall != null) UnityEngine.Object.Destroy(wall);
                if (boar != null) { if (ZNetScene.instance != null) ZNetScene.instance.Destroy(boar); else UnityEngine.Object.Destroy(boar); }
                MWConfig.Targets.AttackPlayers.Value = attackPlayers;
                MWConfig.Targets.AttackTamed.Value = attackTamed;
                MWConfig.Targets.AttackCreatures.Value = attackCreatures;
                MWConfig.Targets.AttackStructures.Value = attackStructures;
                ColonyDev.GatheringEnabled = gatheringBefore;
                if (clutterHidden && ClutterSystem.instance != null) ClutterSystem.instance.enabled = true;
                ctx.Console("mw_kill");
                Player p = Player.m_localPlayer;
                if (p != null) { p.SetCrouch(false); p.SetGodMode(false); }
            }
        }

        private static HitData Blunt(float damage, Vector3 at)
        {
            var hit = new HitData();
            hit.m_damage.m_blunt = damage;
            hit.m_point = at + Vector3.up * 0.1f;
            hit.m_dir = Vector3.forward;
            return hit;
        }

        /// <summary>A flat floor point under <paramref name="p"/> within 3 m of <paramref name="y"/>.</summary>
        private static bool FloorAt(Vector3 p, float y, out Vector3 floor, float maxDy = 3f, float maxAngle = 20f)
        {
            floor = default;
            if (!Physics.Raycast(new Vector3(p.x, y + 40f, p.z), Vector3.down, out RaycastHit hit, 80f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore)) return false;
            if (Mathf.Abs(hit.point.y - y) > maxDy || Vector3.Angle(hit.normal, Vector3.up) > maxAngle) return false;
            floor = hit.point;
            return true;
        }

        /// <summary>The fourth nest: any floor point (not aligned, not flat) at least alert + margin from the three chain nests and 14 m from the player (nearest candidate first).</summary>
        private static bool FarNest(Vector3[] chain, Vector3 origin, float alert, out Vector3 point)
        {
            float need = alert + FarMargin;
            for (float radius = 34f; radius <= 70f; radius += 6f)
                for (int k = 0; k < 12; k++)
                {
                    Vector3 dir = Quaternion.AngleAxis(k * 30f, Vector3.up) * Vector3.forward;
                    if (!FloorAt(chain[2] + dir * radius, origin.y, out point, LayoutDy, LayoutAngle)) continue;
                    if (Vector3.Distance(point, origin) < 14f) continue;
                    if (Vector3.Distance(point, chain[0]) < need || Vector3.Distance(point, chain[1]) < need || Vector3.Distance(point, chain[2]) < need) continue;
                    return true;
                }
            point = default;
            return false;
        }

        /// <summary>The lone worm: a floor point 14 m or more from every nest, 20 m or more from the player, about 25 m to the side first.</summary>
        private static bool LoneSpot(Vector3[] nests, Vector3 origin, Vector3 side, out Vector3 point)
        {
            for (int k = 0; k < 12; k++)
            {
                Vector3 dir = Quaternion.AngleAxis(k % 2 == 0 ? k / 2 * 30f : -(k / 2 + 1) * 30f, Vector3.up) * side;
                if (!FloorAt(origin + dir * 25f, origin.y, out point, LayoutDy, LayoutAngle)) continue;
                bool apart = true;
                for (int n = 0; n < nests.Length && apart; n++) apart = Vector3.Distance(point, nests[n]) >= 14f;
                if (apart && Vector3.Distance(point, origin) >= 20f) return true;
            }
            point = default;
            return false;
        }

        private static void SleeperAround(WormColony colony, Vector3 center, int group, int w)
        {
            WakeMath.RingPoint(w, PerNest, group * 0.7f, 1.5f, 2.5f, out float dx, out float dz);
            // On the floor under the ring point, found from above: on a slope the point 0.5 m over the nest's level lies inside the ground uphill, where a worm has no floor to probe and is
            // removed (r7: a nest on a slope lost one of its five worms). With no floor under it, the worm sleeps at the foot of the nest.
            Vector3 at = center + new Vector3(dx, 0.5f, dz);
            if (Physics.Raycast(center + new Vector3(dx, 4f, dz), Vector3.down, out RaycastHit hit, 10f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore)
                && Mathf.Abs(hit.point.y - center.y) < 3f && Vector3.Angle(hit.normal, Vector3.up) <= LayoutAngle)
                at = hit.point + Vector3.up * 0.1f;
            else
                at = center + Vector3.up * 0.1f;
            int slot = colony.AddWorm(at, WormState.Sleeping, WormMath.HeadingOf(dx, dz));
            if (slot >= 0) colony.Store.nestIndex[slot] = (short)group;
        }

        private static int FirstOfGroup(WormColony colony, int group)
        {
            WormStore s = colony.Store;
            for (int i = 0; i < s.HighWater; i++)
                if (s.alive[i] && s.nestIndex[i] == group) return i;
            return -1;
        }

        private static int CountAsleepNoNest(WormColony colony)
        {
            WormStore s = colony.Store;
            int n = 0;
            for (int i = 0; i < s.HighWater; i++)
                if (s.alive[i] && s.nestIndex[i] < 0 && s.state[i] == (byte)WormState.Sleeping) n++;
            return n;
        }

        private static int CountAsleep(WormColony colony, int group)
        {
            WormStore s = colony.Store;
            int n = 0;
            for (int i = 0; i < s.HighWater; i++)
                if (s.alive[i] && s.nestIndex[i] == group && s.state[i] == (byte)WormState.Sleeping) n++;
            return n;
        }
    }
}
#endif
