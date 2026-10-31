using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace KyfRev.Infrastructure.Vault.Format;

/// <summary>
/// Propriétés JSON inconnues de cette version, conservées pour être réécrites telles quelles
/// après les propriétés connues. La version 0.x ne les gardait qu'au premier niveau de
/// <c>coffre.json</c> ; ici, aucun réglage ni champ d'un autre outil n'est perdu.
/// </summary>
public sealed class UnknownProperties
{
    private UnknownProperties(ImmutableArray<KeyValuePair<string, JsonNode?>> properties)
    {
        Properties = properties;
    }

    /// <summary>Aucune propriété inconnue.</summary>
    public static UnknownProperties None { get; } = new([]);

    /// <summary>Propriétés conservées, dans l'ordre où elles ont été lues.</summary>
    public ImmutableArray<KeyValuePair<string, JsonNode?>> Properties { get; }

    internal static UnknownProperties From(ImmutableArray<KeyValuePair<string, JsonNode?>> properties) =>
        properties.IsEmpty ? None : new(properties);
}
