using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using RimWorld;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class TranslationSupport
    {
        public const string OurPackageId = "rimhunter.factionalwar";
        public static readonly string[] CoreKeys = {
            "MessageRaidersLeaving", "EnemyRaidLeaderPresent", "LetterRelatedPawnsRaidEnemy",
            "CommandFormCaravan", "CommandFormCaravanDesc", "CommandReformCaravan",
            "CommandReformCaravanDesc", "CommandReformCaravanFailHostilePawns",
        };

        public static string Legacy(string folderName) {
            var bracket = folderName.IndexOf('(');
            return (bracket < 0 ? folderName : folderName.Substring(0, bracket)).Trim();
        }

        public static LoadedLanguage LanguageNamed(string folder) {
            var lang = LanguageDatabase.AllLoadedLanguages.SingleOrDefault(l => Legacy(l.folderName) == folder);
            if (lang == null) throw new ScenarioFailure("the game has no language for " + folder);
            return lang;
        }

        static void Switch(LoadedLanguage lang) {
            lang.LoadData();
            LanguageDatabase.activeLanguage = lang;
        }

        static void RequireLanguageShown(string folder, string englishCancel) {
            if (LanguageDatabase.activeLanguage != LanguageNamed(folder)) throw new ScenarioFailure("the active language is not " + folder);
            if (folder != "English" && "Cancel".Translate().ToString() == englishCancel) throw new ScenarioFailure("the game still shows English core text in " + folder);
        }

        public static void InLanguage(string folder, Action<LoadedLanguage> body) {
            var lang = LanguageNamed(folder);
            var englishCancel = "Cancel".Translate().ToString();
            Switch(lang);
            try {
                RequireLanguageShown(folder, englishCancel);
                body(lang);
            } finally {
                Switch(LanguageDatabase.defaultLanguage);
            }
        }

        public static IEnumerator InLanguage(string folder, IEnumerator body) {
            var lang = LanguageNamed(folder);
            var englishCancel = "Cancel".Translate().ToString();
            Switch(lang);
            try {
                RequireLanguageShown(folder, englishCancel);
                while (body.MoveNext()) yield return body.Current;
            } finally {
                Switch(LanguageDatabase.defaultLanguage);
            }
        }

        static ModContentPack Package(string packageId) {
            var pack = LoadedModManager.RunningModsListForReading.FirstOrDefault(m => string.Equals(m.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
            if (pack == null) throw new ScenarioFailure("the mod " + packageId + " is not running");
            return pack;
        }

        static string LanguagesDir(string packageId) => Path.Combine(Package(packageId).RootDir, "Languages");

        static void AddEntries(Dictionary<string, string> into, XmlDocument doc) {
            foreach (var e in doc.DocumentElement.ChildNodes.OfType<XmlElement>()) {
                var text = e.InnerText.Replace("\\n", "\n");
                if (text.Length > 0 && text != "TODO") into[e.Name] = text;
            }
        }

        static Dictionary<string, string> InFolder(string folder, string subFolder) {
            var texts = new Dictionary<string, string>();
            var dir = Path.Combine(folder, subFolder);
            if (!Directory.Exists(dir)) return texts;
            foreach (var file in Directory.GetFiles(dir, "*.xml", SearchOption.AllDirectories)) {
                var doc = new XmlDocument();
                doc.Load(file);
                AddEntries(texts, doc);
            }
            return texts;
        }

        static bool ReadBlock(Stream stream, byte[] block) {
            var read = 0;
            while (read < block.Length) {
                var n = stream.Read(block, read, block.Length - read);
                if (n <= 0) return false;
                read += n;
            }
            return true;
        }

        static Dictionary<string, string> KeyedInTar(string tarPath) {
            var texts = new Dictionary<string, string>();
            using (var stream = File.OpenRead(tarPath)) {
                var header = new byte[512];
                while (ReadBlock(stream, header)) {
                    if (header.All(b => b == 0)) break;
                    var name = Encoding.UTF8.GetString(header, 0, 100).TrimEnd('\0');
                    var size = Convert.ToInt64(Encoding.ASCII.GetString(header, 124, 11).Trim('\0', ' '), 8);
                    var body = new byte[size];
                    var read = 0;
                    while (read < size) {
                        var n = stream.Read(body, read, (int)size - read);
                        if (n <= 0) throw new ScenarioFailure("the archive " + tarPath + " ends inside " + name);
                        read += n;
                    }
                    stream.Seek((512 - (int)(size % 512)) % 512, SeekOrigin.Current);
                    if (name.StartsWith("Keyed/") && name.EndsWith(".xml")) {
                        var doc = new XmlDocument();
                        using (var memory = new MemoryStream(body)) doc.Load(memory);
                        AddEntries(texts, doc);
                    }
                }
            }
            return texts;
        }

        public static Dictionary<string, string> ModKeyed(string legacyFolder) =>
            InFolder(Path.Combine(LanguagesDir(OurPackageId), legacyFolder), "Keyed");

        public static Dictionary<string, string> ModDefInjected(string legacyFolder, string defType) =>
            InFolder(Path.Combine(LanguagesDir(OurPackageId), legacyFolder, "DefInjected"), defType);

        public static Dictionary<string, string> CoreKeyed(string languageFolderName) {
            var languages = LanguagesDir("ludeon.rimworld");
            var tar = Path.Combine(languages, languageFolderName + ".tar");
            return File.Exists(tar) ? KeyedInTar(tar) : InFolder(Path.Combine(languages, languageFolderName), "Keyed");
        }

        public const string FixturePackageId = "rimhunter.factionalwar.germanfixture";

        public static string ExpectedKeyed(LoadedLanguage lang, string key) {
            if (LoadedModManager.RunningModsListForReading.Any(m => string.Equals(m.PackageId, FixturePackageId, StringComparison.OrdinalIgnoreCase))) {
                var fixture = InFolder(Path.Combine(LanguagesDir(FixturePackageId), Legacy(lang.folderName)), "Keyed");
                if (fixture.TryGetValue(key, out var fixtureText)) return fixtureText;
            }
            var own = ModKeyed(Legacy(lang.folderName));
            if (own.TryGetValue(key, out var text)) return text;
            var english = ModKeyed("English");
            if (english.TryGetValue(key, out var fallback)) return fallback;
            throw new ScenarioFailure("neither the " + lang.folderName + " nor the English files on disk hold a text for " + key);
        }

        public static string ExpectedDef(LoadedLanguage lang, string defType, string entry) {
            var own = ModDefInjected(Legacy(lang.folderName), defType);
            if (own.TryGetValue(entry, out var text)) return text;
            var english = ModDefInjected("English", defType);
            if (english.TryGetValue(entry, out var fallback)) return fallback;
            throw new ScenarioFailure("neither the " + lang.folderName + " nor the English DefInjected files on disk hold " + defType + "/" + entry);
        }

        public static bool LooksLikeMissingKey(string drawn) =>
            System.Text.RegularExpressions.Regex.IsMatch(drawn, @"\bSr[A-Z][A-Za-z]+\b|Translation missing|\bTODO\b|NoTranslation|⸨|⸩");
    }
}
