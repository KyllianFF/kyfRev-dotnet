using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Vue de la séance d'étude.</summary>
internal sealed partial class StudySessionPage : UserControl
{
    public StudySessionPage()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
        Item.PreviewMouseDown += (_, e) => StayUnlessNext(e.OriginalSource as DependencyObject);
        Item.PreviewMouseWheel += (_, _) => StayUnlessNext(null);
    }

    private StudySessionViewModel? Session => DataContext as StudySessionViewModel;

    /// <summary>Pendant que la séance avance seule, Échap fait rester sur l'élément validé.</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && Session is { IsAdvancing: true } session)
        {
            session.StayCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Un clic ou la molette dans l'élément, pendant que la séance avance seule : on y reste, sauf sur le bouton qui mène à
    /// la suite, qui y va tout de suite.
    /// </summary>
    private void StayUnlessNext(DependencyObject? source)
    {
        if (Session is not { IsAdvancing: true } session)
        {
            return;
        }
        for (var element = source; element is not null; element = element is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element))
        {
            if (element is Button { Name: "NextButton" })
            {
                return;
            }
        }
        session.StayCommand.Execute(null);
    }
}
