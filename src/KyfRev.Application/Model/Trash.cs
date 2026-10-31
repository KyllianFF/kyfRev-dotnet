namespace KyfRev.Application.Model;

/// <summary>Une fiche à la corbeille.</summary>
/// <param name="Id">Identifiant de la fiche.</param>
/// <param name="Title">Titre.</param>
/// <param name="Folder">Dossier d'origine, relatif à <c>fiches/</c>.</param>
/// <param name="TrashedAt">Mise à la corbeille.</param>
/// <param name="Annotations">Annotations et définitions rangées avec elle.</param>
public sealed record TrashedSheet(string Id, string Title, string Folder, DateTimeOffset TrashedAt, int Annotations);

/// <summary>Une annotation à la corbeille, restaurable.</summary>
public sealed record TrashedAnnotation(string Id, string SheetId, string Title, string Exact, string? Comment, DateTimeOffset TrashedAt);
