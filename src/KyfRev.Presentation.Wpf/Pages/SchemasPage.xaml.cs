using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using KyfRev.Presentation.Wpf.Schemas;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue « Schémas réseau » : l'îlot, qui s'ouvre avec la vue et se ferme avec elle.</summary>
internal sealed partial class SchemasPage : UserControl
{
    private ShellViewModel? _shell;

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

    /// <summary>L'îlot, s'il est ouvert.</summary>
    internal SchemaIsland? Island => IslandHost.Content as SchemaIsland;

    private void Open()
    {
        if (Island is null && DataContext is SchemasPageViewModel page)
        {
            var island = new SchemaIsland(page.Bridge(), page.WebDataFolder);
            island.Failed += (_, reason) =>
            {
                page.Problem = SchemaText.EngineFailedOf(reason);
                Close();
            };
            IslandHost.Content = island;
        }
    }

    private void Close()
    {
        Island?.Dispose();
        IslandHost.Content = null;
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
