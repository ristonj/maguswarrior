# Enemy Token LLD — Magus Warrior

Enemy token data model, site associations, all ability mechanics, garrison state machine,
multi-enemy combat, ruins tokens, and city defender bonuses.

**Cross-references:**
- Rampaging enemy map behavior (provocation, movement restrictions): `hex-movement-lld.md` §6
- Full 4-phase combat sequence and damage computation: `combat-flow-lld.md`
- Card effects that interact with enemy abilities: `effect-lld.md`
- Architecture patterns (EnemyFactory, hooks): `_bmad-output/game-architecture.md`

**Solo v1 scope:** Full base game enemy set. City garrison is out of scope for First
Reconnaissance (the city cannot be entered or conquered in that scenario). §3.5 and §7
are documented for completeness; implementation is deferred.

---

## 1. Data Model

### 1.1 TokenColor (enum)

```csharp
public enum TokenColor
{
    Green,   // Orc Marauder (rampaging, countryside)
    Red,     // Draconum (rampaging, core)
    Brown,   // Dungeon/dark place monsters
    Gray,    // Keep garrison
    Violet,  // Mage tower garrison (and monastery burn defense)
    // City garrison draws from multiple colors per city data — see §3.5
}
```

There are also hexagonal **Ruins tokens** — not round enemy tokens. They are a separate
type. See §8.

---

### 1.2 EnemyAbility (enum)

```csharp
public enum EnemyAbility
{
    // Defensive
    Fortified,           // site fortification doubling; see §5.1
    PhysicalResistance,  // physical attacks halved
    FireResistance,      // fire attacks halved
    IceResistance,       // ice attacks halved
    // Cold fire resistance is DERIVED: IceResistance && FireResistance; no enum value

    // Attack abilities (apply in Block and Assign Damage phases)
    Swift,          // requires 2× attack value to fully block
    Brutal,         // if not fully blocked, full damage doubles
    Poison,         // wounding a unit applies 2 wounds; hero wounded gains extra wound in discard
    Paralyze,       // unit wounded = immediately destroyed; hero wounded = discard all non-Wounds
    Summon,         // no personal attack; summons enemies at start of Block phase

    // Expansion / future use
    ArcaneImmunity, // immune to Cold Toughness ice_block_scaling hook (no base game tokens use this)
}
```

`ArcaneImmunity` has no base game tokens. It is defined so the `IceBlockScalingHook`
(see `effect-lld.md` §cold_toughness) has a stable check point without a code change if
expansion tokens are added.

---

### 1.3 AttackType (enum)

```csharp
public enum AttackType
{
    Physical,
    Fire,
    Ice,
    ColdFire,
    None,  // Summon enemies have no attack value
}
```

---

### 1.4 EnemyTokenDefinition (data, loaded from data/enemies.yaml)

```csharp
public record EnemyTokenDefinition(
    string Id,
    string Name,
    TokenColor Color,
    int Armor,
    int Attack,
    AttackType AttackType,
    int Fame,
    IReadOnlyList<EnemyAbility> Abilities,
    bool IsRampaging,          // true for Orc Marauder and Draconum
    SummonBehavior? Summon     // non-null only for enemies with the Summon ability
);

// Parameters for Summon — configurable per token, not hard-coded
public record SummonBehavior(TokenColor Color, int Count);
```

All per-token stats live in `data/enemies.yaml`. This document specifies the schema and
mechanics only. `EnemyFactory` loads from this file at startup.

**YAML schema (examples):**

```yaml
# Normal enemy
- id: orc_marauder
  name: "Marauding Orcs"
  color: green
  armor: 4
  attack: 2
  attack_type: physical
  fame: 1
  abilities: []
  is_rampaging: true

# Summoner (base game)
- id: orc_summoner
  name: "Orc Summoner"
  color: brown
  armor: 3
  attack: 0
  attack_type: none
  fame: 2
  abilities: [summon]
  is_rampaging: false
  summon:
    color: brown
    count: 1

# Hypothetical expansion summoner (two tokens)
- id: summoner_dragon
  name: "Summoner Dragon"
  color: violet
  armor: 5
  attack: 0
  attack_type: none
  fame: 4
  abilities: [summon]
  is_rampaging: false
  summon:
    color: brown
    count: 2
```

---

### 1.5 EnemyTokenInstance (runtime state)

```csharp
public class EnemyTokenInstance
{
    public EnemyTokenDefinition Definition { get; init; }

    // Map / garrison state
    public bool FaceUp { get; set; }          // false = drawn but not yet revealed to player
    public TokenLocation Location { get; set; } // MapHex | SiteGarrison | Combat | Discarded

    // Combat modifiers — scoped to a single combat; reset when CombatState is torn down
    public int ArmorModifier { get; set; }           // from Tremor, Chill, Ice Shield powered, etc.
    public int AttackModifier { get; set; }          // from city defender bonuses
    public HashSet<EnemyAbility> StrippedAbilities { get; } = new(); // from Expose, Chill, Demolish
    public HashSet<EnemyAbility> GrantedAbilities { get; } = new();  // from city color bonuses
    public bool AttackCancelled { get; set; }        // from Whirlwind, Chill
    public bool FortificationsIgnored { get; set; }  // from Demolish unpowered

    // Summon
    public bool IsSummoned { get; set; }             // true for monsters summoned via Summon ability
    public EnemyTokenInstance? Summoner { get; set; } // back-ref for summoned monsters

    // Computed
    public int EffectiveArmor => Math.Max(1, Definition.Armor + ArmorModifier);
    public int EffectiveAttack => Definition.Attack + AttackModifier;

    public bool HasAbility(EnemyAbility a) =>
        (Definition.Abilities.Contains(a) || GrantedAbilities.Contains(a))
        && !StrippedAbilities.Contains(a);

    // Cold fire resistance is derived — never set directly
    public bool HasColdFireResistance =>
        HasAbility(EnemyAbility.FireResistance) && HasAbility(EnemyAbility.IceResistance);
}
```

---

## 2. Token Piles and Site Associations

### 2.1 Five Enemy Token Piles

```
Pile     Color    Shape    Content
Green    Green    Round    Orc Marauder rampaging enemies
Red      Red      Round    Draconum rampaging enemies
Brown    Brown    Round    Dungeon and dark-place monsters
Gray     Gray     Round    Keep garrison
Violet   Violet   Round    Mage tower garrison + monastery burn defender
```

City garrison draws from one or more of these piles per city level data (§3.5).

There is also a separate **Ruins pile** (hexagonal tokens) — not an enemy pile. See §8.

Each pile has an adjacent discard pile. When a draw pile is exhausted, shuffle its discard
back into it and resume drawing.

---

### 2.2 Which Pile to Draw From (by site type)

| Site | Pile(s) | Count | When Drawn | Pre-placed |
|------|---------|-------|------------|------------|
| Keep | Gray | 1 | At tile reveal | Yes (face-down) |
| Mage Tower | Violet | 1 | At tile reveal | Yes (face-down) |
| Dungeon | Brown | 1 | When hero declares exploration | No |
| Monster Den | Brown | 1 | When hero declares exploration | No |
| Spawning Grounds | Brown | 2 | When hero declares exploration | No |
| Tomb | Brown | 1 | When hero declares exploration | No |
| Ruins | Ruins pile (hexagonal) | 1 ruins token | When hero declares exploration | No |
| Monastery (burn) | Violet | 1 | When hero announces burn attempt | No |
| City | Per city level data | varies | At city reveal | Yes (face-down on City card) |

**Keep / Mage Tower pre-placement:** when the tile is placed, `EnemyFactory` draws one
token from the appropriate pile (face-down) and assigns it to the site. It is not visible
to the player until Day adjacency or assault.

**Adventure sites:** no token exists until exploration is declared. `EnemyFactory` draws
at the moment the hero's exploration action is confirmed — after the undo gate closes,
because the draw is new information.

---

### 2.3 Garrison Reveal — Keeps and Mage Towers

```
State machine per garrison token:

  [FaceDown]
      │ Hero moves adjacent during Day
      ▼
  [FaceUp]         ← stays face-up even after Night begins
      │ Hero assaults
      ▼
  [InCombat]
      │ Defeated
      ▼
  [Discarded]      → returned to corresponding discard pile
```

Night case: if the garrison token was never revealed during a Day phase (no hero was
adjacent while it was Day), it remains face-down. It is only revealed when the hero
assaults at Night — flipped face-up as part of combat setup. This is new information;
the decision to assault cannot be undone once the token is revealed.

Cities are an exception: their garrison is always revealed when any hero is adjacent,
even at Night.

---

## 3. Rampaging Enemies

### 3.1 Token Definitions

Rampaging enemies are always placed face-up on the map. Stats live in `data/enemies.yaml`.

| Token | Color | Notes |
|-------|-------|-------|
| Orc Marauder | Green | One or more placed at game start per starting tile symbols |
| Draconum | Red | Appear on core tiles; generally stronger than Orc Marauders |

Both types are always visible (never face-down).

---

### 3.2 Map Behavior

Governed by `hex-movement-lld.md` §6. Summary:

- Hero cannot enter a rampaging enemy's space under normal movement.
- Moving from one adjacent space to another adjacent space of the same token provokes it.
- Provoked = enemy immediately attacks; movement ends; mandatory combat action.
- Hero may voluntarily challenge adjacent rampaging enemies as their action.
- Multiple adjacent rampaging enemies may be challenged in one combat.
- During a fortified site assault: adjacent rampaging enemies may be challenged to join
  the combat; they are not fortified; they need not be defeated to conquer the site.

---

### 3.3 Fame and Reputation on Defeat

When a rampaging enemy is defeated:

1. **Fame:** the hero gains fame equal to the enemy's `fame` value.
2. **Reputation:**
   - Orc Marauder: Reputation **+1** per defeated token.
   - Draconum: Reputation **+2** per defeated token.
3. **Map cleanup:** discard the token to the appropriate discard pile.

Reputation is awarded regardless of how the combat started (challenge or provocation).

---

### 3.4 Defeated Rampaging Enemy — Space Status

After the token is discarded, the hex retains its underlying terrain type (plains, hills,
etc.) and is now freely traversable. It is a safe space for movement and Forced Withdrawal
purposes. No Shield token is placed on it.

---

### 3.5 City Garrison (Deferred — Not Needed for First Recon)

City garrison composition is data-driven from `data/cities.yaml`. Each city entry has
12 levels; each level specifies how many tokens of each pile color are drawn when that
city is at that level:

```yaml
# data/cities.yaml (schema sketch — implementation deferred)
- city_id: white_city
  levels:
    1: { gray: 1 }
    2: { gray: 1, violet: 1 }
    3: { gray: 2, violet: 1 }
    # ... through level 12
```

`EnemyFactory.DrawForCity(string cityId, int level)` draws tokens per this data.
Implementation is gated behind Epic 5+ (city assault). Revisit this section when city
combat comes into scope.

---

## 4. Enemy Abilities: Defensive

### 4.1 Fortified

The `Fortified` ability combines with whether the enemy is defending a fortified site
(keep, mage tower, or city). Effective fortification level governs what attacks are legal
in the Ranged and Siege phase:

| Condition | Level | Ranged phase |
|-----------|:-----:|--------------|
| No ability, not at fortified site | 0 | Ranged and Siege attacks both work |
| Has `Fortified` ability OR at fortified site (not both) | 1 | Only Siege attacks work; Ranged have no effect |
| Has `Fortified` ability AND at fortified site | 2 | No attacks work in Ranged phase at all |

```csharp
public int GetFortificationLevel(EnemyTokenInstance enemy, bool isAtFortifiedSite)
{
    int level = 0;
    if (enemy.HasAbility(EnemyAbility.Fortified)) level++;
    if (isAtFortifiedSite && !enemy.FortificationsIgnored) level++;
    return level;
}
```

`FortificationsIgnored` is set by Demolish unpowered for the rest of combat. It zeroes
the site's contribution but not the token's own `Fortified` ability — a doubly-fortified
enemy drops to level 1, not 0.

Expose strips `Fortified` via `StrippedAbilities`, which removes the token ability
directly. Expose on a doubly-fortified enemy at a fortified site drops it to level 1
(site contribution remains).

In the Melee Attack phase, fortification level has no effect — all attack types work.

---

### 4.2 Physical Resistance

Total all Physical attack values targeting this enemy → halve the total (round down) →
use that as the effective Physical attack value.

Mixed attacks: if the player declares Physical Attack 3 + Fire Attack 4 against an enemy
with physical resistance only, the Fire component is unaffected: effective = floor(3/2) + 4
= 1 + 4 = 5.

---

### 4.3 Fire Resistance / Ice Resistance

Same halving rule as Physical Resistance, applied to Fire and Ice attack values
respectively.

A note from `effect-lld.md` §Mana Color and Resistance Interaction: non-attack non-block
effects costing red mana have no effect on Fire-resistant enemies; those costing blue mana
have no effect on Ice-resistant enemies. This is a no-effect (the effect simply fails),
not a halving. It applies to special effects only, not to Attack or Block values.

---

### 4.4 Cold Fire Resistance (Derived)

An enemy has Cold Fire resistance if and only if it has **both** Fire resistance **and**
Ice resistance. There is no separate Cold Fire resistance data flag.

```csharp
public bool IsResistantTo(EnemyTokenInstance enemy, AttackType type) => type switch
{
    AttackType.Physical  => enemy.HasAbility(EnemyAbility.PhysicalResistance),
    AttackType.Fire      => enemy.HasAbility(EnemyAbility.FireResistance),
    AttackType.Ice       => enemy.HasAbility(EnemyAbility.IceResistance),
    AttackType.ColdFire  => enemy.HasAbility(EnemyAbility.FireResistance)
                         && enemy.HasAbility(EnemyAbility.IceResistance),
    _ => false,
};
```

---

## 5. Enemy Abilities: Attack Phase

### 5.1 Block Threshold (Base Rule — Applies to All Enemies)

Enemy attacks are all-or-nothing. There is no partial block. The player either reaches
the **block threshold** (full damage is zero) or does not (full damage applies):

```
Normal enemy:  threshold = EffectiveAttack
Swift enemy:   threshold = EffectiveAttack × 2
```

```csharp
public bool IsFullyBlocked(int blockTotal, EnemyTokenInstance enemy)
{
    int threshold = enemy.HasAbility(EnemyAbility.Swift)
        ? enemy.EffectiveAttack * 2
        : enemy.EffectiveAttack;
    return blockTotal >= threshold;
}
```

---

### 5.2 Swift

**Rule:** the block threshold is doubled — the player must contribute block equal to or
greater than **twice** the enemy's EffectiveAttack. Any block total below this threshold
has no effect; the attack deals its full EffectiveAttack value.

This follows from the all-or-nothing base rule. Swift does not introduce a new mechanic;
it only raises the threshold.

Example: Swift enemy, Attack 3. Block 5 → below threshold (6) → no effect, full 3 damage.
Block 6 → threshold met → 0 damage.

---

### 5.3 Brutal

**Rule:** if the attack was NOT fully blocked, the damage **doubles**.

Since blocking is all-or-nothing:
- Block total ≥ threshold → 0 damage (Brutal irrelevant).
- Block total < threshold → damage = EffectiveAttack × 2.

```csharp
public int ComputeDamage(bool isFullyBlocked, EnemyTokenInstance enemy)
{
    if (isFullyBlocked) return 0;
    int damage = enemy.EffectiveAttack;
    if (enemy.HasAbility(EnemyAbility.Brutal)) damage *= 2;
    return damage;
}
```

Swift+Brutal: threshold is 2× Attack; if not reached, damage = Attack × 2.

---

### 5.4 Poison

**Rule — units:** when a unit is wounded by a Poisonous enemy, the unit receives **2
wounds** instead of 1. It must be healed twice to fully recover.

**Rule — hero:** when the hero takes a wound from a Poisonous enemy, the hero receives a
wound in hand as normal AND an additional wound is placed in the **discard pile**. The
discard wound surfaces in the deck next Round.

The discard wound does not count toward the current combat's knockout check (only wounds
in hand do).

---

### 5.5 Paralyze

**Rule — units:** when a unit is assigned any damage by a Paralyzing enemy and would
become wounded, the unit is instead **immediately destroyed** (permanently removed from
the game). Any resistance reduces damage first, but damage > 0 triggers destruction.

**Rule — hero:** when the hero takes a wound from a Paralyzing enemy, the hero
immediately discards **all non-wound cards from hand** to the discard pile. The wound
itself is added to hand as normal.

Paralyze fires per enemy attack. If two Paralyzing enemies are both unblocked, the hero
can be paralyzed twice (nothing to discard the second time is not an error).

```csharp
public async Task ApplyParalyzeEffect(EnemyTokenInstance enemy, AssignmentTarget target,
    GameState state, UIBroker broker)
{
    if (!enemy.HasAbility(EnemyAbility.Paralyze)) return;

    if (target.IsUnit)
    {
        state.UnitRoster.DestroyUnit(target.Unit);
        await broker.ShowNotification("Unit paralyzed — destroyed!");
    }
    else // hero
    {
        var discarded = state.Hero.Hand
            .Where(c => c.CardType != CardType.Wound)
            .ToList();
        state.Hero.DiscardCards(discarded);
        await broker.ShowNotification($"Paralyzed! {discarded.Count} cards discarded.");
    }
}
```

---

### 5.6 Summon

**Rule:**

1. Summon enemies have `AttackType.None` and `Attack = 0` — no personal attack.
2. If NOT defeated in the Ranged phase: at the **start of the Block phase**, draw
   `Definition.Summon.Count` tokens from the `Definition.Summon.Color` pile. The count
   and pile color are defined per-token in `data/enemies.yaml` — they are not hard-coded.
3. Summoned tokens (`IsSummoned = true`) replace the summoner in Block and Assign Damage
   phases. The summoner is temporarily inactive.
4. Each summoned token is resolved independently using its own stats and abilities (Swift,
   Brutal, Poison, Paralyze, etc. all apply from the summoned token).
5. After the Assign Damage phase resolves (whether summoned tokens were blocked or not):
   all summoned tokens are **discarded** to their respective pile's discard. No fame is
   awarded for them.
6. The **Attack phase** is played against the original **summoner** as normal. Fame is
   awarded if the summoner is defeated.

Recursive Summon: if a summoned token itself has the Summon ability, it does **not**
trigger a secondary summon. The summoned token's Summon ability is inert for that combat.

```csharp
public async Task HandleSummonAtBlockPhaseStart(
    EnemyTokenInstance summoner, CombatGroup group, EnemyFactory factory)
{
    if (!summoner.HasAbility(EnemyAbility.Summon) || summoner.AttackCancelled) return;

    var behavior = summoner.Definition.Summon!;
    var summoned = new List<EnemyTokenInstance>();
    for (int i = 0; i < behavior.Count; i++)
        summoned.Add(factory.DrawFromPile(behavior.Color));

    foreach (var s in summoned) { s.IsSummoned = true; s.Summoner = summoner; }
    group.ReplaceSummonerWithSummoned(summoner, summoned);
}
```

---

## 6. Multi-Enemy Combat

### 6.1 Combat Groups

```csharp
public class CombatGroup
{
    public IReadOnlyList<EnemyTokenInstance> ActiveEnemies { get; }
    public IReadOnlyList<EnemyTokenInstance> DefeatedEnemies { get; }
    public bool IsAtFortifiedSite { get; init; }
}
```

Examples: one challenged rampaging enemy; keep garrison; spawning grounds (2 tokens);
fortified site garrison + adjacent challenged rampaging enemies.

---

### 6.2 Attack Targeting in Ranged Phase

The player may target enemies individually or combine them:

- **Single target:** one attack against one enemy. Only that enemy's resistances and
  fortification level apply.
- **Combined target:** one combined attack total against multiple enemies. Must equal or
  exceed the **sum** of all targeted enemies' EffectiveArmor. Effective attack uses the
  **union** of all targeted enemies' resistances — if any one targeted enemy has a
  resistance, that attack type is halved for the entire combined attack.

Fortification for combined targets: if any targeted enemy is fortification level 1,
only Siege attacks work in the Ranged phase for that group. Any enemy at level 2 cannot
be in the group at all — it must be left for the Attack phase.

---

### 6.3 Block Phase — One Enemy Per Block Action

Only one enemy is the target of each block action. Multiple block actions can target
different enemies. Any enemy not blocked deals damage in the Assign Damage phase.

---

### 6.4 Assign Damage Phase Order

The player chooses the order to assign damage from unblocked enemies. Each unblocked
enemy deals `ComputeDamage(isFullyBlocked: false, enemy)` damage. Ability side effects
(Poison, Paralyze) fire per wound resolved, not per enemy.

Full damage assignment rules: `combat-flow-lld.md`.

---

### 6.5 Fame from Multi-Enemy Combat

The hero gains fame equal to the sum of each defeated enemy's `Definition.Fame` value.
All fame from a single combat is accumulated and awarded at end of combat. Level-up is
resolved once at end of turn.

---

## 7. City Assault — Defender Bonuses

**Deferred. Not needed for First Recon.** Revisit when city assault stories are in scope.

When assaulting a city, all defenders receive bonuses based on city color, applied as
modifiers at combat setup and removed when `CombatState` is torn down:

| City Color | Bonus |
|------------|-------|
| White | `ArmorModifier += 1` |
| Blue | `AttackModifier += 2` for Fire/Ice attack; `+= 1` for Cold Fire |
| Red | Tokens with Physical attack gain `EnemyAbility.Brutal` in `GrantedAbilities` |
| Green | Tokens with Physical attack gain `EnemyAbility.Poison` in `GrantedAbilities` |

---

## 8. Ruins Token System

Ruins tokens are **hexagonal** and drawn from the Ruins pile. They are not round enemy
tokens. One is drawn when the hero declares exploration of a ruins space.

### 8.1 Two Ruins Token Types

**Type A — Ancient Altar:**
- Token depicts a colored mana symbol (one basic color).
- No enemies.
- Hero may pay 3 mana of the depicted color → gain 7 fame.
- If unable or unwilling: nothing happens.

**Type B — Enemies With Treasure:**
- Token depicts 1 or 2 enemy token images (by pile color) and a reward icon.
- Hero must fight all depicted enemies. Enemy tokens are drawn from the indicated pile(s)
  at the moment the ruins token is revealed (undo gate closes on the draw).
- If all enemies are defeated: hero claims the reward at end of turn.
- If any enemy survives: hero fails; surviving enemy tokens are returned face-down to the
  ruins space. Next hero to explore draws fresh enemies.
- Reward types: Artifact, Spell, Advanced Action, set of 4 crystals (one per basic color),
  or a free Unit from the offer.

### 8.2 Rampaging Enemies in Ruins

Orc Marauder and Draconum tokens encountered inside ruins do **not** grant Reputation on
defeat (Reputation is only awarded for rampaging enemies defeated on the open map).

---

## 9. EnemyFactory

```csharp
public class EnemyFactory
{
    // Shuffles the discard back into the pile if the draw pile is empty
    public EnemyTokenInstance DrawFromPile(TokenColor color);

    // Convenience: draws from the correct pile for the given site type
    public EnemyTokenInstance DrawForSite(SiteType siteType);

    // Creates an instance from a specific definition (for debug/test spawning)
    public EnemyTokenInstance CreateFromDefinition(EnemyTokenDefinition def);
}
```

`EnemyFactory` is injected via constructor. Pile state lives in `GameState.EnemyPiles`.

---

## 10. Night Visibility Summary

| Token type | Day | Night |
|------------|-----|-------|
| Rampaging enemies (Orc, Draconum) | Face-up, always visible | Always visible |
| Keep/mage tower garrison (previously revealed) | Face-up | Stays face-up |
| Keep/mage tower garrison (not yet revealed) | Revealed when adjacent | Stays face-down until assault |
| City garrison | Revealed when adjacent | Always revealed when adjacent |
| Ruins tokens (on map, not yet entered) | Face-up | Face-down; revealed on entry or next Day |
| Adventure site enemies | Drawn at declaration | Drawn at declaration |

Full Night movement and visibility rules: `hex-movement-lld.md` §11.

---

## Open Questions

None at time of writing.

---

## Resolved Questions

**Q: Are Ruins tokens enemy tokens or a separate category?**
A: Separate. They are hexagonal, non-round, and represent site content (altar or
enemies+reward). `EnemyFactory` does not draw them; a RuinsResolver handles the ruins
pile directly.

**Q: Does Poison's secondary wound (hero) count toward combat knockout?**
A: No. The secondary wound goes to the discard pile. Only wounds in hand count toward
the current combat's knockout check.

**Q: Does Demolish (FortificationsIgnored) remove the token's Fortified ability?**
A: No. `FortificationsIgnored` zeroes the site's contribution to fortification level;
the token's own `Fortified` ability is unaffected. Expose strips `Fortified` via
`StrippedAbilities`, which does affect the token ability directly.

**Q: Do provoked rampaging enemies award Reputation?**
A: Yes. Reputation is awarded whenever a rampaging enemy is defeated, regardless of
whether the combat was a voluntary challenge or a provocation.

**Q: Can a summoned token trigger a second summon?**
A: No. The summoned token's Summon ability is inert for the duration of that combat.

**Q: How is city garrison composition determined?**
A: From `data/cities.yaml` — each city has 12 levels; each level specifies token counts
by pile color. The physical board game uses a rotating clix base with colored circles;
the data file replaces that mechanism entirely.

**Q: Is there a separate rule for ruins on core tiles?**
A: No. Ruins are the same regardless of which tile type they appear on.
