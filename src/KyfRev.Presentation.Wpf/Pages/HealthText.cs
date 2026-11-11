using System.Globalization;
using System.Resources;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Textes de la vue « Santé du coffre », ceux de la 0.x, lus dans les ressources (<c>Resources/HealthText.resx</c>).</summary>
public static class HealthText
{
    private static readonly ResourceManager Resources = new("KyfRev.Presentation.Wpf.Resources.HealthText", typeof(HealthText).Assembly);

    /// <summary>« {0} : {1} ».</summary>
    public static string ActionOnOf(object action, object title) => Format("ActionOn", action, title);

    /// <summary>« Aucun problème : tout est en ordre. ».</summary>
    public static string AllGood => Text(nameof(AllGood));

    /// <summary>« Analyser à nouveau ».</summary>
    public static string Analyze => Text(nameof(Analyze));

    /// <summary>« Sauvegarde à utiliser ».</summary>
    public static string BackupToUse => Text(nameof(BackupToUse));

    /// <summary>« Ces liens ou images mènent à un fichier absent du coffre. ».</summary>
    public static string BrokenLinksHelp => Text(nameof(BrokenLinksHelp));

    /// <summary>« Liens cassés ({0}) ».</summary>
    public static string BrokenLinksTitleOf(object count) => Format("BrokenLinksTitle", count);

    /// <summary>« Les fichiers des fiches se lisent et portent leur identifiant, chaque… ».</summary>
    public static string ChecksText => Text(nameof(ChecksText));

    /// <summary>« Ce qui a été vérifié ».</summary>
    public static string ChecksTitle => Text(nameof(ChecksTitle));

    /// <summary>« Diagnostic copié : collez-le dans votre message. ».</summary>
    public static string DiagnosticCopied => Text(nameof(DiagnosticCopied));

    /// <summary>« Copier le diagnostic ».</summary>
    public static string DiagnosticCopy => Text(nameof(DiagnosticCopy));

    /// <summary>« Un problème avec kyfRev ? Copiez le diagnostic et joignez-le à votr… ».</summary>
    public static string DiagnosticHelp => Text(nameof(DiagnosticHelp));

    /// <summary>« La copie a échoué : sélectionnez le texte ci-dessous (Ctrl+A, Ctrl+… ».</summary>
    public static string DiagnosticNotCopied => Text(nameof(DiagnosticNotCopied));

    /// <summary>« Texte du diagnostic ».</summary>
    public static string DiagnosticText => Text(nameof(DiagnosticText));

    /// <summary>« Diagnostic ».</summary>
    public static string DiagnosticTitle => Text(nameof(DiagnosticTitle));

    /// <summary>« Plusieurs fichiers portent le même identifiant (une fiche copiée ho… ».</summary>
    public static string DuplicatesHelp => Text(nameof(DuplicatesHelp));

    /// <summary>« Copies de conflit ({0}) ».</summary>
    public static string ConflictsTitleOf(object count) => Format("ConflictsTitle", count);

    /// <summary>« Votre outil de synchronisation a gardé deux versions… ».</summary>
    public static string ConflictsHelp => Text(nameof(ConflictsHelp));

    /// <summary>« Fichiers en double ({0}) ».</summary>
    public static string DuplicatesTitleOf(object count) => Format("DuplicatesTitle", count);

    /// <summary>« Masquer ».</summary>
    public static string Hide => Text(nameof(Hide));

    /// <summary>« Pour information ».</summary>
    public static string Information => Text(nameof(Information));

    /// <summary>« Garder comme original ».</summary>
    public static string KeepOriginal => Text(nameof(KeepOriginal));

    /// <summary>« « {0} » garde le suivi ; les copies sont devenues de nouvelles fich… ».</summary>
    public static string KeptOf(object file) => Format("Kept", file);

    /// <summary>« → {0} ».</summary>
    public static string LinkTargetOf(object target) => Format("LinkTarget", target);

    /// <summary>« liens à vérifier après un renommage ».</summary>
    public static string LinksToCheck => Text(nameof(LinksToCheck));

    /// <summary>« Analyse du coffre… ».</summary>
    public static string Loading => Text(nameof(Loading));

    /// <summary>« Leur fichier a disparu du coffre ; leur suivi est conservé. Restaur… ».</summary>
    public static string MissingHelp => Text(nameof(MissingHelp));

    /// <summary>« Fiches manquantes ({0}) ».</summary>
    public static string MissingTitleOf(object count) => Format("MissingTitle", count);

    /// <summary>« modifiée hors de kyfRev depuis la dernière ouverture ».</summary>
    public static string ModifiedOutside => Text(nameof(ModifiedOutside));

    /// <summary>« déplacée hors de kyfRev, rattachée automatiquement : {0} ».</summary>
    public static string MovedOf(object to) => Format("Moved", to);

    /// <summary>« Aucune sauvegarde disponible. ».</summary>
    public static string NoBackup => Text(nameof(NoBackup));

    /// <summary>« introuvable ».</summary>
    public static string NotFound => Text(nameof(NotFound));

    /// <summary>« Ouvrir la fiche ».</summary>
    public static string OpenSheet => Text(nameof(OpenSheet));

    /// <summary>« Leur fiche est introuvable ; ils restent révisables à partir du tex… ».</summary>
    public static string OrphansHelp => Text(nameof(OrphansHelp));

    /// <summary>« Passages sans fiche ({0}) ».</summary>
    public static string OrphansTitleOf(object count) => Format("OrphansTitle", count);

    /// <summary>« {0} · {1} ».</summary>
    public static string PassageStateOf(object sheet, object state) => Format("PassageState", sheet, state);

    /// <summary>« Leur fiche a changé : leur position est incertaine ou introuvable. … ».</summary>
    public static string PassagesToCheckHelp => Text(nameof(PassagesToCheckHelp));

    /// <summary>« Passages à vérifier ({0}) ».</summary>
    public static string PassagesToCheckTitleOf(object count) => Format("PassagesToCheckTitle", count);

    /// <summary>« {0} points attendent une décision. Rien n'est réparé ni effacé sans… ».</summary>
    public static string PointManyOf(object count) => Format("PointMany", count);

    /// <summary>« {0} point attend une décision. Rien n'est réparé ni effacé sans vous. ».</summary>
    public static string PointOneOf(object count) => Format("PointOne", count);

    /// <summary>« position à vérifier ».</summary>
    public static string PositionToCheck => Text(nameof(PositionToCheck));

    /// <summary>« « {0} » ».</summary>
    public static string QuotedOf(object text) => Format("Quoted", text);

    /// <summary>« Recréer son suivi (J0) ».</summary>
    public static string RecreateTracking => Text(nameof(RecreateTracking));

    /// <summary>« Restaurer ».</summary>
    public static string Restore => Text(nameof(Restore));

    /// <summary>« « {0} » est restaurée depuis la sauvegarde. ».</summary>
    public static string RestoredOf(object title) => Format("Restored", title);

    /// <summary>« Voir dans la fiche ».</summary>
    public static string SeeInSheet => Text(nameof(SeeInSheet));

    /// <summary>« Afficher ».</summary>
    public static string Show => Text(nameof(Show));

    /// <summary>« Santé du coffre ».</summary>
    public static string Title => Text(nameof(Title));

    /// <summary>« kyfRev ne les modifie pas. Corrigez-les dans un éditeur de texte, o… ».</summary>
    public static string ToExamineHelp => Text(nameof(ToExamineHelp));

    /// <summary>« Fichiers à examiner ({0}) ».</summary>
    public static string ToExamineTitleOf(object count) => Format("ToExamineTitle", count);

    /// <summary>« Suivre cette fiche (J0) ».</summary>
    public static string Track => Text(nameof(Track));

    /// <summary>« « {0} » est suivie : J1 demain. ».</summary>
    public static string TrackedOf(object file) => Format("Tracked", file);

    /// <summary>« {0} · identifiant présent, suivi perdu ».</summary>
    public static string TrackingLostOf(object path) => Format("TrackingLost", path);

    /// <summary>« Le suivi de « {0} » est recréé : J1 demain. ».</summary>
    public static string TrackingRecreatedOf(object title) => Format("TrackingRecreated", title);

    /// <summary>« Des fichiers Markdown du dossier « fiches » ne sont pas encore révi… ».</summary>
    public static string UntrackedHelp => Text(nameof(UntrackedHelp));

    /// <summary>« Fichiers non suivis ({0}) ».</summary>
    public static string UntrackedTitleOf(object count) => Format("UntrackedTitle", count);

    private static string Format(string name, params object[] values) => string.Format(CultureInfo.CurrentCulture, Text(name), values);

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » introuvable.");
}
