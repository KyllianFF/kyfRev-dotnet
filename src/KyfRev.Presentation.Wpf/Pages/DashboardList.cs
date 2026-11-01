using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// La liste d'« Aujourd'hui ». Une ligne à réviser s'ouvre d'un clic ou d'Entrée ; elle s'ouvre aussi
/// par l'automatisation, pour un lecteur d'écran comme pour les scénarios d'acceptation.
/// </summary>
internal sealed class DashboardList : ListBox
{
    /// <summary>Ouvre la ligne qui lui est passée.</summary>
    public static readonly DependencyProperty OpenCommandProperty =
        DependencyProperty.Register(nameof(OpenCommand), typeof(ICommand), typeof(DashboardList));

    public DashboardList()
    {
        // Le style de toutes les listes : un style implicite ne s'applique pas tout seul à une classe dérivée.
        SetResourceReference(StyleProperty, typeof(ListBox));
    }

    /// <inheritdoc cref="OpenCommandProperty"/>
    public ICommand? OpenCommand
    {
        get => (ICommand?)GetValue(OpenCommandProperty);
        set => SetValue(OpenCommandProperty, value);
    }

    /// <inheritdoc/>
    protected override AutomationPeer OnCreateAutomationPeer() => new ListPeer(this);

    private sealed class ListPeer(DashboardList list) : ListBoxAutomationPeer(list)
    {
        public void Open(object row) =>
            list.Dispatcher.BeginInvoke(() => list.OpenCommand?.Execute(row));

        protected override ItemAutomationPeer CreateItemAutomationPeer(object item) => new RowPeer(item, this);
    }

    private sealed class RowPeer(object row, ListPeer list) : ListBoxItemAutomationPeer(row, list), IInvokeProvider
    {
        public override object GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.Invoke && Item is DueItemRow or ReminderRow ? this : base.GetPattern(patternInterface);

        public void Invoke() => list.Open(Item);
    }
}
