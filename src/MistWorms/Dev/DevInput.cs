#if MW_DEV
using System;
using HarmonyLib;
using UnityEngine;

namespace MistWorms.Dev
{
    /// <summary>
    /// Dev only: drives the local player through the vanilla controls (<c>Player.SetControls</c>) so that speed, sprint, block and roll are those of the
    /// game, status effects included. Inert unless a scenario switches <see cref="Active"/> on: the postfix of <c>PlayerController.FixedUpdate</c> then
    /// replaces the controls of that step (movement relative to the look direction: z forward, x right).
    /// </summary>
    internal static class DevInput
    {
        public static bool Active { get; private set; }
        public static Vector3 Move;
        public static bool Run;
        public static bool Block;
        private static bool s_dodge;

        public static void Set(Vector3 move, bool run = false, bool block = false)
        {
            Active = true;
            Move = move;
            Run = run;
            Block = block;
        }

        /// <summary>Asks for a roll on the next control step.</summary>
        public static void RequestDodge()
        {
            Active = true;
            s_dodge = true;
        }

        public static void Release()
        {
            Active = false;
            Move = Vector3.zero;
            Run = false;
            Block = false;
            s_dodge = false;
        }

        [HarmonyPatch(typeof(PlayerController), "FixedUpdate")]
        private static class ControllerPatch
        {
            private static void Postfix()
            {
                try
                {
                    if (!Active) return;
                    Player player = Player.m_localPlayer;
                    if (player == null) return;
                    bool dodge = s_dodge;
                    s_dodge = false;
                    player.SetControls(Move, attack: false, attackHold: false, secondaryAttack: false, secondaryAttackHold: false,
                        block: Block, blockHold: Block, jump: false, crouch: false, run: Run, autoRun: false, dodge: dodge);
                }
                catch (Exception e)
                {
                    Plugin.LogException("DevInput.FixedUpdate", e);
                }
            }
        }
    }
}
#endif
