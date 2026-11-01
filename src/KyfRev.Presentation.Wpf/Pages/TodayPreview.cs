using KyfRev.Application.Model;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// L'aperçu de la ligne choisie dans « Aujourd'hui », sur les grands écrans : son texte, les étapes de
/// son cycle et ce qui a déjà été révisé. Un passage ou une définition n'y dévoile rien : seule la
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

    /// <summary>La ligne de la liste, ouverte par « Réviser maintenant ».</summary>
    public object Row { get; }

    /// <summary>« Fiche · Biologie · #partiel · cycle 1 · … ».</summary>
    public string Meta { get; }

    /// <summary>Titre de la fiche, terme, passage entre guillemets, ou « Passage à retrouver ».</summary>
    public string Title { get; }

    /// <summary>Texte de la fiche ; rien pour un passage ou une définition.</summary>
    public string? Body { get; private init; }

    /// <summary>La fiche a un texte à montrer.</summary>
    public bool HasBody => Body is not null;

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

    /// <summary>L'aperçu de <paramref name="row"/>, dans la fiche <paramref name="sheet"/>.</summary>
    internal static TodayPreview Of(object row, OpenedSheet sheet) => row switch
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
        DueItemRow item => new TodayPreview(row, item.Meta, sheet.Title) { Body = sheet.Body }.With(sheet.Review),
        _ => new TodayPreview(row, ((ReminderRow)row).Meta, sheet.Title) { Body = sheet.Body }.With(sheet.Review),
    };

    /// <summary>La fiche de <paramref name="row"/> n'a pas pu être lue : <paramref name="error"/>.</summary>
    internal static TodayPreview Failed(object row, string? error) => row is DueItemRow item
        ? new TodayPreview(row, item.Meta, item.HeadingText) { Error = error }
        : new TodayPreview(row, ((ReminderRow)row).Meta, ((ReminderRow)row).Title) { Error = error };

    private TodayPreview With(ReviewState review)
    {
        var steps = StepMark.Of(review);
        return new TodayPreview(Row, Meta, Title)
        {
            Body = Body,
            Prompt = Prompt,
            Steps = steps,
            History = [.. steps.Where(step => step.IsDone).Reverse().Select(step => step.Description)],
        };
    }
}
