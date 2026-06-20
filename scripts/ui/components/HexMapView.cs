using System;
using System.Collections.Generic;
using Godot;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;
using MagusWarrior.Hex;
using MagusWarrior.Map;

namespace MagusWarrior.UI;

public partial class HexMapView : Node2D {
    private const float HexSize = 80f;
    private const int RevealCost = 2;

    private WorldMap _map = null!;
    private GameState _state = null!;
    private InputLock _lock = null!;
    private Polygon2D _heroMarker = null!;
    private Label _previewLabel = null!;
    private HexCoord? _previewedHex;
    private bool _previewIsExplore;
    private readonly Dictionary<HexCoord, Polygon2D> _hexPolygons = new();

    public void Initialize(WorldMap map, GameState state, InputLock inputLock) {
        _map = map;
        _state = state;
        _lock = inputLock;

        var corners = HexCorners(HexSize);
        int hexCount = 0;
        foreach (var tile in _map.PlacedTiles) {
            foreach (var (worldCoord, terrain) in tile.WorldHexes()) {
                var poly = new Polygon2D();
                poly.Polygon = corners;
                poly.Color = tile.IsRevealed ? TerrainColor(terrain) : FogColor();
                poly.Position = HexToPixel(worldCoord);
                AddChild(poly);
                _hexPolygons[worldCoord] = poly;
                hexCount++;
            }
        }

        _heroMarker = new Polygon2D();
        _heroMarker.Name = "HeroMarker";
        _heroMarker.Polygon = HexCorners(HexSize * 0.3f);
        _heroMarker.Color = new Color(1f, 1f, 1f);
        _heroMarker.ZIndex = 1;
        _heroMarker.Position = HexToPixel(_map.HeroPosition);
        AddChild(_heroMarker);

        _previewLabel = new Label();
        _previewLabel.Name = "MoveCostPreview";
        _previewLabel.AddThemeFontSizeOverride("font_size", 28);
        _previewLabel.ZIndex = 2;
        _previewLabel.Visible = false;
        AddChild(_previewLabel);

        _map.HeroMoved += coord => _heroMarker.Position = HexToPixel(coord);
        _map.TileRevealed += OnTileRevealed;
        _state.ResourcesChanged += RefreshPreview;
        _state.DayNightChanged += RefreshPreview;
        Log.Debug("[HexGrid]", $"HexMapView initialized: {hexCount} hexes, hero at {_map.HeroPosition.Q},{_map.HeroPosition.R}");
    }

    // _UnhandledInput (not _Input) so taps a GUI Control already consumed — e.g. an
    // overlapping HandView/Rest/Improvisation card button — never double-fire a hex preview.
    // _UnhandledInput still fires for Node2D; it only skips already-handled events.
    public override void _UnhandledInput(InputEvent @event) {
        if (@event is not InputEventScreenTouch { Pressed: false } touch) return;
        if (touch.Index != 0) return;  // only the primary touch previews; ignore extra fingers
        if (!_lock.TryAcquire()) {
            Log.Debug("[Input]", "HexMapView tap dropped: input locked");
            return;
        }
        try {
            HandleHexTap(ToLocal(touch.Position));
            GetViewport().SetInputAsHandled();
        } finally {
            _lock.Release();
        }
    }

    private void HandleHexTap(Vector2 localPos) {
        var coord = PixelToHex(localPos);

        // Branch 1: hero's current hex → undo last move via UndoController
        if (coord == _map.HeroPosition) {
            if (!_state.UndoController.CanUndoHeroMove) return;
            var move = _state.UndoController.PopHeroMove();
            if (move is { } m) {
                // Clear preview BEFORE AddMovePoints fires ResourcesChanged → RefreshPreview
                _previewedHex = null;
                _previewIsExplore = false;
                _previewLabel.Visible = false;
                // SetHeroPosition fires HeroMoved → marker updates automatically
                _map.SetHeroPosition(m.Previous);
                _state.AddMovePoints(m.CostRefund);
                Log.Debug("[Input]", $"Move undone: back to {m.Previous.Q},{m.Previous.R} refund={m.CostRefund} remaining={_state.MovePointsThisTurn}");
            }
            return;
        }

        // Branch 2: revealed hex → movement commit or cost preview
        var hexState = _map.Grid.GetState(coord);
        if (hexState != null) {
            int? cost = TerrainCosts.GetCost(hexState.Terrain, _state.IsDay);
            bool isAdjacent = false;
            foreach (var n in _map.HeroPosition.Neighbors())
                if (n == coord) { isAdjacent = true; break; }

            if (isAdjacent && cost != null && _state.MovePointsThisTurn >= cost.Value) {
                // Valid move: commit immediately — undo is free until tile reveal
                _previewedHex = null;
                _previewIsExplore = false;
                _previewLabel.Visible = false;
                var prevPos = _map.HeroPosition;  // capture BEFORE CommitHeroMove changes it
                _state.SpendMovePoints(cost.Value);
                _map.CommitHeroMove(coord);
                _state.UndoController.PushHeroMove(prevPos, cost.Value);
                Log.Debug("[Input]", $"Hero moved to {coord.Q},{coord.R} cost={cost} remaining={_state.MovePointsThisTurn}");
            } else {
                _previewedHex = coord;
                _previewIsExplore = false;
                UpdatePreviewLabel(coord, cost);
                Log.Debug("[Input]", $"Hex tapped: {coord.Q},{coord.R} terrain={hexState.Terrain} cost={cost?.ToString() ?? "impassable"}");
            }
            return;
        }

        // Branch 3: unrevealed tile hex → explore commit or explore preview
        var unrevealedTile = _map.FindTileForCoord(coord);
        if (unrevealedTile != null) {
            bool isAdjacent = false;
            foreach (var n in _map.HeroPosition.Neighbors())
                if (n == coord) { isAdjacent = true; break; }

            if (isAdjacent && _state.MovePointsThisTurn >= RevealCost) {
                // Clear preview BEFORE SpendMovePoints fires ResourcesChanged → RefreshPreview
                _previewedHex = null;
                _previewIsExplore = false;
                _previewLabel.Visible = false;
                _state.SpendMovePoints(RevealCost);
                _state.TripUndoGate();
                _map.RevealTile(unrevealedTile);
                Log.Debug("[Input]", $"Tile '{unrevealedTile.TileId}' revealed from {coord.Q},{coord.R} remaining={_state.MovePointsThisTurn}");
            } else {
                _previewedHex = coord;
                _previewIsExplore = true;
                UpdateExploreLabelVisuals(coord);
                Log.Debug("[Input]", $"Unrevealed hex tapped: {coord.Q},{coord.R} adjacent={isAdjacent} affordable={_state.MovePointsThisTurn >= RevealCost}");
            }
            return;
        }

        // Branch 4: complete miss (off-grid, not unrevealed) — no-op
    }

    private void OnTileRevealed(MapTile tile) {
        foreach (var (worldCoord, terrain) in tile.WorldHexes()) {
            if (_hexPolygons.TryGetValue(worldCoord, out var poly))
                poly.Color = TerrainColor(terrain);
        }
        _previewedHex = null;
        _previewIsExplore = false;
        _previewLabel.Visible = false;
        Log.Debug("[HexGrid]", $"Tile '{tile.TileId}' revealed — {_hexPolygons.Count} total hexes");
    }

    // Re-evaluate the visible preview when move points or day/night change.
    // No-op when nothing is currently previewed.
    private void RefreshPreview() {
        if (_previewedHex is not { } coord) return;
        if (_previewIsExplore) {
            // Self-heal parity with the movement branch: if the previewed coord is no
            // longer an unrevealed tile (e.g. its tile got revealed), drop the preview.
            if (_map.FindTileForCoord(coord) == null) { _previewedHex = null; _previewIsExplore = false; _previewLabel.Visible = false; return; }
            UpdateExploreLabelVisuals(coord);
        } else {
            var hexState = _map.Grid.GetState(coord);
            if (hexState == null) { _previewedHex = null; _previewLabel.Visible = false; return; }
            UpdatePreviewLabel(coord, TerrainCosts.GetCost(hexState.Terrain, _state.IsDay));
        }
    }

    private void UpdatePreviewLabel(HexCoord coord, int? cost) {
        if (cost == null) {
            _previewLabel.Text = "Impassable";
            _previewLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        } else {
            _previewLabel.Text = $"Move: {cost}";
            bool canAfford = _state.MovePointsThisTurn >= cost.Value;
            _previewLabel.AddThemeColorOverride("font_color",
                canAfford
                    ? new Color(0.298f, 0.686f, 0.314f)   // #4CAF50 green
                    : new Color(0.957f, 0.263f, 0.212f));  // #F44336 red
        }
        _previewLabel.Position = HexToPixel(coord) + new Vector2(-30f, -HexSize - 10f);
        _previewLabel.Visible = true;
    }

    private void UpdateExploreLabelVisuals(HexCoord coord) {
        _previewLabel.Text = $"Explore: {RevealCost}";
        bool canAfford = _state.MovePointsThisTurn >= RevealCost;
        _previewLabel.AddThemeColorOverride("font_color",
            canAfford
                ? new Color(0.298f, 0.686f, 0.314f)   // #4CAF50 green
                : new Color(0.957f, 0.263f, 0.212f));  // #F44336 red
        _previewLabel.Position = HexToPixel(coord) + new Vector2(-30f, -HexSize - 10f);
        _previewLabel.Visible = true;
    }

    // Inverse of HexToPixel for flat-top hexes.
    // Derived from: x = HexSize * (3/2 * q), y = HexSize * (sqrt3/2 * q + sqrt3 * r)
    private static HexCoord PixelToHex(Vector2 localPos) {
        float q = 2f * localPos.X / (3f * HexSize);
        float r = localPos.Y / (HexSize * MathF.Sqrt(3f)) - q / 2f;
        return HexRound(q, r);
    }

    private static HexCoord HexRound(float q, float r) {
        float s = -q - r;
        int rq = (int)MathF.Round(q);
        int rr = (int)MathF.Round(r);
        int rs = (int)MathF.Round(s);
        float dq = MathF.Abs(rq - q);
        float dr = MathF.Abs(rr - r);
        float ds = MathF.Abs(rs - s);
        if (dq > dr && dq > ds)
            return new HexCoord(-rr - rs, rr);
        if (dr > ds)
            return new HexCoord(rq, -rq - rs);
        return new HexCoord(rq, rr);
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

    private static Color FogColor() => new Color(0.1f, 0.1f, 0.1f);

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
