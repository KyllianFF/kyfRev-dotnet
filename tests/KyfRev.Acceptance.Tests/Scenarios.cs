using System.Globalization;
using System.Text.Json;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Rendering;
using static KyfRev.Acceptance.Tests.RunningApplication;

namespace KyfRev.Acceptance.Tests;

/// <summary>
/// Les scénarios d'acceptation du jalon J6 (conception, section des tests), aux dates qu'elle donne :
/// l'application réelle tourne, et ses gestes s'enregistrent dans un vrai coffre.
///
/// Ce que l'application ne sait pas encore faire passe par les cas d'usage : écrire le texte d'une fiche
/// et surligner un passage (J7), changer les réglages.
/// </summary>
[TestClass]
public sealed class Scenarios
{
    private const string Title = "La Révolution française";

    private const string Body = """
        # La Révolution française

        En 1789, la société d'Ancien Régime est divisée en trois ordres : le clergé, la noblesse et le tiers état.
        """;

    private static readonly string[] FirstCycleReviews = ["2026-10-09 J0", "2026-10-10 J1", "2026-10-13 J3", "2026-10-20 J7", "2026-11-03 J14", "2026-12-03 J30"];

    private static readonly DateOnly Reminder = new(2027, 2, 8);

    private static readonly DateOnly ArchivedForReminder = new(2026, 12, 8);

    [TestMethod]
    public void T2_the_J30_review_archives_the_sheet_and_leaves_its_passages_due()
    {
        using var trial = new Trial();
        var created = new DateOnly(2026, 10, 9);
        var sheet = trial.On(created, app => NewSheet(app, Title));

        foreach (var (day, step) in new[] { (Day(10, 10), "J1"), (Day(10, 13), "J3"), (Day(10, 20), "J7"), (Day(11, 3), "J14") })
        {
            using var running = trial.Launch(day);
            Assert.Contains(step, running.Row(Today, Title) ?? "");
            running.Open(Today, Title);
            running.Validate();
            running.Back();
            running.WaitWithoutRow(Today, Title);
        }
        // Un passage surligné le jour de J14, jamais révisé depuis : il reste à faire.
        var passage = trial.On(Day(11, 3), app => NewPassage(app, sheet, "trois ordres"));
        var passageBefore = trial.AnnotationsFile(sheet);

        var last = new DateOnly(2026, 12, 3);
        using (var running = trial.Launch(last))
        {
            Assert.Contains("J30", running.Row(Today, Title) ?? "");
            running.Open(Today, Title);
            running.Validate();
            running.Back();
            running.WaitWithoutRow(Today, "Fiche, " + Title);
            Assert.IsNotNull(running.Row(Overdue, Title), "Le passage reste à réviser.");

            // Elle est dans les archives, et s'y relit.
            running.OpenView("Archives");
            Assert.Contains("archivée le", running.ArchivedRow(Title));
            running.OpenArchived(Title);
            running.Back();
            Assert.Contains("1 cycle terminé", running.ArchivedRow(Title));
        }

        var tracking = trial.SheetTracking(sheet);
        Assert.AreEqual("archivee", tracking.GetProperty("statut").GetString());
        Assert.AreEqual("2026-12-03", tracking.GetProperty("archive").GetProperty("archiveeLe").GetString());
        Assert.AreEqual(1, tracking.GetProperty("compteurs").GetProperty("cyclesTermines").GetInt32());
        CollectionAssert.AreEqual(
            FirstCycleReviews,
            Reviews(tracking).Select(review => $"{review.GetProperty("jour").GetString()} {review.GetProperty("etape").GetString()}").ToArray());
        Assert.AreEqual(passageBefore, trial.AnnotationsFile(sheet), "Valider la fiche ne touche pas à ses passages.");
        Assert.AreEqual("active", trial.AnnotationTracking(sheet, passage).GetProperty("statut").GetString());
    }

    [TestMethod]
    public void An_archived_sheet_can_be_excluded_then_put_back_in_review_from_the_archives()
    {
        using var trial = new Trial();
        var sheet = ArchivedSheet(trial, Title);
        var day = ArchivedForReminder.AddDays(1);

        using (var running = trial.Launch(day))
        {
            running.OpenView("Archives");
            running.ToggleArchived(Title);
            Assert.IsFalse(trial.SheetTracking(sheet).GetProperty("archive").GetProperty("inclureDansRappels").GetBoolean());
            running.ToggleArchived(Title);
            Assert.IsTrue(trial.SheetTracking(sheet).GetProperty("archive").GetProperty("inclureDansRappels").GetBoolean());
            running.RestartArchived(Title);
        }

        var tracking = trial.SheetTracking(sheet);
        Assert.AreEqual("active", tracking.GetProperty("statut").GetString());
        var cycle = tracking.GetProperty("cycle");
        Assert.AreEqual(2, cycle.GetProperty("numero").GetInt32());
        Assert.AreEqual(day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), cycle.GetProperty("demarreLe").GetString());
        Assert.AreEqual(day.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), cycle.GetProperty("echeance").GetString());
        using (var running = trial.Launch(day.AddDays(1)))
        {
            Assert.Contains("J1", running.Row(Today, Title) ?? "");
        }
    }

    [TestMethod]
    public void T3_no_reminder_before_the_minimal_rest()
    {
        using var trial = new Trial();
        var archived = new DateOnly(2026, 12, 3);
        var sheet = trial.Living(archived.AddDays(-55), (app, clock) =>
        {
            var id = NewSheet(app, Title);
            LiveFirstCycle(app, clock, id, archived.AddDays(-55));
            for (clock.Day = archived.AddDays(1); clock.Day <= new DateOnly(2027, 2, 1); clock.Day = clock.Day.AddDays(1))
            {
                Assert.IsTrue(app.Reminders.Dashboard().Value.Reminders.IsEmpty, $"Aucun rappel le {clock.Day}.");
            }
            return id;
        });

        using (var running = trial.Launch(new DateOnly(2027, 2, 2)))
        {
            Assert.IsEmpty(running.Rows(Reminders), "Le dernier jour du repos.");
        }
        using (var running = trial.Launch(new DateOnly(2027, 2, 3)))
        {
            Assert.IsNotNull(running.Row(Reminders, Title), "Le premier jour éligible.");
        }

        var draws = trial.Draws();
        Assert.IsTrue(draws.Where(draw => string.CompareOrdinal(draw.Key, "2027-02-03") < 0).All(draw => draw.Value.Length == 0), "Aucun tirage avant le délai.");
        CollectionAssert.AreEqual(new[] { sheet }, draws["2027-02-03"]);
    }

    [TestMethod]
    [DataRow("2026-12-31", "2027-02-28", DisplayName = "T3 : la fin d'un mois plus court")]
    [DataRow("2027-12-31", "2028-02-29", DisplayName = "T3 : une année bissextile")]
    public void T3_the_rest_ends_on_the_last_day_of_a_shorter_month(string archivedOn, string eligibleOn)
    {
        using var trial = new Trial();
        var archived = DateOnly.Parse(archivedOn, CultureInfo.InvariantCulture);
        var sheet = trial.On(archived, app =>
        {
            var preferences = app.Settings.Get().Value;
            Assert.IsTrue(app.Settings.Change(preferences with { IncludeManualArchiving = true }).Succeeded);
            var id = NewSheet(app, Title);
            Succeed(app.Reviews.Archive(id));
            return id;
        });

        Assert.AreEqual(eligibleOn, trial.SheetTracking(sheet).GetProperty("archive").GetProperty("eligibleLe").GetString());
    }

    [TestMethod]
    public void T4_a_known_sheet_stays_archived_and_rests_longer()
    {
        using var trial = new Trial();
        var sheet = ArchivedSheet(trial, Title);
        using (var running = trial.Launch(Reminder))
        {
            running.Open(Reminders, Title);
            running.Back();
        }
        var untouched = trial.SheetFile(sheet);
        Assert.AreEqual(1, trial.SheetTracking(sheet).GetProperty("compteurs").GetProperty("cyclesCommences").GetInt32());
        Assert.IsFalse(Answers(trial.SheetTracking(sheet)).Any(), "Ouvrir et refermer ne répond pas.");

        using (var running = trial.Launch(Reminder))
        {
            Assert.AreEqual(untouched, trial.SheetFile(sheet));
            running.Open(Reminders, Title);
            running.AnswerKnown();
        }

        var tracking = trial.SheetTracking(sheet);
        var archive = tracking.GetProperty("archive");
        Assert.AreEqual("archivee", tracking.GetProperty("statut").GetString());
        Assert.AreEqual(1, archive.GetProperty("niveauRepos").GetInt32());
        Assert.AreEqual("2027-06-08", archive.GetProperty("eligibleLe").GetString());
        Assert.AreEqual(1, tracking.GetProperty("compteurs").GetProperty("cyclesCommences").GetInt32());
        Assert.AreEqual("connue", Answers(tracking).Single().GetProperty("resultat").GetString());
    }

    [TestMethod]
    public void T5_a_marked_passage_restarts_alone_and_the_sheet_stays_archived()
    {
        using var trial = new Trial();
        var (sheet, passage) = ArchivedSheetWithPassage(trial, Title);
        using (var running = trial.Launch(Reminder))
        {
            running.Open(Reminders, Title);
            running.StartMarking();
            running.MarkPassage("trois ordres");
            running.FinishMarking();
            running.WaitAnswered();
        }

        var tracking = trial.SheetTracking(sheet);
        Assert.AreEqual("archivee", tracking.GetProperty("statut").GetString());
        Assert.AreEqual("2027-04-08", tracking.GetProperty("archive").GetProperty("eligibleLe").GetString());
        Assert.AreEqual("certains_points_a_revoir", Answers(tracking).Single().GetProperty("resultat").GetString());
        var cycle = trial.AnnotationTracking(sheet, passage).GetProperty("cycle");
        Assert.AreEqual("2027-02-08", cycle.GetProperty("demarreLe").GetString());
        Assert.AreEqual("2027-02-09", cycle.GetProperty("echeance").GetString());

        var sheetBefore = trial.SheetFile(sheet);
        using (var running = trial.Launch(new DateOnly(2027, 2, 9)))
        {
            Assert.Contains("J1", running.Row(Today, Title) ?? "");
            running.Open(Today, Title);
            running.Validate();
        }
        Assert.AreEqual(sheetBefore, trial.SheetFile(sheet), "Valider le passage ne touche pas au suivi de la fiche.");
        Assert.AreEqual("2027-02-12", trial.AnnotationTracking(sheet, passage).GetProperty("cycle").GetProperty("echeance").GetString());
    }

    [TestMethod]
    public void T5_a_new_passage_highlighted_during_a_reminder_counts_as_marked()
    {
        using var trial = new Trial();
        var (sheet, _) = ArchivedSheetWithPassage(trial, Title);
        using (var running = trial.Launch(Reminder))
        {
            running.Open(Reminders, Title);
            running.StartMarking();
            running.HighlightInText("le clergé");
            running.WaitMarked("1");
            running.FinishMarking();
            running.WaitAnswered();
        }

        var tracking = trial.SheetTracking(sheet);
        Assert.AreEqual("archivee", tracking.GetProperty("statut").GetString());
        Assert.AreEqual("certains_points_a_revoir", Answers(tracking).Single().GetProperty("resultat").GetString());
        var created = trial.Annotations(sheet).Single(annotation => annotation.GetProperty("ancre").GetProperty("exact").GetString() == "le clergé");
        Assert.AreEqual("2027-02-09", created.GetProperty("cycle").GetProperty("echeance").GetString());
    }

    [TestMethod]
    public void T5_finishing_with_nothing_marked_does_not_record_some_points()
    {
        using var trial = new Trial();
        var (sheet, _) = ArchivedSheetWithPassage(trial, Title);
        using var running = trial.Launch(Reminder);
        running.Open(Reminders, Title);
        running.StartMarking();
        running.FinishMarking();
        running.Dismiss();
        Assert.IsFalse(Answers(trial.SheetTracking(sheet)).Any(), "Revenir au marquage n'enregistre rien.");

        running.FinishMarking();
        running.Confirm();
        running.WaitAnswered();

        Assert.AreEqual("connue", Answers(trial.SheetTracking(sheet)).Single().GetProperty("resultat").GetString());
    }

    [TestMethod]
    public void T6_reviewing_entirely_starts_a_second_cycle_and_keeps_the_history()
    {
        using var trial = new Trial();
        var (sheet, _) = ArchivedSheetWithPassage(trial, Title);
        var reviewsBefore = Reviews(trial.SheetTracking(sheet)).Select(review => review.GetRawText()).ToArray();
        Assert.HasCount(6, reviewsBefore);
        var annotationsBefore = trial.AnnotationsFile(sheet);

        using (var running = trial.Launch(Reminder))
        {
            running.Open(Reminders, Title);
            running.AnswerReviewEntirely();
        }

        var tracking = trial.SheetTracking(sheet);
        Assert.AreEqual("active", tracking.GetProperty("statut").GetString());
        var cycle = tracking.GetProperty("cycle");
        Assert.AreEqual(2, cycle.GetProperty("numero").GetInt32());
        Assert.AreEqual("2027-02-08", cycle.GetProperty("demarreLe").GetString());
        Assert.AreEqual("2027-02-09", cycle.GetProperty("echeance").GetString());
        var counters = tracking.GetProperty("compteurs");
        Assert.AreEqual(2, counters.GetProperty("cyclesCommences").GetInt32());
        Assert.AreEqual(1, counters.GetProperty("cyclesTermines").GetInt32());
        var reviews = Reviews(tracking).ToArray();
        CollectionAssert.AreEqual(reviewsBefore, reviews.Take(6).Select(review => review.GetRawText()).ToArray(), "L'historique reste intact.");
        Assert.AreEqual("J0", reviews[6].GetProperty("etape").GetString());
        Assert.AreEqual("rappel", reviews[6].GetProperty("origine").GetString());
        Assert.AreEqual(2, reviews[6].GetProperty("cycle").GetInt32());
        Assert.AreEqual(annotationsBefore, trial.AnnotationsFile(sheet), "Les passages gardent leur propre cycle.");
    }

    [TestMethod]
    public void A_sheet_reads_as_a_document_and_no_longer_as_markdown()
    {
        using var trial = new Trial();
        var created = new DateOnly(2026, 10, 9);
        trial.On(created, app => NewSheet(app, Title, "# " + Title + "\n\n## Les causes\n\nLe **tiers état** et *rien* d'autre.\n\n- un\n- deux\n"));

        using var running = trial.Launch(created.AddDays(1));
        running.Open(Today, Title);
        var text = running.SheetText();

        Assert.Contains("Les causes", text);
        Assert.Contains("Le tiers état et rien d'autre.", text);
        Assert.DoesNotContain("**", text);
        Assert.DoesNotContain("## ", text);
        Assert.AreEqual(text.IndexOf(Title, StringComparison.Ordinal), text.LastIndexOf(Title, StringComparison.Ordinal), "Le titre répété n'est montré qu'une fois.");
    }

    [TestMethod]
    public void T9_reading_a_sheet_changes_nothing()
    {
        using var trial = new Trial();
        var created = new DateOnly(2026, 10, 9);
        var longBody = "# " + Title + "\n\n" + string.Join("\n\n", Enumerable.Range(1, 120).Select(line => string.Create(CultureInfo.InvariantCulture, $"Paragraphe {line}.")));
        var sheet = trial.On(created, app => NewSheet(app, Title, longBody));
        var before = trial.SheetFile(sheet);

        using (var running = trial.Launch(created.AddDays(1)))
        {
            running.Open(Today, Title);
            var back = running.BackPosition();
            Assert.AreEqual(100, running.ScrollToEnd(), 0.01);
            Assert.AreEqual(back, running.BackPosition(), "Le retour reste accessible en bas de la fiche.");
            running.Back();
            Assert.IsNotNull(running.Row(Today, Title), "Lire n'est pas réviser.");
        }

        Assert.AreEqual(before, trial.SheetFile(sheet));
    }

    [TestMethod]
    public void T10_a_late_review_moves_the_following_steps()
    {
        using var trial = new Trial();
        var sheet = trial.Living(Day(10, 9), (app, clock) =>
        {
            var id = NewSheet(app, Title);
            clock.Day = Day(10, 10);
            Succeed(app.Reviews.Validate(ItemKind.Sheet, id));
            return id;
        });
        foreach (var (day, next) in new[] { (Day(10, 18), "2026-10-25"), (Day(10, 25), "2026-11-08"), (Day(11, 8), "2026-12-08") })
        {
            using var running = trial.Launch(day);
            running.Open(day == Day(10, 18) ? Overdue : Today, Title);
            running.Validate();
            Assert.AreEqual(next, Due(trial, sheet));
        }
    }

    [TestMethod]
    public void T10_a_postponed_review_counts_from_the_day_it_is_done()
    {
        using var trial = new Trial();
        var sheet = trial.Living(Day(10, 9), (app, clock) =>
        {
            var id = NewSheet(app, Title);
            clock.Day = Day(10, 10);
            Succeed(app.Reviews.Validate(ItemKind.Sheet, id));
            return id;
        });
        using (var running = trial.Launch(Day(10, 13)))
        {
            running.Open(Today, Title);
            running.PostponeUntilTomorrow();
        }
        Assert.AreEqual("2026-10-14", Due(trial, sheet));

        using (var running = trial.Launch(Day(10, 14)))
        {
            running.Open(Today, Title);
            running.Validate();
        }
        Assert.AreEqual("2026-10-21", Due(trial, sheet));
    }

    [TestMethod]
    public void T10_changing_the_sequence_does_not_touch_a_running_cycle()
    {
        using var trial = new Trial();
        var sheet = trial.On(Day(10, 9), app => NewSheet(app, Title));
        var before = trial.SheetTracking(sheet).GetProperty("cycle").GetProperty("sequence").GetRawText();
        trial.On(Day(10, 9), app =>
        {
            var preferences = app.Settings.Get().Value;
            return app.Settings.Change(preferences with { Steps = [2, 4, 8] }).Succeeded;
        });

        using (var running = trial.Launch(Day(10, 10)))
        {
            Assert.Contains("J1", running.Row(Today, Title) ?? "");
            running.Open(Today, Title);
            running.Validate();
        }

        Assert.AreEqual(before, trial.SheetTracking(sheet).GetProperty("cycle").GetProperty("sequence").GetRawText());
        Assert.AreEqual("2026-10-13", Due(trial, sheet));
    }

    [TestMethod]
    public void T11_a_new_sheet_and_a_new_passage_are_due_the_next_day()
    {
        using var trial = new Trial();
        trial.On(Day(10, 9), _ => true);

        using (var running = trial.Launch(Day(10, 9)))
        {
            running.OpenView("Fiches");
            running.CreateSheet(Title);
            running.Back();
            Assert.Contains("J1", running.WaitSheetRow(Title));
            running.OpenView("Aujourd'hui");
            running.WaitRow(Upcoming, Title);
            Assert.IsNull(running.Row(Today, Title), "Rien à réviser le jour de la création.");
        }
        var sheet = trial.On(Day(10, 9), app => app.Sheets.List().Value.Single().Id);
        var first = Reviews(trial.SheetTracking(sheet)).Single();
        Assert.AreEqual("J0", first.GetProperty("etape").GetString());
        Assert.AreEqual("creation", first.GetProperty("origine").GetString());
        Assert.AreEqual("2026-10-10", Due(trial, sheet));

        using (var running = trial.Launch(Day(10, 10)))
        {
            Assert.Contains("J1", running.Row(Today, Title) ?? "");
        }

        // Écrire le texte et surligner n'existent pas encore dans l'application.
        var passage = trial.On(Day(10, 14), app =>
        {
            Assert.IsTrue(app.Editing.Save(sheet, Body, app.Sheets.Open(sheet).Value.Fingerprint, 1).Succeeded);
            return NewPassage(app, sheet, "trois ordres");
        });
        Assert.AreEqual("2026-10-15", trial.AnnotationTracking(sheet, passage).GetProperty("cycle").GetProperty("echeance").GetString());
        using (var running = trial.Launch(Day(10, 15)))
        {
            Assert.Contains("J1", running.Rows(Today).Single(row => row.StartsWith("Passage", StringComparison.Ordinal)) ?? "");
        }
    }

    [TestMethod]
    public void The_sheets_view_finds_and_opens_a_sheet_without_changing_it()
    {
        using var trial = new Trial();
        var sheet = trial.On(Day(10, 9), app =>
        {
            NewSheet(app, "Mitose");
            return NewSheet(app, Title);
        });
        var before = trial.SheetFile(sheet);

        using (var running = trial.Launch(Day(10, 11)))
        {
            running.OpenView("Fiches");
            Assert.Contains("1 j de retard", running.WaitSheetRow(Title));
            running.WaitSheetRow("Mitose");

            running.Search("revolution");
            running.WaitWithoutSheetRow("Mitose");
            running.OpenSheetRow(Title);
            running.Back();
            running.WaitSheetRow(Title);
        }

        Assert.AreEqual(before, trial.SheetFile(sheet), "Consulter une fiche ne la révise pas.");
    }

    [TestMethod]
    public void T18_with_a_hundred_eligible_sheets_the_draw_is_limited_prioritised_and_stable()
    {
        using var trial = new Trial();
        // Trois fiches éligibles depuis deux mois, donc prioritaires, parmi cent ; deux places par jour.
        var drawn = new DateOnly(2027, 2, 3);
        trial.Living(new DateOnly(2026, 10, 1), (app, clock) =>
        {
            var preferences = app.Settings.Get().Value;
            Assert.IsTrue(app.Settings.Change(preferences with { MaxPerDay = 2, MaxWaitDays = 30, IncludeManualArchiving = true }).Succeeded);
            foreach (var rank in Enumerable.Range(1, 3))
            {
                Succeed(app.Reviews.Archive(NewSheet(app, string.Create(CultureInfo.InvariantCulture, $"Ancienne {rank}"))));
            }
            clock.Day = new DateOnly(2026, 12, 1);
            foreach (var rank in Enumerable.Range(1, 97))
            {
                Succeed(app.Reviews.Archive(NewSheet(app, string.Create(CultureInfo.InvariantCulture, $"Récente {rank}"))));
            }
            return true;
        });

        IReadOnlyList<string> rows;
        using (var running = trial.Launch(drawn))
        {
            rows = running.Rows(Reminders);
            Assert.HasCount(2, rows, "Deux rappels au plus.");
            Assert.IsTrue(rows.All(row => row.Contains("Ancienne", StringComparison.Ordinal)), string.Join(" | ", rows));
            Assert.Contains("1", running.PriorityWarning() ?? "");
        }
        var draw = trial.Draws()[drawn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)];
        var journal = trial.DrawsFile();

        using (var running = trial.Launch(drawn))
        {
            CollectionAssert.AreEqual(rows.ToArray(), running.Rows(Reminders).ToArray(), "Le tirage du jour est le même à chaque réouverture.");
        }
        Assert.AreEqual(journal, trial.DrawsFile(), "Rouvrir ne tire pas de nouveau.");
        Assert.HasCount(2, draw);
    }

    private static DateOnly Day(int month, int day) => new(2026, month, day);

    private static void Succeed<T>(Outcome<T> outcome) => Assert.IsTrue(outcome.Succeeded, outcome.Error);

    private static string Due(Trial trial, string sheet) => trial.SheetTracking(sheet).GetProperty("cycle").GetProperty("echeance").GetString()!;

    private static IEnumerable<JsonElement> Reviews(JsonElement tracking) =>
        tracking.GetProperty("historique").EnumerateArray().Where(entry => entry.GetProperty("evt").GetString() == "revision");

    private static IEnumerable<JsonElement> Answers(JsonElement tracking) =>
        tracking.GetProperty("historique").EnumerateArray().Where(entry => entry.GetProperty("evt").GetString() == "rappel");

    private static string NewSheet(KyfRevApplication app, string title, string body = Body)
    {
        var id = app.Sheets.Create("Histoire", title, null).Value;
        var opened = app.Sheets.Open(id).Value;
        Assert.IsTrue(app.Editing.Save(id, body, opened.Fingerprint, 1).Succeeded);
        return id;
    }

    private static string NewPassage(KyfRevApplication app, string sheet, string exact)
    {
        // Comme dans l'application : sur le texte affiché. Une ancre juste n'est plus réécrite à l'ouverture.
        var opened = app.Sheets.Open(sheet).Value;
        var map = TextMap.Of(SheetRenderer.Render(opened.Body, opened.Title));
        var start = map.Text.IndexOf(exact, StringComparison.Ordinal);
        var anchor = Anchoring.AnchorOf(map.Text, new TextPosition(start, start + exact.Length), map.SectionAt(start), map.SourceLineAt(start, opened.FirstBodyLine), opened.Fingerprint);
        return app.Annotations.Create(sheet, anchor, null).Value;
    }

    // Les dates d'un premier cycle fait à temps : J1, J3, J7, J14 et J30, chacun compté depuis le précédent.
    private static void LiveFirstCycle(KyfRevApplication app, ScenarioClock clock, string sheet, DateOnly created)
    {
        foreach (var offset in new[] { 1, 4, 11, 25, 55 })
        {
            clock.Day = created.AddDays(offset);
            Succeed(app.Reviews.Validate(ItemKind.Sheet, sheet));
        }
    }

    // Une fiche archivée le 8 décembre 2026, éligible à un rappel le 8 février 2027.
    private static string ArchivedSheet(Trial trial, string title) =>
        trial.Living(ArchivedForReminder.AddDays(-55), (app, clock) =>
        {
            var id = NewSheet(app, title);
            LiveFirstCycle(app, clock, id, ArchivedForReminder.AddDays(-55));
            return id;
        });

    private static (string Sheet, string Passage) ArchivedSheetWithPassage(Trial trial, string title) =>
        trial.Living(ArchivedForReminder.AddDays(-55), (app, clock) =>
        {
            var id = NewSheet(app, title);
            var passage = NewPassage(app, id, "trois ordres");
            LiveFirstCycle(app, clock, id, ArchivedForReminder.AddDays(-55));
            return (id, passage);
        });
}
