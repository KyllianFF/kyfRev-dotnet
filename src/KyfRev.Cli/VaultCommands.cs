using System.CommandLine;
using KyfRev.Infrastructure.Vault.Analysis;

namespace KyfRev.Cli;

/// <summary>Commandes de <c>kyfrev-coffre</c>.</summary>
internal static class VaultCommands
{
    /// <summary>Aucune erreur (des avertissements peuvent exister).</summary>
    public const int Success = 0;

    /// <summary>Au moins un fichier illisible, en double ou d'un format plus récent.</summary>
    public const int ErrorsFound = 1;

    /// <summary>Le dossier n'est pas un coffre kyfRev.</summary>
    public const int NotAVault = 2;

    public static RootCommand Build(TextWriter output)
    {
        var vault = new Argument<DirectoryInfo>("coffre") { Description = "Dossier du coffre (celui qui contient .suivi/coffre.json)." };
        var json = new Option<bool>("--json") { Description = "Rapport au format JSON, pour un traitement automatique." };
        var verify = new Command("verifier", "Analyse le coffre en lecture seule : rien n'y est écrit ni verrouillé.") { vault, json };
        verify.SetAction(parseResult =>
        {
            var inspection = VaultInspector.Inspect(parseResult.GetValue(vault)!.FullName);
            output.Write(parseResult.GetValue(json) ? InspectionReport.ToJson(inspection) : InspectionReport.ToText(inspection));
            return !inspection.IsVault ? NotAVault : inspection.HasErrors ? ErrorsFound : Success;
        });

        return new RootCommand("Outil du coffre kyfRev.") { verify };
    }
}
