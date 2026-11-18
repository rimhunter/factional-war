using System.IO;
using System.Linq;
using System.Xml;
using RimWorld;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class IdentityTranslations
    {
        const string OurPackageId = "rimhunter.factionalwar";
        static readonly string[] Folders = { "ChineseSimplified", "ChineseTraditional", "English", "French", "German", "Japanese", "Korean", "Polish", "PortugueseBrazilian", "Russian", "SpanishLatin" };

        static string Legacy(string folderName) {
            var bracket = folderName.IndexOf('(');
            return (bracket < 0 ? folderName : folderName.Substring(0, bracket)).Trim();
        }

        static XmlElement[] Entries(string path) {
            var doc = new XmlDocument();
            doc.Load(path);
            return doc.DocumentElement.ChildNodes.OfType<XmlElement>().ToArray();
        }

        [Scenario("every-existing-translation-loads-with-no-error", MapSize = 30, MaxSeconds = 120)]
        static void EveryTranslationLoads(ScenarioContext s) {
            var mod = ModLister.GetActiveModWithIdentifier(OurPackageId, ignorePostfix: true);
            if (mod == null) throw new ScenarioFailure("the mod " + OurPackageId + " is not active");
            var root = mod.RootDir.FullName;
            var loaded = LanguageDatabase.AllLoadedLanguages.ToList();
            foreach (var folder in Folders) {
                var lang = loaded.SingleOrDefault(l => Legacy(l.folderName) == folder);
                if (lang == null) {
                    s.Fail("the game has no language for the folder Languages/" + folder + "; it has: " + string.Join(", ", loaded.Select(l => l.folderName)));
                    continue;
                }
                lang.LoadData();
                s.Check(lang.loadErrors.Count == 0, folder + " loaded with errors: " + string.Join(" | ", lang.loadErrors));
                s.Check(!lang.anyKeyedReplacementsXmlParseError, folder + " has a Keyed file that does not parse: " + lang.lastKeyedReplacementsXmlParseErrorInFile);
                s.Check(!lang.anyDefInjectionsXmlParseError, folder + " has a DefInjected file that does not parse: " + lang.lastDefInjectionsXmlParseErrorInFile);

                var keyed = Path.Combine(root, "Languages", folder, "Keyed");
                var keys = Directory.GetFiles(keyed, "*.xml").SelectMany(Entries).ToList();
                s.Check(keys.Count > 0, folder + " should ship Keyed entries");
                foreach (var e in keys)
                    s.Check(lang.keyedReplacements.TryGetValue(e.Name, out var r) && r.value == e.InnerText.Replace("\\n", "\n"),
                        folder + " did not load the key " + e.Name + " from its Keyed files");

                var injected = Path.Combine(root, "Languages", folder, "DefInjected");
                if (!Directory.Exists(injected)) continue;
                foreach (var typeFolder in Directory.GetDirectories(injected)) {
                    var typeName = Path.GetFileName(typeFolder);
                    var package = lang.defInjections.SingleOrDefault(p => p.defType.Name == typeName);
                    s.Check(package != null, folder + " has the DefInjected folder " + typeName + ", but the game holds no injections for that def type");
                    if (package == null) continue;
                    foreach (var file in Directory.GetFiles(typeFolder, "*.xml", SearchOption.AllDirectories))
                    foreach (var e in Entries(file)) {
                        DefInjectionPackage.DefInjection injection = null;
                        s.Check(package.injections.TryGetValue(e.Name, out injection) && injection.injection == e.InnerText,
                            folder + " did not load the DefInjected key " + typeName + "/" + e.Name + "; the game holds: " + (injection == null ? "nothing" : injection.injection));
                        var defName = e.Name.Split('.')[0];
                        s.Check(GenDefDatabase.GetDefSilentFail(package.defType, defName, false) != null, folder + " translates " + typeName + " " + defName + ", which no def has");
                    }
                }
            }
        }
    }
}
