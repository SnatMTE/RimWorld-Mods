using HarmonyLib;
using Verse;

namespace ArchotechNoDestroy
{
    // this is where vanilla turns a part at zero health into a missing part, and the missing part
    // scrapes the implant off the part on the way in. archotech parts never get one
    [HarmonyPatch(typeof(HediffSet), nameof(HediffSet.AddDirect))]
    internal static class Patch_HediffSet_AddDirect
    {
        private static bool Prefix(HediffSet __instance, Hediff hediff)
        {
            if (!(hediff is Hediff_MissingPart))
            {
                return true;
            }
            return !ArchotechImplantUtility.HasArchotechImplantOn(__instance, hediff.Part);
        }
    }
}
