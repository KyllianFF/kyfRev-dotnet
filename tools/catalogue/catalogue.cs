#:project ../../src/KyfRev.Extensions.Host/KyfRev.Extensions.Host.csproj
#:property ImplicitUsings=enable
#:property PublishAot=false

// Le catalogue signé des extensions (docs/extensions/catalogue.md). À lancer sur le poste de la personne qui publie,
// à la racine du dépôt :
//   dotnet run tools/catalogue/catalogue.cs -- cle <fichier.pem> [nom]   crée une clé de publication, hors du dépôt
//   dotnet run tools/catalogue/catalogue.cs -- publier <fichier.pem>     signe catalogue/ d'après extensions/
//   dotnet run tools/catalogue/catalogue.cs -- version <fichier.pem> <v> signe l'annonce de la version publiée
//   dotnet run tools/catalogue/catalogue.cs -- verifier                  vérifie catalogue/ comme l'application
// La clé privée ne quitte jamais son fichier, chiffré par une phrase de passe demandée au clavier. Le dépôt, lui, ne
// reçoit que la clé publique (src/KyfRev.Extensions.Host/cles-catalogue.json) et le catalogue signé.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using KyfRev.Extensions.Host;

var racine = Directory.GetCurrentDirectory();
if (!File.Exists(Path.Combine(racine, "KyfRev.slnx")))
{
    return Echec("lancez l'outil à la racine du dépôt kyfRev.");
}
return args switch
{
    ["cle", var fichier] => Cle(fichier, "kyfrev-1"),
    ["cle", var fichier, var nom] => Cle(fichier, nom),
    ["publier", var fichier] => Publier(fichier),
    ["version", var fichier, var numero] => Version(fichier, numero),
    ["verifier"] => Verifier(),
    _ => Echec("usage : cle <fichier.pem> [nom] | publier <fichier.pem> | version <fichier.pem> <numero> | verifier"),
};

int Cle(string fichier, string nom)
{
    var chemin = Path.GetFullPath(fichier);
    if (chemin.StartsWith(racine + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
    {
        return Echec("la clé privée ne se range jamais dans le dépôt : choisissez un fichier ailleurs.");
    }
    if (File.Exists(chemin))
    {
        return Echec($"{chemin} existe déjà : rien n'est remplacé.");
    }
    var phrase = Phrase("Phrase de passe de la clé (12 caractères au moins) : ");
    if (phrase.Length < 12 || phrase != Phrase("Même phrase, une seconde fois : "))
    {
        return Echec("les deux phrases diffèrent, ou elle est trop courte : aucune clé n'est créée.");
    }
    using var cle = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    var protegee = cle.ExportEncryptedPkcs8PrivateKeyPem(phrase, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 600_000));
    Directory.CreateDirectory(Path.GetDirectoryName(chemin)!);
    File.WriteAllText(chemin, protegee);
    var publique = cle.ExportSubjectPublicKeyInfoPem();
    Console.WriteLine($"Clé privée écrite dans {chemin}, chiffrée par votre phrase. Gardez-la dans votre gestionnaire de mots de passe.");
    Console.WriteLine();
    Console.WriteLine("Clé publique, à ajouter à src/KyfRev.Extensions.Host/cles-catalogue.json :");
    var lisible = new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    Console.WriteLine(new JsonObject { ["id"] = nom, ["publique"] = publique.ReplaceLineEndings("\n") }.ToJsonString(lisible));
    return 0;
}

int Publier(string fichier)
{
    var cles = ExtensionCatalogue.KnownKeys;
    if (cles.IsEmpty)
    {
        return Echec("cles-catalogue.json ne connaît encore aucune clé publique : ajoutez d'abord la vôtre.");
    }
    if (!File.Exists(fichier))
    {
        return Echec($"{fichier} est introuvable.");
    }
    using var cle = ECDsa.Create();
    try
    {
        cle.ImportFromEncryptedPem(File.ReadAllText(fichier), Phrase("Phrase de passe de la clé : "));
    }
    catch (CryptographicException)
    {
        return Echec("la phrase de passe ne correspond pas à cette clé.");
    }
    var publique = cle.ExportSubjectPublicKeyInfoPem().ReplaceLineEndings("\n").Trim();
    if (cles.FirstOrDefault(connue => connue.PublicKey.ReplaceLineEndings("\n").Trim() == publique) is not { } connue)
    {
        return Echec("cette clé n'est pas dans cles-catalogue.json : l'application refuserait sa signature.");
    }
    var entrees = new List<CatalogueEntry>();
    foreach (var dossier in Directory.EnumerateDirectories(Path.Combine(racine, "extensions")).Order(StringComparer.Ordinal))
    {
        var entree = ExtensionCatalogue.EntryOf(dossier);
        if (!entree.Succeeded)
        {
            return Echec($"{Path.GetFileName(dossier)} : {entree.Error}");
        }
        entrees.Add(entree.Value);
    }
    var actuel = Path.Combine(racine, "catalogue", "catalogue.json");
    var precedent = File.Exists(actuel) && JsonNode.Parse(File.ReadAllBytes(actuel))?["numero"]?.GetValue<int>() is { } numero ? numero : 0;
    var catalogue = ExtensionCatalogue.Write(precedent + 1, DateOnly.FromDateTime(DateTime.Now), entrees);
    var signature = ExtensionCatalogue.Sign(catalogue, cle, connue.Id);
    var verifie = ExtensionCatalogue.Verify(catalogue, signature, cles, precedent);
    if (!verifie.Succeeded)
    {
        return Echec($"le catalogue signé ne se vérifie pas : {verifie.Error}");
    }
    Directory.CreateDirectory(Path.GetDirectoryName(actuel)!);
    File.WriteAllBytes(actuel, catalogue);
    File.WriteAllBytes(Path.Combine(racine, "catalogue", "catalogue.sig"), signature);
    Console.WriteLine($"Catalogue n° {precedent + 1} signé par « {connue.Id} » : {entrees.Count} extension(s). Il ne reste qu'à le proposer dans une PR.");
    return 0;
}

int Version(string fichier, string numero)
{
    if (!File.Exists(fichier))
    {
        return Echec($"{fichier} est introuvable.");
    }
    using var cle = ECDsa.Create();
    try
    {
        cle.ImportFromEncryptedPem(File.ReadAllText(fichier), Phrase("Phrase de passe de la clé : "));
    }
    catch (CryptographicException)
    {
        return Echec("la phrase de passe ne correspond pas à cette clé.");
    }
    var publique = cle.ExportSubjectPublicKeyInfoPem().ReplaceLineEndings("\n").Trim();
    if (ExtensionCatalogue.KnownKeys.FirstOrDefault(connue => connue.PublicKey.ReplaceLineEndings("\n").Trim() == publique) is not { } connue)
    {
        return Echec("cette clé n'est pas dans cles-catalogue.json : l'application refuserait sa signature.");
    }
    byte[] annonce;
    try
    {
        annonce = UpdateChecker.Write(numero, DateOnly.FromDateTime(DateTime.Now));
    }
    catch (ArgumentException erreur)
    {
        return Echec(erreur.Message);
    }
    var signature = ExtensionCatalogue.Sign(annonce, cle, connue.Id);
    var verifie = UpdateChecker.Verify(annonce, signature, ExtensionCatalogue.KnownKeys);
    if (!verifie.Succeeded)
    {
        return Echec($"l'annonce signée ne se vérifie pas : {verifie.Error}");
    }
    var chemin = Path.Combine(racine, UpdateChecker.ManifestFile);
    Directory.CreateDirectory(Path.GetDirectoryName(chemin)!);
    File.WriteAllBytes(chemin, annonce);
    File.WriteAllBytes(Path.Combine(racine, UpdateChecker.SignatureFile), signature);
    Console.WriteLine($"Version {numero} annoncée et signée par « {connue.Id} ». Il ne reste qu'à proposer mise-a-jour/ dans une PR.");
    return 0;
}

int Verifier()
{
    var dossier = Path.Combine(racine, "catalogue");
    if (!File.Exists(Path.Combine(dossier, "catalogue.json")))
    {
        Console.WriteLine("Aucun catalogue publié.");
        return 0;
    }
    var verifie = ExtensionCatalogue.Verify(File.ReadAllBytes(Path.Combine(dossier, "catalogue.json")), File.ReadAllBytes(Path.Combine(dossier, "catalogue.sig")), ExtensionCatalogue.KnownKeys, null);
    if (!verifie.Succeeded)
    {
        return Echec(verifie.Error!);
    }
    var ecarts = ExtensionCatalogue.Differences(verifie.Value, Path.Combine(racine, "extensions"));
    foreach (var ecart in ecarts)
    {
        Console.Error.WriteLine(ecart);
    }
    Console.WriteLine(ecarts.Count == 0 ? $"Catalogue n° {verifie.Value.Number} : signature bonne, fichiers identiques." : "Le catalogue ne correspond plus aux extensions : republiez-le.");
    return ecarts.Count == 0 ? 0 : 1;
}

static string Phrase(string invite)
{
    Console.Write(invite);
    if (Console.IsInputRedirected)
    {
        var lue = Console.ReadLine() ?? string.Empty;
        Console.WriteLine();
        return lue;
    }
    var phrase = new StringBuilder();
    for (var touche = Console.ReadKey(intercept: true); touche.Key != ConsoleKey.Enter; touche = Console.ReadKey(intercept: true))
    {
        if (touche.Key == ConsoleKey.Backspace && phrase.Length > 0)
        {
            phrase.Length--;
        }
        else if (!char.IsControl(touche.KeyChar))
        {
            phrase.Append(touche.KeyChar);
        }
    }
    Console.WriteLine();
    return phrase.ToString();
}

static int Echec(string raison)
{
    Console.Error.WriteLine($"catalogue : {raison}");
    return 1;
}
