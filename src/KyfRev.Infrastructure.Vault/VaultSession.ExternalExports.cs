using System.Globalization;
using System.Text;
using KyfRev.Domain.Reviews;

namespace KyfRev.Infrastructure.Vault;

/// <remarks>
/// Exports vers d'autres outils : les passages et définitions pour Anki, les échéances pour un agenda. Ce sont des copies
/// en clair, à sens unique : rien de ce qui est fait ailleurs ne revient dans le coffre.
/// </remarks>
public sealed partial class VaultSession
{
    /// <summary>Longueur d'une ligne d'agenda, en octets, avant de la replier (RFC 5545).</summary>
    private const int CalendarLineOctets = 75;

    /// <summary>
    /// Les passages et définitions hors corbeille, au format texte d'Anki : une note par ligne, séparée par des tabulations.
    /// La première colonne est l'identifiant du passage : réimporter met la note à jour au lieu de la doubler, et Anki garde
    /// son planning. Une définition donne son terme au recto et son explication au verso ; un passage, son contexte troué
    /// au recto et le passage au verso. Les fiches entières n'y vont pas : une fiche n'est pas une carte.
    /// </summary>
    public string AnkiNotes()
    {
        var text = new StringBuilder("#separator:tab\n#html:true\n#guid column:1\n#notetype:Basic\n#deck:kyfRev\n#tags column:4\n");
        foreach (var entry in SheetsByTitle())
        {
            var sheet = entry.Sheet;
            var tags = string.Join(' ', sheet.Tags.Select(AnkiTag).Where(tag => tag.Length > 0).Prepend("kyfRev"));
            foreach (var annotation in VisibleAnnotations(sheet.Id))
            {
                string front;
                string back;
                if (annotation.Definition is { } definition)
                {
                    front = AnkiField(definition.Term);
                    back = AnkiField(definition.Explanation);
                }
                else
                {
                    var hole = $"{AnkiField(annotation.Anchor.Before)}<b>[…]</b>{AnkiField(annotation.Anchor.After)}";
                    var hint = annotation.Comment is { Length: > 0 } comment ? $"<br><i>{AnkiField(comment)}</i>" : string.Empty;
                    front = $"{hole}{hint}";
                    back = AnkiField(annotation.Anchor.Exact);
                }
                // Le titre de la fiche situe la carte, en petit, sous la question.
                front += $"<br><small>{AnkiField(sheet.Title)}</small>";
                text.Append(annotation.Id).Append('\t').Append(front).Append('\t').Append(back).Append('\t').Append(tags).Append('\n');
            }
        }
        return text.ToString();
    }

    /// <summary>
    /// Les échéances en révision, au format iCalendar : un événement d'une journée par jour chargé, qui dit combien de
    /// révisions tombent et lesquelles, plus un par examen à venir. Les retards sont comptés aujourd'hui, les éléments
    /// suspendus n'y figurent pas. Chaque événement garde le même identifiant d'un export à l'autre : réimporter met
    /// l'agenda à jour au lieu de le doubler.
    /// </summary>
    /// <param name="day">Aujourd'hui.</param>
    /// <param name="stamp">L'instant de l'export.</param>
    public string DueDatesCalendar(DateOnly day, DateTimeOffset stamp)
    {
        var byDay = new SortedDictionary<DateOnly, List<string>>();
        foreach (var entry in SheetsByTitle().Where(entry => entry.Presence != FilePresence.Missing))
        {
            void Add(Cycle cycle, string name)
            {
                var due = cycle.DueDate < day ? day : cycle.DueDate;
                if (!byDay.TryGetValue(due, out var names))
                {
                    byDay[due] = names = [];
                }
                names.Add($"{cycle.StepLabel} · {name}");
            }
            if (entry.Sheet.Tracking.DueCycle is { } cycle)
            {
                Add(cycle, entry.Sheet.Title);
            }
            foreach (var annotation in VisibleAnnotations(entry.Sheet.Id))
            {
                if (annotation.Tracking.DueCycle is { } passageCycle)
                {
                    // Le passage lui-même n'est pas écrit : ce serait donner la réponse, et sortir plus du coffre qu'il ne faut.
                    Add(passageCycle, annotation.Definition is { } definition ? $"{entry.Sheet.Title} (définition : {definition.Term})" : $"{entry.Sheet.Title} (passage)");
                }
            }
        }

        var text = new StringBuilder();
        void Line(string line)
        {
            // Une ligne se replie tous les 75 octets, la suite commençant par une espace ; jamais au milieu d'un caractère.
            var octets = 0;
            foreach (var rune in line.EnumerateRunes())
            {
                var size = rune.Utf8SequenceLength;
                if (octets + size > CalendarLineOctets)
                {
                    text.Append("\r\n ");
                    octets = 1;
                }
                text.Append(rune.ToString());
                octets += size;
            }
            text.Append("\r\n");
        }
        void Event(string id, DateOnly on, string summary, string description)
        {
            Line("BEGIN:VEVENT");
            Line($"UID:kyfrev-{Settings.Id}-{id}@kyfrev");
            Line($"DTSTAMP:{stamp.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)}");
            Line($"DTSTART;VALUE=DATE:{on.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}");
            Line($"DTEND;VALUE=DATE:{on.AddDays(1).ToString("yyyyMMdd", CultureInfo.InvariantCulture)}");
            Line($"SUMMARY:{CalendarText(summary)}");
            if (description.Length > 0)
            {
                Line($"DESCRIPTION:{CalendarText(description)}");
            }
            // Une révision n'occupe pas la journée : l'agenda ne la montre pas comme « occupé ».
            Line("TRANSP:TRANSPARENT");
            Line("END:VEVENT");
        }

        Line("BEGIN:VCALENDAR");
        Line("VERSION:2.0");
        Line("PRODID:-//kyfRev//Echeances//FR");
        Line("CALSCALE:GREGORIAN");
        Line("X-WR-CALNAME:kyfRev");
        foreach (var (due, names) in byDay)
        {
            var summary = names.Count > 1
                ? string.Create(CultureInfo.InvariantCulture, $"kyfRev : {names.Count} révisions")
                : "kyfRev : 1 révision";
            Event(due.ToString("yyyyMMdd", CultureInfo.InvariantCulture), due, summary, string.Join('\n', names));
        }
        foreach (var exam in Settings.Exams.Where(exam => exam.Day >= day))
        {
            var where = exam.Folder.Length == 0 ? "Tout le coffre" : exam.Folder;
            Event($"examen-{Convert.ToHexStringLower(Encoding.UTF8.GetBytes(exam.Folder))}", exam.Day, $"Examen : {exam.Name}", where);
        }
        Line("END:VCALENDAR");
        return text.ToString();
    }

    /// <summary>Un champ d'Anki : du HTML sur une ligne, sans tabulation ; les accents restent des lettres.</summary>
    private static string AnkiField(string text) =>
        text.Trim()
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\r\n", "<br>", StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal)
            .Replace('\t', ' ');

    /// <summary>Une étiquette d'Anki ne contient pas d'espace.</summary>
    private static string AnkiTag(string tag) =>
        string.Join('_', tag.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Un texte d'agenda : la barre oblique inverse, la virgule et le point-virgule protégés, les sauts de ligne écrits.</summary>
    private static string CalendarText(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
}
