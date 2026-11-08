using System.Collections.Immutable;

namespace KyfRev.Application.Model;

/// <summary>Un morceau d'extrait : un mot trouvé, ou le texte qui l'entoure.</summary>
/// <param name="Text">Le texte du morceau, sur une ligne.</param>
/// <param name="Found">C'est un mot cherché.</param>
public sealed record ExcerptPiece(string Text, bool Found);

/// <summary>Une fiche qui correspond à la recherche.</summary>
/// <param name="Id">Identifiant.</param>
/// <param name="Title">Titre.</param>
/// <param name="Folder">Dossier relatif à <c>fiches/</c> (« Réseau/Cisco »), vide à la racine.</param>
/// <param name="Archived">Archivée.</param>
/// <param name="Excerpt">Le passage du texte autour des mots trouvés ; vide quand le titre et les étiquettes les contiennent tous.</param>
public sealed record SheetHit(string Id, string Title, string Folder, bool Archived, ImmutableArray<ExcerptPiece> Excerpt);

/// <summary>Un passage annoté ou une définition, hors corbeille, qui correspond à la recherche.</summary>
/// <param name="Id">L'annotation.</param>
/// <param name="SheetId">Sa fiche.</param>
/// <param name="SheetTitle">Le titre de sa fiche.</param>
/// <param name="Term">Le terme d'une définition ; rien pour un passage.</param>
/// <param name="Passage">Le passage surligné, abrégé.</param>
/// <param name="Excerpt">Autour des mots trouvés ; vide quand le terme les contient tous.</param>
public sealed record AnnotationHit(string Id, string SheetId, string SheetTitle, string? Term, string Passage, ImmutableArray<ExcerptPiece> Excerpt);

/// <summary>Une citation, hors corbeille, qui correspond à la recherche.</summary>
/// <param name="Path">Son fichier, relatif au coffre : c'est ce qui la désigne.</param>
/// <param name="Author">Son auteur.</param>
/// <param name="Text">Son texte, abrégé.</param>
/// <param name="Excerpt">Autour des mots trouvés ; vide quand l'auteur, l'œuvre ou les étiquettes les contiennent tous.</param>
public sealed record QuoteHit(string Path, string? Author, string Text, ImmutableArray<ExcerptPiece> Excerpt);
