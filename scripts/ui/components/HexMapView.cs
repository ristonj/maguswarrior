using System;
using Godot;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;
using MagusWarrior.Hex;
using MagusWarrior.Map;

namespace MagusWarrior.UI;

public partial class HexMapView : Node2D {
    private const float HexSize = 80f;

    private WorldMap _map = null!;
    private Polygon2D _heroMarker = null!;

    public void Initialize(WorldMap map) {
        _map = map;

        var corners = HexCorners(HexSize);
        int hexCount = 0;
        foreach (var (coord, state) in _map.Grid.AllHexes()) {
            var poly = new Polygon2D();
            poly.Polygon = corners;
            poly.Color = TerrainColor(state.Terrain);
            poly.Position = HexToPixel(coord);
            AddChild(poly);
            hexCount++;
        }

        _heroMarker = new Polygon2D();
        _heroMarker.Name = "HeroMarker";
        _heroMarker.Polygon = HexCorners(HexSize * 0.3f);
        _heroMarker.Color = new Color(1f, 1f, 1f);
        _heroMarker.ZIndex = 1;
        _heroMarker.Position = HexToPixel(_map.HeroPosition);
        AddChild(_heroMarker);

        _map.HeroMoved += coord => _heroMarker.Position = HexToPixel(coord);
        Log.Debug("[HexGrid]", $"HexMapView initialized: {hexCount} hexes, hero at {_map.HeroPosition.Q},{_map.HeroPosition.R}");
    }

    private static Vector2 HexToPixel(HexCoord coord) {
        float x = HexSize * (3f / 2f * coord.Q);
        float y = HexSize * (MathF.Sqrt(3f) / 2f * coord.Q + MathF.Sqrt(3f) * coord.R);
        return new Vector2(x, y);
    }

    private static Vector2[] HexCorners(float size) {
        var corners = new Vector2[6];
        for (int i = 0; i < 6; i++) {
            float a = MathF.PI / 3f * i;
            corners[i] = new Vector2(size * MathF.Cos(a), size * MathF.Sin(a));
        }
        return corners;
    }

    private static Color TerrainColor(TerrainType terrain) => terrain switch {
        TerrainType.Plains    => new Color(0.298f, 0.686f, 0.314f),  // #4CAF50
        TerrainType.Hills     => new Color(0.627f, 0.514f, 0.353f),  // #A0835A
        TerrainType.Forest    => new Color(0.106f, 0.369f, 0.125f),  // #1B5E20
        TerrainType.Desert    => new Color(0.976f, 0.659f, 0.145f),  // #F9A825
        TerrainType.Swamp     => new Color(0.427f, 0.478f, 0.235f),  // #6D7A3C
        TerrainType.Wasteland => new Color(0.459f, 0.459f, 0.459f),  // #757575
        TerrainType.Mountain  => new Color(0.216f, 0.278f, 0.310f),  // #37474F
        TerrainType.Lake      => new Color(0.086f, 0.396f, 0.753f),  // #1565C0
        TerrainType.CitySpace => new Color(1.000f, 0.839f, 0.000f),  // #FFD600
        _                     => new Color(0.5f, 0.5f, 0.5f),
    };
}
