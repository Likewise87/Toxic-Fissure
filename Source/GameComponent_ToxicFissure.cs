using RimWorld;
using Verse;
using UnityEngine;

namespace TSA.ToxicQuest
{
    public class GameComponent_ToxicFissure : GameComponent
    {
        public int targetTick = -1;
        public int occurrenceCount = 0;

        public GameComponent_ToxicFissure(Game game) : base() { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref targetTick, "targetTick", -1);
            Scribe_Values.Look(ref occurrenceCount, "occurrenceCount", 0);
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 250 != 0) return;
            if (Find.AnyPlayerHomeMap == null) return;
            if (ToxicQuestSettings.Current == null) return;

            if (targetTick == -1)
            {
                float min, max;
                var s = ToxicQuestSettings.Current;

                if (occurrenceCount == 0)
                {
                    min = s.firstFissureDays.min;
                    max = s.firstFissureDays.max;
                }
                else
                {
                    float reduction = occurrenceCount * s.reductionPerOccurrence;
                    float floor = s.minSubsequentFloor;
                    min = Mathf.Max(floor, s.subsequentDays.min - reduction);
                    max = Mathf.Max(min, s.subsequentDays.max - reduction);
                }

                targetTick = Find.TickManager.TicksGame + Mathf.RoundToInt(Rand.Range(min, max) * 60000f);
            }

            if (Find.TickManager.TicksGame >= targetTick)
            {
                if (ToxicQuestHelper.GenerateQuest())
                {
                    occurrenceCount++;
                }
                // Always reschedule; failed searches retry on the next window without counting.
                targetTick = -1;
            }
        }
    }
}
