using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Presentation.Wpf.Reading;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Une valeur proposée dans une liste de choix.</summary>
/// <param name="Value">La valeur.</param>
/// <param name="Label">Ce qu'en voit l'utilisateur.</param>
public sealed record SettingChoice<T>(T Value, string Label)
{
    /// <summary>Ce que dit le lecteur d'écran.</summary>
    public override string ToString() => Label;
}

/// <summary>L'onglet « Général » : l'apparence et l'accessibilité, retenues sur ce poste et appliquées aussitôt.</summary>
public sealed partial class GeneralSettingsViewModel : ObservableObject, INavigationAware
{
    private static readonly double[] CommonSizes = [0.9, 1, 1.1, 1.25, 1.5];

    private readonly MachineSettings _machine;
    private readonly ISpeech _speech;
    private bool _showing = true;

    /// <summary>L'onglet, sur les préférences de ce poste et ses voix.</summary>
    public GeneralSettingsViewModel(MachineSettings machine, ISpeech speech)
    {
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentNullException.ThrowIfNull(speech);
        _machine = machine;
        _speech = speech;
        Theme = Themes[0];
        Width = Widths[0];
        TextSize = TextSizes[0];
        LineSpacing = LineSpacings[0];
        Font = Fonts[0];
        Rate = Rates[1];
        Show();
        // Le thème se change aussi depuis la barre de titre, cet onglet ouvert.
        machine.Changed += (_, _) =>
        {
            if (Theme.Value != machine.Current.Theme)
            {
                _showing = true;
                Theme = Themes.First(choice => choice.Value == machine.Current.Theme);
                _showing = false;
            }
        };
    }

    /// <summary>Les thèmes.</summary>
    public static IReadOnlyList<SettingChoice<ThemePreference>> Themes { get; } =
    [
        new(ThemePreference.System, GeneralSettingsText.ThemeSystem),
        new(ThemePreference.Light, GeneralSettingsText.ThemeLight),
        new(ThemePreference.Dark, GeneralSettingsText.ThemeDark),
    ];

    /// <summary>Les largeurs du texte.</summary>
    public static IReadOnlyList<SettingChoice<ReadingWidth>> Widths { get; } =
    [
        new(ReadingWidth.Comfortable, GeneralSettingsText.WidthComfortable),
        new(ReadingWidth.Full, GeneralSettingsText.WidthFull),
    ];

    /// <summary>Les tailles du texte.</summary>
    public static IReadOnlyList<SettingChoice<ReadingTextSize>> TextSizes { get; } =
    [
        new(ReadingTextSize.Normal, GeneralSettingsText.TextSizeNormal),
        new(ReadingTextSize.Large, GeneralSettingsText.TextSizeLarge),
        new(ReadingTextSize.ExtraLarge, GeneralSettingsText.TextSizeExtraLarge),
    ];

    /// <summary>Les interlignes.</summary>
    public static IReadOnlyList<SettingChoice<ReadingLineSpacing>> LineSpacings { get; } =
    [
        new(ReadingLineSpacing.Normal, GeneralSettingsText.SpacingNormal),
        new(ReadingLineSpacing.Airy, GeneralSettingsText.SpacingAiry),
    ];

    /// <summary>Les polices.</summary>
    public static IReadOnlyList<SettingChoice<ReadingFont>> Fonts { get; } =
    [
        new(ReadingFont.Sans, GeneralSettingsText.FontSans),
        new(ReadingFont.Serif, GeneralSettingsText.FontSerif),
    ];

    /// <summary>Les vitesses de lecture.</summary>
    public static IReadOnlyList<ReadingRate> Rates => ReadAloudViewModel.Rates;

    /// <summary>Les tailles de l'interface, dont celle en vigueur si elle a été réglée au clavier.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SettingChoice<double>> Sizes { get; private set; } = [];

    /// <summary>Les voix françaises installées.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVoices), nameof(HasNoVoice))]
    public partial IReadOnlyList<SettingChoice<string>> Voices { get; private set; } = [];

    /// <summary>Au moins une voix française est installée.</summary>
    public bool HasVoices => Voices.Count > 0;

    /// <summary>Aucune voix française n'est installée.</summary>
    public bool HasNoVoice => Voices.Count == 0;

    /// <summary>Thème d'affichage.</summary>
    [ObservableProperty]
    public partial SettingChoice<ThemePreference> Theme { get; set; }

    /// <summary>Taille de l'interface.</summary>
    [ObservableProperty]
    public partial SettingChoice<double>? Size { get; set; }

    /// <summary>Largeur du texte des fiches.</summary>
    [ObservableProperty]
    public partial SettingChoice<ReadingWidth> Width { get; set; }

    /// <summary>Taille du texte des fiches.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewSize), nameof(PreviewLineHeight))]
    public partial SettingChoice<ReadingTextSize> TextSize { get; set; }

    /// <summary>Interligne du texte des fiches.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewLineHeight))]
    public partial SettingChoice<ReadingLineSpacing> LineSpacing { get; set; }

    /// <summary>Police du texte des fiches.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewIsSerif))]
    public partial SettingChoice<ReadingFont> Font { get; set; }

    /// <summary>Les mots définis se marquent dans toutes les fiches.</summary>
    [ObservableProperty]
    public partial bool MarkDefinedWords { get; set; }

    /// <summary>Le raccourci global de la capture rapide est demandé à Windows.</summary>
    [ObservableProperty]
    public partial bool GlobalCapture { get; set; }

    /// <summary>Animations réduites.</summary>
    [ObservableProperty]
    public partial bool ReducedMotion { get; set; }

    /// <summary>Contraste élevé.</summary>
    [ObservableProperty]
    public partial bool HighContrast { get; set; }

    /// <summary>Petits boutons agrandis.</summary>
    [ObservableProperty]
    public partial bool LargerTargets { get; set; }

    /// <summary>Voix de la lecture à voix haute.</summary>
    [ObservableProperty]
    public partial SettingChoice<string>? Voice { get; set; }

    /// <summary>Vitesse de la lecture à voix haute.</summary>
    [ObservableProperty]
    public partial ReadingRate Rate { get; set; }

    /// <summary>Le dernier réglage n'a pas pu être retenu sur ce poste.</summary>
    [ObservableProperty]
    public partial bool IsNotRemembered { get; private set; }

    /// <summary>Taille de l'aperçu.</summary>
    public double PreviewSize => Comfort().Size;

    /// <summary>Hauteur d'une ligne de l'aperçu.</summary>
    public double PreviewLineHeight => Comfort().Size * Comfort().LineSpacing;

    /// <summary>L'aperçu a des empattements.</summary>
    public bool PreviewIsSerif => Font.Value == ReadingFont.Serif;

    /// <inheritdoc/>
    public void OnNavigatedTo()
    {
        IsNotRemembered = false;
        Show();
    }

    private ReadingComfort Comfort() => ReadingComfort.Of(new MachinePreferences { TextSize = TextSize.Value, LineSpacing = LineSpacing.Value, Font = Font.Value });

    /// <summary>Montre ce qui est en vigueur, qui a pu changer au clavier ou dans le lecteur d'une fiche.</summary>
    private void Show()
    {
        _showing = true;
        var current = _machine.Current;
        Theme = Themes.First(choice => choice.Value == current.Theme);
        var sizes = CommonSizes.Contains(current.Zoom) ? CommonSizes : [.. CommonSizes.Append(current.Zoom).Order()];
        Sizes = [.. sizes.Select(size => new SettingChoice<double>(size, GeneralSettingsText.PercentOf(((int)Math.Round(size * 100)).ToString(CultureInfo.InvariantCulture))))];
        Size = Sizes.First(choice => choice.Value == current.Zoom);
        Width = Widths.First(choice => choice.Value == current.Width);
        TextSize = TextSizes.First(choice => choice.Value == current.TextSize);
        LineSpacing = LineSpacings.First(choice => choice.Value == current.LineSpacing);
        Font = Fonts.First(choice => choice.Value == current.Font);
        MarkDefinedWords = current.MarkDefinedWords;
        GlobalCapture = current.GlobalCapture;
        ReducedMotion = current.ReducedMotion;
        HighContrast = current.HighContrast;
        LargerTargets = current.LargerTargets;
        Voices = [.. _speech.FrenchVoices().Select(voice => new SettingChoice<string>(voice, ReadingText.ShortVoice(voice)))];
        Voice = Voices.FirstOrDefault(choice => choice.Value == current.Voice) ?? (Voices.Count > 0 ? Voices[0] : null);
        Rate = Rates.FirstOrDefault(rate => rate.Value == current.Rate) ?? Rates[1];
        _showing = false;
    }

    private void Remember(Func<MachinePreferences, MachinePreferences> change)
    {
        if (!_showing)
        {
            IsNotRemembered = !_machine.Update(change);
        }
    }

    partial void OnThemeChanged(SettingChoice<ThemePreference> value) => Remember(preferences => preferences with { Theme = value.Value });

    partial void OnSizeChanged(SettingChoice<double>? value)
    {
        if (value is not null)
        {
            Remember(preferences => preferences with { Zoom = value.Value });
        }
    }

    partial void OnWidthChanged(SettingChoice<ReadingWidth> value) => Remember(preferences => preferences with { Width = value.Value });

    partial void OnTextSizeChanged(SettingChoice<ReadingTextSize> value) => Remember(preferences => preferences with { TextSize = value.Value });

    partial void OnLineSpacingChanged(SettingChoice<ReadingLineSpacing> value) => Remember(preferences => preferences with { LineSpacing = value.Value });

    partial void OnFontChanged(SettingChoice<ReadingFont> value) => Remember(preferences => preferences with { Font = value.Value });

    partial void OnMarkDefinedWordsChanged(bool value) => Remember(preferences => preferences with { MarkDefinedWords = value });

    partial void OnGlobalCaptureChanged(bool value) => Remember(preferences => preferences with { GlobalCapture = value });

    partial void OnReducedMotionChanged(bool value) => Remember(preferences => preferences with { ReducedMotion = value });

    partial void OnHighContrastChanged(bool value) => Remember(preferences => preferences with { HighContrast = value });

    partial void OnLargerTargetsChanged(bool value) => Remember(preferences => preferences with { LargerTargets = value });

    partial void OnVoiceChanged(SettingChoice<string>? value)
    {
        if (value is not null)
        {
            Remember(preferences => preferences with { Voice = value.Value });
        }
    }

    partial void OnRateChanged(ReadingRate value) => Remember(preferences => preferences with { Rate = value.Value });
}
