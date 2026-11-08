using System.Security.Cryptography;
using System.Text;
using KyfRev.Application;
using KyfRev.Application.Ports;

namespace KyfRev.Extensions.Host.Tests;

/// <summary>La recherche de mise à jour : un manifeste signé, et rien d'autre que la page de sa version.</summary>
[TestClass]
public sealed class UpdateCheckerTests
{
    private static readonly DateOnly Day = new(2026, 11, 8);

    private static readonly string[] Asked =
    [
        "https://raw.githubusercontent.com/KyllianFF/kyfRev-dotnet/main/mise-a-jour/derniere.json",
        "https://raw.githubusercontent.com/KyllianFF/kyfRev-dotnet/main/mise-a-jour/derniere.sig",
    ];

    [TestMethod]
    [DataRow("1.0.0", "1.0.0-alpha.0", true)]
    [DataRow("1.0.0-alpha.1", "1.0.0-alpha.0", true)]
    [DataRow("1.0.0-beta.1", "1.0.0-alpha.9", true)]
    [DataRow("1.0.0-alpha.10", "1.0.0-alpha.9", true)]
    [DataRow("1.0.0-alpha.1.2", "1.0.0-alpha.1", true)]
    [DataRow("1.0.0-alpha", "1.0.0-1", true)]
    [DataRow("0.10.0", "0.9.2", true)]
    [DataRow("1.0.1", "1.0.0", true)]
    [DataRow("1.1.0", "1.0.9", true)]
    [DataRow("2.0.0", "1.9.9", true)]
    [DataRow("1.0.0", "1.0.0", false)]
    [DataRow("1.0.0-alpha.0", "1.0.0", false)]
    [DataRow("1.0.0-1", "1.0.0-alpha", false)]
    [DataRow("0.9.9", "1.0.0-alpha.0", false)]
    [DataRow("n'importe quoi", "1.0.0", false)]
    [DataRow("1.0", "0.9.0", false)]
    [DataRow("2.0.0", "pas une version", false)]
    public void Versions_are_ordered_as_semantic_versions(string candidate, string installed, bool newer) =>
        Assert.AreEqual(newer, UpdateChecker.IsNewer(candidate, installed));

    [TestMethod]
    public async Task A_newer_signed_version_is_announced_with_its_page_only()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var online = Signed(key, "1.2.0");

        var result = await Checker(online, key, "1.1.0").CheckAsync(CancellationToken.None);

        Assert.IsTrue(result.Succeeded, result.Error);
        Assert.AreEqual(new UpdateStatus("1.1.0", "1.2.0", true, new Uri("https://github.com/KyllianFF/kyfRev-dotnet/releases/tag/v1.2.0")), result.Value);
        CollectionAssert.AreEqual(Asked, online.Asked, "le manifeste et sa signature, rien d'autre");
    }

    [TestMethod]
    public async Task The_same_version_is_up_to_date()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var result = await Checker(Signed(key, "1.1.0"), key, "1.1.0").CheckAsync(CancellationToken.None);

        Assert.IsTrue(result.Succeeded, result.Error);
        Assert.IsFalse(result.Value.IsNewer);
    }

    [TestMethod]
    public async Task An_altered_manifest_is_refused()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var online = Signed(key, "1.2.0");
        online.Manifest = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(online.Manifest).Replace("1.2.0", "9.9.9", StringComparison.Ordinal));

        var result = await Checker(online, key, "1.1.0").CheckAsync(CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.Contains("signature", result.Error!);
    }

    [TestMethod]
    public async Task A_manifest_signed_by_an_unknown_key_is_refused()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var result = await Checker(Signed(other, "1.2.0"), key, "1.1.0").CheckAsync(CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
    }

    [TestMethod]
    [DataRow("{\"format\":2,\"version\":\"1.2.0\"}")]
    [DataRow("{\"format\":1,\"version\":\"../../autre-page\"}")]
    [DataRow("{\"format\":1}")]
    [DataRow("pas du JSON")]
    [DataRow("{\"format\":\"un\",\"version\":\"1.2.0\"}")]
    public void A_signed_manifest_that_says_nothing_valid_is_refused(string text)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var manifest = Encoding.UTF8.GetBytes(text);

        var result = UpdateChecker.Verify(manifest, ExtensionCatalogue.Sign(manifest, key, "essai"), Keys(key));

        Assert.IsFalse(result.Succeeded);
        Assert.Contains("illisible", result.Error!);
    }

    [TestMethod]
    public void A_huge_manifest_is_not_read()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var manifest = new byte[5000];

        Assert.Contains("trop grosse", UpdateChecker.Verify(manifest, ExtensionCatalogue.Sign(manifest, key, "essai"), Keys(key)).Error!);
    }

    [TestMethod]
    public void An_unreadable_signature_is_refused()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        Assert.IsFalse(UpdateChecker.Verify(UpdateChecker.Write("1.2.0", Day), Encoding.UTF8.GetBytes("pas une signature"), Keys(key)).Succeeded);
    }

    [TestMethod]
    public async Task A_network_failure_is_said()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var result = await Checker(new Online { Fails = true }, key, "1.1.0").CheckAsync(CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.Contains("n'a pas abouti", result.Error!);
    }

    [TestMethod]
    [DataRow("1.2")]
    [DataRow("../autre")]
    [DataRow("1.2.0/../..")]
    public void Only_a_version_number_can_be_published(string version) =>
        Assert.ThrowsExactly<ArgumentException>(() => UpdateChecker.Write(version, Day));

    [TestMethod]
    public void The_published_manifest_if_any_is_signed_by_a_known_key()
    {
        var folder = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(folder, "KyfRev.slnx")))
        {
            folder = Path.GetDirectoryName(folder)!;
        }
        var manifest = Path.Combine(folder, UpdateChecker.ManifestFile);
        if (File.Exists(manifest))
        {
            var verified = UpdateChecker.Verify(File.ReadAllBytes(manifest), File.ReadAllBytes(Path.Combine(folder, UpdateChecker.SignatureFile)), ExtensionCatalogue.KnownKeys);
            Assert.IsTrue(verified.Succeeded, verified.Error);
        }
    }

    private static UpdateChecker Checker(Online online, ECDsa key, string current) => new(online, Keys(key), current);

    private static CatalogueKey[] Keys(ECDsa key) => [new("essai", key.ExportSubjectPublicKeyInfoPem())];

    private static Online Signed(ECDsa key, string version)
    {
        var manifest = UpdateChecker.Write(version, Day);
        return new Online { Manifest = manifest, Signature = ExtensionCatalogue.Sign(manifest, key, "essai") };
    }

    private sealed class Unreachable : OperationException
    {
        public Unreachable()
            : base("réseau injoignable")
        {
        }

        public Unreachable(string message)
            : base(message)
        {
        }

        public Unreachable(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    private sealed class Online : IOnlineReader
    {
        public byte[] Manifest { get; set; } = [];

        public byte[] Signature { get; set; } = [];

        public bool Fails { get; init; }

        public List<string> Asked { get; } = [];

        public Task<string> ReadTextAsync(Uri address, CancellationToken cancellationToken) => throw new NotSupportedException("le manifeste se lit en octets");

        public Task<byte[]> ReadBytesAsync(Uri address, CancellationToken cancellationToken)
        {
            Asked.Add(address.ToString());
            if (Fails)
            {
                throw new Unreachable();
            }
            return Task.FromResult(address.AbsolutePath.EndsWith(".sig", StringComparison.Ordinal) ? Signature : Manifest);
        }
    }
}
