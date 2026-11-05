using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using KyfRev.DesignSystem;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Extensions;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>
/// Un bloc <c>```langage</c> d'une fiche, dessiné par l'extension qui déclare ce langage : ce qu'elle affiche, en éléments natifs, comme
/// sa vue. Un clic sur un de ses boutons passe par elle, puis le bloc se redessine. Si elle ne répond pas, le bloc reste lisible : son
/// texte s'affiche, avec la raison.
/// </summary>
internal sealed class ExtensionBlockView : Border
{
    /// <summary>Le langage du bloc.</summary>
    public static readonly DependencyProperty BlockLanguageProperty = DependencyProperty.Register(
        nameof(BlockLanguage), typeof(string), typeof(ExtensionBlockView), new PropertyMetadata(string.Empty, (sender, _) => ((ExtensionBlockView)sender).Redraw()));

    /// <summary>Le texte du bloc.</summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(string), typeof(ExtensionBlockView), new PropertyMetadata(string.Empty, (sender, _) => ((ExtensionBlockView)sender).Redraw()));

    private int _request;

    public ExtensionBlockView()
    {
        Padding = new Thickness(16, 12, 16, 12);
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(8);
        SetResourceReference(BorderBrushProperty, "Kyf.Brush.Divider");
        Loaded += (_, _) => Redraw();
    }

    /// <inheritdoc cref="BlockLanguageProperty"/>
    public string BlockLanguage
    {
        get => (string)GetValue(BlockLanguageProperty);
        set => SetValue(BlockLanguageProperty, value);
    }

    /// <inheritdoc cref="SourceProperty"/>
    public string Source
    {
        get => (string)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Ce que l'extension affiche est en place.</summary>
    internal bool ShowsContent { get; private set; }

    /// <summary>Le dessin en cours : il se refait à chaque retour sur la fiche.</summary>
    internal Task Drawing { get; private set; } = Task.CompletedTask;

    private void Redraw()
    {
        if (IsLoaded)
        {
            Drawing = DrawAsync();
        }
    }

    private async Task DrawAsync()
    {
        var request = ++_request;
        if (TryFindResource(ExtensionBlocks.ResourceKey) is not ExtensionBlocks blocks)
        {
            ShowText(ReadingText.ExtensionBlockFailedOf(BlockLanguage, ReadingText.NoExtensionBlockOf(BlockLanguage)));
            return;
        }
        var answer = await blocks.RenderAsync(BlockLanguage, Source).ConfigureAwait(true);
        if (request != _request)
        {
            return;
        }
        if (!answer.Succeeded)
        {
            ShowText(ReadingText.ExtensionBlockFailedOf(BlockLanguage, answer.Error));
            return;
        }
        var builder = new ExtensionContentBuilder(action => _ = ActAsync(blocks, action));
        Child = builder.Build(answer.Content ?? ExtensionContent.Empty);
        AutomationProperties.SetName(this, ReadingText.ExtensionBlockOf(BlockLanguage));
        ShowsContent = true;
    }

    private async Task ActAsync(ExtensionBlocks blocks, ExtensionAction action)
    {
        var answer = await blocks.ActAsync(BlockLanguage, action).ConfigureAwait(true);
        if (answer.Succeeded)
        {
            Redraw();
            return;
        }
        ShowText(ReadingText.ExtensionBlockFailedOf(BlockLanguage, answer.Error));
    }

    /// <summary>Le bloc sous sa forme de texte, avec ce qui l'empêche de se dessiner.</summary>
    private void ShowText(string reason)
    {
        var notice = new TextBlock { Text = reason, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
        notice.SetResourceReference(TextBlock.StyleProperty, "Kyf.Text.Caption");
        var code = new TextBlock { Text = Source, TextWrapping = TextWrapping.Wrap };
        code.SetResourceReference(TextBlock.FontFamilyProperty, DesignKeys.CodeFont);
        var panel = new StackPanel();
        panel.Children.Add(notice);
        panel.Children.Add(code);
        Child = panel;
        AutomationProperties.SetName(this, reason);
        ShowsContent = false;
    }
}
