using System.Collections.Generic;
using RimWorld.Planet;
using Verse;
using UnityEngine;

namespace TSA.ToxicQuest
{
    public class WorldComponent_FissureSpread : WorldComponent
    {
        private Dictionary<int, FissureSpreadData> spreaders = new Dictionary<int, FissureSpreadData>();
        private Dictionary<int, float> previousMerged = new Dictionary<int, float>();
        private bool migratedPreviousMerged;

        private static readonly List<int> tmpDisk = new List<int>();

        public WorldComponent_FissureSpread(World world) : base(world) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref spreaders, "spreaders", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref previousMerged, "previousMerged", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref migratedPreviousMerged, "migratedPreviousMerged", false);
            if (spreaders == null) spreaders = new Dictionary<int, FissureSpreadData>();
            if (previousMerged == null) previousMerged = new Dictionary<int, float>();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                MigrateIfNeeded();
            }
        }

        private void MigrateIfNeeded()
        {
            float cap = ToxicQuestSettings.Current?.EffectiveMaxRadius ?? ToxicQuestSettings.AbsoluteMaxRadius;
            foreach (var s in spreaders.Values)
            {
                s.currentRadius = Mathf.Min(s.currentRadius, cap);
                s.MarkClaimedDirty();
                if (s.expireTick < 0 && ToxicQuestSettings.Current != null)
                {
                    s.expireTick = Find.TickManager.TicksGame + ToxicQuestSettings.Current.QuestTimeoutTicks;
                }
            }

            if (migratedPreviousMerged) return;

            // Old saves: claim existing world pollution up to density without adding again.
            foreach (var s in spreaders.Values)
            {
                var disk = s.GetClaimedTiles();
                for (int i = 0; i < disk.Count; i++)
                {
                    int tile = disk[i];
                    float worldPol = Find.WorldGrid[tile].pollution;
                    float claim = Mathf.Min(worldPol, Mathf.Max(s.density, 0.01f));
                    if (!previousMerged.TryGetValue(tile, out float existing) || claim > existing)
                    {
                        previousMerged[tile] = claim;
                    }
                }
            }
            migratedPreviousMerged = true;
        }

        public override void WorldComponentTick()
        {
            if (ToxicQuestSettings.Current == null) return;
            if (spreaders.Count == 0) return;

            if (Find.TickManager.TicksGame % 250 == 0)
            {
                CheckTimeoutsAndOrphans();
            }

            if (Find.TickManager.TicksGame % ToxicQuestSettings.Current.IntervalTicks == 0)
            {
                foreach (var s in spreaders.Values)
                {
                    s.ProcessIntervalTick(this);
                }
            }
        }

        private void CheckTimeoutsAndOrphans()
        {
            if (spreaders.Count == 0) return;
            var toClean = new List<(int tile, string reason)>();
            foreach (var kv in spreaders)
            {
                int tile = kv.Key;
                var data = kv.Value;
                if (data.expireTick >= 0 && Find.TickManager.TicksGame >= data.expireTick)
                {
                    toClean.Add((tile, "timeout"));
                    continue;
                }
                if (!HasFissureSiteAt(tile))
                {
                    toClean.Add((tile, "siteGone"));
                }
            }

            for (int i = 0; i < toClean.Count; i++)
            {
                var (tile, reason) = toClean[i];
                if (reason == "timeout")
                    ToxicQuestHelper.OnFissureExpired(tile);
                else
                    ToxicQuestHelper.OnFissureSiteRemoved(tile);
            }
        }

        public static bool HasFissureSiteAt(int tile)
        {
            foreach (var wo in Find.WorldObjects.ObjectsAt(TileIdUtility.FromId(tile)))
            {
                if (wo is Site site)
                {
                    for (int i = 0; i < site.parts.Count; i++)
                    {
                        if (site.parts[i]?.def?.defName == "TSA_ToxicFissurePart")
                            return true;
                    }
                }
            }
            return false;
        }

        public bool HasActiveFissures() => spreaders.Count > 0;

        public bool TryGetSpreader(int tile, out FissureSpreadData data) => spreaders.TryGetValue(tile, out data);

        public float GetMergedContribution(int tile)
        {
            float max = 0f;
            foreach (var s in spreaders.Values)
            {
                float c = s.ContributionAt(tile);
                if (c > max) max = c;
            }
            return max;
        }

        public void RegisterFissure(int tile)
        {
            if (spreaders.ContainsKey(tile)) return;
            int expire = Find.TickManager.TicksGame + (ToxicQuestSettings.Current?.QuestTimeoutTicks ?? 20 * 60000);
            var data = new FissureSpreadData(tile, expire);
            spreaders[tile] = data;
            migratedPreviousMerged = true;
            data.ApplyInitialCenter(this);
        }

        public void UnregisterAndPeel(int tile)
        {
            if (!spreaders.TryGetValue(tile, out FissureSpreadData target)) return;

            tmpDisk.Clear();
            WorldTileFlood.CollectDisk(target.centerTile, target.RadiusInt, tmpDisk);
            spreaders.Remove(tile);

            for (int i = 0; i < tmpDisk.Count; i++)
            {
                ApplyMergedContribution(tmpDisk[i]);
            }
        }

        public void ApplyMergedContribution(int tile)
        {
            float newVal = GetMergedContribution(tile);
            previousMerged.TryGetValue(tile, out float oldVal);
            if (Mathf.Approximately(oldVal, newVal))
            {
                if (newVal <= 0.0001f)
                    previousMerged.Remove(tile);
                return;
            }

            float world = Find.WorldGrid[tile].pollution;
            float updated = Mathf.Clamp01(world - oldVal + newVal);
            if (!Mathf.Approximately(world, updated))
            {
                Find.WorldGrid[tile].pollution = updated;
                if (Find.World?.renderer != null)
                    Find.World.renderer.Notify_TilePollutionChanged(TileIdUtility.FromId(tile));
            }

            if (newVal <= 0.0001f)
                previousMerged.Remove(tile);
            else
                previousMerged[tile] = newVal;
        }
    }
}
