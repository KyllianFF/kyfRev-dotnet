using System.Printing;
using System.Windows.Documents;
using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Printing;

/// <summary>
/// Les imprimantes de Windows. Un travail part avec ses réglages (format, orientation, recto verso, exemplaires), que
/// l'imprimante valide : ce qu'elle ne sait pas faire (le recto verso, sur bien des modèles) est ramené à ce qu'elle sait.
/// </summary>
public sealed class WindowsPrintService : IPrintService
{
    private static readonly EnumeratedPrintQueueTypes[] Kinds = [EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections];

    /// <inheritdoc/>
    public IReadOnlyList<PrinterInfo> Printers()
    {
        try
        {
            using var server = new LocalPrintServer();
            var preferred = DefaultPrinter();
            var printers = new List<PrinterInfo>();
            using var queues = server.GetPrintQueues(Kinds);
            foreach (var queue in queues)
            {
                using (queue)
                {
                    printers.Add(new PrinterInfo(queue.FullName, queue.FullName == preferred));
                }
            }
            return [.. printers.OrderBy(printer => printer.Name, StringComparer.CurrentCultureIgnoreCase)];
        }
        catch (Exception exception) when (IsPrinting(exception))
        {
            return [];
        }
    }

    /// <inheritdoc/>
    public string? Print(PrintJob job, DocumentPaginator pages)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(pages);
        try
        {
            using var server = new LocalPrintServer();
            using var queues = server.GetPrintQueues(Kinds);
            PrintQueue? queue = null;
            foreach (var candidate in queues)
            {
                if (queue is null && candidate.FullName == job.Printer)
                {
                    queue = candidate;
                }
                else
                {
                    candidate.Dispose();
                }
            }
            if (queue is null)
            {
                return PrintText.PrinterGone;
            }
            using (queue)
            {
                var ticket = TicketFor(job, queue.UserPrintTicket ?? queue.DefaultPrintTicket ?? new PrintTicket());
                var validated = queue.MergeAndValidatePrintTicket(queue.DefaultPrintTicket ?? ticket, ticket).ValidatedPrintTicket;
                queue.CurrentJobSettings.Description = job.Title;
                PrintQueue.CreateXpsDocumentWriter(queue).Write(pages, validated);
            }
            return null;
        }
        catch (Exception exception) when (IsPrinting(exception))
        {
            return PrintText.PrintFailed(exception.Message);
        }
    }

    /// <summary>Le nom de l'imprimante par défaut ; rien si Windows n'en a pas : la première de la liste sera proposée.</summary>
    private static string? DefaultPrinter()
    {
        try
        {
            using var chosen = LocalPrintServer.GetDefaultPrintQueue();
            return chosen?.FullName;
        }
        catch (Exception exception) when (IsPrinting(exception))
        {
            return null;
        }
    }

    /// <summary>Pose les réglages d'un travail sur le ticket d'une imprimante : exemplaires, recto verso, orientation, papier.</summary>
    internal static PrintTicket TicketFor(PrintJob job, PrintTicket ticket)
    {
        ticket.CopyCount = Math.Clamp(job.Copies, 1, PrintViewModel.MaximumCopies);
        ticket.Duplexing = job.Sides switch
        {
            PrintSides.LongEdge => Duplexing.TwoSidedLongEdge,
            PrintSides.ShortEdge => Duplexing.TwoSidedShortEdge,
            _ => Duplexing.OneSided,
        };
        ticket.PageOrientation = job.Layout.Landscape ? PageOrientation.Landscape : PageOrientation.Portrait;
        // Le papier se décrit en portrait : c'est l'orientation qui le tourne.
        var portrait = job.Layout with { Landscape = false };
        ticket.PageMediaSize = new PageMediaSize(MediaName(job.Layout.Format), portrait.WidthPixels, portrait.HeightPixels);
        return ticket;
    }

    private static PageMediaSizeName MediaName(PageFormat format) => format switch
    {
        PageFormat.A5 => PageMediaSizeName.ISOA5,
        PageFormat.A6 => PageMediaSizeName.ISOA6,
        _ => PageMediaSizeName.ISOA4,
    };

    /// <summary>Ce que le système d'impression peut lever : une file absente, un pilote qui refuse, un service arrêté.</summary>
    private static bool IsPrinting(Exception exception) =>
        exception is PrintSystemException or InvalidOperationException or System.IO.IOException or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.COMException or ArgumentException or NotSupportedException;
}
