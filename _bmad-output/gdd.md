---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14]
inputDocuments: []
documentCounts:
  briefs: 0
  research: 0
  brainstorming: 0
  projectDocs: 0
workflowType: 'gdd'
lastStep: 14
project_name: 'maguswarrior'
user_name: 'John'
date: '2026-05-05'
game_type: 'card-game'
game_name: 'Magus Warrior'
---

# Magus Warrior - Game Design Document

**Author:** John
**Game Type:** Card-Driven Tactical Adventure (hybrid)
**Target Platform(s):** Android (v1), iOS (post-v1)

---

## Executive Summary

### Game Name

Magus Warrior

### Core Fantasy

"I am an unstoppable force becoming MORE unstoppable." The arc from struggling to move two hexes to commanding the battlefield is the emotional spine of Magus Warrior. Every design decision — level-up weight, hand display, offer row UX, combat feedback — should serve this feeling.

### Core Concept

Magus Warrior is a faithful solo mobile adaptation of the Mage Knight Ultimate Edition board game, targeting Android (v1) via Godot 4 + C#. Players control a powerful Mage Knight character navigating a hex-grid world — defeating enemies, conquering sites, exploring ruins, and leveling up — in a card-driven tactical adventure where every action flows through a hand of cards drawn from a personal deck.

Cards are the atomic unit of all player decisions: movement, combat, mana generation, healing, and influence. Cards are played normally for their printed effect or "sideways" for a single point of a basic resource (Move, Attack, Block, or Influence). Over the course of a scenario, players advance basic cards into powerful Action Cards, recruit units, and learn spells, building a stronger deck as the run progresses.

### MVP Scope (v1)

The first playable build targets a complete, rules-accurate run of the First Reconnaissance solo scenario using a single hero, Thomas (mechanically Tovak, renamed). Scope is intentionally narrow to enable full end-to-end game testing before expanding.

**In scope for v1:**
- Hero: Thomas only (one starting deck)
- Scenario: First Reconnaissance (solo)
- Map: Fixed countryside tiles (count per rulebook) + 3 semi-random core tiles (2 non-city, 1 city tile shuffled)
- Sites: All base game site types (Site Interaction LLD required before implementation)
- Card pool: Thomas's starting deck + full base game shared offer rows (advanced actions, spells, units)
- Enemies: Full base game enemy set (Enemy Effect LLD required; implementation gated by tier)
- City tile: renders on hex map, hidden until discovered; win condition triggers the turn after discovery
- Platform: Android 10+, Galaxy S21 / equivalent mid-range 2021 hardware

**Explicitly out of scope for v1:**
- Additional heroes
- Additional scenario types
- Multiplayer (solo-only architecture; no design obligation to accommodate multiplayer — accept refactor if added later)
- Expansions
- City combat system (all 12 tiers)
- Non-combat city interaction (TBD — verify against rulebook before implementing)

### "Faithful" Definition

Faithful means true to the *feel* of Mage Knight — the tension of hand scarcity, the satisfaction of a well-executed combo, the pressure of the scenario timer — not a strict 1:1 port of physical mechanisms. Where a physical mechanism has no clean digital equivalent, a design decision will be logged explicitly and evaluated on its own merits. Rules accuracy is the default; feel is the arbiter.

Content not listed in the v1 scope will be added in future milestones, each requiring their own scope sign-off before implementation begins.

### Session Model

Magus Warrior uses continuous autosave/resume. A scenario is one logical session that can be interrupted and resumed at any point. Full game state must be serializable at every decision boundary. There are no discrete "save points."

### Platform & Technology

| Constraint | Decision | Rationale |
|---|---|---|
| Engine | Godot 4 + C# | Continuation; C# typing suits complex game state; no royalties |
| Primary platform | Android v1 | iOS deferred to post-v1 |
| Target device | Samsung Galaxy S21 (Snapdragon 888, 6GB RAM) | Developer test device; capable mid-range 2021 hardware |
| Min Android version | Android 10+ (TBD — confirm before architecture) | |

### Win / Failure Condition (First Reconnaissance)

Win: the scenario ends in victory the turn after the player discovers the city tile. Failure: the scenario timer expires before the city is found. There is no hero death mechanic. "Knockdown" (hand fully replaced by wounds) is a distinct combat state — the hero cannot play cards, but recruited units continue fighting independently.

End of scenario (win or fail): present a summary screen showing Fame score, sites conquered, enemies defeated, and distance to the city if unfound. Failure should feel like a meaningful story — "I pushed too hard, I ran out of time" — not a silent wipe.

### Card Interaction Model

Every card effect that requires a player choice must have an explicit, documented interaction model — specifying the effect category, choice type, UI prompt, legal combat phase(s), and system behavior when preconditions fail.

The GDD establishes the taxonomy: effect categories, choice type definitions, and a phase legality framework table. Four LLDs follow the GDD and must all be signed off before architecture begins:

1. **Effect System LLD** — every in-scope card mapped to the interaction taxonomy
2. **Enemy Effect LLD** — enemy complexity tiered; all tiers specified before enemy implementation begins
3. **Site Interaction LLD** — site-specific logic and interaction sequences
4. **UX LLD** — screen contracts, HUD layout, touch interaction model, phase legality display, gesture stack, all zone state displays (card, unit, mana, day/night), site modal flows, and undo feedback. Authored with Sally (BMM UX agent) as lead. Architecture cannot define view layers or input systems without this document.

Architecture does not begin until all four LLDs are signed off. This is a hard gate, no exceptions.

**Polymorphic Effect System:** Card effects, skill effects, and unit effects are not three different systems — they are all instances of the same base Effect class. A card that provides Move 2, a skill that provides +1 Block, and a unit that generates Influence out of combat all resolve through the same interface. The Effect System LLD must reflect this: every in-scope card, skill, and unit effect is mapped to the unified effect taxonomy. This is the foundational architectural decision for all gameplay resolution.

### Game Type

**Type:** Card-Driven Tactical Adventure (hybrid)
**Frameworks:** Card game (deck building, card effect resolution, hand management) + Turn-based tactics (hex grid, combat positioning, resistances) + RPG (character leveling, skill progression)

### Target Audience

{{target_audience}}

### Unique Selling Points (USPs)

See USPs section below.

---

## Target Platform(s)

### Primary Platform

Android (v1). iOS post-v1.

### Platform Considerations

| Concern | Decision |
|---|---|
| Engine | Godot 4 + C# |
| Target device | Samsung Galaxy S21 (Snapdragon 888, 6GB RAM) |
| Min Android version | Android 10+ (confirm before architecture) |
| Performance target | 30fps (battery-conscious; turn-based game with minimal animation); max frame time TBD in architecture doc |

### Control Scheme

Touch-first throughout. Gestures locked for v1:
- Tap: primary action (select hex, play card, confirm)
- Pinch-to-zoom + pan: hex map navigation
- Long-press: card detail view and hex movement preview

Gesture disambiguation (tap vs. pinch vs. long-press vs. pan all begin with fingers on screen) is a stateful input system requiring explicit design — flagged for the architecture doc, to be validated on the Galaxy S21.

Additional gestures deferred to UX iteration. UX is an identified weak point requiring dedicated design work each sprint.

### Platform Features (v1)

- Cloud saves (Google Play)
- Achievements: minimal set (5–8), simple milestones, implemented last in the v1 cycle
- Local high score list
- Global leaderboards: post-v1 (requires solved balance and fair scoring formula first)

### Interruption Handling

Phone calls and OS interruptions suspend the app with no warning. The game hooks `NOTIFICATION_APPLICATION_PAUSED` in Godot 4's `_notification()` — the last guaranteed callback before Android kills the process. Full game state is written atomically to disk on this notification.

If the OS kills the process before serialization completes, the player resumes from the last committed state, not the exact moment of interruption.

**Save tiers:** local autosave (fast, frequent, device-only) and Google Play cloud save (rate-limited, cross-device) operate as two separate tiers. Promotion policy (which local saves sync to cloud and when), conflict resolution (player-on-two-devices scenario), and save schema versioning are known open decisions deferred to the architecture doc.

---

## Target Audience

### Primary Audience

Hardcore board game players and Mage Knight veterans. They know the rules, they want a faithful digital experience, and they will notice if something is wrong. Design decisions default to serving this audience.

### Secondary Audience

Board game enthusiasts who have not played Mage Knight. The game should be accessible without being simplified — complexity is a feature, not a problem to fix. New players are supported via context-sensitive help (tooltips, inline rules reminders) surfaced at the point of decision, not through tutorial menus or interruptive onboarding flows.

### Gaming Experience

Core to hardcore. Comfort with complex rule systems, long strategy games, and meaningful failure states.

### Session Length

Phone-native: designed for interruption. Sessions may be 5–10 minutes (one combat, a few hexes) or as long as the player wants. On resume, the game re-orients the player to their current context — active phase, staged cards, pending decisions — not just restores state silently.

### Player Motivations

Veterans: Mage Knight in their pocket, faithful to the game they love. Newcomers: tactical depth and deck-building arc on mobile.

---

## Goals and Context

### Project Goals

1. **Ship a complete, playable First Reconnaissance run** — rules-accurate, stable, with every card effect correctly specified and wired up. Succeed where the previous attempt failed.

2. **Mage Knight in your pocket** — a faithful solo experience playable on a phone, anywhere, without a table, setup, or a laptop.

3. **Learn Godot 4 + C#** — build genuine engine proficiency through building a real, complete game.

4. **Create something MK fans find worth playing** — quality and fidelity that respects the source material and the players who love it.

### Background and Rationale

Mage Knight Ultimate Edition is one of the most celebrated solo board games ever made. No faithful mobile adaptation exists — Mage Knight Digital (PC) is not available on mobile, and no other version fills the gap. This project exists because the designer wanted to play Mage Knight on his phone and decided to build it himself.

A previous attempt at this project (Godot 4 + C#) failed at the card interaction layer: effects requiring player choices had no UI prompts wired up, phase validation was inconsistent, and the card effect model was never explicitly specified. Magus Warrior is a deliberate restart with those failures built into the design process — not papered over.

---

## Unique Selling Points (USPs)

### 1. The Only Faithful Mage Knight on Mobile

Mage Knight Digital exists on PC but not on mobile. No phone-native faithful adaptation of Mage Knight exists. Magus Warrior fills that gap for MK fans who want the game on the go.

### 2. Eliminates Physical Friction, Preserves Depth

No setup, no teardown, no shuffling, no token sorting. Bookkeeping (Fame, wounds, mana, enemy stats, combat totals) is handled automatically. The game's complexity is a feature — the tedium of physical components is not.

### 3. Built for the Phone, Not Ported to It

Session design, interruption handling, and UX are designed mobile-first from day one. Five-minute sessions are as valid as two-hour runs. The game re-orients the player on every resume. This is not a PC game adapted for a small screen.

### Competitive Positioning

The target player is a Mage Knight fan who owns the physical game but doesn't always have access to it. Magus Warrior is not competing with the tabletop experience — it's extending it. When the table isn't available, the phone is.

---

## Core Gameplay

### Game Pillars

1. **Power Escalation** — Every system serves the arc from scrub to unstoppable. Level-up moments feel monumental. The deck grows stronger as you encounter various sites on the map. The player should feel the delta between turn 1 and turn 20.

2. **Meaningful Decisions** — Every hand is a triage under scarcity. No filler actions. The game never makes a choice for the player that the player would want to make themselves. When the answer is unambiguous, the system resolves it automatically — preserving player agency for the decisions that actually matter.

3. **Faithful Feel** — Rules accuracy is the default. When a physical mechanism has no clean digital equivalent, the feel it creates is preserved, not the mechanism. Nothing should make an MK veteran say "that's not how it works."

4. **Phone-Native** — Interruption-safe, session-length-agnostic, context-sensitive. The game respects the player's time and attention at every moment. A five-minute session and a two-hour session are equally valid.

**Pillar Priority (when they conflict):** Faithful Feel → Meaningful Decisions → Power Escalation → Phone-Native

### Core Gameplay Loop

**Micro loop (one turn, ~5–15 minutes):**
Draw hand → Survey options → Move across hex grid → Engage encounter (combat / site / influence / locals) → Play cards to resolve → End turn (rest or press on) → Repeat

**Macro loop (full scenario, multiple sessions):**
Start weak (basic deck, low level) → Earn Fame → Level up → Choose Advanced Actions from offer row → Visit sites: recruit units (villages/keeps), learn spells (Mage Towers), interact with locals → The deck grows stronger as you encounter various sites on the map → Discover city tile → Victory declared next turn

**Loop timing:** Single turn: 5–15 minutes. Full scenario: 1–3 hours across multiple sessions.

**What makes each iteration different:**
- Hand variance from deck shuffle
- New hex tile reveals change landscape and paths
- Varied enemy encounters with different resistances and abilities
- Interactions with locals (villages, monasteries, mage towers, keeps)
- Mana dice give different resources each round
- Evolving deck composition as Advanced Actions and Spells enter the pool

### Win/Loss Conditions

- **Victory:** Scenario ends the turn after the player discovers the city tile
- **Failure:** Scenario timer expires before city is found
- **No hero death:** Knockdown (hand fully replaced by wounds) is a distinct combat state — hero cannot play cards, recruited units continue fighting
- **End of scenario:** Summary screen showing Fame, sites conquered, enemies defeated, cards acquired, distance to city if unfound

### Day/Night Cycle

Day and Night alternate every round. In First Reconnaissance (4 rounds total): rounds 1 and 3 are Day; rounds 2 and 4 are Night. The current cycle is always visible to the player and affects three systems:

1. **Mana availability:** Gold mana is restricted to Day; Black mana is restricted to Night. Both restrictions are overridable by specific effects.
2. **Movement costs:** Some terrain has different Move costs during Day vs. Night. Full terrain cost table flagged for LLD.
3. **Spell powering:** Powered spells require Black mana — practically Night-only unless an effect generates Black mana during Day.

The visual transition (WorldEnvironment tween) reflects the current cycle at all times.

### Tactics

At the start of each round, the player selects one Tactics card from the current round's deck (Day deck on rounds 1 and 3, Night deck on rounds 2 and 4). The dummy player receives one randomly from the remaining cards. Both are discarded from the game after selection. Each deck contains 6 cards numbered 1–6.

**Turn order:** The player holding the lower-numbered Tactics card goes first that round. Tactics selection is a meaningful decision: high-value effects may cost first-player advantage.

**Effect categories (design flag):** Tactics effects split into two categories that may require different resolution paths:
- **Resource effects** (move bonus, mana generation, extra card draw) — likely clean Effect System citizens
- **Structural effects** (reserve a mana die, reshuffle deck, draw a specific card from deck) — operate on game state machinery, not resources; may need their own resolution path or an extension to the Effect System

The Effect System LLD must explicitly categorize each Tactics card effect and decide whether structural effects extend the Effect System or route separately. This is a known open design question; do not assume all Tactics effects resolve identically.

Full Tactics card list (Day and Night, 6 each) flagged for Effect System LLD.

---

## Game Mechanics

### Primary Mechanics

**1. Card Play**
The atomic unit of every player decision. Each card can be played in three ways: normally for its full printed effect; sideways for 1 point of a basic resource (Move, Block, Attack, or Influence — player's free choice, a game rule not printed on the card); or powered using a mana token to unlock an enhanced effect. Every hand is a triage between full card effects and resource conversion under scarcity.

Sideways constraints: cards played sideways cannot contribute to Ranged or Siege Attacks. Sideways cards played during the Block phase always provide physical Block 1 — never elemental. Wound cards cannot be played in any way.

**2. Hand Management**
At the start of each turn the player draws up to their current hand size (starting hand size: 5). Cards in hand are the only source of all resources — movement, combat, mana, and influence all flow through the hand. Sequencing matters: some effects depend on order of play. Hand size is a character stat that increases at specific odd-numbered levels.

**3. Hex Movement**
The player spends Move points to cross hexes. Terrain type determines cost; costs vary by round (Day/Night cycle — shown on the Day/Night board). Revealing a new map tile costs 2 Move points. Moving into an unexplored hex reveals new map content. Move point generation scales through deck quality — better cards and skills generate more Move per turn — but terrain costs are fixed by the map. Terrain cost specifics flagged for LLD.

**4. Combat Resolution**
Enemies are encountered at hexes and must be defeated to access sites. Combat has four phases: Ranged and Siege Attack → Block → Assign Damage → Attack (melee). Phase detail is LLD. Enemies have elemental resistances that constrain which attack types are effective. Starting Armor: 2. Knockdown — hand fully replaced by wounds — is the hero's failure state within combat; recruited units continue fighting independently.

**5. Resource Management**
Three resource categories operate simultaneously:

- **Mana:** Six colors — Red (fire), Blue (ice), Green (earth), White (wind/spirit), Gold (Day wildcard — counts as any color; restricted to Day use by default), Black (Night mana — used to power spells; restricted to Night use by default). Gold and Black restrictions are symmetric: neither can be used outside their cycle unless a specific effect explicitly permits it. Mana tokens are generated by: mana dice in the Source, converting crystals from inventory, or specific card/skill/unit effects. Crystals (up to 3 per color in hero inventory, 12 total) convert to mana tokens at any point during your turn. Mana tokens disappear at end of turn.
- **Crystals:** Carried in hero inventory (max 3 per color); acquired at sites. Persistent between turns.
- **Fame:** Earned through combat, site conquest, tile revelation, and specific card/skill/unit effects. Tracks score and gates level-ups. Persistent.
- **Reputation:** A persistent track. Current position provides a modifier (positive = Influence bonus, negative = Influence penalty) applied to every Influence calculation during interactions. Modified by deeds (defeating rampaging enemies raises it; assaulting sites, burning monasteries, plundering villages lowers it) and by specific card/skill/unit effects. At the X position (track floor), Influence is unusable and all local interaction is blocked. Load-bearing for encounter economy and Influence-generating card value.

**6. Deck Acquisition**
The mechanism by which the deck grows stronger over the course of a scenario:

- **Learn:** At each even-numbered level-up, gain 1 Advanced Action from the offer row and 1 Skill tile.
- **Recruit (Units):** Spend Influence at Villages, Keeps, Monasteries, Mage Towers, or Cities to add unit cards to the deck.
- **Acquire:** Purchase cards at sites — Advanced Actions at Monasteries (6 Influence), Spells at Mage Towers (7 Influence + matching mana).
- **Reward:** Receive Advanced Actions, Spells, or Artifacts as rewards for exploring sites.

Artifacts are a distinct card type with their own acquisition path and powering mechanic — detail LLD.

**7. Rest / Press On**
At end of turn the player chooses to rest or press on. Two rest types:

- **Standard Rest** (at least one non-Wound card in hand): discard 1 non-Wound card + any number of Wound cards to the discard pile. Wounds cycle back into the deck as it reshuffles.
- **Exhaustion** (only Wounds in hand): reveal hand, discard 1 Wound to the discard pile.

When Resting: no movement, no combat, no interactions — Special and Healing effects may still be played.

**Healing** is distinct from cycling: spending 1 Healing point permanently removes 1 Wound from your deck (returned to the shared Wound pile). Future combat may add new Wounds. All Wound cards are identical — fungible, drawn from a shared supply.

---

### Mechanic Interactions

| Interaction | Description |
|---|---|
| Card Play ↔ All | Every resource flows through card play — Move, Attack, Block, Influence, and mana are all generated by the hand |
| Crystals + Mana Dice → Card Play | Two inputs to the same output; crystal inventory supplements unpredictable mana dice |
| Deck Acquisition → Card Play | Every acquired card, spell, or unit strengthens future hands; quality compounds over the scenario |
| Hand Management ↔ Rest | Rest resets hand quality; the rest/press-on decision is fundamentally a hand quality judgment |
| Hex Movement → Combat | Moving into an enemy-occupied hex triggers combat; movement and combat are sequentially coupled |
| Fame → Deck Acquisition | Level-ups are Fame gates; each level-up triggers an acquisition moment |
| Reputation ↔ Interaction | Reputation modifies Influence during all local interactions; at X, interaction is blocked entirely |

---

### Mechanic Progression

| Mechanic | How it evolves |
|---|---|
| Card Play | Starts with Thomas's basic deck; compounds as Advanced Actions, Spells, Units, and Artifacts enter the pool |
| Hand Management | Hand size increases at specific odd levels; more options per turn late-game |
| Hex Movement | Unchanged mechanically; Move point generation scales through deck quality |
| Combat Resolution | Armor increases at specific odd levels; enemy difficulty scales with map depth |
| Resource Management | Crystal inventory grows through site interactions; mana color variety increases strategic options |
| Deck Acquisition | Every level adds capability; Skill tiles at even levels compound existing mechanics |
| Rest / Press On | Strategic depth increases as deck grows — more at stake in rest timing decisions |

---

### Level-Up Cadence

| Level type | Rewards |
|---|---|
| **Odd (3, 5, 7…)** | +1 unit command capacity (new Command token); stat increase (Armor at level 3, hand size at level 5, alternating) |
| **Even (2, 4, 6…)** | +1 Skill tile; choose 1 Advanced Action from the offer row |

---

### Controls and Input

#### Control Scheme (Android — touch)

| Input | Action |
|---|---|
| Tap | Primary action: select hex, play card, confirm |
| Pinch-to-zoom + pan | Hex map navigation |
| Long-press | Card detail view; hex movement preview |

#### Card Interaction

- Tap a card → expands in place showing full text and action buttons: Play / Play Sideways / Power / Cancel
- Played cards accumulate in a staging area; HUD shows running totals (Attack: 5, Move: 3, etc.)
- Action commits when the player executes; undo pulls cards back from staging
- Undo is free until new information is revealed (card drawn, tile flipped, enemy drawn, die rolled)
- Illegal cards rendered in red in hand — visible but not tappable; current phase is a prominent HUD element

#### Input Feel

Touch-first throughout. Tap targets sized for Galaxy S21 baseline. Gesture disambiguation (tap vs. pinch vs. long-press vs. pan) flagged for the architecture doc.

#### Accessibility Controls

Deferred to post-v1.

---

## Card-Driven Tactical Adventure Specific Design

### Card Type Taxonomy

Magus Warrior uses six distinct card types. Three types are part of the **deed deck** (the player's personal draw pile and discard pile). One type occupies a separate zone. One type is purely negative content that accumulates through damage.

#### Basic Action Cards

- Part of the deed deck
- Can be played normally for their printed effect or sideways for a basic resource (player's free choice)
- Multiple copies of the same Basic Action card may coexist in the deed deck simultaneously
- Cannot be powered unless a specific card effect explicitly enables it
- Wound cards are a special subtype: cannot be played in any way; exist solely as hand-clogging penalty content

#### Advanced Action Cards

- Part of the deed deck
- More powerful than Basic Actions; always have a powered version requiring mana
- **Uniqueness constraint:** Only one copy of each Advanced Action may exist across all zones simultaneously — deed deck, discard pile, hand, and play area are all counted. Acquiring a duplicate is illegal; the system must prevent it.
- Gained at even-numbered level-ups (choose from offer row) or purchased at eligible sites

#### Spells

- Part of the deed deck
- Always cost mana to use — unlike Action cards, there is no free unpowered play:
  - **Unpowered spell:** costs 1 mana token of the spell's basic color → lesser effect
  - **Powered spell:** costs 1 mana token of the spell's basic color + 1 Black mana token → greater effect
- Black mana is normally only available at Night, making powered spells a Night-favored option (unless an effect generates Black mana during Day)
- **Uniqueness constraint:** Only one copy of each Spell may exist across all zones simultaneously. Acquiring a duplicate is illegal.
- Acquired at Mage Towers (7 Influence + matching mana token) or as site exploration rewards

#### Artifacts

- Part of the deed deck
- Two play modes:
  - **Unpowered:** Played for a lesser effect; card stays in the deed deck after use
  - **Powered (Breaking):** Played for a more powerful effect; breaking the artifact is the powering cost — no mana token required. The card is **removed from the game after the effect resolves**.
- Artifacts are the only card type powered by removing themselves rather than spending mana.
- **Uniqueness constraint:** Only one copy of each Artifact may exist across all zones simultaneously. Acquiring a duplicate is illegal.
- Acquired as site exploration rewards; not purchasable at standard sites

#### Wound Cards

- Subtype of Basic Action; part of the deed deck
- **Cannot be played in any way** — always rendered in red and untappable in the UI regardless of phase
- Accumulate in the deed deck as the hero takes combat damage
- **Throw away (type-conditional algorithm):**
  - Non-Wound card thrown away → permanently removed from the game (returned to shared supply; cannot be reacquired this run)
  - Wound card thrown away → returned to the shared Wound supply pile (not removed from the game)
- All Wound cards are identical and fungible — there are no named Wound subtypes
- Permanently removed from the deed deck by spending Healing points (returned to shared Wound supply)

#### Unit Cards

- **NOT part of the deed deck** — occupy a separate recruited-units zone
- Not drawn as part of the regular hand draw
- Recruited at sites by spending Influence
- **Once per round:** Units can generally only be used once per round. After activation (combat or out-of-combat effect), a unit is exhausted and cannot be used again until the next round.
- Some units have **out-of-combat effects** that generate Move, Influence, Mana, or Crystals during the appropriate phase — these resolve through the same polymorphic Effect System as card and skill effects
- Units contribute combat abilities when deployed in combat
- **Damage absorption:** Units can absorb damage on behalf of the hero. A unit that has absorbed damage is wounded — it cannot be used again until healed. Unit healing is distinct from hero wound healing (hero wounds are removed from the deed deck; unit healing restores the unit to ready state).
- Uniqueness is enforced within the recruited-units zone only; units and deed cards are separate systems
- Units do not enter the discard pile and do not participate in deck cycling

---

### Thomas's Starting Deed Deck (16 cards)

Thomas begins every scenario with this fixed 16-card deck. 14 cards are shared across all heroes; 2 are Thomas-specific. Nothing carries over between scenario runs — the deck resets fully on every new game.

| Card | Copies | Type | Primary Function |
|---|---|---|---|
| Stamina | 3 | Basic Action | Healing and blocking |
| Swiftness | 2 | Basic Action | Movement and blocking |
| March | 2 | Basic Action | Movement |
| Rage | 2 | Basic Action | Attack |
| Threaten | 1 | Basic Action | Influence (Brutal option) |
| Promise | 1 | Basic Action | Influence (Noble option) |
| Battle Versatility | 1 | Basic Action | Flexible (Move, Block, or Attack — player's choice) |
| Crystallize | 1 | Basic Action | Mana acquisition (see Effect System LLD for exact resolution) |
| Tranquility | 1 | Basic Action | Healing or movement (see Tranquility Fizzle note below) |
| **Determination** | **1** | **Basic Action (Thomas-specific)** | **Move or Attack — player's free choice** |
| **Improvisation** | **1** | **Basic Action (Thomas-specific)** | **Context-determined resource (see Improvisation rule below)** |

**Total: 16 cards.** Per-card powered effects and exact rule text are specified in the Effect System LLD.

---

### Improvisation Resolution Rule

Improvisation is a context-determined card, distinct from sideways play. These are two different rules:

- **Sideways:** Always the player's free choice — Move 1, Block 1, Attack 1, or Influence 1. This is a universal rule printed nowhere on any individual card; it applies to all non-Wound cards.
- **Improvisation:** The current game phase when the card is played fixes the resource type. The player has no choice.

| Phase when Improvisation is played | Resource provided |
|---|---|
| Movement phase | Move 1 |
| Block phase | Block 1 — physical, generic; not elemental; does not count as Ranged or Siege Block |
| Attack phase | Attack 1 — melee, physical; does not count as Ranged or Siege Attack |
| Influence phase | Influence 1 |
| Special / Healing phase | Healing 1 |

**Wound discard:** Wound cards cannot be discarded to fuel Improvisation. Improvisation has no discard cost — it is played as a standard card play action.

**Powered Improvisation:** Power → 2 resources of the same context-determined type. Same phase-to-type mapping applies. Exact powered effects are confirmed in the Effect System LLD.

---

### Terminology Reference

Canonical definitions for this project. All downstream LLDs use these terms as defined here.

| Term | Definition |
|---|---|
| **Deed deck** | The player's personal card draw pile + discard pile. All card play, acquisition, and Wound accumulation refers to this deck. |
| **Sideways** | Playing a card rotated 90° for a basic resource. Always the player's free choice: Move 1, Block 1, Attack 1, or Influence 1. Sideways Block is always physical (not elemental). Sideways play cannot produce Ranged or Siege Attack. |
| **Powered** | Unlocking a card's enhanced effect by paying its powering cost. Cost varies by card type: Action cards cost one mana token; Spells cost one basic color mana token + one Black mana token (two tokens total); Artifacts are powered by breaking — removing the card from the game after the effect resolves, no mana required. Gold is restricted to Day use by default; Black is restricted to Night use by default. Both restrictions can be overridden by specific effects. |
| **Throw away** | Type-conditional discard: non-Wound cards thrown away are permanently removed from the game (to shared supply); Wound cards thrown away are returned to the shared Wound supply pile. |
| **Remove from game** | Permanent exit from the deed deck — card goes to shared supply and cannot be reacquired this run. Artifact strong effects trigger removal at declaration. |
| **Healing** | Spending Healing points to permanently remove Wound cards from the deed deck. Wounds removed by Healing return to the shared Wound supply. Distinct from rest-based wound cycling. |
| **Rest cycling** | Wounds discarded during a Rest action go to the discard pile. They return to the deck on the next shuffle — they are not permanently removed. |
| **Standard Rest** | Rest action available when at least 1 non-Wound card is in hand: discard 1 non-Wound card + any number of Wound cards to the discard pile. |
| **Exhaustion** | Rest action when only Wound cards are in hand: reveal hand, discard exactly 1 Wound to the discard pile. |
| **Command capacity** | Maximum number of units the hero may have recruited simultaneously. Increases by 1 at each odd-numbered level-up (via Command token). |
| **Offer row** | The face-up display of Advanced Actions available for acquisition during level-ups or at certain sites. |

---

### Deck Construction Rules

1. **Thomas's starting deck is fixed:** 16 cards, always the same composition, always fully reset on every new scenario run.

2. **Uniqueness constraint (AAs, Spells, Artifacts):** Only one copy of each Advanced Action, Spell, or Artifact card may exist across all zones simultaneously — deed deck, discard pile, hand, and play area are all counted. The system must enforce this; duplicate acquisition must be blocked at the acquisition step.

3. **Basic Actions allow duplicates:** Multiple copies of the same Basic Action card may coexist in the deed deck (Thomas starts with 3 copies of Stamina, for example).

4. **Wounds are fungible and supply-limited:** All Wound cards are identical. The constraint is the shared physical supply size; there is no per-deck Wound limit.

5. **Units are not deed cards:** Recruited units occupy a separate zone and are not subject to deed deck construction rules. Unit uniqueness is enforced within the recruited-units zone only.

6. **No cross-run persistence:** The deed deck resets fully at the start of every run. Fame, Reputation, and Crystals do not carry over. Every run begins from Thomas's fixed 16-card starting deck.

---

### Open Design Flags (Effect System LLD)

The following items require explicit per-card decisions in the Effect System LLD. They are flagged here as known open questions; none block GDD completion.

| Flag | Item | Default assumption |
|---|---|---|
| Tranquility fizzle | If "Heal 1" is chosen with no Wounds in the deck, the effect resolves as no-op (valid play, zero healing) | No invalid play state; card is always legal if Healing is a valid phase action |
| Crystallize mana | Is Crystallize's mana gain an activation cost (requires a mana source) or a free conversion effect? | Free conversion; flagged for LLD |
| Terrain costs | All terrain movement costs, including Day/Night variants | Fully flagged for LLD; not resolved in GDD |

---

## Progression and Balance

### Player Progression

Magus Warrior features **within-run power progression** with no meta-progression between runs. Every run starts fresh with Thomas's 16-card starting deck. Mastery comes from player skill accumulating across sessions — learning the map, enemies, card synergies, and site timing — not from persistent unlocks. Post-v1 replayability is addressed through additional scenarios; First Reconnaissance is intentionally the complete v1 experience.

#### Progression Types

| Type | Implementation |
|------|---------------|
| **Power** | Thomas grows stronger within each run via Advanced Actions, Spells, Artifacts, Units, and Skills |
| **Skill** | Player knowledge accumulates across runs — map patterns, enemy behaviors, card synergies |

#### Progression Vectors (Within a Run)

- **Advanced Actions** — offered at level-up and select sites; added to deed deck
- **Spells** — offered at monasteries and level-up; added to deed deck; potency varies by Day/Night
- **Artifacts** — found at sites; powerful one-shot effects; removed from game at declaration
- **Units** — recruited at villages and monasteries; occupy the units zone (not deed deck)
- **Skills** — chosen at level-up: draw 2 from your personal skill pile, then either (a) keep one and place the other in the common skills offer, or (b) take a skill from the common offer and place both drawn skills there — but if you take from the common offer you must take the Advanced Action at the end of the offer rather than choosing freely. You may only select skills from the common offer that belong to other players, not your own. In solo play, the dummy player randomly contributes one skill to the common offer each time Thomas levels up.

#### Progression Pacing

Thomas gains levels by accumulating Fame, starting at level 1 and capping at level 10 — a maximum of 9 level-ups per run. A typical First Reconnaissance run will reach a subset of that cap. Each level-up grants a skill selection and triggers a card offer. The run arc across First Reconnaissance:

- **Early game:** Fragile Thomas, weak deck, cautious exploration
- **Mid game:** Power coming online, map knowledge accumulating, card synergies emerging
- **Late game:** Round limit pressure forces hard decisions — push for the city tile or consolidate power first

---

### Difficulty Curve

Magus Warrior uses a **fixed challenge** — no difficulty settings. This is consistent with the board game and respects the player's ability to learn and adapt.

#### Challenge Scaling

Difficulty escalates organically through map and round structure rather than artificial tuning:

- Unexplored tiles hide unknown threats, keeping early game tense
- Enemy strength scales with distance from the starting tile
- The round limit creates a hard late-game deadline — the race to discover the city tile forces risk-taking

#### Difficulty Options

None. Fixed challenge only. Players who struggle improve through repeated runs and deeper card knowledge.

---

### Economy and Resources

Magus Warrior implements the full Mage Knight resource system: three persistent tracked resources (Mana crystals, Fame, Reputation) and two turn-generated currencies (Mana tokens, Influence). They are deeply interdependent and all are in scope for v1.

#### Resources

**Persistent tracked resources:**

| Resource | Earned By | Spent On |
|----------|-----------|----------|
| **Mana crystals** | Certain cards/abilities, mines, rewards from certain sites | Powering spells and card effects |
| **Fame** | Combat, site clears, exploration | Level-ups (skills + card offers) |
| **Reputation** | Site interactions, combat choices, certain cards | Determines Influence bonuses/penalties at sites; too low prevents interaction entirely |

**Design rationale for Reputation:** Reputation is a load-bearing balance lever, not a cosmetic track. Rampaging Orcs (weakest enemies, least Fame reward) must grant Reputation to make them worth engaging. Cards that grant Reputation lose meaningful value without the track. Implementation cost is low — single track, single output (Influence modifier + interaction gate).

**Turn-generated and temporary:**
- **Mana tokens** — generated by spending one of the three Source dice on your turn; disappear at end of turn
- **Influence** — produced by playing cards for their influence value; spent at sites within the same phase

#### Economy Flow

Three Mana Source dice are rolled at the start of each round and remain visible to the player. Each turn the player may use one die from the source to generate a mana token of that color. That die is then re-rolled and returned to the source at end of turn, keeping three dice available throughout the round.

**Exhausted dice:** When a die shows Black during a Day round, or Gold during a Night round, that die is exhausted — it cannot normally be selected for the rest of the round. Exhausted dice remain visible in the Source but are unavailable. Specific effects may override this restriction.

---

### Dummy Player

The dummy player represents one of the other Magus Warriors, selected at game start. It serves as round timer, card offer competitor, and skill offer contributor.

**Starting state:** 16-card deck (4 cards of each color), 3 mana crystals (2 of one color, 1 of another). Turn order (first or second) determined randomly.

**On its turn:** Draws 3 cards. If the 3rd card matches the color of any crystal it holds, it draws additional cards equal to the number of crystals of that color. This extra draw triggers once only.

**End of Round trigger:** When the dummy player begins a turn with an empty draw pile, it declares End of Round. The player takes one final turn, then the round ends.

**Between rounds:**
1. The farthest Advanced Action in the offer enters the dummy's deck; remaining cards slide down, new card fills the top
2. The farthest Spell card's color becomes a crystal added to the dummy's supply; that spell is removed from the game; offer slides and refills
3. Dummy's deck is reshuffled with the new Advanced Action; next round begins

**Skill contribution:** Each time Thomas levels up, the dummy player contributes one randomly drawn skill from its pile to the common skill offer pool.

**Offer structure note:** The Advanced Action and Spell offers are positional queues — cards have explicit position (closest to deck = newest, farthest = oldest). The Skill offer is a pool with no positional state.

---

## Level Design Framework

### Structure Type

Magus Warrior uses a **procedural single-level** structure. Each run is one complete hex map — there are no discrete stages or levels in the traditional sense. The entire map constitutes one "level" that the player navigates from start to win condition.

The First Reconnaissance map is V-shaped, defined by the starting tile's coastline. It is composed of:
- **Countryside tiles (green back):** 8 tiles representing the less-developed outer areas; always revealed first, in fixed number order
- **Core tiles (brown back):** 3 tiles (2 non-city + 1 city) shuffled together; always revealed after all countryside tiles, forming a spatial cluster at the deep end of the V

Core tiles always occupy the deepest region of the V — the city is a felt destination, not a random appearance. Procedural variety comes from the shuffled core tile order and enemy token draws. No two runs are identical, but the spatial structure (countryside → core) is consistent every run.

**Tile count is open information:** the UI displays remaining tiles (e.g. "3 countryside and 2 core tiles remaining"), giving players real-time pacing feedback and the ability to track when they're entering core territory.

**Tile revelation:** costs 2 Move points. The player must occupy a space bordering empty table space (not behind the coastline). If two tiles could be revealed from one space, the player must announce which before drawing. Countryside tiles must be placed adjacent to at least 2 existing tiles; Core tiles same rule. *(A hex adjacency diagram is required in documentation — prose descriptions of hex grid placement rules introduce ambiguity. Flag for documentation sprint.)*

**First Reconnaissance special rule:** each tile revealed grants the player +1 Fame — exploration is mechanically rewarded, not just strategically useful.

---

### Enemy Color Taxonomy

Six enemy token colors, each a separate pile with internal variance. Color indicates which pile to draw from at each site. Full enemy taxonomy (per-token stats, abilities, and encounter rules) is in the Enemy Effect LLD.

| Color | Enemy Type | Difficulty | Used At |
|---|---|---|---|
| **Green** | Orcs | Easiest | Rampaging Orcs (map setup) |
| **Gray** | Keep garrison | 2nd easiest | Keeps |
| **Purple** | Mage enemies | ~Gray, more varied | Mage Towers, Monastery defense |
| **Brown** | Monsters | Fairly tough | Dungeons, Monster Dens, Spawning Grounds |
| **Red** | Dragons | Very difficult | Tombs, Draconum (rampaging) |
| **White** | *(unnamed)* | Toughest (≈ Red) | Ruins only (v1 scope) |

**Difficulty order:** Green < Gray ≈ Purple < Brown < Red ≈ White

---

### Site Types

Sites are the primary content units of the map. 14 site types in v1. Full interaction resolution for all sites is in the Site Interaction LLD.

**Countryside and Core Tiles:**

| Site | Type | Primary Interactions |
|---|---|---|
| **Village** | Friendly | Recruit units; buy healing (3 inf/1 healing); Plunder (draw 2 cards, −1 reputation; between turns, not an action) |
| **Keep** | Fortified | Assault (−1 rep, gray enemy token, all enemies fortified); conquered: recruit units, +1 hand size per conquered keep when starting within 1 tile |
| **Monastery** | Friendly / Burnable | On reveal: add 1 AA to unit offer; each unburnt monastery adds 1 AA/round; buy AAs from unit offer (6 inf); buy healing (2 inf/1 healing); recruit units; burn attempt: −3 rep, fight purple enemy — win = artifact (draw 2, keep 1) |
| **Mage Tower** | Fortified | Assault (−1 rep, purple enemy token, all enemies fortified); win assault: free spell from spell offer; buy spell (7 inf + matching mana); recruit units |
| **Magical Glade** | Passive | End of turn: throw away 1 wound from hand or discard pile (not healing — cannot heal Units; allows discard pile search which regular healing cannot); start of turn: gain gold mana token (Day) or black mana token (Night). Accessible while Resting. |
| **Crystal Mine** | Passive | End of turn: gain a crystal matching the mine's color. Accessible while Resting. |
| **Dungeon** | Combat (repeatable) | Night rules always apply; units cannot assist; fight brown enemy token; win: roll mana die at end of turn → gold/black: spell from spell offer; any other color: artifact (draw 2, keep 1). Repeatable: subsequent visits draw new brown token — win = Fame only |
| **Monster Den** | Combat (one-time) | Fight brown enemy token; win: roll spare mana die twice → basic color: crystal of that color; gold: crystal of any color; black: +1 Fame. Cannot be revisited |
| **Ruins** | Variable (two token types) | Draw ruins token. **Type 1:** fight one or more enemies (enemy color, count, and reward vary per token). **Type 2:** pay mana for Fame (mana color, amount, and Fame reward vary per token). Full token contents → Site Interaction LLD |
| **Rampaging Orcs** | Rampaging enemy | Green token placed face-up at setup; blocks movement through space; provoked by moving from one adjacent space to another adjacent to same token; win: Fame + 1 Reputation |

**Core Tiles Only:**

| Site | Type | Primary Interactions |
|---|---|---|
| **Spawning Grounds** | Combat (one-time) | Fight 2 brown enemy tokens; win: roll 3 mana dice → basic color: crystal of that color; gold: crystal of any color; black: +1 Fame per die; + artifact (draw 2, keep 1). Cannot be revisited |
| **Tomb** | Combat (repeatable) | Night rules always apply; units cannot assist; fight red enemy token; win: spell from spell offer + artifact (draw 2, keep 1). Repeatable: new red token — win = Fame only |
| **Draconum** | Rampaging enemy | Red token placed face-up; blocks movement; provoked same as Rampaging Orcs; win: +2 reputation |
| **City** | Win condition | *(Interface contract required before implementation — triggers victory the turn after discovery; entrance requirements, interaction sequence, and multi-phase assault logic TBD. Flag for Site Interaction LLD before architecture begins.)* |

---

### Assault Rules

Applies to Keeps, Mage Towers, and any other fortified sites. *(Resolution detail → Enemy Effect LLD.)*

- Each assault attempt costs −1 reputation
- All enemies treated as **fortified**: Ranged/Siege attack phase accepts Siege attacks only
- Enemies that are always fortified become **double fortified** during assault: cannot be targeted by Ranged or Siege attacks at all

---

### Artifact Acquisition Rule (Universal)

Whenever a player gains an artifact through any means, draw 2 from the artifact deck, keep 1. *(Resolution detail → Site Interaction LLD.)*

---

### Level Progression

No unlock system — the map is revealed organically through movement. Countryside tiles always come first; the 3 core tiles form the deep end of the V. The city tile is one of those 3, position unknown within that cluster.

Natural difficulty escalation by depth:
- **Early (countryside):** Villages, Glades, Mines, Rampaging Orcs — low risk, resource generation
- **Mid (countryside/core boundary):** Keeps, Monasteries, Mage Towers, Dungeons, Dens — combat and acquisition
- **Deep core:** Spawning Grounds, Tombs, Draconum, City — highest challenge, best rewards

**Recovery arc:** The game provides four tiers of comeback tools distributed across the map — healing effects from hand (immediate), Village/Monastery healing (short-term goal), Magical Glade wound discard (environmental lucky break), Rest (floor mechanic). A wrecked player is problem-solving, not dead.

---

### Replayability

Shuffled core tile order and enemy token draws ensure variance across runs. No meta-progression — every run resets fully. The local high score list makes strategic tradeoffs (rushing vs. thorough exploration) legible across runs.

---

### Tutorial Integration

Tutorial content is integrated inline, emulating the walkthrough document's structure — introduce rules at the moment they become relevant, never before. First turn teaches movement and card play. Combat rules introduced at first combat. Site rules surface when a site is first revealed. The +1 Fame per tile revealed rule naturally incentivizes exploration without a prompt.

Context-sensitive tooltips surface rule reminders at decision points. **Veterans can permanently disable tooltips after the first run** — persistent off-toggle, not just "ignore them."

The round tracker must convey urgency as the limit approaches — not just display a number. Visual/audio escalation cues as rounds run low are a UX obligation. *(Flag for UX LLD.)*

---

### Level Design Principles

1. **Depth = danger:** Content difficulty scales with distance from start, never artificially gated
2. **Rushing is self-limiting, not hard-gated:** Beelining for core tiles without deck development is punished by the game's own systems — weak Move, rampaging enemies, round pressure
3. **The unknown is the point:** City tile position within the core cluster is intentionally unpredictable. A lucky early city find is both a valid outcome and a lucky break. Don't surface the city's position.
4. **Mystery over noise:** The tile count UI ("X countryside and Y core tiles remaining") gives players actionable pacing information. Core tiles always cluster at the deep end — the city is a felt destination, not a random appearance.
5. **Every hex is a decision:** Move costs are real; path choice matters. Rampaging enemies may block the optimal path — going around costs Move, fighting through is always valid. The map never guarantees a free route.
6. **The comeback tools are in the map:** Recovery from setbacks is distributed across site types and hand effects — four tiers from immediate to environmental. The framework creates comeback moments; it doesn't script them.
7. **The score screen makes every tradeoff legible:** End-of-run summary (Fame, sites conquered, enemies defeated, distance to city if unfound) contextualises every strategic decision — rushing, burning monasteries, depth vs. speed.
8. **Teach at the moment of need:** Rules introduced inline as the player encounters each new situation. Veterans skip at their discretion.
9. **Round pressure is narrative, not mechanical:** The Dummy Player is a rival racing you, not a countdown clock.

---

## Art and Audio Direction

### Art Style

Vector/flat — clean geometric shapes, bold outlines, readable at small screen sizes. This is a practical-first decision: as a solo developer with a placeholder-first approach, vector/flat scales cleanly from programmer art to polished assets without changing the underlying style language. Fidelity can increase over time without requiring a visual rebrand.

#### Visual References

**Primary reference: Into the Breach (Subset Games).** Readable top-down tactical grid, strong silhouette readability per unit/enemy type, limited palette per faction, clear visual hierarchy between game-state layers. This is the one reference that captures the correct visual language for this game type, platform, and development approach.

Secondary references (Slay the Spire, Darkest Dungeon) were considered and dropped — they represent different visual languages and would create incoherence. Into the Breach is the anchor.

#### Color Palette

Dark fantasy — deep blues, stone greys, earth tones, punctuated by saturated magical accent colors. Mana colors map directly to their game function and must read at a glance: Red (fire), Blue (ice), Green (earth), White (spirit), Gold (Day wildcard), Black (Night amplifier).

**Explicit avoidance:**
- Soft pastels or bright primaries — wrong tone for the setting
- High-detail photorealistic textures — wrong style, wrong dev scope
- Overly desaturated grim-dark — mana colors must remain legible; pure grey-scale fails the HUD

**Day/Night visual shift:** Implemented via a single Godot `WorldEnvironment` tween — not an asset swap. One environmental parameter change recolors the whole scene. Day is warm and open; Night is cool and tense.

#### Camera and Perspective

Fixed-angle top-down overhead. Players navigate via pinch-to-zoom and pan (already spec'd in controls). No camera rotation. The hex grid reads cleanly from overhead and matches the physical game's tabletop perspective.

**Map tile silhouette legibility is a hard usability constraint:** every site type must be recognizable at 48px thumbnail size. This is the acceptance test for all placeholder art — fail silhouette readability, fail the build.

**Highest-stakes visual decisions (do these well even in placeholder form):**
1. Card frame and card iconography — always on screen, always interactive
2. Mana color icons (6 colors) — always in the HUD, always being compared
3. Site type silhouettes (14 types) — player must distinguish them at hex scale

---

### Audio and Music

Ambient with game-state shifts. Music responds to game state — exploration, combat, and deep-core territory each have a distinct feel — but transitions are gradual, not jarring cue swaps. Atmospheric, not cinematic-action. The music marks where you are, not what's happening to you.

#### Music Style

| State | Feel | Notes |
|---|---|---|
| Exploration (countryside) | Open, measured, curious | Breathing room; player thinking |
| Combat | Tension builds, rhythmic pulse | Urgency without bombast |
| Deep core / late-round pressure | Escalating dread | Round pressure is narrative urgency |
| City reveal | Full fanfare — biggest moment in game | See below |

Dark fantasy atmospheric palette. Reference feel: the ambient tension of Darkest Dungeon without its constant oppressiveness; the spatial openness of Journey without its gentleness.

#### Sound Design

**Tactile sound design is the primary feedback layer — implement early, not last.** Before polished art exists, crisp responsive audio makes placeholder visuals feel professional. Sound tells the player their action registered before the animation completes.

Every input that changes game state needs audio confirmation:

| Event | Sound Note |
|---|---|
| Card played / staged | Crisp, satisfying — the most-heard sound in the game |
| Card sideways | Distinct from normal play |
| Wound card greyed out | Subtle "locked" cue — no play allowed |
| Movement confirmed across hex | Footstep or movement thud |
| Tile revealed | Discovery stinger — sense of the unknown opening |
| Combat hit / block / damage taken | Impact, block, and wound sounds distinct |
| Level-up | Weight and ceremony — power escalation pillar |
| Site conquered | Completion cue |
| Round end / Dummy Player advances | Urgency marker |
| **City reveal → "Objective Achieved!" screen** | **Full fanfare — biggest audio moment in the game, bigger than the end-score reveal** |

The city reveal is the emotional peak. It should feel earned, celebratory, and unmistakable. Every other sound in the game should feel smaller by comparison.

**Sound design deliverables (v1):**
- Core UI feedback set (card play, movement, phase change, turn end)
- Combat hit/block/damage set
- Discovery/exploration set (tile reveal, site first-encounter)
- Milestone set (level-up, site conquered)
- "Objective Achieved!" fanfare — highest-priority audio asset

#### Voice/Dialogue

None for v1. Grunts and hero reactions cut entirely — scope reduction, cleaner implementation, and easier to add post-v1 than to remove. No VO budget required for the v1 build.

---

### Aesthetic Goals

| Pillar | How Art & Audio Serves It |
|---|---|
| **Faithful Feel** | Visual language of hex grid, card hand, mana colors, and site tokens must feel like a recognizable translation of the physical game. An MK veteran should feel oriented within 30 seconds. |
| **Meaningful Decisions** | Clear visual hierarchy at all scales — card legibility, mana color distinctness, site silhouette readability — ensures no decision is obscured by visual noise. |
| **Power Escalation** | Level-up and city reveal are the two biggest audio/visual moments. The game must feel different — not just statistically changed — as the hero grows. |
| **Phone-Native** | Vector/flat at 48px readability, touch-sized tap targets, Day/Night via tween (not heavy asset swap), ambient audio that doesn't demand attention. Nothing that fights the phone form factor. |

**Implementation priority order for art and audio:**
1. Core sound effects set — implement in first sprint alongside mechanics
2. Site silhouettes and card frames — placeholder quality, 48px readable
3. Ambient music tracks (exploration + combat)
4. HUD layout and mana color iconography
5. "Objective Achieved!" fanfare
6. Full asset polish pass

---

## Technical Specifications

### Performance Requirements

Magus Warrior targets conservative performance appropriate for a card-driven board game adaptation: minimal animation, static hex map, no physics simulation. Battery life and thermal management on a mid-range Android phone take priority over visual headroom.

#### Frame Rate Target

**30fps** — sufficient for a turn-based game with minimal animation. The frame budget is generous at 30fps; this headroom is reserved for battery life and thermal headroom on extended sessions, not visual effects.

*Max frame time and specific GPU budgets deferred to the architecture doc.*

#### Resolution Support

**Landscape only** — phone form factors. Portrait mode is not supported in v1. No tablet optimization required for v1; the game targets phone screen sizes.

No specific resolution targets beyond what Godot 4's viewport scaling handles automatically. The minimum supported display is the Galaxy S21 screen; larger phones are handled by scaling.

#### Load Times

No hard load time target. Assets are simple (vector/flat, minimal audio, no 3D) — cold launch and scene transitions should be fast by design. If a load time becomes noticeable during development, it is treated as a performance bug, not an accepted tradeoff.

*Cold launch target and any splash screen timing deferred to architecture.*

### Platform-Specific Details

#### Android Requirements

| Requirement | Value | Notes |
|---|---|---|
| Min Android version | Android 12 (API 31) | Locked |
| Orientation | Landscape only | Portrait not supported in v1 |
| Offline play | **Required** | Full game must be playable with no connection |
| Cloud saves | Google Play (rate-limited) | Syncs when connection available; never blocks gameplay |
| Achievements | 5–8 simple milestones | Implemented last in v1 cycle |
| In-app purchases | Out of scope (v1) | — |
| Background interruption | NOTIFICATION_APPLICATION_PAUSED hook | Full state serialized to disk on this callback |

**Offline-first design principle:** Cloud save syncs opportunistically when a connection is available. The game never requires a connection to play, save, or load. Local save is always the authoritative state on-device.

### Asset Requirements

#### Art Assets

| Category | Count Estimate | Notes |
|---|---|---|
| Hex terrain tiles (Day + Night) | ~10 tile types | Day/Night via WorldEnvironment tween, not separate assets |
| Site type icons (14 types) | 14 | Must pass 48px silhouette readability test |
| Enemy token silhouettes (6 colors) | 6 | Per-color identity; internal variety via token backs |
| Hero token (Thomas) | 1 | Top-down silhouette |
| Card frames + iconography | ~5–10 frame variants | Highest-stakes visual assets; polish early |
| Mana color icons (6 + gold + black) | 8 | Always visible in HUD |
| HUD elements | TBD | Phase indicator, running totals, hand display |
| "Objective Achieved!" screen | 1 | Dedicated celebratory asset; highest visual priority milestone |

*Exact counts finalized during architecture/sprint planning.*

#### Audio Assets

| Category | Count Estimate | Notes |
|---|---|---|
| Ambient exploration track | 1 | Looping |
| Combat tension track | 1 | Looping; transitions from exploration |
| Deep core / round pressure variant | 1 | Escalating urgency |
| Core SFX set | ~15–25 | Card play, movement, combat, level-up, tile reveal, wound, phase change |
| "Objective Achieved!" fanfare | 1 | Highest-priority audio asset |

*SFX implemented in first development sprint alongside core mechanics — not deferred to polish pass.*

#### External Assets

External assets (Godot Asset Library, Kenney, CC-licensed packs) permitted for v1 where legally clear and stylistically compatible with vector/flat direction.

**Asset pipeline process:**

| Step | Requirement |
|---|---|
| Folder structure | `/assets/external/` with subdirectories by source (e.g. `/kenney/`, `/godot-library/`). Original/internal assets in `/assets/original/`. No mixing. |
| Legal validation | Before integrating any external asset: (1) confirm license type (CC0, CC-BY, MIT, or equivalent); (2) confirm commercial use permitted; (3) log source URL and license in `/assets/external/ATTRIBUTION.md`. No asset ships without an entry. |
| Style check | Must pass vector/flat compatibility and 48px silhouette readability before integration. |
| Replaceability | All external assets tagged `[PLACEHOLDER]` in ATTRIBUTION.md. A later polish pass replaces them with original assets. |

#### Localization

**i18n infrastructure in place for v1; English-only content for v1.** All player-facing strings externalized from first build. Adding additional languages post-v1 requires only new string data, no code changes.

### Technical Constraints

| Constraint | Decision |
|---|---|
| Engine | Godot 4 + C# |
| Target device | Samsung Galaxy S21 (Snapdragon 888, 6GB RAM) |
| Min OS | Android 12 (API 31) |
| Orientation | Landscape only |
| Offline play | Fully required — cloud save is opportunistic, never blocking |
| Architecture gate | Four LLDs required before architecture begins: Effect System LLD, Enemy Effect LLD, Site Interaction LLD, UX LLD |

---

## Development Epics

### Epic Overview

| # | Epic | Dependencies | Est. Stories |
|---|---|---|---|
| 0 | Foundation | None | 3 |
| 1a | Effect System Architecture | 0 | 3 |
| 1b | Hand Mechanics + Wound Treatment | 1a | 5 |
| 2 | Hex Map + Movement | 1a | 5 |
| 3 | Combat System *(+ mid-combat save verification)* | 1b, 2 | 5 |
| 4 | Site Interactions *(pattern-first: 3–4 sites prove pattern before scaling to 14)* | 1b, 3 | 5 |
| 5 | Deck Building + Progression | 1b, 4 | 5 |
| 6 | Resource Systems | 1b, 2 | 5 |
| 7 | Full Scenario Loop *(+ dummy AI legality + save verification)* | 3, 4, 5, 6 | 5 |
| 8 | UI/UX | 1a–7 | 4 |
| 9 | Art + Audio | 8 | — |
| 10 | Google Play + Release | 7 | 4 |

### Recommended Sequence

**Phase 1 — Foundation (Epics 0, 1a, 1b, 2):** Build pipeline, then Effect System architecture, then hand UI, then hex map. Epic 1a is the load-bearing wall everything else inherits from. Epic 2 starts after 1a (not parallel — they share input handling and undo state).

**Phase 2 — Core Systems (Epics 3, 4, 5, 6):** Combat, Site Interactions, Deck Building, and Resource Systems. Epics 3 and 6 can run in parallel once 1b and 2 are solid. Epic 4 follows Epic 3; Epic 5 follows Epic 4. Mid-combat save serialization verified before Epic 3 is considered complete.

**Phase 3 — Integration (Epic 7):** First complete scenario run. All Phase 2 systems must be working. Dummy player AI legality is an explicit acceptance criterion.

**Phase 4 — Polish and Ship (Epics 8–10):** UI pass, art/audio, release pipeline. Basic HUD iterable from Epic 1b onward even before Epic 8.

### Vertical Slice

**First playable milestone (end of Epic 1b):** One card (Rage) plays end-to-end through the Effect System — staging, running totals, commit, undo. Effect inspector shows every event. This proves Godot 4 + C# + the Effect System before everything else is built on top of it.

### Cross-Epic Architectural Notes

- **Undo as event log:** Designed in Epic 1a, extended (not rebuilt) by Epics 2, 3, and 7. Every state mutation appends to the log; undo pops it. Architecture must be decided once.
- **State consistency thread:** Hand commit (Epic 1b) → map update (Epic 2) → combat resolution (Epic 3) must speak the same language. Verify at each epic boundary, not after.
- **Site interaction pattern:** First 3–4 sites in Epic 4 establish a Template pattern (or equivalent). Remaining 10 sites implement against that pattern. "Pattern locked" is an internal Epic 4 checkpoint.
- **Dummy player AI:** Explicit acceptance criterion in Epic 7 — dummy makes legal moves. Verify via dummy-only run log.
- **Mid-combat save:** Serialize/deserialize mid-combat state verified as part of Epic 3 completion, not deferred to Epic 10.

---

## Success Metrics

### Technical Metrics

| Metric | Target | Measurement Method |
|---|---|---|
| Frame rate | Sustained 30fps minimum; average above 30fps; no spikes during card play, tile reveal, or combat | Godot profiler on Galaxy S21 |
| Input-to-feedback latency | <100ms from tap to visible effect | Godot profiler; feel test during playtesting |
| Crash rate | <1 crash per 100 sessions | Manual session log during playtesting |
| Save reliability | 100% accurate restore across all tested states (mid-turn, mid-combat, mid-round, round boundary) | Forced save/restore tests at each state type |
| Session interruption resilience | 10 interrupted runs (quit mid-turn, mid-action, mid-animation) all restore cleanly | Stress test before Epic 7 ships |
| Build health | APK deploys to Galaxy S21 without errors | Device deploy at each named epic milestone (Epics 0, 1b, 3, 5, 7) |

### Gameplay Metrics

#### Run Completion

≥50% of started runs reach a win or loss screen (not abandoned mid-run). Measured during internal playtesting.

#### Rules Accuracy

| Part | Target | Method |
|---|---|---|
| Implementation accuracy | Veteran reviews the rulebook-to-code mapping document and confirms zero misreadings | Document review before veteran playtest |
| Design intentionality | Any deviation from boardgame rules is either in the documented solo ruleset or caught in design review — no surprise deviations during playtesting | Design review log |

#### Phone Session Length

A 10–20 minute session ends with a clean "Save & Exit" restore. At turn end, the game offers "Save & Exit." Tapping it produces a file that reloads to exact game state. 10–20 min ≈ 3–6 turns on average. Verified during internal playtesting.

#### Power Escalation — Capability Dimensions

Measured by comparing Thomas's capability ceiling at round 1 (starting deck only) vs. round 4 (fully developed). Six dimensions:

| Dimension | What to measure |
|---|---|
| Attack | Max achievable attack value per turn; number of distinct attack types available (physical, ranged, siege, elemental) |
| Block | Max achievable block value per turn; number of distinct block types available |
| Movement | Max achievable Move per turn; ability to cross previously impassable terrain (skill/unit unlocked) |
| Influence | Max achievable Influence per turn |
| Mana | Max mana tokens generatable per turn; number of distinct colors accessible |

**Internal test during Epic 5:** one run tracking these dimensions with a spreadsheet at round 1 and round 4. All dimensions measurably higher and more diverse at round 4 = escalation working. Qualitative check: "Do you feel like you're getting *better* at using your deck, or just wealthier?"

**Full validation** (dramatic city-conquest mana combo moment) deferred to post-v1 Solo Conquest scenario.

### Qualitative Success Criteria

| Signal | Type |
|---|---|
| Veteran says: *"This is just like the boardgame, only without the setup time!"* | Primary success signal |
| Veteran can name a specific moment in the run that felt like their best MK memory | Feel validation — if they can't name one, translation didn't work |
| Veteran says: *"This doesn't feel like Mage Knight"* | Failure — investigate immediately |
| Any unintentional rule violation survives veteran playtest | Failure — rules accuracy not met |
| Player felt resource-starved at least once during a run | Constraint validation — if never, economy may be too generous |

### Metric Review Cadence

| When | What |
|---|---|
| Epics 0, 1b, 3, 5, 7 milestones | Technical metrics (frame rate, crash, save, latency) |
| During Epic 5 | Power escalation internal test (capability dimensions spreadsheet) |
| Before Epic 7 ships | First internal full run review; implementation accuracy doc review |
| Before v1 release | Veteran playtest — full run, rules accuracy, qualitative signal |
| Post-v1 | Power Escalation full validation (Solo Conquest scenario) |

---

## Out of Scope

The following are explicitly out of scope for Magus Warrior v1.0 and will not be implemented unless scope is deliberately expanded.

### Features

| Item | Notes |
|---|---|
| Additional heroes | Thomas (Tovak) only in v1; additional heroes deferred |
| Additional scenarios | First Reconnaissance solo only in v1 |
| Multiplayer | Designed for single-player from the start; multiplayer refactor accepted as post-v1 cost |
| City combat (all 12 tiers) | City tile is the win condition — entering it ends the run; full assault system is post-v1 |
| Non-combat city interaction | Shops, healing, unit recruitment within city — post-v1 |
| Expansion content | No Shades of Tezla, Lost Legion, or other expansion cards, enemies, or tiles in v1 |
| Global leaderboards | Post-v1; v1 includes a local high score list only |
| Accessibility controls | Beyond the baseline gesture set; post-v1 |
| Full Power Escalation validation | V1 measures 5 capability dimensions as a proxy; true multi-scenario arc validation deferred to post-v1 Solo Conquest scenario |

### Platforms

| Platform | Status |
|---|---|
| iOS | Post-v1; Godot export exists but not in v1 scope |
| Console / PC | Not planned |
| Tablet-optimized layout | Not optimized; playable but phone-first only |

### Polish / Content

| Item | Notes |
|---|---|
| Full voice acting | V1 has grunts and reactions only; no narration or dialogue VO |
| Orchestral / fully licensed score | V1 uses layered ambient audio; full score is post-v1 |
| Additional languages | i18n infrastructure in place; English content only in v1 |

### Deferred to Post-Launch

- Additional heroes and their starting decks
- Additional solo scenarios (Conquest, etc.)
- Multiplayer (pass-and-play or online)
- City assault combat system (all 12 tiers)
- iOS release
- Global leaderboards
- Expansion card and enemy sets

---

## Assumptions and Dependencies

### Key Assumptions

| Category | Assumption |
|---|---|
| **Technical** | Godot 4 (LTS) + C# remains stable and receives security patches for the project duration |
| **Technical** | Samsung Galaxy S21 (Snapdragon 888, 6GB RAM, Android 12) is representative of mid-tier Android hardware; performance targets set against it generalize to comparable devices |
| **Technical** | Google Play services (cloud saves, minimal achievements) are available and pass store review |
| **Technical** | Android API 31 remains an appropriate minimum target for the project duration |
| **Team** | Solo developer (John Riston) — no contractors or additional developers in v1; all velocity estimates assume single-person capacity |
| **Process** | All four LLDs (Effect System LLD, Enemy Effect LLD, Site Interaction LLD, UX LLD) are completable before architecture begins — this is a hard gate |
| **Legal** | External art and audio assets used in v1 are under compatible licenses (Creative Commons, royalty-free, or licensed commercial); ATTRIBUTION.md maintained for all attributable assets |
| **Market** | The game is a personal/hobbyist project for Mage Knight fans — not targeting mass market or commercial launch |

### External Dependencies

| Dependency | Purpose | Risk |
|---|---|---|
| Godot 4 (LTS) | Game engine | Low — LTS channel is stable; breaking changes unlikely mid-project |
| C# / .NET | Scripting runtime | Low — tied to Godot version |
| Google Play SDK | Cloud saves, achievements | Medium — API changes or policy updates could require rework |
| External art assets | Visual style, site icons, card art | Medium — license compatibility must be verified per asset |
| External audio assets | Ambient music, SFX | Medium — same license risk as art |
| BMAD / GDS framework | Agent-assisted development workflow | Low — local install; not a runtime dependency |
| Sally (BMM UX agent) | UX LLD authorship | Low — available within BMAD toolchain |

### Risk Factors

| Risk | Likelihood | Mitigation |
|---|---|---|
| Effect System LLD complexity blocks architecture longer than expected | Medium | Pattern-first approach (3–4 sites, vertical slice card) keeps implementation moving while LLD is drafted |
| Solo developer capacity makes schedule unpredictable | High | No fixed release date; scope is fixed, timeline is flexible |
| Google Play store policy changes affect cloud save or achievement features | Low | Features are additive — game is fully playable offline without them |
| External asset licenses incompatible on discovery | Medium | ATTRIBUTION.md review at asset acquisition time; placeholder-first approach limits exposure |

---

## Document Information

**Document:** Magus Warrior — Game Design Document
**Version:** 1.0
**Created:** 2026-05-08
**Author:** John Riston
**Status:** Complete

### Change Log

| Version | Date | Changes |
|---|---|---|
| 1.0 | 2026-05-08 | Initial GDD complete — all 14 steps |
