using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Shapes;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Controls;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Extensions;

/// <summary>Un clic ou un changement sur un élément <c>data-action</c> : le nom de l'action, sa valeur et les attributs <c>data-</c> de l'élément.</summary>
/// <param name="Name">Le nom de l'action.</param>
/// <param name="Value">Un texte, un booléen, les champs d'un formulaire envoyé (par leur <c>name</c>) ou rien.</param>
/// <param name="Data">Les attributs <c>data-</c> de l'élément.</param>
public sealed record ExtensionAction(string Name, object? Value, IReadOnlyDictionary<string, string> Data);

/// <summary>
/// Fait des éléments natifs de ce qu'une extension affiche : les mêmes styles que le reste de l'application, en clair comme en sombre.
/// Rien n'y vient de l'extension que du texte et des noms d'action : ni couleur, ni position, ni taille, ni adresse.
/// </summary>
internal sealed class ExtensionContentBuilder(Action<ExtensionAction> act)
{
    private const double BlockGap = 8;
    private const double FriseDateWidth = 170;
    private const double ListMarkerWidth = 24;
    private const double FieldWidth = 280;

    private FormScope? _form;
    private int _forms;

    /// <summary>Les contrôles qui déclenchent une action, dans l'ordre : le focus y revient après un nouveau rendu.</summary>
    public List<(string Action, Control Control)> Interactive { get; } = [];

    /// <summary>Le contenu, en éléments natifs.</summary>
    public FrameworkElement Build(ExtensionContent content) => Blocks(content.Nodes, TextKind.Body);

    private static bool IsInline(ExtensionNode node) =>
        node.Tag is ExtensionNode.TextTag or "span" or "time" or "small" or "strong" or "em" or "code" or "br" or "button" or "input" or "select" or "textarea"
        || (node.Tag == "label" && !node.Children.Any(HoldsControl));

    private static bool IsControl(ExtensionNode node) => node.Tag is "button" or "input" or "select" or "textarea";

    private static bool HoldsControl(ExtensionNode node) => IsControl(node) || node.Children.Any(HoldsControl);

    private static string KeyOf(TextKind kind) => kind switch
    {
        TextKind.Title => "Kyf.Text.Title",
        TextKind.Subtitle => "Kyf.Text.Subtitle",
        TextKind.Strong => "Kyf.Text.BodyStrong",
        TextKind.Secondary => "Kyf.Text.Secondary",
        TextKind.Caption => "Kyf.Text.Caption",
        _ => "Kyf.Text.Body",
    };

    private static TextKind KindOf(ExtensionNode node, TextKind inherited) =>
        node.Has("secondaire") ? TextKind.Secondary
        : node.Has("titre-extension") ? TextKind.Strong
        : node.Has("question") ? TextKind.Subtitle
        : inherited;

    private static TextBlock NewText(TextKind kind)
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap };
        text.SetResourceReference(FrameworkElement.StyleProperty, KeyOf(kind));
        return text;
    }

    private static Border Line(Thickness thickness)
    {
        var border = new Border { BorderThickness = thickness };
        border.SetResourceReference(Border.BorderBrushProperty, DesignKeys.Brush(ColorRole.Divider));
        return border;
    }

    private StackPanel Blocks(IEnumerable<ExtensionNode> nodes, TextKind kind)
    {
        var panel = new StackPanel();
        var inline = new List<ExtensionNode>();
        void Flush()
        {
            if (BuildRun(inline, kind) is { } run)
            {
                Add(panel, run);
            }
            inline.Clear();
        }
        foreach (var node in nodes)
        {
            if (IsInline(node))
            {
                inline.Add(node);
                continue;
            }
            Flush();
            Add(panel, Block(node, kind));
        }
        Flush();
        return panel;
    }

    private static void Add(StackPanel panel, FrameworkElement element)
    {
        if (panel.Children.Count > 0 && element.Margin.Top == 0)
        {
            element.Margin = new Thickness(element.Margin.Left, BlockGap, element.Margin.Right, element.Margin.Bottom);
        }
        panel.Children.Add(element);
    }

    private FrameworkElement Block(ExtensionNode node, TextKind kind) => node.Tag switch
    {
        "h1" => Heading(node, TextKind.Title, AutomationHeadingLevel.Level1),
        "h2" => Heading(node, TextKind.Subtitle, AutomationHeadingLevel.Level2),
        "h3" => Heading(node, TextKind.Strong, AutomationHeadingLevel.Level3),
        "h4" => Heading(node, TextKind.Strong, AutomationHeadingLevel.Level4),
        "h5" => Heading(node, TextKind.Strong, AutomationHeadingLevel.Level5),
        "h6" => Heading(node, TextKind.Strong, AutomationHeadingLevel.Level6),
        "ul" or "ol" => node.Has("frise") ? Frise(node, kind) : BuildList(node, kind),
        "table" => Table(node),
        "details" => Details(node, kind),
        "pre" => Preformatted(node),
        "hr" => Line(new Thickness(0, 1, 0, 0)),
        "form" => Form(node, kind),
        "label" => Labelled(node, kind),
        "progress" => Progress(node),
        _ => Container(node, kind),
    };

    /// <summary>
    /// Un formulaire : ses champs nommés partent ensemble, par leur <c>name</c>, quand on clique un bouton sans <c>data-action</c> ou
    /// qu'on appuie sur Entrée dans un champ d'une ligne. Un champ obligatoire vide garde le formulaire et reçoit le focus.
    /// </summary>
    private StackPanel Form(ExtensionNode node, TextKind kind)
    {
        var outer = _form;
        _form = new FormScope(node.Action, node.Data, ++_forms, act);
        var inner = Blocks(node.Children, kind);
        _form = outer;
        return inner;
    }

    /// <summary>
    /// Un libellé qui contient un champ : son texte au-dessus, le champ dessous, et le texte devient le nom du champ. Une case à
    /// cocher ou un bouton radio seul le prend comme texte, à côté de lui.
    /// </summary>
    private StackPanel Labelled(ExtensionNode node, TextKind kind)
    {
        var label = string.Concat(node.Children.Where(child => !HoldsControl(child)).Select(child => child.AllText)).Trim();
        var fields = Blocks(node.Children.Where(HoldsControl), kind);
        if (label.Length > 0 && Descendants(fields).OfType<Control>().ToList() is [ToggleButton { Content: null } choice])
        {
            choice.Content = label;
            AutomationProperties.SetName(choice, label);
            return fields;
        }
        var panel = new StackPanel();
        if (label.Length > 0)
        {
            var text = NewText(TextKind.Strong);
            text.Text = label;
            text.Margin = new Thickness(0, 0, 0, 4);
            panel.Children.Add(text);
        }
        // Le libellé nomme le champ, comme en HTML ; son indication reste une aide. Seul un aria-label passe avant lui.
        foreach (var field in Descendants(fields).OfType<Control>().Where(control => AutomationProperties.GetName(control) is var name
            && (string.IsNullOrEmpty(name) || (control is TextBox box && name == Field.GetPlaceholder(box)))))
        {
            AutomationProperties.SetName(field, label);
        }
        panel.Children.Add(fields);
        return panel;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var inner in Descendants(child))
            {
                yield return inner;
            }
        }
    }

    /// <summary>Une barre de progression : <c>value</c> sur <c>max</c> (1 par défaut), sans jamais sortir de la piste.</summary>
    private static ProgressBar Progress(ExtensionNode node)
    {
        static double Number(string? text) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) && value > 0 ? value : 0;
        var max = Number(node.Attribute("max")) is var read && read > 0 ? read : 1;
        var bar = new ProgressBar { Maximum = max, Value = Math.Min(Number(node.Attribute("value")), max), MinWidth = FieldWidth, HorizontalAlignment = HorizontalAlignment.Stretch };
        var label = node.Attribute("aria-label") is { Length: > 0 } named ? named : node.AllText.Trim();
        AutomationProperties.SetName(bar, label.Length > 0 ? label : string.Create(CultureInfo.CurrentCulture, $"{bar.Value} / {max}"));
        return bar;
    }

    /// <summary>Un paragraphe, un <c>div</c>, ou un élément hors de sa place (un <c>li</c> sans liste) : ce qu'il contient, avec ses classes.</summary>
    private FrameworkElement Container(ExtensionNode node, TextKind inherited)
    {
        var kind = KindOf(node, inherited);
        var inner = node.Has("actions") ? Actions(node, kind) : Blocks(node.Children, kind);
        if (node.Has("carte"))
        {
            return new Card { Padding = new Thickness(16), Margin = new Thickness(0, 12, 0, 4), Content = inner };
        }
        if (node.Has("reponse"))
        {
            var line = Line(new Thickness(0, 1, 0, 0));
            line.Margin = new Thickness(0, 10, 0, 0);
            line.Padding = new Thickness(0, 10, 0, 0);
            line.Child = inner;
            return line;
        }
        return node.Action is { } action && !node.Children.Any(IsControl) ? Clickable(node, inner, action) : inner;
    }

    private Button Clickable(ExtensionNode node, FrameworkElement inner, string action)
    {
        var button = new Button { Content = inner, HorizontalAlignment = HorizontalAlignment.Left };
        button.SetResourceReference(FrameworkElement.StyleProperty, "Kyf.Button.Subtle");
        AutomationProperties.SetName(button, node.AllText.Trim());
        button.Click += (_, _) => act(new ExtensionAction(action, node.Data.GetValueOrDefault("valeur"), node.Data));
        Interactive.Add((action, button));
        return button;
    }

    private TextBlock Heading(ExtensionNode node, TextKind kind, AutomationHeadingLevel level)
    {
        var text = NewText(kind);
        Fill(text, node.Children);
        AutomationProperties.SetHeadingLevel(text, level);
        if (level == AutomationHeadingLevel.Level2)
        {
            text.Margin = new Thickness(0, 16, 0, 0);
        }
        return text;
    }

    /// <summary>Une suite d'éléments en ligne ; rien s'il n'y a que des espaces.</summary>
    private FrameworkElement? BuildRun(List<ExtensionNode> nodes, TextKind kind)
    {
        if (nodes.All(node => node.Tag == ExtensionNode.TextTag && string.IsNullOrWhiteSpace(node.Text)))
        {
            return null;
        }
        if (!nodes.Any(IsControl))
        {
            var text = NewText(kind);
            Fill(text, nodes);
            return text;
        }
        var row = new WrapPanel();
        var waiting = new List<ExtensionNode>();
        void Flush()
        {
            if (BuildRun(waiting, kind) is { } run)
            {
                run.VerticalAlignment = VerticalAlignment.Center;
                run.Margin = new Thickness(0, 0, 8, 8);
                row.Children.Add(run);
            }
            waiting.Clear();
        }
        foreach (var node in nodes)
        {
            if (IsControl(node))
            {
                Flush();
                var control = BuildControl(node);
                control.Margin = new Thickness(0, 0, 8, 8);
                control.VerticalAlignment = VerticalAlignment.Center;
                row.Children.Add(control);
            }
            else
            {
                waiting.Add(node);
            }
        }
        Flush();
        return row;
    }

    /// <summary>Une rangée de boutons et de champs, qui passe à la ligne au lieu de déborder.</summary>
    private FrameworkElement Actions(ExtensionNode node, TextKind kind) =>
        BuildRun([.. node.Children], kind) ?? new StackPanel();

    private void Fill(TextBlock text, IEnumerable<ExtensionNode> nodes)
    {
        var list = nodes.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var node = list[i];
            // Les espaces aux bords d'un bloc ne comptent pas, comme en HTML.
            if (node.Tag == ExtensionNode.TextTag)
            {
                var value = node.Text;
                value = i == 0 ? value.TrimStart() : value;
                value = i == list.Count - 1 ? value.TrimEnd() : value;
                if (value.Length > 0)
                {
                    text.Inlines.Add(new Run(value));
                }
                continue;
            }
            text.Inlines.Add(BuildInline(node));
        }
    }

    private Inline BuildInline(ExtensionNode node)
    {
        if (node.Tag == ExtensionNode.TextTag)
        {
            return new Run(node.Text);
        }
        if (node.Tag == "br")
        {
            return new LineBreak();
        }
        if (IsControl(node))
        {
            return new InlineUIContainer(BuildControl(node));
        }
        var span = node.Tag switch
        {
            "strong" => new Bold(),
            "em" => new Italic(),
            _ => node.Action is not null ? new Hyperlink() : new Span(),
        };
        if (node.Tag == "code")
        {
            span.SetResourceReference(TextElement.FontFamilyProperty, DesignKeys.CodeFont);
        }
        if (node.Has("secondaire") || node.Has("pastille") || node.Tag == "small")
        {
            span.SetResourceReference(TextElement.ForegroundProperty, DesignKeys.Brush(ColorRole.TextSecondary));
        }
        if (span is Hyperlink link && node.Action is { } action)
        {
            link.SetResourceReference(TextElement.ForegroundProperty, DesignKeys.Brush(ColorRole.Link));
            link.Click += (_, _) => act(new ExtensionAction(action, node.Data.GetValueOrDefault("valeur"), node.Data));
        }
        foreach (var child in node.Children)
        {
            span.Inlines.Add(BuildInline(child));
        }
        return span;
    }

    private Control BuildControl(ExtensionNode node) => node.Tag switch
    {
        "button" => BuildButton(node),
        "select" => BuildSelect(node),
        "textarea" => BuildField(node, node.AllText, multiline: true),
        _ => BuildInput(node),
    };

    private Button BuildButton(ExtensionNode node)
    {
        // Dans un formulaire, un bouton sans action à lui l'envoie, comme en HTML ; type="button" l'en empêche.
        var submits = _form is { Action: not null } && node.Action is null && node.Attribute("type") is null or "submit";
        var button = new Button { Content = node.AllText.Trim(), IsEnabled = (node.Action is not null || submits) && node.Attribute("disabled") is null };
        var style = node.Has("principal") ? "Kyf.Button.Primary" : node.Has("discret") ? "Kyf.Button.Subtle" : "Kyf.Button";
        button.SetResourceReference(FrameworkElement.StyleProperty, style);
        if (node.Attribute("aria-label") is { Length: > 0 } label)
        {
            AutomationProperties.SetName(button, label);
        }
        if (node.Action is { } action)
        {
            button.Click += (_, _) => act(new ExtensionAction(action, node.Data.GetValueOrDefault("valeur"), node.Data));
            Interactive.Add((action, button));
        }
        else if (submits && _form is { Action: { } submit } form)
        {
            button.Click += (_, _) => form.Submit();
            Interactive.Add((submit, button));
        }
        return button;
    }

    private Control BuildInput(ExtensionNode node)
    {
        var type = node.Attribute("type")?.ToUpperInvariant();
        if (type is not ("CHECKBOX" or "RADIO"))
        {
            return BuildField(node, node.Attribute("value") ?? string.Empty, multiline: false);
        }
        ToggleButton choice = type == "RADIO" ? new RadioButton() : new CheckBox();
        choice.IsChecked = node.Attribute("checked") is not null;
        choice.IsEnabled = node.Attribute("disabled") is null;
        if (_form is { } form && node.Attribute("name") is { Length: > 0 } name)
        {
            if (choice is RadioButton radio)
            {
                // Les boutons radio d'un même nom vont ensemble, dans ce formulaire seulement.
                radio.GroupName = string.Create(CultureInfo.InvariantCulture, $"extension-{form.Number}-{name}");
                var value = node.Attribute("value") ?? "on";
                form.Add(name, () => radio.IsChecked == true ? value : null, radio, isMissing: null, choice: true);
            }
            else
            {
                form.Add(name, () => choice.IsChecked == true, choice, isMissing: null, choice: false);
            }
        }
        if (node.Attribute("aria-label") is { Length: > 0 } label)
        {
            choice.Content = label;
            AutomationProperties.SetName(choice, label);
        }
        if (node.Action is { } action)
        {
            void Changed(object sender, RoutedEventArgs e) => act(new ExtensionAction(action, choice.IsChecked == true, node.Data));
            choice.Checked += Changed;
            choice.Unchecked += Changed;
            Interactive.Add((action, choice));
        }
        return choice;
    }

    private TextBox BuildField(ExtensionNode node, string value, bool multiline)
    {
        var box = new TextBox
        {
            Text = value,
            MinWidth = FieldWidth,
            IsEnabled = node.Attribute("disabled") is null,
            AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
        };
        var placeholder = node.Attribute("placeholder") ?? string.Empty;
        Field.SetPlaceholder(box, placeholder);
        AutomationProperties.SetName(box, node.Attribute("aria-label") is { Length: > 0 } label ? label : placeholder);
        var form = _form;
        if (form is not null && node.Attribute("name") is { Length: > 0 } name)
        {
            var required = node.Attribute("required") is not null;
            AutomationProperties.SetIsRequiredForForm(box, required);
            form.Add(name, () => box.Text, box, required ? () => string.IsNullOrWhiteSpace(box.Text) : null, choice: false);
            if (!multiline && node.Action is null && form.Action is not null)
            {
                box.KeyDown += (_, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        e.Handled = true;
                        form.Submit();
                    }
                };
            }
        }
        if (node.Action is { } action)
        {
            var known = value;
            void Commit()
            {
                if (box.Text != known)
                {
                    known = box.Text;
                    act(new ExtensionAction(action, box.Text, node.Data));
                }
            }
            box.LostKeyboardFocus += (_, _) => Commit();
            box.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter && !multiline)
                {
                    e.Handled = true;
                    Commit();
                }
            };
            Interactive.Add((action, box));
        }
        return box;
    }

    private ComboBox BuildSelect(ExtensionNode node)
    {
        var options = node.Children.Where(child => child.Tag == "option").ToList();
        var box = new ComboBox
        {
            ItemsSource = options.Select(option => option.AllText.Trim()).ToList(),
            SelectedIndex = Math.Max(0, options.FindIndex(option => option.Attribute("selected") is not null)),
            IsEnabled = node.Attribute("disabled") is null,
        };
        AutomationProperties.SetName(box, node.Attribute("aria-label") ?? string.Empty);
        if (_form is { } form && node.Attribute("name") is { Length: > 0 } name && options.Count > 0)
        {
            form.Add(name, () => options[Math.Max(0, box.SelectedIndex)] is var option ? option.Attribute("value") ?? option.AllText.Trim() : null, box, isMissing: null, choice: false);
        }
        if (node.Action is { } action)
        {
            box.SelectionChanged += (_, _) =>
            {
                var option = options[box.SelectedIndex];
                act(new ExtensionAction(action, option.Attribute("value") ?? option.AllText.Trim(), node.Data));
            };
            Interactive.Add((action, box));
        }
        return box;
    }

    private Grid BuildList(ExtensionNode node, TextKind kind)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ListMarkerWidth) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var number = 0;
        foreach (var item in node.Children.Where(child => child.Tag == "li"))
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            number++;
            var marker = NewText(kind);
            marker.Text = node.Tag == "ol" ? string.Create(CultureInfo.CurrentCulture, $"{number}.") : "•";
            marker.Margin = new Thickness(0, 2, 0, 2);
            Grid.SetRow(marker, number - 1);
            grid.Children.Add(marker);
            var content = Blocks(item.Children, KindOf(item, kind));
            content.Margin = new Thickness(0, 2, 0, 2);
            Grid.SetRow(content, number - 1);
            Grid.SetColumn(content, 1);
            grid.Children.Add(content);
        }
        return grid;
    }

    /// <summary>Une frise : un trait à gauche, un point par date, la date dans sa colonne.</summary>
    private Border Frise(ExtensionNode node, TextKind kind)
    {
        var panel = new StackPanel();
        foreach (var item in node.Children.Where(child => child.Tag == "li"))
        {
            var row = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(FriseDateWidth) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var date = NewText(TextKind.Strong);
            date.Margin = new Thickness(18, 0, 8, 0);
            Fill(date, item.Children.Where(child => child.Tag == "time"));
            row.Children.Add(date);
            var rest = Blocks(item.Children.Where(child => child.Tag != "time"), kind);
            Grid.SetColumn(rest, 1);
            row.Children.Add(rest);
            var dot = new Ellipse { Width = 10, Height = 10, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(-6, 5, 0, 0) };
            dot.SetResourceReference(Shape.FillProperty, DesignKeys.Brush(ColorRole.Primary));
            row.Children.Add(dot);
            panel.Children.Add(row);
        }
        var line = Line(new Thickness(2, 0, 0, 0));
        line.Margin = new Thickness(6, 8, 0, 8);
        line.Child = panel;
        return line;
    }

    /// <summary>Un tableau : les colonnes se partagent la largeur et le texte passe à la ligne, sans jamais déborder.</summary>
    private Grid Table(ExtensionNode node)
    {
        var rows = new List<ExtensionNode>();
        void Collect(IEnumerable<ExtensionNode> children)
        {
            foreach (var child in children)
            {
                if (child.Tag == "tr")
                {
                    rows.Add(child);
                }
                else if (child.Tag is "thead" or "tbody")
                {
                    Collect(child.Children);
                }
            }
        }
        Collect(node.Children);
        var grid = new Grid();
        var columns = rows.Count == 0 ? 0 : rows.Max(row => row.Children.Count(cell => cell.Tag is "th" or "td"));
        for (var i = 0; i < columns; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        for (var r = 0; r < rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var c = 0;
            foreach (var cell in rows[r].Children.Where(child => child.Tag is "th" or "td"))
            {
                var border = Line(new Thickness(0, 0, 0, 1));
                border.Padding = new Thickness(10, 6, 10, 6);
                border.Child = Blocks(cell.Children, cell.Tag == "th" ? TextKind.Caption : TextKind.Body);
                Grid.SetRow(border, r);
                Grid.SetColumn(border, c);
                grid.Children.Add(border);
                c++;
            }
            for (; c < columns; c++)
            {
                var empty = Line(new Thickness(0, 0, 0, 1));
                Grid.SetRow(empty, r);
                Grid.SetColumn(empty, c);
                grid.Children.Add(empty);
            }
        }
        return grid;
    }

    /// <summary>Un <c>details</c> : son résumé ouvre ce qu'il cache, au clic et au clavier, sans passer par l'extension.</summary>
    private StackPanel Details(ExtensionNode node, TextKind kind)
    {
        var summary = node.Children.FirstOrDefault(child => child.Tag == "summary");
        var hidden = Blocks(node.Children.Where(child => child != summary), kind);
        var title = summary?.AllText.Trim() is { Length: > 0 } text ? text : ModulesSettingsText.ExtensionDetails;
        var open = node.Attribute("open") is not null;
        hidden.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        hidden.Margin = new Thickness(18, 4, 0, 0);
        var chevron = new Icon { Text = open ? Icons.ChevronDown : Icons.ChevronRight, FontSize = 10, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        var label = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        var header = new DockPanel();
        DockPanel.SetDock(chevron, Dock.Left);
        header.Children.Add(chevron);
        header.Children.Add(label);
        var toggle = new ToggleButton { Content = header, IsChecked = open, HorizontalAlignment = HorizontalAlignment.Left, HorizontalContentAlignment = HorizontalAlignment.Left };
        toggle.SetResourceReference(FrameworkElement.StyleProperty, "Kyf.Button.Subtle");
        AutomationProperties.SetName(toggle, title);
        toggle.Checked += (_, _) =>
        {
            hidden.Visibility = Visibility.Visible;
            chevron.Text = Icons.ChevronDown;
            label.FontWeight = FontWeights.SemiBold;
        };
        toggle.Unchecked += (_, _) =>
        {
            hidden.Visibility = Visibility.Collapsed;
            chevron.Text = Icons.ChevronRight;
            label.FontWeight = FontWeights.Normal;
        };
        var panel = new StackPanel();
        panel.Children.Add(toggle);
        panel.Children.Add(hidden);
        return panel;
    }

    private static TextBlock Preformatted(ExtensionNode node)
    {
        var text = NewText(TextKind.Body);
        text.SetResourceReference(TextBlock.FontFamilyProperty, DesignKeys.CodeFont);
        text.Text = node.AllText;
        return text;
    }

    /// <summary>Un formulaire en cours de construction : ses champs nommés, et ce qui l'envoie.</summary>
    private sealed class FormScope(string? action, IReadOnlyDictionary<string, string> data, int number, Action<ExtensionAction> act)
    {
        private readonly List<(string Name, Func<object?> Value, Control Control, Func<bool>? IsMissing, bool Choice)> _fields = [];

        public string? Action => action;

        public int Number => number;

        public void Add(string name, Func<object?> value, Control control, Func<bool>? isMissing, bool choice) =>
            _fields.Add((name, value, control, isMissing, choice));

        /// <summary>Envoie les champs, par leur nom ; un champ obligatoire vide reçoit le focus à la place.</summary>
        public void Submit()
        {
            if (action is null)
            {
                return;
            }
            if (_fields.FirstOrDefault(field => field.IsMissing?.Invoke() == true) is { Control: { } missing })
            {
                missing.Focus();
                return;
            }
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var field in _fields)
            {
                var value = field.Value();
                // Un groupe de boutons radio vaut celui qui est coché ; aucun, rien.
                if (!field.Choice || value is not null || !values.ContainsKey(field.Name))
                {
                    values[field.Name] = value;
                }
            }
            act(new ExtensionAction(action, values, data));
        }
    }

    private enum TextKind
    {
        Body,
        Title,
        Subtitle,
        Strong,
        Secondary,
        Caption,
    }
}
