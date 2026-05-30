using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using MagusWarrior.Cards.Effects;
using MagusWarrior.Cards.Effects.Movement;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Core;

public static class GameDebug {
    [Conditional("DEBUG")]
    public static void Inspect(string label, object? value) {
        // TODO: dump to on-screen overlay — Story 1a+
    }

    [Conditional("DEBUG")]
    public static void UndoLastEvent(GameState state) {
        var ev = state.EventLog.PopLast();
        if (ev == null) {
            Log.Debug("[Effect]", "UndoLastEvent: nothing to undo");
            return;
        }
        state.RestoreSnapshot(ev.StateBefore);
        Log.Debug("[Effect]", $"UndoLastEvent: undid {ev.SourceCardId}/{ev.EffectType} in {ev.Phase}");
    }

    [Conditional("DEBUG")]
    public static void InspectEventLog(GameState state) {
        if (state.EventLog.Events.Count == 0) {
            Log.Debug("[Effect]", "EventLog: (empty)");
            return;
        }
        for (int i = 0; i < state.EventLog.Events.Count; i++) {
            var e = state.EventLog.Events[i];
            Log.Debug("[Effect]", $"EventLog[{i}]: {e.SourceCardId} {e.EffectType} phase={e.Phase} powered={e.Powered}");
        }
    }

    [Conditional("DEBUG")]
    public static async void FireTestEffect(GameState state, string cardId, GamePhase phase) {
        try {
            var card = state.Cards.FirstOrDefault(c => c.Id == cardId);
            if (card?.Unpowered == null) {
                Log.Debug("[Effect]", $"FireTestEffect: card '{cardId}' not found or has no unpowered spec");
                return;
            }
            if (!PhaseGate.IsLegal(card.Unpowered.EffectType, phase)) {
                Log.Debug("[Effect]", $"FireTestEffect: {card.Unpowered.EffectType} is not legal in {phase}");
                return;
            }
            var ctx = new EffectContext(card.Id, card.Unpowered.EffectType, phase, Powered: false);
            var effect = new MoveEffect(card.Unpowered.Move);
            var scheduler = new EffectScheduler();
            scheduler.Enqueue(effect, priority: 0, ctx);
            await scheduler.ResolveAll(state);
            Log.Debug("[Effect]", $"FireTestEffect complete: {card.Id} fired, MovePoints={state.MovePointsThisTurn}");
        } catch (Exception ex) {
            Log.Debug("[Effect]", $"FireTestEffect threw: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
