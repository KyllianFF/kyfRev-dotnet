# Le catalogue signé des extensions

Le catalogue propose les extensions publiées avec kyfRev (le dossier `extensions/` du dépôt) : dans Paramètres › Modules
et extensions, « Voir le catalogue » les liste, et chacune s'installe ou se met à jour après la même confirmation qu'une
extension installée depuis un dossier.

## Ce que l'application vérifie

- Le catalogue n'est lu **que quand on le demande**, et par l'application seule : les extensions n'ont jamais le réseau.
- Il est lu dans le dépôt public : `catalogue/catalogue.json` et sa signature `catalogue/catalogue.sig`.
- Il n'est pris que si sa **signature** est bonne (ECDSA P-256, SHA-256), par une **clé publique que l'application
  connaît** (`src/KyfRev.Extensions.Host/cles-catalogue.json`, livré avec elle).
- Son **numéro** ne recule jamais : un catalogue plus ancien que le dernier vu sur ce poste est refusé. On ne peut donc
  pas resservir à l'application une ancienne version signée, avec une extension qui aurait eu un défaut.
- Chaque fichier d'une extension est téléchargé puis vérifié par **sa taille et son empreinte SHA-256**, toutes deux
  écrites dans le catalogue signé. Un seul fichier différent, et rien n'est installé ni gardé.
- L'installation montre ensuite ce que l'extension ajoute et ce qu'elle demande, comme pour un dossier.

## La clé de publication

La clé privée ne se range **jamais dans le dépôt**, ni dans un secret GitHub : elle reste sur le poste de la personne
qui publie, dans un fichier chiffré par une phrase de passe. La signature se fait sur ce poste ; la CI ne fait que
vérifier, avec la clé publique.

Une fois, à la racine du dépôt :

```bash
dotnet run tools/catalogue/catalogue.cs -- cle C:\chemin\hors\du\depot\kyfrev-1.pem kyfrev-1
```

L'outil demande la phrase de passe deux fois, écrit le fichier de la clé privée et affiche la clé publique, à ajouter
à `src/KyfRev.Extensions.Host/cles-catalogue.json` :

```json
[
  { "id": "kyfrev-1", "publique": "-----BEGIN PUBLIC KEY-----\n…\n-----END PUBLIC KEY-----" }
]
```

Gardez le fichier de la clé privée dans votre gestionnaire de mots de passe, avec sa phrase, et une copie hors ligne.

**Une clé de secours** : créez de la même façon `kyfrev-2`, rangée ailleurs, et ajoutez aussi sa clé publique. Si la
première est perdue ou volée, vous signez avec la seconde ; la version suivante de kyfRev retire la clé perdue de la
liste.

## Publier

Chaque fois que le dossier `extensions/` change :

```bash
dotnet run tools/catalogue/catalogue.cs -- publier C:\chemin\hors\du\depot\kyfrev-1.pem
```

L'outil relit chaque extension, écrit le catalogue avec le numéro suivant, le signe après avoir demandé la phrase de
passe, et le vérifie comme le fera l'application. Il ne reste qu'à proposer `catalogue/` dans une PR.

## Annoncer une version

La recherche de mise à jour, dans Paramètres › À propos, lit `mise-a-jour/derniere.json` et sa signature, signés par les mêmes
clés. Elle ne télécharge ni n'installe rien ; la page proposée se déduit de la version signée. À chaque version publiée :

```bash
dotnet run tools/catalogue/catalogue.cs -- version C:\chemin\hors\du\depot\kyfrev-1.pem 1.0.0
```

Il ne reste qu'à proposer `mise-a-jour/` dans une PR. Sans ce fichier, la recherche dit qu'elle n'a pas abouti.

Pour vérifier le catalogue publié, sans clé :

```bash
dotnet run tools/catalogue/catalogue.cs -- verifier
```

Un test de la CI fait la même chose : si `catalogue/` existe, sa signature doit être bonne, par une clé connue, et chaque
fichier qu'il décrit doit être identique à celui de `extensions/`. Une extension modifiée sans republier le catalogue
fait donc échouer la CI, au lieu d'être refusée chez l'utilisateur.
