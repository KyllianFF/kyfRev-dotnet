using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using KyfRev.DesignSystem.Controls;
using KyfRev.DesignSystem.Tokens;

namespace KyfRev.DesignSystem.Tests;

/// <summary>Le champ de date : en français, une date qui se tape ou se choisit, des limites tenues, ses couleurs dans chaque thème.</summary>
[TestClass]
public sealed class DateFieldTests
{
    private static readonly DateOnly Day = new(2026, 11, 12);

    private static readonly System.Globalization.CultureInfo French = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");

    [STATestMethod]
    public void The_field_speaks_french_and_starts_the_week_on_monday()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var field = new DateField();
        Host.Of(design, field);

        Assert.AreEqual("fr-fr", field.Language.IetfLanguageTag);
        Assert.AreEqual(DayOfWeek.Monday, field.FirstDayOfWeek);
        Assert.AreEqual(DatePickerFormat.Short, field.SelectedDateFormat);
        Assert.AreEqual(DesignText.DatePlaceholder, Host.Part<TextBlock>(Text(field), "Placeholder").Text);
        Assert.AreEqual(Visibility.Visible, Host.Part<TextBlock>(Text(field), "Placeholder").Visibility);

        field.Day = Day;
        Assert.AreEqual("12/11/2026", Text(field).Text);
        Assert.AreEqual(Visibility.Collapsed, Host.Part<TextBlock>(Text(field), "Placeholder").Visibility);
    }

    [STATestMethod]
    public void The_day_goes_both_ways()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var source = new DaySource { Day = Day };
        var field = new DateField();
        field.SetBinding(DateField.DayProperty, new Binding(nameof(DaySource.Day)) { Source = source });
        Host.Of(design, field);

        Assert.AreEqual(Day.ToDateTime(TimeOnly.MinValue), field.SelectedDate);

        field.SelectedDate = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Local);
        Assert.AreEqual(new DateOnly(2026, 11, 20), source.Day, "un jour choisi au calendrier part vers la source");

        source.Day = new DateOnly(2026, 11, 25);
        Assert.AreEqual(new DateTime(2026, 11, 25, 0, 0, 0, DateTimeKind.Local), field.SelectedDate);

        field.SelectedDate = null;
        Assert.IsNull(source.Day);
    }

    [STATestMethod]
    public void A_date_out_of_the_limits_is_refused_typed_or_given()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var field = new DateField { Minimum = new DateOnly(2026, 11, 10), Maximum = new DateOnly(2026, 11, 20), Day = Day };
        Host.Of(design, field);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai"));

        Assert.HasCount(2, field.BlackoutDates);
        Assert.AreEqual(new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Local), field.DisplayDateStart);
        Assert.AreEqual(new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Local), field.DisplayDateEnd);

        Text(field).Text = "21/11/2026";
        Press(field, source, Text(field), Key.Enter);
        Assert.IsNull(field.Day, "une date refusée n'est pas une date : celle d'avant ne reste pas à sa place");
        Assert.IsNull(field.SelectedDate);
        Assert.AreEqual(string.Empty, Text(field).Text);
        Assert.IsTrue(field.HasRefusedDate);
        Assert.AreEqual(DesignText.DateRefused, AutomationProperties.GetItemStatus(field));
        var frame = Host.Part<Border>(field, "Frame");
        field.ApplyTemplate();
        Assert.AreEqual(((SolidColorBrush)design.Dictionary[DesignKeys.Brush(ColorRole.Critical)]).Color, ((SolidColorBrush)frame.BorderBrush).Color);

        // Écrire à nouveau efface le refus.
        Text(field).Text = "1";
        Assert.IsFalse(field.HasRefusedDate);
        Assert.AreEqual(string.Empty, AutomationProperties.GetItemStatus(field));

        Text(field).Text = "pas une date";
        Press(field, source, Text(field), Key.Enter);
        Assert.IsNull(field.Day);
        Assert.IsTrue(field.HasRefusedDate);
        Text(field).Text = "13/11/2026";
        Press(field, source, Text(field), Key.Enter);
        Assert.AreEqual(new DateOnly(2026, 11, 13), field.Day);
        Assert.IsFalse(field.HasRefusedDate);

        field.Day = new DateOnly(2026, 11, 21);
        Assert.IsNull(field.Day, "hors des limites, la date n'est pas gardée");
        Assert.IsNull(field.SelectedDate);

        field.Day = new DateOnly(2026, 11, 15);
        field.Minimum = new DateOnly(2026, 11, 16);
        Assert.IsNull(field.Day, "une limite qui change emporte la date qui en sort");

        field.Minimum = DateOnly.MinValue;
        field.Maximum = DateOnly.MaxValue;
        Assert.IsEmpty(field.BlackoutDates, "les limites extrêmes n'ôtent aucun jour");
        field.Minimum = null;
        field.Maximum = null;
        Assert.IsNull(field.DisplayDateStart);
        Assert.IsNull(field.DisplayDateEnd);
    }

    [STATestMethod]
    public void Enter_stays_for_the_default_button_of_a_dialog()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var field = new DateField { Day = Day };
        Host.Of(design, field);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai"));

        var pressed = Press(field, source, Text(field), Key.Enter);

        Assert.IsFalse(pressed.Handled);
        var other = Press(field, source, field, Key.Enter, handled: true);
        Assert.IsTrue(other.Handled, "une touche qui vient d'ailleurs n'est pas rendue");
    }

    [STATestMethod]
    public void Enter_on_a_refused_date_stays_in_the_field()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var field = new DateField { Maximum = new DateOnly(2026, 11, 20), Day = Day };
        Host.Of(design, field);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai"));

        Text(field).Text = "30/02/2027";
        var pressed = Press(field, source, Text(field), Key.Enter);

        Assert.IsTrue(field.HasRefusedDate);
        Assert.IsTrue(pressed.Handled, "le bouton par défaut ne part pas sur la date d'avant, et le refus se lit");
    }

    [STATestMethod]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    [DataRow(ThemeKind.HighContrast)]
    public void A_refused_date_is_written_and_read_aloud(ThemeKind theme)
    {
        var design = new DesignResources(theme, reducedMotion: false);
        var field = new DateField { Day = Day, Maximum = new DateOnly(2026, 11, 20) };
        Host.Of(design, field);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai"));
        var message = Host.Part<LiveText>(field, "Refusal");
        Assert.AreEqual(Visibility.Collapsed, message.Visibility, "rien tant que la date n'est pas refusée");

        Text(field).Text = "21/11/2026";
        Press(field, source, Text(field), Key.Enter);

        Assert.AreEqual(Visibility.Visible, message.Visibility, "la couleur du cadre ne suffit pas : le contraste élevé ne la change pas");
        Assert.AreEqual(DesignText.DateRefused, message.Text);
        Assert.AreEqual(((SolidColorBrush)design.Dictionary[DesignKeys.Brush(ColorRole.Critical)]).Color, ((SolidColorBrush)message.Foreground).Color);
        Assert.AreEqual(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(message));
        var peer = UIElementAutomationPeer.CreatePeerForElement(message);
        Assert.AreEqual(DesignText.DateRefused, peer.GetName());
        Assert.IsTrue(peer.IsControlElement(), "un texte du modèle reste atteignable par le lecteur d'écran");
        Assert.AreEqual(DesignText.DateRefused, AutomationProperties.GetItemStatus(Text(field)), "le champ qui a le focus le dit aussi");

        Text(field).Text = "1";
        Assert.AreEqual(Visibility.Collapsed, message.Visibility);
        Assert.AreEqual(string.Empty, AutomationProperties.GetItemStatus(Text(field)));
    }

    [STATestMethod]
    public void A_date_given_after_a_refusal_clears_it()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var field = new DateField { Day = Day, Maximum = new DateOnly(2026, 11, 20) };
        Host.Of(design, field);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai"));
        Text(field).Text = "21/11/2026";
        Press(field, source, Text(field), Key.Enter);
        Assert.IsTrue(field.HasRefusedDate);

        field.Day = new DateOnly(2026, 11, 14);

        Assert.IsFalse(field.HasRefusedDate);
        Assert.AreEqual("14/11/2026", Text(field).Text);
    }

    [STATestMethod]
    public void A_click_on_a_button_the_refusal_disables_gives_the_focus_back_to_the_field()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var field = new DateField { Day = Day, Maximum = new DateOnly(2026, 11, 20) };
        var confirm = new Button { Content = "Valider" };
        confirm.SetBinding(UIElement.IsEnabledProperty, new Binding(nameof(DateField.Day)) { Source = field, Converter = new HasValue() });
        var window = new Window { Content = new StackPanel { Children = { field, confirm } }, Resources = design.Dictionary, ShowActivated = true, Left = -10000, Top = -10000, Width = 480, Height = 320, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Show();
        window.Activate();
        Host.Flush();
        try
        {
            Assert.IsTrue(Text(field).Focus());
            Text(field).Text = "21/11/2026";

            // Le clic prend le focus : le champ perd le sien, refuse la date, et le bouton se désactive.
            Assert.IsTrue(confirm.Focus());
            Assert.IsNull(field.Day);
            Assert.IsFalse(confirm.IsEnabled);
            Until(() => field.IsKeyboardFocusWithin);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Escape_closes_the_calendar_and_not_the_dialog_around_it()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var field = new DateField { Day = Day };
        var dialog = new Dialog { Content = field, IsOpen = true };
        Host.Of(design, dialog);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai"));
        var closed = 0;
        dialog.Closed += (_, _) => closed++;

        field.IsDropDownOpen = true;
        Tunnel(field, source, Key.Escape);
        field.IsDropDownOpen = false;
        field.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent });
        Assert.AreEqual(0, closed);
        Assert.IsTrue(dialog.IsOpen);

        // Fermé, Échap ferme le dialogue.
        Tunnel(field, source, Key.Escape);
        field.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent });
        Assert.AreEqual(1, closed);
    }

    [STATestMethod]
    public void Every_part_has_its_name()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var field = new DateField();
        AutomationProperties.SetName(field, "Date de l'examen");
        Host.Of(design, field);

        Assert.AreEqual("Date de l'examen", UIElementAutomationPeer.CreatePeerForElement(field).GetName());
        Assert.AreEqual("Date de l'examen", AutomationProperties.GetName(Text(field)));
        var button = Host.Part<Button>(field, "PART_Button");
        Assert.AreEqual(DesignText.OpenCalendar, AutomationProperties.GetName(button));
        Assert.AreEqual(Icons.Calendar, button.Content);
        Assert.IsTrue(button.Focusable);
        Assert.AreEqual(KeyboardNavigationMode.Continue, KeyboardNavigation.GetTabNavigation(field), "la tabulation passe du texte au bouton");
        Assert.AreEqual(design.Dictionary["Kyf.Button.Icon"], button.Style);
    }

    [STATestMethod]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    [DataRow(ThemeKind.HighContrast)]
    public void The_calendar_takes_the_colors_of_the_theme(ThemeKind theme)
    {
        var design = new DesignResources(theme, reducedMotion: false);
        Color Of(ColorRole role) => ((SolidColorBrush)design.Dictionary[DesignKeys.Brush(role)]).Color;
        var calendar = new Calendar
        {
            Style = (Style)design.Dictionary["Kyf.Calendar"],
            Language = System.Windows.Markup.XmlLanguage.GetLanguage("fr-FR"),
            FirstDayOfWeek = DayOfWeek.Monday,
            DisplayDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Local),
            SelectedDate = new DateTime(2026, 11, 12, 0, 0, 0, DateTimeKind.Local),
        };
        var host = Host.Of(design, calendar, 400, 460);
        Host.Flush();
        Host.Layout(host);

        Assert.AreEqual(Of(ColorRole.Surface), ((SolidColorBrush)calendar.Background).Color);
        var days = Descendants(calendar).OfType<CalendarDayButton>().ToList();
        Assert.HasCount(42, days);
        var selected = days.Single(day => day.IsSelected);
        var other = days.First(day => !day.IsSelected && !day.IsInactive);
        Assert.AreEqual(Of(ColorRole.OnPrimary), ((SolidColorBrush)selected.Foreground).Color);
        Assert.AreEqual(Of(ColorRole.Primary), ((SolidColorBrush)Host.Part<Border>(selected, "Cell").Background).Color);
        Assert.AreEqual(Of(ColorRole.Text), ((SolidColorBrush)other.Foreground).Color);
        var outside = days.FirstOrDefault(day => day.IsInactive);
        Assert.IsNotNull(outside);
        Assert.AreEqual(Of(ColorRole.TextSecondary), ((SolidColorBrush)outside.Foreground).Color);
    }

    [STATestMethod]
    public void The_calendar_is_written_in_french_and_fits_the_smallest_window()
    {
        var design = new DesignResources(ThemeKind.Dark, reducedMotion: false);
        var calendar = new Calendar
        {
            Style = (Style)design.Dictionary["Kyf.Calendar"],
            Language = System.Windows.Markup.XmlLanguage.GetLanguage("fr-FR"),
            FirstDayOfWeek = DayOfWeek.Monday,
            DisplayDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Local),
        };
        Host.Of(design, calendar, 470, 540);
        Host.Flush();

        Assert.AreEqual("novembre 2026", Part<Button>(calendar, "PART_HeaderButton").Content);
        var titles = Descendants(Part<Grid>(calendar, "PART_MonthView")).OfType<TextBlock>().Select(title => title.Text).Take(7).ToList();
        Assert.AreEqual(French.DateTimeFormat.ShortestDayNames[(int)DayOfWeek.Monday], titles[0], "la semaine commence le lundi");
        Assert.AreEqual(DesignText.PreviousPeriod, AutomationProperties.GetName(Part<Button>(calendar, "PART_PreviousButton")));
        Assert.AreEqual(DesignText.NextPeriod, AutomationProperties.GetName(Part<Button>(calendar, "PART_NextButton")));
        Assert.AreEqual(DesignText.Calendar, AutomationProperties.GetName(calendar));

        calendar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Assert.IsLessThanOrEqualTo(438.0, calendar.DesiredSize.Width, "la largeur de la plus petite fenêtre, ses marges ôtées");
        Assert.IsLessThanOrEqualTo(440.0, calendar.DesiredSize.Height);

        calendar.DisplayMode = CalendarMode.Year;
        Host.Flush();
        Assert.AreEqual(Visibility.Hidden, Part<Grid>(calendar, "PART_MonthView").Visibility);
        Assert.AreEqual(Visibility.Visible, Part<Grid>(calendar, "PART_YearView").Visibility);
        calendar.DisplayMode = CalendarMode.Decade;
        Host.Flush();
        Assert.AreEqual(Visibility.Visible, Part<Grid>(calendar, "PART_YearView").Visibility);
        Assert.AreEqual(((SolidColorBrush)design.Dictionary[DesignKeys.Brush(ColorRole.Text)]).Color, ((SolidColorBrush)Descendants(calendar).OfType<CalendarButton>().First(button => !button.IsInactive).Foreground).Color);
    }

    private static DatePickerTextBox Text(DateField field) => Host.Part<DatePickerTextBox>(field, "PART_TextBox");

    /// <summary>Un élément du modèle du mois, qui vit dans celui de <c>CalendarItem</c>.</summary>
    private static T Part<T>(Calendar calendar, string name)
        where T : FrameworkElement => Descendants(calendar).OfType<T>().First(part => part.Name == name);

    private static void Tunnel(DateField field, HwndSource source, Key key) =>
        field.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });

    /// <summary>Une touche sur <paramref name="origin"/>, dans l'ordre : le tunnel, puis la remontée, traitée au passage comme le champ de Windows.</summary>
    private static KeyEventArgs Press(DateField field, HwndSource source, UIElement origin, Key key, bool handled = false)
    {
        Tunnel(field, source, key);
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.KeyDownEvent, Handled = handled };
        origin.RaiseEvent(args);
        return args;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var deeper in Descendants(child))
            {
                yield return deeper;
            }
        }
    }

    private static void Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.IsLessThan(deadline, DateTime.UtcNow, "délai dépassé");
            Host.Flush();
        }
    }

    /// <summary>Vrai s'il y a une date.</summary>
    private sealed class HasValue : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => value is not null;

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>Une source de liaison dont la date se lit et se change.</summary>
    private sealed class DaySource : System.ComponentModel.INotifyPropertyChanged
    {
        private DateOnly? _day;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public DateOnly? Day
        {
            get => _day;
            set
            {
                _day = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Day)));
            }
        }
    }
}
