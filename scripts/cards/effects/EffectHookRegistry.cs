using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MagusWarrior.Core;

namespace MagusWarrior.Cards.Effects;

public enum HookPoint {
    BeforeBlockTargeting,
    AfterSourceDieRoll,
    EndOfTurn,
}

public interface IEffectHook {
    bool AppliesTo(EffectContext ctx);
    Task<EffectContext> Transform(EffectContext ctx, GameState state);
}

public class EffectHookRegistry {
    private readonly Dictionary<HookPoint, List<(IEffectHook Hook, int Priority)>> _hooks = new();

    public void Register(HookPoint point, IEffectHook hook, int priority = 0) {
        if (!_hooks.TryGetValue(point, out var list)) {
            list = new List<(IEffectHook, int)>();
            _hooks[point] = list;
        }
        list.Add((hook, priority));
    }

    public async Task<EffectContext> RunHooks(HookPoint point, EffectContext ctx, GameState state) {
        if (!_hooks.TryGetValue(point, out var entries)) return ctx;
        foreach (var (hook, _) in entries.OrderBy(e => e.Priority))
            if (hook.AppliesTo(ctx))
                ctx = await hook.Transform(ctx, state);
        return ctx;
    }
}
