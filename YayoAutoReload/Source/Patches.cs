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

    // safety net for every pawn that never runs the drafted reload ticks, which is most of them.
    // the interval is spread out over the id so a raid does not all reload on the same frame
    [HarmonyPatch(typeof(Pawn), "Tick")] // protected override, matched by name at runtime
    internal static class Patch_Pawn_Tick
    {
        private const int MinimumIntervalTicks = 120;
        private const uint IntervalVariationTicks = 61;

        [HarmonyPostfix]
        private static void Postfix(Pawn __instance)
        {
            if (!yayoCombat.YayoCombatCore.ammo
                || (__instance.Faction == Faction.OfPlayer && __instance.RaceProps.Humanlike)
                || !__instance.Spawned)
            {
                return;
            }

            int intervalTicks = MinimumIntervalTicks + (int)((uint)__instance.thingIDNumber % IntervalVariationTicks);
            if (!__instance.IsHashIntervalTick(intervalTicks)
                || !__instance.IsCapableOfReloading()
                || ReloadRetryRegistry.IsReloadJob(__instance.CurJobDef))
            {
                return;
            }

            Thing primary = __instance.equipment.Primary;
            CompApparelReloadable comp = primary?.TryGetComp<CompApparelReloadable>();
            if (comp == null || !AmmoUtility.IsAmmo(comp.AmmoDef, false))
            {
                return;
            }

            bool retryingPartialReload = ReloadRetryRegistry.IsPending(__instance, primary) && comp.RemainingCharges < comp.MaxCharges;
            if (comp.RemainingCharges > 0 && !retryingPartialReload)
            {
                return;
            }

            if (__instance.CountAmmoInInventory(comp) >= comp.MinAmmoNeededChecked())
            {
                if (ReloadUtility.TryReloadFromInventory(__instance, new[] { primary }, false))
                {
                    ReloadRetryRegistry.Clear(__instance, primary);
                }
            }
            else
            {
                VirtualRefillUtility.TryStart(comp);
            }
        }
    }
    // the job tracker is the only place that knows a reload ended badly, so this is where half
    // finished reloads get remembered
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob))]
    internal static class Patch_Pawn_JobTracker_EndCurrentJob
    {
        [HarmonyPrefix]
        private static void Prefix(Pawn ___pawn, Job ___curJob, JobCondition __0)
        {
            ReloadRetryRegistry.ObserveJobEnd(___pawn, ___curJob, __0);
        }
    }
    // returning true means the virtual refill took over. everything else falls through to the
    // Addon's own behaviour, which is exactly what we want
    [HarmonyPatch(typeof(ReloadUtility), nameof(ReloadUtility.TryAutoReloadSingle))]
    internal static class Patch_ReloadUtility_TryAutoReloadSingle
    {
        [HarmonyPrefix]
        private static bool Prefix(CompApparelReloadable __0, ref bool __result)
        {
            return !VirtualRefillUtility.TryHandleSingle(__0, ref __result);
        }
    }

    // same deal for the reload-everything call, first qualifying weapon wins
    [HarmonyPatch(typeof(ReloadUtility), nameof(ReloadUtility.TryAutoReloadAll))]
    internal static class Patch_ReloadUtility_TryAutoReloadAll
    {
        [HarmonyPrefix]
        private static bool Prefix(Pawn __0, ref bool __result)
        {
            return !VirtualRefillUtility.TryHandleAll(__0, ref __result);
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