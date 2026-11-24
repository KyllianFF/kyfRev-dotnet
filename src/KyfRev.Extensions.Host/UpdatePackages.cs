using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using KyfRev.Application;
using KyfRev.Application.Ports;

namespace KyfRev.Extensions.Host;

/// <summary>Ce qu'est devenue la dernière mise à jour lancée, vu au démarrage suivant.</summary>
/// <param name="Version">La version qu'elle devait installer.</param>
/// <param name="Installed">Elle tourne maintenant : l'installation a réussi.</param>
/// <param name="Installer">Son installateur, pour réessayer ; rien si elle a réussi.</param>
public sealed record UpdateAftermath(string Version, bool Installed, InstallerFile? Installer);

/// <summary>
/// L'installateur d'une version annoncée : téléchargé dans un dossier de ce poste, vérifié contre l'annonce signée (taille
/// et empreinte SHA-256), puis lancé en silence une fois kyfRev fermé. Un fichier qui ne correspond pas à l'annonce est
/// effacé, jamais lancé ; il est vérifié de nouveau au moment de le lancer, tenu ouvert pour que rien ne le remplace.
/// </summary>
/// <param name="downloader">Le téléchargement des fichiers.</param>
/// <param name="installer">L'installateur de ce poste.</param>
/// <param name="folder">Le dossier des mises à jour, parmi les données de ce poste.</param>
public sealed partial class UpdatePackages(IFileDownloader downloader, IUpdateInstaller installer, string folder)
{
    /// <summary>La mise à jour lancée, relue au démarrage suivant.</summary>
    public const string PendingFile = "en-attente.json";

    /// <summary>Le journal de la dernière installation.</summary>
    public const string LogFile = "installation.log";

    /// <summary>Le dossier des mises à jour.</summary>
    public string Folder => folder;

    /// <summary>Pour qui kyfRev est installé ; rien si l'installateur ne l'a pas mis là, et la mise à jour ne peut alors pas s'installer.</summary>
    public InstallScope? Scope => installer.Scope;

    /// <summary>
    /// L'installateur de <paramref name="file"/>, prêt à lancer : celui déjà là s'il correspond à l'annonce, sinon
    /// téléchargé et vérifié. Les installateurs d'autres versions sont effacés.
    /// </summary>
    public async Task<Outcome<string>> DownloadAsync(InstallerFile file, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        var path = PathOf(file);
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Outcome.Refused<string>($"Le dossier des mises à jour est inaccessible ({exception.Message.TrimEnd('.')}).");
        }
        ForgetOthers(file);
        if (Matches(path, file))
        {
            progress?.Report(file.Size);
            return Outcome.Of(path);
        }
        try
        {
            await downloader.DownloadAsync(file.Address, path, file.Size, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationException exception)
        {
            return Outcome.Refused<string>($"Le téléchargement n'a pas abouti ({exception.Message.TrimEnd('.')}).");
        }
        if (Matches(path, file))
        {
            return Outcome.Of(path);
        }
        Delete(path);
        return Outcome.Refused<string>("Le fichier téléchargé ne correspond pas à l'annonce signée : il a été effacé, et rien n'est installé.");
    }

    /// <summary>L'installateur de <paramref name="file"/>, s'il est déjà là et correspond à l'annonce ; rien sinon.</summary>
    public string? Find(InstallerFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var path = PathOf(file);
        return Matches(path, file) ? path : null;
    }

    /// <summary>
    /// Lance l'installateur de <paramref name="version"/> en silence, après l'avoir vérifié une dernière fois, et retient
    /// la mise à jour pour le démarrage suivant. À appeler une fois kyfRev fermé : ouvert, il arrête l'installateur.
    /// </summary>
    /// <param name="version">La version à installer.</param>
    /// <param name="file">Son installateur, tel que l'annonce le décrit.</param>
    /// <param name="reopen">kyfRev se rouvre à la fin.</param>
    public Outcome<Done> Launch(string version, InstallerFile file, bool reopen)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(file);
        var path = PathOf(file);
        try
        {
            // Tenu ouvert en lecture seule : vérifié, il ne peut plus être remplacé avant que Windows l'ait lancé.
            using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (!Matches(held, file))
            {
                return Outcome.Refused<Done>("L'installateur ne correspond plus à l'annonce signée : il n'est pas lancé.");
            }
            // Retenue avant le lancement : si Windows ne le lance pas, le démarrage suivant le dit.
            Remember(version, file);
            return installer.Start(path, reopen, Path.Combine(folder, LogFile))
                ? Outcome.Done()
                : Outcome.Refused<Done>("Windows n'a pas lancé l'installation.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Outcome.Refused<Done>($"L'installateur est illisible ({exception.Message.TrimEnd('.')}).");
        }
    }

    /// <summary>
    /// Ce qu'est devenue la dernière mise à jour lancée, une seule fois : rien si aucune ne l'a été. Réussie, son
    /// installateur est effacé ; manquée, il reste pour réessayer.
    /// </summary>
    /// <param name="current">La version qui tourne.</param>
    public UpdateAftermath? TakeAftermath(string current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var pending = Path.Combine(folder, PendingFile);
        UpdateAftermath? aftermath;
        try
        {
            if (!File.Exists(pending))
            {
                return null;
            }
            aftermath = Read(File.ReadAllBytes(pending), current);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceWarning($"Mise à jour lancée illisible : {exception.Message}");
            return null;
        }
        // Dite une seule fois.
        Delete(pending);
        if (aftermath is { Installed: true })
        {
            ForgetOthers(null);
        }
        return aftermath;
    }

    private string PathOf(InstallerFile file) => Path.Combine(folder, file.Name);

    private static UpdateAftermath? Read(byte[] content, string current)
    {
        try
        {
            var root = JsonNode.Parse(content)?.AsObject();
            if (root?["version"]?.GetValue<string>() is not { } version || root["taille"]?.GetValue<long>() is not { } size || root["sha256"]?.GetValue<string>() is not { } sha256)
            {
                return null;
            }
            var file = UpdateChecker.InstallerOf(version, size, sha256);
            return UpdateChecker.IsNewer(version, current) ? new UpdateAftermath(version, false, file) : new UpdateAftermath(version, true, null);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Retient la mise à jour lancée ; si c'est impossible, le démarrage suivant n'en dira rien, sans autre effet.</summary>
    private void Remember(string version, InstallerFile file)
    {
        var root = new JsonObject
        {
            ["version"] = version,
            ["taille"] = file.Size,
            ["sha256"] = file.Sha256,
        };
        try
        {
            LocalFile.Replace(Path.Combine(folder, PendingFile), System.Text.Encoding.UTF8.GetBytes(root.ToJsonString() + "\n"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceWarning($"Mise à jour lancée non retenue : {exception.Message}");
        }
    }

    /// <summary>Efface les installateurs du dossier, sauf celui de <paramref name="kept"/>.</summary>
    private void ForgetOthers(InstallerFile? kept)
    {
        string[] paths;
        try
        {
            paths = Directory.Exists(folder) ? Directory.GetFiles(folder, "kyfRev-*-installation.exe*") : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceWarning($"Dossier des mises à jour illisible : {exception.Message}");
            return;
        }
        foreach (var path in paths.Where(path => IsOurs(path) && (kept is null || !string.Equals(Path.GetFileName(path), kept.Name, StringComparison.OrdinalIgnoreCase))))
        {
            Delete(path);
        }
    }

    /// <summary>
    /// Un fichier que les mises à jour ont écrit, directement dans leur dossier : un installateur, son début de
    /// téléchargement (« .partiel »), ou la mise à jour retenue. Rien d'autre ne s'efface ici.
    /// </summary>
    private bool IsOurs(string path)
    {
        var full = Path.GetFullPath(path);
        return string.Equals(Path.GetDirectoryName(full), Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)), StringComparison.OrdinalIgnoreCase)
            && (Path.GetFileName(full) == PendingFile || InstallerName().IsMatch(Path.GetFileName(full)));
    }

    /// <summary>Le fichier existe, a la taille et l'empreinte annoncées ; illisible, il ne correspond pas.</summary>
    private static bool Matches(string path, InstallerFile file)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Matches(stream, file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceWarning($"Installateur illisible : {exception.Message}");
            return false;
        }
    }

    private static bool Matches(FileStream stream, InstallerFile file) =>
        stream.Length == file.Size && string.Equals(Convert.ToHexStringLower(SHA256.HashData(stream)), file.Sha256, StringComparison.Ordinal);

    /// <summary>Le seul effacement des mises à jour : un de leurs fichiers, dans leur dossier (<see cref="IsOurs"/>).</summary>
    private void Delete(string path)
    {
        if (!IsOurs(path))
        {
            throw new InvalidOperationException($"« {path} » n'est pas un fichier des mises à jour : il n'est pas effacé.");
        }
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Retenu par un autre programme : il sera effacé à la prochaine mise à jour, et n'est jamais lancé sans vérification.
            System.Diagnostics.Trace.TraceWarning($"Fichier des mises à jour non effacé : {exception.Message}");
        }
    }

    [GeneratedRegex(@"^kyfRev-[0-9A-Za-z.-]+-installation\.exe(\.partiel)?$")]
    private static partial Regex InstallerName();
}
