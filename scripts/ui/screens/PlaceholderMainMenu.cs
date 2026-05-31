using System.Linq;
using Godot;
using MagusWarrior.Cards;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;
using MagusWarrior.Deck;
using MagusWarrior.Save;

namespace MagusWarrior.UI;

public partial class PlaceholderMainMenu : CanvasLayer {
    private GameState _state = null!;
    private SaveManager _saveManager = null!;
    private DeckManager _deckManager = null!;
#if DEBUG
    private EffectEventLogPanel _effectInspector = null!;
    private Button _debugToggleArea = null!;
    private int _debugTapCount = 0;
#endif

    public override void _Ready() {
        GD.Print("[BOOT] PlaceholderMainMenu._Ready entered");
        try {
            _state       = new GameState();
            _saveManager = new SaveManager();
            _deckManager = new DeckManager();
            GD.Print("[BOOT] All constructors succeeded");
        } catch (System.Exception ex) {
            GD.PrintErr("[BOOT] Constructor failed: " + ex.GetType().Name + ": " + ex.Message);
            GD.PrintErr("[BOOT] Stack: " + ex.StackTrace);
            return;
        }
#if DEBUG
        // Debug-only effect inspector. The panel and toggle nodes still exist in the
        // scene in release builds, but with no wiring they are unreachable. The undo
        // *mechanism* (GameEventLog/RestoreSnapshot) is non-conditional and ships in
        // release — player-facing undo (story 1b-4) builds on it. Only this dev
        // inspector and its GameDebug.UndoLastEvent wrapper are DEBUG-gated.
        _effectInspector = GetNode<EffectEventLogPanel>("EffectEventLogPanel");
        _debugToggleArea = GetNode<Button>("DebugToggleArea");
        _debugToggleArea.Pressed += OnDebugToggleAreaPressed;
        _effectInspector.Initialize(_state);
#endif

        var result = _saveManager.Load();
        if (result.IsSuccess)
            Log.Debug("[Save]", $"Existing save found: phase={result.Value!.current_phase}");
        else
            Log.Debug("[Save]", $"No save: {result.Error}");
        Log.Debug("[UI]", $"Locale: {TranslationServer.Singleton.GetLocale()}");
        GameDebug.FireTestEffect(_state, "march", GamePhase.Movement);

        var handView = GetNode<HandView>("HandView");
        var testHand = _state.Cards
            .Where(c => c.Type != CardType.Wound)
            .Take(4)
            .ToList();
        _deckManager.SetHand(testHand);
        handView.Initialize(_deckManager, _state);
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
