using System.Windows;
using System.Windows.Input;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>Une action qui a un raccourci clavier, que l'on peut changer.</summary>
/// <param name="Id">Son nom écrit, dans les préférences.</param>
/// <param name="Label">Ce qu'elle fait.</param>
/// <param name="Key">La touche d'origine.</param>
/// <param name="Modifiers">Les touches à tenir, d'origine.</param>
public sealed record ShortcutAction(string Id, string Label, Key Key, ModifierKeys Modifiers);

/// <summary>
/// Les raccourcis clavier de l'application que l'on peut changer, et ceux qui sont choisis sur cet ordinateur. Une vue
/// marque ses raccourcis (<see cref="IdProperty"/>) et les suit (<see cref="Follow"/>) : ils prennent la touche choisie,
/// maintenant et à chaque changement.
/// </summary>
public sealed class ShortcutMap
{
    /// <summary>Le nom de l'action d'un raccourci déclaré dans une vue.</summary>
    public static readonly DependencyProperty IdProperty = DependencyProperty.RegisterAttached(
        "Id", typeof(string), typeof(ShortcutMap), new PropertyMetadata(null));

    private readonly Dictionary<string, (Key Key, ModifierKeys Modifiers)> _chosen;

    private ShortcutMap(Dictionary<string, (Key Key, ModifierKeys Modifiers)> chosen)
    {
        _chosen = chosen;
    }

    /// <summary>Les actions dont le raccourci se change, avec leurs touches d'origine.</summary>
    public static IReadOnlyList<ShortcutAction> Actions { get; } =
    [
        new("recherche", ShellText.ShortcutSearch, Key.K, ModifierKeys.Control),
        new("aide", ShellText.ShortcutHelp, Key.F1, ModifierKeys.None),
        new("concentration", ShellText.ShortcutFocusMode, Key.F, ModifierKeys.Control | ModifierKeys.Shift),
        new("plein-ecran", ShellText.ShortcutFullScreen, Key.F11, ModifierKeys.None),
        new("valider", ShellText.ShortcutValidate, Key.Enter, ModifierKeys.Control),
        new("ecrire", ShellText.ShortcutWrite, Key.E, ModifierKeys.Control),
        new("imprimer", ShellText.ShortcutPrint, Key.P, ModifierKeys.Control),
        new("partager", ShellText.ShortcutShare, Key.P, ModifierKeys.Control | ModifierKeys.Shift),
        new("capture", ShellText.ShortcutCapture, Key.N, ModifierKeys.Control | ModifierKeys.Shift),
        new("verrouiller", ShellText.ShortcutLock, Key.L, ModifierKeys.Control | ModifierKeys.Shift),
    ];

    /// <summary>Les raccourcis en vigueur.</summary>
    public static ShortcutMap Current { get; private set; } = Parse(null);

    /// <summary>Les raccourcis en vigueur viennent de changer.</summary>
    public static event EventHandler? Changed;

    /// <summary>Le nom de l'action d'un raccourci.</summary>
    public static string? GetId(DependencyObject binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return (string?)binding.GetValue(IdProperty);
    }

    /// <summary>Donne à un raccourci le nom de son action.</summary>
    public static void SetId(DependencyObject binding, string? value)
    {
        ArgumentNullException.ThrowIfNull(binding);
        binding.SetValue(IdProperty, value);
    }

    /// <summary>Met en vigueur les raccourcis écrits dans les préférences ; ce qui ne se lit pas garde sa touche d'origine.</summary>
    public static void Use(string? written)
    {
        var next = Parse(written);
        if (next.Written() == Current.Written())
        {
            return;
        }
        Current = next;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Lit « imprimer=Ctrl+Shift+I;partager=Ctrl+J ».</summary>
    public static ShortcutMap Parse(string? written)
    {
        var chosen = new Dictionary<string, (Key Key, ModifierKeys Modifiers)>(StringComparer.Ordinal);
        foreach (var part in (written ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var at = part.IndexOf('=', StringComparison.Ordinal);
            if (at > 0 && Actions.Any(action => action.Id == part[..at]) && Read(part[(at + 1)..]) is var (key, modifiers))
            {
                chosen[part[..at]] = (key, modifiers);
            }
        }
        var map = new ShortcutMap(chosen);
        // Un choix égal à l'origine n'en est pas un.
        foreach (var action in Actions)
        {
            if (chosen.TryGetValue(action.Id, out var gesture) && gesture == (action.Key, action.Modifiers))
            {
                chosen.Remove(action.Id);
            }
        }
        // Deux actions sur la même touche : celle qui a choisi la sienne en dernier (ou la seule à l'avoir choisie) la rend.
        for (var settled = false; !settled;)
        {
            settled = true;
            for (var first = 0; first < Actions.Count && settled; first++)
            {
                for (var second = first + 1; second < Actions.Count && settled; second++)
                {
                    if (map.Of(Actions[first].Id) == map.Of(Actions[second].Id))
                    {
                        chosen.Remove(chosen.ContainsKey(Actions[second].Id) ? Actions[second].Id : Actions[first].Id);
                        settled = false;
                    }
                }
            }
        }
        return map;
    }

    /// <summary>La touche d'une action.</summary>
    public (Key Key, ModifierKeys Modifiers) Of(string id) =>
        _chosen.TryGetValue(id, out var chosen)
            ? chosen
            : Actions.FirstOrDefault(action => action.Id == id) is { } action ? (action.Key, action.Modifiers) : (Key.None, ModifierKeys.None);

    /// <summary>L'action a une autre touche que celle d'origine.</summary>
    public bool IsChanged(string id) => _chosen.ContainsKey(id);

    /// <summary>Les mêmes raccourcis, avec cette touche pour cette action.</summary>
    public ShortcutMap With(string id, Key key, ModifierKeys modifiers)
    {
        var chosen = new Dictionary<string, (Key Key, ModifierKeys Modifiers)>(_chosen, StringComparer.Ordinal) { [id] = (key, modifiers) };
        return Parse(new ShortcutMap(chosen).Written());
    }

    /// <summary>Les mêmes raccourcis, cette action reprenant sa touche d'origine ; sans nom, toutes.</summary>
    public ShortcutMap Without(string? id)
    {
        var chosen = new Dictionary<string, (Key Key, ModifierKeys Modifiers)>(_chosen, StringComparer.Ordinal);
        if (id is null)
        {
            chosen.Clear();
        }
        else
        {
            chosen.Remove(id);
        }
        return new ShortcutMap(chosen);
    }

    /// <summary>Ce qui empêche de donner cette touche à cette action : le nom de l'action qui l'a déjà ; rien si elle est libre.</summary>
    public string? Conflict(string id, Key key, ModifierKeys modifiers) =>
        Actions.FirstOrDefault(action => action.Id != id && Of(action.Id) == (key, modifiers))?.Label;

    /// <summary>Les choix, tels qu'ils s'écrivent dans les préférences : seulement ce qui diffère de l'origine.</summary>
    public string Written() =>
        string.Join(';', Actions.Where(action => _chosen.ContainsKey(action.Id)).Select(action => $"{action.Id}={Write(_chosen[action.Id].Key, _chosen[action.Id].Modifiers)}"));

    /// <summary>Une touche convient pour un raccourci : une touche de fonction, ou une touche tenue avec Ctrl ou Alt.</summary>
    public static bool IsUsable(Key key, ModifierKeys modifiers) =>
        key is not (Key.None or Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.System or Key.Escape or Key.Tab)
        && (key is >= Key.F1 and <= Key.F12 || (modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != 0);

    /// <summary>Une touche, telle qu'on la lit : « Ctrl+Maj+P ».</summary>
    public static string Display(Key key, ModifierKeys modifiers)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }
        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }
        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Maj");
        }
        parts.Add(key switch
        {
            Key.Enter => "Entrée",
            Key.Space => "Espace",
            Key.Right => "→",
            Key.Left => "←",
            Key.Up => "↑",
            Key.Down => "↓",
            Key.OemPlus or Key.Add => "plus",
            Key.OemMinus or Key.Subtract => "moins",
            >= Key.D0 and <= Key.D9 => ((int)key - (int)Key.D0).ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => key.ToString(),
        });
        return string.Join('+', parts);
    }

    /// <summary>La touche en vigueur d'une action, telle qu'on la lit.</summary>
    public string DisplayOf(string id) => Display(Of(id).Key, Of(id).Modifiers);

    /// <summary>
    /// Donne aux raccourcis marqués de cette vue les touches en vigueur, maintenant et à chaque changement tant que la vue
    /// est à l'écran.
    /// </summary>
    public static void Follow(FrameworkElement view)
    {
        ArgumentNullException.ThrowIfNull(view);
        // Une vue ne se touche que depuis son propre fil : un changement annoncé d'ailleurs attendra qu'elle se montre.
        void Apply(object? sender, EventArgs e)
        {
            if (view.Dispatcher.CheckAccess())
            {
                ApplyTo(view);
            }
        }
        view.Loaded += (_, _) =>
        {
            Changed -= Apply;
            Changed += Apply;
            ApplyTo(view);
        };
        view.Unloaded += (_, _) => Changed -= Apply;
        ApplyTo(view);
    }

    /// <summary>Donne aux raccourcis marqués d'une vue les touches en vigueur.</summary>
    public static void ApplyTo(UIElement view)
    {
        ArgumentNullException.ThrowIfNull(view);
        foreach (var binding in view.InputBindings.OfType<KeyBinding>())
        {
            if (GetId(binding) is { } id && Current.Of(id) is { Key: not Key.None } wanted && (binding.Key != wanted.Key || binding.Modifiers != wanted.Modifiers)
                && GestureOf(wanted.Key, wanted.Modifiers) is { } gesture)
            {
                binding.Gesture = gesture;
            }
        }
    }

    /// <summary>Le raccourci de ces touches ; rien si Windows ne l'accepte pas : celui d'origine reste alors en place.</summary>
    private static KeyGesture? GestureOf(Key key, ModifierKeys modifiers)
    {
        try
        {
            return new KeyGesture(key, modifiers);
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static string Write(Key key, ModifierKeys modifiers) =>
        string.Join('+', new[] { modifiers.HasFlag(ModifierKeys.Control) ? "Ctrl" : null, modifiers.HasFlag(ModifierKeys.Alt) ? "Alt" : null, modifiers.HasFlag(ModifierKeys.Shift) ? "Shift" : null, key.ToString() }.Where(part => part is not null));

    private static (Key Key, ModifierKeys Modifiers)? Read(string text)
    {
        var modifiers = ModifierKeys.None;
        var key = Key.None;
        foreach (var part in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part)
            {
                case "Ctrl":
                    modifiers |= ModifierKeys.Control;
                    break;
                case "Alt":
                    modifiers |= ModifierKeys.Alt;
                    break;
                case "Shift":
                    modifiers |= ModifierKeys.Shift;
                    break;
                default:
                    if (key != Key.None || !Enum.TryParse(part, out key) || !Enum.IsDefined(key))
                    {
                        return null;
                    }
                    break;
            }
        }
        return IsUsable(key, modifiers) ? (key, modifiers) : null;
    }
}
