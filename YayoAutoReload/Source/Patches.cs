using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using YayosCombatAddon;

namespace YayoAutoReload
{
    [StaticConstructorOnStartup]
    internal static class PatchBootstrap
    {
        public const string HarmonyId = "local.yayoautoreload";
        public const string LogTag = "[YayoAutoReload]";

        static PatchBootstrap()
        {
            new Harmony(HarmonyId).PatchAll(typeof(PatchBootstrap).Assembly);
            Log.Message(LogTag + " Harmony patches applied.");
        }
    }

    // the Addon had this on UsedOnce in 1.6.10 and quietly dropped it in 1.6.11, so it is back.
    // if the reload cannot happen, a pawn out hunting has to stop and deal with the empty gun
    [HarmonyPatch(typeof(CompApparelReloadable), nameof(CompApparelReloadable.UsedOnce))]
    internal static class Patch_CompApparelReloadable_UsedOnce
    {
        [HarmonyPostfix]
        private static void Postfix(CompApparelReloadable __instance)
        {
            if (!yayoCombat.YayoCombatCore.ammo || __instance.Wearer == null)
            {
                return;
            }
            if (__instance.ReloadableThing.def.equipmentType != EquipmentType.Primary)
            {
                return;
            }
            if (!AmmoUtility.IsAmmo(__instance.AmmoDef))
            {
                return;
            }

            Pawn pawn = __instance.Wearer;
            bool drafted = pawn.Drafted;
            if (!ReloadUtility.TryAutoReloadSingle(__instance, showOutOfAmmoWarning: true, ignoreDistance: !drafted, returnToStartingPosition: drafted)
                && pawn.CurJobDef == JobDefOf.Hunt)
            {
                pawn.jobs.StopAll();
            }
        }
    }
}