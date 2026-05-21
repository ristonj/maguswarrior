# Effect LLD — Magus Warrior

Interaction model for every card: choices required, UI prompts, phase legality,
precondition failures, and sequencing. Read alongside `data/cards.yaml` and
`_bmad-output/game-architecture.md`.

---

## Universal Rules

### Sideways Play

Every card may be played sideways for a reduced effect determined by the current phase.
This is a system rule — not stored per card. Phase-to-sideways mapping:

| Phase | Sideways Effect |
| --- | --- |
| Movement | Move 1 |
| Interaction | Influence 1 |
| Combat — Block | Block 1 |
| Combat — Melee | Attack 1 |

Sideways cannot provide Ranged Attack, Siege Attack, or elemental Attack/Block. During
the Ranged phase there is no useful sideways option — cards are typically held for later
phases or played for their normal effect.

UI: when a player taps a card, always show the normal effect AND the sideways option
side-by-side. The sideways option is always available in any phase.

### Source Dice — Immediate Use

Mana dice taken from the Source must be used immediately to power a card, unit, or
skill. They do not generate tokens and cannot be saved across actions.

**Exceptions** (cards that explicitly convert a source die to a token):

- `mana_pull` powered — generates a mana token on the spot; the token (not the die) persists
- `mana_draw` powered — generates 2 mana tokens; same rule

### Mana Color Taxonomy

| Color | Category | Notes |
| --- | --- | --- |
| Red, Blue, White, Green | Basic | Always available; can produce crystals |
| Black | Non-basic | Available at Night (or via specific card effects); no black crystal |
| Gold | Non-basic wild | Available during Day; substitutes for any color; no gold crystal |

When an effect says **"any basic color"** it means red, blue, white, or green only.
When an effect says **"any color except gold"** (or **"any color"**) it includes black.

Black mana availability is determined by the mana availability system at runtime — Night
rules, sites operating under Night rules, and cards such as Amulet of Darkness can all
make black mana accessible. Color pickers for "any color except gold" effects show black
whenever the player has black mana available; they do not hard-code a Day/Night check.

**Gold as universal substitute:** Gold mana tokens and gold source dice may substitute
for any basic color payment anywhere in the game — card costs, spell costs, unit ability
costs, interaction costs, etc. No per-card note is needed. The color being substituted is
determined by context: if a color picker is shown, the player's selection implicitly
declares what color the gold is acting as. If no picker is shown (e.g. a fixed-color
cost), gold pays for that color directly. Gold cannot substitute for black mana costs.

### Mana Color and Resistance Interaction

Non-attack, non-block effects that cost red mana cannot affect enemies with fire
resistance. Non-attack, non-block effects that cost blue mana cannot affect enemies with
ice resistance. This applies to card effects, unit abilities, and skill effects alike.

Attacks and blocks are exempt — their interaction with resistance is governed by Block
Efficiency and attack type rules instead.

---

### Block Efficiency

All block can be applied against any attack type, but efficiency determines how many block
points cancel 1 damage:

- **Efficient (1:1):** 1 block point cancels 1 damage
- **Inefficient (2:1):** 2 block points cancel 1 damage

| Block Type | Efficient vs | Inefficient vs |
| --- | --- | --- |
| Physical (Block X) | Physical | Fire, Ice, Cold Fire |
| Ice Block | Fire, Physical | Ice, Cold Fire |
| Fire Block | Ice, Physical | Fire, Cold Fire |
| Cold Fire Block | All | — |

Mixed spending is allowed. Example: 1 Ice Block + 2 Physical Block cancels 2 Fire damage
(1 efficient + 2 inefficient = 1 + 1 = 2).

### Heal Points

Heal points accumulate across multiple sources within the same action — cards, unit
abilities, skill effects, and village/monastery interactions can all contribute to the
same pool. Playing two cards that each generate Heal 1 gives Heal 2 to spend.

Heal points can be spent on:

- **Wound cards in hand** — 1 heal point per wound card removed
- **Wound tokens on controlled units** — heal points equal to the unit's level to remove
  one wound token (Level I = 1, Level II = 2, Level III = 3, Level IV = 4)

The wound selection UI presents both as a single combined picker. Each entry shows its
cost. Units whose level exceeds remaining heal points are shown disabled. A card's heal
option is available (not greyed out) as long as at least one valid target exists — a
wound in hand OR a wounded controlled unit.

---

## Base Deck Cards (all heroes)

### Simple Cards

These cards have no player choices beyond sideways play. One tap to play; effect
applies immediately. Precondition: none.

| Card | Unpowered | Phase | Powered | Phase |
| --- | --- | --- | --- | --- |
| march | Move 2 | Movement | Move 4 | Movement |
| stamina | Move 2 | Movement | Move 4 | Movement |
| threaten | Influence 2 | Interaction | Influence 5, Rep −1 | Interaction |
| promise | Influence 2 | Interaction | Influence 4 | Interaction |

**threaten powered edge case:** Reputation −1 applies at end of turn, not immediately
on play. Show a warning badge on the powered option in the `choose_one` panel (e.g.
"Rep −1 at end of turn") so the player sees the cost before confirming. Apply the
penalty during end-of-turn cleanup.

---

### rage

**Unpowered** — Phase: Combat (Melee) or Block

Choices: `choose_one` — Attack 2 or Block 2.

UI sequence:

1. Player taps Rage
2. Show 2-option panel: **Attack 2** | **Block 2**
3. Phase filter: in Block phase, only Block 2 is selectable; in Melee phase, only
   Attack 2 is selectable. The other option is shown but disabled.
4. Player selects → effect applies

Preconditions: none.

**Powered** — Phase: Combat (Melee)

No choice. Attack 4 applies directly. One-tap play.

---

### determination

**Unpowered** — Phase: Combat (Melee) or Block

Identical to Rage unpowered: choose Attack 2 or Block 2. Same phase filter applies.

**Powered** — Phase: Combat (Block)

No choice. Block 5 applies directly.

---

### swiftness

**Unpowered** — Phase: Movement

No choice. Move 2. One-tap play.

**Powered** — Phase: Combat (Ranged or Melee)

No choice. Ranged Attack 3. One-tap play.

Note: Swiftness unpowered is a movement card; powered is a combat card. These are not
interchangeable. The player chooses which to use by deciding whether to pay the mana
cost. The phase context makes it clear which mode is available.

---

### improvisation

**Unpowered** — Phase: any phase where at least one of [Move, Influence, Attack, Block] is legal

Effect: discard another card from hand, then choose one of Move 3 / Influence 3 / Attack
3 / Block 3.

Precondition: player must have at least 1 other card in hand to discard. If hand contains
only Improvisation, it can only be played sideways.

UI sequence:

1. Player taps Improvisation
2. Show hand minus Improvisation — player selects one card to discard
3. Selected card is discarded
4. Show 4-option panel: **Move 3** | **Influence 3** | **Attack 3** | **Block 3**
5. Phase filter: only options legal in the current phase are selectable. Others are shown
   but disabled (grayed-out in panel — not fully unplayable; e.g. in Movement phase:
   only Move 3 is active, other options are dimmed).
6. Player selects → effect applies

**Powered** — Phase: same as unpowered (any phase with at least one legal option)

Identical sequence. Values: Move 5 / Influence 5 / Attack 5 / Block 5.

---

### crystallize

**Unpowered** — Phase: Any

Effect: pay one mana of any basic color → gain a crystal of that color.

Precondition: player must have at least 1 basic mana token (or accessible source die)
to pay. If no mana is available, the card cannot be played for its normal effect.

UI sequence:

1. Player taps Crystallize (no blue mana being paid — this is unpowered)
2. Show mana color picker: basic colors only (red, blue, white, green)
3. Player selects color → pays 1 mana of that color → gains 1 crystal of that color

Note: the mana payment here is ADDITIONAL to any card play cost — it is the effect's
required input, not an optional spend. The player must pay to gain the crystal.

**Powered** — Phase: Any

Effect: gain a crystal of any color. No additional mana payment required (the blue mana
paid to power the card is the only cost).

UI sequence:

1. Player pays blue mana to play Crystallize powered
2. Show crystal color picker: all basic colors (red, blue, white, green)
3. Player selects → crystal added to inventory

---

### concentration

**Unpowered** — Phase: Any

Effect: gain one mana token of any basic color (blue, white, or red).

UI sequence:

1. Player taps Concentration unpowered
2. Show 3-color picker: **Blue** | **White** | **Red** (green is NOT an option)
3. Player selects → gains 1 mana token of that color

**Powered** — Phase: Any

Effect: play another Action card alongside this one; that card's powered (stronger)
effect resolves for free, plus +2 to any resulting Move/Influence/Attack/Block value.

UI sequence:

1. Player pays green mana to play Concentration powered
2. Hand display updates — player must select a second Action card from hand
3. The selected card's powered effect resolves (no mana payment required for second card)
4. If the second card's powered version has a `choose_one`, the player sees that exact
   choice panel as if playing the card directly in powered mode (same options, same phase filter)
5. +2 bonus is applied to the numeric value of any Move, Influence, Attack, or Block result
6. Both cards go to discard

Edge cases:

- Precondition: player must have at least 1 other Action card in hand
- If the second card's powered effect does not produce Move/Influence/Attack/Block (e.g.
  it gains a crystal), the +2 bonus does not apply — the effect resolves without bonus
- Wound cards are not Action cards and cannot be selected as the second card

---

### mana_draw

**Unpowered** — Phase: Any

Effect: player may use 1 additional source die this turn.

UI behavior: Playing Mana Draw increases the "extra source dice available this turn"
counter by 1. The extra die is consumed when the player later selects a source die to
power a card or unit. No immediate UI prompt — effect persists for the duration of the turn.

Precondition: none (the extra die allowance is granted even if Source has no matching
dice; it simply cannot be used if no dice are available).

**Powered** — Phase: Any

Effect: take one die from the Source, set it to any color except gold, gain 2 mana
tokens of that color. Die is not rerolled when returned.

UI sequence:

1. Player pays white mana to play Mana Draw powered
2. Source die display highlights — player selects one die from the Source
3. Color picker: any color except gold (red, blue, white, green, black)
4. Player selects color → die is set to that color → player gains 2 mana tokens of that color
5. Die is marked "no-reroll" for when it returns to Source at round end

Edge case: Source die general rule does NOT apply here — this card explicitly generates
tokens (2 tokens), so the tokens persist for later use this turn.

---

### tranquility

**Unpowered** — Phase: Movement, Interaction, During Rest, End of Turn (NOT combat)

Choices: `choose_one` — Heal 1 or Draw 1 card.

UI sequence:

1. Player taps Tranquility
2. Show 2-option panel: **Heal 1** | **Draw 1 card**
3. Heal 1: adds 1 point to the heal pool. Disabled only if no wounds in hand AND no
   wounded controlled units.
4. Draw 1: draw top card from deck into hand. If deck is empty, shuffle discard first.

**Powered** — Phase: Movement, Interaction, During Rest, End of Turn (NOT combat)

Choices: `choose_one` — Heal 2 or Draw 2 cards. Same sequence; Heal 2 adds 2 points to
the heal pool. Draw pulls 2 cards.

Edge case: Heal is NOT valid in any combat phase (Ranged, Block, Damage, Melee). The
Heal option is fully unavailable during combat — the card itself cannot be played in
combat phases for its normal effect (sideways only).

---

## Hero-Unique Starting Cards

### instinct (Thomas / Torak)

**Unpowered** — Phase: any (Move, Influence, Attack, or Block depending on choice)

Choices: `choose_one` — Move 2 | Influence 2 | Attack 2 | Block 2.

UI: 4-option panel. Phase filter applies — only options valid in current phase are
selectable. Others are shown disabled.

| Option | Legal Phases |
| --- | --- |
| Move 2 | Movement |
| Influence 2 | Interaction |
| Attack 2 | Combat Melee |
| Block 2 | Combat Block |

**Powered** — Phase: same (Move 4 | Influence 4 | Attack 4 | Block 4)

Same sequence, larger values.

---

### cold_toughness (Thomas / Torak)

**Unpowered** — Phase: Combat (Melee) or Block

Choices: `choose_one` — Attack 2 or Ice Block 3.

UI: 2-option panel. Phase filter:

- In Block phase: only Ice Block 3 is selectable
- In Melee phase: only Attack 2 is selectable

Ice Block 3: block value is 3, element is Ice. Efficient (1:1) against fire and physical
attacks; inefficient (2:1) against ice and cold fire attacks. See Universal Rules —
Block Efficiency.

**Powered** — Phase: Combat (Block only)

No choice. Ice Block 5 base value, with dynamic scaling from `ice_block_scaling` hook.

UI sequence:

1. Player pays blue mana to play Cold Toughness powered
2. Player taps an enemy to target for blocking
3. `IceBlockScalingHook` fires at `HookPoint.BeforeBlockTargeting`:

   - Queries the targeted enemy's token abilities
   - Adds bonus per ability (see table below)
   - Arcane Immunity: if enemy has arcane_immunity, scaling does NOT apply → block = 5
4. Displayed block value updates in real time as player hovers/taps enemies
5. Player confirms target → Ice Block resolves with final value

Scaling table (per ability on the enemy token; site fortification does NOT count):

| Ability | Bonus |
| --- | --- |
| physical | +0 |
| fire | +1 |
| ice | +1 |
| cold_fire | +2 |
| swift | +1 |
| brutal | +1 |
| poison | +1 |
| paralyze | +1 |
| fortified | +1 |
| physical_resistance | +1 |
| fire_resistance | +1 |
| ice_resistance | +1 |

Optional: show a breakdown tooltip so the player can see which abilities contributed.

Precondition (powered): must be in Block phase and targeting at least one enemy.

---

### battle_versatility (Aretha / Arythea)

**Unpowered** — Phase: Combat (Block or Ranged or Melee)

Choices: `choose_one` — Attack 2 | Block 2 | Ranged Attack 1.

UI: 3-option panel with phase filter:

| Option | Legal Phases |
| --- | --- |
| Attack 2 (melee) | Combat Melee |
| Block 2 | Combat Block |
| Ranged Attack 1 | Combat Ranged, Combat Melee |

**Powered** — Phase: Combat (Block or Ranged or Melee)

Choices: `choose_one` — 6 options:

| Option | Legal Phases |
| --- | --- |
| Attack 4 | Combat Melee |
| Block 4 | Combat Block |
| Fire Attack 3 | Combat Melee |
| Fire Block 3 | Combat Block |
| Ranged Attack 3 | Combat Ranged, Combat Melee |
| Siege Attack 2 | Combat Ranged, Combat Melee |

UI: 6-option panel. Use 2-column grid layout for 6 options. Phase filter hides/disables
options not valid in current phase. See `docs/ux-interactions-hero-starting-cards.md`
for layout notes.

---

### mana_pull (Aretha / Arythea)

**Unpowered** — Phase: Any

Effect: player may use 1 additional source die this turn. If that die shows black, player
may treat it as any basic color instead.

UI behavior:

1. Playing Mana Pull increases "extra source dice available this turn" by 1 (same as
   Mana Draw unpowered)
2. When the player later selects the extra source die: `BlackSourceDieHook` fires at
   `HookPoint.AfterSourceDieRoll`
3. If die shows black: color picker appears — player selects any basic color (red, blue,
   white, green). Die is treated as that color for powering.
4. If die shows any other color: normal source die flow proceeds.

Note: the extra die must be used immediately (standard source die rule). The black
override only applies to this specific extra die from Mana Pull, not to all source dice.

**Powered** — Phase: Any

Effect: take 2 dice from the Source, set each to any color (not gold), gain 1 mana
token per die. Dice are not rerolled when returned.

UI sequence:

1. Player pays white mana to play Mana Pull powered
2. Source die display highlights — player selects 2 dice from the Source (one at a time)
3. For each die: color picker (any color except gold — includes black if available)
4. Player selects color → gains 1 mana token of that color for each die
5. Both dice marked "no-reroll"

Exception to immediate-use rule: this powered effect explicitly generates tokens that
persist for later use in the turn.

---

### will_focus (George / Goldyx)

**Unpowered** — Phase: Any

Choices: `choose_one` — gain a mana token (blue, white, or red) OR add a green crystal
to inventory.

UI: 2-option panel:

- **Mana Token**: 3-color sub-picker (blue, white, red) appears on selection
- **Green Crystal**: no further input needed; crystal added to inventory immediately

**Powered** — Phase: Any

Effect: play another Action card alongside this one; that card's powered effect resolves
for free, plus +3 to any resulting Move/Influence/Attack/Block value.

Identical to Concentration powered, with +3 bonus instead of +2. Same UI sequence and
edge cases apply. See Concentration powered above.

---

### crystal_joy (George / Goldyx)

**Unpowered** — Phase: Any

Effect: pay 1 basic mana → gain crystal of that color. At end of turn: optional prompt
to discard a non-wound card → Crystal Joy returns to hand.

UI sequence (at play time):

1. Player taps Crystal Joy (no blue mana — unpowered)
2. Mana color picker: basic colors only (red, blue, white, green)
3. Player selects color → pays 1 mana → gains crystal of that color

UI sequence (at end of turn — `EndOfTurnReturnHook`):

1. Hook checks: was Crystal Joy played this turn in unpowered mode?
2. If yes: show optional prompt — "Discard a non-wound card to return Crystal Joy to hand?"
3. Hand display shows remaining cards; wound cards are greyed out (not selectable)
4. Player taps a non-wound card to discard → Crystal Joy returns to hand instead of discard
5. Player taps "Skip" → Crystal Joy goes to discard pile normally

Precondition (unpowered): player must have at least 1 basic mana to pay. If no mana
available, cannot be played for normal effect.

**Powered** — Phase: Any

Effect: gain a crystal of any color (free choice). At end of turn: optional prompt to
discard any card (including wound) → Crystal Joy returns to hand.

UI sequence (at play time):

1. Player pays blue mana to play Crystal Joy powered
2. Crystal color picker: any basic color (red, blue, white, green)
3. Player selects → crystal added to inventory

UI sequence (at end of turn — `EndOfTurnReturnHook`):

1. Hook checks: was Crystal Joy played this turn in powered mode?
2. If yes: show optional prompt — "Discard any card to return Crystal Joy to hand?"
3. Hand display shows all remaining cards; all are selectable (including wounds)
4. Player taps card to discard → Crystal Joy returns to hand
5. Player taps "Skip" → Crystal Joy goes to discard normally

---

### rejuvenate (Nick / Norowas)

**Unpowered** — Phase: Movement, Interaction, During Rest, End of Turn (NOT combat)

Choices: `choose_one` — 4 options:

- Heal 1
- Draw 1 card
- Gain a green mana token
- Ready a level I or II unit

UI: 4-option panel with eligibility filter:

| Option | Disabled when |
| --- | --- |
| Heal 1 | Player has no wound cards in hand AND no wounded units |
| Draw 1 card | Never disabled |
| Green mana token | Never disabled |
| Ready level I or II unit | Player has no spent units at level ≤ 2 |

UI for "Ready a unit":

1. Player selects this option
2. Unit picker panel shows all spent units; units above level 2 are shown but disabled
3. Player taps a level I or II spent unit → that unit is now ready
4. Player confirms → effect applies

**Powered** — Phase: Movement, Interaction, During Rest, End of Turn (NOT combat)

Choices: `choose_one` — 4 options:

- Heal 2
- Draw 2 cards
- Gain a green crystal
- Ready a level I, II, or III unit

UI: same panel structure. Eligibility filter:

| Option | Disabled when |
| --- | --- |
| Heal 2 | Player has no wounds in hand AND no wounded controlled units |
| Draw 2 cards | Never disabled |
| Green crystal | Never disabled |
| Ready level I–III unit | Player has no spent units at level ≤ 3 |

Selecting "Heal 1" or "Heal 2" adds that many points to the heal pool.

---

### noble_manners (Nick / Norowas)

**Unpowered** — Phase: Interaction

Effect: Influence 2. If played during an active interaction (city, village, monastery,
etc.): also grants Fame +1.

UI: One-tap play. No choices.

The +1 Fame badge on the card is:

- **Dimmed/inactive**: when no interaction is active (card played outside interaction context)
- **Active**: when played during an active interaction — fame applies automatically on play

Note: "during an interaction" means the interaction screen is open (the player is
actively in a city, village, etc. offer or resolution). Playing Noble Manners during
movement or combat does NOT trigger the fame bonus.

**Powered** — Phase: Interaction

Effect: Influence 4. If during an active interaction: Fame +1 AND Reputation +1.

Same badge behavior. Both bonuses apply automatically if condition is met.

Precondition: must be in interaction phase. Sideways play is available in any phase.

Reputation goes up at end of turn.

---

## Advanced Actions

### blood_rage

**Unpowered** — Phase: Combat (Melee)

Base effect: Attack 2. Optional: take a Wound to increase the total attack to 5.

UI sequence:

1. Player taps Blood Rage
2. Show prompt: "Attack 2 — take a Wound to increase to Attack 5?" with **Yes** / **No**
3. If Yes: player takes 1 Wound (Wound card added to hand); attack value becomes 5
4. If No: attack remains 2

Note: `attack: 5` in the YAML is the replacement total, not a bonus of 5. Final attack
is 5 (not 2+5).

**Powered** — Phase: Combat (Melee)

Base effect: Attack 4. Optional: take a Wound to increase total attack to 9.
Same prompt sequence.

---

### agility

**Unpowered** — Phase: Movement (and Combat — see below)

Primary effect: Move 2.

Special: during combat this turn, the player may spend remaining unspent move points as
Attack 1 each (melee).

UI behavior:

- Agility is played in the movement phase for Move 2 in the normal way
- During the combat attack phase, a "Convert Move" UI element becomes available
- The player can see their remaining unspent move points and tap to spend them as Attack 1
- Move points can continue to accumulate during combat (from other cards played in
  combat) and can also be converted

**Powered** — Phase: Movement (and Combat — see below)

Primary effect: Move 4.

Special: during combat this turn, the player may spend remaining move points as:

- Attack 1 per move point (melee), OR
- Ranged Attack 1 per 2 move points

UI behavior:

- "Convert Move" UI shows during combat attack phase with two conversion options:

  **Attack 1 / point** | **Ranged Attack 1 / 2 points**
- Player selects conversion type, then number of points to spend

---

### diplomacy

**Unpowered** — Phase: Interaction (primary), and Combat Block (cross-phase)

Primary effect: Influence 2. Special: player may use accumulated influence as Block this
turn.

UI behavior:

- Play Diplomacy in the interaction phase for Influence 2
- During the combat block phase this turn: the block input shows influence as a spendable
  resource alongside normal block cards
- Player can accumulate more influence during the block phase and spend it as block at any
  point; 1 influence = 1 block
- The influence pool is shared — spending influence as block reduces the pool available
  for interaction

**Powered** — Phase: Interaction (primary), and Combat Block (cross-phase)

Primary effect: Influence 4. Special: choose Ice or Fire. Player may use influence as
block of the chosen element this turn.

UI sequence:

1. Player plays Diplomacy powered
2. Element picker: **Ice** | **Fire**
3. Player selects → influence can be spent as block of that element type during block phase
4. During combat block phase: block input shows influence pool with chosen element label

---

### into_the_heat

**Phase:** Start of Combat ONLY (before Ranged phase begins)

Effect: all player units get Attack +2 and Block +2 for this combat. Player may NOT
assign damage to units this combat — hard lock, no card or ability can override it.

**Start of Combat interstitial (general rule):** When the player initiates combat, the
engine enters a Start of Combat state before opening the Ranged phase. If the player
holds at least one start-of-combat card, this state is shown: the hand is visible, only
start-of-combat cards are playable (all others show phase-illegal / sideways only), and
a **"Begin Combat"** button advances to the Ranged phase. If the player holds NO
start-of-combat cards, the Start of Combat state is skipped entirely — no button, no
pause, Ranged phase opens immediately.

UI behavior:

- Start of Combat shown: Into the Heat is highlighted as playable
- On play: confirm overlay — "All units +2 Attack/Block. You cannot assign damage to
  units this combat. Proceed?" — trade-off is significant enough to warrant confirmation
- After player taps "Begin Combat" (or auto-advance): Into the Heat is greyed out for
  the rest of combat (sideways only)

**Powered** — same timing and lock; units get +3 Attack/Block instead of +2

---

### steady_tempo

**Unpowered** — Phase: Movement

Effect: Move 2. At end of turn, this card goes to the bottom of the deck instead of
discard (if deck is not empty).

UI: One-tap play. No choices at play time.

End-of-turn behavior: handled by `end_of_turn_place: deck_bottom`. If deck is empty at
end of turn, Steady Tempo goes to discard normally.

**Powered** — Phase: Movement

Effect: Move 4. At end of turn, this card goes to the top of the deck. If deck is empty at
end of turn, Steady Tempo goes to discard normally.

---

### pure_magic

**Unpowered** — Phase: depends on mana color paid

Effect: pay a mana of any basic color → effect depends on color.

| Mana Paid | Effect | Phase |
| --- | --- | --- |
| Green | Move 4 | Movement |
| White | Influence 4 | Interaction |
| Blue | Block 4 | Combat Block |
| Red | Attack 4 | Combat Melee |

UI sequence:

1. Player taps Pure Magic
2. Show mana color picker: basic colors available in player's pool
3. Player selects color → effect determined by color → applies to current phase
4. Phase legality is checked against the resulting effect type, not the card's base type

Phase legality: Pure Magic can be played in any phase, but the mana color chosen must
produce an effect legal in the current phase. Invalid combinations are shown disabled.

Precondition: player must have at least 1 basic mana.

**Powered** — Phase: same as unpowered, values increase to 7 each

---

### fire_bolt

**Unpowered** — Phase: Any

Effect: gain 1 red crystal. One-tap play. No choices.

**Powered** — Phase: Combat (Ranged or Melee)

Effect: Ranged Fire Attack 3. One-tap play.

---

### ice_bolt

**Unpowered** — Phase: Any

Effect: gain 1 blue crystal. One-tap play. No choices.

**Powered** — Phase: Combat (Ranged or Melee)

Effect: Ranged Ice Attack 3. One-tap play.

---

### swift_bolt

**Unpowered** — Phase: Any

Effect: gain 1 white crystal. One-tap play. No choices.

**Powered** — Phase: Combat (Ranged or Melee)

Effect: Ranged Attack 4. One-tap play.

---

### crushing_bolt

**Unpowered** — Phase: Any

Effect: gain 1 green crystal. One-tap play. No choices.

**Powered** — Phase: Combat (Ranged or Melee)

Effect: Siege Attack 3. One-tap play.

---

### ice_shield

**Unpowered** — Phase: Combat (Block)

Effect: Ice Block 3. One-tap play.

**Powered** — Phase: Combat (Block)

Effect: Ice Block 3. Reduce the Armor of one enemy blocked this way by 3 (minimum 1).
Armor reduction does not apply to enemies with ice resistance.

UI sequence (powered):

1. Block targeting resolves normally (player selects the enemy to block)
2. Ice Block 3 applies; if the targeted enemy does not have ice resistance, its Armor is
   also reduced by 3 (minimum 1) for the remainder of this combat
3. Armor reduction stacks with other reductions and applies to all subsequent attacks by
   that enemy in the same combat

---

### refreshing_walk

**Unpowered** — Phase: Movement

Effect: Move 2 and Heal 1. Both effects apply automatically.

UI behavior:

- Move 2 adds to movement pool normally
- Heal 1 adds 1 point to the heal pool. If no wounds in hand AND no wounded units, the
  heal component silently skips (not a precondition failure — card is always playable
  for its Move 2)

**Powered** — Phase: Movement

Effect: Move 4 and Heal 2. Same behavior; Heal 2 adds 2 points to the heal pool.

---

### intimidate

**Unpowered** — Phase: Interaction or Combat (Melee)

Effect: choose Influence 4 OR Attack 3. Reputation −1 at end of turn regardless of choice.

UI sequence:

1. Player taps Intimidate
2. Show 2-option panel: **Influence 4** | **Attack 3**
3. Phase filter: Influence 4 selectable only in Interaction; Attack 3 selectable only in
   Combat (Melee). The unavailable option is shown disabled.
4. Player selects → effect applies
5. Reputation −1 queued for end of turn (shown as pending in HUD)

The reputation penalty is unconditional — it applies even if the effect is cancelled.

**Powered** — Phase: Interaction or Combat (Melee)

Effect: Influence 8 OR Attack 7. Reputation −2 at end of turn. Same UI sequence.

---

### frost_bridge

**Unpowered** — Phase: Movement

Effect: Move 2. Swamp move cost reduced to 1 this turn.

One-tap play. Passive terrain modifier — swamp hex labels update to "1" on play.

**Powered** — Phase: Movement

Effect: Move 4. Swamp and lake costs both reduced to 1. Lakes are unlocked for travel.

UI behavior (powered):

- Lakes (normally impassable) become highlighted as valid move targets
- Both swamp and lake hexes show cost label "1"
- No further player input required — passive for remainder of turn

---

### song_of_wind

**Unpowered** — Phase: Movement

Effect: Move 2. Plains, desert, and wasteland costs reduced by 1 (minimum 0) this turn.

One-tap play. Passive terrain modifier.

**Powered** — Phase: Movement

Effect: Move 2. Plains, desert, and wasteland costs reduced by 2 (minimum 0). Optional:
pay 1 blue mana to unlock lake travel at cost 1 this turn.

UI sequence (powered):

1. Player plays Song of Wind powered (white mana)
2. Terrain modifiers apply immediately (plains/desert/wasteland −2)
3. Optional prompt: "Pay 1 blue mana to unlock lakes at cost 1 this turn?" **Yes** / **No**
4. If Yes: player selects a blue mana source → lakes become passable at cost 1
5. If No: lakes remain impassable; no additional mana spent

The lake unlock is decided at play time, not when the player first steps toward a lake hex.
If the player later realizes they forgot to pay blue mana, undo is available provided no
new information has been revealed since the play (tile flip, enemy drawn, die rolled).

---

### path_finding

**Unpowered** — Phase: Movement

Effect: Move 2. All terrain costs reduced by 1 (minimum 2) this turn.

One-tap play. Passive modifier — all hex labels update.

**Powered** — Phase: Movement

Effect: Move 4. All terrain costs set to exactly 2 this turn.

Note: the powered cap is a hard maximum. Other terrain modifiers in effect this turn
cannot reduce costs below 2 when Path Finding powered is active.

---

### blood_ritual

**Unpowered** — Phase: Any

Effect: take 1 Wound → gain 1 red mana token + gain 1 mana token of any color (including
black during Night; not during Day).

UI sequence:

1. Player taps Blood Ritual
2. Confirm overlay — "Take 1 Wound to proceed?" (always confirm; wound is irreversible)
3. On confirm: Wound card added to hand
4. Red mana token granted automatically
5. Color picker for second token: all basic colors + black (black shown disabled during Day)
6. Player selects → second token granted

**Powered** — Phase: Any

Effect: take 1 Wound → gain 3 mana tokens of any colors (including black during Night).

UI sequence:

1. Confirm wound overlay
2. On confirm: Wound added
3. Multi-select color picker — player allocates 3 token colors (same Day restriction on
   black applies)
4. All 3 tokens granted

---

### heroic_tale

**Unpowered** — Phase: Interaction

Effect: Influence 3. At end of turn, Reputation +1 for each unit recruited this turn.

One-tap play. End-of-turn hook counts units recruited this turn — both before and after
playing Heroic Tale count toward the bonus.

**Powered** — Phase: Interaction

Effect: Influence 6. At end of turn, Fame +1 and Reputation +1 for each unit recruited.

Same hook; both bonuses apply per unit recruited.

---

### regeneration

**Unpowered** — Phase: Movement, Interaction, During Rest, End of Turn (NOT combat)

Effect: Heal 1 AND ready one Level I or II unit. Both components apply; neither is
optional.

UI sequence:

1. Player taps Regeneration
2. Heal 1: adds 1 point to the heal pool. If no wounds in hand AND no wounded units,
   the heal component silently skips.
3. If player controls spent Level I or II units: unit-selection prompt — player selects
   1 spent unit; that unit becomes Ready (command token moved back above it)
4. If no eligible spent units: unit-ready component silently skips

Precondition: at least one component must have a valid target — a wound in hand or
wounded unit (for heal), OR a spent Level I/II unit (for ready). If both components have
no valid targets, the card is greyed out for normal play — sideways only. Players who
don't want it can discard it at end of turn (rulebook p.4: unplayed cards may be
discarded at end of turn).

**Powered** — Phase: Movement, Interaction, During Rest, End of Turn (NOT combat)

Effect: Heal 2 AND ready one Level I, II, or III unit. Same UI sequence. Heal 2 adds 2
points to the heal pool. Precondition: same — greyed out if no heal targets AND no spent
Level I/II/III units.

---

### in_need

**Unpowered** — Phase: Interaction

Effect: Influence 2 + 1 Influence per wound card in hand or on any controlled unit.

No choices. Value calculated at play time. UI: play confirmation overlay shows total —
"Influence [2 + N wounds] = [total]". Wounds counted: wounds in hand + wound markers on
controlled units.

**Powered** — Phase: Interaction

Effect: Influence 4 + 2 Influence per wound (same count). Doubled per-wound bonus.

---

### decompose

**Unpowered** — Phase: Any

Effect: throw away one Action card from hand → gain 2 crystals of that card's mana color.

UI sequence:

1. Player taps Decompose
2. Hand display — player selects one Action card to throw away (wounds not selectable;
   Decompose not selectable)
3. Card permanently removed from game
4. Player gains 2 crystals matching the thrown card's mana color (e.g. red card → 2 red)

Precondition: at least 1 other Action card in hand (not wounds, not Decompose).

**Powered** — Phase: Any

Effect: throw away one Action card → gain 1 crystal of each basic color that does NOT
match the thrown card's mana color.

Same UI sequence. Example: throw a red card → gain 1 blue + 1 white + 1 green crystal.

---

### crystal_mastery

**Unpowered** — Phase: Any

Effect: gain 1 crystal matching a color you already own.

UI sequence:

1. Player taps Crystal Mastery
2. Crystal color picker — only colors where inventory count > 0 are selectable
3. Player selects → gains 1 crystal of that color

Precondition: must own at least 1 crystal. If inventory is empty, sideways only.

**Powered** — Phase: Any

Effect: at end of turn, all crystals spent this turn are returned to inventory.

One-tap play. `CrystalRefundHook` activates at end of turn — crystals spent during the
turn are returned. The player may treat crystals as free mana for the turn.

---

### mana_storm

**Unpowered** — Phase: Any

Effect: choose a basic-color source die → gain 1 crystal of that color → immediately
reroll that die.

UI sequence:

1. Player taps Mana Storm
2. Source panel highlights — player selects one die showing a basic color (gold/black not
   selectable)
3. Player gains 1 crystal of that die's color
4. Die is immediately rerolled (normal reroll; no-reroll flag is NOT set)

Precondition: at least one basic-color die in Source. If all dice show black/gold (or
Source empty), card cannot be played normally.

**Powered** — Phase: Any

Effect: reroll all Source dice. Gain 3 extra source dice this turn. All Source dice
showing black or gold may be used as any basic color this turn, regardless of Day/Night.

UI sequence (powered):

1. Player plays Mana Storm powered
2. All Source dice reroll immediately
3. Extra dice counter set to +3
4. Black and gold dice gain "any basic color" indicator — tapping one triggers a color
   picker (red/blue/white/green). During Day, black is not offered in the picker even for
   black dice. During Night, black is available as an additional option for black dice.

The black/gold override is turn-scoped only. It does not override the Day restriction on
black mana — it only allows the die to stand in for a basic color.

---

### ambush

**Unpowered** — Phase: Movement (primary); bonus fires in subsequent combat

Effect: Move 2. Additionally, the player's first Attack card play OR first Block card
play this turn gets a bonus: +1 Attack or +2 Block (whichever comes first).

UI behavior:

- Move 2 applies on play
- A "first-combat-play bonus" flag is set: +1 Attack / +2 Block
- When the first Attack or Block card is played in combat, the bonus applies automatically
  and the flag clears; the unused option is forfeited
- HUD shows a small "Ambush ready" indicator until the bonus fires

**Powered** — Phase: Movement; bonus fires in subsequent combat

Effect: Move 4. First Attack +2 or first Block card +4. Same behavior; larger bonuses.

---

### maximal_effect

**Unpowered** — Phase: depends on the thrown card

Effect: throw away one Action card from hand → use that card's basic (unpowered) effect
3 times.

UI sequence:

1. Player taps Maximal Effect
2. Hand display — player selects one Action card to throw away
3. Card permanently removed
4. Thrown card's unpowered effect fires 3 times sequentially
5. If the effect has a `choose_one`: player chooses independently each of the 3 times
6. Phase filter applies to each use individually

Precondition: at least 1 other Action card in hand.

**Powered** — Phase: depends on the thrown card

Effect: throw away one Action card → use that card's powered effect twice, for free (no
mana cost for either use).

Same UI sequence. Powered effect fires twice; choose_one cards allow an independent
choice each time.

End-of-turn hooks fire once per use: 3 times via ME unpowered, 2 times via ME powered.
Exception: Crystal Mastery powered sets a state flag (not a counter) — the refund fires
once regardless of how many times ME triggers it.

---

### magic_talent

**Unpowered** — Phase: Any (follows phase rules of the borrowed Spell)

Effect: discard 1 card from hand → use any Spell from the Spells offer as if it were in
your hand. The Spell stays in the offer (not acquired).

UI sequence:

1. Player taps Magic Talent
2. Hand display — player selects one card to discard (any card including wounds; not
   Magic Talent itself)
3. Spells offer shown — player selects one Spell
4. Selected Spell appears in a "borrowed" card slot in the play UI
5. Player plays the borrowed Spell as normal (may pay mana to use its powered version)
6. After use (or at end of turn if unused), borrowed Spell returns to offer

The Spell is selected at Magic Talent play time. Unused borrowed Spells return to the
offer at end of turn.

**Powered** — Phase: Any

Effect: pay 1 mana of any color → gain a Spell matching that mana's color from the offer
into your discard pile.

UI sequence:

1. Player plays Magic Talent powered (blue mana)
2. Mana color picker: any basic color
3. Spells offer filters to matching color only
4. Player selects one → gained to discard pile

Precondition: offer must contain at least one Spell of the chosen color.

---

### learning

**Unpowered** — Phase: Interaction

Effect: Influence 2. Once this turn, may spend exactly 6 total Influence to acquire an
Advanced Action from the offer into discard.

The Influence 2 is plain Influence — stacks with all other sources and can be used to
recruit units (at their variable costs) or any other Interaction purpose. It is not
reserved for the Advanced Action purchase.

UI behavior:

- Influence 2 added to running total immediately
- "Acquire Advanced Action (costs 6 Influence)" option becomes available in the offer UI
  whenever the player's accumulated Influence is ≥ 6 this turn
- On acquire: 6 Influence consumed; selected card added to discard; option clears
- If unused by end of turn: no penalty

**Powered** — Phase: Interaction

Effect: Influence 4. Once this turn, may spend exactly 9 total Influence to acquire an
Advanced Action directly into hand. Same behavior; card goes to hand instead of discard.

---

### training

**Unpowered** — Phase: Any

Effect: throw away one Action card from hand → take one Advanced Action of the same mana
color from the offer into your discard pile.

UI sequence:

1. Player taps Training
2. Hand display — player selects one Action card to throw away
3. Thrown card's mana color is noted
4. Advanced Actions offer shown; non-matching colors disabled
5. Player selects one matching Advanced Action → added to discard
6. Thrown card permanently removed

Precondition: (a) at least 1 other Action card in hand, and (b) the offer must contain
an Advanced Action matching the mana color of at least one card in hand.

**Powered** — Phase: Any

Same as unpowered; the acquired Advanced Action goes to hand instead of discard.

---

## Spells

### burning_shield

**Unpowered** — Phase: Combat Block

Effect: Fire Block 4. If this block is part of a successful block (enemy attack fully
blocked), player may also use it as Fire Attack 4 during the Attack phase this combat.

UI sequence (block phase):

1. Player plays Burning Shield as Fire Block 4 against an enemy attack
2. If the attack is fully blocked: flag is set — player has a "Burning Shield Attack"
   available
3. During combat Attack phase: show optional "Use Burning Shield Fire Attack 4?" prompt
4. Player confirms → Fire Attack 4 resolves; effect now fully resolved

Note: the Fire Attack is optional. If the player declines in the Attack phase, Burning
Shield has still served as a block.

**Powered** — Phase: Combat Block

Effect: Fire Block 4. If this block is part of a successful block, the blocked enemy is
destroyed (regardless of remaining armor), unless the enemy has fire resistance.

No attack phase follow-up. Destruction replaces the attack step for that enemy.

---

### whirlwind

**Unpowered** — Phase: Combat (Ranged or Melee or Block — "before combat" effect)

Effect: target enemy does not attack this combat.

UI: player taps Whirlwind → enemy targeting mode → player taps enemy → confirm. The
targeted enemy's attack is cancelled for this combat. Can be played in any combat phase
since it modifies an enemy's behavior rather than contributing attack/block directly.

**Powered** — Phase: Combat Attack phase ONLY

Effect: destroy target enemy (regardless of armor).

Phase restriction: this powered effect can only be played during the Attack phase.
Outside the Attack phase, the powered version is unavailable (card can still be played
unpowered for its normal effect or sideways).

UI: same targeting flow. Enemy is removed from combat on confirm.

---

### demolish

**Unpowered** — Phase: Combat (any)

Effect: ignore site fortifications this turn; all enemies get Armor −1 (minimum 1).
Does not affect enemies who have fire resistance.

One-tap play. No choices. Effects apply globally for the remainder of combat.

**Powered** — Phase: Combat Attack phase ONLY

Effect: destroy target enemy. All enemies get Armor −1 (minimum 1). Does not affect enemies who have fire resistance.

Phase restriction: powered Demolish can only be played during the Attack phase.

---

### wings_of_wind

**Unpowered** — Phase: Movement

Effect: spend 1–5 move points to teleport to revealed safe spaces (1 space per point).
Does not provoke rampaging enemies.

UI sequence:

1. Player plays Wings of Wind
2. Hex map enters targeting mode — valid destinations highlighted (revealed, safe, within
   5 spaces of current position regardless of path)
3. Player taps destination → move point cost shown (1 per space teleported)
4. Player confirms → teleports

Note: spaces are counted by straight-line count to destination, not path cost. Swamp and
lake movement restrictions are bypassed.

**Powered** — Phase: Combat (during combat only)

Effect: if player entered combat with at least as many unspent move points as the number
of enemies, skip the Block and Assign Damage phases.

UI:

1. Card only shows as playable (non-sideways) during combat and only if condition is met
2. If condition not met: card is shown disabled for powered play (grey) — sideways only
3. On play: confirm overlay — "Skip Block and Damage phases? Unspent move points:
   [N], Enemies: [N]" — confirm → phases skipped, game advances to Attack phase

---

### space_bending

**Unpowered** — Phase: Movement

Effect: this turn, spaces 2 hexes away are treated as adjacent. Movement does not
provoke rampaging enemies.

UI: passive effect. On play, hex map updates to show 2-away spaces as adjacent (move
cost 1 instead of normal). No further input needed.

**Powered** — Phase: Any (end of turn resolution)

Effect: time bending — set card aside for the Round; put remaining hand cards into draw
pile; skip draw step; cards played this turn (not discarded or thrown away) return to
hand; take another full turn immediately.

This is an extraordinary effect. UI handling:

1. Player plays Space Bending powered at any point during their turn
2. Confirm overlay — "Take another complete turn? Cards you played this turn return to
   your hand. This card is set aside until next round."
3. On confirm:

   - Space Bending set aside (not in deck, not in discard) until start of next round
   - Cards remaining in hand → shuffled into draw pile
   - Cards played this turn (that went to discard) → returned to hand
   - Cards discarded as costs or thrown away → stay discarded/thrown away
   - No draw step — extra turn begins with the returned played-cards as hand
4. New full turn begins immediately with returned cards

Edge case: if the player played no cards this turn when Space Bending resolves, the extra
turn begins with an empty hand.

---

### fireball

**Unpowered** — Phase: Combat (Ranged or Melee)

Effect: Ranged Fire Attack 5. One-tap play.

**Powered** — Phase: Combat (Ranged or Melee) — Firestorm

Effect: take 1 Wound → Siege Fire Attack 8.

UI sequence (powered):

1. Confirm overlay — "Take 1 Wound for Siege Fire Attack 8?"
2. On confirm: Wound added to hand; Siege Fire Attack 8 resolves

---

### snowstorm

**Unpowered** — Phase: Combat (Ranged or Melee)

Effect: Ranged Ice Attack 5. One-tap play.

**Powered** — Phase: Combat (Ranged or Melee) — Blizzard

Effect: take 1 Wound → Siege Ice Attack 8. Same confirm-overlay sequence as Fireball.

---

### expose

**Unpowered** — Phase: Combat (Ranged or Melee)

Effect: target one enemy → that enemy loses all fortifications and resistances this
combat. Ranged Attack 2.

UI sequence:

1. Player plays Expose
2. Enemy targeting — player selects one enemy
3. That enemy loses all fortification bonuses and all elemental/physical resistances for
   this combat
4. Ranged Attack 2 resolves against that enemy

**Powered** — Phase: Combat (Ranged or Melee) — Mass Expose

Effect: choose — all enemies lose all fortifications, OR all enemies lose all resistances.
Ranged Attack 3.

UI sequence (powered):

1. 2-option panel: **Remove fortifications (all enemies)** | **Remove resistances (all enemies)**
2. Player selects → global effect applies immediately
3. Ranged Attack 3 resolves

---

### tremor

**Unpowered** — Phase: Combat

Effect: choose — target one enemy: Armor −3 (minimum 1), OR all enemies: Armor −2
(minimum 1).

UI sequence:

1. 2-option panel: **One enemy: Armor −3** | **All enemies: Armor −2**
2. If "one enemy": targeting mode — player selects enemy; Armor reduced by 3 (min 1)
3. If "all enemies": all enemy Armors reduced by 2 (min 1)

**Powered** — Phase: Combat — Earthquake

Effect: choose — target one enemy: Armor −6 if fortified / −3 otherwise (min 1), OR all
enemies: Armor −4 if fortified / −2 otherwise (min 1).

Same 2-option panel; escalated values. Fortified status is checked per enemy individually.

---

### flame_wall

**Unpowered** — Phase: Combat (Attack or Block)

Effect: choose — Fire Attack 5 OR Fire Block 7.

UI: 2-option panel with phase filter:

| Option | Legal Phases |
| --- | --- |
| Fire Attack 5 | Combat Melee |
| Fire Block 7 | Combat Block |

**Powered** — Phase: Combat — Flame Wave

Effect: Fire Attack 5 OR Fire Block 7. The chosen attack or block gets +2 per enemy
token currently facing the player.

UI sequence (powered):

1. 2-option panel (same phase filter)
2. On selection: total shown — "Fire Attack [5 + 2×N] = [X]" or "Fire Block [7 + 2×N] = [X]"
   where N = current enemy token count
3. Confirm → effect resolves with bonus included

---

### mana_bolt

**Unpowered** — Phase: Combat

Effect: pay 1 additional mana of any basic color → attack determined by color.

| Mana Paid | Attack |
| --- | --- |
| Blue | Ice Attack 8 |
| Red | Cold Fire Attack 7 |
| White | Ranged Ice Attack 6 |
| Green | Siege Ice Attack 5 |

UI sequence:

1. Player plays Mana Bolt (blue mana to cast)
2. Additional mana color picker: basic colors available in pool
3. Selected color determines attack type and value
4. Attack resolves

Precondition: at least 1 basic mana remaining after the blue cast cost.

**Powered** — Phase: Combat — Mana Thunderbolt

Same flow with upgraded values:

| Mana Paid | Attack |
| --- | --- |
| Blue | Ice Attack 11 |
| Red | Cold Fire Attack 9 |
| White | Ranged Ice Attack 8 |
| Green | Siege Ice Attack 8 |

---

### underground_travel

**Unpowered** — Phase: Movement

Effect: teleport to any revealed safe space within 3 hexes. Cannot end in swamp or lake.
Does not provoke rampaging enemies.

UI sequence:

1. Player plays Underground Travel
2. Hex map targeting mode — valid destinations highlighted (revealed, not swamp/lake,
   within 3 hexes by straight-line count; no move point cost)
3. Player taps destination → teleports

Distance is straight-line count, not path cost. Impassable terrain between start and
end does not matter.

**Powered** — Phase: Movement or Combat — Underground Attack

Effect: same teleport, but must end on a fortified site or a space occupied by another
player. Counts as initiating an assault or attack at the destination.

UI:

1. Hex map highlights only valid assault targets (fortified sites within 3 hexes; in
   multiplayer, also spaces occupied by other players)
2. Player selects → teleports and immediately enters combat/assault at that location
3. If withdrawing from that combat: player returns to pre-teleport hex

Solo v1 note: "space occupied by another player" does not apply — only fortified sites
are valid destinations.

---

### chill

**Unpowered** — Phase: Combat (any)

Effect: target enemy does not attack this combat. If it has Fire Resistance, it also
loses that resistance for the rest of the turn. Cannot target enemies with Ice Resistance.

UI sequence:

1. Player plays Chill
2. Enemy targeting — player selects one enemy
3. Targeted enemy's attack is cancelled for this combat
4. If target has fire_resistance: resistance removed; status change shown on enemy token

**Powered** — Phase: Combat (any) — Lethal Chill

Effect: target enemy does not attack this combat AND gets Armor −4 (minimum 1) for the
rest of the turn. Cannot target enemies with Ice Resistance.

Same targeting. Two simultaneous effects: attack cancelled + Armor −4 (min 1).

---

### restoration

**Unpowered** — Phase: Movement, Interaction, During Rest, End of Turn (NOT combat)

Effect: Heal 3. Heal 5 instead if the player is currently in a forest hex.

UI: one-tap play. Forest check is automatic — if in forest, heal value shows as 5.
Adds 3 (or 5 in forest) points to the heal pool.

**Powered** — Phase: same — Rebirth

Effect: Heal 3 (or 5 in forest). Additionally, ready controlled units up to a combined
level total of 3 (or 5 in forest).

UI sequence (powered):

1. Heal: adds heal value (3 or 5) to the heal pool
2. Unit-ready prompt: player selects wounded units to ready; running level total shown
   (e.g. L2 + L1 unit = 3 levels consumed). Selection stops when limit reached.
3. Player confirms

---

### call_to_arms

**Unpowered** — Phase: Interaction or Combat (depends on unit ability used)

Effect: use one ability of a unit in the Units offer as if it were one of your recruited
units this turn. That unit cannot have damage assigned to it.

UI sequence:

1. Player plays Call to Arms
2. Units offer shown — player selects one unit
3. That unit's ability (attack/block value) becomes available in the current phase
4. The unit is marked "no damage" — greyed out in damage assignment UI if combat occurs
5. At end of combat/turn: unit returns to offer (not recruited, not spent)

**Powered** — Phase: Interaction — Call to Glory

Effect: recruit any one unit from the Units offer for free. If at Command limit, disband
one current unit first.

UI sequence:

1. Units offer shown — all units selectable (no influence cost)
2. If at Command limit: "Disband a unit to make room?" — disband picker shown
3. Player selects unit to recruit → added to units zone

---

### meditation

**Unpowered** — Phase: Any (end-of-turn draw at end of turn)

Effect: randomly pick 2 cards from discard → place each on top or bottom of your Deed
deck. Draw 2 extra cards at end of turn (above hand limit).

UI sequence:

1. Player plays Meditation
2. 2 cards randomly selected from discard (fewer if discard has fewer than 2)
3. For each card: player chooses **Top of deck** or **Bottom of deck**
4. End-of-turn hook: player draws 2 extra cards on top of normal hand-limit draw

**Powered** — Phase: Any — Trance

Same as unpowered, but player chooses which 2 cards from discard (not random).

1. Discard pile shown — player selects any 2 cards
2. For each: Top or Bottom of deck choice
3. End-of-turn hook: same +2 cards draw

---

## Artifacts

### Universal Rules — Artifacts

Artifacts differ from Actions and Spells in how their two effects are triggered:

- **Basic effect:** play the card; artifact goes to discard at end of turn.
- **Powered (break) effect:** throw the artifact away (permanently removed from game) to
  trigger the strong effect. Always confirm before breaking: "This artifact will be
  permanently removed from the game. Proceed?"
- **Sideways:** always available as with any deed card.

Artifacts are never powered by spending mana. The cost of the strong effect is the
artifact itself.

---

### Banners — Additional Rules

Banners are a subtype of Artifact with a persistent passive effect while assigned to a
unit.

**Assigning (basic effect):** The banner is placed in the unit zone partially overlapping
the assigned unit card. The unit gains the banner's listed passive bonuses for the
remainder of the round.

- Only one banner per unit. Assigning a second banner to the same unit sends the first
  to the discard pile.
- If the assigned unit is destroyed or disbanded, the banner goes to discard.
- At the end of each round the player may keep the banner assigned or shuffle it back
  into their Deed deck.

**Breaking (powered effect):** A banner may be broken either from hand or from the unit
zone. In both cases the confirm prompt fires and the card is removed from game.

**UI when banner is in hand:** tap → show three options: **Assign to Unit** \| **Break** \| **Play Sideways**.

---

### banner_of_glory

**Basic (Assign)** — Phase: Any

Assigned unit gains Armor +1, Attack +1, Block +1 passively. Fame +1 each time that
unit attacks or blocks in combat (per-use: one attack + one block = Fame +2).

**Powered (Break)** — Phase: Start of Combat or any combat phase

All units the player controls gain Armor +1, Attack +1, Block +1 for the rest of this
combat. Fame +1 for each unit that attacks or blocks during this combat (tracked until
combat ends, end-of-combat hook).

---

### banner_of_fear

**Basic (Assign)** — Phase: Any

During the Block phase, the player may exhaust the assigned unit to cancel one enemy
attack. Fame +1 if they do. The unit must be Ready; this uses its action for the round.

UI: during Block phase, if a Ready unit has Banner of Fear assigned, a **"Cancel Attack"**
option appears alongside that unit. Player selects which enemy attack to cancel → unit
exhausts → attack removed → Fame +1.

**Powered (Break)** — Phase: Start of Combat or during the Ranged phase (before Block opens)

Skip the Block and Damage phases of this combat entirely. The player takes no damage.
After the Ranged Attack phase, go directly to the Melee Attack phase.

---

### banner_of_protection

**Basic (Assign)** — Phase: Any

Assigned unit gains Armor +1, fire resistance, and ice resistance passively. Hero is
unaffected.

**Powered (Break)** — Phase: Any (effect fires at end of turn)

At end of this turn, all Wound cards the player received this turn are thrown away
(removed from game). Only wounds received this turn are affected — wounds already in
hand from prior turns are not.

Breaking registers an end-of-turn hook; the break itself can happen at any point during
the turn.

---

### banner_of_courage

**Basic (Assign)** — Phase: Any

Once per round (not during combat), the player may flip this banner face down to Ready
the assigned unit. The banner flips face up at the start of each round.

UI: a **"Ready Unit"** button appears on the unit when Banner of Courage is assigned and
face-up. Disabled during combat. Tapping it flips the banner face down and readies the
unit.

**Powered (Break)** — Phase: Any except combat

Ready all units the player controls. One-tap after confirm.

---

### Rings

All four rings share the same basic structure. One-tap play; ring goes to discard.

| Card | Basic Effect |
| --- | --- |
| ruby_ring | Gain 1 red mana token + 1 red crystal + Fame +1 |
| sapphire_ring | Gain 1 blue mana token + 1 blue crystal + Fame +1 |
| diamond_ring | Gain 1 white mana token + 1 white crystal + Fame +1 |
| emerald_ring | Gain 1 green mana token + 1 green crystal + Fame +1 |

**Powered (Break)** — all rings

Breaking a ring sets an `mana_unlimited[color]` and `mana_unlimited[black]` flag for
the rest of the turn. Any card or unit ability requiring [ring color] or black mana is
automatically satisfied without consuming a source die or crystal.

The Day restriction on black still applies: during Day, the black component of the
flag is inert. The ring's color is freely available regardless of Day/Night.

In addition, Fame +1 is granted for each Spell of the ring's color cast this turn
(end-of-turn hook counts total spells of that color played).

| Card | Powered colors | Day note |
| --- | --- | --- |
| ruby_ring | Red + Black | Red: always. Black: Night only |
| sapphire_ring | Blue + Black | Blue: always. Black: Night only |
| diamond_ring | White + Black | White: always. Black: Night only |
| emerald_ring | Green + Black | Green: always. Black: Night only |

---

### sword_of_justice

**Unpowered** — Phase: Combat (Melee)

Effect: discard any number of Action cards from hand → Attack 3 per card discarded.
Fame +1 per enemy defeated this turn (end-of-turn hook).

UI sequence:

1. Player taps Sword of Justice
2. Hand display — multi-select; Action cards only (Wounds excluded)
3. Running total shown: "Attack [3×N]" as cards are selected
4. Player confirms → selected cards discarded → Attack applied
5. End-of-turn hook: count enemies defeated this turn → Fame += count

Precondition: none (discarding zero cards is legal but Attack 0 is useless).

**Powered (Break)** — Phase: Start of Combat or any combat phase

Effect: double all physical (non-elemental) Attack values for the rest of this turn.
Enemies lose physical resistance for this combat. Fame +1 per enemy defeated this combat.

Breaking fires immediately: `physical_attack_doubler` flag set; enemy physical resistance
stripped for this combat only; per-enemy fame hook registered. Cards already played this
turn before breaking are not retroactively doubled.

---

### horn_of_wrath

**Unpowered** — Phase: Combat (Ranged or Siege)

Effect: Siege Attack 6. Immediately roll 1 mana die; take 1 Wound if the result is black
or red.

The die roll is automatic on play and constitutes new information — undo gate closes.

UI sequence:

1. Siege Attack 6 applied
2. Die roll animation → result displayed
3. Black or red result: Wound added to hand with notification. Other results: no penalty.

**Powered (Break)** — Phase: Combat (Ranged or Siege)

Effect: Siege Attack 6 + up to +6 bonus attack chosen by the player. For each +1 of
bonus chosen, 1 mana die is rolled; a Wound is taken for each black or red result.

UI sequence:

1. Confirm break prompt
2. Bonus selector: 0–6 (default 0)
3. Player confirms bonus N → Siege Attack (6 + N) applied
4. N dice roll sequentially; each black or red result adds 1 Wound
5. Undo gate closes on first die roll

---

### endless_bag_of_gold

One-tap play in both modes. No choices.

| Mode | Effect |
| --- | --- |
| Basic | Influence 4, Fame +2 — Phase: Interaction |
| Powered (Break) | Influence 9, Fame +3 — Phase: Interaction |

---

### endless_gem_pouch

**Unpowered** — Phase: Any

Effect: roll 1 mana die twice. For each result: basic color → gain crystal of that color;
gold → player's choice of crystal color; black → Fame +1, no crystal.

UI sequence:

1. First die roll animation → result resolved (crystal granted or Fame +1; gold triggers
   1-color picker)
2. Second die roll → same resolution
3. Undo gate closes on first roll

**Powered (Break)** — Phase: Any

Effect: gain 1 mana token of each basic color (4 tokens: red + blue + white + green).
Additionally gain 1 gold token during Day or 1 black token during Night.

One-tap. Day/Night token determined automatically by current round state.

---

### golden_grail

**Unpowered** — Phase: Movement, Interaction, During Rest, End of Turn (NOT combat)

Effect: Heal 2. Registers a turn-scoped hook: Fame +1 per heal point spent this turn,
maximum 2 Fame total. Adds 2 points to the heal pool. One-tap play.

**Powered (Break)** — Phase: Movement, Interaction, During Rest, End of Turn (NOT combat)

Effect: Heal 6. Each time a Wound is removed from hand this turn, draw 1 card.

UI sequence:

1. Confirm break prompt
2. Adds 6 points to the heal pool
3. `draw_per_wound_healed_from_hand` hook registered for the rest of the turn — fires
   each time a wound card is removed from hand when heal pool is spent

---

### book_of_wisdom

**Unpowered** — Phase: Any

Effect: throw away 1 Action card from hand → gain an Advanced Action from the offer
matching that card's mana color directly into hand.

UI sequence:

1. Player plays Book of Wisdom (book to discard)
2. Hand display — player selects 1 Action card to throw away
3. Card's mana color noted; card permanently removed
4. Advanced Action offer shown filtered to matching color
5. Player selects one → added to hand

Precondition: player holds ≥ 1 Action card; offer contains ≥ 1 Advanced Action of
matching color.

**Powered (Break)** — Phase: Any

Effect: throw away 1 Action card from hand → gain a Spell matching that card's mana
color directly into hand + 1 crystal of that color.

UI sequence:

1. Confirm break prompt (book removed from game)
2. Hand display — player selects 1 Action card to throw away
3. Color noted; card removed
4. Spell offer filtered to matching color; player selects one → added to hand
5. Crystal of matching color added to inventory

---

### amulet_of_sun

**Unpowered** — Phase: Any

Effect: gain 1 gold mana token. If it is currently Night, additionally for this turn:

- Forest hexes cost 3 Move to enter
- Gold mana counts as any basic color (red/blue/white/green)
- Fortified sites and Ruins are treated as Day for combat purposes

During Day only the gold token is granted; Night modifiers are inactive.

**Powered (Break)** — Phase: Any

Identical effect, except 3 gold mana tokens are gained instead of 1.

---

### amulet_of_darkness

**Unpowered** — Phase: Any

Effect: gain 1 mana token of any color (color picker: red/blue/white/green/black — the
Day restriction on black does NOT apply here; the amulet grants it regardless of round).
If it is currently Day, additionally for this turn:

- Desert hexes cost 3 Move to enter
- Black mana counts as Night mana (may power Night-restricted spell effects)

During Night only the free mana token is granted; Day modifiers are inactive.

**Powered (Break)** — Phase: Any

Effect: gain 3 mana tokens of any colors (3 independent color pickers; may all be
different; black available regardless of Day/Night). Same Day terrain and
black-as-Night-mana modifier as basic effect.

---

## Open Questions

Items awaiting John's input before implementation:

---

## Resolved Questions

- **Space Bending extra turn:** Return any cards played (not discarded as costs or thrown
  away) this turn to hand. Cards in hand go to draw pile. No draw step. ✅
- **Improvisation — grayed-out vs unplayable:** Card can be played; illegal options are
  shown disabled in the panel (grayed-out), not hidden. Fully unplayable only when ALL
  four options are illegal in the current phase. ✅
- **Diplomacy powered — element:** Ice or Fire picker. Influence spends as that element
  type during the block phase. ✅
- **Blood Ritual / Mana Storm — black mana Day restriction:** Neither card overrides the
  Day restriction on black mana. During Day, black is not a valid color choice in either
  card's picker. Mana Storm powered allows black/gold dice to stand in for any basic color
  (red/blue/white/green) — not black — during Day. ✅
- **Heal 2 — UI:** Adds 2 points to the heal pool; wound selection occurs when pool is
  spent, not at card play time. ✅
- **Book of Wisdom — card destinations:** Both effects deliver to hand. Unpowered: Advanced
  Action → hand. Powered (break): Spell → hand + crystal of matching color. Confirmed
  from card overview PDF. ✅
- **Golden Grail unpowered — Fame scope:** Fame +1 per healing point from this card only
  (max +2), not a turn-scoped hook for all healing. Powered hook ("draw per wound healed
  from hand this turn") remains broader as written. ✅
- **Regeneration — no-op play:** No-op play is not needed. Players can discard unwanted
  cards at end of turn (rulebook p.4). Card is greyed out (sideways only) when both
  components have no valid targets. ✅
- **Maximal Effect — end-of-turn hook stacking:** Hooks fire per-use (3× via ME
  unpowered, 2× via ME powered). Exception: Crystal Mastery powered is a state flag —
  refund fires once only. ✅
- **Song of Wind powered — lake unlock timing:** Optional blue mana is paid at play time
  (alongside white mana), not deferred to first lake step. Effect ("lakes cost 1") persists
  for the turn. Undo handles forgotten payment provided no new info revealed. ✅
- **Concentration / Will Focus powered — choice panel:** If the second card's powered
  effect has a `choose_one`, the full choice panel fires exactly as if playing that card
  directly in powered mode (same options, same phase filter). Many cards have this
  (Instinct, Improvisation, Battle Versatility, etc.) — this is a live requirement, not
  a forward guard. ✅

---

## Unit Abilities

### Activation Model

A unit is **Ready** when its Command token sits above it; **Spent** when the token sits on
top; **Wounded** when a Wound card is laid across it.

To activate a Ready unit:

1. Move its Command token onto it (unit is now Spent)
2. Choose one ability from the unit card
3. Pay mana if the ability has a `mana_cost`
4. Resolve the effect

A Spent or Wounded unit cannot be activated. Units are automatically Readied at the start
of each Round (not healed). Wounding a unit does not make it Spent; it is a separate state.

**Phase rules:** Unit abilities follow the same phase legality as card effects — a Block
ability is only selectable in Combat (Block) phase, a Move ability in Movement phase, etc.
A unit's combat abilities are available in the same phases as the equivalent card effect.

**Mana for units:** Mana used to power a unit ability follows the same source-die rules as
cards — a source die taken to pay for a unit ability must be used immediately and returns
to the source at round end.

---

### Peasants — Level I, Armor 3, Cost 4 (Village)

Three independent abilities; player chooses which one to use on activation.

| Ability | Effect | Phase |
| --- | --- | --- |
| Attack or Block 2 | `choose_one`: Attack 2 or Block 2 | Combat |
| Influence 2 | Influence 2 | Interaction |
| Move 2 | Move 2 | Movement |

UI: activation → 3-option panel; phase filter hides options not legal in current phase.

---

### Herbalists — Level I, Armor 2, Cost 3 (Village, Monastery)

| Ability | Mana | Effect | Notes |
| --- | --- | --- | --- |
| Heal 2 | Green | Heal 2 | Adds 2 points to the heal pool |
| Ready Unit | — | Ready a Level I or II unit | Same unit picker as Rejuvenate |
| Green Mana Token | — | Gain 1 green mana token | Immediate; no choices |

UI: activation → 3-option panel. "Heal 2" disabled if no wounds in hand AND no wounded
controlled units. "Ready Unit" disabled if no spent Level I–II units under player control.

---

### Foresters — Level I, Armor 4, Cost 5 (Village)

| Ability | Effect | Phase |
| --- | --- | --- |
| Move 2 + terrain reduction | Move 2; forests, hills, swamps cost −1 (min 0) this turn | Movement |
| Block 3 | Block 3 | Combat Block |

The terrain reduction is a passive modifier for the remainder of the turn. Hex labels
update when the ability is activated (same as Frost Bridge).

---

### Utem Crossbowmen — Level II, Armor 4, Cost 6 (Village, Keep)

| Ability | Effect | Phase |
| --- | --- | --- |
| Attack or Block 3 | `choose_one`: Attack 3 or Block 3 | Combat |
| Ranged Attack 2 | Ranged Attack 2 | Combat Ranged or Melee |

---

### Utem Guardsmen — Level II, Armor 5, Cost 5 (Village, Keep)

| Ability | Effect | Notes |
| --- | --- | --- |
| Attack 2 | Attack 2 | — |
| Block 4 (Swiftness strip) | Block 4; blocked enemy loses Swiftness | See note |

**Swiftness strip:** after the block resolves, remove the `swift` ability from the blocked
enemy token for the rest of this combat. UI: resolved automatically after block confirms;
no player input required. If the enemy lacks Swiftness, the strip has no effect.

---

### Utem Swordsmen — Level II, Armor 4, Cost 6 (Keep)

| Ability | Effect | Notes |
| --- | --- | --- |
| Attack or Block 3 | `choose_one`: Attack 3 or Block 3 | — |
| Attack or Block 6 (self-wound) | `choose_one`: Attack 6 or Block 6; **this unit becomes Wounded** | See sequence |

#### Self-wound ability UI sequence

1. Player selects "Attack or Block 6" option
2. Confirm overlay: "This unit will become Wounded after use. Proceed?"
3. Player selects Attack or Block from the `choose_one` panel
4. Effect resolves; a Wound card is placed across the Utem Swordsmen token
5. Unit is now Wounded — cannot be activated again until healed

---

### Guardian Golems — Level II, Armor 3, Cost 7 (Keep, Mage Tower)

Passive resistance: Physical.

| Ability | Mana | Effect |
| --- | --- | --- |
| Attack or Block 2 | — | `choose_one`: Attack 2 or Block 2 |
| Fire Block 4 | Red | Fire Block 4 |
| Ice Block 4 | Blue | Ice Block 4 |

Three separate abilities shown in panel. Player chooses which ability to use (not all
three). Only one per activation.

---

### Illusionists — Level II, Armor 2, Cost 7 (Mage Tower, Monastery)

Passive resistance: Physical.

| Ability | Mana | Effect | Notes |
| --- | --- | --- | --- |
| Influence 4 | — | Influence 4 | — |
| Target unfortified enemy does not attack | White | Target enemy skips attack this combat | Condition: target_unfortified_only |
| Gain white crystal | — | White crystal to inventory | — |

#### Unfortified condition UI

1. Player selects the "does not attack" ability
2. Enters enemy targeting mode — map/enemy list displayed
3. Enemies at fortified sites are shown **disabled** (greyed out, not selectable)
4. Player taps an unfortified enemy → that enemy's attack is cancelled for this combat
5. If all visible enemies are fortified: ability is effectively unavailable (disabled in
   panel on activation, same as a phase-illegal card option)

---

### Red Cape Monks — Level II, Armor 4, Cost 7 (Monastery) — 1 copy

| Ability | Mana | Effect |
| --- | --- | --- |
| Attack or Block 3 | — | `choose_one`: Attack 3 or Block 3 |
| Fire Attack or Fire Block 4 | Red | `choose_one`: Fire Attack 4 or Fire Block 4 |

---

### Northern Monks — Level II, Armor 4, Cost 7 (Monastery) — 1 copy

| Ability | Mana | Effect |
| --- | --- | --- |
| Attack or Block 3 | — | `choose_one`: Attack 3 or Block 3 |
| Ice Attack or Ice Block 4 | Blue | `choose_one`: Ice Attack 4 or Ice Block 4 |

---

### Savage Monks — Level II, Armor 4, Cost 7 (Monastery) — 1 copy

| Ability | Mana | Effect |
| --- | --- | --- |
| Attack or Block 3 | — | `choose_one`: Attack 3 or Block 3 |
| Siege Attack 4 | Green | Siege Attack 4 |

---

### Fire Golems — Level III, Armor 4, Cost 8 (Keep, Mage Tower)

Passive resistances: Physical, Fire.

| Ability | Mana | Effect |
| --- | --- | --- |
| Attack or Block 3 | — | `choose_one`: Attack 3 or Block 3 |
| Ranged Fire Attack 4 | Red | Ranged Fire Attack 4 |

---

### Ice Golems — Level III, Armor 4, Cost 8 (Keep, Mage Tower)

Passive resistances: Physical, Ice.

| Ability | Mana | Effect |
| --- | --- | --- |
| Attack or Block 3 | — | `choose_one`: Attack 3 or Block 3 |
| Ice Attack 6 | Blue | Ice Attack 6 — Combat Melee only |

---

### Fire Mages — Level III, Armor 4, Cost 9 (Mage Tower, Monastery)

Passive resistance: Fire.

| Ability | Mana | Effect |
| --- | --- | --- |
| Ranged Fire Attack 3 | — | Ranged Fire Attack 3 |
| Fire Attack or Fire Block 6 | Red | `choose_one`: Fire Attack 6 or Fire Block 6 |
| Gain red mana + red crystal | — | 1 red mana token + 1 red crystal to inventory |

---

### Ice Mages — Level III, Armor 4, Cost 9 (Mage Tower, Monastery)

Passive resistance: Ice.

| Ability | Mana | Effect |
| --- | --- | --- |
| Ice Attack or Ice Block 4 | — | `choose_one`: Ice Attack 4 or Ice Block 4 |
| Siege Ice Attack 4 | Blue | Siege Ice Attack 4 |
| Gain blue mana + blue crystal | — | 1 blue mana token + 1 blue crystal to inventory |

---

### Amotep Gunners — Level III, Armor 6, Cost 8 (Keep, City)

| Ability | Mana | Effect |
| --- | --- | --- |
| Attack or Block 5 | — | `choose_one`: Attack 5 or Block 5 |
| Ranged Fire Attack 6 | Red | Ranged Fire Attack 6 |

---

### Amotep Freezers — Level III, Armor 6, Cost 8 (Keep, City)

| Ability | Mana | Effect |
| --- | --- | --- |
| Attack or Block 5 | — | `choose_one`: Attack 5 or Block 5 |
| Freeze (compound) | Blue | Target enemy does not attack this combat + Armor −3 (min 1) (Does not affect enemies with ice resistance) |

#### Freeze UI sequence

1. Player selects the Freeze ability and pays blue mana
2. Enemy targeting mode — player taps one enemy that does not have Ice Resistance
3. Two simultaneous effects resolve on confirm:

   - That enemy's attack is cancelled for this combat
   - That enemy's Armor is reduced by 3 (minimum 1) for the rest of this combat
4. UI updates: enemy attack indicator removed; Armor value shown with reduction

The Armor reduction stacks with other reductions (e.g., Chill, Tremor) and persists for
all remaining attack phases in this combat.

---

### Catapults — Level III, Armor 4, Cost 9 (Keep, City) — 3 copies

| Ability | Mana | Effect |
| --- | --- | --- |
| Siege Attack 3 | — | Siege Attack 3 |
| Siege Fire Attack 5 | Red | Siege Fire Attack 5 |
| Siege Ice Attack 5 | Blue | Siege Ice Attack 5 |

Player activates → 3-option panel; pay mana for options 2 or 3. Only one per activation.

---

### Altem Mages — Level IV, Armor 5, Cost 12 (City)

Passive resistances: Fire, Ice.

#### Ability 1 — Gain 2 mana tokens

Player gains 2 mana tokens, each of any basic color (independent picks).

UI: two sequential color pickers (red/blue/white/green). Second picker fires after first
is confirmed.

#### Ability 2 — Cold Fire Attack or Block 5 (optional bonus)

Base effect: `choose_one` — Cold Fire Attack 5 or Cold Fire Block 5.
Optional: pay red mana for +2; pay blue mana for +2. Each is independent; both can be
paid for a maximum of 9.

UI sequence:

1. Choose_one panel: **Cold Fire Attack 5** | **Cold Fire Block 5**
2. After choice: optional mana prompt — "Pay red mana? (+2)" **Yes** / **No**
3. Optional mana prompt — "Pay blue mana? (+2)" **Yes** / **No**
4. Displayed value updates as each bonus is confirmed: 5 → 7 → 9
5. Effect resolves with final value

#### Ability 3 — Global attack type override (Black mana)

Requires black mana (available during Night only per standard rules).

Choose_one — two mutually exclusive turn-scoped flags:

| Option | Effect |
| --- | --- |
| all_attacks_become_cold_fire | Every Attack played or generated this turn gains the Cold Fire element |
| all_attacks_become_siege | Every Attack played or generated this turn becomes a Siege Attack (in addition to other properties) |

UI sequence:

1. 2-option panel: **Cold Fire (all attacks)** | **Siege (all attacks)**
2. Player selects → turn-scoped flag is set immediately
3. HUD shows active flag for the remainder of the turn
4. All subsequent Attack resolutions check the flag and apply the override

These flags affect cards, other unit abilities, and skill attacks played later this turn.
They do not retroactively modify attacks already resolved before Altem Mages was activated.

---

### Altem Guardians — Level IV, Armor 7, Cost 11 (City) — 3 copies

| Ability | Mana | Effect | Notes |
| --- | --- | --- | --- |
| Attack 5 | — | Attack 5 | — |
| Block 6 (Swiftness strip) | — | Block 6; blocked enemy loses Swiftness | Same as Utem Guardsmen |
| All units gain resistances | Green | All friendly units gain Fire, Ice, Physical resistance this turn | See note |

**All-resistances ability:** Resolved immediately on activation. Every unit currently
under player control (including this one) gains `fire_resistance`, `ice_resistance`, and
`physical_resistance` as a turn-scoped buff. Units recruited after this point in the turn
also receive the buff (flag is turn-scoped, not point-in-time). No player input required.

---

## Skill Effects

### Skill Use Model

Skills come in three types:

| Type | Reset | Notes |
| --- | --- | --- |
| OncePerTurn | Never flipped — resolves immediately, available every turn | — |
| OncePerRound | Flip face-down on use; flips face-up at start of each Round | — |
| Persistent | Flip face-down on use; flips face-up at start of **your** next turn | Used mid-round for cross-turn effects |

**Solo v1 scope:** Each hero has 11 skill tokens. The skill marked `competitive: true`
(skill_10 for each hero) is excluded from the solo game. The skill marked `cooperative: true`
(skill_11) is included. 10 active skills per hero in solo v1.

**Skill activation:** The player taps a face-up Skill token in their play area. Unlike
cards, skills are not played from hand — they are always visible in the play area and do
not go to discard. The `OncePerRound` and `Persistent` skills visually flip face-down
after use to indicate they are spent.

---

### Tovak — The Barbarian

#### double_time (OncePerTurn)

Move 2 during Day; Move 1 at Night. One-tap. Day/Night check automatic.

#### night_sharpshooting (OncePerRound)

Ranged Attack 1 during Day; Ranged Attack 2 at Night. One-tap. Day/Night automatic.

#### cold_swordsmanship (OncePerTurn)

`choose_one`: Attack 2 or Ice Attack 2. 2-option panel; phase filter applies.

#### shield_mastery (OncePerTurn)

`choose_one`: Block 3, Ice Block 2, or Fire Block 2. 3-option panel; Combat Block phase
only. All three options are available within Block phase — no sub-filter needed.

#### resistance_break (OncePerTurn)

Effect: target one enemy — that enemy's Armor is reduced by 1 for each resistance it has
(minimum 1 total).

UI sequence:

1. Player taps Resistance Break
2. Enemy targeting mode — player selects one enemy
3. Engine counts that enemy's resistances (fire_resistance, ice_resistance,
   physical_resistance, etc.)
4. Armor reduction = resistance count; displayed as "[N] resistances → Armor −[N]"
5. Confirm → Armor reduction applied for the rest of this combat

If the enemy has 0 resistances, the ability has no effect (Armor −0). The option is
still selectable — it does not become disabled. Inform the player of the 0-reduction
outcome in the confirm overlay.

#### i_feel_no_pain (OncePerTurn)

Usable outside combat only. Effect: discard 1 Wound from hand → draw 1 card.

UI: one-tap if wounds are in hand; if exactly one wound, discard is automatic. If
multiple wounds: wound-selection prompt (single-select). After discard, draw 1 card.
Disabled in all combat phases.

#### i_dont_give_a_damn (OncePerTurn)

Effect: one card played sideways this turn gives +2 instead of +1. If that card is an
Advanced Action, Spell, or Artifact: +3 instead.

UI behavior:

- On skill activation: a "Sideways Boost active" indicator appears in the HUD
- The next card played sideways resolves with +2 (or +3 per type) instead of +1
- Flag clears after the first sideways card is played this turn
- The player chooses which sideways card benefits by the order in which they play cards

#### who_needs_magic (OncePerTurn)

Effect: one card played sideways this turn gives +2 instead of +1. If the player uses no
source die this turn, that card gives +3 instead.

UI behavior:

- Similar HUD indicator to "I Don't Give a Damn"
- The +3 vs. +2 is determined at end of turn, not at play time
- If player ultimately uses no source die: the sideways card that consumed the flag
  retroactively resolves as +3 and the delta (+1) is applied

Implementation note: either defer the resolution until end of turn, or apply +2 immediately
and add +1 if no die was used. The latter is simpler; both are correct.

#### motivation_tovak (OncePerRound)

Effect: flip to draw 2 cards. If the player has the least Fame (not tied), also gain 1
blue mana token.

Solo v1 note: in solo there is only one player, so the "least Fame" condition is vacuously
true — no other player can have less. The blue mana bonus always fires in solo v1.

#### mana_exploit (OncePerRound) — competitive skill, excluded from solo v1

#### mana_overload (OncePerRound) — cooperative skill, included in solo v1

Effect: choose a basic color → gain 1 mana token of that color. Put skill in center
marked with a token of that color. The first time any player (in solo: the only player)
uses mana of that color to power a card giving Move, Influence, Attack, or Block: that
card gets +4, and the skill returns to Tovak face-down.

Solo v1 behavior: the token sits in center for the rest of the turn. On the first card
play powered by the chosen color that produces Move/Influence/Attack/Block, +4 bonus
applies and skill returns face-down. If no such play occurs before turn ends: skill
returns at round start as normal.

---

### Arythea — The Black Witch

#### dark_paths (OncePerTurn)

Move 1 during Day; Move 2 at Night. One-tap.

#### burning_power (OncePerTurn)

`choose_one`: Siege Attack 1 or Fire Siege Attack 1. 2-option panel; Combat Ranged/Melee
only.

#### hot_swordsmanship (OncePerTurn)

`choose_one`: Attack 2 or Fire Attack 2. 2-option panel; phase filter applies.

#### dark_negotiation (OncePerTurn)

Influence 2 during Day; Influence 3 at Night. One-tap.

#### dark_fire_magic (OncePerRound)

Gain 1 red crystal to inventory + `choose_one`: 1 red mana token or 1 black mana token.

UI: 2-option panel for mana token color. Red crystal is granted automatically regardless
of choice. Black mana is not available during Day.

#### power_of_pain (OncePerTurn)

Effect: play one Wound card sideways this turn as if it were a non-Wound card, giving +2
(not +1). At end of turn, that Wound goes to discard pile (not thrown away, not removed
from game).

UI behavior:

- On skill activation: a "Pain Mode active" indicator in HUD
- The next Wound card played sideways resolves as +2 to the phase's sideways effect
- At end of turn: `end_of_turn_hook` fires — the Wound that was played sideways is
  moved to the discard pile instead of remaining in its normal destination
- If no Wound is played sideways before turn end: skill fires with no effect; no
  end-of-turn hook needed

Implementation note: wounds played sideways normally go to discard at end of turn anyway
(they cannot be discarded mid-turn per rules). The key effect here is the +2 vs. +1. The
end-of-turn destination is the same — the distinction is that the Wound is NOT thrown away.

#### invokation (OncePerTurn)

Effect: discard a Wound → gain 1 red or black mana token; OR discard a non-Wound card →
gain 1 white or green mana token. The token must be spent immediately.

UI sequence:

1. Player taps Invokation
2. Hand display — player selects one card to discard
3. Based on card type:

   - Wound: 2-option panel — **Red mana** | **Black mana**
   - Non-Wound: 2-option panel — **White mana** | **Green mana**
4. Token granted immediately
5. "Spend immediately" gate: the token is flagged as `must_spend_this_action`. If the
   player does not spend it before the next action resolves, it is lost.

Implementation note: "immediately" means the token must be used to power a card on the
same action resolution — not later in the turn. The simplest approach: the token is
placed in a temporary `immediate_pool` that is cleared at the end of the current card
play, before the next card play begins.

#### polarization (OncePerTurn)

Effect: one mana used this turn may be treated as its "opposite color":

- Green ↔ White
- Red ↔ Blue
- Black ↔ Gold

Day rules:

- Black used via Polarization counts as any color other than black (cannot power Spell
  strong effects that require black)

Night rules:

- Gold used via Polarization counts as black to power Spell strong effects only; not as
  any other color

UI behavior:

- On skill activation: HUD shows "Polarization active"
- When the player selects a mana source to power a card, the mana picker shows both the
  actual color and its opposite as options for each token/die
- The chosen substitution is applied to that single payment; subsequent payments in the
  same turn are normal (skill is OncePerTurn, not once per payment)

Implementation note: Polarization modifies the mana color at the point of payment — not
at the point of token gain. The token retains its actual color in the pool; the
conversion is applied during the "pay mana" step for one payment.

#### motivation_arythea (OncePerRound)

Same structure as Tovak Motivation; color is red.

#### healing_ritual (OncePerRound) — competitive skill, excluded from solo v1

#### ritual_of_pain (OncePerTurn) — cooperative skill, included in solo v1

Effect: throw away up to 2 Wound cards from hand. Put skill token in center. Any player
may return it (face-down) to play a Wound sideways as +3 instead of +1.

Solo v1 behavior: Arythea throws away up to 2 Wounds. Token sits in center for the rest
of the turn. Since there are no other players, the return trigger never fires — token
returns at round start. Effectively: the skill lets Arythea throw away up to 2 Wounds
per turn, at the cost of the token being unavailable for the rest of the round.

---

### Goldyx — The Wild Mage

#### freezing_power (OncePerTurn)

`choose_one`: Siege Attack 1 or Ice Siege Attack 1. 2-option panel; Combat Ranged/Melee.

#### potion_making (OncePerRound)

Heal 2. Adds 2 points to the heal pool. Disabled if no wounds in hand AND no wounded
controlled units.

#### white_crystal_craft (OncePerRound)

Gain 1 blue crystal to inventory + 1 white mana token. Both granted automatically; no choices.

#### green_crystal_craft (OncePerRound)

Gain 1 blue crystal to inventory + 1 green mana token. No choices.

#### red_crystal_craft (OncePerRound)

Gain 1 blue crystal to inventory + 1 red mana token. No choices.

#### glittering_fortune (OncePerTurn)

Effect: Influence 1 per distinct crystal color currently in inventory. Interaction phase
only.

UI: one-tap. Engine counts distinct colors in inventory (maximum 4). Influence is granted
immediately. "Cannot be used outside of interaction" — disabled in all non-Interaction
phases (greyed out, not hidden).

#### flight (OncePerRound)

Effect: move to any adjacent space for free (0 Move points), OR move 2 spaces for 2 Move
points. Movement does not provoke rampaging enemies.

UI sequence:

1. Player taps Flight
2. 2-option panel: **Move to adjacent space (free)** | **Move 2 spaces (2 Move)**
3. If free adjacent: hex targeting mode — only 6 adjacent hexes are valid targets
4. If 2-space: hex targeting mode — spaces exactly 2 away (by movement path, not straight
   line) are highlighted; player must have ≥ 2 Move points available
5. Confirm → teleport. Rampaging enemies on path are not triggered.

The "does not provoke" rule means Flight movement bypasses rampage checks entirely —
the engine should skip the "rampaging enemy encountered" check for this specific movement.

#### universal_power (OncePerTurn)

Effect: when playing one card sideways this turn, pay 1 mana of any basic color → that
card gives +3 instead of +1 (or +4 if it is an Action or Spell card of the matching
color).

UI sequence:

1. Player taps Universal Power — HUD indicator: "Universal Power active"
2. Player plays a card sideways in the normal way
3. At the sideways play point: a mana color picker injects into the flow — "Pay 1 mana
   to boost this sideways play?" with **Yes** / **No**
4. If Yes: color picker (basic colors only); player selects → mana paid
5. Effect value determined:
   - Card color matches paid mana: +4
   - Card color does not match paid mana: +3
   - No mana paid (No): +1 (normal sideways)
6. Flag clears after this sideways play; remaining sideways plays this turn are normal

#### motivation_goldyx (OncePerRound)

Same structure; color is green.

#### source_freeze (OncePerRound) — competitive skill, excluded from solo v1

#### source_opening (OncePerRound) — cooperative skill, included in solo v1

Effect: put skill in center. Goldyx may reroll one source die. Any player (in solo: only
Goldyx) may return the token face-down to use an extra die of a basic color from the
Source and give Goldyx a crystal of that color. That player decides whether to reroll
the die at end of their turn.

Solo v1 behavior: Goldyx places the token in center after rerolling a source die. On the
same or next action, Goldyx may return the token to themselves to use 1 extra source die
and gain a crystal of its color. The token returns face-down (OncePerRound, so not
available again until next round start).

---

### Norowas — The Noble Elf

#### forward_march (OncePerTurn)

Effect: Move 1 per Ready and Unwounded Unit under player control, maximum Move 3.

UI: one-tap. Engine counts Ready + Unwounded units (Command token above, no Wound
card across). Move granted: min(unit_count, 3). If no eligible units: Move 0 (skill
still activates; does not become disabled for 0-move result).

#### day_sharpshooting (OncePerTurn)

Ranged Attack 2 during Day; Ranged Attack 1 at Night. One-tap.

#### inspiration (OncePerRound)

`choose_one`: Ready a unit OR heal a unit (remove 1 wound from a wounded unit).

UI: 2-option panel. If no wounded units: "Heal" shown disabled; "Ready" is the only
option (Wounded ≠ same as Spent — a Wounded unit may be Ready, Spent-and-Wounded, etc.).
If no Spent units: "Ready" disabled. If no eligible units for either: skill cannot be
activated.

#### bright_negotiation (OncePerTurn)

Influence 3 during Day; Influence 2 at Night. One-tap.

#### leaves_in_the_wind (OncePerRound)

Gain 1 green crystal to inventory + 1 white mana token. No choices.

#### whisper_in_the_treetops (OncePerRound)

Gain 1 white crystal to inventory + 1 green mana token. No choices.

#### leadership (OncePerTurn)

Effect: when activating a unit this turn, add one of: +3 Block, +2 Attack, or +1 Ranged
Attack to that unit's ability value, regardless of element.

UI behavior:

- On skill activation: HUD indicator "Leadership active"
- When the player next activates a unit: a bonus panel injects before the ability panel:

  **+3 Block** | **+2 Attack** | **+1 Ranged Attack**
- Player selects one bonus; it applies to whichever ability the unit uses this activation
- Bonus stacks with the unit's base value (e.g. Utem Crossbowmen Ranged Attack 2 + 1 = 3)
- The bonus is applied to the effective value, not to specific elements — "+2 Attack" on
  a Fire Attack ability gives Fire Attack at +2

#### bonds_of_loyalty (Persistent)

Effect: when gained, add 2 Regular Units to the Unit offer. Place this Skill token in
the Unit area as a Command token. A unit recruited under this Command token costs 5 less
Influence (minimum 0). That unit can be activated even when unit use would normally be
prohibited. It cannot be disbanded.

This is a structural persistent skill — it modifies the player's Command limit and
recruitment cost, not a triggered effect.

Implementation notes:

- On skill gain: immediately add 2 silver-back units to the Unit offer (same logic as
  round-start deal)
- The Bonds of Loyalty token in the Unit area functions exactly as a regular Command
  token for purposes of unit capacity
- The discount (−5 Influence) applies only to the unit slot under this specific token;
  other unit slots are unaffected
- "Cannot be disbanded": the unit occupying this slot cannot be removed by any effect
  that says "disband a unit" — the slot is locked
- "Even when unit use would normally be prohibited": this clause IS relevant in solo v1.
  Dungeons, tombs, and monastery burn fights all prohibit unit use (other units refuse
  to participate). The Bonds of Loyalty unit can be activated in these combats regardless.

#### motivation_norowas (OncePerRound)

Same structure; color is white.

#### prayer_of_weather (OncePerRound) — competitive skill, excluded from solo v1

#### calming_the_weather (OncePerRound) — cooperative skill, included in solo v1

Effect: reduce Move costs of all terrain by 2 (min 1) for Norowas this turn. Put token
in center. Any player may return it face-down to reduce their terrain costs by 1 (min 1)
on their turn.

Solo v1 behavior: Norowas gains the terrain reduction. Token sits in center; no other
players to return it. Token returns at round start. Effective result: terrain cost −2
(min 1) for this turn.

---

## Open Questions — Effects

Items awaiting input before implementation:


- **Altem Mages ability 3 — timing of global flag:** Confirm that the
  `all_attacks_become_cold_fire` / `all_attacks_become_siege` flag applies to all
  Attack-producing effects (cards, other unit abilities, skills) played after activation
  this turn, not retroactively.

- **Universal Power — "same color" check:** Card color is determined by the mana
  required for its powered effect (same rule as Action cards). Confirm this is the right
  color reference for the +4 threshold, not the card's art color or type.

## Resolved Questions — Effects

(card-effect resolved questions carried over from the card section above)

- **Motivation — solo Fame condition:** The solo player always has the least Fame (no
  other player exists to compare against), so the condition is vacuously true. The mana
  bonus always fires in solo v1 for all four heroes. ✅
- **Bonds of Loyalty — unit use in solo v1:** The "even when prohibited" clause IS
  relevant in solo v1. Dungeons, tombs, and monastery burn fights all prohibit unit use;
  the Bonds of Loyalty unit may be activated in these combats regardless. ✅
