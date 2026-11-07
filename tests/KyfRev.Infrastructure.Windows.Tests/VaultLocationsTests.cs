using System.IO;
using System.Text;
using KyfRev.Application.Model;

namespace KyfRev.Infrastructure.Windows.Tests;

/// <summary>
/// Emplacements des coffres : les essais de la 0.x (<c>emplacements.rs</c>), plus la reprise du
/// dernier coffre retenu par la 0.x, qui n'est jamais modifié.
/// </summary>
[TestClass]
public sealed class VaultLocationsTests
{
    [TestMethod]
    public void The_portable_vault_is_relative_only()
    {
        const string application = @"E:\MesRévisions\Application";

        Assert.AreEqual(@"E:\MesRévisions\Coffre", VaultLocations.RelativeOn(application, "../Coffre"));
        Assert.AreEqual(@"E:\MesRévisions\Coffre", VaultLocations.RelativeOn(application, @".\Sous\..\..\Coffre"));
        Assert.AreEqual(@"E:\MesRévisions\Application\Coffre", VaultLocations.RelativeOn(application, "Coffre//"));
        Assert.AreEqual(@"E:\", VaultLocations.RelativeOn(application, "../../../.."));
        Assert.AreEqual(application, VaultLocations.RelativeOn(application, string.Empty));
        Assert.IsNull(VaultLocations.RelativeOn(application, @"C:\Ailleurs\Coffre"));
        Assert.IsNull(VaultLocations.RelativeOn(application, @"\Coffre"));
        Assert.IsNull(VaultLocations.RelativeOn(application, "/Coffre"));
        Assert.IsNull(VaultLocations.RelativeOn(application, "C:Coffre"));
    }

    [TestMethod]
    public void Installed_everything_lives_in_the_profile()
    {
        using var test = new TestFolder();
        var program = test.Folder("Program Files", "kyfRev");
        File.WriteAllText(Path.Combine(program, "installation.json"), """{"coffre":"donnees"}""");

        var locations = VaultLocations.From(program, test.Profile);

        Assert.AreEqual(AppMode.Installed, locations.Mode);
        var planned = test.PathOf("Roaming", "kyfRev", "Coffre");
        Assert.AreEqual(planned, locations.VaultToCreate());
        Assert.AreEqual(planned, locations.VaultToOpen());

        // Le dernier coffre ouvert est retenu dans le profil, jamais dans Program Files.
        var elsewhere = test.PathOf("Ailleurs");
        Assert.IsTrue(locations.Remember(elsewhere));
        Assert.IsTrue(File.Exists(test.PathOf("Local", "kyfRev", "v1", "config-locale.json")));
        Assert.IsFalse(File.Exists(Path.Combine(program, "config-locale.json")));
        Assert.IsFalse(File.Exists(test.PathOf("Local", "kyfRev", "config-locale.json")), "celle de la 0.x n'est jamais écrite");
        Assert.AreEqual(elsewhere, locations.VaultToOpen());
        Assert.AreEqual(elsewhere, VaultLocations.From(program, test.Profile).VaultToOpen());

        // Documents choisis à l'installation.
        File.WriteAllText(Path.Combine(program, "installation.json"), """{"coffre":"documents"}""");
        Assert.AreEqual(test.PathOf("Documents", "kyfRev", "Coffre"), VaultLocations.From(program, test.Profile).VaultToCreate());
    }

    [TestMethod]
    public void A_planned_vault_already_created_is_opened_not_proposed()
    {
        using var test = new TestFolder();
        var program = test.Folder("Program Files", "kyfRev");
        File.WriteAllText(Path.Combine(program, "installation.json"), """{"coffre":"documents"}""");
        var planned = test.Folder("Documents", "kyfRev", "Coffre", ".suivi");

        var locations = VaultLocations.From(program, test.Profile);

        Assert.IsNull(locations.VaultToCreate());
        Assert.AreEqual(Path.GetDirectoryName(planned), locations.VaultToOpen());
    }

    [TestMethod]
    public void A_planned_vault_already_encrypted_is_opened_not_proposed()
    {
        // Tout juste chiffré, son dossier de suivi n'est encore que dans son index : son en-tête suffit à le reconnaître.
        using var test = new TestFolder();
        var program = test.Folder("Program Files", "kyfRev");
        File.WriteAllText(Path.Combine(program, "installation.json"), """{"coffre":"documents"}""");
        var planned = test.Folder("Documents", "kyfRev", "Coffre");
        File.WriteAllText(Path.Combine(planned, "kyfrev-chiffre.json"), "{}");

        var locations = VaultLocations.From(program, test.Profile);

        Assert.IsNull(locations.VaultToCreate());
        Assert.AreEqual(planned, locations.VaultToOpen());
    }

    [TestMethod]
    public void The_folder_of_the_0x_is_known_only_when_installed()
    {
        using var test = new TestFolder();
        var program = test.Folder("Programs", "kyfRev");
        Assert.IsNull(VaultLocations.From(program, test.Profile).LegacyLocalData);

        File.WriteAllText(Path.Combine(program, "installation.json"), """{"coffre":"documents"}""");

        Assert.AreEqual(test.PathOf("Local", "kyfRev"), VaultLocations.From(program, test.Profile).LegacyLocalData);
    }

    [TestMethod]
    public void The_vault_remembered_by_the_0x_is_reopened_and_left_untouched()
    {
        using var test = new TestFolder();
        var program = test.Folder("Programs", "kyfRev");
        File.WriteAllText(Path.Combine(program, "installation.json"), """{"coffre":"documents"}""");
        var legacy = Path.Combine(test.Folder("Local", "kyfRev"), "config-locale.json");
        var legacyBytes = Encoding.UTF8.GetBytes("{\n  \"dernierCoffre\": \"D:\\\\Cours\\\\Coffre\"\n}");
        File.WriteAllBytes(legacy, legacyBytes);

        var locations = VaultLocations.From(program, test.Profile);
        Assert.AreEqual(@"D:\Cours\Coffre", locations.VaultToOpen());

        locations.Remember(@"E:\Autre coffre");
        Assert.AreEqual(@"E:\Autre coffre", locations.VaultToOpen());
        CollectionAssert.AreEqual(legacyBytes, File.ReadAllBytes(legacy));
    }

    [TestMethod]
    public void The_last_vault_is_written_like_the_0x_writes_it()
    {
        using var test = new TestFolder();
        var application = test.Folder("Développement");

        var locations = VaultLocations.From(application, test.Profile);
        locations.Remember(@"C:\Users\Élève\Documents\kyfRev\Coffre d'O'Brien");

        Assert.AreEqual(
            "{\n  \"dernierCoffre\": \"C:\\\\Users\\\\Élève\\\\Documents\\\\kyfRev\\\\Coffre d'O'Brien\"\n}",
            File.ReadAllText(Path.Combine(application, "config-locale.json"), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)));
    }

    [TestMethod]
    public void Portable_and_development()
    {
        using var test = new TestFolder();
        var application = test.Folder("MesRévisions", "Application");

        var development = VaultLocations.From(application + Path.DirectorySeparatorChar, test.Profile);
        Assert.AreEqual(AppMode.Development, development.Mode);
        Assert.IsNull(development.VaultToOpen());
        Assert.IsNull(development.VaultToCreate());
        development.Remember(test.PathOf("Coffre de développement"));
        Assert.AreEqual(test.PathOf("Coffre de développement"), development.VaultToOpen());
        Assert.IsTrue(File.Exists(Path.Combine(application, "config-locale.json")));

        // Un portable.json l'emporte sur un installation.json resté là ; le dernier coffre retenu n'y change rien.
        File.WriteAllText(Path.Combine(application, "installation.json"), """{"coffre":"documents"}""");
        File.WriteAllText(Path.Combine(application, "portable.json"), """{"coffre":"../Coffre"}""");
        var portable = VaultLocations.From(application, test.Profile);
        Assert.AreEqual(AppMode.Portable, portable.Mode);
        var vault = test.PathOf("MesRévisions", "Coffre");
        Assert.AreEqual(vault, portable.VaultToOpen());
        Assert.AreEqual(vault, portable.VaultToCreate());

        var before = File.ReadAllBytes(Path.Combine(application, "config-locale.json"));
        Assert.IsTrue(portable.Remember(test.PathOf("Ailleurs")), "rien à retenir en mode portable");
        CollectionAssert.AreEqual(before, File.ReadAllBytes(Path.Combine(application, "config-locale.json")));
        Assert.AreEqual(vault, portable.VaultToOpen());
    }

    [TestMethod]
    [DataRow("""{"coffre":"C:\\Coffre"}""", DisplayName = "portable : chemin absolu")]
    [DataRow("""{"coffre":42}""", DisplayName = "portable : pas un texte")]
    [DataRow("""{"Coffre":"../Coffre"}""", DisplayName = "portable : casse du nom")]
    [DataRow("""["../Coffre"]""", DisplayName = "portable : pas un objet")]
    [DataRow("""{"coffre":"../Coffre",}""", DisplayName = "portable : virgule finale")]
    [DataRow("\uFEFF{\"coffre\":\"../Coffre\"}", DisplayName = "portable : marque d'ordre des octets")]
    public void A_portable_file_that_is_not_valid_is_ignored(string content)
    {
        using var test = new TestFolder();
        var application = test.Folder("Application");
        File.WriteAllText(Path.Combine(application, "portable.json"), content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        Assert.AreEqual(AppMode.Development, VaultLocations.From(application, test.Profile).Mode);
    }

    [TestMethod]
    [DataRow("""{"coffre":"ailleurs"}""")]
    [DataRow("""{"coffre":"Documents"}""")]
    [DataRow("""{"coffre":null}""")]
    [DataRow("pas du JSON")]
    public void An_installation_file_that_is_not_valid_is_ignored(string content)
    {
        using var test = new TestFolder();
        var program = test.Folder("Program Files", "kyfRev");
        File.WriteAllText(Path.Combine(program, "installation.json"), content);

        Assert.AreEqual(AppMode.Development, VaultLocations.From(program, test.Profile).Mode);
    }

    [TestMethod]
    public void A_local_memory_that_is_not_valid_falls_back_to_the_next_one()
    {
        using var test = new TestFolder();
        var program = test.Folder("Program Files", "kyfRev");
        File.WriteAllText(Path.Combine(program, "installation.json"), """{"coffre":"documents"}""");
        File.WriteAllText(Path.Combine(test.Folder("Local", "kyfRev", "v1"), "config-locale.json"), """{"dernierCoffre":null}""");
        File.WriteAllText(Path.Combine(test.Folder("Local", "kyfRev"), "config-locale.json"), """{"dernierCoffre":["D:\\Coffre"]}""");

        Assert.AreEqual(test.PathOf("Documents", "kyfRev", "Coffre"), VaultLocations.From(program, test.Profile).VaultToOpen());
    }

    [TestMethod]
    public void Without_documents_folder_no_vault_is_planned()
    {
        using var test = new TestFolder();
        var program = test.Folder("Program Files", "kyfRev");
        File.WriteAllText(Path.Combine(program, "installation.json"), """{"coffre":"documents"}""");

        var locations = VaultLocations.From(program, test.Profile with { Documents = string.Empty });

        Assert.AreEqual(AppMode.Installed, locations.Mode);
        Assert.IsNull(locations.VaultToCreate());
        Assert.IsNull(locations.VaultToOpen());
    }

    [TestMethod]
    public void A_memory_that_cannot_be_written_changes_nothing()
    {
        using var test = new TestFolder();
        var program = test.Folder("Program Files", "kyfRev");
        File.WriteAllText(Path.Combine(program, "installation.json"), """{"coffre":"documents"}""");
        // Un fichier à la place du dossier kyfRev : rien ne peut y être écrit.
        File.WriteAllText(test.PathOf("Local", "kyfRev"), string.Empty);

        var locations = VaultLocations.From(program, test.Profile);

        Assert.IsFalse(locations.Remember(test.PathOf("Coffre")));
        Assert.AreEqual(test.PathOf("Documents", "kyfRev", "Coffre"), locations.VaultToOpen());
    }

    [TestMethod]
    public void The_running_program_is_in_development_mode()
    {
        // Ni portable.json ni installation.json à côté des essais.
        Assert.AreEqual(AppMode.Development, VaultLocations.Detect().Mode);
    }

    /// <summary>Dossier d'essai jetable, avec un profil fictif (Local, Documents, Roaming).</summary>
    private sealed class TestFolder : IDisposable
    {
        public TestFolder()
        {
            Root = Path.Combine(Path.GetTempPath(), "kyfrev-emplacements", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(PathOf("Local"));
            Profile = new ProfileFolders(PathOf("Local"), PathOf("Documents"), PathOf("Roaming"));
        }

        public string Root { get; }

        public ProfileFolders Profile { get; }

        public string PathOf(params string[] parts) => Path.Combine([Root, .. parts]);

        public string Folder(params string[] parts) => Directory.CreateDirectory(PathOf(parts)).FullName;

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
