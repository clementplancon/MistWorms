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
        public static void Follow() => Active = false;

        [HarmonyPatch(typeof(GameCamera), "LateUpdate")]
        private static class GameCameraLateUpdatePatch
        {
            private static void Postfix(GameCamera __instance)
            {
                if (!Active) return;
                try
                {
                    __instance.transform.SetPositionAndRotation(Position, Rotation);
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
