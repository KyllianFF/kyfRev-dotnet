using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La fenêtre « Partager » et la question « Enregistrer comme modèle » : ce qu'elles écrivent, où, et ce qu'elles taisent.</summary>
[TestClass]
public sealed class SharingTests
{
    [STATestMethod]
    [DataRow(ShareKind.Markdown, ".md")]
    [DataRow(ShareKind.Archive, ".zip")]
    [DataRow(ShareKind.Text, ".txt")]
    [DataRow(ShareKind.WebPage, ".html")]
    [DataRow(ShareKind.Pdf, ".pdf")]
    public void Each_format_writes_its_file_in_the_chosen_folder(ShareKind kind, string extension)
    {
        using var ui = new UiThread();
        using var scene = new SharingScene();
        var sharing = scene.Sharing;
        scene.Open();
        Assert.AreEqual("Partager « Mitose »", sharing.ShareTitle);
        Assert.AreEqual(SharingText.Cancel, sharing.CloseLabel);
        Assert.IsFalse(sharing.IsEncrypted);

        scene.Answer(kind, scene.Outside);

        var file = Directory.GetFiles(scene.Outside).Single();
        Assert.AreEqual("Mitose" + extension, Path.GetFileName(file));
        Assert.AreEqual(SharingText.CreatedOf(file), sharing.Message);
        Assert.IsTrue(sharing.HasMessage);
        Assert.IsFalse(sharing.HasError);
        Assert.IsTrue(sharing.IsSharing, "le résultat se lit dans la fenêtre");
        Assert.IsFalse(sharing.IsBusy);
        Assert.IsTrue(sharing.IsIdle);
        Assert.AreEqual(SharingText.Close, sharing.CloseLabel);
        CollectionAssert.AreEqual(new[] { SharingText.PickFolder }, scene.Vaults.Picker.Titles);
    }

    [STATestMethod]
    public void What_the_files_hold_comes_from_the_same_rendering_as_the_reading()
    {
        using var ui = new UiThread();
        using var scene = new SharingScene();
        scene.WriteImage("Biologie/carte.png");
        scene.Open();

        scene.Answer(ShareKind.WebPage, scene.Outside);
        var page = File.ReadAllText(Path.Combine(scene.Outside, "Mitose.html"));
        StringAssert.Contains(page, "<strong>texte</strong>", StringComparison.Ordinal);
        StringAssert.Contains(page, "src=\"data:image/png;base64,", StringComparison.Ordinal);
        Assert.IsFalse(page.Contains("data-coffre", StringComparison.Ordinal), "l'image est dans le fichier");

        scene.Answer(ShareKind.Text, scene.Outside);
        var text = File.ReadAllText(Path.Combine(scene.Outside, "Mitose.txt"));
        StringAssert.StartsWith(text, "Mitose\n\nLe texte de la fiche,", StringComparison.Ordinal);
        StringAssert.Contains(text, "$x^2$", StringComparison.Ordinal);
        StringAssert.Contains(text, "[Image : carte]", StringComparison.Ordinal);

        scene.Answer(ShareKind.Markdown, scene.Outside);
        StringAssert.StartsWith(File.ReadAllText(Path.Combine(scene.Outside, "Mitose.md")), "# Mitose", StringComparison.Ordinal);

        scene.Answer(ShareKind.Archive, scene.Outside);
        using (var zip = ZipFile.OpenRead(Path.Combine(scene.Outside, "Mitose.zip")))
        {
            Assert.IsTrue(zip.Entries.Any(entry => entry.FullName == "carte.png"), "les images suivent la fiche");
        }

        scene.Answer(ShareKind.Pdf, scene.Outside);
        CollectionAssert.AreEqual(scene.Printer.Result, File.ReadAllBytes(Path.Combine(scene.Outside, "Mitose.pdf")));
        StringAssert.Contains(scene.Printer.Page, "src=\"data:image/png;base64,", StringComparison.Ordinal);
        Assert.IsFalse(scene.Printer.Page!.Contains("data-coffre", StringComparison.Ordinal), "la page cachée ne charge rien d'autre");

        scene.Answer(ShareKind.Text, scene.Outside);
        Assert.IsTrue(File.Exists(Path.Combine(scene.Outside, "Mitose (2).txt")), "un fichier existant n'est pas écrasé");
    }

    [STATestMethod]
    public void Giving_up_on_the_folder_writes_nothing_and_keeps_the_window()
    {
        using var ui = new UiThread();
        using var scene = new SharingScene();
        scene.Open();

        scene.Answer(ShareKind.Markdown, null);

        Assert.IsEmpty(Directory.GetFiles(scene.Outside));
        Assert.IsTrue(scene.Sharing.IsSharing);
        Assert.IsFalse(scene.Sharing.IsBusy);
        Assert.IsFalse(scene.Sharing.HasMessage);
    }

    [STATestMethod]
    public void The_clipboard_gets_the_page_and_the_text_without_any_folder()
    {
        using var ui = new UiThread();
        using var scene = new SharingScene();
        scene.WriteImage("Biologie/carte.png");
        scene.Open();
        scene.Sharing.Choices.Single(choice => choice.Kind == ShareKind.Clipboard).IsChecked = true;
        Assert.AreEqual(SharingText.ActionCopy, scene.Sharing.ActionLabel);

        scene.Answer(ShareKind.Clipboard, folder: null);

        Assert.AreEqual(SharingText.Copied, scene.Sharing.Message);
        Assert.IsEmpty(scene.Vaults.Picker.Titles);
        var (page, text, isPrivate) = scene.Copies.Single();
        StringAssert.Contains(page, "<strong>texte</strong>", StringComparison.Ordinal);
        StringAssert.Contains(page, "src=\"data:image/png;base64,", StringComparison.Ordinal);
        StringAssert.Contains(text, "Le texte de la fiche,", StringComparison.Ordinal);
        Assert.IsFalse(isPrivate, "un coffre en clair va dans l'historique comme tout texte");
    }

    [STATestMethod]
    public void A_clipboard_held_by_another_program_is_said()
    {
        using var ui = new UiThread();
        using var scene = new SharingScene { CopyFails = true };
        scene.Open();

        scene.Answer(ShareKind.Clipboard, folder: null);

        Assert.IsNull(scene.Sharing.Message);
        Assert.AreEqual(SharingText.CopyFailed, scene.Sharing.Error);
        Assert.IsTrue(scene.Sharing.HasError);
    }

    [STATestMethod]
    public void A_pdf_the_engine_cannot_prepare_leaves_no_file()
    {
        using var ui = new UiThread();
        using var scene = new SharingScene();
        scene.Printer.Result = null;
        scene.Open();

        scene.Answer(ShareKind.Pdf, scene.Outside);

        Assert.AreEqual(SharingText.PdfFailed, scene.Sharing.Error);
        Assert.IsEmpty(Directory.GetFiles(scene.Outside));
    }

    [STATestMethod]
    public void A_refused_share_says_why_and_the_window_stays()
    {
        using var ui = new UiThread();
        using var scene = new SharingScene();
        scene.Sharing.RequestShare("f_inconnue", "Disparue");

        scene.Answer(ShareKind.Markdown, scene.Outside);
        Assert.IsTrue(scene.Sharing.HasError);
        Assert.IsTrue(scene.Sharing.IsSharing);
        Assert.IsNull(scene.Sharing.Message);

        foreach (var kind in new[] { ShareKind.Text, ShareKind.WebPage, ShareKind.Pdf, ShareKind.Clipboard })
        {
            scene.Answer(kind, kind == ShareKind.Clipboard ? null : scene.Outside);
            Assert.IsTrue(scene.Sharing.HasError, kind.ToString());
        }
        Assert.IsEmpty(Directory.GetFiles(scene.Outside));
        Assert.IsNull(scene.Printer.Page);
        Assert.IsEmpty(scene.Copies);
    }

    [STATestMethod]
    public void A_shared_file_cannot_land_inside_the_vault()
    {
        using var ui = new UiThread();
        using var scene = new SharingScene();
        scene.Open();

        scene.Answer(ShareKind.Pdf, scene.Root);

        Assert.IsTrue(scene.Sharing.HasError);
        Assert.IsFalse(File.Exists(Path.Combine(scene.Root, "Mitose.pdf")));
    }

    [STATestMethod]
    public void On_an_encrypted_vault_the_window_says_what_leaves_is_in_clear()
    {
        using var ui = new UiThread();
        using var scene = new SharingScene(encrypted: true);
        scene.Open();
        Assert.IsTrue(scene.Sharing.IsEncrypted, "la fenêtre le dit");

        scene.Answer(ShareKind.Text, scene.Outside);
        StringAssert.Contains(File.ReadAllText(Directory.GetFiles(scene.Outside).Single()), "Le texte de la fiche,", StringComparison.Ordinal);

        scene.Answer(ShareKind.Clipboard, folder: null);
        Assert.IsTrue(scene.Copies.Single().IsPrivate, "ni l'historique ni le partage entre appareils");
    }

    [STATestMethod]
    public void Closing_the_window_while_it_works_writes_nothing()
    {
        using var ui = new UiThread();
        using var scene = new SharingScene();
        scene.Printer.Gate = new TaskCompletionSource();
        scene.Open();

        var running = scene.Start(ShareKind.Pdf, scene.Outside);
        UiThread.Until(() => scene.Printer.Page is not null);
        Assert.IsTrue(scene.Sharing.IsBusy);
        Assert.IsFalse(scene.Sharing.IsIdle);

        scene.Sharing.AnswerShareCommand.Execute(false);
        Assert.IsFalse(scene.Sharing.IsBusy, "la fenêtre fermée n'attend plus");
        Assert.IsFalse(scene.Sharing.IsSharing);

        scene.Printer.Gate.SetResult();
        UiThread.Until(() => running.IsCompleted);
        running.GetAwaiter().GetResult();
        Assert.IsEmpty(Directory.GetFiles(scene.Outside));
        Assert.IsNull(scene.Sharing.Error, "rien à reprocher à ce qu'on a abandonné");
    }

    [STATestMethod]
    public void A_newer_request_cancels_the_older_one_even_late_in_its_work()
    {
        using var ui = new UiThread();
        using var scene = new SharingScene();
        scene.Open();

        // La demande est partie préparer le texte ; elle est dépassée avant de revenir sur le fil de l'interface.
        var running = scene.Start(ShareKind.Text, scene.Outside);
        scene.Open();

        UiThread.Until(() => running.IsCompleted);
        running.GetAwaiter().GetResult();
        Assert.IsEmpty(Directory.GetFiles(scene.Outside), "elle n'écrit plus rien");
        Assert.IsNull(scene.Sharing.Message);
        Assert.IsFalse(scene.Sharing.IsBusy, "la nouvelle fenêtre est libre");
    }

    [STATestMethod]
    public void A_copy_overtaken_by_a_newer_request_copies_nothing()
    {
        using var ui = new UiThread();
        using var scene = new SharingScene();
        scene.Open();

        var running = scene.Start(ShareKind.Clipboard, folder: null);
        scene.Open();

        UiThread.Until(() => running.IsCompleted);
        running.GetAwaiter().GetResult();
        Assert.IsEmpty(scene.Copies);
        Assert.IsNull(scene.Sharing.Message);
    }

    [STATestMethod]
    public void A_second_confirmation_while_it_works_does_nothing_more()
    {
        using var ui = new UiThread();
        using var scene = new SharingScene();
        scene.Printer.Gate = new TaskCompletionSource();
        scene.Open();
        var running = scene.Start(ShareKind.Pdf, scene.Outside);
        UiThread.Until(() => scene.Printer.Page is not null);

        scene.Sharing.AnswerShareCommand.Execute(true);
        Assert.IsTrue(scene.Sharing.IsSharing);

        scene.Printer.Gate.SetResult();
        UiThread.Until(() => running.IsCompleted);
        Assert.HasCount(1, Directory.GetFiles(scene.Outside));
    }

    [STATestMethod]
    public void Forgetting_the_vault_cancels_the_share_and_drops_what_the_window_held()
    {
        using var ui = new UiThread();
        using var scene = new SharingScene(encrypted: true);
        var sharing = scene.Sharing;
        scene.Printer.Gate = new TaskCompletionSource();
        scene.Open();
        var pdf = sharing.Choices.Single(choice => choice.Kind == ShareKind.Pdf);
        var running = scene.Start(ShareKind.Pdf, scene.Outside);
        UiThread.Until(() => scene.Printer.Page is not null);

        sharing.Forget();

        Assert.IsFalse(sharing.IsSharing);
        Assert.IsFalse(sharing.IsBusy);
        Assert.AreEqual(string.Empty, sharing.ShareTitle);
        Assert.IsNull(sharing.Message);
        Assert.IsNull(sharing.Error);
        Assert.IsFalse(sharing.IsEncrypted);
        Assert.AreSame(pdf, sharing.Selected, "le format choisi n'est pas du coffre");

        scene.Printer.Gate.SetResult();
        UiThread.Until(() => running.IsCompleted);
        running.GetAwaiter().GetResult();
        Assert.IsEmpty(Directory.GetFiles(scene.Outside), "le partage parti avant n'écrit plus rien");
        Assert.IsNull(sharing.Message);
    }

    [STATestMethod]
    public void A_vault_locked_while_the_folder_is_asked_shares_nothing()
    {
        using var ui = new UiThread();
        using var scene = new SharingScene();
        var outside = scene.Outside;
        var app = scene.App;
        SheetSharingViewModel? sharing = null;
        var picker = new CallingPicker(() =>
        {
            sharing!.Forget();
            return outside;
        });
        sharing = new SheetSharingViewModel(app.Sheets, app.Templates, app.Vaults, picker, new SharedSheetPictures(null), scene.Printer, TimeProvider.System);
        sharing.RequestShare(scene.SheetId, "Mitose");
        sharing.Choices.Single(choice => choice.Kind == ShareKind.Markdown).IsChecked = true;

        var running = sharing.AnswerShareCommand.ExecuteAsync(true);
        UiThread.Until(() => running.IsCompleted);

        Assert.IsEmpty(Directory.GetFiles(outside));
        Assert.IsFalse(sharing.IsBusy, "rien ne reste en cours");
        Assert.IsFalse(sharing.IsSharing);
    }

    [STATestMethod]
    public void Leaving_the_page_closes_the_windows()
    {
        using var ui = new UiThread();
        using var scene = new SharingScene();
        scene.Open();
        Assert.IsTrue(scene.Sharing.IsShowing);

        scene.Sharing.Dismiss();

        Assert.IsFalse(scene.Sharing.IsShowing);
        scene.Sharing.RequestSaveAsTemplate(scene.SheetId, "Mitose", _ => { });
        Assert.IsTrue(scene.Sharing.IsShowing);
        scene.Sharing.Dismiss();
        Assert.IsFalse(scene.Sharing.IsNamingTemplate);
    }

    [STATestMethod]
    public void A_template_takes_the_sheet_title_by_default_and_says_where_it_went()
    {
        using var ui = new UiThread();
        using var scene = new SharingScene();
        var sharing = scene.Sharing;
        var said = new List<string>();

        sharing.RequestSaveAsTemplate(scene.SheetId, "Mitose", said.Add);
        Assert.IsTrue(sharing.IsNamingTemplate);
        Assert.AreEqual("Mitose", sharing.TemplateName);
        sharing.TemplateName = "  Cours  ";
        var running = sharing.AnswerTemplateCommand.ExecuteAsync(true);
        UiThread.Until(() => running.IsCompleted);

        Assert.IsFalse(sharing.IsNamingTemplate);
        Assert.AreEqual(SharingText.TemplateDoneOf("Cours"), said.Single());
        Assert.IsTrue(scene.App.Templates.Templates().Value.Any(template => template.Name == "Cours"));
    }

    [STATestMethod]
    public void A_template_without_a_name_or_refused_keeps_the_question_open()
    {
        using var ui = new UiThread();
        using var scene = new SharingScene();
        var sharing = scene.Sharing;
        var said = new List<string>();

        sharing.RequestSaveAsTemplate(scene.SheetId, "Mitose", said.Add);
        sharing.TemplateName = "   ";
        var empty = sharing.AnswerTemplateCommand.ExecuteAsync(true);
        UiThread.Until(() => empty.IsCompleted);
        Assert.IsTrue(sharing.IsNamingTemplate);
        Assert.AreEqual(SheetActionsText.NameRequired, sharing.TemplateError);
        Assert.IsTrue(sharing.HasTemplateError);

        sharing.RequestSaveAsTemplate("f_inconnue", "Disparue", said.Add);
        Assert.IsFalse(sharing.HasTemplateError, "la question repart à neuf");
        var refused = sharing.AnswerTemplateCommand.ExecuteAsync(true);
        UiThread.Until(() => refused.IsCompleted);
        Assert.IsTrue(sharing.IsNamingTemplate);
        Assert.IsTrue(sharing.HasTemplateError);

        var cancelled = sharing.AnswerTemplateCommand.ExecuteAsync(false);
        UiThread.Until(() => cancelled.IsCompleted);
        Assert.IsFalse(sharing.IsNamingTemplate);
        Assert.IsFalse(sharing.HasTemplateError);
        Assert.IsEmpty(said);
    }

    [STATestMethod]
    public void A_template_saved_while_the_vault_locks_says_nothing()
    {
        using var ui = new UiThread();
        using var scene = new SharingScene();
        var sharing = scene.Sharing;
        var said = new List<string>();
        sharing.RequestSaveAsTemplate(scene.SheetId, "Mitose", said.Add);
        sharing.TemplateName = "Cours";

        var running = sharing.AnswerTemplateCommand.ExecuteAsync(true);
        sharing.Forget();
        Assert.AreEqual(string.Empty, sharing.TemplateName);

        UiThread.Until(() => running.IsCompleted);
        TestShell.Flush();
        Assert.IsEmpty(said);
        Assert.IsFalse(sharing.IsNamingTemplate);
    }

    [TestMethod]
    public void The_window_needs_all_it_works_with()
    {
        using var scene = new SharingScene();
        var app = scene.App;
        var picker = scene.Vaults.Picker;
        var pictures = new SharedSheetPictures(null);
        var printer = new FakePrinter();
        var time = TimeProvider.System;
        Assert.ThrowsExactly<ArgumentNullException>(() => new SheetSharingViewModel(null!, app.Templates, app.Vaults, picker, pictures, printer, time));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SheetSharingViewModel(app.Sheets, null!, app.Vaults, picker, pictures, printer, time));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SheetSharingViewModel(app.Sheets, app.Templates, null!, picker, pictures, printer, time));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SheetSharingViewModel(app.Sheets, app.Templates, app.Vaults, null!, pictures, printer, time));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SheetSharingViewModel(app.Sheets, app.Templates, app.Vaults, picker, null!, printer, time));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SheetSharingViewModel(app.Sheets, app.Templates, app.Vaults, picker, pictures, null!, time));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SheetSharingViewModel(app.Sheets, app.Templates, app.Vaults, picker, pictures, printer, null!));
        Assert.HasCount(6, new SheetSharingViewModel(app.Sheets, app.Templates, app.Vaults, picker, pictures, printer, time).Choices);
    }
}

/// <summary>Un sélecteur de dossiers qui fait quelque chose pendant qu'il est ouvert.</summary>
internal sealed class CallingPicker(Func<string?> answer) : IFolderPicker
{
    public string? Pick(string title) => answer();
}

/// <summary>Une mise en page pour le PDF, qui répond ce qu'on lui dit, et quand.</summary>
internal sealed class FakePrinter : ISheetPrinter
{
    public byte[]? Result { get; set; } = Encoding.ASCII.GetBytes("%PDF-essai");

    public string? Page { get; private set; }

    public TaskCompletionSource? Gate { get; set; }

    public async Task<byte[]?> PrintAsync(string page, CancellationToken cancellation)
    {
        Page = page;
        if (Gate is { } gate)
        {
            await gate.Task.ConfigureAwait(true);
        }
        return cancellation.IsCancellationRequested ? null : Result;
    }
}

/// <summary>Un coffre, en clair ou chiffré, avec la fiche « Mitose », et la fenêtre de partage dessus.</summary>
internal sealed class SharingScene : IDisposable
{
    private const string Body = "Le **texte** de la fiche, et $x^2$.\n\n![carte](carte.png)\n";

    /// <summary>CLIPBRD_E_CANT_OPEN : ce que WPF lève quand un autre programme tient le presse-papiers.</summary>
    private const int ClipboardHeld = unchecked((int)0x800401D0);

    private readonly FakeTime _time = new(new DateTimeOffset(2026, 11, 8, 8, 0, 0, TimeSpan.Zero));

    public SharingScene(bool encrypted = false)
    {
        Vaults = new TestVaults(AppMode.Development, _time);
        Root = Vaults.PathOf("Coffre");
        if (encrypted)
        {
            Vaults.CreateEncryptedVault(Root);
            Assert.AreEqual(Root, App.Vaults.Unlock(Root, VaultSecret.Password(TestVaults.Password), remember: false).Vault);
        }
        else
        {
            Assert.AreEqual(Root, App.Vaults.Create(Root).Vault);
        }
        SheetId = App.Sheets.Create("Biologie", "Mitose", null).Value;
        Assert.IsTrue(App.Editing.Save(SheetId, Body, App.Sheets.Open(SheetId).Value.Fingerprint, 1).Succeeded);
        Outside = Vaults.Folder("Dehors");
        Sharing = new SheetSharingViewModel(App.Sheets, App.Templates, App.Vaults, Vaults.Picker, new SharedSheetPictures(null), Printer, _time, Copy);
    }

    public TestVaults Vaults { get; }

    public KyfRev.Application.UseCases.KyfRevApplication App => Vaults.App;

    public string Root { get; }

    public string SheetId { get; }

    public string Outside { get; }

    public FakePrinter Printer { get; } = new();

    public SheetSharingViewModel Sharing { get; }

    public List<(string Page, string Text, bool IsPrivate)> Copies { get; } = [];

    public bool CopyFails { get; init; }

    /// <summary>Une image dans le coffre.</summary>
    public void WriteImage(string relative) => File.WriteAllBytes(Path.Combine(Root, "fiches", relative), [137, 80, 78, 71]);

    public void Open() => Sharing.RequestShare(SheetId, "Mitose");

    /// <summary>Choisit le format, répond au dossier, et lance le partage, sans attendre qu'il finisse.</summary>
    public Task Start(ShareKind kind, string? folder)
    {
        Sharing.Choices.Single(choice => choice.Kind == kind).IsChecked = true;
        if (kind != ShareKind.Clipboard)
        {
            Vaults.Picker.Answers.Enqueue(folder);
        }
        return Sharing.AnswerShareCommand.ExecuteAsync(true);
    }

    /// <summary>Le partage, jusqu'au bout.</summary>
    public void Answer(ShareKind kind, string? folder)
    {
        var running = Start(kind, folder);
        UiThread.Until(() => running.IsCompleted);
        running.GetAwaiter().GetResult();
    }

    public void Dispose() => Vaults.Dispose();

    private void Copy(string page, string text, bool isPrivate)
    {
        if (CopyFails)
        {
            throw Marshal.GetExceptionForHR(ClipboardHeld)!;
        }
        Copies.Add((page, text, isPrivate));
    }
}
