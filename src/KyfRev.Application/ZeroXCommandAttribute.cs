namespace KyfRev.Application;

/// <summary>
/// Commande de kyfRev 0.x que ce cas d'usage reprend (<c>docs/correspondance-commandes.md</c>
/// en tient la liste complète, vérifiée par les tests).
/// </summary>
/// <param name="name">Nom de la commande Tauri (« valider_revision »).</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ZeroXCommandAttribute(string name) : Attribute
{
    /// <summary>Nom de la commande Tauri.</summary>
    public string Name { get; } = name;
}
