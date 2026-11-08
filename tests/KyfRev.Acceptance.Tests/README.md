# Scénarios d'acceptation

Les scénarios T2 à T6, T9, T10, T11 et T18 de la conception, aux dates qu'elle donne, et celui du coffre chiffré
(étape J8) : sans sa clé, l'application n'en montre que l'écran de déverrouillage ; son mot de passe, donné par le
motif de valeur du champ (rien n'est tapé au clavier), l'ouvre comme un autre.

`J8_nothing_of_an_encrypted_vault_stays_readable_on_the_disk` vaut preuve pour le coffre chiffré : des marqueurs uniques
dans le titre, le texte, un dessin, une annotation, une définition et la frappe ; la lecture, l'écriture, la recherche, le
verrouillage et la fermeture ; et, à chaque étape, tous les octets du dossier d'essai (`DiskScan`), profil du moteur web
et dossier temporaire compris. Il tape au clavier : il tourne dans le bac à sable et en intégration continue.

Chaque scénario lance la vraie application dans son propre processus et la pilote par UI Automation
(FlaUI). Le programme lancé, `tools/acceptance-host`, est l'application telle quelle (ses services, sa
fenêtre, ses coffres), avec deux différences : une date simulée et un dossier jetable, en mode portable.
L'exécutable livré n'a pas ce pouvoir, et rien n'y touche au vrai coffre.

Ce qui a été enregistré se lit dans les fichiers de suivi du coffre, tels qu'ils sont écrits.

```powershell
dotnet test --project tests/KyfRev.Acceptance.Tests -c Release
```

Des fenêtres s'ouvrent et se referment pendant environ une minute, sans prendre le premier plan. Les
boutons et les lignes sont invoqués sans souris ni clavier : le poste reste utilisable.

Ne les lancez pas en même temps que `KyfRev.Presentation.Wpf.Tests`, dont les contrôles d'accessibilité
parcourent les fenêtres du bureau. L'intégration continue passe un projet de tests après l'autre.

## Ce qui passe par l'application, et ce qui ne le peut pas encore

Tout geste de révision passe par l'application : ouvrir une ligne, faire défiler, valider, reporter,
répondre à un rappel, marquer un passage, consulter les archives. Ce que l'interface ne propose pas encore passe par les cas
d'usage, sur le même coffre et à la même date :

| Scénario | Ce qui passe par les cas d'usage | Pourquoi |
|---|---|---|
| Tous, sauf T11 | Créer la fiche de départ | T11 le fait déjà dans l'application |
| T2, T5, T6, T11 | Écrire le texte de la fiche et surligner un passage | Arrive avec J7 |
| T3 | Les jours du repos entre le 4 décembre et le 1er février (la veille et le jour du premier rappel passent par l'application) | Évite une soixantaine de lancements |
| T3, T18 | Archiver à la main les fiches de départ | L'application sait le faire, mais ce n'est pas ce que ces scénarios vérifient |
| T3, T18 | Régler les rappels | La vue « Paramètres » est encore vide |
| T4, T5, T6 | Vivre le premier cycle de la fiche | T2 le fait déjà dans l'application |
| T10 | Changer la séquence des étapes | La vue « Paramètres » est encore vide |

**T5 reste à compléter avec J7.** La conception demande de surligner un passage pendant le rappel. Tant
que la lecture native n'existe pas, le scénario remet à J0 un passage déjà surligné, ce que l'application
sait faire aujourd'hui.
