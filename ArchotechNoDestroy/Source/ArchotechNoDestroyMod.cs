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

        static ArchotechNoDestroyMod()
        {
            new Harmony(HarmonyId).PatchAll(Assembly.GetExecutingAssembly());
            Log.Message(LogTag + " Harmony patches applied.");
        }
    }
}