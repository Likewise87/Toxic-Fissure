using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;

namespace TSA.ToxicQuest
{
    public class MapComponent_LocalPollutionSync : MapComponent
    {
        public const int MaxOwnedCells = 300;
        public const int PollutePerRareTick = 25;

        private HashSet<int> cellsWePolluted = new HashSet<int>();
        private List<int> cellsWePollutedList = new List<int>(); // for scribing

        public MapComponent_LocalPollutionSync(Map map) : base(map) { }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                cellsWePollutedList.Clear();
                cellsWePollutedList.AddRange(cellsWePolluted);
            }
            Scribe_Collections.Look(ref cellsWePollutedList, "cellsWePolluted", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                cellsWePolluted = cellsWePollutedList != null
                    ? new HashSet<int>(cellsWePollutedList)
                    : new HashSet<int>();
                if (cellsWePollutedList == null) cellsWePollutedList = new List<int>();
            }
        }

        public override void MapComponentTick()
        {
            if (!map.IsHashIntervalTick(2500)) return;

            var worldComp = Find.World.GetComponent<WorldComponent_FissureSpread>();
            float contribution = worldComp != null
                ? worldComp.GetMergedContribution(TileIdUtility.Id(map.Tile))
                : 0f;

            SyncGameCondition(contribution);
            if (contribution > 0.001f)
            {
                BudgetPollute(contribution);
            }
        }

        private void SyncGameCondition(float contribution)
        {
            var cond = map.gameConditionManager.GetActiveCondition(DefDatabase<GameConditionDef>.GetNamedSilentFail("TSA_FissureContamination"))
                       as GameCondition_FissureContamination;

            if (contribution > 0.001f)
            {
                if (cond == null)
                {
                    var def = DefDatabase<GameConditionDef>.GetNamedSilentFail("TSA_FissureContamination");
                    if (def == null) return;
                    cond = (GameCondition_FissureContamination)GameConditionMaker.MakeCondition(def);
                    map.gameConditionManager.RegisterCondition(cond);
                }
                cond.severity = contribution;
            }
            else if (cond != null)
            {
                cond.End();
            }
        }

        private void BudgetPollute(float contribution)
        {
            int target = Mathf.Clamp(Mathf.RoundToInt(contribution * MaxOwnedCells), 0, MaxOwnedCells);
            if (cellsWePolluted.Count >= target) return;

            int budget = PollutePerRareTick;
            int guard = 200;
            while (budget > 0 && cellsWePolluted.Count < target && guard-- > 0)
            {
                IntVec3 cell = CellFinder.RandomCell(map);
                if (!cell.InBounds(map)) continue;
                if (!map.pollutionGrid.CanPollute(cell)) continue;
                int idx = map.cellIndices.CellToIndex(cell);
                if (cellsWePolluted.Contains(idx)) continue;
                if (map.pollutionGrid.IsPolluted(cell)) continue;

                map.pollutionGrid.SetPolluted(cell, true, true);
                cellsWePolluted.Add(idx);
                budget--;
            }
        }

        public void ClearOwnedPollution()
        {
            foreach (int idx in cellsWePolluted)
            {
                IntVec3 cell = map.cellIndices.IndexToCell(idx);
                if (!cell.InBounds(map)) continue;
                if (map.pollutionGrid.IsPolluted(cell))
                {
                    map.pollutionGrid.SetPolluted(cell, false, true);
                }
            }
            cellsWePolluted.Clear();

            var cond = map.gameConditionManager.GetActiveCondition(DefDatabase<GameConditionDef>.GetNamedSilentFail("TSA_FissureContamination"));
            cond?.End();
        }
    }

    public class GameCondition_FissureContamination : GameCondition
    {
        public float severity = 0.25f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref severity, "severity", 0.25f);
        }

        public override void GameConditionTick()
        {
            base.GameConditionTick();
            if (Find.TickManager.TicksGame % 345 != 0) return;

            List<Map> maps = AffectedMaps;
            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                var pawns = map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn pawn = pawns[i];
                    if (pawn.Dead || !pawn.RaceProps.IsFlesh) continue;
                    if (pawn.Position.Roofed(map)) continue;

                    float chance = 0.15f * Mathf.Clamp01(severity);
                    if (!Rand.Chance(chance)) continue;

                    float amount = Rand.Range(0.008f, 0.02f) * Mathf.Clamp01(severity);
                    HealthUtility.AdjustSeverity(pawn, HediffDefOf.ToxicBuildup, amount);
                }
            }
        }
    }
}
