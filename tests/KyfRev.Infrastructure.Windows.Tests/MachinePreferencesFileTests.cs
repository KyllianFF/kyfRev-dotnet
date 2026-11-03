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
    public void The_preferences_are_kept_and_read_back()
    {
        using var folder = new Folder();
        var store = new MachinePreferencesFile(Path.Combine(folder.Path, "v1"));

        Assert.IsTrue(store.Save(Chosen));

        Assert.AreEqual(Chosen, store.Load());
        Assert.AreEqual(Chosen, new MachinePreferencesFile(Path.Combine(folder.Path, "v1")).Load(), "au prochain démarrage");
        StringAssert.Contains(File.ReadAllText(Path.Combine(folder.Path, "v1", "preferences.json")), "\"theme\": \"sombre\"", StringComparison.Ordinal);
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
