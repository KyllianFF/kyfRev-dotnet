using Microsoft.Win32;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>Demande un dossier à l'utilisateur.</summary>
public interface IFolderPicker
{
    /// <summary>Le dossier choisi ; rien si l'utilisateur renonce.</summary>
    string? Pick(string title);
}

/// <summary>Le sélecteur de dossiers de Windows, ouvert au-dessus de la fenêtre principale.</summary>
public sealed class FolderPicker : IFolderPicker
{
    private readonly Func<OpenFolderDialog, bool?> _show;

    /// <summary>Le sélecteur de Windows.</summary>
    public FolderPicker()
        : this(dialog => System.Windows.Application.Current?.MainWindow is { } owner ? dialog.ShowDialog(owner) : dialog.ShowDialog())
    {
    }

    /// <summary>Un sélecteur dont l'affichage est fourni (essais).</summary>
    internal FolderPicker(Func<OpenFolderDialog, bool?> show)
    {
        _show = show;
    }

    /// <inheritdoc/>
    public string? Pick(string title)
    {
        var dialog = new OpenFolderDialog { Title = title, Multiselect = false };
        return _show(dialog) == true ? dialog.FolderName : null;
    }
}
