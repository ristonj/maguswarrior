---
stepsCompleted: [step-01-document-discovery, step-02-gdd-analysis, step-03-epic-coverage-validation, step-04-ux-alignment, step-05-epic-quality-review, step-06-final-assessment]
documentsUsed:
  gdd: _bmad-output/gdd.md
  architecture: _bmad-output/game-architecture.md
  epics: _bmad-output/epics.md
  ux: _bmad-output/planning-artifacts/ux-design-specification.md
---

# Implementation Readiness Assessment Report

**Date:** 2026-05-21
**Project:** maguswarrior

---

## GDD Analysis

### Functional Requirements

FR01: Single-hero game — Thomas (mechanically Tovak) is the only playable hero in v1.
FR02: First Reconnaissance solo scenario — full rules-accurate run is the v1 completion target.
FR03: Fixed V-shaped hex map — 8 countryside tiles (fixed) + 3 core tiles (2 non-city + 1 city, shuffled). Countryside always revealed before core.
FR04: All 14 base game site types supported (Village, Keep, Monastery, Mage Tower, Magical Glade, Crystal Mine, Dungeon, Monster Den, Ruins, Spawning Grounds, Tomb, Rampaging Orcs, Draconum, City).
FR05: Thomas's starting deed deck (16 fixed cards) + full base game shared offer rows (Advanced Actions, Spells, Units).
FR06: Full base game enemy set across all 6 token colors (Green, Gray, Purple, Brown, Red, White).
FR07: City tile renders on hex map, hidden until discovered; win condition triggers the turn after discovery; entering ends the run.
FR08: Card play system — three play modes: normal (printed effect), sideways (1 basic resource: player's choice of Move/Block/Attack/Influence 1), powered (mana cost → enhanced effect). Sideways block is always physical. Sideways cannot produce Ranged or Siege Attack.
FR09: Hand management — draw up to current hand limit each turn. Wound cards are always rendered untappable in red regardless of phase.
FR10: Hex movement — player spends Move points to cross hexes; terrain cost varies by Day/Night; revealing a tile costs 2 Move and grants +1 Fame (First Reconnaissance rule).
FR11: Combat resolution — four-phase sequence: Ranged/Siege Attack → Block → Assign Damage → Melee Attack. Enemies have elemental resistances. Starting Armor: 2.
FR12: Knockdown — hand fully replaced by wounds is a distinct combat state; hero cannot play cards; recruited units continue fighting independently.
FR13: Mana resource system — 6 colors (Red, Blue, Green, White, Gold, Black). Gold usable Day only; Black usable Night only by default. Mana tokens disappear at end of turn.
FR14: Crystal inventory — persistent per-color inventory, max 3 per color (12 total). Crystals convert to mana tokens during the player's turn.
FR15: Fame — persistent, earned through combat/sites/exploration. Gates level-ups. Displayed at all times.
FR16: Reputation track — persistent. Provides Influence modifier (positive bonus, negative penalty). At X-position: Influence unusable, all local interaction blocked.
FR17: Deck acquisition — Advanced Actions (at even level-ups and sites), Spells (at Mage Towers and as rewards), Artifacts (as site rewards), Units (Influence at sites), Skills (at even level-ups from personal pile / common pool).
FR18: Uniqueness constraint — only one copy of each AA, Spell, or Artifact may exist across all zones (deed deck + discard + hand + play area) simultaneously. Duplicate acquisition must be blocked.
FR19: Rest mechanics — Standard Rest (non-Wound in hand: discard 1 non-Wound + any Wounds) or Exhaustion (only Wounds: discard exactly 1 Wound). No movement, combat, or influence during rest. Special/Healing effects still allowed.
FR20: Healing — spending Healing points permanently removes Wounds from deed deck to shared supply (not rest cycling which returns to discard).
FR21: Day/Night cycle — alternates every round. Rounds 1 & 3 = Day; Rounds 2 & 4 = Night. Gold mana restricted to Day; Black mana restricted to Night. Movement terrain costs vary by cycle.
FR22: Tactics card selection — at round start, player picks one tactic card matching current Day/Night phase. Dummy draws one randomly from remaining. Tactic with lower number goes first. 12 total tactic cards (6 Day, 6 Night) with varied effects.
FR23: Dummy player — draws 3 cards per turn + bonus equal to crystals matching 3rd card's color. Empty deck at turn start = End of Round declaration. End of round triggers after player takes final turn. Dummy maintains deck across rounds (gains AA + crystal per round).
FR24: Win condition — city tile discovered; scenario victory declared; game ends after player's next full turn.
FR25: Failure condition — scenario timer expires (dummy's deck depletes before city found); failure summary screen shown.
FR26: End-of-scenario summary screen — Fame score, sites conquered, enemies defeated, distance to city if unfound.
FR27: Autosave/resume — full game state serialized at every decision boundary. State restored on resume. Interrupted runs restore cleanly. NOTIFICATION_APPLICATION_PAUSED hook for OS interruption.
FR28: Mana Source dice system — 3 dice rolled at round start. Player takes 1 die per turn for mana; re-rolled and returned at end of turn. Gold exhausted at Night; Black exhausted at Day. Basic-color guarantee: ≥ ceil(count/2) dice must show basic colors after rolling.
FR29: Level-up system — Odd levels: +1 Command token (unit capacity), stat increase alternating (Armor at 3, hand size at 5, etc.). Even levels: +1 Skill token + choose 1 AA from offer row. Level cap: 10.
FR30: Unit system — separate recruited-units zone (not deed deck). Activated once per round. Absorb damage (wound = can't activate until healed). Out-of-combat effects resolve through same Effect System as cards.
FR31: Context-sensitive tooltips — surface rule reminders at decision points. Veterans can permanently disable after first run.
FR32: Tutorial — inline, at-moment-of-relevance introduction of rules. Not interrupting, not a separate tutorial mode.
FR33: Round pressure visualization — round tracker must convey urgency as round limit approaches (visual/audio escalation).
FR34: Undo system — free until new information is revealed (card drawn, tile flipped, enemy drawn, die rolled). Staged cards returnable to hand. HUD shows running resource totals.
FR35: Offer row system — AA and Spell offers are positional queues (newest at top, oldest at bottom). Skill offer is an unordered pool. Positional state affects dummy player consumption and level-up acquisition.
FR36: Google Play cloud saves — opportunistic sync when connected; never blocks gameplay; local save is always authoritative on-device.
FR37: Achievements — 5–8 simple milestones; implemented last in v1 cycle.
FR38: Local high score list — end-of-run Fame score stored locally.
FR39: i18n infrastructure — all player-facing strings externalized from first build; English content only for v1.
FR40: External asset pipeline — `/assets/external/` folder structure; ATTRIBUTION.md for all external assets; license compatibility required before integration; all external assets tagged [PLACEHOLDER].
FR41: Tile count is open information — UI displays remaining countryside + core tile counts.
FR42: Assault rules — entering unconquered fortified site = mandatory assault, −1 Reputation. All garrison enemies treated as fortified (Siege Attacks only in Ranged/Siege phase). Enemies with Fortified token ability at fortified sites = fortified twice (immune to Ranged and Siege Attacks in that phase).
FR43: Artifact acquisition — whenever any artifact is gained, draw 2 from artifact deck, keep 1, place the other at bottom of artifact deck.
FR44: Spell powering — unpowered spell: 1 basic color mana (lesser effect); powered spell: 1 basic color mana + 1 Black mana (greater effect).
FR45: Improvisation card — context-determined resource based on current game phase (not player's choice). Wound cards cannot be discarded to fuel it.
FR46: Skill selection at level-up — draw 2 from personal skill pile; (A) keep 1, place other in common pool, choose any AA from offer; or (B) take from common pool (other hero's only), place both drawn in common pool, take AA from lowest offer position. Dummy contributes one random skill to common pool at each Thomas level-up.

**Total FRs: 46**

---

### Non-Functional Requirements

NFR01: Frame rate — sustained 30fps minimum on Galaxy S21; no spikes during card play, tile reveal, or combat.
NFR02: Input-to-feedback latency — <100ms from tap to visible effect.
NFR03: Platform — Android 12+ (API 31).
NFR04: Target device — Samsung Galaxy S21 (Snapdragon 888, 6GB RAM).
NFR05: Orientation — landscape only; portrait not supported in v1.
NFR06: Offline play — fully required; all gameplay, save, and load must work with no network connection.
NFR07: Crash rate — <1 crash per 100 sessions during playtesting.
NFR08: Save reliability — 100% accurate restore across all tested states (mid-turn, mid-combat, mid-round, round boundary). 10 interrupted runs must all restore cleanly.
NFR09: Build health — APK deploys to Galaxy S21 without errors at each named epic milestone (Epics 0, 1b, 3, 5, 7).
NFR10: Engine — Godot 4 + C# (LTS channel).
NFR11: Art style — vector/flat; all site type icons must pass 48px silhouette readability test; card frames are highest-stakes visual assets.
NFR12: Audio — tactile sound design for all state-changing inputs; implemented in first sprint alongside mechanics (not deferred to polish).
NFR13: Mana color legibility — 6 mana colors must be distinguishable at HUD scale at all times.
NFR14: i18n infrastructure — all strings externalized from first build; adding new languages post-v1 requires no code changes.
NFR15: Load times — no hard target; cold launch and scene transitions must be "fast by design" (vector assets, no 3D); noticeable load time treated as performance bug.
NFR16: Cloud save — opportunistic sync only; rate-limited; never blocks gameplay.
NFR17: Rules accuracy — zero misreadings confirmed by veteran playtest review before v1 release.

**Total NFRs: 17**

---

### Additional Requirements / Constraints

C01: Four LLDs (Effect System, Enemy Effect, Site Interaction, UX) must be complete before architecture begins — hard gate, no exceptions. *(Note: as of 2026-05-21 all LLDs are complete.)*
C02: No cross-run persistence — deed deck, Fame, Reputation, and Crystals reset fully on every new run.
C03: Fixed challenge — no difficulty settings; consistent with the board game.
C04: No meta-progression — mastery comes from player skill across runs, not persistent unlocks.
C05: Solo-only architecture — no design obligation to accommodate multiplayer for v1; multiplayer refactor accepted post-v1.
C06: City combat system (all 12 tiers) is out of scope for v1 — city tile discovery ends the run.
C07: Expansion content (Shades of Tezla, Lost Legion, etc.) is out of scope for v1.
C08: Additional heroes (only Thomas/Tovak in v1) and additional scenarios (only First Reconnaissance in v1) are out of scope.
C09: Phase gate — before any card is played, PhaseGate.IsLegal() must be evaluated against the chosen effect type, not just the card's base type.
C10: Every player decision must use async/await — no polling, flags, or callbacks as substitutes.

---

---

## Epic Coverage Validation

### Coverage Matrix

| FR | Requirement (Short) | Epic Coverage | Status |
|---|---|---|---|
| FR01 | Single hero (Thomas) | Epic 7 (scenario start), Epic 1b (vertical slice) | ✅ Covered |
| FR02 | First Reconnaissance scenario | Epic 7 | ✅ Covered |
| FR03 | V-shaped hex map (8 countryside + 3 core) | Epic 2 | ✅ Covered |
| FR04 | All 14 site types | Epic 4 | ✅ Covered |
| FR05 | Thomas's starting deck + full offer rows | Epic 1b (starting deck), Epic 5 (offer rows) | ✅ Covered |
| FR06 | Full base game enemy set (6 colors) | Epic 3 ("enemy drawing per color pile") — specific abilities behind Enemy LLD gate | ⚠️ Partial |
| FR07 | City tile renders hidden until discovered; win triggers on discovery | Epic 2 (hex rendering), Epic 7 (win condition) | ✅ Covered |
| FR08 | Card play: normal / sideways / powered; sideways constraints | Epic 1b (covers normal/sideways/powered) — sideways combat constraints (no Ranged/Siege, physical-only Block) not explicit | ⚠️ Partial |
| FR09 | Hand draw to hand limit; Wound untappable | Epic 1b | ✅ Covered |
| FR10 | Hex movement with Day/Night terrain costs | Epic 2 | ✅ Covered |
| FR11 | Combat: four-phase sequence | Epic 3 | ✅ Covered |
| FR12 | Knockdown state | Epic 3 | ✅ Covered |
| FR13 | Mana system (6 colors, Gold/Black restrictions) | Epic 6 | ✅ Covered |
| FR14 | Crystal inventory (max 3 per color) | Epic 6 | ✅ Covered |
| FR15 | Fame track | Epic 5 | ✅ Covered |
| FR16 | Reputation track + Influence modifier | Epic 5 | ✅ Covered |
| FR17 | Deck acquisition (AAs, Spells, Artifacts, Units, Skills) | Epic 5 | ✅ Covered |
| FR18 | Uniqueness constraint (AAs/Spells/Artifacts) | Epic 5 UI verification mentions duplicate-prevention — no explicit story | ⚠️ Partial |
| FR19 | Rest mechanics (Standard Rest vs. Exhaustion rules) | **NOT FOUND** — no epic explicitly covers the rest decision or rest phase rules | ❌ Missing |
| FR20 | Healing (permanent Wound removal using Healing points) | Epic 4 covers buying healing at sites; permanent removal mechanic not explicitly a story | ❌ Missing |
| FR21 | Day/Night cycle (round alternation, mana + terrain effects) | Epic 2 (terrain/visual), Epic 6 (mana restrictions) | ✅ Covered |
| FR22 | Tactics card selection (player + dummy, lower number first) | Epic 7 | ✅ Covered |
| FR23 | Dummy player mechanics (draw/round end/between-round maintenance) | Epic 7 | ✅ Covered |
| FR24 | Win condition (city discovery → victory next turn) | Epic 7 | ✅ Covered |
| FR25 | Failure condition (round 4 ends without city) | Epic 7 | ✅ Covered |
| FR26 | End-of-scenario summary screen | Epic 7 | ✅ Covered |
| FR27 | Autosave/resume; NOTIFICATION_APPLICATION_PAUSED; interrupted-run restoration | Epic 0 (skeleton), Epic 3 (mid-combat), Epic 10 (cloud save) — full save-at-decision-boundary completeness not explicit | ⚠️ Partial |
| FR28 | Mana Source dice system (roll/use/reroll, basic-color guarantee) | Epic 6 | ✅ Covered |
| FR29 | Level-up system (odd/even cadence) | Epic 5 | ✅ Covered |
| FR30 | Unit system (zone, once per round, damage absorption, out-of-combat effects) | Epic 3 (combat use + damage), Epic 5 (three-state, out-of-combat) | ✅ Covered |
| FR31 | Context-sensitive tooltips (veteran disable) | Epic 8 | ✅ Covered |
| FR32 | Tutorial inline | Epic 8 | ✅ Covered |
| FR33 | Round pressure visualization (visual/audio escalation) | Epic 8 | ✅ Covered |
| FR34 | Undo system (free until new information revealed) | Epic 1a (event log), Epic 1b (staged card undo) | ✅ Covered |
| FR35 | Offer row positional queue system | Epic 5 | ✅ Covered |
| FR36 | Google Play cloud saves | Epic 10 | ✅ Covered |
| FR37 | Achievements (5–8 milestones) | Epic 10 | ✅ Covered |
| FR38 | Local high score list | Epic 10 | ✅ Covered |
| FR39 | i18n infrastructure | Epic 0 | ✅ Covered |
| FR40 | External asset pipeline (ATTRIBUTION.md) | Epic 9 | ✅ Covered |
| FR41 | Tile count open information (UI display) | Epic 2 | ✅ Covered |
| FR42 | Assault rules (fortified / double-fortified, rep cost) | Epic 3 | ✅ Covered |
| FR43 | Artifact acquisition (draw 2, keep 1) | Epic 4 | ✅ Covered |
| FR44 | Spell powering rules (unpowered: 1 color; powered: 1 color + 1 Black) | Epic 5 covers Spell acquisition; resolution rules not explicit as a story | ⚠️ Partial |
| FR45 | Improvisation card (phase-context-determined resource) | Effect System LLD gate referenced in Epic 1a; no explicit story for the Improvisation rule | ❌ Missing |
| FR46 | Skill selection (personal pile vs. common pool; dummy contribution) | Epic 5 | ✅ Covered |

---

### Missing Requirements

#### Critical Missing FRs

**FR19: Rest mechanics (Standard Rest vs. Exhaustion)**
- No epic or story explicitly covers the rest decision point or its rules.
- Impact: Rest is a fundamental turn-structure mechanic — players take rest turns constantly. Without explicit stories, implementations details (Standard Rest vs. Exhaustion distinction, "no movement/combat/influence during rest, special/healing allowed") could be missed or inconsistently implemented across the turn loop.
- Recommendation: Add a story to Epic 1b or Epic 7: "As a player, I can declare a Rest turn and discard per rest rules so I can recover my hand state."

**FR20: Healing (permanent Wound removal from deck)**
- Epic 4 covers the Influence spend to buy healing at sites but the actual mechanics of spending Healing points to permanently remove Wound cards from the deed deck (returned to shared supply, not discard) is not a story anywhere.
- Impact: This is distinct from rest cycling. Without an explicit story, the implementation risk is that healing gets conflated with rest discarding. A Wound removed via Healing is gone from the run; a Wound discarded during rest comes back.
- Recommendation: Add a story to Epic 4: "As a player, my Healing points permanently remove Wound cards from my deck so they don't cycle back."

**FR45: Improvisation card (phase-context-determined resource)**
- The Improvisation rule is non-obvious: unlike sideways play (player's free choice), Improvisation locks resource type to the current phase. This distinction is easy to get wrong.
- Impact: If Improvisation is implemented as "free choice like sideways," it's a rules error that's hard to catch without a specific test. This is Thomas's unique card — it must resolve correctly.
- Recommendation: Add an explicit AC to the Effect System stories in Epic 1a or Epic 1b: "Improvisation resolves to the context-determined resource for the current phase, not the player's free choice."

#### High Priority Gaps

**FR18: Uniqueness constraint enforcement (AAs, Spells, Artifacts)**
- Referenced in Epic 5's UI verification but not as an explicit story or acceptance criterion. Without a story, there's no defined test for "player tries to acquire a duplicate — system blocks it."
- Recommendation: Add a story or AC to Epic 5: "The system prevents acquiring a duplicate AA, Spell, or Artifact if one already exists in any zone."

**FR27: Save system completeness (full-boundary autosave)**
- Epic 0 covers the skeleton; Epic 3 covers mid-combat save; Epic 10 covers cloud save. But no epic explicitly covers: saving at every decision boundary, the NOTIFICATION_APPLICATION_PAUSED hook, or the 10-interrupted-run restoration test from the GDD.
- Recommendation: Add these as explicit ACs in either Epic 0 (NOTIFICATION hook) or Epic 7 (10-interrupted-run verification as an acceptance criterion of the full loop epic).

**FR08 partial: Sideways play constraints in combat**
- Epic 1b covers sideways play generally. The specific combat constraints (sideways cannot contribute Ranged/Siege Attack; sideways Block is always physical, never elemental) are not covered.
- Recommendation: Add an AC to the combat stories in Epic 3: "Cards played sideways during the Block phase always provide physical Block — never elemental block."

**FR44 partial: Spell resolution rules**
- Spells are acquired in Epic 5 but their two-cost powering mechanic (unpowered: 1 basic color mana; powered: 1 basic + 1 Black) is not an explicit story. This is a card type with rules distinct from other powered cards.
- Recommendation: Add a story to Epic 5 or Epic 6: "As a player, I play a Spell for its unpowered effect (1 mana) or powered effect (1 mana + 1 Black) so the two modes resolve correctly."

---

### Coverage Statistics

- Total GDD FRs: 46
- FRs fully covered in epics: 33
- FRs partially covered (mentioned but not explicit story): 6 (FR06, FR08, FR18, FR27, FR44, and FR46 is actually covered)
- FRs missing (not covered): 3 (FR19, FR20, FR45)
- **Coverage: 72% fully covered / 85% with partial coverage counted**

---

---

## UX Alignment Assessment

### UX Document Status

Found: `_bmad-output/planning-artifacts/ux-design-specification.md` (113.6 KB, 14 steps complete, 2026-05-08).

---

### UX ↔ GDD Alignment

Strong alignment across core mechanics:

| Area | Status | Notes |
|---|---|---|
| Screen contract (5-layer z-order, named zones) | ✅ Aligned | GDD required a screen contract after prior failure; UX spec delivers it explicitly |
| Touch interaction (tap/pinch/long-press) | ✅ Aligned | Both documents define the same v1 gesture set |
| Card play flow (expand, stage, commit, undo) | ✅ Aligned | UX adds animation timing details (150ms expand, ~80ms undo ease-in) consistent with GDD intent |
| Phase legality (two-layer help system) | ✅ Aligned | UX defines card-level and phase-level help layers matching GDD requirement |
| Veteran tooltip toggle (`showHelpText` flag) | ✅ Aligned | UX spec: one flag, one behaviour, persistent once set |
| Session resume orientation (4-priority render order) | ✅ Aligned | UX spec defines explicit priority order for resume rendering |
| Day/Night visual transition | ✅ Aligned | Both: WorldEnvironment tween; UX adds palette shift detail |
| City discovery animation | ✅ Aligned | Both describe the emotional peak; UX adds timing (castle ~500ms, 2–3s silence) |
| Tutorial wraps first run | ✅ Aligned | Consistent across both documents |
| Mana zone shape-primary rendering | ✅ Aligned | UX adds shape-as-secondary-signal for accessibility; extends GDD intent |

**Four UX responsibilities are in scope per the UX spec (marked "v1, not polish") but absent from all epics:**

1. **Run-start tile animation** — UX spec: "Three starting tiles animate in face-up sequentially; 4s with explicit 'tap to begin' before first input; v1 feature, not post-v1 polish." No epic covers this. Would fit in Epic 2 (tile reveal is Epic 2 scope) or Epic 8 (UI pass).

2. **Loss screen capability comparison** — UX spec defines `RoundSnapshot[]` capability delta for the loss screen ("round 1 vs. final capability comparison"). Epic 7's end summary lists Fame, sites, enemies, distance-to-city — but not capability comparison. Requires new data structure (`RoundSnapshot`) not in architecture.

3. **Player Discovery beat** — UX spec: "First off-script card combo: half-beat pause + warm audio cue + quiet end-of-round 'Your discovery' log entry." Requires game-state tracking of card combinations that produce non-obvious outcomes. Not in any epic. Complex to implement correctly.

4. **Round-over-round capability delta feedback** — UX spec: "Round-over-round capability delta is a named feedback responsibility." Not in any epic. Partially overlaps with loss screen capability comparison.

---

### UX ↔ Architecture Alignment

Strong alignment on core mechanisms:

| Area | Status | Notes |
|---|---|---|
| `UIStateMachine.cs` for nested UI states | ✅ Aligned | Architecture defines it; UX 5-layer stack is compatible |
| Screen contracts (5 defined in architecture) | ✅ Aligned | Architecture: GameBoard, Hand Display, Combat, Offer, End-of-Turn. All consistent with UX spec zone definitions |
| `showHelpText` as `ConfigFile` preference | ✅ Aligned | Architecture specifies `user://settings.cfg`; UX spec uses this mechanism |
| Presentation/feedback seam | ✅ Aligned | Architecture: events fire with result data before state commits; UX animation timings build on this seam correctly |
| PendingInteraction for all choices | ✅ Aligned | Non-negotiable in both documents |

**Two architecture gaps relative to UX spec:**

1. **`mock_state` export for component testing** — UX spec: "All Control-node components accept an isolated state harness: a defined `mock_state` export that bypasses the game loop for per-epic testing." Architecture specifies GUT tests but does not define the `mock_state` export mechanism or where it lives. This needs to be added to the architecture or at minimum established as a pattern in Epic 1b when the first UI components are built.

2. **`RoundSnapshot` type for capability delta** — UX spec references `capability_delta: RoundSnapshot[]` as a data input to the loss screen. This type does not exist in the architecture or anywhere in the data model. Without it, the capability comparison feature cannot be implemented. Either the architecture needs this type, or the feature must be explicitly called out as a story in Epic 7.

---

### UX Warnings

- The **Player Discovery beat** is the most complex UX-v1 commitment. Detecting an "off-script card combination" requires semantic game-state awareness beyond what current architecture defines. If this feature is not scoped into an epic explicitly, there is a high risk it gets dropped silently at implementation time. Recommend either: (a) adding an explicit story to Epic 7 or Epic 8, or (b) explicitly deferring it to post-v1 with a note in the UX spec.

- The **run-start tile animation** is explicitly marked "v1, not post-v1 polish" in the UX spec. If it is not added to an epic, it may be treated as polish and deferred, which would contradict the UX spec's design rationale (anticipation as an emotional on-ramp is load-bearing for the first-run experience).

---

---

## Epic Quality Review

### Epic-by-Epic Assessment

| Epic | Player Value | Independence | Stories | Status |
|---|---|---|---|---|
| 0: Foundation | ⚠️ Dev-centric ("nothing to play") | ✅ No dependencies | 3 (all "As a dev") | Acceptable for greenfield |
| 1a: Effect System Architecture | ⚠️ Dev-centric (inspector output) | ✅ Depends on 0 only | 3 (all "As a dev") | Acceptable for architecture proof |
| 1b: Hand Mechanics | ✅ Player-centric | ✅ Depends on 1a | 5 | ✅ Good |
| 2: Hex Map + Movement | ✅ Player-centric | ⚠️ Listed as 1a only; player stories require hand card play (1b) | 5 | See critical issue #1 |
| 3: Combat System | ✅ Player-centric | ✅ Depends on 1b, 2 | 5 | ✅ Good |
| 4: Site Interactions | ✅ Player-centric | ⚠️ Lists 1b, 3; rampaging enemy provocation requires 2 | 5 | See critical issue #2 |
| 5: Deck Building + Progression | ✅ Player-centric | ✅ Depends on 1b, 4 | 5 | ✅ Good |
| 6: Resource Systems | ✅ Player-centric | ✅ Depends on 1b, 2 | 5 | ✅ Good |
| 7: Full Scenario Loop | ✅ Player-centric | ✅ Depends on 3, 4, 5, 6 | 5 | ✅ Good |
| 8: UI/UX | ✅ Player-centric | ✅ Depends on 1a–7 | 4 | ✅ Good |
| 9: Art + Audio | ⚠️ Content milestone | ✅ Depends on 8 | **0 stories defined** | See critical issue #3 |
| 10: Google Play + Release | ✅ Mix of player/infra | ✅ Depends on 7 | 4 | ✅ Good |

---

### Critical Violations

**🔴 Critical Issue #1: Epic 2 understates dependencies**

Epic 2's player stories ("I spend Move points to cross hexes") require the ability to play movement cards from hand. The card play + hand UI system lives in Epic 1b. Epic 2 only lists Epic 1a as a dependency. If Epic 2 is implemented without Epic 1b, movement would require a dev inspector workaround rather than a real card play, making the stories untestable.

The GDD notes "Epic 2 starts after 1a (not parallel)" — but this is about undo state sharing, not hand UI. The dependency should be: Epic 2 → Epic 1a AND Epic 1b.

- **Impact:** Stories "I spend Move points to cross hexes" cannot be verified through the player-facing flow without Epic 1b.
- **Recommendation:** Add Epic 1b to Epic 2's dependency list. Alternatively, confirm that Epic 2 is intended to be tested via the dev inspector (no hand UI) — but then document this explicitly.

**🔴 Critical Issue #2: Epic 4 missing dependency on Epic 2 for rampaging enemies**

The story "As a player, I trigger a Rampaging Orc by moving between its adjacent hexes" requires the hex movement system from Epic 2. Epic 4 only lists dependencies as Epic 1b and Epic 3. This means the Rampaging Orc story implicitly requires Epic 2 without stating it.

- **Impact:** Rampaging enemy provocation cannot be implemented without the movement provocation logic from hex-movement-lld.md, which lives in Epic 2's scope.
- **Recommendation:** Add Epic 2 to Epic 4's dependency list.

**🔴 Critical Issue #3: Epic 9 has no stories**

Epic 9 (Art + Audio) has a priority order but zero defined stories in the epics document. The overview table lists "—" for estimated stories. Without stories, Epic 9 has no acceptance criteria, no testable deliverables, and no way to declare it complete at sprint time.

- **Impact:** The Art + Audio phase cannot be sprint-planned or tracked without stories.
- **Recommendation:** Add at minimum one story per priority item: (1) Core SFX set deployed and triggering; (2) 14 site silhouettes pass 48px test; (3) Card frames and mana iconography in place; (4) Ambient music tracks implemented; (5) "Objective Achieved!" fanfare implemented.

---

### Major Issues

**🟠 Issue: Mid-combat save is an AC item but not a story**

Epic 3's scope explicitly states: "Mid-combat save serialization: serialize/deserialize mid-combat state verified as acceptance criterion for this epic." However, none of the 5 stories in Epic 3 cover this. It exists as a deliverable note but not a testable story.

- **Impact:** Without a story, mid-combat save may be implemented but never formally verified against a pass/fail criterion.
- **Recommendation:** Add a 6th story to Epic 3: "As a dev, I can force-quit mid-combat and restore to the exact decision point so mid-combat save is verified before Epic 7."

**🟠 Issue: Healing mechanic (FR20) has no story**

As identified in the FR coverage gap analysis, the mechanics for permanently removing Wounds from the deck by spending Healing points is not an explicit story anywhere. Epic 4's "buy healing" story covers the Influence spend but not the wound removal mechanic.

- **Recommendation:** Add an AC to the Village/Monastery interaction story in Epic 4: "Healing points spent permanently remove Wound cards from the deed deck (returned to shared supply, not discard pile). This is distinct from rest cycling."

**🟠 Issue: Rest mechanics (FR19) has no story**

Standard Rest vs. Exhaustion rules (when each applies, what may/may not be played during rest) have no explicit story. This is foundational turn structure.

- **Recommendation:** Add a story to Epic 1b or Epic 7: "As a player, I can declare a Rest turn and follow rest rules (Standard Rest vs. Exhaustion) so hand recovery is correctly governed."

**🟠 Issue: Tactics structural effects resolution path undefined**

Epic 7 mentions "resource effects via Effect System; structural effects (reserve die, reshuffle, draw specific card) via their own resolution path." The architecture also acknowledges this as a novel pattern requiring a separate path. But no story defines what "their own resolution path" means or verifies it works. This is exactly the kind of "we'll figure it out in implementation" that causes the failures the GDD is trying to prevent.

- **Recommendation:** Add a story to Epic 7: "As a player, Tactics structural effects (reserve-a-die, reshuffle-deck, draw-specific-card types) resolve correctly via their implementation path so Tactics work end-to-end."

**🟠 Issue: UX v1 features missing from all epics (4 features)**

As identified in UX alignment: run-start tile animation, loss screen capability comparison, Player Discovery beat, and round-over-round capability delta are all named v1 responsibilities in the UX spec but absent from any epic. (See UX Alignment section for details.)

---

### Minor Concerns

**🟡 Story format: "UI Verification" sections are not formal ACs**

All epics use descriptive "UI Verification" blocks rather than Given/When/Then BDD acceptance criteria. This is consistent and intentional but means ACs are qualitative rather than strictly testable. Not a blocker — but story preparation (Step 3 of create-story) should formalize ACs at story-creation time.

**🟡 Epic 4: "Pattern locked" checkpoint is not a trackable story**

The internal "Pattern locked before remaining 10 sites begin" checkpoint is mentioned in scope but not as a story or deliverable. This is a project milestone that could be missed without a formal record.

**🟡 Epics 0 and 1a: dev-centric stories**

Acceptable for a greenfield game project — the foundation and architecture proof epics are industry-standard practice. Not a violation in this context, but worth noting for the sprint planning step: these epics are testing infrastructure, not feature work.

---

### Best Practices Compliance Summary

| Criterion | Status |
|---|---|
| Epics deliver player/user value | ⚠️ 9/11 player-centric; 0 and 1a intentionally dev-centric; 9 has no stories |
| Epic independence (no forward dependencies) | ⚠️ Epic 2 and 4 have missing upstream dependencies |
| Stories appropriately sized | ✅ All stories appear 1–3 day scope; no epic-sized stories |
| No forward dependencies within stories | ✅ No story references a future story |
| Data structures created when needed | ✅ No upfront schema dumping |
| Acceptance criteria present | ⚠️ Present as UI Verification blocks; not formal BDD |
| Traceability to FRs | ⚠️ Partial — 3 FRs missing, 6 partially covered |

---

### GDD Completeness Assessment

The GDD is thorough and well-structured. All core systems are defined with clear scope boundaries. Key observations:

- **Strong:** All 46 FRs are traceable to specific systems. Out-of-scope items are explicitly enumerated. Open design flags are called out (Tranquility fizzle, Crystallize mana, terrain costs).
- **Intentional deferrals documented:** City combat, non-combat city interaction, multiplayer, expansions are all explicitly out of scope with rationale.
- **One residual placeholder:** `{{target_audience}}` in the GDD body (line 111) was never replaced — appears to be a template artifact. Harmless since the Target Audience section is fully populated later in the doc.
- **Architecture gate status:** All four LLDs called out in C01 are complete as of this assessment.

---

## Summary and Recommendations

### Overall Readiness Status

**NEEDS WORK — Proceed with fixes**

The project is in strong shape for a solo developer undertaking this scope. All major documents exist and are aligned. All 6 LLDs are complete. The architecture is sound and the GDD is thorough. However, 11 specific issues were found that are likely to cause scope confusion, missed mechanics, or implementation bugs if not addressed before sprint planning.

None of the critical issues require re-architecting anything. Most are simple additions (a story here, a dependency line there). The project is approximately **one planning session away from full readiness**.

---

### Critical Issues Requiring Immediate Action

These must be resolved before sprint planning (`gds-sprint-planning`):

**1. Epic 2 missing dependency on Epic 1b**
Epic 2's player stories require card play from hand (Move points come from cards), but Epic 2 only lists Epic 1a as a dependency. Add Epic 1b to Epic 2's dependency list, or explicitly document that Epic 2 stories are verified via dev inspector without hand UI.

**2. Epic 4 missing dependency on Epic 2**
The rampaging enemy provocation story requires hex movement from Epic 2. Add Epic 2 to Epic 4's dependency list.

**3. Epic 9 has no stories**
Art + Audio is ungroupable into a sprint without stories. Add one story per priority item (Core SFX set, site silhouettes, card frames, ambient music, city fanfare).

**4. Rest mechanics (FR19) has no story**
Standard Rest vs. Exhaustion is a core turn mechanic with no explicit story anywhere. Add to Epic 1b or Epic 7.

**5. Healing mechanic (FR20) has no story**
Permanent Wound removal via Healing points (distinct from rest cycling) has no story. Add an AC to Epic 4's Village/Monastery story.

**6. Improvisation card rule (FR45) not explicitly covered**
Thomas's unique card has a non-obvious rule (phase-determined resource, not player's choice). Add it as an explicit AC to the Effect System work in Epic 1a or 1b.

---

### Recommended Next Steps

1. **Fix the 6 critical issues above** in `_bmad-output/epics.md` — these are edits, not rewrites. Budget 1–2 hours.

2. **Decide on the 4 UX v1 features absent from epics** — for each of: run-start tile animation, Player Discovery beat, loss screen capability comparison, round-over-round capability delta: either add a story (in Epic 7 or 8) or explicitly mark it deferred to post-v1. The run-start animation and loss screen capability comparison are the most impactful; the Player Discovery beat is the most complex.

3. **Add `RoundSnapshot` type to architecture** (or explicitly call it out as a story-level decision in Epic 7) — required to implement the capability delta features.

4. **Add `mock_state` export pattern to architecture** — UX spec requires all Control-node components to support an isolated state harness for per-epic testing. This needs to be an architectural decision, not a per-story surprise.

5. **Add mid-combat save story to Epic 3** — Currently an acceptance criterion without a story. Make it explicit.

6. **Add Tactics structural effects resolution story to Epic 7** — The "own resolution path" for structural Tactics effects needs a story so it doesn't get deferred indefinitely.

7. **Run `gds-sprint-planning`** once items 1–2 are addressed. Sprint planning will consume this epics file and generate the sprint status that drives the story cycle.

---

### Issue Count by Severity

| Severity | Count | Items |
|---|---|---|
| 🔴 Critical | 3 | Epic 2 dependency, Epic 4 dependency, Epic 9 no stories |
| 🟠 Major | 8 | FR19 missing, FR20 missing, FR45 missing, mid-combat save story, Tactics effects story, 4 UX v1 features missing from epics |
| 🟡 Minor | 4 | FR18/FR08/FR27/FR44 partially covered, "Pattern locked" checkpoint, dev-centric epics 0 and 1a, no formal BDD ACs |
| **Total** | **15** | |

---

**Assessment completed:** 2026-05-21
**Assessor:** Implementation Readiness Check (GDS module)
**Documents reviewed:** `gdd.md`, `game-architecture.md`, `epics.md`, `ux-design-specification.md`, all 6 LLDs in `docs/`
