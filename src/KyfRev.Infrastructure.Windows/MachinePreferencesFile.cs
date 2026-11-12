using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;

namespace KyfRev.Infrastructure.Windows;

/// <summary>
/// Les préférences de ce poste, gardées dans <c>preferences.json</c>, parmi ses données propres
/// (<see cref="VaultLocations.LocalData"/>), jamais dans le coffre. Un fichier absent ou illisible
/// rend les valeurs par défaut ; une valeur inconnue rend la sienne, sans toucher aux autres.
/// </summary>
public sealed class MachinePreferencesFile : IMachinePreferencesStore
{
    private const int Format = 1;

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly Dictionary<string, ThemePreference> Themes = new(StringComparer.Ordinal)
    {
        ["systeme"] = ThemePreference.System,
        ["clair"] = ThemePreference.Light,
        ["sombre"] = ThemePreference.Dark,
    };

    private static readonly Dictionary<string, ReadingWidth> Widths = new(StringComparer.Ordinal)
    {
        ["limitee"] = ReadingWidth.Comfortable,
        ["pleine"] = ReadingWidth.Full,
    };

    private static readonly Dictionary<string, ReadingTextSize> TextSizes = new(StringComparer.Ordinal)
    {
        ["normale"] = ReadingTextSize.Normal,
        ["grande"] = ReadingTextSize.Large,
        ["tres-grande"] = ReadingTextSize.ExtraLarge,
    };

    private static readonly Dictionary<string, ReadingLineSpacing> LineSpacings = new(StringComparer.Ordinal)
    {
        ["normal"] = ReadingLineSpacing.Normal,
        ["aere"] = ReadingLineSpacing.Airy,
    };

    private static readonly Dictionary<string, ReadingFont> Fonts = new(StringComparer.Ordinal)
    {
        ["sans"] = ReadingFont.Sans,
        ["serif"] = ReadingFont.Serif,
        ["manuscrite"] = ReadingFont.Handwriting,
        ["feutre"] = ReadingFont.Marker,
        ["machine"] = ReadingFont.Typewriter,
    };

    private readonly string _file;

    /// <summary>Les préférences gardées dans le dossier <paramref name="folder"/>.</summary>
    public MachinePreferencesFile(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        _file = Path.Combine(folder, "preferences.json");
    }

    /// <inheritdoc/>
    public MachinePreferences Load()
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(_file));
            return document.RootElement.ValueKind == JsonValueKind.Object ? Read(document.RootElement) : new MachinePreferences();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new MachinePreferences();
        }
    }

    /// <inheritdoc/>
    public bool Save(MachinePreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("format", Format);
            writer.WriteString("theme", Name(Themes, preferences.Theme));
            writer.WriteNumber("zoom", preferences.Zoom);
            writer.WriteNumber("zoomFiche", preferences.SheetZoom);
            // La mise en page ne s'écrit que si elle a été choisie : un fichier sans elle se lit comme avant.
            if (preferences.Page.IsPaged)
            {
                writer.WriteString("miseEnPage", preferences.Page.Written());
            }
            if (preferences.SheetPages.Items.Count > 0)
            {
                writer.WriteStartObject("misesEnPage");
                foreach (var (sheet, layout) in preferences.SheetPages.Items)
                {
                    writer.WriteString(sheet, layout.Written());
                }
                writer.WriteEndObject();
            }
            if (preferences.Paper != PaperChoices.Default)
            {
                writer.WriteBoolean("papier", preferences.Paper.IsOn);
                writer.WriteStartObject("papiers");
                foreach (var format in PaperStyles.Formats)
                {
                    writer.WriteString(PaperStyles.FormatName(format), PaperStyles.Name(preferences.Paper.Of(format)));
                }
                writer.WriteEndObject();
            }
            if (preferences.MenuOrder.Length > 0)
            {
                writer.WriteString("menu", preferences.MenuOrder);
            }
            if (preferences.Shortcuts.Length > 0)
            {
                writer.WriteString("raccourcis", preferences.Shortcuts);
            }
            writer.WriteString("largeur", Name(Widths, preferences.Width));
            writer.WriteString("taille", Name(TextSizes, preferences.TextSize));
            writer.WriteString("interligne", Name(LineSpacings, preferences.LineSpacing));
            writer.WriteString("police", Name(Fonts, preferences.Font));
            writer.WriteBoolean("concentration", preferences.FocusMode);
            writer.WriteBoolean("motsDefinis", preferences.MarkDefinedWords);
            writer.WriteBoolean("captureGlobale", preferences.GlobalCapture);
            writer.WriteBoolean("animationsReduites", preferences.ReducedMotion);
            writer.WriteBoolean("contrasteEleve", preferences.HighContrast);
            writer.WriteBoolean("ciblesAgrandies", preferences.LargerTargets);
            if (preferences.Voice is { } voice)
            {
                writer.WriteString("voix", voice);
            }
            writer.WriteNumber("vitesse", preferences.Rate);
            writer.WriteEndObject();
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllBytes(_file, buffer.WrittenSpan.ToArray());
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static MachinePreferences Read(JsonElement root)
    {
        var defaults = new MachinePreferences();
        return new MachinePreferences
        {
            Theme = Choice(root, "theme", Themes, defaults.Theme),
            Zoom = Number(root, "zoom", defaults.Zoom),
            SheetZoom = Number(root, "zoomFiche", defaults.SheetZoom),
            Page = PageLayout.Parse(root.TryGetProperty("miseEnPage", out var page) && page.ValueKind == JsonValueKind.String ? page.GetString() : null),
            SheetPages = SheetPages(root),
            Paper = Paper(root),
            MenuOrder = Text(root, "menu"),
            Shortcuts = Text(root, "raccourcis"),
            Width = Choice(root, "largeur", Widths, defaults.Width),
            TextSize = Choice(root, "taille", TextSizes, defaults.TextSize),
            LineSpacing = Choice(root, "interligne", LineSpacings, defaults.LineSpacing),
            Font = Choice(root, "police", Fonts, defaults.Font),
            FocusMode = Flag(root, "concentration", defaults.FocusMode),
            MarkDefinedWords = Flag(root, "motsDefinis", defaults.MarkDefinedWords),
            GlobalCapture = Flag(root, "captureGlobale", defaults.GlobalCapture),
            ReducedMotion = Flag(root, "animationsReduites", defaults.ReducedMotion),
            HighContrast = Flag(root, "contrasteEleve", defaults.HighContrast),
            LargerTargets = Flag(root, "ciblesAgrandies", defaults.LargerTargets),
            Voice = root.TryGetProperty("voix", out var voice) && voice.ValueKind == JsonValueKind.String ? voice.GetString() : defaults.Voice,
            Rate = Number(root, "vitesse", defaults.Rate),
        };
    }

    private static string Name<T>(Dictionary<string, T> names, T value)
        where T : struct, Enum =>
        names.First(entry => entry.Value.Equals(value)).Key;

    private static T Choice<T>(JsonElement root, string name, Dictionary<string, T> names, T fallback)
        where T : struct, Enum =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && names.TryGetValue(value.GetString()!, out var known) ? known : fallback;

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static PaperChoices Paper(JsonElement root)
    {
        var choices = PaperChoices.Default with { IsOn = Flag(root, "papier", false) };
        if (root.TryGetProperty("papiers", out var papers) && papers.ValueKind == JsonValueKind.Object)
        {
            foreach (var format in PaperStyles.Formats)
            {
                if (papers.TryGetProperty(PaperStyles.FormatName(format), out var name) && name.ValueKind == JsonValueKind.String)
                {
                    choices = choices.With(format, PaperStyles.Parse(name.GetString()));
                }
            }
        }
        return choices;
    }

    private static SheetPageLayouts SheetPages(JsonElement root)
    {
        var layouts = SheetPageLayouts.None;
        if (root.TryGetProperty("misesEnPage", out var pages) && pages.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in pages.EnumerateObject().Where(entry => entry.Name.Length > 0 && entry.Value.ValueKind == JsonValueKind.String))
            {
                layouts = layouts.With(entry.Name, PageLayout.Parse(entry.Value.GetString()));
            }
        }
        return layouts;
    }

    private static double Number(JsonElement root, string name, double fallback) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.GetDouble() > 0 ? value.GetDouble() : fallback;

    private static bool Flag(JsonElement root, string name, bool fallback) =>
        root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : fallback;
}
