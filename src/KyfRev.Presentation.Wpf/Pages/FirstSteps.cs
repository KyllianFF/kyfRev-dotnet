using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Ce que la première utilisation a déjà montré sur cet ordinateur, ou qu'on lui a demandé de ne plus montrer : les pas
/// faits, la carte masquée, les aides lues. Retenu avec les préférences de l'ordinateur ; sans elles, le temps de la séance.
/// </summary>
public sealed class FirstUse
{
    /// <summary>La carte « Premiers pas » a été masquée.</summary>
    public const string StepsHidden = "pas-masques";

    /// <summary>Une première fiche a été écrite.</summary>
    public const string SheetWritten = "pas-fiche";

    /// <summary>Une première révision a été validée.</summary>
    public const string SheetReviewed = "pas-revision";

    /// <summary>Un premier passage a été surligné, ou un premier mot défini.</summary>
    public const string PassageMarked = "pas-passage";

    /// <summary>L'aide de la première révision a été lue.</summary>
    public const string ReviewTipRead = "aide-revision";

    /// <summary>L'aide de la première fiche écrite a été lue.</summary>
    public const string WritingTipRead = "aide-ecriture";

    /// <summary>« Ne plus montrer d'aide » : aucune aide en contexte ne se montre plus.</summary>
    public const string NoTips = "sans-aide";

    private readonly MachineSettings? _machine;
    private readonly HashSet<string> _session = new(StringComparer.Ordinal);

    /// <summary>La mémoire de la première utilisation, retenue dans <paramref name="machine"/> s'il y en a.</summary>
    public FirstUse(MachineSettings? machine = null)
    {
        _machine = machine;
    }

    /// <summary>Ce qui est retenu vient de changer.</summary>
    public event EventHandler? Changed;

    /// <summary>Cela a déjà été vu, fait ou demandé.</summary>
    public bool Has(string mark) => Marks().Contains(mark);

    /// <summary>Retient cela ; rien ne change si c'est déjà retenu.</summary>
    public void Remember(string mark)
    {
        if (Has(mark))
        {
            return;
        }
        if (_machine is null)
        {
            _session.Add(mark);
        }
        else
        {
            var written = string.Join(';', Marks().Append(mark).Order(StringComparer.Ordinal));
            _machine.Update(preferences => preferences with { FirstUse = written });
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private HashSet<string> Marks() => _machine is null
        ? _session
        : [.. _machine.Current.FirstUse.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}

/// <summary>Un des premiers pas : ce qu'il y a à faire, et s'il est fait.</summary>
public sealed partial class FirstStep : ObservableObject
{
    internal FirstStep(int number, string title, string help)
    {
        Number = number.ToString(CultureInfo.CurrentCulture);
        Title = title;
        Help = help;
    }

    /// <summary>Son rang : « 1 ».</summary>
    public string Number { get; }

    /// <summary>Ce qu'il y a à faire.</summary>
    public string Title { get; }

    /// <summary>Ce que cela donne.</summary>
    public string Help { get; }

    /// <summary>Il est fait.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Spoken))]
    public partial bool IsDone { get; internal set; }

    /// <summary>C'est le prochain à faire.</summary>
    [ObservableProperty]
    public partial bool IsCurrent { get; internal set; }

    /// <summary>C'est le premier pas, pas encore fait : « Nouvelle fiche » se propose là.</summary>
    [ObservableProperty]
    public partial bool OffersNewSheet { get; internal set; }

    /// <summary>Ce que dit le lecteur d'écran.</summary>
    public string Spoken => IsDone ? FirstStepsText.DoneOf(Title) : $"{Title}. {Help}";

    /// <inheritdoc/>
    public override string ToString() => Spoken;
}

/// <summary>
/// La carte « Premiers pas » de la vue « Aujourd'hui », dans un coffre qui débute : écrire une première fiche, la réviser,
/// surligner un passage ou définir un mot. Chaque pas se coche seul, d'après ce que montre le tableau de bord ; la carte
/// tient la place de l'état vide tant qu'il n'y a rien à réviser, et ne revient plus une fois les trois pas faits ou sur
/// « Masquer ».
/// </summary>
public sealed partial class FirstSteps : ObservableObject
{
    /// <summary>Au-delà de ce nombre de fiches, ou dès qu'une fiche est archivée, le coffre ne débute plus : la carte ne s'y montre pas.</summary>
    internal const int YoungVault = 5;

    /// <summary>L'étape qu'attend une fiche qui vient d'être écrite : au-delà, elle a été révisée.</summary>
    private const string FirstStep = "J1";

    private readonly FirstUse _memory;
    private readonly Action? _openGuide;

    /// <summary>La carte, sur ce que <paramref name="memory"/> retient ; <paramref name="openGuide"/> ouvre le guide dans kyfRev.</summary>
    public FirstSteps(FirstUse memory, Action? openGuide = null)
    {
        ArgumentNullException.ThrowIfNull(memory);
        _memory = memory;
        _openGuide = openGuide;
        Steps =
        [
            new FirstStep(1, FirstStepsText.WriteTitle, FirstStepsText.WriteHelp),
            new FirstStep(2, FirstStepsText.ReviewTitle, FirstStepsText.ReviewHelp),
            new FirstStep(3, FirstStepsText.MarkTitle, FirstStepsText.MarkHelp),
        ];
        Show();
    }

    /// <summary>Les trois pas, dans l'ordre.</summary>
    public IReadOnlyList<FirstStep> Steps { get; }

    /// <summary>« 1 sur 3 ».</summary>
    [ObservableProperty]
    public partial string Progress { get; private set; } = string.Empty;

    /// <summary>Le guide peut s'ouvrir d'ici.</summary>
    public bool CanOpenGuide => _openGuide is not null;

    /// <summary>« Nouvelle fiche », depuis le premier pas : la vue « Aujourd'hui » le branche sur sa propre commande.</summary>
    public Action? NewSheet { get; set; }

    /// <summary>La carte vient d'être masquée : la vue « Aujourd'hui » refait sa liste.</summary>
    public Action? Hidden { get; set; }

    /// <summary>Le nom lu par le lecteur d'écran.</summary>
    public override string ToString() => $"{FirstStepsText.Title}, {Progress}";

    /// <summary>
    /// Coche les pas que ce tableau de bord montre faits, et dit si la carte se montre : pas masquée, pas finie, dans un
    /// coffre qui débute.
    /// </summary>
    internal bool Follow(Dashboard dashboard)
    {
        ArgumentNullException.ThrowIfNull(dashboard);
        var items = dashboard.Overdue.Concat(dashboard.Today).Concat(dashboard.Upcoming).Concat(dashboard.Suspended).ToList();
        if (dashboard.ActiveSheets + dashboard.ArchivedSheets > 0)
        {
            _memory.Remember(FirstUse.SheetWritten);
        }
        if (dashboard.ArchivedSheets > 0 || dashboard.RecentReviews.Sum(day => day.Reviews) > 0 || items.Exists(item => item.Kind == ItemKind.Sheet && (item.Step != FirstStep || item.Cycle > 1)))
        {
            _memory.Remember(FirstUse.SheetReviewed);
        }
        if (items.Exists(item => item.Kind != ItemKind.Sheet))
        {
            _memory.Remember(FirstUse.PassageMarked);
        }
        Show();
        return !_memory.Has(FirstUse.StepsHidden) && Steps.Any(step => !step.IsDone)
            && dashboard.ArchivedSheets == 0 && dashboard.ActiveSheets <= YoungVault;
    }

    /// <summary>Le premier pas : ouvre la fenêtre « Nouvelle fiche » de la vue.</summary>
    [RelayCommand]
    private void StartSheet() => NewSheet?.Invoke();

    /// <summary>« Lire le guide ».</summary>
    [RelayCommand]
    private void OpenGuide() => _openGuide?.Invoke();

    /// <summary>« Masquer les premiers pas » : la carte ne revient plus sur cet ordinateur.</summary>
    [RelayCommand]
    private void Hide()
    {
        _memory.Remember(FirstUse.StepsHidden);
        Hidden?.Invoke();
    }

    private void Show()
    {
        string[] marks = [FirstUse.SheetWritten, FirstUse.SheetReviewed, FirstUse.PassageMarked];
        var current = true;
        for (var index = 0; index < Steps.Count; index++)
        {
            var step = Steps[index];
            step.IsDone = _memory.Has(marks[index]);
            step.IsCurrent = !step.IsDone && current;
            step.OffersNewSheet = index == 0 && !step.IsDone;
            current &= step.IsDone;
        }
        Progress = FirstStepsText.ProgressOf(Steps.Count(step => step.IsDone), Steps.Count);
    }
}

/// <summary>Les textes de la carte « Premiers pas ».</summary>
public static class FirstStepsText
{
    /// <summary>Le titre de la carte.</summary>
    public const string Title = "Premiers pas";

    /// <summary>Premier pas.</summary>
    public const string WriteTitle = "Écrire votre première fiche";

    /// <summary>Ce qu'il donne.</summary>
    public const string WriteHelp = "Un cours, un chapitre, une leçon. L'écrire compte pour J0 : sa première révision, J1, sera due demain.";

    /// <summary>Deuxième pas.</summary>
    public const string ReviewTitle = "La réviser une première fois";

    /// <summary>Ce qu'il donne.</summary>
    public const string ReviewHelp = "Demain, elle sera dans cette liste : relisez-la, puis « Valider ». Elle reviendra à J3, J7, J14 et J30.";

    /// <summary>Troisième pas.</summary>
    public const string MarkTitle = "Surligner un passage ou définir un mot";

    /// <summary>Ce qu'il donne.</summary>
    public const string MarkHelp = "Dans une fiche, un clic droit sur le texte. Ce que vous voulez retenir à part a son propre calendrier.";

    /// <summary>Le bouton du premier pas.</summary>
    public const string NewSheet = "Nouvelle fiche";

    /// <summary>Le lien vers le guide.</summary>
    public const string ReadGuide = "Lire le guide";

    /// <summary>Le lien qui masque la carte pour de bon.</summary>
    public const string Hide = "Masquer les premiers pas";

    /// <summary>« 1 sur 3 ».</summary>
    public static string ProgressOf(int done, int total) => string.Create(CultureInfo.CurrentCulture, $"{done} sur {total}");

    /// <summary>Un pas fait, pour le lecteur d'écran.</summary>
    public static string DoneOf(string title) => $"{title} : fait";
}
