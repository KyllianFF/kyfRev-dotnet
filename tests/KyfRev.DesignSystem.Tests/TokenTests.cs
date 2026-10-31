using KyfRev.DesignSystem.Tokens;

namespace KyfRev.DesignSystem.Tests;

/// <summary>
/// Les jetons : gammes construites autour des quatre couleurs fournies, contraste de chaque paire
/// de rôles employée dans chaque thème (WCAG 2.1), échelles régulières.
/// </summary>
[TestClass]
public sealed class TokenTests
{
    [TestMethod]
    public void The_four_provided_colors_keep_their_place()
    {
        Assert.AreEqual("#7f4ea8", Palette.Violet[600].ToString());
        Assert.AreEqual("#9c93e5", Palette.Periwinkle[400].ToString());
        Assert.AreEqual("#ba8cbe", Palette.Mauve[400].ToString());
        Assert.AreEqual("#c7a3d2", Palette.Mauve[300].ToString());
    }

    [TestMethod]
    public void Each_ramp_goes_from_light_to_dark()
    {
        foreach (var ramp in new[] { Palette.Violet, Palette.Periwinkle, Palette.Mauve })
        {
            var luminances = ColorRamp.Shades.Select(shade => ramp[shade].RelativeLuminance).ToList();
            CollectionAssert.AreEqual(luminances.OrderDescending().ToList(), luminances, ramp.Name);
        }
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Palette.Violet[650]);
    }

    [TestMethod]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void Every_pair_of_roles_reaches_its_contrast(ThemeKind theme)
    {
        var colors = ThemeColors.For(theme);
        CollectionAssert.AreEquivalent(Enum.GetValues<ColorRole>(), colors.Keys.ToList());
        var failures = ContrastRequirements.All
            .Select(pair => (pair, ratio: colors[pair.Foreground].ContrastWith(colors[pair.Background])))
            .Where(check => check.ratio < check.pair.Minimum)
            .Select(check => $"{check.pair.Foreground} sur {check.pair.Background} : {check.ratio:0.00} < {check.pair.Minimum}")
            .ToList();
        Assert.IsEmpty(failures, string.Join('\n', failures));
    }

    [TestMethod]
    public void Every_role_but_the_decorative_ones_is_checked()
    {
        var checkedRoles = ContrastRequirements.All.SelectMany(pair => new[] { pair.Foreground, pair.Background }).ToHashSet();
        var exempt = Enum.GetValues<ColorRole>().Where(role => !checkedRoles.Contains(role)).ToList();
        CollectionAssert.AreEquivalent(ContrastRequirements.Decorative.ToList(), exempt);
        Assert.HasCount(ContrastRequirements.All.Length, ContrastRequirements.All.Distinct());
    }

    [TestMethod]
    public void Control_borders_fix_the_former_1_29_to_1()
    {
        var light = ThemeColors.For(ThemeKind.Light);
        Assert.IsLessThan(1.5, light[ColorRole.Divider].ContrastWith(light[ColorRole.Surface]));
        Assert.IsGreaterThanOrEqualTo(3.0, light[ColorRole.ControlBorder].ContrastWith(light[ColorRole.SurfaceAlt]));
        Assert.AreEqual(5.90, Math.Round(Palette.Violet[600].ContrastWith(light[ColorRole.OnPrimary]), 2));
    }

    [TestMethod]
    public void High_contrast_uses_only_the_pairs_windows_guarantees()
    {
        (SystemColor, SystemColor)[] guaranteed =
        [
            (SystemColor.WindowText, SystemColor.Window),
            (SystemColor.HighlightText, SystemColor.Highlight),
            (SystemColor.HotTrack, SystemColor.Window),
            (SystemColor.Highlight, SystemColor.Window),
        ];
        foreach (var pair in ContrastRequirements.All)
        {
            var used = (ThemeColors.HighContrast(pair.Foreground), ThemeColors.HighContrast(pair.Background));
            CollectionAssert.Contains(guaranteed, used, $"{pair.Foreground} sur {pair.Background}");
        }
        Assert.ThrowsExactly<ArgumentException>(() => ThemeColors.For(ThemeKind.HighContrast));
        foreach (var color in Enum.GetValues<SystemColor>())
        {
            Assert.AreEqual(byte.MaxValue, ThemeColors.Current(color).A, color.ToString());
        }
    }

    [TestMethod]
    public void Colors_are_written_and_compared_as_in_wcag()
    {
        Assert.AreEqual(new TokenColor(255, 0x7f, 0x4e, 0xa8), TokenColor.FromHex("#7f4ea8"));
        Assert.AreEqual(new TokenColor(0x73, 0x22, 0x1d, 0x2e), TokenColor.FromHex("#73221d2e"));
        Assert.AreEqual("#73221d2e", TokenColor.FromHex("#73221d2e").ToString());
        Assert.AreEqual(21.0, TokenColor.FromHex("#000000").ContrastWith(TokenColor.FromHex("#ffffff")), 1e-9);
        Assert.AreEqual(1.0, Palette.Violet[600].ContrastWith(Palette.Violet[600]), 1e-9);
        Assert.AreEqual(System.Windows.Media.Color.FromArgb(0x73, 0x22, 0x1d, 0x2e), TokenColor.FromHex("#73221d2e").ToColor());
        foreach (var wrong in new[] { "7f4ea8", "#7f4ea", "#7f4ea8z", "#gggggg", "" })
        {
            Assert.ThrowsExactly<FormatException>(() => TokenColor.FromHex(wrong), wrong);
        }
        Assert.ThrowsExactly<ArgumentNullException>(() => TokenColor.FromHex(null!));
        Assert.ThrowsExactly<InvalidOperationException>(() => Palette.Light.Scrim.ContrastWith(Palette.Light.Surface));
    }

    [TestMethod]
    public void Scales_are_regular()
    {
        Assert.IsTrue(Spacing.Scale.All(space => space % Spacing.Unit == 0));
        Assert.IsTrue(Typography.Ramp.All(style => style.LineHeight % Spacing.Unit == 0 && style.LineHeight > style.Size));
        CollectionAssert.AreEqual(Typography.Ramp.Select(style => style.Size).Order().ToList(), Typography.Ramp.Select(style => style.Size).ToList());
        Assert.HasCount(Typography.Ramp.Length, Typography.Ramp.Select(style => style.Name).Distinct());
        Assert.IsTrue(new[] { Radii.Control, Radii.Card, Radii.Dialog }.All(radius => radius % Spacing.Unit == 0));
        Assert.AreEqual(System.Windows.FontWeights.SemiBold, Typography.Title.Weight);
        Assert.AreEqual(System.Windows.FontWeights.Normal, Typography.Body.Weight);
        CollectionAssert.AreEqual(new double[] { 17, 19, 21 }, Typography.ReadingSizes.ToList());
        Assert.IsGreaterThan(Elevation.Flyout.BlurRadius, Elevation.Dialog.BlurRadius);
    }

    [TestMethod]
    public void Animations_last_150_ms_at_most_and_none_when_reduced()
    {
        Assert.IsLessThanOrEqualTo(TimeSpan.FromMilliseconds(150), Motion.Normal);
        Assert.IsLessThan(Motion.Normal, Motion.Fast);
        Assert.AreEqual(TimeSpan.Zero, Motion.Effective(Motion.Normal, reducedMotion: true));
        Assert.AreEqual(Motion.Fast, Motion.Effective(Motion.Fast, reducedMotion: false));
        Assert.AreEqual(!System.Windows.SystemParameters.ClientAreaAnimation, Motion.SystemPrefersReducedMotion);
    }
}
