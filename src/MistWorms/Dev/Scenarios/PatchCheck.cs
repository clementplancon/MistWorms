#if MW_DEV
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace MistWorms.Dev.Scenarios
{
    /// <summary>The application test of a Harmony patch (spec §10.3): the method carries a prefix or a postfix owned by this mod.</summary>
    internal static class PatchCheck
    {
        public static bool Check(ScenarioContext ctx, string name, MethodBase method, bool postfix)
        {
            HarmonyLib.Patches info = method != null ? Harmony.GetPatchInfo(method) : null;
            bool ours = info != null && (postfix ? info.Postfixes : info.Prefixes).Any(p => p.owner == Plugin.ModGuid);
            return ctx.Assert(name, ours, $"a {(postfix ? "postfix" : "prefix")} by {Plugin.ModGuid}", ours ? "patched" : method == null ? "method not found" : "not patched");
        }

        /// <summary>The six patches of the captured loot (spec §10.3).</summary>
        public static void CheckDropPatches(ScenarioContext ctx)
        {
            Check(ctx, "patch_generate_drop_list", AccessTools.Method(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList)), postfix: true);
            Check(ctx, "patch_wearntear_destroy_prefix", AccessTools.Method(typeof(WearNTear), "Destroy", new[] { typeof(HitData), typeof(bool) }), postfix: false);
            Check(ctx, "patch_destructible_destroy_prefix", AccessTools.Method(typeof(Destructible), nameof(Destructible.Destroy)), postfix: false);
            Check(ctx, "patch_piece_drop_resources", AccessTools.Method(typeof(Piece), nameof(Piece.DropResources)), postfix: false);
            Check(ctx, "patch_drop_on_destroyed", AccessTools.Method(typeof(DropOnDestroyed), "OnDestroyed"), postfix: false);
            Check(ctx, "patch_tameable_on_death", AccessTools.Method(typeof(Tameable), "OnDeath"), postfix: false);
        }
    }
}
#endif
