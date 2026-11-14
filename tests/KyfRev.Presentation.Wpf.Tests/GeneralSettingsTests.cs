using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.DesignSystem.Controls;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>L'onglet « Général » et ce qu'il commande : l'apparence, le confort de lecture et la voix, retenus sur ce poste.</summary>
[TestClass]
public sealed class GeneralSettingsTests
{
    private static readonly double[] ZoomsWithTheKeyboardOne = [0.9, 1, 1.1, 1.2, 1.25, 1.5];

    [TestMethod]
    public void The_settings_need_their_store_and_their_change()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new MachineSettings(null!));
        var machine = new MachineSettings(new MemoryMachinePreferences());
        Assert.ThrowsExactly<ArgumentNullException>(() => machine.Update(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new GeneralSettingsViewModel(null!, new FakeVoices()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new GeneralSettingsViewModel(machine, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ReadingComfort.Of(null!));
    }

    [STATestMethod]
    public void Following_needs_the_appearance_and_the_shell()
    {
        var machine = new MachineSettings(new MemoryMachinePreferences());
        var test = new TestShell();
        Assert.ThrowsExactly<ArgumentNullException>(() => machine.Follow(null!, test.Shell));
        Assert.ThrowsExactly<ArgumentNullException>(() => machine.Follow(test.Appearance, null!));
    }

    [TestMethod]
    public void A_change_is_kept_once_and_said()
    {
        var store = new MemoryMachinePreferences();
        var machine = new MachineSettings(store);
        var changes = 0;
        machine.Changed += (_, _) => changes++;

        Assert.IsTrue(machine.Update(preferences => preferences with { Width = ReadingWidth.Full }));
        Assert.IsTrue(machine.Update(preferences => preferences with { Width = ReadingWidth.Full }), "rien de nouveau");

        Assert.AreEqual(1, changes);
        Assert.AreEqual(1, store.Saves);
        Assert.AreEqual(ReadingWidth.Full, store.Stored.Width);
        Assert.AreEqual(ReadingWidth.Full, new MachineSettings(store).Current.Width, "au prochain démarrage");
    }

    [TestMethod]
    public void A_change_that_cannot_be_kept_still_applies()
    {
        var store = new MemoryMachinePreferences { Refuses = true };
        var machine = new MachineSettings(store);

        Assert.IsFalse(machine.Update(preferences => preferences with { Font = ReadingFont.Serif }));

        Assert.AreEqual(ReadingFont.Serif, machine.Current.Font);
    }

    [STATestMethod]
    public void What_is_kept_applies_at_startup_and_at_each_change()
    {
        var store = new MemoryMachinePreferences
        {
            Stored = new MachinePreferences { Theme = ThemePreference.Dark, Zoom = 1.25, TextSize = ReadingTextSize.ExtraLarge },
        };
        var machine = new MachineSettings(store);
        var test = new TestShell();

        machine.Follow(test.Appearance, test.Shell);

        Assert.AreEqual(ThemeKind.Dark, test.Design.Theme);
        Assert.AreEqual(1.25, test.Shell.Zoom);
        Assert.AreEqual(ReadingComfort.Pixels(16), ((ReadingComfort)test.Design.Dictionary[ReadingComfort.ResourceKey]).Size, 1e-9);

        machine.Update(preferences => preferences with { HighContrast = true, ReducedMotion = true, LargerTargets = true, Zoom = 9, LineSpacing = ReadingLineSpacing.Airy });

        Assert.AreEqual(ThemeKind.HighContrast, test.Design.Theme);
        Assert.IsTrue(test.Design.ReducedMotion);
        Assert.IsTrue(test.Design.LargerTargets);
        Assert.AreEqual(ShellViewModel.MaximumZoom, test.Shell.Zoom, "dans les limites du zoom");
        Assert.AreEqual(1.9, ((ReadingComfort)test.Design.Dictionary[ReadingComfort.ResourceKey]).LineSpacing);

        machine.Update(preferences => preferences with { HighContrast = false, ReducedMotion = false });

        Assert.AreEqual(ThemeKind.Dark, test.Design.Theme);
        Assert.IsFalse(test.Design.ReducedMotion);
    }

    [STATestMethod]
    public void The_zoom_changed_with_the_keyboard_is_kept()
    {
        var store = new MemoryMachinePreferences();
        var machine = new MachineSettings(store);
        var test = new TestShell();
        machine.Follow(test.Appearance, test.Shell);

        test.Shell.ZoomIn();

        Assert.AreEqual(1.1, store.Stored.Zoom);
        Assert.AreEqual(1.1, machine.Current.Zoom);
    }

    [STATestMethod]
    public void The_focus_mode_is_kept_on_this_computer()
    {
        var store = new MemoryMachinePreferences { Stored = new MachinePreferences { FocusMode = true } };
        var machine = new MachineSettings(store);
        var test = new TestShell();

        machine.Follow(test.Appearance, test.Shell);

        Assert.IsTrue(test.Shell.IsFocusMode, "retrouvé au démarrage");

        test.Shell.ToggleFocusMode();

        Assert.IsFalse(machine.Current.FocusMode);
        Assert.IsFalse(store.Stored.FocusMode);
        Assert.IsFalse(test.Shell.IsFocusMode);
    }

    [STATestMethod]
    public void The_menu_keeps_its_size_in_every_view()
    {
        var test = new TestShell();
        var shell = test.Shell;
        shell.WindowWidth = 1600;

        foreach (var item in shell.MainNavigation)
        {
            shell.Navigate(item);
            Assert.AreEqual(NavigationLayout.Sidebar, shell.Layout, item.Label);
        }
        shell.Navigate(new NavigationItem("fiche", "Fiche", Icons.Document, int.MaxValue, new object()));

        Assert.AreEqual(NavigationLayout.Sidebar, shell.Layout, "une fiche ouverte aussi : un menu qui bouge sous le clic en choisit une autre entrée");
    }

    [TestMethod]
    public void The_tab_shows_what_is_kept()
    {
        var store = new MemoryMachinePreferences
        {
            Stored = new MachinePreferences
            {
                Theme = ThemePreference.Light,
                Zoom = 1.25,
                Width = ReadingWidth.Full,
                TextSize = ReadingTextSize.Large,
                LineSpacing = ReadingLineSpacing.Airy,
                Font = ReadingFont.Serif,
                MarkDefinedWords = false,
                ReducedMotion = true,
                HighContrast = true,
                LargerTargets = true,
                Voice = "Microsoft Paul Desktop",
                Rate = 1.5,
            },
        };

        var page = new GeneralSettingsViewModel(new MachineSettings(store), new FakeVoices("Microsoft Hortense Desktop", "Microsoft Paul Desktop"));

        Assert.AreEqual("Clair", page.Theme.Label);
        Assert.AreEqual("125 %", page.Size!.Label);
        Assert.AreEqual("Grande (14)", page.TextSize.Label);
        Assert.AreEqual("Aéré", page.LineSpacing.Label);
        Assert.AreEqual("Avec empattements (Georgia)", page.Font.Label);
        Assert.IsTrue(page.ReducedMotion);
        Assert.IsTrue(page.HighContrast);
        Assert.IsTrue(page.LargerTargets);
        Assert.AreEqual("Paul", page.Voice!.Label);
        Assert.AreEqual(ReadingText.RateFast, page.Rate.Label);
        Assert.AreEqual(ReadingComfort.Pixels(14), page.PreviewSize, 1e-9);
        Assert.AreEqual(ReadingComfort.Pixels(14) * 1.9, page.PreviewLineHeight, 0.001);
        Assert.IsTrue(page.PreviewIsSerif);
        Assert.IsFalse(page.MarkDefinedWords);
        Assert.AreEqual(0, store.Saves, "montrer n'écrit rien");
    }

    [TestMethod]
    public void Each_choice_is_kept_at_once()
    {
        var store = new MemoryMachinePreferences();
        var page = new GeneralSettingsViewModel(new MachineSettings(store), new FakeVoices("Microsoft Hortense Desktop", "Microsoft Paul Desktop"));

        page.Theme = GeneralSettingsViewModel.Themes[2];
        page.Size = page.Sizes[2];
        page.TextSize = GeneralSettingsViewModel.TextSizes[2];
        page.LineSpacing = GeneralSettingsViewModel.LineSpacings[1];
        page.Font = GeneralSettingsViewModel.Fonts[1];
        page.MarkDefinedWords = false;
        page.ReducedMotion = true;
        page.HighContrast = true;
        page.LargerTargets = true;
        page.Voice = page.Voices[1];
        page.Rate = GeneralSettingsViewModel.Rates[0];

        Assert.AreEqual(
            new MachinePreferences
            {
                Theme = ThemePreference.Dark,
                Zoom = 1.1,
                TextSize = ReadingTextSize.ExtraLarge,
                LineSpacing = ReadingLineSpacing.Airy,
                Font = ReadingFont.Serif,
                MarkDefinedWords = false,
                ReducedMotion = true,
                HighContrast = true,
                LargerTargets = true,
                Voice = "Microsoft Paul Desktop",
                Rate = 0.8,
            },
            store.Stored);
        Assert.IsFalse(page.IsNotRemembered);

        page.Size = null;
        page.Voice = null;
        Assert.AreEqual(1.1, store.Stored.Zoom, "une liste vidée ne choisit rien");
        Assert.AreEqual("Microsoft Paul Desktop", store.Stored.Voice);
    }

    [TestMethod]
    public void A_choice_that_cannot_be_kept_says_so_until_the_tab_is_opened_again()
    {
        var store = new MemoryMachinePreferences { Refuses = true };
        var page = new GeneralSettingsViewModel(new MachineSettings(store), new FakeVoices());

        page.ReducedMotion = true;
        Assert.IsTrue(page.IsNotRemembered);

        page.OnNavigatedTo();
        Assert.IsFalse(page.IsNotRemembered);
    }

    [TestMethod]
    public void Coming_back_shows_what_changed_elsewhere()
    {
        var machine = new MachineSettings(new MemoryMachinePreferences());
        var page = new GeneralSettingsViewModel(machine, new FakeVoices("Hortense"));
        Assert.AreEqual("100 %", page.Size!.Label);

        machine.Update(preferences => preferences with { Zoom = 1.2, Rate = 1.37 });
        page.OnNavigatedTo();

        Assert.AreEqual("120 %", page.Size!.Label, "un zoom réglé au clavier a sa place dans la liste");
        CollectionAssert.AreEqual(ZoomsWithTheKeyboardOne, page.Sizes.Select(size => size.Value).ToArray());
        Assert.AreEqual(ReadingText.RateNormal, page.Rate.Label, "une vitesse inconnue se lit normale");
        Assert.AreEqual(1.37, machine.Current.Rate, "sans rien écrire");
    }

    [TestMethod]
    public void Without_a_french_voice_the_tab_says_so()
    {
        var page = new GeneralSettingsViewModel(new MachineSettings(new MemoryMachinePreferences()), new FakeVoices());

        Assert.IsTrue(page.HasNoVoice);
        Assert.IsFalse(page.HasVoices);
        Assert.IsNull(page.Voice);
    }

    [TestMethod]
    public void A_voice_no_longer_installed_gives_way_to_the_first_one()
    {
        var store = new MemoryMachinePreferences { Stored = new MachinePreferences { Voice = "Partie" } };

        var page = new GeneralSettingsViewModel(new MachineSettings(store), new FakeVoices("Hortense"));

        Assert.AreEqual("Hortense", page.Voice!.Value);
        Assert.AreEqual("Partie", store.Stored.Voice, "elle reviendra si elle est réinstallée");
    }

    [TestMethod]
    public void The_reader_starts_on_what_is_kept_and_keeps_what_is_chosen()
    {
        var store = new MemoryMachinePreferences { Stored = new MachinePreferences { Voice = "Paul", Rate = 1.2 } };
        var machine = new MachineSettings(store);
        var speech = new FakeVoices("Hortense", "Paul");

        var reader = new ReadAloudViewModel(speech, machine);
        reader.Open(["Un bloc."]);

        Assert.AreEqual("Paul", reader.Voice);
        Assert.AreEqual(1.2, reader.Rate.Value);
        Assert.AreEqual(0, store.Saves);

        reader.Voice = "Hortense";
        reader.Rate = ReadAloudViewModel.Rates[3];

        Assert.AreEqual("Hortense", store.Stored.Voice);
        Assert.AreEqual(1.5, store.Stored.Rate);
    }

    [STATestMethod]
    public void The_text_of_a_sheet_follows_the_comfort_and_a_link_stays_underlined()
    {
        using var ui = new UiThread();
        var sheet = new OpenedSheet("f_1", "Mitose", "fiches/Mitose.md", "", [], "# Grand\n\n#### Petit\n\nUn [lien](https://exemple.org) ici.\n\n- Une puce\n", 1, "sha256:essai", null, DateTimeOffset.UnixEpoch, null!, []);
        var reading = SheetReading.Of(sheet, _ => Outcome.Refused<byte[]>("non"));

        var document = new DocumentBuilder(reading, _ => { }, opensLinks: true, showsHeader: false, comfort: new ReadingComfort(21, 1.9, new FontFamily("Georgia"))).Build();

        Assert.AreEqual(21.0, document.FontSize);
        Assert.AreEqual(21 * 1.9, document.LineHeight, 0.001);
        Assert.AreEqual("Georgia", document.FontFamily.Source);
        var small = document.Blocks.OfType<Paragraph>().ElementAt(1);
        Assert.AreEqual(21.0, small.FontSize, "le plus petit titre a la taille du texte");
        Assert.AreEqual(FontStyles.Italic, small.FontStyle, "et l'italique le distingue");
        Assert.AreEqual(21.0 * 20 / 12, document.Blocks.OfType<Paragraph>().First().FontSize, 1e-9, "le titre 1 vaut 20 points pour un corps de 12, et grandit avec le texte");
        var link = document.Blocks.OfType<Paragraph>().ElementAt(2).Inlines.OfType<Hyperlink>().Single();
        Assert.AreEqual(24 * 21 / ReadingComfort.Default.Size, document.Blocks.OfType<List>().Single().Padding.Left, 0.001, "la puce grandit avec le texte");
        var window = new Window { Content = new FlowDocumentScrollViewer { Document = document }, Left = -10000, Top = -10000, Width = 600, Height = 400, ShowActivated = false };
        window.Show();
        TestShell.Flush();
        Assert.IsTrue(link.TextDecorations.Any(decoration => decoration.Location == TextDecorationLocation.Underline), "un lien se voit sans sa couleur : pas besoin de réglage");
        window.Close();

        var usual = new DocumentBuilder(reading, _ => { }, opensLinks: true, showsHeader: false).Build();

        Assert.AreEqual(16.0, usual.FontSize, "12 points");
        Assert.AreEqual(16 * KyfRev.DesignSystem.Tokens.Typography.ReadingLineSpacing, usual.LineHeight, 0.001);
    }

    [TestMethod]
    public void The_comfort_says_what_the_preferences_say()
    {
        Assert.AreEqual(ReadingComfort.Default, ReadingComfort.Of(new MachinePreferences()));
        Assert.AreEqual(ReadingComfort.Pixels(14), ReadingComfort.Of(new MachinePreferences { TextSize = ReadingTextSize.Large }).Size, 1e-9);
        Assert.AreEqual(ReadingComfort.Pixels(16), ReadingComfort.Of(new MachinePreferences { TextSize = ReadingTextSize.ExtraLarge }).Size, 1e-9);
        Assert.AreEqual(1.9, ReadingComfort.Of(new MachinePreferences { LineSpacing = ReadingLineSpacing.Airy }).LineSpacing);
        Assert.AreEqual("Georgia", ReadingComfort.Of(new MachinePreferences { Font = ReadingFont.Serif }).Family!.Source);
    }

    [STATestMethod]
    public void The_tab_arranges_its_cards_and_never_scrolls_sideways()
    {
        using var ui = new UiThread();
        var test = new TestShell();
        var page = new GeneralSettingsPage { DataContext = new GeneralSettingsViewModel(new MachineSettings(new MemoryMachinePreferences()), new FakeVoices("Hortense")) };
        var host = new Border { Width = 1000, Height = 800, Child = page };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var canvas = new Canvas();
        canvas.Children.Add(host);
        var window = new Window { Content = canvas, Left = -10000, Top = -10000, Width = 800, Height = 800, ShowActivated = false };
        window.Show();
        TestShell.Flush();
        try
        {
            Assert.IsTrue(page.AppearanceCard.IsVisible && page.ReadingCard.IsVisible && page.VoiceCard.IsVisible, "hors de la vue Paramètres, toutes les cartes se montrent");
            Assert.IsGreaterThan(page.AppearanceCard.TranslatePoint(default, page).Y + page.AppearanceCard.ActualHeight - 1, page.AccessibilityCard.TranslatePoint(default, page).Y, "l'une sous l'autre");
            Assert.IsLessThanOrEqualTo(SettingsTab.Column + 0.5, page.AppearanceCard.ActualWidth, "la colonne ne dépasse pas sa largeur");
            // Chaque réglage est à droite de son nom, au même bord que les autres : le dernier choix du thème, les listes
            // et les bascules.
            double Right(FrameworkElement element) => element.TranslatePoint(new Point(element.ActualWidth, 0), page).X;
            var edge = Right(page.AppearanceCard) - 20;
            var lastTheme = (ListBoxItem)page.ThemeChoice.ItemContainerGenerator.ContainerFromIndex(2);
            Assert.AreEqual(edge, Right(lastTheme), 1);
            Assert.AreEqual(edge, Right(page.SizeChoice), 1);
            Assert.AreEqual(edge, Right(page.FontChoice), 1);
            Assert.AreEqual(edge, Right(page.HighContrastBox), 1);
            Assert.AreEqual(page.TextSizeChoice.ActualWidth, page.SizeChoice.ActualWidth, 0.5);
            Assert.IsGreaterThan(page.AppearanceCard.TranslatePoint(default, page).X + 240, page.SizeChoice.TranslatePoint(default, page).X, "le nom à gauche, le réglage à droite");

            // Le thème : trois boutons, tous visibles, dont celui en vigueur ; en choisir un autre vaut aussitôt.
            var model = (GeneralSettingsViewModel)page.DataContext;
            Assert.HasCount(3, page.ThemeChoice.Items);
            Assert.AreEqual(0, page.ThemeChoice.SelectedIndex);
            var width = lastTheme.ActualWidth;
            page.ThemeChoice.SelectedIndex = 2;
            TestShell.Flush();
            Assert.AreEqual(ThemePreference.Dark, model.Theme.Value);
            Assert.AreEqual(FontWeights.SemiBold, lastTheme.FontWeight, "le choix retenu est en gras");
            Assert.AreEqual(width, lastTheme.ActualWidth, 0.01, "sans que le bouton change de largeur");
            page.ThemeChoice.SelectedIndex = 0;

            // Un réglage qui vaut aussitôt est une bascule, pas une case : la piste, et son bouton qui passe à droite.
            var track = Descendants<Border>(page.HighContrastBox).Single(border => border.Name == "Track");
            var knob = Descendants<System.Windows.Shapes.Ellipse>(page.HighContrastBox).Single();
            Assert.AreEqual((44d, 24d), (track.ActualWidth, track.ActualHeight));
            Assert.AreEqual(HorizontalAlignment.Left, knob.HorizontalAlignment);
            page.HighContrastBox.IsChecked = true;
            TestShell.Flush();
            Assert.IsTrue(model.HighContrast);
            Assert.AreEqual(HorizontalAlignment.Right, knob.HorizontalAlignment);
            page.HighContrastBox.IsChecked = false;
            Assert.IsTrue(new[] { page.ReducedMotionBox, page.HighContrastBox, page.LargerTargetsBox, page.DefinedWordsBox }.All(box => Descendants<Border>(box).Any(border => border.Name == "Track")));
            Assert.AreEqual("100 %", page.SizeChoice.SelectionBoxItem.ToString(), "le libellé, pas l'objet");
            Assert.IsFalse(page.NoVoice.IsVisible, "une voix est installée");
            Assert.AreEqual("100 %", ((SettingChoice<double>)page.SizeChoice.SelectedItem).Label);
            Assert.IsLessThanOrEqualTo(page.Scroller.ViewportWidth, page.Scroller.ExtentWidth);

            host.Width = 430;
            TestShell.Flush();

            // En fenêtre étroite, une liste ou un choix passe sous son nom, calé à gauche ; une bascule, étroite, reste au
            // bout de sa rangée.
            var left = page.AppearanceCard.TranslatePoint(default, page).X + 20;
            Assert.AreEqual(left, page.SizeChoice.TranslatePoint(default, page).X, 1);
            Assert.AreEqual(left, page.ThemeChoice.TranslatePoint(default, page).X, 1);
            Assert.AreEqual(Right(page.AccessibilityCard) - 20, Right(page.HighContrastBox), 1);

            Assert.IsGreaterThan(page.ReadingCard.TranslatePoint(default, page).Y, page.PreviewCard.TranslatePoint(default, page).Y, "l'aperçu vient juste sous les réglages qu'il montre");
            Assert.IsLessThan(page.VoiceCard.TranslatePoint(default, page).Y, page.PreviewCard.TranslatePoint(default, page).Y);

            // Dans la vue Paramètres, une section ne montre que ses cartes.
            SettingsPart.SetCurrent(page, SettingsHubViewModel.ReadingPart);
            TestShell.Flush();
            Assert.IsFalse(page.AppearanceCard.IsVisible || page.ReadingCard.IsVisible || page.PreviewCard.IsVisible || page.AccessibilityCard.IsVisible);
            Assert.IsTrue(page.WordsCard.IsVisible && page.VoiceCard.IsVisible);
            SettingsPart.SetCurrent(page, SettingsHubViewModel.AppearancePart);
            TestShell.Flush();
            Assert.IsTrue(page.AppearanceCard.IsVisible && page.ReadingCard.IsVisible && page.PreviewCard.IsVisible && page.AccessibilityCard.IsVisible, "le texte des fiches et son aperçu sont dans Apparence");
            Assert.IsFalse(page.WordsCard.IsVisible || page.VoiceCard.IsVisible);
            Assert.IsLessThanOrEqualTo(page.Scroller.ViewportWidth, page.Scroller.ExtentWidth);
        }
        finally
        {
            window.Close();
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T element)
            {
                yield return element;
            }
            foreach (var nested in Descendants<T>(child))
            {
                yield return nested;
            }
        }
    }
}
