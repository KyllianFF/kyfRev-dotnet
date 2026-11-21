using CommunityToolkit.Mvvm.ComponentModel;
using KyfRev.Application.Model;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Une famille d'animations et son interrupteur, dans la section « Apparence ».</summary>
public sealed class MotionChoice : ObservableObject
{
    private readonly GeneralSettingsViewModel _owner;

    internal MotionChoice(GeneralSettingsViewModel owner, MotionFamilies family, string name, string help)
    {
        _owner = owner;
        Family = family;
        Name = name;
        Help = help;
    }

    /// <summary>La famille.</summary>
    public MotionFamilies Family { get; }

    /// <summary>Son nom, tel qu'il est écrit dans la page.</summary>
    public string Name { get; }

    /// <summary>Ce qu'elle anime.</summary>
    public string Help { get; }

    /// <summary>Allumée, ou coupée.</summary>
    public bool IsOn
    {
        get => (_owner.MotionOff & Family) == 0;
        set => _owner.MotionOff = value ? _owner.MotionOff & ~Family : _owner.MotionOff | Family;
    }

    /// <summary>Ce que l'interrupteur montre a changé ailleurs (le choix « Toutes », « Essentielles »).</summary>
    internal void Refresh() => OnPropertyChanged(nameof(IsOn));
}

/// <summary>Les animations de l'interface : toutes, les essentielles, aucune, ou famille par famille.</summary>
public sealed partial class GeneralSettingsViewModel
{
    /// <summary>Les familles d'animations coupées, quand les animations ne sont pas toutes réduites.</summary>
    [ObservableProperty]
    public partial MotionFamilies MotionOff { get; set; }

    /// <summary>Les familles, chacune avec son interrupteur, dans l'ordre de la page.</summary>
    public IReadOnlyList<MotionChoice> Motions => field ??=
    [
        new(this, MotionFamilies.Feedback, GeneralSettingsText.MotionFeedback, GeneralSettingsText.MotionFeedbackHelp),
        new(this, MotionFamilies.Selection, GeneralSettingsText.MotionSelection, GeneralSettingsText.MotionSelectionHelp),
        new(this, MotionFamilies.Views, GeneralSettingsText.MotionViews, GeneralSettingsText.MotionViewsHelp),
        new(this, MotionFamilies.Panels, GeneralSettingsText.MotionPanels, GeneralSettingsText.MotionPanelsHelp),
        new(this, MotionFamilies.Surfaces, GeneralSettingsText.MotionSurfaces, GeneralSettingsText.MotionSurfacesHelp),
        new(this, MotionFamilies.Lists, GeneralSettingsText.MotionLists, GeneralSettingsText.MotionListsHelp),
        new(this, MotionFamilies.Opening, GeneralSettingsText.MotionOpening, GeneralSettingsText.MotionOpeningHelp),
    ];

    /// <summary>Toutes les animations jouent.</summary>
    public bool IsMotionAll
    {
        get => !ReducedMotion && MotionOff == MotionFamilies.None;
        set
        {
            if (value)
            {
                ReducedMotion = false;
                MotionOff = MotionFamilies.None;
            }
        }
    }

    /// <summary>Seules les essentielles jouent : le retour au toucher et la sélection qui glisse.</summary>
    public bool IsMotionEssential
    {
        get => !ReducedMotion && MotionOff == MotionFamilies.BeyondEssential;
        set
        {
            if (value)
            {
                ReducedMotion = false;
                MotionOff = MotionFamilies.BeyondEssential;
            }
        }
    }

    /// <summary>Aucune animation, même si Windows ne le demande pas.</summary>
    public bool IsMotionNone
    {
        get => ReducedMotion;
        set
        {
            if (value)
            {
                ReducedMotion = true;
            }
        }
    }

    /// <summary>Les interrupteurs des familles servent tant que les animations ne sont pas toutes coupées.</summary>
    public bool CanChooseMotions => !ReducedMotion;

    partial void OnMotionOffChanged(MotionFamilies value)
    {
        Remember(preferences => preferences with { MotionOff = value });
        ShowMotion();
    }

    /// <summary>Le choix d'ensemble et chaque interrupteur redisent ce qui est en vigueur.</summary>
    private void ShowMotion()
    {
        OnPropertyChanged(nameof(IsMotionAll));
        OnPropertyChanged(nameof(IsMotionEssential));
        OnPropertyChanged(nameof(IsMotionNone));
        OnPropertyChanged(nameof(CanChooseMotions));
        foreach (var choice in Motions)
        {
            choice.Refresh();
        }
    }
}
