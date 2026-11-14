using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Reading;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Les textes des onglets « Fiches » et « Raccourcis et menu » des paramètres.</summary>
public static class MoreSettingsText
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>Le groupe des réglages retenus sur l'ordinateur.</summary>
    public const string GroupMachine = "Sur cet ordinateur";

    /// <summary>Ce que dit la feuille sans fin, sous son nom.</summary>
    public const string FormatEndlessDetail = "sans pages";

    /// <summary>Les marges étroites, en un mot.</summary>
    public const string MarginsNarrowShort = "Étroites";

    /// <summary>Les marges normales, en un mot.</summary>
    public const string MarginsNormalShort = "Normales";

    /// <summary>Les marges larges, en un mot.</summary>
    public const string MarginsWideShort = "Larges";

    /// <summary>Le champ des marges sur mesure.</summary>
    public const string MarginsOwn = "Sur mesure";

    /// <summary>L'unité du champ des marges.</summary>
    public const string Centimetres = "cm";

    /// <summary>Ce que le champ des marges accepte.</summary>
    public const string MarginsOwnHelp = "En centimètres, de 0,5 à ce que la feuille permet. Entrée, ou quitter le champ, pour l'appliquer.";

    /// <summary>Le bouton qui remet toutes les fiches à ce réglage.</summary>
    public const string AlignOwnLayouts = "Les aligner sur ce réglage";

    /// <summary>Le bouton qui rend leur mise en page aux fiches alignées.</summary>
    public const string UndoAlign = "Annuler";

    /// <summary>L'interrupteur du papier.</summary>
    public const string PaperShown = "Montrer le papier";

    /// <summary>Ce que dit la carte du papier.</summary>
    public const string PaperMatrixHelp = "Un papier par type de feuille, à l'écran seulement : en lecture, en révision et en écriture. Il ne s'imprime jamais. Sur un papier réglé, le texte se pose sur les lignes.";

    /// <summary>La première colonne de la grille des papiers : les types de feuille.</summary>
    public const string PaperOfEndless = "Sans fin";

    /// <summary>La carte de l'aperçu.</summary>
    public const string PreviewTitle = "Aperçu";

    /// <summary>Le titre de la fiche de l'aperçu.</summary>
    public const string PreviewSheetTitle = "Les organites";

    /// <summary>Le texte de la fiche de l'aperçu.</summary>
    public const string PreviewSheetText = "Le noyau contient l'ADN. Les mitochondries produisent l'énergie de la cellule.\n\nLe réticulum endoplasmique fabrique les protéines, que l'appareil de Golgi trie et expédie vers leur destination.\n\nLes lysosomes digèrent ce qui n'a plus d'usage ; le cytosquelette donne sa forme à la cellule.";

    /// <summary>La carte du zoom.</summary>
    public const string ZoomTitle = "Zoom des fiches";

    /// <summary>Ce que règle le zoom.</summary>
    public const string ZoomHelp = "De 50 à 200 %, le même que le curseur en bas d'une fiche : il vaut pour les fiches ouvertes ensuite. Il ne change pas la taille de l'interface, réglée dans Apparence.";

    /// <summary>La carte de la police.</summary>
    public const string FontTitle = "Police d'écriture";

    /// <summary>Ce que règle la police.</summary>
    public const string FontHelp = "La police du texte des fiches, en lecture, en révision et en écriture. C'est le même réglage que « Police des fiches », dans Lecture.";

    /// <summary>Le bouton qui prend la police conseillée.</summary>
    public const string UseRecommendedFont = "Utiliser cette police";

    /// <summary>« 3 fiches gardent leur propre mise en page, choisie « À cette fiche ». ».</summary>
    public static string OwnLayouts(int count) => count == 1
        ? "1 fiche garde sa propre mise en page, choisie « À cette fiche »."
        : $"{count} fiches gardent leur propre mise en page, choisie « À cette fiche ».";

    /// <summary>« Les 3 fiches ont repris ce réglage. ».</summary>
    public static string Aligned(int count) => count == 1 ? "La fiche a repris ce réglage." : $"Les {count} fiches ont repris ce réglage.";

    /// <summary>« A4 portrait · marges de 2 cm · grands carreaux ».</summary>
    public static string PreviewCaption(string format, string? orientation, string? margin, string? paper) =>
        string.Join(" · ", new[]
        {
            orientation is null ? format : $"{format} {orientation.ToLower(French)}",
            margin is null ? null : $"marges de {margin} cm",
            paper is null ? "papier éteint" : paper.ToLower(French),
        }.Where(part => part is not null));

    /// <summary>« Conseillée pour les grands carreaux : Manuscrite (Segoe Print). ».</summary>
    public static string RecommendedFont(string paper, string font) => $"Conseillée pour le papier « {paper.ToLower(French)} » : {font}.";

    /// <summary>La police manuscrite.</summary>
    public const string FontHandwriting = "Manuscrite (Segoe Print)";

    /// <summary>La police au feutre.</summary>
    public const string FontMarker = "Au feutre (Ink Free)";

    /// <summary>La police à chasse fixe.</summary>
    public const string FontTypewriter = "À chasse fixe (machine à écrire)";

    /// <summary>Le champ de recherche de la vue.</summary>
    public const string SearchLabel = "Chercher un réglage";

    /// <summary>La recherche ne trouve rien.</summary>
    public const string NoResult = "Aucun réglage ne porte ce nom.";

    /// <summary>La liste des résultats.</summary>
    public const string ResultsName = "Réglages trouvés";

    /// <summary>Le bouton qui remet une section à ses valeurs d'origine.</summary>
    public const string RestoreSection = "Rétablir les réglages d'origine";

    /// <summary>Ce que le bouton fait, dit à l'avance.</summary>
    public const string RestoreSectionHelp = "Seulement les réglages de cette section. « Annuler » les remet tant que vous restez ici.";

    /// <summary>C'est fait.</summary>
    public const string Restored = "Réglages d'origine rétablis.";

    /// <summary>C'est fait, mais le fichier des préférences n'a pas pu être écrit.</summary>
    public const string RestoredNotKept = "Réglages d'origine rétablis, mais ils n'ont pas pu être retenus sur cet ordinateur.";

    /// <summary>Le bouton qui rend les réglages d'avant.</summary>
    public const string UndoRestore = "Annuler";

    /// <summary>Des mots qui mènent à la taille de l'interface.</summary>
    public const string WordsZoom = "zoom agrandir rétrécir";

    /// <summary>Des mots qui mènent à la police.</summary>
    public const string WordsFont = "manuscrite feutre chasse fixe empattements conseillée";

    /// <summary>Des mots qui mènent à la mise en page.</summary>
    public const string WordsLayout = "feuille format page A4 A5 A6 sans fin";

    /// <summary>Des mots qui mènent au papier.</summary>
    public const string WordsPaper = "carreaux lignes points millimétré uni cahier";

    /// <summary>Des mots qui mènent au chiffrement.</summary>
    public const string WordsEncryption = "chiffrer mot de passe clé de récupération verrouiller";

    /// <summary>Des mots qui mènent aux éléments supprimés.</summary>
    public const string WordsTrash = "corbeille restaurer";

    /// <summary>Des mots qui mènent à la version.</summary>
    public const string WordsVersion = "version mise à jour";

    /// <summary>Un résultat, pour le lecteur d'écran : « Thème d'affichage, dans Apparence ».</summary>
    public static string HitSpoken(string label, string where) => $"{label}, dans {where}";

    /// <summary>Le groupe des réglages du coffre ouvert.</summary>
    public const string GroupVault = "Dans ce coffre";

    /// <summary>Le groupe de l'application.</summary>
    public const string GroupApp = "kyfRev";

    /// <summary>La section du thème, de la taille, du texte des fiches et de l'accessibilité.</summary>
    public const string Appearance = "Apparence";

    /// <summary>Ce qu'on y règle.</summary>
    public const string AppearanceAbout = "Le thème, la taille de l'interface, le texte des fiches et l'accessibilité. Retenus sur cet ordinateur, appliqués aussitôt.";

    /// <summary>La section des mots définis et de la voix.</summary>
    public const string Reading = "Lecture";

    /// <summary>Ce qu'on y règle.</summary>
    public const string ReadingAbout = "Les mots définis et la lecture à voix haute. Retenus sur cet ordinateur, appliqués aussitôt.";

    /// <summary>La section des feuilles.</summary>
    public const string Sheets = "Feuilles";

    /// <summary>Ce qu'on y règle.</summary>
    public const string SheetsAbout = "Le format des fiches et le papier montré derrière le texte. Retenus sur cet ordinateur, appliqués aussitôt.";

    /// <summary>La section de l'ordre du menu.</summary>
    public const string Menu = "Menu";

    /// <summary>Ce qu'on y règle.</summary>
    public const string MenuAbout = "L'ordre des vues du menu de gauche. Retenu sur cet ordinateur, appliqué aussitôt.";

    /// <summary>La section des raccourcis clavier.</summary>
    public const string Shortcuts = "Raccourcis";

    /// <summary>Ce qu'on y règle.</summary>
    public const string ShortcutsAbout = "Les touches des actions courantes. Retenues sur cet ordinateur, appliquées aussitôt.";

    /// <summary>Ce qu'on règle dans « Révisions ».</summary>
    public const string RevisionsAbout = "Le calendrier des révisions et les rappels de consolidation de ce coffre. À enregistrer d'un bloc.";

    /// <summary>Ce qu'on règle dans « Modules et extensions ».</summary>
    public const string ModulesAbout = "Les modules de ce coffre, et les extensions installées.";

    /// <summary>La section du coffre et de son chiffrement.</summary>
    public const string VaultAndEncryption = "Coffre et chiffrement";

    /// <summary>Ce qu'on y fait.</summary>
    public const string VaultAbout = "Ouvrir ou créer un coffre, le chiffrer, le verrouiller.";

    /// <summary>La section des sauvegardes, de la corbeille et des exports.</summary>
    public const string BackupsAndExports = "Sauvegardes et exports";

    /// <summary>Ce qu'on y fait.</summary>
    public const string BackupsAbout = "Sauvegarder et restaurer ce coffre, retrouver ce qui a été supprimé, exporter.";

    /// <summary>Ce qu'on lit dans « À propos ».</summary>
    public const string AboutAbout = "La version, la licence et les mises à jour.";

    /// <summary>Le bloc du thème et de la taille de l'interface.</summary>
    public const string ThemeAndSizeTitle = "Thème et taille";

    /// <summary>Ce que l'on sait du thème.</summary>
    public const string ThemeHelp = "Le bouton de la barre de titre en change aussi.";

    /// <summary>Où s'applique la taille du texte.</summary>
    public const string TextSizeHelp = "En lecture, en révision et en écriture.";

    /// <summary>À quoi sert l'interligne.</summary>
    public const string SpacingHelp = "Aéré pour relire longtemps.";

    /// <summary>Ce que la police entraîne.</summary>
    public const string ReadingFontHelp = "Les titres la suivent quand elle est manuscrite.";

    /// <summary>Le bloc des mots définis.</summary>
    public const string DefinedWordsTitle = "Mots définis";

    /// <summary>D'où viennent les voix.</summary>
    public const string VoiceHelp = "Les voix françaises installées dans Windows.";

    /// <summary>Où s'applique la vitesse.</summary>
    public const string RateHelp = "Pour la lecture à voix haute d'une fiche.";

    /// <summary>La carte du texte des fiches.</summary>
    public const string ReadingTextTitle = "Texte des fiches";

    /// <summary>La carte de la voix.</summary>
    public const string VoiceTitle = "Lecture à voix haute";

    /// <summary>La carte de la capture rapide.</summary>
    public const string CaptureTitle = "Capture rapide";

    /// <summary>L'onglet des feuilles.</summary>
    public const string SheetsTab = "Fiches";

    /// <summary>L'onglet des raccourcis et du menu.</summary>
    public const string ShortcutsTab = "Raccourcis et menu";

    /// <summary>Ces réglages sont ceux de l'ordinateur.</summary>
    public const string MachineNote = "Ces réglages sont retenus sur cet ordinateur et s'appliquent aussitôt.";

    /// <summary>La carte de la mise en page.</summary>
    public const string LayoutTitle = "Mise en page de toutes les fiches";

    /// <summary>Ce que règle la mise en page.</summary>
    public const string LayoutHelp = "La feuille des fiches qui n'ont pas la leur. Une fiche garde sa propre mise en page quand on la règle « À cette fiche », dans l'onglet Mise en page de l'écriture.";

    /// <summary>Le format de la feuille.</summary>
    public const string Format = "Feuille";

    /// <summary>La feuille sans pages.</summary>
    public const string FormatEndless = "Sans fin";

    /// <summary>L'orientation.</summary>
    public const string Orientation = "Orientation";

    /// <summary>En hauteur.</summary>
    public const string Portrait = "Portrait";

    /// <summary>En largeur.</summary>
    public const string Landscape = "Paysage";

    /// <summary>Les marges.</summary>
    public const string Margins = "Marges";

    /// <summary>1,27 cm.</summary>
    public const string MarginsNarrow = "Étroites (1,27 cm)";

    /// <summary>2 cm.</summary>
    public const string MarginsNormal = "Normales (2 cm)";

    /// <summary>3 cm.</summary>
    public const string MarginsWide = "Larges (3 cm)";

    /// <summary>Des marges réglées dans l'écriture.</summary>
    public static string MarginsCustom(string centimetres) => $"Sur mesure ({centimetres} cm)";

    /// <summary>La carte du papier.</summary>
    public const string PaperTitle = "Papier derrière le texte";

    /// <summary>Ce qu'est le papier.</summary>
    public const string PaperHelp = "Un décor de l'écran, pour lire et écrire comme sur une feuille. Il ne s'imprime jamais. Le bouton à côté du zoom d'une fiche l'allume et l'éteint aussi.";

    /// <summary>La case qui montre le papier.</summary>
    public const string PaperOn = "Montrer le papier";

    /// <summary>Le papier de la feuille sans fin.</summary>
    public const string PaperEndless = "Feuille sans fin";

    /// <summary>Le papier des feuilles A4.</summary>
    public const string PaperA4 = "Feuilles A4";

    /// <summary>Le papier des feuilles A5.</summary>
    public const string PaperA5 = "Feuilles A5";

    /// <summary>Le papier des feuilles A6.</summary>
    public const string PaperA6 = "Feuilles A6";

    /// <summary>Uni.</summary>
    public const string Plain = "Uni";

    /// <summary>Petits carreaux.</summary>
    public const string SmallSquares = "Petits carreaux";

    /// <summary>Grands carreaux.</summary>
    public const string LargeSquares = "Grands carreaux";

    /// <summary>Lignes.</summary>
    public const string Lines = "Lignes";

    /// <summary>Points.</summary>
    public const string Dots = "Points";

    /// <summary>Millimétré.</summary>
    public const string Graph = "Millimétré";

    /// <summary>La carte des raccourcis.</summary>
    public const string ShortcutsTitle = "Raccourcis clavier";

    /// <summary>Comment changer un raccourci.</summary>
    public const string ShortcutsHelp = "« Modifier », puis les touches voulues : une touche de fonction, ou une touche tenue avec Ctrl ou Alt. Échap annule.";

    /// <summary>Le bouton qui attend les touches.</summary>
    public const string Change = "Modifier";

    /// <summary>Le bouton, pendant qu'il attend.</summary>
    public const string Waiting = "Tapez les touches…";

    /// <summary>Le bouton qui rend sa touche d'origine à une action.</summary>
    public const string Restore = "Rétablir";

    /// <summary>Le bouton qui rend toutes les touches d'origine.</summary>
    public const string RestoreAll = "Tout rétablir";

    /// <summary>La touche est déjà prise par une autre action.</summary>
    public static string Taken(string keys, string action) => $"{keys} sert déjà à : {action}";

    /// <summary>La touche ne convient pas.</summary>
    public static string Unusable(string keys) => $"{keys} ne convient pas : il faut une touche de fonction, ou une touche tenue avec Ctrl ou Alt.";

    /// <summary>Ces raccourcis-là ne se changent pas.</summary>
    public const string FixedTitle = "Raccourcis fixes";

    /// <summary>Ce qui ne suit pas un changement.</summary>
    public const string ShortcutsNote = "Les infobulles et la recherche (Ctrl+K) nomment encore la touche d'origine ; l'aide (F1) montre celle en vigueur.";

    /// <summary>La carte du menu.</summary>
    public const string MenuTitle = "Ordre du menu";

    /// <summary>Ce que change l'ordre.</summary>
    public const string MenuHelp = "L'ordre des vues du menu de gauche, et donc de Ctrl+1 à Ctrl+9. Santé du coffre et Paramètres restent en bas.";

    /// <summary>Monter une vue.</summary>
    public static string MoveUp(string view) => $"Monter {view}";

    /// <summary>Descendre une vue.</summary>
    public static string MoveDown(string view) => $"Descendre {view}";

    /// <summary>Le bouton qui rend l'ordre d'origine.</summary>
    public const string RestoreOrder = "Ordre d'origine";
}

/// <summary>
/// La section « Feuilles » des paramètres : la mise en page de toutes les fiches, le papier montré derrière le texte pour
/// chaque type de feuille, le zoom des fiches et leur police. Tout y est retenu sur l'ordinateur, et appliqué aussitôt.
/// </summary>
public sealed partial class SheetsSettingsViewModel : ObservableObject, INavigationAware
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly MachineSettings _machine;
    private bool _showing;
    private SheetPageLayouts? _aligned;

    /// <summary>La section, sur les préférences de l'ordinateur.</summary>
    public SheetsSettingsViewModel(MachineSettings machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        _machine = machine;
        // Ces premières valeurs ne sont pas des choix : rien ne s'écrit avant que les préférences soient montrées.
        _showing = true;
        Format = Formats[0];
        Orientation = Orientations[0];
        Margin = Margins[1];
        PaperEndless = PaperA4 = PaperA5 = PaperA6 = Papers[0];
        Font = Fonts[0];
        Show();
        machine.Changed += (_, _) => Show();
    }

    /// <summary>Les formats de feuille, avec leurs dimensions.</summary>
    public static IReadOnlyList<SettingChoice<PageFormat>> Formats { get; } =
    [
        new(PageFormat.Endless, MoreSettingsText.FormatEndless) { Detail = MoreSettingsText.FormatEndlessDetail },
        new(PageFormat.A4, "A4") { Detail = "21 × 29,7 cm" },
        new(PageFormat.A5, "A5") { Detail = "14,8 × 21 cm" },
        new(PageFormat.A6, "A6") { Detail = "10,5 × 14,8 cm" },
    ];

    /// <summary>Les orientations.</summary>
    public static IReadOnlyList<SettingChoice<bool>> Orientations { get; } =
    [
        new(false, MoreSettingsText.Portrait),
        new(true, MoreSettingsText.Landscape),
    ];

    /// <summary>Les papiers.</summary>
    public static IReadOnlyList<SettingChoice<PaperStyle>> Papers { get; } =
    [
        new(PaperStyle.Plain, MoreSettingsText.Plain),
        new(PaperStyle.SmallSquares, MoreSettingsText.SmallSquares),
        new(PaperStyle.LargeSquares, MoreSettingsText.LargeSquares),
        new(PaperStyle.Lines, MoreSettingsText.Lines),
        new(PaperStyle.Dots, MoreSettingsText.Dots),
        new(PaperStyle.Graph, MoreSettingsText.Graph),
    ];

    /// <summary>Les polices des fiches : les mêmes que dans la section « Apparence ».</summary>
    public static IReadOnlyList<SettingChoice<ReadingFont>> Fonts => GeneralSettingsViewModel.Fonts;

    /// <summary>Les marges proposées ; celles réglées sur mesure s'y ajoutent, sans bouton à elles : leur champ les montre.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SettingChoice<double>> Margins { get; private set; } = StandardMargins;

    /// <summary>Le format de la feuille de toutes les fiches.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPaged))]
    public partial SettingChoice<PageFormat> Format { get; set; }

    /// <summary>Son orientation.</summary>
    [ObservableProperty]
    public partial SettingChoice<bool> Orientation { get; set; }

    /// <summary>Ses marges.</summary>
    [ObservableProperty]
    public partial SettingChoice<double> Margin { get; set; }

    /// <summary>Les marges, en centimètres, telles qu'on les tape : « 2 », « 1,5 ».</summary>
    [ObservableProperty]
    public partial string CustomMargin { get; set; } = string.Empty;

    /// <summary>Le papier est montré.</summary>
    [ObservableProperty]
    public partial bool IsPaperOn { get; set; }

    /// <summary>Le papier de la feuille sans fin.</summary>
    [ObservableProperty]
    public partial SettingChoice<PaperStyle> PaperEndless { get; set; }

    /// <summary>Le papier des feuilles A4.</summary>
    [ObservableProperty]
    public partial SettingChoice<PaperStyle> PaperA4 { get; set; }

    /// <summary>Le papier des feuilles A5.</summary>
    [ObservableProperty]
    public partial SettingChoice<PaperStyle> PaperA5 { get; set; }

    /// <summary>Le papier des feuilles A6.</summary>
    [ObservableProperty]
    public partial SettingChoice<PaperStyle> PaperA6 { get; set; }

    /// <summary>La police des fiches, en lecture comme en écriture.</summary>
    [ObservableProperty]
    public partial SettingChoice<ReadingFont> Font { get; set; }

    /// <summary>Le zoom des fiches, en pour cent : de 50 à 200, par pas de 10.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomLabel))]
    [NotifyCanExecuteChangedFor(nameof(ZoomInCommand), nameof(ZoomOutCommand))]
    public partial double ZoomPercent { get; set; } = 100;

    /// <summary>La feuille a des pages : l'orientation et les marges comptent.</summary>
    public bool IsPaged => Format.Value != PageFormat.Endless;

    /// <summary>La mise en page en vigueur, pour l'aperçu.</summary>
    [ObservableProperty]
    public partial PageLayout PreviewLayout { get; private set; } = PageLayout.Default;

    /// <summary>Le papier de cette feuille, pour l'aperçu ; rien s'il est éteint.</summary>
    [ObservableProperty]
    public partial PaperStyle? PreviewPaper { get; private set; }

    /// <summary>Le confort de lecture en vigueur, pour l'aperçu : la police, la taille.</summary>
    [ObservableProperty]
    public partial ReadingComfort PreviewComfort { get; private set; } = ReadingComfort.Default;

    /// <summary>Ce que montre l'aperçu, en une ligne : « A4 portrait · marges de 2 cm · grands carreaux ».</summary>
    [ObservableProperty]
    public partial string PreviewCaption { get; private set; } = string.Empty;

    /// <summary>« 100 % ».</summary>
    public string ZoomLabel => string.Create(French, $"{ZoomPercent:0} %");

    /// <summary>Combien de fiches gardent leur propre mise en page.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOwnLayouts), nameof(OwnLayoutsNote))]
    [NotifyCanExecuteChangedFor(nameof(AlignOwnLayoutsCommand))]
    public partial int OwnLayouts { get; private set; }

    /// <summary>Des fiches gardent leur propre mise en page.</summary>
    public bool HasOwnLayouts => OwnLayouts > 0;

    /// <summary>« 3 fiches gardent leur propre mise en page… ».</summary>
    public string OwnLayoutsNote => MoreSettingsText.OwnLayouts(OwnLayouts);

    /// <summary>Ce que « Les aligner » vient de faire, tant que cela peut s'annuler.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAlignedNote))]
    [NotifyCanExecuteChangedFor(nameof(UndoAlignCommand))]
    public partial string? AlignedNote { get; private set; }

    /// <summary>Des fiches viennent d'être alignées : « Annuler » leur rend leur mise en page.</summary>
    public bool HasAlignedNote => AlignedNote is not null;

    /// <summary>La police conseillée pour le papier de la feuille en vigueur, en une phrase.</summary>
    [ObservableProperty]
    public partial string RecommendedFontNote { get; private set; } = string.Empty;

    private static IReadOnlyList<SettingChoice<double>> StandardMargins { get; } =
    [
        new(PageLayout.NarrowMargin, MoreSettingsText.MarginsNarrowShort) { Detail = MoreSettingsText.MarginsNarrow },
        new(PageLayout.NormalMargin, MoreSettingsText.MarginsNormalShort) { Detail = MoreSettingsText.MarginsNormal },
        new(PageLayout.WideMargin, MoreSettingsText.MarginsWideShort) { Detail = MoreSettingsText.MarginsWide },
    ];

    /// <summary>La police conseillée pour le papier montré.</summary>
    private ReadingFont Recommended => PaperStyles.RecommendedFont(PreviewPaper ?? PaperStyle.Plain);

    /// <inheritdoc/>
    public void OnNavigatedTo()
    {
        Forget();
        Show();
    }

    /// <summary>Les fiches qui avaient leur propre mise en page prennent celle de toutes ; « Annuler » la leur rend.</summary>
    [RelayCommand(CanExecute = nameof(HasOwnLayouts))]
    private void AlignOwnLayouts()
    {
        var before = _machine.Current.SheetPages;
        var count = before.Items.Count;
        _machine.Update(preferences => preferences with { SheetPages = SheetPageLayouts.None });
        _aligned = before;
        AlignedNote = MoreSettingsText.Aligned(count);
    }

    /// <summary>Rend aux fiches la mise en page qu'elles avaient avant « Les aligner ».</summary>
    [RelayCommand(CanExecute = nameof(HasAlignedNote))]
    private void UndoAlign()
    {
        if (_aligned is { } before)
        {
            _machine.Update(preferences => preferences with { SheetPages = before });
        }
        Forget();
    }

    [RelayCommand(CanExecute = nameof(CanZoomIn))]
    private void ZoomIn() => ZoomPercent += 10;

    [RelayCommand(CanExecute = nameof(CanZoomOut))]
    private void ZoomOut() => ZoomPercent -= 10;

    [RelayCommand]
    private void ResetZoom() => ZoomPercent = 100;

    /// <summary>Prend la police conseillée pour le papier montré.</summary>
    [RelayCommand(CanExecute = nameof(CanUseRecommendedFont))]
    private void UseRecommendedFont() => Font = Fonts.First(choice => choice.Value == Recommended);

    private bool CanZoomIn() => ZoomPercent < 200;

    private bool CanZoomOut() => ZoomPercent > 50;

    private bool CanUseRecommendedFont() => Font.Value != Recommended;

    private void Forget()
    {
        _aligned = null;
        AlignedNote = null;
    }

    private void Show()
    {
        _showing = true;
        var current = _machine.Current;
        var (page, paper) = (current.Page, current.Paper);
        var margin = page.EffectiveMarginCm;
        var written = margin.ToString("0.##", French);
        Margins = StandardMargins.Any(choice => Math.Abs(choice.Value - margin) < 0.005)
            ? StandardMargins
            : [.. StandardMargins, new SettingChoice<double>(margin, MoreSettingsText.MarginsCustom(written)) { IsExtra = true }];
        Format = Formats.First(choice => choice.Value == page.Format);
        Orientation = Orientations.First(choice => choice.Value == page.Landscape);
        Margin = Margins.First(choice => Math.Abs(choice.Value - margin) < 0.005);
        CustomMargin = written;
        IsPaperOn = paper.IsOn;
        PaperEndless = Papers.First(choice => choice.Value == paper.Endless);
        PaperA4 = Papers.First(choice => choice.Value == paper.A4);
        PaperA5 = Papers.First(choice => choice.Value == paper.A5);
        PaperA6 = Papers.First(choice => choice.Value == paper.A6);
        Font = Fonts.First(choice => choice.Value == current.Font);
        ZoomPercent = Math.Clamp(Math.Round(current.SheetZoom * 10) * 10, 50, 200);
        OwnLayouts = current.SheetPages.Items.Count;
        PreviewLayout = page;
        PreviewPaper = paper.IsOn ? paper.Of(page.Format) : null;
        PreviewComfort = ReadingComfort.Of(current);
        var style = Papers.First(choice => choice.Value == paper.Of(page.Format)).Label;
        PreviewCaption = MoreSettingsText.PreviewCaption(
            Format.Label,
            page.IsPaged ? Orientation.Label : null,
            page.IsPaged ? written : null,
            paper.IsOn ? style : null);
        RecommendedFontNote = MoreSettingsText.RecommendedFont(paper.IsOn ? style : MoreSettingsText.Plain, Fonts.First(choice => choice.Value == Recommended).Label);
        UseRecommendedFontCommand.NotifyCanExecuteChanged();
        _showing = false;
    }

    private void Remember()
    {
        if (_showing || Format is null || Orientation is null || Margin is null || PaperEndless is null || PaperA4 is null || PaperA5 is null || PaperA6 is null || Font is null)
        {
            return;
        }
        var layout = new PageLayout(Format.Value, Orientation.Value, Margin.Value);
        var paper = new PaperChoices(IsPaperOn, PaperEndless.Value, PaperA4.Value, PaperA5.Value, PaperA6.Value);
        var (font, zoom) = (Font.Value, ZoomPercent / 100);
        // Les préférences changées reviennent par « Changed » : c'est lui qui remet l'aperçu et les textes à jour.
        _machine.Update(preferences => preferences with { Page = layout with { MarginCm = layout.EffectiveMarginCm }, Paper = paper, Font = font, SheetZoom = zoom });
    }

    partial void OnFormatChanged(SettingChoice<PageFormat> value) => Remember();

    partial void OnOrientationChanged(SettingChoice<bool> value) => Remember();

    partial void OnMarginChanged(SettingChoice<double> value) => Remember();

    /// <summary>Des marges tapées : un nombre de centimètres, avec une virgule ou un point ; tout autre texte est ignoré.</summary>
    partial void OnCustomMarginChanged(string value)
    {
        if (_showing || Format is null || Orientation is null)
        {
            return;
        }
        if (!double.TryParse(value.Replace(',', '.').Replace("cm", string.Empty, StringComparison.OrdinalIgnoreCase).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var centimetres) || !double.IsFinite(centimetres))
        {
            return;
        }
        var layout = new PageLayout(Format.Value, Orientation.Value, centimetres);
        _machine.Update(preferences => preferences with { Page = layout with { MarginCm = Math.Round(layout.EffectiveMarginCm, 2) } });
    }

    partial void OnIsPaperOnChanged(bool value) => Remember();

    partial void OnPaperEndlessChanged(SettingChoice<PaperStyle> value) => Remember();

    partial void OnPaperA4Changed(SettingChoice<PaperStyle> value) => Remember();

    partial void OnPaperA5Changed(SettingChoice<PaperStyle> value) => Remember();

    partial void OnPaperA6Changed(SettingChoice<PaperStyle> value) => Remember();

    partial void OnFontChanged(SettingChoice<ReadingFont> value) => Remember();

    partial void OnZoomPercentChanged(double value)
    {
        var bounded = Math.Clamp(Math.Round(value / 10) * 10, 50, 200);
        if (Math.Abs(bounded - value) > 0.001)
        {
            ZoomPercent = bounded;
            return;
        }
        Remember();
    }
}

/// <summary>Un raccourci de la liste : son action, sa touche, et s'il attend qu'on en tape une autre.</summary>
public sealed partial class ShortcutRow : ObservableObject
{
    internal ShortcutRow(string id, string label)
    {
        Id = id;
        Label = label;
    }

    /// <summary>Le nom écrit de l'action.</summary>
    public string Id { get; }

    /// <summary>Ce que fait l'action.</summary>
    public string Label { get; }

    /// <summary>Sa touche, telle qu'on la lit.</summary>
    [ObservableProperty]
    public partial string Keys { get; internal set; } = string.Empty;

    /// <summary>La touche n'est pas celle d'origine.</summary>
    [ObservableProperty]
    public partial bool IsChanged { get; internal set; }

    /// <summary>La ligne attend qu'on tape la nouvelle touche.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChangeLabel))]
    public partial bool IsWaiting { get; internal set; }

    /// <summary>Le nom du bouton : « Modifier », ou « Tapez les touches… ».</summary>
    public string ChangeLabel => IsWaiting ? MoreSettingsText.Waiting : MoreSettingsText.Change;
}

/// <summary>Une vue du menu, dans la liste qui en règle l'ordre.</summary>
/// <param name="Id">Son identifiant.</param>
/// <param name="Label">Son nom.</param>
public sealed record MenuRow(string Id, string Label)
{
    /// <summary>« Monter Carnet ».</summary>
    public string UpLabel => MoreSettingsText.MoveUp(Label);

    /// <summary>« Descendre Carnet ».</summary>
    public string DownLabel => MoreSettingsText.MoveDown(Label);
}

/// <summary>
/// L'onglet « Raccourcis et menu » des paramètres : chaque raccourci modifiable se change en tapant la nouvelle touche, et
/// les vues du menu se réordonnent.
/// </summary>
public sealed partial class ShortcutsSettingsViewModel : ObservableObject, INavigationAware
{
    private readonly MachineSettings _machine;
    private readonly Func<IReadOnlyList<NavigationItem>>? _menu;

    /// <summary>L'onglet, sur les préférences de l'ordinateur ; <paramref name="menu"/> rend les vues du haut du menu, dans leur ordre.</summary>
    public ShortcutsSettingsViewModel(MachineSettings machine, Func<IReadOnlyList<NavigationItem>>? menu = null)
    {
        ArgumentNullException.ThrowIfNull(machine);
        _machine = machine;
        _menu = menu;
        Rows = [.. ShortcutMap.Actions.Select(action => new ShortcutRow(action.Id, action.Label))];
        // Le menu ne se lit qu'à l'affichage de l'onglet : à la construction, la coquille qui le porte n'existe pas encore,
        // et la demander ici la ferait se construire elle-même en boucle.
        ShowShortcuts();
    }

    /// <summary>Les raccourcis qui ne se changent pas.</summary>
    public static IReadOnlyList<Shortcut> Fixed { get; } =
    [
        new("Entrée, Espace", ShellText.ShortcutReveal),
        new("H", ShellText.ShortcutHighlight),
        new("Ctrl+→", ShellText.ShortcutNext),
        new("Ctrl+1 … Ctrl+9", ShellText.ShortcutGoTo),
        new("Ctrl+plus", ShellText.ShortcutZoomIn),
        new("Ctrl+moins", ShellText.ShortcutZoomOut),
        new("Ctrl+0", ShellText.ShortcutZoomReset),
    ];

    /// <summary>Les raccourcis modifiables.</summary>
    public IReadOnlyList<ShortcutRow> Rows { get; }

    /// <summary>Les vues du menu, dans leur ordre.</summary>
    public ObservableCollection<MenuRow> Menu { get; } = [];

    /// <summary>
    /// Le raccourci global de la capture rapide (Ctrl+Alt+N) est demandé à Windows : il ouvre la capture même quand une
    /// autre application est au premier plan.
    /// </summary>
    [ObservableProperty]
    public partial bool GlobalCapture { get; set; }

    partial void OnGlobalCaptureChanged(bool value)
    {
        if (value != _machine.Current.GlobalCapture)
        {
            _machine.Update(preferences => preferences with { GlobalCapture = value });
        }
    }

    /// <summary>Ce qui a empêché le dernier changement ; rien sinon.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    /// <summary>Il y a un refus à montrer.</summary>
    public bool HasMessage => !string.IsNullOrEmpty(Message);

    /// <summary>Une ligne attend ses touches.</summary>
    public bool IsWaiting => Rows.Any(row => row.IsWaiting);

    /// <inheritdoc/>
    public void OnNavigatedTo() => Show();

    /// <summary>
    /// Les touches tapées pendant qu'une ligne attend : elles deviennent son raccourci si elles conviennent et sont libres.
    /// </summary>
    /// <returns>La frappe a été prise : elle ne doit rien déclencher d'autre.</returns>
    public bool Take(Key key, ModifierKeys modifiers)
    {
        if (Rows.FirstOrDefault(row => row.IsWaiting) is not { } row)
        {
            return false;
        }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.System)
        {
            // Une touche à tenir, seule : on attend la suite.
            return true;
        }
        row.IsWaiting = false;
        if (key == Key.Escape)
        {
            Message = null;
            return true;
        }
        var shown = ShortcutMap.Display(key, modifiers);
        var map = ShortcutMap.Parse(_machine.Current.Shortcuts);
        if (!ShortcutMap.IsUsable(key, modifiers))
        {
            Message = MoreSettingsText.Unusable(shown);
        }
        else if (map.Conflict(row.Id, key, modifiers) is { } other)
        {
            Message = MoreSettingsText.Taken(shown, other);
        }
        else if (Fixed.Any(shortcut => shortcut.Keys == shown))
        {
            Message = MoreSettingsText.Taken(shown, Fixed.First(shortcut => shortcut.Keys == shown).Description);
        }
        else
        {
            Message = null;
            Save(map.With(row.Id, key, modifiers));
        }
        return true;
    }

    /// <summary>« Modifier » : la ligne attend ses touches ; un second clic annule.</summary>
    [RelayCommand]
    private void Change(ShortcutRow? row)
    {
        if (row is null)
        {
            return;
        }
        var waited = row.IsWaiting;
        foreach (var other in Rows)
        {
            other.IsWaiting = false;
        }
        row.IsWaiting = !waited;
        Message = null;
    }

    /// <summary>« Rétablir » : la touche d'origine d'une action ; sans action, de toutes.</summary>
    [RelayCommand]
    private void Restore(ShortcutRow? row)
    {
        foreach (var other in Rows)
        {
            other.IsWaiting = false;
        }
        Message = null;
        Save(ShortcutMap.Parse(_machine.Current.Shortcuts).Without(row?.Id));
    }

    /// <summary>Monte une vue du menu d'un rang.</summary>
    [RelayCommand]
    private void MoveUp(MenuRow? row) => Move(row, -1);

    /// <summary>Descend une vue du menu d'un rang.</summary>
    [RelayCommand]
    private void MoveDown(MenuRow? row) => Move(row, 1);

    /// <summary>Rend au menu son ordre d'origine.</summary>
    [RelayCommand]
    private void RestoreOrder()
    {
        _machine.Update(preferences => preferences with { MenuOrder = string.Empty });
        Show();
    }

    private void Move(MenuRow? row, int step)
    {
        var from = row is null ? -1 : Menu.IndexOf(row);
        var to = from + step;
        if (from < 0 || to < 0 || to >= Menu.Count)
        {
            return;
        }
        Menu.Move(from, to);
        var order = string.Join(',', Menu.Select(entry => entry.Id));
        _machine.Update(preferences => preferences with { MenuOrder = order });
    }

    private void Save(ShortcutMap map)
    {
        // Relu tel qu'il sera lu au prochain démarrage : une action qui reprend sa touche d'origine la reprend à celle qui l'avait.
        var written = ShortcutMap.Parse(map.Written()).Written();
        _machine.Update(preferences => preferences with { Shortcuts = written });
        // Sans coquille pour l'appliquer (un essai), les raccourcis en vigueur suivent quand même.
        ShortcutMap.Use(written);
        Show();
    }

    private void ShowShortcuts()
    {
        GlobalCapture = _machine.Current.GlobalCapture;
        var map = ShortcutMap.Parse(_machine.Current.Shortcuts);
        foreach (var row in Rows)
        {
            row.Keys = map.DisplayOf(row.Id);
            row.IsChanged = map.IsChanged(row.Id);
        }
    }

    private void Show()
    {
        ShowShortcuts();
        var entries = _menu?.Invoke() ?? [];
        if (!Menu.Select(entry => entry.Id).SequenceEqual(entries.Select(entry => entry.Id)))
        {
            Menu.Clear();
            foreach (var entry in entries)
            {
                Menu.Add(new MenuRow(entry.Id, entry.Label));
            }
        }
    }
}
