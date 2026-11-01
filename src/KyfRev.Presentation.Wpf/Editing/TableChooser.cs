using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>
/// Le choix de la taille d'un tableau : une grille où la souris ou les flèches étendent la sélection, et un clic
/// ou Entrée insère. La taille choisie se lit sous la grille.
/// </summary>
internal sealed class TableChooser : Control
{
    /// <summary>Lignes et colonnes au plus, comme dans la 0.x.</summary>
    public const int Maximum = 8;

    private readonly Border[] _cells = new Border[Maximum * Maximum];
    private readonly TextBlock _label = new() { Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };

    public TableChooser()
    {
        Focusable = true;
        IsTabStop = true;
        AutomationProperties.SetName(this, EditingText.TableGrid);
        var grid = new UniformGrid { Rows = Maximum, Columns = Maximum };
        for (var row = 0; row < Maximum; row++)
        {
            for (var column = 0; column < Maximum; column++)
            {
                var cell = new Border { Width = 18, Height = 18, Margin = new Thickness(1), BorderThickness = new Thickness(1) };
                cell.SetResourceReference(Border.BorderBrushProperty, "Kyf.Brush.ControlBorder");
                var (cellRow, cellColumn) = (row + 1, column + 1);
                cell.MouseEnter += (_, _) => Select(cellRow, cellColumn);
                cell.MouseLeftButtonUp += (_, _) => Choose();
                _cells[(row * Maximum) + column] = cell;
                grid.Children.Add(cell);
            }
        }
        _label.SetResourceReference(TextBlock.ForegroundProperty, "Kyf.Brush.Text");
        var content = new StackPanel { Margin = new Thickness(8) };
        content.Children.Add(grid);
        content.Children.Add(_label);
        var host = new Border { Child = content, BorderThickness = new Thickness(1) };
        host.SetResourceReference(Border.BackgroundProperty, "Kyf.Brush.Surface");
        host.SetResourceReference(Border.BorderBrushProperty, "Kyf.Brush.Divider");
        AddVisualChild(host);
        AddLogicalChild(host);
        Host = host;
        Select(3, 3);
    }

    /// <summary>La taille est choisie.</summary>
    public event Action<int, int>? Chosen;

    /// <summary>Lignes retenues, en-tête compris.</summary>
    public int Rows { get; private set; }

    /// <summary>Colonnes retenues.</summary>
    public int Columns { get; private set; }

    /// <summary>La taille, en mots.</summary>
    public string Label => _label.Text;

    /// <inheritdoc/>
    protected override int VisualChildrenCount => 1;

    private Border Host { get; }

    /// <inheritdoc/>
    protected override Visual GetVisualChild(int index) => Host;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size constraint)
    {
        Host.Measure(constraint);
        return Host.DesiredSize;
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        Host.Arrange(new Rect(arrangeBounds));
        return arrangeBounds;
    }

    /// <inheritdoc/>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var (rows, columns) = e.Key switch
        {
            Key.Right => (Rows, Columns + 1),
            Key.Left => (Rows, Columns - 1),
            Key.Down => (Rows + 1, Columns),
            Key.Up => (Rows - 1, Columns),
            _ => (0, 0),
        };
        if (rows > 0)
        {
            e.Handled = true;
            Select(Math.Clamp(rows, 1, Maximum), Math.Clamp(columns, 1, Maximum));
        }
        else if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            Choose();
        }
        base.OnPreviewKeyDown(e);
    }

    private void Select(int rows, int columns)
    {
        (Rows, Columns) = (rows, columns);
        for (var row = 0; row < Maximum; row++)
        {
            for (var column = 0; column < Maximum; column++)
            {
                _cells[(row * Maximum) + column].SetResourceReference(Border.BackgroundProperty, row < rows && column < columns ? "Kyf.Brush.Primary" : "Kyf.Brush.Surface");
            }
        }
        _label.Text = EditingText.TableSize(rows, columns);
        AutomationProperties.SetItemStatus(this, _label.Text);
    }

    private void Choose() => Chosen?.Invoke(Rows, Columns);
}
