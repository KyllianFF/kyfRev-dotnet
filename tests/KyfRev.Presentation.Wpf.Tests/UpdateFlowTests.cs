using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using KyfRev.Application;
using KyfRev.Application.Ports;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Controls;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// La mise à jour, de la recherche au redémarrage : à la demande (le bouton télécharge aussitôt), et au démarrage (proposée,
/// ou retardée à la prochaine ouverture) ; puis « Redémarrer maintenant » ou « Plus tard », et ce qu'elle est devenue au
/// démarrage suivant. Sans réseau ni installation : une annonce signée par une clé d'essai, un faux téléchargement, un
/// faux installateur.
/// </summary>
[TestClass]
public sealed class UpdateFlowTests
{
    private static readonly byte[] Installer = [.. Enumerable.Range(0, 4096).Select(index => (byte)index)];

    [TestMethod]
    public async Task The_button_downloads_checks_and_offers_to_restart_now_or_later()
    {
        using var scene = new Scene();
        Assert.AreEqual(SettingsHubText.UpdateHelp, scene.Flow.Help);

        await scene.Flow.CheckCommand.ExecuteAsync(null);

        Assert.AreEqual(UpdateStage.Ready, scene.Flow.Stage);
        Assert.AreEqual(1, scene.Downloads.Calls);
        Assert.AreEqual(100, scene.Flow.Progress);
        Assert.AreEqual(SettingsHubText.UpdateAtExitOf("1.2.0"), scene.Flow.Message);
        Assert.AreEqual(NotificationSeverity.Info, scene.Flow.MessageSeverity);
        Assert.IsTrue(scene.Flow.IsDialogOpen, "une fois prête, la mise à jour demande quand redémarrer");
        Assert.AreEqual(
            (SettingsHubText.UpdateReadyTitle, SettingsHubText.UpdateReadyOf("1.2.0"), SettingsHubText.RestartNow, SettingsHubText.RestartLater),
            (scene.Flow.DialogTitle, scene.Flow.DialogMessage, scene.Flow.DialogPrimary, scene.Flow.DialogSecondary));
        Assert.IsTrue(scene.Flow.IsDialogPrimaryEnabled && scene.Flow.RestartNowCommand.CanExecute(null));

        // « Plus tard » : elle s'installe à la fermeture de kyfRev, sans le rouvrir.
        await scene.Flow.AnswerAsync(primary: false);

        Assert.IsFalse(scene.Flow.IsDialogOpen);
        Assert.AreEqual(0, scene.Closes);
        scene.Flow.ExitLaunch!.Invoke();
        Assert.AreEqual((scene.Path, false), (scene.Installs.Started.Single().Installer, scene.Installs.Started.Single().Reopen));
    }

    [TestMethod]
    public async Task Restart_now_closes_kyfRev_which_installs_the_update_and_opens_again()
    {
        using var scene = new Scene();
        await scene.Flow.CheckCommand.ExecuteAsync(null);

        await scene.Flow.AnswerAsync(primary: true);

        Assert.AreEqual(1, scene.Closes);
        Assert.IsEmpty(scene.Installs.Started, "rien ne s'installe tant que kyfRev tient ses fichiers");
        scene.Flow.ExitLaunch!.Invoke();
        Assert.IsTrue(scene.Installs.Started.Single().Reopen);
    }

    [TestMethod]
    public async Task A_conversion_keeps_kyfRev_open_and_the_update_waits_for_its_closing()
    {
        using var scene = new Scene();
        scene.Flow.CloseApplication = () => false;
        await scene.Flow.CheckCommand.ExecuteAsync(null);

        scene.Flow.RestartNowCommand.Execute(null);

        Assert.AreEqual(SettingsHubText.UpdateCannotClose, scene.Flow.Error);
        Assert.AreEqual(SettingsHubText.UpdateAtExitOf("1.2.0"), scene.Flow.Message);
        scene.Flow.ExitLaunch!.Invoke();
        Assert.IsFalse(scene.Installs.Started.Single().Reopen);

        scene.Flow.CloseApplication = null;
        scene.Flow.RestartNowCommand.Execute(null);
        Assert.AreEqual(SettingsHubText.UpdateCannotClose, scene.Flow.Error, "sans fenêtre à fermer, de même");
    }

    [TestMethod]
    public async Task At_startup_the_update_is_offered_and_can_wait_for_the_next_opening()
    {
        using var scene = new Scene();

        await scene.Flow.StartAsync(checkNow: true);

        Assert.IsTrue(scene.Flow.IsDialogOpen);
        Assert.AreEqual(
            (SettingsHubText.UpdateOfferTitle, SettingsHubText.UpdateOfferOf("1.2.0", "1.1.0"), SettingsHubText.UpdateNow, SettingsHubText.UpdatePostpone),
            (scene.Flow.DialogTitle, scene.Flow.DialogMessage, scene.Flow.DialogPrimary, scene.Flow.DialogSecondary));
        Assert.AreEqual(0, scene.Downloads.Calls, "rien ne se télécharge sans accord");

        await scene.Flow.AnswerAsync(primary: false);

        Assert.AreEqual(UpdateStage.Postponed, scene.Flow.Stage);
        Assert.AreEqual(SettingsHubText.UpdatePostponedOf("1.2.0"), scene.Flow.Message);
        Assert.IsFalse(scene.Flow.IsDialogOpen);
        Assert.AreEqual(0, scene.Downloads.Calls);
        Assert.IsNull(scene.Flow.ExitLaunch);
    }

    [TestMethod]
    public async Task At_startup_accepting_downloads_in_the_dialog_then_asks_when_to_restart()
    {
        using var scene = new Scene { Gated = true };
        await scene.Flow.StartAsync(checkNow: true);

        var answering = scene.Flow.AnswerAsync(primary: true);

        Assert.AreEqual(UpdateStage.Downloading, scene.Flow.Stage);
        Assert.IsTrue(scene.Flow.IsDialogOpen && scene.Flow.IsDialogDownloading && scene.Flow.IsDownloading);
        Assert.IsFalse(scene.Flow.IsDialogPrimaryEnabled, "« Redémarrer maintenant » attend la fin du téléchargement");
        Assert.AreEqual(
            (SettingsHubText.UpdateDownloadingTitleOf("1.2.0"), SettingsHubText.UpdateDownloading, SettingsHubText.UpdateHide),
            (scene.Flow.DialogTitle, scene.Flow.DialogMessage, scene.Flow.DialogSecondary));
        Assert.AreEqual(SettingsHubText.UpdateProgressOf("0", "0"), scene.Flow.ProgressText);
        Assert.IsFalse(scene.Flow.CheckCommand.CanExecute(null), "une seule recherche à la fois");

        scene.Release();
        await answering;

        Assert.AreEqual(UpdateStage.Ready, scene.Flow.Stage);
        Assert.IsTrue(scene.Flow.IsDialogOpen);
        Assert.IsFalse(scene.Flow.IsDialogDownloading);
        Assert.AreEqual(SettingsHubText.UpdateReadyTitle, scene.Flow.DialogTitle);
    }

    [TestMethod]
    public async Task A_hidden_download_goes_on_and_its_dialog_returns_when_it_is_ready()
    {
        using var scene = new Scene { Gated = true };
        await scene.Flow.StartAsync(checkNow: true);
        var answering = scene.Flow.AnswerAsync(primary: true);

        await scene.Flow.AnswerAsync(primary: false);
        Assert.IsFalse(scene.Flow.IsDialogOpen);
        Assert.AreEqual(UpdateStage.Downloading, scene.Flow.Stage);

        scene.Release();
        await answering;

        Assert.IsTrue(scene.Flow.IsDialogOpen);
        Assert.AreEqual(SettingsHubText.UpdateReadyTitle, scene.Flow.DialogTitle);
    }

    [TestMethod]
    public async Task A_failed_download_is_said_in_its_dialog_or_in_the_tab()
    {
        using var shown = new Scene { Fails = true };
        await shown.Flow.StartAsync(checkNow: true);
        await shown.Flow.AnswerAsync(primary: true);

        Assert.AreEqual(UpdateStage.Failed, shown.Flow.Stage);
        Assert.AreEqual("Le téléchargement n'a pas abouti (réseau coupé).", shown.Flow.Error);
        Assert.IsTrue(shown.Flow.IsDialogOpen);
        Assert.AreEqual(
            (SettingsHubText.UpdateFailedTitle, shown.Flow.Error, DesignText.Close, string.Empty),
            (shown.Flow.DialogTitle, shown.Flow.DialogMessage, shown.Flow.DialogPrimary, shown.Flow.DialogSecondary));
        await shown.Flow.AnswerAsync(primary: true);
        Assert.IsNull(shown.Flow.ExitLaunch);

        using var tab = new Scene { Fails = true };
        await tab.Flow.CheckCommand.ExecuteAsync(null);
        Assert.IsTrue(tab.Flow.HasError);
        Assert.IsFalse(tab.Flow.IsDialogOpen || tab.Flow.HasMessage, "lancée de l'onglet, l'erreur s'y lit");

        using var hidden = new Scene { Fails = true, Gated = true };
        await hidden.Flow.StartAsync(checkNow: true);
        var answering = hidden.Flow.AnswerAsync(primary: true);
        await hidden.Flow.AnswerAsync(primary: false);
        hidden.Release();
        await answering;
        Assert.IsTrue(hidden.Flow.HasError);
        Assert.IsFalse(hidden.Flow.IsDialogOpen, "masqué, le dialogue ne revient pas pour un échec");
    }

    [TestMethod]
    public async Task At_startup_nothing_is_searched_unless_chosen_and_a_failed_search_says_nothing()
    {
        using var off = new Scene();
        await off.Flow.StartAsync(checkNow: false);
        Assert.AreEqual(0, off.Online.Reads);
        Assert.AreEqual(UpdateStage.Idle, off.Flow.Stage);

        using var offline = new Scene { Offline = true };
        await offline.Flow.StartAsync(checkNow: true);
        Assert.AreEqual(UpdateStage.Idle, offline.Flow.Stage);
        Assert.IsFalse(offline.Flow.IsDialogOpen || offline.Flow.HasError || offline.Flow.HasMessage);

        using var current = new Scene { Running = "1.2.0" };
        await current.Flow.StartAsync(checkNow: true);
        Assert.IsFalse(current.Flow.IsDialogOpen);

        await current.Flow.CheckCommand.ExecuteAsync(null);
        Assert.AreEqual(UpdateStage.UpToDate, current.Flow.Stage);
        Assert.AreEqual(SettingsHubText.UpToDateOf("1.2.0"), current.Flow.Message);
        Assert.AreEqual(NotificationSeverity.Success, current.Flow.MessageSeverity);
    }

    [TestMethod]
    public async Task A_copy_that_cannot_install_offers_the_page_at_startup()
    {
        using var scene = new Scene { Installable = false };
        Assert.IsFalse(scene.Flow.CanInstall);

        await scene.Flow.StartAsync(checkNow: true);

        Assert.AreEqual((SettingsHubText.UpdateAvailableOf("1.2.0", "1.1.0"), SettingsHubText.OpenRelease), (scene.Flow.DialogMessage, scene.Flow.DialogPrimary));
        Assert.IsTrue(scene.Flow.HasRelease);
        await scene.Flow.AnswerAsync(primary: true);
        Assert.AreEqual("https://github.com/KyllianFF/kyfRev-dotnet/releases/tag/v1.2.0", scene.Links.Opened.Single());
        Assert.AreEqual(0, scene.Downloads.Calls);
    }

    [TestMethod]
    public async Task An_announcement_without_its_installer_only_opens_the_page()
    {
        using var scene = new Scene { Described = false };

        await scene.Flow.CheckCommand.ExecuteAsync(null);

        Assert.AreEqual(UpdateStage.Available, scene.Flow.Stage);
        Assert.IsTrue(scene.Flow.HasRelease);
        Assert.AreEqual(0, scene.Downloads.Calls);
        Assert.IsNull(scene.Flow.ExitLaunch);
    }

    [TestMethod]
    public async Task The_next_start_says_that_the_update_is_installed()
    {
        using var scene = new Scene();
        await scene.LaunchedAsync();
        using var after = scene.Restarted("1.2.0");

        await after.Flow.StartAsync(checkNow: false);

        Assert.AreEqual(UpdateStage.Installed, after.Flow.Stage);
        Assert.AreEqual(SettingsHubText.UpdateInstalledOf("1.2.0"), after.Flow.Message);
        Assert.AreEqual(NotificationSeverity.Success, after.Flow.MessageSeverity);
        Assert.IsFalse(after.Flow.IsDialogOpen);
        Assert.IsFalse(File.Exists(scene.Path), "son installateur est effacé");
    }

    [TestMethod]
    public async Task The_next_start_offers_to_try_again_an_update_that_did_not_install()
    {
        using var scene = new Scene();
        await scene.LaunchedAsync();
        using var after = scene.Restarted("1.1.0");

        await after.Flow.StartAsync(checkNow: true);

        var log = Path.Combine(scene.Folder, UpdatePackages.LogFile);
        Assert.AreEqual(SettingsHubText.UpdateNotInstalledOf("1.2.0", "1.1.0", log), after.Flow.Error);
        Assert.AreEqual(
            (SettingsHubText.UpdateNotInstalledTitle, after.Flow.Error, SettingsHubText.UpdateRetry, DesignText.Close),
            (after.Flow.DialogTitle, after.Flow.DialogMessage, after.Flow.DialogPrimary, after.Flow.DialogSecondary));
        Assert.AreEqual(0, after.Online.Reads, "la question en attente passe avant une nouvelle recherche");
        Assert.IsNull(after.Flow.ExitLaunch, "rien ne se relance à la fermeture sans le demander");

        await after.Flow.AnswerAsync(primary: true);

        Assert.AreEqual(1, after.Closes);
        after.Flow.ExitLaunch!.Invoke();
        Assert.IsTrue(after.Installs.Started.Single().Reopen);
    }

    [TestMethod]
    public async Task An_update_that_did_not_install_and_whose_installer_is_gone_is_only_said()
    {
        using var scene = new Scene();
        await scene.LaunchedAsync();
        File.Delete(scene.Path);
        using var after = scene.Restarted("1.1.0");

        await after.Flow.StartAsync(checkNow: false);

        Assert.AreEqual(UpdateStage.Failed, after.Flow.Stage);
        Assert.AreEqual((SettingsHubText.UpdateFailedTitle, DesignText.Close, string.Empty), (after.Flow.DialogTitle, after.Flow.DialogPrimary, after.Flow.DialogSecondary));
        await after.Flow.AnswerAsync(primary: false);
        Assert.IsNull(after.Flow.ExitLaunch);
    }

    [TestMethod]
    public async Task For_everyone_on_this_computer_the_update_says_windows_will_ask_for_an_administrator()
    {
        using var scene = new Scene { Scope = InstallScope.AllUsers };

        await scene.Flow.CheckCommand.ExecuteAsync(null);

        Assert.AreEqual($"{SettingsHubText.UpdateReadyOf("1.2.0")} {SettingsHubText.UpdateAllUsers}", scene.Flow.DialogMessage);
    }

    [STATestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void The_update_dialog_fits_a_narrow_window_and_answers_through_its_buttons(bool dark)
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        var test = new TestShell(dark);
        test.Shell.Updates = scene.Flow;
        var window = new ShellWindow(test.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 470, Height = 540, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            TestShell.Flush();
            Assert.IsFalse(window.UpdateDialog.IsVisible, "rien tant que rien n'est proposé");

            // Au démarrage : la proposition, ses deux boutons dans la largeur.
            scene.Flow.StartAsync(checkNow: true).GetAwaiter().GetResult();
            TestShell.Flush();
            Assert.IsTrue(window.UpdateDialog.IsVisible);
            Assert.AreEqual(SettingsHubText.UpdateOfferOf("1.2.0", "1.1.0"), window.UpdateDialogMessage.Text);
            AssertFits(window, window.UpdateDialog);
            Capture(window, $"maj-proposee-{(dark ? "sombre" : "clair")}");

            // « Mettre à jour » : le téléchargement, puis la question du redémarrage.
            Press(Button(window.UpdateDialog, Dialog.PrimaryButtonPart));
            UiThread.Until(() => scene.Flow.Stage == UpdateStage.Ready);
            TestShell.Flush();
            Assert.IsTrue(window.UpdateDialog.IsVisible);
            Assert.AreEqual(SettingsHubText.UpdateReadyOf("1.2.0"), window.UpdateDialogMessage.Text);
            Assert.IsFalse(window.UpdateDialogProgress.IsVisible);
            AssertFits(window, window.UpdateDialog);
            Capture(window, $"maj-prete-{(dark ? "sombre" : "clair")}");

            // « Plus tard » : la mise à jour attend la fermeture.
            Press(Button(window.UpdateDialog, Dialog.SecondaryButtonPart));
            UiThread.Until(() => !scene.Flow.IsDialogOpen);
            TestShell.Flush();
            Assert.IsFalse(window.UpdateDialog.IsVisible);
            Assert.IsNotNull(scene.Flow.ExitLaunch);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void The_download_shows_its_progress_in_the_dialog(bool dark)
    {
        using var ui = new UiThread();
        using var scene = new Scene { Gated = true };
        var test = new TestShell(dark);
        test.Shell.Updates = scene.Flow;
        var window = new ShellWindow(test.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 470, Height = 540, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            scene.Flow.StartAsync(checkNow: true).GetAwaiter().GetResult();
            TestShell.Flush();
            Press(Button(window.UpdateDialog, Dialog.PrimaryButtonPart));
            UiThread.Until(() => scene.Flow.IsDialogDownloading);
            TestShell.Flush();

            Assert.IsTrue(window.UpdateDialogProgress.IsVisible);
            Assert.IsFalse(Button(window.UpdateDialog, Dialog.PrimaryButtonPart).IsEnabled);
            Assert.AreEqual(SettingsHubText.UpdateProgressLabel, AutomationProperties.GetName(window.UpdateDialogBar));
            AssertFits(window, window.UpdateDialog);
            Capture(window, $"maj-telechargement-{(dark ? "sombre" : "clair")}");

            // Échap masque le téléchargement, qui continue.
            window.UpdateDialog.Close(DialogChoice.None);
            UiThread.Until(() => !scene.Flow.IsDialogOpen);
            Assert.AreEqual(UpdateStage.Downloading, scene.Flow.Stage);
            scene.Release();
            UiThread.Until(() => scene.Flow.IsDialogOpen);
            TestShell.Flush();
            Assert.AreEqual(SettingsHubText.UpdateReadyTitle, window.UpdateDialog.Title);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void The_about_tab_shows_the_switch_the_progress_and_the_restart_button(bool dark)
    {
        using var ui = new UiThread();
        using var scene = new Scene { Gated = true };
        using var vaults = new TestVaults(Application.Model.AppMode.Installed);
        var test = new TestShell(dark);
        var store = new MemoryMachinePreferences();
        var about = new AboutSettingsViewModel(vaults.App.Vaults, scene.Flow, new MachineSettings(store));
        var page = new AboutSettingsPage { DataContext = about };
        var window = new Window { Content = page, ShowActivated = false, Left = -10000, Top = -10000, Width = 470, Height = 540, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.SetResourceReference(Control.BackgroundProperty, "Kyf.Brush.Background");
        window.Show();
        try
        {
            TestShell.Flush();
            Assert.IsTrue(page.CheckAtStartupBox.IsVisible);
            Assert.AreEqual(SettingsHubText.CheckAtStartup, AutomationProperties.GetName(page.CheckAtStartupBox));
            Assert.IsFalse(page.RestartButton.IsVisible || page.DownloadProgress.IsVisible || page.OpenReleaseButton.IsVisible);

            Press(page.CheckAtStartupBox);
            Assert.IsTrue(store.Stored.CheckUpdatesAtStartup);

            // Le bouton : la recherche, puis le téléchargement, dont la progression se lit dans l'onglet.
            var checking = scene.Flow.CheckCommand.ExecuteAsync(null);
            UiThread.Until(() => scene.Flow.IsDownloading);
            TestShell.Flush();
            Assert.IsTrue(page.DownloadProgress.IsVisible);
            Assert.IsFalse(page.CheckUpdateButton.IsEnabled, "une seule recherche à la fois");
            AssertFits(window, page);
            Capture(page, $"maj-onglet-telechargement-{(dark ? "sombre" : "clair")}");

            scene.Release();
            UiThread.Until(() => checking.IsCompleted);
            TestShell.Flush();
            Assert.IsTrue(page.RestartButton.IsVisible);
            Assert.IsTrue(page.UpdateNotice.IsOpen);
            Assert.AreEqual(SettingsHubText.UpdateAtExitOf("1.2.0"), page.UpdateNotice.Message);
            AssertFits(window, page);
            Capture(page, $"maj-onglet-prete-{(dark ? "sombre" : "clair")}");

            Press(page.RestartButton);
            Assert.AreEqual(1, scene.Closes);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Rien de visible ne dépasse à droite de la fenêtre.</summary>
    private static void AssertFits(Window window, FrameworkElement root)
    {
        var content = (FrameworkElement)window.Content;
        foreach (var element in Descendants(root).OfType<FrameworkElement>().Where(element => element.IsVisible && element is Control or TextBlock))
        {
            var right = element.TranslatePoint(new Point(element.ActualWidth, 0), content).X;
            Assert.IsLessThanOrEqualTo(content.ActualWidth + 0.5, right, $"{element.Name} « {(element as TextBlock)?.Text ?? (element as ContentControl)?.Content} » dépasse à droite");
        }
    }

    private static void Capture(FrameworkElement element, string name) => RuledPaperPixels.Capture(element is Window { Content: FrameworkElement content } ? content : element, name);

    private static Button Button(Dialog dialog, string part) => (Button)dialog.Template.FindName(part, dialog);

    /// <summary>Comme un clic, ou un lecteur d'écran : le bouton est invoqué, la case basculée.</summary>
    private static void Press(ButtonBase button)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(button);
        if (button is ToggleButton)
        {
            ((System.Windows.Automation.Provider.IToggleProvider)peer.GetPattern(PatternInterface.Toggle)).Toggle();
        }
        else
        {
            ((System.Windows.Automation.Provider.IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
        }
        TestShell.Flush();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// Une mise à jour de 1.1.0 vers 1.2.0 : l'annonce signée, son installateur, le dossier des mises à jour de ce poste, le
    /// faux téléchargement et le faux installateur.
    /// </summary>
    private sealed class Scene : IDisposable
    {
        private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly bool _owner;
        private UpdateFlow? _flow;

        public Scene()
        {
            Folder = Directory.CreateTempSubdirectory("kyfrev-maj-").FullName;
            _owner = true;
        }

        private Scene(Scene before, string running)
        {
            Folder = before.Folder;
            Running = running;
        }

        public string Folder { get; }

        public string Path => System.IO.Path.Combine(Folder, "kyfRev-1.2.0-installation.exe");

        public string Running { get; init; } = "1.1.0";

        public bool Installable { get; init; } = true;

        public bool Described { get; init; } = true;

        public bool Offline { get; init; }

        public bool Fails { get; init; }

        public bool Gated { get; init; }

        public InstallScope Scope { get; init; } = InstallScope.CurrentUser;

        public int Closes { get; private set; }

        public RecordingLinks Links { get; } = new();

        public FakeDownloads Downloads => field ??= new FakeDownloads(this);

        public FakeInstaller Installs => field ??= new FakeInstaller(Scope);

        public AnnouncedVersion Online => field ??= Announced();

        public UpdateFlow Flow => _flow ??= Build();

        /// <summary>La mise à jour prête puis lancée à la fermeture, comme avec « Plus tard ».</summary>
        public async Task LaunchedAsync()
        {
            await Flow.CheckCommand.ExecuteAsync(null);
            await Flow.AnswerAsync(primary: false);
            Flow.ExitLaunch!.Invoke();
        }

        /// <summary>Le démarrage suivant, sur le même dossier, avec la version qui tourne alors.</summary>
        public Scene Restarted(string running) => new(this, running);

        public void Release() => _gate.TrySetResult();

        public Task WaitAsync(CancellationToken cancellationToken) => Gated ? _gate.Task.WaitAsync(cancellationToken) : Task.CompletedTask;

        public void Dispose()
        {
            _key.Dispose();
            if (_owner)
            {
                Directory.Delete(Folder, recursive: true);
            }
        }

        private AnnouncedVersion Announced()
        {
            if (Offline)
            {
                return new AnnouncedVersion([], []);
            }
            var manifest = UpdateChecker.Write("1.2.0", new DateOnly(2026, 11, 15), Described ? Installer : null);
            return new AnnouncedVersion(manifest, ExtensionCatalogue.Sign(manifest, _key, "essai"));
        }

        private UpdateFlow Build()
        {
            var checker = new UpdateChecker(Online, [new("essai", _key.ExportSubjectPublicKeyInfoPem())], Running);
            var packages = Installable ? new UpdatePackages(Downloads, Installs, Folder) : null;
            return new UpdateFlow(checker, packages, Links)
            {
                CloseApplication = () =>
                {
                    Closes++;
                    return true;
                },
            };
        }
    }

    private sealed class FakeDownloads(Scene scene) : IFileDownloader
    {
        public int Calls { get; private set; }

        public async Task DownloadAsync(Uri address, string destination, long maxBytes, IProgress<long>? progress, CancellationToken cancellationToken)
        {
            Calls++;
            await scene.WaitAsync(cancellationToken);
            if (scene.Fails)
            {
                throw new Unreachable();
            }
            await File.WriteAllBytesAsync(destination, Installer, cancellationToken);
        }
    }

    private sealed class FakeInstaller(InstallScope scope) : IUpdateInstaller
    {
        public InstallScope? Scope => scope;

        public List<(string Installer, bool Reopen)> Started { get; } = [];

        public bool Start(string installer, bool reopen, string log)
        {
            Started.Add((installer, reopen));
            return true;
        }
    }

    private sealed class Unreachable : OperationException
    {
        public Unreachable()
            : base("réseau coupé.")
        {
        }

        public Unreachable(string message)
            : base(message)
        {
        }

        public Unreachable(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
