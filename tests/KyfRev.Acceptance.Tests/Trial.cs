using System.Text.Json;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.Infrastructure.Crypto;
using KyfRev.Infrastructure.Vault.Adapters;
using KyfRev.Infrastructure.Vault.Files;
using Microsoft.Extensions.Logging.Abstractions;

namespace KyfRev.Acceptance.Tests;

/// <summary>
/// Un coffre jetable, à vivre aux dates d'un scénario : on y lance la vraie application, et on le
/// prépare par les cas d'usage pour ce qu'elle ne sait pas encore faire. Ses fichiers de suivi, relus
/// tels quels, disent ce qui a été enregistré.
/// </summary>
internal sealed class Trial : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "kyfrev-scenarios", Guid.NewGuid().ToString("N"));

    public Trial()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "portable.json"), """{ "coffre": "Coffre" }""");
    }

    private string Vault => Path.Combine(_folder, "Coffre");

    /// <summary>Le coffre ouvert aux cas d'usage, le jour donné.</summary>
    public T On<T>(DateOnly day, Func<KyfRevApplication, T> action) => Living(day, (app, _) => action(app));

    /// <summary>Le coffre ouvert aux cas d'usage ; l'horloge peut avancer pendant qu'il l'est.</summary>
    public T Living<T>(DateOnly day, Func<KyfRevApplication, ScenarioClock, T> action)
    {
        var clock = new ScenarioClock(day);
        using var app = new KyfRevApplication(new VaultStore(), new BackupFiles(), new NoProfile(), clock, NullLoggerFactory.Instance, "scenario");
        var state = Directory.Exists(Vault) ? app.Vaults.Open(Vault) : app.Vaults.Create(Vault);
        Assert.IsNotNull(state.Vault, "Le coffre du scénario ne s'est pas ouvert.");
        return action(app, clock);
    }

    /// <summary>
    /// Le coffre, chiffré par <paramref name="password"/> (PBKDF2, pour que l'essai reste rapide), puis ouvert aux cas
    /// d'usage le jour donné, comme l'application l'ouvre. Aucune clé n'est retenue.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public T Encrypted<T>(DateOnly day, string password, Func<KyfRevApplication, T> action)
    {
        var clock = new ScenarioClock(day);
        var (header, key, _) = VaultHeader.Create("v_01M6A3SCENAR10ACCEPTAT10N0", password, PasswordKeys.NewPbkdf2());
        Directory.CreateDirectory(Vault);
        using (var storage = EncryptedStorage.Create(Vault, header, key, DiskStorage.Instance))
        using (new VaultStore(storage: storage).Initialize(Vault, Present.Of(clock).Now))
        {
        }
        PasswordKeys.Clear(key);
        var vaults = new EncryptedVaults(
            new VaultStore(),
            storage => new VaultStore(storage: storage, ownsStorage: true),
            new RememberedKeys(Path.Combine(_folder, "essai-cles"), DiskStorage.Instance),
            DiskStorage.Instance);
        using var app = new KyfRevApplication(vaults, new BackupFiles(), new NoProfile(), clock, NullLoggerFactory.Instance, "scenario", vaults);
        Assert.IsNotNull(app.Vaults.Unlock(Vault, VaultSecret.Password(password), remember: false).Vault, "Le coffre chiffré du scénario ne s'est pas ouvert.");
        return action(app);
    }

    /// <summary>
    /// Vrai si le coffre, ou le dossier <paramref name="sibling"/> à côté de lui (une conversion), ne laisse lire
    /// <paramref name="text"/> dans aucun de ses fichiers.
    /// </summary>
    public bool HidesEverywhere(string text, string? sibling = null) =>
        Directory.EnumerateFiles(sibling is null ? Vault : Path.Combine(_folder, sibling), "*", SearchOption.AllDirectories).All(file => !File.ReadAllText(file).Contains(text, StringComparison.Ordinal));

    /// <summary>Un dossier neuf de cet essai.</summary>
    public string NewFolder(string name) => Directory.CreateDirectory(Path.Combine(_folder, name)).FullName;

    /// <summary>Tout ce que cet essai a mis sur le disque, passé au scan ; seul ce dossier est lu, jamais le profil de l'utilisateur.</summary>
    public ScanResult Scan(DiskScan scan) => scan.Of(_folder);

    /// <summary>Le scan, une fois que le moteur web, qui met un instant à finir et écrit jusqu'au bout, a lâché tous ses fichiers.</summary>
    public ScanResult ScanOnceReleased(DiskScan scan)
    {
        var released = FlaUI.Core.Tools.Retry.WhileFalse(() => DiskScan.IsReleased(_folder), TimeSpan.FromSeconds(45), TimeSpan.FromMilliseconds(500));
        Assert.IsTrue(released.Success, "Un programme garde encore des fichiers ouverts dans le dossier d'essai.");
        return Scan(scan);
    }

    /// <summary>La vraie application, ouverte sur ce coffre le jour donné.</summary>
    public RunningApplication Launch(DateOnly day, IReadOnlyDictionary<string, string>? environment = null) => new(_folder, day, environment);

    /// <summary>Le suivi d'une fiche, tel qu'il est écrit dans le coffre.</summary>
    public string SheetPath(string relative) => Path.Combine(Vault, relative);

    /// <summary>Les versions gardées d'une fiche.</summary>
    public IReadOnlyList<string> Versions(string sheet)
    {
        var folder = Path.Combine(Vault, ".suivi", "versions", sheet);
        return Directory.Exists(folder) ? [.. Directory.GetFiles(folder).Select(File.ReadAllText)] : [];
    }

    public string SheetFile(string sheet) => File.ReadAllText(Path.Combine(Vault, ".suivi", "fiches", $"{sheet}.json"));

    /// <summary>Le suivi d'une fiche, à interroger.</summary>
    public JsonElement SheetTracking(string sheet) => Parse(SheetFile(sheet));

    /// <summary>Le suivi des annotations d'une fiche, tel qu'il est écrit dans le coffre.</summary>
    public string AnnotationsFile(string sheet) => File.ReadAllText(Path.Combine(Vault, ".suivi", "annotations", $"{sheet}.json"));

    /// <summary>Le suivi d'une annotation.</summary>
    public JsonElement AnnotationTracking(string sheet, string annotation) =>
        Parse(AnnotationsFile(sheet)).GetProperty("annotations").EnumerateArray().Single(entry => entry.GetProperty("id").GetString() == annotation);

    /// <summary>Les annotations d'une fiche.</summary>
    public IReadOnlyList<JsonElement> Annotations(string sheet) =>
        [.. Parse(AnnotationsFile(sheet)).GetProperty("annotations").EnumerateArray().Select(entry => entry.Clone())];

    /// <summary>Le journal des tirages de rappels.</summary>
    public string DrawsFile() => File.ReadAllText(Path.Combine(Vault, ".suivi", "rappels.json"));

    /// <summary>Les fiches tirées chaque jour.</summary>
    public IReadOnlyDictionary<string, string[]> Draws() =>
        Parse(DrawsFile()).GetProperty("tirages").EnumerateArray().ToDictionary(
            draw => draw.GetProperty("jour").GetString()!,
            draw => draw.GetProperty("selection").EnumerateArray().Select(id => id.GetString()!).ToArray());

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            // Le moteur web de l'application met un instant à lâcher ses fichiers après sa fermeture.
            FlaUI.Core.Tools.Retry.WhileException(() => Directory.Delete(_folder, recursive: true), TimeSpan.FromSeconds(20), TimeSpan.FromMilliseconds(250), throwOnTimeout: true);
        }
    }

    private static JsonElement Parse(string text)
    {
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    /// <summary>Ce coffre n'est retenu nulle part comme dernier coffre ouvert.</summary>
    private sealed class NoProfile : IVaultLocations
    {
        public AppMode Mode => AppMode.Portable;

        public string? VaultToOpen() => null;

        public string? VaultToCreate() => null;

        public bool Remember(string root) => true;
    }
}

/// <summary>Une horloge réglée sur un jour, le matin, en temps universel, comme l'application lancée.</summary>
internal sealed class ScenarioClock(DateOnly day) : TimeProvider
{
    public DateOnly Day { get; set; } = day;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() => At(Day);

    /// <summary>L'instant du matin d'un jour.</summary>
    public static DateTimeOffset At(DateOnly day) => new(day.ToDateTime(new TimeOnly(8, 0)), TimeSpan.Zero);
}
