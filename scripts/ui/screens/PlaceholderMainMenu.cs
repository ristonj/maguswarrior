using System.Linq;
using Godot;
using MagusWarrior.Cards;
using MagusWarrior.Cards.Effects;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;
using MagusWarrior.Deck;
using MagusWarrior.Save;

namespace MagusWarrior.UI;

public partial class PlaceholderMainMenu : CanvasLayer {
    private GameState _state = null!;
    private SaveManager _saveManager = null!;
    private DeckManager _deckManager = null!;
    private EffectScheduler _effectScheduler = null!;
    private StagingManager _stagingManager = null!;
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

        var result = _saveManager.Load();
        if (result.IsSuccess)
            Log.Debug("[Save]", $"Existing save found: phase={result.Value!.current_phase}");
        else
            Log.Debug("[Save]", $"No save: {result.Error}");
        Log.Debug("[UI]", $"Locale: {TranslationServer.Singleton.GetLocale()}");
        GameDebug.FireTestEffect(_state, "march", GamePhase.Movement);

        var handView = new HandView();
        handView.Name = "HandView";
        AddChild(handView);

        var testHand = _state.Cards
            .Where(c => c.Type != CardType.Wound)
            .Take(4)
            .ToList();
        _deckManager.SetHand(testHand);
        handView.Initialize(_deckManager, _state, _effectScheduler, _stagingManager);
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
