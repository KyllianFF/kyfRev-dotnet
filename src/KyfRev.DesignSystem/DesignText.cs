using System.Globalization;
using System.Resources;

namespace KyfRev.DesignSystem;

/// <summary>Textes des composants, lus dans les ressources (<c>Resources/DesignText.resx</c>) : l'interface se traduit sans toucher au code.</summary>
public static class DesignText
{
    private static readonly ResourceManager Resources = new("KyfRev.DesignSystem.Resources.DesignText", typeof(DesignText).Assembly);

    /// <summary>« Annuler ».</summary>
    public static string Cancel => Text(nameof(Cancel));

    /// <summary>« Fermer ».</summary>
    public static string Close => Text(nameof(Close));

    /// <summary>« Chargement… ».</summary>
    public static string Loading => Text(nameof(Loading));

    /// <summary>« OK ».</summary>
    public static string Ok => Text(nameof(Ok));

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » absent des ressources.");
}
