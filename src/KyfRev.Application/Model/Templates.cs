namespace KyfRev.Application.Model;

/// <summary>Un modèle de fiche.</summary>
/// <param name="Name">Nom affiché (« Cours ») ; pour un modèle du coffre, le nom du fichier.</param>
/// <param name="Description">Ce qu'il contient, en une ligne.</param>
/// <param name="Body">Texte Markdown du modèle, variables comprises.</param>
/// <param name="BuiltIn">Intégré à l'application ; sinon, <c>modeles/&lt;nom&gt;.md</c> du coffre.</param>
public sealed record SheetTemplate(string Name, string Description, string Body, bool BuiltIn);
