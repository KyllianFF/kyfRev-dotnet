using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Une fiche ajoutée à une liste (rangée 18C des maquettes) : elle y arrive en fondu, en montant un peu, à sa place ; la
/// liste ne saute pas. Les listes de l'application sont reconstruites entières à chaque changement : on reconnaît une
/// ligne nouvelle à sa clé (l'identifiant de la fiche), jamais vue depuis que la liste est montrée. La première liste
/// montrée, un filtre qui en fait reparaître, une liste remplie d'un coup ne s'animent pas.
/// </summary>
public static partial class Transitions
{
    /// <summary>De combien une ligne ajoutée à une liste monte en arrivant.</summary>
    public const double NewcomerRise = 6;

    /// <summary>Au-delà, ce n'est plus un ajout mais une liste qui change : rien ne s'anime.</summary>
    public const int MostNewcomers = 3;

    /// <summary>
    /// Sur une liste : le chemin, dans chaque ligne, de la clé qui la reconnaît (« Sheet.Id ») ; une ligne dont la clé
    /// n'a jamais été vue arrive en fondu. Vide : rien ne s'anime.
    /// </summary>
    public static readonly DependencyProperty NewcomerKeyProperty =
        DependencyProperty.RegisterAttached("NewcomerKey", typeof(string), typeof(Transitions), new PropertyMetadata(string.Empty, OnNewcomerKey));

    /// <summary>
    /// Les clés vues, par modèle de la page (son contexte de données) : la vue d'une page est refaite à chaque visite, son
    /// modèle reste. Une fiche créée dans l'éditeur arrive ainsi quand on revient au Carnet.
    /// </summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, Dictionary<string, HashSet<object>>> SeenByOwner = [];

    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> KeyProperties = new();

    /// <summary>Lit <see cref="NewcomerKeyProperty"/>.</summary>
    public static string GetNewcomerKey(DependencyObject element) => (string)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(NewcomerKeyProperty);

    /// <summary>Écrit <see cref="NewcomerKeyProperty"/>.</summary>
    public static void SetNewcomerKey(DependencyObject element, string value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(NewcomerKeyProperty, value);

    private static void OnNewcomerKey(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not ItemsControl list)
        {
            return;
        }
        list.ItemContainerGenerator.ItemsChanged -= NewcomerHandlers.Of(list);
        if (e.NewValue is string { Length: > 0 })
        {
            list.ItemContainerGenerator.ItemsChanged += NewcomerHandlers.Of(list);
            // Une vue neuve d'un modèle déjà vu : ses lignes nouvelles arrivent ; sinon, ce sont celles qu'on trouve en arrivant.
            OnListChanged(list);
        }
    }

    /// <summary>La liste a changé : une fois ses lignes à leur place, celles qu'on n'avait jamais vues arrivent.</summary>
    private static void OnListChanged(ItemsControl list) =>
        list.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => Welcome(list));

    /// <summary>
    /// Les lignes nouvelles de la liste (une à trois, une liste déjà montrée) arrivent en fondu en montant ; toutes sont
    /// désormais vues.
    /// </summary>
    internal static int Welcome(ItemsControl list)
    {
        var path = GetNewcomerKey(list);
        if (path.Length == 0)
        {
            return 0;
        }
        // La première liste qui a des lignes est celle qu'on trouve en arrivant : on la retient, sans rien animer.
        if (SeenOf(list) is not { } seen)
        {
            Remember(list);
            return 0;
        }
        var newcomers = Keyed(list.Items, path).Where(entry => !seen.Contains(entry.Key)).ToList();
        if (newcomers.Count is > 0 and <= MostNewcomers && !list.IsVisible)
        {
            // La liste est cachée (la page s'ouvre encore) : les nouvelles lignes attendent qu'elle se montre pour arriver.
            seen.UnionWith(Keyed(list.Items, path).Select(entry => entry.Key).Except(newcomers.Select(entry => entry.Key)));
            list.IsVisibleChanged -= VisibleHandlers.Of(list);
            list.IsVisibleChanged += VisibleHandlers.Of(list);
            return 0;
        }
        Remember(list);
        if (newcomers.Count is 0 or > MostNewcomers)
        {
            return 0;
        }
        var welcomed = 0;
        foreach (var (item, _) in newcomers)
        {
            if (list.ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement row && Enter(row, DesignKeys.NormalDuration, rise: NewcomerRise, kind: MotionKinds.Lists))
            {
                welcomed++;
            }
        }
        return welcomed;
    }

    /// <summary>Toutes les clés que la liste montre sont vues.</summary>
    private static void Remember(ItemsControl list)
    {
        var keys = Keyed(list.Items, GetNewcomerKey(list)).Select(entry => entry.Key).ToList();
        if (SeenOf(list) is { } seen)
        {
            seen.UnionWith(keys);
        }
        else if (keys.Count > 0)
        {
            // Une liste vide n'est pas encore celle qu'on trouve en arrivant : elle se charge peut-être.
            SeenByOwner.GetValue(OwnerOf(list), _ => [])[GetNewcomerKey(list)] = [.. keys];
        }
    }

    /// <summary>Les clés déjà vues par le modèle de cette liste, pour son chemin de clé ; aucune s'il n'a encore rien montré.</summary>
    private static HashSet<object>? SeenOf(ItemsControl list) =>
        SeenByOwner.TryGetValue(OwnerOf(list), out var byPath) && byPath.TryGetValue(GetNewcomerKey(list), out var seen) ? seen : null;

    /// <summary>À qui appartiennent les clés vues : le modèle de la page, sinon la liste elle-même.</summary>
    private static object OwnerOf(ItemsControl list) => list.DataContext ?? list;

    /// <summary>La liste se montre : les lignes qui attendaient arrivent ; on ne l'attend plus.</summary>
    private static void OnListShown(ItemsControl list)
    {
        if (list.IsVisible)
        {
            list.IsVisibleChanged -= VisibleHandlers.Of(list);
            list.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => Welcome(list));
        }
    }

    /// <summary>Le gestionnaire « la liste se montre » de chaque liste, pour pouvoir le retirer.</summary>
    private static class VisibleHandlers
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ItemsControl, DependencyPropertyChangedEventHandler> Handlers = [];

        public static DependencyPropertyChangedEventHandler Of(ItemsControl list) => Handlers.GetValue(list, owner => (_, _) => OnListShown(owner));
    }

    /// <summary>Les lignes qui ont une clé (une rangée de dossier n'en a pas), avec leur clé.</summary>
    private static IEnumerable<(object Item, object Key)> Keyed(IEnumerable items, string path)
    {
        foreach (var item in items)
        {
            if (item is not null && KeyOf(item, path) is { } key)
            {
                yield return (item, key);
            }
        }
    }

    /// <summary>La clé d'une ligne : la valeur au bout du chemin (« Sheet.Id »), ou rien.</summary>
    internal static object? KeyOf(object item, string path)
    {
        var value = (object?)item;
        foreach (var name in path.Split('.'))
        {
            if (value is null)
            {
                return null;
            }
            var property = KeyProperties.GetOrAdd((value.GetType(), name), key => key.Item1.GetProperty(key.Item2, BindingFlags.Instance | BindingFlags.Public));
            value = property?.GetValue(value);
        }
        return value;
    }

    /// <summary>Le gestionnaire de chaque liste, pour pouvoir le retirer.</summary>
    private static class NewcomerHandlers
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ItemsControl, System.Windows.Controls.Primitives.ItemsChangedEventHandler> Handlers = [];

        public static System.Windows.Controls.Primitives.ItemsChangedEventHandler Of(ItemsControl list) => Handlers.GetValue(list, owner => (_, _) => OnListChanged(owner));
    }
}
