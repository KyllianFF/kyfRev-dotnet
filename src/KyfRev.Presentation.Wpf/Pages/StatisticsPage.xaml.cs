using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue « Statistiques ».</summary>
internal sealed partial class StatisticsPage : UserControl
{
    private StatisticsPageViewModel? _page;

    public StatisticsPage()
    {
        InitializeComponent();
        SizeChanged += (_, e) =>
        {
            _page?.PageWidth = e.NewSize.Width;
        };
        DataContextChanged += (_, e) =>
        {
            _page?.PropertyChanged -= OnPageChanged;
            _page = e.NewValue as StatisticsPageViewModel;
            _page?.PropertyChanged += OnPageChanged;
            UpdateDetails();
        };
    }

    private void OnPageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StatisticsPageViewModel.IsSideBySide))
        {
            UpdateDetails();
        }
    }

    /// <summary>Les deux détails côte à côte quand la place le permet, l'un sous l'autre sinon.</summary>
    private void UpdateDetails()
    {
        var sideBySide = _page?.IsSideBySide ?? true;
        RightColumn.Width = sideBySide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Grid.SetColumn(FoldersCard, sideBySide ? 1 : 0);
        Grid.SetRow(FoldersCard, sideBySide ? 0 : 1);
        MonthsCard.Margin = sideBySide ? new Thickness(0, 0, 8, 8) : new Thickness(0, 0, 0, 8);
    }

    private void OnCalendarSized(object sender, SizeChangedEventArgs e) => _page?.CalendarWidth = e.NewSize.Width;
}

/// <summary>
/// Le calendrier d'activité : une seule image pour le lecteur d'écran, et non 371 cases à parcourir. Son nom le résume, et
/// les détails par mois et par dossier donnent les chiffres.
/// </summary>
internal sealed class ActivityCalendar : ItemsControl
{
    /// <inheritdoc/>
    protected override AutomationPeer OnCreateAutomationPeer() => new ActivityCalendarPeer(this);

    private sealed class ActivityCalendarPeer(ActivityCalendar owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override List<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>Des libellés qui répètent ce que disent déjà les détails : le lecteur d'écran n'a pas à les parcourir.</summary>
internal sealed class DecorativeItems : ItemsControl
{
    /// <inheritdoc/>
    protected override AutomationPeer OnCreateAutomationPeer() => new DecorativeItemsPeer(this);

    private sealed class DecorativeItemsPeer(DecorativeItems owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;

        protected override List<AutomationPeer>? GetChildrenCore() => null;
    }
}
