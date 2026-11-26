namespace KyfRev.Application.Model;

/// <summary>
/// Un autre poste qui a le même coffre ouvert, dans un dossier synchronisé : son nom, depuis quand, et quand il l'a dit
/// pour la dernière fois.
/// </summary>
/// <param name="Name">Le nom du poste, tel que Windows le donne.</param>
/// <param name="Since">L'ouverture du coffre sur ce poste.</param>
/// <param name="SeenAt">La dernière fois que ce poste a dit l'avoir ouvert.</param>
public sealed record OtherComputer(string Name, DateTimeOffset Since, DateTimeOffset SeenAt);
