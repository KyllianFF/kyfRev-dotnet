using System.Globalization;
using System.Resources;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>Textes d'un coffre ouvert sur plusieurs postes, lus dans les ressources (<c>Resources/ComputersText.resx</c>).</summary>
public static class ComputersText
{
    private static readonly ResourceManager Resources = new("KyfRev.Presentation.Wpf.Resources.ComputersText", typeof(ComputersText).Assembly);

    /// <summary>« Ce coffre est ouvert sur un autre poste. » : un refus, là où la question ne se pose pas.</summary>
    public static string Occupied => Text(nameof(Occupied));

    /// <summary>Ce que l'ouverture sur deux postes implique.</summary>
    public static string OccupiedHelp => Text(nameof(OccupiedHelp));

    /// <summary>« Ouvrir ».</summary>
    public static string OccupiedOpen => Text(nameof(OccupiedOpen));

    /// <summary>« Lecture seule ».</summary>
    public static string OccupiedReadOnly => Text(nameof(OccupiedReadOnly));

    /// <summary>« Ce coffre est ouvert sur un autre poste ».</summary>
    public static string OccupiedTitle => Text(nameof(OccupiedTitle));

    /// <summary>« Lecture seule », sans autre poste connu.</summary>
    public static string ReadOnlyAlone => Text(nameof(ReadOnlyAlone));

    /// <summary>« Passer en écriture ».</summary>
    public static string Writable => Text(nameof(Writable));

    /// <summary>« … vient d'ouvrir ce coffre. … ».</summary>
    public static string ArrivedOf(object computers) => Format("Arrived", computers);

    /// <summary>« Lecture seule · ouvert aussi sur … ».</summary>
    public static string ReadOnlyBannerOf(object computers) => Format("ReadOnlyBanner", computers);

    /// <summary>« depuis 14 h 05 · vu il y a 2 minutes ».</summary>
    public static string SeenOf(DateTimeOffset since, TimeSpan ago)
    {
        var at = since.ToString("H' h 'mm", CultureInfo.GetCultureInfo("fr-FR"));
        var minutes = (int)Math.Floor(Math.Max(0, ago.TotalMinutes));
        return minutes switch
        {
            0 => Format("SeenJustNow", at),
            1 => Format("SeenOneMinuteAgo", at),
            _ => Format("SeenMinutesAgo", at, minutes),
        };
    }

    private static string Format(string name, object value) => string.Format(CultureInfo.CurrentCulture, Text(name), value);

    private static string Format(string name, object first, object second) => string.Format(CultureInfo.CurrentCulture, Text(name), first, second);

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » absent des ressources.");
}
