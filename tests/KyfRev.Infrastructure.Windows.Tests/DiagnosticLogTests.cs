using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace KyfRev.Infrastructure.Windows.Tests;

/// <summary>
/// Le journal de diagnostic (D57) : il s'allonge d'un démarrage à l'autre, tourne au-delà de
/// 1 Mo (au démarrage comme en cours d'exécution), masque le dossier du profil, et se tait plutôt
/// que d'arrêter l'application.
/// </summary>
[TestClass]
public sealed class DiagnosticLogTests
{
    private static readonly DateTimeOffset Morning = new(2026, 11, 1, 8, 30, 0, TimeSpan.Zero);

    [TestMethod]
    public void The_tail_takes_the_last_lines_and_starts_in_the_previous_log()
    {
        using var folder = new Folder();
        using var log = new DiagnosticLog(folder.Path, @"C:\Users\Élève", new Clock(Morning));
        File.WriteAllText(Path.Combine(folder.Path, "kyfRev.precedent.log"), "une\ndeux\n");
        File.WriteAllText(log.LogFile, "trois\n\nquatre\n");

        Assert.AreEqual("trois\nquatre\n", log.Tail(2));
        Assert.AreEqual("deux\ntrois\nquatre\n", log.Tail(3));
        Assert.AreEqual("une\ndeux\ntrois\nquatre\n", log.Tail(300));
    }

    [TestMethod]
    public void The_tail_of_a_log_that_does_not_exist_is_empty()
    {
        using var folder = new Folder();
        using var log = new DiagnosticLog(folder.Path, @"C:\Users\Élève", new Clock(Morning));
        File.Delete(log.LogFile);

        Assert.AreEqual("\n", log.Tail(10));
    }

    [TestMethod]
    public void Each_start_is_marked_and_lines_follow()
    {
        using var folder = new Folder();
        using var log = new DiagnosticLog(folder.Path, @"C:\Users\Élève", new Clock(Morning));

        log.Started("1.0.0-alpha.1", "installée");
        var logger = log.CreateLogger("KyfRev.Application.UseCases.VaultUseCases");
        Write(logger, LogLevel.Warning, @"Sauvegarde impossible : C:\Users\élève\Documents\kyfRev\Sauvegardes est en lecture seule");
        Write(logger, LogLevel.Debug, "Détail sans intérêt");
        var failure = new InvalidOperationException("Panne");
        DiagnosticLog.UnexpectedStop(logger, failure);
        var timeout = new TimeoutException("Trop long");
        DiagnosticLog.UnobservedTask(logger, timeout);
        using var next = new DiagnosticLog(folder.Path, @"C:\Users\Élève", new Clock(Morning.AddHours(1)));

        var lines = File.ReadAllText(log.LogFile).Split('\n');
        Assert.AreEqual(string.Empty, lines[0]);
        Assert.AreEqual("=== 2026-11-01 08:30:00 ===", lines[1]);
        StringAssert.StartsWith(lines[2], "2026-11-01 08:30:00.000 INFO          kyfRev : Démarrage de kyfRev 1.0.0-alpha.1 (installée), ", StringComparison.Ordinal);
        Assert.AreEqual(@"2026-11-01 08:30:00.000 AVERTISSEMENT KyfRev.Application.UseCases.VaultUseCases : Sauvegarde impossible : %USERPROFILE%\Documents\kyfRev\Sauvegardes est en lecture seule", lines[3]);
        Assert.AreEqual("2026-11-01 08:30:00.000 CRITIQUE      KyfRev.Application.UseCases.VaultUseCases : Arrêt inattendu de kyfRev.", lines[4]);
        Assert.AreEqual("    System.InvalidOperationException: Panne", lines[5]);
        Assert.AreEqual("2026-11-01 08:30:00.000 ERREUR        KyfRev.Application.UseCases.VaultUseCases : Erreur d'une tâche de fond non observée.", lines[6]);
        Assert.AreEqual("    System.TimeoutException: Trop long", lines[7]);
        Assert.AreEqual("=== 2026-11-01 09:30:00 ===", lines[^2], "le démarrage suivant continue le même journal");
        Assert.IsFalse(File.ReadAllText(log.LogFile).Contains("Détail", StringComparison.Ordinal), "à partir de l'information, comme la 0.x");
        Assert.IsNull(logger.BeginScope("portée"));
        Assert.IsFalse(logger.IsEnabled(LogLevel.None));
        Assert.IsTrue(logger.IsEnabled(LogLevel.Critical));
    }

    [TestMethod]
    public void Beyond_one_megabyte_the_log_becomes_the_previous_one()
    {
        using var folder = new Folder();
        var file = Path.Combine(folder.Path, "kyfRev.log");
        var previous = Path.Combine(folder.Path, "kyfRev.precedent.log");
        File.WriteAllText(previous, "très ancien");
        File.WriteAllText(file, new string('a', (int)DiagnosticLog.MaximumSize + 1));

        // Au démarrage.
        using var log = new DiagnosticLog(folder.Path, string.Empty, new Clock(Morning));
        Assert.AreEqual(DiagnosticLog.MaximumSize + 1, new FileInfo(previous).Length, "le précédent est remplacé");
        Assert.IsLessThan(100, new FileInfo(file).Length);

        // En cours d'exécution.
        var logger = log.CreateLogger("essai");
        var big = new string('b', 64 * 1024);
        for (var line = 0; line < 17; line++)
        {
            Write(logger, LogLevel.Information, big);
        }
        Assert.IsGreaterThan(DiagnosticLog.MaximumSize, new FileInfo(previous).Length);
        Assert.IsLessThan(DiagnosticLog.MaximumSize, new FileInfo(file).Length);
        Assert.HasCount(2, Directory.GetFiles(folder.Path), "deux fichiers au plus");
    }

    [TestMethod]
    public void A_log_that_cannot_be_written_falls_silent()
    {
        using var folder = new Folder();
        // Un fichier à la place du dossier du journal.
        var blocked = Path.Combine(folder.Path, "bloqué");
        File.WriteAllText(blocked, string.Empty);

        using var log = new DiagnosticLog(blocked, string.Empty, new Clock(Morning));
        log.Started("1.0.0", "portable");
        Write(log.CreateLogger("essai"), LogLevel.Error, "Rien ne s'écrit.");

        Assert.AreEqual(string.Empty, File.ReadAllText(blocked));
        Assert.AreEqual(Path.Combine(blocked, "kyfRev.log"), log.LogFile);
    }

    [TestMethod]
    public void Lines_from_several_threads_are_never_mixed()
    {
        using var folder = new Folder();
        using var log = new DiagnosticLog(folder.Path, string.Empty, new Clock(Morning));
        var logger = log.CreateLogger("essai");

        Parallel.For(0, 200, n => Write(logger, LogLevel.Information, $"Ligne {n} {new string('x', 200)}"));

        var lines = File.ReadAllLines(log.LogFile, Encoding.UTF8).Where(line => line.Contains("Ligne", StringComparison.Ordinal)).ToList();
        Assert.HasCount(200, lines);
        Assert.IsTrue(lines.All(line => line.EndsWith(new string('x', 200), StringComparison.Ordinal)));
    }

    [TestMethod]
    public void The_log_factory_needs_its_parameters_and_ignores_providers()
    {
        using var folder = new Folder();
        Assert.ThrowsExactly<ArgumentNullException>(() => new DiagnosticLog(null!, string.Empty, TimeProvider.System));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DiagnosticLog(folder.Path, null!, TimeProvider.System));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DiagnosticLog(folder.Path, string.Empty, null!));
        using var log = new DiagnosticLog(folder.Path, string.Empty, TimeProvider.System);
        log.AddProvider(null!);
        Assert.ThrowsExactly<ArgumentNullException>(() => log.CreateLogger("essai").Log(LogLevel.Information, default, "état", null, null!));
        Assert.AreEqual("rien à masquer", log.Mask("rien à masquer"));
    }

    /// <summary>Écrit un message tel quel, comme le ferait un message journalisé.</summary>
    private static void Write(ILogger logger, LogLevel level, string message) => logger.Log(level, default, message, null, (text, _) => text);

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Folder : IDisposable
    {
        public string Path { get; } = Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kyfrev-journal", Guid.NewGuid().ToString("N"))).FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
