using System.Collections;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class SettingsWindow
    {
        const string ModType = "SR.ModRimWorld.FactionalWar.SettingWindow";
        static string CheckboxLabel => "SrNeedOptimization".Translate().ToString();
        static string SliderLabelStart => "SrThreatPointFactor".Translate() + ": ";

        static Mod FactionalWar => LoadedModManager.ModHandles.Single(m => m.GetType().FullName == ModType);

        static string SettingsFile => Path.Combine(GenFilePaths.ConfigFolderPath, GenText.SanitizeFilename("Mod_" + FactionalWar.Content.FolderName + "_SettingWindow.xml"));

        static string Dump(System.Collections.Generic.IEnumerable<DrawnText> texts) => string.Join(" | ", texts.Select(t => t.ToString()));

        static float Value(string name) {
            var root = XDocument.Load(SettingsFile).Root.Element("ModSettings");
            return float.Parse(root.Element(name).Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        static string FileText(string name) => XDocument.Load(SettingsFile).Root.Element("ModSettings").Element(name).Value;

        [Scenario("settings-changed-in-the-settings-window-are-written-to-the-file", MapSize = 40, Windowed = true, OnlyOn = "1.7")]
        static IEnumerator ChangedSettingsAreWritten(ScenarioContext s) {
            s.Check(!File.Exists(SettingsFile), "no settings file before the settings window closes: " + SettingsFile);
            var dialog = new Dialog_ModSettings(FactionalWar);
            Find.WindowStack.Add(dialog);
            yield return s.DrawFrame();
            var opened = s.DrawFrame();
            yield return opened;
            var checkbox = opened.Texts.FirstOrDefault(t => t.Text == CheckboxLabel);
            var slider = opened.Texts.FirstOrDefault(t => t.Text.StartsWith(SliderLabelStart));
            var defaults = opened.Texts.FirstOrDefault(t => t.Text == "Default");
            s.Check(checkbox != null, "the settings window should show the original checkbox '" + CheckboxLabel + "'. Drawn: " + Dump(opened.Texts));
            s.Check(slider != null, "the settings window should show the original slider '" + SliderLabelStart + "<percent>'. Drawn: " + Dump(opened.Texts));
            s.Check(defaults != null, "the settings window should show the Default button. Drawn: " + Dump(opened.Texts));
            if (checkbox == null || slider == null || defaults == null) yield break;
            s.AreEqual("Enable optimization", CheckboxLabel, "the original name of the checkbox");
            s.AreEqual("Threat point factor: ", SliderLabelStart, "the original name of the slider");
            s.AreEqual(SliderLabelStart + "100%", slider.Text, "the slider label before any change (the original default)");
            var before = s.Screenshot("settings-window-defaults");
            yield return before;
            s.Check(before.ShowsText(CheckboxLabel) && before.ShowsText(SliderLabelStart + "100%") && before.ShowsText("Default"), "the screenshot should show the checkbox, the slider at 100% and the Default button. Drawn: " + Dump(before.Texts));

            yield return s.Click(checkbox);
            for (var i = 0; i < 3; i++) yield return s.DrawFrame();

            var scale = Prefs.UIScale;
            var row = new DrawnText {
                Text = "slider",
                Ink = new Rect(defaults.Box.x + defaults.Box.width * 0.25f, slider.Box.y + (Text.LineHeight + 2f + 11f) * scale, 1f, 1f),
            };
            yield return s.Click(row);
            for (var i = 0; i < 3; i++) yield return s.DrawFrame();
            var changed = s.DrawFrame();
            yield return changed;
            var changedSlider = changed.Texts.FirstOrDefault(t => t.Text.StartsWith(SliderLabelStart));
            s.Check(changedSlider != null && changedSlider.Text != SliderLabelStart + "100%", "clicking the slider should change its label from 100%. Drawn: " + Dump(changed.Texts));

            if (changedSlider == null) yield break;
            var after = s.Screenshot("settings-window-changed");
            yield return after;
            s.Check(after.ShowsText(changedSlider.Text), "the screenshot should show the slider with the changed value " + changedSlider.Text + ". Drawn: " + Dump(after.Texts));
            var closing = s.DrawFrame();
            yield return closing;
            var close = closing.Texts.FirstOrDefault(t => t.Text == "CloseButton".Translate().ToString());
            s.Check(close != null, "the settings window should have a close button. Drawn: " + Dump(closing.Texts));
            if (close == null) yield break;
            yield return s.Click(close);
            for (var i = 0; i < 3; i++) yield return s.DrawFrame();
            s.Check(!Find.WindowStack.Windows.Contains(dialog), "the close button should close the settings window");
            s.Check(File.Exists(SettingsFile), "closing the settings window should write " + SettingsFile);
            if (!File.Exists(SettingsFile)) yield break;
            s.AreEqual("False", FileText("needOptimization"), "the checkbox written to the file");
            var factor = Value("threatPointFactor");
            s.Check(factor >= 0.3f && factor <= 0.9f, "the slider clicked in the lower half should write a factor between 0.3 and 0.9, wrote " + factor);
            s.AreEqual(SliderLabelStart + Mathf.RoundToInt(factor * 100f) + "%", changedSlider?.Text, "the slider label shown after the click against the factor written");
            s.RequireNoLogErrors();
        }

        [Scenario("screenshot-settings-window-shows-the-original-options", MapSize = 40, Windowed = true, OnlyOn = "1.6")]
        static IEnumerator SettingsWindowShown(ScenarioContext s) {
            var dialog = new Dialog_ModSettings(FactionalWar);
            Find.WindowStack.Add(dialog);
            yield return s.DrawFrame();
            var shot = s.Screenshot("settings-window-defaults");
            yield return shot;
            s.Check(shot.ShowsText(CheckboxLabel), "the settings window should show the checkbox '" + CheckboxLabel + "'. Drawn: " + Dump(shot.Texts));
            s.Check(shot.ShowsText(SliderLabelStart + "100%"), "the settings window should show the slider at 100%. Drawn: " + Dump(shot.Texts));
            s.Check(shot.ShowsText("Default"), "the settings window should show the Default button. Drawn: " + Dump(shot.Texts));
            Find.WindowStack.TryRemove(dialog, doCloseSound: false);
            s.RequireNoLogErrors();
        }

        [Scenario("settings-saved-values-apply-after-restart", MapSize = 40, Windowed = true, SelfTest = true, OnlyOn = "1.7")]
        static IEnumerator SavedSettingsApply(ScenarioContext s) {
            s.Check(File.Exists(SettingsFile), "the seeded settings file should be in place: " + SettingsFile);
            if (!File.Exists(SettingsFile)) yield break;
            var seededText = File.ReadAllLines(SettingsFile).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            var factor = Value("threatPointFactor");
            s.Check(Mathf.Abs(factor - 1f) > 0.05f, "setup: the seeded factor should differ from the default 1, it is " + factor);
            s.AreEqual("False", FileText("needOptimization"), "setup: the seeded checkbox");
            var dialog = new Dialog_ModSettings(FactionalWar);
            Find.WindowStack.Add(dialog);
            yield return s.DrawFrame();
            var opened = s.DrawFrame();
            yield return opened;
            var slider = opened.Texts.FirstOrDefault(t => t.Text.StartsWith(SliderLabelStart));
            s.Check(slider != null, "the settings window should show the slider. Drawn: " + Dump(opened.Texts));
            if (slider != null)
                s.AreEqual(SliderLabelStart + Mathf.RoundToInt(factor * 100f) + "%", slider.Text, "the slider label after the restart against the seeded file");
            var close = opened.Texts.FirstOrDefault(t => t.Text == "CloseButton".Translate().ToString());
            s.Check(close != null, "the settings window should have a close button. Drawn: " + Dump(opened.Texts));
            if (close == null) yield break;
            yield return s.Click(close);
            for (var i = 0; i < 3; i++) yield return s.DrawFrame();
            var written = File.ReadAllLines(SettingsFile).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            s.Check(written.SequenceEqual(seededText), "closing the window should write back the values it loaded. Seeded: " + string.Join(" ", seededText) + ". Written: " + string.Join(" ", written));
            s.RequireNoLogErrors();
        }
    }
}
