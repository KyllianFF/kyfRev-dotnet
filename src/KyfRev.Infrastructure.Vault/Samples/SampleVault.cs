using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using KyfRev.Application;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.Domain.Reviews;
using KyfRev.Infrastructure.Vault.Adapters;
using Microsoft.Extensions.Logging.Abstractions;

namespace KyfRev.Infrastructure.Vault.Samples;

/// <summary>Ce qu'un coffre d'exemple contient, une fois créé.</summary>
/// <param name="Sheets">Ses fiches.</param>
/// <param name="Marks">Ses passages surlignés et ses définitions.</param>
/// <param name="Schemas">Ses schémas réseau.</param>
/// <param name="Quotes">Ses citations.</param>
public sealed record SampleSummary(int Sheets, int Marks, int Schemas, int Quotes);

/// <summary>
/// Un coffre rempli de fiches inventées, pour voir kyfRev en marche sans rien avoir écrit. Il vit quelques semaines en
/// accéléré jusqu'à aujourd'hui, par les vrais cas d'usage : ses dates tombent donc juste quel que soit le jour où il est
/// créé, avec des révisions en retard, du jour et à venir.
/// </summary>
/// <remarks>
/// Deux tailles : le coffre d'exemple que propose l'accueil (dix fiches sur six matières), et le coffre d'essai complet de
/// <c>tools/coffre-essai</c>, avec ses archives, ses rappels et sa corbeille. Il ne se crée que dans un dossier absent ou
/// vide : aucun coffre existant ne peut être écrasé, et rien n'est retenu dans le profil.
/// </remarks>
public static partial class SampleVault
{
    /// <summary>Message de refus : le dossier a déjà quelque chose.</summary>
    public const string FolderNotEmpty = "Ce dossier n'est pas vide : un coffre d'exemple ne se crée que dans un dossier absent ou vide.";

    /// <summary>Le dossier est absent ou vide : un coffre d'exemple peut y être créé.</summary>
    public static bool CanCreateIn(string root) =>
        !Directory.Exists(root) || !Directory.EnumerateFileSystemEntries(root).Any();

    /// <summary>Crée le coffre d'exemple dans <paramref name="root"/>, tel qu'il serait aujourd'hui.</summary>
    /// <param name="root">Un dossier absent ou vide.</param>
    /// <param name="today">Le jour où il est créé.</param>
    /// <param name="everything">Le coffre d'essai complet, au lieu des dix fiches de l'exemple.</param>
    /// <returns>Ce qu'il contient, ou pourquoi il n'a pas été créé.</returns>
    public static Outcome<SampleSummary> Create(string root, DateOnly today, bool everything = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var full = Path.GetFullPath(root);
        if (!CanCreateIn(full))
        {
            return Outcome.Refused<SampleSummary>(FolderNotEmpty);
        }
        try
        {
            using var builder = new Builder(full, today, everything ? SampleContent.Seeds : SampleContent.Starter, everything);
            return Outcome.Of(builder.Build());
        }
        catch (Exception refused) when (refused is InvalidOperationException or VaultException or IOException or UnauthorizedAccessException)
        {
            return Outcome.Refused<SampleSummary>(refused.Message);
        }
    }

    [GeneratedRegex(@"\{\{schema:([a-z]+)\}\}")]
    private static partial Regex SchemaPlace();

    [GeneratedRegex(@"\{\{image:([a-z-]+)\|([^}]+)\}\}")]
    private static partial Regex ImagePlace();

    [GeneratedRegex(@"\{\{lien:([^|]+)\|([^}]+)\}\}")]
    private static partial Regex LinkPlace();

    [GeneratedRegex("^(#{1,6}) (.+)$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^(#{1,6} |[-*] |> |\d+\. )")]
    private static partial Regex LineMark();

    private static T Check<T>(Outcome<T> outcome) =>
        outcome.Succeeded ? outcome.Value : throw new InvalidOperationException(outcome.Error);

    /// <summary>
    /// L'ancre d'un passage, sur une projection simple du texte affiché : Markdown retiré, blocs de code écartés. Le texte
    /// exact et son contexte retrouvent le passage, comme à toute réouverture ; les positions n'en sont qu'un repère.
    /// </summary>
    private static Anchor AnchorOf(OpenedSheet sheet, string exact)
    {
        var displayed = new StringBuilder();
        var section = new List<string>();
        var levels = new List<int>();
        var fenced = false;
        var start = -1;
        var found = 0;
        ImmutableArray<string> path = [];
        var lines = sheet.Body.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd('\r');
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                fenced = !fenced;
                continue;
            }
            if (fenced)
            {
                continue;
            }
            var heading = Heading().Match(line);
            var plain = LineMark().Replace(line, string.Empty).Replace("**", string.Empty, StringComparison.Ordinal).Replace("`", string.Empty, StringComparison.Ordinal);
            if (heading.Success)
            {
                var level = heading.Groups[1].Length;
                while (levels.Count > 0 && levels[^1] >= level)
                {
                    levels.RemoveAt(levels.Count - 1);
                    section.RemoveAt(section.Count - 1);
                }
                levels.Add(level);
                section.Add(heading.Groups[2].Value);
            }
            var at = plain.IndexOf(exact, StringComparison.Ordinal);
            if (at >= 0 && start < 0)
            {
                start = displayed.Length + at;
                found = index;
                path = [.. section];
            }
            displayed.Append(plain).Append('\n');
        }
        if (start < 0)
        {
            throw new InvalidOperationException($"« {exact} » est introuvable dans « {sheet.Title} ».");
        }
        var end = start + exact.Length;
        var text = displayed.ToString();
        return new Anchor(exact, text[Math.Max(0, start - 32)..start], text[end..Math.Min(text.Length, end + 32)], start, end, path, sheet.FirstBodyLine + found, sheet.Fingerprint);
    }

    /// <summary>Le coffre en train de se remplir : une application à part, sur son horloge, fermée une fois le coffre écrit.</summary>
    private sealed class Builder : IDisposable
    {
        private readonly string _root;
        private readonly DateOnly _today;
        private readonly ImmutableArray<Seed> _seeds;
        private readonly bool _everything;
        private readonly SeedClock _clock = new();
        private readonly Dictionary<string, string> _schemas = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Planted> _planted = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Rhythm> _rhythms = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _reviews = new(StringComparer.Ordinal);
        private KyfRevApplication? _app;
        private int _marks;

        public Builder(string root, DateOnly today, ImmutableArray<Seed> seeds, bool everything)
        {
            _root = root;
            _today = today;
            _seeds = seeds;
            _everything = everything;
        }

        private KyfRevApplication App => _app ?? throw new InvalidOperationException("Le coffre d'exemple n'est pas ouvert.");

        public SampleSummary Build()
        {
            var first = _today.AddDays(-_seeds.Max(seed => seed.Age));
            _clock.Set(first, _today);
            // Les schémas réseau d'abord : les fiches qui les citent ont besoin de leur identifiant.
            var wanted = _seeds.SelectMany(seed => SchemaPlace().Matches(seed.Body).Select(match => match.Groups[1].Value)).ToHashSet(StringComparer.Ordinal);
            using (var session = VaultSession.Initialize(_root, Present.Of(_clock).Now))
            {
                foreach (var (key, schema) in Network.Schemas.Where(schema => wanted.Contains(schema.Key)))
                {
                    _schemas[key] = session.CreateSchema(schema).Id;
                }
            }
            _app = new KyfRevApplication(new VaultStore(), new BackupFiles(), new NoProfile(), _clock, NullLoggerFactory.Instance, "exemple");
            App.Vaults.Open(_root);
            var preferences = Check(App.Settings.Get());
            Check(App.Settings.Change(preferences with { QuotesActive = true, QuotesOnline = false, SchemasActive = _schemas.Count > 0 }));

            for (var day = first; day <= _today; day = day.AddDays(1))
            {
                _clock.Set(day, _today);
                Live(day);
                if (day == _today)
                {
                    break;
                }
                Review(day);
            }

            foreach (var quote in SampleContent.Quotes)
            {
                var now = Present.Of(_clock).Now;
                Check(App.Host.Use(vault => vault.AddQuote(quote, now)));
            }
            Check(App.Exams.Define("Histoire", "Partiel d'histoire", _today.AddDays(18)));
            if (_everything)
            {
                Check(App.Exams.Define("Réseaux", "Certification réseaux", _today.AddDays(41)));
                Check(App.Templates.SaveAsTemplate(_planted["La responsabilité civile"].Id, "Fiche de droit"));
            }
            return new SampleSummary(_planted.Count, _marks, _schemas.Count, SampleContent.Quotes.Length);
        }

        public void Dispose() => _app?.Dispose();

        /// <summary>Ce qui arrive ce jour-là : les fiches créées, puis ce que chacune a de prévu à cet âge.</summary>
        private void Live(DateOnly day)
        {
            foreach (var seed in _seeds.Where(seed => _today.AddDays(-seed.Age) == day))
            {
                Plant(seed);
            }
            foreach (var (seed, sheet) in _planted.Values.Select(entry => (entry.Seed, entry.Id)).ToList())
            {
                var age = day.DayNumber - _today.AddDays(-seed.Age).DayNumber;
                foreach (var mark in seed.Marks.Where(mark => mark.After == age))
                {
                    Highlight(sheet, seed, mark);
                }
                foreach (var scripted in seed.Later.Where(scripted => scripted.After == age))
                {
                    Play(scripted.Act, sheet, seed, day);
                }
            }
        }

        /// <summary>Les révisions d'un jour passé : chacune selon le rythme de sa fiche, et les rappels tirés la veille.</summary>
        private void Review(DateOnly day)
        {
            var board = Check(App.Reminders.Dashboard());
            foreach (var item in board.Overdue.Concat(board.Today))
            {
                var rhythm = _rhythms[item.Id];
                _reviews.TryGetValue(item.Id, out var done);
                if (item.DaysLate >= rhythm.Late && (rhythm.StopAfter is not { } limit || done < limit))
                {
                    Check(App.Reviews.Validate(item.Kind, item.Id));
                    _reviews[item.Id] = done + 1;
                }
            }
            foreach (var reminder in board.Reminders.Where(reminder => reminder.DrawnOn < day))
            {
                var seed = _planted.Values.Single(entry => entry.Id == reminder.SheetId).Seed;
                if (seed.Answer is { } answer)
                {
                    Answer(reminder.SheetId, answer);
                }
            }
        }

        private void Plant(Seed seed)
        {
            var id = Check(App.Sheets.Create(seed.Folder.Length == 0 ? null : seed.Folder, seed.Title, null));
            _planted[seed.Title] = new Planted(id, seed);
            _rhythms[id] = seed.Rhythm;
            if (!seed.Tags.IsEmpty)
            {
                Check(App.Sheets.SetTags(id, seed.Tags));
            }
            var opened = Check(App.Sheets.Open(id));
            var body = SchemaPlace().Replace(seed.Body, match => $"```reseau\nschema: {_schemas[match.Groups[1].Value]}\n```");
            body = ImagePlace().Replace(body, match =>
            {
                var link = Check(App.Editing.AddImage(id, match.Groups[1].Value + ".svg", Encoding.UTF8.GetBytes(Network.Images[match.Groups[1].Value])));
                return $"![{match.Groups[2].Value}]({link})";
            });
            // Un lien vers une fiche que ce coffre n'a pas reste un texte ordinaire.
            body = LinkPlace().Replace(body, match => _planted.ContainsKey(match.Groups[1].Value)
                ? $"[{match.Groups[2].Value}]({RelativeLink(opened, match.Groups[1].Value)})"
                : match.Groups[2].Value);
            Check(App.Editing.Save(id, body, opened.Fingerprint, 1));
        }

        /// <summary>Le lien relatif entre deux fiches : celle qui est citée existe déjà.</summary>
        private string RelativeLink(OpenedSheet from, string title)
        {
            var target = Check(App.Sheets.Open(_planted[title].Id));
            var relative = Path.GetRelativePath(Path.GetDirectoryName(from.Path)!, target.Path).Replace('\\', '/');
            return relative.Replace(" ", "%20", StringComparison.Ordinal);
        }

        private void Highlight(string sheet, Seed seed, Mark mark)
        {
            var opened = Check(App.Sheets.Open(sheet));
            var anchor = AnchorOf(opened, mark.Exact);
            var id = mark.Term is { } term
                ? Check(App.Annotations.CreateDefinition(sheet, anchor, new Definition(term, mark.Explanation ?? string.Empty)))
                : Check(App.Annotations.Create(sheet, anchor, mark.Comment));
            _rhythms[id] = seed.Rhythm;
            _marks++;
            if (mark.Trashed)
            {
                Check(App.Annotations.Trash(id));
            }
        }

        private void Play(Act act, string sheet, Seed seed, DateOnly day)
        {
            switch (act)
            {
                case Act.ArchiveByHand:
                    Check(App.Reviews.Archive(sheet));
                    break;
                case Act.Postpone:
                    Check(App.Reviews.Postpone(ItemKind.Sheet, sheet, day.AddDays(3)));
                    break;
                case Act.Restart:
                    Check(App.Reviews.Restart(ItemKind.Sheet, sheet));
                    break;
                case Act.Rewrite:
                    var opened = Check(App.Sheets.Open(sheet));
                    Check(App.Editing.KeepAsVersion(sheet, opened.Body));
                    Check(App.Editing.Save(sheet, opened.Body.TrimEnd() + "\n\n" + seed.Addition + "\n", opened.Fingerprint, 1));
                    break;
                default:
                    Check(App.Trash.TrashSheet(sheet));
                    break;
            }
        }

        private void Answer(string sheet, ReminderOutcome answer)
        {
            // « Certains points à revoir » : les passages marqués repartent de J0, puis le rappel est répondu.
            if (answer == ReminderOutcome.SomePointsToReview)
            {
                Check(App.Reviews.Restart(ItemKind.Passage, Check(App.Sheets.Open(sheet)).Annotations.First(annotation => !annotation.InTrash).Id));
            }
            Check(App.Reminders.Handle(sheet, answer));
        }
    }

    /// <summary>Une horloge qui suit le jour simulé, à l'heure locale du poste.</summary>
    private sealed class SeedClock : TimeProvider
    {
        private DateTimeOffset _now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;

        /// <summary>Le soir pour les jours passés ; l'heure réelle pour le dernier, s'il est bien aujourd'hui.</summary>
        public void Set(DateOnly day, DateOnly today)
        {
            if (day == today && day == DateOnly.FromDateTime(DateTime.Now))
            {
                _now = DateTimeOffset.Now;
                return;
            }
            var evening = day.ToDateTime(new TimeOnly(18, 30));
            _now = new DateTimeOffset(evening, TimeZoneInfo.Local.GetUtcOffset(evening));
        }

        public override DateTimeOffset GetUtcNow() => _now.ToUniversalTime();
    }

    /// <summary>Aucun profil : ce coffre n'est retenu nulle part comme dernier coffre ouvert.</summary>
    private sealed class NoProfile : IVaultLocations
    {
        public AppMode Mode => AppMode.Portable;

        public string? VaultToOpen() => null;

        public string? VaultToCreate() => null;

        public bool Remember(string root) => true;
    }
}
