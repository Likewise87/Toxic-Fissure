using RimWorld.Planet;

namespace TSA.ToxicQuest
{
    public static class TileIdUtility
    {
        public static int Id(PlanetTile tile) => tile.tileId;

        public static PlanetTile FromId(int tileId) => new PlanetTile(tileId);
    }
}
