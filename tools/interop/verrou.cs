#:project ../../src/KyfRev.Infrastructure.Vault/KyfRev.Infrastructure.Vault.csproj

// Essai d'interopération du verrou du coffre avec kyfRev 0.x (voir tools/interop/verrou.ps1).
// Usage : dotnet run tools/interop/verrou.cs -- <coffre> <secondes>
//   prend le verrou et le garde <secondes> secondes, ou dit qui le tient déjà.
using System.Globalization;
using KyfRev.Infrastructure.Vault.Files;

var root = args[0];
var seconds = int.Parse(args[1], CultureInfo.InvariantCulture);
using var vaultLock = VaultLock.TryAcquire(root, DateTimeOffset.UtcNow, out var holder);
if (vaultLock is null)
{
    Console.WriteLine(holder is null
        ? "refus : coffre déjà ouvert"
        : $"refus : coffre déjà ouvert par le processus {holder.Process} sur « {holder.Machine} »");
    return 3;
}
Console.WriteLine($"ouvert : verrou tenu {seconds} s");
Thread.Sleep(TimeSpan.FromSeconds(seconds));
return 0;
