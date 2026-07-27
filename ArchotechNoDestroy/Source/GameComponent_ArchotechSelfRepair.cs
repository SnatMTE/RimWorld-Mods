using System;
using System.Collections.Generic;
using Verse;

namespace ArchotechNoDestroy
{
    public class GameComponent_ArchotechSelfRepair : GameComponent
    {
        private const int RepairIntervalTicks = 300;
        private const float TicksPerDay = 60000f;
        private const float RepairFractionPerDay = 1f;

        // GameComponent will not touch a subclass without this ctor, which is a shame because
        // there is nothing useful to put in it
        public GameComponent_ArchotechSelfRepair(Game game)
        {
        }

        // called on every tick, but the modulo keeps the actual healing down to once every five
        // seconds
        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % RepairIntervalTicks != 0)
            {
                return;
            }

            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                IReadOnlyList<Pawn> pawns = maps[i].mapPawns.AllPawnsSpawned;
                for (int j = 0; j < pawns.Count; j++)
                {
                    RepairPawn(pawns[j]);
                }
            }
        }

        // heals the archotech parts of one pawn. the parts get collected first because healing
        // an injury removes it from the very list we would still be walking
        private static void RepairPawn(Pawn pawn)
        {
            HediffSet hediffSet = pawn.health.hediffSet;

            List<BodyPartRecord> archotechParts = null;
            List<Hediff> hediffs = hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff is Hediff_Implant && hediff.Part != null && ArchotechImplantUtility.IsArchotech(hediff.def))
                {
                    if (archotechParts == null)
                    {
                        archotechParts = new List<BodyPartRecord>();
                    }
                    if (!archotechParts.Contains(hediff.Part))
                    {
                        archotechParts.Add(hediff.Part);
                    }
                }
            }
            if (archotechParts == null)
            {
                return;
            }

            for (int i = 0; i < archotechParts.Count; i++)
            {
                BodyPartRecord part = archotechParts[i];
                float healAmount = part.def.GetMaxHealth(pawn) * RepairFractionPerDay * RepairIntervalTicks / TicksPerDay;
                HealInjuriesOn(pawn, hediffSet, part, healAmount);
            }
        }

        // spends up to healAmount across the injuries on one part, walking the list backwards
        // because RemoveHediff pulls entries out from under us
        private static void HealInjuriesOn(Pawn pawn, HediffSet hediffSet, BodyPartRecord part, float healAmount)
        {
            List<Hediff> hediffs = hediffSet.hediffs;
            float remaining = healAmount;
            for (int i = hediffs.Count - 1; i >= 0 && remaining > 0f; i--)
            {
                Hediff_Injury injury = hediffs[i] as Hediff_Injury;
                if (injury == null || injury.Part != part)
                {
                    continue;
                }
                float heal = Math.Min(remaining, injury.Severity);
                injury.Heal(heal);
                remaining -= heal;
                if (injury.Severity <= 0f)
                {
                    pawn.health.RemoveHediff(injury);
                }
            }
        }
    }
}