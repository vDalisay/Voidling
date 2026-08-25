using System;
using Godot;
using VoidlingGame;

namespace Voidling.Presentation.UI.Multiplayer;

public sealed record TradeExchangeAssetView(
    string DisplayName,
    bool IsEgg,
    string TintHex,
    bool HasAngelMutation,
    int OtherMutationCount);

public sealed record TradeExchangeScreenState(
    TradeExchangeAssetView? Outgoing,
    TradeExchangeAssetView? Incoming,
    int OutgoingCount,
    int IncomingCount);

/// <summary>
/// Short non-authoritative presentation after a locally persisted multiplayer trade. Ownership has
/// already changed before this screen appears; animation can therefore be skipped without changing
/// game state or network behavior.
/// </summary>
public partial class TradeExchangeScreen : Control
{
    public event Action? ReturnRequested;

    private TradeExchangeScreenState? _state;
    private Control? _outgoingVisual;
    private Control? _incomingVisual;
    private Label _status = null!;
    private Button _return = null!;

    public void Configure(TradeExchangeScreenState state)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("TradeExchangeScreen must be configured before entering the scene tree.");
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public override void _Ready()
    {
        if (_state == null)
            throw new InvalidOperationException("TradeExchangeScreen must be configured before AddChild.");

        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var background = new ColorRect
        {
            Color = Color.FromHtml("#23352F"),
            MouseFilter = MouseFilterEnum.Stop
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var title = UiFactory.CreateTitle(Tr("UI_TRADE_EXCHANGE_TITLE"));
        title.Position = new Vector2(0, 28);
        title.Size = new Vector2(640, 34);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeColorOverride("font_color", Color.FromHtml("#F7E7B2"));
        AddChild(title);

        var sub = UiFactory.CreateLabel(
            string.Format(
                Tr("UI_TRADE_EXCHANGE_SUMMARY"),
                _state.OutgoingCount,
                _state.IncomingCount),
            8);
        sub.Position = new Vector2(0, 64);
        sub.Size = new Vector2(640, 22);
        sub.HorizontalAlignment = HorizontalAlignment.Center;
        sub.AddThemeColorOverride("font_color", Color.FromHtml("#D7E2C7"));
        AddChild(sub);

        var leftLabel = UiFactory.CreateLabel(Tr("UI_TRADE_EXCHANGE_YOU_SENT"), 7);
        leftLabel.Position = new Vector2(78, 103);
        leftLabel.Size = new Vector2(190, 18);
        leftLabel.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(leftLabel);

        var rightLabel = UiFactory.CreateLabel(Tr("UI_TRADE_EXCHANGE_YOU_RECEIVED"), 7);
        rightLabel.Position = new Vector2(372, 103);
        rightLabel.Size = new Vector2(190, 18);
        rightLabel.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(rightLabel);

        if (_state.Outgoing != null)
        {
            _outgoingVisual = CreateAssetVisual(_state.Outgoing);
            _outgoingVisual.Position = new Vector2(139, 124);
            _outgoingVisual.PivotOffset = new Vector2(34, 34);
            AddChild(_outgoingVisual);
        }

        if (_state.Incoming != null)
        {
            _incomingVisual = CreateAssetVisual(_state.Incoming);
            _incomingVisual.Position = new Vector2(286, 124);
            _incomingVisual.Scale = Vector2.Zero;
            _incomingVisual.Modulate = new Color(1, 1, 1, 0);
            _incomingVisual.ZIndex = 6;
            AddChild(_incomingVisual);
        }

        _status = UiFactory.CreateLabel(Tr("UI_TRADE_EXCHANGE_SENDING"), 9);
        _status.Position = new Vector2(110, 267);
        _status.Size = new Vector2(420, 22);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(_status);

        _return = UiFactory.CreateButton(Tr("UI_COMMON_SKIP"));
        _return.Position = new Vector2(250, 310);
        _return.Size = new Vector2(140, 28);
        _return.Pressed += () => ReturnRequested?.Invoke();
        AddChild(_return);

        PlayExchange();
    }

    private async void PlayExchange()
    {
        if (_outgoingVisual != null)
        {
            _status.Text = string.Format(
                Tr("UI_TRADE_EXCHANGE_GOODBYE"),
                _state!.Outgoing!.DisplayName);
            var farewell = CreateTween().SetLoops(4);
            farewell.TweenProperty(_outgoingVisual, "scale", new Vector2(1.08f, 1.08f), 0.25)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            farewell.TweenProperty(_outgoingVisual, "scale", Vector2.One, 0.25)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            await ToSignal(farewell, Tween.SignalName.Finished);
            if (!IsInsideTree())
                return;

            var leave = CreateTween().SetParallel(true);
            leave.TweenProperty(_outgoingVisual, "position:x", -110.0f, 0.62)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
            leave.TweenProperty(_outgoingVisual, "modulate:a", 0.0f, 0.42).SetDelay(0.18);
            await ToSignal(leave, Tween.SignalName.Finished);
            if (!IsInsideTree())
                return;
        }

        _status.Text = Tr("UI_TRADE_EXCHANGE_TRAVELLING");
        await ToSignal(GetTree().CreateTimer(0.55), SceneTreeTimer.SignalName.Timeout);
        if (!IsInsideTree())
            return;

        if (_incomingVisual != null)
        {
            _status.Text = string.Format(
                Tr("UI_TRADE_EXCHANGE_WELCOME"),
                _state!.Incoming!.DisplayName);
            var arrive = CreateTween().SetParallel(true);
            arrive.TweenProperty(_incomingVisual, "position:x", 433.0f, 0.62)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            arrive.TweenProperty(_incomingVisual, "scale", Vector2.One, 0.52)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            arrive.TweenProperty(_incomingVisual, "modulate:a", 1.0f, 0.28);
            await ToSignal(arrive, Tween.SignalName.Finished);
            if (!IsInsideTree())
                return;
        }

        _status.Text = Tr("UI_TRADE_EXCHANGE_COMPLETE");
        _return.Text = Tr("UI_COMMON_RETURN");
    }

    private static Control CreateAssetVisual(TradeExchangeAssetView asset)
    {
        var box = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(68, 116)
        };
        box.AddThemeConstantOverride("separation", 4);

        Control visual;
        if (asset.IsEgg)
        {
            var label = UiFactory.CreateLabel("🥚", 32);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.CustomMinimumSize = new Vector2(68, 68);
            visual = label;
        }
        else
        {
            visual = UiFactory.CreatePortrait(
                UiFactory.ParseTint(asset.TintHex),
                asset.HasAngelMutation,
                asset.OtherMutationCount,
                new Vector2(68, 68));
        }
        box.AddChild(visual);

        var name = UiFactory.CreateLabel(asset.DisplayName, 8);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.CustomMinimumSize = new Vector2(68, 22);
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        box.AddChild(name);
        return box;
    }
}
