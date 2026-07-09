using HarmonyLib;
using Verse;

namespace AdvancedMechUpgrades
{
    [StaticConstructorOnStartup]
    internal static class AdvancedMechUpgradesMod
    {
        internal const string LogTag = "[AdvancedMechUpgrades]";

        // runs once every def is in, which is the only point where other mods' advanced mechs
        // actually exist to be found
        static AdvancedMechUpgradesMod()
        {
            new Harmony("snat.advancedmechupgrades").PatchAll();
            MechUpgradeOptions.Rebuild();
            Log.Message(LogTag + " " + MechUpgradeOptions.Count + " mech upgrade paths found.");
        }
    }
}
