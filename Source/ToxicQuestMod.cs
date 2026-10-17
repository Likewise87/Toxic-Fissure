using System;
using UnityEngine;
using Verse;

namespace TSA.ToxicQuest
{
    public enum SliderFormat { Fixed0, Fixed1, Fixed2, Multiplier, Percent, PercentDecimal }

    public class ToxicQuestMod : Mod
    {
        public ToxicQuestSettings settings;

        private Vector2 scrollPos = Vector2.zero;
        private static readonly Color SectionHeaderColor = new Color(0.55f, 0.85f, 1f);

        private bool timingExpanded = true;
        private bool pollutionExpanded = true;
        private bool questExpanded = true;

        public ToxicQuestMod(ModContentPack content) : base(content)
        {
            this.settings = GetSettings<ToxicQuestSettings>();
            ToxicQuestSettings.Current = this.settings;
        }

        public override string SettingsCategory() => "TSA: Toxic Fissure";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            float contentWidth = inRect.width - 24f;
            float viewHeight = EstimateScrollHeight();
            Rect viewRect = new Rect(0f, 0f, contentWidth, viewHeight);

            Widgets.BeginScrollView(inRect, ref scrollPos, viewRect);
            Listing_Standard l = new Listing_Standard();
            l.Begin(viewRect);

            DrawMenuTopBar(l, "Restore defaults", () => settings.ResetToDefaults(),
                () => { timingExpanded = pollutionExpanded = questExpanded = true; },
                () => { timingExpanded = pollutionExpanded = questExpanded = false; });

            if (DrawCollapsibleHeader(l, "Timing", ref timingExpanded, SectionHeaderColor))
            {
                l.Label("First Fissure Arrival (Min/Max Days)");
                Rect firstRect = l.GetRect(28f);
                TooltipHandler.TipRegion(firstRect, TooltipWithDefault(
                    "The range of days when the very first fissure can appear in a new game.",
                    "60.0 – 90.0"));
                Widgets.FloatRange(firstRect, 1001, ref settings.firstFissureDays, 0.1f, 300f, null, ToStringStyle.FloatOne);

                l.Gap(4f);
                l.Label("Subsequent Fissure Base Timing (Min/Max Days)");
                Rect subRect = l.GetRect(28f);
                TooltipHandler.TipRegion(subRect, TooltipWithDefault(
                    "The base wait time range before the next fissure spawns (before reductions).",
                    "30.0 – 50.0"));
                Widgets.FloatRange(subRect, 1002, ref settings.subsequentDays, 0.1f, 200f, null, ToStringStyle.FloatOne);

                l.Gap(6f);
                settings.reductionPerOccurrence = LabeledSlider(l,
                    "Wait time reduction per occurrence",
                    settings.reductionPerOccurrence, 0f, 20f,
                    "How many days the wait time is reduced after each successful fissure spawn.",
                    0.1f, SliderFormat.Fixed1, 2f);
                settings.minSubsequentFloor = LabeledSlider(l,
                    "Subsequent timing floor",
                    settings.minSubsequentFloor, 0.1f, 15f,
                    "The absolute minimum time allowed between spawns, even after many reductions have occurred.",
                    0.1f, SliderFormat.Fixed1, 2f);
            }

            if (DrawCollapsibleHeader(l, "Pollution pulse", ref pollutionExpanded, SectionHeaderColor))
            {
                settings.intervalHours = LabeledSlider(l,
                    "Pollution pulse interval (hours)",
                    settings.intervalHours, 0.5f, 48f,
                    "Hours between pollution pulses.",
                    0.1f, SliderFormat.Fixed1, 12f);
                settings.rangeIncrease = LabeledSlider(l,
                    "Expansion per pulse (tiles)",
                    settings.rangeIncrease, 0.1f, 5f,
                    "How many world tiles the radius expands per pulse.",
                    0.1f, SliderFormat.Fixed1, 1.0f);
                settings.pollutionIncrease = LabeledSlider(l,
                    "Pollution density per pulse",
                    settings.pollutionIncrease, 0.01f, 0.5f,
                    "Density added for newly claimed rings per pulse.",
                    0.01f, SliderFormat.Percent, 0.1f);
                settings.maxPollutionRange = LabeledSlider(l,
                    "Max pollution radius (tiles)",
                    settings.maxPollutionRange, 1f, ToxicQuestSettings.AbsoluteMaxRadius,
                    "Maximum regional radius (hard-capped at 15).",
                    1f, SliderFormat.Fixed0, ToxicQuestSettings.AbsoluteMaxRadius);
            }

            if (DrawCollapsibleHeader(l, "Quest", ref questExpanded, SectionHeaderColor))
            {
                settings.questTimeoutDays = LabeledSlider(l,
                    "Quest timeout (days)",
                    settings.questTimeoutDays, 5f, 60f,
                    "If the fissure is not sealed in time, the quest fails and regional fissure contamination is cleaned up.",
                    1f, SliderFormat.Fixed0, 20f);
                settings.insectScaling = LabeledSlider(l,
                    "Insect scaling per occurrence",
                    settings.insectScaling, 1.0f, 3.0f,
                    "Increases the number of insects based on how many fissures have appeared in this save.",
                    0.1f, SliderFormat.Multiplier, 1.2f);
            }

            l.End();
            Widgets.EndScrollView();
        }

        public override void WriteSettings()
        {
            settings.maxPollutionRange = Mathf.Clamp(settings.maxPollutionRange, 1f, ToxicQuestSettings.AbsoluteMaxRadius);
            base.WriteSettings();
        }

        private float EstimateScrollHeight()
        {
            float h = 80f;
            if (timingExpanded) h += 220f;
            if (pollutionExpanded) h += 180f;
            if (questExpanded) h += 100f;
            h += 3 * 50f;
            return Mathf.Max(h, 400f);
        }

        // ----- WD-style helpers (kept in ToxicQuestMod; no separate library file) -----

        private static bool DrawCollapsibleHeader(Listing_Standard l, string label, ref bool expanded, Color? color = null, string tip = null)
        {
            l.Gap(12f);
            Rect r = l.GetRect(26f);
            Widgets.DrawHighlightIfMouseover(r);
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
            if (Widgets.ButtonInvisible(r)) expanded = !expanded;
            Color c = color ?? Color.white;
            string arrow = expanded ? "▼" : "▶";
            Widgets.Label(r, $"<b><color=#{ColorUtility.ToHtmlStringRGBA(c)}>{arrow}  {label}</color></b>");
            if (expanded) { l.GapLine(6f); l.Gap(4f); }
            else l.Gap(4f);
            return expanded;
        }

        private static void DrawMenuTopBar(Listing_Standard l, string resetLabel, Action onReset, Action expandAll, Action collapseAll)
        {
            if (onReset == null) return;
            l.Gap(4f);
            Rect row = l.GetRect(30f);
            float sideBtnW = 110f;
            float rightPairW = sideBtnW * 2f + 8f;
            float resetW = Mathf.Min(250f, row.width - rightPairW - 24f);
            if (resetW < 120f) resetW = Mathf.Max(100f, row.width * 0.35f);
            Rect resetRect = new Rect(row.x, row.y, resetW, row.height);
            Rect collapseRect = new Rect(row.xMax - sideBtnW, row.y, sideBtnW, row.height);
            Rect expandRect = new Rect(collapseRect.x - 8f - sideBtnW, row.y, sideBtnW, row.height);
            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.5f, 0.5f);
            if (Widgets.ButtonText(resetRect, resetLabel)) onReset();
            GUI.color = prev;
            if (expandAll != null && Widgets.ButtonText(expandRect, "Expand all")) expandAll();
            if (collapseAll != null && Widgets.ButtonText(collapseRect, "Collapse all")) collapseAll();
            l.Gap(6f);
        }

        private static float LabeledSlider(Listing_Standard l, string label, float val, float min, float max, string tooltip = null, float step = 0.1f, SliderFormat format = SliderFormat.Fixed1, float? defaultValue = null)
        {
            l.Gap(2f);
            Rect r = l.GetRect(24f);
            if (!tooltip.NullOrEmpty()) TooltipHandler.TipRegion(r, TooltipWithDefault(tooltip, defaultValue, format));
            Widgets.Label(r.LeftPart(0.5f), $"{label}: {FormatValue(val, format).Colorize(Color.cyan)}");
            return Widgets.HorizontalSlider(r.RightPart(0.5f), val, min, max, false, null, null, null, step);
        }

        private static string TooltipWithDefault(string tooltip, float? defaultValue, SliderFormat format = SliderFormat.Fixed1)
        {
            if (!defaultValue.HasValue) return tooltip;
            return AppendDefault(tooltip, FormatValue(defaultValue.Value, format));
        }

        private static string TooltipWithDefault(string tooltip, string defaultValue)
        {
            if (defaultValue.NullOrEmpty()) return tooltip;
            return AppendDefault(tooltip, defaultValue);
        }

        private static string AppendDefault(string tooltip, string def)
        {
            string line = $"Default: {def}";
            return tooltip.NullOrEmpty() ? line : tooltip + "\n" + line;
        }

        private static string FormatValue(float val, SliderFormat format) => format switch
        {
            SliderFormat.Fixed0 => val.ToString("F0"),
            SliderFormat.Fixed1 => val.ToString("F1"),
            SliderFormat.Fixed2 => val.ToString("F2"),
            SliderFormat.Multiplier => val.ToString("F2") + "x",
            SliderFormat.Percent => (val * 100f).ToString("F0") + " %",
            SliderFormat.PercentDecimal => (val * 100f).ToString("F1") + " %",
            _ => val.ToString()
        };
    }
}
