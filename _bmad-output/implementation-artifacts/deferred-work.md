# Deferred Work

Items logged here were surfaced during code review but deferred as pre-existing, out-of-scope, or not actionable at the time of review.

---

## Deferred from: code review of 0-3-add-ui-string-via-string-table (2026-05-29)

- **Test path hardcoded depth** — `I18nTest.cs` resolves CSV via `AppContext.BaseDirectory + "../../../../"`. Breaks if test output structure differs (e.g., CI publish subdirectory). Consider a walk-up search or `Directory.GetParent()` loop as a more robust alternative. [tests/unit/I18nTest.cs:10-11]
- **Weak CSV value assertion** — `CsvContainsPlaceholderTitleKey` only checks that the value is non-empty. A stricter assertion against the exact canonical string would catch accidental truncation or corruption. [tests/unit/I18nTest.cs:26]
- **CSV encoding undeclared** — `ui_strings.csv` uses an em dash (`—`, U+2014) with no BOM or `.editorconfig` charset rule. A non-UTF-8 tool re-save corrupts the character silently. Add `charset = utf-8` to `.editorconfig` for `*.csv`. [data/strings/ui_strings.csv]
- **`data/strings/` naming convention undocumented** — Intended naming pattern (`ui_strings.<locale>.csv`), subdirectory policy, and how to add new locales should be noted in a comment or doc before the string table grows. [data/strings/]
- **CRLF brittleness in header test** — `Assert.Equal("keys,en", firstLine)` fails on Windows CI due to trailing `\r`. Guard with `.TrimEnd('\r', '\n')` if the project ever runs tests on Windows. [tests/unit/I18nTest.cs:16]
- **`First()` throws on empty CSV** — `File.ReadLines(CsvPath).First()` throws `InvalidOperationException` (not a test failure message) if the file is empty. Pre-read to a list and assert non-empty first for a clearer failure. [tests/unit/I18nTest.cs:16]
- **Key prefix collision risk** — `StartsWith("ui.placeholder_menu.title,")` matches any key beginning with that string. The comma delimiter is sufficient now, but if a key like `ui.placeholder_menu.title_extended` is added without the comma guard being updated, the wrong row is matched. [tests/unit/I18nTest.cs:24]

## Deferred from: code review of 0-1-build-and-deploy-to-galaxy-s21 (2026-05-21)

- **GameState.CurrentPhase private set** — CombatResolver (Epic 3) must be able to advance CurrentPhase; `private set` will cause a compile error. Fix: expose `internal set` or a dedicated `SetPhase` method when CombatResolver is implemented. [scripts/core/GameState.cs]
- **GamePhase missing CombatStart/ActionPhase** — `docs/combat-flow-lld.md` references `CombatStart`, `CombatAssignDamage`, and `ActionPhase` which are absent from the committed enum. Pending resolution of Decision D3 (enum vs LLD authority). [scripts/core/types/GamePhase.cs]
- **GameDebug.Inspect(object?) boxes value types in release builds** — `[Conditional("DEBUG")]` strips the body but arguments are still evaluated and boxed in release. Revisit when Inspect is called from hot paths; consider wrapping call sites in `#if DEBUG` or changing signature to `string`. [scripts/core/GameDebug.cs]
- **GameConstants.MaxHandSize vs Hero.UnmodifiedHandSize** — Two independent sources of truth for starting hand size. When Hero is built (Epic 3+), ensure UnmodifiedHandSize is initialized from GameConstants.MaxHandSize and there is a single canonical source. [scripts/core/GameConstants.cs]
- **SiteType single Unknown value** — Placeholder only. When Epic 4 site types are added, ensure all switch expressions over SiteType have exhaustive cases or a discard arm to prevent CS8509 compile errors. [scripts/core/types/SiteType.cs]
- **AllowUnsafeBlocks enabled with no stated reason** — May be a Godot SDK requirement; if so, add a comment explaining why. If not required, remove it. No active unsafe code exists today. [maguswarrior.csproj]
- **AC 1/6/7 hardware deployment unverified** — Build success (AC 1, 6) and Galaxy S21 launch (AC 7) are runtime outcomes that the agent could not verify. Story should not be fully closed until user confirms the APK builds and runs on device.
- **Result<T> default(T) on Fail footgun** — `Result<int>.Fail(...)` returns Value=0; callers that read Value without first checking IsSuccess silently receive a valid-looking value. Enforce the check-first contract via convention or add a `ThrowIfFailure()` helper when the pattern proves error-prone in practice. [scripts/core/Result.cs]
