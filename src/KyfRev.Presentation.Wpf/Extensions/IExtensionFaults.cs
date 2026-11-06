namespace KyfRev.Presentation.Wpf.Extensions;

/// <summary>Une erreur d'une extension : quand, où (sa vue, une action, une commande…) et ce qu'elle a dit.</summary>
/// <param name="At">Quand.</param>
/// <param name="Where">Ce qui était demandé : « vue », « action « x » », « commande « y » »…</param>
/// <param name="Message">L'erreur.</param>
public sealed record ExtensionFault(DateTimeOffset At, string Where, string Message);

/// <summary>Les dernières erreurs de chaque extension, pour le mode développeur.</summary>
public interface IExtensionFaults
{
    /// <summary>Une extension a une erreur de plus, ou plus aucune.</summary>
    event EventHandler<ExtensionEventArgs>? FaultsChanged;

    /// <summary>Ses dernières erreurs, de la plus récente à la plus ancienne.</summary>
    /// <param name="id">L'extension.</param>
    IReadOnlyList<ExtensionFault> Faults(string id);

    /// <summary>Oublie ses erreurs : elle vient d'être rechargée.</summary>
    /// <param name="id">L'extension.</param>
    void ForgetFaults(string id);
}
