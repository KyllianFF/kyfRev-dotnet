using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue d'une fiche ouverte pour la lire et la réviser : son plan, son texte et ses passages.</summary>
internal sealed partial class SheetPage : UserControl
{
    public SheetPage()
    {
        InitializeComponent();
        SizeChanged += (_, e) =>
        {
            if (DataContext is SheetPageViewModel page)
            {
                page.PageWidth = e.NewSize.Width;
            }
        };
        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is SheetPageViewModel page)
            {
                page.PageWidth = ActualWidth;
            }
        };
    }

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
            SheetText.Show(entry.Heading);
        }
    }

    private void OnPassageEntryClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SheetPageViewModel { Reading: { } reading } page && ((FrameworkElement)sender).DataContext is PassageEntry entry)
        {
            page.CloseOverlayCommand.Execute(null);
            if (reading.PositionOf(entry.Annotation) is { } position)
            {
                SheetText.Show(position.Start);
            }
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
