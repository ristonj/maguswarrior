using System.Threading.Tasks;
using MagusWarrior.Core;

namespace MagusWarrior.Cards.Effects.Influence;

public class InfluenceEffect : IEffect {
    private readonly int _points;

    public InfluenceEffect(int points) {
        _points = points;
    }

    public Task<EffectResult> Execute(GameState state, EffectContext ctx) {
        state.AddInfluencePoints(_points);
        return Task.FromResult(EffectResult.Ok());
    }
}
