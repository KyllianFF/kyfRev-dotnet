using System.Globalization;
using KyfRev.Domain.Calendar;

namespace KyfRev.Infrastructure.Vault;

public sealed partial class VaultSession
{
    /// <summary>Le titre de la fiche où vont les notes prises à la volée.</summary>
    public const string InboxTitle = "Boîte de réception";

    /// <summary>La plus longue note prise à la volée : au-delà, c'est une fiche qu'il faut écrire.</summary>
    public const int MaxCaptureLength = 2000;

    /// <summary>Une seule session d'édition pour toutes les captures : la fiche n'est copiée dans ses versions qu'une fois.</summary>
    private const ulong CaptureSession = 0x6B79_6652_6576_0001;

    /// <summary>
    /// Ajoute une note prise à la volée à la fiche « Boîte de réception », à la racine des fiches : une ligne de liste,
    /// datée. La fiche est créée au besoin, et suspendue aussitôt : c'est un bloc-notes à trier, pas une leçon à réviser.
    /// Reprise par l'utilisateur, elle reste reprise.
    /// </summary>
    /// <returns>L'identifiant de la boîte de réception.</returns>
    /// <exception cref="VaultException">La note est vide ou trop longue, ou la fiche a changé pendant l'écriture.</exception>
    public string CaptureToInbox(string text, Moment now)
    {
        ArgumentNullException.ThrowIfNull(text);
        // Sur une ligne : une note à la volée n'a pas de mise en page.
        var note = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (note.Length == 0)
        {
            throw VaultException.InvalidSettings("il n'y a rien à noter.");
        }
        if (note.Length > MaxCaptureLength)
        {
            throw VaultException.InvalidSettings($"une note à la volée fait au plus {MaxCaptureLength} caractères.");
        }
        var inbox = _sheets.Values
            .Where(entry => entry.Presence == FilePresence.Present && entry.Sheet.Title == InboxTitle && DisplayFolder(entry.Sheet.Path).Length == 0)
            .OrderBy(entry => entry.Sheet.Id, StringComparer.Ordinal)
            .FirstOrDefault()?.Sheet.Id;
        if (inbox is null)
        {
            inbox = CreateSheet(null, InboxTitle, string.Empty, now);
            ApplyTo(ItemKind.Sheet, inbox, (tracking, _) => tracking.Suspend(now));
        }
        var read = ReadSheet(inbox);
        var body = read.Text[HeaderPart(read.Text).Length..].TrimEnd('\r', '\n');
        var line = string.Create(CultureInfo.InvariantCulture, $"- {now.Day:yyyy-MM-dd} : {note}\n");
        var saved = SaveBody(inbox, body.Length == 0 ? line : $"{body}\n{line}", read.Fingerprint, CaptureSession, now);
        return saved is SheetSaved
            ? inbox
            : throw VaultException.InvalidSettings("la boîte de réception a changé pendant l'écriture : réessayez.");
    }
}
