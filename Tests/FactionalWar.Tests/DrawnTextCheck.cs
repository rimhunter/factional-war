using System.Text.RegularExpressions;

namespace FactionalWar.Tests;

static class DrawnTextCheck
{
    static readonly Regex Literal = new(@"\$""(?:[^""\\{]|\\.|\{[^{}]*\})*""|""(?:[^""\\]|\\.)*""");
    static readonly Regex NotDrawn = new(
        @"Scribe_\w+\.Look|\bLog\.\w+|\bthrow\b|Exception\(|\bnameof\b|\.Named\(" +
        @"|Trigger_Memo\(|MapGenerator\.(?:TryGetVar|SetVar)|symbolStack\.Push|\btutorTag\b|\brepeatAvoiderTag:");
    static readonly Regex Comment = new(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline);
    static readonly Regex Tokens = new(Comment + "|" + @"@""(?:[^""]|"""")*""|'(?:[^'\\]|\\.)'|" + Literal, RegexOptions.Singleline);
    static readonly Regex TwoLetters = new(@"\p{L}{2}");
    static readonly Regex KeyShape = new(@"^""[A-Za-z0-9_.]+""$");
    static readonly Regex Interpolation = new(@"\{[^{}]*\}");
    static readonly Regex Escape = new(@"\\.");
    static readonly Regex Translated = new(@"^\s*\.(?:Translate|TryTranslate)\b");

    public static List<string> Find(string source) {
        var text = Tokens.Replace(source, m => m.Value.StartsWith("//") || m.Value.StartsWith("/*") ? Regex.Replace(m.Value, @"[^\n]", " ") : m.Value);
        var masked = Literal.Replace(text, m => new string('S', m.Length));
        var found = new List<string>();
        foreach (Match m in Literal.Matches(text)) {
            var literal = m.Value;
            var shown = literal.StartsWith('$') ? Interpolation.Replace(literal, "") : literal;
            if (!TwoLetters.IsMatch(Escape.Replace(shown, ""))) continue;
            if (KeyShape.IsMatch(literal) && Translated.IsMatch(text.Substring(m.Index + m.Length))) continue;
            var start = new[] { ';', '{', '}' }.Max(c => masked.LastIndexOf(c, m.Index));
            var statement = masked.Substring(start + 1, m.Index - start - 1) + text.Substring(start + 1, m.Index + m.Length - start - 1);
            if (NotDrawn.IsMatch(statement)) continue;
            found.Add("line " + (text.Take(m.Index).Count(c => c == '\n') + 1) + ": " + literal);
        }
        return found;
    }
}
