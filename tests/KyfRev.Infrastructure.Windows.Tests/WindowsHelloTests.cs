using System.Text;
using KyfRev.Application.Ports;

namespace KyfRev.Infrastructure.Windows.Tests;

/// <summary>
/// Windows Hello (rangée 29C) : la logique sur un NCrypt simulé, qui ne montre jamais la fenêtre de Windows ; et, pour de
/// vrai, seulement ce qui ne demande rien à l'utilisateur : NCrypt sur le fournisseur logiciel, avec une clé éphémère.
/// </summary>
[TestClass]
public sealed class WindowsHelloTests
{
    private const string Sid = "S-1-5-21-1-2-3-1001";

    [TestMethod]
    public void The_key_is_created_the_first_time_then_seals_without_asking()
    {
        var ncrypt = new FakeNcrypt();
        var hello = new WindowsHello(ncrypt, Sid) { Owner = 42 };
        Assert.IsTrue(hello.IsAvailable);
        Assert.AreEqual(42, hello.Owner);

        var sealedKey = hello.Seal([1, 2, 3]);

        CollectionAssert.AreEqual(new byte[] { 3, 2, 1 }, sealedKey);
        Assert.AreEqual(WindowsHello.KeyNameOf(Sid), ncrypt.Created);
        Assert.AreEqual(2048, ncrypt.Int("Length"));
        Assert.AreEqual(3, ncrypt.Int("Key Usage"), "déchiffrer et signer, jamais exporter");
        Assert.AreEqual(1, ncrypt.Int("NgcCacheType"), "le geste à chaque usage");
        Assert.AreEqual(42, ncrypt.Int("HWND Handle"), "la fenêtre de Windows se pose sur celle de kyfRev");
        Assert.AreEqual(WindowsHello.Messages.Creating + "\0", ncrypt.Text("Use Context"));
        Assert.IsTrue(ncrypt.Finalized);
        Assert.IsTrue(ncrypt.SilentOpens > 0, "sceller ne demande rien");

        ncrypt.Created = null;
        CollectionAssert.AreEqual(new byte[] { 5, 4 }, hello.Seal([4, 5]));
        Assert.IsNull(ncrypt.Created, "la clé existe : elle n'est pas recréée");
        Assert.AreEqual(0, ncrypt.Open, "chaque objet ouvert est libéré");
    }

    [TestMethod]
    public void Opening_asks_windows_with_the_gesture_required_and_a_cancel_gives_nothing()
    {
        var ncrypt = new FakeNcrypt { KeyExists = true };
        var hello = new WindowsHello(ncrypt, Sid);

        CollectionAssert.AreEqual(new byte[] { 2, 1 }, hello.Open([1, 2], "Ouvrir le coffre « Coffre » dans kyfRev."));
        Assert.AreEqual("Ouvrir le coffre « Coffre » dans kyfRev.\0", ncrypt.Text("Use Context"));
        Assert.AreEqual(1, ncrypt.Int("PinCacheIsGestureRequired"));
        Assert.IsFalse(ncrypt.Has("HWND Handle"), "sans fenêtre, Windows la place lui-même");
        Assert.AreEqual(0, ncrypt.SilentOpens, "rouvrir demande le geste de l'utilisateur");

        ncrypt.DecryptStatus = Ncrypt.UserCancelled;
        Assert.IsNull(hello.Open([1, 2], "m"));
        ncrypt.DecryptStatus = Ncrypt.Cancelled;
        Assert.IsNull(hello.Open([1, 2], "m"));
        ncrypt.DecryptStatus = unchecked((int)0x80280095);
        var refused = Assert.ThrowsExactly<WindowsHelloException>(() => hello.Open([1, 2], "m"));
        Assert.Contains("0x80280095", refused.Message);
        ncrypt.DecryptStatus = 0;
        ncrypt.Refused.Add("PinCacheIsGestureRequired");
        Assert.Contains("rouvrir la clé", Assert.ThrowsExactly<WindowsHelloException>(() => hello.Open([1, 2], "m")).Message);
        Assert.AreEqual(0, ncrypt.Open);
    }

    [TestMethod]
    public void A_missing_key_a_cancelled_creation_or_a_refusal_is_said()
    {
        var ncrypt = new FakeNcrypt();
        var hello = new WindowsHello(ncrypt, Sid);
        Assert.AreEqual(WindowsHello.Messages.NoKey, Assert.ThrowsExactly<WindowsHelloException>(() => hello.Open([1], "m")).Message);

        ncrypt.FinalizeStatus = Ncrypt.UserCancelled;
        Assert.IsNull(hello.Seal([1]), "la création annulée ne retient rien");
        Assert.IsFalse(ncrypt.KeyExists);
        ncrypt.FinalizeStatus = 0;
        ncrypt.CreateStatus = unchecked((int)0x80090009);
        Assert.Contains("trouver la clé", Assert.ThrowsExactly<WindowsHelloException>(() => hello.Seal([1])).Message);
        ncrypt.CreateStatus = 0;

        // Les premières versions de Windows 10 : l'autre nom de la propriété. Une propriété refusée défait la création.
        ncrypt.Refused.Add("NgcCacheType");
        Assert.IsNotNull(hello.Seal([1]));
        Assert.AreEqual(1, ncrypt.Int("NgcCacheTypeProperty"));
        ncrypt.KeyExists = false;
        ncrypt.Refused.Add("NgcCacheTypeProperty");
        Assert.ThrowsExactly<WindowsHelloException>(() => hello.Seal([1]));
        Assert.AreEqual(0, ncrypt.Open, "rien ne reste d'une création qui échoue");
        ncrypt.Refused.Clear();

        ncrypt.KeyExists = true;
        ncrypt.EncryptStatus = unchecked((int)0x80090027);
        Assert.Contains("sceller la clé", Assert.ThrowsExactly<WindowsHelloException>(() => hello.Seal([1])).Message);
        ncrypt.ProviderStatus = unchecked((int)0x80090017);
        Assert.Contains("fournisseur", Assert.ThrowsExactly<WindowsHelloException>(() => hello.Seal([1])).Message);
        Assert.Contains("fournisseur", Assert.ThrowsExactly<WindowsHelloException>(() => hello.Open([1], "m")).Message);
        Assert.AreEqual(0, ncrypt.Open);

        _ = new WindowsHelloException();
        Assert.AreEqual("cause", new WindowsHelloException("m", new InvalidOperationException("cause")).InnerException!.Message);
    }

    [TestMethod]
    public void Without_windows_hello_nothing_is_sealed_or_opened()
    {
        var ncrypt = new FakeNcrypt { HelloConfigured = false };
        var hello = new WindowsHello(ncrypt, Sid);
        Assert.IsFalse(hello.IsAvailable);
        Assert.IsNull(hello.Seal([1]));
        Assert.AreEqual(WindowsHello.Messages.Unavailable, Assert.ThrowsExactly<WindowsHelloException>(() => hello.Open([1], "m")).Message);
        Assert.IsFalse(new WindowsHello(new FakeNcrypt(), string.Empty).IsAvailable, "sans utilisateur connu, rien");

        Assert.IsFalse(NoWindowsHello.Instance.IsAvailable);
        Assert.IsNull(NoWindowsHello.Instance.Seal([1]));
        Assert.IsNull(NoWindowsHello.Instance.Open([1], "m"));
    }

    [TestMethod]
    public void The_native_calls_seal_and_open_with_the_software_provider_and_an_ephemeral_key()
    {
        // Le fournisseur logiciel de Windows, une clé sans nom : rien n'est affiché, rien n'est gardé.
        var native = new NativeNcrypt("Microsoft Software Key Storage Provider");
        Assert.AreEqual(0, native.OpenProvider(out var provider));
        try
        {
            Assert.AreNotEqual(0, native.OpenKey(provider, "kyfRev-essai-" + Guid.NewGuid().ToString("N"), silent: true, out var none), "aucune clé de ce nom");
            native.Free(none);
            Assert.AreEqual(0, native.CreateKey(provider, null, out var key));
            try
            {
                Assert.AreEqual(0, native.SetProperty(key, "Length", BitConverter.GetBytes(2048)));
                Assert.AreEqual(0, native.FinalizeKey(key));
                var secret = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
                Assert.AreEqual(0, native.Encrypt(key, secret, out var sealedSecret));
                Assert.HasCount(256, sealedSecret);
                Assert.AreEqual(0, native.Decrypt(key, sealedSecret, out var opened));
                CollectionAssert.AreEqual(secret, opened);
                Assert.AreNotEqual(0, native.Decrypt(key, [1, 2, 3], out var refused));
                Assert.IsEmpty(refused);
                Assert.AreNotEqual(0, native.Encrypt(key, new byte[300], out var tooLong));
                Assert.IsEmpty(tooLong);
            }
            finally
            {
                native.Free(key);
            }
        }
        finally
        {
            native.Free(provider);
        }
        native.Free(0);

        // Windows Hello lui-même : seulement la question de sa configuration, qui ne montre rien.
        _ = new NativeNcrypt(WindowsHello.HelloProvider).HasHello(Sid);
        _ = new WindowsHello().IsAvailable;
    }

    /// <summary>NCrypt simulé : chiffrer inverse les octets, chaque propriété posée est gardée, chaque objet ouvert compté.</summary>
    private sealed class FakeNcrypt : INcrypt
    {
        private readonly Dictionary<string, byte[]> _properties = [];

        public bool HelloConfigured { get; init; } = true;

        public bool KeyExists { get; set; }

        public int ProviderStatus { get; set; }

        public int CreateStatus { get; set; }

        public int FinalizeStatus { get; set; }

        public int EncryptStatus { get; set; }

        public int DecryptStatus { get; set; }

        public HashSet<string> Refused { get; } = [];

        public string? Created { get; set; }

        public bool Finalized { get; private set; }

        public int SilentOpens { get; private set; }

        public int Open { get; private set; }

        public bool Has(string property) => _properties.ContainsKey(property);

        public long Int(string property) => _properties[property].Length == 8 ? BitConverter.ToInt64(_properties[property]) : BitConverter.ToInt32(_properties[property]);

        public string Text(string property) => Encoding.Unicode.GetString(_properties[property]);

        public bool HasHello(string sid) => HelloConfigured;

        public int OpenProvider(out nint provider)
        {
            provider = ProviderStatus == 0 ? Opened() : 0;
            return ProviderStatus;
        }

        public int OpenKey(nint provider, string name, bool silent, out nint key)
        {
            SilentOpens += silent ? 1 : 0;
            key = KeyExists ? Opened() : 0;
            return KeyExists ? 0 : Ncrypt.NoKey;
        }

        public int CreateKey(nint provider, string? name, out nint key)
        {
            key = 0;
            if (CreateStatus != 0)
            {
                return CreateStatus;
            }
            Created = name;
            key = Opened();
            return 0;
        }

        public int SetProperty(nint handle, string property, ReadOnlySpan<byte> value)
        {
            if (Refused.Contains(property))
            {
                return unchecked((int)0x80090029);
            }
            _properties[property] = value.ToArray();
            return 0;
        }

        public int FinalizeKey(nint key)
        {
            Finalized = FinalizeStatus == 0;
            KeyExists = Finalized;
            return FinalizeStatus;
        }

        public int Encrypt(nint key, ReadOnlySpan<byte> secret, out byte[] sealedSecret)
        {
            var reversed = secret.ToArray();
            Array.Reverse(reversed);
            sealedSecret = EncryptStatus == 0 ? reversed : [];
            return EncryptStatus;
        }

        public int Decrypt(nint key, byte[] sealedSecret, out byte[] secret)
        {
            var reversed = (byte[])sealedSecret.Clone();
            Array.Reverse(reversed);
            secret = DecryptStatus == 0 ? reversed : [];
            return DecryptStatus;
        }

        public void Free(nint handle)
        {
            if (handle != 0)
            {
                Open--;
            }
        }

        private nint Opened()
        {
            Open++;
            return 100 + Open;
        }
    }
}
