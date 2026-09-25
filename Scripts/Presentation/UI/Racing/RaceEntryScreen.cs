using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Presentation.Racing;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Motion;
using Voidling.Presentation.Voidlings;
using VoidlingGame;

namespace Voidling.Presentation.UI.Racing;

public readonly record struct RacePickerStatViewState(
    string StatId,
    string Name,
    Color Color,
    string Rank,
    int Level,
    float Progress,
    int Value);

public readonly record struct RacePickerVoidlingViewState(
    string Id,
    string Name,
    VoidlingVisualAppearance Appearance,
    bool HasAngelMutation,
    int OtherMutationCount,
    string StatSummary,
    IReadOnlyList<RacePickerStatViewState> Stats);

public readonly record struct RacePickerCourseViewState(
    string Id,
    int Version,
    string Name,
    string Summary,
    IReadOnlyList<string> Sections,
    int LengthMeters,
    float StartX,
    float EndX,
    IReadOnlyList<CourseMinimapSegment> Segments,
    IReadOnlyList<float> Obstacles);

/// <summary>Best local finish for one course/level, or a blank time when nothing is recorded yet.</summary>
public readonly record struct RaceCourseRecordViewState(string CreatureName, string Time);

public sealed record RacePickerScreenState(
    IReadOnlyList<RacePickerVoidlingViewState> Voidlings,
    string SelectedId,
    IReadOnlyList<RacePickerCourseViewState> Courses,
    string SelectedCourseId,
    int SelectedCourseVersion,
    int SelectedLevel,
    IReadOnlyDictionary<string, RaceCourseRecordViewState> Records);

public enum RaceEntryStep
{
    Course,
    Racer,
    Confirm
}

/// <summary>
/// Full-screen race entry: course and difficulty, then racer, then a final confirmation. Each step
/// owns the whole screen so the choice being made is the only thing on it. Appearance stays semantic
/// until the shared visual factory composes portraits, and course choice emits stable IDs/versions.
/// </summary>
public partial class RaceEntryScreen : Control
{
    public const int MinLevel = 1;
    public const int MaxLevel = 3;
    private const int RacerColumns = 3;
    private const int RacerRows = 3;
    private const int RacersPerPage = RacerColumns * RacerRows;

    private static readonly Texture2D RoundButtons = GD.Load<Texture2D>(
        UiFactory.UiRoot + "buttons/round/medium colored round buttons.png");
    private static readonly Texture2D WoodStars = GD.Load<Texture2D>(
        UiFactory.UiRoot + "Icons/special icons/stars in wood.png");

    // Difficulty reads as colour before it reads as a number: calm green, warm yellow, hot red.
    private static readonly int[] LevelColorRows = { 7, 9, 11 };

    /// <summary>Creature ID, course ID, course version, difficulty level.</summary>
    public event Action<string, string, int, int>? RaceRequested;

    /// <summary>Raised when the first step is backed out of, so the host can close the screen.</summary>
    public event Action? Dismissed;

    public event Action<string, string, int, int>? SelectionChanged;

    private RacePickerScreenState? _state;
    private RaceEntryStep _step = RaceEntryStep.Course;
    private bool _courseLocked;
    private string _selectedId = string.Empty;
    private string _selectedCourseId = string.Empty;
    private int _selectedCourseVersion;
    private int _level = MinLevel;
    private int _racerPage;

    private Control _body = null!;
    private Label _stepTitle = null!;
    private Button _primary = null!;
    private Button _back = null!;

    public RaceEntryStep Step => _step;

    public void Configure(RacePickerScreenState state, RaceEntryStep startStep = RaceEntryStep.Course, bool courseLocked = false)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("RaceEntryScreen must be configured before it enters the scene tree.");
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _step = startStep;
        _courseLocked = courseLocked;
    }

    public override void _Ready()
    {
        if (_state == null)
            throw new InvalidOperationException("RaceEntryScreen must be configured before AddChild.");

        Name = "RaceEntry";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        var column = new VBoxContainer();
        column.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        column.AddThemeConstantOverride("separation", 5);
        AddChild(column);

        // The step's name on a golden tag, the same shape as every window's title tag.
        var banner = new PanelContainer
        {
            Name = "StepBanner", SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            CustomMinimumSize = new Vector2(0, UiSkin.TitleTagHeight)
        };
        var bannerStyle = UiSkin.TitleTag();
        bannerStyle.ModulateColor = new Color(1.06f, 0.9f, 0.6f);
        banner.AddThemeStyleboxOverride("panel", bannerStyle);
        var bannerRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        bannerRow.AddThemeConstantOverride("separation", 6);
        bannerRow.AddChild(new TextureRect
        {
            Texture = UiFactory.CreateGardenIcon(13, 1),
            CustomMinimumSize = new Vector2(16, 16),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        });
        _stepTitle = UiFactory.CreateLabel(string.Empty, 10);
        _stepTitle.AddThemeColorOverride("font_color", Color.FromHtml("#4A3218"));
        bannerRow.AddChild(_stepTitle);
        banner.AddChild(bannerRow);
        column.AddChild(banner);

        var bodyHolder = new CenterContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddChild(bodyHolder);
        _body = bodyHolder;

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 6);
        _back = UiFactory.CreateButton(Tr("UI_COMMON_BACK"));
        _back.Name = "EntryBack";
        _back.CustomMinimumSize = new Vector2(70, 26);
        _back.Pressed += GoBack;
        footer.AddChild(_back);
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _primary = UiFactory.CreateButton(string.Empty);
        _primary.Name = "EntryPrimary";
        _primary.CustomMinimumSize = new Vector2(180, 30);
        UiFactory.ApplyPrimaryStyle(_primary);
        _primary.Icon = UiFactory.CreateGardenIcon(13, 1);
        _primary.AddThemeConstantOverride("icon_max_width", 16);
        _primary.Pressed += GoForward;
        footer.AddChild(_primary);
        column.AddChild(footer);

        if (_state.Voidlings.Count == 0 || _state.Courses.Count == 0)
        {
            _stepTitle.Text = Tr(_state.Voidlings.Count == 0 ? "UI_RACE_PICKER_EMPTY" : "UI_RACE_PICKER_NO_COURSES");
            _primary.Visible = false;
            return;
        }

        _selectedId = _state.Voidlings.Any(v => v.Id == _state.SelectedId) ? _state.SelectedId : _state.Voidlings[0].Id;
        var course = _state.Courses.FirstOrDefault(c =>
            c.Id == _state.SelectedCourseId && c.Version == _state.SelectedCourseVersion);
        if (string.IsNullOrWhiteSpace(course.Id)) course = _state.Courses[0];
        _selectedCourseId = course.Id;
        _selectedCourseVersion = course.Version;
        _level = Mathf.Clamp(_state.SelectedLevel, MinLevel, MaxLevel);
        _racerPage = Math.Max(0, _state.Voidlings.ToList().FindIndex(v => v.Id == _selectedId)) / RacersPerPage;

        RebuildStep();
    }

    public void FocusSelection()
    {
        var focusable = _body.FindChildren("*", "Button", true, false).OfType<Button>()
            .FirstOrDefault(button => !button.IsQueuedForDeletion() && button.IsVisibleInTree() && button.ButtonPressed);
        (focusable ?? _primary).GrabFocus();
    }

    private void GoBack()
    {
        if (_step == RaceEntryStep.Confirm && !_courseLocked) SetStep(RaceEntryStep.Racer);
        else if (_step == RaceEntryStep.Racer && !_courseLocked) SetStep(RaceEntryStep.Course);
        else Dismissed?.Invoke();
    }

    private void GoForward()
    {
        switch (_step)
        {
            case RaceEntryStep.Course:
                SetStep(RaceEntryStep.Racer);
                break;
            case RaceEntryStep.Racer when _courseLocked:
                RaiseRace();
                break;
            case RaceEntryStep.Racer:
                SetStep(RaceEntryStep.Confirm);
                break;
            default:
                RaiseRace();
                break;
        }
    }

    private void RaiseRace()
        => RaceRequested?.Invoke(_selectedId, _selectedCourseId, _selectedCourseVersion, _level);

    private void SetStep(RaceEntryStep step)
    {
        _step = step;
        RebuildStep();
        FocusSelection();
    }

    private void RebuildStep()
    {
        var stepChanged = _shownStep != _step;
        _shownStep = _step;
        Clear(_body);
        switch (_step)
        {
            case RaceEntryStep.Course:
                _stepTitle.Text = Tr("UI_RACE_STEP_COURSE");
                _primary.Text = Tr("UI_RACE_CHOOSE_RACER");
                _body.AddChild(BuildCourseStep());
                break;
            case RaceEntryStep.Racer:
                _stepTitle.Text = Tr("UI_RACE_STEP_RACER");
                _primary.Text = Tr(_courseLocked ? "UI_RACE_START" : "UI_RACE_ENTER");
                _body.AddChild(BuildRacerStep());
                break;
            default:
                _stepTitle.Text = Tr("UI_RACE_STEP_CONFIRM");
                _primary.Text = Tr("UI_RACE_START");
                _body.AddChild(BuildConfirmStep());
                break;
        }
        if (!stepChanged || !IsInsideTree()) return;
        // A new step deals its cards in and the banner re-stamps; picks within a step do not.
        UiMotion.Pop(_stepTitle.GetParent().GetParent<Control>(), 0.1f, UiMotion.Slow);
        var cards = new List<CanvasItem>();
        foreach (var node in _body.FindChildren("*", "PanelContainer", true, false))
            if (node is PanelContainer card && card.Name.ToString() is "CourseListPanel" or "CourseRecord" or "RosterPanel" or "RacerStats" or "ConfirmCourse")
                cards.Add(card);
        UiMotion.StaggerIn(cards, 0.06f, 0.2f, 0.05f);
    }

    private RaceEntryStep? _shownStep;

    // ---- Step 1: course and difficulty ------------------------------------------------------

    // The Chao Stadium's race board: the courses as cards down the left, each showing the kinds of
    // stretch it has and its level medals, and on the right a live window onto the picked course
    // with its profile and record underneath.
    private Control BuildCourseStep()
    {
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        row.AddThemeConstantOverride("separation", 8);

        var listPanel = PaperPanel(new Vector2(250, 0));
        listPanel.Name = "CourseListPanel";
        listPanel.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        var left = new VBoxContainer();
        left.AddThemeConstantOverride("separation", 4);
        listPanel.AddChild(left);

        var headers = new HBoxContainer();
        headers.AddThemeConstantOverride("separation", 4);
        headers.AddChild(Header(Tr("UI_RACE_PICKER_COURSE"), 150));
        headers.AddChild(Header(Tr("UI_RACE_LEVEL"), 0));
        left.AddChild(headers);

        var list = new VBoxContainer { Name = "CourseList" };
        list.AddThemeConstantOverride("separation", 5);
        foreach (var course in _state!.Courses)
            list.AddChild(BuildCourseRow(course));
        left.AddChild(list);
        row.AddChild(listPanel);

        row.AddChild(BuildRecordPanel());
        return row;
    }

    private Control BuildCourseRow(RacePickerCourseViewState course)
    {
        var selected = CourseKey(course) == CourseKey(_selectedCourseId, _selectedCourseVersion);

        // The picked card glows gold, so the course the board is describing is obvious at a glance.
        var card = new PanelContainer { Name = "CourseCard_" + course.Id };
        card.AddThemeStyleboxOverride("panel", CardStyle(selected));
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 3);
        card.AddChild(box);

        var name = UiFactory.CreateButton(course.Name);
        name.Name = "Course_" + course.Id;
        name.ToggleMode = true;
        name.ButtonPressed = selected;
        name.CustomMinimumSize = new Vector2(226, 24);
        name.Alignment = HorizontalAlignment.Left;
        UiFactory.ApplyPixelFont(name, 8);
        name.Pressed += () =>
        {
            _selectedCourseId = course.Id;
            _selectedCourseVersion = course.Version;
            RaiseSelectionChanged();
            RebuildStep();
        };
        box.AddChild(name);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 3);
        row.AddChild(SectionChips(course, 12.0f));
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });

        for (var level = MinLevel; level <= MaxLevel; level++)
        {
            var captured = level;
            var pill = BuildLevelPill(course, captured, selected && captured == _level);
            pill.Pressed += () =>
            {
                _selectedCourseId = course.Id;
                _selectedCourseVersion = course.Version;
                _level = captured;
                RaiseSelectionChanged();
                RebuildStep();
            };
            row.AddChild(pill);
        }
        box.AddChild(row);
        return card;
    }

    // Difficulty pills reuse the premium round-button sheet: tan for an unpicked level, and green,
    // yellow or red for the picked one, so the row reads at a glance without any extra wording.
    private Button BuildLevelPill(RacePickerCourseViewState course, int level, bool active)
    {
        var button = new Button
        {
            Name = $"Level_{course.Id}_{level}",
            Text = level.ToString(),
            ToggleMode = true,
            ButtonPressed = active,
            CustomMinimumSize = new Vector2(26, 26),
            FocusMode = FocusModeEnum.All
        };
        var colorRow = active ? LevelColorRows[Mathf.Clamp(level, MinLevel, MaxLevel) - 1] : 1;
        foreach (var (state, column) in new[] { ("normal", 0), ("hover", 1), ("pressed", 2), ("hover_pressed", 2), ("focus", 1) })
        {
            button.AddThemeStyleboxOverride(state, new StyleBoxTexture
            {
                Texture = new AtlasTexture
                {
                    Atlas = RoundButtons,
                    Region = new Rect2(column * 32, colorRow * 32, 32, 32)
                },
                ContentMarginLeft = 0, ContentMarginRight = 0,
                ContentMarginTop = 0, ContentMarginBottom = 0
            });
        }
        button.AddThemeFontOverride("font", UiSkin.NumberFont);
        button.AddThemeFontSizeOverride("font_size", UiSkin.NumberFontSize);
        button.AddThemeColorOverride("font_color", Color.FromHtml(active ? "#3A2C18" : "#7A6650"));
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Colors.White);
        ButtonJuice.Attach(button, ButtonFeel.Soft);
        return button;
    }

    private Control BuildRecordPanel()
    {
        var panel = PaperPanel(new Vector2(334, 0));
        panel.Name = "CourseRecord";
        panel.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        panel.AddChild(box);

        var course = SelectedCourse();
        box.AddChild(BuildPreviewFrame(course, new Vector2(318, 100)));

        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", 6);
        var title = UiFactory.CreateLabel(course.Name, 10);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.VerticalAlignment = VerticalAlignment.Center;
        title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        titleRow.AddChild(title);
        titleRow.AddChild(PaperCard.StarRating(_level, MaxLevel, 16.0f));
        box.AddChild(titleRow);

        box.AddChild(BuildMinimap(course, new Vector2(318, 38)));
        box.AddChild(BuildCourseDetail(course));
        box.AddChild(BuildRecordPlaque(course));
        return panel;
    }

    /// <summary>
    /// The record as a wooden plaque: trophy, holder and time on one line. The time keeps its node
    /// name, which the Garden UI smoke reads.
    /// </summary>
    private Control BuildRecordPlaque(RacePickerCourseViewState course)
    {
        var plaque = new PanelContainer { Name = "RecordPlaque" };
        var style = UiSkin.Well();
        style.ModulateColor = new Color(1.05f, 0.96f, 0.78f);
        style.ContentMarginTop = 3;
        style.ContentMarginBottom = 4;
        style.ContentMarginLeft = 6;
        style.ContentMarginRight = 8;
        plaque.AddThemeStyleboxOverride("panel", style);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        plaque.AddChild(row);
        row.AddChild(new TextureRect
        {
            Texture = UiFactory.CreateGardenIcon(13, 1),
            CustomMinimumSize = new Vector2(14, 14),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = MouseFilterEnum.Ignore
        });
        var heading = Header(Tr("UI_RACE_COURSE_RECORD"), 0);
        heading.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(heading);

        var record = Record(course, _level);
        var holder = UiFactory.CreateLabel(
            string.IsNullOrEmpty(record.CreatureName) ? Tr("UI_RACE_NO_RECORD") : record.CreatureName, 8);
        holder.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        holder.HorizontalAlignment = HorizontalAlignment.Right;
        holder.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        row.AddChild(holder);
        var time = UiFactory.CreateLabel(record.Time, 10);
        time.Name = "RecordTime";
        time.AddThemeColorOverride("font_color", Color.FromHtml("#4A3218"));
        row.AddChild(time);
        return plaque;
    }

    // ---- Step 2: racer ----------------------------------------------------------------------

    private Control BuildRacerStep()
    {
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        row.AddThemeConstantOverride("separation", 8);

        var rosterPanel = PaperPanel(Vector2.Zero);
        rosterPanel.Name = "RosterPanel";
        var rosterBox = new VBoxContainer();
        rosterBox.AddThemeConstantOverride("separation", 3);
        rosterPanel.AddChild(rosterBox);

        var roster = _state!.Voidlings
            .Select(racer => new RosterEntry(
                racer.Id, racer.Name, racer.Appearance, racer.HasAngelMutation, racer.OtherMutationCount))
            .ToArray();
        rosterBox.AddChild(VoidlingRosterGrid.Build(
            roster,
            _racerPage,
            entry => entry.Id == _selectedId,
            _ => string.Empty,
            entry =>
            {
                _selectedId = entry.Id;
                RaiseSelectionChanged();
                RebuildStep();
            },
            page => { _racerPage = page; RebuildStep(); },
            out var pages));
        _racerPage = Mathf.Clamp(_racerPage, 0, pages - 1);

        if (pages > 1)
        {
            var pageLabel = UiFactory.CreateLabel(string.Format(Tr("UI_RACE_ROSTER_PAGE"), _racerPage + 1, pages), 6);
            pageLabel.HorizontalAlignment = HorizontalAlignment.Center;
            rosterBox.AddChild(pageLabel);
        }

        row.AddChild(rosterPanel);
        row.AddChild(BuildStatPanel(SelectedRacer(), 246));
        return row;
    }

    private Control BuildStatPanel(RacePickerVoidlingViewState racer, float width)
    {
        var panel = PaperPanel(new Vector2(width, 0));
        panel.Name = "RacerStats";
        panel.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 3);
        panel.AddChild(box);

        var heading = new HBoxContainer();
        heading.AddThemeConstantOverride("separation", 5);
        var portrait = UiFactory.CreatePortrait(
            racer.Appearance, racer.HasAngelMutation, racer.OtherMutationCount, new Vector2(38, 38));
        heading.AddChild(portrait);
        var name = UiFactory.CreateLabel(racer.Name, 10);
        name.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        heading.AddChild(name);
        box.AddChild(heading);

        foreach (var stat in racer.Stats)
            box.AddChild(BuildStatRow(stat));
        return panel;
    }

    private Control BuildStatRow(RacePickerStatViewState stat)
    {
        var block = new VBoxContainer();
        block.AddThemeConstantOverride("separation", 0);
        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 4);
        var name = UiFactory.CreateLabel(stat.Name, 7);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        name.AddThemeColorOverride("font_color", PaperInk(stat.Color));
        top.AddChild(name);
        var level = UiFactory.CreateLabel(string.Format(Tr("UI_PROFILE_LEVEL_VALUE"), stat.Level), 7);
        level.CustomMinimumSize = new Vector2(38, 0);
        top.AddChild(level);
        var rank = UiFactory.CreateLabel(stat.Rank, 7);
        rank.CustomMinimumSize = new Vector2(16, 0);
        top.AddChild(rank);
        var value = UiFactory.CreateLabel(stat.Value.ToString("0000"), 7);
        value.HorizontalAlignment = HorizontalAlignment.Right;
        value.CustomMinimumSize = new Vector2(34, 0);
        top.AddChild(value);
        block.AddChild(top);

        var bar = new ProgressBar
        {
            Name = "Progress_" + stat.StatId,
            MinValue = 0,
            MaxValue = 1,
            Value = stat.Progress,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 7)
        };
        var background = new StyleBoxFlat
        {
            BgColor = UiPalette.Beige,
            BorderColor = UiPalette.Tan,
            AntiAliasing = false
        };
        background.SetBorderWidthAll(1);
        var fill = new StyleBoxFlat
        {
            BgColor = PaperInk(stat.Color), AntiAliasing = false,
            BorderColor = PaperInk(stat.Color).Lightened(0.3f), BorderWidthTop = 1
        };
        bar.AddThemeStyleboxOverride("background", background);
        bar.AddThemeStyleboxOverride("fill", fill);
        block.AddChild(bar);
        return block;
    }

    // ---- Step 3: confirmation ---------------------------------------------------------------

    private Control BuildConfirmStep()
    {
        var course = SelectedCourse();
        var racer = SelectedRacer();

        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        row.AddThemeConstantOverride("separation", 8);

        var coursePanel = PaperPanel(new Vector2(330, 0));
        coursePanel.Name = "ConfirmCourse";
        coursePanel.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        var courseBox = new VBoxContainer();
        courseBox.AddThemeConstantOverride("separation", 4);
        coursePanel.AddChild(courseBox);
        courseBox.AddChild(BuildPreviewFrame(course, new Vector2(314, 96)));

        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", 6);
        var courseName = UiFactory.CreateLabel(course.Name, 11);
        courseName.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        courseName.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        titleRow.AddChild(courseName);
        var levelLine = UiFactory.CreateLabel(string.Format(Tr("UI_RACE_LEVEL_VALUE"), _level), 8);
        levelLine.VerticalAlignment = VerticalAlignment.Center;
        titleRow.AddChild(levelLine);
        titleRow.AddChild(BuildStarRating(_level));
        courseBox.AddChild(titleRow);

        courseBox.AddChild(BuildMinimap(course, new Vector2(314, 36)));
        courseBox.AddChild(BuildCourseDetail(course));
        var record = Record(course, _level);
        var recordLine = UiFactory.CreateLabel(
            string.Format(Tr("UI_RACE_RECORD_LINE"),
                string.IsNullOrEmpty(record.CreatureName) ? Tr("UI_RACE_NO_RECORD") : record.CreatureName,
                record.Time), 6);
        recordLine.HorizontalAlignment = HorizontalAlignment.Center;
        courseBox.AddChild(recordLine);
        row.AddChild(coursePanel);

        row.AddChild(BuildStatPanel(racer, 246));
        return row;
    }

    // ---- Shared ------------------------------------------------------------------------------

    /// <summary>
    /// The live course preview in a dark timber frame, like a window onto the stadium. Courses the
    /// catalog cannot resolve simply show no window; the profile strip below still describes them.
    /// </summary>
    private static Control BuildPreviewFrame(RacePickerCourseViewState course, Vector2 size)
    {
        var frame = new PanelContainer { Name = "CoursePreviewFrame", MouseFilter = MouseFilterEnum.Ignore };
        // Dark timber around the window: square pixel edges with a lit inner rim.
        var style = new StyleBoxFlat
        {
            BgColor = Color.FromHtml("#9CCB6B"), BorderColor = Color.FromHtml("#6B4A31"), AntiAliasing = false,
            ShadowColor = new Color(0.25f, 0.16f, 0.1f, 0.35f), ShadowSize = 0, ShadowOffset = new Vector2(0, 2)
        };
        style.SetBorderWidthAll(3);
        style.SetContentMarginAll(3);
        frame.AddThemeStyleboxOverride("panel", style);

        var preview = new CoursePreview { Name = "CoursePreview", CustomMinimumSize = size };
        if (preview.SetCourse(course.Id, course.Version))
        {
            frame.AddChild(preview);
        }
        else
        {
            preview.QueueFree();
            frame.Visible = false;
        }
        return frame;
    }

    /// <summary>Section chips followed by the section names and course length.</summary>
    private static Control BuildCourseDetail(RacePickerCourseViewState course)
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(SectionChips(course, 10.0f));
        var detail = UiFactory.CreateLabel($"{string.Join(" · ", course.Sections)}   {course.LengthMeters} M", 6);
        detail.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(detail);
        return row;
    }

    /// <summary>
    /// One coloured chip per kind of stretch the course contains, carrying the same glyph as the
    /// signposts on the track.
    /// </summary>
    private static Control SectionChips(RacePickerCourseViewState course, float glyphSize)
    {
        var chips = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        chips.AddThemeConstantOverride("separation", 2);
        foreach (var kind in course.Segments.Select(segment => segment.Kind).Distinct())
        {
            var chip = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkCenter };
            var style = new StyleBoxFlat
            {
                BgColor = CourseMinimap.ColorForKind(kind),
                BorderColor = CourseMinimap.ColorForKind(kind).Darkened(0.35f),
                AntiAliasing = false
            };
            style.SetBorderWidthAll(1);
            style.SetContentMarginAll(2);
            chip.AddThemeStyleboxOverride("panel", style);
            chip.AddChild(new TextureRect
            {
                Texture = RaceSectionGlyphs.For(kind, Color.FromHtml("#4A3A2C")),
                CustomMinimumSize = new Vector2(glyphSize, glyphSize),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = TextureFilterEnum.Nearest,
                MouseFilter = MouseFilterEnum.Ignore
            });
            chips.AddChild(chip);
        }
        return chips;
    }

    /// <summary>Course cards are pack paper; the picked one glows gold.</summary>
    private static StyleBoxTexture CardStyle(bool selected)
    {
        var style = UiSkin.Paper(selected ? new Color(1.08f, 0.96f, 0.66f) : Colors.White);
        style.ContentMarginLeft = style.ContentMarginRight = 5;
        style.ContentMarginTop = 4;
        style.ContentMarginBottom = 5;
        return style;
    }

    private Control BuildMinimap(RacePickerCourseViewState course, Vector2 size)
    {
        var map = new CourseMinimap { Name = "Minimap", CustomMinimumSize = size, MouseFilter = MouseFilterEnum.Ignore };
        map.SetCourse(course.StartX, course.EndX, course.Segments, course.Obstacles);
        return map;
    }

    /// <summary>
    /// Difficulty as filled and empty wooden stars from the premium icon pack, so a level reads as
    /// a rating rather than only a number.
    /// </summary>
    private static Control BuildStarRating(int level) => PaperCard.StarRating(level, MaxLevel, 20.0f);

    private static PanelContainer PaperPanel(Vector2 minimumSize) => PaperCard.Panel(minimumSize);

    private static Label Header(string text, float width) => PaperCard.Header(text, width);

    private RacePickerCourseViewState SelectedCourse()
        => _state!.Courses.First(course => CourseKey(course) == CourseKey(_selectedCourseId, _selectedCourseVersion));

    private RacePickerVoidlingViewState SelectedRacer()
        => _state!.Voidlings.First(racer => racer.Id == _selectedId);

    private RaceCourseRecordViewState Record(RacePickerCourseViewState course, int level)
        => _state!.Records.TryGetValue(RecordKey(course.Id, course.Version, level), out var record)
            ? record
            : new RaceCourseRecordViewState(string.Empty, Tr("UI_RACE_BLANK_TIME"));

    public static string RecordKey(string courseId, int version, int level) => $"{courseId}@{version}@{level}";

    private void RaiseSelectionChanged()
        => SelectionChanged?.Invoke(_selectedId, _selectedCourseId, _selectedCourseVersion, _level);

    private static Color PaperInk(Color color) => PaperCard.Ink(color);

    private static string CourseKey(RacePickerCourseViewState course) => CourseKey(course.Id, course.Version);

    private static string CourseKey(string id, int version) => $"{id}@{version}";

    private static void Clear(Node node) => PaperCard.Clear(node);
}
