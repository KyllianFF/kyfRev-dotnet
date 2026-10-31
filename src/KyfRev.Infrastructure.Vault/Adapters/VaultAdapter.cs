using System.Globalization;
using KyfRev.Application.Ports;
using KyfRev.Domain.Calendar;
using KyfRev.Domain.Reviews;
using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Paths;
using KyfRev.Infrastructure.Vault.Sheets;

namespace KyfRev.Infrastructure.Vault.Adapters;

/// <summary>
/// Le port <see cref="IVault"/> sur une <see cref="VaultSession"/> : les chemins reçus en texte
/// sont validés (refus avec le message de la 0.x), les repères des passages convertis, et la vue
/// d'une fiche ouverte composée comme le fait la 0.x (<c>lecture.rs</c>).
/// </summary>
public sealed class VaultAdapter(VaultSession inner) : IVault
{
    /// <inheritdoc/>
    public string Root => inner.Root;

    /// <inheritdoc/>
    public VaultPreferences Preferences => inner.Preferences;

    /// <inheritdoc/>
    public string Id => inner.Settings.Id;

    /// <summary>Ferme la session : son verrou est libéré.</summary>
    public void Dispose() => inner.Dispose();

    /// <inheritdoc/>
    public string CreateSheet(string? folder, string title, string body, Moment now) => inner.CreateSheet(folder, title, body, now);

    /// <inheritdoc/>
    public OpenedSheet OpenSheet(string id, DateOnly day)
    {
        var content = inner.ReadSheet(id);
        var sheet = inner.Sheet(id).Sheet;
        var bodyStart = FrontMatter.Split(content.Text).BodyStart;
        var parent = sheet.Path.Parent?.Value ?? string.Empty;
        var folder = parent.StartsWith(VaultLayout.Sheets, StringComparison.Ordinal) ? parent[VaultLayout.Sheets.Length..].TrimStart('/') : string.Empty;
        var created = FrontMatter.Read(content.Text) is { IsSuccess: true, Header.Created: { } text }
            && DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var createdOn)
                ? createdOn
                : (DateOnly?)null;
        var annotations = inner.Annotations(id).Select(annotation => new AnnotationView(
            annotation.Id,
            annotation.Comment,
            annotation.Definition is { } definition ? new Definition(definition.Term, definition.Explanation) : null,
            ToAnchor(annotation.Anchor),
            annotation.AnchorState,
            annotation.TrashedAt is not null,
            ReviewState.Of(annotation.Tracking, day)));
        return new OpenedSheet(
            id,
            sheet.Title,
            sheet.Path.Value,
            folder,
            sheet.Tags,
            content.Text[bodyStart..],
            content.Text.AsSpan(0, bodyStart).Count('\n') + 1,
            content.Fingerprint,
            created,
            sheet.ModifiedAt,
            ReviewState.Of(sheet.Tracking, day),
            [.. annotations]);
    }

    /// <inheritdoc/>
    public string? SheetIdAt(string path) => inner.Sheets.FirstOrDefault(entry => entry.Sheet.Path.Value == path)?.Sheet.Id;

    /// <inheritdoc/>
    public void ChangeTitle(string id, string title, Moment now) => inner.ChangeTitle(id, title, now);

    /// <inheritdoc/>
    public IReadOnlyList<string> SetTags(string id, IReadOnlyList<string> tags, Moment now) => inner.SetTags(id, tags, now);

    /// <inheritdoc/>
    public void MoveSheet(string id, string? folder, Moment now) => inner.MoveSheet(id, folder, now);

    /// <inheritdoc/>
    public string TrackFile(string path, Moment now) => inner.TrackFile(VaultPath.Create(path), now);

    /// <inheritdoc/>
    public ImportReport ImportFolder(string source, string name, bool track, Moment now) => inner.ImportFolder(source, name, track, now);

    /// <inheritdoc/>
    public string ShareSheet(string id, ShareFormat format, string folder, string? content) => inner.ShareSheet(id, format, folder, content);

    /// <inheritdoc/>
    public SaveResult SaveBody(string id, string body, string expectedFingerprint, ulong session, Moment now) =>
        inner.SaveBody(id, body, expectedFingerprint, session, now);

    /// <inheritdoc/>
    public void KeepAsVersion(string id, string body, Moment now) => inner.KeepAsVersion(id, body, now);

    /// <inheritdoc/>
    public IReadOnlyList<SheetVersion> Versions(string id) => inner.Versions(id);

    /// <inheritdoc/>
    public string ReadVersion(string id, string name) => inner.ReadVersion(id, name);

    /// <inheritdoc/>
    public SaveResult RestoreVersion(string id, string name, string expectedFingerprint, Moment now) => inner.RestoreVersion(id, name, expectedFingerprint, now);

    /// <inheritdoc/>
    public string AddResource(string id, string name, ReadOnlySpan<byte> content) => inner.AddResource(id, name, content);

    /// <inheritdoc/>
    public IReadOnlyList<string> Folders() => inner.Folders();

    /// <inheritdoc/>
    public string CreateFolder(string folder) => inner.CreateFolder(folder);

    /// <inheritdoc/>
    public string MoveFolder(string folder, string destination, Moment now) => inner.MoveFolder(folder, destination, now);

    /// <inheritdoc/>
    public void TrashSheet(string id, Moment now) => inner.TrashSheet(id, now);

    /// <inheritdoc/>
    public void RestoreSheet(string id, Moment now) => inner.RestoreSheet(id, now);

    /// <inheritdoc/>
    public IReadOnlyList<TrashedSheet> TrashedSheets() => inner.TrashedSheets();

    /// <inheritdoc/>
    public int TrashFolder(string folder, Moment now) => inner.TrashFolder(folder, now);

    /// <inheritdoc/>
    public IReadOnlyList<TrashedAnnotation> TrashedAnnotations() => inner.TrashedAnnotations();

    /// <summary>Valide la révision attendue : avec les règles de rappel du coffre pour une fiche, aucune pour un passage.</summary>
    public ReviewOutcome ValidateReview(ItemKind kind, string id, Moment now) => kind == ItemKind.Sheet
        ? inner.ApplyReview(id, tracking => tracking.ValidateReview(now, null, inner.Settings.Reminders))
        : inner.ApplyAnnotationReview(id, tracking => tracking.ValidateReview(now, null, null));

    /// <inheritdoc/>
    public void Postpone(ItemKind kind, string id, Moment now, DateOnly newDueDate) =>
        inner.ApplyTo(kind, id, (tracking, _) => tracking.Postpone(now, newDueDate));

    /// <inheritdoc/>
    public void Restart(ItemKind kind, string id, Moment now) =>
        inner.ApplyTo(kind, id, (tracking, _) => tracking.Restart(now, inner.Settings.Sequence));

    /// <inheritdoc/>
    public void ArchiveSheet(string id, Moment now) =>
        inner.ApplyTo(ItemKind.Sheet, id, (tracking, rules) => tracking.ArchiveManually(now, rules));

    /// <inheritdoc/>
    public ReviewState ReviewStateOf(ItemKind kind, string id, DateOnly day) =>
        ReviewState.Of(kind == ItemKind.Sheet ? inner.Sheet(id).Sheet.Tracking : inner.Annotation(id).Tracking, day);

    /// <inheritdoc/>
    public string CreateAnnotation(string sheetId, Anchor anchor, string? comment, Moment now) =>
        inner.CreateAnnotation(sheetId, ToStored(anchor), comment, now);

    /// <inheritdoc/>
    public string CreateDefinition(string sheetId, Anchor anchor, Definition definition, Moment now) =>
        inner.CreateDefinition(sheetId, ToStored(anchor), ToStored(definition), now);

    /// <inheritdoc/>
    public void ModifyDefinition(string id, Definition definition, Moment now) => inner.ModifyDefinition(id, ToStored(definition), now);

    /// <inheritdoc/>
    public void ModifyComment(string id, string? comment, Moment now) => inner.ModifyComment(id, comment, now);

    /// <inheritdoc/>
    public void Reanchor(string id, Anchor anchor, AnchorState state, Moment now) => inner.Reanchor(id, ToStored(anchor), state, now);

    /// <inheritdoc/>
    public void ModifyPassage(string id, Anchor anchor, bool restart, Moment now) => inner.ModifyPassage(id, ToStored(anchor), restart, now);

    /// <inheritdoc/>
    public void TrashAnnotation(string id, Moment now) => inner.TrashAnnotation(id, now);

    /// <inheritdoc/>
    public void RestoreAnnotation(string id, Moment now) => inner.RestoreAnnotation(id, now);

    /// <inheritdoc/>
    public void HandleReminder(string sheetId, ReminderOutcome outcome, Moment now) => inner.HandleReminder(sheetId, outcome, now);

    /// <inheritdoc/>
    public void IncludeInReminders(string sheetId, bool include, Moment now) => inner.IncludeInReminders(sheetId, include, now);

    /// <inheritdoc/>
    public DayPreparation PrepareDay(Moment now) => inner.PrepareDay(now);

    /// <inheritdoc/>
    public void DefineExam(string folder, string name, DateOnly day) => inner.DefineExam(folder, name, day);

    /// <inheritdoc/>
    public void WithdrawExam(string folder) => inner.WithdrawExam(folder);

    /// <inheritdoc/>
    public int TightenBeforeExam(string folder, Moment now) => inner.TightenBeforeExam(folder, now);

    /// <inheritdoc/>
    public void ChangePreferences(VaultPreferences preferences) => inner.ChangePreferences(preferences);

    /// <inheritdoc/>
    public HealthDetails HealthDetails() => inner.HealthDetails();

    /// <inheritdoc/>
    public void Analyze(Moment now) => inner.Analyze(now);

    /// <inheritdoc/>
    public string RecreateTracking(string path, Moment now) => inner.RecreateTracking(VaultPath.Create(path), now);

    /// <inheritdoc/>
    public IReadOnlyList<string> KeepOriginal(string id, string original, Moment now) => inner.KeepOriginal(id, VaultPath.Create(original), now);

    /// <inheritdoc/>
    public string RestoreSheetFromBackup(string id, string backup, Moment now) => inner.RestoreSheetFromBackup(id, backup, now).Value;

    /// <inheritdoc/>
    public BackupFile Backup(string folder, DateTime timestamp) => inner.Backup(folder, timestamp);

    /// <inheritdoc/>
    public IReadOnlyList<string> Export(ExportKind kind, string folder, DateTime timestamp, TimeZoneInfo zone) => inner.Export(kind, folder, timestamp, zone);

    /// <inheritdoc/>
    public IReadOnlyList<SheetTemplate> Templates() => inner.Templates();

    /// <inheritdoc/>
    public string TemplateBody(string name, string title, string folder, DateOnly day) => inner.TemplateBody(name, title, folder, day);

    /// <inheritdoc/>
    public string SaveAsTemplate(string id, string name) => inner.SaveAsTemplate(id, name);

    /// <inheritdoc/>
    public QuoteList Quotes() => inner.Quotes();

    /// <inheritdoc/>
    public Quote AddQuote(QuoteContent content, Moment now) => inner.AddQuote(content, now);

    /// <inheritdoc/>
    public Quote ModifyQuote(string path, QuoteContent content, Moment now) => inner.ModifyQuote(path, content, now);

    /// <inheritdoc/>
    public string TrashQuote(string path) => inner.TrashQuote(path);

    /// <inheritdoc/>
    public string RestoreQuote(string path) => inner.RestoreQuote(path);

    /// <inheritdoc/>
    public Quote? KeepOnlineQuote(string page, IReadOnlyList<QuoteContent> found, Moment now) => inner.KeepOnlineQuote(page, found, now);

    /// <inheritdoc/>
    public Quote? OnlineQuoteOf(DateOnly day) => inner.OnlineQuoteOf(day);

    /// <inheritdoc/>
    public DateOnly? OnlineQuoteDay() => inner.OnlineQuoteDay();

    private static Anchor ToAnchor(StoredAnchor anchor) =>
        new(anchor.Exact, anchor.Before, anchor.After, anchor.Start, anchor.End, anchor.Section, anchor.SourceLine, anchor.SheetFingerprint);

    private static StoredAnchor ToStored(Anchor anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        return new StoredAnchor(
            anchor.Exact, anchor.Before, anchor.After, anchor.Start, anchor.End, anchor.Section, anchor.SourceLine, anchor.SheetFingerprint, UnknownProperties.None);
    }

    private static StoredDefinition ToStored(Definition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new StoredDefinition(definition.Term, definition.Explanation);
    }
}
