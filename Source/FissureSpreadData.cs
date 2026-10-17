using System.Collections.Generic;
using RimWorld.Planet;
using Verse;
using UnityEngine;

namespace TSA.ToxicQuest
{
    public static class WorldTileFlood
    {
        private static readonly List<PlanetTile> neighborBuffer = new List<PlanetTile>(8);

        /// <summary>BFS tiles whose graph distance is in (minExclusive, maxInclusive].</summary>
        public static void CollectRing(int centerTile, int minExclusive, int maxInclusive, List<int> outTiles)
        {
            outTiles.Clear();
            if (maxInclusive < 0) return;
            if (minExclusive < 0) minExclusive = -1;

            var grid = Find.WorldGrid;
            var dist = new Dictionary<int, int>();
            var queue = new Queue<int>();
            dist[centerTile] = 0;
            queue.Enqueue(centerTile);

            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                int d = dist[cur];
                if (d > minExclusive && d <= maxInclusive)
                {
                    outTiles.Add(cur);
                }
                if (d >= maxInclusive) continue;

                neighborBuffer.Clear();
                grid.GetTileNeighbors(cur, neighborBuffer);
                for (int i = 0; i < neighborBuffer.Count; i++)
                {
                    PlanetTile n = neighborBuffer[i];
                    if (!n.Valid) continue;
                    int nid = n.tileId;
                    if (dist.ContainsKey(nid)) continue;
                    dist[nid] = d + 1;
                    queue.Enqueue(nid);
                }
            }
        }

        public static void CollectDisk(int centerTile, int radius, List<int> outTiles)
        {
            CollectRing(centerTile, -1, radius, outTiles);
        }

        public static int GraphDistance(int from, int to, int maxRadius)
        {
            if (from == to) return 0;
            var grid = Find.WorldGrid;
            var dist = new Dictionary<int, int>();
            var queue = new Queue<int>();
            dist[from] = 0;
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                int d = dist[cur];
                if (d >= maxRadius) continue;
                neighborBuffer.Clear();
                grid.GetTileNeighbors(cur, neighborBuffer);
                for (int i = 0; i < neighborBuffer.Count; i++)
                {
                    PlanetTile n = neighborBuffer[i];
                    if (!n.Valid) continue;
                    int nid = n.tileId;
                    if (dist.ContainsKey(nid)) continue;
                    int nd = d + 1;
                    if (nid == to) return nd;
                    dist[nid] = nd;
                    queue.Enqueue(nid);
                }
            }
            return int.MaxValue;
        }
    }

    public class FissureSpreadData : IExposable
    {
        public int centerTile;
        public float currentRadius;
        public float density;
        public int expireTick = -1;

        private List<int> claimedTiles = new List<int>();
        private bool claimedDirty = true;

        public FissureSpreadData() { }

        public FissureSpreadData(int tile, int expireAtTick)
        {
            centerTile = tile;
            currentRadius = 0f;
            density = 0f;
            expireTick = expireAtTick;
            claimedDirty = true;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref centerTile, "centerTile");
            Scribe_Values.Look(ref currentRadius, "currentRadius", 0f);
            Scribe_Values.Look(ref density, "density", 0f);
            Scribe_Values.Look(ref expireTick, "expireTick", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                float cap = ToxicQuestSettings.Current?.EffectiveMaxRadius ?? ToxicQuestSettings.AbsoluteMaxRadius;
                currentRadius = Mathf.Min(currentRadius, cap);
                claimedDirty = true;
            }
        }

        public int RadiusInt => Mathf.FloorToInt(currentRadius);

        public List<int> GetClaimedTiles()
        {
            if (claimedDirty || claimedTiles == null)
            {
                if (claimedTiles == null) claimedTiles = new List<int>();
                WorldTileFlood.CollectDisk(centerTile, RadiusInt, claimedTiles);
                claimedDirty = false;
            }
            return claimedTiles;
        }

        public bool Covers(int tile)
        {
            if (tile == centerTile) return true;
            return WorldTileFlood.GraphDistance(centerTile, tile, RadiusInt) <= RadiusInt;
        }

        public float ContributionAt(int tile)
        {
            return Covers(tile) ? density : 0f;
        }

        public void MarkClaimedDirty() => claimedDirty = true;

        public void ProcessIntervalTick(WorldComponent_FissureSpread worldComp)
        {
            var s = ToxicQuestSettings.Current;
            if (s == null) return;

            float maxR = s.EffectiveMaxRadius;
            float oldRadius = currentRadius;
            density = Mathf.Clamp01(density + s.pollutionIncrease);
            currentRadius = Mathf.Min(currentRadius + s.rangeIncrease, maxR);

            int oldR = Mathf.FloorToInt(oldRadius);
            int newR = Mathf.FloorToInt(currentRadius);
            claimedDirty = true;

            var ring = new List<int>();
            if (oldR <= 0 && newR >= 0)
            {
                // Ensure center is applied on first pulse / growth from zero
                WorldTileFlood.CollectRing(centerTile, -1, newR, ring);
            }
            else if (newR > oldR)
            {
                WorldTileFlood.CollectRing(centerTile, oldR, newR, ring);
            }
            else
            {
                // Radius unchanged: still refresh center disk contribution at new density for newly overlapped? Plan: only new ring.
                // Density rise does not rewrite interior.
                return;
            }

            for (int i = 0; i < ring.Count; i++)
            {
                worldComp.ApplyMergedContribution(ring[i]);
            }
        }

        public void ApplyInitialCenter(WorldComponent_FissureSpread worldComp)
        {
            var s = ToxicQuestSettings.Current;
            density = Mathf.Clamp01(s?.pollutionIncrease ?? 0.1f);
            currentRadius = 0f;
            claimedDirty = true;
            worldComp.ApplyMergedContribution(centerTile);
        }
    }
}
