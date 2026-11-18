using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace FactionalWar.Tests;

public class TranslationTests
{
    static readonly string Root = ModRoot.Path;

    static readonly string[] CoreKeys = {
        "MessageRaidersLeaving", "EnemyRaidLeaderPresent", "LetterRelatedPawnsRaidEnemy",
        "CommandFormCaravan", "CommandFormCaravanDesc", "CommandReformCaravan",
        "CommandReformCaravanDesc", "CommandReformCaravanFailHostilePawns",
    };

    static readonly string[] DefTextFields = {
        "label", "labelShort", "description", "reportString", "arrivedLetter", "letterLabelEnemy",
        "arrivalTextEnemy", "textEnemy", "textFriendly", "textWillArrive", "jobReportString",
    };

    static IEnumerable<string> SourceFiles() =>
        Directory.GetFiles(Path.Combine(Root, "Source"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(p => p is "obj" or "bin" or "Lib"));

    static Dictionary<string, string> EnglishKeyed() =>
        Directory.GetFiles(Path.Combine(Root, "Languages", "English", "Keyed"), "*.xml")
            .SelectMany(f => XDocument.Load(f).Root.Elements())
            .ToDictionary(e => e.Name.LocalName, e => e.Value);

    [Fact]
    public void Source_has_files_to_check() {
        Assert.True(SourceFiles().Count() > 40, "the check found too few source files, so it would pass on nothing");
    }

    [Fact]
    public void No_code_file_passes_a_literal_with_letters_to_a_drawing_or_message_call() {
        var bad = SourceFiles().SelectMany(f => DrawnTextCheck.Find(File.ReadAllText(f)).Select(m => Path.GetRelativePath(Root, f) + " " + m)).ToList();
        Assert.True(bad.Count == 0, "English text written in code instead of a translation key: " + string.Join(" | ", bad));
    }

    [Theory]
    [InlineData("ls.Label(\"Planted label\");")]
    [InlineData("if (ls.ButtonText(\"Planted button\")) { }")]
    [InlineData("Messages.Message(\"Planted message\", MessageTypeDefOf.NeutralEvent);")]
    [InlineData("var l = LetterMaker.MakeLetter(\"Planted label\", \"Planted text\", LetterDefOf.NeutralEvent);")]
    [InlineData("ls.Label($\"Planted {x}: {y}\");")]
    [InlineData("var c = new Command_Action { defaultLabel = \"Planted gizmo\" };")]
    [InlineData("var o = new FloatMenuOption(\"Planted option\", null);")]
    [InlineData("TooltipHandler.TipRegion(rect, \"Planted tip\");")]
    [InlineData("ls.CheckboxLabeled(\"Planted\", ref b, \"Planted tip\");")]
    [InlineData("Find.LetterStack.ReceiveLetter(\"Planted\", \"text\", LetterDefOf.NeutralEvent);")]
    [InlineData("ls.Label(\"Hello world\".Translate());")]
    [InlineData("ls.Label(\"Hello world\".TryTranslate(out var t));")]
    [InlineData("ls.Label(\"\u4e2d\u6587\u6807\u7b7e\");")]
    [InlineData("ls.Label(string.Format(\"Planted {0}\", x));")]
    [InlineData("ls.Label(\"Planted \" + x);")]
    [InlineData("ls.Label(x + \" planted\");")]
    [InlineData("const string Planted = \"Planted label\"; ls.Label(Planted);")]
    [InlineData("Draw(\"Planted label\");")]
    [InlineData("var u = \"//\"; ls.Label(\"Planted after a slash pair\");")]
    [InlineData("ls.Label(\"Planted\"); /* \" */ ls.Label(\"Planted two\");")]
    public void A_planted_literal_is_rejected(string code) {
        Assert.NotEmpty(DrawnTextCheck.Find("class P {\nvoid M() {\n" + code + "\n}\n}"));
    }

    [Theory]
    [InlineData("var a = \"SrKey\".Translate();")]
    [InlineData("ls.Label(\"SrKey\".Translate(x));")]
    [InlineData("Log.Warning(\"a log line\");")]
    [InlineData("throw new InvalidOperationException(\"an error message\");")]
    [InlineData("Scribe_Values.Look(ref _point, \"_point\");")]
    [InlineData("ls.Label($\"{\"SrKey\".Translate()}: {x}\");")]
    [InlineData("var s = x.ToString(\"F1\");")]
    [InlineData("// ls.Label(\"a comment\");")]
    [InlineData("/* ls.Label(\"a comment\"); */")]
    [InlineData("ls.Label(string.Format(\"SrKey\".Translate(), x));")]
    [InlineData("ls.Label(x.Named(\"LEADER\"));")]
    public void Keys_logs_errors_and_comments_are_accepted(string code) {
        Assert.Empty(DrawnTextCheck.Find("class P {\nvoid M() {\n" + code + "\n}\n}"));
    }

    [Fact]
    public void A_planted_literal_is_reported_with_its_line() {
        var found = DrawnTextCheck.Find("class P {\nvoid M() {\nls.Label(\"Planted\");\n}\n}");
        Assert.Equal(new[] { "line 3: \"Planted\"" }, found);
    }

    [Fact]
    public void Every_key_the_code_translates_has_an_English_text() {
        var english = EnglishKeyed();
        var used = SourceFiles().SelectMany(f => Regex.Matches(File.ReadAllText(f), "\"([A-Za-z0-9_.]+)\"\\s*\\.(?:Translate|TryTranslate)\\b").Select(m => m.Groups[1].Value)).ToHashSet();
        Assert.True(used.Count >= 15, "the check found too few translated keys: " + used.Count);
        var missing = used.Except(CoreKeys).Except(english.Keys).OrderBy(k => k).ToList();
        Assert.True(missing.Count == 0, "keys used in code with no English text: " + string.Join(", ", missing));
        Assert.True(english.All(p => p.Value.Trim().Length > 0), "an English key has no text");
    }

    [Fact]
    public void Every_def_text_has_an_English_DefInjected_entry() {
        var missing = new List<string>();
        var checkedFields = 0;
        foreach (var file in Directory.GetFiles(Path.Combine(Root, "Defs"), "*.xml", SearchOption.AllDirectories))
        foreach (var def in XDocument.Load(file).Root.Elements()) {
            var name = def.Element("defName")?.Value;
            if (name == null) continue;
            var folder = Path.Combine(Root, "Languages", "English", "DefInjected", def.Name.LocalName);
            var injected = (Directory.Exists(folder) ? Directory.GetFiles(folder, "*.xml") : Array.Empty<string>())
                .SelectMany(f => XDocument.Load(f).Root.Elements()).Select(e => e.Name.LocalName).ToHashSet();
            foreach (var field in def.Elements().Where(e => DefTextFields.Contains(e.Name.LocalName) && Regex.IsMatch(e.Value, "[A-Za-z]{2}"))) {
                checkedFields++;
                if (!injected.Contains(name + "." + field.Name.LocalName)) missing.Add(def.Name.LocalName + " " + name + "." + field.Name.LocalName);
            }
        }
        Assert.True(checkedFields >= 19, "the check found too few def text fields: " + checkedFields);
        Assert.True(missing.Count == 0, "def text with no English DefInjected entry: " + string.Join(", ", missing));
    }
}
