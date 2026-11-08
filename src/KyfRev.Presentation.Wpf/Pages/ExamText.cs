using System.Globalization;
using System.Resources;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Textes des examens, ceux de la 0.x, lus dans les ressources (<c>Resources/ExamText.resx</c>).</summary>
public static class ExamText
{
    private static readonly ResourceManager Resources = new("KyfRev.Presentation.Wpf.Resources.ExamText", typeof(ExamText).Assembly);

    /// <summary>« Examens ».</summary>
    public static string Exams => Text(nameof(Exams));

    /// <summary>« Examen… ».</summary>
    public static string NewExam => Text(nameof(NewExam));

    /// <summary>« Fixer une date d'examen ».</summary>
    public static string NewExamName => Text(nameof(NewExamName));

    /// <summary>« Date d'examen ».</summary>
    public static string EditorNew => Text(nameof(EditorNew));

    /// <summary>« Modifier l'examen ».</summary>
    public static string EditorEdit => Text(nameof(EditorEdit));

    /// <summary>« Le tableau de bord compte les jours qui restent et signale les révisions qui tomberaient après l'examen. ».</summary>
    public static string EditorHelp => Text(nameof(EditorHelp));

    /// <summary>« Nom ».</summary>
    public static string NameLabel => Text(nameof(NameLabel));

    /// <summary>« Bac blanc ».</summary>
    public static string NamePlaceholder => Text(nameof(NamePlaceholder));

    /// <summary>« Dossier concerné ».</summary>
    public static string FolderLabel => Text(nameof(FolderLabel));

    /// <summary>« Date ».</summary>
    public static string DateLabel => Text(nameof(DateLabel));

    /// <summary>« Enregistrer ».</summary>
    public static string Save => Text(nameof(Save));

    /// <summary>« Tout le coffre ».</summary>
    public static string WholeVault => Text(nameof(WholeVault));

    /// <summary>« Donnez un nom à l'examen (« Bac blanc », « Partiel de janvier »…). ».</summary>
    public static string NameRequired => Text(nameof(NameRequired));

    /// <summary>« Choisissez la date de l'examen. ».</summary>
    public static string DayRequired => Text(nameof(DayRequired));

    /// <summary>« aujourd'hui ».</summary>
    public static string Today => Text(nameof(Today));

    /// <summary>« demain ».</summary>
    public static string Tomorrow => Text(nameof(Tomorrow));

    /// <summary>« J-… ».</summary>
    public static string DaysLeftOf(object days) => Format("DaysLeft", days);

    /// <summary>« passé depuis … jour ».</summary>
    public static string PastOneOf(object days) => Format("PastOne", days);

    /// <summary>« passé depuis … jours ».</summary>
    public static string PastManyOf(object days) => Format("PastMany", days);

    /// <summary>« … élément actif ».</summary>
    public static string ItemsOneOf(object count) => Format("ItemsOne", count);

    /// <summary>« … éléments actifs ».</summary>
    public static string ItemsManyOf(object count) => Format("ItemsMany", count);

    /// <summary>« …, dont  ».</summary>
    public static string WithIncludingOf(object detail) => Format("WithIncluding", detail);

    /// <summary>« … prévu après l'examen ».</summary>
    public static string AfterOneOf(object count) => Format("AfterOne", count);

    /// <summary>« … prévus après l'examen ».</summary>
    public static string AfterManyOf(object count) => Format("AfterMany", count);

    /// <summary>« Examen « … », …, …, … » : ce que lit le lecteur d'écran.</summary>
    public static string SpokenOf(object name, object countdown, object day, object detail) => Format("Spoken", name, countdown, day, detail);

    /// <summary>« Resserrer… ».</summary>
    public static string TightenAction => Text(nameof(TightenAction));

    /// <summary>« Resserrer avant l'examen … ».</summary>
    public static string TightenNameOf(object name) => Format("TightenName", name);

    /// <summary>« Modifier… ».</summary>
    public static string EditAction => Text(nameof(EditAction));

    /// <summary>« Retirer l'examen… ».</summary>
    public static string WithdrawAction => Text(nameof(WithdrawAction));

    /// <summary>« Autres actions de l'examen … ».</summary>
    public static string MoreActionsOf(object name) => Format("MoreActions", name);

    /// <summary>« Resserrer avant l'examen ? ».</summary>
    public static string TightenTitle => Text(nameof(TightenTitle));

    /// <summary>« … échéance tombe ».</summary>
    public static string TightenFallsOneOf(object count) => Format("TightenFallsOne", count);

    /// <summary>« … échéances tombent ».</summary>
    public static string TightenFallsManyOf(object count) => Format("TightenFallsMany", count);

    /// <summary>« … après la veille de « … » (…). Elles seront ramenées avant, réparties de demain à la veille, comme des reports : l'historique les garde, et chaque échéance peut être déplacée de nouveau. ».</summary>
    public static string TightenMessageOf(object falls, object name, object day) => Format("TightenMessage", falls, name, day);

    /// <summary>« Resserrer ».</summary>
    public static string TightenConfirm => Text(nameof(TightenConfirm));

    /// <summary>« Rien à resserrer : aucune échéance ne tombe après l'examen. ».</summary>
    public static string NothingToTighten => Text(nameof(NothingToTighten));

    /// <summary>« … échéance ramenée avant « … », réparties jusqu'à la veille. ».</summary>
    public static string TightenedOneOf(object count, object name) => Format("TightenedOne", count, name);

    /// <summary>« … échéances ramenées avant « … », réparties jusqu'à la veille. ».</summary>
    public static string TightenedManyOf(object count, object name) => Format("TightenedMany", count, name);

    /// <summary>« Retirer l'examen ? ».</summary>
    public static string WithdrawTitle => Text(nameof(WithdrawTitle));

    /// <summary>« « … » ne sera plus affiché. Les calendriers de révision ne changent pas. ».</summary>
    public static string WithdrawMessageOf(object name) => Format("WithdrawMessage", name);

    /// <summary>« Retirer ».</summary>
    public static string WithdrawConfirm => Text(nameof(WithdrawConfirm));

    /// <summary>« Examen « … » retiré ; les calendriers ne changent pas. ».</summary>
    public static string WithdrawnOf(object name) => Format("Withdrawn", name);

    private static string Format(string name, params object[] values) => string.Format(CultureInfo.CurrentCulture, Text(name), values);

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » absent des ressources.");
}
