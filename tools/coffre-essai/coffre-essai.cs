#:project ../../src/KyfRev.Infrastructure.Vault/KyfRev.Infrastructure.Vault.csproj
#:property ImplicitUsings=enable
#:property PublishAot=false

// Crée un coffre d'essai rempli de données inventées, pour essayer l'application sans toucher à un
// vrai coffre (voir tools/coffre-essai/README.md).
// Usage, à la racine du dépôt : dotnet run tools/coffre-essai/coffre-essai.cs -- <dossier absent ou vide>
// Le coffre vit plusieurs mois en accéléré, jusqu'à aujourd'hui : il a des retards, des révisions du
// jour, des archives, des rappels en attente et un historique.
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.Domain.Reviews;
using KyfRev.Infrastructure.Vault;
using KyfRev.Infrastructure.Vault.Adapters;
using Microsoft.Extensions.Logging.Abstractions;

Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
if (args.Length != 1)
{
    Console.Error.WriteLine("Usage : dotnet run tools/coffre-essai/coffre-essai.cs -- <dossier absent ou vide>");
    return 2;
}
var root = Path.GetFullPath(args[0]);
// Un coffre existant n'est jamais vide : il ne peut pas être écrasé.
if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
{
    Console.Error.WriteLine($"Refusé : {root} n'est pas vide. Ce générateur ne remplit qu'un dossier absent ou vide.");
    return 2;
}

var today = DateOnly.FromDateTime(DateTime.Now);
var clock = new SeedClock();
var first = today.AddDays(-Content.Seeds.Max(seed => seed.Age));
clock.Set(first, today);

var schemas = new Dictionary<string, string>(StringComparer.Ordinal);
using (var session = VaultSession.Initialize(root, Present.Of(clock).Now))
{
    foreach (var (key, schema) in Network.Schemas)
    {
        schemas[key] = session.CreateSchema(schema).Id;
    }
}

using var app = new KyfRevApplication(new VaultStore(), new BackupFiles(), new NoProfile(), clock, NullLoggerFactory.Instance, "essai");
app.Vaults.Open(root);
var preferences = Check(app.Settings.Get());
Check(app.Settings.Change(preferences with { QuotesActive = true, QuotesOnline = false, SchemasActive = true }));

var planted = new Dictionary<string, Planted>(StringComparer.Ordinal);
var rhythms = new Dictionary<string, Rhythm>(StringComparer.Ordinal);
var reviews = new Dictionary<string, int>(StringComparer.Ordinal);
var marks = 0;

for (var day = first; day <= today; day = day.AddDays(1))
{
    clock.Set(day, today);
    foreach (var seed in Content.Seeds.Where(seed => today.AddDays(-seed.Age) == day))
    {
        Plant(seed);
    }
    foreach (var (seed, sheet) in planted.Values.Select(entry => (entry.Seed, entry.Id)))
    {
        var age = day.DayNumber - today.AddDays(-seed.Age).DayNumber;
        foreach (var mark in seed.Marks.Where(mark => mark.After == age))
        {
            Highlight(sheet, seed, mark);
        }
        foreach (var scripted in seed.Later.Where(scripted => scripted.After == age))
        {
            Play(scripted.Act, sheet, seed, day);
        }
    }

    var board = Check(app.Reminders.Dashboard());
    if (day == today)
    {
        break;
    }
    foreach (var item in board.Overdue.Concat(board.Today))
    {
        var rhythm = rhythms[item.Id];
        reviews.TryGetValue(item.Id, out var done);
        if (item.DaysLate >= rhythm.Late && (rhythm.StopAfter is not { } limit || done < limit))
        {
            Check(app.Reviews.Validate(item.Kind, item.Id));
            reviews[item.Id] = done + 1;
        }
    }
    foreach (var reminder in board.Reminders.Where(reminder => reminder.DrawnOn < day))
    {
        var seed = planted.Values.Single(entry => entry.Id == reminder.SheetId).Seed;
        if (seed.Answer is { } answer)
        {
            Answer(reminder.SheetId, answer);
        }
    }
}

foreach (var quote in Content.Quotes)
{
    var now = Present.Of(clock).Now;
    Check(app.Host.Use(vault => vault.AddQuote(quote, now)));
}
Check(app.Exams.Define("Histoire", "Partiel d'histoire", today.AddDays(18)));
Check(app.Exams.Define("Réseaux", "Certification réseaux", today.AddDays(41)));
Check(app.Templates.SaveAsTemplate(planted["La responsabilité civile"].Id, "Fiche de droit"));

var final = Check(app.Reminders.Dashboard());
var health = Check(app.Maintenance.Analyze());
Console.WriteLine($"Coffre d'essai créé : {root}");
Console.WriteLine($"  {planted.Count} fiches ({final.ActiveSheets} en révision, {final.ArchivedSheets} archivées), {marks} passages et définitions, {schemas.Count} schémas réseau, {Content.Quotes.Length} citations");
Console.WriteLine($"  Aujourd'hui : {final.Overdue.Length} en retard, {final.Today.Length} à réviser, {final.Reminders.Length} rappels en attente, {final.Upcoming.Length} à venir dans la semaine");
Console.WriteLine($"  Révisions des 7 derniers jours : {string.Join(" ", final.RecentReviews.Select(reviewed => reviewed.Reviews.ToString(CultureInfo.InvariantCulture)))}");
Console.WriteLine($"  Santé du coffre : {health.Problems} problème(s)");
foreach (var link in health.BrokenLinks)
{
    Console.WriteLine($"    lien cassé : {link}");
}
foreach (var passage in health.AnnotationsToCheck.Concat(health.OrphanAnnotations))
{
    Console.WriteLine($"    passage à vérifier : {passage}");
}
return health.Problems == 0 ? 0 : 1;

void Plant(Seed seed)
{
    var id = Check(app.Sheets.Create(seed.Folder.Length == 0 ? null : seed.Folder, seed.Title, null));
    planted[seed.Title] = new Planted(id, seed);
    rhythms[id] = seed.Rhythm;
    if (!seed.Tags.IsEmpty)
    {
        Check(app.Sheets.SetTags(id, seed.Tags));
    }
    var opened = Check(app.Sheets.Open(id));
    var body = Regex.Replace(seed.Body, @"\{\{schema:([a-z]+)\}\}", match => $"```reseau\nschema: {schemas[match.Groups[1].Value]}\n```");
    body = Regex.Replace(body, @"\{\{image:([a-z-]+)\|([^}]+)\}\}", match =>
    {
        var link = Check(app.Editing.AddImage(id, match.Groups[1].Value + ".svg", Encoding.UTF8.GetBytes(Network.Images[match.Groups[1].Value])));
        return $"![{match.Groups[2].Value}]({link})";
    });
    body = Regex.Replace(body, @"\{\{lien:([^|]+)\|([^}]+)\}\}", match => $"[{match.Groups[2].Value}]({RelativeLink(opened, match.Groups[1].Value)})");
    Check(app.Editing.Save(id, body, opened.Fingerprint, 1));
}

// Le lien relatif entre deux fiches : celle qui est citée existe déjà.
string RelativeLink(OpenedSheet from, string title)
{
    var target = Check(app.Sheets.Open(planted[title].Id));
    var relative = Path.GetRelativePath(Path.GetDirectoryName(from.Path)!, target.Path).Replace('\\', '/');
    return relative.Replace(" ", "%20", StringComparison.Ordinal);
}

void Highlight(string sheet, Seed seed, Mark mark)
{
    var opened = Check(app.Sheets.Open(sheet));
    var anchor = AnchorOf(opened, mark.Exact);
    var id = mark.Term is { } term
        ? Check(app.Annotations.CreateDefinition(sheet, anchor, new Definition(term, mark.Explanation ?? "")))
        : Check(app.Annotations.Create(sheet, anchor, mark.Comment));
    rhythms[id] = seed.Rhythm;
    marks++;
    if (mark.Trashed)
    {
        Check(app.Annotations.Trash(id));
    }
}

void Play(Act act, string sheet, Seed seed, DateOnly day)
{
    switch (act)
    {
        case Act.ArchiveByHand:
            Check(app.Reviews.Archive(sheet));
            break;
        case Act.Postpone:
            Check(app.Reviews.Postpone(ItemKind.Sheet, sheet, day.AddDays(3)));
            break;
        case Act.Restart:
            Check(app.Reviews.Restart(ItemKind.Sheet, sheet));
            break;
        case Act.Rewrite:
            var opened = Check(app.Sheets.Open(sheet));
            Check(app.Editing.KeepAsVersion(sheet, opened.Body));
            Check(app.Editing.Save(sheet, opened.Body.TrimEnd() + "\n\n" + seed.Addition + "\n", opened.Fingerprint, 1));
            break;
        default:
            Check(app.Trash.TrashSheet(sheet));
            break;
    }
}

void Answer(string sheet, ReminderOutcome answer)
{
    // « Certains points à revoir » : les passages marqués repartent de J0, puis le rappel est répondu.
    if (answer == ReminderOutcome.SomePointsToReview)
    {
        Check(app.Reviews.Restart(ItemKind.Passage, Check(app.Sheets.Open(sheet)).Annotations.First(annotation => !annotation.InTrash).Id));
    }
    Check(app.Reminders.Handle(sheet, answer));
}

static T Check<T>(Outcome<T> outcome) =>
    outcome.Succeeded ? outcome.Value : throw new InvalidOperationException(outcome.Error);

// L'ancre d'un passage, sur une projection simple du texte affiché : Markdown retiré, blocs de code
// écartés. Le texte exact et son contexte retrouvent le passage, comme à toute réouverture ; les
// positions n'en sont qu'un repère.
static Anchor AnchorOf(OpenedSheet sheet, string exact)
{
    var displayed = new StringBuilder();
    var section = new List<string>();
    var levels = new List<int>();
    var fenced = false;
    Anchor? found = null;
    var lines = sheet.Body.Split('\n');
    var positions = new List<(int Start, int End, int Line, ImmutableArray<string> Section)>();
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
        var heading = Regex.Match(line, "^(#{1,6}) (.+)$");
        var plain = Regex.Replace(line, @"^(#{1,6} |[-*] |> |\d+\. )", "").Replace("**", "", StringComparison.Ordinal).Replace("`", "", StringComparison.Ordinal);
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
        if (at >= 0 && positions.Count == 0)
        {
            positions.Add((displayed.Length + at, displayed.Length + at + exact.Length, index, [.. section]));
        }
        displayed.Append(plain).Append('\n');
    }
    if (positions.Count == 1)
    {
        var (start, end, line, path) = positions[0];
        var text = displayed.ToString();
        found = new Anchor(
            exact,
            text[Math.Max(0, start - 32)..start],
            text[end..Math.Min(text.Length, end + 32)],
            start,
            end,
            path,
            sheet.FirstBodyLine + line,
            sheet.Fingerprint);
    }
    return found ?? throw new InvalidOperationException($"« {exact} » est introuvable dans « {sheet.Title} ».");
}

/// <summary>Une horloge qui suit le jour simulé, à l'heure locale du poste.</summary>
internal sealed class SeedClock : TimeProvider
{
    private DateTimeOffset _now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;

    // Le soir pour les jours passés ; l'heure réelle pour le dernier, qui n'est pas fini.
    public void Set(DateOnly day, DateOnly today)
    {
        if (day == today)
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
internal sealed class NoProfile : IVaultLocations
{
    public AppMode Mode => AppMode.Portable;

    public string? VaultToOpen() => null;

    public string? VaultToCreate() => null;

    public bool Remember(string root) => true;
}

internal sealed record Planted(string Id, Seed Seed);

/// <summary>Quand ses révisions sont faites : avec <see cref="Late"/> jours de retard, et plus du tout après <see cref="StopAfter"/>.</summary>
internal sealed record Rhythm(int Late = 0, int? StopAfter = null)
{
    public static Rhythm OnTime { get; } = new();

    public static Rhythm Stops(int after) => new(StopAfter: after);
}

internal enum Act
{
    ArchiveByHand,
    Postpone,
    Restart,
    Rewrite,
    Trash,
}

internal sealed record Scripted(int After, Act Act);

/// <summary>Un passage surligné, ou une définition si <see cref="Term"/> est donné, <see cref="After"/> jours après la création de la fiche.</summary>
internal sealed record Mark(string Exact, string? Comment = null, string? Term = null, string? Explanation = null, int After = 0, bool Trashed = false);

/// <summary>
/// Une fiche créée il y a <see cref="Age"/> jours. Révisée à temps, elle est attendue 1, 4, 11, 25 et
/// 55 jours après sa création, puis archivée ; un rappel la tire deux mois plus tard.
/// </summary>
internal sealed record Seed(string Folder, string Title, int Age, Rhythm Rhythm, string Body)
{
    public ImmutableArray<string> Tags { get; init; } = [];

    public ImmutableArray<Mark> Marks { get; init; } = [];

    public ImmutableArray<Scripted> Later { get; init; } = [];

    public ReminderOutcome? Answer { get; init; }

    public string? Addition { get; init; }
}

/// <summary>Les schémas réseau et les images, tous inventés.</summary>
internal static class Network
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    public static readonly ImmutableArray<(string Key, string Text)> Schemas =
    [
        ("maison", Home()),
        ("pme", Company()),
        ("sites", TwoSites()),
    ];

    public static readonly ImmutableDictionary<string, string> Images = new Dictionary<string, string>
    {
        ["cellule"] = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 320 200" role="img">
              <title>Une cellule eucaryote : la membrane, le cytoplasme, le noyau et deux mitochondries</title>
              <ellipse cx="160" cy="100" rx="150" ry="90" fill="#EAF2EC" stroke="#234F3B" stroke-width="4"/>
              <circle cx="150" cy="95" r="38" fill="#C7DCCB" stroke="#234F3B" stroke-width="3"/>
              <circle cx="158" cy="88" r="11" fill="#234F3B"/>
              <ellipse cx="245" cy="70" rx="28" ry="13" fill="#E3A63F" stroke="#7A5513" stroke-width="2"/>
              <ellipse cx="235" cy="135" rx="24" ry="12" fill="#E3A63F" stroke="#7A5513" stroke-width="2"/>
              <text x="150" y="150" font-family="Segoe UI" font-size="14" text-anchor="middle" fill="#234F3B">noyau</text>
              <text x="245" y="50" font-family="Segoe UI" font-size="14" text-anchor="middle" fill="#7A5513">mitochondrie</text>
            </svg>
            """,
        ["cycle"] = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 240 240" role="img">
              <title>Le cycle cellulaire : les phases G1, S et G2 de l'interphase, puis la mitose</title>
              <circle cx="120" cy="120" r="90" fill="none" stroke="#C7DCCB" stroke-width="28"/>
              <path d="M120 30 A90 90 0 0 1 203 154" fill="none" stroke="#234F3B" stroke-width="28"/>
              <path d="M203 154 A90 90 0 0 1 75 198" fill="none" stroke="#357A57" stroke-width="28"/>
              <path d="M75 198 A90 90 0 0 1 48 66" fill="none" stroke="#7FAF8E" stroke-width="28"/>
              <path d="M48 66 A90 90 0 0 1 120 30" fill="none" stroke="#E3A63F" stroke-width="28"/>
              <text x="120" y="126" font-family="Segoe UI" font-size="15" text-anchor="middle" fill="#234F3B">G1 · S · G2 · M</text>
            </svg>
            """,
    }.ToImmutableDictionary();

    private static string Home() => Text(new
    {
        format = 2,
        id = "",
        titre = "Réseau de la colocation",
        description = "Une box, un commutateur et une borne Wi-Fi pour quatre personnes.",
        modeles = Array.Empty<object>(),
        equipements = new[]
        {
            Device("d_box", "routeur", "Box", 80, 80, "Passerelle", Port("i_box_wan", "WAN", "fibre", 1000, "wan"), Port("i_box_lan", "LAN1", "cuivre", 1000, "lan", "192.168.1.1", 24)),
            Device("d_sw", "switch", "Commutateur du salon", 320, 80, "Accès", Port("i_sw_1", "1", "cuivre", 1000, "uplink"), Port("i_sw_2", "2", "cuivre", 1000, "acces"), Port("i_sw_3", "3", "cuivre", 1000, "acces")),
            Device("d_ap", "point-acces", "Borne Wi-Fi", 560, 40, "Wi-Fi", Port("i_ap_eth", "eth0", "cuivre", 1000, "uplink", "192.168.1.2", 24)),
            Device("d_nas", "serveur", "NAS des photos", 560, 180, "Stockage", Port("i_nas_eth", "eth0", "cuivre", 1000, "acces", "192.168.1.10", 24)),
        },
        liens = new[]
        {
            Cable("l_1", "d_box", "i_box_lan", "d_sw", "i_sw_1", "cuivre", 1000),
            Cable("l_2", "d_sw", "i_sw_2", "d_ap", "i_ap_eth", "cuivre", 1000),
            Cable("l_3", "d_sw", "i_sw_3", "d_nas", "i_nas_eth", "cuivre", 1000),
        },
        relations = Array.Empty<object>(),
        vlans = Array.Empty<object>(),
        agregats = Array.Empty<object>(),
        groupes = Array.Empty<object>(),
        annotations = new[] { Note("n_1", "Plan d'adressage : 192.168.1.0/24", 80, 240) },
        vue = "physique",
    });

    private static string Company() => Text(new
    {
        format = 2,
        id = "",
        titre = "Réseau de la PME Lumière",
        description = "Trois VLAN séparés par le pare-feu : bureaux, serveurs et invités.",
        modeles = Array.Empty<object>(),
        equipements = new[]
        {
            Device("d_fw", "pare-feu", "Pare-feu", 80, 200, "Sécurité", Port("i_fw_wan", "WAN", "fibre", 1000, "wan"), Trunk(Port("i_fw_lan", "LAN", "cuivre", 10000, "lan", "10.0.10.1", 24), "10,20,30")),
            Device("d_core", "switch", "Commutateur de cœur", 320, 200, "Cœur", Trunk(Port("i_core_1", "1", "cuivre", 10000, "uplink"), "10,20,30"), Trunk(Port("i_core_2", "2", "fibre", 10000, "uplink"), "10,30"), Access(Port("i_core_3", "3", "cuivre", 1000, "acces"), 20), Access(Port("i_core_4", "4", "cuivre", 1000, "acces"), 20)),
            Device("d_acc", "switch", "Commutateur des bureaux", 560, 80, "Accès", Trunk(Port("i_acc_1", "1", "fibre", 10000, "uplink"), "10,30"), Access(Port("i_acc_2", "2", "cuivre", 1000, "acces"), 10), Trunk(Port("i_acc_3", "3", "cuivre", 1000, "acces"), "10,30")),
            Device("d_ap", "point-acces", "Borne des invités", 800, 40, "Wi-Fi", Trunk(Port("i_ap_eth", "eth0", "cuivre", 1000, "uplink"), "10,30")),
            Device("d_pc", "pc", "Poste de la comptabilité", 800, 160, "Bureau", Port("i_pc_eth", "eth0", "cuivre", 1000, "acces", "10.0.10.42", 24)),
            Device("d_web", "serveur", "Serveur web", 560, 300, "Intranet", Port("i_web_eth", "eth0", "cuivre", 1000, "acces", "10.0.20.10", 24)),
            Device("d_bdd", "serveur", "Base de données", 560, 400, "Comptabilité", Port("i_bdd_eth", "eth0", "cuivre", 1000, "acces", "10.0.20.11", 24)),
        },
        liens = new[]
        {
            Cable("l_1", "d_fw", "i_fw_lan", "d_core", "i_core_1", "cuivre", 10000),
            Cable("l_2", "d_core", "i_core_2", "d_acc", "i_acc_1", "fibre-multimode", 10000),
            Cable("l_3", "d_core", "i_core_3", "d_web", "i_web_eth", "cuivre", 1000),
            Cable("l_4", "d_core", "i_core_4", "d_bdd", "i_bdd_eth", "cuivre", 1000),
            Cable("l_5", "d_acc", "i_acc_2", "d_pc", "i_pc_eth", "cuivre", 1000),
            Cable("l_6", "d_acc", "i_acc_3", "d_ap", "i_ap_eth", "cuivre", 1000),
        },
        relations = Array.Empty<object>(),
        vlans = new[]
        {
            new { id = "v_10", numero = 10, nom = "Bureaux", couleur = "#357A57", sousReseaux = ImmutableArray.Create("10.0.10.0/24") },
            new { id = "v_20", numero = 20, nom = "Serveurs", couleur = "#1F62B0", sousReseaux = ImmutableArray.Create("10.0.20.0/24") },
            new { id = "v_30", numero = 30, nom = "Invités", couleur = "#A35A00", sousReseaux = ImmutableArray.Create("10.0.30.0/24") },
        },
        agregats = Array.Empty<object>(),
        groupes = new[]
        {
            new { id = "g_salle", nom = "Salle serveurs", genre = "salle", membres = ImmutableArray.Create("d_web", "d_bdd"), position = new { x = 520, y = 270 }, taille = new { largeur = 240, hauteur = 220 } },
        },
        annotations = new[] { Note("n_1", "Les invités n'atteignent ni les bureaux ni les serveurs : le pare-feu refuse.", 80, 360) },
        vue = "physique",
    });

    private static string TwoSites() => Text(new
    {
        format = 2,
        id = "",
        titre = "Deux sites reliés par un tunnel",
        description = "Le routage statique de deux agences à travers un opérateur.",
        modeles = Array.Empty<object>(),
        equipements = new[]
        {
            Device("d_r1", "routeur", "Routeur de Lyon", 120, 120, "Agence", Port("i_r1_wan", "wan0", "fibre", 1000, "wan", "203.0.113.2", 30), Port("i_r1_lan", "lan0", "cuivre", 1000, "lan", "10.1.0.1", 24)),
            Device("d_r2", "routeur", "Routeur de Nantes", 520, 120, "Agence", Port("i_r2_wan", "wan0", "fibre", 1000, "wan", "203.0.113.6", 30), Port("i_r2_lan", "lan0", "cuivre", 1000, "lan", "10.2.0.1", 24)),
            Device("d_p1", "pc", "Poste de Lyon", 120, 300, "Bureau", Port("i_p1", "eth0", "cuivre", 1000, "acces", "10.1.0.20", 24)),
            Device("d_p2", "pc", "Poste de Nantes", 520, 300, "Bureau", Port("i_p2", "eth0", "cuivre", 1000, "acces", "10.2.0.20", 24)),
        },
        liens = new[]
        {
            Cable("l_1", "d_r1", "i_r1_wan", "d_r2", "i_r2_wan", "operateur", 1000),
            Cable("l_2", "d_r1", "i_r1_lan", "d_p1", "i_p1", "cuivre", 1000),
            Cable("l_3", "d_r2", "i_r2_lan", "d_p2", "i_p2", "cuivre", 1000),
        },
        relations = new[]
        {
            new { id = "r_1", genre = "tunnel", de = "d_r1", vers = "d_r2", nom = "Tunnel IPsec", statut = "actif", proprietes = new { }, points = Array.Empty<object>() },
        },
        vlans = Array.Empty<object>(),
        agregats = Array.Empty<object>(),
        groupes = Array.Empty<object>(),
        annotations = Array.Empty<object>(),
        vue = "logique",
    });

    private static string Text(object schema) => JsonSerializer.Serialize(schema, Options);

    private static PortData Port(string id, string name, string genre, int speed, string role, string? address = null, int? prefix = null) =>
        new(id, name, genre, "physique", speed, role, "actif", [], address is null ? [] : [new AddressData(address, prefix)], null);

    private static PortData Access(PortData port, int vlan) => port with { Vlan = new VlanData("acces", vlan, null) };

    private static PortData Trunk(PortData port, string tagged) => port with { Vlan = new VlanData("trunk", null, tagged) };

    private static object Device(string id, string type, string name, int x, int y, string role, params PortData[] ports) => new
    {
        id,
        type,
        nom = name,
        role,
        statut = "actif",
        tags = Array.Empty<string>(),
        proprietes = new { },
        membres = Array.Empty<object>(),
        interfaces = ports.Select(port => new
        {
            id = port.Id,
            nom = port.Name,
            genre = port.Genre,
            nature = port.Nature,
            vitesse = port.Speed,
            role = port.Role,
            admin = port.Admin,
            macs = port.Macs,
            adresses = port.Addresses.Select(address => new { adresse = address.Address, prefixe = address.Prefix }),
            vlan = port.Vlan is { } vlan ? new { mode = vlan.Mode, acces = vlan.Access, tagues = vlan.Tagged } : null,
        }),
        position = new { x, y },
    };

    private static object Cable(string id, string a, string aPort, string b, string bPort, string support, int speed) => new
    {
        id,
        a = new { equipement = a, @interface = aPort },
        b = new { equipement = b, @interface = bPort },
        support,
        debit = speed,
        statut = "actif",
        points = Array.Empty<object>(),
    };

    private static object Note(string id, string text, int x, int y) => new { id, texte = text, position = new { x, y }, forme = "note" };

    private sealed record AddressData(string Address, int? Prefix);

    private sealed record VlanData(string Mode, int? Access, string? Tagged);

    private sealed record PortData(string Id, string Name, string Genre, string Nature, int Speed, string Role, string Admin, string[] Macs, AddressData[] Addresses, VlanData? Vlan);
}

/// <summary>Les fiches et les citations. Leurs dates font un coffre varié, quel que soit le jour où il est créé.</summary>
internal static class Content
{
    public static readonly ImmutableArray<QuoteContent> Quotes =
    [
        new("Je pense, donc je suis.", "René Descartes", "1637", QuoteSource.None, "Discours de la méthode", ["philosophie"]),
        new("Le cœur a ses raisons que la raison ne connaît point.", "Blaise Pascal", "1670", QuoteSource.None, "Pensées", ["philosophie"]),
        new("Science sans conscience n'est que ruine de l'âme.", "François Rabelais", "1532", QuoteSource.None, "Pantagruel", ["études"]),
        new("Ce que l'on conçoit bien s'énonce clairement.", "Nicolas Boileau", "1674", QuoteSource.None, "L'Art poétique", ["méthode"]),
        new("Il faut cultiver notre jardin.", "Voltaire", "1759", QuoteSource.None, "Candide", ["philosophie"]),
        new("Que sais-je ?", "Michel de Montaigne", "1580", QuoteSource.None, "Essais", ["philosophie"]),
    ];

    public static readonly ImmutableArray<Seed> Seeds =
    [
        // Histoire : un partiel approche, plusieurs retards.
        new("Histoire", "La Révolution française : les causes", 4, Rhythm.OnTime, """
            # La Révolution française : les causes

            ## Une société d'ordres

            En 1789, la société d'Ancien Régime est divisée en trois ordres : le **clergé**, la **noblesse** et le **tiers état**.
            Seul le tiers état paie l'essentiel des impôts, alors qu'il réunit près de 98 % de la population.

            | Ordre | Part de la population | Privilèges |
            |---|---|---|
            | Clergé | environ 0,5 % | exemption d'impôts, dîme |
            | Noblesse | environ 1,5 % | droits seigneuriaux |
            | Tiers état | environ 98 % | aucun |

            ## La crise financière

            Les guerres ont vidé le Trésor : le remboursement de la dette absorbe près de la moitié des dépenses en 1788.
            Louis XVI convoque les États généraux pour faire accepter de nouveaux impôts.

            ```mermaid
            flowchart LR
              A[Crise financière] --> B[États généraux]
              B --> C[Serment du Jeu de paume]
              C --> D[Prise de la Bastille]
            ```

            ## Les idées nouvelles

            Les Lumières critiquent l'absolutisme : Montesquieu défend la **séparation des pouvoirs**, Rousseau la souveraineté du peuple.

            > Qu'est-ce que le tiers état ? Tout. Qu'a-t-il été jusqu'à présent ? Rien.

            ```quiz
            Q: Qui convoque les États généraux en 1789 ?
            R: Louis XVI
            Q: Combien d'ordres compte la société d'Ancien Régime ?
            R: Trois : le clergé, la noblesse et le tiers état
            ```
            """)
        {
            Tags = ["histoire", "partiel"],
            Marks =
            [
                new("trois ordres", "Qui paie l'impôt, et pourquoi ?"),
                new("tiers état", Term: "Tiers état", Explanation: "Ordre qui regroupe tous ceux qui ne sont ni nobles ni membres du clergé : paysans, artisans, bourgeois."),
                new("États généraux", Term: "États généraux", Explanation: "Assemblée des représentants des trois ordres, convoquée par le roi. Elle ne s'était plus réunie depuis 1614.", After: 1),
                new("séparation des pouvoirs", "Montesquieu, De l'esprit des lois (1748)", After: 2),
            ],
        },
        new("Histoire", "Napoléon et l'Empire", 13, Rhythm.Stops(2), """
            # Napoléon et l'Empire

            ## L'ascension

            Général victorieux en Italie, Napoléon Bonaparte prend le pouvoir par le coup d'État du 18 Brumaire (9 novembre 1799).
            Il devient Premier consul, puis se fait sacrer empereur le 2 décembre 1804.

            ## L'œuvre intérieure

            - le **Code civil** de 1804, qui unifie le droit français ;
            - les préfets, qui représentent l'État dans chaque département ;
            - le franc germinal, monnaie stable ;
            - les lycées, pour former les cadres de la nation.

            ## La chute

            La campagne de Russie de 1812 décime la Grande Armée. Battu à Leipzig puis à Waterloo le 18 juin 1815, Napoléon meurt en exil à Sainte-Hélène en 1821.
            """)
        {
            Tags = ["histoire", "partiel"],
            Marks =
            [
                new("Code civil", Term: "Code civil", Explanation: "Recueil de lois de 1804 qui fixe le droit des personnes, des biens et des contrats, et consacre l'égalité devant la loi."),
                new("18 Brumaire", "Date à retenir : 9 novembre 1799"),
            ],
        },
        new("Histoire", "La Première Guerre mondiale", 11, Rhythm.OnTime, """
            # La Première Guerre mondiale

            ## Du conflit local à la guerre mondiale

            L'attentat de Sarajevo, le 28 juin 1914, déclenche le jeu des alliances entre la Triple-Entente et la Triple-Alliance.

            ## Une guerre totale

            Après l'échec de la guerre de mouvement, le front se fige dans les tranchées. La bataille de Verdun, en 1916, dure dix mois.
            Les sociétés entières sont mobilisées : les usines tournent pour l'armée et les femmes remplacent les hommes partis au front.

            ## Sortir de la guerre

            L'armistice est signé le 11 novembre 1918. Le traité de Versailles, en 1919, impose de lourdes réparations à l'Allemagne.
            """)
        {
            Tags = ["histoire", "partiel"],
            Marks =
            [
                new("guerre totale", Term: "Guerre totale", Explanation: "Conflit qui mobilise toutes les ressources d'un pays : soldats, économie, civils et propagande."),
                new("Verdun", "Dix mois de combats, de février à décembre 1916"),
            ],
        },
        new("Histoire", "La décolonisation", 28, Rhythm.Stops(3), """
            # La décolonisation

            ## Les causes

            La Seconde Guerre mondiale affaiblit les puissances coloniales, tandis que l'ONU proclame le droit des peuples à disposer d'eux-mêmes.

            ## Deux voies

            L'Inde obtient son indépendance en 1947 par la négociation et la résistance non violente de Gandhi.
            L'Algérie l'obtient en 1962, après huit ans de guerre et les accords d'Évian.

            ## Le tiers-monde

            La conférence de Bandung, en 1955, affirme la place des pays nouvellement indépendants sur la scène internationale.
            """)
        {
            Tags = ["histoire"],
            Marks = [new("accords d'Évian", "Signés le 18 mars 1962", After: 4)],
        },
        new("Histoire", "La Guerre froide", 122, Rhythm.OnTime, """
            # La Guerre froide

            ## Deux blocs

            De 1947 à 1991, les États-Unis et l'URSS s'opposent sans s'affronter directement.
            La doctrine Truman et le plan Marshall répondent à l'expansion du communisme en Europe.

            ## Les crises

            - le blocus de Berlin, en 1948 ;
            - la guerre de Corée, de 1950 à 1953 ;
            - la crise de Cuba, en octobre 1962.

            ## La fin

            La chute du mur de Berlin, le 9 novembre 1989, annonce la dislocation de l'URSS en décembre 1991.
            """)
        {
            Tags = ["histoire"],
            Marks =
            [
                new("doctrine Truman", Term: "Doctrine Truman", Explanation: "Politique américaine de 1947 : aider les pays menacés par le communisme, pour contenir son expansion."),
            ],
        },

        // Biologie : des images, et les trois réponses possibles à un rappel.
        new("Biologie", "La cellule eucaryote", 1, Rhythm.OnTime, """
            # La cellule eucaryote

            {{image:cellule|Une cellule eucaryote}}

            ## Ses composants

            La cellule eucaryote possède un **noyau** délimité par une membrane, qui renferme l'ADN.
            Les mitochondries produisent l'énergie sous forme d'ATP.
            Le réticulum endoplasmique et l'appareil de Golgi fabriquent et transportent les protéines.

            ## Eucaryote ou procaryote ?

            | | Eucaryote | Procaryote |
            |---|---|---|
            | Noyau | oui | non |
            | Taille | 10 à 100 µm | 1 à 10 µm |
            | Exemples | animaux, plantes | bactéries |
            """)
        {
            Tags = ["biologie"],
            Marks =
            [
                new("mitochondries", Term: "Mitochondrie", Explanation: "Organite de la cellule où la respiration produit l'ATP, la réserve d'énergie immédiatement utilisable."),
            ],
        },
        new("Biologie", "Mitose et méiose", 150, Rhythm.OnTime, """
            # Mitose et méiose

            {{image:cycle|Le cycle cellulaire}}

            ## La mitose

            La mitose donne **deux cellules filles** identiques à la cellule mère. Elle assure la croissance et le renouvellement des tissus.
            Ses quatre phases sont la prophase, la métaphase, l'anaphase et la télophase.

            ## La méiose

            La méiose produit quatre cellules haploïdes : les gamètes. Le brassage génétique y rend chaque gamète unique.
            """)
        {
            Tags = ["biologie", "génétique"],
            Marks = [new("quatre cellules haploïdes", "n chromosomes au lieu de 2n")],
            Answer = ReminderOutcome.Known,
        },
        new("Biologie", "L'ADN et la synthèse des protéines", 145, Rhythm.OnTime, """
            # L'ADN et la synthèse des protéines

            ## La structure de l'ADN

            L'ADN est une double hélice de nucléotides. Les bases s'associent toujours par paires : adénine avec thymine, guanine avec cytosine.

            ## De l'ADN à la protéine

            1. La transcription copie un gène en ARN messager, dans le noyau.
            2. La traduction lit l'ARN messager dans le cytoplasme, codon par codon.
            3. Le ribosome assemble les acides aminés en protéine.
            """)
        {
            Tags = ["biologie", "génétique"],
            Marks =
            [
                new("double hélice", "Watson et Crick, 1953"),
                new("transcription", Term: "Transcription", Explanation: "Copie d'un gène de l'ADN en ARN messager, dans le noyau."),
            ],
            Answer = ReminderOutcome.SomePointsToReview,
        },
        new("Biologie", "La photosynthèse", 10, Rhythm.OnTime, """
            # La photosynthèse

            ## Le bilan

            Les plantes fabriquent leur matière à partir de la lumière, de l'eau et du dioxyde de carbone.

            $$6\,CO_2 + 6\,H_2O \rightarrow C_6H_{12}O_6 + 6\,O_2$$

            ## Où et comment

            Elle a lieu dans les chloroplastes, grâce à la chlorophylle, qui capte surtout le rouge et le bleu de la lumière.
            """)
        {
            Tags = ["biologie"],
            Marks = [new("chloroplastes", Term: "Chloroplaste", Explanation: "Organite des cellules végétales où se déroule la photosynthèse.")],
        },
        new("Biologie", "Le système immunitaire", 135, Rhythm.OnTime, """
            # Le système immunitaire

            ## L'immunité innée

            Rapide et sans mémoire, elle s'appuie sur la barrière de la peau, l'inflammation et la phagocytose.

            ## L'immunité adaptative

            Plus lente, elle est spécifique d'un antigène. Les lymphocytes B produisent des anticorps, les lymphocytes T détruisent les cellules infectées.
            La **mémoire immunitaire** explique l'efficacité de la vaccination.
            """)
        {
            Tags = ["biologie"],
            Marks = [new("phagocytose", Term: "Phagocytose", Explanation: "Une cellule immunitaire englobe puis digère un élément étranger.")],
            Answer = ReminderOutcome.ReviewEntirely,
        },

        // Réseaux : les fiches qui citent des schémas.
        new("Réseaux", "Le modèle OSI", 21, Rhythm.OnTime, """
            # Le modèle OSI

            ## Les sept couches

            | N° | Couche | Unité | Exemple |
            |---|---|---|---|
            | 7 | Application | donnée | HTTP, DNS |
            | 6 | Présentation | donnée | TLS |
            | 5 | Session | donnée | RPC |
            | 4 | Transport | segment | TCP, UDP |
            | 3 | Réseau | paquet | IP |
            | 2 | Liaison | trame | Ethernet |
            | 1 | Physique | bit | câble, fibre |

            ## L'encapsulation

            Chaque couche ajoute son en-tête aux données de la couche supérieure : c'est l'**encapsulation**.

            ## Un réseau simple

            {{schema:maison}}
            """)
        {
            Tags = ["réseaux", "certification"],
            Marks =
            [
                new("encapsulation", Term: "Encapsulation", Explanation: "Chaque couche ajoute son en-tête aux données reçues de la couche du dessus ; le destinataire les retire dans l'ordre inverse."),
                new("TCP, UDP", "TCP garantit la livraison, UDP non", After: 3),
            ],
        },
        new("Réseaux", "Adressage IPv4 et sous-réseaux", 16, new Rhythm(Late: 2), """
            # Adressage IPv4 et sous-réseaux

            ## Le masque

            Une adresse IPv4 compte 32 bits. Le masque sépare la partie réseau de la partie hôte : `/24` laisse 8 bits aux hôtes, soit 254 adresses utilisables.

            ## Calculer un sous-réseau

            ```text
            Adresse   : 192.168.10.37/26
            Masque    : 255.255.255.192
            Réseau    : 192.168.10.0
            Diffusion : 192.168.10.63
            Hôtes     : 192.168.10.1 à 192.168.10.62
            ```

            ## Les plages privées

            - 10.0.0.0/8
            - 172.16.0.0/12
            - 192.168.0.0/16
            """)
        {
            Tags = ["réseaux", "certification"],
            Marks =
            [
                new("254 adresses utilisables", "2^8 − 2 : le réseau et la diffusion sont réservés"),
                new("masque", Term: "Masque de sous-réseau", Explanation: "Suite de bits qui indique quelle partie d'une adresse IP désigne le réseau.", After: 2),
            ],
        },
        new("Réseaux", "Les VLAN", 11, Rhythm.OnTime, """
            # Les VLAN

            ## À quoi ça sert

            Un VLAN sépare logiquement un même commutateur en plusieurs réseaux. Les équipements de VLAN différents ne se parlent qu'en passant par un routeur ou un pare-feu.

            ## Accès ou trunk

            Un port d'**accès** appartient à un seul VLAN. Un port **trunk** transporte plusieurs VLAN, marqués par la norme 802.1Q.

            ## Un exemple

            {{schema:pme}}
            """)
        {
            Tags = ["réseaux", "certification"],
            Marks =
            [
                new("802.1Q", Term: "802.1Q", Explanation: "Norme qui ajoute à une trame Ethernet une étiquette indiquant son VLAN."),
                new("par un routeur ou un pare-feu", "Sans routage, deux VLAN restent isolés"),
            ],
        },
        new("Réseaux", "Routage et pare-feu", 52, Rhythm.OnTime, """
            # Routage et pare-feu

            ## La table de routage

            Pour chaque paquet, le routeur retient la route **la plus précise** qui correspond à sa destination, sinon la route par défaut.

            ## Le filtrage

            Un pare-feu applique ses règles dans l'ordre, et tout ce qui n'est pas autorisé est refusé.

            ## Relier deux sites

            {{schema:sites}}

            Le tunnel chiffre le trafic entre les agences à travers le réseau de l'opérateur.
            """)
        {
            Tags = ["réseaux"],
            Marks = [new("la route par défaut", "0.0.0.0/0")],
        },

        // Droit : fin de cycle, archivage à la main, nouvelle version.
        new("Droit", "Le contrat : conditions de validité", 55, Rhythm.OnTime, """
            # Le contrat : conditions de validité

            ## Les trois conditions

            Selon l'article 1128 du Code civil, un contrat valide suppose :

            1. le consentement des parties ;
            2. leur capacité de contracter ;
            3. un contenu licite et certain.

            ## Les vices du consentement

            L'erreur, le dol et la violence rendent le contrat annulable.
            """)
        {
            Tags = ["droit"],
            Marks =
            [
                new("dol", Term: "Dol", Explanation: "Manœuvre destinée à tromper le cocontractant pour obtenir son consentement."),
                new("article 1128", "Depuis la réforme de 2016"),
            ],
        },
        new("Droit", "La responsabilité civile", 30, Rhythm.OnTime, """
            # La responsabilité civile

            ## Les trois éléments

            Pour obtenir réparation, la victime prouve un **fait générateur**, un dommage et un lien de causalité.

            ## Les deux régimes

            - la responsabilité contractuelle, quand un contrat n'est pas exécuté ;
            - la responsabilité délictuelle, en dehors de tout contrat.
            """)
        {
            Tags = ["droit"],
            Marks = [new("lien de causalité", "Le dommage doit découler du fait générateur")],
            Later = [new Scripted(4, Act.Rewrite)],
            Addition = """
                ## Un exemple

                Un cycliste renverse un piéton : la faute est le fait générateur, la blessure est le dommage, et la collision les relie.
                """,
        },
        new("Droit", "Les sources du droit", 60, Rhythm.OnTime, """
            # Les sources du droit

            ## La hiérarchie des normes

            La Constitution prime sur les traités, les traités sur la loi, et la loi sur le règlement.

            ## La jurisprudence

            Les décisions des juges précisent la manière d'appliquer les textes.
            """)
        {
            Tags = ["droit"],
            Later = [new Scripted(10, Act.ArchiveByHand)],
        },

        // Mathématiques : des formules.
        new("Mathématiques", "Dérivées usuelles", 8, Rhythm.OnTime, """
            # Dérivées usuelles

            ## Le tableau

            | $f(x)$ | $f'(x)$ |
            |---|---|
            | $x^n$ | $n\,x^{n-1}$ |
            | $\sqrt{x}$ | $\frac{1}{2\sqrt{x}}$ |
            | $e^x$ | $e^x$ |
            | $\ln x$ | $\frac{1}{x}$ |

            ## Les règles

            La dérivée d'un produit se calcule ainsi : $(uv)' = u'v + uv'$.
            Celle d'une composée suit la règle de la chaîne : $(g \circ f)' = (g' \circ f) \times f'$.
            """)
        {
            Tags = ["maths"],
            Marks = [new("règle de la chaîne", Term: "Règle de la chaîne", Explanation: "Pour dériver une composée : la dérivée de l'extérieur, en l'intérieur, fois la dérivée de l'intérieur.")],
        },
        new("Mathématiques", "Probabilités conditionnelles", 0, Rhythm.OnTime, """
            # Probabilités conditionnelles

            ## La définition

            La probabilité de A sachant B vaut $P_B(A) = \frac{P(A \cap B)}{P(B)}$, pour B de probabilité non nulle.

            ## L'indépendance

            A et B sont **indépendants** lorsque $P(A \cap B) = P(A) \times P(B)$.
            """)
        {
            Tags = ["maths"],
            Marks = [new("indépendants", "Ne pas confondre avec incompatibles")],
        },
        new("Mathématiques", "Thalès et Pythagore", 128, Rhythm.OnTime, """
            # Thalès et Pythagore

            ## Pythagore

            Dans un triangle rectangle, le carré de l'hypoténuse est égal à la somme des carrés des deux autres côtés : $a^2 + b^2 = c^2$.

            ## Thalès

            Deux droites parallèles coupées par deux sécantes déterminent des longueurs proportionnelles.
            """)
        {
            Tags = ["maths"],
            Marks = [new("hypoténuse", Term: "Hypoténuse", Explanation: "Le côté opposé à l'angle droit, le plus long d'un triangle rectangle.")],
        },

        // Anglais : des retards d'un jour ou deux.
        new("Anglais", "Verbes irréguliers", 6, new Rhythm(Late: 1), """
            # Verbes irréguliers

            ## À savoir par cœur

            | Base verbale | Prétérit | Participe passé | Traduction |
            |---|---|---|---|
            | begin | began | begun | commencer |
            | bring | brought | brought | apporter |
            | choose | chose | chosen | choisir |
            | teach | taught | taught | enseigner |
            | write | wrote | written | écrire |

            ## Une astuce

            Regroupez les verbes par sonorité : *bring*, *buy* et *think* donnent tous un prétérit en *-ought*.
            """)
        {
            Tags = ["anglais"],
            Marks = [new("Regroupez les verbes par sonorité", "À appliquer aux 50 verbes de la liste")],
        },
        new("Anglais", "Les temps du passé", 3, Rhythm.OnTime, """
            # Les temps du passé

            ## Prétérit

            Il décrit une action terminée à un moment précis : *I saw her yesterday.*

            ## Present perfect

            Il relie le passé au présent : *I have lost my keys* (je ne les ai toujours pas).
            """)
        {
            Tags = ["anglais"],
            Marks = [new("relie le passé au présent", "Pas de date précise avec ce temps")],
        },

        // Philosophie : une révision repoussée, un cycle recommencé.
        new("Philosophie", "La conscience", 11, Rhythm.OnTime, """
            # La conscience

            ## Le cogito

            Descartes fait de la conscience le point de départ de toute certitude : même si je doute de tout, je ne peux pas douter que je pense.

            ## Les limites

            Freud montre qu'une partie de notre vie psychique nous échappe : c'est l'**inconscient**.
            """)
        {
            Tags = ["philosophie"],
            Marks = [new("inconscient", Term: "Inconscient", Explanation: "Chez Freud, part de la vie psychique qui échappe à la conscience et influence nos actes.")],
            Later = [new Scripted(10, Act.Postpone)],
        },
        new("Philosophie", "Liberté et déterminisme", 20, Rhythm.OnTime, """
            # Liberté et déterminisme

            ## Le déterminisme

            Spinoza voit dans la liberté une illusion : nous connaissons nos actes, mais nous ignorons les causes qui nous y poussent.

            ## La liberté comme choix

            Pour Sartre, au contraire, l'être humain est condamné à être libre, et donc responsable de ce qu'il fait de lui-même.
            """)
        {
            Tags = ["philosophie"],
            Later = [new Scripted(17, Act.Restart)],
        },

        // Hors dossier, et à la corbeille.
        new("", "À trier : idées pour le mémoire", 9, Rhythm.Stops(1), """
            # À trier : idées pour le mémoire

            - comparer la révision espacée et la relecture ;
            - interroger la promotion sur son rythme de travail ;
            - relire {{lien:Mitose et méiose|la fiche sur la mitose}} pour l'exemple de mémorisation.
            """),
        new("Histoire", "Brouillon à jeter", 8, Rhythm.OnTime, """
            # Brouillon à jeter

            Une fiche commencée puis abandonnée : elle attend à la corbeille, restaurable.
            """)
        {
            Later = [new Scripted(2, Act.Trash)],
        },
    ];
}
