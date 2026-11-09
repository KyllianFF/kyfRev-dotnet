using CommunityToolkit.Mvvm.ComponentModel;
using KyfRev.Application.Model;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Une pastille du filtre par étiquettes.</summary>
/// <param name="label">L'étiquette, sous sa forme la plus fréquente.</param>
/// <param name="count">Le nombre de fiches qui la portent.</param>
/// <param name="isChosen">Elle filtre déjà la liste.</param>
/// <param name="toggled">Ce qui suit un clic.</param>
public sealed partial class TagChip(string label, int count, bool isChosen, Action<TagChip> toggled) : ObservableObject
{
    /// <summary>L'étiquette.</summary>
    public string Label { get; } = label;

    /// <summary>Le nombre de fiches qui la portent.</summary>
    public int Count { get; } = count;

    /// <summary>« #examen 3 ».</summary>
    public string Text => $"#{Label} {Count}";

    /// <summary>« #examen, 3 fiches », pour un lecteur d'écran.</summary>
    public string Name => $"#{Label}, {Dates.Plural(Count, SheetsText.CountOneOf, SheetsText.CountManyOf)}";

    /// <summary>La pastille filtre la liste.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hint))]
    public partial bool IsChosen { get; set; } = isChosen;

    /// <summary>Ce que fera un clic.</summary>
    public string Hint => IsChosen ? SheetsText.TagFilterRemove : SheetsText.TagFilterAdd;

    partial void OnIsChosenChanged(bool value) => toggled(this);

    /// <summary>
    /// Les étiquettes de ces fiches, dans l'ordre de <paramref name="order"/>, chacune avec le nombre de fiches qui la portent. La
    /// casse ne les distingue pas, comme dans le coffre ; la forme gardée est la plus fréquente.
    /// </summary>
    internal static List<(string Label, int Count)> Tally(IEnumerable<VaultSheet> sheets, IComparer<string> order) =>
    [
        .. sheets
            .SelectMany(sheet => sheet.Tags.Distinct(StringComparer.OrdinalIgnoreCase))
            .GroupBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .Select(group => (Label: group.GroupBy(form => form, StringComparer.Ordinal).OrderByDescending(form => form.Count()).First().Key, Count: group.Count()))
            .OrderBy(tag => tag.Label, order),
    ];
}
