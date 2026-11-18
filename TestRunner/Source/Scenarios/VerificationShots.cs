using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using static RimhunterTestRunner.Scenarios.TranslationSupport;

namespace RimhunterTestRunner.Scenarios
{
    static class VerificationShots
    {
        const int CellsAcross = 40;

        static IEnumerator ShootMap(ScenarioContext s, Map map, string name, Action<ScenarioContext.Shot> check) {
            var onMap = new ScenarioContext(map, 120, s.PlayerLogPath);
            var shot = onMap.Screenshot(name);
            yield return shot;
            foreach (var failure in onMap.Failures) s.Fail(failure);
            s.ScreenshotFiles.AddRange(onMap.ScreenshotFiles);
            check(shot);
        }

        static bool OnScreen(ScenarioContext s, IntVec3 cell) {
            var p = s.ScreenPixelOf(cell);
            return p.x > 40f && p.y > 40f && p.x < UnityEngine.Screen.width - 40f && p.y < UnityEngine.Screen.height - 120f;
        }

        static List<Pawn> Standing(Map map, Faction faction) =>
            map.mapPawns.SpawnedPawnsInFaction(faction).Where(BattleHelpers.Standing).ToList();

        static (Pawn A, Pawn B) ClosestOpposed(Map map) {
            var fighters = BattleHelpers.Combatants(map).Where(p => p.Spawned && BattleHelpers.Standing(p) && !p.Faction.Hidden).ToList();
            (Pawn A, Pawn B) best = (null, null);
            var distance = float.MaxValue;
            foreach (var a in fighters)
                foreach (var b in fighters) {
                    if (a.Faction == b.Faction || !BattleHelpers.AtWarByGame(a.Faction, b.Faction)) continue;
                    var d = a.Position.DistanceTo(b.Position);
                    if (d < distance) { distance = d; best = (a, b); }
                }
            return best;
        }

        static bool PairIsClose((Pawn A, Pawn B) pair) => pair.A != null && pair.A.Position.DistanceTo(pair.B.Position) <= 12f;

        static IEnumerator PlaceCamera(ScenarioContext s, IntVec3 cell, string name) {
            var placed = false;
            for (var attempt = 0; attempt < 10 && !placed; attempt++) {
                s.LookAt(cell, CellsAcross);
                yield return ScenarioContext.Frames(5);
                s.LookAt(cell, CellsAcross);
                yield return ScenarioContext.Frames(5);
                placed = OnScreen(s, cell);
            }
            s.Check(placed, name + ": the camera should show " + cell + " after being placed on it; it shows " + Find.CameraDriver.MapPosition);
        }

        static IEnumerator BattleInProgress(ScenarioContext s, Map map, string name, Func<Pawn, bool> sideOne, Func<Pawn, bool> sideTwo, string sideOneLabel, string sideTwoLabel) {
            var started = s.RunUntil(() => BattleHelpers.Hurt(map) >= 2 && PairIsClose(ClosestOpposed(map)), 20000);
            var pair = ClosestOpposed(map);
            var state = string.Join("; ", BattleHelpers.Sides(map).Select(kv => kv.Key.Name + " standing " + kv.Value.Count(BattleHelpers.Standing) + " of " + kv.Value.Count)) + "; closest opposed pair " + (pair.A == null ? "none" : pair.A.Position.DistanceTo(pair.B.Position).ToString("0") + " cells apart");
            s.Check(started, name + ": the battle should be in progress (two combatants hurt, two opposed pawns standing within 12 cells of each other) within 20000 ticks; hurt " + BattleHelpers.Hurt(map) + "; " + state);
            if (pair.A == null) yield break;
            var middle = new IntVec3((pair.A.Position.x + pair.B.Position.x) / 2, 0, (pair.A.Position.z + pair.B.Position.z) / 2);
            var edgeScroll = Prefs.EdgeScreenScroll;
            Prefs.EdgeScreenScroll = false;
            try {
                CameraJumper.TryHideWorld();
                for (var i = 0; i < 300 && WorldRendererUtility.WorldRendered; i++) yield return null;
                s.Check(!WorldRendererUtility.WorldRendered, name + ": the world view should be closed before the camera is placed");
                yield return ScenarioContext.Frames(10);
                yield return PlaceCamera(s, middle, name);
                var fighters = BattleHelpers.Combatants(map).Where(p => p.Spawned && BattleHelpers.Standing(p)).ToList();
                var one = fighters.Where(sideOne).ToList();
                var two = fighters.Where(sideTwo).ToList();
                s.Metric(name + ": standing " + sideOneLabel + " pawns", one.Count);
                s.Metric(name + ": standing " + sideTwoLabel + " pawns", two.Count);
                yield return ShootMap(s, map, name, shot => {
                    var onOne = one.Count(p => OnScreen(s, p.Position));
                    var onTwo = two.Count(p => OnScreen(s, p.Position));
                    s.Metric(name + ": " + sideOneLabel + " pawns on screen", onOne);
                    s.Metric(name + ": " + sideTwoLabel + " pawns on screen", onTwo);
                    var camera = "camera at " + Find.CameraDriver.MapPosition + ", wanted " + middle + ", pair " + pair.A.Position + " and " + pair.B.Position + ", map " + map.Size + ", current map is the site map " + (Find.CurrentMap == map) + ", game speed " + Find.TickManager.CurTimeSpeed;
                    s.Check(onOne >= 1, name + ": at least one standing " + sideOneLabel + " pawn should be on screen; on screen: " + onOne + "; " + camera);
                    s.Check(onTwo >= 1, name + ": at least one standing " + sideTwoLabel + " pawn should be on screen; on screen: " + onTwo);
                    s.Check(BattleHelpers.Hurt(map) >= 2, name + ": the shot should be taken with the battle in progress and combatants hurt");
                });
            } finally {
                Prefs.EdgeScreenScroll = edgeScroll;
            }
        }

        static IEnumerator OneSite(ScenarioContext s, (string Incident, string WorldObject) kind, string name) {
            var site = BattleHelpers.FireSiteIncident(s, kind.Incident, kind.WorldObject);
            s.Check(site != null, kind.WorldObject + ": the site should exist");
            if (site == null) yield break;
            Caravan caravan = null;
            try {
                var map = BattleHelpers.ArriveByCaravan(s, site, out caravan);
                Current.Game.CurrentMap = map;
                var sides = BattleHelpers.Sides(map);
                s.AreEqual(2, sides.Count, name + ": two sides should be on the site map; found " + BattleHelpers.Describe(sides));
                if (sides.Count != 2) yield break;
                var factions = sides.Keys.ToList();
                yield return BattleInProgress(s, map, name, p => p.Faction == factions[0], p => p.Faction == factions[1], factions[0].Name, factions[1].Name);
            } finally {
                BattleHelpers.Cleanup(s, site, caravan);
            }
        }

        [Scenario("screenshot-battle-site-contention-in-progress-with-both-sides", MapSize = 80, Windowed = true, MaxSeconds = 150)]
        static IEnumerator ContentionInProgress(ScenarioContext s) {
            yield return OneSite(s, PlayerSideHelpers.SiteKinds[0], "battle-site-contention-in-progress");
        }

        [Scenario("screenshot-battle-site-shelling-in-progress-with-both-sides", MapSize = 80, Windowed = true, MaxSeconds = 150)]
        static IEnumerator ShellingInProgress(ScenarioContext s) {
            yield return OneSite(s, PlayerSideHelpers.SiteKinds[1], "battle-site-shelling-in-progress");
        }

        [Scenario("screenshot-battle-site-camp-in-progress-with-both-sides", MapSize = 80, Windowed = true, MaxSeconds = 165)]
        static IEnumerator CampInProgress(ScenarioContext s) {
            yield return OneSite(s, PlayerSideHelpers.SiteKinds[2], "battle-site-camp-in-progress");
        }

        [Scenario("screenshot-faction-assault-in-progress-with-both-sides-and-its-letter", MapSize = 120, Windowed = true, MaxSeconds = 150)]
        static IEnumerator AssaultInProgress(ScenarioContext s) {
            var lettersBefore = Find.LetterStack.LettersListForReading.Count;
            var fired = DefDatabase<IncidentDef>.GetNamed("SrFactionWar").Worker.TryExecute(new IncidentParms { target = s.Map, points = 1500f });
            s.Check(fired, "the Faction Assault incident should fire");
            if (!fired) yield break;
            var factions = BattleHelpers.Combatants(s.Map).Select(p => p.Faction).Distinct().ToList();
            s.AreEqual(2, factions.Count, "two NPC factions should fight on the map, got " + BattleHelpers.Names(factions));
            if (factions.Count != 2) yield break;
            s.Check(BattleHelpers.AtWarByGame(factions[0], factions[1]), "the two factions should be at war by the game's own relations; " + BattleHelpers.Goodwill(factions[0], factions[1]));
            var letter = BattleHelpers.NewLetters(lettersBefore).FirstOrDefault(l => factions.All(f => l.Label.ToString().Contains(f.Name)));
            s.Check(letter != null, "the player should get a letter naming both factions");
            yield return BattleInProgress(s, s.Map, "faction-assault-in-progress", p => p.Faction == factions[0], p => p.Faction == factions[1], factions[0].Name, factions[1].Name);
        }

        [Scenario("screenshot-loot-lies-on-the-site-map-after-a-battle", MapSize = 80, Windowed = true, MaxSeconds = 165)]
        static IEnumerator LootAfterBattle(ScenarioContext s) {
            var run = PlayerSideHelpers.Start(s, PlayerSideHelpers.SiteKinds[1], false);
            try {
                if (run == null || run.Sides.Count != 2) yield break;
                var map = run.Map;
                var factions = run.Sides.Keys.ToList();
                s.RunUntil(() => BattleHelpers.Hurt(map) >= 2 && factions.All(f => Standing(map, f).Count >= 6), 20000);
                var pair = ClosestOpposed(map);
                s.Check(BattleHelpers.Hurt(map) >= 2, "the battle should have left at least 2 pawns downed or dead within 20000 ticks; hurt " + BattleHelpers.Hurt(map) + "; " + string.Join("; ", factions.Select(f => f.Name + " standing " + Standing(map, f).Count + " at war with the other side " + BattleHelpers.AtWarByGame(f, factions.First(o => o != f)))) + "; closest opposed pair " + (pair.A == null ? "none" : pair.A.Position.DistanceTo(pair.B.Position).ToString("0") + " cells apart; jobs " + pair.A.CurJob?.def.defName + " and " + pair.B.CurJob?.def.defName) + "; toils " + string.Join(", ", map.lordManager.lords.Select(l => l.faction?.Name + ":" + l.CurLordToil?.GetType().Name + ":" + l.ownedPawns.Count)) + "; jobs " + string.Join(", ", Standing(map, factions[0]).Concat(Standing(map, factions[1])).GroupBy(p => p.CurJob?.def.defName ?? "none").Select(g => g.Key + "=" + g.Count())));
                var killedKinds = string.Join(", ", factions.SelectMany(f => map.mapPawns.SpawnedPawnsInFaction(f)).GroupBy(p => p.kindDef.defName + (p.RaceProps.Humanlike ? "" : "(not humanlike)")).Select(g => g.Key + "=" + g.Count()));
                foreach (var side in factions) PlayerSideHelpers.KillSide(map, side, null);
                s.Tick(60);
                var corpses = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>().Where(c => c.Spawned && c.InnerPawn.Faction != null && factions.Contains(c.InnerPawn.Faction)).ToList();
                var weapons = map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon).Where(t => t.Spawned).ToList();
                s.Check(corpses.Count >= 2, "corpses of the fallen should lie on the map; found " + corpses.Count + "; killed " + killedKinds + "; corpses of any faction on the map " + map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).Count);
                s.Check(weapons.Count >= 2, "dropped weapons should lie on the map; found " + weapons.Count);
                if (corpses.Count == 0) yield break;
                var anchor = corpses.OrderByDescending(c => corpses.Count(o => o.Position.DistanceTo(c.Position) < 12f)).First();
                var cell = GenRadial.RadialCellsAround(anchor.Position, 8f, true).First(c => c.InBounds(map) && c.Standable(map) && c.GetFirstThing<Corpse>(map) == null);
                var colonist = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                GenSpawn.Spawn(colonist, cell, map);
                CameraJumper.TryHideWorld();
                for (var i = 0; i < 300 && WorldRendererUtility.WorldRendered; i++) yield return null;
                yield return ScenarioContext.Frames(10);
                var edgeScroll = Prefs.EdgeScreenScroll;
                Prefs.EdgeScreenScroll = false;
                try {
                    yield return PlaceCamera(s, anchor.Position, "battle-site-loot-after-the-battle");
                    yield return ShootMap(s, map, "battle-site-loot-after-the-battle", shot => {
                        var nearby = corpses.Count(c => OnScreen(s, c.PositionHeld));
                        var nearbyWeapons = weapons.Count(w => OnScreen(s, w.PositionHeld));
                        s.Check(nearby >= 2, "at least two corpses should be on screen; on screen: " + nearby);
                        s.Check(nearbyWeapons >= 2, "at least two dropped weapons should be on screen; on screen: " + nearbyWeapons);
                        s.Check(OnScreen(s, colonist.Position), "the player's pawn should be on screen near the loot");
                    });
                } finally {
                    Prefs.EdgeScreenScroll = edgeScroll;
                }
            } finally {
                PlayerSideHelpers.Finish(s, run);
            }
        }

        [Scenario("screenshot-empire-battle-in-progress-with-both-sides", MapSize = 120, Windowed = true, OnlyOn = "1.7", MaxSeconds = 160, Mods = "ludeon.rimworld.royalty")]
        static IEnumerator EmpireBattle(ScenarioContext s) {
            using (var scope = new DlcHelpers.FactionScope("Empire", "Pirate")) {
                var empire = DlcHelpers.FactionOf("Empire");
                var pirates = DlcHelpers.FactionOf("Pirate");
                s.Check(DefDatabase<IncidentDef>.GetNamed("SrFactionWar").Worker.TryExecute(new IncidentParms { target = s.Map, points = 2500f }), "the Faction Assault incident should fire");
                s.Check(DlcHelpers.Spawned(s.Map, empire).Count > 0 && DlcHelpers.Spawned(s.Map, pirates).Count > 0, "the Empire and the Pirates should both have pawns on the map");
                yield return BattleInProgress(s, s.Map, "empire-battle-in-progress", p => p.Faction == empire, p => p.Faction == pirates, "Empire", "Pirates");
            }
            s.RequireNoLogErrors();
        }

        [Scenario("screenshot-shambler-battle-in-progress-with-both-sides", MapSize = 120, Windowed = true, OnlyOn = "1.7", MaxSeconds = 160, Mods = "ludeon.rimworld.anomaly")]
        static IEnumerator ShamblerBattle(ScenarioContext s) {
            using (var scope = new DlcHelpers.FactionScope("Pirate", "RimhunterTestShamblerCult")) {
                var cult = scope.Add("RimhunterTestShamblerCult");
                var pirates = DlcHelpers.FactionOf("Pirate");
                s.Check(DefDatabase<IncidentDef>.GetNamed("SrFactionWar").Worker.TryExecute(new IncidentParms { target = s.Map, points = 1500f }), "the Faction Assault incident should fire");
                var shamblers = DlcHelpers.Spawned(s.Map, cult);
                s.Check(shamblers.Count > 0 && shamblers.All(p => p.IsMutant), "the shambler faction should have shamblers on the map");
                yield return BattleInProgress(s, s.Map, "shambler-battle-in-progress", p => p.Faction == cult && p.IsMutant, p => p.Faction == pirates, "shambler", "Pirates");
            }
            s.RequireNoLogErrors();
        }

        static IEnumerator LetterShots(ScenarioContext s, LoadedLanguage lang, string prefix) {
            var kind = PlayerSideHelpers.SiteKinds[0];
            var labelKey = "SrLabelFactionWarContention";
            var textKey = "SrDescFactionWarContention";
            var lettersBefore = Find.LetterStack.LettersListForReading.Count;
            var site = BattleHelpers.FireSiteIncident(s, kind.Incident, kind.WorldObject);
            s.Check(site != null, "the contention site should exist");
            if (site == null) yield break;
            try {
                var expectedLabel = ExpectedKeyed(lang, labelKey);
                var expectedText = ExpectedKeyed(lang, textKey);
                var letter = BattleHelpers.NewLetters(lettersBefore).FirstOrDefault(l => l.Label.ToString() == expectedLabel) as ChoiceLetter;
                s.Check(letter != null, prefix + ": the letter should be titled '" + expectedLabel + "'");
                if (letter == null) yield break;
                s.AreEqual(expectedText, letter.Text.ToString(), prefix + " letter text");
                yield return s.Screenshot(prefix + "-letter-in-the-letter-stack").Then(s, shot =>
                    s.Check(shot.ShowsText(expectedLabel), prefix + ": the letter stack should draw '" + expectedLabel + "'; drawn: " + shot.TextsMatching(expectedLabel.Substring(0, Math.Min(6, expectedLabel.Length)))));
                letter.OpenLetter();
                yield return ScenarioContext.Frames(5);
                var firstWords = expectedText.Substring(0, Math.Min(24, expectedText.Length));
                yield return s.Screenshot(prefix + "-letter-opened").Then(s, shot => {
                    s.Check(shot.ShowsText(firstWords), prefix + ": the opened letter should draw its text starting '" + firstWords + "'; drawn: " + shot.TextsMatching(firstWords));
                    s.Check(shot.Texts.All(t => !LooksLikeMissingKey(t.Text)), prefix + ": no drawn text should be a missing-key marker");
                });
                foreach (var window in Find.WindowStack.Windows.ToList()) Find.WindowStack.TryRemove(window, doCloseSound: false);
            } finally {
                if (!site.Destroyed) site.Destroy();
            }
        }

        [Scenario("screenshot-letter-in-the-stack-and-opened-in-english", MapSize = 80, Windowed = true, MaxSeconds = 120)]
        static IEnumerator EnglishLetter(ScenarioContext s) {
            yield return LetterShots(s, LanguageDatabase.defaultLanguage, "english");
        }

        [Scenario("screenshot-letter-in-the-stack-and-opened-in-russian", MapSize = 80, Windowed = true, MaxSeconds = 120)]
        static IEnumerator RussianLetter(ScenarioContext s) {
            var lang = LanguageNamed("Russian");
            s.Check(ExpectedKeyed(lang, "SrLabelFactionWarContention") != ExpectedKeyed(LanguageDatabase.defaultLanguage, "SrLabelFactionWarContention"), "the Russian letter title should differ from the English one");
            yield return InLanguage("Russian", LetterShots(s, lang, "russian"));
        }
    }

    static class ShotExtensions
    {
        public static IEnumerator Then(this ScenarioContext.Shot shot, ScenarioContext s, Action<ScenarioContext.Shot> check) {
            yield return shot;
            check(shot);
        }
    }
}
