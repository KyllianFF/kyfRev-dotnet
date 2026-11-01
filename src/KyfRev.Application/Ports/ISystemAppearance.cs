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

/// <summary>Cadre des fenêtres dessiné par Windows.</summary>
public interface IWindowChrome
{
    /// <summary>Barre de titre sombre ou claire pour la fenêtre <paramref name="windowHandle"/> ; faux si Windows ne le permet pas.</summary>
    bool UseDarkTitleBar(nint windowHandle, bool dark);
}
