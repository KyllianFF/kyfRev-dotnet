using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Extensions;

/// <summary>
/// Les blocs <c>```langage</c> que dessinent les extensions actives, partout où une fiche s'affiche : lecture, révision, aperçu et
/// éditeur. Le langage d'un bloc désigne l'extension qui le déclare ; son code ne démarre qu'au premier bloc à dessiner.
/// </summary>
public sealed class ExtensionBlocks
{
    /// <summary>La clé sous laquelle les vues trouvent ce service dans les ressources de l'application.</summary>
    public const string ResourceKey = "Kyf.ExtensionBlocks";

    private readonly ExtensionLibrary _library;
    private readonly ExtensionEngine _engine;
    private readonly Func<bool> _isDark;
    private Dictionary<string, string>? _owners;

    /// <summary>Les blocs des extensions de cette bibliothèque.</summary>
    /// <param name="library">Les extensions installées.</param>
    /// <param name="engine">Le moteur où tourne leur code.</param>
    /// <param name="isDark">Le thème est sombre.</param>
    public ExtensionBlocks(ExtensionLibrary library, ExtensionEngine engine, Func<bool> isDark)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(isDark);
        _library = library;
        _engine = engine;
        _isDark = isDark;
    }

    /// <summary>Les langages dessinés, triés : l'éditeur les reçoit pour savoir quels blocs demander.</summary>
    public IReadOnlyList<string> Languages => [.. Owners().Keys.Order(StringComparer.Ordinal)];

    /// <summary>Une extension active dessine ce langage.</summary>
    /// <param name="language">Le langage du bloc.</param>
    public bool Draws(string? language) => language is { Length: > 0 } && Owners().ContainsKey(language);

    /// <summary>Ce que l'extension du langage affiche pour ce bloc ; un refus si aucune extension active ne le dessine.</summary>
    /// <param name="language">Le langage du bloc.</param>
    /// <param name="source">Son texte.</param>
    public Task<ExtensionAnswer> RenderAsync(string language, string source) =>
        Owners().TryGetValue(language, out var id)
            ? _engine.RenderBlockAsync(id, language, source, _isDark())
            : Task.FromResult(new ExtensionAnswer(null, ReadingText.NoExtensionBlockOf(language)));

    /// <summary>Transmet un clic dans un bloc à l'extension qui le dessine.</summary>
    /// <param name="language">Le langage du bloc.</param>
    /// <param name="action">L'action.</param>
    public Task<ExtensionAnswer> ActAsync(string language, ExtensionAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Owners().TryGetValue(language, out var id)
            ? _engine.ActAsync(id, action.Name, action.Value, action.Data)
            : Task.FromResult(new ExtensionAnswer(null, ReadingText.NoExtensionBlockOf(language)));
    }

    /// <summary>Les extensions ont changé : les langages se relisent au prochain bloc.</summary>
    public void Forget() => _owners = null;

    private Dictionary<string, string> Owners()
    {
        if (_owners is null)
        {
            var owners = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var manifest in _library.Installed().Where(extension => extension.IsActive).Select(extension => extension.Manifest))
            {
                foreach (var language in manifest.Blocks)
                {
                    owners.TryAdd(language, manifest.Id);
                }
            }
            _owners = owners;
        }
        return _owners;
    }
}
