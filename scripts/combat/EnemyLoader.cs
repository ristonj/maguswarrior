using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagusWarrior.Core.Types;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MagusWarrior.Combat;

public static class EnemyLoader {
    public static List<EnemyTokenDefinition> LoadAll(string yamlPath) =>
        ParseAll(File.ReadAllText(yamlPath));

    public static List<EnemyTokenDefinition> ParseAll(string yamlContent) {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var root = deserializer.Deserialize<EnemyFileRoot>(yamlContent)
            ?? throw new InvalidOperationException("enemies.yaml deserialized to null");
        if (root.Enemies == null)
            throw new InvalidOperationException("enemies.yaml is missing the top-level 'enemies:' key");

        var seen   = new HashSet<string>();
        var result = new List<EnemyTokenDefinition>(root.Enemies.Count);

        foreach (var e in root.Enemies) {
            if (string.IsNullOrEmpty(e.Id))
                throw new InvalidOperationException("enemies.yaml contains an entry with missing id");
            if (!seen.Add(e.Id))
                throw new InvalidOperationException($"enemies.yaml contains duplicate id '{e.Id}'");

            var color     = ParseColor(e.Id, e.Color);
            var attacks   = BuildAttacks(e.Id, e.Attacks);
            var abilities = BuildAbilities(e.Id, e.Resistances, e.Fortified, e.Attacks);

            result.Add(new EnemyTokenDefinition(
                Id:          e.Id,
                Name:        e.Name ?? e.Id,
                Color:       color,
                Armor:       e.Armor,
                Attacks:     attacks,
                FameValue:   e.Fame,
                Abilities:   abilities,
                IsRampaging: e.IsRampaging,
                Summon:      e.Summon != null
                                 ? new SummonBehavior(ParseColor(e.Id, e.Summon.Color), e.Summon.Count)
                                 : null
            ));
        }
        return result;
    }

    private static TokenColor ParseColor(string id, string? raw) {
        if (string.IsNullOrEmpty(raw))
            throw new InvalidOperationException($"Enemy '{id}' is missing 'color'");
        if (Enum.TryParse<TokenColor>(raw, ignoreCase: true, out var color))
            return color;
        throw new InvalidOperationException($"Enemy '{id}' has unknown color '{raw}'");
    }

    private static IReadOnlyList<EnemyAttack> BuildAttacks(string id, List<YamlAttack>? raw) {
        if (raw == null || raw.Count == 0) return Array.Empty<EnemyAttack>();
        return raw.Select(a => new EnemyAttack(a.Value, ParseAttackType(id, a.AttackType))).ToList();
    }

    private static AttackType ParseAttackType(string id, string? raw) => raw?.ToLowerInvariant() switch {
        "physical"  => AttackType.Physical,
        "fire"      => AttackType.Fire,
        "ice"       => AttackType.Ice,
        "cold_fire" => AttackType.ColdFire,
        "summon"    => AttackType.None,
        _ => throw new InvalidOperationException($"Enemy '{id}' has unknown attack_type '{raw ?? "<null>"}'"),
    };

    private static IReadOnlyList<EnemyAbility> BuildAbilities(
        string id,
        List<string>?  resistances,
        bool           fortified,
        List<YamlAttack>? rawAttacks) {

        var abilities = new HashSet<EnemyAbility>();

        if (resistances != null) {
            foreach (var r in resistances) {
                abilities.Add(r.ToLowerInvariant() switch {
                    "physical" => EnemyAbility.PhysicalResistance,
                    "fire"     => EnemyAbility.FireResistance,
                    "ice"      => EnemyAbility.IceResistance,
                    _ => throw new InvalidOperationException($"Enemy '{id}' has unknown resistance '{r}'"),
                });
            }
        }

        if (fortified)
            abilities.Add(EnemyAbility.Fortified);

        if (rawAttacks != null) {
            foreach (var atk in rawAttacks) {
                if (atk.AttackType?.ToLowerInvariant() == "summon")
                    abilities.Add(EnemyAbility.Summon);

                if (atk.Modifiers == null) continue;
                foreach (var mod in atk.Modifiers) {
                    if (Enum.TryParse<EnemyAbility>(mod, ignoreCase: true, out var ability))
                        abilities.Add(ability);
                    else
                        throw new InvalidOperationException($"Enemy '{id}' has unknown modifier '{mod}'");
                }
            }
        }

        return abilities.ToList();
    }

    // ── Private POCOs for YamlDotNet ─────────────────────────────────────────

    private class EnemyFileRoot {
        public List<YamlEnemyEntry> Enemies { get; set; } = new();
    }

    private class YamlEnemyEntry {
        public string?        Id          { get; set; }
        public string?        Name        { get; set; }
        public string?        Color       { get; set; }
        public int            Armor       { get; set; }
        public int            Fame        { get; set; }
        public bool           Fortified   { get; set; }
        public bool           IsRampaging { get; set; }
        public List<string>?  Resistances { get; set; }
        public List<YamlAttack>? Attacks  { get; set; }
        public YamlSummon?    Summon      { get; set; }
    }

    private class YamlAttack {
        public int           Value      { get; set; }
        public string?       AttackType { get; set; }
        public List<string>? Modifiers  { get; set; }
    }

    private class YamlSummon {
        public string? Color { get; set; }
        public int     Count { get; set; }
    }
}
