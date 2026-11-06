using System.Text;
using KyfRev.Application;

namespace KyfRev.Extensions.Host;

/// <summary>
/// « Créer une extension… » : un dossier prêt à modifier, au contrat 2. Il a son manifeste, un script qui montre l'essentiel (une
/// vue, une action, une commande, des données gardées), une icône, les types de l'objet <c>kyfrev</c> pour l'éditeur et un mode
/// d'emploi. Il ne demande que la lecture des fiches et ses propres données.
/// </summary>
public static class ExtensionTemplate
{
    /// <summary>L'identifiant d'une extension créée ; suivi d'un numéro s'il est déjà pris.</summary>
    public const string BaseId = "mon-extension";

    /// <summary>Le nom d'une extension créée ; suivi du même numéro.</summary>
    public const string BaseName = "Mon extension";

    /// <summary>Le fichier des types de l'objet <c>kyfrev</c>, copié dans le dossier créé.</summary>
    public const string TypesFile = "kyfrev.d.ts";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Crée le dossier d'une nouvelle extension dans <paramref name="parent"/> ; rend son chemin.</summary>
    /// <param name="parent">Le dossier où la créer.</param>
    /// <param name="taken">Les identifiants déjà installés, à ne pas reprendre.</param>
    public static Outcome<string> Create(string parent, IReadOnlyCollection<string> taken)
    {
        ArgumentException.ThrowIfNullOrEmpty(parent);
        ArgumentNullException.ThrowIfNull(taken);
        if (!Directory.Exists(parent))
        {
            return Outcome.Refused<string>($"dossier introuvable : {parent}");
        }
        var number = 1;
        string Id() => number == 1 ? BaseId : $"{BaseId}-{number}";
        while (taken.Contains(Id()) || Directory.Exists(Path.Combine(parent, Id())))
        {
            number++;
        }
        var id = Id();
        var name = number == 1 ? BaseName : $"{BaseName} {number}";
        var folder = Path.Combine(parent, id);
        try
        {
            Directory.CreateDirectory(folder);
            Write(folder, ExtensionManifest.FileName, Manifest.Replace("{id}", id, StringComparison.Ordinal).Replace("{nom}", name, StringComparison.Ordinal));
            Write(folder, "extension.js", Script.Replace("{nom}", name, StringComparison.Ordinal));
            Write(folder, "icone.svg", Icon);
            Write(folder, "jsconfig.json", EditorSettings);
            Write(folder, "LISEZMOI.md", Guide.Replace("{nom}", name, StringComparison.Ordinal));
            Write(folder, TypesFile, Types());
            return Outcome.Of(folder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Outcome.Refused<string>(exception.Message);
        }
    }

    /// <summary>Les types de l'objet <c>kyfrev</c>, tels que publiés dans <c>docs/extensions</c>.</summary>
    public static string Types()
    {
        using var stream = typeof(ExtensionTemplate).Assembly.GetManifestResourceStream(TypesFile)
            ?? throw new InvalidOperationException($"{TypesFile} manque dans l'assemblage.");
        using var reader = new StreamReader(stream, Utf8);
        return reader.ReadToEnd();
    }

    private static void Write(string folder, string file, string text) => File.WriteAllText(Path.Combine(folder, file), text.ReplaceLineEndings("\n"), Utf8);

    private const string Manifest = """
        {
          "api": 2,
          "id": "{id}",
          "nom": "{nom}",
          "version": "0.1.0",
          "description": "Une extension à modifier : elle compte vos fiches, garde un compteur et répond à une commande.",
          "icone": "icone.svg",
          "permissions": ["fiches:lecture", "stockage"],
          "vue": { "titre": "{nom}" },
          "commandes": [{ "id": "bonjour", "titre": "Dire bonjour" }]
        }

        """;

    private const string Script = """
        // {nom}, une extension de kyfRev (contrat 2).
        //
        // Modifiez ce fichier, puis, dans Paramètres, onglet « Modules et extensions », mode développeur : « Recharger ». Les erreurs
        // de l'extension s'affichent sous elle. Les types de l'objet kyfrev sont dans kyfrev.d.ts : un éditeur comme VS Code s'en sert
        // pour compléter et vérifier ce que vous écrivez.
        "use strict";

        /** Le texte reste du texte : kyfRev nettoie le HTML rendu, mais un « < » écrit par l'utilisateur doit s'afficher tel quel. */
        const echapper = (texte) =>
          String(texte ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);

        let message = "";

        // La vue, dans le menu : du HTML simple, que kyfRev dessine en éléments natifs (titres, paragraphes, listes, tableaux,
        // boutons, champs, formulaires). Ni style, ni lien, ni image, ni script : ils sont retirés.
        kyfrev.vue(async () => {
          const fiches = await kyfrev.fiches.lister(); // permission « fiches:lecture »
          const donnees = (await kyfrev.stockage.lire()) ?? { compteur: 0 }; // permission « stockage »
          return `
            <h1>{nom}</h1>
            ${message ? `<p><strong>${echapper(message)}</strong></p>` : ""}
            <p>Votre coffre compte ${fiches.length} fiche(s), dont ${fiches.filter((f) => f.archivee).length} archivée(s).</p>
            <p class="secondaire">Le compteur vaut ${donnees.compteur} ; il est gardé dans le coffre.</p>
            <p class="actions"><button class="principal" data-action="compter">Ajouter un</button></p>`;
        });

        // Un bouton de la vue appelle l'action de son data-action ; la vue se redessine ensuite.
        kyfrev.action("compter", async () => {
          const donnees = (await kyfrev.stockage.lire()) ?? { compteur: 0 };
          donnees.compteur += 1;
          await kyfrev.stockage.ecrire(donnees);
          message = "";
        });

        // Une commande déclarée dans extension.json : elle paraît dans la recherche (Ctrl+K).
        kyfrev.commande("bonjour", async () => {
          message = "Bonjour ! Cette commande vient de la recherche (Ctrl+K).";
          await kyfrev.ouvrirVue();
        });

        """;

    private const string Icon = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M12 3v18M3 12h18" />
        </svg>

        """;

    private const string EditorSettings = """
        {
          "compilerOptions": { "checkJs": true, "target": "ES2022", "lib": ["ES2022"] },
          "include": ["*.js", "*.d.ts"]
        }

        """;

    private const string Guide = """
        # {nom}

        Une extension de kyfRev, créée par « Créer une extension… ».

        - `extension.json` dit ce qu'elle ajoute (sa vue, ses commandes) et ce qu'elle demande (ses permissions).
        - `extension.js` est son code. Il tourne isolé, sans réseau, et ne passe par kyfRev que par l'objet `kyfrev`.
        - `icone.svg` est l'icône de son entrée du menu.
        - `kyfrev.d.ts` et `jsconfig.json` servent à l'éditeur (VS Code, par exemple) : il complète et vérifie les appels.

        Pour essayer un changement : enregistrez, puis dans Paramètres, onglet « Modules et extensions », mode développeur,
        « Recharger ». Les dernières erreurs de l'extension s'affichent sous elle. Si elle demande une permission de plus, kyfRev
        la montre et demande de confirmer.

        Pour la partager : donnez ce dossier ; on l'installe par « Installer depuis un dossier… ».

        Le guide complet : https://github.com/KyllianFF/kyfRev-dotnet/blob/main/docs/extensions/guide.md

        """;
}
