# UX Interactions — Hero-Specific Starting Cards

Novel UX interaction patterns introduced by the 8 hero-unique starting action cards
(instinct, cold_toughness, battle_versatility, mana_pull, will_focus, crystal_joy,
noble_manners, rejuvenate). These are above and beyond the patterns already covered by
the 14 shared base-deck cards.

---

## New Systems Required

### 1. Dynamic Block Value — `ice_block_scaling` (Cold Toughness powered)

**What it is:** Ice Block value is not fixed at play time. It is calculated from the
blocked enemy's token abilities at resolution time.

**UX behavior:**
- When the player selects Cold Toughness powered as their block and then targets an enemy,
  the displayed Ice Block value updates in real time based on that enemy's token properties.
- Each offensive ability on the token adds to the total (cold_fire: +2, physical: +0,
  all others: +1). Only abilities printed on the token count — site-based fortification
  does not.
- Optionally display a breakdown tooltip so the player can see which properties contributed.

**Implementation note:** Requires the card resolution system to query enemy token data
during the block-targeting step, before finalizing the block value.

---

### 2. Conditional Source Die Color Override — `black_source_die_as_any_color` (Mana Pull unpowered)

**What it is:** The standard source die flow uses the die color as-is. Mana Pull intercepts:
if the extra die shows black, the player gets a color picker instead of being locked to black.

**UX behavior:**
- When a player uses the extra source die granted by Mana Pull and the die shows black,
  inject a color selection step (any basic color) before the die is applied.
- If the die shows any other color, proceed with the normal source die flow.
- This is the primary reason Mana Pull is stronger during Day: black dice are normally
  unusable (depleted) during Day, so this card converts an otherwise wasted die.

**Implementation note:** The source die flow needs a hook for "color override on black"
separate from the general source die use path.

---

### 3. End-of-Turn Optional Discard-to-Retrieve — `end_of_turn_return` (Crystal Joy)

**What it is:** At end of turn, Crystal Joy prompts the player to optionally discard a card
from hand to return Crystal Joy to hand (rather than to the discard pile).

**UX behavior:**
- At end of turn, if Crystal Joy was played this turn, show an optional prompt.
- Basic (unpowered): player may discard one non-wound card → Crystal Joy returns to hand.
- Powered: player may discard any card, including a Wound → Crystal Joy returns to hand.
- If the player declines, Crystal Joy goes to the discard pile normally.
- The hand display during this prompt should visually indicate ineligible cards
  (wounds are greyed out for the unpowered version).

**Implementation note:** Requires a new end-of-turn hook phase. Crystal Joy must track
whether it was played this turn and which effect (basic/powered) was used, to determine
the correct discard filter.

---

### 4. Unit Selection Panel with Level Filter — `ready_unit` (Rejuvenate)

**What it is:** Choosing the "ready a unit" option from Rejuvenate requires the player
to select one of their wounded units, filtered by level.

**UX behavior:**
- When the player selects the `ready_unit` option, show a panel of the player's wounded
  units.
- Unpowered: only units at level I or II are selectable; higher-level units are shown
  but disabled.
- Powered: units at level I, II, or III are selectable.
- Confirm selection removes one wound token from that unit.

**Implementation note:** Requires a unit picker component that accepts a `max_level`
filter parameter, reusable for any future card or effect that targets units by level.

---

## Wider Variants of Existing Patterns

### 5. Large `choose_one` Panels (Instinct, Rejuvenate, Battle Versatility)

The choose_one UI already exists for 2-option cards. These cards require:
- 4-option layout: Instinct (both effects), Rejuvenate unpowered
- 6-option layout: Battle Versatility powered

The layout must remain readable at 6 options without horizontal overflow or illegible
button labels. Consider a 2-column grid for 5+ options.

---

## Conditional Display on Existing Effect Types

### 6. Context-Aware Bonus Badges — `fame_if_interaction`, `reputation_if_interaction` (Noble Manners)

Noble Manners grants bonus fame and reputation only when played during an Interaction
(city, village, monastery, etc.).

**UX behavior:**
- The card face shows the bonus values (+1 Fame, +1 Rep) in a distinct badge.
- During non-interaction phases, these badges are visually dimmed/inactive.
- During an active interaction, they activate and the values are automatically applied
  on play — no additional player action required.

---

## Source Dice General Rule (applies to all source die cards)

Mana dice taken from the Source must be used immediately to power a card, unit, or skill.
They do not generate tokens and cannot be saved. The UX must not allow a player to "hold"
a source die between actions.

**Exceptions** (cards that explicitly convert a source die to a token):
- Mana Pull powered
- Mana Draw powered

For these cards, the token is generated immediately on die selection, and the token
(not the die) persists for later use in the turn.
