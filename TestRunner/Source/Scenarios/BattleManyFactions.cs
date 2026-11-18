using System.Linq;
using RimWorld;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class BattleManyFactions
    {
        [Scenario("battle-30-visible-factions-fight-only-visible-factions-at-war", MapSize = 120, MaxSeconds = 170, OnlyOn = "1.7", Mods = "ludeon.rimworld.ideology")]
        static void ThirtyFactions(ScenarioContext s) {
            BattleHelpers.AddVisibleFactions(30);
            var visible = Find.FactionManager.AllFactionsVisible.ToList();
            s.AreEqual(30, visible.Count, "the world should hold 30 visible factions");
            s.Check(Find.FactionManager.AllFactionsListForReading.Any(f => f.Hidden), "the world should also hold a hidden faction (the Ancients)");

            var def = DefDatabase<IncidentDef>.GetNamed("SrFactionWar");
            for (var round = 1; round <= 8; round++) {
                foreach (var pawn in s.Map.mapPawns.AllPawnsSpawned.Where(p => p.Faction != null && !p.Faction.IsPlayer).ToList())
                    pawn.Destroy();
                var fired = def.Worker.TryExecute(new IncidentParms { target = s.Map, points = 2000f });
                s.Check(fired, "round " + round + ": the Faction Assault incident should fire");
                var factions = BattleHelpers.NpcFactionsOnMap(s.Map);
                var what = "round " + round + ": " + BattleHelpers.Names(factions) + " (" + string.Join(", ", factions.Select(f => f.def.defName + " hidden=" + f.Hidden)) + ")";
                s.AreEqual(2, factions.Count, what + " should be two factions");
                s.Check(factions.All(f => visible.Contains(f) && !f.IsPlayer && !f.defeated), what + " should all be visible, existing NPC factions");
                if (factions.Count == 2)
                    s.Check(BattleHelpers.AtWarByGame(factions[0], factions[1]), what + " should be at war by the game's own relations; " + BattleHelpers.Goodwill(factions[0], factions[1]));
            }
            s.RequireNoLogErrors();
        }
    }
}
