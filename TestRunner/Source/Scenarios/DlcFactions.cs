using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class DlcFactions
    {
        static void FactionsFight(ScenarioContext s, string defNameA, string defNameB) {
            using (var scope = new DlcHelpers.FactionScope(defNameA, defNameB)) {
                FactionsFightIn(s, scope, defNameA, defNameB);
            }
        }

        static void FactionsFightIn(ScenarioContext s, DlcHelpers.FactionScope scope, string defNameA, string defNameB) {
            var a = scope.Add(defNameA);
            var b = scope.Add(defNameB);
            s.Check(BattleHelpers.AtWarByGame(a, b), defNameA + " and " + defNameB + " should be at war by the game's own relations; " + BattleHelpers.Goodwill(a, b));

            var def = DefDatabase<IncidentDef>.GetNamed("SrFactionWar");
            s.Check(def.Worker.TryExecute(new IncidentParms { target = s.Map, points = 2500f }), "the Faction Assault incident should fire");

            var onMap = BattleHelpers.NpcFactionsOnMap(s.Map);
            s.AreEqual(defNameA + " " + defNameB, string.Join(" ", onMap.Select(f => f.def.defName).OrderBy(n => n == defNameA ? 0 : 1)), "the factions on the map");
            var sideA = DlcHelpers.Spawned(s.Map, a);
            var sideB = DlcHelpers.Spawned(s.Map, b);
            s.Check(sideA.Count > 0 && sideB.Count > 0, "both factions should have pawns on the map: " + a.Name + " " + sideA.Count + ", " + b.Name + " " + sideB.Count);
            s.Check(sideA.All(p => p.Faction == a) && sideB.All(p => p.Faction == b), "every pawn should carry its own faction");

            var over = s.RunUntil(() => !sideA.Any(p => p.Spawned && BattleHelpers.Standing(p)) || !sideB.Any(p => p.Spawned && BattleHelpers.Standing(p)), 30000);
            s.Check(over, "one side should have no standing pawn at the end of the battle");
            s.Check(sideA.Any(p => p.Dead || p.Downed || p.health.hediffSet.hediffs.Any(h => h is Hediff_Injury)), defNameA + " pawns should have been hurt in the fight: " + string.Join(", ", sideA.Select(p => p.LabelShort).Take(5)));
            s.Check(sideB.Any(p => p.Dead || p.Downed || p.health.hediffSet.hediffs.Any(h => h is Hediff_Injury)), defNameB + " pawns should have been hurt in the fight: " + string.Join(", ", sideB.Select(p => p.LabelShort).Take(5)));
        }

        [Scenario("dlc-ideology-factions-are-picked-and-their-pawns-fight", MapSize = 120, MaxSeconds = 150, OnlyOn = "1.7", Mods = "ludeon.rimworld.ideology")]
        static void Ideology(ScenarioContext s) => FactionsFight(s, "CannibalPirate", "TribeCannibal");

        [Scenario("dlc-biotech-factions-are-picked-and-their-pawns-fight", MapSize = 120, MaxSeconds = 150, OnlyOn = "1.7", Mods = "ludeon.rimworld.biotech")]
        static void Biotech(ScenarioContext s) => FactionsFight(s, "PirateWaster", "TribeRoughNeanderthal");

        [Scenario("dlc-odyssey-faction-that-cannot-walk-in-is-never-picked-for-a-battle", MapSize = 120, MaxSeconds = 60, OnlyOn = "1.7", Mods = "ludeon.rimworld.odyssey")]
        static void OdysseyFactionThatCannotWalkIn(ScenarioContext s) {
            using (var scope = new DlcHelpers.FactionScope("TradersGuild", "Pirate")) {
                var guild = DlcHelpers.FactionOf("TradersGuild");
                var pirates = DlcHelpers.FactionOf("Pirate");
                s.Check(BattleHelpers.AtWarByGame(guild, pirates), "the Traders Guild and the Pirates should be at war; " + BattleHelpers.Goodwill(guild, pirates));
                var def = DefDatabase<IncidentDef>.GetNamed("SrFactionWar");
                var fired = def.Worker.TryExecute(new IncidentParms { target = s.Map, points = 2000f });
                s.Check(!fired, "the incident should not fire when the only hostile pair includes the Traders Guild, which can only arrive by drop pod");
                s.AreEqual(0, BattleHelpers.NpcFactionsOnMap(s.Map).Count, "factions on the map after the refused incident");
            }
            s.RequireNoLogErrors();
        }

        [Scenario("dlc-odyssey-battle-and-sites-start-and-end-with-no-error", MapSize = 120, MaxSeconds = 160, OnlyOn = "1.7", Mods = "ludeon.rimworld.odyssey")]
        static void Odyssey(ScenarioContext s) {
            var layers = Find.WorldGrid.PlanetLayers.Values.Select(l => l.Def.defName).ToList();
            s.Check(layers.Contains("Orbit"), "the world should hold the Orbit layer; layers: " + string.Join(", ", layers));
            var def = DefDatabase<IncidentDef>.GetNamed("SrFactionWar");
            s.Check(def.Worker.TryExecute(new IncidentParms { target = s.Map, points = 2000f }), "the Faction Assault incident should fire");
            var factions = BattleHelpers.NpcFactionsOnMap(s.Map);
            var over = s.RunUntil(() => factions.Any(f => !s.Map.mapPawns.SpawnedPawnsInFaction(f).Any(BattleHelpers.Standing)), 30000);
            s.Check(over, "one faction should have no standing pawn left at the end of the battle");
            foreach (var pair in new[] { ("SrFactionWarContentionSiteGenerate", "SrSiteFactionWarContention"), ("SrFactionWarShellingSiteGenerate", "SrSiteFactionWarShelling"), ("SrFactionWarTempCampSiteGenerate", "SrSiteFactionWarTempCamp") }) {
                for (var i = 0; i < 4; i++) {
                    var site = BattleHelpers.FireSiteIncident(s, pair.Item1, pair.Item2);
                    s.Check(site != null, pair.Item2 + " should be on the world map");
                    if (site == null) return;
                    s.AreEqual("Surface", site.Tile.LayerDef.defName, pair.Item2 + " should be placed on the surface layer");
                    site.Destroy();
                }
            }
        }
    }
}
