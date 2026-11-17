namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Les textes de l'accueil : ses trois entrées et la méthode des J en trois temps.</summary>
public static class WelcomeText
{
    /// <summary>La première entrée.</summary>
    public const string Create = "Créer mon coffre";

    /// <summary>Sous elle, quand le coffre a un emplacement prévu : le chemin suit.</summary>
    public const string CreateHere = "Un dossier ordinaire, ici :";

    /// <summary>Sous elle, sans emplacement prévu.</summary>
    public const string CreateAnywhere = "Un dossier ordinaire, dans un dossier vide de votre choix.";

    /// <summary>Ce que fait la deuxième entrée.</summary>
    public const string OpenHelp = "Un dossier de fiches que vous avez déjà, ou celui d'un autre ordinateur.";

    /// <summary>La troisième entrée.</summary>
    public const string Sample = "Essayer avec un coffre d'exemple";

    /// <summary>Ce qu'elle fait.</summary>
    public const string SampleHelp = "Dix fiches sur six matières, déjà en cours de révision, pour voir kyfRev en marche. Il se supprime quand vous voulez.";

    /// <summary>Le titre de la fenêtre qui demande où créer le coffre d'exemple, sans emplacement prévu.</summary>
    public const string ChooseSampleFolder = "Choisissez un dossier vide pour le coffre d'exemple";

    /// <summary>Le titre de la méthode.</summary>
    public const string MethodTitle = "La méthode des J, en trois temps";

    /// <summary>Premier temps.</summary>
    public const string WriteTitle = "Écrivez la fiche";

    /// <summary>Ce qu'il est.</summary>
    public const string WriteText = "Un cours, un chapitre, une leçon : en texte mis en forme, avec images, tableaux et formules.";

    /// <summary>Deuxième temps.</summary>
    public const string ReviewTitle = "Révisez-la aux bons jours";

    /// <summary>Ce qu'il est.</summary>
    public const string ReviewText = "Chaque étape tombe n jours après la révision faite. Oubliée ? Elle recule d'une étape.";

    /// <summary>Troisième temps.</summary>
    public const string ArchiveTitle = "Elle s'archive, puis revient";

    /// <summary>Ce qu'il est.</summary>
    public const string ArchiveText = "Le cycle fini, la fiche est archivée, puis rappelée au hasard, de plus en plus rarement.";

    /// <summary>Le lien vers le guide, lu dans kyfRev.</summary>
    public const string ReadGuide = "Lire le guide";

    /// <summary>Les étapes de la méthode, telles qu'elles s'écrivent.</summary>
    public static IReadOnlyList<string> Steps { get; } = ["J0", "J1", "J3", "J7", "J14", "J30"];
}
