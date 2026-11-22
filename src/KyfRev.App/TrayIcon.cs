using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using KyfRev.Infrastructure.Windows;

namespace KyfRev.App;

/// <summary>
/// L'icône de kyfRev dans la barre d'état (rangée 25D) : un clic ouvre kyfRev, le clic droit ouvre son menu, dans le dessin
/// des menus de kyfRev. L'icône vient de celle de l'exécutable.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _icon;
    private readonly Func<IReadOnlyList<Control>> _items;
    private readonly Action _open;

    /// <summary>Une icône, cachée tant qu'on ne la montre pas.</summary>
    public TrayIcon(Func<IReadOnlyList<Control>> items, Action open)
    {
        _items = items;
        _open = open;
        _icon = new System.Windows.Forms.NotifyIcon
        {
            Text = KyfRev.Presentation.Wpf.Shell.TrayText.Name,
            Icon = Environment.ProcessPath is { } path ? System.Drawing.Icon.ExtractAssociatedIcon(path) : null,
            Visible = false,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                ShowMenu();
            }
            else if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                _open();
            }
        };
    }

    /// <summary>Montrée près de l'horloge.</summary>
    public bool Visible
    {
        get => _icon.Visible;
        set => _icon.Visible = value;
    }

    /// <summary>Une bulle de Windows, sous l'icône.</summary>
    public void Tell(string title, string text)
    {
        if (_icon.Visible)
        {
            _icon.ShowBalloonTip(5000, title, text, System.Windows.Forms.ToolTipIcon.None);
        }
    }

    /// <summary>
    /// Le menu, là où est la souris. Sa fenêtre est mise au premier plan à l'ouverture : sans cela, un clic ailleurs ne le
    /// fermerait pas, kyfRev n'étant pas l'application active.
    /// </summary>
    private void ShowMenu()
    {
        var menu = new ContextMenu { Placement = PlacementMode.MousePoint };
        foreach (var item in _items())
        {
            menu.Items.Add(item);
        }
        menu.Opened += (_, _) =>
        {
            if (PresentationSource.FromVisual(menu) is HwndSource source)
            {
                ForegroundWindow.Bring(source.Handle);
            }
        };
        menu.IsOpen = true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Icon?.Dispose();
        _icon.Dispose();
    }
}
