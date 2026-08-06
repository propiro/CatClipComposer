using System.Windows;
using System.Windows.Controls;
using CatClipComposer.Core.Models;

namespace CatClipComposer.Workspace;

internal enum WorkspacePanelKind
{
    ContentBrowser,
    Preview,
    Layers,
    Timeline
}

internal sealed class WorkspaceLayoutController(
    FrameworkElement contentBrowser,
    FrameworkElement preview,
    FrameworkElement layers,
    FrameworkElement timeline)
{
    private readonly IReadOnlyDictionary<WorkspacePanelKind, FrameworkElement> _panels =
        new Dictionary<WorkspacePanelKind, FrameworkElement>
        {
            [WorkspacePanelKind.ContentBrowser] = contentBrowser,
            [WorkspacePanelKind.Preview] = preview,
            [WorkspacePanelKind.Layers] = layers,
            [WorkspacePanelKind.Timeline] = timeline
        };

    public void Apply(ApplicationSettings settings)
    {
        ApplyPanel(WorkspacePanelKind.ContentBrowser, settings.ContentBrowserDock);
        ApplyPanel(WorkspacePanelKind.Preview, settings.PreviewDock);
        ApplyPanel(WorkspacePanelKind.Layers, settings.LayersDock);
        ApplyPanel(WorkspacePanelKind.Timeline, settings.TimelineDock);
    }

    public static void MovePanel(
        ApplicationSettings settings,
        WorkspacePanelKind panel,
        WorkspaceDockSlot target)
    {
        var current = GetSlot(settings, panel);
        if (current == target)
        {
            return;
        }

        var occupant = Enum.GetValues<WorkspacePanelKind>()
            .Single(candidate => GetSlot(settings, candidate) == target);
        SetSlot(settings, occupant, current);
        SetSlot(settings, panel, target);
    }

    private void ApplyPanel(WorkspacePanelKind panel, WorkspaceDockSlot slot)
    {
        var element = _panels[panel];
        element.Margin = slot == WorkspaceDockSlot.Bottom
            ? new Thickness(0, 4, 0, 0)
            : new Thickness(0, 0, 4, 0);
        Grid.SetRowSpan(element, 1);
        Grid.SetColumnSpan(element, 1);

        switch (slot)
        {
            case WorkspaceDockSlot.Left:
                Grid.SetRow(element, 0);
                Grid.SetColumn(element, 0);
                break;
            case WorkspaceDockSlot.Center:
                Grid.SetRow(element, 0);
                Grid.SetColumn(element, 2);
                break;
            case WorkspaceDockSlot.Right:
                Grid.SetRow(element, 0);
                Grid.SetColumn(element, 4);
                element.Margin = new Thickness(0);
                break;
            case WorkspaceDockSlot.Bottom:
                Grid.SetRow(element, 2);
                Grid.SetColumn(element, 0);
                Grid.SetColumnSpan(element, 5);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
        }
    }

    private static WorkspaceDockSlot GetSlot(
        ApplicationSettings settings,
        WorkspacePanelKind panel) => panel switch
        {
            WorkspacePanelKind.ContentBrowser => settings.ContentBrowserDock,
            WorkspacePanelKind.Preview => settings.PreviewDock,
            WorkspacePanelKind.Layers => settings.LayersDock,
            WorkspacePanelKind.Timeline => settings.TimelineDock,
            _ => throw new ArgumentOutOfRangeException(nameof(panel), panel, null)
        };

    private static void SetSlot(
        ApplicationSettings settings,
        WorkspacePanelKind panel,
        WorkspaceDockSlot slot)
    {
        switch (panel)
        {
            case WorkspacePanelKind.ContentBrowser:
                settings.ContentBrowserDock = slot;
                break;
            case WorkspacePanelKind.Preview:
                settings.PreviewDock = slot;
                break;
            case WorkspacePanelKind.Layers:
                settings.LayersDock = slot;
                break;
            case WorkspacePanelKind.Timeline:
                settings.TimelineDock = slot;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(panel), panel, null);
        }
    }
}
