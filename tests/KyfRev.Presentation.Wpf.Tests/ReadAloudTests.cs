using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using KyfRev.Application;
using KyfRev.Application.Ports;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Écouter une fiche : le lecteur sur des voix simulées, les blocs lus, et la page.</summary>
[TestClass]
public sealed class ReadAloudTests
{
    private static readonly string[] Blocks = ["Un.", "Deux.", "Trois."];

    private static readonly string[] Hortense = ["Hortense"];

    [TestMethod]
    public void Opening_prepares_the_reader_without_reading()
    {
        var speech = new FakeSpeech();
        var reader = new ReadAloudViewModel(speech);

        reader.Open(Blocks);

        Assert.IsTrue(reader.IsOpen);
        Assert.IsFalse(reader.IsPlaying);
        Assert.AreEqual("– / 3", reader.Progress);
        Assert.AreEqual(ReadingText.ListenPlay, reader.PlayLabel);
        Assert.AreEqual(Icons.Play, reader.PlayGlyph);
        Assert.AreEqual("Vitesse : Normale", reader.RateLabel);
        Assert.AreEqual("Voix : Hortense", reader.VoiceLabel);
        Assert.IsEmpty(speech.Said);
        Assert.IsFalse(reader.HasMessage);
    }

    [TestMethod]
    public async Task It_reads_block_after_block_then_stops()
    {
        var speech = new FakeSpeech();
        var reader = new ReadAloudViewModel(speech);
        reader.Open(Blocks);

        reader.PlayPauseCommand.Execute(null);
        Assert.AreEqual("Un.", speech.Said.Last().Text);
        Assert.AreEqual(0, reader.Current);
        Assert.AreEqual("1 / 3", reader.Progress);
        Assert.AreEqual(ReadingText.ListenPause, reader.PlayLabel);
        Assert.AreEqual(Icons.Pause, reader.PlayGlyph);

        await speech.FinishAsync();
        Assert.AreEqual("Deux.", speech.Said.Last().Text);
        Assert.AreEqual("2 / 3", reader.Progress);

        await speech.FinishAsync();
        await speech.FinishAsync();
        Assert.IsFalse(reader.IsPlaying);
        Assert.IsNull(reader.Current);
        Assert.AreEqual("– / 3", reader.Progress);
    }

    [TestMethod]
    public void Pause_and_resume_go_through_the_voice()
    {
        var speech = new FakeSpeech();
        var reader = new ReadAloudViewModel(speech);
        reader.Open(Blocks);
        reader.PlayPauseCommand.Execute(null);

        reader.PlayPauseCommand.Execute(null);
        Assert.IsTrue(reader.IsPaused);
        Assert.AreEqual(1, speech.Pauses);
        Assert.AreEqual(ReadingText.ListenResume, reader.PlayLabel);
        Assert.AreEqual(Icons.Play, reader.PlayGlyph);

        reader.PlayPauseCommand.Execute(null);
        Assert.IsFalse(reader.IsPaused);
        Assert.AreEqual(1, speech.Resumes);
        Assert.HasCount(1, speech.Said, "le bloc reprend sans repartir");
    }

    [TestMethod]
    public void Moving_to_a_neighbour_block_reads_it_and_interrupts_the_other()
    {
        var speech = new FakeSpeech();
        var reader = new ReadAloudViewModel(speech);
        reader.Open(Blocks);
        Assert.IsFalse(reader.NextCommand.CanExecute(null), "rien hors lecture");
        Assert.IsFalse(reader.PreviousCommand.CanExecute(null));
        Assert.IsFalse(reader.StopCommand.CanExecute(null));
        reader.PlayPauseCommand.Execute(null);
        Assert.IsFalse(reader.PreviousCommand.CanExecute(null), "pas avant le premier");
        Assert.IsTrue(reader.NextCommand.CanExecute(null));
        var first = speech.Said[0].Cancellation;

        reader.NextCommand.Execute(null);
        Assert.IsTrue(first.IsCancellationRequested);
        Assert.AreEqual("Deux.", speech.Said.Last().Text);

        reader.NextCommand.Execute(null);
        Assert.IsFalse(reader.NextCommand.CanExecute(null), "pas après le dernier");

        reader.PreviousCommand.Execute(null);
        Assert.AreEqual("Deux.", speech.Said.Last().Text);
        Assert.AreEqual(1, reader.Current);
    }

    [TestMethod]
    public async Task An_interrupted_block_does_not_continue_the_reading()
    {
        var speech = new FakeSpeech();
        var reader = new ReadAloudViewModel(speech);
        reader.Open(Blocks);
        reader.PlayPauseCommand.Execute(null);

        reader.StopCommand.Execute(null);
        await speech.FinishAsync();

        Assert.HasCount(1, speech.Said);
        Assert.IsFalse(reader.IsPlaying);
        Assert.IsNull(reader.Current);
    }

    [TestMethod]
    public void Changing_the_rate_or_the_voice_reads_the_block_again()
    {
        var speech = new FakeSpeech("Hortense", "Julie");
        var reader = new ReadAloudViewModel(speech);
        reader.Open(Blocks);

        reader.Rate = ReadAloudViewModel.Rates[3];
        Assert.IsEmpty(speech.Said, "hors lecture, le réglage attend");

        reader.PlayPauseCommand.Execute(null);
        reader.NextCommand.Execute(null);
        Assert.AreEqual(1.5, speech.Said.Last().Rate);

        reader.Rate = ReadAloudViewModel.Rates[0];
        Assert.AreEqual(("Deux.", 0.8), (speech.Said.Last().Text, speech.Said.Last().Rate));
        Assert.AreEqual("Vitesse : Lente", reader.RateLabel);

        reader.Voice = "Julie";
        Assert.AreEqual(("Deux.", "Julie"), (speech.Said.Last().Text, speech.Said.Last().Voice));
    }

    [TestMethod]
    public void Without_a_french_voice_it_says_how_to_get_one()
    {
        var speech = new FakeSpeech([]);
        var reader = new ReadAloudViewModel(speech);

        reader.Open(Blocks);
        Assert.AreEqual(ReadingText.NoFrenchVoice, reader.Message);
        Assert.AreEqual("Voix : aucune", reader.VoiceLabel);

        reader.PlayPauseCommand.Execute(null);
        Assert.IsFalse(reader.IsPlaying);
        Assert.IsEmpty(speech.Said);
    }

    [TestMethod]
    public void Without_anything_to_read_it_says_so()
    {
        var speech = new FakeSpeech();
        var reader = new ReadAloudViewModel(speech);

        reader.Open([]);
        Assert.AreEqual(ReadingText.NothingToListen, reader.Message);

        reader.PlayPauseCommand.Execute(null);
        Assert.IsFalse(reader.IsPlaying);
    }

    [TestMethod]
    public async Task A_voice_that_fails_stops_and_says_why()
    {
        var speech = new FakeSpeech();
        var reader = new ReadAloudViewModel(speech);
        reader.Open(Blocks);
        reader.PlayPauseCommand.Execute(null);

        await speech.FailAsync("pas de sortie audio");

        Assert.IsFalse(reader.IsPlaying);
        Assert.IsNull(reader.Current);
        StringAssert.Contains(reader.Message!, "pas de sortie audio", StringComparison.Ordinal);

        reader.PlayPauseCommand.Execute(null);
        Assert.IsNull(reader.Message, "une nouvelle lecture efface le message");
        Assert.IsTrue(reader.IsPlaying);
    }

    [TestMethod]
    public void Closing_stops_the_reading()
    {
        var speech = new FakeSpeech();
        var reader = new ReadAloudViewModel(speech);
        reader.Open(Blocks);
        reader.PlayPauseCommand.Execute(null);

        reader.CloseCommand.Execute(null);

        Assert.IsFalse(reader.IsOpen);
        Assert.IsFalse(reader.IsPlaying);
        Assert.IsTrue(speech.Said[0].Cancellation.IsCancellationRequested);
    }

    [TestMethod]
    public void A_voice_gone_since_last_time_gives_way_to_the_first()
    {
        var speech = new FakeSpeech("Hortense", "Julie");
        var reader = new ReadAloudViewModel(speech);
        reader.Open(Blocks);
        reader.Voice = "Julie";
        reader.Open(Blocks);
        Assert.AreEqual("Julie", reader.Voice, "le choix reste");

        speech.Voices = Hortense;
        reader.Open(Blocks);
        Assert.AreEqual("Hortense", reader.Voice);
    }

    [TestMethod]
    public void A_voice_is_called_by_its_short_name()
    {
        var reader = new ReadAloudViewModel(new FakeSpeech("Microsoft Hortense Desktop"));

        reader.Open(Blocks);

        Assert.AreEqual("Microsoft Hortense Desktop", reader.Voice, "la synthèse la connaît sous son nom entier");
        Assert.AreEqual("Voix : Hortense", reader.VoiceLabel);
    }

    [TestMethod]
    public void The_arguments_are_required()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ReadingText.ShortVoice(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ReadAloudViewModel(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ReadAloudViewModel(new FakeSpeech()).Open(null!));
    }

    [STATestMethod]
    public void Only_the_text_is_read_with_formulas_and_images_named()
    {
        using var ui = new UiThread();
        const string body = """
            # Partie

            Un paragraphe avec **du gras** et une $x^2$ formule.

            - Premier point
            - [x] Une tâche faite

            > Une citation.

            | Tête | Colonne |
            |---|---|
            | Case | Autre |

            ```
            code qui ne se lit pas
            ```

            ---

            ![Le schéma](https://example.com/a.png)
            """;
        using var vault = new ReadingVault(body, passage: false);
        var viewer = new SheetDocumentViewer { Reading = vault.Reading() };

        var spoken = viewer.SpokenBlocks().Select(block => block.Text).ToList();

        CollectionAssert.Contains(spoken, "Partie");
        CollectionAssert.Contains(spoken, "Un paragraphe avec du gras et une formule formule.");
        CollectionAssert.Contains(spoken, "Premier point");
        CollectionAssert.Contains(spoken, ReadingText.TaskDone + " Une tâche faite");
        CollectionAssert.Contains(spoken, "Une citation.");
        CollectionAssert.Contains(spoken, "Tête");
        CollectionAssert.Contains(spoken, "Autre");
        CollectionAssert.DoesNotContain(spoken, "code qui ne se lit pas");
        Assert.IsFalse(spoken.Any(text => text.Contains("Biologie", StringComparison.Ordinal)), "ce qui entoure le titre ne se lit pas");
    }

    [STATestMethod]
    public void A_viewer_without_a_sheet_has_nothing_to_read()
    {
        var viewer = new SheetDocumentViewer();

        Assert.IsEmpty(viewer.SpokenBlocks());
        viewer.MarkSpoken(null);
    }

    [STATestMethod]
    public void The_page_reads_with_the_block_marked_and_stops_when_leaving_reading()
    {
        using var ui = new UiThread();
        var (test, window, vault, page, speech) = Open();
        try
        {
            var toggle = Named<ToggleButton>(window, "ListenToggle");
            Assert.AreEqual(Visibility.Visible, toggle.Visibility);
            var bar = Named<WrapPanel>(window, "ListenBar");
            Assert.AreEqual(Visibility.Collapsed, bar.Visibility);

            Click(toggle);
            TestShell.Flush();
            Assert.AreEqual(Visibility.Visible, bar.Visibility);
            Assert.IsTrue(page.ReadAloud!.IsOpen);

            page.ReadAloud.PlayPauseCommand.Execute(null);
            TestShell.Flush();
            var viewer = Named<SheetDocumentViewer>(window, "SheetText");
            var marked = viewer.SpokenBlocks()[0].Block;
            Assert.AreNotEqual(DependencyProperty.UnsetValue, marked.ReadLocalValue(TextElement.BackgroundProperty), "le bloc lu ressort");

            page.ReadAloud.NextCommand.Execute(null);
            TestShell.Flush();
            Assert.AreEqual(DependencyProperty.UnsetValue, marked.ReadLocalValue(TextElement.BackgroundProperty), "il redevient comme avant");

            page.ReadAloud.StopCommand.Execute(null);
            TestShell.Flush();
            Assert.IsFalse(viewer.SpokenBlocks().Any(block => block.Block.ReadLocalValue(TextElement.BackgroundProperty) != DependencyProperty.UnsetValue), "plus rien ne ressort");

            page.ReadAloud.PlayPauseCommand.Execute(null);
            page.StartWritingCommand.Execute(null);
            UiThread.Until(() => page.Editor is not null);
            TestShell.Flush();
            Assert.IsFalse(page.ReadAloud.IsOpen, "écrire ferme le lecteur");
            Assert.IsTrue(speech.Said.Last().Cancellation.IsCancellationRequested);
            Assert.AreEqual(Visibility.Collapsed, toggle.Visibility);
            page.StopWritingCommand.Execute(null);
        }
        finally
        {
            window.Close();
            vault.Dispose();
        }
        GC.KeepAlive(test);
    }

    [STATestMethod]
    public void The_listen_button_shrinks_to_its_icon_when_room_lacks()
    {
        using var ui = new UiThread();
        var vault = new ReadingVault("# Mitose\n\nUn paragraphe.\n", passage: false);
        var page = vault.Page(speech: new FakeSpeech());
        var test = new TestShell();
        var view = new SheetPage { DataContext = page };
        var host = new Border { Width = 1000, Height = 700, Child = view };
        var window = new Window { ShowActivated = false, Left = -10000, Top = -10000, Width = 800, Height = 600, WindowStartupLocation = WindowStartupLocation.Manual, Content = host };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            page.LoadCommand.Execute(null);
            UiThread.Until(() => page.Reading is not null && !page.IsLoading);
            TestShell.Flush();
            var label = Named<TextBlock>(window, "ListenLabel");
            Assert.AreEqual(Visibility.Visible, label.Visibility);

            host.Width = 374;
            TestShell.Flush();

            Assert.AreEqual(Visibility.Collapsed, label.Visibility);
            Assert.AreEqual(ReadingText.Listen, System.Windows.Automation.AutomationProperties.GetName(Named<ToggleButton>(window, "ListenToggle")), "le bouton garde son nom");
        }
        finally
        {
            window.Close();
            vault.Dispose();
        }
    }

    [STATestMethod]
    public void A_revision_with_holes_is_not_read_aloud()
    {
        using var ui = new UiThread();
        var (test, window, vault, page, _) = Open();
        try
        {
            var toggle = Named<ToggleButton>(window, "ListenToggle");
            page.Holes.IsActive = true;
            page.IsReviseOn = true;
            TestShell.Flush();

            Assert.AreEqual(Visibility.Collapsed, toggle.Visibility, "la voix dirait ce qui est masqué");
        }
        finally
        {
            window.Close();
            vault.Dispose();
        }
        GC.KeepAlive(test);
    }

    [STATestMethod]
    public void The_choices_open_under_their_button_with_the_current_one_checked()
    {
        using var ui = new UiThread();
        var (test, window, vault, page, _) = Open("Hortense", "Julie");
        try
        {
            Click(Named<ToggleButton>(window, "ListenToggle"));
            TestShell.Flush();

            var rate = Named<Button>(window, "ListenRate");
            Click(rate);
            var rates = rate.ContextMenu!.Items.OfType<MenuItem>().ToList();
            Assert.HasCount(ReadAloudViewModel.Rates.Count, rates);
            Assert.IsTrue(rates.Single(item => (string)item.Header == ReadingText.RateNormal).IsChecked);
            rates[3].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            rate.ContextMenu.IsOpen = false;
            Assert.AreEqual(ReadAloudViewModel.Rates[3], page.ReadAloud!.Rate);

            var voice = Named<Button>(window, "ListenVoice");
            Click(voice);
            var voices = voice.ContextMenu!.Items.OfType<MenuItem>().ToList();
            Assert.HasCount(2, voices);
            voices[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            voice.ContextMenu.IsOpen = false;
            Assert.AreEqual("Julie", page.ReadAloud.Voice);

            Click(Named<ToggleButton>(window, "ListenToggle"));
            Assert.IsFalse(page.ReadAloud.IsOpen, "le bouton referme aussi");
        }
        finally
        {
            window.Close();
            vault.Dispose();
        }
        GC.KeepAlive(test);
    }

    private static (TestShell Test, ShellWindow Window, ReadingVault Vault, SheetPageViewModel Page, FakeSpeech Speech) Open(params string[] voices)
    {
        var vault = new ReadingVault("# Mitose\n\nPremier paragraphe.\n\nSecond paragraphe.\n\nTroisième paragraphe.\n", passage: false);
        var speech = voices.Length > 0 ? new FakeSpeech(voices) : new FakeSpeech();
        var page = vault.Page(speech: speech);
        var test = new TestShell();
        test.Shell.Navigate(new NavigationItem("fiche", "Fiche", Icons.Document, int.MaxValue, page));
        var window = new ShellWindow(test.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 1000, Height = 760, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        page.LoadCommand.Execute(null);
        UiThread.Until(() => page.Reading is not null && !page.IsLoading);
        TestShell.Flush();
        return (test, window, vault, page, speech);
    }

    private static void Click(ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    private static T Named<T>(DependencyObject root, string name)
        where T : FrameworkElement =>
        Descendants(root).OfType<T>().First(element => element.Name == name);

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>Des voix qui ne disent rien : l'essai décide quand un bloc est dit.</summary>
    private sealed class FakeSpeech(params string[] voices) : ISpeech
    {
        private readonly Queue<TaskCompletionSource<Outcome<Done>>> _waiting = new();

        public FakeSpeech()
            : this("Hortense")
        {
        }

        public IReadOnlyList<string> Voices { get; set; } = voices;

        public List<(string Text, string? Voice, double Rate, CancellationToken Cancellation)> Said { get; } = [];

        public int Pauses { get; private set; }

        public int Resumes { get; private set; }

        public IReadOnlyList<string> FrenchVoices() => Voices;

        public Task<Outcome<Done>> SpeakAsync(string text, string? voice, double rate, CancellationToken cancellation)
        {
            Said.Add((text, voice, rate, cancellation));
            var waiting = new TaskCompletionSource<Outcome<Done>>();
            _waiting.Enqueue(waiting);
            cancellation.Register(() => waiting.TrySetResult(Outcome.Done()));
            return waiting.Task;
        }

        public void PauseSpeaking() => Pauses++;

        public void ResumeSpeaking() => Resumes++;

        public async Task FinishAsync()
        {
            _waiting.Dequeue().TrySetResult(Outcome.Done());
            await Task.Yield();
        }

        public async Task FailAsync(string reason)
        {
            _waiting.Dequeue().TrySetResult(Outcome.Refused<Done>(reason));
            await Task.Yield();
        }
    }
}
