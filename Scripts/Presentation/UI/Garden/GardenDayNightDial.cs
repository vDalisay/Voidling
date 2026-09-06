using System;
using Godot;
using VoidlingGame;

namespace Voidling.Presentation.UI.Garden;

/// <summary>The premium weather frame and sun/moon insert, driven by the Garden's cosmetic clock.</summary>
public partial class GardenDayNightDial : Control
{
    private const string WeatherRoot = "res://Assets/Sprout Lands - UI Pack - Premium pack/emojis/emoji style ui/weather/";
    private AtlasTexture _currentIcon = null!;
    private Node2D _dayNightArrow = null!;
    private Label _period = null!;

    public override void _Ready()
    {
        Name = "GardenDayNightDial";
        Size = new Vector2(78, 66);
        MouseFilter = MouseFilterEnum.Stop;
        var art = new Control { Scale = new Vector2(0.75f, 0.75f), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(art);
        var sheet = GD.Load<Texture2D>(WeatherRoot + "Weather_UI.png");
        // Fill the frame's transparent windows first, then lay its premium border over them.
        _currentIcon = new AtlasTexture
        {
            Atlas = GD.Load<Texture2D>(WeatherRoot + "Weather_Icons_Big.png"),
            Region = new Rect2(0, 0, 48, 48)
        };
        art.AddChild(Sprite(_currentIcon, new Vector2(8, 8), new Vector2(48, 48)));
        art.AddChild(Sprite(new AtlasTexture { Atlas = sheet, Region = new Rect2(64, 96, 32, 48) },
            new Vector2(56, 8), new Vector2(32, 48)));
        art.AddChild(Sprite(new AtlasTexture { Atlas = sheet, Region = new Rect2(224, 0, 104, 88) },
            Vector2.Zero, new Vector2(104, 88)));
        _dayNightArrow = new Node2D { Name = "DayNightArrow", Position = new Vector2(55, 32) };
        var arrowArtwork = Sprite(
            new AtlasTexture { Atlas = sheet, Region = new Rect2(336, 32, 16, 16) },
            new Vector2(-2, -12), new Vector2(24, 24));
        arrowArtwork.Name = "ArrowArtwork";
        _dayNightArrow.AddChild(arrowArtwork);
        art.AddChild(_dayNightArrow);
        _period = UiFactory.CreateLabel(string.Empty, 11);
        _period.Position = new Vector2(4, 58.5f);
        _period.Size = new Vector2(74, 20);
        _period.HorizontalAlignment = HorizontalAlignment.Center;
        _period.VerticalAlignment = VerticalAlignment.Center;
        _period.MouseFilter = MouseFilterEnum.Ignore;
        art.AddChild(_period);
    }

    public void ShowTime(DateTime localTime)
    {
        var hour = localTime.TimeOfDay.TotalHours;
        // Keep the pointer on the same 20:00 night boundary as the moon icon.
        var daylight = hour < 5 || hour >= 20 ? 0.0
            : hour < 9 ? (hour - 5) / 4.0
            : hour < 18 ? 1.0
            : (20 - hour) / 2.0;
        _dayNightArrow.Rotation = Mathf.Lerp(-Mathf.Pi * 0.25f, Mathf.Pi * 0.25f, (float)(1.0 - daylight));
        _currentIcon.Region = new Rect2(hour < 5 || hour >= 20 ? 384 : 0, 0, 48, 48);
        _period.Text = localTime.ToShortTimeString();
        TooltipText = string.Format(Tr("UI_GARDEN_TIME_HINT"), _period.Text);
    }

    private static TextureRect Sprite(Texture2D texture, Vector2 position, Vector2 size)
        => new() { Texture = texture, Position = position, Size = size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore };
}
