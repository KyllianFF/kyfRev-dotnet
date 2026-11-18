using System.Windows;
using System.Windows.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Une aide en contexte, montrée la première fois qu'une chose arrive (la première révision, la première fiche écrite) :
/// un titre, deux phrases, « Compris ». Elle ne bloque rien, ne revient pas une fois lue, et « Ne plus montrer d'aide » les
/// coupe toutes. Ce qui a été lu est retenu par <see cref="FirstUse"/>, que la fenêtre transmet à ce qu'elle contient ;
/// sans lui (un essai, une vue seule), aucune aide ne se montre.
/// </summary>
internal sealed partial class FirstTip : UserControl
{
    /// <summary>Ce que la première utilisation a déjà montré : transmis par la fenêtre à tout ce qu'elle contient.</summary>
    public static readonly DependencyProperty MemoryProperty = DependencyProperty.RegisterAttached(
        "Memory", typeof(FirstUse), typeof(FirstTip), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, (element, _) => (element as FirstTip)?.Follow()));

    /// <summary>Ce que cette aide marque comme lu (voir <see cref="FirstUse"/>).</summary>
    public static readonly DependencyProperty MarkProperty = DependencyProperty.Register(
        nameof(Mark), typeof(string), typeof(FirstTip), new PropertyMetadata(string.Empty, (element, _) => ((FirstTip)element).Show()));

    /// <summary>Son titre.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(FirstTip), new PropertyMetadata(string.Empty));

    /// <summary>Ce qu'elle dit.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(FirstTip), new PropertyMetadata(string.Empty));

    /// <summary>Le moment est venu : ce qu'elle explique est à l'écran.</summary>
    public static readonly DependencyProperty IsOfferedProperty = DependencyProperty.Register(
        nameof(IsOffered), typeof(bool), typeof(FirstTip), new PropertyMetadata(true, (element, _) => ((FirstTip)element).Show()));

    private FirstUse? _followed;

    public FirstTip()
    {
        InitializeComponent();
        Loaded += (_, _) => Follow();
        Unloaded += (_, _) => Unfollow();
    }

    /// <inheritdoc cref="MarkProperty"/>
    public string Mark
    {
        get => (string)GetValue(MarkProperty);
        set => SetValue(MarkProperty, value);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="TextProperty"/>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <inheritdoc cref="IsOfferedProperty"/>
    public bool IsOffered
    {
        get => (bool)GetValue(IsOfferedProperty);
        set => SetValue(IsOfferedProperty, value);
    }

    /// <summary>Ce que la première utilisation a déjà montré, pour cet élément et ce qu'il contient.</summary>
    public static FirstUse? GetMemory(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (FirstUse?)element.GetValue(MemoryProperty);
    }

    /// <summary>Donne à cet élément, et à ce qu'il contient, ce que la première utilisation a déjà montré.</summary>
    public static void SetMemory(DependencyObject element, FirstUse? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(MemoryProperty, value);
    }

    /// <summary>L'aide suit la mémoire tant qu'elle est à l'écran : une autre aide, ailleurs, peut les couper toutes.</summary>
    private void Follow()
    {
        var memory = GetMemory(this);
        if (!ReferenceEquals(memory, _followed))
        {
            Unfollow();
            _followed = memory;
            _followed?.Changed += OnMemoryChanged;
        }
        Show();
    }

    private void Unfollow()
    {
        _followed?.Changed -= OnMemoryChanged;
        _followed = null;
    }

    private void OnMemoryChanged(object? sender, EventArgs e) => Show();

    private void Show() =>
        Visibility = IsOffered && Mark.Length > 0 && GetMemory(this) is { } memory && !memory.Has(Mark) && !memory.Has(FirstUse.NoTips)
            ? Visibility.Visible
            : Visibility.Collapsed;

    private void OnUnderstood(object sender, RoutedEventArgs e) => GetMemory(this)?.Remember(Mark);

    private void OnNoMore(object sender, RoutedEventArgs e) => GetMemory(this)?.Remember(FirstUse.NoTips);
}

/// <summary>Les textes des aides de la première fois.</summary>
public static class FirstTipText
{
    /// <summary>Ferme l'aide, qui ne revient pas.</summary>
    public const string Understood = "Compris";

    /// <summary>Coupe toutes les aides de la première fois.</summary>
    public const string NoMore = "Ne plus montrer d'aide";

    /// <summary>La première révision.</summary>
    public const string ReviewTitle = "Votre première révision";

    /// <summary>Ce qu'il faut en savoir.</summary>
    public const string ReviewText = "Relisez, puis « Valider » : l'étape suivante est planifiée, jusqu'à J30. « J'avais oublié » compte aussi la révision, mais fait reculer d'une étape.";

    /// <summary>La première fiche écrite.</summary>
    public const string WritingTitle = "Votre première fiche";

    /// <summary>Ce qu'il faut en savoir.</summary>
    public const string WritingText = "Ce que vous écrivez ici est aussi ce que vous réviserez. Le gras et les passages surlignés pourront être masqués en révision, pour vous faire retrouver l'essentiel.";
}
