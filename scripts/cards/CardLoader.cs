using System;
using System.Collections.Generic;
using System.IO;
using MagusWarrior.Core.Types;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MagusWarrior.Cards;

public static class CardLoader {
    // LoadAll reads from the filesystem — used by tests with an absolute path.
    public static IReadOnlyList<CardDefinition> LoadAll(string yamlPath) =>
        ParseAll(File.ReadAllText(yamlPath));

    // ParseAll parses YAML content directly — used at runtime via Godot.FileAccess.
    public static IReadOnlyList<CardDefinition> ParseAll(string yamlContent) {
        var yaml = yamlContent;
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithTypeConverter(new StringOrListConverter())
            .IgnoreUnmatchedProperties()
            .Build();

        var root = deserializer.Deserialize<CardFileRoot>(yaml)
            ?? throw new InvalidOperationException("cards.yaml deserialized to null");
        if (root.Cards == null)
            throw new InvalidOperationException("cards.yaml is missing the top-level 'cards:' key");

        var seen = new HashSet<string>();
        var result = new List<CardDefinition>(root.Cards.Count);
        foreach (var entry in root.Cards) {
            if (string.IsNullOrEmpty(entry.Id))
                throw new InvalidOperationException("cards.yaml contains a card with missing id");
            if (!seen.Add(entry.Id))
                throw new InvalidOperationException($"cards.yaml contains duplicate id '{entry.Id}'");
            if (!TryParseCardType(entry.Type, out var cardType))
                throw new InvalidOperationException($"Card '{entry.Id}' has unrecognized type '{entry.Type}'");

            result.Add(new CardDefinition {
                Id        = entry.Id,
                Name      = entry.Name ?? entry.Id,
                Type      = cardType,
                ManaCost  = ParseManaColor(entry.ManaCost),
                Unpowered = ConvertSpec(entry.Unpowered),
                Powered   = ConvertSpec(entry.Powered),
            });
        }
        return result;
    }

    private static EffectSpec? ConvertSpec(YamlEffectSpec? yaml) {
        if (yaml == null) return null;
        return new EffectSpec {
            EffectType = ParseEffectType(yaml.EffectType?.Count > 0 ? yaml.EffectType[0] : null),
            Text       = yaml.Text ?? string.Empty,
            Move       = yaml.Move,
            Attack     = yaml.Attack,
            Block      = yaml.Block,
            Influence  = yaml.Influence,
            Heal       = yaml.Heal,
        };
    }

    private static EffectType ParseEffectType(string? raw) => raw?.ToLowerInvariant() switch {
        "move"         => EffectType.Move,
        "attackmelee"  => EffectType.AttackMelee,
        "attackranged" => EffectType.AttackRanged,
        "attacksiege"  => EffectType.AttackSiege,
        "combat"       => EffectType.AttackMelee,
        "block"        => EffectType.Block,
        "influence"    => EffectType.Influence,
        "heal"         => EffectType.Heal,
        "healing"      => EffectType.Heal,
        "mana"         => EffectType.Mana,
        "crystal"      => EffectType.Crystal,
        "fame"         => EffectType.Fame,
        "reputation"   => EffectType.Reputation,
        "banner"       => EffectType.Banner,
        "special"      => EffectType.Special,
        _              => throw new InvalidOperationException(
                              $"Unknown or missing effect_type '{raw ?? "<null>"}' in cards.yaml"),
    };

    private static bool TryParseCardType(string? raw, out CardType result) {
        result = raw?.ToLowerInvariant() switch {
            "basicaction"    => CardType.BasicAction,
            "advancedaction" => CardType.AdvancedAction,
            "spell"          => CardType.Spell,
            "artifact"       => CardType.Artifact,
            "wound"          => CardType.Wound,
            _                => (CardType)(-1),
        };
        return (int)result >= 0;
    }

    private static ManaColor? ParseManaColor(string? raw) {
        if (string.IsNullOrEmpty(raw)) return null;
        return raw.ToLowerInvariant() switch {
            "white" => ManaColor.White,
            "blue"  => ManaColor.Blue,
            "red"   => ManaColor.Red,
            "green" => ManaColor.Green,
            "gold"  => ManaColor.Gold,
            "black" => ManaColor.Black,
            _       => throw new InvalidOperationException($"Unknown mana_cost '{raw}' in cards.yaml"),
        };
    }

    // ── Private POCOs for YamlDotNet deserialization ──────────────────────────

    private class CardFileRoot {
        public List<YamlCardEntry> Cards { get; set; } = new();
    }

    private class YamlCardEntry {
        public string?       Id       { get; set; }
        public string?       Type     { get; set; }
        public string?       Name     { get; set; }
        public string?       ManaCost { get; set; }
        public YamlEffectSpec? Unpowered { get; set; }
        public YamlEffectSpec? Powered   { get; set; }
    }

    private class YamlEffectSpec {
        // List<string> because some cards declare effect_type as [Move, Influence, Combat];
        // StringOrListConverter normalises a scalar "Move" into ["Move"] automatically.
        public List<string>? EffectType { get; set; }
        public string?       Text       { get; set; }
        public int           Move       { get; set; }
        public int           Attack     { get; set; }
        public int           Block      { get; set; }
        public int           Influence  { get; set; }
        public int           Heal       { get; set; }
    }

    // Handles effect_type being either a scalar string or a YAML sequence of strings.
    private class StringOrListConverter : IYamlTypeConverter {
        public bool Accepts(Type type) => type == typeof(List<string>);

        public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer) {
            if (parser.TryConsume<Scalar>(out var scalar))
                return new List<string> { scalar.Value };

            var list = new List<string>();
            parser.Consume<SequenceStart>();
            while (!parser.TryConsume<SequenceEnd>(out _))
                list.Add(parser.Consume<Scalar>().Value);
            return list;
        }

        public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
            throw new NotSupportedException();
    }
}
