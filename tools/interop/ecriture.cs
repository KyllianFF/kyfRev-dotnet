#:project ../../src/KyfRev.Infrastructure.Vault/KyfRev.Infrastructure.Vault.csproj

// Essai d'interopération des écritures avec kyfRev 0.x (voir tools/interop/ecriture.ps1).
// Usage : dotnet run tools/interop/ecriture.cs -- <coffre> analyse|ecrire
//   analyse : ouvre le coffre et résume sa santé, au format de reference-vectors --sante ;
//   ecrire  : ouvre le coffre et y fait une série d'écritures (fiches, annotations, rappels,
//             réglages, citations, schémas, modèle) dans « Essai interop .NET ».
using System.Text;
using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Vault;
using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Quotes;

Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
var root = args[0];
var now = new Moment(DateOnly.FromDateTime(DateTime.UtcNow), DateTimeOffset.UtcNow);
using var vault = VaultSession.Open(root, now);
if (args[1] == "analyse")
{
    Console.WriteLine(Summary(vault.Health));
    Console.WriteLine($"problèmes : {vault.Health.Problems}");
    return 0;
}

const string folder = "Essai interop .NET";
var essay = vault.CreateSheet(folder, "Écrite par la génération .NET", "# Essai\n\nVoir [l'autre fiche](Autre.md).\n", now);
var other = vault.CreateSheet(folder, "Autre", "Retour vers [l'essai](<Écrite par la génération .NET.md>).\n", now);
var read = vault.ReadSheet(essay);
vault.SaveBody(essay, "# Essai\n\nCorps enregistré, puis [l'autre fiche](Autre.md).\n", read.Fingerprint, session: 1, now);
vault.SetTags(essay, ["interop", "essai"], now);
vault.AddResource(essay, "Schéma réseau.PNG", [0x89, 0x50, 0x4E, 0x47]);
vault.ChangeTitle(essay, "Essai : titre changé", now);
vault.CreateFolder($"{folder}/Sous-dossier");
vault.MoveSheet(other, $"{folder}/Sous-dossier", now);
vault.MoveFolder($"{folder}/Sous-dossier", $"{folder}/Déplacé", now);
vault.TrashSheet(other, now);
vault.RestoreSheet(other, now);

var anchor = new StoredAnchor("Corps enregistré", "", ", puis", 9, 25, ["Essai"], 3, vault.ReadSheet(essay).Fingerprint, UnknownProperties.None);
var passage = vault.CreateAnnotation(essay, anchor, "Écrit par .NET", now);
vault.CreateDefinition(essay, anchor with { Exact = "Essai", Start = 2, End = 7 }, new StoredDefinition("Essai", "Fiche d'essai."), now);
vault.ModifyComment(passage, "Commentaire modifié", now);
var tomorrow = new Moment(now.Day.AddDays(1), now.Instant.AddDays(1));
vault.ApplyAnnotationReview(passage, tracking => tracking.ValidateReview(tomorrow, null, null));
vault.PrepareDay(now);
vault.DefineExam(folder, "Examen d'essai", now.Day.AddDays(30));
vault.TightenBeforeExam(folder, now);
vault.SaveExtensionData("essai-interop", "{\"ok\": true}");
var quote = vault.AddQuote(new QuoteContent("Écrite par la génération .NET.", "kyfRev", "2026", QuoteSource.None, null, ["essai"]), now);
vault.ModifyQuote(quote.Path, quote.Content with { Context = "Essai d'interopération" }, now);
var schema = vault.CreateSchema("{\"format\": 2, \"titre\": \"Essai interop\", \"equipements\": [], \"liens\": []}");
vault.DuplicateSchema(schema.Id);
vault.SaveAsTemplate(essay, "Essai interop");
vault.Analyze(now);
Console.WriteLine($"écrit : {vault.Sheets.Count()} fiche(s), {vault.Health.Problems} problème(s)");
return 0;

// Même format que scenarios::rapport côté Rust : listes triées par octets UTF-8.
static string Summary(HealthReport health)
{
    static string Sorted(IEnumerable<string> items) =>
        string.Join(';', items.OrderBy(item => Encoding.UTF8.GetBytes(item), Comparer<byte[]>.Create((x, y) => x.AsSpan().SequenceCompareTo(y))));
    return $"deplacees=[{Sorted(health.Moved.Select(m => $"{m.Id} {m.From} {m.To}"))}]"
        + $" modifiees=[{Sorted(health.ModifiedOutside)}]"
        + $" manquantes=[{Sorted(health.Missing)}]"
        + $" doublons=[{Sorted(health.Duplicates.Select(d => $"{d.Id} {string.Join(',', d.Paths)}"))}]"
        + $" non_suivies=[{Sorted(health.Untracked.Select(p => p.Value))}]"
        + $" sans_suivi=[{Sorted(health.WithoutTracking.Select(w => $"{w.Id} {w.Path}"))}]"
        + $" illisibles=[{Sorted(health.Unreadable.Select(u => u.Path))}]"
        + $" liens=[{Sorted(health.LinksToCheck.Select(p => p.Value))}]"
        + $" orphelines=[{Sorted(health.OrphanAnnotations)}]";
}
