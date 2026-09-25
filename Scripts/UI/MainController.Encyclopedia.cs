using System.Linq;
using Godot;
using Voidling.Application.Collection;
using Voidling.Presentation.UI.Encyclopedia;
using Voidling.Presentation.Voidlings;

namespace VoidlingGame;

public partial class MainController
{
    /// <summary>The journal: every form and special variant, discovered or still "???".</summary>
    private void ShowEncyclopedia()
    {
        var projection = _session.CreateEncyclopediaProjection();
        var entries = projection.Entries
            .Select(entry => new EncyclopediaEntryViewState(
                entry.EntryId,
                entry.Discovered,
                Tr("JOURNAL_NAME_" + JournalKey(entry.EntryId)),
                Tr("JOURNAL_HOW_" + JournalKey(entry.EntryId)),
                entry.Discovered
                    ? string.Format(Tr("UI_JOURNAL_FOUND_BY"), entry.Order, entry.FirstDiscoveredBy)
                    : string.Empty,
                new VoidlingVisualAppearance(entry.VisualTypeId, entry.PaletteHue, System.Array.Empty<string>(), "#F6F0C9")))
            .ToArray();

        var box = OpenModal(Tr("UI_JOURNAL_TITLE"), new Vector2(520, 300), Voidling.Presentation.UI.Common.ScreenIcons.Journal);
        var screen = new EncyclopediaScreen();
        screen.Configure(new EncyclopediaScreenState(entries, projection.DiscoveredCount, projection.Total));
        box.AddChild(screen);
        screen.CallDeferred(EncyclopediaScreen.MethodName.FocusSelection);
    }

    /// <summary>Entry IDs as localization key suffixes: "swamp-guy" becomes "SWAMP_GUY".</summary>
    private static string JournalKey(string entryId) => entryId.Replace('-', '_').ToUpperInvariant();
}
