using System.IO;
using System.Text;
using KyfRev.Application.Model;

namespace KyfRev.Infrastructure.Windows.Tests;

/// <summary>Les préférences de ce poste (<c>preferences.json</c>), hors du coffre.</summary>
[TestClass]
public sealed class MachinePreferencesFileTests
{
    private static readonly MachinePreferences Chosen = new()
    {
        Theme = ThemePreference.Dark,
        Zoom = 1.25,
        Width = ReadingWidth.Full,
        TextSize = ReadingTextSize.ExtraLarge,
        LineSpacing = ReadingLineSpacing.Airy,
        Font = ReadingFont.Serif,
        FocusMode = true,
        MarkDefinedWords = false,
        ReducedMotion = true,
        HighContrast = true,
        LargerTargets = true,
        Voice = "Microsoft Hortense Desktop",
        Rate = 1.5,
    };

    [TestMethod]
    public void Nothing_kept_gives_the_defaults()
    {
        using var folder = new Folder();

        Assert.AreEqual(new MachinePreferences(), new MachinePreferencesFile(folder.Path).Load());
    }

    [TestMethod]
    [DataRow(ReadingFont.Sans, "sans")]
    [DataRow(ReadingFont.Serif, "serif")]
    [DataRow(ReadingFont.Handwriting, "manuscrite")]
    [DataRow(ReadingFont.Marker, "feutre")]
    [DataRow(ReadingFont.Typewriter, "machine")]
    public void The_font_of_the_sheets_is_kept_under_its_name(ReadingFont font, string name)
    {
        using var folder = new Folder();
        var store = new MachinePreferencesFile(folder.Path);
        Assert.IsTrue(store.Save(new MachinePreferences { Font = font }));

        StringAssert.Contains(File.ReadAllText(Path.Combine(folder.Path, "preferences.json")), $"\"police\": \"{name}\"", StringComparison.Ordinal);
        Assert.AreEqual(font, new MachinePreferencesFile(folder.Path).Load().Font);
    }

    [TestMethod]
    public void The_page_layout_is_kept_for_all_sheets_and_for_each_sheet_that_has_its_own()
    {
        using var folder = new Folder();
        var store = new MachinePreferencesFile(folder.Path);
        Assert.IsTrue(store.Save(new MachinePreferences()));
        Assert.DoesNotContain("miseEnPage", File.ReadAllText(Path.Combine(folder.Path, "preferences.json")), "rien ne s'écrit tant que rien n'est choisi");
        Assert.DoesNotContain("misesEnPage", File.ReadAllText(Path.Combine(folder.Path, "preferences.json")));

        var chosen = new MachinePreferences
        {
            Page = new PageLayout(PageFormat.A4, MarginCm: 1.27),
            SheetPages = SheetPageLayouts.None.With("f_2", new PageLayout(PageFormat.A5, Landscape: true, MarginCm: 1.5)).With("f_1", PageLayout.Default),
        };
        Assert.IsTrue(store.Save(chosen));

        Assert.AreEqual(chosen, new MachinePreferencesFile(folder.Path).Load());
        var written = File.ReadAllText(Path.Combine(folder.Path, "preferences.json"));
        StringAssert.Contains(written, "\"miseEnPage\": \"a4 portrait 1.27\"", StringComparison.Ordinal);
        StringAssert.Contains(written, "\"f_2\": \"a5 paysage 1.5\"", StringComparison.Ordinal);
        StringAssert.Contains(written, "\"f_1\": \"sans-fin\"", StringComparison.Ordinal);

        // Un fichier abîmé à cet endroit laisse la feuille sans fin, et garde le reste.
        File.WriteAllText(Path.Combine(folder.Path, "preferences.json"), "{\"format\": 1, \"zoom\": 1.25, \"miseEnPage\": 7, \"misesEnPage\": [\"a4\"]}");
        Assert.AreEqual(new MachinePreferences { Zoom = 1.25 }, new MachinePreferencesFile(folder.Path).Load());
    }

    [TestMethod]
    public void The_paper_the_menu_order_and_the_changed_shortcuts_are_kept()
    {
        using var folder = new Folder();
        var store = new MachinePreferencesFile(folder.Path);
        var chosen = new MachinePreferences
        {
            Paper = new PaperChoices(IsOn: true, Endless: PaperStyle.Lines, A4: PaperStyle.LargeSquares, A6: PaperStyle.Graph),
            MenuOrder = "fiches,aujourd-hui",
            Shortcuts = "imprimer=Ctrl+Shift+I",
        };
        Assert.IsTrue(store.Save(chosen));

        Assert.AreEqual(chosen, new MachinePreferencesFile(folder.Path).Load());
        var written = File.ReadAllText(Path.Combine(folder.Path, "preferences.json"));
        StringAssert.Contains(written, "\"papier\": true", StringComparison.Ordinal);
        StringAssert.Contains(written, "\"sans-fin\": \"lignes\"", StringComparison.Ordinal);
        StringAssert.Contains(written, "\"a4\": \"grands-carreaux\"", StringComparison.Ordinal);
        StringAssert.Contains(written, "\"a6\": \"millimetre\"", StringComparison.Ordinal);
        StringAssert.Contains(written, "\"menu\": \"fiches,aujourd-hui\"", StringComparison.Ordinal);

        // Rien de choisi : rien d'écrit. Un papier inconnu est une feuille unie.
        Assert.IsTrue(store.Save(new MachinePreferences()));
        Assert.DoesNotContain("papier", File.ReadAllText(Path.Combine(folder.Path, "preferences.json")));
        Assert.DoesNotContain("raccourcis", File.ReadAllText(Path.Combine(folder.Path, "preferences.json")));
        File.WriteAllText(Path.Combine(folder.Path, "preferences.json"), "{\"format\": 1, \"papier\": true, \"papiers\": {\"a5\": \"velin\", \"a4\": 3}, \"menu\": 7}");
        Assert.AreEqual(new MachinePreferences { Paper = new PaperChoices(IsOn: true) }, new MachinePreferencesFile(folder.Path).Load());
    }

    [TestMethod]
    public void The_preferences_are_kept_and_read_back()
    {
        using var folder = new Folder();
        var store = new MachinePreferencesFile(Path.Combine(folder.Path, "v1"));

        Assert.IsTrue(store.Save(Chosen));

        Assert.AreEqual(Chosen, store.Load());
        Assert.AreEqual(Chosen, new MachinePreferencesFile(Path.Combine(folder.Path, "v1")).Load(), "au prochain démarrage");
        StringAssert.Contains(File.ReadAllText(Path.Combine(folder.Path, "v1", "preferences.json")), "\"theme\": \"sombre\"", StringComparison.Ordinal);
        StringAssert.Contains(File.ReadAllText(Path.Combine(folder.Path, "v1", "preferences.json")), "\"motsDefinis\": false", StringComparison.Ordinal);
        Assert.IsTrue(new MachinePreferences().MarkDefinedWords, "les mots définis se marquent tant qu'on ne l'a pas coupé");
    }

    [TestMethod]
    [DataRow(ThemePreference.System, ReadingTextSize.Normal)]
    [DataRow(ThemePreference.Light, ReadingTextSize.Large)]
    public void Each_choice_keeps_its_name(ThemePreference theme, ReadingTextSize size)
    {
        using var folder = new Folder();
        var store = new MachinePreferencesFile(folder.Path);
        var preferences = new MachinePreferences { Theme = theme, TextSize = size };

        Assert.IsTrue(store.Save(preferences));

        Assert.AreEqual(preferences, store.Load());
        Assert.DoesNotContain("\"voix\"", File.ReadAllText(Path.Combine(folder.Path, "preferences.json")), "pas de voix choisie, pas de voix écrite");
    }

    [TestMethod]
    [DataRow("pas du json")]
    [DataRow("[1, 2]")]
    public void An_unreadable_file_gives_the_defaults(string content)
    {
        using var folder = new Folder();
        File.WriteAllText(Path.Combine(folder.Path, "preferences.json"), content, Encoding.UTF8);

        Assert.AreEqual(new MachinePreferences(), new MachinePreferencesFile(folder.Path).Load());
    }

    [TestMethod]
    public void An_unknown_value_gives_its_default_without_touching_the_others()
    {
        using var folder = new Folder();
        File.WriteAllText(
            Path.Combine(folder.Path, "preferences.json"),
            """{ "theme": "fuchsia", "zoom": -3, "largeur": 12, "taille": "grande", "animationsReduites": "oui", "contrasteEleve": true, "voix": 4, "vitesse": "vite" }""",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var preferences = new MachinePreferencesFile(folder.Path).Load();

        Assert.AreEqual(new MachinePreferences { TextSize = ReadingTextSize.Large, HighContrast = true }, preferences);
    }

    [TestMethod]
    public void A_folder_that_cannot_be_written_says_so()
    {
        using var folder = new Folder();
        var file = Path.Combine(folder.Path, "un-fichier");
        File.WriteAllText(file, "x", Encoding.UTF8);

        Assert.IsFalse(new MachinePreferencesFile(file).Save(Chosen));
    }

    [TestMethod]
    public void The_arguments_are_required()
    {
        using var folder = new Folder();
        Assert.ThrowsExactly<ArgumentNullException>(() => new MachinePreferencesFile(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new MachinePreferencesFile(folder.Path).Save(null!));
    }

    private sealed class Folder : IDisposable
    {
        public string Path { get; } = Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kyfrev-preferences", Guid.NewGuid().ToString("N"))).FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
