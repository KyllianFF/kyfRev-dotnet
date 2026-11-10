using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Textes de l'étalement des échéances.</summary>
public static class SpreadText
{
    /// <summary>Le bouton de « Aujourd'hui ».</summary>
    public const string Action = "Étaler…";

    /// <summary>Ce que fait le bouton, pour l'infobulle et le lecteur d'écran.</summary>
    public const string ActionName = "Étaler un retard ou prévoir une pause";

    /// <summary>Le titre de la fenêtre.</summary>
    public const string Title = "Étaler les révisions";

    /// <summary>Ce que la fenêtre explique d'abord.</summary>
    public const string Help = "Les échéances déplacées sont de simples reports, inscrits dans l'historique : aucune fiche ne recommence à J0, et rien n'est validé à votre place.";

    /// <summary>Premier choix : rattraper.</summary>
    public const string CatchUp = "Rattraper un retard";

    /// <summary>Ce que fait le rattrapage.</summary>
    public const string CatchUpHelp = "Les révisions les plus fragiles restent aujourd'hui ; les autres partent sur les jours suivants.";

    /// <summary>Second choix : la pause.</summary>
    public const string Pause = "Prévoir une pause";

    /// <summary>Ce que fait la pause.</summary>
    public const string PauseHelp = "Ce qui tombe pendant ces jours part après votre retour.";

    /// <summary>Le plafond.</summary>
    public const string PerDay = "Révisions par jour, au plus";

    /// <summary>Le premier jour de la pause.</summary>
    public const string From = "Premier jour sans révision";

    /// <summary>Le dernier jour de la pause.</summary>
    public const string To = "Dernier jour sans révision";

    /// <summary>Le bouton qui applique.</summary>
    public const string Confirm = "Étaler";

    /// <summary>Le plafond n'est pas un nombre.</summary>
    public const string PerDayInvalid = "Donnez un nombre de révisions par jour, de 1 à 500.";

    /// <summary>Les jours de la pause manquent.</summary>
    public const string DaysRequired = "Donnez le premier et le dernier jour de la pause.";

    /// <summary>Rien à rattraper.</summary>
    public static string NothingLate(int kept) => kept == 0
        ? "Rien n'est dû aujourd'hui : il n'y a rien à étaler."
        : string.Create(CultureInfo.CurrentCulture, $"Rien à étaler : ce qui est dû aujourd'hui ({kept}) tient dans ce plafond.");

    /// <summary>Le plan d'un rattrapage.</summary>
    public static string CatchUpPlan(int kept, int moved, DateOnly last) =>
        string.Create(CultureInfo.CurrentCulture, $"{Reviews(kept)} aujourd'hui ; {Leave(moved)} sur les jours suivants, jusqu'au {Dates.Long(last)}.");

    /// <summary>Rien ne tombe pendant la pause.</summary>
    public const string NothingInPause = "Rien ne tombe pendant ces jours : il n'y a rien à déplacer.";

    /// <summary>Le plan d'une pause.</summary>
    public static string PausePlan(int moved, DateOnly last) =>
        string.Create(CultureInfo.CurrentCulture, $"{Leave(moved)} après votre retour, jusqu'au {Dates.Long(last)}.");

    /// <summary>Ce qui a été fait.</summary>
    public static string Done(int moved, DateOnly last) => moved > 1
        ? string.Create(CultureInfo.CurrentCulture, $"{moved} échéances reportées, jusqu'au {Dates.Long(last)}.")
        : string.Create(CultureInfo.CurrentCulture, $"1 échéance reportée au {Dates.Long(last)}.");

    /// <summary>Rien n'a bougé.</summary>
    public const string NothingDone = "Rien n'était à étaler : aucune échéance n'a bougé.";

    private static string Reviews(int count) => count > 1
        ? string.Create(CultureInfo.CurrentCulture, $"{count} révisions restent")
        : string.Create(CultureInfo.CurrentCulture, $"{count} révision reste");

    private static string Leave(int count) => count > 1
        ? string.Create(CultureInfo.CurrentCulture, $"{count} échéances partent")
        : string.Create(CultureInfo.CurrentCulture, $"{count} échéance part");
}

/// <summary>
/// L'étalement des échéances, depuis « Aujourd'hui » : rattraper un retard (retour d'absence) ou libérer des jours de pause.
/// La fenêtre montre le plan avant qu'on l'accepte, et il se recalcule à chaque changement. Rien ne recommence : ce sont
/// des reports.
/// </summary>
public sealed partial class DashboardSpread : ObservableObject
{
    private const int DefaultPerDay = 10;
    private const int DefaultPauseDays = 7;

    private readonly ExamUseCases _planning;
    private readonly Func<Task> _refresh;
    private readonly TimeProvider _time;

    /// <summary>Nombre d'oublis du coffre : une réponse partie avant le dernier n'y remet rien.</summary>
    private int _generation;

    /// <summary>Numéro du dernier plan demandé : seul lui s'affiche.</summary>
    private int _ticket;

    internal DashboardSpread(ExamUseCases planning, Func<Task> refresh, TimeProvider time)
    {
        _planning = planning;
        _refresh = refresh;
        _time = time;
        PerDay = DefaultPerDay.ToString(CultureInfo.CurrentCulture);
        Plan = string.Empty;
        Message = string.Empty;
        Error = string.Empty;
    }

    /// <summary>La fenêtre est ouverte.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>On prévoit une pause ; sinon, on rattrape un retard.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCatchUp))]
    public partial bool IsPause { get; set; }

    /// <summary>On rattrape un retard.</summary>
    public bool IsCatchUp
    {
        get => !IsPause;
        set => IsPause = !value;
    }

    /// <summary>Le plafond par jour, tel qu'il est tapé.</summary>
    [ObservableProperty]
    public partial string PerDay { get; set; }

    /// <summary>Premier jour de la pause.</summary>
    [ObservableProperty]
    public partial DateOnly? PauseFrom { get; set; }

    /// <summary>Dernier jour de la pause.</summary>
    [ObservableProperty]
    public partial DateOnly? PauseTo { get; set; }

    /// <summary>Une pause commence au plus tôt demain.</summary>
    [ObservableProperty]
    public partial DateOnly? Earliest { get; private set; }

    /// <summary>Ce que ferait l'étalement, ou ce qui manque pour le calculer.</summary>
    [ObservableProperty]
    public partial string Plan { get; private set; }

    /// <summary>Ce qui vient d'être fait.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string Message { get; private set; }

    /// <summary>Quelque chose vient d'être fait.</summary>
    public bool HasMessage => Message.Length > 0;

    /// <summary>Le refus du coffre.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string Error { get; private set; }

    /// <summary>Le coffre a refusé.</summary>
    public bool HasError => Error.Length > 0;

    /// <summary>Un étalement est en cours.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Rien n'est en cours.</summary>
    public bool IsIdle => !IsBusy;

    private DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    /// <summary>Oublie tout ce qui vient du coffre.</summary>
    public void Forget()
    {
        _generation++;
        _ticket++;
        IsOpen = false;
        IsBusy = false;
        Plan = string.Empty;
        ClearFeedback();
    }

    /// <summary>Les messages se ferment quand on revient sur la vue.</summary>
    internal void ClearFeedback()
    {
        Message = string.Empty;
        Error = string.Empty;
    }

    /// <summary>La demande telle qu'elle est remplie ; rien, avec la raison, si elle ne peut pas partir.</summary>
    internal SpreadRequest? Request(out string reason)
    {
        reason = string.Empty;
        if (!int.TryParse(PerDay.Trim(), NumberStyles.None, CultureInfo.CurrentCulture, out var perDay) || perDay is < 1 or > SpreadRequest.MaxPerDay)
        {
            reason = SpreadText.PerDayInvalid;
            return null;
        }
        if (!IsPause)
        {
            return new SpreadRequest(perDay);
        }
        if (PauseFrom is not { } from || PauseTo is not { } to)
        {
            reason = SpreadText.DaysRequired;
            return null;
        }
        return new SpreadRequest(perDay, from, to);
    }

    /// <summary>Ouvre la fenêtre : le rattrapage d'abord, le plafond de la dernière fois, une pause d'une semaine dès demain.</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private Task OpenAsync()
    {
        var tomorrow = Today.AddDays(1);
        Earliest = tomorrow;
        PauseFrom = tomorrow;
        PauseTo = tomorrow.AddDays(DefaultPauseDays - 1);
        IsPause = false;
        ClearFeedback();
        IsOpen = true;
        return ShowPlanAsync();
    }

    /// <summary>La réponse à la fenêtre : étaler, ou ne rien faire.</summary>
    [RelayCommand]
    private async Task AnswerAsync(bool confirmed)
    {
        IsOpen = false;
        _ticket++;
        if (!confirmed)
        {
            return;
        }
        if (Request(out var reason) is not { } request)
        {
            Error = reason;
            return;
        }
        var generation = _generation;
        IsBusy = true;
        ClearFeedback();
        try
        {
            var outcome = await Task.Run(() => _planning.Spread(request)).ConfigureAwait(true);
            if (generation != _generation)
            {
                return;
            }
            if (outcome.Succeeded)
            {
                Message = outcome.Value is { Moved: > 0, LastDay: { } last } ? SpreadText.Done(outcome.Value.Moved, last) : SpreadText.NothingDone;
                await _refresh().ConfigureAwait(true);
            }
            else
            {
                Error = outcome.Error!;
            }
        }
        finally
        {
            if (generation == _generation)
            {
                IsBusy = false;
            }
        }
    }

    partial void OnIsPauseChanged(bool value) => _ = ShowPlanAsync();

    partial void OnPerDayChanged(string value) => _ = ShowPlanAsync();

    partial void OnPauseFromChanged(DateOnly? value) => _ = ShowPlanAsync();

    partial void OnPauseToChanged(DateOnly? value) => _ = ShowPlanAsync();

    /// <summary>Calcule le plan de ce qui est rempli, sans rien écrire ; un plan plus récent le remplace.</summary>
    internal async Task ShowPlanAsync()
    {
        if (!IsOpen)
        {
            return;
        }
        var ticket = ++_ticket;
        if (Request(out var reason) is not { } request)
        {
            Plan = reason;
            return;
        }
        var outcome = await Task.Run(() => _planning.PlanSpread(request)).ConfigureAwait(true);
        if (ticket != _ticket)
        {
            return;
        }
        Plan = outcome switch
        {
            { Succeeded: false } => outcome.Error!,
            { Value: { Moved: > 0, LastDay: { } last } plan } => request.PauseFrom is null ? SpreadText.CatchUpPlan(plan.Kept, plan.Moved, last) : SpreadText.PausePlan(plan.Moved, last),
            _ => request.PauseFrom is null ? SpreadText.NothingLate(outcome.Value.Kept) : SpreadText.NothingInPause,
        };
    }
}
