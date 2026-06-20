using System.Threading.Tasks;
using MagusWarrior.Combat;

namespace MagusWarrior.Broker;

// Minimal stub — methods return immediately; real UI panels wired in later stories.
// All methods must remain pure C# (no Godot types) so the test project can compile this file.
public class UIBroker {
    public Task ShowStartOfCombatInterstitial(CombatState combat) => Task.CompletedTask;
    public Task PromptHeroRangedAttacks(CombatState combat)       => Task.CompletedTask;
    public Task ResolveEnemyAttackVsHero(EnemyTokenInstance enemy, CombatState combat) => Task.CompletedTask;
    public Task PromptHeroMeleeAttacks(CombatState combat)        => Task.CompletedTask;
}
