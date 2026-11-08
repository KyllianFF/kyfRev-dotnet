using System.Globalization;
using System.Text;
using System.Windows;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>Copie une fiche partagée : sa page, pour coller avec la mise en forme, et son texte, pour le reste.</summary>
public static class SharedClipboard
{
    private const string BodyStart = "<body>";
    private const string BodyEnd = "</body>";
    private const string FragmentStart = "<!--StartFragment-->";
    private const string FragmentEnd = "<!--EndFragment-->";

    /// <summary>Copie <paramref name="page"/> et <paramref name="text"/> ; d'un coffre chiffré, hors de l'historique de Windows.</summary>
    /// <param name="page">La page, un seul fichier.</param>
    /// <param name="text">Le texte brut.</param>
    /// <param name="isPrivate">Le contenu vient d'un coffre chiffré.</param>
    /// <exception cref="System.Runtime.InteropServices.ExternalException">Le presse-papiers est pris par un autre programme.</exception>
    public static void Copy(string page, string text, bool isPrivate) => Clipboard.SetDataObject(DataOf(page, text, isPrivate), copy: true);

    /// <summary>La page et le texte, tels qu'ils vont au presse-papiers.</summary>
    internal static DataObject DataOf(string page, string text, bool isPrivate)
    {
        var data = new DataObject();
        data.SetData(DataFormats.Html, HtmlFormat(page));
        data.SetText(text, TextDataFormat.UnicodeText);
        if (isPrivate)
        {
            SecretClipboard.Protect(data);
        }
        return data;
    }

    /// <summary>
    /// Le format HTML du presse-papiers : un en-tête qui dit, en octets UTF-8, où commencent la page et ce qui se colle, ici le
    /// contenu de <c>body</c>. Les styles restent à côté, dans <c>head</c>, pour qui sait les lire.
    /// </summary>
    internal static string HtmlFormat(string page)
    {
        var bodyAt = page.IndexOf(BodyStart, StringComparison.Ordinal) + BodyStart.Length;
        var endAt = page.LastIndexOf(BodyEnd, StringComparison.Ordinal);
        var marked = string.Concat(page.AsSpan(0, bodyAt), FragmentStart, page.AsSpan(bodyAt, endAt - bodyAt), FragmentEnd + page[endAt..]);
        var header = Header(0, 0, 0, 0);
        var htmlAt = Encoding.UTF8.GetByteCount(header);
        var startAt = htmlAt + Encoding.UTF8.GetByteCount(marked.AsSpan(0, bodyAt + FragmentStart.Length));
        var stopAt = startAt + Encoding.UTF8.GetByteCount(marked.AsSpan(bodyAt + FragmentStart.Length, endAt - bodyAt));
        return Header(htmlAt, htmlAt + Encoding.UTF8.GetByteCount(marked), startAt, stopAt) + marked;
    }

    private static string Header(int start, int end, int fragmentStart, int fragmentEnd) => string.Create(
        CultureInfo.InvariantCulture,
        $"Version:0.9\r\nStartHTML:{start:D10}\r\nEndHTML:{end:D10}\r\nStartFragment:{fragmentStart:D10}\r\nEndFragment:{fragmentEnd:D10}\r\n");
}
