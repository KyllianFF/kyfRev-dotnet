using KyfRev.Application;
using KyfRev.Application.UseCases;

namespace KyfRev.Extensions.Host;

/// <summary>Les données des extensions dans le coffre ouvert (<c>.suivi/extensions/&lt;id&gt;.json</c>). Elles restent quand une extension est retirée.</summary>
/// <param name="host">Le coffre ouvert.</param>
public sealed class ExtensionDataUseCases(VaultHost host)
{
    /// <summary>Les données d'une extension, en JSON ; rien si elle n'en a pas.</summary>
    /// <param name="id">Son identifiant.</param>
    [ZeroXCommand("donnees_extension")]
    public Outcome<string?> Read(string id) => host.Use(vault => vault.ExtensionData(id));

    /// <summary>Remplace les données d'une extension : du JSON valide, 1 Mo au plus.</summary>
    /// <param name="id">Son identifiant.</param>
    /// <param name="json">Ses données.</param>
    [ZeroXCommand("enregistrer_donnees_extension")]
    public Outcome<Done> Save(string id, string json) => host.Use(vault => vault.SaveExtensionData(id, json));
}

/// <summary>Le coffre ouvert, tel que les extensions le voient : ses fiches présentes, et leurs propres données. Rien n'écrit dans une fiche.</summary>
/// <param name="sheets">Les fiches.</param>
/// <param name="data">Les données des extensions.</param>
public sealed class VaultExtensionData(SheetUseCases sheets, ExtensionDataUseCases data) : IExtensionData
{
    /// <inheritdoc/>
    public Task<Outcome<IReadOnlyList<ExtensionSheet>>> SheetsAsync() => Task.Run(() =>
    {
        var list = sheets.List();
        return list.Succeeded
            ? Outcome.Of<IReadOnlyList<ExtensionSheet>>([.. list.Value.Where(sheet => sheet.FilePresent).Select(sheet => new ExtensionSheet(sheet.Id, sheet.Title, sheet.Folder, sheet.Tags, sheet.Archived))])
            : Outcome.Refused<IReadOnlyList<ExtensionSheet>>(list.Error!);
    });

    /// <inheritdoc/>
    public Task<Outcome<ExtensionSheetText>> SheetAsync(string id) => Task.Run(() =>
    {
        var sheet = sheets.Open(id);
        return sheet.Succeeded
            ? Outcome.Of(new ExtensionSheetText(sheet.Value.Id, sheet.Value.Title, sheet.Value.Body))
            : Outcome.Refused<ExtensionSheetText>(sheet.Error!);
    });

    /// <inheritdoc/>
    public Task<Outcome<string?>> StoredAsync(string extension) => Task.Run(() => data.Read(extension));

    /// <inheritdoc/>
    public Task<Outcome<Done>> StoreAsync(string extension, string json) => Task.Run(() => data.Save(extension, json));
}
