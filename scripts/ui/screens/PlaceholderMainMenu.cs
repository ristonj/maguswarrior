using System.Collections.Generic;
using System.Linq;
using Godot;
using MagusWarrior.Cards;
using MagusWarrior.Cards.Effects;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;
using MagusWarrior.Deck;
using MagusWarrior.Hex;
using MagusWarrior.Map;
using MagusWarrior.Save;

namespace MagusWarrior.UI;

public partial class PlaceholderMainMenu : CanvasLayer {
    private GameState _state = null!;
    private SaveManager _saveManager = null!;
    private DeckManager _deckManager = null!;
    private EffectScheduler _effectScheduler = null!;
    private StagingManager _stagingManager = null!;
    private InputLock _inputLock = null!;
    private WorldMap _worldMap = null!;
    private RestView _restView = null!;
    private ImprovisationView _improvView = null!;
#if DEBUG
    private EffectEventLogPanel _effectInspector = null!;
    private Button _debugToggleArea = null!;
    private int _debugTapCount = 0;
#endif

    public override void _Ready() {
        _state           = new GameState();
        _saveManager     = new SaveManager();
        _deckManager     = new DeckManager();
        _effectScheduler = new EffectScheduler();
        _stagingManager  = new StagingManager();
        _inputLock       = new InputLock();
        _worldMap        = BuildStartingMap();

#if DEBUG
        _effectInspector = new EffectEventLogPanel();
        _effectInspector.Name = "EffectEventLogPanel";
        AddChild(_effectInspector);
        _effectInspector.Initialize(_state, _worldMap);

        _debugToggleArea = new Button();
        _debugToggleArea.Name = "DebugToggleArea";
        _debugToggleArea.OffsetRight = 80f;
        _debugToggleArea.OffsetBottom = 80f;
        _debugToggleArea.Flat = true;
        _debugToggleArea.ZIndex = 10;
        _debugToggleArea.Pressed += OnDebugToggleAreaPressed;
        AddChild(_debugToggleArea);
#endif

        var titleLabel = new Label();
        titleLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        titleLabel.AddThemeFontSizeOverride("font_size", 72);
        titleLabel.Text = "ui.placeholder_menu.title";
        titleLabel.AutoTranslateMode = Node.AutoTranslateModeEnum.Always;
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        titleLabel.VerticalAlignment = VerticalAlignment.Center;
        AddChild(titleLabel);

        var hexMapView = new HexMapView();
        hexMapView.Name = "HexMapView";
        hexMapView.Position = new Vector2(540f, 600f);
        AddChild(hexMapView);
        hexMapView.Initialize(_worldMap, _state, _inputLock);

        var result = _saveManager.Load();
        if (result.IsSuccess)
            Log.Debug("[Save]", $"Existing save found: phase={result.Value!.current_phase}");
        else
            Log.Debug("[Save]", $"No save: {result.Error}");
        Log.Debug("[UI]", $"Locale: {TranslationServer.Singleton.GetLocale()}");
        var handView = new HandView();
        handView.Name = "HandView";
        AddChild(handView);

        // Explicit test hand: Improvisation first so it's always verifiable on device.
        var allNonWound = _state.Cards.Where(c => c.Type != CardType.Wound).ToList();
        var improv = allNonWound.First(c => c.Id == "improvisation");
        var others = allNonWound.Where(c => c.Id != "improvisation").Take(3).ToList();
        var testHand = new List<CardDefinition> { improv };
        testHand.AddRange(others);
        testHand.Add(WoundCard.Create());
        _deckManager.SetHand(testHand);
        handView.Initialize(_deckManager, _state, _effectScheduler, _stagingManager, _inputLock);

        _restView = new RestView();
        _restView.Name = "RestView";
        AddChild(_restView);
        _restView.Initialize(_deckManager, _state, _inputLock);

        _improvView = new ImprovisationView();
        _improvView.Name = "ImprovisationView";
        AddChild(_improvView);
        _improvView.Initialize(_deckManager, _state, _stagingManager, _inputLock);
        handView.SetImprovisationView(_improvView);
    }

    private static WorldMap BuildStartingMap() {
        var tile = new MapTile(
            tileId: "starting",
            tileType: TileType.Starting,
            isRevealed: true,
            origin: new HexCoord(0, 0),
            hexes: new[] {
                (new HexCoord( 0,  0), TerrainType.Plains),
                (new HexCoord( 1,  0), TerrainType.Plains),
                (new HexCoord(-1,  0), TerrainType.Forest),
                (new HexCoord( 0,  1), TerrainType.Hills),
                (new HexCoord( 0, -1), TerrainType.Swamp),
                (new HexCoord( 1, -1), TerrainType.Plains),
                (new HexCoord(-1,  1), TerrainType.Wasteland),
            }
        );
        var map = new WorldMap(new HexCoord(0, 0));
        map.PlaceTile(tile);
        return map;
    }

    public override void _Notification(int what) {
        if (what == NotificationApplicationPaused)
            _saveManager.Save(_state);
    }

#if DEBUG
    private void OnDebugToggleAreaPressed() {
        _debugTapCount++;
        if (_debugTapCount < 5) return;
        _debugTapCount = 0;
        _effectInspector.Visible = !_effectInspector.Visible;
        if (_effectInspector.Visible)
            _effectInspector.RefreshDisplay();
    }
#endif
}
