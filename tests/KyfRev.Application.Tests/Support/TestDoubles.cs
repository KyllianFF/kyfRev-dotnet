using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.Infrastructure.Vault.Adapters;
using Microsoft.Extensions.Logging;

namespace KyfRev.Application.Tests.Support;

/// <summary>Horloge simulée : l'instant avance à la demande, dans un fuseau choisi.</summary>
internal sealed class FakeTime(DateTimeOffset utcNow, TimeZoneInfo zone) : TimeProvider
{
    /// <summary>Fuseau d'essai à UTC+2, sans heure d'été : minuit local tombe à 22 h UTC.</summary>
    public static TimeZoneInfo PlusTwo { get; } = TimeZoneInfo.CreateCustomTimeZone("Essai+02", TimeSpan.FromHours(2), "Essai +02", "Essai +02");

    /// <summary>L'instant présent, toujours en temps universel (le contrat de <see cref="TimeProvider.GetUtcNow"/>).</summary>
    public DateTimeOffset UtcNow { get; private set; } = utcNow.ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone => zone;

    /// <summary>Horloge réglée sur une heure locale du fuseau d'essai.</summary>
    public static FakeTime AtLocal(int year, int month, int day, int hour, int minute, int second = 0) =>
        new(new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.FromHours(2)), PlusTwo);

    public override DateTimeOffset GetUtcNow() => UtcNow;

    public void Advance(TimeSpan duration) => UtcNow += duration;
}

/// <summary>Journal en mémoire.</summary>
internal sealed class ListLogs : ILoggerFactory
{
    private readonly Lock _gate = new();
    private readonly List<(LogLevel Level, string Message)> _entries = [];

    public IReadOnlyList<(LogLevel Level, string Message)> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }

    public IEnumerable<string> Warnings => Entries.Where(e => e.Level == LogLevel.Warning).Select(e => e.Message);

    public ILogger CreateLogger(string categoryName) => new Logger(this);

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }

    private sealed class Logger(ListLogs logs) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (logs._gate)
            {
                logs._entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}

/// <summary>Emplacements des coffres, en mémoire ; la mémoire peut échouer à la demande.</summary>
internal sealed class VaultLocations : IVaultLocations
{
    public AppMode Mode { get; set; } = AppMode.Development;

    /// <summary>Coffre prévu par le mode portable ou installé.</summary>
    public string? Planned { get; set; }

    /// <summary>Le coffre prévu est déjà créé.</summary>
    public bool PlannedExists { get; set; }

    public List<string> Remembered { get; } = [];

    public Exception? Failure { get; set; }

    /// <summary>La mémoire ne peut pas être écrite.</summary>
    public bool Unwritable { get; set; }

    public string? VaultToOpen() => Mode == AppMode.Portable ? Planned : Remembered.LastOrDefault() ?? Planned;

    public string? VaultToCreate() => PlannedExists ? null : Planned;

    public bool Remember(string root)
    {
        if (Failure is not null)
        {
            throw Failure;
        }
        if (Unwritable)
        {
            return false;
        }
        Remembered.Add(root);
        return true;
    }
}

/// <summary>Dossier d'essai jetable : <c>…/MesRévisions/Coffre</c>, ses sauvegardes à côté.</summary>
internal sealed class TestFolder : IDisposable
{
    public TestFolder()
    {
        Root = Path.Combine(Path.GetTempPath(), "kyfrev-application-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string Vault => Path.Combine(Root, "Coffre");

    public string Backups => Path.Combine(Root, "Sauvegardes");

    public string PathOf(params string[] parts) => Path.Combine([Root, .. parts]);

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}

/// <summary>L'application assemblée sur de vrais coffres, avec une horloge et un journal d'essai.</summary>
internal sealed class TestApplication : IDisposable
{
    public TestApplication(FakeTime time)
    {
        Time = time;
        App = new KyfRevApplication(new VaultStore(), new BackupFiles(), Locations, time, Logs, "1.0.0-essai");
    }

    public FakeTime Time { get; }

    public ListLogs Logs { get; } = new();

    public VaultLocations Locations { get; } = new();

    public KyfRevApplication App { get; }

    public void Dispose()
    {
        App.Dispose();
        Logs.Dispose();
    }
}
