using System.ComponentModel;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Presentation.Wpf.Reading;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>Les préférences de ce poste : lues une fois au démarrage, écrites à chaque changement, appliquées aussitôt.</summary>
public sealed class MachineSettings
{
    private readonly IMachinePreferencesStore _store;

    /// <summary>Les préférences gardées dans <paramref name="store"/>.</summary>
    public MachineSettings(IMachinePreferencesStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        Current = store.Load();
    }

    /// <summary>Les préférences en vigueur.</summary>
    public MachinePreferences Current { get; private set; }

    /// <summary>Une préférence vient de changer.</summary>
    public event EventHandler? Changed;

    /// <summary>Change une préférence : elle s'applique même si elle n'a pas pu être retenue, ce que dit le résultat faux.</summary>
    public bool Update(Func<MachinePreferences, MachinePreferences> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var next = change(Current);
        if (next == Current)
        {
            return true;
        }
        Current = next;
        Changed?.Invoke(this, EventArgs.Empty);
        return _store.Save(next);
    }

    /// <summary>
    /// Applique les préférences à l'apparence et à la coquille, maintenant et à chaque changement, et retient
    /// le zoom changé au clavier.
    /// </summary>
    public void Follow(AppearanceController appearance, ShellViewModel shell)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        ArgumentNullException.ThrowIfNull(shell);
        Apply(appearance, shell);
        Changed += (_, _) => Apply(appearance, shell);
        appearance.PropertyChanged += (_, e) =>
        {
            if (e is PropertyChangedEventArgs { PropertyName: nameof(AppearanceController.Preference) })
            {
                Update(preferences => preferences with { Theme = appearance.Preference });
            }
        };
        shell.PropertyChanged += (_, e) =>
        {
            if (e is PropertyChangedEventArgs { PropertyName: nameof(ShellViewModel.ChosenZoom) })
            {
                Update(preferences => preferences with { Zoom = shell.ChosenZoom });
            }
            if (e is PropertyChangedEventArgs { PropertyName: nameof(ShellViewModel.IsFocusMode) })
            {
                Update(preferences => preferences with { FocusMode = shell.IsFocusMode });
            }
        };
    }

    private void Apply(AppearanceController appearance, ShellViewModel shell)
    {
        var preferences = Current;
        appearance.Preference = preferences.Theme;
        appearance.ForcesHighContrast = preferences.HighContrast;
        appearance.ForcesReducedMotion = preferences.ReducedMotion;
        appearance.LargerTargets = preferences.LargerTargets;
        appearance.Resources[ReadingComfort.ResourceKey] = ReadingComfort.Of(preferences);
        // Les feuilles et leur papier : toute vue qui lit une fiche les trouve là, et suit leurs changements.
        if (!Equals(appearance.Resources[SheetPaper.ResourceKey], SheetPaper.Of(preferences)))
        {
            appearance.Resources[SheetPaper.ResourceKey] = SheetPaper.Of(preferences);
        }
        shell.SetMenuOrder(preferences.MenuOrder);
        ShortcutMap.Use(preferences.Shortcuts);
        shell.RefreshShortcuts();
        shell.SetZoom(preferences.Zoom);
        shell.IsFocusMode = preferences.FocusMode;
    }
}
