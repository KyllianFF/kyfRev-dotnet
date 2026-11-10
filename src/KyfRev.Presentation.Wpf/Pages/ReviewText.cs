using System.Globalization;
using System.Resources;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Textes de la révision d'une fiche ou d'un passage, ceux de la 0.x, lus dans les ressources (<c>Resources/ReviewText.resx</c>).</summary>
public static class ReviewText
{
    private static readonly ResourceManager Resources = new("KyfRev.Presentation.Wpf.Resources.ReviewText", typeof(ReviewText).Assembly);

    /// <summary>« La position de ce passage dans la fiche est à vérifier : le texte ci-dessus reste celui que vous aviez surligné. ».</summary>
    public static string AnchorNeedsReview => Text(nameof(AnchorNeedsReview));

    /// <summary>« La position de ce passage dans la fiche est introuvable : le texte ci-dessus reste celui que vous aviez surligné. ».</summary>
    public static string AnchorNotFound => Text(nameof(AnchorNotFound));

    /// <summary>« Archiver… ».</summary>
    public static string ArchiveAction => Text(nameof(ArchiveAction));

    /// <summary>« Archiver ».</summary>
    public static string ArchiveConfirm => Text(nameof(ArchiveConfirm));

    /// <summary>« Elle quitte les révisions actives mais reste consultable dans « Archives ». Rien n'est supprimé, et ses passages annotés gardent leur propre calendrier. ».</summary>
    public static string ArchiveMessage => Text(nameof(ArchiveMessage));

    /// <summary>« Archiver la fiche ? ».</summary>
    public static string ArchiveTitle => Text(nameof(ArchiveTitle));

    /// <summary>« Fiche archivée. Elle reste consultable dans « Archives ». ».</summary>
    public static string ArchivedMessage => Text(nameof(ArchivedMessage));

    /// <summary>« le … ».</summary>
    public static string ArchivedOnOf(object day) => Format("ArchivedOn", day);

    /// <summary>« Retour ».</summary>
    public static string Back => Text(nameof(Back));

    /// <summary>« Annuler ».</summary>
    public static string CancelDay => Text(nameof(CancelDay));

    /// <summary>« Reporter à une date… ».</summary>
    public static string ChoosePostponeDay => Text(nameof(ChoosePostponeDay));

    /// <summary>« Valider à une date passée… ».</summary>
    public static string ChooseValidationDay => Text(nameof(ChooseValidationDay));

    /// <summary>« Cette révision est prévue …. La valider au … fera repartir le calendrier de ce jour-là. ».</summary>
    public static string EarlyOnDayMessageOf(object due, object day) => Format("EarlyOnDayMessage", due, day);

    /// <summary>« Date du report ».</summary>
    public static string PostponeDay => Text(nameof(PostponeDay));

    /// <summary>« Reporter à cette date ».</summary>
    public static string PostponeOnThatDay => Text(nameof(PostponeOnThatDay));

    /// <summary>« Valider à cette date ».</summary>
    public static string ValidateOnThatDay => Text(nameof(ValidateOnThatDay));

    /// <summary>« … validée au …. Prochaine étape : …, …. ».</summary>
    public static string ValidatedOnDayOf(object step, object day, object next, object due) => Format("ValidatedOnDay", step, day, next, due);

    /// <summary>« Date de la révision ».</summary>
    public static string ValidationDay => Text(nameof(ValidationDay));

    /// <summary>« … cycle(s) terminé(s) ».</summary>
    public static string CyclesCompletedOf(object count) => Format("CyclesCompleted", count);

    /// <summary>« Définition ».</summary>
    public static string Definition => Text(nameof(Definition));

    /// <summary>« archivée ».</summary>
    public static string DefinitionArchivedStep => Text(nameof(DefinitionArchivedStep));

    /// <summary>« en retard de … j (prévue …) ».</summary>
    public static string DueLateOf(object days, object day) => Format("DueLate", days, day);

    /// <summary>« prévue … ».</summary>
    public static string DueOnOf(object day) => Format("DueOn", day);

    /// <summary>« prévue aujourd'hui ».</summary>
    public static string DueToday => Text(nameof(DueToday));

    /// <summary>« Valider maintenant ».</summary>
    public static string EarlyConfirm => Text(nameof(EarlyConfirm));

    /// <summary>« Cette révision est prévue …. La valider maintenant fera repartir le calendrier d'aujourd'hui. ».</summary>
    public static string EarlyMessageOf(object day) => Format("EarlyMessage", day);

    /// <summary>« Réviser en avance ? ».</summary>
    public static string EarlyTitle => Text(nameof(EarlyTitle));

    /// <summary>« prochain rappel possible à partir du … ».</summary>
    public static string EligibleOnOf(object day) => Format("EligibleOn", day);

    /// <summary>« Exclure des rappels ».</summary>
    public static string ExcludeFromReminders => Text(nameof(ExcludeFromReminders));

    /// <summary>« Fiche exclue des rappels de consolidation. ».</summary>
    public static string Excluded => Text(nameof(Excluded));

    /// <summary>« exclue des rappels ».</summary>
    public static string ExcludedFromReminders => Text(nameof(ExcludedFromReminders));

    /// <summary>« Étape attendue ».</summary>
    public static string ExpectedStep => Text(nameof(ExpectedStep));

    /// <summary>« passage caché : … ».</summary>
    public static string HiddenPassageOf(object words) => Format("HiddenPassage", words);

    /// <summary>« … mots ».</summary>
    public static string HiddenWordsManyOf(object count) => Format("HiddenWordsMany", count);

    /// <summary>« … mot ».</summary>
    public static string HiddenWordsOneOf(object count) => Format("HiddenWordsOne", count);

    /// <summary>« Réinclure dans les rappels ».</summary>
    public static string IncludeInReminders => Text(nameof(IncludeInReminders));

    /// <summary>« Fiche réincluse dans les rappels de consolidation. ».</summary>
    public static string Included => Text(nameof(Included));

    /// <summary>« Autres actions de révision ».</summary>
    public static string MoreActions => Text(nameof(MoreActions));

    /// <summary>« Mon commentaire : ».</summary>
    public static string MyComment => Text(nameof(MyComment));

    /// <summary>« Prochaine étape ».</summary>
    public static string NextStep => Text(nameof(NextStep));

    /// <summary>« Ouverture du passage… ».</summary>
    public static string OpeningPassage => Text(nameof(OpeningPassage));

    /// <summary>« Ouverture de la fiche… ».</summary>
    public static string OpeningSheet => Text(nameof(OpeningSheet));

    /// <summary>« Passage ».</summary>
    public static string Passage => Text(nameof(Passage));

    /// <summary>« Passage archivé ».</summary>
    public static string PassageArchived => Text(nameof(PassageArchived));

    /// <summary>« archivé ».</summary>
    public static string PassageArchivedStep => Text(nameof(PassageArchivedStep));

    /// <summary>« Ce passage est introuvable. ».</summary>
    public static string PassageNotFound => Text(nameof(PassageNotFound));

    /// <summary>« Reporter de 3 jours ».</summary>
    public static string PostponeThreeDays => Text(nameof(PostponeThreeDays));

    /// <summary>« Reporter ».</summary>
    public static string Postpone => Text(nameof(Postpone));

    /// <summary>« Reporter à demain ».</summary>
    public static string PostponeTomorrow => Text(nameof(PostponeTomorrow));

    /// <summary>« … reportée au …. ».</summary>
    public static string PostponedOf(object step, object day) => Format("Postponed", step, day);

    /// <summary>« Rappelez-vous sa définition, puis vérifiez. ».</summary>
    public static string RecallDefinition => Text(nameof(RecallDefinition));

    /// <summary>« Rappelez-vous le passage surligné, puis vérifiez. ».</summary>
    public static string RecallPassage => Text(nameof(RecallPassage));

    /// <summary>« rappel en attente ».</summary>
    public static string ReminderPending => Text(nameof(ReminderPending));

    /// <summary>« Recommencer à J0… ».</summary>
    public static string RestartAction => Text(nameof(RestartAction));

    /// <summary>« Remettre en révision (J0) ».</summary>
    public static string RestartArchived => Text(nameof(RestartArchived));

    /// <summary>« Recommencer ».</summary>
    public static string RestartConfirm => Text(nameof(RestartConfirm));

    /// <summary>« Le cycle en cours du passage sera interrompu et un nouveau cycle commencera aujourd'hui. L'historique est conservé. ».</summary>
    public static string RestartPassageMessage => Text(nameof(RestartPassageMessage));

    /// <summary>« Le cycle en cours de la fiche sera interrompu et un nouveau cycle commencera aujourd'hui. L'historique est conservé. ».</summary>
    public static string RestartSheetMessage => Text(nameof(RestartSheetMessage));

    /// <summary>« Recommencer à J0 ? ».</summary>
    public static string RestartTitle => Text(nameof(RestartTitle));

    /// <summary>« Nouveau cycle : J0 validé aujourd'hui, … …. ».</summary>
    public static string RestartedOf(object step, object day) => Format("Restarted", step, day);

    /// <summary>« Révision ».</summary>
    public static string Review => Text(nameof(Review));

    /// <summary>« Révision du jour enregistrée ».</summary>
    public static string ReviewedToday => Text(nameof(ReviewedToday));

    /// <summary>« Voir dans la fiche ».</summary>
    public static string SeeInSheet => Text(nameof(SeeInSheet));

    /// <summary>« Fiche archivée ».</summary>
    public static string SheetArchived => Text(nameof(SheetArchived));

    /// <summary>« Fiche ».</summary>
    public static string SheetPage => Text(nameof(SheetPage));

    /// <summary>« Afficher la définition ».</summary>
    public static string ShowDefinition => Text(nameof(ShowDefinition));

    /// <summary>« Afficher le passage ».</summary>
    public static string ShowPassage => Text(nameof(ShowPassage));

    /// <summary>« … : validée le … ».</summary>
    public static string StepDoneOf(object step, object day) => Format("StepDone", step, day);

    /// <summary>« … : validée le … (… j en avance) ».</summary>
    public static string StepDoneEarlyOf(object step, object day, object days) => Format("StepDoneEarly", step, day, days);

    /// <summary>« … : validée le … (… j de retard) ».</summary>
    public static string StepDoneLateOf(object step, object day, object days) => Format("StepDoneLate", step, day, days);

    /// <summary>« … : étape attendue ».</summary>
    public static string StepExpectedOf(object step) => Format("StepExpected", step);

    /// <summary>« … : étape attendue, prévue … ».</summary>
    public static string StepExpectedOnOf(object step, object day) => Format("StepExpectedOn", step, day);

    /// <summary>« … : à venir ».</summary>
    public static string StepUpcomingOf(object step) => Format("StepUpcoming", step);

    /// <summary>« Étapes du cycle ».</summary>
    public static string Steps => Text(nameof(Steps));

    /// <summary>« J'avais oublié ».</summary>
    public static string Forgot => Text(nameof(Forgot));

    /// <summary>« La révision compte, et l'étape recule d'un cran au lieu d'avancer. ».</summary>
    public static string ForgotHelp => Text(nameof(ForgotHelp));

    /// <summary>« … revue, oubli noté. Elle reviendra à l'étape …, …. ».</summary>
    public static string ForgotDoneOf(object done, object step, object day) => Format("ForgotDone", done, step, day);

    /// <summary>« Valider la révision … ».</summary>
    public static string ValidateOf(object step) => Format("Validate", step);

    /// <summary>« … validée. Prochaine étape : …, …. ».</summary>
    public static string ValidatedOf(object done, object step, object day) => Format("Validated", done, step, day);

    /// <summary>« … validée : cycle terminé. Le passage est archivé. ».</summary>
    public static string ValidatedLastPassageOf(object done) => Format("ValidatedLastPassage", done);

    /// <summary>« … validée : cycle terminé. La fiche est archivée ; un rappel de consolidation viendra dans deux mois. ».</summary>
    public static string ValidatedLastSheetOf(object done) => Format("ValidatedLastSheet", done);

    /// <summary>« Revoir toute la fiche ? ».</summary>
    public static string EntirelyTitle => Text(nameof(EntirelyTitle));

    /// <summary>« Elle redevient active : … ».</summary>
    public static string EntirelyMessage => Text(nameof(EntirelyMessage));

    /// <summary>« Remettre en révision ».</summary>
    public static string EntirelyConfirm => Text(nameof(EntirelyConfirm));

    /// <summary>« La fiche est de nouveau en révision : J1 …. ».</summary>
    public static string EntirelyDoneOf(object day) => Format("EntirelyDone", day);

    /// <summary>« Annuler ».</summary>
    public static string CancelMarking => Text(nameof(CancelMarking));

    /// <summary>« Terminer ».</summary>
    public static string FinishMarking => Text(nameof(FinishMarking));

    /// <summary>« Fiche connue : elle reste archivée. ».</summary>
    public static string KnownDone => Text(nameof(KnownDone));

    /// <summary>« Remis à J0 ».</summary>
    public static string Marked => Text(nameof(Marked));

    /// <summary>« 1 passage marqué ».</summary>
    public static string MarkedOneOf(object count) => Format("MarkedOne", count);

    /// <summary>« 2 passages marqués ».</summary>
    public static string MarkedManyOf(object count) => Format("MarkedMany", count);

    /// <summary>« Remettre à J0 ».</summary>
    public static string MarkPassage => Text(nameof(MarkPassage));

    /// <summary>« Prochain rappel possible à partir du …. ».</summary>
    public static string NextReminderOf(object day) => Format("NextReminder", day);

    /// <summary>« Aucun passage marqué ».</summary>
    public static string NoMarkTitle => Text(nameof(NoMarkTitle));

    /// <summary>« « Certains points sont à revoir » demande … ».</summary>
    public static string NoMarkMessage => Text(nameof(NoMarkMessage));

    /// <summary>« Passage remis à J0 : à revoir dès demain. ».</summary>
    public static string PassageMarked => Text(nameof(PassageMarked));

    /// <summary>« Fiche à revoir entièrement… ».</summary>
    public static string ReminderEntirely => Text(nameof(ReminderEntirely));

    /// <summary>« Fiche archivée le …. Relisez-la, puis dites ce qu'il en reste. ».</summary>
    public static string ReminderIntroOf(object day) => Format("ReminderIntro", day);

    /// <summary>« Fiche connue ».</summary>
    public static string ReminderKnown => Text(nameof(ReminderKnown));

    /// <summary>« Remettez à J0 les passages à revoir… ».</summary>
    public static string ReminderMarking => Text(nameof(ReminderMarking));

    /// <summary>« Cette fiche n'a aucun passage annoté. ».</summary>
    public static string ReminderNoPassage => Text(nameof(ReminderNoPassage));

    /// <summary>« Certains points sont à revoir ».</summary>
    public static string ReminderSomePoints => Text(nameof(ReminderSomePoints));

    /// <summary>« Rappel de consolidation ».</summary>
    public static string ReminderTitle => Text(nameof(ReminderTitle));

    /// <summary>« 1 passage à revoir dès demain (J1). La fiche reste archivée. ».</summary>
    public static string SomeDoneOneOf(object count) => Format("SomeDoneOne", count);

    /// <summary>« 2 passages à revoir dès demain (J1). La fiche reste archivée. ».</summary>
    public static string SomeDoneManyOf(object count) => Format("SomeDoneMany", count);

    private static string Format(string name, params object[] values) => string.Format(CultureInfo.CurrentCulture, Text(name), values);

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » absent des ressources.");
}
