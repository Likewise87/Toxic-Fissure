using System.Collections.Generic;
using RimWorld;
using RimWorld.QuestGen;
using RimWorld.Planet;
using Verse;
using UnityEngine;

namespace TSA.ToxicQuest
{
    public static class ToxicQuestHelper
    {
        public const int MinSpawnDistance = 6;
        public const int MaxSpawnDistance = 14;

        public static bool GenerateQuest()
        {
            var def = DefDatabase<QuestScriptDef>.GetNamedSilentFail("TSA_ToxicFissure_Quest");
            if (def == null) return false;

            var homeMap = Find.AnyPlayerHomeMap;
            if (homeMap == null) return false;

            int foundTileID = FindTileNearHome(TileIdUtility.Id(homeMap.Tile));
            if (foundTileID < 0) return false;

            var slate = new Slate();
            slate.Set("siteTile", TileIdUtility.FromId(foundTileID));
            int timeoutTicks = ToxicQuestSettings.Current?.QuestTimeoutTicks ?? 20 * 60000;
            slate.Set("timeoutTicks", timeoutTicks);

            Quest quest;
            try
            {
                quest = QuestGen.Generate(def, slate);
            }
            catch (System.Exception e)
            {
                Log.Error("[TSA Toxic Fissure] Quest generation failed: " + e);
                return false;
            }

            if (quest == null) return false;

            Find.QuestManager.Add(quest);
            Find.World.GetComponent<WorldComponent_FissureSpread>()?.RegisterFissure(foundTileID);

            Find.LetterStack.ReceiveLetter(
                "Toxic Fissure",
                "A toxic fissure has erupted nearby. Seal it with explosives before regional contamination densifies — or wait until it collapses on its own.",
                LetterDefOf.ThreatBig,
                new GlobalTargetInfo(foundTileID));

            return true;
        }

        public static int FindTileNearHome(int homeTile)
        {
            var candidates = new List<int>();
            WorldTileFlood.CollectRing(homeTile, MinSpawnDistance - 1, MaxSpawnDistance, candidates);
            candidates.Shuffle();

            for (int i = 0; i < candidates.Count; i++)
            {
                int t = candidates[i];
                PlanetTile pt = TileIdUtility.FromId(t);
                if (!TileFinder.IsValidTileForNewSettlement(pt)) continue;
                if (Find.WorldObjects.AnyWorldObjectAt(pt)) continue;
                return t;
            }
            return -1;
        }

        public static void OnFissureDestroyed(Map map)
        {
            if (map == null) return;
            CleanupFissure(
                TileIdUtility.Id(map.Tile),
                QuestEndOutcome.Success,
                "The fissure has been sealed. Regional contamination is dissipating.",
                MessageTypeDefOf.PositiveEvent);
        }

        public static void OnFissureExpired(int tile)
        {
            CleanupFissure(
                tile,
                QuestEndOutcome.Fail,
                "The toxic fissure has collapsed on its own. Regional contamination is dissipating.",
                MessageTypeDefOf.NeutralEvent,
                sendLetter: true,
                letterLabel: "Fissure collapsed",
                letterText: "The toxic fissure destabilized and sealed itself. The regional pollution cloud is clearing.");
            DestroyFissureSiteAt(tile);
        }

        private static void DestroyFissureSiteAt(int tile)
        {
            PlanetTile pt = TileIdUtility.FromId(tile);
            var toDestroy = new List<WorldObject>();
            foreach (var wo in Find.WorldObjects.ObjectsAt(pt))
            {
                if (wo is Site site)
                {
                    for (int i = 0; i < site.parts.Count; i++)
                    {
                        if (site.parts[i]?.def?.defName == "TSA_ToxicFissurePart")
                        {
                            toDestroy.Add(wo);
                            break;
                        }
                    }
                }
            }
            for (int i = 0; i < toDestroy.Count; i++)
            {
                toDestroy[i].Destroy();
            }
        }

        public static void OnFissureSiteRemoved(int tile)
        {
            CleanupFissure(
                tile,
                QuestEndOutcome.Fail,
                "The toxic fissure site is gone. Regional contamination is dissipating.",
                MessageTypeDefOf.NeutralEvent);
        }

        public static void CleanupFissure(
            int tile,
            QuestEndOutcome? questOutcome,
            string message,
            MessageTypeDef messageType,
            bool sendLetter = false,
            string? letterLabel = null,
            string? letterText = null)
        {
            var worldComp = Find.World.GetComponent<WorldComponent_FissureSpread>();
            worldComp?.UnregisterAndPeel(tile);

            ClearLocalEffectsNear(tile);

            if (questOutcome.HasValue)
            {
                EndMatchingQuest(tile, questOutcome.Value);
            }

            if (!string.IsNullOrEmpty(message))
            {
                Messages.Message(message, messageType);
            }
            if (sendLetter && !string.IsNullOrEmpty(letterLabel))
            {
                Find.LetterStack.ReceiveLetter(letterLabel, letterText ?? message, LetterDefOf.NeutralEvent);
            }
        }

        private static void ClearLocalEffectsNear(int centerTile)
        {
            float maxR = ToxicQuestSettings.Current?.EffectiveMaxRadius ?? ToxicQuestSettings.AbsoluteMaxRadius;
            int maxCheck = Mathf.CeilToInt(maxR) + 1;
            foreach (Map activeMap in Find.Maps)
            {
                int mapTile = TileIdUtility.Id(activeMap.Tile);
                if (WorldTileFlood.GraphDistance(centerTile, mapTile, maxCheck) > maxR)
                    continue;
                activeMap.GetComponent<MapComponent_LocalPollutionSync>()?.ClearOwnedPollution();
            }
        }

        private static void EndMatchingQuest(int tile, QuestEndOutcome outcome)
        {
            var quests = Find.QuestManager.QuestsListForReading;
            for (int i = 0; i < quests.Count; i++)
            {
                Quest q = quests[i];
                if (q.root?.defName != "TSA_ToxicFissure_Quest") continue;
                if (q.State != QuestState.Ongoing) continue;

                bool match = false;
                foreach (var t in q.QuestLookTargets)
                {
                    if (TileIdUtility.Id(t.Tile) == tile)
                    {
                        match = true;
                        break;
                    }
                }
                if (!match) continue;

                q.End(outcome);
                return;
            }
        }
    }
}
