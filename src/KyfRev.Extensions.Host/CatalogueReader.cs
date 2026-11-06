using KyfRev.Application;
using KyfRev.Application.Ports;

namespace KyfRev.Extensions.Host;

/// <summary>
/// Le catalogue en ligne : il est lu dans le dépôt public de kyfRev, seulement quand on le demande, et seulement par
/// l'application (les extensions, elles, n'ont jamais le réseau). Il n'est pris que vérifié ; une extension n'est
/// téléchargée que fichier par fichier, chacun vérifié par sa taille et son empreinte avant d'être écrit.
/// </summary>
/// <param name="reader">Le lecteur en ligne de l'application.</param>
/// <param name="library">La bibliothèque, qui retient le numéro du dernier catalogue pris.</param>
/// <param name="keys">Les clés publiques de publication connues.</param>
/// <param name="downloads">Le dossier où les extensions téléchargées attendent leur installation.</param>
public sealed class CatalogueReader(IOnlineReader reader, ExtensionLibrary library, IReadOnlyList<CatalogueKey> keys, string downloads)
{
    /// <summary>Le dépôt public d'où viennent le catalogue et les extensions.</summary>
    public static Uri Repository { get; } = new("https://raw.githubusercontent.com/KyllianFF/kyfRev-dotnet/main/");

    /// <summary>L'application connaît au moins une clé de publication : le catalogue peut s'ouvrir.</summary>
    public bool HasKeys => keys.Count > 0;

    /// <summary>Lit le catalogue et le vérifie ; un catalogue pris retient son numéro.</summary>
    public async Task<Outcome<Catalogue>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!HasKeys)
        {
            return Outcome.Refused<Catalogue>("Le catalogue n'a pas encore de clé de publication : il s'ouvrira avec la version de kyfRev qui la connaît.");
        }
        byte[] catalogue;
        byte[] signature;
        try
        {
            catalogue = await reader.ReadBytesAsync(new Uri(Repository, "catalogue/catalogue.json"), cancellationToken).ConfigureAwait(false);
            signature = await reader.ReadBytesAsync(new Uri(Repository, "catalogue/catalogue.sig"), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationException exception)
        {
            return Outcome.Refused<Catalogue>(exception.Message);
        }
        var verified = ExtensionCatalogue.Verify(catalogue, signature, keys, library.CatalogueNumber);
        if (verified.Succeeded)
        {
            library.SetCatalogueNumber(verified.Value.Number);
        }
        return verified;
    }

    /// <summary>
    /// Télécharge une extension du catalogue dans un dossier neuf, chaque fichier vérifié ; rend ce dossier, prêt à être
    /// lu et installé comme un dossier choisi. Un seul fichier différent du catalogue, et rien n'est gardé.
    /// </summary>
    public async Task<Outcome<string>> DownloadAsync(CatalogueEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var folder = Path.Combine(downloads, $"{entry.Id}-{Guid.NewGuid():N}");
        try
        {
            foreach (var file in entry.Files)
            {
                var content = await reader.ReadBytesAsync(new Uri(Repository, $"extensions/{entry.Id}/{file.Path}"), cancellationToken).ConfigureAwait(false);
                if (!ExtensionCatalogue.Matches(file, content))
                {
                    Discard(folder);
                    return Outcome.Refused<string>($"« {file.Path} » de {entry.Name} ne correspond pas au catalogue : l'extension n'est pas installée.");
                }
                var target = Path.Combine(folder, file.Path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await File.WriteAllBytesAsync(target, content, cancellationToken).ConfigureAwait(false);
            }
            return Outcome.Of(folder);
        }
        catch (OperationException exception)
        {
            Discard(folder);
            return Outcome.Refused<string>(exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Discard(folder);
            return Outcome.Refused<string>(exception.Message);
        }
    }

    /// <summary>
    /// Range un dossier téléchargé, une fois l'extension installée ou refusée. Rien n'est effacé hors du dossier des
    /// téléchargements : ni le coffre, ni un dossier choisi.
    /// </summary>
    public void Discard(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var root = Path.GetFullPath(downloads) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(folder).StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Un dossier temporaire qui reste n'empêche rien : le suivant a un autre nom.
            System.Diagnostics.Trace.TraceWarning($"Dossier téléchargé gardé : {exception.Message}");
        }
    }
}
