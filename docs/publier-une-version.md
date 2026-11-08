# Publier une version

La publication se fait **en local**, pour que l'étiquette et la version GitHub soient au nom de KyllianFF. La CI construit les
fichiers ; le script les reprend sans rien reconstruire.

## 1. Préparer

Sur une branche `release/x.y.z` :

1. `<Version>` dans `Directory.Build.props` ;
2. dans `CHANGELOG.md`, renommer `[Non publié]` en `## [x.y.z] - AAAA-MM-JJ`, ouvrir une nouvelle section `[Non publié]`, et
   compléter les liens de comparaison en bas du fichier ;
3. demande de fusion, puis fusion dans main. La date du CHANGELOG entre dans l'installateur et dans l'exécutable : la fusion doit
   venir après elle.

## 2. Publier

Attendre que la CI de main soit verte, puis, sur main à jour :

```powershell
pwsh tools/publier-version.ps1
```

Le script vérifie que l'étiquette, la version et le CHANGELOG concordent, que le dossier est propre, que le commit est le dernier
de main et que sa CI est verte, puis il télécharge l'installateur et la version portable. Il dit ce qu'il ferait, sans rien
modifier. Si tout convient :

```powershell
pwsh tools/publier-version.ps1 -Publier
```

Il pose l'étiquette annotée `vx.y.z`, la pousse et crée la version GitHub, marquée préversion si le numéro a un suffixe. Une
étiquette qui ne concorde pas fait échouer le workflow « Étiquette ».

## 3. Annoncer

La recherche de mise à jour lit une annonce signée. Avec votre clé de publication, jamais rangée dans le dépôt :

```powershell
dotnet run tools/catalogue/catalogue.cs -- version C:\chemin\hors\du\depot\kyfrev-1.pem x.y.z
```

Puis proposer `mise-a-jour/` dans une demande de fusion.
