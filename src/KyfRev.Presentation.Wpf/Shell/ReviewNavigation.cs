using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>Ouvre un élément du tableau de bord pour le réviser.</summary>
public interface IReviewOpener
{
    /// <summary>Ouvre une fiche ou un passage à réviser.</summary>
    void Open(DueItem item, bool activeRecall);

    /// <summary>Ouvre la fiche d'un rappel de consolidation.</summary>
    void OpenReminder(PendingReminder reminder);
}

/// <summary>
/// Ouvre les fiches et les passages hors du menu, comme dans la 0.x : une fiche, un passage, la
/// fiche d'un passage (« Voir dans la fiche »). « Retour » ramène à la vue d'où la page a été
/// ouverte, qui est alors relue.
/// </summary>
public sealed class ReviewNavigation : IReviewOpener
{
    private readonly SheetUseCases _sheets;
    private readonly ReviewUseCases _reviews;
    private readonly ReminderUseCases _reminders;
    private readonly TimeProvider _time;
    private ShellViewModel? _shell;

    /// <summary>La navigation, sur les cas d'usage des fiches, des révisions et des rappels.</summary>
    public ReviewNavigation(SheetUseCases sheets, ReviewUseCases reviews, ReminderUseCases reminders, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(reviews);
        ArgumentNullException.ThrowIfNull(reminders);
        ArgumentNullException.ThrowIfNull(time);
        _sheets = sheets;
        _reviews = reviews;
        _reminders = reminders;
        _time = time;
    }

    private ShellViewModel Shell => _shell ?? throw new InvalidOperationException("La coquille n'est pas encore assemblée.");

    /// <summary>La coquille où les pages s'ouvrent (elle est assemblée après les vues qui ouvrent ces pages).</summary>
    public void Attach(ShellViewModel shell)
    {
        ArgumentNullException.ThrowIfNull(shell);
        _shell = shell;
    }

    /// <inheritdoc/>
    public void Open(DueItem item, bool activeRecall)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Kind == ItemKind.Sheet)
        {
            OpenSheet(item.Id);
        }
        else
        {
            OpenPassage(item.Id, item.SheetId, activeRecall);
        }
    }

    /// <inheritdoc/>
    public void OpenReminder(PendingReminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        OpenSheet(reminder.SheetId);
    }

    /// <summary>Ouvre une fiche ; « Retour » ramène à la page courante.</summary>
    public void OpenSheet(string sheetId)
    {
        var shell = Shell;
        var from = shell.Current;
        var page = new SheetPageViewModel(_sheets, Bar, sheetId, () => shell.Navigate(from));
        shell.Navigate(new NavigationItem("fiche", ReviewText.SheetPage, Icons.Document, int.MaxValue, page));
        page.LoadCommand.Execute(null);
    }

    private void OpenPassage(string passageId, string sheetId, bool activeRecall)
    {
        var shell = Shell;
        var from = shell.Current;
        var page = new PassagePageViewModel(_sheets, Bar, passageId, sheetId, activeRecall, () => shell.Navigate(from), OpenSheet);
        shell.Navigate(new NavigationItem("passage", ReviewText.Passage, Icons.Highlighter, int.MaxValue, page));
        page.LoadCommand.Execute(null);
    }

    private ReviewBarViewModel Bar(ItemKind kind, string id, ReviewState state) => new(_reviews, _reminders, _time, kind, id, state);
}
