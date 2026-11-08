using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// Les examens sur « Aujourd'hui » : le compte à rebours, en fixer un, le modifier, le retirer, ramener les échéances avant
/// lui ; à la plus petite largeur, et au clavier.
/// </summary>
[TestClass]
public sealed class ExamsTests
{
    private static readonly DateOnly October1 = new(2026, 10, 1);

    [TestMethod]
    [DataRow(14, "J-14", ExamUrgency.Normal, true)]
    [DataRow(8, "J-8", ExamUrgency.Normal, true)]
    [DataRow(7, "J-7", ExamUrgency.Soon, true)]
    [DataRow(2, "J-2", ExamUrgency.Soon, true)]
    [DataRow(1, "demain", ExamUrgency.Soon, false)]
    [DataRow(0, "aujourd'hui", ExamUrgency.Soon, false)]
    [DataRow(-1, "passé depuis 1 jour", ExamUrgency.Past, false)]
    [DataRow(-3, "passé depuis 3 jours", ExamUrgency.Past, false)]
    public void The_countdown_is_written_as_in_the_0x(int daysLeft, string countdown, ExamUrgency urgency, bool canTighten)
    {
        var row = new ExamRow(new UpcomingExam("Biologie/Cellule", "Partiel", October1.AddDays(daysLeft), daysLeft, 5, 2));

        Assert.AreEqual(countdown, row.Countdown);
        Assert.AreEqual(urgency, row.Urgency);
        Assert.AreEqual(canTighten, row.CanTighten);
        Assert.AreEqual(daysLeft > 0, row.HasAlert);
    }

    [TestMethod]
    public void The_detail_says_where_how_many_and_what_falls_after()
    {
        var row = new ExamRow(new UpcomingExam("Biologie/Cellule", "Partiel", new DateOnly(2026, 11, 2), 32, 5, 2));

        Assert.AreEqual("Biologie › Cellule · 5 éléments actifs, dont ", row.Detail);
        Assert.AreEqual("2 prévus après l'examen", row.AfterExam);
        Assert.AreEqual("lun. 2 nov.", row.DayLabel);
        Assert.AreEqual("Examen Partiel, J-32, lun. 2 nov., Biologie › Cellule · 5 éléments actifs, dont 2 prévus après l'examen", row.ToString());
        Assert.AreEqual("Resserrer avant l'examen Partiel", row.TightenName);
        Assert.AreEqual("Autres actions de l'examen Partiel", row.MoreName);

        var whole = new ExamRow(new UpcomingExam(string.Empty, "Bac", new DateOnly(2026, 11, 2), 32, 1, 1));
        Assert.AreEqual("Tout le coffre · 1 élément actif, dont ", whole.Detail);
        Assert.AreEqual("1 prévu après l'examen", whole.AfterExam);

        var none = new ExamRow(new UpcomingExam(string.Empty, "Bac", new DateOnly(2026, 11, 2), 32, 0, 0));
        Assert.IsFalse(none.HasAlert);
        Assert.IsFalse(none.CanTighten);

        var arrived = new ExamRow(new UpcomingExam(string.Empty, "Bac", October1, 0, 3, 1));
        Assert.AreEqual("Tout le coffre · 3 éléments actifs", arrived.Detail);
        Assert.AreEqual(string.Empty, arrived.AfterExam);
    }

    [TestMethod]
    public async Task The_exams_come_first_on_today_and_count_down()
    {
        using var vault = new ExamVault();
        Assert.IsTrue(vault.App.Exams.Define("Biologie", "Partiel", new DateOnly(2026, 10, 15)).Succeeded);
        var page = vault.Page();

        await page.RefreshCommand.ExecuteAsync(null);

        Assert.AreEqual("Examens, 1", page.Rows[0].ToString());
        var row = (ExamRow)page.Rows[1];
        Assert.AreEqual("J-14", row.Countdown);
        Assert.AreEqual("Biologie · 1 élément actif, dont ", row.Detail);
        Assert.AreEqual("0 prévu après l'examen", row.AfterExam);
        Assert.IsFalse(page.HasError);
        Assert.IsTrue(page.CanPlanExams);

        var group = (DashboardGroup)page.Rows[0];
        group.ToggleCommand.Execute(null);
        Assert.IsFalse(page.Rows.OfType<ExamRow>().Any(), "un groupe qui se replie");
    }

    [TestMethod]
    public async Task Without_their_use_cases_the_exams_are_only_counted_down()
    {
        using var vault = new ExamVault();
        Assert.IsTrue(vault.App.Exams.Define(string.Empty, "Bac", new DateOnly(2026, 10, 15)).Succeeded);
        var page = new DashboardPageViewModel(vault.App.Reminders, vault.App.Sheets, new FakeOpener());

        await page.RefreshCommand.ExecuteAsync(null);

        Assert.IsNull(page.Exams);
        Assert.IsFalse(page.CanPlanExams);
        Assert.HasCount(1, page.Rows.OfType<ExamRow>().ToList());
        page.Forget();
        Assert.IsEmpty(page.Rows);
    }

    [TestMethod]
    public async Task Fixing_an_exam_asks_a_name_and_a_day()
    {
        using var vault = new ExamVault();
        var page = vault.Page();
        await page.RefreshCommand.ExecuteAsync(null);
        var exams = page.Exams!;

        await exams.NewCommand.ExecuteAsync(null);

        Assert.IsTrue(exams.IsEditing);
        Assert.AreEqual(ExamText.EditorNew, exams.EditorTitle);
        Assert.AreEqual(string.Empty, exams.ExamName);
        Assert.AreEqual(October1, exams.Earliest);
        Assert.AreEqual(new DateOnly(2026, 10, 15), exams.Day, "quatorze jours plus tard");
        Assert.AreEqual(ExamText.WholeVault, exams.Folder!.Label);
        Assert.AreEqual(ExamText.WholeVault + " | Biologie | Biologie › Cellule", string.Join(" | ", exams.Folders.Select(folder => folder.Label)));

        await exams.AnswerEditorCommand.ExecuteAsync(true);
        Assert.AreEqual(ExamText.NameRequired, exams.EditorError);
        Assert.IsTrue(exams.HasEditorError);
        Assert.IsTrue(exams.IsEditing, "le refus laisse le formulaire ouvert");

        exams.ExamName = "  Bac blanc ";
        exams.Day = null;
        await exams.AnswerEditorCommand.ExecuteAsync(true);
        Assert.AreEqual(ExamText.DayRequired, exams.EditorError);
        Assert.IsTrue(exams.IsEditing);

        exams.Day = new DateOnly(2026, 10, 20);
        exams.Folder = exams.Folders[1];
        await exams.AnswerEditorCommand.ExecuteAsync(true);

        Assert.IsFalse(exams.IsEditing);
        Assert.IsNull(exams.EditorError);
        var row = page.Rows.OfType<ExamRow>().Single();
        Assert.AreEqual("Bac blanc", row.Name);
        Assert.AreEqual("Biologie", row.Exam.Folder);
        Assert.AreEqual(new DateOnly(2026, 10, 20), row.Exam.Day);
    }

    [TestMethod]
    public async Task The_form_starts_from_today_even_before_the_dashboard_is_read()
    {
        using var vault = new ExamVault();
        var exams = vault.Page().Exams!;

        await exams.NewCommand.ExecuteAsync(null);

        Assert.AreEqual(October1, exams.Earliest, "aujourd'hui, pas l'an 1");
        Assert.AreEqual(new DateOnly(2026, 10, 15), exams.Day);
    }

    [TestMethod]
    public async Task A_failed_action_leaves_no_old_success_beside_it()
    {
        using var vault = new ExamVault();
        Assert.IsTrue(vault.App.Exams.Define("Biologie", "Partiel", new DateOnly(2026, 10, 15)).Succeeded);
        var page = vault.Page();
        await page.RefreshCommand.ExecuteAsync(null);
        var exams = page.Exams!;
        var row = page.Rows.OfType<ExamRow>().Single();
        exams.RequestWithdraw(row);
        await exams.AnswerCommand.ExecuteAsync(true);
        Assert.IsTrue(exams.HasMessage);

        // L'examen n'est plus là : resserrer est refusé.
        exams.RequestTighten(row);
        await exams.AnswerCommand.ExecuteAsync(true);

        Assert.IsTrue(exams.HasError);
        Assert.IsFalse(exams.HasMessage);
    }

    [STATestMethod]
    public void Nothing_starts_on_the_exams_while_an_action_runs()
    {
        using var ui = new UiThread();
        using var vault = new ExamVault();
        Assert.IsTrue(vault.App.Reviews.Postpone(ItemKind.Sheet, vault.SheetId, new DateOnly(2026, 11, 20)).Succeeded);
        Assert.IsTrue(vault.App.Exams.Define("Biologie", "Partiel", new DateOnly(2026, 10, 15)).Succeeded);
        var page = vault.Page();
        var test = new TestShell();
        var window = Show(test, page, 1200, 800);
        try
        {
            page.RefreshCommand.Execute(null);
            UiThread.Until(() => page.Dashboard is not null);
            TestShell.Flush();
            var exams = page.Exams!;
            var row = page.Rows.OfType<ExamRow>().Single();
            var list = Named<ListBox>(window, "Items");
            var container = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(row);
            var tighten = Descendants(container).OfType<Button>().First(button => button.Name == "Tighten");
            var more = Descendants(container).OfType<Button>().First(button => button.Name == "More");
            var view = Descendants(window).OfType<DashboardPage>().Single();
            Assert.IsTrue(exams.IsIdle);
            Assert.IsTrue(tighten.IsEnabled);
            Assert.IsNotEmpty(view.RowMenuItems(row));

            exams.RequestTighten(row);
            var answer = exams.AnswerCommand.ExecuteAsync(true);

            Assert.IsTrue(exams.IsBusy);
            Assert.IsFalse(exams.IsIdle);
            Assert.IsFalse(exams.HasMessage, "le message d'avant se ferme : le suivant sera annoncé");
            Assert.IsFalse(tighten.IsEnabled);
            Assert.IsFalse(more.IsEnabled);
            Assert.IsFalse(exams.NewCommand.CanExecute(null));
            Assert.IsFalse(Named<Button>(window, "NewExamButton").IsEnabled);
            Assert.IsFalse(exams.EditCommand.CanExecute(row));
            Assert.IsEmpty(view.RowMenuItems(row), "le menu de la ligne n'a rien à proposer");
            exams.RequestTighten(row);
            Assert.IsFalse(exams.IsConfirming, "une seconde confirmation dirait « Rien à resserrer »");
            exams.RequestWithdraw(row);
            Assert.IsFalse(exams.IsConfirming);
            var opening = exams.NewCommand.ExecuteAsync(null);
            Assert.IsTrue(opening.IsCompleted);
            Assert.IsFalse(exams.IsEditing);

            UiThread.Until(() => answer.IsCompleted);
            TestShell.Flush();
            StringAssert.StartsWith(exams.Message, "1 échéance ramenée", StringComparison.Ordinal);
            Assert.IsTrue(exams.IsIdle);
            Assert.IsTrue(exams.NewCommand.CanExecute(null));
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Saving_an_exam_also_waits_its_turn()
    {
        using var ui = new UiThread();
        using var vault = new ExamVault();
        var page = vault.Page();
        var refresh = page.RefreshCommand.ExecuteAsync(null);
        UiThread.Until(() => refresh.IsCompleted);
        var exams = page.Exams!;
        var opening = exams.NewCommand.ExecuteAsync(null);
        UiThread.Until(() => opening.IsCompleted);
        exams.ExamName = "Bac";

        var saved = exams.AnswerEditorCommand.ExecuteAsync(true);

        Assert.IsTrue(exams.IsBusy);
        UiThread.Until(() => saved.IsCompleted);
        Assert.IsFalse(exams.IsBusy);
    }

    [STATestMethod]
    public void A_refused_date_keeps_the_form_open_and_saves_nothing()
    {
        using var ui = new UiThread();
        using var vault = new ExamVault();
        Assert.IsTrue(vault.App.Exams.Define("Biologie", "Partiel", new DateOnly(2026, 10, 15)).Succeeded);
        var page = vault.Page();
        var test = new TestShell();
        var window = Show(test, page, 470, 540);
        try
        {
            page.RefreshCommand.Execute(null);
            UiThread.Until(() => page.Dashboard is not null);
            TestShell.Flush();
            var exams = page.Exams!;
            exams.EditCommand.Execute(page.Rows.OfType<ExamRow>().Single());
            UiThread.Until(() => exams.IsEditing);
            TestShell.Flush();
            var field = Named<DateField>(window, "ExamDay");
            UiThread.Until(() => Named<Dialog>(window, "ExamEditor").IsKeyboardFocusWithin);
            var text = Descendants(field).OfType<DatePickerTextBox>().Single();
            Assert.IsTrue(text.Focus());

            // Entrée sur une date passée : le champ la refuse, et le bouton par défaut ne part pas sur l'ancienne.
            text.Text = "30/09/2026";
            var enter = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(text), 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent };
            text.RaiseEvent(enter);

            Assert.IsTrue(enter.Handled);
            Assert.IsTrue(exams.IsEditing);
            Assert.IsTrue(field.HasRefusedDate);
            Assert.IsNull(exams.Day);
            Assert.IsTrue(Descendants(field).OfType<LiveText>().Single(block => block.Name == "Refusal").IsVisible, "le refus se lit");

            // « Enregistrer » après un refus : la date manque, le formulaire reste ouvert, et l'examen ne change pas.
            Named<Dialog>(window, "ExamEditor").Close(DialogChoice.Primary);
            UiThread.Until(() => exams.HasEditorError);
            TestShell.Flush();
            Assert.AreEqual(ExamText.DayRequired, exams.EditorError);
            Assert.IsTrue(exams.IsEditing);
            Assert.AreEqual(new DateOnly(2026, 10, 15), vault.App.Reminders.Dashboard().Value.Exams.Single().Day);
            Named<Dialog>(window, "ExamEditor").Close(DialogChoice.None);
            UiThread.Until(() => !exams.IsEditing);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void After_a_dialog_the_focus_comes_back_to_the_exam()
    {
        using var ui = new UiThread();
        using var vault = new ExamVault();
        Assert.IsTrue(vault.App.Exams.Define("Biologie", "Partiel", new DateOnly(2026, 10, 15)).Succeeded);
        Assert.IsTrue(vault.App.Exams.Define(string.Empty, "Bac", new DateOnly(2026, 10, 20)).Succeeded);
        var page = vault.Page();
        var test = new TestShell();
        var window = Show(test, page, 1200, 800);
        try
        {
            page.RefreshCommand.Execute(null);
            UiThread.Until(() => page.Dashboard is not null);
            TestShell.Flush();
            var exams = page.Exams!;
            var list = Named<ListBox>(window, "Items");
            ListBoxItem Container(object row) => (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(row);
            ExamRow Exam(string folder) => page.Rows.OfType<ExamRow>().Single(row => row.Exam.Folder == folder);
            var editor = Named<Dialog>(window, "ExamEditor");
            var confirmation = Named<Dialog>(window, "ExamConfirmation");

            // Annuler : la ligne n'a pas été refaite.
            var biology = Exam("Biologie");
            Assert.IsTrue(Container(biology).Focus());
            exams.RequestWithdraw(biology);
            TestShell.Flush();
            UiThread.Until(() => confirmation.IsKeyboardFocusWithin);
            confirmation.Close(DialogChoice.Secondary);
            UiThread.Until(() => Container(biology).IsKeyboardFocused);

            // Enregistrer : la liste est relue, le focus revient à la ligne refaite.
            exams.EditCommand.Execute(biology);
            UiThread.Until(() => exams.IsEditing);
            TestShell.Flush();
            UiThread.Until(() => editor.IsKeyboardFocusWithin);
            editor.Close(DialogChoice.Primary);
            UiThread.Until(() => !exams.IsBusy && !ReferenceEquals(Exam("Biologie"), biology));
            TestShell.Flush();
            UiThread.Until(() => Keyboard.FocusedElement is ListBoxItem { DataContext: ExamRow { Exam.Folder: "Biologie" } });

            // Retiré : le focus va à l'en-tête du groupe.
            exams.RequestWithdraw(Exam("Biologie"));
            TestShell.Flush();
            UiThread.Until(() => confirmation.IsKeyboardFocusWithin);
            confirmation.Close(DialogChoice.Primary);
            UiThread.Until(() => !page.Rows.OfType<ExamRow>().Any(row => row.Exam.Folder == "Biologie"));
            TestShell.Flush();
            UiThread.Until(() => Keyboard.FocusedElement is ListBoxItem { DataContext: DashboardGroup });

            // Le dernier retiré : plus de groupe, le focus va au bouton qui en crée un.
            exams.RequestWithdraw(Exam(string.Empty));
            TestShell.Flush();
            UiThread.Until(() => confirmation.IsKeyboardFocusWithin);
            confirmation.Close(DialogChoice.Primary);
            UiThread.Until(() => !page.Rows.OfType<ExamRow>().Any());
            TestShell.Flush();
            UiThread.Until(() => Named<Button>(window, "NewExamButton").IsKeyboardFocused);
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public async Task Cancelling_the_form_fixes_nothing_and_forgets_its_refusal()
    {
        using var vault = new ExamVault();
        var page = vault.Page();
        await page.RefreshCommand.ExecuteAsync(null);
        var exams = page.Exams!;
        await exams.NewCommand.ExecuteAsync(null);
        await exams.AnswerEditorCommand.ExecuteAsync(true);
        Assert.IsTrue(exams.HasEditorError);

        await exams.AnswerEditorCommand.ExecuteAsync(false);

        Assert.IsFalse(exams.IsEditing);
        Assert.IsFalse(exams.HasEditorError);
        Assert.IsFalse(page.Rows.OfType<ExamRow>().Any());
    }

    [TestMethod]
    public async Task An_exam_is_modified_in_place()
    {
        using var vault = new ExamVault();
        Assert.IsTrue(vault.App.Exams.Define("Biologie", "Partiel", new DateOnly(2026, 10, 15)).Succeeded);
        var page = vault.Page();
        await page.RefreshCommand.ExecuteAsync(null);
        var exams = page.Exams!;

        await exams.EditCommand.ExecuteAsync(page.Rows.OfType<ExamRow>().Single());

        Assert.AreEqual(ExamText.EditorEdit, exams.EditorTitle);
        Assert.AreEqual("Partiel", exams.ExamName);
        Assert.AreEqual("Biologie", exams.Folder!.Value);
        Assert.AreEqual(new DateOnly(2026, 10, 15), exams.Day);

        exams.ExamName = "Partiel de janvier";
        await exams.AnswerEditorCommand.ExecuteAsync(true);

        Assert.AreEqual("Partiel de janvier", page.Rows.OfType<ExamRow>().Single().Name);
    }

    [TestMethod]
    public async Task An_exam_in_a_folder_that_is_gone_keeps_its_folder_and_a_past_one_its_day()
    {
        using var vault = new ExamVault();
        Assert.IsTrue(vault.App.Exams.Define("Disparu/Sous", "Ancien", new DateOnly(2026, 9, 28)).Succeeded);
        var page = vault.Page();
        await page.RefreshCommand.ExecuteAsync(null);
        var exams = page.Exams!;
        var row = page.Rows.OfType<ExamRow>().Single();
        Assert.AreEqual("passé depuis 3 jours", row.Countdown);

        await exams.EditCommand.ExecuteAsync(row);

        Assert.AreEqual("Disparu › Sous", exams.Folder!.Label);
        Assert.AreEqual(new DateOnly(2026, 9, 28), exams.Earliest, "sa date passée reste affichable");
        Assert.AreEqual(new DateOnly(2026, 9, 28), exams.Day);
    }

    [TestMethod]
    public async Task Tightening_brings_the_due_dates_before_the_exam_after_confirmation()
    {
        using var vault = new ExamVault();
        var page = vault.Page();
        Assert.IsTrue(vault.App.Reviews.Postpone(ItemKind.Sheet, vault.SheetId, new DateOnly(2026, 11, 20)).Succeeded);
        Assert.IsTrue(vault.App.Exams.Define("Biologie", "Partiel", new DateOnly(2026, 10, 15)).Succeeded);
        await page.RefreshCommand.ExecuteAsync(null);
        var exams = page.Exams!;
        var row = page.Rows.OfType<ExamRow>().Single();
        Assert.IsTrue(row.CanTighten);

        exams.RequestTighten(row);

        Assert.AreEqual(ExamConfirmation.Tighten, exams.Confirmation);
        Assert.IsTrue(exams.IsConfirming);
        Assert.AreEqual(ExamText.TightenTitle, exams.ConfirmTitle);
        Assert.AreEqual(ExamText.TightenConfirm, exams.ConfirmLabel);
        StringAssert.StartsWith(exams.ConfirmMessage, "1 échéance tombe après la veille de « Partiel » (jeu. 15 oct.).", StringComparison.Ordinal);

        await exams.AnswerCommand.ExecuteAsync(false);
        Assert.IsFalse(exams.IsConfirming);
        Assert.IsFalse(exams.HasMessage);
        Assert.AreEqual(new DateOnly(2026, 11, 20), page.Dashboard!.Workload.SelectMany(day => day.Due).Concat(page.Dashboard.Upcoming).FirstOrDefault()?.DueDate ?? new DateOnly(2026, 11, 20));

        exams.RequestTighten(row);
        await exams.AnswerCommand.ExecuteAsync(true);

        Assert.AreEqual("1 échéance ramenée avant « Partiel », réparties jusqu'à la veille.", exams.Message);
        Assert.IsTrue(exams.HasMessage);
        Assert.IsFalse(exams.IsBusy);
        Assert.IsFalse(page.Rows.OfType<ExamRow>().Single().CanTighten, "le tableau est relu");

        // Rien à ramener : cela se dit aussi.
        exams.RequestTighten(row);
        await exams.AnswerCommand.ExecuteAsync(true);
        Assert.AreEqual(ExamText.NothingToTighten, exams.Message);
    }

    [TestMethod]
    public async Task Tightening_several_due_dates_counts_them()
    {
        using var vault = new ExamVault();
        var page = vault.Page();
        var other = vault.App.Sheets.Create("Biologie", "Méiose", null).Value;
        Assert.IsTrue(vault.App.Reviews.Postpone(ItemKind.Sheet, vault.SheetId, new DateOnly(2026, 11, 20)).Succeeded);
        Assert.IsTrue(vault.App.Reviews.Postpone(ItemKind.Sheet, other, new DateOnly(2026, 11, 21)).Succeeded);
        Assert.IsTrue(vault.App.Exams.Define("Biologie", "Partiel", new DateOnly(2026, 10, 15)).Succeeded);
        await page.RefreshCommand.ExecuteAsync(null);
        var exams = page.Exams!;
        var row = page.Rows.OfType<ExamRow>().Single();

        exams.RequestTighten(row);
        StringAssert.StartsWith(exams.ConfirmMessage, "2 échéances tombent après", StringComparison.Ordinal);
        await exams.AnswerCommand.ExecuteAsync(true);

        Assert.AreEqual("2 échéances ramenées avant « Partiel », réparties jusqu'à la veille.", exams.Message);
    }

    [TestMethod]
    public async Task Withdrawing_is_confirmed_and_leaves_the_calendars_alone()
    {
        using var vault = new ExamVault();
        Assert.IsTrue(vault.App.Exams.Define("Biologie", "Partiel", new DateOnly(2026, 10, 15)).Succeeded);
        var page = vault.Page();
        await page.RefreshCommand.ExecuteAsync(null);
        var exams = page.Exams!;
        var due = page.Dashboard!.Upcoming.Single().DueDate;

        exams.RequestWithdraw(page.Rows.OfType<ExamRow>().Single());
        Assert.AreEqual(ExamText.WithdrawTitle, exams.ConfirmTitle);
        Assert.AreEqual(ExamText.WithdrawConfirm, exams.ConfirmLabel);
        Assert.AreEqual("« Partiel » ne sera plus affiché. Les calendriers de révision ne changent pas.", exams.ConfirmMessage);

        await exams.AnswerCommand.ExecuteAsync(true);

        Assert.AreEqual("Examen « Partiel » retiré ; les calendriers ne changent pas.", exams.Message);
        Assert.IsFalse(page.Rows.OfType<ExamRow>().Any());
        Assert.AreEqual(due, page.Dashboard!.Upcoming.Single().DueDate);
        Assert.AreEqual(string.Empty, exams.ConfirmTitle);
        Assert.AreEqual(string.Empty, exams.ConfirmMessage);
    }

    [TestMethod]
    public async Task A_refusal_is_said_and_the_message_goes_when_the_view_comes_back()
    {
        using var vault = new ExamVault();
        Assert.IsTrue(vault.App.Exams.Define("Biologie", "Partiel", new DateOnly(2026, 10, 15)).Succeeded);
        var page = vault.Page();
        await page.RefreshCommand.ExecuteAsync(null);
        var exams = page.Exams!;
        var row = page.Rows.OfType<ExamRow>().Single();

        // L'examen a disparu entre-temps : « Resserrer » est refusé.
        Assert.IsTrue(vault.App.Exams.Withdraw("Biologie").Succeeded);
        exams.RequestTighten(row);
        await exams.AnswerCommand.ExecuteAsync(true);
        Assert.IsTrue(exams.HasError);
        Assert.IsFalse(exams.IsBusy);

        exams.RequestWithdraw(row);
        await exams.AnswerCommand.ExecuteAsync(true);
        Assert.IsTrue(exams.HasMessage);
        Assert.IsFalse(exams.HasError);

        page.OnNavigatedTo();
        await page.RefreshCommand.ExecutionTask!;
        Assert.IsFalse(exams.HasMessage, "les messages de la fois d'avant s'en vont");

        await exams.AnswerCommand.ExecuteAsync(true);
        Assert.IsFalse(exams.HasMessage, "sans demande, la réponse ne fait rien");
    }

    [TestMethod]
    public async Task A_closed_vault_refuses_the_form()
    {
        using var vault = new ExamVault();
        var page = vault.Page();
        await page.RefreshCommand.ExecuteAsync(null);
        var exams = page.Exams!;
        await exams.NewCommand.ExecuteAsync(null);
        exams.ExamName = "Bac";

        vault.App.Vaults.Close();

        await exams.AnswerEditorCommand.ExecuteAsync(true);
        Assert.IsTrue(exams.HasEditorError);
        Assert.IsTrue(exams.IsEditing);

        await exams.AnswerEditorCommand.ExecuteAsync(false);
        await exams.NewCommand.ExecuteAsync(null);
        Assert.IsFalse(exams.IsEditing);
        Assert.IsTrue(exams.HasError);
    }

    [STATestMethod]
    public void Forgetting_drops_the_exams_and_an_answer_that_arrives_after_puts_nothing_back()
    {
        using var ui = new UiThread();
        using var vault = new ExamVault();
        Assert.IsTrue(vault.App.Exams.Define("Biologie", "Partiel", new DateOnly(2026, 10, 15)).Succeeded);
        var page = vault.Page();
        var refresh = page.RefreshCommand.ExecuteAsync(null);
        UiThread.Until(() => refresh.IsCompleted);
        var exams = page.Exams!;
        var row = page.Rows.OfType<ExamRow>().Single();
        var opening = exams.NewCommand.ExecuteAsync(null);
        UiThread.Until(() => opening.IsCompleted);

        exams.ExamName = "Secret";
        exams.RequestWithdraw(row);
        page.Forget();

        Assert.IsFalse(page.Rows.OfType<ExamRow>().Any());
        Assert.IsFalse(exams.IsEditing);
        Assert.AreEqual(string.Empty, exams.ExamName);
        Assert.IsEmpty(exams.Folders);
        Assert.IsNull(exams.Folder);
        Assert.IsNull(exams.Day);
        Assert.IsNull(exams.Earliest);
        Assert.IsFalse(exams.IsConfirming);
        Assert.AreEqual(string.Empty, exams.ConfirmMessage);

        // Partie avant l'oubli, une réponse n'y remet rien.
        exams.RequestWithdraw(row);
        var answer = exams.AnswerCommand.ExecuteAsync(true);
        page.Forget();
        UiThread.Until(() => answer.IsCompleted);
        Assert.IsFalse(exams.HasMessage);
        Assert.IsFalse(exams.IsBusy);

        var reopened = exams.NewCommand.ExecuteAsync(null);
        page.Forget();
        UiThread.Until(() => reopened.IsCompleted);
        Assert.IsFalse(exams.IsEditing, "le formulaire ne s'ouvre pas sur le coffre oublié");

        var name = exams.NewCommand.ExecuteAsync(null);
        UiThread.Until(() => name.IsCompleted);
        exams.ExamName = "Bac";
        exams.Day = new DateOnly(2026, 11, 1);
        var saved = exams.AnswerEditorCommand.ExecuteAsync(true);
        page.Forget();
        UiThread.Until(() => saved.IsCompleted);
        Assert.IsFalse(exams.IsEditing);
        Assert.IsFalse(exams.HasEditorError);
    }

    [STATestMethod]
    public void The_row_and_the_dialogs_fit_the_smallest_window_and_answer_the_keyboard()
    {
        using var ui = new UiThread();
        using var vault = new ExamVault();
        Assert.IsTrue(vault.App.Reviews.Postpone(ItemKind.Sheet, vault.SheetId, new DateOnly(2026, 11, 20)).Succeeded);
        Assert.IsTrue(vault.App.Exams.Define("Biologie", "Un examen au nom vraiment très long pour la largeur de la fenêtre", new DateOnly(2026, 10, 5)).Succeeded);
        var page = vault.Page();
        var test = new TestShell();
        var window = Show(test, page, 470, 540);
        try
        {
            page.RefreshCommand.Execute(null);
            UiThread.Until(() => page.Dashboard is not null);
            TestShell.Flush();
            var list = Named<ListBox>(window, "Items");
            var row = page.Rows.OfType<ExamRow>().Single();
            var container = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(row);

            // Rien ne dépasse, et le bouton garde sa place.
            var tighten = Descendants(container).OfType<Button>().First(button => button.Name == "Tighten");
            Assert.AreEqual(row.TightenName, AutomationProperties.GetName(tighten));
            Assert.AreEqual(0, Descendants(list).OfType<ScrollViewer>().First().ScrollableWidth);
            Assert.IsLessThanOrEqualTo(window.ActualWidth, tighten.TranslatePoint(new Point(tighten.ActualWidth, 0), window).X);
            var layout = Descendants(container).OfType<AdaptiveRow>().Single();
            var text = (FrameworkElement)Descendants(layout).OfType<TextBlock>().First().Parent;
            Assert.IsTrue(layout.IsStacked || text.ActualWidth >= layout.MainMinWidth, "le nom garde sa place");
            Assert.AreEqual(Visibility.Collapsed, Descendants(Named<Button>(window, "NewExamButton")).OfType<TextBlock>().First(label => label.Text == ExamText.NewExam).Visibility, "à l'étroit, l'icône suffit");
            Assert.AreEqual(ExamText.NewExamName, AutomationProperties.GetName(Named<Button>(window, "NewExamButton")));

            // Dans la semaine, le compte à rebours passe en ambre, lisible sur son fond.
            var countdown = Descendants(container).OfType<Border>().First(border => border.Name == "Countdown");
            Assert.AreEqual(test.Design.Dictionary["Kyf.Brush.WarningBackground"], countdown.Background);

            // La ligne choisie : tout son texte se lit sur son fond.
            list.SelectedItem = row;
            TestShell.Flush();
            var detail = Descendants(container).OfType<TextBlock>().First(block => block.Name == "ExamDetail");
            Assert.AreEqual(test.Design.Dictionary["Kyf.Brush.NavigationActiveText"], detail.Foreground);
            list.SelectedItem = null;
            TestShell.Flush();
            Assert.AreEqual(test.Design.Dictionary["Kyf.Brush.TextSecondary"], detail.Foreground);

            // Les boutons de la ligne.
            tighten.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.IsTrue(page.Exams!.IsConfirming);
            TestShell.Flush();
            var confirmation = Named<Dialog>(window, "ExamConfirmation");
            Assert.IsTrue(confirmation.IsOpen);
            Assert.AreEqual(ExamText.TightenTitle, confirmation.Title);
            confirmation.Close(DialogChoice.Secondary);
            UiThread.Until(() => !page.Exams.IsConfirming);

            var more = Descendants(container).OfType<Button>().First(button => button.Name == "More");
            Assert.AreEqual(row.MoreName, AutomationProperties.GetName(more));
            more.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            var view = Descendants(window).OfType<DashboardPage>().Single();
            var menu = (ContextMenu)typeof(DashboardPage).GetField("_examMenu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(view)!;
            Assert.IsTrue(menu.IsOpen);
            Assert.AreSame(more, menu.PlacementTarget);
            Assert.AreEqual(PlacementMode.Bottom, menu.Placement);
            menu.IsOpen = false;

            // Au clavier : Entrée sur la ligne ouvre le même menu.
            Assert.IsTrue(container.Focus());
            Press(container, Key.Enter);
            Assert.IsTrue(menu.IsOpen);
            Assert.AreSame(container, menu.PlacementTarget);
            menu.IsOpen = false;

            // Le menu de la ligne : ce qu'on peut faire de cet examen.
            var entries = view.RowMenuItems(row);
            Same(ExamText.TightenAction + " | " + ExamText.EditAction + " | " + ExamText.WithdrawAction, entries.OfType<MenuItem>().Select(item => (string)item.Header));
            Click(entries[2]);
            Assert.AreEqual(ExamConfirmation.Withdraw, page.Exams.Confirmation);
            page.Exams.AnswerCommand.Execute(false);
            Click(entries[0]);
            Assert.AreEqual(ExamConfirmation.Tighten, page.Exams.Confirmation);
            page.Exams.AnswerCommand.Execute(false);
            Click(entries[1]);
            UiThread.Until(() => page.Exams.IsEditing);
            page.Exams.AnswerEditorCommand.Execute(false);
            UiThread.Until(() => !page.Exams.IsEditing);
            var calm = new ExamRow(new UpcomingExam(string.Empty, "Bac", October1, 0, 1, 0));
            Same(ExamText.EditAction + " | " + ExamText.WithdrawAction, view.RowMenuItems(calm).OfType<MenuItem>().Select(item => (string)item.Header));

            // Le formulaire : chaque champ est nommé, et tient dans la fenêtre.
            page.Exams.NewCommand.Execute(null);
            UiThread.Until(() => page.Exams.IsEditing);
            TestShell.Flush();
            var editor = Named<Dialog>(window, "ExamEditor");
            Assert.IsTrue(editor.IsOpen);
            var field = Named<DateField>(window, "ExamDay");
            Assert.AreEqual(ExamText.DateLabel, AutomationProperties.GetName(field));
            Assert.AreEqual(new DateOnly(2026, 10, 15), field.Day);
            Assert.AreEqual(October1, field.Minimum);
            Assert.AreEqual(ExamText.NameLabel, AutomationProperties.GetName(Named<TextBox>(window, "ExamNameBox")));
            Assert.AreEqual(ExamText.FolderLabel, AutomationProperties.GetName(Named<ComboBox>(window, "ExamFolderChoice")));
            Assert.IsLessThanOrEqualTo(window.ActualWidth, field.TranslatePoint(new Point(field.ActualWidth, 0), window).X);

            editor.Close(DialogChoice.Primary);
            UiThread.Until(() => page.Exams.HasEditorError);
            TestShell.Flush();
            Assert.IsTrue(editor.IsOpen, "le refus laisse le formulaire ouvert");
            Assert.IsTrue(Named<Notification>(window, "ExamEditorError").IsOpen);
            editor.Close(DialogChoice.None);
            UiThread.Until(() => !page.Exams.IsEditing);

            // « Charge » ouvre la vue à venir, avec le filtre en cours.
            page.SelectedFilter = page.Filters[2];
            Named<Button>(window, "WorkloadButton").Command.Execute(null);
            CollectionAssert.Contains(vault.Opener.Opened, ("charge", DashboardFilter.Passages));
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void At_ease_the_row_keeps_its_buttons_beside_the_text()
    {
        using var ui = new UiThread();
        using var vault = new ExamVault();
        Assert.IsTrue(vault.App.Exams.Define("Biologie", "Partiel", new DateOnly(2026, 10, 15)).Succeeded);
        var page = vault.Page();
        var test = new TestShell(dark: true);
        test.Design.Theme = KyfRev.DesignSystem.Tokens.ThemeKind.Dark;
        var window = Show(test, page, 1200, 800);
        try
        {
            page.RefreshCommand.Execute(null);
            UiThread.Until(() => page.Dashboard is not null);
            TestShell.Flush();
            var list = Named<ListBox>(window, "Items");
            var row = page.Rows.OfType<ExamRow>().Single();
            var container = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(row);

            Assert.IsFalse(Descendants(container).OfType<AdaptiveRow>().Single().IsStacked);
            Assert.AreEqual(Visibility.Visible, Descendants(Named<Button>(window, "NewExamButton")).OfType<TextBlock>().First(label => label.Text == ExamText.NewExam).Visibility);
            var text = Descendants(container).OfType<TextBlock>().First(block => block.Name == "CountdownText");
            Assert.AreEqual(test.Design.Dictionary["Kyf.Brush.PillSheetText"], text.Foreground);
        }
        finally
        {
            window.Close();
        }
    }

    private static ShellWindow Show(TestShell test, object page, double width, double height)
    {
        test.Shell.Navigate(new NavigationItem("vue", "Vue", KyfRev.DesignSystem.Controls.Icons.Document, int.MaxValue, page));
        var window = new ShellWindow(test.Shell) { ShowActivated = true, Left = -10000, Top = -10000, Width = width, Height = height, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        window.Activate();
        TestShell.Flush();
        return window;
    }

    private static void Press(UIElement element, Key key) =>
        element.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(element), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });

    private static void Click(Control item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));

    private static void Same(string expected, IEnumerable<string> actual) => Assert.AreEqual(expected, string.Join(" | ", actual));

    private static T Named<T>(DependencyObject root, string name)
        where T : FrameworkElement =>
        Descendants(root).OfType<T>().First(element => element.Name == name);

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>Un coffre d'essai : la fiche « Mitose » créée le jeudi 1er octobre 2026, dans « Biologie » ; un dossier vide « Biologie/Cellule ».</summary>
    private sealed class ExamVault : IDisposable
    {
        private readonly TestVaults _vaults;

        public ExamVault()
        {
            Time = new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
            _vaults = new TestVaults(AppMode.Development, Time);
            Assert.IsNotNull(App.Vaults.Create(_vaults.PathOf("Coffre")).Vault);
            SheetId = App.Sheets.Create("Biologie", "Mitose", null).Value;
            Assert.IsTrue(App.Folders.Create("Biologie/Cellule").Succeeded);
        }

        public FakeTime Time { get; }

        public FakeOpener Opener { get; } = new();

        public KyfRev.Application.UseCases.KyfRevApplication App => _vaults.App;

        public string SheetId { get; }

        public DashboardPageViewModel Page() => new(App.Reminders, App.Sheets, Opener, null, App.Reviews, App.Exams, App.Folders, Time);

        public void Dispose() => _vaults.Dispose();
    }
}
