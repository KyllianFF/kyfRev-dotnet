using System.IO;
using System.Reflection;
using KyfRev.Application.Model;
using KyfRev.Application.Modules;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Extensions.Host;
using KyfRev.Infrastructure.Crypto;
using KyfRev.Infrastructure.Online;
using KyfRev.Infrastructure.Vault.Adapters;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Windows;
using KyfRev.Modules.Citations;
using KyfRev.Modules.Schemas;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Extensions;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Schemas;
using KyfRev.Presentation.Wpf.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KyfRev.App;

/// <summary>
/// Racine de composition : le seul endroit qui relie la présentation à l'infrastructure. Les
/// modules s'y ajouteront à l'ouverture d'un coffre, selon ses réglages (<see cref="ModuleCatalog"/>).
/// </summary>
internal static class Composition
{
    /// <summary>Version de l'application, sans l'empreinte du commit.</summary>
    public static string Version { get; } =
        typeof(Composition).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    /// <summary>Les services de l'application.</summary>
    /// <param name="time">Horloge ; celle du système par défaut.</param>
    /// <param name="locations">Emplacements ; ceux de l'exécutable en cours par défaut.</param>
    public static ServiceProvider Build(TimeProvider? time = null, VaultLocations? locations = null)
    {
        var clock = time ?? TimeProvider.System;
        var services = new ServiceCollection();
        // Signalé ouvert à l'installateur tant que les services vivent.
        services.AddSingleton<OpenMarker>();
        services.AddSingleton<ISystemAppearance, WindowsAppearance>();
        services.AddSingleton<IWindowChrome, WindowChrome>();
        services.AddSingleton<IExternalLinks, ShellLinks>();
        services.AddSingleton(_ => locations ?? VaultLocations.Detect());
        services.AddSingleton<IVaultLocations>(provider => provider.GetRequiredService<VaultLocations>());
        services.AddSingleton<IStudySessionStore>(provider => new StudySessionFile(provider.GetRequiredService<VaultLocations>().LocalData));
        services.AddSingleton<ILoggerFactory>(provider =>
        {
            var locations = provider.GetRequiredService<VaultLocations>();
            var log = new DiagnosticLog(locations.LocalData, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), clock);
            log.Started(Version, ModeName(locations.Mode));
            return log;
        });
        services.AddSingleton<IMachinePreferencesStore>(provider => new MachinePreferencesFile(provider.GetRequiredService<VaultLocations>().LocalData));
        services.AddSingleton<MachineSettings>();
        services.AddSingleton<IFolderPicker, FolderPicker>();
        services.AddSingleton<IOnlineReader>(_ => new OnlineReader(Version));
        services.AddSingleton(_ => new ModuleCatalog([new CitationsModule(), new SchemasModule()]));
        services.AddSingleton(provider => new ActiveModules(provider.GetRequiredService<ModuleCatalog>(), modules =>
        {
            // Ce que les modules attendent du cœur.
            modules.AddSingleton(provider.GetRequiredService<KyfRevApplication>().Host);
            modules.AddSingleton(clock);
            modules.AddSingleton(provider.GetRequiredService<IOnlineReader>());
            modules.AddSingleton(provider.GetRequiredService<ILoggerFactory>());
            modules.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        }));
        // Les coffres en clair et chiffrés : un coffre chiffré s'ouvre sur son stockage, que sa session prend et libère
        // avec elle (sa clé est alors effacée) ; sa clé peut être retenue sur ce poste, hors de tout coffre.
        services.AddSingleton(provider => new EncryptedVaults(
            new VaultStore(),
            storage => new VaultStore(storage: storage, ownsStorage: true),
            new RememberedKeys(provider.GetRequiredService<VaultLocations>().LocalData, DiskStorage.Instance),
            DiskStorage.Instance));
        // Le coffre ouvert est fermé, et son verrou libéré, quand les services le sont.
        services.AddSingleton(provider => new KyfRevApplication(
            provider.GetRequiredService<EncryptedVaults>(),
            new BackupFiles(),
            provider.GetRequiredService<IVaultLocations>(),
            clock,
            provider.GetRequiredService<ILoggerFactory>(),
            Version,
            provider.GetRequiredService<EncryptedVaults>()));
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Vaults);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Reminders);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Sheets);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Annotations);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Folders);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Templates);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Reviews);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Editing);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Settings);
        services.AddSingleton<ISpelling, WindowsSpelling>();
        services.AddSingleton<ISpeech, WindowsSpeech>();
        services.AddSingleton(provider => new EditorEnvironment(
            provider.GetRequiredService<ISpelling>(),
            Path.Combine(provider.GetRequiredService<VaultLocations>().LocalData, "WebView2"),
            Schemas: () => provider.GetRequiredService<INetworkSchemas>())
        {
            Report = reason => DiagnosticLog.WebEngineTrouble(provider.GetRequiredService<ILoggerFactory>().CreateLogger("Éditeur"), reason),
        });
        services.AddSingleton<INetworkSchemas>(provider => new NetworkSchemas(
            () => provider.GetRequiredService<ActiveModules>().Find<SchemaUseCases>(),
            () => provider.GetRequiredService<ShellViewModel>()));
        services.AddSingleton(provider => new MermaidEngine(Path.Combine(provider.GetRequiredService<VaultLocations>().LocalData, "WebView2")));
        services.AddSingleton(clock);
        services.AddSingleton<ReviewNavigation>();
        services.AddSingleton<IReviewOpener>(provider => provider.GetRequiredService<ReviewNavigation>());
        services.AddSingleton(provider =>
        {
            var modules = provider.GetRequiredService<ActiveModules>();
            return new DashboardPageViewModel(provider.GetRequiredService<ReminderUseCases>(), provider.GetRequiredService<SheetUseCases>(), provider.GetRequiredService<IReviewOpener>(), modules.Widgets, provider.GetRequiredService<ReviewUseCases>())
            {
                LookForNews = () => LookForQuoteAsync(modules),
            };
        });
        services.AddSingleton<SheetsPageViewModel>();
        services.AddSingleton<DefinitionsPageViewModel>();
        services.AddSingleton<SettingsPageViewModel>();
        services.AddSingleton<ArchivesPageViewModel>();
        services.AddSingleton<StatisticsPageViewModel>();
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Maintenance);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Backups);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Trash);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Exports);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Encryption);
        services.AddSingleton(provider =>
        {
            void VaultChanged() => Composition.VaultChanged(provider);
            return new DataSettingsViewModel(
                provider.GetRequiredService<VaultUseCases>(),
                provider.GetRequiredService<BackupUseCases>(),
                provider.GetRequiredService<TrashUseCases>(),
                provider.GetRequiredService<AnnotationUseCases>(),
                provider.GetRequiredService<ExportUseCases>(),
                provider.GetRequiredService<SettingsUseCases>(),
                provider.GetRequiredService<IFolderPicker>(),
                provider.GetRequiredService<IReviewOpener>(),
                VaultChanged,
                () => provider.GetRequiredService<ActiveModules>().Find<QuoteUseCases>())
            {
                // Un coffre chiffré choisi d'ici attend sa clé : son écran la demande, l'actuel restant ouvert derrière.
                VaultLocked = state => VaultStartup.ShowUnlock(
                    provider.GetRequiredService<ShellViewModel>(),
                    provider.GetRequiredService<VaultUseCases>(),
                    provider.GetRequiredService<IFolderPicker>(),
                    state,
                    VaultChanged),
                // La carte Chiffrement, pour un coffre chiffré : mot de passe, clé de récupération, clé retenue, délai.
                Encryption = new EncryptionSettingsViewModel(
                    provider.GetRequiredService<EncryptionUseCases>(),
                    provider.GetRequiredService<SettingsUseCases>(),
                    () => provider.GetRequiredService<VaultUseCases>().State().Encrypted,
                    () => provider.GetRequiredService<ShellViewModel>().Locking?.Invoke() ?? Task.CompletedTask,
                    SecretClipboard.Copy)
                {
                    LockDelay = provider.GetRequiredService<LockDelay>(),
                },
            };
        });
        services.AddSingleton(provider =>
        {
            var locations = provider.GetRequiredService<VaultLocations>();
            var library = new ExtensionLibrary(Path.Combine(locations.LocalData, "extensions"), clock);
            if (locations.LegacyLocalData is { } legacy)
            {
                library.Import(Path.Combine(legacy, "extensions"));
            }
            return library;
        });
        services.AddSingleton(provider => new ExtensionEngine(
            Path.Combine(provider.GetRequiredService<VaultLocations>().LocalData, "WebView2-extensions"),
            provider.GetRequiredService<ExtensionLibrary>(),
            new ExtensionRequests(
                provider.GetRequiredService<ExtensionLibrary>(),
                provider.GetRequiredService<VaultExtensionData>(),
                clock)));
        services.AddSingleton(provider => new ExtensionDataUseCases(provider.GetRequiredService<KyfRevApplication>().Host));
        services.AddSingleton(provider => new VaultExtensionData(
            provider.GetRequiredService<SheetUseCases>(),
            provider.GetRequiredService<EditingUseCases>(),
            provider.GetRequiredService<ExtensionDataUseCases>(),
            clock));
        services.AddSingleton(provider => new ExtensionPages(
            provider.GetRequiredService<ExtensionLibrary>(),
            provider.GetRequiredService<ExtensionEngine>(),
            () => provider.GetRequiredService<AppearanceController>().Theme == ThemeKind.Dark));
        services.AddSingleton(provider => new ExtensionBlocks(
            provider.GetRequiredService<ExtensionLibrary>(),
            provider.GetRequiredService<ExtensionEngine>(),
            () => provider.GetRequiredService<AppearanceController>().Theme == ThemeKind.Dark));
        services.AddSingleton(provider => new ExtensionSettingsViewModel(
            provider.GetRequiredService<ExtensionLibrary>(),
            provider.GetRequiredService<IFolderPicker>(),
            provider.GetRequiredService<ExtensionDataUseCases>().Journal,
            provider.GetRequiredService<ExtensionEngine>())
        {
            Catalogue = new CatalogueReader(
                provider.GetRequiredService<IOnlineReader>(),
                provider.GetRequiredService<ExtensionLibrary>(),
                ExtensionCatalogue.KnownKeys,
                Path.Combine(Path.GetTempPath(), "kyfRev", "catalogue")),
        });
        services.AddSingleton(provider => new ModulesSettingsViewModel(
            provider.GetRequiredService<SettingsUseCases>(),
            provider.GetRequiredService<ActiveModules>().Load,
            provider.GetRequiredService<ExtensionSettingsViewModel>()));
        services.AddSingleton<GeneralSettingsViewModel>();
        services.AddSingleton<AboutSettingsViewModel>();
        services.AddSingleton<SettingsHubViewModel>();
        services.AddSingleton(provider => new HealthPageViewModel(
            provider.GetRequiredService<MaintenanceUseCases>(),
            provider.GetRequiredService<BackupUseCases>(),
            provider.GetRequiredService<SheetUseCases>(),
            provider.GetRequiredService<IReviewOpener>(),
            () => DiagnosticText(provider),
            System.Windows.Clipboard.SetText,
            provider.GetRequiredService<ActiveModules>().HealthChecks));
        services.AddSingleton(_ => new DesignResources(ThemeKind.Light, Motion.SystemPrefersReducedMotion));
        services.AddSingleton<AppearanceController>();
        services.AddSingleton<GalleryPageViewModel>();
        services.AddSingleton(provider =>
        {
            var shell = new ShellViewModel(
                CorePages.All(new Dictionary<string, object>
                {
                    [CorePages.Today] = provider.GetRequiredService<DashboardPageViewModel>(),
                    [CorePages.Sheets] = provider.GetRequiredService<SheetsPageViewModel>(),
                    [CorePages.Definitions] = provider.GetRequiredService<DefinitionsPageViewModel>(),
                    [CorePages.Settings] = provider.GetRequiredService<SettingsHubViewModel>(),
                    [CorePages.Archives] = provider.GetRequiredService<ArchivesPageViewModel>(),
                    [CorePages.Statistics] = provider.GetRequiredService<StatisticsPageViewModel>(),
                    [CorePages.Health] = provider.GetRequiredService<HealthPageViewModel>(),
                }),
                provider.GetRequiredService<AppearanceController>(),
                provider.GetRequiredService<GalleryPageViewModel>());
            // Les fiches et les passages s'ouvrent dans la coquille.
            provider.GetRequiredService<ReviewNavigation>().Attach(shell);
            provider.GetRequiredService<MachineSettings>().Follow(provider.GetRequiredService<AppearanceController>(), shell);
            var modules = provider.GetRequiredService<ActiveModules>();
            var extensionPages = provider.GetRequiredService<ExtensionPages>();
            void ShowEntries() => shell.SetModuleEntries([.. ModuleEntries(modules, provider.GetRequiredService<IExternalLinks>(), schemas => new SchemasPageViewModel(
                schemas,
                provider.GetRequiredService<IFolderPicker>(),
                provider.GetRequiredService<IReviewOpener>(),
                provider.GetRequiredService<EditorEnvironment>().WebDataFolder)), .. extensionPages.Entries()]);
            modules.Changed += (_, _) => ShowEntries();
            void ShowExtensions()
            {
                ShowEntries();
                shell.SetExtensionCommands(extensionPages.Commands());
                _ = extensionPages.RefreshCountsAsync();
            }
            provider.GetRequiredService<ExtensionSettingsViewModel>().Changed += (_, e) =>
            {
                extensionPages.Forget(e.Extension);
                provider.GetRequiredService<ExtensionBlocks>().Forget();
                ShowExtensions();
            };
            extensionPages.ViewAsked += (_, e) =>
            {
                if (shell.MainNavigation.Concat(shell.SecondaryNavigation).FirstOrDefault(item => item.Id == ExtensionPages.EntryId(e.Extension)) is { } entry)
                {
                    shell.Navigate(entry);
                }
            };
            provider.GetRequiredService<SheetsPageViewModel>().ExtensionMenus = extensionPages.MenuEntries;
            provider.GetRequiredService<ExtensionEngine>().Screen = new ExtensionScreen(shell, provider.GetRequiredService<ReviewNavigation>());
            provider.GetRequiredService<DashboardPageViewModel>().ExtensionCards = extensionPages.CardsAsync;
            shell.WorkspaceOpening += (_, _) =>
            {
                extensionPages.VaultChanged();
                provider.GetRequiredService<ExtensionBlocks>().Forget();
                var settings = provider.GetRequiredService<SettingsUseCases>().Get() is { Succeeded: true } read ? read.Value : null;
                modules.Load(settings);
                ShowExtensions();
                var encrypted = provider.GetRequiredService<VaultUseCases>().State().Encrypted;
                shell.CanLock = encrypted;
                // Le verrouillage automatique suit le délai du coffre chiffré, lu ici une fois, et non à chaque regard du
                // minuteur ; un coffre en clair ne se verrouille pas, et l'inactivité ne le regarde pas.
                var delay = provider.GetRequiredService<LockDelay>();
                if (!encrypted)
                {
                    delay.Clear();
                }
                else if (settings is not null)
                {
                    delay.Set(settings.LockAfterMinutes);
                }
                else
                {
                    delay.Reset();
                }
            };
            shell.Locking = () => provider.GetRequiredService<VaultLocking>().LockAsync();
            return shell;
        });
        services.AddTransient<ShellWindow>();
        // Le coffre chiffré se verrouille, à la demande ou de lui-même : rien de lui ne reste dans les vues.
        services.AddSingleton<IUserPresence, UserPresence>();
        services.AddSingleton<LockDelay>();
        services.AddSingleton(provider => new VaultLocking(
            provider.GetRequiredService<ShellViewModel>(),
            provider.GetRequiredService<VaultUseCases>(),
            provider.GetRequiredService<IFolderPicker>(),
            () => ForgetVault(provider),
            () => VaultChanged(provider),
            () => DraftViews(provider),
            // La carte Chiffrement oublie dès le début : une dérivation qui finit pendant la fermeture ne s'y montre plus.
            () => provider.GetRequiredService<DataSettingsViewModel>().Encryption?.Forget()));
        services.AddSingleton(provider =>
        {
            var delay = provider.GetRequiredService<LockDelay>();
            return new AutoLock(
                provider.GetRequiredService<IUserPresence>(),
                () => delay.Value,
                () => provider.GetRequiredService<VaultLocking>().LockAsync());
        });
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    /// <summary>Un autre coffre est ouvert : les réglages lus de l'ancien sont oubliés, et l'espace de travail revient.</summary>
    private static void VaultChanged(IServiceProvider provider)
    {
        provider.GetRequiredService<SettingsPageViewModel>().Forget();
        // Un brouillon de l'ancien coffre ne s'écrit jamais dans le nouveau.
        provider.GetRequiredService<DefinitionsPageViewModel>().Forget();
        // Une clé de récupération préparée pour l'ancien coffre ne se montre pas dans le nouveau.
        provider.GetRequiredService<DataSettingsViewModel>().Encryption?.Forget();
        provider.GetRequiredService<ShellViewModel>().ShowWorkspace();
    }

    /// <summary>
    /// Le coffre s'est verrouillé : chaque vue oublie ce qu'elle avait lu, les modules se déchargent, et les moteurs web
    /// des extensions et des schémas repartent à vide.
    /// </summary>
    private static void ForgetVault(IServiceProvider provider)
    {
        foreach (var view in ForgettingViews(provider))
        {
            view.Forget();
        }
        provider.GetRequiredService<SettingsPageViewModel>().Forget();
        provider.GetRequiredService<ExtensionBlocks>().Forget();
        // Les pages des extensions ont relancé leur moteur, qui dit encore les réponses perdues : elles s'oublient aussi.
        provider.GetRequiredService<ExtensionEngine>().ForgetAllFaults();
        provider.GetRequiredService<MermaidEngine>().Reset();
        provider.GetRequiredService<ActiveModules>().Load(null);
        provider.GetRequiredService<LockDelay>().Clear();
    }

    /// <summary>Les vues qui peuvent garder un brouillon à enregistrer avant le verrouillage, même quand elles ne sont pas affichées.</summary>
    private static ILeaving[] DraftViews(IServiceProvider provider) =>
    [
        provider.GetRequiredService<DefinitionsPageViewModel>(),
    ];

    /// <summary>Les vues qui gardent ce qu'elles ont lu du coffre.</summary>
    internal static IForgetsVault[] ForgettingViews(IServiceProvider provider) =>
    [
        provider.GetRequiredService<DashboardPageViewModel>(),
        provider.GetRequiredService<SheetsPageViewModel>(),
        provider.GetRequiredService<DefinitionsPageViewModel>(),
        provider.GetRequiredService<ArchivesPageViewModel>(),
        provider.GetRequiredService<StatisticsPageViewModel>(),
        provider.GetRequiredService<HealthPageViewModel>(),
        provider.GetRequiredService<DataSettingsViewModel>(),
        provider.GetRequiredService<AboutSettingsViewModel>(),
        provider.GetRequiredService<ExtensionSettingsViewModel>(),
        provider.GetRequiredService<ExtensionPages>(),
        provider.GetRequiredService<ModulesSettingsViewModel>(),
    ];

    /// <summary>Les entrées du menu apportées par les modules actifs, avec leurs vues.</summary>
    internal static IReadOnlyList<NavigationItem> ModuleEntries(ActiveModules modules, IExternalLinks links, Func<SchemaUseCases, SchemasPageViewModel> schemasPage) =>
        [.. modules.All<INavigationContributor>().Select(contributor => contributor.Entry).Select(entry =>
        {
            var item = CorePages.FromModule(entry);
            return entry.Id switch
            {
                CitationsModule.ModuleId when modules.Find<QuoteUseCases>() is { } quotes =>
                    item with { Page = new QuotesPageViewModel(quotes, links) },
                SchemasModule.ModuleId when modules.Find<SchemaUseCases>() is { } schemas =>
                    item with { Page = schemasPage(schemas) },
                _ => item,
            };
        })];

    /// <summary>La citation du jour, cherchée en ligne si le module et ses réglages le veulent ; vrai si elle a changé.</summary>
    private static async Task<bool> LookForQuoteAsync(ActiveModules modules) =>
        modules.Find<QuoteUseCases>() is { } quotes
        && await quotes.SearchQuoteOfTheDayAsync(CancellationToken.None).ConfigureAwait(true) is { Succeeded: true, Value: not null };

    /// <summary>Le mode tel que le journal le nomme, comme la 0.x.</summary>
    private static string DiagnosticText(IServiceProvider provider) => DiagnosticReport.Write(
        Version,
        ModeName(provider.GetRequiredService<VaultLocations>().Mode),
        WebViewVersion(),
        provider.GetRequiredService<VaultUseCases>().State().Vault is not null,
        provider.GetRequiredService<TimeProvider>().GetLocalNow(),
        provider.GetRequiredService<ILoggerFactory>() is DiagnosticLog log ? log.Tail(DiagnosticReport.LogLines) : string.Empty);

    private static string WebViewVersion()
    {
        try
        {
            return Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString();
        }
        catch (Microsoft.Web.WebView2.Core.WebView2RuntimeNotFoundException problem)
        {
            return $"introuvable ({problem.Message})";
        }
    }

    internal static string ModeName(AppMode mode) => mode switch
    {
        AppMode.Portable => "portable",
        AppMode.Installed => "installée",
        _ => "développement",
    };
}
