using System;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Hex;

public static class TerrainCosts {
    // Returns null for impassable terrain (Mountain, Lake).
    // Source of truth: docs/hex-movement-lld.md §2.
    public static int? GetCost(TerrainType terrain, bool isDay) => terrain switch {
        TerrainType.Plains    => 2,
        TerrainType.Hills     => 3,
        TerrainType.Forest    => isDay ? 3 : 5,
        TerrainType.Desert    => isDay ? 5 : 3,
        TerrainType.Swamp     => 5,
        TerrainType.Wasteland => 4,
        TerrainType.Mountain  => null,
        TerrainType.Lake      => null,
        TerrainType.CitySpace => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(terrain), terrain, "Unknown terrain type"),
    };
}
