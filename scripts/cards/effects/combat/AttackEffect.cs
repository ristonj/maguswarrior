using System.Threading.Tasks;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Cards.Effects.Combat;

public class AttackEffect : IEffect {
    private readonly int _points;
    private readonly EffectType _distance;
    private readonly AttackElement _element;

    public AttackEffect(int points, EffectType distance, AttackElement element) {
        _points = points;
        _distance = distance;
        _element = element;
    }

    public Task<EffectResult> Execute(GameState state, EffectContext ctx) {
        state.AddAttackPoints(_points, _distance, _element);
        return Task.FromResult(EffectResult.Ok());
    }
}
