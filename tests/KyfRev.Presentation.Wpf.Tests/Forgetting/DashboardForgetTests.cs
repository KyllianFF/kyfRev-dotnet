using KyfRev.Application.Model;
using KyfRev.Application.Modules;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// La vue « Aujourd'hui » oublie le coffre qui se verrouille : la liste, l'aperçu, le résumé, les encarts et la mise aux
/// archives en attente s'en vont ; le filtre, le tri et la largeur restent ; la prochaine ouverture relit le coffre, et une
/// lecture partie avant l'oubli n'y remet rien.
/// </summary>
[TestClass]
public sealed class DashboardForgetTests
{
    [TestMethod]
    public async Task Forgetting_drops_what_was_read_from_the_vault_and_the_next_opening_reads_it_again()
    {
        using var today = new TodayVault();
        using var widget = new SlowWidget();
        var page = today.Page(widget);
        page.PageWidth = DashboardPageViewModel.PreviewMinimum;
        page.SelectedSort = page.Sorts[4];
        await page.RefreshCommand.ExecuteAsync(null);
        await page.LoadPreviewCommand.ExecutionTask!;
        page.RequestArchive(page.Rows.OfType<DueItemRow>().Single());

        // Avant l'oubli : Mitose est partout.
        Assert.AreEqual("Mitose", ((DueItemRow)page.SelectedRow!).Item.Title);
        Assert.AreEqual("Mitose", page.Preview!.Title);
        Assert.AreEqual("Samedi 3 octobre", page.DayLabel);
        Assert.HasCount(1, page.Cards);
        Assert.IsTrue(page.CanStart);
        Assert.IsTrue(page.CanResume);
        Assert.IsTrue(page.IsConfirmingArchive);

        page.Forget();

        Assert.IsEmpty(page.Rows);
        Assert.IsEmpty(page.SecondRows);
        Assert.IsNull(page.SelectedRow);
        Assert.IsNull(page.Preview);
        Assert.IsFalse(page.ShowsPreview);
        Assert.IsNull(page.Dashboard);
        Assert.AreEqual(string.Empty, page.Subtitle);
        Assert.AreEqual(string.Empty, page.DayLabel);
        Assert.AreEqual(string.Empty, page.Footer);
        Assert.AreEqual(0, page.ReviewCount);
        Assert.IsFalse(page.HasReviews);
        Assert.AreEqual(string.Empty, page.ReviewNoun);
        Assert.IsEmpty(page.Chips);
        Assert.IsEmpty(page.SideReminders);
        Assert.IsFalse(page.HasSideReminders);
        Assert.IsEmpty(page.Forecast);
        Assert.IsEmpty(page.Week);
        Assert.AreEqual(0, page.WeekTotal);
        Assert.AreEqual(string.Empty, page.WeekNoun);
        Assert.IsEmpty(page.Cards);
        Assert.IsFalse(page.HasCards);
        Assert.IsNull(page.HealthWarning);
        Assert.IsNull(page.PriorityWarning);
        Assert.IsNull(page.Error);
        Assert.IsFalse(page.IsLoading);
        Assert.AreEqual(string.Empty, page.StartLabel);
        Assert.IsFalse(page.CanStart);
        Assert.IsFalse(page.StartCommand.CanExecute(null));
        Assert.AreEqual(string.Empty, page.ResumeLabel);
        Assert.IsFalse(page.CanResume);
        Assert.IsFalse(page.IsConfirmingArchive);

        // Les réglages de la vue ne viennent pas du coffre : ils restent.
        Assert.AreEqual(DashboardPageViewModel.PreviewMinimum, page.PageWidth);
        Assert.AreSame(page.Sorts[4], page.SelectedSort);

        // La fiche visée par la confirmation est oubliée avec le reste : rien n'est archivé.
        await page.AnswerArchiveCommand.ExecuteAsync(true);
        Assert.IsEmpty(today.App.Reminders.Archives().Value);

        // La prochaine ouverture relit le coffre, comme la première.
        page.OnNavigatedTo();
        await page.RefreshCommand.ExecutionTask!;
        await page.LoadPreviewCommand.ExecutionTask!;
        Assert.AreEqual("Mitose", page.Rows.OfType<DueItemRow>().Single().Item.Title);
        Assert.AreEqual("Mitose", ((DueItemRow)page.SelectedRow!).Item.Title);
        Assert.AreEqual("Mitose", page.Preview!.Title);
        Assert.AreEqual("Samedi 3 octobre", page.DayLabel);
        Assert.Contains("1 fiche active", page.Footer);
        Assert.HasCount(1, page.Cards);
        Assert.IsTrue(page.CanStart);
        Assert.IsTrue(page.CanResume);
        Assert.IsFalse(page.IsLoading);
    }

    [TestMethod]
    public async Task A_reading_started_before_forgetting_puts_nothing_back()
    {
        using var today = new TodayVault();
        using var widget = new SlowWidget();
        var page = today.Page(widget);
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.AreEqual("Mitose", page.Rows.OfType<DueItemRow>().Single().Item.Title);

        // Une relecture part et reste en cours, retenue par l'encart, le temps que le coffre se verrouille.
        widget.Hold();
        var late = page.RefreshCommand.ExecuteAsync(null);
        page.Forget();
        widget.Release();
        await late;

        Assert.IsEmpty(page.Rows);
        Assert.IsNull(page.Dashboard);
        Assert.IsEmpty(page.Cards);
        Assert.AreEqual(string.Empty, page.Subtitle);
        Assert.AreEqual(string.Empty, page.DayLabel);
        Assert.AreEqual(0, page.ReviewCount);
        Assert.IsFalse(page.CanStart);
        Assert.IsNull(page.Error);
        Assert.IsFalse(page.IsLoading);

        // La lecture suivante, partie après l'oubli, remplit la vue.
        page.OnNavigatedTo();
        await page.RefreshCommand.ExecutionTask!;
        Assert.AreEqual("Mitose", page.Rows.OfType<DueItemRow>().Single().Item.Title);
        Assert.HasCount(1, page.Cards);
    }

    /// <summary>Un encart de module qui parle de Mitose ; sa lecture peut être retenue, et la relecture du tableau de bord avec elle.</summary>
    private sealed class SlowWidget : IDashboardWidget, IDisposable
    {
        private readonly ManualResetEventSlim _open = new(initialState: true);

        public string Id => "encart-lent";

        public int Order => 10;

        public DashboardCard? Card()
        {
            Assert.IsTrue(_open.Wait(TimeSpan.FromSeconds(10)), "délai dépassé");
            return new DashboardCard("Mitose", "À revoir aujourd'hui", null);
        }

        /// <summary>La prochaine lecture attend <see cref="Release"/>.</summary>
        public void Hold() => _open.Reset();

        public void Release() => _open.Set();

        public void Dispose() => _open.Dispose();
    }

    /// <summary>Une fiche, Mitose, en retard d'un jour sur un vrai coffre, et une séance à reprendre.</summary>
    private sealed class TodayVault : IDisposable
    {
        private readonly TestVaults _vaults;

        public TodayVault()
        {
            var time = new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
            _vaults = new TestVaults(AppMode.Development, time);
            Assert.IsNotNull(App.Vaults.Create(_vaults.PathOf("Coffre")).Vault);
            App.Sheets.Create("Biologie", "Mitose", null).Value.ToString();
            time.AdvanceDays(2);
        }

        public KyfRev.Application.UseCases.KyfRevApplication App => _vaults.App;

        public FakeOpener Opener { get; } = new() { Resumable = 3 };

        public DashboardPageViewModel Page(IDashboardWidget widget) => new(App.Reminders, App.Sheets, Opener, [widget], App.Reviews);

        public void Dispose() => _vaults.Dispose();
    }
}
