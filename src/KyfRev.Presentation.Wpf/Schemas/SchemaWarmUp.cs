using System.Windows.Controls;
using System.Windows.Threading;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Schemas;

/// <summary>
/// Prépare l'îlot des schémas en arrière-plan, peu après l'ouverture du coffre. Le moteur web met deux à cinq secondes à
/// démarrer : préparé avant qu'on en ait besoin, l'îlot s'affiche dès le premier clic sur « Schémas réseau ». Il est posé, caché,
/// dans <paramref name="host"/> le temps que sa page charge son code, puis retiré : il reste ouvert avec sa vue-modèle
/// (<see cref="SchemasPageViewModel"/>), qui le ferme avec elle.
/// </summary>
/// <param name="host">Une place cachée de la fenêtre, où le moteur peut démarrer.</param>
/// <param name="page">La vue « Schémas réseau » du coffre ouvert, si le module est actif.</param>
internal sealed class SchemaWarmUp(ContentControl host, Func<SchemasPageViewModel?> page)
{
    private readonly DispatcherTimer _delay = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(3) };
    private bool _started;

    /// <summary>Le délai avant la préparation : la fenêtre a d'abord fini de s'afficher, et la vue d'accueil de lire le coffre.</summary>
    internal TimeSpan Delay
    {
        get => _delay.Interval;
        set => _delay.Interval = value;
    }

    /// <summary>Un coffre vient de s'ouvrir : l'îlot de ses schémas se préparera après <see cref="Delay"/>.</summary>
    internal void Schedule()
    {
        if (!_started)
        {
            _started = true;
            _delay.Tick += (_, _) =>
            {
                _delay.Stop();
                Warm();
            };
        }
        _delay.Stop();
        _delay.Start();
    }

    /// <summary>
    /// Prépare l'îlot tout de suite, s'il n'est pas déjà ouvert (la vue a pu s'afficher entre-temps). Un moteur qui ne démarre pas
    /// est refermé sans rien dire : la vue, affichée, réessaiera et dira pourquoi.
    /// </summary>
    internal void Warm()
    {
        if (host.Content is not null || page() is not { Island: null } schemas)
        {
            return;
        }
        var island = schemas.TakeIsland();
        void Leave()
        {
            island.Ready -= OnReady;
            island.Failed -= OnFailed;
            if (ReferenceEquals(host.Content, island))
            {
                host.Content = null;
            }
        }
        void OnReady(object? sender, EventArgs e) => Leave();
        void OnFailed(object? sender, string reason)
        {
            Leave();
            if (ReferenceEquals(schemas.Island, island))
            {
                schemas.CloseIsland();
            }
        }
        island.Ready += OnReady;
        island.Failed += OnFailed;
        host.Content = island;
    }
}
