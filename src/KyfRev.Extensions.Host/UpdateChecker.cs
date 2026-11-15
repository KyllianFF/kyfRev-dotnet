using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using KyfRev.Application;
using KyfRev.Application.Ports;

namespace KyfRev.Extensions.Host;

/// <summary>Ce que dit la recherche de mise à jour.</summary>
/// <param name="Current">La version qui tourne.</param>
/// <param name="Latest">La dernière version publiée.</param>
/// <param name="IsNewer">La version publiée est plus récente que celle qui tourne.</param>
/// <param name="Page">La page de cette version, dans le dépôt de kyfRev.</param>
public sealed record UpdateStatus(string Current, string Latest, bool IsNewer, Uri Page)
{
    /// <summary>Son installateur, si l'annonce le décrit ; rien pour une annonce plus ancienne, qui ne propose que la page.</summary>
    public InstallerFile? Installer { get; init; }
}

/// <summary>L'installateur d'une version publiée, tel que l'annonce signée le décrit.</summary>
/// <param name="Name">Le nom du fichier : « kyfRev-1.2.0-installation.exe ».</param>
/// <param name="Address">Son adresse, déduite de la version : la version publiée de kyfRev, jamais l'annonce.</param>
/// <param name="Size">Sa taille exacte, en octets.</param>
/// <param name="Sha256">Son empreinte SHA-256, en hexadécimal minuscule.</param>
public sealed record InstallerFile(string Name, Uri Address, long Size, string Sha256);

/// <summary>Ce que dit une annonce vérifiée : la version publiée, et son installateur s'il est décrit.</summary>
/// <param name="Version">La version publiée.</param>
/// <param name="Installer">Son installateur ; rien si l'annonce ne le décrit pas.</param>
public sealed record UpdateAnnouncement(string Version, InstallerFile? Installer);

/// <summary>
/// La recherche de mise à jour, à la demande, ou au démarrage si on l'a choisi : elle lit dans le dépôt public un court
/// manifeste signé par une clé de publication connue. Il donne la version publiée et, s'il le décrit, la taille et
/// l'empreinte SHA-256 de son installateur ; la recherche elle-même ne télécharge rien. La page et l'installateur se
/// déduisent de la version signée : aucune adresse ne vient du manifeste.
/// </summary>
/// <param name="reader">Le lecteur en ligne de l'application.</param>
/// <param name="keys">Les clés publiques de publication connues.</param>
/// <param name="current">La version qui tourne.</param>
public sealed partial class UpdateChecker(IOnlineReader reader, IReadOnlyList<CatalogueKey> keys, string current)
{
    /// <summary>Le manifeste, à la racine du dépôt.</summary>
    public const string ManifestFile = "mise-a-jour/derniere.json";

    /// <summary>Sa signature, à la racine du dépôt.</summary>
    public const string SignatureFile = "mise-a-jour/derniere.sig";

    private const int MaxManifestBytes = 4096;

    /// <summary>Au-delà, un installateur annoncé n'est pas pris : celui de kyfRev pèse une soixantaine de mégaoctets.</summary>
    private const long MaxInstallerBytes = 1L << 30;

    /// <summary>Les pages des versions publiées : la seule destination possible.</summary>
    public static Uri Releases { get; } = new("https://github.com/KyllianFF/kyfRev-dotnet/releases/tag/");

    /// <summary>Les fichiers des versions publiées : le seul endroit d'où un installateur se télécharge.</summary>
    public static Uri Downloads { get; } = new("https://github.com/KyllianFF/kyfRev-dotnet/releases/download/");

    /// <summary>La version qui tourne.</summary>
    public string Current => current;

    /// <summary>Lit le manifeste, le vérifie et compare sa version à celle qui tourne.</summary>
    [ZeroXCommand("rechercher_mise_a_jour")]
    public async Task<Outcome<UpdateStatus>> CheckAsync(CancellationToken cancellationToken)
    {
        byte[] manifest;
        byte[] signature;
        try
        {
            manifest = await reader.ReadBytesAsync(new Uri(CatalogueReader.Repository, ManifestFile), cancellationToken).ConfigureAwait(false);
            signature = await reader.ReadBytesAsync(new Uri(CatalogueReader.Repository, SignatureFile), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationException exception)
        {
            return Outcome.Refused<UpdateStatus>($"La recherche n'a pas abouti ({exception.Message.TrimEnd('.')}).");
        }
        var latest = Verify(manifest, signature, keys);
        return latest.Succeeded
            ? Outcome.Of(new UpdateStatus(current, latest.Value.Version, IsNewer(latest.Value.Version, current), PageOf(latest.Value.Version))
            {
                Installer = latest.Value.Installer,
            })
            : Outcome.Refused<UpdateStatus>(latest.Error!);
    }

    /// <summary>La page de <paramref name="version"/>, dans le dépôt de kyfRev.</summary>
    public static Uri PageOf(string version) => new(Releases, $"v{version}");

    /// <summary>L'installateur de <paramref name="version"/>, à sa place dans la version publiée.</summary>
    /// <exception cref="ArgumentException">Ce n'est pas un numéro de version.</exception>
    public static InstallerFile InstallerOf(string version, long size, string sha256)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(sha256);
        RequireVersion(version);
        var name = $"kyfRev-{version}-installation.exe";
        return new InstallerFile(name, new Uri(Downloads, $"v{version}/{name}"), size, sha256);
    }

    /// <summary>
    /// Écrit un manifeste : la version publiée, sa date et, si on le donne, la taille et l'empreinte de son installateur.
    /// Il reste au format 1 : une version qui ne connaît pas l'installateur y lit toujours la version.
    /// </summary>
    /// <param name="version">La version publiée.</param>
    /// <param name="publishedOn">Le jour de l'annonce.</param>
    /// <param name="installer">Les octets exacts de l'installateur publié ; rien pour n'annoncer que la version.</param>
    /// <exception cref="ArgumentException">Ce n'est pas un numéro de version, ou l'installateur est vide.</exception>
    public static byte[] Write(string version, DateOnly publishedOn, byte[]? installer = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(version);
        RequireVersion(version);
        var root = new JsonObject
        {
            ["format"] = 1,
            ["version"] = version,
            ["publieLe"] = publishedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };
        if (installer is not null)
        {
            if (installer.Length == 0)
            {
                throw new ArgumentException("L'installateur est vide.", nameof(installer));
            }
            root["installateur"] = new JsonObject
            {
                ["taille"] = installer.LongLength,
                ["sha256"] = Convert.ToHexStringLower(SHA256.HashData(installer)),
            };
        }
        return Encoding.UTF8.GetBytes(root.ToJsonString() + "\n");
    }

    /// <summary>Vérifie un manifeste et sa signature ; rend ce qu'il annonce, ou la raison du refus.</summary>
    /// <param name="manifest">Les octets exacts du manifeste.</param>
    /// <param name="signature">Les octets de sa signature.</param>
    /// <param name="keys">Les clés publiques connues.</param>
    public static Outcome<UpdateAnnouncement> Verify(byte[] manifest, byte[] signature, IReadOnlyList<CatalogueKey> keys)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(keys);
        if (manifest.Length > MaxManifestBytes)
        {
            return Outcome.Refused<UpdateAnnouncement>("L'annonce de version est trop grosse : elle n'est pas lue.");
        }
        if (!ExtensionCatalogue.IsSignedBy(manifest, signature, keys))
        {
            return Outcome.Refused<UpdateAnnouncement>("La signature de l'annonce de version ne correspond à aucune clé connue : elle n'est pas prise.");
        }
        return Read(manifest) is { } announcement
            ? Outcome.Of(announcement)
            : Outcome.Refused<UpdateAnnouncement>("L'annonce de version est signée mais illisible : elle n'est pas prise.");
    }

    /// <summary>Vrai si <paramref name="candidate"/> vient après <paramref name="installed"/>, selon le versionnement sémantique ; un numéro illisible ne l'est jamais.</summary>
    public static bool IsNewer(string candidate, string installed)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(installed);
        var left = VersionPattern().Match(candidate);
        var right = VersionPattern().Match(installed);
        if (!left.Success || !right.Success)
        {
            return false;
        }
        for (var group = 1; group <= 3; group++)
        {
            var order = CompareNumbers(left.Groups[group].Value, right.Groups[group].Value);
            if (order != 0)
            {
                return order > 0;
            }
        }
        return ComparePrerelease(left.Groups[4].Value, right.Groups[4].Value) > 0;
    }

    private static void RequireVersion(string version)
    {
        if (!VersionPattern().IsMatch(version))
        {
            throw new ArgumentException($"« {version} » n'est pas un numéro de version.", nameof(version));
        }
    }

    /// <summary>La version et, s'il est décrit, l'installateur ; rien si le manifeste est illisible, installateur mal décrit compris.</summary>
    private static UpdateAnnouncement? Read(byte[] manifest)
    {
        try
        {
            var root = JsonNode.Parse(manifest)?.AsObject();
            if (root?["format"]?.GetValue<int>() != 1 || root["version"]?.GetValue<string>() is not { } version || !VersionPattern().IsMatch(version))
            {
                return null;
            }
            if (root["installateur"] is not { } described)
            {
                return new UpdateAnnouncement(version, null);
            }
            var size = described["taille"]?.GetValue<long>() ?? 0;
            var sha256 = described["sha256"]?.GetValue<string>() ?? string.Empty;
            return size is > 0 and <= MaxInstallerBytes && Sha256Pattern().IsMatch(sha256)
                ? new UpdateAnnouncement(version, InstallerOf(version, size, sha256))
                : null;
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or InvalidOperationException or FormatException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Deux suites de chiffres, sans limite de taille : la plus longue, une fois les zéros de tête retirés, est la plus grande.</summary>
    private static int CompareNumbers(string left, string right)
    {
        var (a, b) = (left.TrimStart('0'), right.TrimStart('0'));
        return a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b);
    }

    /// <summary>Une version sans préversion vient après la même avec ; sinon, identifiant par identifiant.</summary>
    private static int ComparePrerelease(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0)
        {
            return right.Length.CompareTo(left.Length);
        }
        var (a, b) = (left.Split('.'), right.Split('.'));
        foreach (var (x, y) in a.Zip(b))
        {
            var (xNumber, yNumber) = (x.All(char.IsAsciiDigit), y.All(char.IsAsciiDigit));
            var order = xNumber && yNumber ? CompareNumbers(x, y) : xNumber ? -1 : yNumber ? 1 : string.CompareOrdinal(x, y);
            if (order != 0)
            {
                return order;
            }
        }
        return a.Length.CompareTo(b.Length);
    }

    [GeneratedRegex(@"^(\d{1,9})\.(\d{1,9})\.(\d{1,9})(?:-([0-9A-Za-z]+(?:\.[0-9A-Za-z]+)*))?$")]
    private static partial Regex VersionPattern();

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Sha256Pattern();
}
