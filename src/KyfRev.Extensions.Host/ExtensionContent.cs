using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using KyfRev.Application;

namespace KyfRev.Extensions.Host;

/// <summary>Un élément de ce qu'une extension affiche, une fois passé par la liste blanche.</summary>
/// <param name="Tag">Sa nature, parmi celles de la liste blanche ; <see cref="TextTag"/> pour du texte.</param>
/// <param name="Text">Son texte, pour un élément de texte.</param>
/// <param name="Classes">Ses classes, parmi celles que kyfRev connaît.</param>
/// <param name="Data">Ses attributs <c>data-</c>, sous leur nom de <c>dataset</c>.</param>
/// <param name="Attributes">Ses attributs, parmi ceux que kyfRev lit.</param>
/// <param name="Children">Ce qu'il contient.</param>
public sealed record ExtensionNode(
    string Tag,
    string Text,
    ImmutableArray<string> Classes,
    ImmutableDictionary<string, string> Data,
    ImmutableDictionary<string, string> Attributes,
    ImmutableArray<ExtensionNode> Children)
{
    /// <summary>La nature d'un élément de texte.</summary>
    public const string TextTag = "#texte";

    /// <summary>L'action que déclenche cet élément ; rien s'il n'en déclenche pas.</summary>
    public string? Action => Data.GetValueOrDefault("action");

    /// <summary>Tout le texte qu'il contient.</summary>
    public string AllText => Tag == TextTag ? Text : string.Concat(Children.Select(child => child.AllText));

    /// <summary>Cet élément a cette classe.</summary>
    /// <param name="name">Le nom de la classe.</param>
    public bool Has(string name) => Classes.Contains(name);

    /// <summary>La valeur d'un attribut ; rien s'il n'y est pas.</summary>
    /// <param name="name">Le nom de l'attribut.</param>
    public string? Attribute(string name) => Attributes.GetValueOrDefault(name);
}

/// <summary>
/// Ce qu'une extension affiche : son HTML, lu par le moteur web sans rien exécuter, puis filtré ici. Rien ne passe qui ne soit dans
/// la liste blanche : ni style, ni lien, ni image, ni cadre, ni script, ni attribut d'événement. Le résultat n'est jamais du HTML :
/// la présentation en fait des éléments natifs, et une extension ne peut donc ni imiter un dialogue, ni faire sortir un lien.
/// </summary>
public sealed class ExtensionContent
{
    private const int MaxDepth = 64;
    private const int MaxDataEntries = 32;
    private const int MaxDataLength = 4096;

    /// <summary>Les balises gardées, et ce qu'elles deviennent.</summary>
    private static readonly FrozenDictionary<string, string> Kept = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["h1"] = "h1",
        ["h2"] = "h2",
        ["h3"] = "h3",
        ["h4"] = "h4",
        ["h5"] = "h5",
        ["h6"] = "h6",
        ["p"] = "p",
        ["div"] = "div",
        ["section"] = "div",
        ["article"] = "div",
        ["header"] = "div",
        ["footer"] = "div",
        ["main"] = "div",
        ["aside"] = "div",
        ["nav"] = "div",
        ["blockquote"] = "div",
        ["ul"] = "ul",
        ["ol"] = "ol",
        ["li"] = "li",
        ["table"] = "table",
        ["thead"] = "thead",
        ["tbody"] = "tbody",
        ["tfoot"] = "tbody",
        ["tr"] = "tr",
        ["th"] = "th",
        ["td"] = "td",
        ["details"] = "details",
        ["summary"] = "summary",
        ["pre"] = "pre",
        ["hr"] = "hr",
        ["br"] = "br",
        ["span"] = "span",
        ["a"] = "span",
        ["abbr"] = "span",
        ["label"] = "label",
        ["form"] = "form",
        ["fieldset"] = "div",
        ["legend"] = "p",
        ["progress"] = "progress",
        ["meter"] = "progress",
        ["mark"] = "span",
        ["u"] = "span",
        ["s"] = "span",
        ["sub"] = "span",
        ["sup"] = "span",
        ["time"] = "time",
        ["small"] = "small",
        ["strong"] = "strong",
        ["b"] = "strong",
        ["em"] = "em",
        ["i"] = "em",
        ["code"] = "code",
        ["kbd"] = "code",
        ["samp"] = "code",
        ["button"] = "button",
        ["input"] = "input",
        ["select"] = "select",
        ["option"] = "option",
        ["textarea"] = "textarea",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Les balises retirées avec tout ce qu'elles contiennent. Une autre balise inconnue disparaît, son contenu reste.</summary>
    private static readonly FrozenSet<string> Dropped = FrozenSet.ToFrozenSet(
        ["script", "style", "iframe", "object", "embed", "applet", "link", "meta", "base", "head", "title", "template", "noscript", "svg", "math", "audio", "video", "canvas", "frame", "frameset"],
        StringComparer.Ordinal);

    private static readonly FrozenSet<string> KnownClasses = FrozenSet.ToFrozenSet(
        ["bouton", "principal", "discret", "champ", "pastille", "carte", "secondaire", "titre-extension", "actions", "question", "reponse", "frise", "quiz"],
        StringComparer.Ordinal);

    private static readonly FrozenSet<string> KnownAttributes = FrozenSet.ToFrozenSet(
        ["type", "value", "placeholder", "checked", "selected", "disabled", "open", "aria-label", "name", "max", "required"],
        StringComparer.Ordinal);

    /// <summary>Les types de champ gardés : ni fichier, ni mot de passe, ni champ caché.</summary>
    private static readonly FrozenSet<string> TextInputs = FrozenSet.ToFrozenSet(
        ["", "text", "search", "number", "email", "url", "tel", "date", "time", "checkbox", "radio"],
        StringComparer.OrdinalIgnoreCase);

    private ExtensionContent(ImmutableArray<ExtensionNode> nodes)
    {
        Nodes = nodes;
    }

    /// <summary>Un contenu vide.</summary>
    public static ExtensionContent Empty { get; } = new([]);

    /// <summary>Ce qui s'affiche, dans l'ordre.</summary>
    public ImmutableArray<ExtensionNode> Nodes { get; }

    /// <summary>Tout son texte.</summary>
    public string Text => string.Concat(Nodes.Select(node => node.AllText));

    /// <summary>Lit l'arbre que le moteur web a fait du HTML, et n'en garde que ce que dit la liste blanche.</summary>
    /// <param name="tree">Le tableau des éléments, en JSON.</param>
    public static Outcome<ExtensionContent> Read(JsonElement tree)
    {
        if (tree.ValueKind != JsonValueKind.Array)
        {
            return Outcome.Refused<ExtensionContent>("rendu illisible");
        }
        var nodes = Children(tree, 0, preserveSpaces: false);
        return nodes is { } read ? Outcome.Of(new ExtensionContent(read)) : Outcome.Refused<ExtensionContent>("rendu trop profond");
    }

    /// <summary>Tous les éléments, en profondeur.</summary>
    public IEnumerable<ExtensionNode> Descendants()
    {
        var waiting = new Stack<ExtensionNode>(Nodes.Reverse());
        while (waiting.TryPop(out var node))
        {
            yield return node;
            foreach (var child in node.Children.Reverse())
            {
                waiting.Push(child);
            }
        }
    }

    private static ImmutableArray<ExtensionNode>? Children(JsonElement list, int depth, bool preserveSpaces)
    {
        if (depth > MaxDepth)
        {
            return null;
        }
        var nodes = ImmutableArray.CreateBuilder<ExtensionNode>();
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            if (item.TryGetProperty("texte", out var text) && text.ValueKind == JsonValueKind.String)
            {
                var value = text.GetString()!;
                nodes.Add(Plain(preserveSpaces ? value : Collapse(value)));
                continue;
            }
            if (!Element(item, depth, preserveSpaces, nodes))
            {
                return null;
            }
        }
        return nodes.DrainToImmutable();
    }

    /// <summary>Ajoute ce que devient un élément ; faux s'il est trop profond.</summary>
    private static bool Element(JsonElement item, int depth, bool preserveSpaces, ImmutableArray<ExtensionNode>.Builder nodes)
    {
        var tag = TextOf(item, "balise") ?? string.Empty;
        var attributes = Attributes(item);
        if (Dropped.Contains(tag) || attributes.ContainsKey("hidden"))
        {
            return true;
        }
        if (tag == "img")
        {
            // Une image ne sort jamais du moteur : son texte de remplacement seul reste.
            if (TextOf(item, "alt") is { Length: > 0 } alternative)
            {
                nodes.Add(Plain(Collapse(alternative)));
            }
            return true;
        }
        var kept = Kept.GetValueOrDefault(tag);
        if (tag == "input" && !TextInputs.Contains(attributes.GetValueOrDefault("type", string.Empty)))
        {
            return true;
        }
        var list = item.TryGetProperty("enfants", out var inner) && inner.ValueKind == JsonValueKind.Array ? inner : default;
        var children = list.ValueKind == JsonValueKind.Array ? Children(list, depth + 1, preserveSpaces || kept == "pre") : [];
        if (children is not { } content)
        {
            return false;
        }
        if (kept is null)
        {
            nodes.AddRange(content);
            return true;
        }
        nodes.Add(new ExtensionNode(kept, string.Empty, Classes(item), Data(item), attributes, content));
        return true;
    }

    private static ExtensionNode Plain(string text) => new(ExtensionNode.TextTag, text, [], [], [], []);

    private static string? TextOf(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static ImmutableArray<string> Classes(JsonElement item) =>
        item.TryGetProperty("classes", out var list) && list.ValueKind == JsonValueKind.Array
            ? [.. list.EnumerateArray().Where(name => name.ValueKind == JsonValueKind.String).Select(name => name.GetString()!).Where(KnownClasses.Contains).Distinct(StringComparer.Ordinal)]
            : [];

    private static ImmutableDictionary<string, string> Data(JsonElement item) =>
        item.TryGetProperty("donnees", out var data) && data.ValueKind == JsonValueKind.Object
            ? data.EnumerateObject()
                .Where(entry => entry.Value.ValueKind == JsonValueKind.String && entry.Value.GetString()!.Length <= MaxDataLength)
                .Take(MaxDataEntries)
                .ToImmutableDictionary(entry => entry.Name, entry => entry.Value.GetString()!, StringComparer.Ordinal)
            : [];

    private static ImmutableDictionary<string, string> Attributes(JsonElement item)
    {
        var read = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        if (item.TryGetProperty("attributs", out var attributes) && attributes.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in attributes.EnumerateObject().Where(entry => entry.Value.ValueKind == JsonValueKind.String))
            {
                if (KnownAttributes.Contains(entry.Name) || entry.Name == "hidden")
                {
                    read[entry.Name] = entry.Value.GetString()!;
                }
            }
        }
        return read.ToImmutable();
    }

    /// <summary>Les espaces comme en HTML : une suite d'espaces et de retours à la ligne en vaut un seul.</summary>
    private static string Collapse(string text)
    {
        var result = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                space = true;
                continue;
            }
            if (space)
            {
                result.Append(' ');
                space = false;
            }
            result.Append(c);
        }
        if (space)
        {
            result.Append(' ');
        }
        return result.ToString();
    }
}
