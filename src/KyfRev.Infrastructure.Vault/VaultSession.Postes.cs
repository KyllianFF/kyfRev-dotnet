using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Json;
using KyfRev.Infrastructure.Vault.Paths;

namespace KyfRev.Infrastructure.Vault;

/// <summary>
/// Un coffre ouvert sur plusieurs postes, dans un dossier synchronisé : le verrou du coffre ne vaut que sur un poste, et
/// la session écrit le suivi d'une fiche, ses passages et les réglages à partir de ce qu'elle a lu. Avant de les changer,
/// elle relit donc le fichier s'il n'est plus celui qu'elle a lu ou écrit (son empreinte le dit) : ce qu'un autre poste y
/// a écrit entre-temps, une révision, un passage, un examen, est repris avant que le changement de ce poste ne s'y
/// ajoute, au lieu d'être effacé. Un fichier qui ne se lit pas pour l'instant laisse la session comme elle était.
/// </summary>
public sealed partial class VaultSession
{
    private readonly Dictionary<string, string> _trackingPrints = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _annotationPrints = new(StringComparer.Ordinal);
    private string? _settingsPrint;

    /// <summary>Le suivi d'une fiche vient d'être lu ou écrit, tel quel.</summary>
    private void KnowTracking(string id, ReadOnlySpan<byte> bytes) => _trackingPrints[id] = Fingerprint.Of(bytes);

    /// <summary>Les passages d'une fiche viennent d'être lus ou écrits, tels quels.</summary>
    private void KnowAnnotations(string sheetId, ReadOnlySpan<byte> bytes) => _annotationPrints[sheetId] = Fingerprint.Of(bytes);

    /// <summary>Les réglages viennent d'être lus ou écrits, tels quels.</summary>
    internal void KnowSettings(ReadOnlySpan<byte> bytes) => _settingsPrint = Fingerprint.Of(bytes);

    /// <summary>
    /// Reprend le suivi de la fiche tel qu'un autre poste l'a écrit, s'il a changé depuis qu'il a été lu ou écrit ici :
    /// son étape, ses révisions, son archivage. Le chemin, l'empreinte, le titre et les étiquettes restent ceux que ce poste
    /// a relevés sur la fiche elle-même.
    /// </summary>
    private void RefreshTracking(string id)
    {
        if (!_sheets.TryGetValue(id, out var entry)
            || TryReadAllBytes(Confine(VaultPath.Create($"{VaultLayout.SheetTracking}/{id}.json"))) is not { } bytes
            || IsKnown(_trackingPrints, id, bytes))
        {
            return;
        }
        TrackedSheetFile disk;
        try
        {
            disk = TrackedSheetFile.Read(bytes);
        }
        catch (VaultFormatException)
        {
            // Illisible : le suivi de ce poste reste, et sera écrit tel quel.
            return;
        }
        if (disk.Id == id)
        {
            _sheets[id] = entry with { Sheet = entry.Sheet with { Tracking = disk.Tracking, Unknown = disk.Unknown } };
            KnowTracking(id, bytes);
        }
    }

    /// <summary>Reprend les passages et les définitions d'une fiche tels qu'un autre poste les a écrits, s'ils ont changé.</summary>
    private void RefreshAnnotations(string sheetId)
    {
        if (!VaultIds.IsSheetId(sheetId)
            || TryReadAllBytes(Confine(VaultPath.Create($"{VaultLayout.Annotations}/{sheetId}.json"))) is not { } bytes
            || IsKnown(_annotationPrints, sheetId, bytes))
        {
            return;
        }
        AnnotationsFile disk;
        try
        {
            disk = AnnotationsFile.Read(bytes);
        }
        catch (VaultFormatException)
        {
            return;
        }
        if (disk.SheetId == sheetId)
        {
            _annotations[sheetId] = disk;
            _unreadableAnnotations.Remove(sheetId);
            KnowAnnotations(sheetId, bytes);
        }
    }

    /// <summary>Reprend les réglages du coffre tels qu'un autre poste les a écrits, s'ils ont changé : ses examens, ses préférences.</summary>
    private void RefreshSettings()
    {
        if (TryReadAllBytes(Confine(SettingsPath)) is not { } bytes || _settingsPrint == Fingerprint.Of(bytes))
        {
            return;
        }
        try
        {
            Settings = VaultSettingsFile.Read(bytes);
        }
        catch (VaultFormatException)
        {
            return;
        }
        KnowSettings(bytes);
    }

    private static bool IsKnown(Dictionary<string, string> prints, string key, ReadOnlySpan<byte> bytes) =>
        prints.TryGetValue(key, out var known) && known == Fingerprint.Of(bytes);
}
