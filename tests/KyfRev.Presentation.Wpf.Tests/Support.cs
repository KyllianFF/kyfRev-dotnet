using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.Conversion;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Domain.Calendar;
using KyfRev.Extensions.Host;
using KyfRev.Infrastructure.Crypto;
using KyfRev.Infrastructure.Vault.Adapters;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Presentation.Wpf.Editing;
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

    public WindowOverhang Overhang { get; set; }

    public bool UseDarkTitleBar(nint windowHandle, bool dark)
    {
        Calls.Add((windowHandle, dark));
        return true;
    }

    public WindowOverhang MaximizedOverhang(nint windowHandle) => Overhang;

    public ScreenArea Work { get; set; }

    public ScreenArea WorkArea(nint windowHandle) => Work;
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

/// <summary>Les préférences de ce poste, en mémoire.</summary>
internal sealed class MemoryMachinePreferences : IMachinePreferencesStore
{
    public MachinePreferences Stored { get; set; } = new();

    public bool Refuses { get; set; }

    public int Saves { get; private set; }

    public MachinePreferences Load() => Stored;

    public bool Save(MachinePreferences preferences)
    {
        Saves++;
        if (Refuses)
        {
            return false;
        }
        Stored = preferences;
        return true;
    }
}

/// <summary>Des voix installées, qui ne disent rien.</summary>
internal sealed class FakeVoices(params string[] voices) : ISpeech
{
    public IReadOnlyList<string> FrenchVoices() => voices;

    public Task<Outcome<Done>> SpeakAsync(string text, string? voice, double rate, CancellationToken cancellation) => new TaskCompletionSource<Outcome<Done>>().Task;

    public void PauseSpeaking()
    {
    }

    public void ResumeSpeaking()
    {
    }
}

/// <summary>La carte des extensions, sur une bibliothèque qui n'existe pas encore.</summary>
internal static class FakeExtensions
{
    public static ExtensionSettingsViewModel Settings() =>
        new(new ExtensionLibrary(Path.Combine(Path.GetTempPath(), "kyfrev-essai-" + Guid.NewGuid().ToString("N")), TimeProvider.System), new FakePicker());
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

    public string? VaultToCreate() =>
        planned is not null && !Directory.Exists(Path.Combine(planned, ".suivi")) && !File.Exists(Path.Combine(planned, "kyfrev-chiffre.json")) ? planned : null;

    public bool Remember(string root)
    {
        Remembered.Add(root);
        return true;
    }
}

/// <summary>
/// Les cas d'usage sur un dossier d'essai jetable, avec une horloge au choix ; le coffre prévu y est « Prévu/Coffre ». Les
/// coffres s'y ouvrent comme sur le poste, en clair ou chiffrés ; les clés retenues vont dans « Poste ».
/// </summary>
internal sealed class TestVaults : IDisposable
{
    /// <summary>Le mot de passe des coffres chiffrés des essais.</summary>
    public const string Password = "un mot de passe d'essai";

    /// <summary>Un en-tête, sa clé et sa clé de récupération pour tous les coffres chiffrés : dériver un vrai réglage prend une fraction de seconde.</summary>
    private static readonly Lazy<(VaultHeader Header, byte[] Key, string Recovery)> Shared =
        new(() => VaultHeader.Create("v_01M6A2PRES3NTAT10NWPF0TE5T", Password, PasswordKeys.NewPbkdf2()));

    public TestVaults(AppMode mode, TimeProvider? time = null)
    {
        Time = time ?? TimeProvider.System;
        Root = Path.Combine(Path.GetTempPath(), "kyfrev-presentation", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Planned = Path.Combine(Root, "Prévu", "Coffre");
        Locations = new FakeLocations(mode, mode == AppMode.Development ? null : Planned);
        App = NewApplication(Locations, Time, LocalData);
    }

    /// <summary>La clé de récupération des coffres chiffrés des essais.</summary>
    public static string RecoveryKey => Shared.Value.Recovery;

    /// <summary>Les données du poste : les clés retenues.</summary>
    public string LocalData => Path.Combine(Root, "Poste");

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
        using (var other = NewApplication(new FakeLocations(AppMode.Development, null), Time, LocalData))
        {
            Assert.AreEqual(root, other.Vaults.Create(root).Vault);
        }
        return root;
    }

    /// <summary>Un coffre chiffré vide, que sa session a créé puis refermé ; il s'ouvre avec <see cref="Password"/>.</summary>
    public string CreateEncryptedVault(string root)
    {
        Directory.CreateDirectory(root);
        var now = new Moment(DateOnly.FromDateTime(Time.GetUtcNow().UtcDateTime), Time.GetUtcNow());
        using var storage = EncryptedStorage.Create(root, Shared.Value.Header, Shared.Value.Key, DiskStorage.Instance);
        using var vault = new VaultStore(storage: storage).Initialize(root, now);
        return root;
    }

    /// <summary>Le démarrage suivant : le coffre ouvert est fermé ; les mêmes emplacements sont repris.</summary>
    public KyfRevApplication Restart()
    {
        App.Dispose();
        App = NewApplication(Locations, Time, LocalData);
        return App;
    }

    public void Dispose()
    {
        App.Dispose();
        Directory.Delete(Root, recursive: true);
    }

    /// <summary>L'application assemblée comme sur le poste : les coffres en clair et chiffrés, et leurs clés.</summary>
    private static KyfRevApplication NewApplication(IVaultLocations locations, TimeProvider time, string localData)
    {
        var vaults = new EncryptedVaults(
            new VaultStore(),
            storage => new VaultStore(storage: storage, ownsStorage: true),
            new RememberedKeys(localData, DiskStorage.Instance),
            DiskStorage.Instance);
        return new(vaults, new BackupFiles(), locations, time, NullLoggerFactory.Instance, "1.0.0-essai", vaults, new VaultConversions(PasswordKeys.NewPbkdf2()));
    }
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

    public void OpenPassageInSheet(string sheetId, string passageId) => Opened.Add((sheetId, passageId));

    public void ReviseSheet(string sheetId) => Opened.Add(("réviser", sheetId));

    public void WriteSheet(string sheetId) => Opened.Add(("écrire", sheetId));

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

    public static void Until(Func<bool> condition) => Until(condition, TimeSpan.FromSeconds(10));

    public static void Until(Func<bool> condition, TimeSpan patience)
    {
        var deadline = DateTime.UtcNow + patience;
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

/// <summary>
/// Un essai sur son propre fil STA, comme celui de MSTest, dont le nom le remplace ici ; mais, à la fin, le fil ferme son
/// dispatcher. Sinon, c'est à la fin du fil que Windows défait le service de saisie (TSF), s'il y a servi : il rappelle alors
/// le TextStore de WPF, en .NET, sur un fil que .NET a déjà quitté, et tout le processus plante.
/// </summary>
internal sealed class STATestMethodAttribute([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
    : TestMethodAttribute(callerFilePath, callerLineNumber)
{
    public string CallerFilePath { get; } = callerFilePath;

    public int CallerLineNumber { get; } = callerLineNumber;

    public override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
    {
        var outcome = new TaskCompletionSource<Task<TestResult[]>>();
        var thread = new Thread(() =>
        {
            var running = base.ExecuteAsync(testMethod);
            ((IAsyncResult)running).AsyncWaitHandle.WaitOne();
            Dispatcher.CurrentDispatcher.InvokeShutdown();
            outcome.SetResult(running);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return outcome.Task.Unwrap();
    }
}

/// <summary>
/// Un essai qui emploie le vrai moteur web : toujours sur le même fil, qui dure autant que le processus. Le moteur et son
/// environnement n'ont ainsi à démarrer qu'une fois, comme dans l'application.
/// </summary>
internal sealed class EngineTestAttribute([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
    : TestMethodAttribute(callerFilePath, callerLineNumber)
{
    private static readonly Dispatcher Engine = Start();

    public string CallerFilePath { get; } = callerFilePath;

    public int CallerLineNumber { get; } = callerLineNumber;

    public override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod) =>
        Engine.InvokeAsync(() => base.ExecuteAsync(testMethod)).Task.Unwrap();

    private static Dispatcher Start()
    {
        var started = new TaskCompletionSource<Dispatcher>();
        var thread = new Thread(() =>
        {
            started.SetResult(Dispatcher.CurrentDispatcher);
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "Moteur web",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return started.Task.GetAwaiter().GetResult();
    }
}

/// <summary>L'environnement de l'écriture dans les essais : un seul dossier WebView2 par machine de test.</summary>
internal static class FakeEnvironment
{
    public static string WebData { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kyfrev-essais-webview2");

    /// <summary>Le dossier du moteur des extensions, qui a le sien.</summary>
    public static string ExtensionData { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kyfrev-essais-webview2-extensions");

    /// <summary>Ouvrir sur le code : sans le moteur web, pour les essais qui n'en ont pas besoin.</summary>
    public static EditorEnvironment Markdown() => new(new FakeSpelling(), WebData, StartsInMarkdown: true);
}

/// <summary>Correcteur simulé : « ékole » est la seule faute qu'il connaisse.</summary>
internal sealed class FakeSpelling : ISpelling
{
    private const string Wrong = "ékole";

    public List<string> Added { get; } = [];

    public IReadOnlyList<SpellingMistake> Check(string text)
    {
        var found = new List<SpellingMistake>();
        for (var at = text.IndexOf(Wrong, StringComparison.Ordinal); at >= 0 && !Added.Contains(Wrong); at = text.IndexOf(Wrong, at + 1, StringComparison.Ordinal))
        {
            found.Add(new SpellingMistake(at, Wrong.Length));
        }
        return found;
    }

    public SpellingCorrection? Correct(string word)
    {
        var at = word.IndexOf(Wrong, StringComparison.Ordinal);
        return at < 0 || Added.Contains(Wrong) ? null : new SpellingCorrection(at, Wrong.Length, ["école", "écoles"]);
    }

    public bool Add(string word)
    {
        Added.Add(word);
        return true;
    }
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
