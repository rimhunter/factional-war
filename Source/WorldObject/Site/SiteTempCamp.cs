using JetBrains.Annotations;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace SR.ModRimWorld.FactionalWar
{
    [UsedImplicitly]
    public class SiteTempCamp : BattleSite
    {
        public static readonly IntRange ThreatPoints = new IntRange(2000, 5000);
        private const int Radius = 10;

        protected override void StageBattle()
        {
            if (Faction != SideA)
            {
                Log.Error("[SR.ModRimWorld.FactionalWar]" + def.defName + " at tile " + Tile + " belongs to " +
                          Faction + " but its camp faction is " + SideA);
                return;
            }

            var staged = TryStage(Radius, (SideB, ThreatPoints.RandomInRange));
            if (staged == null)
            {
                return;
            }

            var lordJobRaidFactionFirst = new LordJobRaidFactionFirst(SideB, SideA);
            LordMaker.MakeNewLord(SideB, lordJobRaidFactionFirst, Map, staged[0].Pawns);
        }
    }
}
