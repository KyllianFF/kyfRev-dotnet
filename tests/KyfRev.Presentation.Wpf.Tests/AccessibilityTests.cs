using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Automation;
using Axe.Windows.Automation;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// Les règles automatiques d'Accessibility Insights (FastPass), appliquées par Axe.Windows à
/// l'application elle-même : chaque vue, l'aide et la recherche, en thème clair et sombre, sans
/// aucune erreur. L'application est lancée dans son propre processus (Axe.Windows n'examine pas
/// le sien) et pilotée par UI Automation, comme le ferait un lecteur d'écran.
/// </summary>
[TestClass]
public sealed class AccessibilityTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);
    private static readonly string[] Menus = ["Navigation principale", "Navigation secondaire"];

    [TestMethod]
    [DataRow("Clair")]
    [DataRow("Sombre")]
    public void Every_view_passes_the_automated_accessibility_checks(string theme)
    {
        using var app = Process.Start(new ProcessStartInfo(ApplicationPath(), "--galerie") { UseShellExecute = false })!;
        try
        {
            var window = WindowOf(app);
            var scanner = ScannerFactory.CreateScanner(Config.Builder.ForProcessId(app.Id).WithOutputFileFormat(OutputFileFormat.None).Build());
            var errors = new List<string>();
            var views = new List<string>();
            void Check(string view)
            {
                var output = scanner.Scan(null);
                errors.AddRange(output.WindowScanOutputs.SelectMany(scan => scan.Errors).Select(error =>
                    $"{theme}, {view} : {error.Rule.ID} ({error.Rule.Description}) sur {Describe(error.Element)}"));
                views.Add(view);
            }

            Select(Named(window, ControlType.ListItem, theme));
            WaitUntil(window, () => Named(window, ControlType.ListItem, theme).GetCurrentPattern(SelectionItemPattern.Pattern) is SelectionItemPattern { Current.IsSelected: true });
            Check("galerie");

            var entries = Menus
                .SelectMany(list => Named(window, ControlType.List, list).FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)).Cast<AutomationElement>())
                .Select(item => item.Current.Name)
                .ToList();
            Assert.HasCount(7, entries);
            foreach (var entry in entries)
            {
                Select(Named(window, ControlType.ListItem, entry));
                // La page affichée porte son titre : deux textes à ce nom avec l'entrée du menu.
                WaitUntil(window, () => window.FindAll(TreeScope.Descendants, new AndCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
                    new PropertyCondition(AutomationElement.NameProperty, entry))).Count >= 2);
                Check(entry);
            }

            Invoke(Named(window, ControlType.Button, "Raccourcis clavier"));
            WaitUntil(window, () => window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Afficher les raccourcis clavier")) is not null);
            Check("aide");
            Invoke(Named(Named(window, ControlType.Pane, "Raccourcis clavier"), ControlType.Button, "Fermer"));

            Invoke(Named(window, ControlType.Button, "Rechercher une commande ou une vue"));
            WaitUntil(window, () => window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)) is not null);
            Check("recherche");

            Assert.HasCount(10, views);
            Assert.IsEmpty(errors, string.Join('\n', errors));
        }
        finally
        {
            app.Kill(entireProcessTree: true);
            app.WaitForExit();
        }
    }

    /// <summary>L'exécutable de l'application, compilé dans la même configuration que les tests.</summary>
    private static string ApplicationPath()
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var path = Path.Combine(Repository.Root, "src", "KyfRev.App", "bin", configuration, "net10.0-windows", "win-x64", "kyfRev.exe");
        Assert.IsTrue(File.Exists(path), path);
        return path;
    }

    /// <summary>La fenêtre de l'application : cherchée de nouveau à chaque fenêtre que Windows signale ouverte.</summary>
    private static AutomationElement WindowOf(Process app)
    {
        var condition = new AndCondition(
            new PropertyCondition(AutomationElement.ProcessIdProperty, app.Id),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window));
        using var opened = new AutoResetEvent(initialState: false);
        var handler = new AutomationEventHandler((_, _) => opened.Set());
        Automation.AddAutomationEventHandler(WindowPattern.WindowOpenedEvent, AutomationElement.RootElement, TreeScope.Children, handler);
        try
        {
            var clock = Stopwatch.StartNew();
            AutomationElement? window;
            while ((window = AutomationElement.RootElement.FindFirst(TreeScope.Children, condition)) is null)
            {
                var left = Patience - clock.Elapsed;
                Assert.IsTrue(left > TimeSpan.Zero && opened.WaitOne(left), "L'application n'a pas ouvert sa fenêtre à temps.");
            }
            return window;
        }
        finally
        {
            Automation.RemoveAutomationEventHandler(WindowPattern.WindowOpenedEvent, AutomationElement.RootElement, handler);
        }
    }

    private static AutomationElement Named(AutomationElement root, ControlType type, string name)
    {
        AutomationElement? element = null;
        WaitUntil(
            root,
            () => (element = root.FindFirst(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, type),
                new PropertyCondition(AutomationElement.NameProperty, name)))) is not null,
            $"{type.ProgrammaticName} « {name} »");
        return element!;
    }

    private static void Select(AutomationElement item) => ((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();

    private static void Invoke(AutomationElement button) => ((InvokePattern)button.GetCurrentPattern(InvokePattern.Pattern)).Invoke();

    /// <summary>
    /// Attend que <paramref name="condition"/> soit vraie : elle est réévaluée à chaque changement
    /// de structure ou de sélection que UI Automation signale sous <paramref name="scope"/>.
    /// </summary>
    private static void WaitUntil(AutomationElement scope, Func<bool> condition, [CallerArgumentExpression(nameof(condition))] string step = "")
    {
        using var changed = new AutoResetEvent(initialState: false);
        var structure = new StructureChangedEventHandler((_, _) => changed.Set());
        var property = new AutomationPropertyChangedEventHandler((_, _) => changed.Set());
        Automation.AddStructureChangedEventHandler(scope, TreeScope.Subtree, structure);
        Automation.AddAutomationPropertyChangedEventHandler(scope, TreeScope.Subtree, property, SelectionItemPattern.IsSelectedProperty, AutomationElement.NameProperty);
        try
        {
            var clock = Stopwatch.StartNew();
            while (!condition())
            {
                var left = Patience - clock.Elapsed;
                Assert.IsTrue(left > TimeSpan.Zero && changed.WaitOne(left), $"L'interface n'a pas répondu à temps : {step}");
            }
        }
        finally
        {
            Automation.RemoveStructureChangedEventHandler(scope, structure);
            Automation.RemoveAutomationPropertyChangedEventHandler(scope, property);
        }
    }

    private static string Describe(ElementInfo element) =>
        element.Properties.TryGetValue("ControlType", out var type) | element.Properties.TryGetValue("Name", out var name)
            ? $"{type} « {name} »"
            : "élément sans propriétés";
}
