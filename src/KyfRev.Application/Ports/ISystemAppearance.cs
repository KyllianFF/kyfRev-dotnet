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

/// <summary>L'espace de travail d'un écran, sans la barre des tâches, en pixels.</summary>
/// <param name="Width">Sa largeur.</param>
/// <param name="Height">Sa hauteur.</param>
public readonly record struct ScreenArea(int Width, int Height);

/// <summary>Un rectangle de l'écran, en pixels.</summary>
/// <param name="Left">Bord gauche.</param>
/// <param name="Top">Bord haut.</param>
/// <param name="Right">Bord droit.</param>
/// <param name="Bottom">Bord bas.</param>
public readonly record struct ScreenBounds(int Left, int Top, int Right, int Bottom)
{
    /// <summary>Le rectangle n'a pas de surface : Windows n'a rien su en dire.</summary>
    public bool IsEmpty => Right <= Left || Bottom <= Top;
}

/// <summary>Cadre des fenêtres dessiné par Windows.</summary>
public interface IWindowChrome
{
    /// <summary>
    /// Ce qui dépasse de l'espace de travail d'une fenêtre agrandie, en pixels : Windows étend une fenêtre redimensionnable au-delà
    /// de l'écran, de l'épaisseur de son cadre, et une fenêtre qui dessine le sien doit le compenser.
    /// </summary>
    /// <param name="windowHandle">La fenêtre.</param>
    WindowOverhang MaximizedOverhang(nint windowHandle);

    /// <summary>L'espace de travail de l'écran où se trouve la fenêtre <paramref name="windowHandle"/>, en pixels ; vide sans fenêtre.</summary>
    /// <param name="windowHandle">La fenêtre.</param>
    ScreenArea WorkArea(nint windowHandle);

    /// <summary>
    /// Ce qui se voit vraiment de la fenêtre <paramref name="windowHandle"/>, en pixels : sans la bordure invisible que
    /// Windows lui garde pour la redimensionner. Vide sans fenêtre, ou si Windows ne le dit pas.
    /// </summary>
    /// <param name="windowHandle">La fenêtre.</param>
    ScreenBounds VisibleBounds(nint windowHandle);

    /// <summary>Barre de titre sombre ou claire pour la fenêtre <paramref name="windowHandle"/> ; faux si Windows ne le permet pas.</summary>
    bool UseDarkTitleBar(nint windowHandle, bool dark);
}
