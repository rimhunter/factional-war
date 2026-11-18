using System.Collections.Generic;
using JetBrains.Annotations;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace SR.ModRimWorld.FactionalWar
{
    [UsedImplicitly]
    public class SiteFactionWarShelling : BattleSite
    {
        public static readonly IntRange ThreatPoints = new IntRange(3000, 9999);
        private const float MinBlueprintPoints = 60f;
        private const int Radius = 8;

        public static bool CanFight(Faction faction)
        {
            return faction.def.techLevel >= TechLevel.Industrial;
        }

        protected override void StageBattle()
        {
            var points = ThreatPoints.RandomInRange;
            var staged = TryStage(Radius, (SideA, points), (SideB, points));
            if (staged == null)
            {
                return;
            }

            ResolveLordJob(points, staged[0].Parms.spawnCenter, staged[0].Pawns, SideA, SideB);
            ResolveLordJob(points, staged[1].Parms.spawnCenter, staged[1].Pawns, SideB, SideA);
        }

        private void ResolveLordJob(int points, IntVec3 loc, IEnumerable<Pawn> pawns, Faction assaultFaction,
            Faction targetFaction)
        {
            var siegeSpot = RCellFinder.FindSiegePositionFrom(loc, Map);
            var blueprintPoints = points * Rand.Range(0.2f, 0.3f);
            if (blueprintPoints < MinBlueprintPoints)
            {
                blueprintPoints = MinBlueprintPoints;
            }

            var lordJobShellFactionFirst =
                new LordJobShellFactionFirst(assaultFaction, targetFaction, siegeSpot, blueprintPoints);
            LordMaker.MakeNewLord(assaultFaction, lordJobShellFactionFirst, Map, pawns);
        }
    }
}
