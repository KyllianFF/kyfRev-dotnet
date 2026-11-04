using CommunityToolkit.Mvvm.ComponentModel;
using KyfRev.Modules.Schemas;
using KyfRev.Presentation.Wpf.Schemas;
using KyfRev.Presentation.Wpf.Shell;
using Microsoft.Win32;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue « Schémas réseau » : un îlot web, et ce qu'il faut pour qu'il parle au coffre.</summary>
public sealed partial class SchemasPageViewModel : ObservableObject
{
    private readonly SchemaUseCases _schemas;
    private readonly IFolderPicker _folders;
    private readonly IReviewOpener _opener;
    private Action? _back;

    /// <summary>La vue d'un module, sur ses cas d'usage.</summary>
    /// <param name="schemas">Les cas d'usage du module.</param>
    /// <param name="folders">Le choix d'un dossier d'export.</param>
    /// <param name="opener">Ouvre une fiche qui cite un schéma.</param>
    /// <param name="webDataFolder">Le dossier de données du moteur web.</param>
    public SchemasPageViewModel(SchemaUseCases schemas, IFolderPicker folders, IReviewOpener opener, string webDataFolder)
    {
        _schemas = schemas;
        _folders = folders;
        _opener = opener;
        WebDataFolder = webDataFolder;
    }

    /// <summary>Le dossier de données du moteur web.</summary>
    public string WebDataFolder { get; }

    /// <summary>Le moteur web n'a pas pu démarrer : l'îlot ne peut pas s'ouvrir.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem))]
    public partial string? Problem { get; set; }

    /// <summary>Un problème est affiché.</summary>
    public bool HasProblem => Problem is not null;

    /// <summary>Demande le fichier à importer ; à défaut, le dialogue de Windows.</summary>
    internal Func<string?> PickSchemaFile { get; set; } = AskFile;

    /// <summary>Le schéma à ouvrir dans l'éditeur, demandé depuis une fiche ; l'îlot le prend en charge.</summary>
    [ObservableProperty]
    public partial string? ToOpen { get; set; }

    /// <summary>Le pont vers le coffre, pour un îlot.</summary>
    internal SchemaBridge Bridge() => new(_schemas, _folders, () => PickSchemaFile(), _opener.OpenSheet, EditorClosed);

    /// <summary>Ouvre l'éditeur sur un schéma ; le fermer revient d'où l'on vient.</summary>
    /// <param name="id">Le schéma.</param>
    /// <param name="back">Revient à la vue d'où il a été ouvert.</param>
    internal void OpenFrom(string id, Action back)
    {
        _back = back;
        ToOpen = id;
    }

    private void EditorClosed()
    {
        var back = _back;
        _back = null;
        back?.Invoke();
    }

    private static string? AskFile()
    {
        var dialog = new OpenFileDialog { Title = SchemaText.ChooseFile, Filter = SchemaText.FileFilter };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
