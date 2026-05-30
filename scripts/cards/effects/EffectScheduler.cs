using System.Collections.Generic;
using System.Threading.Tasks;
using MagusWarrior.Core;

namespace MagusWarrior.Cards.Effects;

public class EffectScheduler {
    private record PendingEffect(IEffect Effect, EffectContext Ctx);

    private readonly PriorityQueue<PendingEffect, int> _queue = new();

    public void Enqueue(IEffect effect, int priority, EffectContext ctx) =>
        _queue.Enqueue(new PendingEffect(effect, ctx), priority);

    public async Task ResolveAll(GameState state) {
        while (_queue.Count > 0) {
            var pending = _queue.Dequeue();
            var snapshot = state.TakeSnapshot();
            var result = await pending.Effect.Execute(state, pending.Ctx);
            state.EventLog.Append(new EffectFiredEvent(
                pending.Ctx.SourceCardId,
                pending.Ctx.ChosenType,
                pending.Ctx.Phase,
                pending.Ctx.Powered,
                snapshot));
            foreach (var triggered in result.Triggered)
                _queue.Enqueue(new PendingEffect(triggered.Effect, triggered.Ctx), triggered.Priority);
        }
    }
}
