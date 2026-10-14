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

            // Yayo's reads its settings before this assembly is loaded
            Patch_YayoCombatCore_ApplySettingsFrom.ApplyCurrentSetting();
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

    // there is exactly one 500 in the settings window and it is this clamp. if a future version
    // moves it, the log says so instead of the slider quietly staying capped
    [HarmonyPatch(typeof(yayoCombat.YayoCombatMod), nameof(yayoCombat.YayoCombatMod.DoSettingsWindowContents))]
    internal static class Patch_YayoCombatMod_DoSettingsWindowContents
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool replaced = false;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldc_I4 && instruction.operand is int value && value == 500)
                {
                    instruction.operand = int.MaxValue;
                    replaced = true;
                }
                yield return instruction;
            }

            if (!replaced)
            {
                Log.Warning(PatchBootstrap.LogTag + " could not find the 500% NPC ammo cap in Yayo's settings window, skipped.");
            }
        }
    }

    // Yayo's applies its settings before this assembly is loaded, so the value gets set once here
    // and once again on every later change
    [HarmonyPatch(typeof(yayoCombat.YayoCombatCore), nameof(yayoCombat.YayoCombatCore.ApplySettingsFrom))]
    internal static class Patch_YayoCombatCore_ApplySettingsFrom
    {
        [HarmonyPostfix]
        private static void Postfix(yayoCombat.YayoCombatSettings __0)
        {
            Apply(__0);
        }

        internal static void ApplyCurrentSetting()
        {
            Apply(yayoCombat.YayoCombatMod.Instance?.Settings);
        }

        private static void Apply(yayoCombat.YayoCombatSettings settings)
        {
            if (settings == null)
            {
                return;
            }
            yayoCombat.YayoCombatCore.s_enemyAmmo = UnityEngine.Mathf.Max(0f, settings.enemyAmmo / 100f);
        }
    }
}