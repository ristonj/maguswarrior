# Magus Warrior — Development Epics

## Epic Overview

| # | Epic | Dependencies | Est. Stories |
|---|---|---|---|
| 0 | Foundation | None | 3 |
| 1a | Effect System Architecture | 0 | 3 |
| 1b | Hand Mechanics + Wound Treatment | 1a | 5 |
| 2 | Hex Map + Movement | 1a | 5 |
| 3 | Combat System | 1b, 2 | 5 |
| 4 | Site Interactions | 1b, 3 | 5 |
| 5 | Deck Building + Progression | 1b, 4 | 5 |
| 6 | Resource Systems | 1b, 2 | 5 |
| 7 | Full Scenario Loop | 3, 4, 5, 6 | 5 |
| 8 | UI/UX | 1a–7 | 4 |
| 9 | Art + Audio | 8 | — |
| 10 | Google Play + Release | 7 | 4 |

---

## Epic 0: Foundation

### Goal
A deployable APK on target hardware with save pipeline and i18n infrastructure in place. Nothing to play — but everything subsequent epics need to exist.

### Scope

**Includes:**
- Godot 4 + C# project structure
- Android build pipeline (API 31, Galaxy S21 target)
- Local save system skeleton (write/read arbitrary state)
- i18n string table (all UI strings go through it from day one)
- Basic scene structure (placeholder main menu + game scene)

**Excludes:**
- Any gameplay mechanics
- Art assets
- Cloud save / Google Play integration

### Dependencies
None.

### Deliverable
A signed debug APK that launches on the Galaxy S21, shows a placeholder screen, and successfully writes and reads a save state.

### Stories
- As a dev, I can build and deploy to Galaxy S21 so I can test on target hardware from sprint one
- As a dev, I can write game state to local save and read it back so the save pipeline is verified
- As a dev, I can add a UI string via the string table so i18n is enforced from the start

### UI Verification
Build deploys to device. Save/load cycle visible via debug log. Changing a string key in the string table reflects immediately in UI.

---

## Epic 1a: Effect System Architecture

### Goal
Prove the polymorphic Effect System architecture works before any UI is built on top of it. This is the load-bearing wall everything else inherits from.

### Scope

**Includes:**
- Base Effect class hierarchy (card/skill/unit effects all inherit the same base)
- Effect types: Move, Attack, Block, Influence, Heal, Mana, Crystal, Fame, Reputation
- Effect resolution pipeline
- **Undo system as event log** — designed here as a foundation extended by Epics 2, 3, and beyond; not a card-only undo stack. Every state mutation appends to the log; undo pops it.
- Phase validation framework (legal/illegal action definitions)
- One effect firing end-to-end via dev inspector (no hand UI)

**Excludes:**
- Hand draw/display/staging UI
- Wound treatment
- Specific card implementations beyond the one test effect

### Dependencies
Epic 0.

### Deliverable
Fire one effect via inspector, see it resolve with a full event log. Undo pops the last event. Architecture validated before UI is built on top.

### Stories
- As a dev, I can define an effect and fire it through the system so the architecture is proven
- As a dev, I can undo the last event so the event log pattern works before hand UI exists
- As a dev, I can see the full effect event log in the inspector so nothing is invisible

### UI Verification
Effect inspector showing every effect fired, source, type, value. Event log with full history. Undo pops cleanly from inspector.

---

## Epic 1b: Hand Mechanics + Wound Treatment

### Goal
Build the player's hand UI on top of the proven Effect System, delivering one complete card interaction end-to-end as proof the full stack works.

### Scope

**Includes:**
- Hand draw and display
- Card tap → expand in place (Play / Play Sideways / Power / Cancel)
- Staging area with running totals (Attack: N, Move: N, etc.)
- Commit and effect resolution via Effect System
- Undo via event log (extends Epic 1a — not rebuilt)
- Wound card red/untappable treatment (rendered in red, never tappable regardless of phase)
- Phase indicator (HUD foundation)
- One full card vertical slice: Rage (normal → Attack 2, sideways → Attack 1, powered → Attack 4)

**Excludes:**
- All specific card effects beyond Rage
- Skill/unit effects
- Combat resolution
- Hex map (placeholder only)

### Dependencies
Epic 1a.

### Deliverable
Tap Rage → see Play / Play Sideways / Power / Cancel → play it → staging area shows Attack total → commit → effect resolves and phase advances. Undo pulls it back. Wound card renders red and cannot be tapped.

### Stories
- As a player, I can tap a card to see my options so I know what I can do with it
- As a player, I can play a card sideways for a basic resource so I always have a fallback
- As a player, I can stage multiple cards and see running totals so I can plan before committing
- As a player, I can undo staged cards before new information is revealed
- As a player, I cannot tap a Wound card so I know it is unplayable

### UI Verification
Effect inspector (from 1a) shows card as effect source. Running totals update real-time. Wound renders red. Phase indicator updates correctly.

---

## Epic 2: Hex Map + Movement

### Goal
A navigable hex map with tile revelation, terrain movement costs including Day/Night variants, and the Day/Night visual transition.

### Scope

**Includes:**
- Hex grid rendering
- V-shape layout (8 countryside tiles + 3 core tiles, tile back distinction)
- Tile revelation: 2 Move cost, adjacency rules, coastline constraint
- Terrain movement cost per hex type
- Day/Night state flag (odd rounds = Day, even rounds = Night; First Reconnaissance: rounds 1+3 Day, 2+4 Night)
- WorldEnvironment tween for Day/Night visual transition
- Tile count display (X countryside, Y core remaining)
- Pinch-to-zoom + pan
- Long-press hex movement preview
- +1 Fame per tile revealed (First Reconnaissance rule)

**Excludes:**
- Sites on tiles (terrain only — no site interactions)
- Enemies on tiles
- Combat

### Dependencies
Epic 1a (undo event log extended here for tile revelation).

### Deliverable
A V-shaped hex map renders. Player moves by spending Move points. New tiles reveal at the map edge. Day/Night tween fires when the round changes. Tile count updates on reveal.

### Stories
- As a player, I can see the hex map and my hero position so I can plan movement
- As a player, I can tap a hex to preview its Move cost so I know if I can afford it
- As a player, I spend Move points to cross hexes, with terrain costs applying correctly
- As a player, I reveal a new tile by moving to the map edge, costing 2 Move
- As a player, I see remaining tile counts so I know how deep into the map I am

### UI Verification
Move cost label per hex on tap (including Day/Night variant). Tile count overlay. Day/Night toggle dev tool fires tween and updates terrain costs immediately. Coastline and adjacency constraints enforced visually.

---

## Epic 3: Combat System

### Goal
All four combat phases resolve correctly against any enemy, with units participating, unit damage absorption tracked, and mid-combat save state verifiable.

### Scope

**Includes:**
- Four combat phases in sequence: Ranged/Siege Attack → Block → Assign Damage → Melee Attack
- Enemy drawing per color pile
- Elemental and physical attack/block types
- Resistance rules (elemental vs. physical)
- Fortification: Siege-only ranged phase
- Double-fortification (assault): no ranged/siege at all
- Assault reputation cost (−1 per attempt)
- Knockdown state (hand fully wounds — hero cannot play cards, units continue)
- Unit deployment in combat
- Unit damage absorption: unit becomes wounded, unavailable until healed
- Armor stat
- **Mid-combat save serialization: serialize/deserialize mid-combat state verified as acceptance criterion for this epic (not deferred to Epic 10)**

**Excludes:**
- Specific per-enemy abilities beyond basic attack/resistance (Enemy Effect LLD gate)
- City combat (out of scope v1)

### Dependencies
Epic 1b (Effect System — all combat actions are effects), Epic 2 (combat triggers on movement into enemy hex).

### Deliverable
Move into an enemy hex → combat triggers → all four phases resolve → enemy defeated or hero knocked down. Units absorb damage and become wounded. Fortified enemies require correct attack types. Mid-combat state saves and reloads cleanly.

### Stories
- As a player, I can see the current combat phase so I know which card types are legal
- As a player, I can use Ranged/Siege Attack in phase 1 to soften enemies before melee
- As a player, I can block incoming damage to reduce wounds taken
- As a player, I can assign damage to a unit instead of my hero to protect my hand
- As a player, I enter knockdown when my hand is all wounds, and my units fight on without me

### UI Verification
Combat state inspector: current phase, enemy stats (attack/armor/resistances/fortification status), damage assignment breakdown, unit state (available/wounded). Phase-legality enforcement visible (illegal cards red in hand during combat). Save/load test: mid-combat state restores to exact decision point.

---

## Epic 4: Site Interactions

### Goal
All 14 site types fully implemented with correct interaction sequences, rewards, and reputation consequences — using a proven pattern established on the first 3–4 sites.

### Scope

**Includes:**
- **Pattern-first approach:** Implement Village, Keep, Dungeon, and Magical Glade first (covering friendly / fortified / combat / passive types) to establish and validate a site interaction Template pattern. "Pattern locked" is an internal checkpoint before the remaining 10 sites are implemented.
- All 14 site types: Village, Keep, Monastery, Mage Tower, Magical Glade, Crystal Mine, Dungeon, Monster Den, Ruins (both token types), Rampaging Orcs, Spawning Grounds, Tomb, Draconum, City (win condition trigger only)
- Influence spending at sites
- Unit recruitment at applicable sites
- Artifact draw-2-keep-1 (consistent across all artifact-rewarding sites)
- Rampaging enemy provocation mechanic
- Repeatable vs. one-time site state persistence

**Excludes:**
- City assault/combat (out of scope v1)
- Non-combat city interaction (verify against rulebook before implementing)
- Specific per-enemy abilities beyond basic attack/resistance (Enemy Effect LLD gate)

### Dependencies
Epic 1b (Effect System), Epic 3 (most sites require combat as prerequisite).

### Deliverable
Every site type triggers its correct interaction sequence. Rewards are accurate. Reputation changes apply. Artifact draw-2-keep-1 is consistent across all sources. Pattern is documented before the final 10 sites are built.

### Stories
- As a player, I can visit a Village and spend Influence to recruit a unit or buy healing
- As a player, I can assault a Keep to conquer it, accepting the reputation cost
- As a player, I can explore a Dungeon at Night for a chance at a spell reward
- As a player, I draw 2 artifacts and keep 1 whenever I gain an artifact through any means
- As a player, I trigger a Rampaging Orc by moving between its adjacent hexes

### UI Verification
Site interaction log (dev tool): each step, effects fired, reputation delta, rewards received. Ruins token type (fight vs. pay mana) distinguishable in log. Repeatable site state persists correctly across visits. Pattern checkpoint: first 4 sites reviewed before remaining 10 begin.

---

## Epic 5: Deck Building + Progression

### Goal
Level-up, offer rows, skill system, recruited-units zone with full state tracking, out-of-combat unit effects, artifacts, fame, and reputation — everything that makes the deck grow and the hero feel more powerful.

### Scope

**Includes:**
- Level-up cadence (odd levels: command token + stat increase; even levels: skill + Advanced Action from offer row)
- Advanced Action and Spell offer rows (positional queues; dummy player advancement between rounds)
- Skill acquisition: draw 2 from personal pile, choose 1 (or take from common offer per rules)
- Dummy player skill contribution at each level-up
- Recruited-units zone with three-state tracking: ready / exhausted (used this round) / wounded (absorbed damage, needs healing)
- Out-of-combat unit effect activation (Move, Influence, Mana, Crystal via Effect System)
- Artifact deck and acquisition flow
- Fame track with level threshold markers
- Reputation track with current Influence modifier application

**Excludes:**
- Dummy player full turn logic (Epic 7)
- City combat

### Dependencies
Epic 1b (Effect System — skill and unit effects), Epic 4 (unit recruitment happens at sites).

### Deliverable
Reach a Fame threshold → level-up triggers → choose Advanced Action → choose Skill → new cards in deck. Units display correct three-state. Out-of-combat unit effects fire correctly through the Effect System.

### Stories
- As a player, I level up when I reach a Fame threshold so my deck grows stronger
- As a player, I choose an Advanced Action from the offer row at even levels
- As a player, I navigate skill selection (my pile vs. common offer) to build my hero's identity
- As a player, I see my units with ready/exhausted/wounded states so I know what I can deploy
- As a player, I can activate a unit out of combat for its resource effect so I have options beyond my hand

### UI Verification
Fame progress bar with level threshold markers. Offer row showing positional order and duplicate-prevention enforcement. Skill selection panel with both piles shown. Units zone with three-state display. Reputation track showing current modifier value.

---

## Epic 6: Resource Systems

### Goal
Full mana source dice system with exhaustion state, crystal inventory, and Day/Night cycle restrictions enforced — the economic engine behind every powered card and spell.

### Scope

**Includes:**
- Three Source dice with roll/use/reroll cycle
- Exhausted die state: Black die during Day and Gold die during Night are unavailable for the rest of the round (overridable by specific effects)
- Mana token generation from dice, crystals, and card/skill/unit effects
- Crystal inventory (per-color, max 3 each, 12 total)
- Gold/Black Day/Night restriction enforcement with effect override support
- Day/Night mana availability applied correctly per round

**Excludes:**
- Specific card/skill/unit effects that generate mana (those wire into Effect System in Epics 1a/5)

### Dependencies
Epic 1b (Effect System — mana tokens feed into powered card resolution), Epic 2 (Day/Night state flag).

### Deliverable
Source dice display with correct exhaustion states. Player takes one die per turn for a mana token. Crystals convert to tokens. Gold/Black restrictions enforce. Effect overrides bypass restrictions correctly when triggered.

### Stories
- As a player, I can see the three Source dice and their current colors so I know available mana
- As a player, I take one die per turn to generate a mana token of that color
- As a player, I cannot select a Black die during Day (or Gold die during Night) so cycle rules are enforced
- As a player, I convert crystals to mana tokens to supplement the dice
- As a player, I see exhausted dice visually distinguished so I don't try to select them

### UI Verification
Source dice panel with exhausted state indicator. Crystal inventory display per color. Mana token generation log (source: die / crystal / effect). Effect override visible in log when restriction bypassed.

---

## Epic 7: Full Scenario Loop

### Goal
A complete, rules-accurate First Reconnaissance run from setup to win or loss — including Tactics, round structure, dummy player, and the "Objective Achieved!" moment.

### Scope

**Includes:**
- Scenario start conditions: Thomas 16-card deck, V-shape map setup, dummy player setup
- Round structure: Tactics selection → player turns → dummy player turn → round end when dummy deck empties
- Day/Night alternation: rounds 1+3 Day, rounds 2+4 Night
- Tactics decks: Day (1–6) and Night (1–6); player picks one, dummy gets random remainder; both discarded; lower number goes first
- Tactics effect resolution: resource effects via Effect System; structural effects (reserve die, reshuffle, draw specific card) via their own resolution path
- **Dummy player AI — explicit acceptance criterion:** dummy makes legal moves. Verify via dummy-only run log showing each action and its legality. "Dummy player works" is not sufficient.
- Round end sequence: offer row advancement, dummy deck reshuffle
- Win condition: city tile discovered → "Objective Achieved!" screen with full fanfare → victory declared next turn
- Loss condition: round 4 ends without city found → end summary screen
- End summary screen: Fame, sites conquered, enemies defeated, distance to city if unfound

**Excludes:**
- Art/audio polish (Epic 9)
- Google Play achievements (Epic 10)

### Dependencies
Epics 3, 4, 5, 6 (all gameplay systems complete).

### Deliverable
A complete, playable First Reconnaissance run from setup to win or loss. Every round, every site type, every combat phase — start to finish. Both end states reach their respective screens.

### Stories
- As a player, I select a Tactics card at round start so I have a round-long advantage
- As a player, the lower-numbered Tactics goes first so turn order is a real decision
- As a player, the round ends when the dummy deck empties so time pressure is tangible
- As a player, I discover the city tile and see "Objective Achieved!" with full fanfare so the win moment lands
- As a player, I see an end summary when the run ends so every decision feels meaningful in retrospect

### UI Verification
Round/turn tracker. Tactics selection screen showing both cards. Dummy player turn log (what it drew, what action it took, legality flag). Force-state dev tools: force city reveal, force round 4 end, force knockdown — verify all end states without a full playthrough.

---

## Epic 8: UI/UX

### Goal
A complete UI pass — every game state clearly communicated. No player should ever be confused about what phase they're in, what they can do, or why a card is red.

### Scope

**Includes:**
- HUD: phase indicator, running totals, turn tracker, round number, Day/Night indicator
- Card detail expand-in-place (Play / Play Sideways / Power / Cancel)
- Tutorial tooltip system (rule reminders at decision points, surfaces each rule exactly once)
- Veteran tooltip toggle: persistent off after first run — survives app restarts
- Round pressure visual and audio escalation as round 4 approaches
- Tile count display (X countryside, Y core remaining)
- Context-sensitive help at all key decision points

**Notes:** Basic HUD (card preview, hex highlight) iterable from Epic 1b onward — don't wait for Epic 8 to start HUD work.

**Excludes:**
- Final art/audio assets (Epic 9)

### Dependencies
Epics 1a–7 (all mechanics — UI wraps them).

### Deliverable
Every game state is clearly communicated. Phase indicator is always visible. Tooltips fire at correct moments. Veteran toggle persists.

### Stories
- As a player, I always know the current phase from the HUD so I never play an illegal card by accident
- As a player, I see a tooltip the first time I encounter each rule so I learn inline
- As a veteran, I can permanently disable tooltips after my first run so I'm not slowed down
- As a player, I feel the round limit tightening through escalating visual and audio cues

### UI Verification
Phase indicator stress test (all phases, all illegal states). Tooltip trigger coverage checklist (each rule surfaces exactly once at the right moment). Veteran toggle persists across app restarts. Round pressure cues verify on force-advanced round counter.

---

## Epic 9: Art + Audio

### Goal
Replace all placeholder assets with final art and audio, highest-impact first. SFX in the first sprint — not last.

### Priority Order

1. **Core SFX set** — card play, movement, combat hits, level-up, tile reveal, wound, phase change. Implement alongside core mechanics, not as a polish pass.
2. **Site type silhouettes** (14 types) — must pass 48px readability test before integration
3. **Card frames and mana color iconography** — always on screen, highest-stakes visual assets
4. **Ambient music** — exploration, combat, deep core / round pressure
5. **"Objective Achieved!" fanfare** — highest-priority audio asset; biggest moment in the game
6. **Hero token, enemy token silhouettes** (6 colors)
7. **Full art polish pass**

**External asset process:** Follow ATTRIBUTION.md pipeline — legal validation before integration, all placeholders tagged `[PLACEHOLDER]`.

### Dependencies
Epic 8 (UI complete — all asset slots defined).

### Deliverable
A build with no placeholder assets. All 14 site icons pass the 48px readability test. All SFX set events have sound.

### UI Verification
48px silhouette readability test for all 14 site icons. Day/Night WorldEnvironment tween visual check. Sound event coverage — every entry in the SFX set triggers correctly during a full run.

---

## Epic 10: Google Play + Release

### Goal
Complete Google Play integration and produce a release-ready build.

### Scope

**Includes:**
- Cloud save full implementation: sync policy, two-device conflict resolution, save schema versioning
- Achievements: 5–8 simple milestones, implemented last in the v1 cycle
- Local high score list
- Release build pipeline: APK/AAB signing, Play Store metadata, internal testing track submission

**Excludes:**
- Global leaderboards (post-v1)

### Dependencies
Epic 7 (full scenario — achievement triggers and score require a complete run).

### Deliverable
A signed release build submitted to Google Play internal testing track.

### Stories
- As a player, my save syncs to the cloud when I reconnect so I can resume on another device
- As a player, I earn achievements for meaningful milestones so my progress is recognized
- As a player, I can see my high scores so I have a target to beat

### UI Verification
Cloud save sync verified on two-device scenario. Achievement triggers verified against milestone list. High score list sorts and persists correctly across reinstalls.
