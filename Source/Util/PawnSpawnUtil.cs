using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SR.ModRimWorld.FactionalWar
{
    public class PawnSpawnUtil : BaseSingleTon<PawnSpawnUtil>
    {
        public static bool SpawnPawns(IReadOnlyList<Pawn> pawns, IncidentParms incidentParms, Map map, int radius)
        {
            if (!RCellFinder.TryFindRandomPawnEntryCell(out incidentParms.spawnCenter, map,
                CellFinder.EdgeRoadChance_Hostile, true))
            {
                Log.Error("[SR.ModRimWorld.FactionalWar]no walkable edge cell on map " + map + " (tile " + map.Tile +
                          ") to bring in " + pawns.Count + " pawns of " + incidentParms.faction);
                return false;
            }

            var stageLoc = RCellFinder.FindSiegePositionFrom(incidentParms.spawnCenter, map);
            var spawnRotation = Rot4.FromAngleFlat((map.Center - stageLoc).AngleFlat);
            foreach (var pawn in pawns)
            {
                var loc = CellFinder.RandomClosewalkCellNear(stageLoc, map, radius);
                GenSpawn.Spawn(pawn, loc, map, spawnRotation);
            }

            return true;
        }
    }
}
