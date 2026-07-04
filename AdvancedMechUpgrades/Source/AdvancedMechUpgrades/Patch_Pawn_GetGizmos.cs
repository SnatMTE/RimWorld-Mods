using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace AdvancedMechUpgrades
{
    // Pawn.GetGizmos is lazy, so this has to stay an iterator rather than building a list
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    internal static class Patch_Pawn_GetGizmos
    {
        [HarmonyPostfix]
        private static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (Gizmo gizmo in __result)
            {
                yield return gizmo;
            }
            foreach (Gizmo gizmo in MechUpgradeUtility.GetUpgradeGizmos(__instance))
            {
                yield return gizmo;
            }
        }
    }
}
