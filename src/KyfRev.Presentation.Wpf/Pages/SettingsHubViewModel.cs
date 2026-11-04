using CommunityToolkit.Mvvm.ComponentModel;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Un onglet de la vue « Paramètres ».</summary>
public sealed partial class SettingsTab : ObservableObject
{
    internal SettingsTab(string label, object page)
    {
        Label = label;
        Page = page;
    }

    /// <summary>« Révisions », « Données »….</summary>
    public string Label { get; }

    /// <summary>Le contenu de l'onglet.</summary>
    public object Page { get; }

    /// <summary>L'onglet est celui que l'on voit.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>La vue « Paramètres » : des onglets par thème, et une seule page à la fois.</summary>
public sealed partial class SettingsHubViewModel : ObservableObject, INavigationAware
{
    private readonly List<SettingsTab> _tabs;

    /// <summary>La vue, avec ses onglets, dans l'ordre ; le premier s'ouvre.</summary>
    public SettingsHubViewModel(GeneralSettingsViewModel general, SettingsPageViewModel revisions, ModulesSettingsViewModel modules, DataSettingsViewModel data, AboutSettingsViewModel about)
    {
        ArgumentNullException.ThrowIfNull(general);
        ArgumentNullException.ThrowIfNull(revisions);
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(about);
        _tabs =
        [
            new SettingsTab(GeneralSettingsText.General, general),
            new SettingsTab(SettingsHubText.Revisions, revisions),
            new SettingsTab(ModulesSettingsText.Tab, modules),
            new SettingsTab(SettingsHubText.Data, data),
            new SettingsTab(SettingsHubText.About, about),
        ];
        foreach (var tab in _tabs)
        {
            tab.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SettingsTab.IsSelected) && tab.IsSelected)
                {
                    Select(tab);
                }
            };
        }
        Selected = _tabs[0];
        Selected.IsSelected = true;
    }

    /// <summary>« Paramètres ».</summary>
    public static string Title => SettingsText.Title;

    /// <summary>Les onglets.</summary>
    public IReadOnlyList<SettingsTab> Tabs => _tabs;

    /// <summary>L'onglet que l'on voit.</summary>
    [ObservableProperty]
    public partial SettingsTab Selected { get; private set; }

    /// <inheritdoc/>
    public void OnNavigatedTo() => (Selected.Page as INavigationAware)?.OnNavigatedTo();

    private void Select(SettingsTab tab)
    {
        if (ReferenceEquals(tab, Selected))
        {
            return;
        }
        Selected.IsSelected = false;
        Selected = tab;
        OnNavigatedTo();
    }
}
