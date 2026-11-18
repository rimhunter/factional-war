using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class BattleArrival
    {
        const string AllDlc = "ludeon.rimworld.royalty ludeon.rimworld.anomaly ludeon.rimworld.odyssey";

        static readonly (string Incident, string WorldObject)[] SiteKinds = {
            ("SrFactionWarContentionSiteGenerate", "SrSiteFactionWarContention"),
            ("SrFactionWarShellingSiteGenerate", "SrSiteFactionWarShelling"),
            ("SrFactionWarTempCampSiteGenerate", "SrSiteFactionWarTempCamp"),
        };

        static void BothSidesOnMap(ScenarioContext s, WorldObject site, Map map, string label) {
            var sides = BattleHelpers.Sides(map);
            var described = BattleHelpers.Describe(sides);
            s.AreEqual(2, sides.Count, label + ": exactly two humanlike NPC factions should be on the map; found: " + described);
            if (sides.Count != 2) return;
            var pair = sides.Keys.ToList();
            s.Check(BattleHelpers.AtWarByGame(pair[0], pair[1]), label + ": the two sides should be at war with each other; " + BattleHelpers.Goodwill(pair[0], pair[1]));
            s.Check(sides.Values.All(pawns => pawns.Any(BattleHelpers.Standing)), label + ": each side should have standing pawns; found: " + described);
            if (site.def.defName == "SrSiteFactionWarTempCamp")
                s.Check(site.Faction != null && sides.ContainsKey(site.Faction), label + ": the camp's own faction " + site.Faction?.Name + " should be one of the sides; found: " + described);
            else
                s.Check(pair.All(f => f.def.techLevel >= TechLevel.Industrial), label + ": both sides should be Industrial or higher; found: " + described);
            if (site.def.defName == "SrSiteFactionWarContention")
                s.Check(pair.All(f => f.HostileTo(Faction.OfPlayer)), label + ": both contenders should be hostile to the player; found: " + described);
            s.Check(site is MapParent mp && mp.AllComps.All(c => c.GetType().Name != "TimedDetectionRaids"), label + ": the site should have no raid countdown");
        }

        static void ArriveAndCheck(ScenarioContext s, (string Incident, string WorldObject) kind) {
            var site = BattleHelpers.FireSiteIncident(s, kind.Incident, kind.WorldObject);
            s.Check(site != null, kind.WorldObject + ": the site should exist");
            if (site == null) return;
            Caravan caravan = null;
            try {
                var map = BattleHelpers.ArriveByCaravan(s, site, out caravan);
                BothSidesOnMap(s, site, map, kind.WorldObject);
            } finally {
                BattleHelpers.Cleanup(s, site, caravan);
            }
        }

        [Scenario("battle-arrival-all-dlc-contention-both-sides-on-map", MapSize = 120, MaxSeconds = 150, OnlyOn = "1.7", Mods = AllDlc)]
        static void ContentionAllDlc(ScenarioContext s) => ArriveAndCheck(s, SiteKinds[0]);

        [Scenario("battle-arrival-all-dlc-shelling-both-sides-on-map", MapSize = 120, MaxSeconds = 150, OnlyOn = "1.7", Mods = AllDlc)]
        static void ShellingAllDlc(ScenarioContext s) => ArriveAndCheck(s, SiteKinds[1]);

        [Scenario("battle-arrival-all-dlc-camp-both-sides-on-map", MapSize = 120, MaxSeconds = 150, OnlyOn = "1.7", Mods = AllDlc)]
        static void CampAllDlc(ScenarioContext s) => ArriveAndCheck(s, SiteKinds[2]);

        [Scenario("battle-contention-creates-no-site-when-the-player-is-at-peace-with-all-but-the-pirates", MapSize = 120, MaxSeconds = 120, OnlyOn = "1.7")]
        static void PeaceNoContention(ScenarioContext s) {
            var friends = Find.FactionManager.AllFactionsVisible.Where(f => !f.IsPlayer && !f.def.permanentEnemy).ToList();
            var before = friends.ToDictionary(f => f, f => f.PlayerGoodwill);
            try {
                foreach (var f in friends) f.TryAffectGoodwillWith(Faction.OfPlayer, 200, false, false);
                var hostile = Find.FactionManager.AllFactionsVisible.Where(f => !f.IsPlayer && f.HostileTo(Faction.OfPlayer) && f.def.techLevel >= TechLevel.Industrial).ToList();
                s.AreEqual("Pirate", string.Join(",", hostile.Select(f => f.def.defName)), "setup: only the pirates should be an Industrial or higher faction hostile to the player");

                var lettersBefore = Find.LetterStack.LettersListForReading.Count;
                var fired = DefDatabase<IncidentDef>.GetNamed(SiteKinds[0].Incident).Worker.TryExecute(new IncidentParms { target = s.Map, points = 3000f });
                s.Check(!fired, SiteKinds[0].Incident + " should not fire when fewer than two factions are hostile to the player");
                s.AreEqual(0, Find.WorldObjects.AllWorldObjects.Count(w => w.def.defName == SiteKinds[0].WorldObject), "no " + SiteKinds[0].WorldObject + " should exist");
                s.AreEqual(0, BattleHelpers.NewLetters(lettersBefore).Count, "the player should get no letter about a battle that cannot happen");

                ArriveAndCheck(s, SiteKinds[1]);
            } finally {
                foreach (var f in friends) f.TryAffectGoodwillWith(Faction.OfPlayer, before[f] - f.PlayerGoodwill, false, false);
            }
            s.RequireNoLogErrors();
        }

        [Scenario("battle-site-timeout-keeps-counting-after-the-site-is-set-up-again", MapSize = 120, MaxSeconds = 60, OnlyOn = "1.7")]
        static void TimeoutSurvivesRespawn(ScenarioContext s) {
            foreach (var kind in SiteKinds) {
                var site = BattleHelpers.FireSiteIncident(s, kind.Incident, kind.WorldObject);
                s.Check(site != null, kind.WorldObject + ": the site should exist");
                if (site == null) continue;
                var timeout = site.GetComponent<TimeoutComp>();
                s.Check(timeout != null && timeout.Active, kind.WorldObject + ": the site should have a running timeout");
                if (timeout == null) { site.Destroy(); continue; }
                s.Tick(300);
                var before = timeout.TicksLeft;
                s.Check(before < 90000, kind.WorldObject + ": the timeout should have counted down after 300 ticks; ticks left " + before);
                site.SpawnSetup();
                s.Check(timeout.TicksLeft <= before, kind.WorldObject + ": setting the site up again (as a load does) should not restart the timeout; ticks left " + before + " before and " + timeout.TicksLeft + " after");
                site.Destroy();
            }
            s.RequireNoLogErrors();
        }

        static void AwayFor(ScenarioContext s, (string Incident, string WorldObject) kind, bool endBattleWhileAway) {
            var site = BattleHelpers.FireSiteIncident(s, kind.Incident, kind.WorldObject);
            s.Check(site != null, kind.WorldObject + ": the site should exist");
            if (site == null) return;
            Caravan caravan = null;
            try {
                var map = BattleHelpers.ArriveByCaravan(s, site, out caravan);
                var first = BattleHelpers.Sides(map);
                s.Check(first.Count >= 2, kind.WorldObject + ": two sides should be on the map; found: " + BattleHelpers.Describe(first));
                var firstPawns = first.Values.SelectMany(p => p).ToList();

                caravan = BattleHelpers.LeaveBy(map);
                Current.Game.CurrentMap = s.Map;
                s.Tick(120);
                var mapParent = (MapParent)site;
                var standing = string.Join("; ", first.Keys.Select(f => f.Name + ": " + f.def.defName + " standing " + firstPawns.Count(p => p.Faction == f && BattleHelpers.Standing(p) && p.Spawned)));
                s.Check(!site.Destroyed && mapParent.HasMap && Find.Maps.Contains(map), kind.WorldObject + ": the battle should go on, with its site and map kept, while the player is away; after 120 ticks away " + standing);
                if (site.Destroyed || !mapParent.HasMap) return;

                if (!endBattleWhileAway) {
                    var again = GetOrGenerateMapUtility.GetOrGenerateMap(site.Tile, new IntVec3(250, 1, 250), null);
                    s.Check(again == map, kind.WorldObject + ": arriving again should give the map that was left");
                    CaravanEnterMapUtility.Enter(caravan, again, CaravanEnterMode.Edge);
                    var after = BattleHelpers.Sides(again);
                    var strangers = after.Values.SelectMany(p => p).Where(p => !firstPawns.Contains(p)).ToList();
                    s.AreEqual(0, strangers.Count, kind.WorldObject + ": no second set of pawns should appear; strangers: " + string.Join(", ", strangers.Select(p => p.Faction.Name + "/" + p.LabelShort)));
                    var lost = firstPawns.Where(p => !p.Dead && !p.Spawned).ToList();
                    s.AreEqual(0, lost.Count, kind.WorldObject + ": no pawn of the first set should be lost; lost: " + string.Join(", ", lost.Select(p => p.Faction.Name + "/" + p.LabelShort)));
                    s.Check(after.Count >= 2, kind.WorldObject + ": both sides should still be on the map; found: " + BattleHelpers.Describe(after));
                } else {
                    var losing = first.Keys.First();
                    foreach (var pawn in map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == losing).ToList())
                        pawn.Kill(null);
                    var removed = s.RunUntil(() => site.Destroyed, 3000);
                    s.Check(removed, kind.WorldObject + ": the site should disappear once the battle ended while the player was away");
                    s.Check(!Find.Maps.Contains(map), kind.WorldObject + ": the site's map should be removed with it");
                }
            } finally {
                BattleHelpers.Cleanup(s, site, caravan);
            }
        }

        [Scenario("battle-leaving-contention-and-arriving-again-keeps-the-same-pawns", MapSize = 120, MaxSeconds = 150, OnlyOn = "1.7")]
        static void ContentionAgain(ScenarioContext s) => AwayFor(s, SiteKinds[0], false);

        [Scenario("battle-leaving-shelling-and-arriving-again-keeps-the-same-pawns", MapSize = 120, MaxSeconds = 150, OnlyOn = "1.7")]
        static void ShellingAgain(ScenarioContext s) => AwayFor(s, SiteKinds[1], false);

        [Scenario("battle-leaving-camp-and-arriving-again-keeps-the-same-pawns", MapSize = 120, MaxSeconds = 150, OnlyOn = "1.7")]
        static void CampAgain(ScenarioContext s) => AwayFor(s, SiteKinds[2], false);

        [Scenario("battle-contention-ending-while-the-player-is-away-removes-the-site", MapSize = 120, MaxSeconds = 150, OnlyOn = "1.7")]
        static void ContentionEnds(ScenarioContext s) => AwayFor(s, SiteKinds[0], true);

        [Scenario("battle-shelling-ending-while-the-player-is-away-removes-the-site", MapSize = 120, MaxSeconds = 150, OnlyOn = "1.7")]
        static void ShellingEnds(ScenarioContext s) => AwayFor(s, SiteKinds[1], true);

        [Scenario("battle-camp-ending-while-the-player-is-away-removes-the-site", MapSize = 120, MaxSeconds = 150, OnlyOn = "1.7")]
        static void CampEnds(ScenarioContext s) => AwayFor(s, SiteKinds[2], true);
    }
}
