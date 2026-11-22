using System.Windows.Controls;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Le mode dans lequel une fiche est ouverte, s'il y en a un : son action rapide est marquée dans le menu.</summary>
public enum SheetMode
{
    /// <summary>La fiche n'est pas ouverte : une ligne, un aperçu, un résultat.</summary>
    None,

    /// <summary>Ouverte en lecture.</summary>
    Read,

    /// <summary>Ouverte pour la réviser.</summary>
    Revise,

    /// <summary>Ouverte pour l'écrire.</summary>
    Write,
}

/// <summary>
/// La fiche visée par un clic droit, où qu'elle soit : une ligne du Carnet, d'Aujourd'hui ou des Archives, un aperçu, la
/// fiche ouverte, un résultat de recherche.
/// </summary>
/// <param name="Id">Son identifiant.</param>
/// <param name="Title">Son titre.</param>
public sealed record SheetTarget(string Id, string Title)
{
    /// <summary>Elle est archivée.</summary>
    public bool IsArchived { get; init; }

    /// <summary>C'est une simple note, hors des révisions.</summary>
    public bool IsNote { get; init; }

    /// <summary>Archivée, elle est tenue hors des rappels ; rien si la vue ne le sait pas (le réglage n'est alors pas proposé).</summary>
    public bool? IsExcludedFromReminders { get; init; }

    /// <summary>Son dossier, pour les extensions.</summary>
    public string Folder { get; init; } = string.Empty;

    /// <summary>Elle est en révision : ni archivée, ni simple note.</summary>
    public bool IsInReview => !IsArchived && !IsNote;
}

/// <summary>Ce que le menu d'une fiche sait faire : ce que la vue fait elle-même, et le reste par les actions communes.</summary>
public interface ISheetMenuHost
{
    /// <summary>Les actions qui touchent au coffre (renommer, déplacer, archiver…) sont possibles ici.</summary>
    bool CanManage { get; }

    /// <summary>Partager et enregistrer comme modèle sont possibles ici.</summary>
    bool CanShare { get; }

    /// <summary>Ouvre la fiche en lecture.</summary>
    void Read(SheetTarget sheet);

    /// <summary>Ouvre la fiche pour la réviser.</summary>
    void Revise(SheetTarget sheet);

    /// <summary>Ouvre la fiche pour l'écrire.</summary>
    void Write(SheetTarget sheet);

    /// <summary>Reporte sa révision à demain.</summary>
    void PostponeTomorrow(SheetTarget sheet);

    /// <summary>Demande de l'archiver.</summary>
    void Archive(SheetTarget sheet);

    /// <summary>Demande de la remettre en révision.</summary>
    void Restart(SheetTarget sheet);

    /// <summary>Archivée : l'exclut des rappels, ou l'y remet.</summary>
    void ToggleReminders(SheetTarget sheet);

    /// <summary>Demande un nouveau titre.</summary>
    void Rename(SheetTarget sheet);

    /// <summary>Demande ses étiquettes.</summary>
    void Tags(SheetTarget sheet);

    /// <summary>Demande où la déplacer.</summary>
    void Move(SheetTarget sheet);

    /// <summary>Ouvre sa fenêtre « Imprimer ».</summary>
    void Print(SheetTarget sheet);

    /// <summary>Ouvre « Partager ».</summary>
    void Share(SheetTarget sheet);

    /// <summary>Ouvre « Enregistrer comme modèle ».</summary>
    void SaveAsTemplate(SheetTarget sheet);

    /// <summary>En fait une simple note, ou une fiche de révision.</summary>
    void ToggleNote(SheetTarget sheet);

    /// <summary>Demande de la supprimer.</summary>
    void Trash(SheetTarget sheet);

    /// <summary>Les entrées que les extensions ajoutent au clic droit d'une fiche.</summary>
    IReadOnlyList<MenuContribution> Extensions(SheetTarget sheet);
}

/// <summary>
/// Le menu d'une fiche (rangée 23A des maquettes), le même partout : Lire, Réviser, Écrire en tête, puis toujours dans cet
/// ordre la révision, le rangement, la sortie, le passage en note, la suppression et les extensions. Une entrée sans objet
/// est absente ; Réviser une fiche archivée est grisé.
/// </summary>
internal static class SheetMenu
{
    /// <summary>Les entrées du menu de cette fiche.</summary>
    public static List<Control> Items(SheetTarget sheet, ISheetMenuHost host, SheetMode current = SheetMode.None)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(host);
        List<Control> items =
        [
            ActionMenus.Quick(EditingText.Read, Icons.Read, () => host.Read(sheet), current: current == SheetMode.Read),
            ActionMenus.Quick(ReadingText.Revise, Icons.Review, () => host.Revise(sheet), isEnabled: !sheet.IsArchived, current: current == SheetMode.Revise),
            ActionMenus.Quick(EditingText.Write, Icons.Edit, () => host.Write(sheet), current: current == SheetMode.Write),
        ];
        if (host.CanManage)
        {
            Block(items, Review(sheet, host));
            Block(items,
            [
                ActionMenus.Item(SheetActionsText.Rename, () => host.Rename(sheet)),
                ActionMenus.Item(SheetActionsText.Tags, () => host.Tags(sheet)),
                ActionMenus.Item(SheetActionsText.Move, () => host.Move(sheet)),
            ]);
        }
        List<Control> output = [ActionMenus.Item(PrintText.PrintAction, () => host.Print(sheet), "Ctrl+P")];
        if (host.CanShare)
        {
            output.Add(ActionMenus.Item(SharingText.Share, () => host.Share(sheet)));
            output.Add(ActionMenus.Item(SharingText.SaveAsTemplate, () => host.SaveAsTemplate(sheet)));
        }
        Block(items, output);
        if (host.CanManage)
        {
            if (sheet.IsInReview)
            {
                Block(items, [ActionMenus.Item(SheetsText.MakeNote, () => host.ToggleNote(sheet))]);
            }
            Block(items, [ActionMenus.Item(SheetActionsText.Delete, () => host.Trash(sheet))]);
        }
        Block(items, [.. host.Extensions(sheet).Select(entry => (Control)ActionMenus.Item(entry.Label, entry.Run))]);
        return items;
    }

    /// <summary>
    /// Ce qui touche à la révision : reporter et archiver une fiche en révision ; la remettre en révision et régler ses rappels
    /// si elle est archivée ; faire d'une note une fiche de révision.
    /// </summary>
    private static List<Control> Review(SheetTarget sheet, ISheetMenuHost host)
    {
        if (sheet.IsArchived)
        {
            List<Control> archived = [ActionMenus.Item(SheetActionsText.Restart, () => host.Restart(sheet))];
            if (sheet.IsExcludedFromReminders is { } excluded)
            {
                archived.Add(ActionMenus.Item(excluded ? ArchivesText.Include : ArchivesText.Exclude, () => host.ToggleReminders(sheet)));
            }
            return archived;
        }
        if (sheet.IsNote)
        {
            return [ActionMenus.Item(SheetsText.MakeReview, () => host.ToggleNote(sheet))];
        }
        return
        [
            ActionMenus.Item(ReviewText.PostponeTomorrow, () => host.PostponeTomorrow(sheet)),
            ActionMenus.Item(SheetActionsText.Archive, () => host.Archive(sheet)),
        ];
    }

    /// <summary>Un bloc du menu, après un filet ; rien s'il est vide.</summary>
    private static void Block(List<Control> items, List<Control> block)
    {
        if (block.Count == 0)
        {
            return;
        }
        items.Add(new Separator());
        items.AddRange(block);
    }
}

/// <summary>
/// Le menu d'une fiche dans une vue qui n'a pas ses propres fenêtres : tout passe par les actions communes, sauf l'ouverture,
/// que la vue peut faire à sa façon (un rappel s'ouvre pour être consolidé). Sans actions communes, seules l'ouverture et
/// l'impression restent.
/// </summary>
internal sealed class SharedSheetMenu(SheetActionsViewModel? actions, IReviewOpener opener, Action<SheetTarget>? read = null, Action<SheetTarget>? revise = null) : ISheetMenuHost
{
    public bool CanManage => actions is not null;

    public bool CanShare => actions?.CanShare ?? false;

    public void Read(SheetTarget sheet)
    {
        if (read is not null)
        {
            read(sheet);
        }
        else
        {
            opener.OpenSheet(sheet.Id);
        }
    }

    public void Revise(SheetTarget sheet)
    {
        if (revise is not null)
        {
            revise(sheet);
        }
        else
        {
            opener.ReviseSheet(sheet.Id);
        }
    }

    public void Write(SheetTarget sheet) => opener.WriteSheet(sheet.Id);

    public void Print(SheetTarget sheet) => opener.PrintSheet(sheet.Id);

    public void PostponeTomorrow(SheetTarget sheet) => actions?.PostponeTomorrow(sheet);

    public void Archive(SheetTarget sheet) => actions?.Archive(sheet);

    public void Restart(SheetTarget sheet) => actions?.Restart(sheet);

    public void ToggleReminders(SheetTarget sheet) => actions?.ToggleReminders(sheet);

    public void Rename(SheetTarget sheet) => actions?.Rename(sheet);

    public void Tags(SheetTarget sheet) => actions?.Tags(sheet);

    public void Move(SheetTarget sheet) => actions?.Move(sheet);

    public void Share(SheetTarget sheet) => actions?.Share(sheet);

    public void SaveAsTemplate(SheetTarget sheet) => actions?.SaveAsTemplate(sheet);

    public void ToggleNote(SheetTarget sheet) => actions?.ToggleNote(sheet);

    public void Trash(SheetTarget sheet) => actions?.Trash(sheet);

    public IReadOnlyList<MenuContribution> Extensions(SheetTarget sheet) => actions?.Extensions(sheet) ?? [];
}
