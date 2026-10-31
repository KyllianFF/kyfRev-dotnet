using System.Collections.Immutable;

namespace KyfRev.Application.Model;

/// <summary>Résultat de la préparation de la journée.</summary>
/// <param name="Day">Jour préparé.</param>
/// <param name="AlreadyPrepared">Le tirage du jour avait déjà été fait : rien n'a changé.</param>
/// <param name="NewReminders">Fiches tirées pour un rappel de consolidation.</param>
/// <param name="UnservedPriority">Fiches prioritaires qui n'ont pas trouvé de place.</param>
public sealed record DayPreparation(DateOnly Day, bool AlreadyPrepared, ImmutableArray<string> NewReminders, int UnservedPriority);

/// <summary>Un examen vu du tableau de bord.</summary>
/// <param name="Folder">Dossier, relatif à <c>fiches/</c> ; vide pour tout le coffre.</param>
/// <param name="Name">Nom de l'examen.</param>
/// <param name="Day">Jour de l'examen.</param>
/// <param name="DaysLeft">Jours avant l'examen (0 le jour même, négatif après).</param>
/// <param name="Items">Fiches et passages actifs du dossier.</param>
/// <param name="AfterExam">Dont l'étape attendue tombe après la veille de l'examen.</param>
public sealed record UpcomingExam(string Folder, string Name, DateOnly Day, int DaysLeft, int Items, int AfterExam);
