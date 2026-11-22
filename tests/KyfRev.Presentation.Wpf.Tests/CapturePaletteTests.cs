using System.Windows;
using System.Windows.Input;
using KyfRev.Application;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// La palette de capture rapide (rangée 25B) : ce qu'elle propose selon ce qu'on tape et selon le coffre, ce que fait chaque
/// choix, et sa fenêtre au clavier.
/// </summary>
[TestClass]
public sealed class CapturePaletteTests
{
    private static readonly CaptureChoiceKind[] WithText = [CaptureChoiceKind.Inbox, CaptureChoiceKind.NewSheet, CaptureChoiceKind.NewNote, CaptureChoiceKind.Search, CaptureChoiceKind.Revise, CaptureChoiceKind.Open];
    private static readonly CaptureChoiceKind[] WithoutText = [CaptureChoiceKind.Revise, CaptureChoiceKind.Open];
    private static readonly CaptureChoiceKind[] OnlyOpen = [CaptureChoiceKind.Open];
    private static readonly bool[] Closed = [false];
    private static readonly bool[] SaidDone = [true];

    [STATestMethod]
    public void Without_a_vault_or_with_a_locked_one_it_says_so_and_only_opens_kyfrev()
    {
        using var ui = new UiThread();
        var shell = new TestShell().Shell;
        shell.Capture = _ => Outcome.Of("f_boite");
        var shown = 0;
        var palette = new CapturePaletteViewModel(shell) { ShowApplication = () => shown++ };
        var finished = new List<bool>();
        palette.Finished = finished.Add;

        palette.Open();
        Assert.IsTrue(palette.IsLocked);
        Assert.AreEqual(CapturePaletteText.NoVault, palette.LockedMessage);
        CollectionAssert.AreEqual(OnlyOpen, palette.Choices.Select(choice => choice.Kind).ToArray());

        // Chiffré et verrouillé : la page de déverrouillage est montrée.
        shell.ShowUnlock(new object());
        palette.Open();
        Assert.AreEqual(CapturePaletteText.Locked, palette.LockedMessage);
        palette.Text = "Une idée";
        CollectionAssert.AreEqual(OnlyOpen, palette.Choices.Select(choice => choice.Kind).ToArray(), "rien ne s'écrit derrière un déverrouillage");
        palette.RunSelectedCommand.Execute(null);
        Assert.AreEqual(1, shown, "Entrée ouvre kyfRev, pour le déverrouiller");
        CollectionAssert.AreEqual(Closed, finished);

        // Déverrouillé : la palette le sait à sa prochaine ouverture.
        shell.ShowWorkspace();
        palette.Open();
        Assert.IsFalse(palette.IsLocked);
        Assert.IsNull(palette.LockedMessage);
    }

    [STATestMethod]
    public void What_is_typed_can_be_noted_created_searched_and_the_choices_follow_the_text()
    {
        using var ui = new UiThread();
        var shell = new TestShell().Shell;
        var notes = new List<string>();
        var created = new List<(string Title, bool Note)>();
        string? refusal = null;
        shell.Capture = text =>
        {
            notes.Add(text);
            return refusal is null ? Outcome.Of("f_boite") : Outcome.Refused<string>(refusal);
        };
        shell.ShowWorkspace();
        var shown = 0;
        var palette = new CapturePaletteViewModel(shell, (title, note) =>
        {
            created.Add((title, note));
            return Outcome.Of("f_nouvelle");
        }, () => 9)
        { ShowApplication = () => shown++ };
        var finished = new List<bool>();
        palette.Finished = finished.Add;

        // Rien de tapé : réviser, ouvrir ; le nombre du jour arrive à côté.
        palette.Open();
        Assert.IsFalse(palette.IsLocked);
        CollectionAssert.AreEqual(WithoutText, palette.Choices.Select(choice => choice.Kind).ToArray());
        UiThread.Until(() => palette.Choices[0].Hint == CapturePaletteText.DueOf(9));
        Assert.AreEqual("rien à réviser", CapturePaletteText.DueOf(0));

        // Un texte : noter, créer, chercher, puis aller.
        palette.Text = "  Relire le chapitre 3 ";
        CollectionAssert.AreEqual(WithText, palette.Choices.Select(choice => choice.Kind).ToArray());
        Assert.AreEqual(CaptureChoiceKind.Inbox, palette.Selected!.Kind, "Entrée note dans la Boîte de réception");
        Assert.AreEqual(CapturePaletteText.InboxOf("Relire le chapitre 3"), palette.Selected.Title);
        Assert.AreEqual($"{palette.Selected.Title}, {CapturePaletteText.Enter}", palette.Selected.ToString());
        Assert.AreEqual(CapturePaletteText.Write, palette.Choices[0].Group);
        Assert.AreEqual(CapturePaletteText.Go, palette.Choices[^1].Group);

        // Les flèches tournent.
        palette.Move(-1);
        Assert.AreEqual(CaptureChoiceKind.Open, palette.Selected!.Kind);
        palette.Move(1);
        Assert.AreEqual(CaptureChoiceKind.Inbox, palette.Selected!.Kind);

        // Un refus garde le texte, et le dit.
        refusal = "le coffre ne répond pas.";
        palette.RunSelectedCommand.Execute(null);
        UiThread.Until(() => palette.HasError);
        Assert.AreEqual(refusal, palette.Error);
        Assert.IsTrue(palette.HasError);
        Assert.IsEmpty(finished);
        palette.Text += " ";
        Assert.IsNull(palette.Error, "on réécrit : le refus s'efface");

        // Noté : la palette le dit, puis se ferme.
        refusal = null;
        palette.RunSelectedCommand.Execute(null);
        UiThread.Until(() => palette.HasDone);
        Assert.AreEqual("Relire le chapitre 3", notes[^1]);
        Assert.AreEqual(CapturePaletteText.Noted, palette.Done);
        Assert.IsTrue(palette.HasDone);
        CollectionAssert.AreEqual(SaidDone, finished);

        // Ctrl+Entrée : une fiche à ce titre ; la note se crée depuis son choix.
        palette.Open();
        palette.Text = "Mitose";
        palette.RunNewSheetCommand.Execute(null);
        UiThread.Until(() => palette.HasDone);
        Assert.AreEqual(("Mitose", false), created[^1]);
        Assert.AreEqual(CapturePaletteText.CreatedOf("Mitose", note: false), palette.Done);
        palette.Open();
        palette.Text = "Mitose";
        palette.RunCommand.Execute(palette.Choices.Single(choice => choice.Kind == CaptureChoiceKind.NewNote));
        UiThread.Until(() => palette.HasDone);
        Assert.AreEqual(("Mitose", true), created[^1]);
        Assert.AreEqual(CapturePaletteText.CreatedOf("Mitose", note: true), palette.Done);

        // Chercher ramène kyfRev, avec la recherche ouverte sur ce texte.
        palette.Open();
        palette.Text = "mitose";
        palette.RunCommand.Execute(palette.Choices.Single(choice => choice.Kind == CaptureChoiceKind.Search));
        Assert.IsTrue(shell.IsPaletteOpen);
        Assert.AreEqual("mitose", shell.PaletteQuery);

        // Réviser ramène kyfRev sur « Aujourd'hui ».
        palette.Open();
        palette.RunCommand.Execute(palette.Choices.Single(choice => choice.Kind == CaptureChoiceKind.Revise));
        Assert.AreEqual(CorePages.Today, shell.Current.Id);
        Assert.AreEqual(2, shown, "chercher, réviser : kyfRev revient");
        Assert.AreEqual(2, finished.Count(done => !done), "et la palette se ferme aussitôt");
    }

    [STATestMethod]
    public void Ctrl_shift_n_opens_the_palette_once_kyfrev_has_one()
    {
        var shell = new TestShell().Shell;
        var opened = 0;
        shell.ShowCapturePalette = () => opened++;
        shell.ShowHelp();

        Assert.IsTrue(shell.OpenCapture(), "même sans coffre : la palette le dira");
        Assert.AreEqual(1, opened);
        Assert.IsFalse(shell.IsCaptureOpen, "l'ancienne fenêtre ne s'ouvre plus");
        Assert.IsFalse(shell.IsHelpOpen);
        Assert.IsFalse(shell.IsLockedOut);
    }

    [STATestMethod]
    public void The_window_answers_the_keys_and_hides()
    {
        using var ui = new UiThread();
        var test = new TestShell();
        var shell = test.Shell;
        var notes = new List<string>();
        shell.Capture = text =>
        {
            notes.Add(text);
            return Outcome.Of("f_boite");
        };
        shell.ShowWorkspace();
        var palette = new CapturePaletteViewModel(shell, (_, _) => Outcome.Of("f_nouvelle"));
        var window = new CapturePaletteWindow(palette) { Left = -10000, Top = -10000 };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        try
        {
            window.ShowNear(null);
            TestShell.Flush();
            Assert.IsTrue(window.IsVisible);
            Assert.IsFalse(window.ShowInTaskbar);
            Assert.IsTrue(window.Topmost);

            palette.Text = "Une idée";
            TestShell.Flush();
            Press(window, Key.Down);
            Assert.AreEqual(CaptureChoiceKind.NewSheet, palette.Selected!.Kind);
            Press(window, Key.Up);
            Press(window, Key.Enter);
            UiThread.Until(() => palette.HasDone);
            Assert.AreEqual("Une idée", notes.Single());
            TestShell.Flush();
            Assert.IsTrue(window.IsVisible, "elle dit ce qui est fait avant de se cacher");
            UiThread.Until(() => !window.IsVisible);

            // Échap la cache, sans rien noter.
            window.ShowNear(null);
            palette.Text = "Autre chose";
            Press(window, Key.Escape);
            Assert.IsFalse(window.IsVisible);
            Assert.HasCount(1, notes);

            // Au-dessus de kyfRev à l'écran : centrée sur lui.
            var application = new Window { Left = 100, Top = 100, Width = 1200, Height = 800, ShowActivated = false, ShowInTaskbar = false };
            application.Show();
            try
            {
                window.ShowNear(application);
                Assert.AreEqual(100 + ((application.ActualWidth - window.Width) / 2), window.Left, 1);
                Assert.AreEqual(100 + (application.ActualHeight * 0.15), window.Top, 1);
            }
            finally
            {
                application.Close();
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static void Press(Window window, Key key) =>
        window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
}
