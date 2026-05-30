using System.Threading.Tasks;
using MagusWarrior.Core;

namespace MagusWarrior.Cards.Effects;

public interface IEffect {
    Task<EffectResult> Execute(GameState state, EffectContext ctx);
}
