using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class BattleFactionAssault
    {
        [Scenario("battle-faction-assault-two-npc-factions-fight-and-player-gets-a-letter", MapSize = 120, MaxSeconds = 150)]
        static void AssaultBetweenNpcFactions(ScenarioContext s) {
            var def = DefDatabase<IncidentDef>.GetNamed("SrFactionWar");
            var lettersBefore = Find.LetterStack.LettersListForReading.Count;
            var fired = def.Worker.TryExecute(new IncidentParms { target = s.Map, points = 1500f });
            s.Check(fired, "the Faction Assault incident should fire");

            var npcPawns = BattleHelpers.Combatants(s.Map).ToList();
            s.AreEqual(0, BattleHelpers.Hurt(s.Map), "no combatant should be hurt before the battle starts");
            var factions = npcPawns.Select(p => p.Faction).Distinct().ToList();
            s.AreEqual(2, factions.Count, "two NPC factions should have pawns on the map, got: " + BattleHelpers.Names(factions));
            if (factions.Count != 2) return;

            s.Check(factions.All(f => !f.Hidden && !f.IsPlayer), "no hidden or player faction should fight: " + string.Join(", ", factions.Select(f => f.Name + " hidden=" + f.Hidden + " def=" + f.def.defName)));
            s.Check(BattleHelpers.AtWarByGame(factions[0], factions[1]), "the two factions should be at war by the game's own relations; " + BattleHelpers.Goodwill(factions[0], factions[1]));

            var letters = BattleHelpers.NewLetters(lettersBefore);
            var strategy = DefDatabase<RaidStrategyDef>.GetNamed("SrFactionFirst");
            var letter = letters.FirstOrDefault(l => factions.All(f => l.Label.ToString().Contains(f.Name)));
            s.Check(letter != null && letter.Label.ToString().StartsWith(strategy.letterLabelEnemy), "the letter label should start with the raid strategy's label '" + strategy.letterLabelEnemy + "'; letters: " + string.Join(" | ", letters.Select(l => l.Label.ToString())));
            if (letter == null) { s.Fail("the player should get a letter naming both factions; letters: " + string.Join(" | ", letters.Select(l => l.Label.ToString()))); return; }
            if (letter.lookTargets == null) { s.Fail("the letter '" + letter.Label + "' has no look targets"); return; }
            var target = letter.lookTargets.TryGetPrimaryTarget();
            s.Check(target.Thing is Pawn lookPawn && npcPawns.Contains(lookPawn), "the letter should look at one of the battle's pawns; it looks at " + target);

            var over = s.RunUntil(() => factions.Any(f => !s.Map.mapPawns.SpawnedPawnsInFaction(f).Any(BattleHelpers.Standing)), 30000);
            s.Check(over, "one of the two factions should have no standing pawn left at the end of the battle");
            s.Check(BattleHelpers.Hurt(s.Map) > 0 || npcPawns.Any(p => p.Dead || p.Downed), "some pawns should have been hurt or killed in the battle");
        }
    }
}
