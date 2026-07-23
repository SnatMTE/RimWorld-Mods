using HarmonyLib;
using Verse;

namespace ArchotechNoDestroy
{
    // a body part dies at 0 health and every destruction check reads it through GetPartHealth.
    // parts carrying an archotech implant report 1 instead, so they sit at 0% until self-repair
    // walks them back up
    [HarmonyPatch(typeof(HediffSet), nameof(HediffSet.GetPartHealth))]
    internal static class Patch_HediffSet_GetPartHealth
    {
        // bails out on anything that is not already at zero, which is nearly every call in the game
        private static void Postfix(HediffSet __instance, BodyPartRecord part, ref float __result)
        {
            if (__result > 0f || part == null)
            {
                return;
            }
            if (!__instance.PartIsMissing(part) && ArchotechImplantUtility.HasArchotechImplantOn(__instance, part))
            {
                __result = 1f;
            }
        }
    }
}