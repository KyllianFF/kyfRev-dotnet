namespace KyfRev.Extensions.Host;

/// <summary>
/// Écrit un fichier local (l'état des extensions, la mise à jour lancée) d'un coup : le contenu va d'abord dans un fichier voisin, forcé sur le disque,
/// puis prend la place de l'ancien. Après une coupure ou sur un disque plein, le fichier est l'ancien ou le nouveau, jamais
/// un fichier tronqué. Le voisin porte toujours le même nom : un échec le laisse, l'écriture suivante le reprend.
/// </summary>
internal static class LocalFile
{
    /// <summary>Ce qui suit le nom du fichier voisin.</summary>
    internal const string PendingSuffix = ".ecriture";

    /// <summary>Remplace le fichier, ou le crée.</summary>
    public static void Replace(string path, ReadOnlySpan<byte> content)
    {
        var pending = path + PendingSuffix;
        using (var stream = new FileStream(pending, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(content);
            stream.Flush(flushToDisk: true);
        }
        File.Move(pending, path, overwrite: true);
    }
}
