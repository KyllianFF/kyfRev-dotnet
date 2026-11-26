using KyfRev.Application.Ports;

namespace KyfRev.Infrastructure.Crypto.Tests;

/// <summary>
/// La clé retenue avec Windows Hello (rangée 29C) : scellée par lui, puis protégée pour la session ; elle n'ouvre jamais le
/// coffre seule, au démarrage, mais seulement par le geste de l'utilisateur, même après un verrouillage.
/// </summary>
public sealed partial class EncryptedVaultsTests
{
    [TestMethod]
    public void With_windows_hello_the_remembered_key_opens_only_by_the_users_gesture()
    {
        using var place = new Place();
        place.CreateEncrypted();
        var hello = new FakeHello();
        var vaults = place.Vaults(hello: hello);
        Assert.IsTrue(vaults.CanUseHello);
        Assert.IsFalse(vaults.IsRememberedWithHello(place.Vault));

        using (var key = vaults.KeyOf(place.Vault, VaultSecret.Password(Password)))
        {
            Assert.IsTrue(vaults.Remember(place.Vault, key));
        }
        Assert.AreEqual(1, hello.Sealed);
        Assert.IsTrue(vaults.IsRemembered(place.Vault));
        Assert.IsTrue(vaults.IsRememberedWithHello(place.Vault));
        Assert.Contains("\"hello\": true", File.ReadAllText(Path.Combine(place.LocalData, RememberedKeys.FileName)));

        // Au démarrage, rien ne s'ouvre seul ; par le geste, oui, et Windows dit quel coffre.
        Assert.IsNull(vaults.Remembered(place.Vault));
        Assert.AreEqual(0, hello.Asked);
        using (var opened = vaults.RememberedWithHello(place.Vault))
        {
            Assert.IsNotNull(opened);
            CollectionAssert.AreEqual(Shared.Value.Key, opened.Bytes.ToArray());
        }
        Assert.AreEqual("Ouvrir le coffre « Coffre » dans kyfRev.", hello.Message);

        // Verrouillé, il s'ouvre encore par le geste : c'est tout l'intérêt.
        Assert.IsTrue(vaults.StayLocked(place.Vault));
        using (var again = vaults.RememberedWithHello(place.Vault))
        {
            Assert.IsNotNull(again);
        }

        // L'utilisateur renonce : rien. Windows refuse : la raison remonte.
        hello.Cancels = true;
        Assert.IsNull(vaults.RememberedWithHello(place.Vault));
        hello.Cancels = false;
        hello.Refuses = true;
        Assert.ThrowsExactly<VaultCryptoException>(() => vaults.RememberedWithHello(place.Vault));
        hello.Refuses = false;

        // Une clé rendue de travers ne sert à rien, et une clé que l'index refuse est oubliée.
        hello.Gives = new byte[5];
        Assert.IsNull(vaults.RememberedWithHello(place.Vault));
        hello.Gives = new byte[PasswordKeys.KeyBytes];
        Assert.IsNull(vaults.RememberedWithHello(place.Vault));
        Assert.IsFalse(vaults.IsRemembered(place.Vault), "l'index l'a refusée : elle est oubliée");
    }

    [TestMethod]
    public void Without_windows_hello_or_when_it_refuses_the_key_is_not_sealed()
    {
        using var place = new Place();
        place.CreateEncrypted();

        // Une création annulée, ou refusée, ne retient rien : jamais de repli silencieux vers la clé de session.
        var hello = new FakeHello { Cancels = true };
        var vaults = place.Vaults(hello: hello);
        using (var key = vaults.KeyOf(place.Vault, VaultSecret.Password(Password)))
        {
            Assert.IsFalse(vaults.Remember(place.Vault, key));
            hello.Cancels = false;
            hello.Refuses = true;
            Assert.IsFalse(vaults.Remember(place.Vault, key));
        }
        Assert.IsFalse(vaults.IsRemembered(place.Vault));

        // Sans Windows Hello, rien ne change : la clé de session, qui rouvre le coffre au démarrage.
        var plain = place.Vaults(hello: new FakeHello { Available = false });
        Assert.IsFalse(plain.CanUseHello);
        using (var key = plain.KeyOf(place.Vault, VaultSecret.Password(Password)))
        {
            Assert.IsTrue(plain.Remember(place.Vault, key));
        }
        Assert.IsFalse(plain.IsRememberedWithHello(place.Vault));
        Assert.IsNull(plain.RememberedWithHello(place.Vault));
        using (var remembered = plain.Remembered(place.Vault))
        {
            Assert.IsNotNull(remembered);
        }

        // Un en-tête illisible : rien n'est dit retenu avec Windows Hello.
        Assert.IsFalse(place.Vaults(hello: hello).IsRememberedWithHello(Path.Combine(place.Root, "absent")));
        Assert.IsFalse(place.Vaults().CanUseHello, "sans Windows Hello donné, aucun");
    }

    [TestMethod]
    public void A_sealed_key_is_protected_for_the_session_and_its_vault()
    {
        var protectedKey = RememberedKey.ProtectSealed([1, 2, 3], "v_1");
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, RememberedKey.UnprotectSealed(protectedKey, "v_1"));
        Assert.IsNull(RememberedKey.UnprotectSealed(protectedKey, "v_2"), "lié à son coffre");
        Assert.IsNull(RememberedKey.Unprotect(protectedKey, "v_1"), "et distinct d'une clé de session");
        Assert.IsNull(RememberedKey.UnprotectSealed([9, 9, 9], "v_1"));
    }

    /// <summary>Windows Hello simulé : sceller inverse les octets, rouvrir les remet, et compte les fois où il demande.</summary>
    private sealed class FakeHello : IWindowsHello
    {
        public bool Available { get; init; } = true;

        public bool Cancels { get; set; }

        public bool Refuses { get; set; }

        public byte[]? Gives { get; set; }

        public int Sealed { get; private set; }

        public int Asked { get; private set; }

        public string? Message { get; private set; }

        public bool IsAvailable => Available;

        public byte[]? Seal(ReadOnlySpan<byte> secret)
        {
            if (Refuses)
            {
                throw new VaultCryptoException("Windows Hello a refusé.");
            }
            if (Cancels)
            {
                return null;
            }
            Sealed++;
            var reversed = secret.ToArray();
            Array.Reverse(reversed);
            return reversed;
        }

        public byte[]? Open(byte[] sealedSecret, string message)
        {
            Asked++;
            Message = message;
            if (Refuses)
            {
                throw new VaultCryptoException("Windows Hello a refusé.");
            }
            if (Cancels)
            {
                return null;
            }
            if (Gives is { } given)
            {
                return (byte[])given.Clone();
            }
            var reversed = (byte[])sealedSecret.Clone();
            Array.Reverse(reversed);
            return reversed;
        }
    }
}
