using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Champ de date : un champ de saisie et son calendrier, en français (la semaine commence le lundi). La date se tape
/// (jj/mm/aaaa) ou se choisit ; hors de <see cref="Minimum"/> et <see cref="Maximum"/>, elle est refusée : le champ se
/// vide, il dit pourquoi, et <see cref="Day"/> ne vaut plus rien tant qu'une date valable n'est pas donnée. Le calendrier
/// s'ouvre avec Alt+Bas ou son bouton, se parcourt aux flèches et se referme avec Échap, qui ne ferme pas le dialogue
/// autour de lui. Entrée valide la date, et reste au bouton par défaut d'un dialogue, sauf si elle vient d'être refusée.
/// </summary>
public sealed class DateField : DatePicker
{
    /// <summary>La date choisie.</summary>
    public static readonly DependencyProperty DayProperty =
        DependencyProperty.Register(nameof(Day), typeof(DateOnly?), typeof(DateField), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (field, _) => ((DateField)field).ShowDay()));

    /// <summary>Le premier jour permis ; rien : pas de limite.</summary>
    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(DateOnly?), typeof(DateField), new PropertyMetadata(null, (field, _) => ((DateField)field).ApplyLimits()));

    /// <summary>Le dernier jour permis ; rien : pas de limite.</summary>
    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(DateOnly?), typeof(DateField), new PropertyMetadata(null, (field, _) => ((DateField)field).ApplyLimits()));

    private static readonly DependencyPropertyKey HasRefusedDateKey = DependencyProperty.RegisterReadOnly(
        nameof(HasRefusedDate), typeof(bool), typeof(DateField), new PropertyMetadata(false, (field, e) => ((DateField)field).ShowRefusal((bool)e.NewValue)));

    /// <summary>La date tapée vient d'être refusée.</summary>
    public static readonly DependencyProperty HasRefusedDateProperty = HasRefusedDateKey.DependencyProperty;

    /// <summary>La date affichée change parce que <see cref="Day"/> a changé : ce n'est pas un choix à lui renvoyer.</summary>
    private bool _showing;

    /// <summary>Échap referme le calendrier : la touche s'arrête là.</summary>
    private bool _closingCalendar;

    static DateField()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(DateField), new FrameworkPropertyMetadata(typeof(DateField)));
    }

    /// <summary>Un champ de date en français, sans date ni limite.</summary>
    public DateField()
    {
        Language = XmlLanguage.GetLanguage("fr-FR");
        FirstDayOfWeek = DayOfWeek.Monday;
        SelectedDateFormat = DatePickerFormat.Short;
        DateValidationError += (_, _) => Refuse();
        AddHandler(KeyDownEvent, new KeyEventHandler(OnKeyDownSeen), handledEventsToo: true);
        AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnTextEdited), handledEventsToo: true);
    }

    /// <summary>La date choisie ; rien tant qu'il n'y en a pas.</summary>
    public DateOnly? Day
    {
        get => (DateOnly?)GetValue(DayProperty);
        set => SetValue(DayProperty, value);
    }

    /// <summary>Le premier jour permis ; rien : pas de limite.</summary>
    public DateOnly? Minimum
    {
        get => (DateOnly?)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>Le dernier jour permis ; rien : pas de limite.</summary>
    public DateOnly? Maximum
    {
        get => (DateOnly?)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>La date tapée vient d'être refusée : le champ le dit jusqu'à ce qu'on y écrive ou qu'une date lui soit donnée.</summary>
    public bool HasRefusedDate => (bool)GetValue(HasRefusedDateProperty);

    /// <inheritdoc/>
    protected override void OnSelectedDateChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectedDateChanged(e);
        SetValue(HasRefusedDateKey, false);
        if (!_showing)
        {
            SetCurrentValue(DayProperty, SelectedDate is { } date ? DateOnly.FromDateTime(date) : null);
        }
    }

    /// <inheritdoc/>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        // Le calendrier se referme avant que la touche ne remonte : c'est ici qu'on sait qu'il était ouvert.
        _closingCalendar = e.Key == Key.Escape && IsDropDownOpen;
        base.OnPreviewKeyDown(e);
    }

    private static DateTime ToDate(DateOnly day) => day.ToDateTime(TimeOnly.MinValue);

    /// <summary>
    /// Une date refusée n'est pas une date : celle d'avant ne reste pas à sa place, pour qu'aucune action ne parte sur un
    /// jour que l'on vient de remplacer.
    /// </summary>
    private void Refuse()
    {
        SetCurrentValue(DayProperty, null);
        SetValue(HasRefusedDateKey, true);
        Dispatcher.BeginInvoke(DispatcherPriority.Input, TakeBackFocus);
    }

    /// <summary>Le bouton cliqué dépendait de cette date : désactivé, il perd le focus, et le champ le reprend pour qu'on la corrige.</summary>
    private void TakeBackFocus()
    {
        if (KeyboardFocus.IsLost())
        {
            Focus();
        }
    }

    private void OnKeyDownSeen(object sender, KeyEventArgs e)
    {
        if (_closingCalendar)
        {
            _closingCalendar = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && e.OriginalSource is DatePickerTextBox && !HasRefusedDate)
        {
            // La date tapée est prise en compte : Entrée reste au bouton par défaut d'un dialogue.
            e.Handled = false;
        }
    }

    /// <summary>On écrit à nouveau : le refus s'efface. Le champ qui se vide en refusant une date n'en est pas une nouvelle saisie.</summary>
    private void OnTextEdited(object sender, TextChangedEventArgs e)
    {
        if (e.OriginalSource is DatePickerTextBox { Text.Length: > 0 })
        {
            SetValue(HasRefusedDateKey, false);
        }
    }

    private void ShowRefusal(bool refused) => AutomationProperties.SetItemStatus(this, refused ? DesignText.DateRefused : string.Empty);

    private bool IsAllowed(DateOnly day) => (Minimum is not { } low || day >= low) && (Maximum is not { } high || day <= high);

    /// <summary>Montre <see cref="Day"/> ; une date hors des limites ne se montre pas, et n'est pas gardée.</summary>
    private void ShowDay()
    {
        if (Day is { } day && !IsAllowed(day))
        {
            SetCurrentValue(DayProperty, null);
            return;
        }
        _showing = true;
        try
        {
            SetCurrentValue(SelectedDateProperty, Day is { } shown ? ToDate(shown) : null);
        }
        finally
        {
            _showing = false;
        }
    }

    /// <summary>
    /// Les jours hors des limites sont retirés du calendrier : une date tapée y est refusée, et on ne peut pas les
    /// choisir. La date en cours part d'abord si elle en sort : le calendrier refuserait de retirer un jour choisi.
    /// </summary>
    private void ApplyLimits()
    {
        if (Day is { } day && !IsAllowed(day))
        {
            SetCurrentValue(DayProperty, null);
        }
        BlackoutDates.Clear();
        if (Minimum is { } low && low > DateOnly.MinValue)
        {
            BlackoutDates.Add(new CalendarDateRange(DateTime.MinValue, ToDate(low.AddDays(-1))));
        }
        if (Maximum is { } high && high < DateOnly.MaxValue)
        {
            BlackoutDates.Add(new CalendarDateRange(ToDate(high.AddDays(1)), DateTime.MaxValue));
        }
        SetCurrentValue(DisplayDateStartProperty, Minimum is { } first ? ToDate(first) : null);
        SetCurrentValue(DisplayDateEndProperty, Maximum is { } last ? ToDate(last) : null);
    }
}
