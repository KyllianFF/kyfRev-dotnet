using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using KyfRev.Infrastructure.Vault.Analysis;

namespace KyfRev.Cli;

/// <summary>Mise en forme du rapport d'analyse d'un coffre.</summary>
internal static class InspectionReport
{
    public static string ToText(VaultInspection inspection)
    {
        var text = new StringBuilder();
        Line(text, $"Coffre : {inspection.Root}");
        if (inspection.IsVault)
        {
            var s = inspection.Summary;
            Line(text, $"Identifiant : {s.VaultId ?? "inconnu"}{(s.CreatedOn is { } created ? $" (créé le {created:yyyy-MM-dd})" : string.Empty)}");
            Line(text, $"Fiches : {s.TrackedSheets} suivie(s) présente(s) ({s.ActiveSheets} active(s), {s.ArchivedSheets} archivée(s) au total), {s.UntrackedFiles} non suivie(s), {s.TrashedSheets} à la corbeille");
            Line(text, $"Passages annotés : {s.Annotations}, dont {s.Definitions} définition(s) ; {s.TrashedAnnotations} à la corbeille");
            Line(text, $"Versions : {s.Versions} · Citations : {s.Quotes} · Schémas : {s.Schemas} · Données d'extension : {s.ExtensionData}");
            Line(text, $"Fichiers lus : {s.FilesRead}");
        }
        text.Append('\n');

        var problems = inspection.Findings.Where(f => f.Severity != FindingSeverity.Information).ToList();
        Line(text, problems.Count == 0 ? "Aucun problème." : $"{problems.Count} point(s) à examiner :");
        foreach (var finding in inspection.Findings.OrderByDescending(f => f.Severity))
        {
            var label = finding.Severity switch
            {
                FindingSeverity.Error => "erreur",
                FindingSeverity.Warning => "attention",
                _ => "info",
            };
            Line(text, $"  [{label}] {(finding.Path is null ? string.Empty : finding.Path + " : ")}{finding.Message}");
        }
        text.Append('\n');
        Line(text, "Analyse en lecture seule : rien n'a été écrit dans le coffre.");
        return text.ToString();
    }

    public static string ToJson(VaultInspection inspection)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, NewLine = "\n" }))
        {
            w.WriteStartObject();
            w.WriteString("coffre", inspection.Root);
            w.WriteBoolean("estUnCoffre", inspection.IsVault);
            w.WriteBoolean("erreurs", inspection.HasErrors);
            var s = inspection.Summary;
            w.WriteStartObject("resume");
            w.WriteString("identifiant", s.VaultId);
            w.WriteString("creeLe", s.CreatedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            w.WriteNumber("fichesSuivies", s.TrackedSheets);
            w.WriteNumber("fichesActives", s.ActiveSheets);
            w.WriteNumber("fichesArchivees", s.ArchivedSheets);
            w.WriteNumber("fichesNonSuivies", s.UntrackedFiles);
            w.WriteNumber("fichesALaCorbeille", s.TrashedSheets);
            w.WriteNumber("annotations", s.Annotations);
            w.WriteNumber("definitions", s.Definitions);
            w.WriteNumber("annotationsALaCorbeille", s.TrashedAnnotations);
            w.WriteNumber("versions", s.Versions);
            w.WriteNumber("citations", s.Quotes);
            w.WriteNumber("schemas", s.Schemas);
            w.WriteNumber("donneesExtension", s.ExtensionData);
            w.WriteNumber("fichiersLus", s.FilesRead);
            w.WriteEndObject();
            w.WriteStartArray("constats");
            foreach (var finding in inspection.Findings)
            {
                w.WriteStartObject();
                w.WriteString("gravite", finding.Severity switch
                {
                    FindingSeverity.Error => "erreur",
                    FindingSeverity.Warning => "attention",
                    _ => "info",
                });
                w.WriteString("code", finding.Code);
                w.WriteString("chemin", finding.Path);
                w.WriteString("message", finding.Message);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static void Line(StringBuilder text, string line) => text.Append(line).Append('\n');
}
