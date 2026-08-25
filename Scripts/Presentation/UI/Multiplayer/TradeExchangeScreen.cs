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
    private static readonly Texture2D EggTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - Basic pack/Objects/Egg item.png");

    public event Action? ReturnRequested;

    private TradeExchangeScreenState? _state;
    private Control? _outgoingVisual;
    private Control? _incomingVisual;
    private Polygon2D _energy = null!;
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
        AddChild(new ColorRect
        {
            Color = Color.FromHtml("#A7D8C7"),
            Position = Vector2.Zero,
            Size = new Vector2(640, 360),
            MouseFilter = MouseFilterEnum.Ignore
        });

        var title = UiFactory.CreateTitle(Tr("UI_TRADE_EXCHANGE_TITLE"));
        title.Position = new Vector2(190, 18);
        title.Size = new Vector2(260, 26);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(title);

        AddStation(50, string.Format(Tr("UI_TRADE_EXCHANGE_YOURS"), _state.OutgoingCount));
        AddStation(430, string.Format(Tr("UI_TRADE_EXCHANGE_INCOMING"), _state.IncomingCount));

        var cable = new Line2D
        {
            Width = 4,
            DefaultColor = Color.FromHtml("#6C7F70"),
            Points = new[] { new Vector2(185, 174), new Vector2(455, 174) },
            ZIndex = 2
        };
        AddChild(cable);

        _energy = new Polygon2D
        {
            Polygon = BuildCircle(22, 20),
            Color = Color.FromHtml("#FFF2A8"),
            Position = new Vector2(320, 174),
            Scale = Vector2.Zero,
            ZIndex = 4
        };
        AddChild(_energy);

        // Show one representative per side; sequence every asset if batch trades become common.
        if (_state.Outgoing != null)
        {
            _outgoingVisual = CreateAssetVisual(_state.Outgoing);
            _outgoingVisual.Position = new Vector2(106, 124);
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

            _status.Text = Tr("UI_TRADE_EXCHANGE_SENDING");
            var sent = CreateTween().SetParallel(true);
            sent.TweenProperty(_outgoingVisual, "position", new Vector2(286, 124), 0.8)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            sent.TweenProperty(_outgoingVisual, "scale", new Vector2(0.15f, 0.15f), 0.8);
            sent.TweenProperty(_outgoingVisual, "modulate:a", 0.0f, 0.8);
            await ToSignal(sent, Tween.SignalName.Finished);
            if (!IsInsideTree())
                return;
        }

        _status.Text = Tr("UI_TRADE_EXCHANGE_LINKING");
        var flash = CreateTween();
        flash.TweenProperty(_energy, "scale", Vector2.One, 0.22)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        flash.TweenProperty(_energy, "scale", new Vector2(0.25f, 0.25f), 0.25);
        await ToSignal(flash, Tween.SignalName.Finished);
        if (!IsInsideTree())
            return;

        if (_incomingVisual == null || _state!.Incoming == null)
        {
            _return.Text = Tr("UI_RACE_RETURN");
            return;
        }

        _status.Text = string.Format(Tr("UI_TRADE_EXCHANGE_ARRIVED"), _state.Incoming.DisplayName);
        var arrived = CreateTween().SetParallel(true);
        arrived.TweenProperty(_incomingVisual, "position", new Vector2(466, 124), 0.9)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        arrived.TweenProperty(_incomingVisual, "scale", Vector2.One, 0.7)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        arrived.TweenProperty(_incomingVisual, "modulate:a", 1.0f, 0.35);
        await ToSignal(arrived, Tween.SignalName.Finished);
        if (!IsInsideTree())
            return;

        _return.Text = Tr("UI_RACE_RETURN");
    }

    private void AddStation(float x, string heading)
    {
        var panel = UiFactory.CreatePanel(new Vector2(160, 176));
        panel.Position = new Vector2(x, 76);
        panel.Size = new Vector2(160, 176);
        AddChild(panel);

        var label = UiFactory.CreateLabel(heading, 8);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        panel.AddChild(label);
    }

    private static Control CreateAssetVisual(TradeExchangeAssetView asset)
    {
        if (!asset.IsEgg)
        {
            var portrait = UiFactory.CreatePortrait(
                ParseTint(asset.TintHex),
                asset.HasAngelMutation,
                asset.OtherMutationCount,
                new Vector2(68, 68));
            portrait.Size = new Vector2(68, 68);
            return portrait;
        }

        return new TextureRect
        {
            Texture = EggTexture,
            Size = new Vector2(68, 68),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
    }

    private static Color ParseTint(string tintHex)
    {
        try { return Color.FromHtml(tintHex); }
        catch { return Color.FromHtml("#F6F0C9"); }
    }

    private static Vector2[] BuildCircle(float radius, int points)
    {
        var polygon = new Vector2[points];
        for (var i = 0; i < points; i++)
        {
            var angle = Mathf.Tau * i / points;
            polygon[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
        return polygon;
    }
}
