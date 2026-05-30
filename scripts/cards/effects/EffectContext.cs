using MagusWarrior.Core.Types;

namespace MagusWarrior.Cards.Effects;

public record EffectContext(string SourceCardId, EffectType ChosenType, GamePhase Phase, bool Powered);
