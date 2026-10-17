using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;
using UnityEngine;

namespace TSA.ToxicQuest
{
    public class SitePartWorker_ToxicFissureMap : SitePartWorker
    {
        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            IntVec3 center = map.Center;

            CellRect rect = CellRect.CenteredOn(center, 10).ClipInsideMap(map);
            foreach (var c in rect)
            {
                c.GetEdifice(map)?.Destroy();
                map.fogGrid.Unfog(c);
            }

            Thing fissure = GenSpawn.Spawn(ThingDef.Named("ToxicFissure"), center, map);
            var link = fissure.TryGetComp<CompFissureLink>();
            if (link != null)
            {
                link.centerTile = TileIdUtility.Id(map.Tile);
            }

            var comp = Current.Game.GetComponent<GameComponent_ToxicFissure>();
            int occCount = comp != null ? Mathf.Max(1, comp.occurrenceCount) : 1;
            float scaleFactor = ToxicQuestSettings.Current?.insectScaling ?? 1.2f;

            int insectCount = Mathf.RoundToInt(10f * (1f + (occCount - 1) * (scaleFactor - 1f)));
            insectCount = Mathf.Clamp(insectCount, 1, 150);

            PawnKindDef[] kinds = { PawnKindDefOf.Megaspider, PawnKindDefOf.Spelopede };
            List<Pawn> insects = new List<Pawn>();

            for (int i = 0; i < insectCount; i++)
            {
                IntVec3 spawnLoc = CellFinder.RandomClosewalkCellNear(center, map, 6);
                Pawn insect = PawnGenerator.GeneratePawn(kinds.RandomElement(), Faction.OfInsects);
                GenSpawn.Spawn(insect, spawnLoc, map);
                insects.Add(insect);
            }

            if (insects.Count > 0)
            {
                LordMaker.MakeNewLord(Faction.OfInsects, new LordJob_DefendPoint(center), map, insects);
            }
        }
    }

    public class CompProperties_DestroyQuestComplete : CompProperties
    {
        public CompProperties_DestroyQuestComplete() => this.compClass = typeof(CompDestroyQuestComplete);
    }

    public class CompDestroyQuestComplete : ThingComp
    {
        public override void CompTick()
        {
            base.CompTick();
            if (this.parent.Spawned && this.parent.IsHashIntervalTick(90))
            {
                GasUtility.AddGas(this.parent.Position, this.parent.Map, GasType.ToxGas, 150);
                for (int i = 0; i < 4; i++)
                {
                    IntVec3 randomCell = this.parent.Position + GenRadial.RadialPattern[Rand.Range(1, 40)];
                    if (randomCell.InBounds(this.parent.Map))
                        GasUtility.AddGas(randomCell, this.parent.Map, GasType.ToxGas, 150);
                }
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (previousMap != null)
                ToxicQuestHelper.OnFissureDestroyed(previousMap);
        }
    }

    public class CompProperties_FissureLink : CompProperties
    {
        public CompProperties_FissureLink() => this.compClass = typeof(CompFissureLink);
    }

    public class CompFissureLink : ThingComp
    {
        public int centerTile = -1;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (centerTile < 0 && parent.Spawned)
            {
                centerTile = TileIdUtility.Id(parent.Map.Tile);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref centerTile, "centerTile", -1);
        }
    }
}
