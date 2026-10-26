#if MW_DEV
using System;
using System.Collections;
using MistWorms.Assets;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Dev.Preview;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>
    /// V06 (spec §15.5, §12.4, phase 7-1): the nest as a world object, in the infested mine (parameter where=open for the open ground). A nest is placed in
    /// front of the player and captured from 1 m and from 3 m; then it takes a lethal hit and the destruction is captured at +0.05, +0.2 and +0.5 s. Assertions:
    /// the renderer draws the mound, the destruction effect is a local copy with no ZNetView and a sound, the nest and its ZDO are gone, the worms are
    /// released and nothing is dropped. The look of the captures is judged by eye.
    /// </summary>
    internal sealed class V06_Nest : IDevScenario
    {
        private const float SpawnDistance = 3.5f;
        private const int Directions = 16;
        private static readonly float[] BurstTimes = { 0.05f, 0.2f, 0.5f };

        public string Name => "V06_Nest";

        public IEnumerator Run(ScenarioContext ctx)
        {
            string where = ctx.Param("where", "mine").ToLowerInvariant();
            if (!ctx.Params.ContainsKey("timeout")) ctx.TimeoutSeconds = 240f;
            bool clutterHidden = false;
            bool gatheringBefore = ColonyDev.GatheringEnabled;
            GameObject visual = null;
            bool visualWasActive = false;
            DevShots.HideUi();
            try
            {
                yield return ctx.WaitUntil(() => Player.m_localPlayer != null && DevBridge.IsReady(Player.m_localPlayer), 60f, "player_ready");
                if (ctx.LastWaitTimedOut) yield break;

                ctx.Console("mw_kill");
                ctx.Console("mw_freeze off");
                ColonyDev.GatheringEnabled = false;
                yield return ctx.WaitFrames(3);

                bool assets = AssetCatalog.IsReady && MaterialFactory.EnsureBuilt();
                ctx.Assert("assets_and_materials", assets, true, assets ? "ready" : MaterialFactory.Error ?? AssetCatalog.Error);
                bool prefab = ZNetScene.instance != null && ZNetScene.instance.GetPrefab(MWNames.NestPrefab) != null;
                ctx.Assert("prefab_registered", prefab, MWNames.NestPrefab, prefab ? "registered" : "missing");
                if (!assets || !prefab) yield break;

                Player.m_localPlayer.SetGodMode(true);
                bool inMine = false;
                if (where != "open")
                {
                    Player.m_localPlayer.SetGhostMode(true);
                    yield return ctx.TpMine();
                    inMine = ctx.LastTpMine.Ok;
                    if (!inMine) ctx.Log($"mine not reachable ({ctx.LastTpMine.Error}): falling back to open ground");
                    else yield return ctx.Wait(1f);
                }
                if (!inMine)
                {
                    if (ClutterSystem.instance != null)
                    {
                        ClutterSystem.instance.enabled = false;
                        ClutterSystem.instance.ClearAll();
                        clutterHidden = true;
                    }
                    yield return ctx.MoveToOpenGround(5f);
                    if (!ctx.LastOpenGround.Found) yield break;
                    Player.m_localPlayer.SetGhostMode(false);
                }
                ctx.Log(inMine ? "context: infested mine" : "context: open ground");

                Player player = Player.m_localPlayer;
                visual = player.m_visual;
                if (visual != null)
                {
                    visualWasActive = visual.activeSelf;
                    visual.SetActive(false);
                }

                Vector3 spot = default, toCamera = default;
                bool spotFound = ChooseSpot(player.transform.position, player.transform.forward, out spot, out toCamera);
                if (!spotFound && inMine)
                {
                    // Cramped around the entrance: another loaded room of the mine, the player moved there.
                    Vector3 roomPoint = default;
                    yield return FindRoomSpot(ctx, player, (ok, p) => { spotFound = ok; roomPoint = p; });
                    if (spotFound)
                    {
                        var move = new TeleportOutcome();
                        yield return DevWorld.TeleportPlayer(roomPoint + Vector3.up * 0.1f, player.transform.rotation, false, 30f, move);
                        player = Player.m_localPlayer;
                        spotFound = move.Ok && ChooseSpot(player.transform.position, player.transform.forward, out spot, out toCamera);
                        if (spotFound) yield return ctx.Wait(0.5f);
                    }
                    if (!spotFound)
                    {
                        // No room fits: the open ground, as without the mine.
                        ctx.Log("no free spot in the loaded rooms of the mine: falling back to open ground");
                        if (ClutterSystem.instance != null)
                        {
                            ClutterSystem.instance.enabled = false;
                            ClutterSystem.instance.ClearAll();
                            clutterHidden = true;
                        }
                        yield return ctx.MoveToOpenGround(5f);
                        if (!ctx.LastOpenGround.Found) yield break;
                        player = Player.m_localPlayer;
                        player.SetGhostMode(false);
                        inMine = false;
                        spotFound = ChooseSpot(player.transform.position, player.transform.forward, out spot, out toCamera);
                    }
                }
                if (!spotFound)
                {
                    ctx.Log($"last spot search rejected: {LastRejects}");
                    ctx.Assert("spot_with_a_free_camera", false, "a flat floor with room for both cameras", "none within reach");
                    yield break;
                }
                WormColony colony = ColonyDev.FindOrCreateColony(player, out string how);
                WormNest nest = colony != null ? colony.SpawnNest(spot, player.transform.eulerAngles.y) : null;
                if (!ctx.Assert("nest_placed", nest != null, true, nest != null ? $"nest {nest.Index} in {how} colony" : "failed")) yield break;
                yield return ctx.WaitFrames(5);

                // ---- from 1 m and from 3 m
                Vector3 look = spot + Vector3.up * 0.25f;
                int worldMask = LayerMask.GetMask("terrain", "static_solid", "Default", "Default_small", "piece");
                yield return Frame(ctx, "1m", spot + toCamera * 1f + Vector3.up * 0.45f, look);
                yield return Frame(ctx, "3m", spot + toCamera * 3f + Vector3.up * 0.9f, look);
                // The line to the nest, from where the 3 m camera really is. The nest itself does not count, nor does the flat stuff lying on the floor (a dvergr slime puddle).
                Vector3 cam3 = spot + toCamera * 3f + Vector3.up * 0.9f;
                RaycastHit[] hits = Physics.RaycastAll(cam3, look - cam3, (look - cam3).magnitude, worldMask, QueryTriggerInteraction.Ignore);
                string blocker = null;
                foreach (RaycastHit h in hits)
                {
                    if (h.collider.transform.IsChildOf(nest.transform)) continue;
                    if (h.collider.bounds.max.y <= spot.y + 0.2f && h.collider.gameObject.layer != LayerMask.NameToLayer("terrain")) continue;
                    blocker = h.collider.name;
                    break;
                }
                ctx.Assert("camera_has_a_clear_line", blocker == null, "clear", blocker ?? "clear");

                // ---- destruction, from 3 m
                Vector3 where3 = nest.transform.position;
                int itemsBefore = ItemsNear(where3);
                int released = colony.WormsReleased;
                int fxBefore = WormEffects.FxSpawned;
                ZDOID id = nest.ZdoId;
                NestDev.Hit(nest, MWConfig.Nest.Health.Value * 2f);
                float t0 = Time.realtimeSinceStartup;
                yield return ctx.WaitFrames(2);
                for (int i = 0; i < BurstTimes.Length; i++)
                {
                    float wait = BurstTimes[i] - (Time.realtimeSinceStartup - t0);
                    if (wait > 0f) yield return ctx.Wait(wait);
                    yield return ctx.Shot($"burst_{BurstTimes[i].ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', '_')}");
                }

                ctx.Assert("nest_destroyed", nest == null, true, nest == null);
                ctx.Assert("nest_zdo_removed", ZDOMan.instance != null && ZDOMan.instance.GetZDO(id) == null, "no ZDO", ZDOMan.instance != null && ZDOMan.instance.GetZDO(id) == null ? "removed" : "still there");
                int wanted = MWConfig.Nest.ReleaseOnDestroy.Value;
                ctx.Assert("worms_released", colony.WormsReleased - released == wanted, wanted, colony.WormsReleased - released);
                ctx.Assert("nothing_dropped", ItemsNear(where3) == itemsBefore, itemsBefore, ItemsNear(where3));
                GameObject fx = WormEffects.LastFx;
                bool played = WormEffects.FxSpawned > fxBefore && fx != null;
                ctx.Assert("destruction_effect_played", played, "an effect spawned", WormEffects.FxSpawned - fxBefore);
                if (played)
                {
                    ctx.Assert("destruction_effect_is_local", fx.GetComponentsInChildren<ZNetView>(true).Length == 0, "no ZNetView", fx.GetComponentsInChildren<ZNetView>(true).Length);
                    ctx.Assert("destruction_effect_has_a_sound", fx.GetComponentInChildren<ZSFX>(true) != null, "a ZSFX", fx.GetComponentInChildren<ZSFX>(true) != null);
                }
            }
            finally
            {
                DevShots.RestoreUi();
                if (visual != null) visual.SetActive(visualWasActive);
                if (clutterHidden && ClutterSystem.instance != null) ClutterSystem.instance.enabled = true;
                ColonyDev.GatheringEnabled = gatheringBefore;
                ctx.Console("mw_kill");
                DevCamera.Follow();
            }
        }

        private static IEnumerator Frame(ScenarioContext ctx, string name, Vector3 camera, Vector3 look)
        {
            DevCamera.Free(camera, look);
            yield return ctx.WaitFrames(8);
            WormRenderer.FrameStats stats = WormRenderer.Last;
            ctx.Assert($"nest_drawn_{name}", stats.Instances >= 1, ">= 1 instance", $"{stats.Instances} instances, {stats.Triangles} triangles");
            yield return ctx.Shot($"nest_{name}");
        }

        /// <summary>
        /// A flat floor about 3.5 m from the player in the direction nearest to the player's facing, clear of walls and with a free camera on one side at 1 m
        /// (45 cm up) and at 3 m (90 cm up). <paramref name="toCamera"/> is the horizontal direction from the nest to the cameras.
        /// </summary>
        private static bool ChooseSpot(Vector3 origin, Vector3 forward, out Vector3 spot, out Vector3 toCamera)
        {
            Vector3 face = forward;
            face.y = 0f;
            face = face.sqrMagnitude > 1e-4f ? face.normalized : Vector3.forward;
            int mask = LayerMask.GetMask("terrain", "static_solid", "Default", "Default_small", "piece");
            int noFloor = 0, steep = 0, crowded = 0, camBlocked = 0, camNoView = 0;

            for (int k = 0; k < Directions; k++)
            {
                int step = (k + 1) / 2 * (k % 2 == 1 ? 1 : -1);
                Vector3 dir = Quaternion.Euler(0f, step * 360f / Directions, 0f) * face;
                Vector3 probe = origin + dir * SpawnDistance;
                if (!Physics.Raycast(probe + Vector3.up * 1f, Vector3.down, out RaycastHit floor, 3f, mask, QueryTriggerInteraction.Ignore)) { noFloor++; continue; }
                if (floor.normal.y < 0.9f) { steep++; continue; }      // about 25 degrees: the mound sits on it
                // Room for the mound above the floor: the sphere's bottom is 40 cm over the floor (it used to be centred 40 cm up with a 70 cm radius, so it always hit the floor itself).
                if (Physics.CheckSphere(floor.point + Vector3.up * 1.1f, 0.7f, mask, QueryTriggerInteraction.Ignore)) { crowded++; continue; }
                Vector3 look = floor.point + Vector3.up * 0.25f;
                for (int side = 0; side < 8; side++)
                {
                    Vector3 away = Quaternion.Euler(0f, side * 45f, 0f) * -dir;
                    Vector3 near = floor.point + away * 1f + Vector3.up * 0.45f;
                    Vector3 far = floor.point + away * 3f + Vector3.up * 0.9f;
                    if (Physics.CheckSphere(near, 0.15f, mask, QueryTriggerInteraction.Ignore) || Physics.CheckSphere(far, 0.3f, mask, QueryTriggerInteraction.Ignore)) { camBlocked++; continue; }
                    if (Physics.Linecast(near, look, mask, QueryTriggerInteraction.Ignore) || Physics.Linecast(far, look, mask, QueryTriggerInteraction.Ignore)) { camNoView++; continue; }
                    spot = floor.point;
                    toCamera = away;
                    return true;
                }
            }
            LastRejects = $"{noFloor} without floor, {steep} too steep, {crowded} too crowded, {camBlocked} camera inside geometry, {camNoView} camera without a line";
            spot = default;
            toCamera = default;
            return false;
        }

        private static string LastRejects = "";

        /// <summary>A floor point in one of the loaded rooms of the mine, closest to the player first, from which <see cref="ChooseSpot"/> succeeds (a grid of 2 m over each room).</summary>
        private static IEnumerator FindRoomSpot(ScenarioContext ctx, Player player, Action<bool, Vector3> done)
        {
            DungeonGenerator generator = DevWorld.NearestGenerator(player.transform.position);
            Room[] rooms = generator != null ? generator.GetComponentsInChildren<Room>() : new Room[0];
            Vector3 from = player.transform.position;
            Array.Sort(rooms, (a, b) => (a.transform.position - from).sqrMagnitude.CompareTo((b.transform.position - from).sqrMagnitude));
            int mask = LayerMask.GetMask("terrain", "static_solid", "Default", "Default_small", "piece");
            int tested = 0;
            foreach (Room room in rooms)
            {
                Vector3 size = new Vector3(room.m_size.x, room.m_size.y, room.m_size.z);
                Transform t = room.transform;
                for (float x = -size.x * 0.5f; x <= size.x * 0.5f; x += 2f)
                {
                    for (float z = -size.z * 0.5f; z <= size.z * 0.5f; z += 2f)
                    {
                        Vector3 top = t.TransformPoint(new Vector3(x, size.y * 0.5f, z));
                        if (!Physics.Raycast(top, Vector3.down, out RaycastHit floor, size.y + 4f, mask, QueryTriggerInteraction.Ignore) || floor.normal.y < 0.9f) continue;
                        tested++;
                        if (!ChooseSpot(floor.point + Vector3.up * 0.1f, t.forward, out _, out _)) continue;
                        ctx.Log($"free spot found in room {room.name} after {tested} floor points");
                        done(true, floor.point);
                        yield break;
                    }
                }
                yield return null;
            }
            ctx.Log($"no free spot in {rooms.Length} rooms ({tested} floor points); last rejects: {LastRejects}");
            done(false, default);
        }

        private static int ItemsNear(Vector3 where)
        {
            int n = 0;
            foreach (ItemDrop item in UnityEngine.Object.FindObjectsByType<ItemDrop>(FindObjectsSortMode.None))
                if (item != null && (item.transform.position - where).sqrMagnitude < 9f) n++;
            return n;
        }
    }
}
#endif
