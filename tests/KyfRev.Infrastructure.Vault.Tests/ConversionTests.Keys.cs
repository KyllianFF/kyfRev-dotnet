using KyfRev.Cli;
using KyfRev.Conversion;
using KyfRev.Infrastructure.Crypto;
using KyfRev.Infrastructure.Vault.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary><c>kyfrev-coffre changer-mot-de-passe</c> et <c>cle-de-recuperation</c> : l'en-tête seul change, et seulement s'il le doit.</summary>
public sealed partial class ConversionTests
{
    private const string OtherPassword = "un tout autre mot de passe";

    [TestMethod]
    public void A_new_password_replaces_the_old_one_and_leaves_the_files_alone()
    {
        using var vault = TemporaryVault.CopyOfReference();
        using var place = new Place();
        var recovery = Encrypt(vault, place);
        var objects = ObjectsOf(place);

        var (changed, output) = Run(Lines(Password, OtherPassword, OtherPassword), "changer-mot-de-passe", place.Encrypted, "--pbkdf2");

        Assert.AreEqual(VaultCommands.Success, changed, output);
        CollectionAssert.AreEqual(objects, ObjectsOf(place), "aucun fichier du coffre n'est réécrit");
        Assert.AreEqual(VaultCommands.Refused, Run(Lines(Password), "dechiffrer", place.Encrypted, place.Decrypted).ExitCode, "l'ancien mot de passe n'ouvre plus rien");
        Assert.AreEqual(VaultCommands.Success, Run(Lines(OtherPassword), "dechiffrer", place.Encrypted, place.Decrypted).ExitCode);
        CollectionAssert.AreEqual(Contents(vault.Root), Contents(place.Decrypted));
        Assert.AreEqual(VaultCommands.Success, Run(Lines(recovery), "dechiffrer", place.Encrypted, place.Recovered).ExitCode, "la clé de récupération reste valable");
    }

    [TestMethod]
    public void The_recovery_key_can_set_a_new_password()
    {
        using var vault = TemporaryVault.CopyOfReference();
        using var place = new Place();
        var recovery = Encrypt(vault, place);

        Assert.AreEqual(VaultCommands.Success, Run(Lines(recovery, OtherPassword, OtherPassword), "changer-mot-de-passe", place.Encrypted, "--pbkdf2").ExitCode);

        Assert.AreEqual(VaultCommands.Success, Run(Lines(OtherPassword), "dechiffrer", place.Encrypted, place.Decrypted).ExitCode);
    }

    [TestMethod]
    public void A_refused_password_change_leaves_the_header_as_it_was()
    {
        using var vault = TemporaryVault.CopyOfReference();
        using var place = new Place();
        Encrypt(vault, place);
        var before = HeaderOf(place);

        Assert.AreEqual(VaultCommands.Refused, Run(Lines("un mauvais mot de passe", OtherPassword, OtherPassword), "changer-mot-de-passe", place.Encrypted, "--pbkdf2").ExitCode);
        Assert.AreEqual(VaultCommands.Refused, Run(Lines(Password, OtherPassword, "une faute de frappe"), "changer-mot-de-passe", place.Encrypted, "--pbkdf2").ExitCode);
        Assert.AreEqual(VaultCommands.Refused, Run(Lines(Password, "court", "court"), "changer-mot-de-passe", place.Encrypted, "--pbkdf2").ExitCode);

        CollectionAssert.AreEqual(before, HeaderOf(place));
    }

    [TestMethod]
    public void A_new_recovery_key_replaces_the_old_one_once_it_is_noted()
    {
        using var vault = TemporaryVault.CopyOfReference();
        using var place = new Place();
        var old = Encrypt(vault, place);

        var (exitCode, output) = Run(Lines(Password, "oui"), "cle-de-recuperation", place.Encrypted);

        Assert.AreEqual(VaultCommands.Success, exitCode, output);
        var fresh = RecoveryLine().Match(output).Groups[1].Value;
        Assert.AreNotEqual(old, fresh);
        Assert.AreEqual(VaultCommands.Refused, Run(Lines(old), "dechiffrer", place.Encrypted, place.Decrypted).ExitCode, "l'ancienne n'ouvre plus rien");
        Assert.AreEqual(VaultCommands.Success, Run(Lines(fresh), "dechiffrer", place.Encrypted, place.Decrypted).ExitCode);
        Assert.AreEqual(VaultCommands.Success, Run(Lines(Password), "dechiffrer", place.Encrypted, place.Recovered).ExitCode, "le mot de passe reste valable");
    }

    [TestMethod]
    [DataRow("non")]
    [DataRow("")]
    public void A_recovery_key_nobody_noted_changes_nothing(string answer)
    {
        using var vault = TemporaryVault.CopyOfReference();
        using var place = new Place();
        var old = Encrypt(vault, place);
        var before = HeaderOf(place);

        var (exitCode, output) = Run(Lines(Password, answer), "cle-de-recuperation", place.Encrypted);

        Assert.AreEqual(VaultCommands.Refused, exitCode, output);
        Assert.Contains(VaultKeyChanges.NotNoted, output);
        CollectionAssert.AreEqual(before, HeaderOf(place));
        Assert.AreEqual(VaultCommands.Success, Run(Lines(old), "dechiffrer", place.Encrypted, place.Decrypted).ExitCode);
    }

    [TestMethod]
    public void A_wrong_secret_shows_no_recovery_key()
    {
        using var vault = TemporaryVault.CopyOfReference();
        using var place = new Place();
        Encrypt(vault, place);

        var (exitCode, output) = Run(Lines("un mauvais mot de passe", "oui"), "cle-de-recuperation", place.Encrypted);

        Assert.AreEqual(VaultCommands.Refused, exitCode, output);
        Assert.IsFalse(RecoveryLine().IsMatch(output));
    }

    [TestMethod]
    public void A_plain_vault_has_no_keys_to_change()
    {
        using var vault = TemporaryVault.CopyOfReference();
        var before = vault.Snapshot();

        var (exitCode, output) = Run(Lines(Password, OtherPassword, OtherPassword), "changer-mot-de-passe", vault.Root, "--pbkdf2");

        Assert.AreEqual(VaultCommands.Refused, exitCode);
        Assert.Contains(VaultKeyChanges.NotEncrypted, output);
        CollectionAssert.AreEqual(before, vault.Snapshot());
    }

    [TestMethod]
    public void An_encrypted_vault_open_in_the_application_is_refused()
    {
        using var vault = TemporaryVault.CopyOfReference();
        using var place = new Place();
        Encrypt(vault, place);
        var before = HeaderOf(place);
        var lockFile = VaultLayout.Absolute(place.Encrypted, VaultLayout.LockFile);
        Directory.CreateDirectory(Path.GetDirectoryName(lockFile)!);

        using (new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        {
            var (password, passwordOutput) = Run(Lines(Password, OtherPassword, OtherPassword), "changer-mot-de-passe", place.Encrypted, "--pbkdf2");
            var (recovery, recoveryOutput) = Run(Lines(Password, "oui"), "cle-de-recuperation", place.Encrypted);

            Assert.AreEqual(VaultCommands.Refused, password);
            Assert.Contains("ouvert dans kyfRev", passwordOutput);
            Assert.AreEqual(VaultCommands.Refused, recovery);
            Assert.Contains("ouvert dans kyfRev", recoveryOutput);
        }

        CollectionAssert.AreEqual(before, HeaderOf(place));
    }

    [TestMethod]
    public void A_damaged_header_is_refused_without_being_rewritten()
    {
        using var vault = TemporaryVault.CopyOfReference();
        using var place = new Place();
        Encrypt(vault, place);
        var header = Path.Combine(place.Encrypted, EncryptedVault.HeaderFile);
        File.WriteAllText(header, "pas un en-tête");

        var result = VaultKeyChanges.ChangePassword(place.Encrypted, Password, OtherPassword, PasswordKeys.NewPbkdf2());

        Assert.IsFalse(result.Succeeded, result.Message);
        Assert.AreEqual("pas un en-tête", File.ReadAllText(header));
    }

    private static string Encrypt(TemporaryVault vault, Place place)
    {
        var (exitCode, output) = Run(Lines(Password, Password), "chiffrer", vault.Root, place.Encrypted, "--pbkdf2");
        Assert.AreEqual(VaultCommands.Success, exitCode, output);
        return RecoveryLine().Match(output).Groups[1].Value;
    }

    private static byte[] HeaderOf(Place place) => File.ReadAllBytes(Path.Combine(place.Encrypted, EncryptedVault.HeaderFile));

    private static string[] ObjectsOf(Place place) =>
        [.. Directory.EnumerateFiles(place.Encrypted, "*", SearchOption.AllDirectories)
            .Where(file => Path.GetFileName(file) != EncryptedVault.HeaderFile)
            .Order(StringComparer.Ordinal)
            .Select(file => file + ":" + Convert.ToHexString(File.ReadAllBytes(file)))];
}
