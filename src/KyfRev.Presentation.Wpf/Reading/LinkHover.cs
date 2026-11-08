using System.Windows.Documents;
using System.Windows.Threading;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>
/// Quand montrer et quand cacher l'aperçu d'une fiche liée, comme dans la 0.x (<c>survol.ts</c>) : après un court délai sur
/// un lien, tout de suite s'il a le focus ; peu après que le pointeur a quitté le lien et l'aperçu, de quoi atteindre celui-ci.
/// </summary>
internal sealed class LinkHover
{
    /// <summary>Délai avant l'aperçu, le pointeur posé sur un lien.</summary>
    public static readonly TimeSpan ShowDelay = TimeSpan.FromMilliseconds(450);

    /// <summary>Délai avant le retrait, le pointeur parti.</summary>
    public static readonly TimeSpan HideDelay = TimeSpan.FromMilliseconds(220);

    private readonly DispatcherTimer _showing = new() { Interval = ShowDelay };
    private readonly DispatcherTimer _hiding = new() { Interval = HideDelay };
    private readonly Action<Hyperlink> _show;
    private readonly Action _hide;
    private readonly Func<bool> _isShown;
    private Hyperlink? _waiting;

    /// <summary>Surveille les liens : <paramref name="show"/> lit et montre l'aperçu, <paramref name="hide"/> le referme, et annule sa lecture.</summary>
    /// <param name="show">Montre l'aperçu d'un lien.</param>
    /// <param name="hide">Referme l'aperçu.</param>
    /// <param name="isShown">Un aperçu est à l'écran.</param>
    public LinkHover(Action<Hyperlink> show, Action hide, Func<bool> isShown)
    {
        _show = show;
        _hide = hide;
        _isShown = isShown;
        _showing.Tick += (_, _) => ShowWaiting();
        _hiding.Tick += (_, _) => Hide();
    }

    /// <summary>Le lien dont l'aperçu est montré, ou en cours de lecture.</summary>
    public Hyperlink? Current { get; private set; }

    /// <summary>L'aperçu partira bientôt, faute de pointeur.</summary>
    public bool IsHiding => _hiding.IsEnabled;

    /// <summary>Le pointeur ou le focus arrive sur un lien.</summary>
    public void Enter(Hyperlink link, bool atOnce)
    {
        _hiding.Stop();
        _showing.Stop();
        _waiting = ReferenceEquals(link, Current) ? null : link;
        if (_waiting is null)
        {
            return;
        }
        if (atOnce)
        {
            ShowWaiting();
        }
        else
        {
            _showing.Start();
        }
    }

    /// <summary>Le pointeur ou le focus quitte le lien : un aperçu encore en lecture s'annule, un aperçu montré part bientôt.</summary>
    public void Leave()
    {
        _showing.Stop();
        _waiting = null;
        if (_isShown())
        {
            _hiding.Start();
        }
        else
        {
            Hide();
        }
    }

    /// <summary>Le pointeur est dans l'aperçu : il reste.</summary>
    public void Keep() => _hiding.Stop();

    /// <summary>Le pointeur a quitté l'aperçu : il part bientôt.</summary>
    public void Release() => _hiding.Start();

    /// <summary>Cache l'aperçu tout de suite.</summary>
    public void Hide()
    {
        _showing.Stop();
        _hiding.Stop();
        _waiting = null;
        if (Current is not null)
        {
            Current = null;
            _hide();
        }
    }

    private void ShowWaiting()
    {
        _showing.Stop();
        if (_waiting is { } link)
        {
            _waiting = null;
            Current = link;
            _show(link);
        }
    }
}
