using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MechsNeverDie
{
    [StaticConstructorOnStartup]
    internal static class MechsNeverDieHarmony
    {
        // MakeDowned is private, so reflection is the only way in
        private static readonly MethodInfo MakeDownedMethod = AccessTools.Method(typeof(Pawn_HealthTracker), "MakeDowned");

        // no point registering patches that cannot work, so if the lookup failed, say so and stop
        static MechsNeverDieHarmony()
        {
            if (MakeDownedMethod == null)
            {
                Log.Error("[Mechs Never Die] Pawn_HealthTracker.MakeDowned not found, mechs will die as normal.");
                return;
            }

            new Harmony("matth.mechsneverdie").PatchAll(Assembly.GetExecutingAssembly());
        }

        // player mechs, colony mechs and anything neutral get the mercy rule. hostile mechs are
        // excluded, otherwise a raid could never actually end
        internal static bool ShouldPreventDeath(Pawn pawn)
        {
            if (pawn.Dead || !pawn.RaceProps.IsMechanoid)
            {
                return false;
            }

            // war urchins run on a non-rechargeable power cell, and vanilla kills them the
            // moment it runs dry. downing one instead just leaves an unrepairable brick on the
            // floor, so these die as intended
            if (pawn.TryGetComp<CompMechPowerCell>() != null)
            {
                return false;
            }

            Faction playerFaction = Faction.OfPlayerSilentFail;
            return playerFaction == null || !pawn.HostileTo(playerFaction);
        }

        // killing is a big deal in RimWorld, so hand it to the vanilla downing path and let it
        // deal with the hediffs, the state and all the bookkeeping nobody wants to copy
        internal static void DownInsteadOfDying(Pawn pawn, DamageInfo? dinfo, Hediff exactCulprit)
        {
            if (pawn.Destroyed || pawn.Downed)
            {
                return;
            }

            MakeDownedMethod.Invoke(pawn.health, new object[] { dinfo, exactCulprit });
        }
    }

    // the health tracker asks this before it pulls the plug, so answer no for our mechs
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.ShouldBeDead))]
    internal static class Patch_PawnHealthTracker_ShouldBeDead
    {
        private static void Postfix(Pawn_HealthTracker __instance, Pawn ___pawn, ref bool __result)
        {
            if (__result && !__instance.Dead && MechsNeverDieHarmony.ShouldPreventDeath(___pawn))
            {
                __result = false;
            }
        }
    }

    // Kill gets called directly by plenty of things that never ask ShouldBeDead first
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    internal static class Patch_Pawn_Kill
    {
        private static bool Prefix(Pawn __instance, DamageInfo? dinfo, Hediff exactCulprit)
        {
            if (!MechsNeverDieHarmony.ShouldPreventDeath(__instance))
            {
                return true;
            }

            MechsNeverDieHarmony.DownInsteadOfDying(__instance, dinfo, exactCulprit);
            return false;
        }
    }

    // DamageUntilDead is what dev commands and a few vanilla events use, and it does not ask
    // nicely first either
    [HarmonyPatch(typeof(HealthUtility), nameof(HealthUtility.DamageUntilDead))]
    internal static class Patch_HealthUtility_DamageUntilDead
    {
        private static bool Prefix(Pawn p, DamageDef damage, ThingDef sourceDef, BodyPartGroupDef bodyGroupDef)
        {
            if (!MechsNeverDieHarmony.ShouldPreventDeath(p))
            {
                return true;
            }

            HealthUtility.DamageUntilDowned(p, allowBleedingWounds: true, damage, sourceDef, bodyGroupDef);
            return false;
        }
    }
}
