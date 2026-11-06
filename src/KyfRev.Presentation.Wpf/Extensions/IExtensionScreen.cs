namespace KyfRev.Presentation.Wpf.Extensions;

/// <summary>Ce que rend une sélection guidée : un texte pris dans une fiche, écrit à la main, passé, ou la fin de la série.</summary>
/// <param name="Choice">« selection », « saisie », « passer » ou « terminer ».</param>
/// <param name="Text">Le texte pris ou écrit ; rien pour « passer » et « terminer ».</param>
/// <param name="SheetId">La fiche d'où vient le texte pris.</param>
/// <param name="SheetTitle">Son titre.</param>
/// <param name="Start">Le début du texte pris, dans le texte affiché de la fiche.</param>
/// <param name="End">Sa fin.</param>
public sealed record GuidedSelection(string Choice, string? Text = null, string? SheetId = null, string? SheetTitle = null, int? Start = null, int? End = null)
{
    /// <summary>Le texte pris dans une fiche.</summary>
    public const string Taken = "selection";

    /// <summary>Le texte écrit à la main.</summary>
    public const string Typed = "saisie";

    /// <summary>Celle-ci est passée.</summary>
    public const string Skipped = "passer";

    /// <summary>La série s'arrête.</summary>
    public const string Stopped = "terminer";
}

/// <summary>Une sélection guidée demandée par une extension.</summary>
/// <param name="Extension">Le nom de l'extension, montré avec la consigne.</param>
/// <param name="Prompt">La consigne : ce qu'il faut sélectionner.</param>
/// <param name="SheetId">La fiche où sélectionner ; rien pour celle qui est ouverte.</param>
/// <param name="AllowsTyping">Ce qui manque dans la fiche peut s'écrire à la main.</param>
public sealed record SelectionAsk(string Extension, string Prompt, string? SheetId, bool AllowsTyping);

/// <summary>Ce que l'application montre pour une extension : une sélection guidée dans une fiche, une fiche ouverte à un passage.</summary>
public interface IExtensionScreen
{
    /// <summary>Demande à l'utilisateur de sélectionner un texte dans une fiche ; un refus dit pourquoi ce n'est pas possible.</summary>
    /// <param name="ask">La demande.</param>
    Task<(GuidedSelection? Answer, string? Refusal)> AskSelectionAsync(SelectionAsk ask);

    /// <summary>Ouvre une fiche en lecture, et y sélectionne un passage s'il est donné ; rend ce qui l'en empêche.</summary>
    /// <param name="sheetId">La fiche.</param>
    /// <param name="passageStart">Le début du passage, dans le texte affiché.</param>
    /// <param name="passageEnd">Sa fin.</param>
    string? OpenSheet(string sheetId, int? passageStart, int? passageEnd);
}
