using System;
using System.Collections.Generic;

namespace MagusWarrior.Cards.Effects;

public record EffectResult(bool Success, IReadOnlyList<TriggeredEffect> Triggered) {
    public static EffectResult Ok() => new(true, Array.Empty<TriggeredEffect>());
}

public record TriggeredEffect(IEffect Effect, EffectContext Ctx, int Priority);
