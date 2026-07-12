using Godot;

namespace MagusWarrior.Core;

// Central i18n accessor. Keys live in data/strings/ui_strings.csv, which Godot imports to
// ui_strings.en.translation and project.godot registers under locale/translations.
//
// There are TWO mechanisms, and picking the wrong one silently produces an untranslated string:
//
//   STATIC strings — assign the KEY itself to the Control's Text and set
//     AutoTranslateMode = Always. Godot resolves it at render time. No helper needed:
//         btn.Text = "ui.combat.damage.hero_takes_it";
//         btn.AutoTranslateMode = Node.AutoTranslateModeEnum.Always;
//
//   INTERPOLATED strings — auto-translate CANNOT work. Godot looks up the finished Text as a
//     key, and "Minotaur: Physical — 7 damage to assign" is unique at runtime, so it will never
//     match anything. The TEMPLATE has to be translated first and filled afterwards:
//         label.Text = Strings.Format("ui.combat.damage.info", name, type, remaining);
//     The Control's AutoTranslateMode must then be Disabled, or Godot would try (and fail) to
//     translate the already-formatted result.
//
// This class is Godot-dependent (TranslationServer), so it lives in scripts/core/ alongside Log.cs
// and is deliberately NOT listed in tests/maguswarrior.Tests.csproj's per-file scripts/core includes.
public static class Strings {
    // A key with no placeholders. Prefer key-in-Text + AutoTranslateMode for Controls; use this
    // when the string is needed as a plain C# value (a dialog body, a log line, a composed label).
    public static string Get(string key) =>
        TranslationServer.Translate(key).ToString();

    // A format template with {0}, {1}, … placeholders, translated then filled.
    public static string Format(string key, params object[] args) =>
        string.Format(TranslationServer.Translate(key).ToString(), args);
}
