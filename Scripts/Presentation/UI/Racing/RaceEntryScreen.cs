using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
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

        var banner = new PanelContainer { Name = "StepBanner", SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        var bannerStyle = new StyleBoxFlat { BgColor = Color.FromHtml("#E8B75C"), BorderColor = Color.FromHtml("#B07C33") };
        bannerStyle.SetBorderWidthAll(2);
        bannerStyle.SetCornerRadiusAll(4);
        bannerStyle.SetContentMarginAll(5);
        bannerStyle.ContentMarginLeft = 12;
        bannerStyle.ContentMarginRight = 12;
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
    }

    // ---- Step 1: course and difficulty ------------------------------------------------------

    private Control BuildCourseStep()
    {
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        row.AddThemeConstantOverride("separation", 8);

        var listPanel = PaperPanel(new Vector2(268, 0));
        listPanel.Name = "CourseListPanel";
        var left = new VBoxContainer();
        left.AddThemeConstantOverride("separation", 4);
        listPanel.AddChild(left);

        var headers = new HBoxContainer();
        headers.AddThemeConstantOverride("separation", 4);
        headers.AddChild(Header(string.Empty, 14));
        headers.AddChild(Header(Tr("UI_RACE_PICKER_COURSE"), 136));
        headers.AddChild(Header(Tr("UI_RACE_LEVEL"), 100));
        left.AddChild(headers);

        var list = new VBoxContainer { Name = "CourseList" };
        list.AddThemeConstantOverride("separation", 4);
        foreach (var course in _state!.Courses)
            list.AddChild(BuildCourseRow(course));
        left.AddChild(list);
        row.AddChild(listPanel);

        row.AddChild(BuildRecordPanel());
        return row;
    }

    private Control BuildCourseRow(RacePickerCourseViewState course)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);

        var selected = CourseKey(course) == CourseKey(_selectedCourseId, _selectedCourseVersion);

        // A gold star marks the picked course, so the row the record card is describing is obvious
        // without having to read the button states.
        row.AddChild(new TextureRect
        {
            Texture = new AtlasTexture { Atlas = WoodStars, Region = new Rect2(0, 0, 32, 32) },
            CustomMinimumSize = new Vector2(16, 16),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            Modulate = selected ? Colors.White : new Color(1, 1, 1, 0),
            MouseFilter = MouseFilterEnum.Ignore
        });

        var name = UiFactory.CreateButton(course.Name);
        name.Name = "Course_" + course.Id;
        name.ToggleMode = true;
        name.ButtonPressed = selected;
        name.CustomMinimumSize = new Vector2(136, 30);
        name.Alignment = HorizontalAlignment.Left;
        UiFactory.ApplyPixelFont(name, 8);
        name.Pressed += () =>
        {
            _selectedCourseId = course.Id;
            _selectedCourseVersion = course.Version;
            RaiseSelectionChanged();
            RebuildStep();
        };
        row.AddChild(name);

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
        return row;
    }

    // Difficulty pills reuse the premium round-button sheet: tan for an unpicked level, blue for the
    // picked one, so the row reads at a glance without any extra wording.
    private Button BuildLevelPill(RacePickerCourseViewState course, int level, bool active)
    {
        var button = new Button
        {
            Name = $"Level_{course.Id}_{level}",
            Text = level.ToString(),
            ToggleMode = true,
            ButtonPressed = active,
            CustomMinimumSize = new Vector2(32, 32),
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
        UiFactory.ApplyPixelFont(button, 8);
        button.AddThemeColorOverride("font_color", Color.FromHtml(active ? "#3A2C18" : "#7A6650"));
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Colors.White);
        return button;
    }

    private Control BuildRecordPanel()
    {
        var panel = PaperPanel(new Vector2(246, 0));
        panel.Name = "CourseRecord";
        panel.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        panel.AddChild(box);

        var course = SelectedCourse();
        var title = UiFactory.CreateLabel(course.Name, 10);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(title);
        box.AddChild(BuildStarRating(_level));
        box.AddChild(Header(Tr("UI_RACE_COURSE_RECORD"), 0));
        var record = Record(course, _level);
        var holder = UiFactory.CreateLabel(
            string.IsNullOrEmpty(record.CreatureName) ? Tr("UI_RACE_NO_RECORD") : record.CreatureName, 8);
        holder.HorizontalAlignment = HorizontalAlignment.Center;
        holder.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        box.AddChild(holder);
        var time = UiFactory.CreateLabel(record.Time, 10);
        time.Name = "RecordTime";
        time.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(time);

        box.AddChild(BuildMinimap(course, new Vector2(230, 74)));
        var detail = UiFactory.CreateLabel(
            $"{string.Join(" · ", course.Sections)}   {course.LengthMeters} M", 6);
        detail.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(detail);
        return panel;
    }

    // ---- Step 2: racer ----------------------------------------------------------------------

    private Control BuildRacerStep()
    {
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        row.AddThemeConstantOverride("separation", 8);

        var pages = Mathf.Max(1, (_state!.Voidlings.Count + RacersPerPage - 1) / RacersPerPage);
        _racerPage = Mathf.Clamp(_racerPage, 0, pages - 1);

        var rosterPanel = PaperPanel(Vector2.Zero);
        rosterPanel.Name = "RosterPanel";
        var rosterBox = new VBoxContainer();
        rosterBox.AddThemeConstantOverride("separation", 3);
        rosterPanel.AddChild(rosterBox);

        var gridRow = new HBoxContainer();
        gridRow.AddThemeConstantOverride("separation", 4);
        gridRow.AddChild(BuildPageArrow("RosterPrev", -1, pages));

        var grid = new GridContainer { Name = "RosterGrid", Columns = RacerColumns };
        grid.AddThemeConstantOverride("h_separation", 3);
        grid.AddThemeConstantOverride("v_separation", 1);
        var page = _state.Voidlings.Skip(_racerPage * RacersPerPage).Take(RacersPerPage).ToArray();
        foreach (var creature in page)
        {
            var captured = creature;
            var entry = UiFactory.CreateVoidlingCard(
                creature.Name,
                creature.Appearance,
                creature.HasAngelMutation,
                creature.OtherMutationCount,
                pressed =>
                {
                    if (!pressed) return;
                    _selectedId = captured.Id;
                    RaiseSelectionChanged();
                    RebuildStep();
                },
                out var card);
            card.Name = "Racer_" + creature.Id;
            card.SetPressedNoSignal(creature.Id == _selectedId);
            // Three rows have to fit above the footer, so the card keeps only the height its
            // portrait and name actually need.
            entry.CustomMinimumSize = new Vector2(84, 72);
            if (creature.Id == _selectedId)
            {
                card.AddChild(new TextureRect
                {
                    Texture = new AtlasTexture { Atlas = WoodStars, Region = new Rect2(0, 0, 32, 32) },
                    Position = new Vector2(60, 1),
                    Size = new Vector2(17, 17),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = MouseFilterEnum.Ignore
                });
            }
            grid.AddChild(entry);
        }
        // Empty slots stay drawn so the roster keeps a stable 3x3 shape however many Voidlings a
        // player owns, instead of the grid collapsing around a short last page.
        for (var filler = page.Length; filler < RacersPerPage; filler++)
            grid.AddChild(BuildEmptySlot());
        gridRow.AddChild(grid);
        gridRow.AddChild(BuildPageArrow("RosterNext", 1, pages));
        rosterBox.AddChild(gridRow);

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

    private static Control BuildEmptySlot()
    {
        var slot = new PanelContainer { CustomMinimumSize = new Vector2(84, 70), MouseFilter = MouseFilterEnum.Ignore };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.72f, 0.66f, 0.52f, 0.30f),
            BorderColor = new Color(0.62f, 0.55f, 0.42f, 0.45f)
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(3);
        slot.AddThemeStyleboxOverride("panel", style);
        return slot;
    }

    private Button BuildPageArrow(string name, int delta, int pages)
    {
        var button = UiFactory.CreateButton(string.Empty);
        button.Name = name;
        button.CustomMinimumSize = new Vector2(24, 36);
        button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        button.Disabled = pages <= 1;
        button.AddChild(new TextureRect
        {
            Texture = UiFactory.CreateGardenIcon(13, 3),
            FlipH = delta < 0,
            CustomMinimumSize = new Vector2(24, 36),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, pages <= 1 ? 0.35f : 1.0f)
        });
        button.Pressed += () =>
        {
            _racerPage = (_racerPage + delta + pages) % pages;
            RebuildStep();
        };
        return button;
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
            BgColor = Color.FromHtml("#C5B798"),
            BorderColor = Color.FromHtml("#8A7A5A")
        };
        background.SetBorderWidthAll(1);
        var fill = new StyleBoxFlat { BgColor = PaperInk(stat.Color) };
        background.SetCornerRadiusAll(1);
        fill.SetCornerRadiusAll(1);
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

        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 8);

        var coursePanel = PaperPanel(new Vector2(300, 0));
        coursePanel.Name = "ConfirmCourse";
        coursePanel.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        var courseBox = new VBoxContainer();
        courseBox.AddThemeConstantOverride("separation", 4);
        coursePanel.AddChild(courseBox);
        var courseName = UiFactory.CreateLabel(course.Name, 11);
        courseName.HorizontalAlignment = HorizontalAlignment.Center;
        courseBox.AddChild(courseName);
        var levelLine = UiFactory.CreateLabel(string.Format(Tr("UI_RACE_LEVEL_VALUE"), _level), 8);
        levelLine.HorizontalAlignment = HorizontalAlignment.Center;
        courseBox.AddChild(levelLine);
        courseBox.AddChild(BuildStarRating(_level));
        courseBox.AddChild(BuildMinimap(course, new Vector2(284, 84)));
        var detail = UiFactory.CreateLabel(
            $"{string.Join(" · ", course.Sections)}   {course.LengthMeters} M", 6);
        detail.HorizontalAlignment = HorizontalAlignment.Center;
        courseBox.AddChild(detail);
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
    private static Control BuildStarRating(int level)
    {
        var row = new HBoxContainer { Name = "StarRating", Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 1);
        for (var star = MinLevel; star <= MaxLevel; star++)
        {
            row.AddChild(new TextureRect
            {
                Texture = new AtlasTexture { Atlas = WoodStars, Region = new Rect2(star <= level ? 0 : 32, 0, 32, 32) },
                CustomMinimumSize = new Vector2(20, 20),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore
            });
        }
        return row;
    }

    /// <summary>The window's warm paper, one shade lighter, so a card reads as part of the same sheet.</summary>
    private static PanelContainer PaperPanel(Vector2 minimumSize)
    {
        var panel = UiFactory.CreatePanel(minimumSize);
        var style = (StyleBoxTexture)panel.GetThemeStylebox("panel").Duplicate();
        style.ModulateColor = new Color(247f / 220f, 233f / 224f, 197f / 210f);
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private static Label Header(string text, float width)
    {
        var label = UiFactory.CreateLabel(text, 7);
        if (width > 0) label.CustomMinimumSize = new Vector2(width, 0);
        return label;
    }

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

    // Stat identity colours are authored for the dark Garden inspector; on the entry screen's paper
    // the pale ones (swim yellow, stamina white) vanish, so darken by however much luminance is over.
    private static Color PaperInk(Color color)
        => color.Darkened(Mathf.Clamp(color.Luminance - 0.35f, 0.0f, 0.6f));

    private static string CourseKey(RacePickerCourseViewState course) => CourseKey(course.Id, course.Version);

    private static string CourseKey(string id, int version) => $"{id}@{version}";

    private static void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is CanvasItem canvasItem) canvasItem.Visible = false;
            if (child is Control control) control.MouseFilter = MouseFilterEnum.Ignore;
            child.QueueFree();
        }
    }
}
