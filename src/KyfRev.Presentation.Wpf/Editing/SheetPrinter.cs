using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>Ce qui met en page une fiche partagée pour en faire un PDF.</summary>
public interface ISheetPrinter
{
    /// <summary>Le PDF de cette page, ses images déjà intégrées ; rien s'il n'a pas pu se faire, ou si la demande est dépassée.</summary>
    /// <param name="page">La page, un seul fichier.</param>
    /// <param name="cancellation">Annule une demande dépassée.</param>
    Task<byte[]?> PrintAsync(string page, CancellationToken cancellation);
}

/// <summary>
/// Met en page une fiche partagée dans une page cachée, le temps d'en faire un PDF, puis la ferme : le même moteur web que l'éditeur,
/// aussi fermé, sans script, en navigation privée. La page vient de la mémoire, et rien d'autre n'est servi ; le PDF revient en
/// mémoire, sans passer par un fichier : c'est le coffre qui l'écrit, là où on l'a demandé.
/// </summary>
public sealed class SheetPrinter : ISheetPrinter
{
    private const string Host = "partage.kyfrev.invalid";
    private const string Address = $"https://{Host}/fiche.html";
    private const int PopupWindow = unchecked((int)0x80000000);
    private const int HiddenWidth = 1000;
    private const int HiddenHeight = 1400;
    private const double A4Width = 8.27;
    private const double A4Height = 11.69;
    private const double Margin = 0.6;

    private readonly string _dataFolder;

    /// <summary>Le moteur garde ses données dans ce dossier.</summary>
    public SheetPrinter(string dataFolder)
    {
        _dataFolder = dataFolder;
    }

    /// <summary>Le temps que la mise en page peut prendre.</summary>
    public TimeSpan Patience { get; set; } = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    public async Task<byte[]?> PrintAsync(string page, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(page);
        using var window = new HwndSource(new HwndSourceParameters("kyfRev")
        {
            WindowStyle = PopupWindow,
            PositionX = -32000,
            PositionY = -32000,
            Width = HiddenWidth,
            Height = HiddenHeight,
        });
        CoreWebView2Controller? controller = null;
        try
        {
            var environment = await VisualEditor.EnvironmentOf(_dataFolder).ConfigureAwait(true);
            var options = environment.CreateCoreWebView2ControllerOptions();
            options.IsInPrivateModeEnabled = true;
            options.ProfileName = "partage";
            controller = await environment.CreateCoreWebView2ControllerAsync(window.Handle, options).ConfigureAwait(true);
            controller.Bounds = new Rectangle(0, 0, HiddenWidth, HiddenHeight);
            var web = controller.CoreWebView2;
            VisualEditor.Harden(web);
            web.Settings.IsScriptEnabled = false;
            web.Settings.IsWebMessageEnabled = false;
            return await PrintPageAsync(environment, web, page, cancellation).ConfigureAwait(true);
        }
        catch (Exception problem) when (problem is OperationCanceledException or TimeoutException or WebView2RuntimeNotFoundException or InvalidOperationException or COMException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
        finally
        {
            controller?.Close();
        }
    }

    private async Task<byte[]?> PrintPageAsync(CoreWebView2Environment environment, CoreWebView2 web, string page, CancellationToken cancellation)
    {
        var content = Encoding.UTF8.GetBytes(page);
        web.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        web.WebResourceRequested += (_, e) => e.Response = e.Request.Uri == Address
            ? environment.CreateWebResourceResponse(new MemoryStream(content), 200, "OK", "Content-Type: text/html; charset=utf-8\r\nCache-Control: no-store")
            : environment.CreateWebResourceResponse(null, 403, "Forbidden", "Cache-Control: no-store");
        web.NavigationStarting += (_, e) => e.Cancel = e.Uri != Address;
        var loaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        web.NavigationCompleted += (_, e) => loaded.TrySetResult(e.IsSuccess);
        web.ProcessFailed += (_, e) =>
        {
            if (WebProcesses.StopsThePage(e.ProcessFailedKind))
            {
                loaded.TrySetResult(false);
            }
        };
        web.Navigate(Address);
        if (!await loaded.Task.WaitAsync(Patience, cancellation).ConfigureAwait(true))
        {
            return null;
        }
        var settings = environment.CreatePrintSettings();
        settings.PageWidth = A4Width;
        settings.PageHeight = A4Height;
        settings.MarginTop = Margin;
        settings.MarginBottom = Margin;
        settings.MarginLeft = Margin;
        settings.MarginRight = Margin;
        settings.ShouldPrintBackgrounds = true;
        settings.ShouldPrintHeaderAndFooter = false;
        settings.ColorMode = CoreWebView2PrintColorMode.Color;
        using var printed = await web.PrintToPdfStreamAsync(settings).WaitAsync(Patience, cancellation).ConfigureAwait(true);
        using var pdf = new MemoryStream();
        await printed.CopyToAsync(pdf, cancellation).ConfigureAwait(true);
        return pdf.Length == 0 ? null : pdf.ToArray();
    }

    /// <summary>L'adresse de la page, pour les essais.</summary>
    internal static string PageAddress => string.Create(CultureInfo.InvariantCulture, $"{Address}");
}
