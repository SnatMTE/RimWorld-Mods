using HarmonyLib;
using Verse;

namespace AdvancedMechUpgrades
{
    [StaticConstructorOnStartup]
    internal static class AdvancedMechUpgradesMod
    {
        internal const string LogTag = "[AdvancedMechUpgrades]";

        static AdvancedMechUpgradesMod()
        {
            new Harmony("snat.advancedmechupgrades").PatchAll();
            Log.Message(LogTag + " Harmony patches applied.");
        }
    }
}