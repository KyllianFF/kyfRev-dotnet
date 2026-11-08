using System.Globalization;
using System.Resources;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Textes de la vue « Citations », ceux de la 0.x, lus dans les ressources (<c>Resources/QuotesText.resx</c>).</summary>
public static class QuotesText
{
    private static readonly ResourceManager Resources = new("KyfRev.Presentation.Wpf.Resources.QuotesText", typeof(QuotesText).Assembly);

    /// <summary>« Citation ajoutée. ».</summary>
    public static string Added => Text(nameof(Added));

    /// <summary>« Citation trouvée par la recherche. ».</summary>
    public static string FoundBySearch => Text(nameof(FoundBySearch));

    /// <summary>« Afficher toutes les citations ».</summary>
    public static string ShowAll => Text(nameof(ShowAll));

    /// <summary>« ajoutée le {0} ».</summary>
    public static string AddedOnOf(object day) => Format("AddedOn", day);

    /// <summary>« Adresse web ».</summary>
    public static string AddressLabel => Text(nameof(AddressLabel));

    /// <summary>« Toutes les étiquettes ».</summary>
    public static string AllTags => Text(nameof(AllTags));

    /// <summary>« — {0} ».</summary>
    public static string AttributionOf(object author) => Format("Attribution", author);

    /// <summary>« Auteur ».</summary>
    public static string AuthorLabel => Text(nameof(AuthorLabel));

    /// <summary>« Contexte ».</summary>
    public static string ContextLabel => Text(nameof(ContextLabel));

    /// <summary>« {0} citations ».</summary>
    public static string CountManyOf(object count) => Format("CountMany", count);

    /// <summary>« {0} citation ».</summary>
    public static string CountOneOf(object count) => Format("CountOne", count);

    /// <summary>« Citation · {0} ».</summary>
    public static string DataKindOf(object author) => Format("DataKind", author);

    /// <summary>« Date ».</summary>
    public static string DateLabel => Text(nameof(DateLabel));

    /// <summary>« 1670, mai 1968, XVIIe siècle… ».</summary>
    public static string DatePlaceholder => Text(nameof(DatePlaceholder));

    /// <summary>« Modifier ».</summary>
    public static string Edit => Text(nameof(Edit));

    /// <summary>« Modifier la citation : {0} ».</summary>
    public static string EditNameOf(object beginning) => Format("EditName", beginning);

    /// <summary>« Modifier la citation ».</summary>
    public static string EditQuote => Text(nameof(EditQuote));

    /// <summary>« Seul le texte est obligatoire : l'auteur, la date et la source sont… ».</summary>
    public static string EmptyDescription => Text(nameof(EmptyDescription));

    /// <summary>« Aucune citation pour l'instant ».</summary>
    public static string EmptyTitle => Text(nameof(EmptyTitle));

    /// <summary>« « {0} » — {1} ».</summary>
    public static string ExcerptOf(object text, object author) => Format("Excerpt", text, author);

    /// <summary>« {0} : {1} ».</summary>
    public static string KindAndTitleOf(object kind, object title) => Format("KindAndTitle", kind, title);

    /// <summary>« Article ».</summary>
    public static string KindArticle => Text(nameof(KindArticle));

    /// <summary>« Livre ».</summary>
    public static string KindBook => Text(nameof(KindBook));

    /// <summary>« Émission ».</summary>
    public static string KindBroadcast => Text(nameof(KindBroadcast));

    /// <summary>« Film ».</summary>
    public static string KindFilm => Text(nameof(KindFilm));

    /// <summary>« Type de source ».</summary>
    public static string KindLabel => Text(nameof(KindLabel));

    /// <summary>« Journal ».</summary>
    public static string KindNewspaper => Text(nameof(KindNewspaper));

    /// <summary>« — ».</summary>
    public static string KindNone => Text(nameof(KindNone));

    /// <summary>« Autre ».</summary>
    public static string KindOther => Text(nameof(KindOther));

    /// <summary>« Chanson ».</summary>
    public static string KindSong => Text(nameof(KindSong));

    /// <summary>« Discours ».</summary>
    public static string KindSpeech => Text(nameof(KindSpeech));

    /// <summary>« Inconnue ».</summary>
    public static string KindUnknown => Text(nameof(KindUnknown));

    /// <summary>« Source inconnue ».</summary>
    public static string KindUnknownSource => Text(nameof(KindUnknownSource));

    /// <summary>« Site web ».</summary>
    public static string KindWebsite => Text(nameof(KindWebsite));

    /// <summary>« Citations ».</summary>
    public static string ListName => Text(nameof(ListName));

    /// <summary>« Citation modifiée. ».</summary>
    public static string Modified => Text(nameof(Modified));

    /// <summary>« Nouvelle citation ».</summary>
    public static string NewQuote => Text(nameof(NewQuote));

    /// <summary>« Aucune citation ne correspond à cette recherche. ».</summary>
    public static string NoMatch => Text(nameof(NoMatch));

    /// <summary>« Ouvrir la source de la citation : {0} ».</summary>
    public static string OpenSourceOf(object beginning) => Format("OpenSource", beginning);

    /// <summary>« Page ».</summary>
    public static string PageLabel => Text(nameof(PageLabel));

    /// <summary>« Restaurer ».</summary>
    public static string Restore => Text(nameof(Restore));

    /// <summary>« Restaurer : {0} ».</summary>
    public static string RestoreNameOf(object excerpt) => Format("RestoreName", excerpt);

    /// <summary>« Citation restaurée. ».</summary>
    public static string Restored => Text(nameof(Restored));

    /// <summary>« Enregistrer ».</summary>
    public static string Save => Text(nameof(Save));

    /// <summary>« Rechercher une citation ».</summary>
    public static string SearchLabel => Text(nameof(SearchLabel));

    /// <summary>« Texte, auteur, source… ».</summary>
    public static string SearchPlaceholder => Text(nameof(SearchPlaceholder));

    /// <summary>« Ajout récent ».</summary>
    public static string SortAdded => Text(nameof(SortAdded));

    /// <summary>« Auteur ».</summary>
    public static string SortAuthor => Text(nameof(SortAuthor));

    /// <summary>« Tri ».</summary>
    public static string SortLabel => Text(nameof(SortLabel));

    /// <summary>« Époque ».</summary>
    public static string SortPeriod => Text(nameof(SortPeriod));

    /// <summary>« Source ».</summary>
    public static string Source => Text(nameof(Source));

    /// <summary>« Titre ».</summary>
    public static string SourceTitleLabel => Text(nameof(SourceTitleLabel));

    /// <summary>« Étiquette ».</summary>
    public static string TagLabel => Text(nameof(TagLabel));

    /// <summary>« Étiquettes ».</summary>
    public static string TagsLabel => Text(nameof(TagsLabel));

    /// <summary>« philosophie, poésie ».</summary>
    public static string TagsPlaceholder => Text(nameof(TagsPlaceholder));

    /// <summary>« Texte ».</summary>
    public static string TextLabel => Text(nameof(TextLabel));

    /// <summary>« Le texte de la citation est obligatoire. ».</summary>
    public static string TextRequired => Text(nameof(TextRequired));

    /// <summary>« Citations ».</summary>
    public static string Title => Text(nameof(Title));

    /// <summary>« Supprimer ».</summary>
    public static string Trash => Text(nameof(Trash));

    /// <summary>« Elle quitte la liste mais reste récupérable dans la corbeille, en b… ».</summary>
    public static string TrashConfirm => Text(nameof(TrashConfirm));

    /// <summary>« Supprimer cette citation ? ».</summary>
    public static string TrashConfirmTitle => Text(nameof(TrashConfirmTitle));

    /// <summary>« Corbeille des citations ».</summary>
    public static string TrashListName => Text(nameof(TrashListName));

    /// <summary>« Supprimer la citation : {0} ».</summary>
    public static string TrashNameOf(object beginning) => Format("TrashName", beginning);

    /// <summary>« Corbeille ({0}) ».</summary>
    public static string TrashTitleOf(object count) => Format("TrashTitle", count);

    /// <summary>« Citation placée dans la corbeille. ».</summary>
    public static string Trashed => Text(nameof(Trashed));

    /// <summary>« Auteur inconnu ».</summary>
    public static string UnknownAuthor => Text(nameof(UnknownAuthor));

    /// <summary>« date inconnue ».</summary>
    public static string UnknownDate => Text(nameof(UnknownDate));

    /// <summary>« {0} dans « citations » : {1}. kyfRev ne les modifie pas. ».</summary>
    public static string UnreadableOf(object count, object files) => Format("Unreadable", count, files);

    /// <summary>« {0} fichiers illisibles ».</summary>
    public static string UnreadableManyOf(object count) => Format("UnreadableMany", count);

    /// <summary>« {0} fichier illisible ».</summary>
    public static string UnreadableOneOf(object count) => Format("UnreadableOne", count);

    /// <summary>« Wikiquote ».</summary>
    public static string Wikiquote => Text(nameof(Wikiquote));

    /// <summary>« {0}, p. {1} ».</summary>
    public static string WithPageOf(object work, object page) => Format("WithPage", work, page);

    private static string Format(string name, params object[] values) => string.Format(CultureInfo.CurrentCulture, Text(name), values);

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » introuvable.");
}
