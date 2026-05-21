# Hex/Movement LLD — Magus Warrior

## 1. Overview

This document specifies all movement-phase rules for the Godot/C# implementation: terrain costs, move point generation and spending, tile reveal, rampaging enemy provocation, site entry, safe spaces, forced withdrawal, and the portal space.

**Cross-references:**
- Rampaging enemy stats and site associations: `enemy-token-lld.md`
- Combat triggered by movement (assaults, provocation): `combat-flow-lld.md`
- What happens once the hero declares a site action: `site-interaction-lld.md`

**Solo v1 scope:** All rules in this document apply as written. Player vs. Player movement interactions (occupying another hero's space, keep owned by opponent) are not in scope but noted where the rule text references them.

---

## 2. Terrain Types and Move Costs

Move costs are displayed on the Day/Night board. The hero pays the cost of the **destination** space when entering it.

| Terrain | Day Cost | Night Cost | Notes |
|---------|:--------:|:----------:|-------|
| Plains | 2 | 2 | |
| Hills | 3 | 3 | |
| Forest | 3 | 5 | Night cost shown as small blue number on board |
| Desert | 5 | 3 | Day cost shown as small blue number on board |
| Swamp | 5 | 5 | |
| Wasteland | 4 | 4 | |
| Mountain | — | — | Impassable under all conditions |
| Lake | — | — | Impassable under all conditions |
| City space | 2 | 2 | Always 2, regardless of underlying terrain |

**Rules:**
- Mountain and Lake hexes are impassable (marked X on board). Special movement effects can allow entry; see Section 4.3.
- City spaces always cost 2 regardless of the terrain art on the hex.
- Forest and Desert are the only terrains whose cost changes between Day and Night. They change in opposite directions: Forest is harder at Night; Desert is harder during the Day.
- Move cost cannot be reduced below 0 by any cost-reduction effect.

---

## 3. Generating Move Points

Move points are accumulated during the movement phase and spent space by space.

**Sources:**
- Action cards (Basic or Advanced) with the Move icon, played from hand.
- Any non-Wound card played sideways → Move 1.
- Hero skill tokens with a movement ability.
- Unit abilities with movement (e.g., Peasants: Move 2).

**Timing:**
- Cards can be played at any point during movement, including after revealing a new tile.
- Cards already played cannot be powered with mana retroactively; mana must be committed when the card is played.
- Special (✦) and healing effects can be played during movement. Influence effects cannot.

---

## 4. Moving the Figure

### 4.1 General Rules

1. Movement is optional on a Regular turn. Resting heroes cannot move.
2. All movement must be completed before taking any action (combat, interaction, exploration declaration).
3. The hero may move as many spaces as they have Move points for, in any combination of directions.
4. Each space entered costs Move points equal to the terrain cost of the destination hex.
5. The hero may only enter accessible adjacent hexes. Impassable terrain (mountain, lake, unplaced tiles) cannot be entered normally.
6. If a terrain's Move cost is reduced to 0 by an effect, the hero may enter those hexes without spending Move points.
7. The hero may pass through spaces occupied by another player's figure freely.
8. Unused Move points are lost at the moment the action phase begins, **unless** a specific card or ability explicitly allows them to carry into combat (e.g., the Cumbersome enemy ability lets the hero spend Move points during the Block phase to reduce the enemy's attack).

### 4.2 Mandatory Actions Triggered by Entering a Space

**Entering** certain spaces immediately ends movement regardless of Move points remaining:

- **Unconquered fortified site (keep, mage tower, city):** Entering the space immediately ends movement and triggers a mandatory assault. Any remaining Move points are lost. The hero cannot pass through a fortified site.
- **Rampaging enemy provocation:** See Section 6. Moving from one space adjacent to a rampaging enemy to another adjacent space of the same token immediately ends movement.

### 4.3 Special Movement Effects

Some effects allow the hero to move over or into normally inaccessible spaces (e.g., lakes, mountains, fortified sites, spaces occupied by rampaging enemies):
- The hero does not pay normal terrain costs for spaces traversed this way — only what the effect specifies.
- If the effect says "end this move on a safe space," Forced Withdrawal applies if they do not (see Section 9).
- Moving over a rampaging enemy's space via a special effect does NOT provoke it.

---

## 5. Tile Reveal (Exploration)

Revealing new tiles happens during the movement phase. It is not an action.

### 5.1 When Reveal Is Available

- The hero must be on a space adjacent to at least one empty table position.
- That empty position must not be behind the extended coastline of the starting tile.
- The hero cannot reveal tiles after declaring an action.
- Resting heroes cannot reveal tiles.

### 5.2 Cost

Revealing a tile costs **2 Move points**, paid at the moment of reveal. Move points from effects played after the reveal are added to any leftover points and can be used to continue moving on the newly revealed tile.

### 5.3 If Two Spaces Can Be Explored

When the hero's position borders two different empty spaces, they must announce which space they are exploring before drawing and placing the tile.

### 5.4 Tile Placement Rules

- Tiles are placed in a fixed orientation: the tile number corner must face the same direction as the matching symbol on the starting tile.
- **Countryside tiles (green back):** Must be adjacent to at least two already-placed tiles, or adjacent to a tile that itself borders at least two tiles.
- **Core tiles (brown back):** Must be adjacent to at least two already-placed tiles.
- No tile can be placed behind the coastline.

### 5.5 Tile Deck Exhausted

1. Use a random Countryside tile removed from the game during setup.
2. If all Countryside tiles are placed, use non-City Core tiles instead.
3. Replacement tiles may only be placed adjacent to at least three other tiles (filling holes).

### 5.6 Events on Reveal

When a tile is revealed, check the Site Description card for each site on that tile before continuing play.

**Monastery revealed:** Draw one Advanced Action card and add it to the **Unit offer** (not the Advanced Action offer).

**City revealed:**
1. Take the corresponding City card; place it near the map.
2. Check the scenario description for the required city level and set the City figure accordingly.
3. For each circle on the base of the City figure, draw one enemy token of the corresponding color face-down and place it on the City card.

Note: Steps 2–3 (garrison setup) are scenario-dependent. In First Recon, the city cannot be entered or conquered — treat the city space as inaccessible and skip garrison setup.

**All other sites:** Follow the "When Revealed" section of the corresponding Site Description card.

---

## 6. Rampaging Enemy Provocation

Rampaging enemies (Orc Marauder and Draconum) restrict and react to hero movement. Their stats, combat abilities, and site associations are in `enemy-token-lld.md`.

### 6.1 Cannot Enter Their Space

A hero cannot enter a space occupied by an undefeated rampaging enemy token under normal movement. The space is treated as impassable.

**Exception:** A special movement effect that allows moving over inaccessible spaces also allows moving over a rampaging enemy's space without provoking it.

### 6.2 Provocation by Lateral Movement

**Definition:** A hero provokes a rampaging enemy by moving directly from a space adjacent to that enemy to another space adjacent to the **same** rampaging enemy token.

**Effect:** The provoked enemy immediately attacks the hero. Movement ends. Any remaining Move points are lost. The hero must resolve this as a mandatory combat action.

**Not provoked:**
- Moving away from an adjacent space to a non-adjacent space does not provoke.
- Moving to a space adjacent to an enemy from a non-adjacent space does not provoke.
- Only the single move step that crosses from one adjacent space to another adjacent space of the same token triggers provocation.

### 6.3 Challenging Rampaging Enemies Voluntarily

When standing adjacent to a rampaging enemy, the hero may voluntarily challenge it as their action for the turn (after movement ends). The hero may challenge one or more adjacent rampaging enemies in a single combat.

### 6.4 Rampaging Enemies During a Fortified Site Assault

When the hero assaults a fortified site, rampaging enemies on spaces adjacent to the site may be challenged and added to that combat. These rampaging enemies:
- Are not fortified (even though the site is).
- Do not need to be defeated to conquer the site.
- Join the combat alongside the garrisoned defenders.

---

## 7. Site Entry Rules

### 7.1 Fortified Sites — Keep, Mage Tower, City (Unconquered)

**Entering** a space with an unconquered fortified site immediately ends movement. The hero cannot pass through a fortified site.

- The hero loses **1 Reputation** immediately upon entering, regardless of combat outcome.
- The hero must fight **all garrisoned enemies** at that site.
- Siege attacks are the only ranged-phase attacks available (because the site is fortified).
- A keep owned by another player also ends movement immediately and counts as an attack on that player (not in scope for solo v1).

### 7.2 Adventure Sites — Ruins, Dungeon, Tomb, Monster Den, Spawning Grounds

Entering a space with an adventure site does **not** end movement. The hero may:

- Ignore the site entirely and continue moving (or stop and do nothing).
- Stop and **declare exploration** as their action for the turn.

Once exploration is declared, the hero cannot move further or reveal more tiles. Combat follows from the site's enemy tokens.

### 7.3 Monastery

- Entering a monastery space does not end movement.
- The hero may choose one action at this space:
  - **Interact:** Spend Influence for Healing and Advanced Action card access.
  - **Burn:** Costs −3 Reputation; draws a random violet enemy as defender; reward is one Artifact.

### 7.4 Inhabited Sites — Village, Conquered Keep, Conquered Mage Tower

- Entering these spaces does not end movement.
- The hero may interact as their action (recruit units, buy Healing, buy Spells or Advanced Actions depending on site type).

---

## 8. Safe Spaces

A **safe space** is required by Forced Withdrawal (Section 9). A space qualifies as safe if **all** of the following are true:

1. It is accessible under **normal movement conditions** — no special effects are required to enter it.
2. It is **not** an unconquered fortified site (keep, mage tower, or city).
3. It does **not** contain another hero's figure, except at sites that explicitly allow multiple heroes (portal space, conquered city).
4. It does **not** contain a rampaging enemy token.

**Safe space examples:** Plains, hills, forest, desert, swamp, wasteland hexes (if accessible and no rampaging enemy); villages; crystal mines; magical glades; conquered keeps; conquered mage towers; conquered cities.

**Not safe:** Mountain and lake hexes (impassable); unconquered fortified sites; any space containing a rampaging enemy; any space entered only via a special movement effect that bypasses normal accessibility.

**Solo v1 note:** The "no other hero" condition is vacuously satisfied everywhere except the portal and conquered city (which are listed exceptions anyway). No special handling needed.

---

## 9. Forced Withdrawal

**When it applies:** At the end of the hero's turn (during the End of Turn sequence), if the hero is not on a safe space.

### 9.1 Procedure

1. Trace back through the spaces the hero moved through this turn.
2. The hero backtracks until they reach the nearest safe space visited this turn.
3. For **each space backtracked**, the hero gains one Wound card into their hand.
4. The hero's turn ends on that safe space.

### 9.2 This Is Not Forced Withdrawal

- **Failed fortified site assault:** The hero withdraws to the space they attacked from. This does not count as Forced Withdrawal and causes no Wounds. However, if the space withdrawn to is not a safe space, Forced Withdrawal then applies to that position.
- **Player vs. Player retreat:** Not in scope for solo v1.

### 9.3 When This Triggers in Practice

In normal play the hero controls where they stop, so Forced Withdrawal is rare. It becomes relevant when a special movement effect places the hero on an impassable space (e.g., a mountain or lake hex) without requiring them to end on a safe space.

---

## 10. Portal Space

The portal space is on the starting tile and follows special rules distinct from all other spaces.

### 10.1 Removal at Turn End

If the hero ends their turn on the portal space for any reason (including their very first turn), the hero's figure is **removed from the map** and placed face-up in front of the player.

### 10.2 Return at Turn Start

At the **start of the hero's next turn**, before any other action, the figure is returned to the portal space. Movement begins from there as normal.

### 10.3 Multiple Heroes at the Portal

- Any number of heroes can occupy the portal space simultaneously.
- All figures at the portal are removed from the map; they cannot attack each other.
- Combat never occurs on the portal space.

### 10.4 Safe Space Status

The portal space is a safe space (accessible under normal conditions; not a fortified site; no rampaging enemy). A hero forced to backtrack to the portal follows the normal portal removal rule at turn end — this is not a Wound-causing backtrack step.

---

## 11. Night Movement

### 11.1 Terrain Cost Changes

| Terrain | Day Cost | Night Cost |
|---------|:--------:|:----------:|
| Forest | 3 | **5** |
| Desert | **5** | 3 |

All other terrains cost the same regardless of Day or Night.

### 11.2 Mana

- **Gold mana** acts as any basic color during Day. At Night it is completely unavailable — gold dice in the Source are set aside as depleted. Gold mana produced by any other means is also unusable at Night.
- **Black mana** becomes available at Night. It can power the strong effects of Spell cards. Unlike gold mana during the Day, black mana is **not** a wildcard — it does not substitute for other colors.

### 11.3 Garrison and Site Visibility

**Keeps and Mage Towers:**
- During Day: when a hero moves to a space adjacent to a keep or mage tower, the garrison token is revealed face-up. It remains face-up even after Night begins.
- During Night: garrison tokens that have not yet been revealed remain face-down. They are not revealed until a hero assaults the site.

**Cities:**
- Cities never sleep — their garrison is always revealed when a hero is adjacent, even at Night.

**Ruins:**
- During Day: ruins tokens are face-up and visible from anywhere on the map.
- During Night: ruins tokens are placed face-down. They remain face-down until a hero moves onto the ruins space, or until the next Day Round begins. The hero may still choose whether to enter or ignore the site after the token is revealed.

**Rampaging enemies** remain visible at all times, Day and Night.

---

## Open Questions

None at time of writing.

## Resolved Questions

**Q: Do Move points from cards played before a tile reveal carry over into movement on the newly revealed tile?**
A: Yes. Leftover Move points carry forward. Additional cards may be played after the reveal to add more. (Rulebook §6b)

**Q: Does provocation trigger if the hero moves away from an enemy and later in the same turn arrives adjacent to the same enemy again?**
A: No. Provocation requires a single move step that goes from one adjacent space to another adjacent space of the same token. Movement earlier in the turn to a non-adjacent space breaks the chain.

**Q: Is the portal space a safe space?**
A: Yes. It is accessible under normal conditions, not a fortified site, and in solo v1 there is no other hero. The figure removal at turn end is the portal rule, not Forced Withdrawal, and causes no Wounds.

**Q: Can the hero explore a tile and then continue moving onto that tile in the same turn?**
A: Yes. Tile exploration is part of movement. After paying 2 Move points to reveal, remaining Move points can be spent to enter spaces on the newly revealed tile. (Rulebook §6a)

**Q: Does entering a fortified site immediately end movement, or only when the hero chooses to stop there?**
A: Entry immediately ends movement, regardless of remaining Move points. The hero cannot pass through a fortified site. (Rulebook §4a)

**Q: Are unused Move points always lost when the action phase begins?**
A: Not always. The general rule is yes, but certain effects explicitly allow Move points to carry into combat (e.g., Cumbersome enemy ability lets the hero spend Move points during the Block phase to reduce the enemy's attack value).

**Q: Do cities reveal their garrison at Night when a hero is adjacent?**
A: Yes. Cities always reveal their garrison when adjacent, even at Night. Keeps and mage towers do not — their garrison stays hidden at Night until the site is assaulted. (Walkthrough §Night Rules — Visibility)
