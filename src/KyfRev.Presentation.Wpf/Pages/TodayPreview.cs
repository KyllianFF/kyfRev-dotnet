using KyfRev.Application.Model;
using KyfRev.Application.UseCases;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// L'aperçu de la ligne choisie dans « Aujourd'hui » ou « Fiches », sur les grands écrans : son texte, les
/// étapes de son cycle et ce qui a déjà été révisé. Un passage ou une définition n'y dévoile rien : seule la
/// révision le montre.
/// </summary>
public sealed class TodayPreview
{
    private TodayPreview(object row, string meta, string title)
    {
        Row = row;
        Meta = meta;
        Title = title;
        Steps = [];
        History = [];
    }

    /// <summary>La ligne de la liste, ouverte par le bouton de l'aperçu.</summary>
    public object Row { get; }

    /// <summary>« Réviser maintenant » ou « Ouvrir la fiche ».</summary>
    public string OpenLabel => Row is SheetRow ? SheetsText.OpenSheet : DashboardText.ReviewNow;

    /// <summary>« Fiche · Biologie · #partiel · cycle 1 · … ».</summary>
    public string Meta { get; }

    /// <summary>Titre de la fiche, terme, passage entre guillemets, ou « Passage à retrouver ».</summary>
    public string Title { get; }

    /// <summary>La fiche prête à lire ; rien pour un passage ou une définition.</summary>
    public SheetReading? Reading { get; private init; }

    /// <summary>La fiche a un texte à montrer.</summary>
    public bool HasBody => Reading is not null;

    /// <summary>Ce qu'il faut se rappeler, à la place du texte d'un passage caché ou d'une définition.</summary>
    public string? Prompt { get; private init; }

    /// <summary>Un rappel est demandé.</summary>
    public bool HasPrompt => Prompt is not null;

    /// <summary>La fiche n'a pas pu être lue.</summary>
    public string? Error { get; private init; }

    /// <summary>Un refus est affiché.</summary>
    public bool HasError => Error is not null;

    /// <summary>Les étapes du cycle.</summary>
    public IReadOnlyList<StepMark> Steps { get; private init; }

    /// <summary>Le cycle est connu.</summary>
    public bool HasSteps => Steps.Count > 0;

    /// <summary>Les étapes validées, de la plus récente à la plus ancienne (« J3 : validée le … »).</summary>
    public IReadOnlyList<string> History { get; private init; }

    /// <summary>Au moins une étape est validée.</summary>
    public bool HasHistory => History.Count > 0;

    /// <summary>« Historique · 6 ».</summary>
    public string HistoryLabel => DashboardText.HistoryCountOf(History.Count);

    /// <summary>L'aperçu de <paramref name="row"/>, dans la fiche <paramref name="sheet"/>.</summary>
    internal static TodayPreview Of(object row, OpenedSheet sheet, SheetUseCases sheets) => row switch
    {
        DueItemRow { Item.Kind: ItemKind.Passage } passage => new TodayPreview(row, passage.Meta, passage.HeadingText)
        {
            Prompt = passage.Heading switch
            {
                DueItemHeading.Term => ReviewText.RecallDefinition,
                DueItemHeading.Hidden => ReviewText.RecallPassage,
                _ => null,
            },
        }.With(sheet.Annotations.FirstOrDefault(annotation => annotation.Id == passage.Item.Id)?.Review ?? sheet.Review),
        DueItemRow item => new TodayPreview(row, item.Meta, sheet.Title) { Reading = SheetReading.Of(sheet, sheets) }.With(sheet.Review),
        SheetRow listed => new TodayPreview(row, listed.Details, sheet.Title) { Reading = SheetReading.Of(sheet, sheets) }.With(sheet.Review),
        _ => new TodayPreview(row, ((ReminderRow)row).Meta, sheet.Title) { Reading = SheetReading.Of(sheet, sheets) }.With(sheet.Review),
    };

    /// <summary>La fiche de <paramref name="row"/> n'a pas pu être lue : <paramref name="error"/>.</summary>
    internal static TodayPreview Failed(object row, string? error) => row switch
    {
        DueItemRow item => new TodayPreview(row, item.Meta, item.HeadingText) { Error = error },
        SheetRow listed => new TodayPreview(row, listed.Details, listed.Title) { Error = error },
        _ => new TodayPreview(row, ((ReminderRow)row).Meta, ((ReminderRow)row).Title) { Error = error },
    };

    private TodayPreview With(ReviewState review)
    {
        var steps = StepMark.Of(review);
        return new TodayPreview(Row, Meta, Title)
        {
            Reading = Reading,
            Prompt = Prompt,
            Steps = steps,
            History = [.. steps.Where(step => step.IsDone).Reverse().Select(step => step.Description)],
        };
    }
}
