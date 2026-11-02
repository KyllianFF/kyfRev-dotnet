using System.Windows;
using System.Windows.Controls;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>Deux textes en face l'un de l'autre, ce qui diffère marqué.</summary>
internal sealed partial class DiffView : UserControl
{
    /// <summary>Les lignes de la comparaison.</summary>
    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(
        nameof(Rows), typeof(IReadOnlyList<DiffRow>), typeof(DiffView));

    public DiffView()
    {
        InitializeComponent();
    }

    /// <inheritdoc cref="RowsProperty"/>
    public IReadOnlyList<DiffRow>? Rows
    {
        get => (IReadOnlyList<DiffRow>?)GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }
}
