using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace KyfRev.Infrastructure.Windows;

/// <summary>
/// Le journal de diagnostic, comme dans la 0.x (<c>diagnostic.rs</c>, D57) : <c>kyfRev.log</c>
/// s'allonge d'un démarrage à l'autre, pour qu'un arrêt inattendu reste expliqué après la
/// relance ; chaque démarrage y commence par une ligne repère. Au-delà de 1 Mo, il devient
/// <c>kyfRev.precedent.log</c> (le précédent est remplacé) : deux fichiers au plus. Comme le
/// prévoit le plan, la rotation se fait aussi en cours d'exécution, et le dossier du profil de
/// l'utilisateur est masqué (<c>%USERPROFILE%</c>). Le journal ne bloque jamais l'application :
/// s'il ne peut plus être écrit, il se tait.
/// </summary>
public sealed partial class DiagnosticLog : ILoggerFactory
{
    /// <summary>Taille au-delà de laquelle le journal passe en <c>kyfRev.precedent.log</c>.</summary>
    public const long MaximumSize = 1024 * 1024;

    private const string ProfileMask = "%USERPROFILE%";

    private readonly Lock _gate = new();
    private readonly string _file;
    private readonly string _previous;
    private readonly string _profile;
    private readonly TimeProvider _time;
    private bool _silent;

    /// <summary>Le journal du dossier <paramref name="folder"/> ; le dossier du profil <paramref name="profile"/> y est masqué.</summary>
    public DiagnosticLog(string folder, string profile, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(time);
        _file = Path.Combine(folder, "kyfRev.log");
        _previous = Path.Combine(folder, "kyfRev.precedent.log");
        _profile = Path.TrimEndingDirectorySeparator(profile);
        _time = time;
        Write(() =>
        {
            Directory.CreateDirectory(folder);
            RotateIfFull();
            // Un démarrage se repère d'un coup d'œil dans un journal qui s'allonge.
            File.AppendAllText(_file, $"\n=== {Now:yyyy-MM-dd HH:mm:ss} ===\n");
        });
    }

    /// <summary>Le fichier du journal.</summary>
    public string LogFile => _file;

    /// <summary>Les dernières lignes du journal, au plus <paramref name="count"/>, le précédent servant de début.</summary>
    public string Tail(int count)
    {
        var lines = new[] { _previous, _file }.SelectMany(ReadLines).ToList();
        return string.Join('\n', lines.Skip(Math.Max(0, lines.Count - count))) + '\n';
    }

    /// <summary>Les lignes d'un journal, même en cours d'écriture ; rien s'il n'existe pas.</summary>
    private static string[] ReadLines(string file)
    {
        if (!File.Exists(file))
        {
            return [];
        }
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    private DateTimeOffset Now => _time.GetLocalNow();

    /// <summary>Consigne le démarrage : version, mode, système.</summary>
    public void Started(string version, string mode)
    {
        var logger = CreateLogger("kyfRev");
        if (logger.IsEnabled(LogLevel.Information))
        {
            LogStarted(logger, version, mode, RuntimeInformation.OSDescription, RuntimeInformation.OSArchitecture);
        }
    }

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    /// <summary>Sans effet : le journal n'a qu'une destination.</summary>
    public void AddProvider(ILoggerProvider provider)
    {
    }

    /// <summary>Rien à libérer : chaque ligne est écrite et fermée aussitôt.</summary>
    public void Dispose()
    {
    }

    /// <summary>Le texte avec le dossier du profil masqué.</summary>
    internal string Mask(string text) =>
        _profile.Length == 0 ? text : text.Replace(_profile, ProfileMask, StringComparison.OrdinalIgnoreCase);

    private void Append(LogLevel level, string category, string message, Exception? exception)
    {
        var line = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"{Now:yyyy-MM-dd HH:mm:ss.fff} {Name(level),-13} {category} : {message}\n");
        if (exception is not null)
        {
            // L'exception et sa pile, en retrait sous la ligne.
            line.Append(("    " + exception + "\n").Replace("\n", "\n    ", StringComparison.Ordinal).TrimEnd(' '));
        }
        var text = Mask(line.ToString());
        Write(() =>
        {
            RotateIfFull();
            File.AppendAllText(_file, text);
        });
    }

    /// <summary>Écrit sous verrou ; un échec rend le journal muet plutôt que d'arrêter l'application.</summary>
    private void Write(Action write)
    {
        lock (_gate)
        {
            if (_silent)
            {
                return;
            }
            try
            {
                write();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _silent = true;
            }
        }
    }

    private void RotateIfFull()
    {
        if (new FileInfo(_file) is { Exists: true, Length: > MaximumSize })
        {
            File.Move(_file, _previous, overwrite: true);
        }
    }

    /// <summary>Consigne un arrêt inattendu de l'application (exception non rattrapée).</summary>
    [LoggerMessage(Level = LogLevel.Critical, Message = "Arrêt inattendu de kyfRev.")]
    public static partial void UnexpectedStop(ILogger logger, Exception? exception);

    /// <summary>Consigne l'erreur d'une tâche de fond dont personne n'a observé l'issue.</summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Erreur d'une tâche de fond non observée.")]
    public static partial void UnobservedTask(ILogger logger, Exception exception);

    /// <summary>Consigne un incident du moteur web : sa raison, jamais le contenu d'une fiche.</summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Moteur web : {Reason}.")]
    public static partial void WebEngineTrouble(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Démarrage de kyfRev {Version} ({Mode}), {System} {Architecture}.")]
    private static partial void LogStarted(ILogger logger, string version, string mode, string system, Architecture architecture);

    private static string Name(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DÉBOGAGE",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "AVERTISSEMENT",
        LogLevel.Error => "ERREUR",
        _ => "CRITIQUE",
    };

    private sealed class Logger(DiagnosticLog log, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        // Comme la 0.x : à partir de l'information.
        public bool IsEnabled(LogLevel logLevel) => logLevel is >= LogLevel.Information and < LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            if (IsEnabled(logLevel))
            {
                log.Append(logLevel, category, formatter(state, exception), exception);
            }
        }
    }
}
