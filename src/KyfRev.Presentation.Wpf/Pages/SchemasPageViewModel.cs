using CommunityToolkit.Mvvm.ComponentModel;
using KyfRev.Modules.Schemas;
using KyfRev.Presentation.Wpf.Schemas;
using KyfRev.Presentation.Wpf.Shell;
using Microsoft.Win32;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Vue « Schémas réseau » : un îlot web, et ce qu'il faut pour qu'il parle au coffre. L'îlot vit avec cette vue, pas avec son
/// affichage : quitter l'onglet le garde ouvert, et y revenir ne relance pas le moteur web. Il se ferme avec elle, quand les
/// entrées du menu changent (un coffre verrouillé, un autre coffre, le module coupé).
/// </summary>
public sealed partial class SchemasPageViewModel : ObservableObject, ILeaving, IDisposable
{
    private readonly SchemaUseCases _schemas;
    private readonly IFolderPicker _folders;
    private readonly IReviewOpener _opener;
    private Action? _back;
    private bool _disposed;

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
        FinishIsland = () => Island is { IsDisposed: false } island ? island.FinishAsync() : Task.FromResult(true);
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

    /// <summary>Finit ce qui est en cours dans l'îlot ouvert (<see cref="SchemaIsland.FinishAsync"/>) ; rien quand l'îlot n'est pas ouvert.</summary>
    internal Func<Task<bool>> FinishIsland { get; set; }

    /// <summary>L'îlot ouvert pour cette vue, affiché ou non ; la vue l'ouvre la première fois qu'elle s'affiche.</summary>
    internal SchemaIsland? Island { get; private set; }

    /// <summary>L'îlot de cette vue : celui qui est déjà ouvert, sinon un nouveau, qui démarre dès qu'il est posé dans une fenêtre.</summary>
    internal SchemaIsland TakeIsland()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Island is not { IsDisposed: false })
        {
            Island = new SchemaIsland(Bridge(), WebDataFolder);
        }
        return Island;
    }

    /// <summary>Ferme l'îlot : le moteur web s'est arrêté, ou la vue n'en a plus besoin. La vue en ouvrira un autre.</summary>
    internal void CloseIsland()
    {
        Island?.Dispose();
        Island = null;
    }

    /// <summary>La vue ne sert plus (ses entrées du menu ont été remplacées) : son îlot se ferme.</summary>
    public void Dispose()
    {
        _disposed = true;
        CloseIsland();
    }

    /// <summary>
    /// Avant le verrouillage : le champ où l'on écrivait dans l'éditeur s'applique, et le schéma s'enregistre. Faux s'il n'a pas pu
    /// l'être (refusé, modifié ailleurs, ou l'îlot n'a pas répondu) : le coffre reste alors ouvert. Vrai quand l'îlot n'est pas ouvert.
    /// </summary>
    public async Task<bool> LeaveAsync() => await FinishIsland().ConfigureAwait(true);

    /// <summary>Le pont vers le coffre, pour un îlot.</summary>
    internal SchemaBridge Bridge() => new(_schemas, _folders, () => PickSchemaFile(), _opener.OpenSheet, EditorClosed, CopyImage);

    /// <summary>Ce qui copie l'image d'un schéma : le presse-papiers de Windows, sauf dans les essais.</summary>
    internal Action<byte[]> CopyImage { get; init; } = ToClipboard;

    /// <summary>Met une image PNG dans le presse-papiers ; une image illisible est refusée comme un appel mal formé.</summary>
    private static void ToClipboard(byte[] png)
    {
        try
        {
            using var stream = new System.IO.MemoryStream(png);
            var picture = System.Windows.Media.Imaging.BitmapFrame.Create(stream, System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
            picture.Freeze();
            System.Windows.Clipboard.SetImage(picture);
        }
        catch (Exception exception) when (exception is NotSupportedException or System.IO.FileFormatException or System.Runtime.InteropServices.ExternalException)
        {
            throw new InvalidOperationException(Schemas.SchemaText.ClipboardBusy, exception);
        }
    }

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
