using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using KyfRev.Presentation.Wpf.Schemas;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Vue « Schémas réseau » : elle montre l'îlot de sa vue-modèle, l'ouvre la première fois, et le rend en partant. L'îlot reste
/// ouvert avec la vue-modèle (<see cref="SchemasPageViewModel"/>) : y revenir est immédiat, et la liste relit alors le coffre.
/// </summary>
internal sealed partial class SchemasPage : UserControl
{
    private ShellViewModel? _shell;
    private SchemasPageViewModel? _page;

    public SchemasPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _shell = Window.GetWindow(this)?.DataContext as ShellViewModel;
            _shell?.PropertyChanged += OnShellChanged;
            Open();
            UpdateShell();
        };
        Unloaded += (_, _) =>
        {
            _shell?.PropertyChanged -= OnShellChanged;
            _shell = null;
            Close();
        };
        DataContextChanged += (_, _) =>
        {
            Close();
            if (IsLoaded)
            {
                Open();
            }
        };
    }

    /// <summary>L'îlot, s'il est affiché ici.</summary>
    internal SchemaIsland? Island => IslandHost.Content as SchemaIsland;

    private void Open()
    {
        if (Island is not null || DataContext is not SchemasPageViewModel page)
        {
            return;
        }
        var kept = page.Island is { IsDisposed: false };
        var island = page.TakeIsland();
        // Gardé ouvert, il était peut-être encore posé ailleurs (préparé en arrière-plan) : il quitte cette place.
        if (island.Parent is ContentControl previous && !ReferenceEquals(previous, IslandHost))
        {
            previous.Content = null;
        }
        IslandHost.Content = island;
        island.Failed += OnFailed;
        page.PropertyChanged += OnPageChanged;
        _page = page;
        if (kept)
        {
            island.Returned();
        }
        OpenRequested();
    }

    private void OnFailed(object? sender, string reason)
    {
        if (_page is { } page)
        {
            page.Problem = SchemaText.EngineFailedOf(reason);
            Close();
            page.CloseIsland();
        }
    }

    private void OnPageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SchemasPageViewModel.ToOpen))
        {
            OpenRequested();
        }
    }

    private void OpenRequested()
    {
        if (_page?.ToOpen is { } id && Island is { } island)
        {
            _page.ToOpen = null;
            island.OpenSchema(id);
        }
    }

    /// <summary>La vue part : l'îlot quitte l'affichage, sans se fermer ; la vue-modèle le garde.</summary>
    private void Close()
    {
        _page?.PropertyChanged -= OnPageChanged;
        _page = null;
        if (Island is { } island)
        {
            island.Failed -= OnFailed;
            IslandHost.Content = null;
        }
    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.IsPaletteOpen) or nameof(ShellViewModel.IsHelpOpen) or nameof(ShellViewModel.Zoom))
        {
            UpdateShell();
        }
    }

    private void UpdateShell()
    {
        if (_shell is { } shell && Island is { } island)
        {
            _ = island.SetCoveredAsync(shell.IsPaletteOpen || shell.IsHelpOpen);
            island.SetZoom(shell.Zoom);
        }
    }
}
