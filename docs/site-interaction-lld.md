# Site Definition LLD — Magus Warrior

Interaction model, data types, state machine, and UI flows for all site types on the world
map. Read alongside `docs/hex-movement-lld.md` (movement triggers and assault entry),
`docs/combat-flow-lld.md` (garrison and adventure combat), `docs/effect-lld.md` (card
effects during interaction), `docs/turn-structure-lld.md` (passive benefit timing), and
`_bmad-output/game-architecture.md` (screen contracts).

**Solo v1 scope:** Cooperative city assault and PvP rules are out of scope. All rules apply
as written for solo play.

---

## 1. Overview

Sites are per-hex game objects that offer interaction, combat, or passive benefits.

| Category | Sites | Interaction |
| --- | --- | --- |
| Passive | Crystal Mine, Magical Glade | Automatic benefit; no action cost |
| Inhabited | Village, Monastery | Interaction action; spend Influence |
| Fortified | Keep, Mage Tower, City | Entering unconquered = mandatory assault |
| Adventure | Monster Den, Spawning Grounds, Dungeon, Tomb, Ancient Ruins | Enter as action; optional |
| Rampaging | Orc Marauder, Draconum | Hex occupants; block movement; challenge combat |

Rampaging enemies occupy hexes and constrain movement but are not true sites. They are
covered in Section 10.

---

## 2. Terminology

| Term | Meaning |
| --- | --- |
| SiteDefinition | Static, per-hex data loaded from tile data. Immutable after tile reveal. |
| SiteState | Mutable runtime state: revealed, conquered, current enemies, Shield tokens. |
| SiteInteraction | The Interaction action for a hero turn. Active during `GamePhase.Interaction`. |
| Garrison | Enemy token(s) on a fortified site. May be face-down until approached. |
| Assault | Mandatory combat triggered by entering an unconquered fortified site. |
| Conquest | Hero defeated all enemies at a site and placed a Shield token there. |
| Shield token | A player's marker on a conquered site; used for ownership and end-game scoring. |
| PendingReward | A reward earned during combat, deferred to End of Turn resolution. |
| Night rules | No gold mana; black mana may power strong Spell effects. Applied unconditionally to Dungeon and Tomb regardless of current round Day/Night state. |
| Fortified (site) | All garrison enemies at a fortified site are treated as fortified: only Siege Attacks can target them in the Ranged/Siege phase; Ranged Attacks cannot. |
| Fortified twice | A garrison enemy that also has the Fortified ability token. Even Siege Attacks cannot target it in the Ranged/Siege phase. |

---

## 3. Site Taxonomy

```csharp
public enum SiteType {
    CrystalMine, MagicalGlade,
    Village, Monastery,
    Keep, MageTower, City,
    MonsterDen, SpawningGrounds, Dungeon, Tomb, AncientRuins
}

public enum CityColor { Red, Blue, White, Green }

public enum RuinsAdventureType {
    Unknown,              // face-down at Night; type not yet revealed
    AncientAltar,         // pay mana → Fame
    EnemiesWithTreasure   // fight tokens → claim reward
}
```

Rampaging enemy hex occupants (Orc Marauder, Draconum) are `EnemyTokenInstance` values in
`HexState.RampagingEnemies`, not `SiteDefinition` entries. See Section 10.

---

## 4. Data Model

All types live in `scripts/map/` unless noted.

### 4.1 SiteDefinition (static, sealed hierarchy)

Most site types carry no extra static data. Use the abstract base directly for those; use
concrete subtypes only where extra fields are needed.

```csharp
public abstract record SiteDefinition(SiteType Type);

// Crystal mines each have their own color
public sealed record CrystalMineSiteDefinition(ManaColor MineColor)
    : SiteDefinition(SiteType.CrystalMine);

// City sites reference which city they represent; combat data comes from data/cities.yaml
public sealed record CitySiteDefinition(CityColor CityColor)
    : SiteDefinition(SiteType.City);

// All other site types carry no additional static data — use SiteDefinition directly
// Example: new SiteDefinition(SiteType.Village)
```

### 4.2 SiteState (mutable)

```csharp
public class SiteState {
    public SiteType          Type;
    public bool              IsRevealed;
    public bool              IsBurned;             // Monastery only
    public List<string>      ConquerorShields;     // player IDs with Shield tokens here
    public List<EnemyTokenInstance> Enemies;       // current garrison or adventure enemies
    public bool              GarrisonFaceDown;     // fortified sites: true until Day approach
    public RuinsSiteState?   Ruins;               // non-null only for AncientRuins
    public CityCardState?    CityCard;            // non-null only for City
}
```

### 4.3 RuinsSiteState

```csharp
public class RuinsSiteState {
    public RuinsAdventureType AdventureType;
    public ManaColor?         AltarColor;      // AncientAltar: required mana color
    public int                AltarFameReward; // AncientAltar: always 7 in base game
    public List<EnemyTokenInstance> Enemies;   // EnemiesWithTreasure: remaining enemies
    public SiteRewardSpec?    Reward;          // EnemiesWithTreasure: reward when last enemy falls
}
```

### 4.4 CityCardState

Enemy composition (how many of which pile color) is loaded from `data/cities.yaml` when
the city tile is revealed — not hardcoded in the engine. The City Card UI is designed per
the screen contracts; the data representation here supports that without prescribing the
visual layout.

```csharp
public class CityCardState {
    public CityColor Color;
    // Loaded from data/cities.yaml at reveal. Keyed by enemy pile color; value = count.
    public Dictionary<EnemyPileColor, int> EnemyComposition;
    // Enemies instantiated from EnemyComposition and placed here face-down at reveal.
    public List<EnemyTokenInstance> Enemies;
    // One Shield token placed here per enemy defeated during any assault on this city.
    public Dictionary<string, int> ShieldsPerPlayer; // player id → count
    // Leader = player with most Shields; first-placed wins ties.
    public string? LeaderPlayerId;
}
```

### 4.5 SiteRewardSpec

```csharp
public record SiteRewardSpec(SiteRewardType Type);

public enum SiteRewardType {
    TwoCrystals,        // MonsterDen: roll 2 dice
    ThreeCrystalsAndArtifact, // SpawningGrounds: roll 3 crystal dice + 1 Artifact
    SpellOrArtifact,    // Dungeon: roll die → gold/black = Spell, else Artifact
    ArtifactAndSpell,   // Tomb: 1 Artifact AND 1 Spell (both)
    Artifact,           // Monastery burn; or one of AncientRuins EnemiesWithTreasure rewards
    Spell,              // MageTower conquest; or one of AncientRuins rewards
    FourCrystals,       // AncientRuins: one of each basic color
    FreeUnit,           // AncientRuins: recruit any unit from offer at no Influence cost
    AdvancedAction,     // AncientRuins: take from AA offer (offer replenished)
    AltarFame,          // AncientAltar: Fame amount stored in RuinsSiteState.AltarFameReward
}
```

---

## 5. When Revealed

When a tile is revealed during movement, `TileRevealHandler` reads each hex's site
definition and calls the appropriate `OnReveal` handler.

| Site | On Reveal |
| --- | --- |
| CrystalMine | No action. Mine color visible from tile art. |
| MagicalGlade | No action. |
| Village | No action. |
| Monastery | Draw 1 card from Advanced Action deck → add to Unit offer (not the AA offer). |
| Keep | Place one gray enemy token face-down on the hex. Reveal face-up when a player moves adjacent during a Day Round; remains face-down at Night until assaulted. |
| MageTower | Place one violet enemy token face-down. Same visibility rules as Keep. Set up Spell offer if not yet prepared: flip top 3 Spell deck cards face-up into the Spell offer column. |
| City | Load `CityCardState` from `data/cities.yaml` for this `CityColor`. Instantiate enemy tokens from `EnemyComposition`; place face-down on the City Card. Place City Figure on the hex. |
| MonsterDen | No action. |
| SpawningGrounds | No action. |
| Dungeon | No action. |
| Tomb | No action. |
| AncientRuins | Draw the yellow Ruins token. Day: place face-up (adventure type visible). Night: place face-down; flips face-up when a Day Round begins or a player enters the hex. |

---

## 6. Passive Sites

Passive sites grant benefits automatically. The hero's action for the turn is not consumed.
Passive benefits fire on both Regular and Rest turns. They do NOT fire if the hero forfeits
their turn (announces End of Round without taking a turn).

Timing (documented in `turn-structure-lld.md`):
- **Imbued with Magic (Magical Glade)** → fires at **Turn Start** (§7 Step 2).
- **Crystal Mine benefit** and **Healing Essence (Magical Glade)** → fire at **End of
  Turn** (§9 Step 4).

### 6.1 Crystal Mine

**End-of-turn benefit:** Gain 1 crystal of the mine's color.

**Cap rule:** If the hero already holds 3 crystals of that color, nothing is gained — no
substitute token is granted. (Walkthrough p13: turn-structure-lld.md §9 Step 4 also
encodes this.)

**Timing note:** Crystal is gained after the hero announces turn end; it cannot be used as
mana this turn.

UI: Notification — "Crystal Mine: gained [color] crystal." No input needed.
If at cap: "Crystal Mine: inventory full, no crystal gained."

### 6.2 Magical Glade

**Imbued with Magic (Turn Start):**

Gain 1 mana token based on current round: Day → gold; Night → black.

Token follows standard pure mana rules: must be used before turn end or it disappears.
Already handled in `turn-structure-lld.md` §7 Step 2.

**Healing Essence (End of Turn):**

The hero may throw away 1 Wound card from hand OR from the discard pile (returned to Wound
pile). This is NOT regular healing — it cannot be applied to Wounded Units, and cannot be
combined with card-based Heal effects. It uniquely allows searching the discard pile for a
Wound (regular healing cannot).

If no Wounds exist in hand or discard pile: silently skips.

UI sequence:
1. Prompt: "Magical Glade — Remove a Wound? [From Hand] [From Discard] [Skip]"
2. **From Hand**: show Wound cards in hand as tappable. Player selects one → returned to
   Wound pile.
3. **From Discard**: show Wound cards in discard pile. Player selects one → returned to
   Wound pile.
4. **Skip**: no effect.

---

## 7. Inhabited Sites — Interaction Action

Villages and Monasteries are interacted with using the Interaction action. The hero declares
interaction at the start of their action step (after movement ends). `GamePhase` becomes
`Interaction` for the duration. Influence-icon cards are playable.

### Influence Total

Total Influence = sum of all Influence generated this turn ± Reputation modifier.

The Reputation modifier is read once when the hero begins interaction and applied at that
moment. It does not re-apply per purchase.

**Reputation track X:** Hero cannot interact with locals (recruit, heal, learn Advanced
Actions). Burning a monastery is NOT an interaction with locals — it is still possible at
Reputation X.

### Multiple Purchases

Any number of purchases may happen in one interaction as long as Influence is sufficient.
Purchases may mix types.

### 7.1 Village

**Recruiting a Unit:** Pay Influence equal to the unit's cost (upper-left of Unit card).
Only units bearing the village icon (upper-right of the Description card) may be recruited
here.

UI:
1. Unit offer displayed; non-village units greyed out.
2. Hero taps a unit → cost deducted from running Influence total.
3. If at Command limit: disband prompt fires (hero selects a current unit to remove from
   game).
4. Recruited unit placed in Unit area, Command token above it (Ready).

**Buying Healing:** 3 Influence per Healing point. No stated maximum per turn.

UI:
1. "Buy Healing — 3 Influence per point."
2. Stepper or tap-to-increment. Running Influence cost shown.
3. Confirm → Healing points added to the heal pool for this turn.

**Plundering:** NOT part of the Interaction action. Happens during the inter-turn window
after the hero's turn ends, before the next player's turn begins.

Rules:
- Hero announces plundering. Reputation −1 applied immediately.
- Draw 2 cards from top of Deed deck into hand (carry into next round).
- May only plunder once per inter-turn window at a given village.
- If the hero is at the same village at the end of their NEXT turn, they may plunder again
  after that turn ends — there is no multi-turn blocking rule.

### 7.2 Monastery

**When revealed:** Draw 1 card from the Advanced Action deck → add to Unit offer (not the
AA offer). The monk's knowledge card may be learned at any inhabited site via interaction.

**Round Start replenishment:** At the start of each Round, for each monastery on the map
that has NOT been burned, add 1 Advanced Action card to the Unit offer. **This step is
missing from `turn-structure-lld.md` §5.1 and must be added there.**

**Recruiting a Unit:** Same as Village. Only units with the monastery icon may be recruited.

**Buying Healing:** 2 Influence per Healing point (cheaper than a village). No stated
maximum per turn.

**Learning an Advanced Action:** 6 Influence → take one Advanced Action card from the Unit
offer (not the AA offer) → place on TOP of Deed deck. Multiple may be purchased in one
interaction if affordable. The Unit offer slot is NOT replenished until the next Round
Start.

**Burning the Monastery:**

Declaring a burn is the hero's action for the turn. No other action may be taken before it.
Burn is NOT blocked by Reputation X — the hero is not interacting with locals.

Burn sequence:
1. Hero announces burn. Reputation −3 applied immediately (cannot be undone once declared).
2. Draw one random violet enemy token → place face-up on the hex.
3. Combat begins. Units do not participate (they disapprove). No unit activation; no damage
   assigned to units.
4. **Success:** Monastery permanently burned. Place Shield token. Monastery becomes empty
   space. Pending reward: 1 Artifact (resolved at End of Turn). The AA in the Unit offer
   from this monastery stays; this monastery no longer contributes new AA cards at Round
   Start.
5. **Failure:** Enemy discarded (rulebook p10: monastery failure enemies are discarded). A
   new enemy will be drawn next attempt. Reputation has already been reduced. Monastery
   remains intact; the hero may attempt to burn again on a future turn.

---

## 8. Fortified Sites — Assault and Conquest

### Assault Trigger

Entering a hex containing an unconquered fortified site during movement immediately ends
movement and triggers a mandatory assault. Handled by `HexMovementSystem` before the
combat loop.

- Reputation −1 at the moment the hero enters (win or lose).
- All garrison enemies are treated as fortified (Ranged Attacks cannot target them in
  Ranged/Siege phase; Siege Attacks can).
- Garrison enemies that also have the Fortified ability on their token cannot be targeted
  even by Siege Attacks in the Ranged/Siege phase (fortified twice).

**Failure:** Hero withdraws to the hex they came from. Not Forced Withdrawal. If the
destination is unsafe, Forced Withdrawal rules then apply.

**Success:** Place a Shield token on the hex (or City Card for cities). Site = Conquered.

### Terrain Movement

All fortified sites use normal terrain movement costs for the hex they occupy. No
site-specific movement modifiers beyond normal terrain rules.

### Adjacent Rampaging Enemies

Rampaging enemies provoked by the movement path leading to an assault join the combat
alongside the garrison. These rampaging enemies are NOT fortified. See Section 10 for
Reputation rules.

### 8.1 Keep

**Garrison:** 1 gray enemy token, face-down at reveal.

**Visibility:** Flips face-up when a player moves adjacent during a Day Round. Remains
face-down at Night until the hero assaults.

**After conquest:**
- Place Shield token.
- Units with the keep icon may be recruited here via Interaction action.

**Hand limit bonus (draw step):** When ending a turn on or adjacent to an owned keep, Hand
limit increases by the number of keeps the hero owns anywhere on the map. Take only the
higher of keep bonus or city bonus (not both).

### 8.2 Mage Tower

**Garrison:** 1 violet enemy token, face-down at reveal. Same visibility rules as Keep.

**On reveal:** Prepare Spell offer if not yet done: flip top 3 Spell deck cards face-up
into the Spell offer column. Done once per tile reveal.

**After conquest:**
- Place Shield token.
- Pending reward: 1 Spell (End of Turn — see Section 11).

**Interaction at a conquered mage tower (any player may interact, not just the conqueror):**

Buying a Spell:
1. Interaction action. Reputation modifier applies.
2. Cost per Spell: 7 Influence + 1 mana of the Spell's color.
3. Spell placed on TOP of Deed deck.
4. Spell offer replenishes (shift cards down; draw 1 new card to top slot).

Units with the mage tower icon may also be recruited here. Reputation modifier applies to
all purchases at a mage tower.

### 8.3 City

**On reveal:** Load `CityCardState` from `data/cities.yaml`. Enemies placed face-down on
City Card. City Figure placed on hex.

**Assault:** Entering the city hex ends movement and triggers mandatory assault. All city
enemies revealed face-up. They are treated as fortified (Siege only in Ranged/Siege phase).
Enemies with the Fortified token ability are additionally fortified twice.

City-specific garrison bonuses applied to `CombatGroup` at assault setup:

| City | Bonus |
| --- | --- |
| White | All defenders: Armor +1 |
| Blue | Defenders with Fire or Ice Attack: Attack +2. Cold Fire Attack: Attack +1 |
| Red | Defenders with physical Attack: gain Brutal ability |
| Green | Defenders with physical Attack: gain Poison ability |

**Shield tokens on City Card:** One Shield token placed per enemy defeated. Tokens placed in
order of defeat; first-placed wins ties for leader.

Leader = player with the most Shield tokens on this City Card.

**Hand limit bonus:** When adjacent to a conquered city:
- Player has ≥1 Shield token on this City Card: +1 Hand limit.
- Player is leader: +2 Hand limit (replaces +1).
- Take only the higher of keep bonus or city bonus.

**Reputation for city garrison enemies:** Green and red tokens drawn as part of a city
garrison are NOT rampaging enemies and do NOT grant Reputation when defeated. See Section
10 for how `EnemyTokenInstance.IsRampaging` is set.

**City interaction (any player, at any conquered city; Reputation modifier applies):**

Influence bonus: +1 per Shield token the hero holds on this specific City Card.

| City | Option | Cost | Outcome |
| --- | --- | --- | --- |
| Red | Buy Artifact | 12 Influence each | Draw 2 Artifacts at End of Turn; keep 1 |
| Blue | Buy Spell | 7 Influence + 1 matching mana | Spell → top of Deed deck; offer replenished |
| White | Recruit any unit | Normal cost | No icon restriction |
| White | Add Elite to offer | 2 Influence | 1 Elite Unit added to Unit offer immediately |
| Green | Buy AA (from offer) | 6 Influence | Take from AA offer → top of Deed deck; offer replenished |
| Green | Buy AA (from deck) | 6 Influence | Take top card from AA deck → top of Deed deck |

---

## 9. Adventure Sites — Enter as Action

Adventure sites do NOT block movement. A hero may treat an adventure site hex as empty and
pass through or rest on it freely. No assault is triggered.

To enter an adventure site, the hero declares it as their action for the turn after movement
ends. This must be declared before any other action this turn.

### 9.1 Monster Den

**Enter sequence:**
1. Hero declares entry as their action.
2. Draw 1 brown enemy token. Place face-up.
3. Combat begins. Normal Day/Night rules for current round. Units may participate.

**Reward on victory:** 2 random crystals. See Section 11 — Crystal Roll.

Mark hex with Shield token. **The Monster Den cannot be re-entered after conquest.**

**Failure:** Enemy returned face-up to the Monster Den space (or face-down to avoid
confusion with rampaging enemies, per rulebook p10). Same enemy is fought next time the
hero enters.

### 9.2 Spawning Grounds

**Enter sequence:**
1. Hero declares entry as their action.
2. Draw enemies as specified by the site card (2 or more brown tokens). Place all face-up.
3. Combat begins. Normal Day/Night rules for current round. Units may participate.

**Reward:** A flat reward for defeating ALL enemies: 3 crystal rolls + 1 Artifact (see
Section 11). No reward is granted for defeating only some enemies.

Mark hex with Shield token once all enemies are defeated. **The Spawning Grounds cannot be
re-entered after conquest.**

**Failure / partial defeat:** Undefeated enemies returned face-up to the hex (same enemies
next time). Defeated enemies are NOT replaced at Round Start. The reward is not available
until all enemies are defeated.

### 9.3 Dungeon

**Always-active constraints (regardless of current round):**
1. **No Units** — Units refuse to enter. No unit activation; no damage assigned to units.
2. **Night rules** — Gold mana cannot be used. Black mana may power strong Spell effects.

**Enter sequence:**
1. Hero declares entry as their action.
2. Draw 1 brown enemy token. Place face-up.
3. Combat begins under Night rules and no-units constraints.

**Reward on victory:** Roll the spare mana die once:
- Gold or Black → gain 1 Spell (pick from Spell offer; top of Deed deck; offer replenished).
- Any basic color → gain 1 Artifact (draw 2, keep 1, return 1 to bottom of deck).

Mark hex with Shield token. **Dungeon CAN be re-entered after conquest** (unique among
adventure sites). On re-entry: draw a new brown enemy; same constraints; no reward; Fame
only.

**Failure:** Enemy discarded (rulebook p10: dungeon/tomb failure enemies are discarded). A
new enemy will be drawn next time the hero enters.

### 9.4 Tomb

**Always-active constraints (identical to Dungeon):**
1. **No Units** — Units refuse to enter.
2. **Night rules** — Gold mana disabled; black mana powers strong Spells.

**Enter sequence:**
1. Hero declares entry as their action.
2. Draw 1 **red** enemy token. Place face-up.
3. Combat begins under Night rules and no-units constraints.

**Note on Reputation:** Red enemy tokens drawn from the Tomb are NOT rampaging enemies and
do NOT grant Reputation when defeated.

**Reward on victory:** 1 Artifact AND 1 Spell (both, independently resolved — see Section
11).

Mark hex with Shield token. **Tomb CAN be re-entered after conquest** (same as Dungeon).
On re-entry: draw a new red enemy; same constraints; no reward; Fame only.

**Failure:** Enemy discarded. New red enemy drawn next time.

### 9.5 Ancient Ruins

**Ruins token:** Yellow hexagonal token placed on reveal.
- **Day reveal:** Face-up (adventure type visible).
- **Night reveal:** Face-down; flips face-up at start of next Day Round or when player
  enters the hex.

Two adventure types:

---

#### Ancient Altar

Token shows 3 mana symbols of one specific color.

**Sequence:**
1. Hero declares entry as their action.
2. If face-down: flip now (undo gate closes).
3. Prompt: "Pay 3 [color] mana to gain [N] Fame? [Pay] [Decline]"
4. **Pay:** spend 3 mana of required color → gain Fame immediately (not deferred) → discard
   ruins token → mark hex with Shield token.
5. **Decline:** no effect. Token stays. May attempt again on a future turn.

No combat. Fame applied immediately (not a PendingReward). Altar always grants 7 Fame in base game.

---

#### Enemies With Treasure

Token shows 1–2 enemy symbols and a reward symbol.

**Sequence:**
1. Hero declares entry as their action.
2. If face-down: flip now (undo gate closes).
3. Draw the depicted enemy tokens (may include red and/or white tokens — the most powerful
   enemy types in the game, some with Cold Fire Attack). Place face-up.
4. Combat begins. Current round Day/Night rules apply. Units may participate.

**Reputation:** Orc Marauder and Draconum tokens encountered in ruins do NOT grant
Reputation when defeated. Red and white enemy tokens never grant Reputation. See Section 10.

**Multi-enemy notes:**
- Mixed fortified/unfortified group: Siege only vs. fortified enemies in Ranged phase.
- Mixed resistance groups: target resistant enemies individually to avoid halving all
  attacks of a matching type.

**Partial victory:** Undefeated enemies returned face-up to the hex (same enemies next
time). Whoever defeats the LAST remaining enemy removes the ruins token, marks the hex with
their Shield token, and claims the reward at their End of Turn. Partial clearers get Fame
for enemies they defeated but no reward until all are gone.

**Reward options (depicted on the token face):**

| Reward Symbol | Effect |
| --- | --- |
| Artifact | Draw 2 Artifact cards; keep 1; return 1 to bottom of deck |
| Spell | Pick 1 Spell from Spell offer; top of Deed deck; offer replenished |
| Set of 4 crystals | Gain 1 crystal of each basic color |
| Unit | Free recruit any unit from Unit offer; still requires a free Command token (or disband) |
| Advanced Action | Take 1 AA from the AA offer (not the Unit offer); replenish the AA offer; card goes to top of Deed deck |

---

## 10. Rampaging Enemies (Orc Marauder, Draconum)

Rampaging enemies are `EnemyTokenInstance` values in `HexState.RampagingEnemies`, not
`SiteDefinition` entries. The `IsRampaging` flag on `EnemyTokenInstance` is set to `true`
only when the token is drawn from the rampaging enemy piles.

**Reputation is earned only for defeating tokens with `IsRampaging = true`.** Green tokens
from city garrisons and red tokens from Tomb or city garrisons are not rampaging enemies
and never grant Reputation, even if they share a color with rampaging token types.

Rampaging enemies that join a city assault because they were adjacent and provoked (see
Assault and Adjacent Rampaging Enemies, §8) ARE still rampaging (`IsRampaging = true`) and
DO grant Reputation when defeated.

### Placement

Tile reveals and initial setup place rampaging enemies on their designated hexes face-up.

### Movement Restriction

- **Occupied hex:** A hero cannot enter a hex occupied by an undefeated rampaging enemy.
  `HexMovementSystem` treats such hexes as impassable.
- **Provocation:** Moving from a hex adjacent to a rampaging enemy to another hex adjacent
  to the same token triggers a mandatory attack. Movement ends; combat begins.

### Challenging

The hero may declare challenging a rampaging enemy as their action. Hero must be on an
adjacent hex. This is the hero's ONE action for the turn.

### Orc Marauder (green token)

Normal combat rules. **Reward on defeat:** Reputation +1. Token discarded; hex clears.

### Draconum (red token)

Stronger enemy. **Reward on defeat:** Reputation +2. Token discarded; hex clears.

---

## 11. Reward Resolution

All PendingRewards are resolved in `EndOfTurnRewardResolver` at End of Turn Step 5 (per
`turn-structure-lld.md` §9 Step 5). The hero resolves rewards in any order.

**Crystal Roll:**

```csharp
// Called once per roll in the reward spec
ManaColor result = RollSpareManadie();
switch (result) {
    case ManaColor.Gold:
        ManaColor chosen = await _uiBroker.ChooseColor(new ChooseColorRequest {
            Colors = BasicColors, Prompt = "Choose crystal color"
        });
        TryAddCrystal(chosen);  // no-op if already at 3 of that color
        break;
    case ManaColor.Black:
        AddFame(1);
        break;
    default:  // basic color
        TryAddCrystal(result);  // no-op if already at 3 of that color
        break;
}
```

Cap: if count for the color is already 3, nothing is gained.

**SiteRewardType resolution:**

| Reward | Resolution |
| --- | --- |
| TwoCrystals | Crystal Roll × 2 |
| ThreeCrystalsAndArtifact | Crystal Roll × 3, then Artifact draw (below) |
| SpellOrArtifact | Roll spare die: Gold or Black → Spell (below); else → Artifact (below) |
| ArtifactAndSpell | Artifact draw, then Spell pick (both) |
| Artifact | Draw 2 from Artifact deck; hero keeps 1; return 1 to bottom |
| Spell | Hero picks 1 from Spell offer; place on TOP of Deed deck; offer replenished |
| FourCrystals | Add 1 crystal of each basic color (Red, Blue, White, Green); cap applies per color |
| FreeUnit | Show Unit offer; hero picks any 1; no Influence cost; disband prompt if at Command limit |
| AdvancedAction | Hero picks 1 from AA offer; place on TOP of Deed deck; AA offer replenished |
| AltarFame | Applied immediately at altar activation — not deferred to End of Turn |

**Spell reward placement:** Always goes to TOP of Deed deck. Hero draws it as their next
card this turn (if drawing) or first card of next turn.

**Artifact draw:** Draw (count + 1) cards. Hero keeps 1 and places it on top of Deed deck.
All remaining drawn Artifacts go to the BOTTOM of the Artifact deck.

---

## 12. Hand Limit Bonuses from Sites

Evaluated at End of Turn draw step (§9 Step 8 of turn-structure-lld.md).

```csharp
int siteBonus = 0;

// Keep bonus: hero must be on or adjacent to one of their own keeps
if (IsOnOrAdjacentToOwnedKeep(hero.Position, playerId))
    siteBonus = Math.Max(siteBonus, CountPlayerKeeps(playerId));

// City bonus: hero must be adjacent to a conquered city where they have Shields
foreach (var cityHex in MapHexesWithType(SiteType.City)) {
    if (!IsAdjacentOrOn(hero.Position, cityHex)) continue;
    var cc = cityHex.SiteState.CityCard!;
    if (!cc.ShieldsPerPlayer.TryGetValue(playerId, out var shields) || shields == 0) continue;
    int cityBonus = cc.LeaderPlayerId == playerId ? 2 : 1;
    siteBonus = Math.Max(siteBonus, cityBonus);
}

player.HandLimitThisTurn += siteBonus;
```

Take only the higher of keep bonus or city bonus — do not add both.

---

## 13. Night Rule Impacts by Site

| Site | Night Rule |
| --- | --- |
| CrystalMine | Unchanged. Benefit fires end of turn. |
| MagicalGlade | Imbued with Magic gives black token instead of gold. Healing Essence unchanged. |
| Village | Unchanged. |
| Monastery | Unchanged. |
| Keep | Garrison face-down until assaulted. Hero does not know garrison before entering at Night. |
| MageTower | Same as Keep. Terrain movement cost to enter unchanged. |
| City | City enemies face-down before assault. |
| MonsterDen | Current round Day/Night rules apply. |
| SpawningGrounds | Current round Day/Night rules apply. |
| Dungeon | Night rules ALWAYS apply. Gold mana disabled; black mana enables strong Spells. |
| Tomb | Night rules ALWAYS apply (same as Dungeon). |
| AncientRuins | Token face-down on Night reveal. Ruins combat follows current round Day/Night. |

---

## 14. Open Questions

None.

---

## 15. Resolved Questions

- **Ancient Altar 10-Fame variant:** Likely Lost Legion expansion content, not base game.
  `RuinsSiteState.AltarFameReward` defaults to 7 (3-mana single-color altar). No variant
  implemented in v1 scope. ✅ (out of scope)
- **Crystal Mine cap — nothing gained:** If inventory is already at 3 of that color, nothing
  is granted (no substitute token). (Walkthrough p13; turn-structure-lld.md §9 Step 4.) ✅
- **Crystal Mine timing:** Crystal gained after announcing turn end; cannot be used as mana
  this turn. (Walkthrough p13.) ✅
- **Magical Glade timing:** Imbued with Magic fires at Turn Start; Healing Essence fires at
  End of Turn. Both fire on Rest and Regular turns. Both documented in turn-structure-lld.md.
  (Walkthrough p12.) ✅
- **Village healing — no maximum:** No stated cap. The hero may buy as many Healing points
  as Influence allows. ✅
- **Village plundering — consecutive turns allowed:** "Once between each of your turns"
  (Walkthrough p9). If the hero stays at the same village, they may plunder again after each
  of those turns. ✅
- **Monastery AA per Round — missing from turn-structure-lld.md:** At Round Start, add 1 AA
  card to Unit offer per unburned monastery on the map. Must be added to §5.1 of
  turn-structure-lld.md. ✅ (flagged)
- **Burn monastery at Reputation X:** Burn is not interaction with locals; Reputation X does
  not block it. ✅
- **All fortified site garrison enemies treated as fortified:** Being at a fortified site =
  treated as having the Fortified symbol; Ranged Attacks cannot target in Ranged/Siege phase.
  Enemies with the Fortified token ability additionally = fortified twice (even Siege cannot
  target). (Walkthrough p13.) ✅
- **Terrain movement — no site-type exceptions:** Normal terrain costs apply to all fortified
  sites. ✅
- **Reputation modifier at mage towers:** Applies to all purchases (Spells, units). ✅
- **City defenders — not automatically fortified twice:** Only enemies with the Fortified
  token ability, defending a fortified site, are fortified twice. (Walkthrough p13.) ✅
- **City Shield tokens — one per defeated enemy:** Confirmed. ✅
- **Reputation — only for rampaging tokens:** `IsRampaging` flag on `EnemyTokenInstance`
  determines Reputation eligibility. Green/red tokens from garrisons or Tomb are not
  rampaging. Rampaging enemies that join an assault while adjacent are still rampaging. ✅
- **Monster Den / Spawning Grounds — no re-entry after conquest.** ✅
- **Dungeon / Tomb — re-enterable after conquest** (Fame only, no reward). ✅
- **Dungeon / Tomb failure — enemies discarded:** New enemy drawn next visit. (Rulebook
  p10.) ✅
- **Monster Den / Spawning Grounds failure — enemies returned:** Same enemies on the hex
  next visit. (Rulebook p10.) ✅
- **Spawning Grounds reward:** Flat 3 crystal rolls + 1 Artifact for clearing all enemies.
  No reward for partial clearing. Defeated enemies are not replaced at Round Start. ✅
- **Tomb draws red enemy token** (not brown). ✅
- **Tomb reward: Artifact AND Spell** (both). ✅
- **Ancient Ruins — Advanced Action reward:** Taken from the AA offer (not Unit offer); AA
  offer is replenished. (Walkthrough p16.) ✅
- **City enemy composition — data-driven from data/cities.yaml.** ✅
- **Dungeon / Tomb Night rules always active** regardless of current round. (Walkthrough
  p15.) ✅
- **Ruins Reputation — Orcs/Draconum in ruins:** No Reputation. (Walkthrough p16.) ✅
