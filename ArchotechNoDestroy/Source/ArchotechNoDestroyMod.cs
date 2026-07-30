using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace ArchotechNoDestroy
{
    [StaticConstructorOnStartup]
    internal static class ArchotechNoDestroyMod
    {
        internal const string HarmonyId = "snat.archotech.unbreakable";
        internal const string LogTag = "[Archotech Unbreakable]";

        // Harmony is perfectly happy to fail quietly, so patch here and complain loudly if it
        // does, instead of letting the mod sit in the list doing nothing at all
        static ArchotechNoDestroyMod()
        {
            try
            {
                new Harmony(HarmonyId).PatchAll(Assembly.GetExecutingAssembly());
                Log.Message(LogTag + " Harmony patches applied.");
            }
            catch (Exception ex)
            {
                Log.Error(LogTag + " failed to apply Harmony patches: " + ex);
            }
        }
    }
}
