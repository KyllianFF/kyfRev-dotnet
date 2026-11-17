#:project ../../src/KyfRev.Infrastructure.Vault/KyfRev.Infrastructure.Vault.csproj
#:property ImplicitUsings=enable
#:property PublishAot=false

// Crée un coffre d'essai rempli de données inventées, pour essayer l'application sans toucher à un
// vrai coffre (voir tools/coffre-essai/README.md).
// Usage, à la racine du dépôt : dotnet run tools/coffre-essai/coffre-essai.cs -- <dossier absent ou vide>
// Le coffre vit plusieurs mois en accéléré, jusqu'à aujourd'hui : il a des retards, des révisions du
// jour, des archives, des rappels en attente et un historique.
using System.Text;
using KyfRev.Infrastructure.Vault.Samples;

Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
if (args.Length != 1)
{
    Console.Error.WriteLine("Usage : dotnet run tools/coffre-essai/coffre-essai.cs -- <dossier absent ou vide>");
    return 2;
}
var root = Path.GetFullPath(args[0]);
// Un coffre existant n'est jamais vide : il ne peut pas être écrasé. Le moteur et le contenu sont ceux du coffre d'exemple
// de l'application (KyfRev.Infrastructure.Vault.Samples), dans leur version complète.
var created = SampleVault.Create(root, DateOnly.FromDateTime(DateTime.Now), everything: true);
if (!created.Succeeded)
{
    Console.Error.WriteLine($"Refusé : {created.Error}");
    return 2;
}
var summary = created.Value;
Console.WriteLine($"Coffre d'essai créé : {root}");
Console.WriteLine($"  {summary.Sheets} fiches, {summary.Marks} passages et définitions, {summary.Schemas} schémas réseau, {summary.Quotes} citations");
return 0;
