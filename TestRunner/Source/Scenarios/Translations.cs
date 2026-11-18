using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using static RimhunterTestRunner.Scenarios.TranslationSupport;

namespace RimhunterTestRunner.Scenarios
{
    static class Translations
    {
        const string ModType = "SR.ModRimWorld.FactionalWar.SettingWindow";

        static void CheckKeys(ScenarioContext s, LoadedLanguage lang) {
            var english = ModKeyed("English");
            s.Check(english.Count >= 15, "the English Keyed files should hold at least 15 keys, found " + english.Count);
            foreach (var key in english.Keys) {
                var shown = key.Translate().ToString();
                s.Check(!LooksLikeMissingKey(shown) && shown != key, lang.folderName + " draws a missing-key marker or the raw key for " + key + ": " + shown);
                s.AreEqual(ExpectedKeyed(lang, key), shown, lang.folderName + " text for " + key + " (read from the mod's language files on disk: the language's own, else English)");
            }
            var core = CoreKeyed(lang.folderName);
            var coreEnglish = CoreKeyed("English");
            foreach (var key in CoreKeys) {
                var shown = key.Translate().ToString();
                s.Check(shown != key && !LooksLikeMissingKey(shown), lang.folderName + " draws a missing-key marker or the raw key for the Core key " + key + ": " + shown);
                var expected = core.TryGetValue(key, out var own) ? own : coreEnglish.TryGetValue(key, out var fallback) ? fallback : null;
                s.Check(expected != null, "neither Core's " + lang.folderName + " nor its English files hold a text for " + key);
                if (expected != null) s.AreEqual(expected, shown, lang.folderName + " text for the Core key " + key + " (read from Core's language files on disk)");
            }
        }

        static void Each(ScenarioContext s, params string[] folders) {
            foreach (var folder in folders) InLanguage(folder, lang => CheckKeys(s, lang));
            s.RequireNoLogErrors();
        }

        [Scenario("translation-english-keys-draw-their-english-text", MapSize = 30, MaxSeconds = 120)]
        static void English(ScenarioContext s) => Each(s, "English");

        [Scenario("translation-chinese-japanese-korean-and-russian-keys-draw-their-text-from-the-files", MapSize = 30, MaxSeconds = 150)]
        static void FirstGroup(ScenarioContext s) => Each(s, "ChineseSimplified", "ChineseTraditional", "Japanese", "Korean", "Russian");

        [Scenario("translation-french-german-polish-and-portuguese-keys-draw-their-text-from-the-files", MapSize = 30, MaxSeconds = 150)]
        static void SecondGroup(ScenarioContext s) => Each(s, "French", "German", "Polish", "PortugueseBrazilian", "SpanishLatin");

        static readonly (string Incident, string WorldObject, string Part, string LabelKey, string TextKey)[] Sites = {
            ("SrFactionWarContentionSiteGenerate", "SrSiteFactionWarContention", "SrFactionContention", "SrLabelFactionWarContention", "SrDescFactionWarContention"),
            ("SrFactionWarShellingSiteGenerate", "SrSiteFactionWarShelling", "SrFactionWarShelling", "SrLabelFactionWarShelling", "SrDescFactionWarShelling"),
            ("SrFactionWarTempCampSiteGenerate", "SrSiteFactionWarTempCamp", "SrFactionTempCamp", "SrLabelFactionWarTempCamp", "SrDescFactionWarTempCamp"),
        };

        static void CheckLetters(ScenarioContext s, LoadedLanguage lang) {
            foreach (var site in Sites) {
                var lettersBefore = Find.LetterStack.LettersListForReading.Count;
                var worldObject = BattleHelpers.FireSiteIncident(s, site.Incident, site.WorldObject);
                s.Check(worldObject != null, "the world object " + site.WorldObject + " should be on the world map");
                if (worldObject == null) continue;
                try {
                    var letters = BattleHelpers.NewLetters(lettersBefore);
                    var expectedLabel = ExpectedKeyed(lang, site.LabelKey);
                    var expectedText = ExpectedKeyed(lang, site.TextKey);
                    var letter = letters.FirstOrDefault(l => l.Label.ToString() == expectedLabel);
                    s.Check(letter != null, lang.folderName + ": the letter title should be '" + expectedLabel + "' (the files on disk); letters: " + string.Join(" | ", letters.Select(l => l.Label.ToString())));
                    if (letter is ChoiceLetter choice) s.AreEqual(expectedText, choice.Text.ToString(), lang.folderName + " letter text for " + site.TextKey);
                    foreach (var l in letters) {
                        if (l is ChoiceLetter text) s.Check(!LooksLikeMissingKey(l.Label.ToString()) && !LooksLikeMissingKey(text.Text.ToString()), lang.folderName + ": a letter shows a missing-key marker or raw key: " + l.Label);
                        else s.Fail(lang.folderName + ": the letter '" + l.Label + "' is a " + l.GetType().Name + ", not a ChoiceLetter, so its text cannot be checked");
                    }

                    var part = DefDatabase<SitePartDef>.GetNamed(site.Part);
                    s.AreEqual(ExpectedDef(lang, "SitePartDef", site.Part + ".label"), part.label, lang.folderName + " label of " + site.Part + " (DefInjected files on disk, else English)");
                    s.AreEqual(ExpectedDef(lang, "SitePartDef", site.Part + ".description"), part.description, lang.folderName + " description of " + site.Part);
                    s.AreEqual(ExpectedDef(lang, "SitePartDef", site.Part + ".arrivedLetter"), part.arrivedLetter, lang.folderName + " arrival letter of " + site.Part);
                    s.AreEqual(ExpectedDef(lang, "SitePartDef", site.Part + ".label"), worldObject.Label, lang.folderName + " label of the world object " + site.WorldObject + " on the map");
                } finally {
                    if (!worldObject.Destroyed) worldObject.Destroy();
                }
            }
            var job = DefDatabase<JobDef>.GetNamed("SrKillMelee");
            s.AreEqual(ExpectedDef(lang, "JobDef", "SrKillMelee.reportString"), job.reportString, lang.folderName + " report string of SrKillMelee");
            var strategy = DefDatabase<RaidStrategyDef>.GetNamed("SrFactionFirst");
            s.AreEqual(ExpectedDef(lang, "RaidStrategyDef", "SrFactionFirst.letterLabelEnemy"), strategy.letterLabelEnemy, lang.folderName + " letter label of the Faction Assault strategy");
            s.AreEqual(ExpectedDef(lang, "RaidStrategyDef", "SrFactionFirst.arrivalTextEnemy"), strategy.arrivalTextEnemy, lang.folderName + " arrival text of the Faction Assault strategy");
            var arrival = DefDatabase<PawnsArrivalModeDef>.GetNamed("SrTwoFactionsEdgeWalkIn");
            s.AreEqual(ExpectedDef(lang, "PawnsArrivalModeDef", "SrTwoFactionsEdgeWalkIn.textEnemy"), arrival.textEnemy, lang.folderName + " arrival text of the walk-in mode");
        }

        [Scenario("translation-booted-language-letters-and-labels-come-from-the-language-files", MapSize = 80, MaxSeconds = 150, OnlyOn = "1.7", SelfTest = true)]
        static void BootedLanguageLetters(ScenarioContext s) {
            var lang = LanguageDatabase.activeLanguage;
            if (lang == LanguageDatabase.defaultLanguage) throw new ScenarioFailure("the game must be started in a language other than English: run scripts/test-translations, which passes --language");
            CheckLetters(s, lang);
        }

        const string GermanFixture = "TestRunner/Fixtures/factional-war-german";

        static Mod FactionalWar => LoadedModManager.ModHandles.Single(m => m.GetType().FullName == ModType);

        static IEnumerator SettingsWindowBody(ScenarioContext s, LoadedLanguage lang) {
            var dialog = new Dialog_ModSettings(FactionalWar);
            Find.WindowStack.Add(dialog);
            yield return s.DrawFrame();
            var opened = s.DrawFrame();
            yield return opened;
            var reset = opened.Texts.FirstOrDefault(t => t.Text == ExpectedKeyed(lang, "SrSettingsDefault"));
            s.Check(reset != null, lang.folderName + ": the settings window should draw the Default button '" + ExpectedKeyed(lang, "SrSettingsDefault") + "' (an earlier scenario in the launch may have changed the settings)");
            if (reset != null) yield return s.Click(reset);
            for (var i = 0; i < 3; i++) yield return s.DrawFrame();
            var frame = s.DrawFrame();
            yield return frame;
            var drawn = frame.Texts.Select(t => t.Text).ToList();
            var all = string.Join(" | ", drawn);
            var expected = new Dictionary<string, string> {
                ["the settings category (the window title)"] = ExpectedKeyed(lang, "SrSettingsCategory"),
                ["the checkbox"] = ExpectedKeyed(lang, "SrNeedOptimization"),
                ["the slider label"] = ExpectedKeyed(lang, "SrThreatPointFactor") + ": 100%",
                ["the Default button"] = ExpectedKeyed(lang, "SrSettingsDefault"),
            };
            foreach (var pair in expected)
                s.Check(drawn.Contains(pair.Value), lang.folderName + ": the settings window should draw " + pair.Key + " as '" + pair.Value + "' (read from the files on disk). Drawn: " + all);
            s.Check(drawn.All(t => !LooksLikeMissingKey(t)), lang.folderName + ": the settings window draws a missing-key marker or a raw key. Drawn: " + all);
            Find.WindowStack.TryRemove(dialog, doCloseSound: false);
            yield return s.DrawFrame();
            s.RequireNoLogErrors();
        }

        [Scenario("translation-chinese-settings-window-draws-its-text-from-the-language-files", MapSize = 40, Windowed = true, OnlyOn = "1.7", MaxSeconds = 120)]
        static IEnumerator ChineseSettingsWindow(ScenarioContext s) => Windowed("ChineseSimplified", s);

        [Scenario("translation-polish-settings-window-falls-back-to-english-from-the-files", MapSize = 40, Windowed = true, OnlyOn = "1.7", MaxSeconds = 120)]
        static IEnumerator PolishSettingsWindow(ScenarioContext s) => Windowed("Polish", s);

        [Scenario("translation-german-settings-window-draws-the-keyed-texts-not-english-in-code", MapSize = 40, Windowed = true, OnlyOn = "1.7", MaxSeconds = 120, Mods = GermanFixture)]
        static IEnumerator GermanSettingsWindow(ScenarioContext s) => Windowed("German", s);

        [Scenario("translation-german-world-object-buttons-draw-the-keyed-texts", MapSize = 80, OnlyOn = "1.7", MaxSeconds = 150, Mods = GermanFixture)]
        static void GermanGizmos(ScenarioContext s) {
            InLanguage("German", lang => {
                var site = BattleHelpers.FireSiteIncident(s, "SrFactionWarContentionSiteGenerate", "SrSiteFactionWarContention");
                s.Check(site != null, "the contention site should be on the world map");
                if (site == null) return;
                var map = BattleHelpers.ArriveAt(site);
                var wasDevMode = Prefs.DevMode;
                try {
                    Prefs.DevMode = true;
                    var gizmos = site.GetGizmos().OfType<Command>().ToList();
                    var labels = gizmos.Select(g => g.LabelCap.ToString()).ToList();
                    var core = CoreKeyed(lang.folderName);
                    s.Check(labels.Contains(ExpectedKeyed(lang, "SrDevShowAvailableExits")), "the German world object buttons should include the keyed dev button '" + ExpectedKeyed(lang, "SrDevShowAvailableExits") + "'; buttons: " + string.Join(" | ", labels));
                    s.Check(labels.Contains(core["CommandReformCaravan"].CapitalizeFirst()), "the German world object buttons should include Core's German '" + core["CommandReformCaravan"] + "'; buttons: " + string.Join(" | ", labels));
                    s.Check(labels.All(l => !LooksLikeMissingKey(l)), "a world object button shows a missing-key marker or a raw key: " + string.Join(" | ", labels));
                    var disabled = gizmos.Where(c => c.Disabled).ToList();
                    s.Check(disabled.Count >= 1, "the reform caravan button should be disabled while the battle is on; buttons: " + string.Join(" | ", labels));
                    foreach (var g in disabled)
                        s.AreEqual(core["CommandReformCaravanFailHostilePawns"], g.disabledReason, "the reason the reform button is disabled while enemies are on the map (Core's German text on disk)");

                    foreach (var faction in BattleHelpers.NpcFactionsOnMap(map)) faction.TryAffectGoodwillWith(Faction.OfPlayer, 200, false, false, null, null);
                    foreach (var pawn in map.mapPawns.AllPawnsSpawned.Where(p => p.Faction != null && !p.Faction.IsPlayer && p.HostileTo(Faction.OfPlayer)).ToList()) pawn.Destroy();
                    s.Check(map.mapPawns.AllPawnsSpawned.Any(p => p.Faction != null && !p.Faction.IsPlayer), "a faction friendly to the player should still be on the map");
                    var quiet = site.GetGizmos().OfType<Command>().Where(c => c.Disabled).ToList();
                    s.Check(quiet.Count >= 1, "the reform caravan button should be disabled with the allies still fighting; buttons: " + string.Join(" | ", site.GetGizmos().OfType<Command>().Select(c => c.LabelCap.ToString() + (c.Disabled ? " (disabled: " + c.disabledReason + ")" : ""))));
                    foreach (var g in quiet)
                        s.AreEqual(ExpectedKeyed(lang, "SrCommandReformCaravanFailAlliesCollectingLoot"), g.disabledReason, "the reason the reform button is disabled while the allies collect loot (the mod's German text on disk)");
                } finally {
                    Prefs.DevMode = wasDevMode;
                    Current.Game.CurrentMap = s.Map;
                    if (Find.Maps.Contains(map)) Current.Game.DeinitAndRemoveMap(map, false);
                    if (!site.Destroyed) site.Destroy();
                }
            });
        }

        static IEnumerator Windowed(string folder, ScenarioContext s) =>
            InLanguage(folder, SettingsWindowBody(s, LanguageNamed(folder)));
    }
}
