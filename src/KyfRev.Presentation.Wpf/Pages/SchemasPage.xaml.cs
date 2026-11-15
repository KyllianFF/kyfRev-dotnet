using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Schemas;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Vue « Schémas réseau » : elle montre l'îlot de sa vue-modèle, l'ouvre la première fois, et le rend en partant. L'îlot reste
/// ouvert avec la vue-modèle (<see cref="SchemasPageViewModel"/>) : y revenir est immédiat, et la liste relit alors le coffre. La
/// fenêtre « Imprimer » se pose sur l'îlot, recouvert de son image le temps qu'elle est ouverte.
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
        page.Print.PropertyChanged += OnPrintChanged;
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
        _page?.Print.PropertyChanged -= OnPrintChanged;
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

    private void OnPrintChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PrintViewModel.IsOpen))
        {
            UpdateShell();
        }
    }

    /// <summary>« Imprimer » : le travail part ; s'il est refusé, la fenêtre reste ouverte et dit pourquoi.</summary>
    private void OnPrintClosed(object sender, DialogClosedEventArgs e)
    {
        if ((DataContext as SchemasPageViewModel)?.Print is not { } print)
        {
            return;
        }
        if (e.Choice == DialogChoice.Primary && !print.Print())
        {
            PrintDialog.SetCurrentValue(Dialog.IsOpenProperty, true);
            return;
        }
        print.Close();
    }

    /// <summary>L'îlot est recouvert de son image quand la palette, l'aide ou la fenêtre « Imprimer » sont ouvertes.</summary>
    private void UpdateShell()
    {
        if (Island is not { } island)
        {
            return;
        }
        _ = island.SetCoveredAsync(_shell is { IsPaletteOpen: true } or { IsHelpOpen: true } || _page?.Print.IsOpen == true);
        if (_shell is { } shell)
        {
            island.SetZoom(shell.Zoom);
        }
    }
}
