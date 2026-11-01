using System.IO;
using System.Text;
using KyfRev.Application.Model;

namespace KyfRev.Infrastructure.Windows.Tests;

/// <summary>La séance d'étude gardée sur ce poste (<c>seance.json</c>), pour être reprise.</summary>
[TestClass]
public sealed class StudySessionFileTests
{
    private static readonly StudySession Session = new(
        @"C:\Users\Élève\Documents\kyfRev\Coffre",
        new DateOnly(2026, 11, 2),
        [new SessionItem(ItemKind.Sheet, "01J0FICHE", "01J0FICHE"), new SessionItem(ItemKind.Passage, "01J0PASSAGE", "01J0FICHE")],
        1,
        1,
        0,
        0,
        TimeSpan.FromSeconds(83.25),
        true);

    [TestMethod]
    public void A_session_is_kept_and_read_back()
    {
        using var folder = new Folder();
        var store = new StudySessionFile(Path.Combine(folder.Path, "v1"));

        Assert.IsNull(store.Load(), "rien de gardé");
        Assert.IsTrue(store.Save(Session));

        var read = store.Load()!;
        Assert.AreEqual(Session with { Items = [] }, read with { Items = [] });
        CollectionAssert.AreEqual(Session.Items.ToArray(), read.Items.ToArray());
        Assert.AreEqual(1, read.Remaining);
        Assert.AreEqual(
            "{\n  \"format\": 1,\n  \"coffre\": \"C:\\\\Users\\\\Élève\\\\Documents\\\\kyfRev\\\\Coffre\",\n  \"jour\": \"2026-11-02\",\n  \"elements\": [\n    {\n      \"genre\": \"fiche\",\n      \"id\": \"01J0FICHE\",\n      \"fiche\": \"01J0FICHE\"\n    },\n    {\n      \"genre\": \"passage\",\n      \"id\": \"01J0PASSAGE\",\n      \"fiche\": \"01J0FICHE\"\n    }\n  ],\n  \"position\": 1,\n  \"faits\": 1,\n  \"reportes\": 0,\n  \"passes\": 0,\n  \"dureeSecondes\": 83.25,\n  \"rappelActif\": true\n}",
            File.ReadAllText(Path.Combine(folder.Path, "v1", "seance.json"), Encoding.UTF8));

        Assert.IsTrue(store.Forget());
        Assert.IsNull(store.Load(), "une séance terminée est oubliée");
    }

    [TestMethod]
    [DataRow("pas du JSON")]
    [DataRow("[]")]
    [DataRow("""{"format":2}""")]
    [DataRow("""{"format":1,"elements":{}}""")]
    [DataRow("""{"format":1,"elements":[{"genre":"carte","id":"a","fiche":"a"}]}""")]
    [DataRow("""{"format":1,"elements":[{"genre":"fiche","id":null,"fiche":"a"}]}""")]
    [DataRow("""{"format":1,"elements":[],"coffre":"C","jour":"2 novembre","position":0,"faits":0,"reportes":0,"passes":0,"dureeSecondes":0,"rappelActif":true}""")]
    [DataRow("""{"format":1,"elements":[],"coffre":"C","jour":"2026-11-02","position":"0","faits":0,"reportes":0,"passes":0,"dureeSecondes":0,"rappelActif":true}""")]
    [DataRow("""{"format":1,"elements":[],"coffre":"C","jour":"2026-11-02"}""")]
    public void A_file_that_is_not_valid_resumes_nothing(string content)
    {
        using var folder = new Folder();
        File.WriteAllText(Path.Combine(folder.Path, "seance.json"), content);

        Assert.IsNull(new StudySessionFile(folder.Path).Load());
    }

    [TestMethod]
    public void A_folder_that_cannot_be_written_keeps_nothing()
    {
        using var folder = new Folder();
        // Un fichier à la place du dossier.
        var blocked = Path.Combine(folder.Path, "v1");
        File.WriteAllText(blocked, string.Empty);
        var store = new StudySessionFile(blocked);

        Assert.IsFalse(store.Save(Session));
        Assert.IsFalse(store.Forget());
        Assert.IsNull(store.Load());
        Assert.ThrowsExactly<ArgumentNullException>(() => new StudySessionFile(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.Save(null!));
    }

    private sealed class Folder : IDisposable
    {
        public string Path { get; } = Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kyfrev-seance", Guid.NewGuid().ToString("N"))).FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
