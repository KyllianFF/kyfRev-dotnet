using System.Diagnostics;
using System.Globalization;
using System.Windows;
using KyfRev.Infrastructure.Windows;

namespace KyfRev.Acceptance.Host;

/// <summary>
/// Lance l'application dans <c>args[0]</c> (son <c>portable.json</c> désigne le coffre), à la date
/// <c>args[1]</c>. Rien ne sort de ce dossier.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var folder = args[0];
        var application = new App.App
        {
            Time = new SimulatedClock(DateTimeOffset.Parse(args[1], CultureInfo.InvariantCulture)),
            Locations = VaultLocations.From(folder, new ProfileFolders(folder, folder, folder)),
        };
        // Assez étroite pour une seule colonne, quel que soit l'écran : les rappels sont alors dans la liste.
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((sender, _) =>
        {
            var window = (Window)sender;
            window.Width = 800;
            window.Height = 700;
        }));
        application.InitializeComponent();
        return application.Run();
    }

    /// <summary>Une horloge qui part d'un instant donné, en temps universel : les dates ne dépendent pas du poste.</summary>
    private sealed class SimulatedClock(DateTimeOffset start) : TimeProvider
    {
        private readonly Stopwatch _elapsed = Stopwatch.StartNew();

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => start.ToUniversalTime() + _elapsed.Elapsed;
    }
}
