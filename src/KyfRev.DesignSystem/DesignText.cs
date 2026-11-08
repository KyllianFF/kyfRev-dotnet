using System.Globalization;
using System.Resources;

namespace KyfRev.DesignSystem;

/// <summary>Textes des composants, lus dans les ressources (<c>Resources/DesignText.resx</c>) : l'interface se traduit sans toucher au code.</summary>
public static class DesignText
{
    private static readonly ResourceManager Resources = new("KyfRev.DesignSystem.Resources.DesignText", typeof(DesignText).Assembly);

    /// <summary>« Copier ».</summary>
    public static string Copy => Text(nameof(Copy));

    /// <summary>« Couper ».</summary>
    public static string Cut => Text(nameof(Cut));

    /// <summary>« Coller ».</summary>
    public static string Paste => Text(nameof(Paste));

    /// <summary>« Tout sélectionner ».</summary>
    public static string SelectAll => Text(nameof(SelectAll));

    /// <summary>« Annuler ».</summary>
    public static string Cancel => Text(nameof(Cancel));

    /// <summary>« Fermer ».</summary>
    public static string Close => Text(nameof(Close));

    /// <summary>« Chargement… ».</summary>
    public static string Loading => Text(nameof(Loading));

    /// <summary>« OK ».</summary>
    public static string Ok => Text(nameof(Ok));

    /// <summary>« Calendrier ».</summary>
    public static string Calendar => Text(nameof(Calendar));

    /// <summary>« Ouvrir le calendrier ».</summary>
    public static string OpenCalendar => Text(nameof(OpenCalendar));

    /// <summary>« Précédent ».</summary>
    public static string PreviousPeriod => Text(nameof(PreviousPeriod));

    /// <summary>« Suivant ».</summary>
    public static string NextPeriod => Text(nameof(NextPeriod));

    /// <summary>« jj/mm/aaaa ».</summary>
    public static string DatePlaceholder => Text(nameof(DatePlaceholder));

    /// <summary>« Date refusée : elle n'existe pas ou sort des jours permis. ».</summary>
    public static string DateRefused => Text(nameof(DateRefused));

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » absent des ressources.");
}
