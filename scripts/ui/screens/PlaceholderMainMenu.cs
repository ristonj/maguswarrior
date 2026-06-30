using System.Collections.Generic;
using System.Linq;
using Godot;
using MagusWarrior.Broker;
using MagusWarrior.Cards;
using MagusWarrior.Cards.Effects;
using MagusWarrior.Combat;
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
    private InputLock _inputLock = null!;
    private WorldMap _worldMap = null!;
    private TileStock _tileStock = null!;
    private TileCountView _tileCountView = null!;
    private RestView _restView = null!;
    private ImprovisationView _improvView = null!;
    private UIBroker _broker = null!;
    private CombatResolver _combatResolver = null!;
    private List<EnemyTokenDefinition> _enemyDefs = null!;
    private CombatInterstitialPanel _combatInterstitialPanel = null!;
    private RangedTargetingPanel _rangedTargetingPanel = null!;

    // First Reconnaissance scenario deck sizes (V-shape: 8 countryside + 3 core).
    // Hardcoded for now; real per-scenario configuration lands in Epic 7.
    private const int FirstReconCountrysideTiles = 8;
    private const int FirstReconCoreTiles = 3;
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
        _inputLock       = new InputLock();
        _worldMap        = BuildStartingMap();

        _tileStock = new TileStock(FirstReconCountrysideTiles, FirstReconCoreTiles);
        _worldMap.TileRevealed += tile => _tileStock.RecordReveal(tile.TileType);

#if DEBUG
        _effectInspector = new EffectEventLogPanel();
        _effectInspector.Name = "EffectEventLogPanel";
        AddChild(_effectInspector);
        _effectInspector.Initialize(_state);

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

        _tileCountView = new TileCountView();
        _tileCountView.Name = "TileCountView";
        AddChild(_tileCountView);
        _tileCountView.Initialize(_tileStock);

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
        handView.Initialize(_deckManager, _state, _effectScheduler, _inputLock, _worldMap);

        _restView = new RestView();
        _restView.Name = "RestView";
        AddChild(_restView);
        _restView.Initialize(_deckManager, _state, _inputLock);

        _improvView = new ImprovisationView();
        _improvView.Name = "ImprovisationView";
        AddChild(_improvView);
        _improvView.Initialize(_deckManager, _state, _effectScheduler, _inputLock);
        handView.SetImprovisationView(_improvView);

        // Combat system setup. Open with an explicit error check — GetFileAsString returns
        // "" on a failed open, which would deserialize to null and throw far from the cause.
        using (var enemiesFile = FileAccess.Open("res://data/enemies.yaml", FileAccess.ModeFlags.Read)) {
            if (enemiesFile == null)
                throw new System.IO.IOException(
                    $"res://data/enemies.yaml not found (Godot error {FileAccess.GetOpenError()})");
            _enemyDefs = EnemyLoader.ParseAll(enemiesFile.GetAsText());
        }

        _broker = new UIBroker();

        _combatInterstitialPanel = new CombatInterstitialPanel();
        _combatInterstitialPanel.Name = "CombatInterstitialPanel";
        AddChild(_combatInterstitialPanel);
        _broker.InterstitialProvider = c => _combatInterstitialPanel.ShowAndAwait(c);

        _rangedTargetingPanel = new RangedTargetingPanel();
        _rangedTargetingPanel.Name = "RangedTargetingPanel";
        AddChild(_rangedTargetingPanel);
        _broker.RangedAttackProvider = c => _rangedTargetingPanel.ShowAndAwait(c, _state);

        _combatResolver = new CombatResolver(_state, _broker, _effectScheduler, new EffectHookRegistry());

#if DEBUG
        var devCombatBtn = new Button();
        devCombatBtn.Name = "DevCombatBtn";
        devCombatBtn.Text = "Combat: Dev (Brown Token)";
        devCombatBtn.AddThemeFontSizeOverride("font_size", 24);
        devCombatBtn.Position = new Vector2(10f, 540f);
        devCombatBtn.Pressed += OnDevCombatPressed;
        AddChild(devCombatBtn);
#endif
    }

#if DEBUG
    private bool _combatInProgress;

    private async void OnDevCombatPressed() {
        if (!_inputLock.TryAcquire()) return;
        _inputLock.Release();   // release immediately — panels and HandView share the lock;
                                // holding it across ResolveCombat would block card plays during targeting

        // The lock is released immediately, so it cannot guard against a second press during
        // combat — _combatInProgress does. Without it, a re-press spawns a concurrent
        // ResolveCombat and a second interstitial over the first.
        if (_combatInProgress) return;

        var def = _enemyDefs.FirstOrDefault(e => e.Color == TokenColor.Brown);
        if (def == null) {
            Log.Error("[Combat]", "No Brown enemy tokens loaded — cannot start dev combat");
            return;
        }

        _combatInProgress = true;
        try {
            var group  = new CombatGroup { Enemies = new List<EnemyTokenInstance> { new(def) }, IsAtFortifiedSite = false };
            var combat = new CombatState { Group = group };

            _state.TripUndoGate();   // enemy token is being revealed — undo cannot go past this point

            var result = await _combatResolver.ResolveCombat(combat);

            Log.Debug("[Combat]", result.HeroWon
                ? $"Dev combat WON — {result.DefeatedEnemies.Count} token(s) defeated"
                : $"Dev combat LOST — {result.DefeatedEnemies.Count} token(s) defeated");
        } catch (System.Exception ex) {
            // async void swallows exceptions silently — catch and log so a resolver fault is visible.
            Log.Error("[Combat]", $"Dev combat threw: {ex.Message}");
        } finally {
            // TearDownCombatState left phase at EndOfTurn; return to Movement for continued dev play.
            _state.SetPhase(GamePhase.Movement);
            _combatInProgress = false;
        }
    }
#endif

    private static WorldMap BuildStartingMap() {
        var startingTile = new MapTile(
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

        // Countryside tile centered at (3,0). World hex (2,0) is adjacent to starting (1,0).
        // No world coords overlap with the starting tile (verified in story 2-4 spec).
        var countryside1 = new MapTile(
            tileId: "countryside-1",
            tileType: TileType.Countryside,
            isRevealed: false,
            origin: new HexCoord(3, 0),
            hexes: new[] {
                (new HexCoord( 0,  0), TerrainType.Plains),     // world (3,0)
                (new HexCoord( 1,  0), TerrainType.Hills),      // world (4,0)
                (new HexCoord(-1,  0), TerrainType.Forest),     // world (2,0) — adjacent to starting (1,0)
                (new HexCoord( 0,  1), TerrainType.Wasteland),  // world (3,1)
                (new HexCoord( 0, -1), TerrainType.Desert),     // world (3,-1)
                (new HexCoord( 1, -1), TerrainType.Plains),     // world (4,-1)
                (new HexCoord(-1,  1), TerrainType.Mountain),   // world (2,1)
            }
        );

        var map = new WorldMap(new HexCoord(0, 0));
        map.PlaceTile(startingTile);
        map.PlaceTile(countryside1);
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
