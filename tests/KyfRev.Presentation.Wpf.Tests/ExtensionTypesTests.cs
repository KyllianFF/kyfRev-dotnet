using System.IO;
using System.Text.RegularExpressions;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Les types publiés pour écrire une extension (<c>docs/extensions/kyfrev.d.ts</c>) disent exactement ce que donne le cadre.</summary>
[TestClass]
public sealed partial class ExtensionTypesTests
{
    [TestMethod]
    public void The_published_types_name_everything_the_frame_gives_and_nothing_else()
    {
        var frame = File.ReadAllText(Path.Combine(Repository.Root, "src", "KyfRev.Presentation.Wpf", "Extensions", "Web", "cadre.js"));
        var types = File.ReadAllText(Path.Combine(Repository.Root, "docs", "extensions", "kyfrev.d.ts"));
        var given = frame[frame.IndexOf("const kyfrev = Object.freeze({", StringComparison.Ordinal)..frame.IndexOf("Object.defineProperty(globalThis", StringComparison.Ordinal)];
        var described = types[types.IndexOf("interface Kyfrev {", StringComparison.Ordinal)..types.IndexOf("declare const kyfrev", StringComparison.Ordinal)];

        Assert.AreEqual(
            "action bloc commande compteur encart fiches fiches.creer fiches.ecrire fiches.etiqueter fiches.lire fiches.lister fiches.ouvrir ouvrirVue rafraichir selection selection.demander stockage stockage.ecrire stockage.lire vue",
            Members(given, indent: 4));
        Assert.AreEqual(Members(given, indent: 4), Members(described, indent: 2));
    }

    /// <summary>Les membres d'un objet, triés ; ceux d'un objet imbriqué, deux espaces plus loin, préfixés de son nom.</summary>
    private static string Members(string text, int indent)
    {
        var names = new List<string>();
        string? parent = null;
        foreach (var line in text.Split('\n'))
        {
            if (Member().Match(line) is not { Success: true } match)
            {
                continue;
            }
            var name = match.Groups["nom"].Value;
            var depth = match.Groups["retrait"].Value.Length;
            if (depth == indent + 2 && parent is not null)
            {
                names.Add($"{parent}.{name}");
            }
            else if (depth == indent)
            {
                names.Add(name);
                parent = match.Groups["objet"].Success ? name : null;
            }
        }
        return string.Join(' ', names.Order(StringComparer.Ordinal));
    }

    /// <summary>Un membre : une méthode, une fonction fléchée ou un objet, après son retrait.</summary>
    [GeneratedRegex(@"^(?<retrait> +)(readonly )?(?<nom>[a-zA-Z]+)(\(|: \(|: (Object\.freeze\()?(?<objet>\{))")]
    private static partial Regex Member();
}
