using System.Diagnostics;
using System.Globalization;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using FlauiApplication = FlaUI.Core.Application;

namespace KyfRev.Acceptance.Tests;

/// <summary>
/// La vraie application, dans son propre processus, pilotée par UI Automation : les boutons et les
/// lignes sont invoqués sans souris ni clavier, ce qui laisse le poste libre et ne dépend pas de la
/// fenêtre au premier plan.
/// </summary>
internal sealed class RunningApplication : IDisposable
{
    public const string Overdue = "En retard";
    public const string Today = "Aujourd'hui";
    public const string Reminders = "Rappels de consolidation";
    public const string Upcoming = "À venir — 7 jours";

    private static readonly string[] Groups = [Overdue, Today, Reminders, Upcoming];
    private static readonly string[] RowKinds = ["Fiche, ", "Passage, ", "Définition, ", "Rappel, "];
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    // Le tout premier démarrage du moteur web sur une machine neuve a pris jusqu'à 39 s sur le runner de la CI.
    private static readonly TimeSpan EnginePatience = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(100);

    private readonly Process _process;
    private readonly FlauiApplication _application;
    private readonly UIA3Automation _automation = new();
    private readonly Window _window;

    public RunningApplication(string folder, DateOnly day, IReadOnlyDictionary<string, string>? environment = null)
    {
        var start = ScenarioClock.At(day).ToString("O", CultureInfo.InvariantCulture);
        var information = new ProcessStartInfo(HostPath(), [folder, start]) { UseShellExecute = false };
        foreach (var (name, value) in environment ?? new Dictionary<string, string>())
        {
            information.Environment[name] = value;
        }
        _process = Process.Start(information)!;
        _application = FlauiApplication.Attach(_process.Id);
        _window = _application.GetMainWindow(_automation, Patience) ?? throw new InvalidOperationException("La fenêtre de l'application ne s'est pas ouverte.");
    }

    /// <summary>Les lignes d'un groupe de la liste d'« Aujourd'hui », une fois le tableau de bord lu.</summary>
    public IReadOnlyList<string> Rows(string group)
    {
        var rows = new List<string>();
        var current = "";
        foreach (var name in RowNames())
        {
            if (Groups.FirstOrDefault(known => name.StartsWith(known + ", ", StringComparison.Ordinal)) is { } header)
            {
                current = header;
            }
            else if (current == group && RowKinds.Any(kind => name.StartsWith(kind, StringComparison.Ordinal)))
            {
                rows.Add(name);
            }
        }
        return rows;
    }

    /// <summary>La ligne du groupe qui parle de ce titre, ou rien.</summary>
    public string? Row(string group, string title) => Rows(group).FirstOrDefault(row => row.Contains(title, StringComparison.Ordinal));

    /// <summary>Attend qu'une ligne apparaisse dans un groupe : la liste se relit au retour sur la vue.</summary>
    public string WaitRow(string group, string title) =>
        Retry.WhileNull(() => Row(group, title), Patience, Interval, throwOnTimeout: true, timeoutMessage: $"« {title} » n'est pas dans « {group} ».").Result!;

    /// <summary>Attend qu'une ligne disparaisse d'un groupe : la liste se relit au retour d'une fiche.</summary>
    public void WaitWithoutRow(string group, string title) =>
        Assert.IsTrue(
            Retry.WhileTrue(() => Row(group, title) is not null, Patience, Interval).Result,
            $"« {title} » est resté dans « {group} » : {string.Join(" | ", Rows(group))}");

    /// <summary>Ouvre une ligne comme d'un clic, qui la choisit puis l'ouvre : la page de la fiche, du passage ou du rappel.</summary>
    public void Open(string group, string title)
    {
        var name = Retry.WhileNull(() => Row(group, title), Patience, Interval, throwOnTimeout: true, timeoutMessage: $"« {title} » n'est pas dans « {group} ».").Result!;
        var row = List().FindAllChildren().First(child => child.Name == name);
        row.Patterns.SelectionItem.Pattern.Select();
        row.Patterns.Invoke.Pattern.Invoke();
        Wait("BackButton");
    }

    /// <summary>Revient à la vue d'où la fiche a été ouverte.</summary>
    public void Back()
    {
        Press("BackButton");
        Retry.WhileTrue(() => Find("BackButton") is not null, Patience, Interval, throwOnTimeout: true, timeoutMessage: "La fiche ne s'est pas refermée.");
    }

    /// <summary>Valide la révision, et attend que ce soit fait.</summary>
    public void Validate()
    {
        Press("ValidateButton");
        Retry.WhileTrue(() => Find("ValidateButton") is not null, Patience, Interval, throwOnTimeout: true, timeoutMessage: "La révision n'a pas été validée.");
    }

    /// <summary>Reporte la révision au lendemain, et attend que ce soit fait.</summary>
    public void PostponeUntilTomorrow()
    {
        var before = Wait("Due").Name;
        Press("PostponeButton");
        MenuEntry("PostponeTomorrow").Patterns.Invoke.Pattern.Invoke();
        Retry.WhileTrue(() => Wait("Due").Name == before, Patience, Interval, throwOnTimeout: true, timeoutMessage: "La révision n'a pas été reportée.");
    }

    /// <summary>Rappel : « Fiche connue ».</summary>
    public void AnswerKnown()
    {
        Press("Known");
        WaitAnswered();
    }

    /// <summary>Rappel : « Fiche à revoir entièrement », confirmé.</summary>
    public void AnswerReviewEntirely()
    {
        Press("ReviewEntirely");
        Confirm();
        WaitAnswered();
    }

    /// <summary>Rappel : « Certains points sont à revoir » : les passages à marquer apparaissent.</summary>
    public void StartMarking()
    {
        Press("SomePoints");
        Wait("FinishMarking");
    }

    /// <summary>Remet un passage à J0, et attend que ce soit fait.</summary>
    public void MarkPassage(string passage)
    {
        var button = Retry.WhileNull(
            () => Wait("Passages").FindAllDescendants().FirstOrDefault(element => element.Name.Contains(passage, StringComparison.Ordinal) && element.Patterns.Invoke.IsSupported),
            Patience,
            Interval,
            throwOnTimeout: true,
            timeoutMessage: $"Le passage « {passage} » n'est pas proposé.").Result!;
        var before = Wait("MarkedLabel").Name;
        button.Patterns.Invoke.Pattern.Invoke();
        Retry.WhileTrue(() => Wait("MarkedLabel").Name == before, Patience, Interval, throwOnTimeout: true, timeoutMessage: "Le passage n'a pas été marqué.");
    }

    /// <summary>« Terminer » ; une réponse enregistrée referme le marquage, sinon une question s'ouvre.</summary>
    public void FinishMarking() => Press("FinishMarking");

    /// <summary>Attend que le rappel soit répondu.</summary>
    public void WaitAnswered() =>
        Retry.WhileTrue(() => Find("Known") is not null || Find("FinishMarking") is not null, Patience, Interval, throwOnTimeout: true, timeoutMessage: "Le rappel n'a pas été répondu.");

    /// <summary>Le bouton principal du dialogue ouvert.</summary>
    public void Confirm() => Press("PART_PrimaryButton");

    /// <summary>Le bouton « Annuler » du dialogue ouvert, et attend sa fermeture.</summary>
    public void Dismiss()
    {
        Press("PART_SecondaryButton");
        Retry.WhileTrue(() => Find("PART_SecondaryButton") is not null, Patience, Interval, throwOnTimeout: true, timeoutMessage: "Le dialogue ne s'est pas refermé.");
    }

    /// <summary>Fait défiler la fiche jusqu'en bas ; rend la position atteinte, en pourcentage.</summary>
    public double ScrollToEnd()
    {
        var scrollable = Retry.WhileNull(
            () => _window.FindAllDescendants().FirstOrDefault(element => element.Patterns.Scroll.IsSupported && element.Patterns.Scroll.Pattern.VerticallyScrollable),
            Patience,
            Interval,
            throwOnTimeout: true,
            timeoutMessage: "La fiche ne défile pas.").Result!;
        var scroll = scrollable.Patterns.Scroll.Pattern;
        scroll.SetScrollPercent(-1, 100);
        return scroll.VerticalScrollPercent;
    }

    /// <summary>La position du bouton « Retour » à l'écran : une fiche défilée ne doit pas l'emporter.</summary>
    public int BackPosition() => Wait("BackButton").BoundingRectangle.Top;

    /// <summary>Sélectionne un mot du texte de la fiche et le surligne avec la touche H.</summary>
    public void HighlightInText(string exact)
    {
        var view = Wait("SheetText");
        var document = view.FindAllDescendants().Append(view).First(element => element.Patterns.Text.IsSupported);
        document.Patterns.Text.Pattern.DocumentRange.FindText(exact, backward: false, ignoreCase: false).Select();
        view.SetForeground();
        view.Focus();
        Keyboard.Type(VirtualKeyShort.KEY_H);
    }

    /// <summary>Attend ce décompte sur le bouton des passages.</summary>
    public void WaitPassages(string count) =>
        Retry.WhileFalse(() => Find("PassagesToggle")?.Name.Contains(count, StringComparison.Ordinal) == true, Patience, Interval, throwOnTimeout: true, timeoutMessage: $"Le décompte {count} des passages n'arrive pas.");

    /// <summary>Passe à l'écriture par le bouton « Écrire » : le texte mis en forme s'ouvre.</summary>
    public void StartWriting()
    {
        Wait("WriteToggle").Patterns.Toggle.Pattern.Toggle();
        WaitTextFocused();
    }

    /// <summary>Revient à la lecture par le raccourci Ctrl+E.</summary>
    public void StopWriting()
    {
        WaitTextFocused();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_E);
        Retry.WhileFalse(() => Find("ReadToggle")?.Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On, Patience, Interval, throwOnTimeout: true, timeoutMessage: "L'écriture reste ouverte.");
    }

    /// <summary>Tape un texte au clavier, à la place du curseur.</summary>
    public void TypeInSheet(string text)
    {
        WaitTextFocused();
        Keyboard.Type(text);
    }

    /// <summary>Ouvre le code Markdown à gauche, par son bouton.</summary>
    public void ShowMarkdown()
    {
        Wait("MarkdownToggle").Patterns.Toggle.Pattern.Toggle();
        Wait("SheetSource");
    }

    /// <summary>
    /// La frappe ne part qu'une fois le texte prêt à la recevoir. Le texte mis en forme s'annonce sous son nom : c'est
    /// aussi ce qu'entend un lecteur d'écran.
    /// </summary>
    private void WaitTextFocused()
    {
        var seen = string.Empty;
        _window.SetForeground();
        var ready = Retry.WhileFalse(
            () =>
            {
                if (Find("SheetSource") is { IsOffscreen: false } source)
                {
                    source.Focus();
                    return source.Properties.HasKeyboardFocus.ValueOrDefault;
                }
                var focused = _automation.FocusedElement();
                seen = $"{focused?.Properties.ControlType.ValueOrDefault} « {focused?.Name} »";
                return focused?.Name == "Texte de la fiche";
            },
            EnginePatience,
            Interval);
        Assert.IsTrue(ready.Success, $"Le texte de la fiche ne prend pas le focus : {seen} l'a.");
    }

    /// <summary>Attend que tout soit enregistré.</summary>
    public void WaitSaved() => WaitSaveState("Enregistré");

    /// <summary>Attend cet état de l'enregistrement.</summary>
    public void WaitSaveState(string state) =>
        Retry.WhileFalse(() => Find("SaveStateText")?.Name == state, Patience, Interval, throwOnTimeout: true, timeoutMessage: $"L'état « {state} » n'arrive pas.");

    /// <summary>Le Markdown du champ d'écriture, tel que le lit un lecteur d'écran.</summary>
    public string WrittenText() => Wait("SheetSource").Patterns.Value.Pattern.Value.Value;

    /// <summary>Garde le texte en cours d'écriture, pendant un conflit.</summary>
    public void KeepMyVersion() => Press("KeepMineButton");

    /// <summary>Reprend le texte du disque, pendant un conflit.</summary>
    public void ReloadDiskVersion() => Press("ReloadDiskButton");

    /// <summary>Attend que le décompte des passages à revoir atteigne ce nombre.</summary>
    public void WaitMarked(string count) =>
        Retry.WhileFalse(() => Find("MarkedLabel")?.Name.Contains(count, StringComparison.Ordinal) == true, Patience, Interval, throwOnTimeout: true, timeoutMessage: $"Le décompte n'atteint pas {count}.");

    /// <summary>Le texte de la fiche, tel que le lit un lecteur d'écran.</summary>
    public string SheetText() => Retry.WhileEmpty(
        () => Wait("SheetText").FindAllDescendants().Append(Wait("SheetText")).FirstOrDefault(element => element.Patterns.Text.IsSupported)?.Patterns.Text.Pattern.DocumentRange.GetText(-1) ?? string.Empty,
        Patience,
        Interval,
        throwOnTimeout: true,
        timeoutMessage: "Le texte de la fiche ne se lit pas.").Result!;

    /// <summary>Ouvre une vue du menu, comme d'un clic sur son entrée, et attend son titre.</summary>
    public void OpenView(string name)
    {
        var entry = Retry.WhileNull(
            () => Wait("MainNavigation").FindAllChildren().Concat(Wait("SecondaryNavigation").FindAllChildren()).FirstOrDefault(child => child.Name == name),
            Patience,
            Interval,
            throwOnTimeout: true,
            timeoutMessage: $"« {name} » n'est pas dans le menu.").Result!;
        entry.Patterns.SelectionItem.Pattern.Select();
        Retry.WhileFalse(() => Find("Heading")?.Name == name, Patience, Interval, throwOnTimeout: true, timeoutMessage: $"La vue « {name} » ne s'est pas ouverte.");
    }

    /// <summary>« Nouvelle fiche » : écrit le titre et confirme ; la fiche créée s'ouvre.</summary>
    public void CreateSheet(string title)
    {
        Press("NewSheetButton");
        Wait("NewTitleBox").AsTextBox().Text = title;
        Confirm();
        Wait("BackButton");
    }

    /// <summary>Cherche dans la vue ouverte.</summary>
    public void Search(string text) => Wait("SearchBox").AsTextBox().Text = text;

    /// <summary>Change la séquence des étapes dans la vue « Paramètres ».</summary>
    public void ChangeSteps(string steps) => Wait("StepsBox").AsTextBox().Text = steps;

    /// <summary>Enregistre les réglages, et attend que la vue le confirme.</summary>
    public void SaveSettings()
    {
        Press("SaveSettings");
        Wait("SavedMessage");
    }

    /// <summary>La ligne d'une fiche dans la vue « Fiches », une fois lue.</summary>
    public string WaitSheetRow(string title) => WaitListRow(title).Name;

    /// <summary>Attend qu'une fiche quitte la liste de la vue « Fiches ».</summary>
    public void WaitWithoutSheetRow(string title) =>
        Retry.WhileTrue(() => FindListRow(title) is not null, Patience, Interval, throwOnTimeout: true, timeoutMessage: $"« {title} » est restée dans la liste.");

    /// <summary>Ouvre une fiche de la vue « Fiches », comme d'un clic.</summary>
    public void OpenSheetRow(string title)
    {
        var row = WaitListRow(title);
        row.Patterns.SelectionItem.Pattern.Select();
        row.Patterns.Invoke.Pattern.Invoke();
        Wait("BackButton");
    }

    /// <summary>La ligne d'une fiche dans la vue « Archives », une fois lue.</summary>
    public string ArchivedRow(string title) => WaitArchive("OpenRow", title).Name;

    /// <summary>Ouvre une fiche depuis la vue « Archives ».</summary>
    public void OpenArchived(string title)
    {
        WaitArchive("OpenRow", title).Patterns.Invoke.Pattern.Invoke();
        Wait("BackButton");
    }

    /// <summary>« Exclure des rappels » ou « Réinclure » : attend que la ligne ait changé.</summary>
    public void ToggleArchived(string title)
    {
        var before = WaitArchive("ToggleRow", title).Name;
        WaitArchive("ToggleRow", title).Patterns.Invoke.Pattern.Invoke();
        Retry.WhileTrue(() => WaitArchive("ToggleRow", title).Name == before, Patience, Interval, throwOnTimeout: true, timeoutMessage: "Le choix des rappels n'a pas changé.");
    }

    /// <summary>« Remettre en révision… », confirmé : attend que la fiche ait quitté la liste.</summary>
    public void RestartArchived(string title)
    {
        WaitArchive("RestartRow", title).Patterns.Invoke.Pattern.Invoke();
        Confirm();
        Retry.WhileTrue(() => FindArchive("OpenRow", title) is not null, Patience, Interval, throwOnTimeout: true, timeoutMessage: "La fiche est restée dans les archives.");
    }

    /// <summary>Le texte de l'alerte de saturation des rappels ; rien sans alerte.</summary>
    public string? PriorityWarning()
    {
        _ = RowNames();
        return Find("PriorityWarning")?.Name;
    }

    /// <summary>Referme la fenêtre, comme d'un clic sur la croix, et attend la fin du processus.</summary>
    public void Dispose()
    {
        if (!_process.HasExited)
        {
            _window.Patterns.Window.Pattern.Close();
            if (!_process.WaitForExit(Patience))
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit();
            }
        }
        _automation.Dispose();
        _application.Dispose();
        _process.Dispose();
    }

    private static string HostPath()
    {
        var output = new DirectoryInfo(AppContext.BaseDirectory);
        var root = output;
        while (!File.Exists(Path.Combine(root!.FullName, "KyfRev.slnx")))
        {
            root = root.Parent;
        }
        var path = Path.Combine(root.FullName, "tools", "acceptance-host", "bin", output.Parent!.Name, "net10.0-windows", "win-x64", "KyfRev.Acceptance.Host.exe");
        Assert.IsTrue(File.Exists(path), path);
        return path;
    }

    private string[] RowNames() =>
        Retry.WhileEmpty(() => List().FindAllChildren().Select(child => child.Name).ToArray(), Patience, Interval, throwOnTimeout: true, timeoutMessage: "Le tableau de bord ne s'est pas affiché.").Result!;

    private AutomationElement List() => Wait("Items");

    private AutomationElement? FindListRow(string title) =>
        List().FindAllChildren().FirstOrDefault(child => child.Name.StartsWith(title + ",", StringComparison.Ordinal));

    private AutomationElement WaitListRow(string title) =>
        Retry.WhileNull(() => FindListRow(title), Patience, Interval, throwOnTimeout: true, timeoutMessage: $"« {title} » n'est pas dans la liste.").Result!;

    private AutomationElement? FindArchive(string id, string title) =>
        _window.FindAllDescendants(condition => condition.ByAutomationId(id)).FirstOrDefault(button => button.Name.Contains(title, StringComparison.Ordinal));

    private AutomationElement WaitArchive(string id, string title) =>
        Retry.WhileNull(() => FindArchive(id, title), Patience, Interval, throwOnTimeout: true, timeoutMessage: $"« {title} » n'est pas dans les archives.").Result!;

    private AutomationElement? Find(string id) => _window.FindFirstDescendant(condition => condition.ByAutomationId(id));

    private AutomationElement Wait(string id) =>
        Retry.WhileNull(() => Find(id), Patience, Interval, throwOnTimeout: true, timeoutMessage: $"« {id} » n'est pas apparu.").Result!;

    private void Press(string id)
    {
        var button = Wait(id);
        Retry.WhileFalse(() => button.IsEnabled, Patience, Interval, throwOnTimeout: true, timeoutMessage: $"« {id} » reste inactif.");
        button.Patterns.Invoke.Pattern.Invoke();
    }

    // Un menu s'ouvre dans sa propre petite fenêtre, sous le bureau et non sous celle de l'application.
    private AutomationElement MenuEntry(string id) =>
        Retry.WhileNull(
            () => _automation.GetDesktop()
                .FindAllChildren(condition => condition.ByProcessId(_process.Id))
                .Select(window => window.FindFirstDescendant(condition => condition.ByAutomationId(id)))
                .FirstOrDefault(found => found is not null),
            Patience,
            Interval,
            throwOnTimeout: true,
            timeoutMessage: $"Le menu « {id} » ne s'est pas ouvert.").Result!;
}
