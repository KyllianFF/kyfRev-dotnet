using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace KyfRev.Infrastructure.Vault.Paths;

/// <summary>
/// Chemin relatif à la racine du coffre, validé : ni « .. », ni chemin absolu, ni lecteur, ni
/// caractère ou nom interdit par Windows. Les séparateurs sont toujours « / », comme dans les
/// fichiers du coffre. Reprend exactement les règles de <c>CheminRelatif</c> (version 0.x).
/// </summary>
public sealed class VaultPath : IEquatable<VaultPath>, IComparable<VaultPath>
{
    private const int MaxComponentBytes = 255;
    private static readonly SearchValues<char> Forbidden = SearchValues.Create("<>:\"/\\|?*");
    private static readonly string[] Reserved =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    private VaultPath(string value)
    {
        Value = value;
    }

    /// <summary>Le chemin, avec des « / ».</summary>
    public string Value { get; }

    /// <summary>Dernier composant : le nom du fichier ou du dossier.</summary>
    public string Name => Value[(Value.LastIndexOf('/') + 1)..];

    /// <summary>Dossier parent, ou <see langword="null"/> à la racine du coffre.</summary>
    public VaultPath? Parent => Value.LastIndexOf('/') is var slash and >= 0 ? new VaultPath(Value[..slash]) : null;

    /// <summary>Valide un chemin ; les « \ » sont convertis en « / ».</summary>
    /// <param name="text">Chemin à valider.</param>
    /// <param name="path">Chemin validé.</param>
    /// <param name="reason">Raison du refus.</param>
    public static bool TryParse(string text, [NotNullWhen(true)] out VaultPath? path, [NotNullWhen(false)] out string? reason)
    {
        ArgumentNullException.ThrowIfNull(text);
        path = null;
        var normalized = text.Replace('\\', '/');
        if (normalized.StartsWith('/'))
        {
            reason = "chemin absolu";
            return false;
        }
        foreach (var component in normalized.Split('/'))
        {
            if (CheckComponent(component) is { } refusal)
            {
                reason = refusal;
                return false;
            }
        }
        path = new VaultPath(normalized);
        reason = null;
        return true;
    }

    /// <summary>Valide un chemin connu pour être correct (constantes, chemins déjà validés).</summary>
    /// <exception cref="ArgumentException">Le chemin est refusé.</exception>
    public static VaultPath Parse(string text) =>
        TryParse(text, out var path, out var reason)
            ? path
            : throw new ArgumentException($"Chemin « {text} » refusé : {reason}.", nameof(text));

    /// <summary>Chemin relatif d'un fichier situé sous <paramref name="root"/>.</summary>
    public static bool TryFromAbsolute(string root, string absolute, [NotNullWhen(true)] out VaultPath? path, [NotNullWhen(false)] out string? reason)
    {
        var relative = System.IO.Path.GetRelativePath(root, absolute);
        if (System.IO.Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            path = null;
            reason = "hors du coffre";
            return false;
        }
        return TryParse(relative, out path, out reason);
    }

    /// <summary>Ajoute un composant (nom de dossier ou de fichier) au chemin.</summary>
    public bool TryCombine(string name, [NotNullWhen(true)] out VaultPath? path, [NotNullWhen(false)] out string? reason) =>
        TryParse(Value + "/" + name, out path, out reason);

    /// <summary>Vrai si <paramref name="prefix"/> est ce chemin ou l'un de ses dossiers parents.</summary>
    public bool IsWithin(VaultPath prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return Value == prefix.Value
            || (Value.StartsWith(prefix.Value, StringComparison.Ordinal) && Value.Length > prefix.Value.Length && Value[prefix.Value.Length] == '/');
    }

    /// <summary>Chemin absolu sous <paramref name="root"/>.</summary>
    public string ToAbsolute(string root) => System.IO.Path.Combine([root, .. Value.Split('/')]);

    /// <inheritdoc/>
    public bool Equals([NotNullWhen(true)] VaultPath? other) => other is not null && Value == other.Value;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => Equals(obj as VaultPath);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <summary>Ordre des octets UTF-8, comme la version 0.x (ordre des listes et des archives).</summary>
    public int CompareTo(VaultPath? other) =>
        other is null ? 1 : Encoding.UTF8.GetBytes(Value).AsSpan().SequenceCompareTo(Encoding.UTF8.GetBytes(other.Value));

    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Égalité des chemins.</summary>
    public static bool operator ==(VaultPath? left, VaultPath? right) => left is null ? right is null : left.Equals(right);

    /// <summary>Différence des chemins.</summary>
    public static bool operator !=(VaultPath? left, VaultPath? right) => !(left == right);

    /// <summary>Ordre des octets UTF-8.</summary>
    public static bool operator <(VaultPath? left, VaultPath? right) => left is null ? right is not null : left.CompareTo(right) < 0;

    /// <summary>Ordre des octets UTF-8.</summary>
    public static bool operator <=(VaultPath? left, VaultPath? right) => left is null || left.CompareTo(right) <= 0;

    /// <summary>Ordre des octets UTF-8.</summary>
    public static bool operator >(VaultPath? left, VaultPath? right) => left is not null && left.CompareTo(right) > 0;

    /// <summary>Ordre des octets UTF-8.</summary>
    public static bool operator >=(VaultPath? left, VaultPath? right) => left is null ? right is null : left.CompareTo(right) >= 0;

    private static string? CheckComponent(string component)
    {
        if (component.Length == 0)
        {
            return "composant vide (« // » ou « / » en fin de chemin)";
        }
        if (component is "." or "..")
        {
            return "« . » et « .. » sont interdits";
        }
        if (component.IndexOfAny(Forbidden) >= 0 || component.Any(char.IsControl))
        {
            return "caractère interdit par Windows";
        }
        if (component.EndsWith('.') || component.EndsWith(' '))
        {
            return "un nom ne peut pas finir par un point ou une espace";
        }
        if (IsReserved(component))
        {
            return "nom réservé par Windows";
        }
        if (Encoding.UTF8.GetByteCount(component) > MaxComponentBytes)
        {
            return "nom trop long";
        }
        return null;
    }

    /// <summary>Noms réservés par Windows, quelle que soit l'extension (« CON.md » compris).</summary>
    private static bool IsReserved(string name)
    {
        var dot = name.IndexOf('.', StringComparison.Ordinal);
        var stem = (dot >= 0 ? name[..dot] : name).TrimEnd();
        return Reserved.Any(reserved => string.Equals(reserved, stem, StringComparison.OrdinalIgnoreCase));
    }
}
