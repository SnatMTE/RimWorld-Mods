using HarmonyLib;
using Verse;

namespace ArchotechNoDestroy
{
    // vanilla throws away any hediff reporting severity zero, bionics included, and some mods park
    // an implant's hit points in that severity, so a damaged archotech used to delete itself
    [HarmonyPatch(typeof(Hediff), nameof(Hediff.ShouldRemove), MethodType.Getter)]
    internal static class Patch_Hediff_ShouldRemove
    {
        private static void Postfix(Hediff __instance, ref bool __result)
        {
            if (!__result)
            {
                return;
            }
            if (__instance is Hediff_Implant && ArchotechImplantUtility.IsArchotech(__instance.def))
            {
                __result = false;
            }
        }
    }
}
