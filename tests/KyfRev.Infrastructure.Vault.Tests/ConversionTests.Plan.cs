using KyfRev.Application;
using KyfRev.Application.Ports;
using KyfRev.Conversion;
using KyfRev.Infrastructure.Crypto;
using KyfRev.Infrastructure.Vault.Tests.Support;
using KyfRev.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>
/// Le chiffrement en deux temps (préparer, puis écrire), la progression d'une conversion, le choix d'une destination libre,
/// et l'adaptateur de l'application.
/// </summary>
public sealed partial class ConversionTests
{
    [TestMethod]
    public void Preparing_refuses_what_is_not_a_plain_vault_or_a_short_password_and_writes_nothing()
    {
        using var vault = SmallVault();
        using var place = new Place();
        Assert.IsTrue(VaultConversion.Encrypt(vault.Root, place.Encrypted, Password, PasswordKeys.NewPbkdf2()).Succeeded);
        var before = vault.Snapshot();
        var encrypted = Snapshot(place.Encrypted);

        // Le coffre d'abord, le mot de passe ensuite : comme le chiffrement d'un coup.
        foreach (var folder in new[] { place.Root, place.Encrypted })
        {
            foreach (var password in new[] { Password, "court" })
            {
                var refused = VaultConversion.Prepare(folder, password, PasswordKeys.NewPbkdf2());
                Assert.IsNull(refused.Plan, folder);
                Assert.AreEqual($"« {folder} » n'est pas un coffre kyfRev en clair (il n'a pas de {VaultLayout.SettingsFile}).", refused.Refusal);
            }
        }
        var tooShort = VaultConversion.Prepare(vault.Root, "court", PasswordKeys.NewPbkdf2());
        Assert.IsNull(tooShort.Plan);
        Assert.AreEqual($"Le mot de passe doit faire au moins {VaultConversion.MinimumPasswordLength} caractères.", tooShort.Refusal);

        // Des réglages trop faibles sont une erreur de programmation, comme pour le chiffrement d'un coup.
        Assert.ThrowsExactly<ArgumentException>(() => VaultConversion.Prepare(vault.Root, Password, PasswordKeys.NewPbkdf2() with { Passes = 1 }));

        CollectionAssert.AreEqual(before, vault.Snapshot(), "préparer n'écrit rien dans la source");
        CollectionAssert.AreEqual(encrypted, Snapshot(place.Encrypted));
        Assert.AreEqual(1, Directory.GetFileSystemEntries(place.Root).Length, "ni ailleurs");
    }

    [TestMethod]
    public void A_prepared_encryption_converts_and_its_recovery_key_opens_the_result()
    {
        using var vault = TemporaryVault.CopyOfReference();
        using var place = new Place();
        var before = vault.Snapshot();

        var prepared = VaultConversion.Prepare(vault.Root, Password, PasswordKeys.NewPbkdf2());
        using var plan = prepared.Plan;

        Assert.IsNotNull(plan, prepared.Refusal);
        Assert.AreEqual(string.Empty, prepared.Refusal);
        Assert.AreEqual(Path.GetFullPath(vault.Root), plan.Source);
        Assert.AreEqual("v_01M69JFV2A3WD8KH55EHWC1G7N", plan.VaultId, "le coffre chiffré garde l'identifiant du coffre en clair");
        Assert.AreEqual(plan.VaultId, plan.Header.VaultId);
        Assert.AreEqual("chiffrement préparé", plan.ToString(), "jamais la clé");
        Assert.IsEmpty(Directory.GetFileSystemEntries(place.Root), "préparer n'écrit rien");

        var encrypted = VaultConversion.Encrypt(plan, place.Encrypted);

        Assert.IsTrue(encrypted.Succeeded, encrypted.Message);
        Assert.AreEqual(plan.RecoveryKey, encrypted.RecoveryKey);
        Assert.AreEqual(Contents(vault.Root).Count, encrypted.Files);
        Assert.AreEqual(encrypted.Message, encrypted.ToString(), "le résultat se montre sans la clé de récupération");
        CollectionAssert.AreEqual(before, vault.Snapshot(), "la source n'a pas changé, pas même son verrou");
        Assert.AreEqual(plan.VaultId, EncryptedVault.ReadHeader(place.Encrypted).VaultId);
        Assert.AreEqual(0, Leftovers(place));

        Assert.IsTrue(VaultConversion.Decrypt(place.Encrypted, place.Decrypted, Password).Succeeded);
        CollectionAssert.AreEqual(Contents(vault.Root), Contents(place.Decrypted), "chaque fichier, à l'octet près");
        var recovered = VaultConversion.Decrypt(place.Encrypted, place.Recovered, plan.RecoveryKey);
        Assert.IsTrue(recovered.Succeeded, recovered.Message);
        CollectionAssert.AreEqual(Contents(vault.Root), Contents(place.Recovered), "la clé de récupération notée avant la conversion ouvre le coffre");
    }

    [TestMethod]
    public void A_prepared_encryption_refused_or_cut_converts_on_the_next_attempt()
    {
        using var vault = SmallVault();
        using var place = new Place();
        using var plan = Prepared(vault.Root);

        Directory.CreateDirectory(place.Encrypted);
        var refused = VaultConversion.Encrypt(plan, place.Encrypted);
        Assert.IsFalse(refused.Succeeded);
        Assert.Contains("existe déjà", refused.Message);
        Directory.Delete(place.Encrypted);

        // Coupée à l'écriture de l'en-tête : la conversion échoue, et ne garde rien de sa copie de la clé.
        var cut = VaultConversion.Encrypt(plan, place.Encrypted, disk: new FailingDisk { FailAt = 3 });
        Assert.IsFalse(cut.Succeeded, cut.Message);
        Assert.Contains("Coupure simulée", cut.Message);
        Assert.IsFalse(Directory.Exists(place.Encrypted));
        Assert.AreEqual(0, Leftovers(place));

        var again = VaultConversion.Encrypt(plan, place.Encrypted);
        Assert.IsTrue(again.Succeeded, again.Message);
        CollectionAssert.AreEqual(Contents(vault.Root), Decrypted(place));
        Assert.IsTrue(VaultConversion.Decrypt(place.Encrypted, place.Recovered, plan.RecoveryKey).Succeeded, "la clé de récupération notée avant le premier essai ouvre le coffre");
    }

    [TestMethod]
    public void A_prepared_encryption_checks_its_source_again_before_writing()
    {
        using var vault = SmallVault();
        using var place = new Place();
        using var plan = Prepared(vault.Root);

        // Le coffre a cessé d'être un coffre en clair depuis la préparation.
        File.Move(vault.PathOf(VaultLayout.SettingsFile), vault.PathOf(VaultLayout.SettingsFile + ".bak"));
        var refused = VaultConversion.Encrypt(plan, place.Encrypted);
        Assert.IsFalse(refused.Succeeded);
        Assert.Contains("n'est pas un coffre kyfRev en clair", refused.Message);
        File.Move(vault.PathOf(VaultLayout.SettingsFile + ".bak"), vault.PathOf(VaultLayout.SettingsFile));

        // Ouvert dans kyfRev depuis : l'application tient son verrou.
        using (new FileStream(vault.PathOf(VaultLayout.LockFile), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        {
            Assert.Contains("ouvert dans kyfRev", VaultConversion.Encrypt(plan, place.Encrypted).Message);
        }

        Assert.IsEmpty(Directory.GetFileSystemEntries(place.Root), "rien n'est écrit");
        Assert.IsTrue(VaultConversion.Encrypt(plan, place.Encrypted).Succeeded);
    }

    [TestMethod]
    public void A_released_preparation_encrypts_nothing()
    {
        using var vault = SmallVault();
        using var place = new Place();
        var plan = Prepared(vault.Root);
        plan.Dispose();
        plan.Dispose();

        var refused = VaultConversion.Encrypt(plan, place.Encrypted);

        Assert.IsFalse(refused.Succeeded);
        Assert.Contains("abandonné", refused.Message);
        Assert.IsEmpty(Directory.GetFileSystemEntries(place.Root), "rien n'est écrit");
    }

    [TestMethod]
    public void Each_conversion_reports_every_file_written_then_every_file_read_again()
    {
        using var vault = SmallVault();
        using var place = new Place();
        var files = Contents(vault.Root).Count;

        var encrypting = new Reports();
        Assert.IsTrue(VaultConversion.Encrypt(vault.Root, place.Encrypted, Password, PasswordKeys.NewPbkdf2(), progress: encrypting).Succeeded);
        AssertSteps(encrypting, files, "chiffrement");

        var decrypting = new Reports();
        Assert.IsTrue(VaultConversion.Decrypt(place.Encrypted, place.Decrypted, Password, progress: decrypting).Succeeded);
        AssertSteps(decrypting, files, "déchiffrement");

        using var plan = Prepared(vault.Root);
        var planned = new Reports();
        Assert.IsTrue(VaultConversion.Encrypt(plan, place.Recovered, planned).Succeeded);
        AssertSteps(planned, files, "chiffrement préparé");

        // Une conversion refusée ne dit rien : elle n'a rien écrit.
        var refused = new Reports();
        Assert.IsFalse(VaultConversion.Encrypt(plan, place.Recovered, refused).Succeeded);
        Assert.IsFalse(VaultConversion.Decrypt(place.Encrypted, place.Decrypted, Password, progress: refused).Succeeded);
        Assert.IsEmpty(refused.All);
    }

    [TestMethod]
    public void A_free_destination_skips_every_folder_or_file_of_the_same_name()
    {
        using var place = new Place();
        const string wanted = "Coffre chiffré";
        string Numbered(int number) => Path.Combine(place.Root, $"{wanted} ({number})");

        Assert.AreEqual(Path.Combine(place.Root, wanted), VaultConversion.FreeDestination(place.Root, wanted));
        Directory.CreateDirectory(Path.Combine(place.Root, wanted));
        Assert.AreEqual(Numbered(2), VaultConversion.FreeDestination(place.Root, wanted));
        File.WriteAllText(Numbered(2), "un fichier, pas un dossier");
        Assert.AreEqual(Numbered(3), VaultConversion.FreeDestination(place.Root, wanted), "un fichier du même nom prend la place");
        Assert.AreEqual(Path.Combine(place.Root, "coffre chiffré (3)"), VaultConversion.FreeDestination(place.Root, "coffre chiffré"), "sans tenir compte de la casse, comme Windows");
        Assert.HasCount(2, Directory.GetFileSystemEntries(place.Root), "chercher n'écrit rien");

        // Mille noms pris : la recherche s'arrête, et le dit.
        for (var number = 3; number <= 1000; number++)
        {
            File.WriteAllBytes(Numbered(number), []);
        }
        var full = Assert.ThrowsExactly<IOException>(() => VaultConversion.FreeDestination(place.Root, wanted));
        Assert.Contains($"« {wanted} (1000) »", full.Message);
    }

    [TestMethod]
    public void The_application_adapter_prepares_encrypts_and_decrypts_into_new_folders()
    {
        using var vault = SmallVault();
        using var place = new Place();
        var conversions = new VaultConversions(PasswordKeys.NewPbkdf2());
        var files = Contents(vault.Root).Count;

        // Un refus de la conversion est une opération refusée : le cas d'usage en fait un refus, avec le même message.
        var notAVault = Assert.Throws<OperationException>(() => conversions.PrepareEncryption(place.Root, Password).Dispose());
        Assert.AreEqual($"« {place.Root} » n'est pas un coffre kyfRev en clair (il n'a pas de {VaultLayout.SettingsFile}).", notAVault.Message);
        Assert.Contains("au moins", Assert.Throws<OperationException>(() => conversions.PrepareEncryption(vault.Root, "court").Dispose()).Message);

        using var draft = conversions.PrepareEncryption(vault.Root, Password);
        Assert.AreEqual(Path.GetFullPath(vault.Root), draft.Source);
        using var foreign = new ForeignDraft(vault.Root);
        Assert.ThrowsExactly<ArgumentException>(() => conversions.Encrypt(foreign, place.Encrypted));
        Assert.IsFalse(foreign.Released, "il reste à qui l'a donné");
        Assert.IsEmpty(Directory.GetFileSystemEntries(place.Root), "rien n'est écrit");

        var progress = new Reports();
        var encrypted = conversions.Encrypt(draft, place.Encrypted, progress);
        Assert.AreEqual(new ConversionReport(true, encrypted.Message, place.Encrypted, files), encrypted);
        Assert.StartsWith("Coffre chiffré dans", encrypted.Message);
        AssertSteps(progress, files, "chiffrement");

        var wrong = conversions.Decrypt(place.Encrypted, place.Decrypted, VaultSecret.Password("un mauvais mot de passe"));
        Assert.AreEqual(new ConversionReport(false, VaultConversion.WrongSecret, place.Decrypted), wrong);
        Assert.IsFalse(Directory.Exists(place.Decrypted));

        var decrypting = new Reports();
        var decrypted = conversions.Decrypt(place.Encrypted, place.Decrypted, VaultSecret.Password(Password), decrypting);
        Assert.AreEqual(new ConversionReport(true, decrypted.Message, place.Decrypted, files), decrypted);
        AssertSteps(decrypting, files, "déchiffrement");
        CollectionAssert.AreEqual(Contents(vault.Root), Contents(place.Decrypted));
        Assert.IsTrue(conversions.Decrypt(place.Encrypted, place.Recovered, VaultSecret.RecoveryKey(draft.RecoveryKey)).Succeeded);

        Assert.AreEqual(Path.Combine(place.Root, "chiffre (2)"), conversions.FreeDestination(place.Root, "chiffre"));
    }

    [TestMethod]
    public void The_application_adapter_refuses_what_it_cannot_prepare_and_keeps_the_kind_of_secret()
    {
        using var vault = SmallVault();
        using var place = new Place();

        // Une erreur à la préparation (ici des réglages refusés, comme un disque qui lâche) est un refus, que l'écran sait dire.
        var refused = Assert.Throws<OperationException>(() => new VaultConversions(PasswordKeys.NewPbkdf2() with { Passes = 1 }).PrepareEncryption(vault.Root, Password).Dispose());
        Assert.Contains("rien n'a été préparé", refused.Message);
        Assert.IsNotNull(refused.InnerException);

        // Un mot de passe qui a la forme d'une clé de récupération reste un mot de passe : l'adaptateur dit ce que c'est.
        var conversions = new VaultConversions(PasswordKeys.NewPbkdf2());
        var random = RecoveryKey.Create();
        var lookalike = RecoveryKey.Format(random);
        PasswordKeys.Clear(random);
        using var draft = conversions.PrepareEncryption(vault.Root, lookalike);
        Assert.IsTrue(conversions.Encrypt(draft, place.Encrypted).Succeeded);

        var decrypted = conversions.Decrypt(place.Encrypted, place.Decrypted, VaultSecret.Password(lookalike));

        Assert.IsTrue(decrypted.Succeeded, decrypted.Message);
        Assert.IsTrue(conversions.Decrypt(place.Encrypted, place.Recovered, VaultSecret.RecoveryKey(draft.RecoveryKey)).Succeeded);
    }

    [TestMethod]
    public void The_lock_of_the_source_is_held_for_the_whole_conversion()
    {
        using var vault = SmallVault();
        using var place = new Place();
        var lockFile = Path.Combine(vault.Root, VaultLayout.LockFile);
        Assert.IsTrue(File.Exists(lockFile), "un coffre déjà ouvert a son verrou");
        var before = File.ReadAllBytes(lockFile);
        var taken = new List<bool>();

        void Try(string root)
        {
            using var other = KyfRev.Infrastructure.Vault.Files.VaultLock.TryAcquire(root, DateTimeOffset.UnixEpoch, out _);
            taken.Add(other is not null);
        }

        Assert.IsTrue(VaultConversion.Encrypt(vault.Root, place.Encrypted, Password, PasswordKeys.NewPbkdf2(), _ => Try(vault.Root)).Succeeded);
        Assert.IsFalse(taken.Single(), "pendant le chiffrement, une autre fenêtre trouve le coffre pris");

        // Le déchiffrement tient de même celui du coffre chiffré, qui reste sur le disque.
        Directory.CreateDirectory(Path.Combine(place.Encrypted, ".suivi"));
        File.WriteAllBytes(Path.Combine(place.Encrypted, VaultLayout.LockFile), []);
        taken.Clear();
        Assert.IsTrue(VaultConversion.Decrypt(place.Encrypted, place.Decrypted, Password, _ => Try(place.Encrypted)).Succeeded);
        Assert.IsFalse(taken.Single(), "pendant le déchiffrement aussi");

        CollectionAssert.AreEqual(before, File.ReadAllBytes(lockFile), "le verrou n'est pas écrit");
        using var after = KyfRev.Infrastructure.Vault.Files.VaultLock.TryAcquire(vault.Root, DateTimeOffset.UnixEpoch, out _);
        Assert.IsNotNull(after, "relâché, une fois la conversion finie");
    }

    [TestMethod]
    public void The_application_adapter_draws_a_new_salt_for_each_preparation()
    {
        using var vault = SmallVault();
        var parameters = PasswordKeys.NewPbkdf2();
        var conversions = new VaultConversions(parameters);

        using var first = (EncryptionPlan)conversions.PrepareEncryption(vault.Root, Password);
        using var second = (EncryptionPlan)conversions.PrepareEncryption(vault.Root, Password);

        Assert.AreEqual(parameters with { Salt = first.Header.Password.Salt }, first.Header.Password, "les réglages donnés, sel excepté");
        Assert.AreNotEqual(parameters.Salt, first.Header.Password.Salt);
        Assert.AreNotEqual(first.Header.Password.Salt, second.Header.Password.Salt, "chaque coffre a son propre sel");
        Assert.AreNotEqual(first.RecoveryKey, second.RecoveryKey);

        // Sans réglages, Argon2id est calibré sur ce poste à chaque préparation : ici le temps d'un refus.
        using var place = new Place();
        Assert.Throws<OperationException>(() => new VaultConversions().PrepareEncryption(place.Root, Password).Dispose());
    }

    /// <summary>Le chiffrement préparé du coffre en clair <paramref name="source"/>, avec le mot de passe des essais.</summary>
    private static EncryptionPlan Prepared(string source)
    {
        var prepared = VaultConversion.Prepare(source, Password, PasswordKeys.NewPbkdf2());
        Assert.IsNotNull(prepared.Plan, prepared.Refusal);
        return prepared.Plan;
    }

    /// <summary>Chaque fichier écrit, de 1 à <paramref name="files"/>, puis chaque fichier relu, dans l'ordre.</summary>
    private static void AssertSteps(Reports reports, int files, string what)
    {
        Assert.IsGreaterThan(1, files, what);
        ConversionProgress[] expected =
        [
            .. Enumerable.Range(1, files).Select(done => new ConversionProgress(ConversionStep.Writing, done, files)),
            .. Enumerable.Range(1, files).Select(done => new ConversionProgress(ConversionStep.Verifying, done, files)),
        ];
        CollectionAssert.AreEqual(expected, reports.All, $"{what} : {string.Join(", ", reports.All)}");
    }

    /// <summary>
    /// Une progression synchrone : chaque étape est notée au moment où la conversion la dit (<see cref="Progress{T}"/> la
    /// posterait plus tard, sur un autre fil).
    /// </summary>
    private sealed class Reports : IProgress<ConversionProgress>
    {
        public List<ConversionProgress> All { get; } = [];

        public void Report(ConversionProgress value) => All.Add(value);
    }

    /// <summary>Un chiffrement préparé ailleurs que par la conversion : l'adaptateur le refuse.</summary>
    private sealed class ForeignDraft(string source) : EncryptionDraft(source, "clé")
    {
        public bool Released { get; private set; }

        protected override void Dispose(bool disposing) => Released = true;
    }
}
