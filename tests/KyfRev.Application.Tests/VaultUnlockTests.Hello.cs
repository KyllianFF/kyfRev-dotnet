using KyfRev.Application.Ports;
using KyfRev.Infrastructure.Crypto;

namespace KyfRev.Application.Tests;

/// <summary>
/// Windows Hello (rangée 29C) : la clé retenue est scellée par lui ; le coffre attend au démarrage comme après un
/// verrouillage, et le geste de l'utilisateur le rouvre, sans mot de passe.
/// </summary>
public sealed partial class VaultUnlockTests
{
    [TestMethod]
    public void With_windows_hello_the_vault_waits_at_start_and_after_a_lock_and_the_gesture_opens_it()
    {
        var hello = new FakeHello();
        using var scene = new Scene(hello);
        scene.CreateEncrypted();
        var root = scene.Folder.Vault;
        var vaults = scene.App.Vaults;
        Assert.IsTrue(vaults.UsesHello);
        Assert.IsTrue(scene.App.Encryption.UsesHello);
        Assert.IsFalse(vaults.OffersHello(root), "rien n'est retenu encore");

        Assert.AreEqual(root, vaults.Unlock(root, VaultSecret.Password(Password), remember: true).Vault);
        Assert.IsTrue(vaults.OffersHello(root));
        Assert.AreEqual(root, vaults.UnlockWithHello(root).Vault, "déjà ouvert : rien n'est demandé");
        Assert.AreEqual(0, hello.Asked);

        // Verrouillé : il attend ; le geste le rouvre, et il n'est plus verrouillé.
        Assert.AreEqual(root, vaults.Lock().Locked);
        var opened = vaults.UnlockWithHello(root);
        Assert.AreEqual(root, opened.Vault);
        Assert.IsNull(opened.Locked);
        Assert.AreEqual(1, hello.Asked);
        Assert.IsTrue(scene.App.Sheets.Create(null, "Mitose", string.Empty).Succeeded);

        // Relancé : rien ne s'ouvre seul ; le geste, si.
        Assert.IsNotNull(vaults.Close().Version);
        using var next = scene.Restart();
        Assert.AreEqual(root, next.App.Vaults.OpenAtStartup().Locked, "la clé scellée n'ouvre rien seule");
        Assert.AreEqual(root, next.App.Vaults.UnlockWithHello(root).Vault);
        Assert.IsNotNull(next.App.Vaults.Close().Version);

        // L'utilisateur renonce : il attend, sans message. Windows refuse : il attend, avec la raison.
        hello.Cancels = true;
        var cancelled = next.App.Vaults.UnlockWithHello(root);
        Assert.AreEqual(root, cancelled.Locked);
        Assert.IsNull(cancelled.OpenError);
        hello.Cancels = false;
        hello.Refuses = true;
        var refused = next.App.Vaults.UnlockWithHello(root);
        Assert.AreEqual(root, refused.Locked);
        Assert.AreEqual(FakeHello.Refusal, refused.OpenError);
    }

    [TestMethod]
    public void Without_windows_hello_nothing_changes()
    {
        using var scene = new Scene(new FakeHello { Available = false });
        scene.CreateEncrypted();
        var root = scene.Folder.Vault;
        Assert.IsFalse(scene.App.Vaults.UsesHello);
        Assert.AreEqual(root, scene.App.Vaults.Unlock(root, VaultSecret.Password(Password), remember: true).Vault);
        Assert.IsFalse(scene.App.Vaults.OffersHello(root));
        Assert.IsNotNull(scene.App.Vaults.Close().Version);
        Assert.AreEqual(root, scene.App.Vaults.UnlockWithHello(root).Locked, "pas de clé scellée : il attend son mot de passe");
    }

    /// <summary>Windows Hello simulé : sceller inverse les octets, rouvrir les remet, et compte les fois où il demande.</summary>
    private sealed class FakeHello : IWindowsHello
    {
        public const string Refusal = "Windows Hello a refusé.";

        public bool Available { get; init; } = true;

        public bool Cancels { get; set; }

        public bool Refuses { get; set; }

        public int Asked { get; private set; }

        public bool IsAvailable => Available;

        public byte[]? Seal(ReadOnlySpan<byte> secret)
        {
            var reversed = secret.ToArray();
            Array.Reverse(reversed);
            return reversed;
        }

        public byte[]? Open(byte[] sealedSecret, string message)
        {
            Asked++;
            if (Refuses)
            {
                throw new VaultCryptoException(Refusal);
            }
            if (Cancels)
            {
                return null;
            }
            var reversed = (byte[])sealedSecret.Clone();
            Array.Reverse(reversed);
            return reversed;
        }
    }
}
