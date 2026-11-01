using System.Collections.Immutable;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Dates affichées comme dans la 0.x, en français.</summary>
internal static class Dates
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>« vendredi 9 octobre ».</summary>
    public static string Long(DateOnly day) => day.ToString("dddd d MMMM", French);

    /// <summary>« ven. 9 oct. ».</summary>
    public static string Short(DateOnly day) => day.ToString("ddd d MMM", French);

    /// <summary>« Vendredi 9 octobre ».</summary>
    public static string Capitalized(string text) => text.Length == 0 ? text : char.ToUpper(text[0], French) + text[1..];

    /// <summary>Le texte au singulier pour 0 ou 1, au pluriel au-delà, comme en français.</summary>
    public static string Plural(int count, Func<object, string> one, Func<object, string> many) => count > 1 ? many(count) : one(count);
}

/// <summary>Ton d'un groupe : il colore son en-tête.</summary>
public enum DashboardTone
{
    /// <summary>Ton normal.</summary>
    Normal,

    /// <summary>En retard.</summary>
    Overdue,

    /// <summary>Rappels de consolidation.</summary>
    Reminder,
}

/// <summary>
/// En-tête d'un groupe du tableau de bord (« En retard », « Aujourd'hui »…), avec son nombre
/// d'éléments ; il replie ou déplie le groupe.
/// </summary>
public sealed partial class DashboardGroup : ObservableObject
{
    private readonly Action _changed;

    internal DashboardGroup(string title, string glyph, DashboardTone tone, bool expanded, Action changed)
    {
        Title = title;
        Glyph = glyph;
        Tone = tone;
        IsExpanded = expanded;
        _changed = changed;
    }

    /// <summary>Titre.</summary>
    public string Title { get; }

    /// <summary>Icône.</summary>
    public string Glyph { get; }

    /// <summary>Ton.</summary>
    public DashboardTone Tone { get; }

    /// <summary>Nombre d'éléments du groupe.</summary>
    [ObservableProperty]
    public partial int Count { get; internal set; }

    /// <summary>Le groupe est déplié.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>« En retard, 3 » : le nom lu par le lecteur d'écran.</summary>
    public override string ToString() => $"{Title}, {Count}";

    /// <summary>Replie ou déplie le groupe.</summary>
    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;

    partial void OnIsExpandedChanged(bool value) => _changed?.Invoke();
}

/// <summary>Comment s'affiche le titre d'une ligne.</summary>
public enum DueItemHeading
{
    /// <summary>Le titre de la fiche.</summary>
    Title,

    /// <summary>Le terme d'une définition, en gras, puis la fiche.</summary>
    Term,

    /// <summary>Le passage surligné, entre guillemets, puis la fiche.</summary>
    Excerpt,

    /// <summary>Passage caché par le rappel actif : seulement de quoi le retrouver.</summary>
    Hidden,
}

/// <summary>Une fiche ou un passage à réviser, tel que la 0.x le présente (<c>LigneElement.svelte</c>).</summary>
public sealed class DueItemRow
{
    internal DueItemRow(DueItem item, bool activeRecall, bool showDueDate)
    {
        Item = item;
        var sheet = item.Kind == ItemKind.Sheet;
        // Rappel actif : le texte d'un passage n'est pas montré (une définition, si : son terme ne donne pas la réponse).
        var hidden = activeRecall && !sheet && item.Term is null;
        PillKind = sheet ? PillKind.Sheet : PillKind.Passage;
        KindLabel = sheet ? DashboardText.Sheet : item.Term is not null ? DashboardText.Definition : DashboardText.Passage;
        (Heading, HeadingText, Source) = (item.Term, item.Excerpt) switch
        {
            ({ } term, _) => (DueItemHeading.Term, term, $"· {item.Title}"),
            (null, not null) when hidden => (DueItemHeading.Hidden, DashboardText.PassageToRecall, $"· {string.Join(" › ", [item.Title, .. item.Section.TakeLast(1)])}"),
            (null, { } excerpt) => (DueItemHeading.Excerpt, $"« {excerpt} »", $"· {item.Title}"),
            _ => (DueItemHeading.Title, item.Title, null),
        };
        var details = ImmutableArray.CreateBuilder<string>();
        if (hidden && item.Comment is { } comment)
        {
            details.Add(DashboardText.CommentHintOf(comment));
        }
        if (item.Folder.Length > 0)
        {
            details.Add(item.Folder);
        }
        details.AddRange(item.Tags.Select(tag => $"#{tag}"));
        details.Add(DashboardText.CycleOf(item.Cycle));
        details.Add(DashboardText.LastReviewOf(Dates.Short(item.LastReview)));
        Details = details.ToImmutable();
        Late = item.DaysLate > 0 ? DashboardText.DaysLateOf(item.DaysLate) : null;
        DueDate = item.DaysLate == 0 && showDueDate ? Dates.Short(item.DueDate) : null;
    }

    /// <summary>L'élément.</summary>
    public DueItem Item { get; }

    /// <summary>Pastille : fiche ou passage.</summary>
    public PillKind PillKind { get; }

    /// <summary>« Fiche », « Définition » ou « Passage ».</summary>
    public string KindLabel { get; }

    /// <summary>Comment s'affiche le titre.</summary>
    public DueItemHeading Heading { get; }

    /// <summary>Titre de la fiche, terme, passage entre guillemets, ou « Passage à retrouver ».</summary>
    public string HeadingText { get; }

    /// <summary>« · Titre de la fiche » après un terme ou un passage ; rien pour une fiche.</summary>
    public string? Source { get; }

    /// <summary>Dossier, étiquettes, cycle, dernière révision (et l'indice d'un passage caché).</summary>
    public ImmutableArray<string> Details { get; }

    /// <summary>Le fichier de la fiche est introuvable.</summary>
    public bool FileMissing => !Item.FilePresent;

    /// <summary>Étape attendue (« J7 »).</summary>
    public string Step => Item.Step;

    /// <summary>« Retard 3 j » ; rien sans retard.</summary>
    public string? Late { get; }

    /// <summary>Échéance (« ven. 9 oct. »), dans « À venir » seulement.</summary>
    public string? DueDate { get; }

    /// <summary>Le nom lu par le lecteur d'écran : nature, titre, étape, retard ou échéance.</summary>
    public override string ToString() =>
        string.Join(", ", new[] { KindLabel, Source is null ? HeadingText : $"{HeadingText} {Source}", Step, Late ?? DueDate }.Where(part => part is not null));
}

/// <summary>Un rappel de consolidation en attente (<c>LigneRappel.svelte</c>).</summary>
public sealed class ReminderRow
{
    internal ReminderRow(PendingReminder reminder)
    {
        Reminder = reminder;
        Rank = reminder.PreviousReminders == 0 ? DashboardText.FirstReminder : DashboardText.NthReminderOf(reminder.PreviousReminders + 1);
        Archived = DashboardText.ArchivedOnOf(Dates.Short(reminder.ArchivedOn));
    }

    /// <summary>Le rappel.</summary>
    public PendingReminder Reminder { get; }

    /// <summary>Titre de la fiche.</summary>
    public string Title => Reminder.Title;

    /// <summary>Dossier ; vide à la racine.</summary>
    public string Folder => Reminder.Folder;

    /// <summary>« 1er rappel », « 3e rappel ».</summary>
    public string Rank { get; }

    /// <summary>« Archivée le ven. 9 oct. ».</summary>
    public string Archived { get; }

    /// <summary>Le nom lu par le lecteur d'écran.</summary>
    public override string ToString() => string.Join(", ", DashboardText.Reminder, Title, Rank, Archived);
}

/// <summary>Rien à réviser aujourd'hui : l'état vide, en tête de liste (« À venir » peut suivre).</summary>
/// <param name="Description">Prochaine révision, première fiche à créer, ou fiches toutes archivées.</param>
public sealed record EmptyDashboardRow(string Description)
{
    /// <summary>« Rien à réviser aujourd'hui ».</summary>
    public static string Title => DashboardText.EmptyTitle;

    /// <summary>Le nom lu par le lecteur d'écran.</summary>
    public override string ToString() => $"{Title}. {Description}";
}

/// <summary>Un choix de la vue (filtre, tri) et son libellé.</summary>
/// <typeparam name="T">Valeur choisie.</typeparam>
/// <param name="Value">Valeur.</param>
/// <param name="Label">Libellé.</param>
public sealed record Choice<T>(T Value, string Label)
{
    /// <summary>Le libellé : c'est aussi le nom lu par le lecteur d'écran.</summary>
    public override string ToString() => Label;
}
