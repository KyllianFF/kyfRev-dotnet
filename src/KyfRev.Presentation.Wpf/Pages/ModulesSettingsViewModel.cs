using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Un domaine de la citation du jour.</summary>
public sealed partial class QuoteDomainChoice : ObservableObject
{
    private readonly Action _changed;

    internal QuoteDomainChoice(string name, string label, string pages, Action changed)
    {
        Name = name;
        Label = label;
        Pages = pages;
        _changed = changed;
    }

    /// <summary>Son nom dans les réglages du coffre.</summary>
    public string Name { get; }

    /// <summary>Ce qu'en voit l'utilisateur.</summary>
    public string Label { get; }

    /// <summary>Les pages de Wikiquote qu'il couvre.</summary>
    public string Pages { get; }

    /// <summary>Le domaine est choisi.</summary>
    [ObservableProperty]
    public partial bool IsChosen { get; set; }

    partial void OnIsChosenChanged(bool value) => _changed();
}

/// <summary>
/// L'onglet « Modules et extensions » : des fonctions facultatives, pour ce coffre. Désactiver un module le
/// retire des menus ; ce qu'il a créé reste dans le coffre, intact.
/// </summary>
public sealed partial class ModulesSettingsViewModel : ObservableObject, INavigationAware
{
    private readonly SettingsUseCases _settings;
    private readonly Action<VaultPreferences> _applied;
    private bool _showing = true;

    /// <summary>L'onglet ; <paramref name="applied"/> charge les modules que disent les réglages qui viennent de changer.</summary>
    public ModulesSettingsViewModel(SettingsUseCases settings, Action<VaultPreferences> applied)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(applied);
        _settings = settings;
        _applied = applied;
        Domains =
        [
            new("philosophie", ModulesSettingsText.DomainPhilosophy, ModulesSettingsText.DomainPhilosophyPages, SaveDomains),
            new("art", ModulesSettingsText.DomainArt, ModulesSettingsText.DomainArtPages, SaveDomains),
            new("science", ModulesSettingsText.DomainScience, ModulesSettingsText.DomainSciencePages, SaveDomains),
            new("informatique", ModulesSettingsText.DomainComputing, ModulesSettingsText.DomainComputingPages, SaveDomains),
        ];
        _showing = false;
    }

    /// <summary>Les domaines de la citation du jour.</summary>
    public IReadOnlyList<QuoteDomainChoice> Domains { get; }

    /// <summary>Le module Citations est actif.</summary>
    [ObservableProperty]
    public partial bool QuotesActive { get; set; }

    /// <summary>La citation du jour est cherchée sur Wikiquote.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsDomains))]
    public partial bool QuotesOnline { get; set; }

    /// <summary>Le choix des domaines a un sens.</summary>
    public bool ShowsDomains => QuotesOnline;

    /// <summary>Refus du dernier changement.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Un changement a été refusé.</summary>
    public bool HasError => Error is not null;

    /// <summary>Résultat du dernier changement.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    /// <summary>Un module vient de changer.</summary>
    public bool HasMessage => Message is not null;

    /// <inheritdoc/>
    public void OnNavigatedTo()
    {
        Error = null;
        Message = null;
        var outcome = _settings.Get();
        if (outcome.Succeeded)
        {
            Show(outcome.Value);
        }
        else
        {
            Error = outcome.Error;
        }
    }

    private void Show(VaultPreferences preferences)
    {
        _showing = true;
        QuotesActive = preferences.QuotesActive;
        QuotesOnline = preferences.QuotesOnline;
        foreach (var domain in Domains)
        {
            domain.IsChosen = preferences.QuoteDomains.Contains(domain.Name);
        }
        _showing = false;
    }

    private void Save(Func<VaultPreferences, VaultPreferences> change, string? done)
    {
        if (_showing)
        {
            return;
        }
        Error = null;
        Message = null;
        var outcome = _settings.Update(change);
        if (!outcome.Succeeded)
        {
            Error = outcome.Error;
            ShowStored();
            return;
        }
        Message = done;
        _applied(outcome.Value);
    }

    /// <summary>Un refus : l'onglet remontre ce qui est enregistré, sans effacer la raison.</summary>
    private void ShowStored()
    {
        if (_settings.Get() is { Succeeded: true } stored)
        {
            Show(stored.Value);
        }
    }

    private void SaveDomains()
    {
        ImmutableArray<string> chosen = [.. Domains.Where(domain => domain.IsChosen).Select(domain => domain.Name)];
        Save(preferences => preferences with { QuoteDomains = chosen }, null);
    }

    partial void OnQuotesActiveChanged(bool value) =>
        Save(preferences => preferences with { QuotesActive = value }, value ? ModulesSettingsText.QuotesEnabled : ModulesSettingsText.QuotesDisabled);

    partial void OnQuotesOnlineChanged(bool value) =>
        Save(preferences => preferences with { QuotesOnline = value }, null);
}
