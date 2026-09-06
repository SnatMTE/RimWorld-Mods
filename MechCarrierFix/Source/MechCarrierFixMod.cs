using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MechCarrierFix
{
    [StaticConstructorOnStartup]
    public static class MechCarrierFixMod
    {
        internal const string HarmonyId = "snat.mechcarrierfix";

        static MechCarrierFixMod()
        {
            MethodInfo target = AccessTools.Method(typeof(ThingOwnerUtility), nameof(ThingOwnerUtility.TryGetInnerInteractableThingOwner));
            new Harmony(HarmonyId).Patch(target, postfix: new HarmonyMethod(typeof(MechCarrierFixMod), nameof(Postfix)));
            Log.Message("[Mech Carrier Fix] Harmony patches applied.");
        }

        // vanilla resolves haul destinations by walking the comps in order and taking the first
        // one that holds things. some mods bolt a second IThingHolder onto mechs and it wins the
        // race, so put the carrier back in front for any pawn that has one
        private static void Postfix(Thing thing, ref ThingOwner __result)
        {
            Pawn pawn = thing as Pawn;
            if (pawn == null)
            {
                return;
            }

            CompMechCarrier carrier = pawn.TryGetComp<CompMechCarrier>();
            if (carrier != null)
            {
                __result = carrier.GetDirectlyHeldThings();
            }
        }
    }
}