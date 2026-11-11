using System.Text;
using KyfRev.Application.Model;

namespace KyfRev.Application.UseCases;

/// <summary>
/// Édition : enregistrement du corps (l'en-tête est conservé) avec détection des conflits,
/// versions et images. Rien n'est jamais perdu : en cas de conflit, rien n'est écrit et le
/// contenu du disque est rendu pour comparer.
/// </summary>
public sealed class EditingUseCases(VaultHost host, TimeProvider time)
{
    private static readonly string[] ImageExtensions = ["png", "jpg", "jpeg", "gif", "webp", "avif", "bmp", "svg"];

    /// <summary>Enregistre le corps de la fiche, sauf si elle a changé sur le disque depuis sa lecture.</summary>
    /// <param name="id">Identifiant de la fiche.</param>
    /// <param name="body">Nouveau corps.</param>
    /// <param name="fingerprint">Empreinte du contenu chargé.</param>
    /// <param name="session">Session d'édition : l'ancien contenu est gardé en version une fois par session.</param>
    [ZeroXCommand("enregistrer_fiche")]
    public Outcome<SaveResult> Save(string id, string body, string fingerprint, ulong session)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.SaveBody(id, body, fingerprint, session, now));
    }

    /// <summary>
    /// Capture rapide : ajoute une ligne datée à la fiche « Boîte de réception », créée et suspendue au besoin ; rend son
    /// identifiant. Refusé sans coffre ouvert, ou sur un coffre verrouillé.
    /// </summary>
    public Outcome<string> Capture(string text)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.CaptureToInbox(text, now));
    }

    /// <summary>Range le texte en cours d'édition dans les versions, avant de recharger celui du disque.</summary>
    [ZeroXCommand("garder_en_version")]
    public Outcome<Done> KeepAsVersion(string id, string body)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.KeepAsVersion(id, body, now));
    }

    /// <summary>Versions gardées de la fiche.</summary>
    [ZeroXCommand("versions")]
    public Outcome<IReadOnlyList<SheetVersion>> Versions(string id) => host.Use(vault => vault.Versions(id));

    /// <summary>Corps d'une version.</summary>
    [ZeroXCommand("lire_version")]
    public Outcome<string> ReadVersion(string id, string name) => host.Use(vault => vault.ReadVersion(id, name));

    /// <summary>Remet une version, sauf si la fiche a changé sur le disque depuis sa lecture.</summary>
    [ZeroXCommand("restaurer_version")]
    public Outcome<SaveResult> RestoreVersion(string id, string name, string fingerprint)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.RestoreVersion(id, name, fingerprint, now));
    }

    /// <summary>
    /// Copie une image (glissée, collée ou choisie) à côté de la fiche ; rend le lien relatif à
    /// insérer. Seuls les types d'image affichables sont acceptés.
    /// </summary>
    [ZeroXCommand("ajouter_image")]
    public Outcome<string> AddImage(string sheetId, string name, ReadOnlyMemory<byte> content)
    {
        ArgumentNullException.ThrowIfNull(name);
        var dot = name.LastIndexOf('.');
        var extension = dot < 0 ? null : name[(dot + 1)..];
        return extension is not null && ImageExtensions.Any(known => Ascii.EqualsIgnoreCase(extension, known))
            ? host.Use(vault => vault.AddResource(sheetId, name, content.Span))
            : Outcome.Refused<string>($"« {name} » n'est pas une image reconnue (PNG, JPEG, GIF, WebP, AVIF, BMP ou SVG).");
    }
}
