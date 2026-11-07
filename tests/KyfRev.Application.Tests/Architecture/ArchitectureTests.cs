using System.Reflection;
using System.Text.RegularExpressions;

namespace KyfRev.Application.Tests.Architecture;

/// <summary>
/// Règles d'architecture et garde-fous, vérifiés sur les assemblys et le code source : couches,
/// dépendances, et la règle « rien ne s'efface » reprise de la 0.x (<c>garde_fous.rs</c>).
/// </summary>
[TestClass]
public sealed partial class ArchitectureTests
{
    /// <summary>Les fins de ligne de C#.</summary>
    private static readonly char[] LineEnds = ['\n', '\r', '\u0085', '\u2028', '\u2029'];

    private const string Domain = "KyfRev.Domain";
    private const string Application = "KyfRev.Application";

    private const string Rendering = "KyfRev.Rendering";

    /// <summary>Morceaux de nom interdits pour une opération publique (français et anglais).</summary>
    private static readonly string[] DeletionWords = ["supprim", "effac", "detrui", "delete", "remove", "purge", "erase", "destroy"];

    /// <summary>Les seules opérations qui effacent : depuis la corbeille, à la demande expresse de la personne.</summary>
    private static readonly string[] Purges = ["Purge", "PurgeSheet", "PurgeFolder", "PurgeAnnotation", "PurgeQuote"];

    private static readonly string[] Layers = [Domain, Application, "KyfRev.Extensions.Host", "KyfRev.Infrastructure.Crypto", "KyfRev.Infrastructure.Online", "KyfRev.Infrastructure.Vault", "KyfRev.Modules.Citations", "KyfRev.Modules.Schemas", Rendering, "kyfrev-coffre"];

    /// <summary>Paquets permis au domaine, aux cas d'usage et aux modules, en plus du framework.</summary>
    private static readonly string[] Abstractions = ["Microsoft.Extensions.DependencyInjection.Abstractions", "Microsoft.Extensions.Logging.Abstractions"];

    /// <summary>Assemblys de kyfRev présents à côté des tests (les tests eux-mêmes exceptés).</summary>
    private static readonly Lazy<IReadOnlyList<Assembly>> Ours = new(() =>
    [
        .. Directory.GetFiles(AppContext.BaseDirectory, "*.dll")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => IsOurs(name!) && !name!.EndsWith(".Tests", StringComparison.Ordinal))
            .Select(name => Assembly.Load(name!)),
    ]);

    [TestMethod]
    public void Each_layer_depends_only_on_the_layers_below_it()
    {
        CollectionAssert.IsSubsetOf(Layers, Ours.Value.Select(a => a.GetName().Name).ToList());
        foreach (var assembly in Ours.Value)
        {
            var name = assembly.GetName().Name!;
            var forbidden = assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(reference => IsOurs(reference) && !MayReference(name, reference))
                .ToList();
            Assert.IsEmpty(forbidden, $"{name} ne doit pas dépendre de {string.Join(", ", forbidden)}");
        }
    }

    [TestMethod]
    public void The_domain_the_use_cases_the_rendering_and_the_modules_rely_only_on_the_framework()
    {
        var modules = Ours.Value.Select(a => a.GetName().Name!).Where(name => name.StartsWith("KyfRev.Modules.", StringComparison.Ordinal)).ToList();
        Assert.IsNotEmpty(modules);
        foreach (var (assembly, packages) in new[] { (Domain, Array.Empty<string>()), (Application, Abstractions), (Rendering, Array.Empty<string>()) }.Concat(modules.Select(module => (module, Abstractions))))
        {
            var outside = Assembly.Load(assembly).GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(reference => !IsFramework(reference) && !IsOurs(reference) && !packages.Contains(reference))
                .ToList();
            Assert.IsEmpty(outside, $"{assembly} dépend de {string.Join(", ", outside)}");
        }
    }

    /// <summary>
    /// Les modules sont compilés avec l'application et déclarés à la racine de composition :
    /// aucun code n'est chargé dynamiquement (ni assembly lu sur disque, ni contexte de chargement).
    /// </summary>
    [TestMethod]
    public void No_code_is_loaded_dynamically()
    {
        foreach (var assembly in Ours.Value)
        {
            Assert.DoesNotContain("System.Runtime.Loader", assembly.GetReferencedAssemblies().Select(reference => reference.Name), assembly.GetName().Name);
        }
        var src = Path.Combine(RepositoryRoot(), "src");
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories).Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            Assert.IsFalse(DynamicLoading().IsMatch(File.ReadAllText(file)), $"Chargement dynamique dans {Path.GetRelativePath(src, file)}");
        }
    }

    /// <summary>Chaque module vit dans son assembly <c>KyfRev.Modules.*</c>, sous un identifiant unique.</summary>
    [TestMethod]
    public void Each_module_lives_in_its_own_assembly()
    {
        var modules = Ours.Value
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass && !type.IsAbstract && typeof(Modules.IKyfRevModule).IsAssignableFrom(type))
            .ToList();
        Assert.IsNotEmpty(modules);
        foreach (var type in modules)
        {
            Assert.StartsWith("KyfRev.Modules.", type.Assembly.GetName().Name!, type.FullName);
        }
        var ids = modules.Select(type => ((Modules.IKyfRevModule)Activator.CreateInstance(type)!).Id).ToList();
        Assert.HasCount(ids.Count, ids.Distinct(StringComparer.Ordinal));
    }

    /// <summary>T1 : aucune opération publique ne porte un nom de suppression, hors les effacements demandés depuis la corbeille.</summary>
    [TestMethod]
    public void No_public_operation_is_named_after_a_deletion()
    {
        var checkedMethods = 0;
        foreach (var type in Ours.Value.SelectMany(assembly => assembly.GetExportedTypes()))
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName))
            {
                Assert.IsFalse(
                    !Purges.Contains(method.Name) && DeletionWords.Any(word => method.Name.Contains(word, StringComparison.OrdinalIgnoreCase)),
                    $"Opération suspecte : {type.FullName}.{method.Name}");
                checkedMethods++;
            }
        }
        Assert.IsGreaterThan(300, checkedMethods);
    }

    /// <summary>
    /// T1 : rien n'est effacé, sauf sept exceptions : la rotation des copies de sauvegarde (le nom
    /// et le dossier de la copie vérifiés), les deux fichiers temporaires de l'écriture atomique et
    /// de la sauvegarde, le dossier d'une extension téléchargée du catalogue (dans le dossier des
    /// téléchargements), le dossier temporaire d'une conversion et son marqueur, et l'effacement
    /// demandé depuis la corbeille. Trois comptes exacts, fichier par fichier, sur le code seul (les
    /// lignes de commentaire n'effacent rien) : les effacements directs du disque (le disque applique
    /// <c>Purge</c> à un dossier ou à un fichier) ; chaque purge, quoi qui la reçoive (le coffre
    /// chiffré retire l'entrée de son index puis efface ses objets, et lui seul efface un objet que
    /// l'index ne nomme plus ; les deux effacements définitifs de « Éléments supprimés » passent par
    /// un cas d'usage) ; et chaque <c>.Delete</c> qui n'efface rien, nommé : un partage de fichier
    /// (<c>FileShare.Delete</c>), une touche (<c>Key.Delete</c>), un libellé (<c>…Text.Delete</c>).
    /// </summary>
    [TestMethod]
    public void Files_are_deleted_only_where_allowed()
    {
        var src = Path.Combine(RepositoryRoot(), "src");
        var deletions = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["KyfRev.Infrastructure.Vault/Backups/VaultBackups.cs"] = 1,
            ["KyfRev.Extensions.Host/CatalogueReader.cs"] = 1,
            ["KyfRev.Infrastructure.Vault/Files/AtomicFile.cs"] = 1,
            ["KyfRev.Infrastructure.Vault/Files/DiskStorage.cs"] = 2,
            ["KyfRev.Infrastructure.Vault/VaultSession.Backups.cs"] = 1,
        };
        var purges = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["KyfRev.Cli/VaultConversion.cs"] = 3,
            ["KyfRev.Infrastructure.Vault/VaultSession.Trash.cs"] = 1,
            ["KyfRev.Infrastructure.Crypto/EncryptedStorage.cs"] = 1,
            ["KyfRev.Infrastructure.Crypto/EncryptedVault.cs"] = 1,
            ["KyfRev.Presentation.Wpf/Pages/DataSettingsViewModel.cs"] = 2,
        };
        var harmless = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["KyfRev.Cli/VaultConversion.cs"] = 1,
            ["KyfRev.Infrastructure.Vault/Files/DiskStorage.cs"] = 1,
            ["KyfRev.Infrastructure.Vault/Files/VaultLock.cs"] = 1,
            ["KyfRev.Infrastructure.Windows/DiagnosticLog.cs"] = 1,
            ["KyfRev.Presentation.Wpf/Editing/DiagramEditorViewModel.cs"] = 1,
            ["KyfRev.Presentation.Wpf/Pages/SheetsPage.xaml.cs"] = 2,
        };
        string Code(string file) => CodeOnly(File.ReadAllText(Path.Combine(src, file)));
        var files = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(src, file).Replace('\\', '/'))
            .Where(file => !file.Contains("/obj/", StringComparison.Ordinal) && !file.Contains("/bin/", StringComparison.Ordinal))
            .ToList();
        Assert.IsGreaterThan(50, files.Count);
        foreach (var file in files)
        {
            var text = Code(file);
            Assert.AreEqual(deletions.GetValueOrDefault(file), DiskDeletion().Count(text), $"Effacement direct du disque dans {file}");
            Assert.AreEqual(purges.GetValueOrDefault(file), PurgeCall().Count(text), $"Appel à Purge dans {file}");
            Assert.AreEqual(harmless.GetValueOrDefault(file), HarmlessDelete().Count(text), $"« .Delete » qui n'efface rien dans {file}");
        }

        // Les contrôles lisent le code seul : un appel mis en commentaire ne les satisfait pas.
        var discard = Code("KyfRev.Extensions.Host/CatalogueReader.cs");
        Assert.Contains("Path.GetFullPath(folder).StartsWith(root", discard, "un dossier téléchargé ne s'efface que dans le dossier des téléchargements");
        var rotation = Code("KyfRev.Infrastructure.Vault/Backups/VaultBackups.cs");
        var copy = rotation[rotation.IndexOf("private static void RemoveOutdatedCopy", StringComparison.Ordinal)..rotation.IndexOf("File.Delete(path)", StringComparison.Ordinal)];
        Assert.Contains("TimestampOf(name) is null", copy, "le nom de la copie doit être vérifié");
        Assert.Contains("Path.GetDirectoryName(path)", copy, "le dossier de la copie doit être vérifié");
        foreach (var file in new[] { "KyfRev.Infrastructure.Vault/Files/AtomicFile.cs", "KyfRev.Infrastructure.Vault/VaultSession.Backups.cs" })
        {
            Assert.Contains("File.Delete(temporary)", Code(file), file);
        }
        var conversion = Code("KyfRev.Cli/VaultConversion.cs");
        Assert.Contains("!IsTemporaryOf(destination, temporary)", conversion, "la conversion n'efface que son dossier temporaire, reconnu à son nom");
        Assert.Contains("disk.Purge(temporary)", conversion);
        Assert.Contains("disk.Purge(_path)", conversion, "et son marqueur, juste avant de prendre le nom de la destination");
        Assert.Contains("disk.Purge(entry.FullName)", conversion, "ce que contient le dossier temporaire, son marqueur excepté, qui part en dernier");
        var removed = Code("KyfRev.Presentation.Wpf/Pages/DataSettingsViewModel.cs");
        Assert.Contains("_annotations.Purge(passage.Id)", removed, "« Éléments supprimés » efface une annotation par son cas d'usage");
        Assert.Contains("quotes.Purge(quote.Path)", removed, "et une citation par le sien");
    }

    /// <summary>
    /// T1 : la règle tient deux comptes séparés. L'un voit chaque effacement direct du disque ; l'autre chaque appel
    /// <c>.Purge(</c>, quoi qui le reçoive : un nom de variable, un champ, une propriété ou un <c>!</c> n'y change rien. Un
    /// effacement direct ne peut donc pas prendre la place d'un appel <c>Purge</c> sans que la règle le voie.
    /// </summary>
    [TestMethod]
    public void Every_purge_is_counted_whatever_receives_it()
    {
        foreach (var call in new[] { "_storage.Purge(absolute)", "disk.Purge(temporary)", "DiskStorage.Instance.Purge(x)", "_storage.Raw.Purge(x)", "_storage!.Purge(x)", "_files?.Purge(x)", "_annotations.Purge(passage.Id)", "list.ForEach(_storage.Purge)", "Action purge = disk.Purge;" })
        {
            Assert.IsTrue(PurgeCall().IsMatch(call), call);
            Assert.IsFalse(DiskDeletion().IsMatch(call), $"{call} : une purge n'est pas un effacement direct");
        }
        foreach (var code in new[] { "public override void Purge(string path)", "vault.PurgeSheet(id)", "TryPurgeObject(id)", "settings.Purges" })
        {
            Assert.IsFalse(PurgeCall().IsMatch(code), code);
        }
        foreach (var call in new[] { "File.Delete(path)", "Directory.Delete(path, recursive: true)", "info.Delete()", "FileSystem.DeleteFile(path)", "Array.ForEach(files, File.Delete)", "Action erase = Directory.Delete;", "FileOptions.DeleteOnClose", "Action erase = info.Delete;", "files.ForEach(new FileInfo(path).Delete)" })
        {
            Assert.IsTrue(DiskDeletion().IsMatch(call), call);
        }
        foreach (var code in new[] { "FileShare.ReadWrite | FileShare.Delete", "case Key.Delete:", "SheetActionsText.Delete" })
        {
            Assert.IsFalse(DiskDeletion().IsMatch(code), $"{code} : n'efface rien");
            Assert.IsTrue(HarmlessDelete().IsMatch(code), $"{code} : compté à part, et nommé");
        }
        Assert.AreEqual(1, PurgeCall().Count(CodeOnly("/// Un effacement (<see cref=\"VaultStorage.Purge\"/>).\n        _storage.Purge(path);")), "un commentaire n'efface rien");
        Assert.AreEqual(0, PurgeCall().Count(CodeOnly("x();\r// a.Purge(b)\u0085// c.Purge(d)\u2028// e.Purge(f)\u2029// g.Purge(h)")), "un commentaire finit à toute fin de ligne, pas seulement au saut de ligne");
    }

    /// <summary>Racine du dépôt : le dossier qui contient la solution.</summary>
    internal static string RepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "KyfRev.slnx")))
            {
                return folder.FullName;
            }
        }
        throw new InvalidOperationException("Solution introuvable.");
    }

    /// <summary>Ce qu'un assembly de kyfRev peut référencer parmi les autres.</summary>
    private static bool MayReference(string assembly, string reference) => assembly switch
    {
        Domain => false,
        Application => reference == Domain,
        // Le moteur de rendu lit le modèle de l'application, et rien d'autre.
        Rendering => reference is Domain or Application,
        _ when assembly.StartsWith("KyfRev.Infrastructure.", StringComparison.Ordinal) => reference is Domain or Application,
        // Aucun module ne référence un autre module, ni l'infrastructure.
        _ when assembly.StartsWith("KyfRev.Modules.", StringComparison.Ordinal) => reference is Domain or Application,
        // Les extensions ne connaissent ni les modules, ni l'infrastructure, ni la présentation.
        _ when assembly.StartsWith("KyfRev.Extensions.", StringComparison.Ordinal) => reference is Domain or Application,
        // La présentation ne référence aucune infrastructure concrète : seule la racine de composition les relie.
        _ when assembly.StartsWith("KyfRev.Presentation.", StringComparison.Ordinal) => !reference.StartsWith("KyfRev.Infrastructure.", StringComparison.Ordinal),
        _ => true,
    };

    private static bool IsOurs(string name) => name.StartsWith("KyfRev.", StringComparison.Ordinal) || name == "kyfrev-coffre";

    private static bool IsFramework(string name) =>
        name is "netstandard" or "mscorlib" || name.StartsWith("System.", StringComparison.Ordinal) || name.StartsWith("Microsoft.Win32.", StringComparison.Ordinal);

    /// <summary>
    /// Un effacement direct du disque : un appel, ou une méthode passée comme délégué, statique (<c>File.Delete</c>) comme
    /// d'instance (<c>info.Delete</c>), ou un fichier ouvert pour s'effacer à sa fermeture. Seuls les <c>.Delete</c> qui
    /// n'effacent rien (<see cref="HarmlessDelete"/>) n'y sont pas.
    /// </summary>
    [GeneratedRegex(@"(?<!FileShare|Key|Text)\.Delete\b|DeleteFile|DeleteDirectory|RemoveDirectory|FileSystem\.Delete|SendToRecycleBin|DeleteOnClose")]
    private static partial Regex DiskDeletion();

    /// <summary>Un <c>.Delete</c> qui n'efface rien : un partage de fichier, une touche, un libellé.</summary>
    [GeneratedRegex(@"(?<=FileShare|Key|Text)\.Delete\b")]
    private static partial Regex HarmlessDelete();

    /// <summary>
    /// Une purge, quoi qui la reçoive : un stockage du coffre, ou l'effacement définitif d'un cas d'usage ; appelée, ou passée
    /// comme délégué (<c>list.ForEach(_storage.Purge)</c>).
    /// </summary>
    [GeneratedRegex(@"\.Purge\b")]
    private static partial Regex PurgeCall();

    /// <summary>Le code, sans les lignes de commentaire : un commentaire qui nomme une méthode n'efface rien.</summary>
    /// <remarks>Un commentaire <c>//</c> finit à toute fin de ligne de C# : saut de ligne, retour chariot, U+0085, U+2028, U+2029.</remarks>
    private static string CodeOnly(string text) =>
        string.Join('\n', text.Split(LineEnds).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    [GeneratedRegex(@"Assembly\.(Load|LoadFrom|LoadFile|UnsafeLoadFrom)\(|AssemblyLoadContext|AppDomain\.CurrentDomain\.Load|Type\.GetType\(")]
    private static partial Regex DynamicLoading();
}
