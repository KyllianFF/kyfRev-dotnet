namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Page d'une vue qui n'est pas encore portée : son titre et ce qu'elle fera.</summary>
/// <param name="Title">Titre de la vue.</param>
/// <param name="Glyph">Icône.</param>
/// <param name="Description">Explication.</param>
public sealed record PlaceholderPageViewModel(string Title, string Glyph, string Description);
