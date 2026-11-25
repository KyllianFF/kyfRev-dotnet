using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Vault.Adapters;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Vault.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>
/// La présence des postes qui ont le coffre ouvert (rangée 29A), et l'ouverture en lecture seule. L'autre poste est un
/// fichier de présence écrit comme il l'écrirait.
/// </summary>
[TestClass]
public sealed class ComputerPresenceTests
{
    private const string Other = "PC-BUREAU";

    private static readonly DateTimeOffset Nine = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly Moment Now = new(new DateOnly(2026, 11, 2), Nine);

    [TestMethod]
    public void This_computer_says_it_has_the_vault_open_then_closed()
    {
        using var folder = TemporaryVault.Empty();
        using (var vault = folder.Initialize(Now))
        {
            Assert.IsTrue(folder.FileExists($"{VaultLayout.Presence}/{ComputerPresence.FileNameOf(ComputerPresence.ThisComputer)}"));
            Assert.IsEmpty(vault.Heartbeat(Nine.AddMinutes(1)), "ce poste ne se compte pas parmi les autres");
            Assert.IsEmpty(ComputerPresence.Others(folder.Root, DiskStorage.Instance, "AUTRE", Nine.AddMinutes(10)), "renouvelée il y a plus de 3 minutes, elle est ignorée");
            Assert.HasCount(1, ComputerPresence.Others(folder.Root, DiskStorage.Instance, "AUTRE", Nine.AddMinutes(2)));
        }

        Assert.IsEmpty(ComputerPresence.Others(folder.Root, DiskStorage.Instance, "AUTRE", Nine.AddMinutes(1)), "fermé, ce poste n'a plus le coffre ouvert");
    }

    [TestMethod]
    public void Another_computer_is_seen_while_its_presence_is_fresh()
    {
        using var folder = TemporaryVault.Empty();
        using var vault = folder.Initialize(Now);
        Assert.IsTrue(ComputerPresence.Announce(folder.Root, DiskStorage.Instance, Other, Nine.AddMinutes(-30), Nine, open: true));
        folder.Write($"{VaultLayout.Presence}/illisible.json", "{ abîmé");
        folder.Write($"{VaultLayout.Presence}/futur.json", """{"format":2,"poste":"X","depuis":"2026-11-02T09:00:00Z","vu":"2026-11-02T09:00:00Z","ouvert":true}""");

        var others = vault.Heartbeat(Nine.AddMinutes(1));
        Assert.AreEqual(Other, others.Single().Name);
        Assert.AreEqual(Nine.AddMinutes(-30), others[0].Since);
        Assert.AreEqual(Nine, others[0].SeenAt);
        Assert.HasCount(1, new VaultStore().OthersHaving(folder.Root, Nine.AddMinutes(2)), "lu sans ouvrir le coffre");

        Assert.IsEmpty(vault.Heartbeat(Nine.AddMinutes(4)), "plus renouvelée depuis 3 minutes : éteint, en veille, ou en retard");
        Assert.IsEmpty(vault.Heartbeat(Nine.AddMinutes(-4)), "vue dans un futur trop lointain : une horloge fausse");
        Assert.IsTrue(ComputerPresence.Announce(folder.Root, DiskStorage.Instance, Other, Nine.AddMinutes(-30), Nine.AddMinutes(1), open: false));
        Assert.IsEmpty(vault.Heartbeat(Nine.AddMinutes(1)), "fermé là-bas");
        Assert.AreEqual("PC_bureau_2_.json", ComputerPresence.FileNameOf("PC bureau(2)"));
    }

    [TestMethod]
    public void A_vault_without_presence_folder_has_no_other_computer()
    {
        using var folder = TemporaryVault.Empty();
        Assert.IsEmpty(ComputerPresence.Others(folder.Root, DiskStorage.Instance, "ICI", Nine));
        Directory.CreateDirectory(folder.PathOf(".suivi"));
        File.WriteAllText(folder.PathOf(VaultLayout.Presence), "un fichier à la place du dossier");
        Assert.IsFalse(ComputerPresence.Announce(folder.Root, DiskStorage.Instance, "ICI", Nine, Nine, open: true), "un échec n'est qu'un avis manqué");
    }

    [TestMethod]
    public void Opened_for_reading_nothing_is_written_and_every_write_is_refused()
    {
        using var folder = TemporaryVault.Empty();
        string id;
        using (var vault = folder.Initialize(Now))
        {
            id = vault.CreateSheet("Biologie", "Mitose", "# Mitose\n", Now);
        }
        File.Delete(folder.PathOf(VaultLayout.LockInfoFile));
        Directory.Delete(folder.PathOf(VaultLayout.Presence), recursive: true);
        var before = folder.Snapshot();

        using (var reading = VaultSession.Open(folder.Root, Now, readOnly: true))
        {
            Assert.IsTrue(reading.IsReadOnly);
            Assert.AreEqual("Mitose", reading.Sheet(id).Sheet.Title, "tout se lit");
            var refused = Assert.ThrowsExactly<VaultException>(() => reading.CreateSheet("Biologie", "Méiose", "# Méiose\n", Now));
            Assert.Contains("lecture seule", refused.Message);
            Assert.IsEmpty(reading.Heartbeat(Nine.AddMinutes(1)));
        }

        var after = folder.Snapshot();
        after.Remove(VaultLayout.LockFile);
        before.Remove(VaultLayout.LockFile);
        CollectionAssert.AreEquivalent(before.ToList(), after.ToList(), "ni le verrou, ni la présence, ni rien d'autre n'est écrit");
    }

    [TestMethod]
    public void A_backup_leaves_the_presence_out()
    {
        using var folder = TemporaryVault.Empty();
        using var outside = TemporaryVault.Empty();
        using var vault = folder.Initialize(Now);
        Assert.IsTrue(folder.FileExists($"{VaultLayout.Presence}/{ComputerPresence.FileNameOf(ComputerPresence.ThisComputer)}"));

        var backup = vault.Backup(outside.PathOf("sauvegardes"), Nine.UtcDateTime);

        using var archive = System.IO.Compression.ZipFile.OpenRead(backup.Path);
        Assert.IsFalse(archive.Entries.Any(entry => entry.FullName.StartsWith(VaultLayout.Presence, StringComparison.Ordinal)));
    }
}
