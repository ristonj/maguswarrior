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
