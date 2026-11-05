namespace KyfRev.Application.Ports;

/// <summary>
/// Préférences d'affichage choisies dans Windows : thème des applications, contraste élevé,
/// animations. L'interface les suit, et se met à jour quand elles changent.
/// </summary>
public interface ISystemAppearance
{
    /// <summary>Windows demande des applications sombres.</summary>
    bool PrefersDarkTheme { get; }

    /// <summary>Le contraste élevé de Windows est actif.</summary>
    bool HighContrast { get; }

    /// <summary>Windows demande de réduire les animations.</summary>
    bool ReducedMotion { get; }

    /// <summary>L'une des préférences a changé (sur un fil quelconque).</summary>
    event EventHandler? Changed;
}

/// <summary>Ce qui dépasse de chaque bord de l'espace de travail, en pixels ; zéro de chaque côté pour une fenêtre qui n'en sort pas.</summary>
/// <param name="Left">À gauche.</param>
/// <param name="Top">En haut.</param>
/// <param name="Right">À droite.</param>
/// <param name="Bottom">En bas.</param>
public readonly record struct WindowOverhang(int Left, int Top, int Right, int Bottom);

/// <summary>Cadre des fenêtres dessiné par Windows.</summary>
public interface IWindowChrome
{
    /// <summary>
    /// Ce qui dépasse de l'espace de travail d'une fenêtre agrandie, en pixels : Windows étend une fenêtre redimensionnable au-delà
    /// de l'écran, de l'épaisseur de son cadre, et une fenêtre qui dessine le sien doit le compenser.
    /// </summary>
    /// <param name="windowHandle">La fenêtre.</param>
    WindowOverhang MaximizedOverhang(nint windowHandle);

    /// <summary>Barre de titre sombre ou claire pour la fenêtre <paramref name="windowHandle"/> ; faux si Windows ne le permet pas.</summary>
    bool UseDarkTitleBar(nint windowHandle, bool dark);
}
