namespace KyfRev.Application.Model;

/// <summary>Un schéma réseau lu sur le disque.</summary>
/// <param name="Id">Identifiant (<c>s_</c> + ULID).</param>
/// <param name="Text">Texte JSON.</param>
/// <param name="Fingerprint">À rendre à l'enregistrement : un enregistrement fait sur une autre version est refusé.</param>
public sealed record SchemaDocument(string Id, string Text, string Fingerprint);

/// <summary>Une fiche qui insère un schéma.</summary>
/// <param name="Id">Identifiant de la fiche.</param>
/// <param name="Title">Titre de la fiche.</param>
public sealed record SchemaSheet(string Id, string Title);

/// <summary>Où un schéma est rangé dans le coffre.</summary>
public enum SchemaPlace
{
    /// <summary>Dans la liste des schémas.</summary>
    Active,

    /// <summary>Mis de côté : hors de la liste, mais les fiches qui l'insèrent le montrent toujours.</summary>
    Archived,

    /// <summary>À la corbeille : « supprimé », et restaurable.</summary>
    Trash,
}

/// <summary>Un schéma du coffre, pour la liste.</summary>
/// <param name="Id">Identifiant.</param>
/// <param name="Title">Titre.</param>
/// <param name="Devices">Nombre d'équipements.</param>
/// <param name="ModifiedAt">Dernière modification du fichier.</param>
/// <param name="Sheets">Les fiches qui l'insèrent, par titre.</param>
/// <param name="Place">Où il est rangé.</param>
public sealed record SchemaSummary(string Id, string Title, int Devices, DateTimeOffset? ModifiedAt, IReadOnlyList<SchemaSheet> Sheets, SchemaPlace Place = SchemaPlace.Active);
