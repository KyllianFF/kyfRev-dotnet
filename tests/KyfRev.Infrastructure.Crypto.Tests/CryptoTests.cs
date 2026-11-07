using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace KyfRev.Infrastructure.Crypto.Tests;

/// <summary>
/// Le noyau du coffre chiffré : vecteurs publiés, Argon2id recalculé par une seconde implémentation, et toute altération,
/// tout échange, tout mauvais mot de passe refusés.
/// </summary>
[TestClass]
[SupportedOSPlatform("windows")]
public sealed class CryptoTests
{
    private const string Vault = "v_01JC0FFEE0000000000000000";

    /// <summary>Des réglages faibles, pour que les essais restent rapides ; un vrai coffre les refuse.</summary>
    private static readonly PasswordParameters Quick = new(PasswordKeys.Argon2id, 1024, 1, new ImmutableBytes(new byte[PasswordKeys.SaltBytes]));

    [TestMethod]
    public void Aes_gcm_gives_the_published_test_vectors()
    {
        // McGrew et Viega, « The Galois/Counter Mode of Operation », cas 13 et 14 : clé et nonce à zéro.
        using var aes = new AesGcm(new byte[32], 16);
        var tag = new byte[16];
        aes.Encrypt(new byte[12], Array.Empty<byte>(), Array.Empty<byte>(), tag);
        Assert.AreEqual("530f8afbc74536b9a963b4f1c4cb738b", Convert.ToHexStringLower(tag));
        var cipher = new byte[16];
        aes.Encrypt(new byte[12], new byte[16], cipher, tag);
        Assert.AreEqual("cea7403d4d606b6e074ec5d3baf39d18", Convert.ToHexStringLower(cipher));
        Assert.AreEqual("d0d1c8a799996bf0265b98b5d48ab919", Convert.ToHexStringLower(tag));
    }

    [TestMethod]
    public void Pbkdf2_gives_the_published_test_vectors()
    {
        // RFC 7914, section 11 : PBKDF2-HMAC-SHA256.
        var parameters = new PasswordParameters(PasswordKeys.Pbkdf2, 0, 1, new ImmutableBytes([.. "salt"u8, .. new byte[12]]));
        Assert.AreEqual(
            "55ac046e56e3089fec1691c22544b605f94185216dde0465e68b9d57c20dacbc49ca9cccf179b645991664b39d77ef317c71b845b1e30bd509112041d3a19783",
            Convert.ToHexStringLower(Rfc2898DeriveBytes.Pbkdf2("passwd"u8, "salt"u8, 1, HashAlgorithmName.SHA256, 64)));
        Assert.AreEqual(
            "4ddcd8f60b98be21830cee5ef22701f9641a4418d04c0414aeff08876b34ab56a1d425a1225833549adb841b51c9b3176a272bdebba1d078478f62b397f33c8d",
            Convert.ToHexStringLower(Rfc2898DeriveBytes.Pbkdf2("Password"u8, "NaCl"u8, 80000, HashAlgorithmName.SHA256, 64)));
        CollectionAssert.AreEqual(
            Rfc2898DeriveBytes.Pbkdf2("passwd"u8, parameters.Salt.ToArray(), 1, HashAlgorithmName.SHA256, 32),
            PasswordKeys.Derive("passwd", parameters),
            "la dérivation du coffre est bien PBKDF2-HMAC-SHA256");
    }

    [TestMethod]
    [DataRow("mot de passe", 1024, 1)]
    [DataRow("Ünïcödé et ☕", 4096, 3)]
    [DataRow("une phrase de passe assez longue pour dépasser un bloc de BLAKE2b, avec des accents et des chiffres 1234567890", 2048, 2)]
    public void Argon2id_from_libsodium_matches_an_independent_implementation(string password, int memoryKib, int passes)
    {
        var salt = Encoding.ASCII.GetBytes("sel-de-16-octets");
        var parameters = new PasswordParameters(PasswordKeys.Argon2id, memoryKib, passes, new ImmutableBytes(salt));
        using var reference = new Argon2id(Encoding.UTF8.GetBytes(password)) { Salt = salt, DegreeOfParallelism = 1, MemorySize = memoryKib, Iterations = passes };

        CollectionAssert.AreEqual(reference.GetBytes(32), PasswordKeys.Derive(password, parameters));
    }

    [TestMethod]
    public void Weak_or_unreadable_settings_are_refused()
    {
        Assert.IsTrue(PasswordKeys.IsStrong(PasswordKeys.NewArgon2id()));
        Assert.IsTrue(PasswordKeys.IsStrong(PasswordKeys.NewPbkdf2()));
        Assert.IsFalse(PasswordKeys.IsStrong(Quick));
        Assert.IsFalse(PasswordKeys.IsStrong(PasswordKeys.NewPbkdf2() with { Passes = 1000 }));
        Assert.IsFalse(PasswordKeys.IsStrong(PasswordKeys.NewArgon2id() with { Salt = new ImmutableBytes(new byte[8]) }));
        Assert.ThrowsExactly<VaultCryptoException>(() => PasswordKeys.Derive("x", Quick with { Algorithm = "md5" }));
        Assert.ThrowsExactly<VaultCryptoException>(() => PasswordKeys.Derive("x", Quick with { MemoryKib = 4 * 1024 * 1024 }));
        Assert.ThrowsExactly<VaultCryptoException>(() => PasswordKeys.Derive("x", PasswordKeys.NewPbkdf2() with { MemoryKib = 1 }));
        Assert.ThrowsExactly<ArgumentException>(() => VaultHeader.Create(Vault, "mot de passe", Quick));
        Assert.ThrowsExactly<ArgumentException>(() => VaultHeader.Create(Vault, string.Empty, PasswordKeys.NewArgon2id()));
    }

    [TestMethod]
    public void Calibration_keeps_the_minimum_and_adds_passes_for_a_longer_target()
    {
        var quick = PasswordKeys.Calibrated(TimeSpan.Zero);
        Assert.AreEqual((PasswordKeys.Argon2id, PasswordKeys.MinimumMemoryKib, PasswordKeys.MinimumPasses), (quick.Algorithm, quick.MemoryKib, quick.Passes));
        Assert.IsTrue(PasswordKeys.IsStrong(PasswordKeys.Calibrated(TimeSpan.FromMilliseconds(200))));
        Assert.AreNotEqual(PasswordKeys.NewArgon2id().Salt, PasswordKeys.NewArgon2id().Salt, "un sel neuf à chaque coffre");
    }

    [TestMethod]
    public void A_sealed_file_opens_only_with_its_key_its_vault_and_its_name()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var content = "# La notion d'autorité\n\nObéir sans contrainte ni persuasion.\n"u8.ToArray();

        var sealedBytes = SealedFile.Seal(key, Vault, "f_1", content);

        CollectionAssert.AreEqual(content, SealedFile.Open(key, Vault, "f_1", sealedBytes));
        Assert.AreEqual(content.Length + SealedFile.Overhead, sealedBytes.Length);
        Assert.IsFalse(sealedBytes.AsSpan().IndexOf("autorit"u8) >= 0, "rien du contenu en clair");
        CollectionAssert.AreNotEqual(sealedBytes, SealedFile.Seal(key, Vault, "f_1", content), "un nonce neuf à chaque écriture");
        StringAssert.Contains(Assert.ThrowsExactly<VaultCryptoException>(() => SealedFile.Open(key, Vault, "f_2", sealedBytes)).Message, "déplacé", StringComparison.Ordinal);
        Assert.ThrowsExactly<VaultCryptoException>(() => SealedFile.Open(key, "v_autre", "f_1", sealedBytes));
        Assert.ThrowsExactly<VaultCryptoException>(() => SealedFile.Open(RandomNumberGenerator.GetBytes(32), Vault, "f_1", sealedBytes));
        CollectionAssert.AreEqual(Array.Empty<byte>(), SealedFile.Open(key, Vault, "vide", SealedFile.Seal(key, Vault, "vide", [])));
    }

    [TestMethod]
    public void Changing_any_byte_of_a_sealed_file_is_refused()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var sealedBytes = SealedFile.Seal(key, Vault, "f_1", "Un passage."u8);

        for (var i = 0; i < sealedBytes.Length; i++)
        {
            var changed = (byte[])sealedBytes.Clone();
            changed[i] ^= 0x01;
            Assert.ThrowsExactly<VaultCryptoException>(() => SealedFile.Open(key, Vault, "f_1", changed), $"octet {i}");
        }
        Assert.ThrowsExactly<VaultCryptoException>(() => SealedFile.Open(key, Vault, "f_1", sealedBytes.AsSpan(0, 20)));
        Assert.ThrowsExactly<VaultCryptoException>(() => SealedFile.Open(key, Vault, "f_1", sealedBytes.AsSpan(0, sealedBytes.Length - 1)));
    }

    [TestMethod]
    public void A_header_opens_with_its_password_or_its_recovery_key_and_never_otherwise()
    {
        var (header, dataKey, recovery) = VaultHeader.Create(Vault, "correct horse", Quick with { Salt = Salt() }, enforcesStrength: false);

        CollectionAssert.AreEqual(dataKey, header.Open("correct horse"));
        CollectionAssert.AreEqual(dataKey, header.OpenWithRecovery(recovery));
        CollectionAssert.AreEqual(dataKey, header.OpenWithRecovery(Lower(recovery).Replace("-", " ", StringComparison.Ordinal)));
        StringAssert.Contains(Assert.ThrowsExactly<VaultCryptoException>(() => header.Open("Correct horse")).Message, "incorrect", StringComparison.Ordinal);
        StringAssert.Contains(Assert.ThrowsExactly<VaultCryptoException>(() => header.OpenWithRecovery(RecoveryKey.Format(RecoveryKey.Create()))).Message, "pas celle", StringComparison.Ordinal);
        StringAssert.Contains(Assert.ThrowsExactly<VaultCryptoException>(() => header.OpenWithRecovery("ABCD")).Message, "faute de frappe", StringComparison.Ordinal);

        var relu = VaultHeader.Read(header.Write());
        CollectionAssert.AreEqual(dataKey, relu.Open("correct horse"));
        Assert.AreEqual(Vault, relu.VaultId);
        Assert.IsFalse(Encoding.UTF8.GetString(header.Write()).Contains(Convert.ToBase64String(dataKey), StringComparison.Ordinal));
    }

    [TestMethod]
    public void A_new_password_or_recovery_key_rewraps_the_same_data_key_and_retires_the_old_one()
    {
        var (header, dataKey, recovery) = VaultHeader.Create(Vault, "ancien", Quick with { Salt = Salt() }, enforcesStrength: false);

        var changed = header.WithPassword(dataKey, "nouveau mot de passe", PasswordKeys.NewArgon2id());
        CollectionAssert.AreEqual(dataKey, changed.Open("nouveau mot de passe"));
        Assert.ThrowsExactly<VaultCryptoException>(() => changed.Open("ancien"));
        CollectionAssert.AreEqual(dataKey, changed.OpenWithRecovery(recovery), "la clé de récupération reste valable");

        var (renewed, fresh) = changed.WithNewRecovery(dataKey);
        CollectionAssert.AreEqual(dataKey, renewed.OpenWithRecovery(fresh));
        Assert.ThrowsExactly<VaultCryptoException>(() => renewed.OpenWithRecovery(recovery));
        CollectionAssert.AreEqual(dataKey, renewed.Open("nouveau mot de passe"));
        Assert.ThrowsExactly<ArgumentException>(() => header.WithPassword(dataKey, "faible", Quick));
    }

    [TestMethod]
    [DataRow("\"memoireKio\": 1024", "\"memoireKio\": 2048")]
    [DataRow("\"passes\": 1", "\"passes\": 2")]
    [DataRow("\"coffre\": \"v_01JC0FFEE0000000000000000\"", "\"coffre\": \"v_01JC0FFEE0000000000000001\"")]
    public void A_header_changed_in_its_file_no_longer_opens(string written, string changed)
    {
        var (header, _, _) = VaultHeader.Create(Vault, "correct horse", Quick with { Salt = Salt() }, enforcesStrength: false);
        var text = Encoding.UTF8.GetString(header.Write());
        StringAssert.Contains(text, written, StringComparison.Ordinal);

        var tampered = VaultHeader.Read(Encoding.UTF8.GetBytes(text.Replace(written, changed, StringComparison.Ordinal)));

        Assert.ThrowsExactly<VaultCryptoException>(() => tampered.Open("correct horse"));
    }

    [TestMethod]
    [DataRow("pas du json")]
    [DataRow("[1]")]
    [DataRow("{\"format\": 2}")]
    [DataRow("{\"format\": 1, \"coffre\": \"v\", \"motDePasse\": {}, \"recuperation\": {}}")]
    [DataRow("{\"format\": 1, \"coffre\": \"v\", \"motDePasse\": {\"derivation\": \"argon2id\", \"memoireKio\": 1024, \"passes\": 1, \"sel\": \"AAAAAAAAAAAAAAAAAAAAAA==\", \"enveloppe\": \"AAAA\"}, \"recuperation\": {\"enveloppe\": \"AAAA\"}}")]
    public void An_unreadable_header_is_refused(string json) =>
        Assert.ThrowsExactly<VaultCryptoException>(() => VaultHeader.Read(Encoding.UTF8.GetBytes(json)));

    [TestMethod]
    public void A_recovery_key_reads_back_and_refuses_a_typo()
    {
        var key = RecoveryKey.Create();
        var written = RecoveryKey.Format(key);

        Assert.AreEqual(14, written.Split('-').Length);
        Assert.IsTrue(written.Split('-').All(group => group.Length == 4));
        Assert.IsFalse(written.IndexOfAny(['I', 'L', 'O', 'U']) >= 0, "aucun caractère qui se confond");
        CollectionAssert.AreEqual(key, RecoveryKey.Parse(written));
        CollectionAssert.AreEqual(key, RecoveryKey.Parse($"  {Lower(written).Replace('-', ' ')}\n"));
        CollectionAssert.AreEqual(key, RecoveryKey.Parse(written.Replace('0', 'O').Replace('1', 'L')));
        var typo = written.ToCharArray();
        typo[5] = typo[5] == 'A' ? 'B' : 'A';
        Assert.IsNull(RecoveryKey.Parse(new string(typo)));
        Assert.IsNull(RecoveryKey.Parse(written + "A"));
        Assert.IsNull(RecoveryKey.Parse(written.Replace(written[0], 'U')));
        Assert.ThrowsExactly<ArgumentException>(() => RecoveryKey.Format(new byte[16]));
        CollectionAssert.AreNotEqual(RecoveryKey.WrappingKey(key, Vault), RecoveryKey.WrappingKey(key, "v_autre"), "propre à chaque coffre");
    }

    [TestMethod]
    public void A_remembered_key_opens_only_for_its_vault_in_this_session()
    {
        var key = RandomNumberGenerator.GetBytes(32);

        var remembered = RememberedKey.Protect(key, Vault);

        CollectionAssert.AreEqual(key, RememberedKey.Unprotect(remembered, Vault));
        Assert.IsNull(RememberedKey.Unprotect(remembered, "v_autre"));
        var changed = (byte[])remembered.Clone();
        changed[^1] ^= 1;
        Assert.IsNull(RememberedKey.Unprotect(changed, Vault));
        Assert.IsNull(RememberedKey.Unprotect(RememberedKey.Protect(new byte[16], Vault), Vault), "une clé de mauvaise taille n'est pas rendue");
    }

    [TestMethod]
    public void Bytes_compare_by_content_and_never_leak_their_array()
    {
        var bytes = new ImmutableBytes([1, 2, 3]);
        var copy = bytes.ToArray();
        copy[0] = 9;

        Assert.AreEqual(new ImmutableBytes([1, 2, 3]), bytes);
        Assert.AreNotEqual(new ImmutableBytes([1, 2, 4]), bytes);
        Assert.AreEqual(1, bytes.ToArray()[0]);
        Assert.IsFalse(bytes.Equals((object)"autre chose"));
        Assert.AreEqual(3, bytes.Length);
    }

    /// <summary>Le texte en minuscules, comme une personne le recopierait.</summary>
    private static string Lower(string text) => string.Concat(text.Select(char.ToLowerInvariant));

    private static ImmutableBytes Salt() => new(RandomNumberGenerator.GetBytes(PasswordKeys.SaltBytes));
}
