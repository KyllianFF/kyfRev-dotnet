using System.Globalization;
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
public sealed record UpdateStatus(string Current, string Latest, bool IsNewer, Uri Page);

/// <summary>
/// La recherche de mise à jour, seulement quand on la demande : elle lit dans le dépôt public un court manifeste signé par une
/// clé de publication connue, et ne télécharge ni n'installe rien. La page proposée se déduit de la version signée : aucune
/// adresse ne vient du manifeste.
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

    /// <summary>Les pages des versions publiées : la seule destination possible.</summary>
    public static Uri Releases { get; } = new("https://github.com/KyllianFF/kyfRev-dotnet/releases/tag/");

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
            ? Outcome.Of(new UpdateStatus(current, latest.Value, IsNewer(latest.Value, current), new Uri(Releases, $"v{latest.Value}")))
            : Outcome.Refused<UpdateStatus>(latest.Error!);
    }

    /// <summary>Écrit un manifeste : la version publiée et sa date.</summary>
    /// <exception cref="ArgumentException">Ce n'est pas un numéro de version.</exception>
    public static byte[] Write(string version, DateOnly publishedOn)
    {
        ArgumentException.ThrowIfNullOrEmpty(version);
        if (!VersionPattern().IsMatch(version))
        {
            throw new ArgumentException($"« {version} » n'est pas un numéro de version.", nameof(version));
        }
        var root = new JsonObject
        {
            ["format"] = 1,
            ["version"] = version,
            ["publieLe"] = publishedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };
        return Encoding.UTF8.GetBytes(root.ToJsonString() + "\n");
    }

    /// <summary>Vérifie un manifeste et sa signature ; rend la version qu'il annonce, ou la raison du refus.</summary>
    /// <param name="manifest">Les octets exacts du manifeste.</param>
    /// <param name="signature">Les octets de sa signature.</param>
    /// <param name="keys">Les clés publiques connues.</param>
    public static Outcome<string> Verify(byte[] manifest, byte[] signature, IReadOnlyList<CatalogueKey> keys)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(keys);
        if (manifest.Length > MaxManifestBytes)
        {
            return Outcome.Refused<string>("L'annonce de version est trop grosse : elle n'est pas lue.");
        }
        if (!ExtensionCatalogue.IsSignedBy(manifest, signature, keys))
        {
            return Outcome.Refused<string>("La signature de l'annonce de version ne correspond à aucune clé connue : elle n'est pas prise.");
        }
        return ReadVersion(manifest) is { } version
            ? Outcome.Of(version)
            : Outcome.Refused<string>("L'annonce de version est signée mais illisible : elle n'est pas prise.");
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

    private static string? ReadVersion(byte[] manifest)
    {
        try
        {
            var root = JsonNode.Parse(manifest)?.AsObject();
            return root?["format"]?.GetValue<int>() == 1 && root["version"]?.GetValue<string>() is { } version && VersionPattern().IsMatch(version) ? version : null;
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
}
