using System.Linq;
using RimWorld;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class DlcEmpire
    {
        [Scenario("dlc-empire-winners-leave-when-a-beaten-pawn-cannot-be-reached", MapSize = 120, MaxSeconds = 170, OnlyOn = "1.7", Mods = "ludeon.rimworld.royalty")]
        static void EmpireWinnersLeaveWhenARemnantCannotBeReached(ScenarioContext s) {
            using (var scope = new DlcHelpers.FactionScope("Empire", "Pirate")) {
                var empire = DlcHelpers.FactionOf("Empire");
                var pirates = DlcHelpers.FactionOf("Pirate");
                var def = DefDatabase<IncidentDef>.GetNamed("SrFactionWar");
                s.Check(def.Worker.TryExecute(new IncidentParms { target = s.Map, points = 2500f }), "the Faction Assault incident should fire");

                var soldiers = DlcHelpers.Spawned(s.Map, empire);
                s.Check(soldiers.Count > 0, "the Empire should have soldiers on the map");
                var remnant = DlcHelpers.Spawned(s.Map, pirates).First();
                foreach (var pirate in DlcHelpers.Spawned(s.Map, pirates).Where(p => p != remnant)) pirate.Destroy();
                HealthUtility.DamageUntilDowned(remnant, false);
                var cell = remnant.Position;
                foreach (var around in GenAdj.CellsAdjacent8Way(new TargetInfo(cell, s.Map)).ToList()) {
                    foreach (var thing in around.GetThingList(s.Map).ToList()) thing.Destroy();
                    s.SpawnBuilding("Wall", around, "BlocksGranite");
            }
            s.Check(remnant.Downed && !remnant.Dead && remnant.Spawned, "the last pirate should be alive, downed and walled in");

            var gone = s.RunUntil(() => !DlcHelpers.Spawned(s.Map, empire).Any(), 25000);
            s.Check(gone, "the Empire soldiers should leave the map once the only pirate left cannot be reached; still on the map after 25000 ticks: " + string.Join(", ", DlcHelpers.Spawned(s.Map, empire).Take(6).Select(p => p.LabelShort + " job=" + (p.CurJob?.def.defName ?? "none"))));
            s.Check(!remnant.Dead && remnant.Spawned, "the walled-in pirate should still be alive");
            }
            s.RequireNoLogErrors();
        }
    }
}
