using KyfRev.Application.Model;

namespace KyfRev.Application.UseCases;

/// <summary>Dossiers de fiches : ils se créent, se renomment et se déplacent, liens compris.</summary>
public sealed class FolderUseCases(VaultHost host, TimeProvider time)
{
    /// <summary>Dossiers du coffre, relatifs à <c>fiches/</c>.</summary>
    [ZeroXCommand("dossiers")]
    public Outcome<IReadOnlyList<string>> Folders() => host.Use(vault => vault.Folders());

    /// <summary>Crée un dossier de fiches ; rend son chemin.</summary>
    [ZeroXCommand("creer_dossier")]
    public Outcome<string> Create(string folder) => host.Use(vault => vault.CreateFolder(folder));

    /// <summary>Renomme ou déplace un dossier : <paramref name="destination"/> est son nouveau chemin, rendu tel qu'enregistré.</summary>
    [ZeroXCommand("deplacer_dossier")]
    public Outcome<string> Move(string folder, string destination)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.MoveFolder(folder, destination, now));
    }
}

/// <summary>
/// Corbeille : « supprimer » une fiche, un dossier ou une annotation les y range, restaurables. Seule la
/// demande expresse de la personne les efface pour de bon.
/// </summary>
public sealed class TrashUseCases(VaultHost host, TimeProvider time)
{
    /// <summary>« Supprimer » une fiche : elle va à la corbeille, avec ses ressources, son suivi et ses annotations.</summary>
    [ZeroXCommand("mettre_fiche_a_la_corbeille")]
    public Outcome<Done> TrashSheet(string id)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.TrashSheet(id, now));
    }

    /// <summary>Remet une fiche de la corbeille à sa place (ou à côté, si elle est prise).</summary>
    [ZeroXCommand("restaurer_fiche_de_la_corbeille")]
    public Outcome<Done> RestoreSheet(string id)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.RestoreSheet(id, now));
    }

    /// <summary>Fiches à la corbeille.</summary>
    [ZeroXCommand("fiches_a_la_corbeille")]
    public Outcome<IReadOnlyList<TrashedSheet>> TrashedSheets() => host.Use(vault => vault.TrashedSheets());

    /// <summary>« Supprimer » un dossier : ses fiches vont à la corbeille ; rend leur nombre.</summary>
    [ZeroXCommand("mettre_dossier_a_la_corbeille")]
    public Outcome<int> TrashFolder(string folder)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.TrashFolder(folder, now));
    }

    /// <summary>Annotations à la corbeille (« Éléments supprimés »), les plus récentes d'abord.</summary>
    [ZeroXCommand("elements_a_la_corbeille")]
    public Outcome<IReadOnlyList<TrashedAnnotation>> TrashedAnnotations() => host.Use(vault => vault.TrashedAnnotations());

    /// <summary>Efface pour de bon une fiche déjà à la corbeille ; cela ne s'annule pas.</summary>
    public Outcome<Done> PurgeSheet(string id) => host.Use(vault => vault.PurgeSheet(id));

    /// <summary>Efface pour de bon un dossier et tout ce qu'il contient ; rend le nombre de fiches effacées.</summary>
    public Outcome<int> PurgeFolder(string folder)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.PurgeFolder(folder, now));
    }
}

/// <summary>
/// Examens par dossier, et échéances déplacées en nombre : ramenées avant un examen, étalées après une absence, ou
/// sorties d'une pause annoncée.
/// </summary>
public sealed class ExamUseCases(VaultHost host, TimeProvider time)
{
    /// <summary>Fixe ou remplace l'examen d'un dossier (vide : tout le coffre).</summary>
    [ZeroXCommand("definir_examen")]
    public Outcome<Done> Define(string folder, string name, DateOnly day) => host.Use(vault => vault.DefineExam(folder, name, day));

    /// <summary>Retire l'examen d'un dossier ; les calendriers ne changent pas.</summary>
    [ZeroXCommand("retirer_examen")]
    public Outcome<Done> Withdraw(string folder) => host.Use(vault => vault.WithdrawExam(folder));

    /// <summary>Ramène avant l'examen du dossier les échéances qui tombent après ; rend leur nombre.</summary>
    [ZeroXCommand("resserrer_avant_examen")]
    public Outcome<int> Tighten(string folder)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.TightenBeforeExam(folder, now));
    }

    /// <summary>
    /// Ce que donnerait un étalement, sans rien écrire : combien d'échéances restent aujourd'hui, combien partent, et
    /// jusqu'à quel jour. Sans jours de pause, c'est le retard qui s'étale.
    /// </summary>
    public Outcome<SpreadPlan> PlanSpread(SpreadRequest request)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.SpreadDueDates(request, now, apply: false));
    }

    /// <summary>Étale pour de bon : chaque échéance déplacée est un report ordinaire, inscrit dans l'historique.</summary>
    public Outcome<SpreadPlan> Spread(SpreadRequest request)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.SpreadDueDates(request, now, apply: true));
    }
}

/// <summary>Réglages du coffre (écran Paramètres).</summary>
public sealed class SettingsUseCases(VaultHost host)
{
    /// <summary>Réglages du coffre ouvert.</summary>
    [ZeroXCommand("reglages")]
    public Outcome<VaultPreferences> Get() => host.Use(vault => vault.Preferences);

    /// <summary>Enregistre les réglages, après vérification.</summary>
    [ZeroXCommand("modifier_reglages")]
    public Outcome<Done> Change(VaultPreferences preferences) => host.Use(vault => vault.ChangePreferences(preferences));

    /// <summary>Modifie les réglages à partir de ceux du moment : ce que l'appelant ne touche pas ne change pas. Rend ce qui est enregistré.</summary>
    public Outcome<VaultPreferences> Update(Func<VaultPreferences, VaultPreferences> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return host.Use(vault =>
        {
            vault.ChangePreferences(change(vault.Preferences));
            return vault.Preferences;
        });
    }
}

/// <summary>Modèles de fiche.</summary>
public sealed class TemplateUseCases(VaultHost host)
{
    /// <summary>Modèles du coffre, puis les intégrés.</summary>
    [ZeroXCommand("modeles")]
    public Outcome<IReadOnlyList<SheetTemplate>> Templates() => host.Use(vault => vault.Templates());

    /// <summary>Enregistre le corps de la fiche comme modèle du coffre ; rend le nom retenu.</summary>
    [ZeroXCommand("enregistrer_modele")]
    public Outcome<string> SaveAsTemplate(string id, string name) => host.Use(vault => vault.SaveAsTemplate(id, name));
}
