using System.Globalization;
using System.Resources;

namespace KyfRev.Presentation.Wpf;

/// <summary>Textes de la coquille, lus dans les ressources (<c>Resources/ShellText.resx</c>) : l'interface se traduit sans toucher au code.</summary>
public static class ShellText
{
    private static readonly ResourceManager Resources = new("KyfRev.Presentation.Wpf.Resources.ShellText", typeof(ShellText).Assembly);

    /// <summary>« kyfRev ».</summary>
    public static string AppName => Text(nameof(AppName));

    /// <summary>« Archives ».</summary>
    public static string Archives => Text(nameof(Archives));

    /// <summary>« Choisissez un dossier vide pour le nouveau coffre ».</summary>
    public static string ChooseEmptyFolder => Text(nameof(ChooseEmptyFolder));

    /// <summary>« Choisissez le dossier du coffre ».</summary>
    public static string ChooseVaultFolder => Text(nameof(ChooseVaultFolder));

    /// <summary>Explication des vues à venir.</summary>
    public static string ComingSoon => Text(nameof(ComingSoon));

    /// <summary>« Créer un coffre ».</summary>
    public static string CreateVault => Text(nameof(CreateVault));

    /// <summary>« Créer le coffre ici ».</summary>
    public static string CreateVaultHere => Text(nameof(CreateVaultHere));

    /// <summary>« Définitions ».</summary>
    public static string Definitions => Text(nameof(Definitions));

    /// <summary>« Mode concentration ».</summary>
    public static string FocusMode => Text(nameof(FocusMode));

    /// <summary>« Afficher le menu (Ctrl+Maj+F) ».</summary>
    public static string ExitFocusMode => Text(nameof(ExitFocusMode));

    /// <summary>« Galerie des composants ».</summary>
    public static string Gallery => Text(nameof(Gallery));

    /// <summary>Explication de la galerie.</summary>
    public static string GalleryDescription => Text(nameof(GalleryDescription));

    /// <summary>« Santé du coffre ».</summary>
    public static string Health => Text(nameof(Health));

    /// <summary>« Réduire ».</summary>
    public static string WindowMinimize => Text(nameof(WindowMinimize));

    /// <summary>« Agrandir ».</summary>
    public static string WindowMaximize => Text(nameof(WindowMaximize));

    /// <summary>« Restaurer ».</summary>
    public static string WindowRestore => Text(nameof(WindowRestore));

    /// <summary>« Fermer ».</summary>
    public static string WindowClose => Text(nameof(WindowClose));

    /// <summary>« Passer au thème sombre ».</summary>
    public static string ThemeToDark => Text(nameof(ThemeToDark));

    /// <summary>« Passer au thème clair ».</summary>
    public static string ThemeToLight => Text(nameof(ThemeToLight));

    /// <summary>« Raccourcis clavier ».</summary>
    public static string Help => Text(nameof(Help));

    /// <summary>« Navigation principale ».</summary>
    public static string MainNavigation => Text(nameof(MainNavigation));

    /// <summary>« Plus » : l'onglet des autres vues.</summary>
    public static string More => Text(nameof(More));

    /// <summary>« Autres vues » : le menu de l'onglet « Plus ».</summary>
    public static string MoreViews => Text(nameof(MoreViews));

    /// <summary>« Un nouveau coffre se crée dans un dossier vide. »</summary>
    public static string NewVaultHelp => Text(nameof(NewVaultHelp));

    /// <summary>« Aucune commande ne correspond. ».</summary>
    public static string NoCommand => Text(nameof(NoCommand));

    /// <summary>« Ouvrir un coffre existant ».</summary>
    public static string OpenExistingVault => Text(nameof(OpenExistingVault));

    /// <summary>« Rechercher ».</summary>
    public static string Search => Text(nameof(Search));

    /// <summary>« Rechercher partout ».</summary>
    public static string SearchPlaceholder => Text(nameof(SearchPlaceholder));

    /// <summary>« Rechercher une commande ».</summary>
    public static string SearchCommandsPlaceholder => Text(nameof(SearchCommandsPlaceholder));

    /// <summary>« archivée ».</summary>
    public static string SearchBadgeArchived => Text(nameof(SearchBadgeArchived));

    /// <summary>« définition ».</summary>
    public static string SearchBadgeDefinition => Text(nameof(SearchBadgeDefinition));

    /// <summary>« passage ».</summary>
    public static string SearchBadgePassage => Text(nameof(SearchBadgePassage));

    /// <summary>« Passages et définitions ».</summary>
    public static string SearchGroupAnnotations => Text(nameof(SearchGroupAnnotations));

    /// <summary>« Fiches · 12 ».</summary>
    public static string SearchGroupOf(object label, object total) => Format("SearchGroup", label, total);

    /// <summary>Comment chercher dans le coffre.</summary>
    public static string SearchHint => Text(nameof(SearchHint));

    /// <summary>« ↑ ↓ pour choisir · Entrée pour ouvrir ».</summary>
    public static string SearchKeys => Text(nameof(SearchKeys));

    /// <summary>« … autres résultats : précisez la recherche. ».</summary>
    public static string SearchMoreManyOf(object count) => Format("SearchMoreMany", count);

    /// <summary>« … autre résultat : précisez la recherche. ».</summary>
    public static string SearchMoreOneOf(object count) => Format("SearchMoreOne", count);

    /// <summary>« Rien ne correspond à « … ». ».</summary>
    public static string SearchNothingOf(object text) => Format("SearchNothing", text);

    /// <summary>«  · Histoire », à la suite d'un titre.</summary>
    public static string SearchPlaceOf(object place) => Format("SearchPlace", place);

    /// <summary>« « … » » : un passage cité.</summary>
    public static string SearchQuotedOf(object passage) => Format("SearchQuoted", passage);

    /// <summary>« Navigation secondaire ».</summary>
    public static string SecondaryNavigation => Text(nameof(SecondaryNavigation));

    /// <summary>« Paramètres ».</summary>
    public static string Settings => Text(nameof(Settings));

    /// <summary>« Fiches ».</summary>
    public static string Sheets => Text(nameof(Sheets));

    /// <summary>Raccourci du mode concentration.</summary>
    public static string ShortcutFocusMode => Text(nameof(ShortcutFocusMode));

    /// <summary>« Plein écran ».</summary>
    public static string ShortcutFullScreen => Text(nameof(ShortcutFullScreen));

    /// <summary>Raccourci du verrouillage du coffre chiffré.</summary>
    public static string ShortcutLock => Text(nameof(ShortcutLock));

    /// <summary>Raccourci des vues du menu.</summary>
    public static string ShortcutGoTo => Text(nameof(ShortcutGoTo));

    /// <summary>Raccourci de l'aide.</summary>
    public static string ShortcutHelp => Text(nameof(ShortcutHelp));

    /// <summary>« Élément suivant de la séance, ou le passer ».</summary>
    public static string ShortcutNext => Text(nameof(ShortcutNext));

    /// <summary>Raccourci de la recherche.</summary>
    public static string ShortcutSearch => Text(nameof(ShortcutSearch));

    /// <summary>« Valider la révision ».</summary>
    public static string ShortcutValidate => Text(nameof(ShortcutValidate));

    /// <summary>Raccourci pour agrandir.</summary>
    public static string ShortcutZoomIn => Text(nameof(ShortcutZoomIn));

    /// <summary>Raccourci pour réduire.</summary>
    public static string ShortcutZoomOut => Text(nameof(ShortcutZoomOut));

    /// <summary>Raccourci de la taille normale.</summary>
    public static string ShortcutZoomReset => Text(nameof(ShortcutZoomReset));

    /// <summary>« Statistiques ».</summary>
    public static string Statistics => Text(nameof(Statistics));

    /// <summary>« Thème ».</summary>
    public static string Theme => Text(nameof(Theme));

    /// <summary>« Sombre ».</summary>
    public static string ThemeDark => Text(nameof(ThemeDark));

    /// <summary>« Clair ».</summary>
    public static string ThemeLight => Text(nameof(ThemeLight));

    /// <summary>« Comme Windows ».</summary>
    public static string ThemeSystem => Text(nameof(ThemeSystem));

    /// <summary>« Aujourd'hui ».</summary>
    public static string Today => Text(nameof(Today));

    /// <summary>« Le coffre n'a pas pu être ouvert. »</summary>
    public static string VaultNotOpened => Text(nameof(VaultNotOpened));

    /// <summary>« Bienvenue dans kyfRev ».</summary>
    public static string Welcome => Text(nameof(Welcome));

    /// <summary>Présentation du coffre, sur l'accueil.</summary>
    public static string WelcomeIntro => Text(nameof(WelcomeIntro));

    /// <summary>« Aller à : … ».</summary>
    public static string GoTo(string view) => Format(nameof(GoTo), view);

    /// <summary>« Thème : … ».</summary>
    public static string ThemeTo(string theme) => Format(nameof(ThemeTo), theme);

    /// <summary>« Zoom : … % ».</summary>
    public static string Zoom(int percent) => Format(nameof(Zoom), percent);

    private static string Format(string name, object value) => string.Format(CultureInfo.CurrentCulture, Text(name), value);

    private static string Format(string name, object first, object second) => string.Format(CultureInfo.CurrentCulture, Text(name), first, second);

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » absent des ressources.");
}
