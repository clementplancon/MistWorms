#if MW_DEV
using HarmonyLib;
using UnityEngine;

namespace MistWorms.Dev.Preview
{
    /// <summary>
    /// Scripted camera for visual scenarios. While active, the pose is forced right after GameCamera.LateUpdate,
    /// so vanilla still runs its whole update (FOV, listener, shake) and we only overwrite the transform.
    /// </summary>
    internal static class DevCamera
    {
        public delegate void PoseProvider(out Vector3 position, out Vector3 lookAt);

        /// <summary>When set, the pose is asked of it every frame (a camera that follows something) instead of the fixed one.</summary>
        public static PoseProvider Provider;

        public static bool Active { get; private set; }
        public static Vector3 Position { get; private set; }
        public static Quaternion Rotation { get; private set; }

        /// <summary>Looks from <paramref name="pos"/> at <paramref name="lookAt"/> and keeps that pose until <see cref="Follow"/>.</summary>
        public static void Free(Vector3 pos, Vector3 lookAt)
        {
            Vector3 dir = lookAt - pos;
            Position = pos;
            Rotation = dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dir, Vector3.up) : Rotation;
            Active = true;
        }

        /// <summary>Freezes the camera at its current pose.</summary>
        public static void FreezeHere()
        {
            Camera cam = Utils.GetMainCamera();
            if (cam == null) return;
            Position = cam.transform.position;
            Rotation = cam.transform.rotation;
            Active = true;
        }

        /// <summary>Gives the camera back to vanilla.</summary>
        public static void Follow() { Active = false; Provider = null; SetFillLight(false); }

        private static GameObject s_fill;

        /// <summary>
        /// Dev fill light for the clips of the masses: a warm point light (range 6 m, moderate intensity, no shadows) that rides on the camera, so the pale
        /// glossy worms read in the dark of a mine. Off with <see cref="Follow"/>.
        /// </summary>
        public static void SetFillLight(bool on)
        {
            if (on)
            {
                if (s_fill == null)
                {
                    s_fill = new GameObject("MW_ClipFill") { hideFlags = HideFlags.HideAndDontSave };
                    Light l = s_fill.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(1f, 0.82f, 0.55f);
                    l.range = 6f;
                    l.intensity = 1.6f;
                    l.shadows = LightShadows.None;
                }
                s_fill.SetActive(true);
            }
            else if (s_fill != null) s_fill.SetActive(false);
        }

        [HarmonyPatch(typeof(GameCamera), "LateUpdate")]
        private static class GameCameraLateUpdatePatch
        {
            private static void Postfix(GameCamera __instance)
            {
                if (!Active) return;
                try
                {
                    if (Provider != null)
                    {
                        Provider(out Vector3 p, out Vector3 look);
                        Vector3 dir = look - p;
                        Position = p;
                        if (dir.sqrMagnitude > 1e-6f) Rotation = Quaternion.LookRotation(dir, Vector3.up);
                    }
                    __instance.transform.SetPositionAndRotation(Position, Rotation);
                    if (s_fill != null && s_fill.activeSelf) s_fill.transform.position = Position + Vector3.up * 0.3f;
                }
                catch (System.Exception e)
                {
                    Plugin.LogException("DevCamera", e);
                    Active = false;
                }
            }
        }
    }
}
#endif
