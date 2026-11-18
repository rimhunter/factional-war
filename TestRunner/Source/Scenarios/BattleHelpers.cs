using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class BattleHelpers
    {
        public static bool AtWarByGame(Faction a, Faction b) =>
            a.RelationKindWith(b) == FactionRelationKind.Hostile && b.RelationKindWith(a) == FactionRelationKind.Hostile;

        public static string Goodwill(Faction a, Faction b) => a.Name + "/" + b.Name + " goodwill " + a.GoodwillWith(b) + " and " + b.GoodwillWith(a);

        public static IEnumerable<Pawn> Combatants(Map map) =>
            map.mapPawns.AllPawnsSpawned.Where(p => p.Faction != null && !p.Faction.IsPlayer && p.Faction.def.humanlikeFaction);

        public static int Hurt(Map map) => Combatants(map).Count(p => p.Dead || p.Downed);

        public static string Names(IEnumerable<Faction> factions) => string.Join(", ", factions.Select(f => f.Name));

        public static void AddVisibleFactions(int total) {
            var defs = new[] { "OutlanderCivil", "OutlanderRough", "TribeCivil", "TribeRough", "Pirate", "TribeSavage" }
                .Select(n => DefDatabase<FactionDef>.GetNamed(n)).ToList();
            var i = 0;
            while (Find.FactionManager.AllFactionsVisible.Count() < total) {
                var def = defs[i++ % defs.Count];
                var faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def));
                Find.FactionManager.Add(faction);
            }
        }

        public static List<Letter> NewLetters(int before) =>
            Find.LetterStack.LettersListForReading.Skip(before).ToList();

        public static List<Faction> NpcFactionsOnMap(Map map) =>
            map.mapPawns.AllPawnsSpawned.Where(p => p.Faction != null && !p.Faction.IsPlayer && p.Faction.def.humanlikeFaction).Select(p => p.Faction).Distinct().ToList();

        public static bool Standing(Pawn p) => !p.Downed && !p.Dead;

        public static WorldObject FireSiteIncident(ScenarioContext s, string incidentDefName, string worldObjectDefName) {
            var def = DefDatabase<IncidentDef>.GetNamed(incidentDefName);
            var before = Find.WorldObjects.AllWorldObjects.Count(w => w.def.defName == worldObjectDefName);
            s.AreEqual(0, before, "no " + worldObjectDefName + " before the incident");
            var parms = new IncidentParms { target = s.Map, points = 3000f };
            s.Check(def.Worker.TryExecute(parms), "the incident " + incidentDefName + " should fire");
            return Find.WorldObjects.AllWorldObjects.FirstOrDefault(w => w.def.defName == worldObjectDefName);
        }

        public static Map ArriveAt(WorldObject site) {
            var map = GetOrGenerateMapUtility.GetOrGenerateMap(site.Tile, new IntVec3(100, 1, 100), null);
            var colonist = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            GenSpawn.Spawn(colonist, new IntVec3(2, 0, 2), map);
            return map;
        }

        public static Map ArriveByCaravan(ScenarioContext s, WorldObject site, out Caravan caravan) {
            var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            caravan = CaravanMaker.MakeCaravan(new[] { pawn }, Faction.OfPlayer, s.Map.Tile, true);
            var map = GetOrGenerateMapUtility.GetOrGenerateMap(site.Tile, new IntVec3(250, 1, 250), null);
            CaravanEnterMapUtility.Enter(caravan, map, CaravanEnterMode.Edge);
            return map;
        }

        public static Caravan LeaveBy(Map map) {
            var colonists = map.mapPawns.FreeColonistsSpawned.ToList();
            return CaravanExitMapUtility.ExitMapAndCreateCaravan(colonists, Faction.OfPlayer, map.Tile, map.Tile, PlanetTile.Invalid, false);
        }

        public static void Cleanup(ScenarioContext s, WorldObject site, Caravan caravan) {
            Current.Game.CurrentMap = s.Map;
            var mapParent = site as MapParent;
            if (mapParent != null && mapParent.HasMap && Find.Maps.Contains(mapParent.Map)) Current.Game.DeinitAndRemoveMap(mapParent.Map, false);
            if (!site.Destroyed) site.Destroy();
            if (caravan != null && !caravan.Destroyed) caravan.Destroy();
        }

        public static Dictionary<Faction, List<Pawn>> Sides(Map map) =>
            Combatants(map).Where(p => !p.Faction.Hidden).GroupBy(p => p.Faction).ToDictionary(g => g.Key, g => g.ToList());

        public static string Describe(Dictionary<Faction, List<Pawn>> sides) =>
            string.Join("; ", sides.Select(kv => kv.Key.Name + ": " + string.Join(", ", kv.Value.Take(4).Select(p => p.LabelShort)) + " (" + kv.Value.Count + " pawns)"));
    }
}
