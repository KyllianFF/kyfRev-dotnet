using System.IO;
using System.IO.Packaging;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Xps.Packaging;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Printing;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La fenêtre « Imprimer » : ses réglages, les feuilles choisies, et les pages qu'elle envoie.</summary>
[TestClass]
public sealed class PrintTests
{
    private static readonly int[] FirstSecondFourth = [1, 2, 4];
    private static readonly int[] OneToThree = [1, 2, 3];

    [TestMethod]
    public void Sheet_numbers_are_read_as_numbers_and_ranges()
    {
        CollectionAssert.AreEqual(FirstSecondFourth, PageSelection.Parse("1-2, 4", 4)!.ToArray());
        CollectionAssert.AreEqual(FirstSecondFourth, PageSelection.Parse(" 4;2 1 , 2 ", 4)!.ToArray());
        CollectionAssert.AreEqual(OneToThree, PageSelection.Parse("1 – 3", 4)!.ToArray());

        foreach (var wrong in new[] { null, "", "  ", "0", "5", "3-2", "1-", "-2", "a", "1,,x", "1-2-3", "+1", "1.5" })
        {
            Assert.IsNull(PageSelection.Parse(wrong, 4), wrong);
        }
    }

    [STATestMethod]
    public void The_printers_of_windows_are_listed_and_an_unknown_one_refuses_without_printing()
    {
        var service = new WindowsPrintService();

        // La liste peut être vide (un poste sans imprimante, un service arrêté) ; elle est triée, avec au plus une par défaut.
        var printers = service.Printers();
        CollectionAssert.AreEqual(printers.OrderBy(printer => printer.Name, StringComparer.CurrentCultureIgnoreCase).ToList(), printers.ToList());
        Assert.IsLessThanOrEqualTo(1, printers.Count(printer => printer.IsDefault));
        Assert.IsTrue(printers.All(printer => printer.Name.Length > 0));

        // Une imprimante qui n'existe pas : rien ne part, et le refus se dit.
        var pages = new FixedDocument().DocumentPaginator;
        var job = new PrintJob("Une imprimante qui n'existe pas " + Guid.NewGuid().ToString("N"), "Mitose", new PageLayout(PageFormat.A5), PrintSides.OneSided, 1);
        var refusal = service.Print(job, pages);
        Assert.IsFalse(string.IsNullOrEmpty(refusal));
        if (printers.Count > 0)
        {
            Assert.AreEqual(PrintText.PrinterGone, refusal);
        }
        Assert.ThrowsExactly<ArgumentNullException>(() => service.Print(null!, pages));
        Assert.ThrowsExactly<ArgumentNullException>(() => service.Print(job, null!));
    }

    [TestMethod]
    public void A_job_puts_its_settings_on_the_ticket_of_the_printer()
    {
        var a5 = new PageLayout(PageFormat.A5, Landscape: true);
        var ticket = WindowsPrintService.TicketFor(new PrintJob("Bureau", "Mitose", a5, PrintSides.ShortEdge, 5000), new PrintTicket());
        Assert.AreEqual(PrintViewModel.MaximumCopies, ticket.CopyCount, "pas plus d'exemplaires que la fenêtre n'en propose");
        Assert.AreEqual(Duplexing.TwoSidedShortEdge, ticket.Duplexing);
        Assert.AreEqual(PageOrientation.Landscape, ticket.PageOrientation);
        Assert.AreEqual(PageMediaSizeName.ISOA5, ticket.PageMediaSize.PageMediaSizeName);
        // Le papier se décrit en portrait : c'est l'orientation qui le tourne.
        Assert.AreEqual((a5 with { Landscape = false }).WidthPixels, ticket.PageMediaSize.Width);
        Assert.AreEqual((a5 with { Landscape = false }).HeightPixels, ticket.PageMediaSize.Height);

        var a4 = WindowsPrintService.TicketFor(new PrintJob("Bureau", "Mitose", new PageLayout(PageFormat.A4), PrintSides.OneSided, 0), new PrintTicket());
        Assert.AreEqual((1, Duplexing.OneSided, PageOrientation.Portrait, PageMediaSizeName.ISOA4), (a4.CopyCount, a4.Duplexing, a4.PageOrientation, a4.PageMediaSize.PageMediaSizeName));
        var a6 = WindowsPrintService.TicketFor(new PrintJob("Bureau", "Mitose", new PageLayout(PageFormat.A6), PrintSides.LongEdge, 2), new PrintTicket());
        Assert.AreEqual((2, Duplexing.TwoSidedLongEdge, PageMediaSizeName.ISOA6), (a6.CopyCount, a6.Duplexing, a6.PageMediaSize.PageMediaSizeName));
    }

    [STATestMethod]
    public void Without_a_printer_nothing_is_sent_and_the_choices_still_answer()
    {
        using var ui = new UiThread();
        var service = new FakePrinting { Installed = [] };
        var print = new PrintViewModel(service);
        print.Open(Reading("Texte."), new PageLayout(PageFormat.A5));
        UiThread.Until(() => !print.IsLookingForPrinters);

        Assert.IsEmpty(print.Printers);
        Assert.IsFalse(print.CanPrint || print.HasPages);
        Assert.IsFalse(print.Print(), "sans imprimante, rien ne part");
        Assert.IsEmpty(service.Jobs);

        // Les marges et le recto verso se choisissent quand même.
        print.Margin = print.MarginOptions.Single(option => option.Kind == PrintMargin.Narrow);
        Assert.AreEqual(PageLayout.NarrowMargin, print.Layout.MarginCm);
        print.Margin = print.MarginOptions.Single(option => option.Kind == PrintMargin.Normal);
        Assert.AreEqual(PageLayout.NormalMargin, print.Layout.MarginCm);
        print.SetSidesCommand.Execute("short");
        Assert.AreEqual(PrintSides.ShortEdge, print.Sides);
        print.SetSidesCommand.Execute("long");
        Assert.AreEqual(PrintSides.LongEdge, print.Sides);
        print.SetSidesCommand.Execute("one");
        Assert.AreEqual(PrintSides.OneSided, print.Sides);
        StringAssert.Contains(PrintText.PrintFailed("le pilote refuse"), "le pilote refuse", StringComparison.Ordinal);

        // Sans service d'impression du tout (un essai, une version sans Windows) : aucune imprimante, et rien ne casse.
        var none = new PrintViewModel(null);
        none.Open(Reading("Texte."), PageLayout.Default);
        Assert.IsEmpty(none.Printers);
        Assert.IsFalse(none.Print());
    }

    [TestMethod]
    public void Two_sided_printing_halves_the_paper()
    {
        Assert.AreEqual(3, PageSelection.PaperSheets(3, PrintSides.OneSided, 1));
        Assert.AreEqual(2, PageSelection.PaperSheets(3, PrintSides.LongEdge, 1));
        Assert.AreEqual(6, PageSelection.PaperSheets(4, PrintSides.ShortEdge, 3));
        Assert.AreEqual(0, PageSelection.PaperSheets(0, PrintSides.LongEdge, 2));
    }

    [STATestMethod]
    public void The_window_starts_from_the_layout_of_the_sheet()
    {
        using var ui = new UiThread();
        var service = new FakePrinting();
        var print = new PrintViewModel(service);

        print.Open(Reading("Texte."), new PageLayout(PageFormat.A5, Landscape: true, MarginCm: 1));
        UiThread.Until(() => !print.IsLookingForPrinters);

        Assert.IsTrue(print.IsOpen);
        Assert.AreEqual("Imprimer « Mitose »", print.Title);
        Assert.AreEqual(new PageLayout(PageFormat.A5, Landscape: true, MarginCm: 1), print.Layout);
        Assert.AreEqual("Celles de la fiche (1 cm)", print.Margin.Label);
        Assert.AreEqual("Bureau", print.Printer, "l'imprimante par défaut de Windows");
        Assert.IsTrue(print.IsOneSided && print.AllPages && print.ShowsNumbers);
        Assert.AreEqual(1, print.Copies);

        // Une fiche restée en feuille sans fin s'imprime en A4, en portrait, avec des marges normales.
        print.Close();
        print.Open(Reading("Texte."), PageLayout.Default);
        Assert.AreEqual(new PageLayout(PageFormat.A4), print.Layout);
        Assert.AreEqual("Bureau", print.Printer);

        print.Margin = print.MarginOptions.Single(option => option.Kind == PrintMargin.Wide);
        Assert.AreEqual(3, print.Layout.MarginCm);
        print.Margin = print.MarginOptions.Single(option => option.Kind == PrintMargin.Custom);
        print.CustomMargin = "1,5";
        Assert.AreEqual(1.5, print.Layout.MarginCm);
        print.CustomMargin = "n'importe quoi";
        Assert.AreEqual(2, print.Layout.MarginCm, "une saisie illisible laisse les marges de la fiche");
        print.SetOrientationCommand.Execute("landscape");
        Assert.IsTrue(print.Layout.Landscape);
    }

    [STATestMethod]
    [DataRow(false, 860)]
    [DataRow(true, 860)]
    [DataRow(false, 430)]
    public void Another_paper_chosen_in_the_window_redoes_the_pages_of_the_preview(bool dark, int width)
    {
        using var ui = new UiThread();
        var test = new TestShell(dark);
        var body = "# Chapitre\n\n" + string.Join("\n\n", Enumerable.Range(1, 30).Select(index => $"Paragraphe {index} : " + string.Join(' ', Enumerable.Repeat("des mots qui font des lignes", 5))));
        var print = new PrintViewModel(new FakePrinting());
        var view = new PrintView { DataContext = print, Width = width, Height = 700 };
        var host = new Border { Child = view, Width = width, Height = 700 };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        host.SetResourceReference(Border.BackgroundProperty, "Kyf.Brush.Surface");
        var window = new Window { Content = new Canvas { Children = { host } }, Left = -10000, Top = -10000, Width = 400, Height = 300, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        try
        {
            print.Open(Reading(body), new PageLayout(PageFormat.A5));
            UiThread.Until(() => !print.IsPreparing && print.TotalPages > 0);
            TestShell.Flush();
            var onA5 = print.TotalPages;
            var a5 = new PageLayout(PageFormat.A5);
            Assert.AreEqual(a5.WidthPixels, view.PageCanvas.Width);

            // Le choix du papier : dans la fenêtre, entier, sur celui de la fiche.
            var box = (ComboBox)view.FindName("PaperFormatBox");
            Assert.IsTrue(box.IsVisible);
            Assert.AreEqual(3, box.Items.Count);
            Assert.AreSame(print.Format, box.SelectedItem);
            Assert.AreEqual(PrintText.PaperFormat, System.Windows.Automation.AutomationProperties.GetName(box));
            Assert.IsGreaterThanOrEqualTo(0, box.TranslatePoint(default, host).X);
            Assert.IsLessThanOrEqualTo(width, box.TranslatePoint(new Point(box.ActualWidth, 0), host).X, "il tient dans la fenêtre");

            // Large, les réglages et l'aperçu sont côte à côte ; étroite, l'un sous l'autre, et rien ne sort du cadre.
            var preview = (FrameworkElement)view.FindName("PreviewPane");
            Assert.AreEqual(width < PrintView.StackedBelow, view.IsStacked);
            Assert.IsGreaterThanOrEqualTo(-0.5, preview.TranslatePoint(default, host).X);
            Assert.IsLessThanOrEqualTo(width + 0.5, preview.TranslatePoint(new Point(preview.ActualWidth, 0), host).X, "l'aperçu tient dans la largeur de la fenêtre");
            if (view.IsStacked)
            {
                Assert.IsGreaterThan(box.TranslatePoint(default, host).Y, preview.TranslatePoint(default, host).Y, "sous les réglages");
                Assert.AreEqual(ScrollBarVisibility.Auto, ((ScrollViewer)view.FindName("Whole")).VerticalScrollBarVisibility, "et l'ensemble défile");
            }
            else
            {
                Assert.IsGreaterThan(box.TranslatePoint(new Point(box.ActualWidth, 0), host).X, preview.TranslatePoint(default, host).X, "à droite des réglages");
            }

            // A4 : la feuille de l'aperçu grandit, et le même texte tient sur moins de feuilles.
            box.SelectedIndex = 0;
            var a4 = new PageLayout(PageFormat.A4);
            UiThread.Until(() => !print.IsPreparing && view.PageCanvas.Width == a4.WidthPixels && print.TotalPages > 0 && print.TotalPages < onA5);
            Assert.AreEqual(a4.HeightPixels, view.PageCanvas.Height);
            Assert.AreEqual(PageFormat.A4, print.Layout.Format);
            TestShell.Flush();
            host.Capture($"impression-a4-{width}-{(dark ? "sombre" : "clair")}");
            if (view.IsStacked)
            {
                ((ScrollViewer)view.FindName("Whole")).ScrollToEnd();
                TestShell.Flush();
                host.Capture($"impression-a4-{width}-bas");
            }

            // A6 : plus petite, donc plus de feuilles ; ce qui part à l'imprimante a sa taille.
            box.SelectedIndex = 2;
            var a6 = new PageLayout(PageFormat.A6);
            UiThread.Until(() => !print.IsPreparing && view.PageCanvas.Width == a6.WidthPixels && print.TotalPages > onA5);
            TestShell.Flush();
            Assert.AreEqual(new Size(a6.WidthPixels, a6.HeightPixels), view.Paginate([1], numbers: true)!.PageSize);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_paper_is_the_one_of_the_sheet_until_another_is_chosen()
    {
        using var ui = new UiThread();
        var service = new FakePrinting();
        var print = new PrintViewModel(service);

        print.Open(Reading("Texte."), new PageLayout(PageFormat.A5, MarginCm: 1));
        UiThread.Until(() => !print.IsLookingForPrinters);
        CollectionAssert.AreEqual(new[] { PageFormat.A4, PageFormat.A5, PageFormat.A6 }, print.FormatOptions.Select(option => option.Format).ToArray());
        Assert.AreEqual("A5 (14,8 × 21 cm), celui de la fiche", print.Format.Label);
        Assert.AreEqual("A4 (21 × 29,7 cm)", print.FormatOptions[0].ToString());

        // Un autre papier : la feuille imprimée change, l'aperçu l'apprend, la fiche n'en sait rien.
        var changed = new List<string?>();
        print.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        print.Format = print.FormatOptions.Single(option => option.Format == PageFormat.A4);
        Assert.AreEqual(new PageLayout(PageFormat.A4, MarginCm: 1), print.Layout);
        CollectionAssert.Contains(changed, nameof(PrintViewModel.Layout));

        // Le plus petit : des marges trop grandes pour lui sont ramenées à ce qu'il peut tenir.
        print.Margin = print.MarginOptions.Single(option => option.Kind == PrintMargin.Custom);
        print.CustomMargin = "9";
        print.Format = print.FormatOptions.Single(option => option.Format == PageFormat.A6);
        Assert.AreEqual(PageFormat.A6, print.Layout.Format);
        Assert.IsLessThan(print.Layout.WidthPixels, 2 * print.Layout.MarginPixels, "il reste de la place pour le texte");

        // Le papier choisi part à l'imprimante, avec son nom de format.
        print.Margin = print.MarginOptions.Single(option => option.Kind == PrintMargin.Normal);
        var ticket = WindowsPrintService.TicketFor(new PrintJob("Bureau", "Mitose", print.Layout, PrintSides.OneSided, 1), new System.Printing.PrintTicket());
        Assert.AreEqual(System.Printing.PageMediaSizeName.ISOA6, ticket.PageMediaSize!.PageMediaSizeName);

        // Une fiche sans fin part en A4, sans « celui de la fiche » : elle n'a pas de papier à elle. Rouverte, la fenêtre
        // oublie le papier choisi la fois d'avant.
        print.Close();
        print.Open(Reading("Texte."), PageLayout.Default);
        Assert.AreEqual("A4 (21 × 29,7 cm)", print.Format.Label);
        Assert.IsFalse(print.FormatOptions.Any(option => option.Label.Contains("fiche", StringComparison.Ordinal)));
    }

    [STATestMethod]
    public void What_is_asked_goes_to_the_printer_or_the_window_says_why_not()
    {
        using var ui = new UiThread();
        var service = new FakePrinting();
        var print = new PrintViewModel(service);
        var said = new List<string>();
        print.Printed = said.Add;
        print.Open(Reading("Texte."), new PageLayout(PageFormat.A4));
        UiThread.Until(() => !print.IsLookingForPrinters);
        Assert.IsFalse(print.CanPrint, "l'aperçu n'est pas prêt");

        print.TotalPages = 4;
        print.IsPreparing = false;
        var asked = new List<(IReadOnlyList<int> Pages, bool Numbers)>();
        print.Paginator = (pages, numbers) =>
        {
            asked.Add((pages, numbers));
            return new FixedDocument().DocumentPaginator;
        };
        Assert.IsTrue(print.CanPrint);
        Assert.AreEqual("4 feuille(s) à imprimer · 4 feuille(s) de papier", print.Summary);

        print.SetPagesCommand.Execute("some");
        Assert.IsFalse(print.CanPrint, "aucun numéro encore");
        Assert.IsFalse(print.HasPagesError);
        print.PagesText = "1-2, 9";
        Assert.IsTrue(print.HasPagesError);
        StringAssert.Contains(print.PagesMessage, "de 1 à 4", StringComparison.Ordinal);
        print.PagesText = "1-2, 4";
        Assert.IsTrue(print.CanPrint);
        print.SetSidesCommand.Execute("long");
        print.NudgeCopiesCommand.Execute("+");
        Assert.AreEqual("3 feuille(s) à imprimer · 4 feuille(s) de papier", print.Summary);
        print.Copies = 500;
        Assert.AreEqual(PrintViewModel.MaximumCopies, print.Copies);
        print.Copies = 2;
        print.TurnCommand.Execute("+");
        print.TurnCommand.Execute("+");
        Assert.AreEqual("3 sur 4", print.PageLabel);
        Assert.IsFalse(print.IsCurrentPrinted, "la feuille 3 n'est pas demandée");
        print.ShowsNumbers = false;
        print.Printer = "Atelier";

        // L'imprimante refuse : la fenêtre reste, et dit pourquoi.
        service.Refusal = "plus de papier";
        Assert.IsFalse(print.Print());
        Assert.IsTrue(print.IsOpen && print.HasError);
        Assert.AreEqual("plus de papier", print.Error);

        service.Refusal = null;
        Assert.IsTrue(print.Print());
        Assert.IsFalse(print.IsOpen);
        Assert.AreEqual("Envoyé à l'imprimante « Atelier ».", said.Single());
        var job = service.Jobs[^1];
        Assert.AreEqual(("Atelier", "Mitose", PrintSides.LongEdge, 2), (job.Printer, job.Title, job.Sides, job.Copies));
        Assert.AreEqual(new PageLayout(PageFormat.A4), job.Layout);
        CollectionAssert.AreEqual(FirstSecondFourth, asked[^1].Pages.ToArray());
        Assert.IsFalse(asked[^1].Numbers);

        // Sans imprimante installée, rien ne s'imprime.
        service.Installed = [];
        var alone = new PrintViewModel(service);
        alone.Open(Reading("Texte."), null);
        UiThread.Until(() => !alone.IsLookingForPrinters);
        Assert.IsTrue(alone.HasNoPrinter);
        Assert.IsFalse(alone.CanPrint);
    }

    [STATestMethod]
    public void The_preview_and_the_printed_pages_are_cut_from_the_same_layout()
    {
        using var ui = new UiThread();
        var body = "# Chapitre\n\n" + string.Join("\n\n", Enumerable.Range(1, 30).Select(index => $"Paragraphe {index} : " + string.Join(' ', Enumerable.Repeat("des mots qui font des lignes", 5))));
        var print = new PrintViewModel(new FakePrinting());
        var view = new PrintView { DataContext = print, Width = 860, Height = 600 };
        var window = new Window { Content = view, Left = -10000, Top = -10000, Width = 900, Height = 660, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        try
        {
            var layout = new PageLayout(PageFormat.A6);
            print.Open(Reading(body), layout);
            UiThread.Until(() => !print.IsPreparing && print.TotalPages > 0);
            TestShell.Flush();

            var total = print.TotalPages;
            Assert.IsGreaterThan(3, total);
            Assert.AreEqual(total, view.Viewer.PageCount);
            Assert.AreEqual(layout.WidthPixels, view.PageCanvas.Width);
            Assert.AreEqual($"1/{total}", view.PageNumber.Text);
            print.TurnCommand.Execute("+");
            Assert.AreEqual($"2/{total}", view.PageNumber.Text);
            print.ShowsNumbers = false;
            Assert.AreEqual(string.Empty, view.PageNumber.Text);

            // Les pages envoyées : à la taille de la feuille, avec du texte dedans et des marges blanches.
            var pages = view.Paginate([1, 3], numbers: true)!;
            Assert.AreEqual(2, pages.PageCount);
            Assert.AreEqual(new Size(layout.WidthPixels, layout.HeightPixels), pages.PageSize);
            var first = Pixels(pages.GetPage(0).Visual, pages.PageSize);
            var third = Pixels(pages.GetPage(1).Visual, pages.PageSize);
            Assert.IsGreaterThan(200, Dark(first, 0, first.PixelHeight), "du texte sur la première feuille");
            Assert.AreEqual(0, Dark(first, 0, (int)layout.MarginPixels - 4), "la marge du haut est blanche");
            Assert.IsGreaterThan(0, Dark(first, first.PixelHeight - (int)layout.MarginPixels + 2, first.PixelHeight), "le numéro est dans la marge du bas");
            Assert.AreNotEqual(Dark(first, 0, first.PixelHeight), Dark(third, 0, third.PixelHeight), "deux feuilles différentes");
            var bare = Pixels(view.Paginate([1], numbers: false)!.GetPage(0).Visual, pages.PageSize);
            Assert.AreEqual(0, Dark(bare, bare.PixelHeight - (int)layout.MarginPixels + 2, bare.PixelHeight), "sans numéro, la marge du bas est blanche");

            // Et elles s'écrivent comme un vrai document à imprimer, le texte restant du texte.
            var file = Path.Combine(Path.GetTempPath(), "kyfrev-impression-" + Guid.NewGuid().ToString("N") + ".xps");
            try
            {
                using (var package = Package.Open(file, FileMode.Create))
                using (var document = new XpsDocument(package))
                {
                    XpsDocument.CreateXpsDocumentWriter(document).Write(pages);
                }
                using var written = Package.Open(file, FileMode.Open, FileAccess.Read);
                var sheets = written.GetParts().Where(part => part.Uri.OriginalString.EndsWith(".fpage", StringComparison.OrdinalIgnoreCase)).ToList();
                Assert.HasCount(2, sheets);
                using var reader = new StreamReader(sheets[0].GetStream());
                StringAssert.Contains(reader.ReadToEnd(), "<Glyphs", StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(file);
            }

            // D'autres marges : la fiche est remise en page, et l'aperçu suit.
            print.Margin = print.MarginOptions.Single(option => option.Kind == PrintMargin.Wide);
            for (var pass = 0; pass < 8; pass++)
            {
                TestShell.Flush();
                view.UpdateLayout();
            }
            Assert.IsGreaterThan(total, print.TotalPages, "des marges plus larges demandent plus de feuilles");
        }
        finally
        {
            window.Close();
        }
    }

    private static SheetReading Reading(string body) => SheetReading.Of(
        new OpenedSheet("f_1", "Mitose", "fiches/Biologie/Mitose.md", "Biologie", [], body, 1, "sha256:essai", null, DateTimeOffset.UnixEpoch, null!, []),
        _ => Outcome.Refused<byte[]>("non"));

    private static RenderTargetBitmap Pixels(Visual visual, Size size)
    {
        var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    /// <summary>Le nombre de points nettement sombres entre deux lignes de l'image.</summary>
    private static int Dark(BitmapSource bitmap, int fromRow, int toRow)
    {
        var stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        var count = 0;
        for (var row = Math.Max(0, fromRow); row < Math.Min(bitmap.PixelHeight, toRow); row++)
        {
            for (var column = 0; column < bitmap.PixelWidth; column++)
            {
                var at = (row * stride) + (column * 4);
                if (pixels[at + 3] > 128 && pixels[at] + pixels[at + 1] + pixels[at + 2] < 300)
                {
                    count++;
                }
            }
        }
        return count;
    }

    private sealed class FakePrinting : IPrintService
    {
        public IReadOnlyList<PrinterInfo> Installed { get; set; } = [new("Atelier", false), new("Bureau", true)];

        public string? Refusal { get; set; }

        public List<PrintJob> Jobs { get; } = [];

        public IReadOnlyList<PrinterInfo> Printers() => Installed;

        public string? Print(PrintJob job, DocumentPaginator pages)
        {
            Jobs.Add(job);
            return Refusal;
        }
    }
}
