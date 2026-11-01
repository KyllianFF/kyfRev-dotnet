# Coffre d'essai

Crée un coffre rempli de données inventées, pour essayer l'application sans toucher à un vrai coffre.

```powershell
dotnet run tools/coffre-essai/coffre-essai.cs -- <dossier absent ou vide>
```

Le dossier doit être absent ou vide : l'outil refuse tout autre emplacement, donc tout coffre existant,
et n'écrit rien dans le profil de l'utilisateur. Une sauvegarde du coffre est rangée à côté de lui, dans
`Sauvegardes`, comme le fait l'application.

## Ce qu'il contient

Le coffre vit 150 jours en accéléré, jusqu'à aujourd'hui, avec les vrais cas d'usage : ses dates
tombent donc juste quel que soit le jour où il est créé.

- **26 fiches** en huit dossiers (histoire, biologie, réseaux, droit, mathématiques, anglais, philosophie
  et hors dossier), avec des titres, des tableaux, des listes, des citations, du code, des formules, un
  diagramme Mermaid, un quiz, des liens entre fiches, des étiquettes et des images ;
- **32 passages surlignés et définitions** ;
- **3 schémas réseau**, cités par les fiches de réseaux, au format 2 de `schemas/` ;
- **6 citations** du domaine public, 2 examens à venir, 1 modèle de fiche, des versions et 1 fiche à la
  corbeille ;
- des révisions **en retard**, **du jour** et **à venir**, un report, un cycle recommencé, des fiches
  **archivées**, 2 **rappels en attente**, et les trois réponses possibles à un rappel dans l'historique.

## Limites

Les positions des passages sont calculées sur une projection simple du texte : ce qui les retrouve est
leur texte exact et son contexte, comme à toute réouverture d'une fiche. Elles pourront donc être
recalées lorsque la lecture native (J7) ouvrira ces fiches.

Tant que J7 n'est pas livré, les fiches s'affichent en texte brut, et aucune page n'affiche encore les
schémas ni les citations (J9) : ces données sont déjà dans le coffre, prêtes pour ces jalons.
