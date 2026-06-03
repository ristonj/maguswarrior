using System.Threading.Tasks;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Cards.Effects.Combat;

public class BlockEffect : IEffect {
    private readonly int _points;
    private readonly AttackElement _element;

    public BlockEffect(int points, AttackElement element) {
        _points = points;
        _element = element;
    }

    public Task<EffectResult> Execute(GameState state, EffectContext ctx) {
        state.AddBlockPoints(_points, _element);
        return Task.FromResult(EffectResult.Ok());
    }
}
