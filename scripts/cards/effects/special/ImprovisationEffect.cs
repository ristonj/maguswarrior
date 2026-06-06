using System;
using System.Threading.Tasks;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Cards.Effects.Special;

public class ImprovisationEffect : IEffect {
    private readonly EffectType _effectType;
    private readonly int _amount;

    public ImprovisationEffect(EffectType effectType, int amount) {
        _effectType = effectType;
        _amount = amount;
    }

    public Task<EffectResult> Execute(GameState state, EffectContext ctx) {
        switch (_effectType) {
            case EffectType.Move:
                state.AddMovePoints(_amount);
                break;
            case EffectType.AttackMelee:
                state.AddAttackPoints(_amount, EffectType.AttackMelee, AttackElement.Physical);
                break;
            case EffectType.Block:
                state.AddBlockPoints(_amount, AttackElement.Physical);
                break;
            case EffectType.Influence:
                state.AddInfluencePoints(_amount);
                break;
            default:
                throw new InvalidOperationException(
                    $"ImprovisationEffect: unsupported EffectType {_effectType}");
        }
        return Task.FromResult(EffectResult.Ok());
    }
}
