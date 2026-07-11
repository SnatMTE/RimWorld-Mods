using System.Collections.Generic;
using RimWorld;
using Verse;

namespace AdvancedMechUpgrades
{
    // the mech spends the whole rebuild unconscious, which is as close to switched off as a
    // pawn gets without being dead
    public class Hediff_SelfUpgrade : HediffWithComps
    {
        public int ticksLeft;

        public override string TipStringExtra => "AMU.SelfUpgradeHediffTip".Translate(ticksLeft.ToStringTicksToPeriod());

        // the actual swap happens on the game component, so this only counts down and queues
        public override void Tick()
        {
            base.Tick();
            ticksLeft--;
            if (ticksLeft > 0)
            {
                return;
            }

            Current.Game.GetComponent<GameComponent_SelfUpgrades>().Queue(pawn);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksLeft, "ticksLeft");
        }
    }

    // pawns cannot be destroyed and replaced halfway through their own health tick, so the
    // queue gets flushed once that tick is good and over
    public class GameComponent_SelfUpgrades : GameComponent
    {
        private readonly List<Pawn> queued = new List<Pawn>();

        // the game hands itself to every component it creates, though this one has no use for it
        public GameComponent_SelfUpgrades(Game game)
        {
        }

        public void Queue(Pawn mech)
        {
            if (!queued.Contains(mech))
            {
                queued.Add(mech);
            }
        }

        public override void GameComponentTick()
        {
            for (int i = queued.Count - 1; i >= 0; i--)
            {
                Pawn mech = queued[i];
                queued.RemoveAt(i);
                MechUpgradeUtility.CompleteSelfUpgrade(mech);
            }
        }
    }
}
