using System.Linq;
using Godot;
using MagusWarrior.Cards;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;
using MagusWarrior.Deck;

namespace MagusWarrior.UI;

public partial class RestView : Control {
    private DeckManager _deck = null!;
    private GameState _state = null!;

    private Button _declareRestButton = null!;
    private VBoxContainer _restPanel = null!;
    private Label _statusLabel = null!;
    private VBoxContainer _cardRows = null!;
    private Button _endRestButton = null!;

    private bool _nonWoundDiscarded;
    private bool _inExhaustion;

    public override void _Ready() {
        AnchorLeft = 0f;
        AnchorRight = 1f;
        AnchorTop = 0f;
        AnchorBottom = 0f;
        OffsetBottom = 160f;
        GrowHorizontal = GrowDirection.Both;
        GrowVertical = GrowDirection.End;

        var layout = new VBoxContainer();
        layout.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        layout.AddThemeConstantOverride("separation", 4);
        AddChild(layout);

        _declareRestButton = new Button();
        _declareRestButton.Text = "Declare Rest";
        _declareRestButton.AddThemeFontSizeOverride("font_size", 32);
        _declareRestButton.Pressed += OnDeclareRestPressed;
        layout.AddChild(_declareRestButton);

        _restPanel = new VBoxContainer();
        _restPanel.Visible = false;
        _restPanel.AddThemeConstantOverride("separation", 4);
        layout.AddChild(_restPanel);

        _statusLabel = new Label();
        _statusLabel.AddThemeFontSizeOverride("font_size", 26);
        _statusLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        _restPanel.AddChild(_statusLabel);

        _cardRows = new VBoxContainer();
        _cardRows.AddThemeConstantOverride("separation", 2);
        _restPanel.AddChild(_cardRows);

        _endRestButton = new Button();
        _endRestButton.Text = "End Rest";
        _endRestButton.AddThemeFontSizeOverride("font_size", 32);
        _endRestButton.Disabled = true;
        _endRestButton.Pressed += OnEndRestPressed;
        _restPanel.AddChild(_endRestButton);
    }

    public void Initialize(DeckManager deck, GameState state) {
        _deck = deck;
        _state = state;
        _deck.HandChanged += RefreshView;
        RefreshView();
    }

    private void OnDeclareRestPressed() {
        _state.SetPhase(GamePhase.Rest);

        if (RestRule.IsExhaustion(_deck.Hand)) {
            _inExhaustion = true;
            _nonWoundDiscarded = false;
            Log.Debug("[UI]", "Rest declared: Exhaustion");
            _statusLabel.Text = "Exhaustion rest — discarding 1 Wound…";
            _restPanel.Visible = true;
            _declareRestButton.Disabled = true;

            var r = _deck.DiscardCard("wound");
            if (r.IsSuccess) {
                _nonWoundDiscarded = true;
                _endRestButton.Disabled = false;
                _statusLabel.Text = "Exhaustion rest — Wound discarded";
                Log.Debug("[UI]", "Rest discard: wound (wound=True) [Exhaustion auto-discard]");
            } else {
                Log.Warn("[UI]", $"Exhaustion auto-discard failed: {r.Error}");
            }
        } else {
            _inExhaustion = false;
            _nonWoundDiscarded = false;
            Log.Debug("[UI]", "Rest declared: Standard Rest");
            _statusLabel.Text = "Standard Rest: discard 1 non-Wound card";
            _endRestButton.Disabled = true;
            _restPanel.Visible = true;
            _declareRestButton.Disabled = true;
            BuildDiscardRows();
        }
    }

    private void BuildDiscardRows() {
        foreach (Node child in _cardRows.GetChildren())
            child.QueueFree();

        foreach (var card in _deck.Hand) {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            _cardRows.AddChild(row);

            var nameLabel = new Label();
            nameLabel.Text = card.Name;
            nameLabel.AddThemeFontSizeOverride("font_size", 24);
            nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(nameLabel);

            var discardBtn = new Button();
            discardBtn.Text = "Discard";
            discardBtn.AddThemeFontSizeOverride("font_size", 24);

            bool isWound = card.Type == CardType.Wound;
            discardBtn.Disabled = isWound && !_nonWoundDiscarded;

            var capturedId = card.Id;
            var capturedType = card.Type;
            discardBtn.Pressed += () => OnDiscardPressed(capturedId, capturedType);

            row.AddChild(discardBtn);
        }
    }

    private void OnDiscardPressed(string cardId, CardType cardType) {
        var r = _deck.DiscardCard(cardId);
        if (!r.IsSuccess) {
            Log.Warn("[UI]", $"DiscardCard failed: {r.Error}");
            return;
        }

        Log.Debug("[UI]", $"Rest discard: {cardId} (wound={cardType == CardType.Wound})");

        if (cardType != CardType.Wound) {
            _nonWoundDiscarded = true;
            _endRestButton.Disabled = false;
            _statusLabel.Text = "Non-Wound discarded — optionally discard Wounds, then End Rest";
            // DiscardCard already fired HandChanged → BuildDiscardRows ran with the OLD flag
            // value, so the Wound rows were rebuilt disabled. Rebuild once more now that
            // _nonWoundDiscarded is set, otherwise the optional Wound discards stay disabled
            // forever (no further HandChanged would fire to re-enable them without illegally
            // discarding a second non-Wound).
            BuildDiscardRows();
        }
        // For a Wound discard, the HandChanged from DiscardCard already rebuilt the rows
        // correctly (the flag is already true by this point).
    }

    private void RefreshView() {
        if (_declareRestButton == null) return;

        bool inRest = _state != null && _state.CurrentPhase == GamePhase.Rest;
        bool canDeclare = _state != null && RestRule.CanDeclareRest(_deck.Hand);
        _declareRestButton.Disabled = inRest || !canDeclare;

        if (inRest && !_inExhaustion)
            BuildDiscardRows();
    }

    private void OnEndRestPressed() {
        _state.SetPhase(GamePhase.Movement);
        _nonWoundDiscarded = false;
        _inExhaustion = false;
        _restPanel.Visible = false;
        _endRestButton.Disabled = true;
        Log.Debug("[UI]", "Rest completed — phase returned to Movement");
        RefreshView();
    }
}
