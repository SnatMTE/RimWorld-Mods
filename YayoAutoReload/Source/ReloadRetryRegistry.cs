using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using YayosCombatAddon;

namespace YayoAutoReload
{
    // remembers reloads that stopped halfway, keyed by pawn because the weapon on its own does
    // not say who was holding it
    internal static class ReloadRetryRegistry
    {
        private static readonly Dictionary<Pawn, Thing> Pending = new Dictionary<Pawn, Thing>();

        // a plain dictionary write: only the last weapon a pawn failed to reload matters
        internal static void Mark(Pawn pawn, Thing weapon)
        {
            Pending[pawn] = weapon;
        }

        // true only for the same weapon, because a pawn that swapped guns should not inherit the
        // retry from the old one
        internal static bool IsPending(Pawn pawn, Thing weapon)
        {
            Thing pendingWeapon;
            return Pending.TryGetValue(pawn, out pendingWeapon) && pendingWeapon == weapon;
        }

        internal static void Clear(Pawn pawn, Thing weapon)
        {
            Thing pendingWeapon;
            if (Pending.TryGetValue(pawn, out pendingWeapon) && pendingWeapon == weapon)
            {
                Pending.Remove(pawn);
            }
        }

        // matched by defName because the Addon and vanilla each have their own idea of what a
        // reload job is called, and new ones turn up between versions
        internal static bool IsReloadJob(JobDef jobDef)
        {
            if (jobDef == null)
            {
                return false;
            }

            string defName = jobDef.defName;
            return defName == "YCA_ReloadFromInventory"
                || defName == "YCA_ReloadFromSurrounding"
                || defName == "Reload";
        }
    }
}