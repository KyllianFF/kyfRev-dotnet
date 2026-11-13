using System.Globalization;
using System.Resources;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Textes de l'onglet « Général », lus dans les ressources (<c>Resources/GeneralSettingsText.resx</c>).</summary>
public static class GeneralSettingsText
{
    private static readonly ResourceManager Resources = new("KyfRev.Presentation.Wpf.Resources.GeneralSettingsText", typeof(GeneralSettingsText).Assembly);

    /// <summary>« Accessibilité ».</summary>
    public static string AccessibilityTitle => Text(nameof(AccessibilityTitle));

    /// <summary>« Apparence ».</summary>
    public static string AppearanceTitle => Text(nameof(AppearanceTitle));

    /// <summary>« Police des fiches ».</summary>
    public static string Font => Text(nameof(Font));

    /// <summary>« Marquer les mots définis dans toutes les fiches ».</summary>
    public static string DefinedWords => Text(nameof(DefinedWords));

    /// <summary>« Un terme défini dans une fiche est souligné d'un pointillé discret partout où il apparaît… ».</summary>
    public static string DefinedWordsHelp => Text(nameof(DefinedWordsHelp));

    /// <summary>« Capture rapide depuis n'importe quelle application (Ctrl+Alt+N) ».</summary>
    public static string GlobalCapture => Text(nameof(GlobalCapture));

    /// <summary>« Tant que kyfRev est ouvert… ».</summary>
    public static string GlobalCaptureHelp => Text(nameof(GlobalCaptureHelp));

    /// <summary>« Sans empattements ».</summary>
    public static string FontSans => Text(nameof(FontSans));

    /// <summary>« Avec empattements (Georgia) ».</summary>
    public static string FontSerif => Text(nameof(FontSerif));

    /// <summary>« Général ».</summary>
    public static string General => Text(nameof(General));

    /// <summary>« Contraste élevé ».</summary>
    public static string HighContrast => Text(nameof(HighContrast));

    /// <summary>« Les couleurs de contraste de Windows, même si ce mode y est désacti… ».</summary>
    public static string HighContrastHelp => Text(nameof(HighContrastHelp));

    /// <summary>« Taille de l'interface ».</summary>
    public static string InterfaceSize => Text(nameof(InterfaceSize));

    /// <summary>« Aussi Ctrl+plus, Ctrl+moins, Ctrl+0. ».</summary>
    public static string InterfaceSizeHelp => Text(nameof(InterfaceSizeHelp));

    /// <summary>« Agrandir les petits boutons ».</summary>
    public static string LargerTargets => Text(nameof(LargerTargets));

    /// <summary>« Une cible plus haute, plus facile à atteindre. ».</summary>
    public static string LargerTargetsHelp => Text(nameof(LargerTargetsHelp));

    /// <summary>« Retenus sur cet ordinateur, appliqués aussitôt. ».</summary>
    public static string MachineNote => Text(nameof(MachineNote));

    /// <summary>« Aucune voix française n'est installée sur cet ordinateur. ».</summary>
    public static string NoVoice => Text(nameof(NoVoice));

    /// <summary>« Ce réglage s'applique, mais n'a pas pu être retenu sur cet ordinate… ».</summary>
    public static string NotRemembered => Text(nameof(NotRemembered));

    /// <summary>« {0} % ».</summary>
    public static string PercentOf(object value) => Format("Percent", value);

    /// <summary>« Aperçu ».</summary>
    public static string Preview => Text(nameof(Preview));

    /// <summary>« La mitose partage une cellule en deux cellules filles identiques : … ».</summary>
    public static string PreviewText => Text(nameof(PreviewText));

    /// <summary>« Vitesse de lecture ».</summary>
    public static string Rate => Text(nameof(Rate));

    /// <summary>« Réduire les animations ».</summary>
    public static string ReducedMotion => Text(nameof(ReducedMotion));

    /// <summary>« Même si Windows ne le demande pas. ».</summary>
    public static string ReducedMotionHelp => Text(nameof(ReducedMotionHelp));

    /// <summary>« Interligne des fiches ».</summary>
    public static string Spacing => Text(nameof(Spacing));

    /// <summary>« Aéré ».</summary>
    public static string SpacingAiry => Text(nameof(SpacingAiry));

    /// <summary>« Normal ».</summary>
    public static string SpacingNormal => Text(nameof(SpacingNormal));

    /// <summary>« Taille du texte des fiches ».</summary>
    public static string TextSize => Text(nameof(TextSize));

    /// <summary>« Très grande (16) ».</summary>
    public static string TextSizeExtraLarge => Text(nameof(TextSizeExtraLarge));

    /// <summary>« Grande (14) ».</summary>
    public static string TextSizeLarge => Text(nameof(TextSizeLarge));

    /// <summary>« Normale (12) ».</summary>
    public static string TextSizeNormal => Text(nameof(TextSizeNormal));

    /// <summary>« Thème d'affichage ».</summary>
    public static string Theme => Text(nameof(Theme));

    /// <summary>« Sombre ».</summary>
    public static string ThemeDark => Text(nameof(ThemeDark));

    /// <summary>« Clair ».</summary>
    public static string ThemeLight => Text(nameof(ThemeLight));

    /// <summary>« Comme Windows ».</summary>
    public static string ThemeSystem => Text(nameof(ThemeSystem));

    /// <summary>« Voix de lecture ».</summary>
    public static string Voice => Text(nameof(Voice));

    /// <summary>« Largeur des fiches ».</summary>
    public static string Width => Text(nameof(Width));

    /// <summary>« Confortable ».</summary>
    public static string WidthComfortable => Text(nameof(WidthComfortable));

    /// <summary>« Toute la largeur ».</summary>
    public static string WidthFull => Text(nameof(WidthFull));

    private static string Format(string name, params object[] values) => string.Format(CultureInfo.CurrentCulture, Text(name), values);

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » introuvable.");
}
