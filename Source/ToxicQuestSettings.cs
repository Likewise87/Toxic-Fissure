using UnityEngine;
using Verse;

namespace TSA.ToxicQuest
{
    public class ToxicQuestSettings : ModSettings
    {
        public const float AbsoluteMaxRadius = 15f;

        public FloatRange firstFissureDays = new FloatRange(60f, 90f);
        public FloatRange subsequentDays = new FloatRange(30f, 50f);
        public float reductionPerOccurrence = 2f;
        public float minSubsequentFloor = 2f;
        public float intervalHours = 12f;
        public float rangeIncrease = 1.0f;
        public float pollutionIncrease = 0.1f;
        public float maxPollutionRange = AbsoluteMaxRadius;
        public float insectScaling = 1.2f;
        public float questTimeoutDays = 20f;

        public static ToxicQuestSettings? Current;

        public float EffectiveMaxRadius => Mathf.Clamp(maxPollutionRange, 1f, AbsoluteMaxRadius);

        public void ResetToDefaults()
        {
            firstFissureDays = new FloatRange(60f, 90f);
            subsequentDays = new FloatRange(30f, 50f);
            reductionPerOccurrence = 2f;
            minSubsequentFloor = 2f;
            intervalHours = 12f;
            rangeIncrease = 1.0f;
            pollutionIncrease = 0.1f;
            maxPollutionRange = AbsoluteMaxRadius;
            insectScaling = 1.2f;
            questTimeoutDays = 20f;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref firstFissureDays, "firstFissureDays", new FloatRange(60f, 90f));
            Scribe_Values.Look(ref subsequentDays, "subsequentDays", new FloatRange(30f, 50f));
            Scribe_Values.Look(ref reductionPerOccurrence, "reductionPerOccurrence", 2f);
            Scribe_Values.Look(ref minSubsequentFloor, "minSubsequentFloor", 2f);
            Scribe_Values.Look(ref intervalHours, "intervalHours", 12f);
            Scribe_Values.Look(ref rangeIncrease, "rangeIncrease", 1.0f);
            Scribe_Values.Look(ref pollutionIncrease, "pollutionIncrease", 0.1f);
            Scribe_Values.Look(ref maxPollutionRange, "maxPollutionRange", AbsoluteMaxRadius);
            Scribe_Values.Look(ref insectScaling, "insectScaling", 1.2f);
            Scribe_Values.Look(ref questTimeoutDays, "questTimeoutDays", 20f);
            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                maxPollutionRange = Mathf.Clamp(maxPollutionRange, 1f, AbsoluteMaxRadius);
            }
        }

        public int IntervalTicks => Mathf.Max(1, Mathf.RoundToInt(intervalHours * 2500f));

        public int QuestTimeoutTicks => Mathf.Max(60000, Mathf.RoundToInt(questTimeoutDays * 60000f));
    }
}
