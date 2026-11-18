using System.Collections.Generic;
using JetBrains.Annotations;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace SR.ModRimWorld.FactionalWar
{
    [UsedImplicitly]
    public class SiteFactionWarContention : BattleSite
    {
        public static readonly IntRange FactionPoints = new IntRange(3000, 5000);
        private const int Radius = 8;

        public static bool CanFight(Faction faction)
        {
            return faction.def.techLevel >= TechLevel.Industrial && faction.HostileTo(Faction.OfPlayer);
        }

        protected override void StageBattle()
        {
            var staged = TryStage(Radius, (SideA, FactionPoints.RandomInRange), (SideB, 2 * FactionPoints.RandomInRange));
            if (staged == null)
            {
                return;
            }

            ResolveLordJob(staged[0].Pawns, SideA);
            ResolveLordJob(staged[1].Pawns, SideB);
        }

        private void ResolveLordJob(IEnumerable<Pawn> pawns, Faction faction)
        {
            var lordJobFactionContention = new LordJobFactionContention(Map.Center);
            LordMaker.MakeNewLord(faction, lordJobFactionContention, Map, pawns);
        }
    }
}
