using System.IO;
using System.Windows;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// Copie un secret, la clé de récupération, dans le presse-papiers sans qu'il aille dans l'historique de Windows (Win+V)
/// ni dans le presse-papiers partagé entre les appareils : il y reste jusqu'à la prochaine copie. Les programmes qui
/// surveillent le presse-papiers sont priés de le laisser de côté ; rien ne les y oblige.
/// </summary>
public static class SecretClipboard
{
    /// <summary>Les programmes qui surveillent le presse-papiers laissent ce contenu de côté (la valeur ne compte pas).</summary>
    internal const string ExcludeFromMonitors = "ExcludeClipboardContentFromMonitorProcessing";

    /// <summary>Un DWORD : 0, le contenu ne va pas dans l'historique du presse-papiers.</summary>
    internal const string CanIncludeInHistory = "CanIncludeInClipboardHistory";

    /// <summary>Un DWORD : 0, le contenu ne part pas vers le presse-papiers des autres appareils.</summary>
    internal const string CanUploadToCloud = "CanUploadToCloudClipboard";

    /// <summary>Copie <paramref name="text"/>, et le laisse au presse-papiers une fois l'application fermée.</summary>
    /// <param name="text">Le secret.</param>
    /// <exception cref="System.Runtime.InteropServices.ExternalException">Le presse-papiers est pris par un autre programme.</exception>
    public static void Copy(string text) => Clipboard.SetDataObject(DataOf(text), copy: true);

    /// <summary>
    /// Marque <paramref name="data"/>, déjà prêt à être copié (par le clavier, par un menu), pour qu'il reste hors de
    /// l'historique et du partage, et que les programmes qui surveillent le presse-papiers le laissent de côté.
    /// </summary>
    /// <param name="data">Ce qui va être copié.</param>
    public static void Protect(IDataObject data)
    {
        ArgumentNullException.ThrowIfNull(data);
        data.SetData(ExcludeFromMonitors, Dword(0));
        data.SetData(CanIncludeInHistory, Dword(0));
        data.SetData(CanUploadToCloud, Dword(0));
    }

    /// <summary>Le texte, et ce qui le tient hors de l'historique, du partage et des programmes qui surveillent.</summary>
    internal static DataObject DataOf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var data = new DataObject();
        data.SetText(text, TextDataFormat.UnicodeText);
        Protect(data);
        return data;
    }

    private static MemoryStream Dword(int value) => new(BitConverter.GetBytes(value), writable: false);
}
