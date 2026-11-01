# Scénarios d'acceptation

Les scénarios T2 à T6, T9, T10, T11 et T18 de la conception, aux dates qu'elle donne.

Chaque scénario lance la vraie application dans son propre processus et la pilote par UI Automation
(FlaUI). Le programme lancé, `tools/acceptance-host`, est l'application telle quelle (ses services, sa
fenêtre, ses coffres), avec deux différences : une date simulée et un dossier jetable, en mode portable.
L'exécutable livré n'a pas ce pouvoir, et rien n'y touche au vrai coffre.

Ce qui a été enregistré se lit dans les fichiers de suivi du coffre, tels qu'ils sont écrits.

```powershell
dotnet test --project tests/KyfRev.Acceptance.Tests -c Release
```

Des fenêtres s'ouvrent et se referment pendant environ une minute. Les boutons et les lignes sont
invoqués sans souris ni clavier : le poste reste utilisable.

## Ce qui passe par l'application, et ce qui ne le peut pas encore

Tout geste de révision passe par l'application : ouvrir une ligne, faire défiler, valider, reporter,
répondre à un rappel, marquer un passage. Ce que l'interface ne propose pas encore passe par les cas
d'usage, sur le même coffre et à la même date :

| Scénario | Ce qui passe par les cas d'usage | Disponible dans l'interface |
|---|---|---|
| Tous | Créer une fiche | J8 |
| T2, T11 | Surligner un passage | J7 |
| T2 | Lire la vue « Archives » | J8 |
| T3 | Les jours du repos entre le 4 décembre et le 1er février (la veille et le jour du premier rappel passent par l'application) | Sans objet |
| T3, T18 | Archiver à la main les fiches de départ, et régler les rappels | J8 |
| T4, T5, T6 | Vivre le premier cycle de la fiche (T2 le fait dans l'application) | Sans objet |
| T10 | Changer la séquence des étapes | J8 |

**T5 reste à compléter avec J7.** La conception demande de surligner un passage pendant le rappel. Tant
que la lecture native n'existe pas, le scénario remet à J0 un passage déjà surligné, ce que l'application
sait faire aujourd'hui.
