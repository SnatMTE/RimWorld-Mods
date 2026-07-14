using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.Sound;

namespace AdvancedMechUpgrades
{
    internal static class MechUpgradeUtility
    {
        private static readonly HediffDef SelfUpgradeHediff = DefDatabase<HediffDef>.GetNamed("AMU_SelfUpgrading");

        // both upgrade paths live in the gizmo bar. anything wrong with them comes back disabled
        // with every complaint stacked into the tooltip, because hiding the reason would be cruel
        public static IEnumerable<Gizmo> GetUpgradeGizmos(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || !pawn.RaceProps.IsMechanoid || !pawn.IsColonyMech
                || pawn.IsGestating() || pawn.health.hediffSet.HasHediff(SelfUpgradeHediff))
            {
                yield break;
            }

            MechUpgradeOption option = MechUpgradeOptions.Get(pawn.def);
            if (option == null || !option.recipe.AvailableNow)
            {
                yield break;
            }

            List<MechUpgradeCost> costs = MechUpgradeOptions.BuildCosts(option.recipe);
            List<string> problems = new List<string>();
            List<string> missing = GetMissingResources(pawn.MapHeld, costs);
            if (missing.Count > 0)
            {
                problems.Add("AMU.MissingResources".Translate(string.Join(", ", missing)));
            }
            string bandwidthIssue = GetBandwidthIssue(pawn, option);
            if (!bandwidthIssue.NullOrEmpty())
            {
                problems.Add(bandwidthIssue);
            }

            Command_Action command = new Command_Action
            {
                defaultLabel = "AMU.GizmoLabel".Translate(option.advancedDef.label),
                defaultDesc = BuildDescription(option, costs),
                icon = option.icon,
                action = () => Upgrade(pawn, option)
            };
            if (problems.Count > 0)
            {
                command.Disabled = true;
                command.disabledReason = string.Join("\n", problems);
            }
            yield return command;

            Command_Action selfUpgrade = new Command_Action
            {
                defaultLabel = "AMU.SelfUpgradeLabel".Translate(option.advancedDef.label),
                defaultDesc = BuildSelfUpgradeDescription(option),
                icon = option.icon,
                action = () => StartSelfUpgrade(pawn, option)
            };
            if (!bandwidthIssue.NullOrEmpty())
            {
                selfUpgrade.Disabled = true;
                selfUpgrade.disabledReason = bandwidthIssue;
            }
            yield return selfUpgrade;
        }

        // a bigger mech can push the mechanitor over budget, and finding that out after the
        // resources are already gone would be a bit rude
        private static string GetBandwidthIssue(Pawn mech, MechUpgradeOption option)
        {
            Pawn overseer = mech.GetOverseer();
            if (overseer == null || overseer.mechanitor == null)
            {
                return null;
            }

            int oldCost = (int)mech.GetStatValue(StatDefOf.BandwidthCost);
            int newCost = (int)option.advancedDef.GetStatValueAbstract(StatDefOf.BandwidthCost);
            Pawn_MechanitorTracker mechanitor = overseer.mechanitor;
            if (mechanitor.UsedBandwidthFromSubjects - oldCost + newCost <= mechanitor.TotalBandwidth)
            {
                return null;
            }
            return "AMU.NotEnoughBandwidth".Translate();
        }

        // one line per ingredient, plus a dash for the recipes that cost nothing
        private static string BuildDescription(MechUpgradeOption option, List<MechUpgradeCost> costs)
        {
            string costLines = "-";
            if (costs.Count > 0)
            {
                costLines = string.Join("\n", costs.Select(cost => "AMU.CostLine".Translate(cost.count, cost.Label)));
            }
            return "AMU.GizmoDesc".Translate(option.baseDef.label, option.advancedDef.label, costLines);
        }

        // the free path, which is paid for in downtime instead of steel
        private static string BuildSelfUpgradeDescription(MechUpgradeOption option)
        {
            string downtime = MechUpgradeOptions.SelfUpgradeTicks(option.recipe).ToStringTicksToPeriod();
            return "AMU.SelfUpgradeDesc".Translate(option.baseDef.label, option.advancedDef.label, downtime);
        }

        // counts what is sitting on the map and not forbidden to the player, then reports one
        // shortfall per ingredient
        private static List<string> GetMissingResources(Map map, List<MechUpgradeCost> costs)
        {
            List<string> missing = new List<string>();
            foreach (MechUpgradeCost cost in costs)
            {
                int available = 0;
                foreach (ThingDef def in cost.filter.AllowedThingDefs)
                {
                    foreach (Thing thing in map.listerThings.ThingsOfDef(def))
                    {
                        if (!thing.IsForbidden(Faction.OfPlayer))
                        {
                            available += thing.stackCount;
                        }
                    }
                }
                if (available < cost.count)
                {
                    missing.Add("AMU.MissingEntry".Translate(cost.count - available, cost.Label));
                }
            }
            return missing;
        }

        // takes from the nearest stacks first, so an upgrade does not strip the far corner of the
        // map for no good reason
        private static void Consume(Map map, IntVec3 near, List<MechUpgradeCost> costs)
        {
            foreach (MechUpgradeCost cost in costs)
            {
                List<Thing> things = new List<Thing>();
                foreach (ThingDef def in cost.filter.AllowedThingDefs)
                {
                    foreach (Thing thing in map.listerThings.ThingsOfDef(def))
                    {
                        if (!thing.IsForbidden(Faction.OfPlayer))
                        {
                            things.Add(thing);
                        }
                    }
                }
                things.Sort((a, b) => a.PositionHeld.DistanceToSquared(near).CompareTo(b.PositionHeld.DistanceToSquared(near)));

                int remaining = cost.count;
                for (int i = 0; i < things.Count && remaining > 0; i++)
                {
                    Thing thing = things[i];
                    int take = remaining < thing.stackCount ? remaining : thing.stackCount;
                    remaining -= take;
                    if (take >= thing.stackCount)
                    {
                        thing.Destroy();
                    }
                    else
                    {
                        thing.SplitOff(take).Destroy();
                    }
                }

                if (remaining > 0)
                {
                    Log.Error(AdvancedMechUpgradesMod.LogTag + " failed to consume " + remaining + "x " + cost.Label + " for a mech upgrade.");
                }
            }
        }

        // checks the cost again, because the gizmo may have been built a while ago, then pays up
        // and rebuilds
        private static void Upgrade(Pawn mech, MechUpgradeOption option)
        {
            Map map = mech.MapHeld;
            if (map == null)
            {
                return;
            }

            List<MechUpgradeCost> costs = MechUpgradeOptions.BuildCosts(option.recipe);
            List<string> missing = GetMissingResources(map, costs);
            if (missing.Count > 0)
            {
                Messages.Message("AMU.MissingResources".Translate(string.Join(", ", missing)), mech, MessageTypeDefOf.RejectInput);
                return;
            }
            string bandwidthIssue = GetBandwidthIssue(mech, option);
            if (!bandwidthIssue.NullOrEmpty())
            {
                Messages.Message(bandwidthIssue, mech, MessageTypeDefOf.RejectInput);
                return;
            }

            Consume(map, mech.PositionHeld, costs);
            Replace(mech, option);
        }

        // shuts the mech down for a while and lets it rebuild itself, with no bill attached
        private static void StartSelfUpgrade(Pawn mech, MechUpgradeOption option)
        {
            if (!mech.Spawned || mech.health.hediffSet.HasHediff(SelfUpgradeHediff))
            {
                return;
            }

            string bandwidthIssue = GetBandwidthIssue(mech, option);
            if (!bandwidthIssue.NullOrEmpty())
            {
                Messages.Message(bandwidthIssue, mech, MessageTypeDefOf.RejectInput);
                return;
            }

            Hediff_SelfUpgrade hediff = (Hediff_SelfUpgrade)HediffMaker.MakeHediff(SelfUpgradeHediff, mech);
            hediff.ticksLeft = MechUpgradeOptions.SelfUpgradeTicks(option.recipe);
            mech.health.AddHediff(hediff);
            SoundDefOf.MechSelfShutdown.PlayOneShot(new TargetInfo(mech.PositionHeld, mech.MapHeld));
        }

        // the shutdown hediff hands the finished build over here a tick later, which keeps the
        // swap out of the pawn's own health tick
        internal static void CompleteSelfUpgrade(Pawn mech)
        {
            if (!mech.Spawned || !mech.health.hediffSet.HasHediff(SelfUpgradeHediff))
            {
                return;
            }

            MechUpgradeOption option = MechUpgradeOptions.Get(mech.def);
            if (option == null)
            {
                // the advanced mech vanished with a mod update, so let the poor thing wake up
                mech.health.RemoveHediff(mech.health.hediffSet.GetFirstHediffOfDef(SelfUpgradeHediff));
                return;
            }

            Replace(mech, option);
        }

        // generates the replacement and hands over everything the old mech had: name, charge,
        // control group, drafted state and selection
        private static void Replace(Pawn mech, MechUpgradeOption option)
        {
            Map map = mech.MapHeld;

            // newborn, otherwise the graphic comes out grown
            PawnGenerationRequest request = new PawnGenerationRequest(
                option.advancedKind,
                Faction.OfPlayer,
                PawnGenerationContext.NonPlayer,
                map.Tile);
            request.AllowedDevelopmentalStages = DevelopmentalStage.Newborn;
            Pawn upgraded = PawnGenerator.GeneratePawn(request);

            Pawn overseer = mech.GetOverseer();
            MechanitorControlGroup controlGroup = overseer?.mechanitor?.GetControlGroup(mech);
            string controlGroupTag = controlGroup?.GetTag(mech);
            Need_MechEnergy oldEnergy = mech.needs?.TryGetNeed<Need_MechEnergy>();
            bool wasDrafted = mech.Drafted;
            bool wasSelected = Find.Selector.IsSelected(mech);
            IntVec3 position = mech.PositionHeld;
            Rot4 rotation = mech.Rotation;
            Name name = mech.Name;

            if (mech.carryTracker.CarriedThing != null)
            {
                Thing dropped;
                mech.carryTracker.TryDropCarriedThing(position, ThingPlaceMode.Near, out dropped);
            }
            if (overseer != null)
            {
                overseer.relations.TryRemoveDirectRelation(PawnRelationDefOf.Overseer, mech);
            }
            mech.Destroy(DestroyMode.Vanish);

            upgraded.Name = name;
            GenSpawn.Spawn(upgraded, position, map, rotation);

            if (oldEnergy != null)
            {
                Need_MechEnergy newEnergy = upgraded.needs?.TryGetNeed<Need_MechEnergy>();
                if (newEnergy != null)
                {
                    newEnergy.CurLevelPercentage = oldEnergy.CurLevelPercentage;
                }
            }

            if (overseer != null)
            {
                overseer.relations.AddDirectRelation(PawnRelationDefOf.Overseer, upgraded);
                if (controlGroup != null)
                {
                    controlGroup.Assign(upgraded);
                    if (!controlGroupTag.NullOrEmpty())
                    {
                        controlGroup.SetTag(upgraded, controlGroupTag);
                    }
                }
            }

            if (wasDrafted && upgraded.drafter != null)
            {
                upgraded.drafter.Drafted = true;
            }

            if (wasSelected)
            {
                Find.Selector.ClearSelection();
                Find.Selector.Select(upgraded);
            }

            SoundDefOf.MechGestatorBill_Completed.PlayOneShot(new TargetInfo(position, map));
            Messages.Message(
                "AMU.UpgradeComplete".Translate(upgraded.LabelShortCap, option.advancedDef.LabelCap),
                upgraded,
                MessageTypeDefOf.PositiveEvent);
        }
    }
}
