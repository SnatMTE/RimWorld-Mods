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

        // called for every job that ends in the game, so bail out fast on anything that is not a
        // reload. a reload that finished properly clears the entry, anything else marks it
        internal static void ObserveJobEnd(Pawn pawn, Job job, JobCondition condition)
        {
            if (job == null || !IsReloadJob(job.def))
            {
                return;
            }

            Thing weapon = job.targetA.Thing;
            CompApparelReloadable comp = weapon?.TryGetComp<CompApparelReloadable>();
            if (comp == null)
            {
                return;
            }

            if (comp.RemainingCharges >= comp.MaxCharges)
            {
                Clear(pawn, weapon);
            }
            else if (condition != JobCondition.Succeeded)
            {
                Mark(pawn, weapon);
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
                || defName == "Reload"
                || VirtualRefillUtility.IsVirtualReloadJob(jobDef);
        }
    }
}
