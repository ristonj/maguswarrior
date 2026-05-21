# Architecture Input Brief — Magus Warrior

Input brief for the `gds-game-architecture` workflow. Read this before producing any
architectural decisions. It captures hard constraints, prior failure modes, and specific
systems the architecture must address.

---

## Tech Stack

- Engine: Godot 4, C#
- Testing: GUT (Godot Unit Test)
- Single-player only for initial scope (no PvP, no multiplayer)

---

## Prior Failure Modes (Previous Project)

The previous implementation attempt (`/home/jriston/git/mageknight`) failed in the
following ways. The architecture must explicitly address each.

1. **UI was never wired to game logic.** A `PendingChoices` system existed in `TurnState`
   but the UI layer never polled or responded to it. Cards that required player choices
   (select target, choose mana color, pick option) threw `Choice required: <type>` errors
   at runtime. Features were impossible to test.

2. **No UI State Contract was written.** Each screen's expected state, signals, and user
   actions were left for implementing agents to infer. They got it wrong consistently.

3. **Card effect LLD was never written.** Each card's interaction model (choices required,
   UI prompts triggered, phase legality, precondition failure) was left undocumented. The
   implementing agent had to guess.

4. **Phase validation was too permissive.** Cards that couldn't generate ranged/siege
   attack were still playable in those phases. No table of effect type vs. legal phase
   existed.

5. **Advanced action and spell offers were never wired to the UI.** The offer population
   system existed in logic but never surfaced to a screen.

---

## Architecture Must Include

### UI Architecture (non-negotiable)

The architecture must produce a **UI State Contract**: for each game screen, document:
- What game state it reads
- What signals/events trigger UI updates
- What user actions it handles and what game events they emit

Screens that must be designed (minimum):
- Main game board / map view
- Hand display and card play flow
- Combat resolution screen
- City / interaction offer screen
- End-of-turn summary / cleanup screen

### Player Choice / Pending Interaction System

Define how game logic surfaces pending player decisions to the UI and how the UI signals
back. This is the system that failed last time. It must handle:

- `choose_one`: present N options, await selection
- Target selection: highlight valid targets (enemies, units, terrain), await tap/click
- Source die color picker: present color options, await selection
- Unit picker with level filter: show wounded units filtered by `max_level`, await selection
- End-of-turn prompts: show optional discard-to-retrieve decisions after turn resolves

The architecture must specify the interface contract between game logic and UI for each of
these choice types — not leave it to the implementing story.

### Card Resolution Pipeline

Define the card play lifecycle: play → validate phase legality → resolve effects →
surface any pending choices → apply outcomes → update game state. The pipeline must
support multi-step effects (a card that makes a choice, then applies an effect based on
that choice) without the UI losing track of where it is.

### Phase Gate System

Produce a table of effect types vs. legal phases. Cards must be validated against this
table at play time, not left to per-card logic. Effect types to cover at minimum:
move, attack (melee/ranged/siege), block, influence, heal, mana, crystal, unit actions.

---

## Novel UX Interactions to Design For

The following card effects introduce interaction patterns not present in standard card
play. The architecture must design the general system that handles each — not hardcode
the specific card.

Full details in `docs/ux-interactions-hero-starting-cards.md`. Summary:

| Pattern | Card | What the system must do |
|---|---|---|
| `ice_block_scaling` | Cold Toughness (powered) | Query enemy token abilities at block resolution; compute and display dynamic block value |
| `black_source_die_as_any_color` | Mana Pull (unpowered) | Inject a color picker step when source die shows black before applying it |
| `end_of_turn_return` | Crystal Joy | Register an end-of-turn hook; prompt optional discard with hand filtered by wound status |
| `ready_unit (max_level)` | Rejuvenate | Show unit picker filtered by level; confirm removes wound from selected unit |

---

## Story Acceptance Criteria Template

Every logic story must include this AC before it can be closed:

> UI is connected and displays correct game state. All player choices required by this
> story's card effects are surfaced as prompts and resolved correctly before the effect
> is applied. No `Choice required` or unhandled state errors occur during normal play.

Bake this into the story template — do not rely on individual story authors to add it.

---

## Reference Documents

- UX Design Specification: `_bmad-output/planning-artifacts/ux-design-specification.md`
- Hero-specific UX interactions: `docs/ux-interactions-hero-starting-cards.md`
- Card definitions: `data/cards.yaml`
- Hero definitions: `data/heroes.yaml`
