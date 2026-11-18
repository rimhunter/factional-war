using System.Linq;
using RimWorld;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class DlcShamblers
    {
        [Scenario("dlc-shambler-faction-stays-and-fights-until-its-shamblers-are-dead", MapSize = 120, MaxSeconds = 170, OnlyOn = "1.7", Mods = "ludeon.rimworld.anomaly")]
        static void ShamblerFactionStaysAndFights(ScenarioContext s) {
            using (var scope = new DlcHelpers.FactionScope("Pirate", "RimhunterTestShamblerCult")) {
                var cult = scope.Add("RimhunterTestShamblerCult");
                var pirates = DlcHelpers.FactionOf("Pirate");
                s.Check(BattleHelpers.AtWarByGame(cult, pirates), "the test faction and the Pirates should be at war; " + BattleHelpers.Goodwill(cult, pirates));

                var def = DefDatabase<IncidentDef>.GetNamed("SrFactionWar");
                s.Check(def.Worker.TryExecute(new IncidentParms { target = s.Map, points = 1500f }), "the Faction Assault incident should fire");

                var shamblers = DlcHelpers.Spawned(s.Map, cult);
                var raiders = DlcHelpers.Spawned(s.Map, pirates);
                s.Check(shamblers.Count > 0, "the shambler faction should have pawns on the map under its own faction; it has none; the map holds: " + string.Join(", ", s.Map.mapPawns.AllPawnsSpawned.Select(p => p.LabelShort + " (" + p.Faction?.Name + ")").Take(8)));
                s.Check(shamblers.All(p => p.IsMutant), "every pawn of the shambler faction should be a shambler: " + string.Join(", ", shamblers.Select(p => p.LabelShort)));
                s.Check(raiders.Count > 0, "the Pirates should have pawns on the map");
                if (shamblers.Count == 0 || raiders.Count == 0) return;

                var clearedEarly = "";
                var over = s.RunUntil(() => {
                    var shamblersAlive = shamblers.Where(p => !p.Dead).ToList();
                    var clearing = raiders.Where(p => !p.Dead && p.CurJob != null && (p.CurJob.def.defName == "Steal" || p.CurJob.def.defName == "Kidnap")).ToList();
                    if (shamblersAlive.Count > 0 && clearing.Count > 0 && clearedEarly == "")
                        clearedEarly = "at tick " + Find.TickManager.TicksGame + " " + string.Join(", ", clearing.Select(p => p.LabelShort + " " + p.CurJob.def.defName)) + " while these shamblers were alive: " + string.Join(", ", shamblersAlive.Select(p => p.LabelShort + (p.Downed ? " (downed)" : "")).Take(6));
                    return shamblersAlive.Count == 0 || !raiders.Any(p => !p.Dead && p.Spawned);
                }, 40000);
                s.Check(over, "the battle should end: all shamblers dead, or every pirate dead or gone");
                s.Check(clearedEarly == "", "pirates should keep fighting until the shamblers are dead, not start taking loot or captives; " + clearedEarly);
            }
            s.RequireNoLogErrors();
        }
    }
}
