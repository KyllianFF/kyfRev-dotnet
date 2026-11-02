using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>Ce que l'application demande à l'éditeur visuel.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(LoadRequest), "load")]
[JsonDerivedType(typeof(ThemeRequest), "theme")]
[JsonDerivedType(typeof(SyncRequest), "sync")]
[JsonDerivedType(typeof(MarkRequest), "mark")]
[JsonDerivedType(typeof(HeadingRequest), "heading")]
[JsonDerivedType(typeof(ListRequest), "list")]
[JsonDerivedType(typeof(CaseRequest), "case")]
[JsonDerivedType(typeof(TableRequest), "table")]
[JsonDerivedType(typeof(TableActionRequest), "tableAction")]
[JsonDerivedType(typeof(CodeRequest), "code")]
[JsonDerivedType(typeof(RuleRequest), "rule")]
[JsonDerivedType(typeof(AskFormulaRequest), "askFormula")]
[JsonDerivedType(typeof(FormulaRequest), "formula")]
[JsonDerivedType(typeof(AskLinkRequest), "askLink")]
[JsonDerivedType(typeof(LinkRequest), "link")]
[JsonDerivedType(typeof(UnlinkRequest), "unlink")]
[JsonDerivedType(typeof(ClearFormatRequest), "clearFormat")]
[JsonDerivedType(typeof(ColorRequest), "color")]
[JsonDerivedType(typeof(SizeRequest), "size")]
[JsonDerivedType(typeof(AlignRequest), "align")]
[JsonDerivedType(typeof(ImageRequest), "image")]
[JsonDerivedType(typeof(SheetLinkRequest), "sheetLink")]
[JsonDerivedType(typeof(ReplaceWordRequest), "replaceWord")]
[JsonDerivedType(typeof(UndoRequest), "undo")]
[JsonDerivedType(typeof(RedoRequest), "redo")]
[JsonDerivedType(typeof(FocusRequest), "focus")]
[JsonDerivedType(typeof(GoToHeadingRequest), "goToHeading")]
[JsonDerivedType(typeof(ClipboardRequest), "clipboard")]
[JsonDerivedType(typeof(ListOpenRequest), "listOpen")]
[JsonDerivedType(typeof(ResourceRequest), "resource")]
internal abstract record EditorRequest;

/// <summary>Ouvre ce Markdown : rien à enregistrer, rien à annuler.</summary>
internal sealed record LoadRequest(string Text, string Folder) : EditorRequest;

/// <summary>Les couleurs du thème, en variables CSS.</summary>
internal sealed record ThemeRequest(IReadOnlyDictionary<string, string> Variables) : EditorRequest;

/// <summary>Demande le Markdown du moment, tout de suite.</summary>
internal sealed record SyncRequest(int Id) : EditorRequest;

/// <summary>Met en forme la sélection, ou retire sa mise en forme.</summary>
internal sealed record MarkRequest(InlineMark Mark) : EditorRequest;

/// <summary>Donne un niveau de titre ; 0 en refait du texte.</summary>
internal sealed record HeadingRequest(int Level) : EditorRequest;

/// <summary>Met en liste, ou en sort.</summary>
internal sealed record ListRequest(ListKind List) : EditorRequest;

/// <summary>Change la casse ; sans style, la suivante.</summary>
internal sealed record CaseRequest(CaseStyle? Style) : EditorRequest;

/// <summary>Insère un tableau.</summary>
internal sealed record TableRequest(int Rows, int Columns) : EditorRequest;

/// <summary>Ajoute ou retire une ligne, une colonne, le tableau.</summary>
internal sealed record TableActionRequest(string Action) : EditorRequest;

/// <summary>Crée un bloc de code, ou change le langage de celui où est le curseur.</summary>
internal sealed record CodeRequest(string Language) : EditorRequest;

/// <summary>Insère une ligne horizontale.</summary>
internal sealed record RuleRequest : EditorRequest;

/// <summary>Demande la formule sélectionnée, pour ouvrir sa saisie.</summary>
internal sealed record AskFormulaRequest : EditorRequest;

/// <summary>Insère une formule, ou remplace celle de cette position.</summary>
internal sealed record FormulaRequest(string Tex, bool Block, int? Position) : EditorRequest;

/// <summary>Demande le lien du curseur, pour ouvrir sa saisie.</summary>
internal sealed record AskLinkRequest : EditorRequest;

/// <summary>Crée ou modifie un lien.</summary>
internal sealed record LinkRequest(string Label, string Address) : EditorRequest;

/// <summary>Retire le lien du curseur.</summary>
internal sealed record UnlinkRequest : EditorRequest;

/// <summary>Efface la mise en forme de la sélection.</summary>
internal sealed record ClearFormatRequest : EditorRequest;

/// <summary>Pose une couleur sur la sélection, écrite <c>#rrggbb</c> ; rien : celle du thème.</summary>
internal sealed record ColorRequest(string? Color) : EditorRequest;

/// <summary>Pose une taille sur la sélection, écrite <c>1.25em</c> ; rien : la taille normale.</summary>
internal sealed record SizeRequest(string? Size) : EditorRequest;

/// <summary>Aligne les blocs de la sélection.</summary>
internal sealed record AlignRequest(TextAlign Align) : EditorRequest;

/// <summary>Insère une image du coffre.</summary>
internal sealed record ImageRequest(string Link, string Alternative, int? Position) : EditorRequest;

/// <summary>Remplace la saisie « [[… » par un lien vers une fiche.</summary>
internal sealed record SheetLinkRequest(string Label, string Address) : EditorRequest;

/// <summary>Remplace une étendue par un mot, comme une frappe.</summary>
internal sealed record ReplaceWordRequest(int Start, int End, string Text) : EditorRequest;

/// <summary>Annule le dernier geste.</summary>
internal sealed record UndoRequest : EditorRequest;

/// <summary>Rétablit le dernier geste annulé.</summary>
internal sealed record RedoRequest : EditorRequest;

/// <summary>Rend le focus au texte.</summary>
internal sealed record FocusRequest : EditorRequest;

/// <summary>Amène un titre en haut de la vue.</summary>
internal sealed record GoToHeadingRequest(string Title, int Occurrence) : EditorRequest;

/// <summary>Couper, copier, coller, coller sans mise en forme.</summary>
internal sealed record ClipboardRequest(string Action) : EditorRequest;

/// <summary>Dit si la liste des fiches est ouverte : elle prend alors ses touches.</summary>
internal sealed record ListOpenRequest(bool Open) : EditorRequest;

/// <summary>Le contenu d'une image du coffre, en adresse <c>data:</c> ; rien si elle est introuvable.</summary>
internal sealed record ResourceRequest(string Path, string? DataUrl) : EditorRequest;

/// <summary>Ce que l'éditeur visuel dit à l'application.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ReadyEvent), "ready")]
[JsonDerivedType(typeof(EditedEvent), "edited")]
[JsonDerivedType(typeof(TextEvent), "text")]
[JsonDerivedType(typeof(SaveEvent), "save")]
[JsonDerivedType(typeof(ToolsEvent), "tools")]
[JsonDerivedType(typeof(AskLinkEvent), "askLink")]
[JsonDerivedType(typeof(AskFormulaEvent), "askFormula")]
[JsonDerivedType(typeof(LinkInputEvent), "linkInput")]
[JsonDerivedType(typeof(ListKeyEvent), "listKey")]
[JsonDerivedType(typeof(MenuEvent), "menu")]
[JsonDerivedType(typeof(DroppedEvent), "dropped")]
[JsonDerivedType(typeof(AskResourceEvent), "askResource")]
internal abstract record EditorEvent;

/// <summary>La page est prête.</summary>
internal sealed record ReadyEvent : EditorEvent;

/// <summary>L'utilisateur a modifié le texte ; le Markdown suit.</summary>
internal sealed record EditedEvent : EditorEvent;

/// <summary>Le Markdown du moment ; avec un numéro, la réponse à une demande.</summary>
internal sealed record TextEvent(int? Id, string Markdown) : EditorEvent;

/// <summary>Ctrl+S, ou le texte a perdu le focus.</summary>
internal sealed record SaveEvent : EditorEvent;

/// <summary>Ce que le ruban affiche.</summary>
internal sealed record ToolsEvent(bool Bold, bool Italic, bool Underline, bool Strike, bool Superscript, bool Subscript, bool Link, int Heading, bool InCode, ListKind? List, bool Table, string? Color, string? Size, TextAlign Align, bool CanUndo, bool CanRedo) : EditorEvent;

/// <summary>Ctrl+K, ou la réponse à une demande : le lien à préremplir.</summary>
internal sealed record AskLinkEvent(string Label, string Address) : EditorEvent;

/// <summary>Un double-clic sur une formule, ou la réponse à une demande.</summary>
internal sealed record AskFormulaEvent(int? Position, string Tex, bool Block) : EditorEvent;

/// <summary>Une saisie « [[… » au curseur ; sans recherche, plus de saisie.</summary>
internal sealed record LinkInputEvent(string? Query, int Start, double X, double Y) : EditorEvent;

/// <summary>Une touche prise par la liste des fiches.</summary>
internal sealed record ListKeyEvent(string Key) : EditorEvent;

/// <summary>Un clic droit : ce qu'il faut pour le menu natif.</summary>
internal sealed record MenuEvent(string? Word, int WordStart, bool HasSelection, bool InCode, bool InTable, bool OnLink) : EditorEvent;

/// <summary>Une image collée ou déposée ; sans contenu, elle est trop lourde pour être lue.</summary>
internal sealed record DroppedEvent(string Name, string? Base64, int? Position) : EditorEvent;

/// <summary>La page a besoin d'une image du coffre.</summary>
internal sealed record AskResourceEvent(string Path) : EditorEvent;

/// <summary>
/// Le contrat du bus : le même des deux côtés. Les types de la page (<c>tools/visual-editor/src/messages.ts</c>)
/// sont tirés de ces messages, et un test vérifie qu'ils sont à jour.
/// </summary>
internal static class VisualContract
{
    /// <summary>La forme des messages en JSON.</summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        AllowOutOfOrderMetadataProperties = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Les types de la page.</summary>
    public static string TypeScript()
    {
        var text = new StringBuilder();
        text.Append("// Généré depuis VisualMessages.cs : ne pas modifier à la main.\n\n");
        Union(text, "VersEditeur", typeof(EditorRequest));
        text.Append('\n');
        Union(text, "DepuisEditeur", typeof(EditorEvent));
        return text.ToString();
    }

    private static void Union(StringBuilder text, string name, Type root)
    {
        text.Append("export type ").Append(name).Append(" =\n");
        var context = new NullabilityInfoContext();
        foreach (var derived in root.GetCustomAttributes<JsonDerivedTypeAttribute>())
        {
            text.Append("  | { kind: \"").Append(derived.TypeDiscriminator).Append('"');
            foreach (var property in derived.DerivedType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                text.Append("; ").Append(JsonNamingPolicy.CamelCase.ConvertName(property.Name)).Append(": ").Append(Shape(property, context));
            }
            text.Append(" }\n");
        }
        text.Length--;
        text.Append(";\n");
    }

    private static string Shape(PropertyInfo property, NullabilityInfoContext context)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var shape = type switch
        {
            _ when type == typeof(string) => "string",
            _ when type == typeof(int) || type == typeof(double) => "number",
            _ when type == typeof(bool) => "boolean",
            { IsEnum: true } => string.Join(" | ", Enum.GetNames(type).Select(name => $"\"{JsonNamingPolicy.CamelCase.ConvertName(name)}\"")),
            _ => "Record<string, string>",
        };
        return context.Create(property).ReadState == NullabilityState.Nullable || Nullable.GetUnderlyingType(property.PropertyType) is not null
            ? $"{shape} | null"
            : shape;
    }
}
