using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KyfRev.DesignSystem.Tokens;

namespace KyfRev.DesignSystem.Tests;

/// <summary>
/// Les ressources : chaque clé que les styles nomment existe, les thèmes changent sans recharger
/// l'interface, aucune couleur n'est écrite en dur hors des jetons.
/// </summary>
[TestClass]
public sealed partial class ResourceTests
{
    private static readonly string[] UiFaces = ["Bold Italic", "Bold Normal", "Normal Italic", "Normal Normal", "SemiBold Italic", "SemiBold Normal"];
    private static readonly string[] CodeFaces = ["Normal Normal", "SemiBold Normal"];
    private static readonly string[] Licences = ["OFL-AtkinsonHyperlegibleNext.txt", "OFL-AtkinsonHyperlegibleMono.txt"];

    [STATestMethod]
    public void Every_key_named_by_the_styles_exists()
    {
        var resources = new DesignResources(ThemeKind.Light, reducedMotion: false).Dictionary;
        var xaml = Directory.GetFiles(Path.Combine(Repository.Root, "src", "KyfRev.DesignSystem", "Styles"), "*.xaml");
        Assert.HasCount(7, xaml);
        var keys = xaml.SelectMany(file => ResourceReference().Matches(File.ReadAllText(file)).Select(match => match.Groups["key"].Value)).ToHashSet(StringComparer.Ordinal);
        Assert.IsGreaterThan(40, keys.Count);
        var missing = keys.Where(key => !resources.Contains(key)).ToList();
        Assert.IsEmpty(missing, string.Join(", ", missing));
    }

    [STATestMethod]
    public void Each_role_has_a_color_and_a_frozen_brush()
    {
        var resources = new DesignResources(ThemeKind.Dark, reducedMotion: false).Dictionary;
        foreach (var role in Enum.GetValues<ColorRole>())
        {
            var brush = (SolidColorBrush)resources[DesignKeys.Brush(role)];
            Assert.IsTrue(brush.IsFrozen, role.ToString());
            Assert.AreEqual(ThemeColors.For(ThemeKind.Dark)[role].ToColor(), brush.Color, role.ToString());
            Assert.AreEqual(brush.Color, (Color)resources[DesignKeys.Color(role)]);
        }
        Assert.AreEqual(Typography.Body.Size, (double)resources[DesignKeys.FontSize(Typography.Body)]);
        Assert.AreEqual(Typography.Title.LineHeight, (double)resources[DesignKeys.LineHeight(Typography.Title)]);
        Assert.AreEqual(new CornerRadius(Radii.Card), (CornerRadius)resources[DesignKeys.CardRadius]);
        Assert.IsNotNull(resources[DesignKeys.Shadow(Elevation.Dialog)]);
        Assert.IsNull(resources[DesignKeys.Shadow(Elevation.Card)], "en thème sombre, une carte se détache par sa surface et son filet");
    }

    [STATestMethod]
    public void The_theme_changes_without_reloading_the_interface()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var button = new Button { Content = "Valider", Style = (Style)design.Dictionary["Kyf.Button.Primary"] };
        var host = Host.Of(design, button);

        Assert.AreEqual(Palette.Forest[700].ToColor(), ((SolidColorBrush)button.Background).Color);
        design.Theme = ThemeKind.Dark;
        Assert.AreEqual(ThemeKind.Dark, design.Theme);
        Assert.AreEqual(Palette.Forest[300].ToColor(), ((SolidColorBrush)button.Background).Color);
        design.Theme = ThemeKind.HighContrast;
        Assert.AreEqual(SystemColors.HighlightColor, ((SolidColorBrush)button.Background).Color);
        Assert.IsNull(design.Dictionary[DesignKeys.Shadow(Elevation.Flyout)]);
        Assert.AreSame(design.Dictionary, host.Resources);
    }

    [STATestMethod]
    public void The_atkinson_fonts_ship_with_their_licence()
    {
        // Les ressources « pack://application » se lisent une fois WPF initialisé, comme dans l'application.
        RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
        var resources = new DesignResources(ThemeKind.Light, reducedMotion: false).Dictionary;
        Assert.AreEqual(Typography.UiFamily, ((FontFamily)resources[DesignKeys.UiFont]).Source);
        Assert.AreEqual(Typography.CodeFamily, ((FontFamily)resources[DesignKeys.CodeFont]).Source);

        // Les obliques sont simulées par WPF : seuls comptent les fichiers livrés.
        static List<string> Faces(string family) =>
        [
            .. new FontFamily(Typography.FontsBase, $"./#{family}").GetTypefaces()
                .Where(typeface => typeface.Style != FontStyles.Oblique && typeface.TryGetGlyphTypeface(out _))
                .Select(typeface => $"{typeface.Weight} {typeface.Style}")
                .Order(StringComparer.Ordinal),
        ];
        CollectionAssert.AreEqual(UiFaces, Faces("Atkinson Hyperlegible Next"));
        CollectionAssert.AreEqual(CodeFaces, Faces("Atkinson Hyperlegible Mono"));
        foreach (var licence in Licences)
        {
            StringAssert.Contains(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fonts", licence)), "SIL Open Font License, Version 1.1", StringComparison.Ordinal);
        }
    }

    [STATestMethod]
    public void Reduced_motion_stops_every_animation()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        Assert.IsTrue((bool)design.Dictionary[DesignKeys.AnimationsEnabled]);
        Assert.AreEqual(new Duration(Motion.Normal), design.Dictionary[DesignKeys.NormalDuration]);

        design.ReducedMotion = true;

        Assert.IsTrue(design.ReducedMotion);
        Assert.IsFalse((bool)design.Dictionary[DesignKeys.AnimationsEnabled]);
        Assert.AreEqual(new Duration(TimeSpan.Zero), design.Dictionary[DesignKeys.FastDuration]);
        Assert.AreEqual(new Duration(TimeSpan.Zero), design.Dictionary[DesignKeys.NormalDuration]);
    }

    [STATestMethod]
    public void Larger_targets_replace_the_small_heights()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        Assert.IsFalse(design.LargerTargets);
        Assert.AreEqual(28.0, design.Dictionary[DesignKeys.CompactTarget]);
        Assert.AreEqual(30.0, design.Dictionary[DesignKeys.TabTarget]);

        design.LargerTargets = true;

        Assert.IsTrue(design.LargerTargets);
        Assert.AreEqual(40.0, design.Dictionary[DesignKeys.CompactTarget]);
        Assert.AreEqual(40.0, design.Dictionary[DesignKeys.TabTarget]);
    }

    /// <summary>
    /// Aucune couleur écrite en dur dans l'interface : les valeurs ne s'écrivent que dans
    /// <c>Tokens/Palette.cs</c>, et seul <c>Tokens/TokenColor.cs</c> fabrique une couleur WPF.
    /// </summary>
    [TestMethod]
    public void No_color_is_written_outside_the_tokens()
    {
        var src = Path.Combine(Repository.Root, "src");
        var files = Directory.GetDirectories(src)
            .Where(folder => Path.GetFileName(folder) is "KyfRev.DesignSystem" || Path.GetFileName(folder).StartsWith("KyfRev.Presentation", StringComparison.Ordinal) || Path.GetFileName(folder) is "KyfRev.App")
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
            .Where(file => file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".xaml", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(file => (Relative: Path.GetRelativePath(src, file).Replace('\\', '/'), Text: File.ReadAllText(file)))
            .ToList();
        Assert.IsGreaterThan(20, files.Count);
        var namedColors = typeof(Colors).GetProperties().Select(property => property.Name).Where(name => name != nameof(Colors.Transparent)).ToHashSet(StringComparer.Ordinal);
        foreach (var (relative, text) in files)
        {
            if (relative != "KyfRev.DesignSystem/Tokens/Palette.cs")
            {
                Assert.IsFalse(HexColor().IsMatch(text), $"Couleur écrite en dur dans {relative}");
            }
            if (relative != "KyfRev.DesignSystem/Tokens/TokenColor.cs")
            {
                Assert.IsFalse(MadeColor().IsMatch(text), $"Couleur fabriquée dans {relative}");
            }
            if (relative.EndsWith(".xaml", StringComparison.Ordinal))
            {
                var named = XamlValue().Matches(text).Select(match => match.Groups["value"].Value).Where(namedColors.Contains).ToList();
                Assert.IsEmpty(named, $"Couleur nommée dans {relative} : {string.Join(", ", named)}");
            }
        }
    }

    [TestMethod]
    public void The_design_system_depends_on_no_other_layer()
    {
        var ours = typeof(DesignResources).Assembly.GetReferencedAssemblies().Where(reference => reference.Name!.StartsWith("KyfRev.", StringComparison.Ordinal)).ToList();
        Assert.IsEmpty(ours, string.Join(", ", ours));
    }

    [GeneratedRegex(@"\{(?:Dynamic|Static)Resource (?<key>Kyf\.[A-Za-z.]+)\}")]
    private static partial Regex ResourceReference();

    [GeneratedRegex(@"#[0-9A-Fa-f]{6}(?:[0-9A-Fa-f]{2})?\b")]
    private static partial Regex HexColor();

    [GeneratedRegex(@"Color\.From(?:Rgb|Argb|ScRgb)|\bColors\.(?!Transparent)\w+|\bBrushes\.\w+")]
    private static partial Regex MadeColor();

    [GeneratedRegex(@"=""(?<value>[A-Za-z]+)""")]
    private static partial Regex XamlValue();
}
