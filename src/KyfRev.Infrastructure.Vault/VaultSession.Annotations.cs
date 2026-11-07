using System.Collections.Immutable;
using KyfRev.Application.Text;
using KyfRev.Domain.Calendar;
using KyfRev.Domain.Reviews;
using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Json;
using KyfRev.Infrastructure.Vault.Paths;

namespace KyfRev.Infrastructure.Vault;

/// <remarks>
/// Annotations : passages surlignés et définitions, chacun avec son propre calendrier des J,
/// rangés dans <c>.suivi/annotations/&lt;idFiche&gt;.json</c>. Le fichier Markdown n'est jamais
/// modifié ; le passage et le commentaire y sont copiés, si bien qu'une annotation reste
/// révisable même si sa fiche disparaît.
/// </remarks>
public sealed partial class VaultSession
{
    /// <summary>Longueur maximale d'un passage annoté et d'un commentaire, en caractères.</summary>
    public const int MaxPassageChars = 10_000;

    /// <summary>Longueur maximale du terme d'une définition, en caractères.</summary>
    public const int MaxTermChars = 200;

    private SortedDictionary<string, AnnotationsFile> _annotations = new(StringComparer.Ordinal);
    private HashSet<string> _unreadableAnnotations = new(StringComparer.Ordinal);

    /// <summary>Annotations d'une fiche, corbeille comprise, dans l'ordre du fichier.</summary>
    public ImmutableArray<StoredAnnotation> Annotations(string sheetId) =>
        _annotations.TryGetValue(sheetId, out var file) ? file.Annotations : [];

    /// <summary>Toutes les annotations du coffre, corbeille comprise.</summary>
    public IEnumerable<StoredAnnotation> AllAnnotations => _annotations.Values.SelectMany(file => file.Annotations);

    /// <summary>Passages hors corbeille dont la position est à vérifier ou introuvable.</summary>
    public int AnnotationsToCheck => AllAnnotations.Count(a => a.TrashedAt is null && a.AnchorState != AnchorState.Anchored);

    /// <summary>Une annotation, où qu'elle soit.</summary>
    /// <exception cref="VaultException">Aucune annotation ne porte cet identifiant.</exception>
    public StoredAnnotation Annotation(string id) =>
        AllAnnotations.FirstOrDefault(a => a.Id == id) ?? throw VaultException.UnknownAnnotation(id);

    /// <summary>Crée une annotation (passage surligné, commentaire facultatif) sur une fiche présente ; sa création vaut J0.</summary>
    /// <returns>Identifiant de l'annotation.</returns>
    public string CreateAnnotation(string sheetId, StoredAnchor anchor, string? comment, Moment now) =>
        AddAnnotation(sheetId, anchor, CleanComment(comment), definition: null, now);

    /// <summary>Crée une définition (terme et explication) sur un passage d'une fiche présente ; sa création vaut J0.</summary>
    /// <returns>Identifiant de la définition.</returns>
    public string CreateDefinition(string sheetId, StoredAnchor anchor, StoredDefinition definition, Moment now) =>
        AddAnnotation(sheetId, anchor, comment: null, CleanDefinition(definition), now);

    /// <summary>Modifie le terme ou l'explication d'une définition ; la modification est inscrite dans l'historique.</summary>
    public void ModifyDefinition(string id, StoredDefinition definition, Moment now)
    {
        var current = Annotation(id).Definition ?? throw VaultException.NotADefinition();
        var clean = CleanDefinition(definition) with { Everywhere = current.Everywhere };
        if (current == clean)
        {
            return;
        }
        ModifyAnnotation(id, a => a with
        {
            Definition = clean,
            ModifiedAt = now.Instant,
            Tracking = a.Tracking.RecordModification(now, ModifiedObject.Definition),
        });
    }

    /// <summary>
    /// Marque le terme d'une définition dans toutes les fiches, ou seulement à sa place. C'est un choix d'affichage :
    /// ni la date de modification ni l'historique ne changent.
    /// </summary>
    public void MarkDefinitionEverywhere(string id, bool everywhere)
    {
        var current = Annotation(id).Definition ?? throw VaultException.NotADefinition();
        if (current.Everywhere != everywhere)
        {
            ModifyAnnotation(id, a => a with { Definition = current with { Everywhere = everywhere } });
        }
    }

    /// <summary>Modifie le commentaire ; le calendrier ne change pas, la modification est inscrite dans l'historique.</summary>
    public void ModifyComment(string id, string? comment, Moment now)
    {
        var clean = CleanComment(comment);
        if (Annotation(id).Comment == clean)
        {
            return;
        }
        ModifyAnnotation(id, a => a with
        {
            Comment = clean,
            ModifiedAt = now.Instant,
            Tracking = a.Tracking.RecordModification(now, ModifiedObject.Comment),
        });
    }

    /// <summary>
    /// Enregistre la position d'un passage recalculée après une modification de sa fiche, ou son
    /// nouvel état d'ancrage. Le passage lui-même ne change pas ici : jamais de rattachement
    /// automatique à un autre texte.
    /// </summary>
    public void Reanchor(string id, StoredAnchor anchor, AnchorState state, Moment now)
    {
        VerifyAnchor(anchor);
        ModifyAnnotation(id, a => a.Anchor.Exact != anchor.Exact
            ? throw VaultException.InvalidAnchor("le passage ne change que sur demande explicite")
            : a with { Anchor = anchor, AnchorState = state, ModifiedAt = now.Instant });
    }

    /// <summary>
    /// Change le passage, à la demande de l'utilisateur ; le calendrier continue, ou repart de J0
    /// si <paramref name="restart"/>.
    /// </summary>
    public void ModifyPassage(string id, StoredAnchor anchor, bool restart, Moment now)
    {
        VerifyAnchor(anchor);
        var sequence = Settings.Sequence;
        ModifyAnnotation(id, a =>
        {
            if (a.TrashedAt is not null)
            {
                throw VaultException.AnnotationInTrash();
            }
            var changed = a.Anchor.Exact != anchor.Exact;
            a = a with { Anchor = anchor, AnchorState = AnchorState.Anchored, ModifiedAt = now.Instant };
            if (changed)
            {
                a = a with { Tracking = a.Tracking.RecordModification(now, ModifiedObject.Passage) };
            }
            return restart ? a with { Tracking = Started(a.Tracking.Restart(now, sequence)) } : a;
        });
    }

    /// <summary>« Supprimer » une annotation : elle quitte la fiche et les révisions, et reste restaurable.</summary>
    public void TrashAnnotation(string id, Moment now) =>
        ModifyAnnotation(id, a => a with { TrashedAt = a.TrashedAt ?? now.Instant });

    /// <summary>Remet une annotation de la corbeille.</summary>
    public void RestoreAnnotation(string id, Moment now) =>
        ModifyAnnotation(id, a => a with { TrashedAt = null, ModifiedAt = now.Instant });

    /// <summary>Efface pour de bon une annotation déjà à la corbeille : elle quitte le fichier de sa fiche.</summary>
    public void PurgeAnnotation(string id)
    {
        var annotation = Annotation(id);
        if (annotation.TrashedAt is null)
        {
            throw VaultException.UnknownAnnotation(id);
        }
        var sheetId = annotation.SheetId;
        SaveAnnotations(sheetId, ModifiableAnnotations(sheetId).RemoveAll(other => other.Id == id));
    }

    /// <summary>
    /// Applique une opération de la méthode des J au calendrier d'un passage. Valider un passage
    /// ne touche jamais le suivi de sa fiche, et inversement.
    /// </summary>
    /// <returns>Le nouveau suivi.</returns>
    public Tracking ApplyToAnnotationTracking(string id, Func<Tracking, TrackingResult> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Tracking? tracking = null;
        ModifyAnnotation(id, a =>
        {
            if (a.TrashedAt is not null)
            {
                throw VaultException.AnnotationInTrash();
            }
            tracking = Started(operation(a.Tracking));
            return a with { Tracking = tracking };
        });
        return tracking!;
    }

    /// <summary>Valide une révision d'un passage et l'enregistre.</summary>
    /// <returns>L'issue de la révision.</returns>
    public ReviewOutcome ApplyAnnotationReview(string id, Func<Tracking, ReviewResult> review)
    {
        ArgumentNullException.ThrowIfNull(review);
        ReviewOutcome? outcome = null;
        ModifyAnnotation(id, a =>
        {
            if (a.TrashedAt is not null)
            {
                throw VaultException.AnnotationInTrash();
            }
            var result = review(a.Tracking);
            if (!result.IsSuccess)
            {
                throw VaultException.Refused(result.Error);
            }
            outcome = result.Outcome;
            return a with { Tracking = result.Tracking };
        });
        return outcome!;
    }

    /// <summary>
    /// Applique une opération de la méthode des J à une fiche ou à un passage. L'opération reçoit
    /// les règles de rappel : celles du coffre pour une fiche, aucune pour un passage.
    /// </summary>
    public Tracking ApplyTo(ItemKind kind, string id, Func<Tracking, ReminderRules?, TrackingResult> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var rules = Settings.Reminders;
        return kind == ItemKind.Sheet
            ? ApplyToTracking(id, tracking => operation(tracking, rules))
            : ApplyToAnnotationTracking(id, tracking => operation(tracking, null));
    }

    /// <summary>
    /// Charge toutes les annotations pendant l'analyse ; signale les fichiers illisibles (jamais
    /// réécrits) et les annotations dont la fiche est introuvable (elles restent révisables).
    /// </summary>
    private void LoadAnnotations(HealthReport report)
    {
        var annotations = new SortedDictionary<string, AnnotationsFile>(StringComparer.Ordinal);
        var unreadable = new HashSet<string>(StringComparer.Ordinal);
        var folder = VaultLayout.Absolute(Root, VaultLayout.Annotations);
        foreach (var file in Io(folder, () => Directory.EnumerateFileSystemEntries(folder).Order(StringComparer.Ordinal).ToList()))
        {
            var name = Path.GetFileName(file);
            if (Extension(name) != "json")
            {
                continue;
            }
            var sheetId = name[..name.LastIndexOf('.')];
            var shown = $"{VaultLayout.Annotations}/{sheetId}.json";
            try
            {
                var read = AnnotationsFile.Read(File.ReadAllBytes(file));
                if (read.SheetId != sheetId)
                {
                    throw new VaultFormatException("le nom du fichier ne correspond pas à la fiche");
                }
                annotations[sheetId] = read;
            }
            catch (Exception exception) when (exception is VaultFormatException || IsIo(exception))
            {
                report.UnreadableList.Add(new UnreadableFile(shown, exception.Message));
                unreadable.Add(sheetId);
            }
        }
        _annotations = annotations;
        _unreadableAnnotations = unreadable;
        foreach (var annotation in AllAnnotations.Where(a => a.TrashedAt is null))
        {
            var present = _sheets.TryGetValue(annotation.SheetId, out var entry) && entry.Presence != FilePresence.Missing;
            if (!present)
            {
                report.OrphanAnnotationsList.Add(annotation.Id);
            }
        }
    }

    private string AddAnnotation(string sheetId, StoredAnchor anchor, string? comment, StoredDefinition? definition, Moment now)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        _ = AvailableSheet(sheetId);
        VerifyAnchor(anchor);
        var id = $"a_{_ids.Next()}";
        var tracking = Started(Tracking.Start(now, Settings.Sequence, StartOrigin.Creation));
        var annotation = new StoredAnnotation(id, sheetId, now.Instant, now.Instant, comment, definition, anchor, AnchorState.Anchored, TrashedAt: null, tracking, UnknownProperties.None);
        var list = ModifiableAnnotations(sheetId).Add(annotation);
        SaveAnnotations(sheetId, list);
        return id;
    }

    private void ModifyAnnotation(string id, Func<StoredAnnotation, StoredAnnotation> change)
    {
        var sheetId = Annotation(id).SheetId;
        var list = ModifiableAnnotations(sheetId);
        var index = -1;
        for (var i = 0; i < list.Length && index < 0; i++)
        {
            if (list[i].Id == id)
            {
                index = i;
            }
        }
        if (index < 0)
        {
            throw VaultException.UnknownAnnotation(id);
        }
        SaveAnnotations(sheetId, list.SetItem(index, change(list[index])));
    }

    /// <summary>Annotations d'une fiche, à modifier puis enregistrer ; refusé si le fichier est illisible.</summary>
    private ImmutableArray<StoredAnnotation> ModifiableAnnotations(string sheetId) =>
        _unreadableAnnotations.Contains(sheetId) ? throw VaultException.UnreadableAnnotations(sheetId) : Annotations(sheetId);

    private void SaveAnnotations(string sheetId, ImmutableArray<StoredAnnotation> annotations)
    {
        if (!VaultIds.IsSheetId(sheetId))
        {
            throw VaultException.UnknownSheet(sheetId);
        }
        var file = _annotations.TryGetValue(sheetId, out var existing)
            ? existing with { Format = 1, Annotations = annotations }
            : new AnnotationsFile(1, sheetId, annotations, UnknownProperties.None);
        ReplaceFile(Confine(VaultPath.Create($"{VaultLayout.Annotations}/{sheetId}.json")), file.Write());
        _annotations[sheetId] = file;
    }

    private static void VerifyAnchor(StoredAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        if (anchor.Exact.Trim().Length == 0 || anchor.Start >= anchor.End)
        {
            throw VaultException.InvalidAnchor("passage vide");
        }
        if (RustText.CharCount(anchor.Exact) > MaxPassageChars)
        {
            throw VaultException.InvalidAnchor("passage trop long");
        }
    }

    /// <summary>Terme et explication obligatoires, sans espaces superflus.</summary>
    private static StoredDefinition CleanDefinition(StoredDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var term = definition.Term.Trim();
        var explanation = definition.Explanation.Trim();
        if (term.Length == 0)
        {
            throw VaultException.InvalidDefinition("le terme est vide");
        }
        if (explanation.Length == 0)
        {
            throw VaultException.InvalidDefinition("l'explication est vide");
        }
        if (RustText.CharCount(term) > MaxTermChars)
        {
            throw VaultException.InvalidDefinition("terme trop long");
        }
        if (RustText.CharCount(explanation) > MaxPassageChars)
        {
            throw VaultException.InvalidDefinition("explication trop longue");
        }
        return new StoredDefinition(term, explanation) { Everywhere = definition.Everywhere };
    }

    private static string? CleanComment(string? comment)
    {
        var clean = comment?.Trim();
        if (string.IsNullOrEmpty(clean))
        {
            return null;
        }
        return RustText.CharCount(clean) > MaxPassageChars ? throw VaultException.InvalidAnchor("commentaire trop long") : clean;
    }
}
