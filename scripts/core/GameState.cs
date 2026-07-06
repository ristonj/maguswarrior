using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MagusWarrior.Cards;
using MagusWarrior.Core.Types;
#if GODOT
using Godot;
#endif

namespace MagusWarrior.Core;

public class GameState {
    public event Action? ResourcesChanged;
    public event Action? DayNightChanged;
    public event Action? UndoGateCrossed;
    public event Action? PhaseChanged;
    public GameStateSnapshot? LastGateSnapshot { get; private set; }

    public GamePhase CurrentPhase { get; private set; }
    public bool IsDay { get; private set; } = true;
    public int MovePointsThisTurn { get; private set; }
    public int InfluencePointsThisTurn { get; private set; }
    public int TotalAttackThisTurn => _attackPool.Values.Sum();
    public int TotalBlockThisTurn  => _blockPool.Values.Sum();
    // Wrapped in ReadOnlyDictionary so the live backing dictionary can't be cast back to
    // Dictionary and mutated — all writes must go through AddAttackPoints/AddBlockPoints,
    // which keeps the snapshot/LOCKSTEP discipline intact. Re-wrapped per access because
    // RestoreSnapshot reassigns the backing fields.
    public IReadOnlyDictionary<(EffectType Distance, AttackElement Element), int> AttackPool =>
        new ReadOnlyDictionary<(EffectType Distance, AttackElement Element), int>(_attackPool);
    public IReadOnlyDictionary<AttackElement, int> BlockPool =>
        new ReadOnlyDictionary<AttackElement, int>(_blockPool);
    public IReadOnlyList<CardDefinition> Cards { get; private set; }
    public GameEventLog EventLog { get; } = new();
    public UndoController UndoController { get; } = new();

    // Hero is deliberately EXCLUDED from the undo snapshot (TakeSnapshot/RestoreSnapshot).
    // The hero's combat wound-state is mutated only during combat resolution. Per
    // combat-flow-lld §15, the undo gate closes the instant hidden information is revealed —
    // a face-down enemy drawn, a garrison flipped, a ruins token turned — or any die is rolled;
    // for those combats that reveal happens at the START of combat, before the Assign-Damage
    // phase draws wounds. So wounds are always drawn AFTER the gate has closed and can never be
    // rolled back by undo, which makes snapshotting Hero unnecessary. (Base combat rolls no dice
    // — verified against MKUE Rulebook pp. 8–9 — so the reveal is the only gate trigger.)
    //
    // KNOWN GAP (not reachable today): a FACE-UP rampaging enemy (orc/draconum) provoked with no
    // die roll reveals nothing, so per §15 undo stays OPEN through that whole combat — there,
    // excluding Hero would let combat wounds survive an undo (half-rollback). Unreachable now:
    // the dev-combat handler trips the gate at combat start (PlaceholderMainMenu), and rampager
    // provocation is Epic-4 story 4-5. When that lands, handle wound rollback by unwinding the
    // whole combat on undo of the trigger-move — NOT by field-level snapshotting. Do not add
    // Hero to the snapshot without that design. Mid-combat SAVE persistence is a separate
    // serialization path (story 3-6), also not this undo snapshot. See deferred-work.md.
    public Hero Hero { get; } = new Hero(GameConstants.HeroBaseArmor);

    private Dictionary<(EffectType Distance, AttackElement Element), int> _attackPool = new();
    private Dictionary<AttackElement, int> _blockPool = new();

    public GameState() : this(LoadCardsOrThrow()) { }

    public GameState(IReadOnlyList<CardDefinition> cards) {
        Cards = cards;
    }

    // SetPhase is the explicit phase-mutation API for the turn loop (TurnManager, RestView).
    // CurrentPhase is already in GameStateSnapshot, so phase transitions are captured by
    // TakeSnapshot/RestoreSnapshot without any additional snapshot changes.
    public void SetPhase(GamePhase phase) {
        if (CurrentPhase == phase) return;
        CurrentPhase = phase;
        PhaseChanged?.Invoke();
    }

    public void SetIsDay(bool isDay) {
        if (IsDay == isDay) return;
        IsDay = isDay;
        DayNightChanged?.Invoke();
    }

    public int Fame { get; private set; } = 0;

    public void AddMovePoints(int n) { MovePointsThisTurn += n; ResourcesChanged?.Invoke(); }
    public void SpendMovePoints(int n) { MovePointsThisTurn -= n; ResourcesChanged?.Invoke(); }
    public void ResetMovePoints() { MovePointsThisTurn = 0; ResourcesChanged?.Invoke(); }
    public void AddFame(int n) { Fame += n; ResourcesChanged?.Invoke(); }
    public void AddInfluencePoints(int n) { InfluencePointsThisTurn += n; ResourcesChanged?.Invoke(); }

    public void AddAttackPoints(int n, EffectType distance, AttackElement element) {
        var key = (distance, element);
        _attackPool[key] = _attackPool.GetValueOrDefault(key) + n;
        ResourcesChanged?.Invoke();
    }

    public void AddBlockPoints(int n, AttackElement element) {
        _blockPool[element] = _blockPool.GetValueOrDefault(element) + n;
        ResourcesChanged?.Invoke();
    }

    public void ClearAttackAndBlockPools() {
        _attackPool.Clear();
        _blockPool.Clear();
        ResourcesChanged?.Invoke();
    }

    public void TripUndoGate() {
        LastGateSnapshot = TakeSnapshot();
        EventLog.Clear();
        UndoController.Clear();
        UndoGateCrossed?.Invoke();
#if GODOT
        Log.Debug("[Core]", "Undo gate crossed — snapshot written, event log cleared");
#endif
    }

    // LOCKSTEP: every mutable field added to GameState MUST also be added to
    // GameStateSnapshot and restored here, or undo silently produces a half-rollback.
    // Current snapshot fields: CurrentPhase (mutated via SetPhase and RestoreSnapshot),
    // MovePointsThisTurn, InfluencePointsThisTurn,
    // AttackPool (keyed by distance+element), BlockPool (keyed by element), IsDay.
    // Add Hand, Fame, Reputation, etc. here the moment they land in GameState.
    // EXCEPTION: Hero is deliberately NOT snapshotted. Its wound-state changes only during
    // combat, and for every combat reachable today the undo gate has already closed before
    // wounds are drawn (combat-flow-lld §15). The one open case — a face-up rampager fight, in
    // which undo stays open — is unreachable until Epic-4 story 4-5 and is tracked in
    // deferred-work.md. See the full rationale on the Hero property above.
    public GameStateSnapshot TakeSnapshot() => new(
        CurrentPhase,
        MovePointsThisTurn,
        InfluencePointsThisTurn,
        new Dictionary<(EffectType, AttackElement), int>(_attackPool),
        new Dictionary<AttackElement, int>(_blockPool),
        IsDay);

    public void RestoreSnapshot(GameStateSnapshot snapshot) {
        var dayChanged   = IsDay != snapshot.IsDay;
        var phaseChanged = CurrentPhase != snapshot.CurrentPhase;
        CurrentPhase = snapshot.CurrentPhase;
        MovePointsThisTurn = snapshot.MovePointsThisTurn;
        InfluencePointsThisTurn = snapshot.InfluencePointsThisTurn;
        _attackPool = new Dictionary<(EffectType, AttackElement), int>(snapshot.AttackPool);
        _blockPool = new Dictionary<AttackElement, int>(snapshot.BlockPool);
        IsDay = snapshot.IsDay;
        // RestoreSnapshot is a mutator like the additive ones above — it MUST fire the same
        // notifications, or observers (StagingAreaView subscribes only to ResourcesChanged)
        // stay stale after an undo. See LOCKSTEP note: event-firing parity, not just field parity.
        ResourcesChanged?.Invoke();
        if (dayChanged)   DayNightChanged?.Invoke();
        if (phaseChanged) PhaseChanged?.Invoke();
    }

    private static IReadOnlyList<CardDefinition> LoadCardsOrThrow() {
        try {
#if GODOT
            using var f = FileAccess.Open("res://data/cards.yaml", FileAccess.ModeFlags.Read);
            if (f == null)
                throw new System.IO.IOException(
                    $"res://data/cards.yaml not found (Godot error {FileAccess.GetOpenError()})");
            return CardLoader.ParseAll(f.GetAsText());
#else
            return CardLoader.LoadAll("data/cards.yaml");
#endif
        } catch (InvalidOperationException) {
            throw;
        } catch (Exception ex) {
            throw new InvalidOperationException(
                "Failed to load cards.yaml — startup failure", ex);
        }
    }
}
