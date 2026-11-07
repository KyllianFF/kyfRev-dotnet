using System.IO;
using System.Windows.Threading;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// Le verrouillage d'un coffre chiffré, sur de vrais coffres : Ctrl+Maj+L, ou de lui-même après l'inactivité ou au départ
/// de l'utilisateur. Ce qui s'écrit s'enregistre d'abord, le coffre se ferme, chaque vue oublie, et seul son mot de passe le
/// rouvre. Un coffre en clair ne se verrouille pas.
/// </summary>
[TestClass]
public sealed class LockTests
{
    [STATestMethod]
    public void Locking_shows_the_unlock_screen_closes_the_vault_and_makes_every_view_forget()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.OpenEncrypted(remember: true);
        Assert.IsTrue(scene.Shell.CanLock);
        Assert.IsTrue(scene.Shell.LockCommand.CanExecute(null));

        var locking = scene.Locking.LockAsync();
        UiThread.Until(() => locking.IsCompleted);

        Assert.IsTrue(locking.Result);
        Assert.IsTrue(scene.Shell.IsWelcome);
        var page = (UnlockPageViewModel)scene.Shell.Current.Page;
        Assert.AreEqual(scene.Vaults.Planned, page.Vault);
        Assert.AreEqual(EncryptionText.BackToWelcome, page.BackLabel, "le coffre est fermé : rien derrière");
        Assert.IsNull(scene.Vaults.App.Vaults.State().Vault);
        Assert.AreEqual(1, scene.Forgotten);
        Assert.IsFalse(scene.Shell.LockCommand.CanExecute(null), "rien à verrouiller sur l'écran de déverrouillage");

        // La clé retenue ne le rouvre pas : seul son mot de passe le fait.
        var unlocking = page.UnlockCommand.ExecuteAsync(TestVaults.Password);
        UiThread.Until(() => unlocking.IsCompleted);
        Assert.IsFalse(scene.Shell.IsWelcome);
        Assert.AreEqual(scene.Vaults.Planned, scene.Vaults.App.Vaults.State().Vault);
        Assert.AreEqual(1, scene.Changes);
    }

    [STATestMethod]
    public void Nothing_locks_without_an_encrypted_vault_open_or_twice_at_once()
    {
        using var ui = new UiThread();
        using var scene = new Scene();

        // Sans coffre : l'accueil.
        Assert.IsFalse(Run(scene.Locking.LockAsync()));

        // Un coffre en clair : il reste ouvert, rien n'est oublié.
        var plain = scene.Vaults.CreateVault("Clair");
        scene.Vaults.App.Vaults.Open(plain);
        scene.Shell.ShowWorkspace();
        Assert.IsFalse(Run(scene.Locking.LockAsync()));
        Assert.AreEqual(plain, scene.Vaults.App.Vaults.State().Vault);
        Assert.AreEqual(0, scene.Forgotten);

        // Deux verrouillages à la fois : le second ne fait rien.
        scene.OpenEncrypted(remember: false);
        var held = new HeldPage();
        scene.Shell.Navigate(new NavigationItem("vue", "Vue", "", int.MaxValue, held));
        var first = scene.Locking.LockAsync();
        Assert.IsFalse(Run(scene.Locking.LockAsync()));
        held.Finish(true);
        UiThread.Until(() => first.IsCompleted);
        Assert.IsTrue(first.Result);
        Assert.AreEqual(1, scene.Forgotten);
    }

    [STATestMethod]
    public void What_cannot_be_saved_keeps_the_vault_open()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.OpenEncrypted(remember: false);
        var held = new HeldPage();
        scene.Shell.Navigate(new NavigationItem("vue", "Vue", "", int.MaxValue, held));

        var locking = scene.Locking.LockAsync();
        Assert.IsFalse(locking.IsCompleted, "le verrouillage attend que ce qui s'écrit soit enregistré");
        held.Finish(false);
        UiThread.Until(() => locking.IsCompleted);

        Assert.IsFalse(locking.Result);
        Assert.AreSame(held, scene.Shell.Current.Page);
        Assert.AreEqual(scene.Vaults.Planned, scene.Vaults.App.Vaults.State().Vault, "rien ne se perd : le coffre reste ouvert");
        Assert.AreEqual(0, scene.Forgotten);
    }

    [STATestMethod]
    public void A_running_session_is_kept_to_be_resumed_after_the_lock()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        scene.OpenEncrypted(remember: false);
        var sessions = new MemorySessions();
        var session = new StudySession(scene.Vaults.Planned, new DateOnly(2026, 11, 7), [new SessionItem(ItemKind.Sheet, "a", "a"), new SessionItem(ItemKind.Sheet, "b", "b")], 0, 0, 0, 0, TimeSpan.Zero, true);
        var study = new StudySessionViewModel(session, sessions, TimeProvider.System, (_, _) => new HeldPage(), () => { }, () => { });
        scene.Shell.Navigate(new NavigationItem("seance", "Séance", "", int.MaxValue, study));

        var locking = scene.Locking.LockAsync();
        ((HeldPage)study.Current!).Finish(true);
        UiThread.Until(() => locking.IsCompleted);

        Assert.IsTrue(locking.Result);
        Assert.IsNotNull(sessions.Kept, "la séance est gardée pour être reprise");

        // Ce qui s'écrit sur l'élément en cours n'a pas pu être enregistré : la séance reste ouverte, et n'est pas gardée.
        var other = new StudySessionViewModel(session, sessions, TimeProvider.System, (_, _) => new HeldPage(), () => { }, () => { });
        var leaving = other.LeaveAsync();
        ((HeldPage)other.Current!).Finish(false);
        UiThread.Until(() => leaving.IsCompleted);
        Assert.IsFalse(leaving.Result);
    }

    [STATestMethod]
    public void A_sheet_that_is_only_read_leaves_at_once()
    {
        using var ui = new UiThread();
        using var reading = new ReadingVault("Le texte de la fiche.", passage: false);
        var page = reading.Page();

        var leaving = page.LeaveAsync();
        UiThread.Until(() => leaving.IsCompleted);
        Assert.IsTrue(leaving.Result);
    }

    [STATestMethod]
    public void The_lock_command_and_its_search_entry_follow_the_vault()
    {
        var shell = new TestShell().Shell;
        shell.ShowWorkspace();
        Assert.IsFalse(shell.LockCommand.CanExecute(null), "un coffre en clair ne se verrouille pas");
        shell.PaletteQuery = "verrouiller";
        Assert.IsTrue(shell.HasNoResult);

        shell.CanLock = true;
        Assert.IsFalse(shell.LockCommand.CanExecute(null), "rien ne verrouille encore");
        var locked = 0;
        shell.Locking = () =>
        {
            locked++;
            return Task.CompletedTask;
        };
        shell.CanLock = false;
        shell.CanLock = true;
        Assert.IsTrue(shell.LockCommand.CanExecute(null));
        shell.PaletteQuery = "verrouiller";
        var entry = shell.PaletteResults.Single();
        Assert.AreEqual("Ctrl+Maj+L", entry.Detail);
        entry.Execute();
        Assert.AreEqual(1, locked);

        shell.ShowUnlock(new object());
        Assert.IsFalse(shell.LockCommand.CanExecute(null));
        shell.PaletteQuery = "verrouiller";
        Assert.IsTrue(shell.HasNoResult, "sur l'écran de déverrouillage, la recherche ne le propose plus");
    }

    [STATestMethod]
    public void Idleness_locks_after_the_delay_and_leaving_the_computer_locks_at_once()
    {
        using var ui = new UiThread();
        var presence = new FakePresence();
        var locks = 0;
        TimeSpan? delay = TimeSpan.FromMinutes(15);
        using var auto = new AutoLock(presence, () => delay, () =>
        {
            locks++;
            return Task.FromResult(true);
        });
        auto.Start();

        presence.Idle = TimeSpan.FromMinutes(14);
        Assert.IsFalse(Run(auto.CheckAsync()));
        presence.Idle = TimeSpan.FromMinutes(15);
        Assert.IsTrue(Run(auto.CheckAsync()));
        Assert.AreEqual(1, locks);

        // Jamais sur l'inactivité : le départ verrouille toujours.
        delay = null;
        presence.Idle = TimeSpan.FromHours(3);
        Assert.IsFalse(Run(auto.CheckAsync()));
        presence.Leave();
        UiThread.Until(() => locks == 2);

        auto.Dispose();
        presence.Leave();
        TestShell.Flush();
        Assert.AreEqual(2, locks, "arrêté, il ne verrouille plus");
        Assert.ThrowsExactly<ArgumentNullException>(() => new AutoLock(null!, () => delay, () => Task.FromResult(true)));
        Assert.ThrowsExactly<ArgumentNullException>(() => new AutoLock(presence, null!, () => Task.FromResult(true)));
        Assert.ThrowsExactly<ArgumentNullException>(() => new AutoLock(presence, () => delay, null!));
    }

    [STATestMethod]
    public void The_timer_looks_at_idleness_on_its_own()
    {
        using var ui = new UiThread();
        var presence = new FakePresence { Idle = TimeSpan.FromHours(1) };
        var locks = 0;
        using var auto = new AutoLock(presence, () => TimeSpan.FromMinutes(15), () =>
        {
            locks++;
            return Task.FromResult(true);
        }, Dispatcher.CurrentDispatcher);
        auto.Start();
        UiThread.Until(() => locks > 0, AutoLock.CheckInterval + TimeSpan.FromSeconds(10));
    }

    private static bool Run(Task<bool> task)
    {
        UiThread.Until(() => task.IsCompleted);
        return task.Result;
    }

    /// <summary>Une page qui finit ce qu'elle a en cours quand l'essai le dit.</summary>
    private sealed class HeldPage : ILeaving
    {
        private readonly TaskCompletionSource<bool> _done = new();

        public Task<bool> LeaveAsync() => _done.Task;

        public void Finish(bool saved) => _done.TrySetResult(saved);
    }

    private sealed class FakePresence : IUserPresence
    {
        public event EventHandler? Away;

        public TimeSpan Idle { get; set; }

        public void Leave() => Away?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Un coffre chiffré prévu (mode portable), la coquille, et le verrouillage assemblé comme sur le poste.</summary>
    private sealed class Scene : IDisposable
    {
        public Scene()
        {
            Vaults = new TestVaults(AppMode.Portable);
            Shell = new TestShell().Shell;
            Locking = new VaultLocking(Shell, Vaults.App.Vaults, Vaults.Picker, () => Forgotten++, () =>
            {
                Changes++;
                Shell.ShowWorkspace();
            });
            Shell.Locking = Locking.LockAsync;
        }

        public TestVaults Vaults { get; }

        public ShellViewModel Shell { get; }

        public VaultLocking Locking { get; }

        public int Forgotten { get; private set; }

        public int Changes { get; private set; }

        /// <summary>Le coffre prévu, chiffré et déverrouillé ; l'espace de travail affiché.</summary>
        public void OpenEncrypted(bool remember)
        {
            if (!File.Exists(Path.Combine(Vaults.Planned, "kyfrev-chiffre.json")))
            {
                Vaults.CreateEncryptedVault(Vaults.Planned);
            }
            Assert.AreEqual(Vaults.Planned, Vaults.App.Vaults.Unlock(Vaults.Planned, VaultSecret.Password(TestVaults.Password), remember).Vault);
            Shell.ShowWorkspace();
            Shell.CanLock = Vaults.App.Vaults.State().Encrypted;
        }

        public void Dispose() => Vaults.Dispose();
    }
}
