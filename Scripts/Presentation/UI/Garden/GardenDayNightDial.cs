using System;
using Godot;
using Voidling.Presentation.UI.Motion;
using VoidlingGame;

namespace Voidling.Presentation.UI.Garden;

/// <summary>
/// The premium pack's small weather frame at its own pixel size: the Garden's weather in the left
/// window, the sun/moon insert in the right with the pack's pointer arrow on whichever half the
/// hour belongs to, and the local time on the plate hanging underneath. Driven by the Garden's
/// cosmetic clock and weather; nothing here is read by game rules.
/// </summary>
public partial class GardenDayNightDial : Control
{
    private const string WeatherRoot = "res://Assets/Sprout Lands - UI Pack - Premium pack/emojis/emoji style ui/weather/";

    /// <summary>Where the pointer sits, inside the frame's notch beside the sun/moon insert.</summary>
    public static readonly Vector2 ArrowPivot = new(34, 16);

    /// <summary>The time plate's centre in dial pixels.</summary>
    public static readonly Vector2 TimePlateCenter = new(24, 44);

    /// <summary>The pack's three pointer frames: at the sun, level, and at the moon.</summary>
    public static readonly Rect2 DayArrow = new(340, 84, 9, 9);
    public static readonly Rect2 DuskArrow = new(340, 101, 10, 8);
    public static readonly Rect2 NightArrow = new(340, 116, 9, 9);

    private AtlasTexture _weatherIcon = null!;
    private AtlasTexture _arrowTexture = null!;
    private TextureRect _arrowArtwork = null!;
    private Node2D _dayNightArrow = null!;
    private Label _period = null!;
    private Control _art = null!;
    private double _hour = 12;
    private (float Cloud, float Rain, float Storm) _weather;
    private Rect2 _shownIcon;

    public override void _Ready()
    {
        Name = "GardenDayNightDial";
        Size = new Vector2(65, 54);
        MouseFilter = MouseFilterEnum.Stop;
        _art = new Control { Name = "DialArt", MouseFilter = MouseFilterEnum.Ignore, Size = Size };
        AddChild(_art);
        var sheet = GD.Load<Texture2D>(WeatherRoot + "Weather_UI.png");
        // Fill the frame's windows first, then lay its premium border over them.
        _weatherIcon = new AtlasTexture
        {
            Atlas = GD.Load<Texture2D>(WeatherRoot + "Weather_Icons_small.png"),
            Region = IconRegion(0, 0)
        };
        _art.AddChild(Sprite(_weatherIcon, new Vector2(9, 8), new Vector2(18, 17)));
        _art.AddChild(Sprite(new AtlasTexture { Atlas = sheet, Region = new Rect2(369, 119, 13, 17) },
            new Vector2(36, 8), new Vector2(13, 17)));
        _art.AddChild(Sprite(new AtlasTexture { Atlas = sheet, Region = new Rect2(358, 55, 65, 54) },
            Vector2.Zero, new Vector2(65, 54)));

        _dayNightArrow = new Node2D { Name = "DayNightArrow", Position = ArrowPivot };
        _arrowTexture = new AtlasTexture { Atlas = sheet, Region = DayArrow };
        _arrowArtwork = Sprite(_arrowTexture, Vector2.Zero, DayArrow.Size);
        _arrowArtwork.Name = "ArrowArtwork";
        _dayNightArrow.AddChild(_arrowArtwork);
        _art.AddChild(_dayNightArrow);

        _period = UiFactory.CreateLabel(string.Empty, 8);
        // Clipped rather than grown, so a long clock format never pushes the time off its plate.
        _period.ClipText = true;
        _period.Position = new Vector2(0, 36);
        _period.Size = new Vector2(48, 16);
        _period.HorizontalAlignment = HorizontalAlignment.Center;
        _period.VerticalAlignment = VerticalAlignment.Center;
        _period.MouseFilter = MouseFilterEnum.Ignore;
        _period.AddThemeColorOverride("font_color", Color.FromHtml("#6B4A31"));
        _art.AddChild(_period);
        // However tall the font's line box is, the text stays centred on the plate.
        _period.Resized += CenterPeriod;
        CenterPeriod();
        PlaceArrow(DayArrow);
        Resized += () => UiMotion.CenterPivot(this);
    }

    public void ShowTime(DateTime localTime)
    {
        _hour = localTime.TimeOfDay.TotalHours;
        var daylight = Daylight(_hour);
        PlaceArrow(daylight >= 0.75 ? DayArrow : daylight <= 0.25 ? NightArrow : DuskArrow);
        _period.Text = localTime.ToShortTimeString();
        TooltipText = string.Format(Tr("UI_GARDEN_TIME_HINT"), _period.Text);
        RefreshIcon();
    }

    /// <summary>The Garden's cosmetic weather, shown in the left window.</summary>
    public void ShowWeather(float cloud, float rain, float storm)
    {
        _weather = (cloud, rain, storm);
        RefreshIcon();
    }

    /// <summary>1 by day, 0 at night, easing through dawn (5-9) and dusk (18-20).</summary>
    public static double Daylight(double hour)
        => hour < 5 || hour >= 20 ? 0.0
            : hour < 9 ? (hour - 5) / 4.0
            : hour < 18 ? 1.0
            : (20 - hour) / 2.0;

    private void CenterPeriod()
        => _period.Position = (TimePlateCenter - _period.Size * 0.5f).Round();

    private void PlaceArrow(Rect2 frame)
    {
        _arrowTexture.Region = frame;
        _arrowArtwork.Size = frame.Size;
        // Up-right and down-right lean towards the sun or the moon; level sits in the middle.
        _arrowArtwork.Position = frame == DayArrow ? new Vector2(-4, -7)
            : frame == NightArrow ? new Vector2(-4, 0)
            : new Vector2(-4, -4);
    }

    private void RefreshIcon()
    {
        var night = Daylight(_hour) <= 0.0;
        var twilight = !night && (_hour < 8 || _hour >= 18);
        var (cloud, rain, storm) = _weather;
        // Column/row on the pack's small weather sheet: day row 0, night row 1, dusk row 2.
        var region = storm > 0.5f ? IconRegion(3, 1)
            : rain > 0.5f ? (night ? IconRegion(2, 1) : twilight ? IconRegion(2, 2) : IconRegion(3, 0))
            : cloud > 0.55f ? (night ? IconRegion(1, 1) : twilight ? IconRegion(1, 2) : IconRegion(4, 1))
            : night ? IconRegion(0, 1) : twilight ? IconRegion(0, 2) : IconRegion(0, 0);
        if (region == _shownIcon) return;
        var changed = _shownIcon.Size != Vector2.Zero;
        _shownIcon = region;
        _weatherIcon.Region = region;
        if (changed) UiMotion.Pop(this, 0.12f, UiMotion.Slow);
    }

    private static Rect2 IconRegion(int column, int row) => new(7 + column * 32, 7 + row * 32, 18, 17);

    private static TextureRect Sprite(Texture2D texture, Vector2 position, Vector2 size)
        => new() { Texture = texture, Position = position, Size = size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore };
}
