using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace SR.ModRimWorld.FactionalWar
{
    public abstract class BattleSite : Site
    {
        private const int TimeOutTick = 90000;
        private Faction _sideA;
        private Faction _sideB;

        protected Faction SideA => _sideA;
        protected Faction SideB => _sideB;

        protected sealed class StagedSide
        {
            public Faction Faction;
            public List<Pawn> Pawns;
            public IncidentParms Parms;
        }

        public void SetSides(Faction sideA, Faction sideB)
        {
            _sideA = sideA;
            _sideB = sideB;
        }

        public override void SpawnSetup()
        {
            base.SpawnSetup();
            var comp = GetComponent<TimeoutComp>();
            if (comp == null)
            {
                Log.Error("[SR.ModRimWorld.FactionalWar]can't find TimeoutComp in " + def.defName);
                return;
            }

            if (!comp.Active)
            {
                comp.StartTimeout(TimeOutTick);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref _sideA, "sideA");
            Scribe_References.Look(ref _sideB, "sideB");
        }

        public override void PostMapGenerate()
        {
            base.PostMapGenerate();
            if (_sideA == null || _sideB == null)
            {
                Log.Error("[SR.ModRimWorld.FactionalWar]" + def.defName + " at tile " + Tile +
                          " has no warring factions, so its map holds no battle");
                return;
            }

            StageBattle();
        }

        protected abstract void StageBattle();

        protected List<StagedSide> TryStage(int radius, params (Faction faction, float points)[] sides)
        {
            var staged = new List<StagedSide>();
            foreach (var (faction, points) in sides)
            {
                var parms = new IncidentParms {points = points, faction = faction, target = Map};
                var groupParms =
                    IncidentParmsUtility.GetDefaultPawnGroupMakerParms(PawnGroupKindDefOf.Combat, parms);
                var pawns = PawnGroupMakerUtility.GeneratePawns(groupParms);
                staged.Add(new StagedSide {Faction = faction, Pawns = pawns, Parms = parms});
                if (pawns.Count > 0)
                {
                    continue;
                }

                Log.Error("[SR.ModRimWorld.FactionalWar]no pawns came out of the " + faction + " combat group at " +
                          points + " points for " + def.defName + " at tile " + Tile +
                          " (another mod may have thrown while generating pawns)");
                Discard(staged);
                return null;
            }

            foreach (var side in staged)
            {
                if (PawnSpawnUtil.SpawnPawns(side.Pawns, side.Parms, Map, radius))
                {
                    continue;
                }

                Discard(staged);
                return null;
            }

            return staged;
        }

        private static void Discard(IEnumerable<StagedSide> staged)
        {
            foreach (var pawn in staged.SelectMany(side => side.Pawns))
            {
                if (pawn.Spawned)
                {
                    pawn.DeSpawn();
                }

                if (Find.WorldPawns.Contains(pawn))
                {
                    Find.WorldPawns.RemoveAndDiscardPawnViaGC(pawn);
                }
            }
        }

        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
        {
            var remove = base.ShouldRemoveMapNow(out alsoRemoveWorldObject);
            if (!remove || !BattleContinues)
            {
                return remove;
            }

            alsoRemoveWorldObject = false;
            return false;
        }

        private bool BattleContinues
        {
            get
            {
                var comp = GetComponent<TimeoutComp>();
                if (comp == null || comp.Passed)
                {
                    return false;
                }

                return Standing(_sideA) && Standing(_sideB);
            }
        }

        private bool Standing(Faction faction)
        {
            return faction != null &&
                   Map.mapPawns.SpawnedPawnsInFaction(faction).Any(pawn => !pawn.Dead && !pawn.Downed);
        }
    }
}
