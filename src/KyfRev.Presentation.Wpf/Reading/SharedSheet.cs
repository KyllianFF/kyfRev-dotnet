using System.Collections.Frozen;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Schemas;
using KyfRev.Rendering;
using KyfRev.Rendering.Diagrams;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>La fiche que l'on partage : son titre, le dossier d'où partent ses liens, et le jour du partage.</summary>
/// <param name="Title">Son titre.</param>
/// <param name="Folder">Son dossier dans le coffre.</param>
/// <param name="Day">Le jour où elle est partagée.</param>
internal sealed record SharedFacts(string Title, string Folder, DateOnly Day);

/// <summary>Ce qui se dessine hors du texte d'une fiche partagée.</summary>
internal sealed class SharedDrawings
{
    /// <summary>Rien de dessiné : une formule reste en TeX, un schéma en code.</summary>
    public static SharedDrawings None { get; } = new();

    /// <summary>L'image de chaque formule, et de chaque schéma (par son élément <c>code</c>), qui a pu se dessiner.</summary>
    public IReadOnlyDictionary<RenderElement, string> Pictures { get; init; } = new Dictionary<RenderElement, string>();

    /// <summary>Le titre de chaque schéma réseau qui en a un (par son élément <c>code</c>).</summary>
    public IReadOnlyDictionary<RenderElement, string> Titles { get; init; } = new Dictionary<RenderElement, string>();
}

/// <summary>
/// Une fiche mise en forme pour l'extérieur, d'après le même contenu que la lecture : une page web d'un seul fichier, ou du texte
/// brut. Tout le texte de la fiche est échappé, et rien n'y est chargé d'ailleurs : une image distante reste signalée, et la
/// page interdit elle-même toute adresse.
/// </summary>
internal static partial class SharedSheet
{
    /// <summary>Les images du coffre, marquées ainsi, que le coffre intègre ensuite à la page.</summary>
    private const string VaultImageAttribute = "data-coffre";

    /// <summary>« [ ] », avec son espace.</summary>
    private const int TaskMarkLength = 4;

    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private static readonly Lazy<string> Styles = new(ReadStyles);

    /// <summary>Les couleurs du thème clair, par nom de ressource : la page est toujours claire, quel que soit le thème de l'application.</summary>
    private static readonly FrozenDictionary<string, TokenColor> Light =
        ThemeColors.For(ThemeKind.Light).ToFrozenDictionary(pair => DesignKeys.Brush(pair.Key), pair => pair.Value, StringComparer.Ordinal);

    /// <summary>Le fond de la page, sur lequel une couleur de la fiche doit rester lisible.</summary>
    private static TokenColor Ground => ThemeColors.For(ThemeKind.Light)[ColorRole.Surface];

    /// <summary>La page web de la fiche, un seul fichier : styles compris, sans script ni adresse à charger.</summary>
    /// <param name="root">Son contenu.</param>
    /// <param name="facts">Ce qui l'entoure.</param>
    /// <param name="drawings">Ce qui s'est dessiné.</param>
    public static string Page(RenderElement root, SharedFacts facts, SharedDrawings drawings)
    {
        var writer = new HtmlWriter(facts, drawings);
        writer.WriteChildren(root);
        var title = Escape(facts.Title);
        var heading = StartsWithTitle(root) ? string.Empty : $"<h1>{title}</h1>\n";
        var footer = Escape(SharingText.FooterOf(facts.Day.ToString("d MMMM yyyy", French)));
        return $"""
            <!doctype html>
            <html lang="fr">
            <head>
            <meta charset="utf-8">
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; style-src 'unsafe-inline'">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{title}</title>
            <style>
            :root {'{'} {Variables()} {'}'}
            {Styles.Value}
            </style>
            </head>
            <body>
            <main>
            {heading}{writer.Text}
            <footer>{footer}</footer>
            </main>
            </body>
            </html>

            """;
    }

    /// <summary>Le texte brut de la fiche : titres et paragraphes, formules en notation TeX, dessins nommés.</summary>
    /// <param name="root">Son contenu.</param>
    /// <param name="facts">Ce qui l'entoure.</param>
    /// <param name="drawings">Les titres des schémas.</param>
    public static string PlainText(RenderElement root, SharedFacts facts, SharedDrawings drawings)
    {
        var writer = new PlainWriter(facts, drawings);
        writer.Write(root);
        var body = BlankRun().Replace(writer.Text, "\n\n").Trim();
        return StartsWithTitle(root) ? $"{body}\n" : $"{facts.Title}\n\n{body}\n";
    }

    /// <summary>Le texte d'une balise ou d'un attribut, sans rien qui puisse y devenir une balise.</summary>
    internal static string Escape(string text) => text
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);

    /// <summary>Les couleurs qu'attendent les SmartArt, écrits avec les variables de l'éditeur.</summary>
    private static string Variables()
    {
        var colors = ThemeColors.For(ThemeKind.Light);
        return string.Join(' ', new (string Name, ColorRole Role)[]
        {
            ("--texte", ColorRole.Text),
            ("--texte-secondaire", ColorRole.TextSecondary),
            ("--surface-2", ColorRole.SurfaceAlt),
            ("--principale", ColorRole.Primary),
            ("--actif-fond", ColorRole.NavigationActiveBackground),
            ("--actif-texte", ColorRole.NavigationActiveText),
            ("--pastille-fond", ColorRole.PillPassageBackground),
            ("--pastille-texte", ColorRole.PillPassageText),
        }.Select(variable => $"{variable.Name}: {Hex(colors[variable.Role])};"));
    }

    private static string Hex(TokenColor color) => string.Create(CultureInfo.InvariantCulture, $"#{color.R:x2}{color.G:x2}{color.B:x2}");

    private static bool StartsWithTitle(RenderElement root) => root.Children.OfType<RenderElement>().FirstOrDefault() is { IsRepeatedTitle: true };

    private static string ReadStyles()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("kyfrev-partage.css")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string CodeOf(RenderElement code) => code.Children.OfType<RenderText>().FirstOrDefault()?.Value.TrimEnd('\n') ?? string.Empty;

    /// <summary>Le premier texte d'un élément : celui où est écrite la case d'une tâche.</summary>
    private static RenderText? FirstText(RenderElement element)
    {
        foreach (var child in element.Children)
        {
            switch (child)
            {
                case RenderText text when !string.IsNullOrWhiteSpace(text.Value):
                    return text;
                case RenderElement inner when FirstText(inner) is { } found:
                    return found;
            }
        }
        return null;
    }

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankRun();

    /// <summary>Écrit le contenu en HTML ; un élément inconnu ne garde que son contenu.</summary>
    private sealed class HtmlWriter(SharedFacts facts, SharedDrawings drawings)
    {
        private readonly StringBuilder _out = new();
        private RenderText? _taskText;
        private bool _taskDone;

        public string Text => _out.ToString();

        public void WriteChildren(RenderElement parent)
        {
            foreach (var child in parent.Children)
            {
                Write(child);
            }
        }

        private void Write(RenderNode node)
        {
            if (node is RenderText text)
            {
                WriteText(text);
                return;
            }
            var element = (RenderElement)node;
            switch (element.Tag)
            {
                case "br" or "hr":
                    _out.Append(CultureInfo.InvariantCulture, $"<{element.Tag}>");
                    break;
                case "img":
                    WriteImage(element);
                    break;
                case "a":
                    WriteLink(element);
                    break;
                case "pre":
                    WriteCode(element);
                    break;
                case "table":
                    WriteTable(element);
                    break;
                case "span" or "div" when element.Formula is { } formula:
                    WriteFormula(element, formula);
                    break;
                case "span" or "div":
                    Wrap(element, element.Tag, StyleOf(element));
                    break;
                case "ol" when element.ListStart is { } start:
                    Wrap(element, "ol", string.Create(CultureInfo.InvariantCulture, $" start=\"{start}\""));
                    break;
                case "li":
                    WriteItem(element);
                    break;
                case "section":
                    Wrap(element, "section", " class=\"footnotes\"");
                    break;
                case "article":
                    WriteChildren(element);
                    break;
                default:
                    Wrap(element, element.Tag, string.Empty);
                    break;
            }
        }

        private void Wrap(RenderElement element, string tag, string attributes)
        {
            _out.Append(CultureInfo.InvariantCulture, $"<{tag}{attributes}>");
            WriteChildren(element);
            _out.Append(CultureInfo.InvariantCulture, $"</{tag}>");
        }

        private void WriteText(RenderText text)
        {
            if (!ReferenceEquals(text, _taskText))
            {
                _out.Append(Escape(text.Value));
                return;
            }
            // La case d'une tâche se dessine : elle remplace « [ ] », écrit dans le texte.
            _taskText = null;
            _out.Append(_taskDone ? "☑ " : "☐ ").Append(Escape(text.Value[Math.Min(TaskMarkLength, text.Value.Length)..]));
        }

        private void WriteItem(RenderElement element)
        {
            var identifier = element.Identifier is { } id ? $" id=\"{Escape(id)}\"" : string.Empty;
            if (element.Task is { } done && FirstText(element) is { } first)
            {
                (_taskText, _taskDone) = (first, done);
                Wrap(element, "li", $"{identifier} class=\"tache{(done ? " faite" : string.Empty)}\"");
                return;
            }
            Wrap(element, "li", identifier);
        }

        private void WriteImage(RenderElement element)
        {
            var alternative = Escape(element.AlternativeText ?? string.Empty);
            switch (ContentTargets.OfImage(element.Target ?? string.Empty, facts.Folder))
            {
                case VaultImage vault:
                    _out.Append(CultureInfo.InvariantCulture, $"<img {VaultImageAttribute}=\"{Escape(vault.Path)}\" alt=\"{alternative}\">");
                    break;
                case EmbeddedImage embedded:
                    _out.Append(CultureInfo.InvariantCulture, $"<img src=\"{Escape(embedded.Data)}\" alt=\"{alternative}\">");
                    break;
                case BlockedImage blocked:
                    _out.Append(CultureInfo.InvariantCulture, $"<span class=\"image-bloquee\">{Escape(ReadingText.Blocked(blocked))}</span>");
                    break;
            }
        }

        /// <summary>Un lien vers le web garde son adresse, un renvoi sa place ; un lien vers une autre fiche n'a plus d'objet hors du coffre.</summary>
        private void WriteLink(RenderElement element)
        {
            var identifier = element.Identifier is { } id ? $" id=\"{Escape(id)}\"" : string.Empty;
            switch (ContentTargets.OfLink(element.Target ?? string.Empty, facts.Folder))
            {
                case ExternalLink external:
                    Wrap(element, "a", $" href=\"{Escape(external.Address)}\"{identifier}");
                    break;
                case PageAnchor anchor:
                    Wrap(element, "a", $" href=\"#{Escape(anchor.Identifier)}\"{identifier}");
                    break;
                default:
                    Wrap(element, "span", identifier);
                    break;
            }
        }

        private void WriteFormula(RenderElement element, string formula)
        {
            var tag = element.IsFormulaBlock ? "div" : "span";
            var name = Escape(formula);
            var kind = element.IsFormulaBlock ? "formule bloc" : "formule";
            var content = drawings.Pictures.GetValueOrDefault(element)
                ?? (element.IsFormulaBlock ? $"<code>$${name}$$</code>" : $"<code>${name}$</code>");
            _out.Append(CultureInfo.InvariantCulture, $"<{tag} class=\"{kind}\" role=\"math\" aria-label=\"{name}\">{content}</{tag}>");
        }

        private void WriteCode(RenderElement element)
        {
            var code = element.Children.OfType<RenderElement>().First();
            var text = CodeOf(code);
            if (code.Language == SmartArt.Language && SmartArt.Draw(code.CodeArguments, text) is { } scene)
            {
                _out.Append(CultureInfo.InvariantCulture, $"<figure class=\"diagramme\">{DiagramSvg.Write(scene)}</figure>");
                return;
            }
            if (drawings.Pictures.TryGetValue(code, out var picture))
            {
                var isNetwork = code.Language == NetworkBlock.Language;
                var caption = drawings.Titles.TryGetValue(code, out var title) ? $"<figcaption>{Escape(title)}</figcaption>" : string.Empty;
                _out.Append(CultureInfo.InvariantCulture, $"<figure class=\"{(isNetwork ? "reseau" : "diagramme")}\">{picture}{caption}</figure>");
                return;
            }
            if (code.Language is MermaidTemplates.Language or NetworkBlock.Language)
            {
                _out.Append(CultureInfo.InvariantCulture, $"<p class=\"schema-en-code\">{Escape(ReadingText.DiagramAsText)}</p>");
            }
            _out.Append(CultureInfo.InvariantCulture, $"<pre><code>{Escape(text)}</code></pre>");
        }

        private static string StyleOf(RenderElement element)
        {
            var styles = new List<string>();
            if (element.TextColor is { } color)
            {
                styles.Add($"color:{Hex(TokenColor.FromHex(color).ReadableOn(Ground))}");
            }
            if (element.FontScale is { } scale)
            {
                styles.Add(string.Create(CultureInfo.InvariantCulture, $"font-size:{scale}em"));
            }
            if (element.Alignment is { } alignment)
            {
                styles.Add($"text-align:{alignment}");
            }
            return styles.Count == 0 ? string.Empty : $" style=\"{string.Join(';', styles)}\"";
        }

        /// <summary>Un tableau, dans son apparence : sans ligne d'apparence, celle de la page.</summary>
        private void WriteTable(RenderElement element)
        {
            var look = element.Look ?? TableLook.Default;
            var groups = element.Children.OfType<RenderElement>().ToList();
            var rows = groups.SelectMany(group => group.Children.OfType<RenderElement>()).ToList();
            var columns = rows.Max(row => row.Children.OfType<RenderElement>().Count());
            var paints = TableBrushes.Of(look.Theme);
            var attributes = element.Look is null ? string.Empty : " class=\"apparence\"";
            _out.Append(CultureInfo.InvariantCulture, $"<table{attributes}{(look.FullWidth ? " style=\"width:100%\"" : string.Empty)}>");
            var index = 0;
            foreach (var group in groups)
            {
                _out.Append(CultureInfo.InvariantCulture, $"<{group.Tag}>");
                foreach (var row in group.Children.OfType<RenderElement>())
                {
                    _out.Append("<tr>");
                    var column = 0;
                    foreach (var cell in row.Children.OfType<RenderElement>())
                    {
                        var place = new TablePlace(look, paints, index, column, rows.Count, columns);
                        WriteCell(element, cell, place);
                        column++;
                    }
                    _out.Append("</tr>");
                    index++;
                }
                _out.Append(CultureInfo.InvariantCulture, $"</{group.Tag}>");
            }
            _out.Append("</table>");
        }

        private void WriteCell(RenderElement table, RenderElement cell, TablePlace place)
        {
            var styles = new List<string>();
            if (cell.Alignment is { } alignment)
            {
                styles.Add($"text-align:{alignment}");
            }
            if (table.Look is null)
            {
                Wrap(cell, cell.Tag, styles.Count == 0 ? string.Empty : $" style=\"{string.Join(';', styles)}\"");
                return;
            }
            var border = place.Border;
            var line = place.IsTotal || place.Look.Lines == TableLines.Header ? place.Paints.Total : place.Paints.Line;
            styles.Add(string.Create(CultureInfo.InvariantCulture, $"border-style:solid;border-width:{border.Top}px {border.Right}px {border.Bottom}px {border.Left}px;border-color:{Color(line)}"));
            if (place.IsHeader)
            {
                styles.Add($"background:{Color(place.Paints.Header)};color:{Color(place.Paints.HeaderText)};font-weight:600");
            }
            else if (place.IsBand)
            {
                styles.Add($"background:{Color(place.Paints.Band)}");
            }
            if (place.IsTotal)
            {
                styles.Add("font-weight:600");
            }
            Wrap(cell, place.IsHeader ? "th" : "td", $" style=\"{string.Join(';', styles)}\"");
        }

        private static string Color(string resource) => Hex(Light[resource]);
    }

    /// <summary>
    /// Écrit le contenu tel qu'un navigateur le rend en texte : un retour à la ligne après chaque bloc, une ligne vide après
    /// les titres et les paragraphes, les cases d'un tableau séparées par une tabulation.
    /// </summary>
    private sealed class PlainWriter(SharedFacts facts, SharedDrawings drawings)
    {
        private readonly StringBuilder _out = new();
        private bool _afterBreak;

        public string Text => _out.ToString();

        public void Write(RenderElement parent)
        {
            foreach (var child in parent.Children)
            {
                Write(child, parent);
            }
        }

        private void Write(RenderNode node, RenderElement parent)
        {
            if (node is RenderText text)
            {
                WriteText(text, parent);
                return;
            }
            var element = (RenderElement)node;
            switch (element.Tag)
            {
                case "br":
                    _out.Append('\n');
                    _afterBreak = true;
                    break;
                case "span" when element.Formula is { } formula:
                    _out.Append(CultureInfo.InvariantCulture, $"${formula}$");
                    break;
                case "div" when element.Formula is { } formula:
                    Block(() => _out.Append(CultureInfo.InvariantCulture, $"$$ {formula} $$"));
                    break;
                case "img":
                    WriteImage(element);
                    break;
                case "pre":
                    Block(() => WriteCode(element));
                    break;
                case "li":
                    NewLine();
                    Write(element);
                    NewLine();
                    break;
                case "tr":
                    WriteRow(element);
                    break;
                case "hr":
                    BlankLine();
                    break;
                case "p" or "ul" or "ol" or "blockquote" or "section" or "table" or "div" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
                    Block(() => Write(element));
                    break;
                default:
                    Write(element);
                    break;
            }
        }

        private void Block(Action content)
        {
            BlankLine();
            content();
            BlankLine();
        }

        private void NewLine()
        {
            if (_out.Length > 0 && _out[^1] != '\n')
            {
                _out.Append('\n');
            }
        }

        private void BlankLine()
        {
            NewLine();
            if (_out.Length > 0 && (_out.Length < 2 || _out[^2] != '\n'))
            {
                _out.Append('\n');
            }
        }

        /// <summary>Les sauts de ligne de mise en forme entre les blocs ne s'écrivent pas ; dans un texte, un saut simple se lit comme une espace.</summary>
        private void WriteText(RenderText text, RenderElement parent)
        {
            if (string.IsNullOrWhiteSpace(text.Value) && parent.Tag is "article" or "ul" or "ol" or "li" or "blockquote" or "section" or "table" or "thead" or "tbody" or "tr" or "div")
            {
                return;
            }
            var value = text.Value.Replace('\n', ' ');
            _out.Append(_afterBreak && value.StartsWith(' ') ? value[1..] : value);
            _afterBreak = false;
        }

        private void WriteImage(RenderElement element)
        {
            if (ContentTargets.OfImage(element.Target ?? string.Empty, facts.Folder) is BlockedImage blocked)
            {
                _out.Append(ReadingText.Blocked(blocked));
                return;
            }
            _out.Append(element.AlternativeText is { Length: > 0 } alternative ? SharingText.ImageNamedOf(alternative) : SharingText.Image);
        }

        private void WriteCode(RenderElement element)
        {
            var code = element.Children.OfType<RenderElement>().First();
            _out.Append(code.Language switch
            {
                NetworkBlock.Language => drawings.Titles.TryGetValue(code, out var title) ? SharingText.NetworkNamedOf(title) : SharingText.Network,
                MermaidTemplates.Language or SmartArt.Language => SharingText.Diagram,
                _ => CodeOf(code),
            });
        }

        private void WriteRow(RenderElement row)
        {
            NewLine();
            var isFirst = true;
            foreach (var cell in row.Children.OfType<RenderElement>())
            {
                if (!isFirst)
                {
                    _out.Append('\t');
                }
                Write(cell);
                isFirst = false;
            }
            NewLine();
        }
    }
}
