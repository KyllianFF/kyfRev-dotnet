namespace KyfRev.Application.Model;

/// <summary>Issue d'un enregistrement du corps d'une fiche : <see cref="SheetSaved"/> ou <see cref="SaveConflict"/>.</summary>
public abstract record SaveResult
{
    private protected SaveResult()
    {
    }
}

/// <summary>Enregistré.</summary>
/// <param name="Fingerprint">Empreinte du nouveau contenu.</param>
public sealed record SheetSaved(string Fingerprint) : SaveResult;

/// <summary>Refusé : la fiche a changé sur le disque depuis sa lecture. Rien n'a été écrit.</summary>
/// <param name="DiskFingerprint">Empreinte du contenu actuel.</param>
/// <param name="DiskBody">Corps actuel, pour comparer.</param>
public sealed record SaveConflict(string DiskFingerprint, string DiskBody) : SaveResult;

/// <summary>Version antérieure d'une fiche, dans <c>.suivi/versions/&lt;id&gt;/</c>.</summary>
public sealed record SheetVersion(string Name, DateTimeOffset SavedAt, long Size);
