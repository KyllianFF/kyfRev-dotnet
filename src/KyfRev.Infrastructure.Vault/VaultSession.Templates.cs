using System.Globalization;
using System.Text;
using KyfRev.Application.Text;
using KyfRev.Infrastructure.Vault.Paths;
using KyfRev.Infrastructure.Vault.Sheets;

namespace KyfRev.Infrastructure.Vault;

/// <remarks>
/// Modèles de fiche : intégrés à l'application ou propres au coffre (<c>modeles/*.md</c>), avec
/// trois variables : <c>{{titre}}</c>, <c>{{date}}</c> et <c>{{dossier}}</c>. Un modèle du coffre
/// remplace le modèle intégré du même nom.
/// </remarks>
public sealed partial class VaultSession
{
    private const string TemplatesFolder = "modeles";

    private static readonly SheetTemplate[] BuiltInTemplates =
    [
        new("Cours", "Objectifs, notions clés en gras (texte à trous), à retenir, questions pour réviser.", "# {{titre}}\n\n## Objectifs\n\n- \n\n## Notions clés\n\n**Terme** : définition.\n\n## À retenir\n\n- \n\n## Questions pour réviser\n\n1. \n", BuiltIn: true),
        new("Chronologie", "Un tableau de repères datés, puis l'essentiel à retenir.", "# {{titre}}\n\n## Repères\n\n| Date | Événement | Pourquoi c'est important |\n|---|---|---|\n| | | |\n\n## À retenir\n\n- \n", BuiltIn: true),
        new("Fiche de lecture", "Auteur, thèse, arguments, citations, avis personnel.", "# {{titre}}\n\n**Auteur** : \n**Lu le** : {{date}}\n\n## Thèse\n\n\n## Arguments\n\n1. \n\n## Citations\n\n> \n\n## Mon avis\n\n", BuiltIn: true),
        new("Définitions", "Une liste de termes en gras à définir : chacun devient un trou en révision.", "# {{titre}}\n\n**Terme** : définition.\n\n**Terme** : définition.\n", BuiltIn: true),
    ];

    private static readonly string[] Months =
        ["janvier", "février", "mars", "avril", "mai", "juin", "juillet", "août", "septembre", "octobre", "novembre", "décembre"];

    /// <summary>Les modèles disponibles : ceux du coffre (par nom), puis les intégrés qu'aucun ne remplace.</summary>
    public IReadOnlyList<SheetTemplate> Templates()
    {
        var templates = new List<SheetTemplate>();
        var folder = Path.Combine(Root, TemplatesFolder);
        List<string> files;
        try
        {
            files = Directory.Exists(folder)
                ? [.. Directory.EnumerateFiles(folder)
                    .Where(file => Extension(Path.GetFileName(file)) is { } extension && Ascii.EqualsIgnoreCase(extension, "md"))
                    .OrderBy(file => Path.GetFileName(file), Utf8Order.Instance)]
                : [];
        }
        catch (Exception exception) when (IsIo(exception))
        {
            files = [];
        }
        foreach (var file in files)
        {
            if (TryReadAllBytes(file) is { } bytes && TryDecode(bytes, out var body))
            {
                var name = Path.GetFileName(file);
                templates.Add(new SheetTemplate(name[..name.LastIndexOf('.')], Description(body), body, BuiltIn: false));
            }
        }
        templates.AddRange(BuiltInTemplates.Where(builtIn => !templates.Any(t => Ascii.EqualsIgnoreCase(t.Name, builtIn.Name))));
        return templates;
    }

    /// <summary>
    /// Corps d'une nouvelle fiche <paramref name="title"/> dans <paramref name="folder"/> d'après
    /// le modèle <paramref name="name"/>, variables remplacées ; vide si le modèle est inconnu.
    /// </summary>
    public string TemplateBody(string name, string title, string folder, DateOnly day) =>
        Templates().FirstOrDefault(t => t.Name == name) is { } template ? ApplyTemplate(template.Body, title, folder, day) : string.Empty;

    /// <summary>
    /// Enregistre le corps de la fiche <paramref name="id"/> comme modèle du coffre
    /// (<c>modeles/&lt;nom&gt;.md</c>), son titre de tête devenu <c>{{titre}}</c> ; un modèle du même
    /// nom est remplacé.
    /// </summary>
    /// <returns>Le nom retenu.</returns>
    public string SaveAsTemplate(string id, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Trim().Length == 0)
        {
            throw VaultException.InvalidPath(name, "donnez un nom au modèle");
        }
        var fileName = FileNames.ForTitle(name.Trim());
        var read = ReadSheet(id);
        var body = read.Text[FrontMatter.Split(read.Text).BodyStart..];
        var folder = Confine(VaultPath.Create(TemplatesFolder));
        CreateDirectory(folder);
        ReplaceFile(Path.Combine(folder, $"{fileName}.md"), Encoding.UTF8.GetBytes(AsTemplate(body)));
        return fileName;
    }

    /// <summary>Les variables d'un modèle remplacées.</summary>
    public static string ApplyTemplate(string template, string title, string folder, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(template);
        var month = Months[day.Month - 1];
        var date = $"{(day.Day == 1 ? "1er" : day.Day.ToString(CultureInfo.InvariantCulture))} {month} {day.Year}";
        return template
            .Replace("{{titre}}", title, StringComparison.Ordinal)
            .Replace("{{date}}", date, StringComparison.Ordinal)
            .Replace("{{dossier}}", folder, StringComparison.Ordinal);
    }

    /// <summary>Description d'un modèle du coffre : son premier commentaire <c>&lt;!-- … --&gt;</c>, sinon ses premiers titres.</summary>
    private static string Description(string body)
    {
        var start = body.IndexOf("<!--", StringComparison.Ordinal);
        if (start >= 0)
        {
            var end = body.IndexOf("-->", start, StringComparison.Ordinal);
            if (end >= start + 4)
            {
                var comment = body[(start + 4)..end].Trim();
                if (comment.Length > 0)
                {
                    return RustText.CollapseWhitespace(comment);
                }
            }
        }
        var titles = body.Split('\n')
            .Select(line => line.EndsWith('\r') ? line[..^1] : line)
            .Select(line => line.StartsWith("## ", StringComparison.Ordinal) ? line[3..] : line.StartsWith("### ", StringComparison.Ordinal) ? line[4..] : null)
            .OfType<string>()
            .Select(title => title.Trim())
            .Where(title => title.Length > 0)
            .Take(4)
            .ToList();
        return titles.Count == 0 ? "Modèle du coffre." : $"{string.Join(", ", titles)}.";
    }

    /// <summary>Le corps d'une fiche prêt à servir de modèle : son « # Titre » de tête devient <c># {{titre}}</c>.</summary>
    private static string AsTemplate(string body)
    {
        body = body.TrimStart('\r', '\n');
        var newline = body.IndexOf('\n', StringComparison.Ordinal);
        if (newline >= 0 && body[..newline].TrimEnd().StartsWith("# ", StringComparison.Ordinal))
        {
            return "# {{titre}}\n" + body[(newline + 1)..];
        }
        return newline < 0 && body.TrimEnd().StartsWith("# ", StringComparison.Ordinal) ? "# {{titre}}\n" : body;
    }
}
