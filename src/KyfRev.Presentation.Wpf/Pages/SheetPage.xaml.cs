using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using KyfRev.DesignSystem.Controls;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Extensions;
using KyfRev.Presentation.Wpf.Reading;
using KyfRev.Rendering;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue d'une fiche ouverte pour la lire et la réviser : son plan, son texte et ses passages.</summary>
internal sealed partial class SheetPage : UserControl
{
    private const double ListenLabelWidth = 520;
    private const double ShareLabelWidth = 1000;
    private const int WordCardLength = 600;

    private IReadOnlyList<SpokenBlock> _spoken = [];
    private object? _pressedOn;

    public SheetPage()
    {
        InitializeComponent();
        Ribbon.ChooseImages = EditorView.ChooseImages;
        AddPanelsToRibbon();
        Unloaded += (_, _) => (DataContext as SheetPageViewModel)?.ReadAloud?.CloseCommand.Execute(null);
        EditorView.CoveredTextClicked = () => (DataContext as SheetPageViewModel)?.CloseOverlayCommand.Execute(null);
        SizeChanged += (_, e) =>
        {
            // Quand la place manque, « Écouter » se réduit à son icône : les autres boutons restent lisibles.
            ListenLabel.Visibility = e.NewSize.Width < ListenLabelWidth ? Visibility.Collapsed : Visibility.Visible;

            // « Partager » aussi, plutôt que de faire passer les modes à la ligne.
            ShareLabel.Visibility = e.NewSize.Width < ShareLabelWidth ? Visibility.Collapsed : Visibility.Visible;

            // Et les boutons du rattachement passent sous leur consigne, plutôt que de l'écraser.
            DockPanel.SetDock(RebindButtons, e.NewSize.Width < ListenLabelWidth ? Dock.Bottom : Dock.Right);
            RebindButtons.Margin = e.NewSize.Width < ListenLabelWidth ? new Thickness(0, 8, 0, 0) : default;
            if (DataContext is SheetPageViewModel page)
            {
                page.PageWidth = e.NewSize.Width;
            }
        };
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is SheetPageViewModel previous)
            {
                previous.PropertyChanged -= OnPageChanged;
                previous.Sharing?.PropertyChanged -= OnSharingChanged;
                if (previous.ReadAloud is { } reader)
                {
                    reader.PropertyChanged -= OnReadAloudChanged;
                    reader.CloseCommand.Execute(null);
                }
            }
            if (e.NewValue is SheetPageViewModel page)
            {
                page.PageWidth = ActualWidth;
                page.PropertyChanged += OnPageChanged;
                page.Sharing?.PropertyChanged += OnSharingChanged;
                page.ReadAloud?.PropertyChanged += OnReadAloudChanged;
                UpdateListenToggle(page);
                ShowWantedPassage(page);
            }
        };
    }

    /// <summary>Le texte mis en forme a sa propre fenêtre : tout ce qui le recouvre le lui dit.</summary>
    private void OnPageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SheetPageViewModel.Reading) or nameof(SheetPageViewModel.IsReading) or nameof(SheetPageViewModel.IsRevising) && sender is SheetPageViewModel listened)
        {
            listened.ReadAloud?.CloseCommand.Execute(null);
            UpdateListenToggle(listened);
        }
        if (e.PropertyName == nameof(SheetPageViewModel.Reading) && sender is SheetPageViewModel opened)
        {
            ShowWantedPassage(opened);
        }
        if (e.PropertyName is nameof(SheetPageViewModel.Reading) or nameof(SheetPageViewModel.IsRevising) && sender is SheetPageViewModel { IsReading: true, Holes.IsActive: true })
        {
            // Entrée et Espace affichent le trou suivant : ils doivent aller au texte, et non au bouton « Réviser ».
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () => SheetText.Focus());
        }
        if (e.PropertyName is nameof(SheetPageViewModel.IsEditing) or nameof(SheetPageViewModel.IsDeleting)
            or nameof(SheetPageViewModel.ShowsOutlineOverlay) or nameof(SheetPageViewModel.ShowsPassagesOverlay))
        {
            UpdateCovered((SheetPageViewModel)sender!);
        }
    }

    /// <summary>Les fenêtres de partage se posent aussi sur le texte mis en forme.</summary>
    private void OnSharingChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SheetSharingViewModel.IsShowing) && DataContext is SheetPageViewModel page)
        {
            UpdateCovered(page);
        }
    }

    private void UpdateCovered(SheetPageViewModel page) =>
        EditorView.PageCovered = page.IsEditing || page.IsDeleting || page.ShowsOutlineOverlay || page.ShowsPassagesOverlay || page.Sharing is { IsShowing: true };

    /// <summary>La réponse à la confirmation (révision en avance, J0, archivage) part vers la barre de révision.</summary>
    private void OnConfirmationClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is SheetPageViewModel { Review: { } review })
        {
            review.AnswerCommand.Execute(e.Choice == DialogChoice.Primary);
        }
    }

    /// <summary>Revenir au texte referme le volet ouvert par-dessus.</summary>
    private void OnTextPressed(object sender, MouseButtonEventArgs e)
    {
        TextPressed(e.OriginalSource);
    }

    /// <summary>L'appui sur le texte : retient ce qu'il vise, et referme le volet ouvert par-dessus.</summary>
    internal void TextPressed(object? source)
    {
        _pressedOn = source;
        if (DataContext is SheetPageViewModel page)
        {
            page.CloseOverlayCommand.Execute(null);
        }
    }

    private void OnOutlineEntryClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SheetPageViewModel page && ((FrameworkElement)sender).DataContext is OutlineEntry entry)
        {
            page.CloseOverlayCommand.Execute(null);
            if (page.Editor is { } editor)
            {
                editor.GoToHeading(entry.Title, page.Outline.TakeWhile(other => !ReferenceEquals(other, entry)).Count(other => other.Title == entry.Title));
            }
            else
            {
                SheetText.Show(entry.Heading);
            }
        }
    }

    private void OnPassageEntryClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SheetPageViewModel page && ((FrameworkElement)sender).DataContext is PassageEntry entry)
        {
            ShowPassage(page, entry);
        }
    }

    /// <summary>On écoute ce qu'on lit. Pendant une révision à trous, la voix dirait ce qui est masqué.</summary>
    private void UpdateListenToggle(SheetPageViewModel page) =>
        ListenToggle.Visibility = page is { ReadAloud: not null, IsReading: true, Holes.IsActive: false } ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// À l'écriture, en barre courte, le plan et les passages passent sous le « ⋯ » du ruban, après l'affichage : l'en-tête
    /// tient sur une ligne. Ils suivent la page, quelle qu'elle soit.
    /// </summary>
    private void AddPanelsToRibbon()
    {
        var visible = new BooleanToVisibilityConverter();
        var separator = new Separator();
        separator.SetBinding(VisibilityProperty, new Binding("DataContext.HasPanels") { Source = this, Converter = visible, FallbackValue = Visibility.Collapsed });
        var outline = new MenuItem { Header = ReadingText.Outline, IsCheckable = true };
        outline.SetBinding(MenuItem.IsCheckedProperty, new Binding("DataContext.IsOutlineOn") { Source = this, Mode = BindingMode.TwoWay });
        outline.SetBinding(VisibilityProperty, new Binding("DataContext.HasOutline") { Source = this, Converter = visible, FallbackValue = Visibility.Collapsed });
        var passages = new MenuItem { IsCheckable = true };
        passages.SetBinding(HeaderedItemsControl.HeaderProperty, new Binding("DataContext.PassagesLabel") { Source = this });
        passages.SetBinding(MenuItem.IsCheckedProperty, new Binding("DataContext.IsPassagesOn") { Source = this, Mode = BindingMode.TwoWay });
        passages.SetBinding(VisibilityProperty, new Binding("DataContext.HasPassages") { Source = this, Converter = visible, FallbackValue = Visibility.Collapsed });
        Ribbon.ViewMenu.Items.Add(separator);
        Ribbon.ViewMenu.Items.Add(outline);
        Ribbon.ViewMenu.Items.Add(passages);
        var share = new MenuItem { Header = SharingText.Share };
        share.SetBinding(MenuItem.CommandProperty, new Binding("DataContext.RequestShareCommand") { Source = this });
        share.SetBinding(VisibilityProperty, new Binding("DataContext.CanShare") { Source = this, Converter = visible, FallbackValue = Visibility.Collapsed });
        Ribbon.ViewMenu.Items.Add(share);
    }

    /// <summary>« ⋯ » de l'en-tête étroit : le plan et les passages, dans un menu ouvert sous le bouton.</summary>
    private void OnHeaderMore(object sender, RoutedEventArgs e)
    {
        HeaderMenu.PlacementTarget = HeaderMoreButton;
        HeaderMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        HeaderMenu.IsOpen = true;
    }

    private void OnListenToggle(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SheetPageViewModel { ReadAloud: { } reader })
        {
            return;
        }
        if (reader.IsOpen)
        {
            reader.CloseCommand.Execute(null);
        }
        else
        {
            _spoken = SheetText.SpokenBlocks();
            reader.Open([.. _spoken.Select(block => block.Text)]);
        }
    }

    /// <summary>Le bloc en cours ressort dans le texte.</summary>
    private void OnReadAloudChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReadAloudViewModel.Current) && sender is ReadAloudViewModel reader)
        {
            SheetText.MarkSpoken(reader.Current is { } index && index < _spoken.Count ? _spoken[index].Block : null);
        }
    }

    private void OnRateMenu(object sender, RoutedEventArgs e)
    {
        if (DataContext is SheetPageViewModel { ReadAloud: { } reader })
        {
            OpenChoices((Button)sender, ReadAloudViewModel.Rates, reader.Rate, rate => rate.Label, rate => reader.Rate = rate);
        }
    }

    private void OnVoiceMenu(object sender, RoutedEventArgs e)
    {
        if (DataContext is SheetPageViewModel { ReadAloud: { } reader })
        {
            OpenChoices((Button)sender, reader.Voices, reader.Voice, ReadingText.ShortVoice, voice => reader.Voice = voice);
        }
    }

    /// <summary>Un menu de choix sous son bouton, celui du moment coché.</summary>
    private static void OpenChoices<T>(Button button, IReadOnlyList<T> choices, T? current, Func<T, string> label, Action<T> choose)
    {
        var menu = new ContextMenu { PlacementTarget = button, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var choice in choices)
        {
            var item = new MenuItem { Header = label(choice), IsCheckable = true, IsChecked = EqualityComparer<T>.Default.Equals(choice, current) };
            item.Click += (_, _) => choose(choice);
            menu.Items.Add(item);
        }
        button.ContextMenu = menu;
        menu.IsOpen = choices.Count > 0;
    }

    /// <summary>La fiche s'ouvre sur le passage demandé : elle peut être lue avant ou après que la vue la montre.</summary>
    private void ShowWantedPassage(SheetPageViewModel page)
    {
        if (page is { Reading: not null, RangeToShow: { } range })
        {
            page.RangeToShow = null;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => SheetText.Select(range.Start, range.End));
        }
        if (page is { Reading: not null, PassageToShow: { } wanted })
        {
            page.PassageToShow = null;
            if (page.Passages.FirstOrDefault(entry => entry.Annotation.Id == wanted) is { } passage)
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () => ShowPassage(page, passage));
            }
        }
    }

    private void ShowPassage(SheetPageViewModel page, PassageEntry entry)
    {
        page.CloseOverlayCommand.Execute(null);
        // Afficher un passage masqué, c'est d'abord le découvrir : demander à le voir vaut pour réponse.
        page.Holes.RevealPassage(entry.Annotation.Id);
        page.ActivateCommand.Execute(entry.Annotation.Id);
        if (entry.Position is { } position && page.IsReading)
        {
            SheetText.Show(position.Start);
        }
    }

    /// <summary>Touche H : surligne la sélection, comme dans la 0.x.</summary>
    private void OnTextKey(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space && Keyboard.Modifiers == ModifierKeys.None && SheetText.RevealNextHole())
        {
            e.Handled = true;
            return;
        }
        if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.None && DataContext is SheetPageViewModel page && SelectedPosition() is { } position)
        {
            e.Handled = true;
            page.HighlightCommand.Execute(position);
        }
    }

    /// <summary>Un clic sur un passage surligné le fait ressortir dans le panneau.</summary>
    private void OnTextReleased(object sender, MouseButtonEventArgs e) => TextReleased(e.OriginalSource);

    /// <summary>Le relâchement sur le texte.</summary>
    internal void TextReleased(object? source)
    {
        // Le texte prend la souris dès l'appui : au relâchement, seul l'appui dit ce qui est visé. Tirer pour sélectionner n'est
        // pas un clic.
        var target = _pressedOn ?? source;
        _pressedOn = null;
        if (SheetText.Selection is { IsEmpty: false })
        {
            return;
        }
        // Le texte ne change qu'après : il ne rend la souris que si sa mise en page est encore valable à ce moment-là, et le
        // clic suivant serait perdu.
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => TextClicked(target));
    }

    private void TextClicked(object? target)
    {
        if (SheetText.RevealHoleAt(target))
        {
            return;
        }
        if (DataContext is not SheetPageViewModel page)
        {
            return;
        }
        if (SheetText.AnnotationsAt(target) is [var id, ..])
        {
            page.ActivateCommand.Execute(id);
        }
        else if (SheetText.WordAt(target) is { } word)
        {
            ShowWord(page, word);
        }
    }

    /// <summary>La définition d'un mot marqué, montrée là où il a été cliqué, avec ce qu'on peut en faire.</summary>
    internal void ShowWord(SheetPageViewModel page, DefinedWordMark word)
    {
        var panel = new StackPanel { MaxWidth = 380 };
        var term = new TextBlock { Text = word.Term, TextWrapping = TextWrapping.Wrap };
        term.SetResourceReference(StyleProperty, "Kyf.Text.BodyStrong");
        panel.Children.Add(term);
        var explanation = word.Explanation.Length > WordCardLength
            ? string.Concat(word.Explanation.AsSpan(0, WordCardLength).TrimEnd(), "\u2026")
            : word.Explanation;
        var body = new TextBlock { Text = explanation, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
        body.SetResourceReference(StyleProperty, "Kyf.Text.Body");
        panel.Children.Add(body);
        var source = new TextBlock { Text = ReadingText.DefinedIn(word.SheetTitle), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        source.SetResourceReference(StyleProperty, "Kyf.Text.Caption");
        panel.Children.Add(source);
        var actions = new WrapPanel { Margin = new Thickness(-6, 8, 0, 0) };
        var see = WordButton(ReadingText.SeeWordDefinition(word.Term), () => page.OpenWordDefinitionCommand.Execute(word));
        actions.Children.Add(see);
        actions.Children.Add(WordButton(ReadingText.StopMarkingWord(word.Term), () => page.StopMarkingWordCommand.Execute(word)));
        panel.Children.Add(actions);
        var card = new Card { Content = panel, Padding = new Thickness(14, 12, 14, 10) };
        card.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && WordCard is { } open)
            {
                open.IsOpen = false;
                e.Handled = true;
            }
        };
        WordCard = new Popup { Child = card, StaysOpen = false, Placement = PlacementMode.MousePoint, PlacementTarget = SheetText, AllowsTransparency = true };
        WordCard.IsOpen = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => see.Focus());
    }

    /// <summary>La carte d'un mot marqué, ouverte ou refermée ; rien avant le premier clic sur un mot.</summary>
    internal Popup? WordCard { get; private set; }

    private Button WordButton(string text, Action action)
    {
        var button = new Button { Content = text, Margin = new Thickness(0, 0, 4, 2) };
        button.SetResourceReference(StyleProperty, "Kyf.Button.Subtle");
        button.Click += (_, _) =>
        {
            WordCard?.IsOpen = false;
            action();
        };
        return button;
    }

    /// <summary>Les entrées du clic droit sur un mot marqué.</summary>
    private List<Control> WordMenuItems(SheetPageViewModel page, DefinedWordMark word) =>
    [
        ActionMenus.Quick(ReadingText.ShowDefinition, Icons.Show, () => ShowWord(page, word)),
        Item(ReadingText.SeeWordDefinition(word.Term), () => page.OpenWordDefinitionCommand.Execute(word)),
        Item(ReadingText.StopMarkingWord(word.Term), () => page.StopMarkingWordCommand.Execute(word)),
    ];

    /// <summary>Le menu du texte : ce qu'on peut faire de la sélection, ou du passage visé.</summary>
    private void OnTextMenu(object sender, ContextMenuEventArgs e)
    {
        Fill(TextMenu, TextMenuItems(e.OriginalSource), e);
    }

    private void OnPassageEntryMenu(object sender, ContextMenuEventArgs e)
    {
        var button = (FrameworkElement)sender;
        if (DataContext is SheetPageViewModel page && button.DataContext is PassageEntry entry)
        {
            Fill(button.ContextMenu!, PassageMenuItems(page, entry), e);
        }
    }

    private static void Fill(ContextMenu menu, IReadOnlyList<Control> items, ContextMenuEventArgs e) => ActionMenus.Fill(menu, items, e);

    private static MenuItem Item(string header, Action action, string? shortcut = null) => ActionMenus.Item(header, action, shortcut);

    /// <summary>La position sélectionnée, si elle peut devenir un passage.</summary>
    private TextPosition? SelectedPosition() =>
        SheetText.CurrentSelection() is { } position && position.End - position.Start <= SheetPageViewModel.PassageMaximum ? position : null;

    internal IReadOnlyList<Control> TextMenuItems(object? source)
    {
        if (DataContext is not SheetPageViewModel page)
        {
            return [];
        }
        if (SelectedPosition() is { } position)
        {
            if (page.IsRebinding)
            {
                return [Item(ReadingText.RebindHere, () => page.RebindAtCommand.Execute(position)), Item(ReadingText.RebindCancel, () => page.CancelRebindingCommand.Execute(null))];
            }
            var guided = page.Guided.IsActive ? [Item(ReadingText.GuidedTakeFor(page.Guided.Extension), () => Take(page, position)), new Separator()] : new List<Control>();
            return
            [
                .. guided,
                ActionMenus.Quick(ReadingText.Copy, Icons.Copy, ApplicationCommands.Copy, SheetText),
                ActionMenus.Quick(ReadingText.Highlight, Icons.Highlighter, () => page.HighlightCommand.Execute(position)),
                ActionMenus.Quick(ReadingText.Annotate, Icons.Comment, () => page.StartAnnotatingCommand.Execute(position)),
                ActionMenus.Quick(ReadingText.Define, Icons.Dictionary, () => page.StartDefiningCommand.Execute(position)),
                .. ExtensionItems(page, position),
                new Separator(),
                .. ListenItems(page, SheetText.Selection?.Start),
                EditSheetItem(page),
                .. SharingItems(page),
            ];
        }
        var entry = page.Passages.FirstOrDefault(candidate => SheetText.AnnotationsAt(source).Contains(candidate.Annotation.Id));
        if (entry is not null)
        {
            return [.. PassageMenuItems(page, entry), new Separator(), EditSheetItem(page), .. SharingItems(page)];
        }
        var listen = ListenItems(page, (source as TextElement)?.ContentStart);
        return SheetText.WordAt(source) is { } word
            ? [.. WordMenuItems(page, word), new Separator(), .. listen, EditSheetItem(page), .. SharingItems(page)]
            : [.. listen, EditSheetItem(page), .. SharingItems(page)];
    }

    /// <summary>« Partager… » et « Enregistrer comme modèle… », à la fin du menu du texte, comme dans la 0.x.</summary>
    private static List<Control> SharingItems(SheetPageViewModel page) => page.CanShare
        ?
        [
            Item(SharingText.Share, () => page.RequestShareCommand.Execute(null)),
            Item(SharingText.SaveAsTemplate, () => page.RequestSaveAsTemplateCommand.Execute(null)),
        ]
        : [];

    /// <summary>Les entrées que les extensions ajoutent au clic droit d'un texte sélectionné, avec ce texte et sa place.</summary>
    private List<Control> ExtensionItems(SheetPageViewModel page, TextPosition position)
    {
        if (page.Reading is not { } reading || TryFindResource(ExtensionPages.ResourceKey) is not ExtensionPages pages)
        {
            return [];
        }
        var context = new Dictionary<string, string>
        {
            ["id"] = page.SheetId,
            ["titre"] = reading.Title,
            ["dossier"] = reading.Folder,
            ["texte"] = reading.Map.Text[position.Start..position.End],
            ["debut"] = position.Start.ToString(CultureInfo.InvariantCulture),
            ["fin"] = position.End.ToString(CultureInfo.InvariantCulture),
        };
        var entries = pages.MenuEntries(ExtensionMenuZone.Selection, context);
        return entries.Count == 0 ? [] : [new Separator(), .. entries.Select(entry => Item(entry.Label, entry.Run))];
    }

    /// <summary>« Prendre la sélection » : le texte sélectionné part à l'extension qui l'attend.</summary>
    private void OnGuidedTake(object sender, RoutedEventArgs e)
    {
        if (DataContext is SheetPageViewModel page)
        {
            if (SheetText.CurrentSelection() is { } position && position.End > position.Start)
            {
                Take(page, position);
            }
            else
            {
                page.Guided.NeedsSelection();
            }
        }
    }

    private static void Take(SheetPageViewModel page, TextPosition position)
    {
        if (page.Reading is { } reading)
        {
            page.Guided.Take(page.SheetId, reading.Title, position.Start, position.End, reading.Map.Text[position.Start..position.End]);
        }
    }

    /// <summary>« Écouter d'ici », là où l'écoute a un sens.</summary>
    private List<Control> ListenItems(SheetPageViewModel page, TextPointer? place) =>
        page is { ReadAloud: not null, IsReading: true, Holes.IsActive: false } && place is not null
            ? [Item(ReadingText.ListenFromHere, () => ListenFrom(page.ReadAloud, place))]
            : [];

    /// <summary>Ouvre le lecteur et lit à partir du bloc qui contient cet endroit.</summary>
    internal void ListenFrom(ReadAloudViewModel reader, TextPointer place)
    {
        _spoken = SheetText.SpokenBlocks();
        reader.Open([.. _spoken.Select(block => block.Text)]);
        var index = _spoken.ToList().FindIndex(block => block.Block.ContentEnd.CompareTo(place) >= 0);
        reader.PlayFrom(Math.Max(index, 0));
    }

    private static MenuItem EditSheetItem(SheetPageViewModel page) =>
        Item(EditingText.EditSheet, () => page.StartWritingCommand.Execute(null), EditingText.WriteKey);

    internal IReadOnlyList<Control> PassageMenuItems(SheetPageViewModel page, PassageEntry entry)
    {
        var items = new List<Control>();
        if (entry.Position is not null)
        {
            items.Add(ActionMenus.Quick(entry.IsDefinition ? ReadingText.ShowDefinition : ReadingText.ShowPassage, Icons.Show, () => ShowPassage(page, entry)));
        }
        items.Add(ActionMenus.Quick(entry.IsDefinition ? ReadingText.EditDefinition : ReadingText.EditComment, Icons.Comment, () => page.StartEditingCommand.Execute(entry)));
        items.Add(new Separator());
        if (entry.IsCandidate)
        {
            items.Add(Item(ReadingText.ConfirmPosition, () => page.ConfirmPositionCommand.Execute(entry)));
            items.Add(Item(ReadingText.KeepWithoutPosition, () => page.KeepWithoutPositionCommand.Execute(entry)));
        }
        items.Add(Item(entry.State == KyfRev.Application.Model.AnchorState.Anchored ? ReadingText.RebindOther : ReadingText.RebindLost, () => page.StartRebindingCommand.Execute(entry)));
        items.Add(new Separator());
        items.Add(Item(entry.IsDefinition ? ReadingText.DeleteDefinition : ReadingText.DeletePassage, () => page.RequestDeleteCommand.Execute(entry)));
        return items;
    }

    private void OnEditorClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is SheetPageViewModel page)
        {
            page.AnswerEditorCommand.Execute(e.Choice == DialogChoice.Primary);
        }
    }

    /// <summary>Le passage en attente prend pour place la sélection ; sans sélection, on le rappelle.</summary>
    private void OnRebindHere(object sender, RoutedEventArgs e)
    {
        if (DataContext is SheetPageViewModel page)
        {
            RebindAtSelection(page);
        }
    }

    private void RebindAtSelection(SheetPageViewModel page)
    {
        if (SelectedPosition() is { } position)
        {
            page.RebindAtCommand.Execute(position);
        }
        else
        {
            page.AskForRebindingSelection();
        }
    }

    private void OnRebindClosed(object sender, DialogClosedEventArgs e) =>
        (DataContext as SheetPageViewModel)?.AnswerRebindingCommand.Execute(e.Choice switch
        {
            DialogChoice.Primary => false,
            DialogChoice.Secondary => true,
            _ => (bool?)null,
        });

    private void OnRebindConfirmCancel(object sender, RoutedEventArgs e) => RebindConfirm.Close(DialogChoice.None);

    private void OnLinkClosed(object sender, DialogClosedEventArgs e) =>
        (DataContext as SheetPageViewModel)?.Editor?.AnswerLinkCommand.Execute(e.Choice == DialogChoice.Primary);

    private void OnHistoryClosed(object sender, DialogClosedEventArgs e) =>
        (DataContext as SheetPageViewModel)?.Editor?.CloseHistoryCommand.Execute(null);

    private void OnConflictCompareClosed(object sender, DialogClosedEventArgs e)
    {
        if ((DataContext as SheetPageViewModel)?.Editor is { IsComparingConflict: true } editor)
        {
            editor.CompareConflictCommand.Execute(null);
        }
    }

    private void OnNetworkChoiceClosed(object sender, DialogClosedEventArgs e) =>
        (DataContext as SheetPageViewModel)?.Editor?.AnswerNetworkChoiceCommand.Execute(e.Choice == DialogChoice.Primary);

    private void OnDiagramEditorClosed(object sender, DialogClosedEventArgs e) =>
        (DataContext as SheetPageViewModel)?.Editor?.AnswerDiagramEditorCommand.Execute(e.Choice == DialogChoice.Primary);

    private void OnGalleryClosed(object sender, DialogClosedEventArgs e) =>
        (DataContext as SheetPageViewModel)?.Editor?.AnswerGalleryCommand.Execute(e.Choice == DialogChoice.Primary);

    private void OnFormulaClosed(object sender, DialogClosedEventArgs e) =>
        (DataContext as SheetPageViewModel)?.Editor?.AnswerFormulaCommand.Execute(e.Choice == DialogChoice.Primary);

    private void OnDeleteClosed(object sender, DialogClosedEventArgs e)
    {
        if (DataContext is SheetPageViewModel page)
        {
            page.AnswerDeleteCommand.Execute(e.Choice == DialogChoice.Primary);
        }
    }
}

/// <summary>Le retrait d'un titre du plan, selon son niveau.</summary>
public sealed class IndentConverter : IValueConverter
{
    private const double Step = 12;

    private IndentConverter()
    {
    }

    /// <summary>L'unique instance.</summary>
    public static IndentConverter Instance { get; } = new();

    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        new Thickness((int)value * Step, 0, 0, 0);

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
