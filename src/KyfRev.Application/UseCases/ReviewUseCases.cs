using KyfRev.Application.Model;
using KyfRev.Domain.Reviews;

namespace KyfRev.Application.UseCases;

/// <summary>
/// Méthode des J d'une fiche ou d'un passage. Valider une révision est toujours une action
/// explicite : ouvrir une fiche ne valide rien. Chaque opération rend l'état qui en résulte.
/// </summary>
public sealed class ReviewUseCases(VaultHost host, TimeProvider time)
{
    /// <summary>Valide la révision attendue (une seule par élément et par jour).</summary>
    [ZeroXCommand("valider_revision")]
    public Outcome<ReviewState> Validate(ItemKind kind, string id)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault =>
        {
            vault.ValidateReview(kind, id, now);
            return vault.ReviewStateOf(kind, id, now.Day);
        });
    }

    /// <summary>Reporte l'échéance au jour <paramref name="newDueDate"/>.</summary>
    [ZeroXCommand("reporter_revision")]
    public Outcome<ReviewState> Postpone(ItemKind kind, string id, DateOnly newDueDate)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault =>
        {
            vault.Postpone(kind, id, now, newDueDate);
            return vault.ReviewStateOf(kind, id, now.Day);
        });
    }

    /// <summary>Recommence le cycle à J0, avec la séquence du coffre.</summary>
    [ZeroXCommand("recommencer_cycle")]
    public Outcome<ReviewState> Restart(ItemKind kind, string id)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault =>
        {
            vault.Restart(kind, id, now);
            return vault.ReviewStateOf(kind, id, now.Day);
        });
    }

    /// <summary>Archive une fiche à la main.</summary>
    [ZeroXCommand("archiver_fiche")]
    public Outcome<ReviewState> Archive(string id)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault =>
        {
            vault.ArchiveSheet(id, now);
            return vault.ReviewStateOf(ItemKind.Sheet, id, now.Day);
        });
    }
}

/// <summary>
/// Rappels de consolidation : le tirage du jour (une seule fois par jour), et les réponses. Le
/// résultat d'un rappel n'est enregistré que sur un choix explicite.
/// </summary>
public sealed class ReminderUseCases(VaultHost host, TimeProvider time)
{
    /// <summary>
    /// Prépare la journée (tirage des rappels) ; sans effet si elle l'est déjà. Fait à l'ouverture
    /// du coffre, et de nouveau par le tableau de bord si l'application est restée ouverte après
    /// minuit.
    /// </summary>
    public Outcome<DayPreparation> PrepareDay()
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.PrepareDay(now));
    }

    /// <summary>
    /// Tableau de bord du jour. La journée est d'abord préparée : si l'application est restée
    /// ouverte après minuit, les rappels du nouveau jour sont tirés.
    /// </summary>
    [ZeroXCommand("tableau_de_bord")]
    public Outcome<Dashboard> Dashboard()
    {
        var now = Present.Of(time).Now;
        return host.Use(vault =>
        {
            vault.PrepareDay(now);
            return vault.Dashboard(now.Day);
        });
    }

    /// <summary>Fiches archivées et où elles en sont des rappels.</summary>
    [ZeroXCommand("archives")]
    public Outcome<IReadOnlyList<ArchivedSheetEntry>> Archives()
    {
        var day = Present.Of(time).Now.Day;
        return host.Use(vault => vault.Archives(day));
    }

    /// <summary>Réponse à un rappel : connue, certains points à revoir, ou à revoir entièrement.</summary>
    [ZeroXCommand("traiter_rappel")]
    public Outcome<Done> Handle(string sheetId, ReminderOutcome choice)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.HandleReminder(sheetId, choice, now));
    }

    /// <summary>« Exclure des rappels » ou « Réinclure ».</summary>
    [ZeroXCommand("inclure_dans_rappels")]
    public Outcome<Done> Include(string sheetId, bool include)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.IncludeInReminders(sheetId, include, now));
    }
}

/// <summary>Passages annotés et définitions, chacun avec son propre calendrier des J.</summary>
public sealed class AnnotationUseCases(VaultHost host, TimeProvider time)
{
    /// <summary>Surligne un passage, avec un commentaire facultatif ; rend son identifiant.</summary>
    [ZeroXCommand("creer_annotation")]
    public Outcome<string> Create(string sheetId, Anchor anchor, string? comment)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.CreateAnnotation(sheetId, anchor, comment, now));
    }

    /// <summary>Le glossaire du coffre.</summary>
    [ZeroXCommand("liste_des_definitions")]
    public Outcome<IReadOnlyList<VaultDefinition>> Definitions()
    {
        var day = Present.Of(time).Now.Day;
        return host.Use(vault => vault.Definitions(day));
    }

    /// <summary>Pose une définition (terme et explication) sur un passage ; sa création vaut J0.</summary>
    [ZeroXCommand("creer_definition")]
    public Outcome<string> CreateDefinition(string sheetId, Anchor anchor, Definition definition)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.CreateDefinition(sheetId, anchor, definition, now));
    }

    /// <summary>Change le terme ou l'explication d'une définition.</summary>
    [ZeroXCommand("modifier_definition")]
    public Outcome<Done> ModifyDefinition(string id, Definition definition)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.ModifyDefinition(id, definition, now));
    }

    /// <summary>Change ou retire le commentaire.</summary>
    [ZeroXCommand("modifier_commentaire")]
    public Outcome<Done> ModifyComment(string id, string? comment)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.ModifyComment(id, comment, now));
    }

    /// <summary>Enregistre un ancrage recalculé à l'ouverture de la fiche.</summary>
    [ZeroXCommand("reancrer_annotation")]
    public Outcome<Done> Reanchor(string id, Anchor anchor, AnchorState state)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.Reanchor(id, anchor, state, now));
    }

    /// <summary>Passage changé à la demande de l'utilisateur ; <paramref name="restart"/> repart de J0.</summary>
    [ZeroXCommand("modifier_passage")]
    public Outcome<Done> ModifyPassage(string id, Anchor anchor, bool restart)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.ModifyPassage(id, anchor, restart, now));
    }

    /// <summary>Met une annotation à la corbeille, restaurable.</summary>
    [ZeroXCommand("mettre_annotation_a_la_corbeille")]
    public Outcome<Done> Trash(string id)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.TrashAnnotation(id, now));
    }

    /// <summary>Remet une annotation de la corbeille.</summary>
    [ZeroXCommand("restaurer_annotation")]
    public Outcome<Done> Restore(string id)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.RestoreAnnotation(id, now));
    }
}
