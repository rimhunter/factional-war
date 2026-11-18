using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace RimhunterTestRunner.Scenarios
{
    static class BattleSites
    {
        static void SiteBattle(ScenarioContext s, string incident, string worldObject, string labelKey, string textKey, int maxTicks) {
            var lettersBefore = Find.LetterStack.LettersListForReading.Count;
            var site = BattleHelpers.FireSiteIncident(s, incident, worldObject);
            s.Check(site != null, "the world object " + worldObject + " should be on the world map");
            if (site == null) return;

            var letters = BattleHelpers.NewLetters(lettersBefore);
            var letter = letters.FirstOrDefault(l => l.Label.ToString() == labelKey.Translate().ToString());
            s.Check(letter != null, "the player should get a letter titled '" + labelKey.Translate() + "'; letters: " + string.Join(" | ", letters.Select(l => l.Label.ToString())));
            if (letter is ChoiceLetter choice)
                s.AreEqual(textKey.Translate().ToString(), choice.Text.ToString(), "the letter text");
            s.Check(letter != null && letter.lookTargets != null && letter.lookTargets.TryGetPrimaryTarget().WorldObject == site, "the letter should point at the site on the world map");

            var map = BattleHelpers.ArriveAt(site);
            s.Check(map != null, "arriving at the site should generate its map");
            if (map == null) return;
            s.Check(Find.WorldObjects.MapParentAt(site.Tile) == site, "the map should belong to the site");

            try {
                var factions = BattleHelpers.NpcFactionsOnMap(map);
                s.Check(factions.Count >= 2, "the site map should hold pawns of two NPC factions; found: " + BattleHelpers.Names(factions));
                s.Check(factions.All(f => !f.Hidden && !f.IsPlayer), "no hidden or player faction should be on the site map: " + BattleHelpers.Names(factions));
                s.Check(factions.Any(f => factions.Any(g => f != g && BattleHelpers.AtWarByGame(f, g))), "two of the factions on the site map should be at war by the game's own relations: " + string.Join("; ", factions.SelectMany(f => factions.Where(g => g != f).Select(g => BattleHelpers.Goodwill(f, g)))));
                s.AreEqual(0, BattleHelpers.Hurt(map), "no combatant should be hurt before the battle starts");

                Current.Game.CurrentMap = map;
                var fought = s.RunUntil(() => BattleHelpers.Hurt(map) > 0, maxTicks);
                s.Check(fought, "combatants should be hurt in the battle on the site map");
                var ended = s.RunUntil(() => factions.Count(f => map.mapPawns.SpawnedPawnsInFaction(f).Any(BattleHelpers.Standing)) < 2, maxTicks);
                s.Check(ended, "the battle should end with at most one faction standing or one that left the map");
            } finally {
                Current.Game.CurrentMap = s.Map;
                if (Find.Maps.Contains(map)) Current.Game.DeinitAndRemoveMap(map, false);
                if (!site.Destroyed) site.Destroy();
            }
        }

        [Scenario("battle-contention-site-letter-and-two-factions-fight", MapSize = 80, MaxSeconds = 150, OnlyOn = "1.7")]
        static void Contention(ScenarioContext s) =>
            SiteBattle(s, "SrFactionWarContentionSiteGenerate", "SrSiteFactionWarContention", "SrLabelFactionWarContention", "SrDescFactionWarContention", 20000);

        [Scenario("battle-shelling-site-letter-and-two-factions-fight", MapSize = 80, MaxSeconds = 150, OnlyOn = "1.7")]
        static void Shelling(ScenarioContext s) =>
            SiteBattle(s, "SrFactionWarShellingSiteGenerate", "SrSiteFactionWarShelling", "SrLabelFactionWarShelling", "SrDescFactionWarShelling", 20000);

        [Scenario("battle-camp-site-letter-and-faction-assaults-camp", MapSize = 80, MaxSeconds = 165, OnlyOn = "1.7")]
        static void TempCamp(ScenarioContext s) =>
            SiteBattle(s, "SrFactionWarTempCampSiteGenerate", "SrSiteFactionWarTempCamp", "SrLabelFactionWarTempCamp", "SrDescFactionWarTempCamp", 40000);
    }
}
