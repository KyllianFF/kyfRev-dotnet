using CommunityToolkit.Mvvm.ComponentModel;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Un thème proposé au choix.</summary>
/// <param name="Preference">Thème.</param>
/// <param name="Label">Libellé.</param>
public sealed record ThemeChoice(ThemePreference Preference, string Label)
{
    /// <summary>Le libellé : c'est aussi le nom lu par le lecteur d'écran.</summary>
    public override string ToString() => Label;
}

/// <summary>Galerie des composants, pour la revue visuelle : chaque composant dans le thème choisi.</summary>
public sealed partial class GalleryPageViewModel : ObservableObject
{
    private readonly AppearanceController _appearance;

    /// <summary>La galerie, qui change le thème de toute l'application.</summary>
    public GalleryPageViewModel(AppearanceController appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        _appearance = appearance;
        Themes =
        [
            new(ThemePreference.System, ShellText.ThemeSystem),
            new(ThemePreference.Light, ShellText.ThemeLight),
            new(ThemePreference.Dark, ShellText.ThemeDark),
        ];
        _appearance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppearanceController.Preference))
            {
                OnPropertyChanged(nameof(SelectedTheme));
            }
        };
    }

    /// <summary>Thèmes proposés.</summary>
    public IReadOnlyList<ThemeChoice> Themes { get; }

    /// <summary>Thème choisi.</summary>
    public ThemeChoice SelectedTheme
    {
        get => Themes.First(choice => choice.Preference == _appearance.Preference);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _appearance.Preference = value.Preference;
        }
    }
}
