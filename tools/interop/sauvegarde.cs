#:project ../../src/KyfRev.Infrastructure.Vault/KyfRev.Infrastructure.Vault.csproj

// Contrôle croisé des sauvegardes avec kyfRev 0.x (voir tools/interop/sauvegardes.ps1).
// Usage : dotnet run tools/interop/sauvegarde.cs -- sauvegarder <coffre> <dossier>
//         dotnet run tools/interop/sauvegarde.cs -- restaurer <archive> <dossier>
// Écrit sur la sortie le chemin de l'archive créée ou du coffre restauré.
using System.Text;
using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Vault;
using KyfRev.Infrastructure.Vault.Backups;

Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
var day = new DateOnly(2026, 10, 31);
if (args[0] == "sauvegarder")
{
    var now = new Moment(day, new DateTimeOffset(day.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero));
    using var vault = VaultSession.Open(args[1], now);
    Console.WriteLine(vault.Backup(args[2], day.ToDateTime(new TimeOnly(12, 0))).Path);
    return 0;
}
try
{
    Console.WriteLine(VaultBackups.Restore(args[1], args[2], day));
    return 0;
}
catch (VaultException exception)
{
    Console.WriteLine($"refus : {exception.Message}");
    return 3;
}
