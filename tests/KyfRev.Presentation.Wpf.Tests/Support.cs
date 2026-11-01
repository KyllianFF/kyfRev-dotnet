using System.IO;
using System.Windows.Threading;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Infrastructure.Vault.Adapters;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;
using Microsoft.Extensions.Logging.Abstractions;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Préférences de Windows simulées : modifiables, et leur changement signalé à la demande.</summary>
internal sealed class FakeAppearance : ISystemAppearance
{
    public event EventHandler? Changed;

    public bool PrefersDarkTheme { get; set; }

    public bool HighContrast { get; set; }

    public bool ReducedMotion { get; set; }

    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>Cadre de fenêtre simulé : note chaque demande de barre de titre.</summary>
internal sealed class FakeChrome : IWindowChrome
{
    public List<(nint Handle, bool Dark)> Calls { get; } = [];

    public bool UseDarkTitleBar(nint windowHandle, bool dark)
    {
        Calls.Add((windowHandle, dark));
        return true;
    }
}

/// <summary>Une coquille assemblée comme dans l'application, avec des préférences de Windows simulées.</summary>
internal sealed class TestShell
{
    public TestShell(bool dark = false)
    {
        System = new FakeAppearance { PrefersDarkTheme = dark };
        Design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        Appearance = new AppearanceController(Design, System, Chrome);
        Gallery = new GalleryPageViewModel(Appearance);
        Shell = new ShellViewModel(CorePages.All(), Appearance, Gallery);
    }

    public FakeAppearance System { get; }

    public FakeChrome Chrome { get; } = new();

    public DesignResources Design { get; }

    public AppearanceController Appearance { get; }

    public GalleryPageViewModel Gallery { get; }

    public ShellViewModel Shell { get; }

    /// <summary>Exécute ce qui attend sur le fil de l'interface.</summary>
    public static void Flush() => Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, () => { });
}

/// <summary>Racine du dépôt : le dossier qui contient la solution.</summary>
internal static class Repository
{
    public static string Root { get; } = Find();

    private static string Find()
    {
        for (var folder = new System.IO.DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(folder.FullName, "KyfRev.slnx")))
            {
                return folder.FullName;
            }
        }
        throw new InvalidOperationException("Solution introuvable.");
    }
}

/// <summary>Sélecteur de dossiers simulé : rend les réponses prévues, note les titres.</summary>
internal sealed class FakePicker : IFolderPicker
{
    public Queue<string?> Answers { get; } = new();

    public List<string> Titles { get; } = [];

    public string? Pick(string title)
    {
        Titles.Add(title);
        return Answers.Dequeue();
    }
}

/// <summary>Emplacements simulés : un coffre prévu (selon le mode) et la mémoire du dernier ouvert.</summary>
internal sealed class FakeLocations(AppMode mode, string? planned) : IVaultLocations
{
    public AppMode Mode => mode;

    public List<string> Remembered { get; } = [];

    public string? VaultToOpen() => mode == AppMode.Portable ? planned : Remembered.LastOrDefault() ?? planned;

    public string? VaultToCreate() => planned is not null && !Directory.Exists(Path.Combine(planned, ".suivi")) ? planned : null;

    public bool Remember(string root)
    {
        Remembered.Add(root);
        return true;
    }
}

/// <summary>Les cas d'usage sur un dossier d'essai jetable, avec une horloge au choix ; le coffre prévu y est « Prévu/Coffre ».</summary>
internal sealed class TestVaults : IDisposable
{
    public TestVaults(AppMode mode, TimeProvider? time = null)
    {
        Time = time ?? TimeProvider.System;
        Root = Path.Combine(Path.GetTempPath(), "kyfrev-presentation", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Planned = Path.Combine(Root, "Prévu", "Coffre");
        Locations = new FakeLocations(mode, mode == AppMode.Development ? null : Planned);
        App = NewApplication(Locations, Time);
    }

    public string Root { get; }

    public string Planned { get; }

    public FakeLocations Locations { get; }

    public FakePicker Picker { get; } = new();

    public KyfRevApplication App { get; private set; }

    public TimeProvider Time { get; }

    public WelcomePageViewModel Welcome(Action<AppState> opened) => new(App.Vaults, Picker, App.Vaults.State(), opened);

    public string PathOf(string name) => Path.Combine(Root, name);

    public string Folder(string name) => Directory.CreateDirectory(PathOf(name)).FullName;

    /// <summary>Un coffre créé puis refermé, prêt à être ouvert.</summary>
    public string CreateVault(string name)
    {
        var root = PathOf(name);
        using (var other = NewApplication(new FakeLocations(AppMode.Development, null), Time))
        {
            Assert.AreEqual(root, other.Vaults.Create(root).Vault);
        }
        return root;
    }

    /// <summary>Le démarrage suivant : le coffre ouvert est fermé ; les mêmes emplacements sont repris.</summary>
    public KyfRevApplication Restart()
    {
        App.Dispose();
        App = NewApplication(Locations, Time);
        return App;
    }

    public void Dispose()
    {
        App.Dispose();
        Directory.Delete(Root, recursive: true);
    }

    private static KyfRevApplication NewApplication(IVaultLocations locations, TimeProvider time) =>
        new(new VaultStore(), new BackupFiles(), locations, time, NullLoggerFactory.Instance, "1.0.0-essai");
}

/// <summary>Horloge simulée, en temps universel : l'instant avance à la demande.</summary>
internal sealed class FakeTime(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() => Now;

    /// <summary>Le même instant, <paramref name="days"/> jours plus tard.</summary>
    public void AdvanceDays(int days) => Now = Now.AddDays(days);
}

/// <summary>Ouverture simulée des lignes du tableau de bord : note ce qui est ouvert.</summary>
internal sealed class FakeOpener : IReviewOpener
{
    public List<object> Opened { get; } = [];

    public void Open(DueItem item, bool activeRecall) => Opened.Add((item, activeRecall));

    public void OpenSheet(string sheetId) => Opened.Add(sheetId);

    public void OpenReminder(PendingReminder reminder) => Opened.Add(reminder);

    /// <summary>Éléments restants de la séance à reprendre, simulés.</summary>
    public int? Resumable { get; set; }

    public void StartSession(IReadOnlyList<DueItem> items, bool activeRecall) => Opened.Add(("séance", items, activeRecall));

    public int? ResumableItems() => Resumable;

    public void ResumeSession() => Opened.Add("reprise");
}

/// <summary>Attente d'une condition qu'une tâche de fond rendra vraie, sans délai fixe.</summary>
internal static class Waiting
{
    public static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.IsLessThan(deadline, DateTime.UtcNow, "délai dépassé");
            await Task.Yield();
        }
    }
}

/// <summary>
/// Fait tourner le fil de l'interface d'un essai STA jusqu'à ce qu'une condition soit vraie : les
/// suites des tâches de fond y reviennent, comme dans l'application.
/// </summary>
internal sealed class UiThread : IDisposable
{
    private readonly SynchronizationContext? _previous = SynchronizationContext.Current;

    public UiThread()
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
    }

    public static void Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.IsLessThan(deadline, DateTime.UtcNow, "délai dépassé");
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
        }
    }

    public void Dispose() => SynchronizationContext.SetSynchronizationContext(_previous);
}

/// <summary>Séance gardée en mémoire ; peut refuser d'être écrite.</summary>
internal sealed class MemorySessions : IStudySessionStore
{
    public StudySession? Kept { get; set; }

    public int Saves { get; private set; }

    public bool Forgotten { get; private set; }

    public StudySession? Load() => Kept;

    public bool Save(StudySession session)
    {
        Saves++;
        Kept = session;
        Forgotten = false;
        return true;
    }

    public bool Forget()
    {
        Kept = null;
        Forgotten = true;
        return true;
    }
}
