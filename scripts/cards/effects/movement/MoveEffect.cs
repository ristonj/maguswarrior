using System.Threading.Tasks;
using MagusWarrior.Core;

namespace MagusWarrior.Cards.Effects.Movement;

public class MoveEffect : IEffect {
    private readonly int _points;

    public MoveEffect(int points) {
        _points = points;
    }

    public Task<EffectResult> Execute(GameState state, EffectContext ctx) {
        state.AddMovePoints(_points);
        return Task.FromResult(EffectResult.Ok());
    }
}
