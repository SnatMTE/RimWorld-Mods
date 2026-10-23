using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using YayosCombatAddon;

namespace YayoAutoReload
{
    // the refill job is built in code rather than in XML, so this keeps working when the Addon
    // changes or drops its own defs
    internal static class VirtualRefillJobDefOf
    {
        internal const string DefName = "YayoAutoReload_VirtualRefill";
        internal static JobDef VirtualRefill;

        // prefer a def from XML and fall back to building one here. the contains check is there
        // because adding the same def twice is not as harmless as it looks
        internal static void Initialize()
        {
            VirtualRefill = DefDatabase<JobDef>.GetNamedSilentFail(DefName);
            if (VirtualRefill == null)
            {
                VirtualRefill = new JobDef
                {
                    defName = DefName,
                    driverClass = typeof(JobDriver_VirtualRefill),
                    reportString = "Reloading",
                    playerInterruptible = true,
                    suspendable = false,
                    allowOpportunisticPrefix = false,
                };
            }

            if (!DefDatabase<JobDef>.AllDefsListForReading.Contains(VirtualRefill))
            {
                DefDatabase<JobDef>.Add(VirtualRefill);
            }
        }
    }

    // waits out the normal reload time, then fills the weapon. ammo in the inventory goes first
    // and the rest is conjured by refillMechAmmo, which is the entire point of the mod
    internal class JobDriver_VirtualRefill : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Downed);
            this.FailOnIncapable(PawnCapacityDefOf.Manipulation);

            CompApparelReloadable comp = TargetThingA?.TryGetComp<CompApparelReloadable>();
            yield return Toils_General.Wait(comp == null ? 1 : Math.Max(1, comp.BaseReloadTicks))
                .WithProgressBarToilDelay(TargetIndex.A);

            yield return new Toil
            {
                initAction = () => VirtualRefillUtility.Complete(pawn, TargetThingA),
            };
        }
    }

    internal static class VirtualRefillUtility
    {
        // true means the refill took over, so the original Addon logic never runs
        internal static bool TryHandleSingle(CompApparelReloadable comp, ref bool result)
        {
            if (!IsVirtualRefillCase(comp, false))
            {
                return false;
            }

            result = StartJob(comp);
            return true;
        }

        // same for the reload-everything call: the first weapon that qualifies takes it, the rest
        // are left to the Addon
        internal static bool TryHandleAll(Pawn pawn, ref bool result)
        {
            if (pawn == null)
            {
                return false;
            }

            foreach (Thing thing in pawn.GetAllReloadableThings())
            {
                CompApparelReloadable comp = thing.TryGetComp<CompApparelReloadable>();
                if (IsVirtualRefillCase(comp, true))
                {
                    result = StartJob(comp);
                    return true;
                }
            }
            return false;
        }

        internal static bool TryStart(CompApparelReloadable comp)
        {
            if (!IsVirtualRefillCase(comp, false))
            {
                return false;
            }

            return StartJob(comp);
        }

        // one job at a time. starting a refill on top of a running refill just loses the first one
        private static bool StartJob(CompApparelReloadable comp)
        {
            Pawn pawn = comp.Wearer;
            if (pawn == null || IsVirtualReloadJob(pawn.CurJobDef))
            {
                return false;
            }

            ReloadRetryRegistry.Clear(pawn, comp.parent);
            Job job = JobMaker.MakeJob(VirtualRefillJobDefOf.VirtualRefill, comp.parent);
            pawn.jobs.StartJob(job, JobCondition.InterruptForced, resumeCurJobAfterwards: true, canReturnCurJobToPool: true);
            return true;
        }

        // remainingCharges is private in the Addon's comp, so there is no polite way to top it up
        internal static void Complete(Pawn pawn, Thing weapon)
        {
            CompApparelReloadable comp = weapon?.TryGetComp<CompApparelReloadable>();
            if (pawn == null || !IsVirtualRefillPawn(comp) || comp.RemainingCharges >= comp.MaxCharges)
            {
                return;
            }

            ConsumeAvailableAmmo(pawn, comp);
            Traverse.Create(comp).Field("remainingCharges").SetValue(comp.MaxCharges);
            ReloadRetryRegistry.Clear(pawn, weapon);
        }

        // eats the pawn's own ammo first, otherwise a mech gets free rounds on top of the box it
        // was already carrying
        private static void ConsumeAvailableAmmo(Pawn pawn, CompApparelReloadable comp)
        {
            int remaining = comp.MaxAmmoNeeded(false);
            if (remaining <= 0)
            {
                return;
            }

            foreach (Thing ammo in pawn.inventory.innerContainer.ToList())
            {
                if (remaining <= 0)
                {
                    break;
                }
                if (ammo.def != comp.AmmoDef)
                {
                    continue;
                }

                int count = Math.Min(remaining, ammo.stackCount);
                if (count == ammo.stackCount)
                {
                    ammo.Destroy();
                }
                else
                {
                    ammo.stackCount -= count;
                }
                remaining -= count;
            }
        }

        // the whole condition in one place: the pawn qualifies, the weapon is short, and there is
        // not enough ammo in the inventory to do it the normal way
        private static bool IsVirtualRefillCase(CompApparelReloadable comp, bool allowLowAmmo)
        {
            Pawn pawn = comp?.Wearer;
            return IsVirtualRefillPawn(comp)
                && comp.RemainingCharges < comp.MaxCharges
                && (allowLowAmmo
                    || comp.RemainingCharges <= 0
                    || ReloadRetryRegistry.IsPending(pawn, comp.parent))
                && pawn.CountAmmoInInventory(comp) < comp.MinAmmoNeededChecked();
        }

        // humans keep the Addon's normal reload. this is only for non-humanoids, and only while
        // the player has left refillMechAmmo switched on
        private static bool IsVirtualRefillPawn(CompApparelReloadable comp)
        {
            Pawn pawn = comp?.Wearer;
            return pawn != null
                && !pawn.RaceProps.Humanlike
                && yayoCombat.YayoCombatCore.refillMechAmmo
                && AmmoUtility.IsAmmo(comp.AmmoDef)
                && pawn.IsCapableOfReloading();
        }

        internal static bool IsVirtualReloadJob(JobDef jobDef)
        {
            return jobDef != null && jobDef == VirtualRefillJobDefOf.VirtualRefill;
        }
    }
}
