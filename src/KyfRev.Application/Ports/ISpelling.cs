namespace KyfRev.Application.Ports;

/// <summary>Une faute dans un texte.</summary>
/// <param name="Start">Son début.</param>
/// <param name="Length">Sa longueur.</param>
public sealed record SpellingMistake(int Start, int Length);

/// <summary>La faute d'un mot, et ce qui est proposé à la place.</summary>
/// <param name="Start">Le début de la partie fautive dans le mot.</param>
/// <param name="Length">Sa longueur.</param>
/// <param name="Suggestions">Les remplacements, du plus probable au moins probable.</param>
public sealed record SpellingCorrection(int Start, int Length, IReadOnlyList<string> Suggestions);

/// <summary>
/// Le correcteur d'orthographe du système, en français. Sans correcteur, rien n'est signalé. À appeler hors du
/// fil de l'interface : la première demande charge le dictionnaire.
/// </summary>
public interface ISpelling
{
    /// <summary>Les fautes d'un texte.</summary>
    IReadOnlyList<SpellingMistake> Check(string text);

    /// <summary>La faute d'un mot et ses remplacements ; rien si le mot est juste.</summary>
    SpellingCorrection? Correct(string word);

    /// <summary>
    /// Un mot ajouté ne vaut que jusqu'à la fermeture de l'application : le dictionnaire de Windows est un fichier en clair,
    /// qu'un coffre chiffré ne nourrit pas.
    /// </summary>
    bool AddsUntilClosing { get; }

    /// <summary>
    /// Ajoute un mot au dictionnaire de l'utilisateur, ou seulement jusqu'à la fermeture si <see cref="AddsUntilClosing"/> ;
    /// rend faux si ce n'est pas un mot, ou si c'est impossible.
    /// </summary>
    bool Add(string word);
}
