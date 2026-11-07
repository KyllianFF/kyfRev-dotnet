# Le coffre chiffré

Le chiffrement au repos est une option par coffre (plan de refonte, §2.8 et étape J8). Ce document décrit les formats du
noyau (`src/KyfRev.Infrastructure.Crypto`) : ce qui est écrit sur le disque, et ce que chaque vérification refuse. Le
modèle de menace complet viendra avec le dernier lot.

## Les choix

- **Argon2id**, par libsodium (paquet NSec). Il est réglé sur le poste à la création pour durer environ 0,5 s, sans
  descendre sous 64 Mio de mémoire et 3 passes. libsodium ne calcule Argon2id que sur un seul fil, là où la RFC 9106
  en conseille quatre ; les passes ajoutées par le réglage compensent.
- **PBKDF2-HMAC-SHA256**, 600 000 itérations au moins, reste lisible en option.
- **Verrouillage automatique** après 15 minutes d'inactivité par défaut.
- **« Se souvenir sur cet ordinateur »** est proposé, et décoché par défaut.

## Les clés

- **La clé de données** : 256 bits aléatoires, une par coffre. Elle chiffre tous les fichiers et ne change jamais. Changer
  le mot de passe ou la clé de récupération ne réécrit donc aucun fichier.
- **La clé du mot de passe** : tirée du mot de passe et d'un sel de 16 octets propre au coffre. Elle enveloppe la clé de
  données.
- **La clé de récupération** : 256 bits aléatoires, montrés une seule fois et écrits en base32 de Crockford (sans I, L,
  O ni U). Ils sont suivis de quatre caractères de contrôle, en 14 groupes de quatre. Une faute de frappe est refusée
  à la saisie. La clé qui enveloppe vient de HKDF-SHA256, salée par l'identifiant du coffre.
- **La clé retenue par Windows** (« se souvenir sur cet ordinateur ») : la clé de données protégée par DPAPI pour la
  session de l'utilisateur, avec une entropie propre à kyfRev et au coffre. Elle reste sur ce poste, jamais dans le
  coffre.

Chaque clé est effacée de la mémoire après usage (`CryptographicOperations.ZeroMemory`).

## L'en-tête

Un fichier JSON, sans rien de secret en clair :

```json
{
  "format": 1,
  "coffre": "v_01JC…",
  "contenu": "aes-256-gcm",
  "motDePasse": {
    "derivation": "argon2id",
    "memoireKio": 65536,
    "passes": 4,
    "sel": "…",
    "enveloppe": "…"
  },
  "recuperation": { "enveloppe": "…" }
}
```

Une enveloppe fait 60 octets : nonce (12), clé de données chiffrée (32), étiquette GCM (16). Ses données associées
lient son rôle (`mot-de-passe` ou `recuperation`), l'identifiant du coffre et, pour le mot de passe, chacun de ses
réglages. Changer un réglage ou l'identifiant dans le fichier fait échouer l'ouverture. Ça empêche aussi d'affaiblir
les réglages en cachette.

## Un fichier chiffré

| Octets | Contenu |
|---|---|
| 3 | `KYF` |
| 1 | version du format, `1` |
| 12 | nonce aléatoire, neuf à chaque écriture |
| n | contenu chiffré en AES-256-GCM |
| 16 | étiquette |

Les données associées sont `KYF`, la version, puis l'identifiant du coffre et celui du fichier, chacun précédé de sa
longueur. Un fichier recopié sous un autre identifiant, ou dans un autre coffre, ne s'ouvre plus.

## Le coffre sur le disque

Un coffre chiffré ne garde aucun nom en clair : ni les titres, ni les dossiers, ni le texte. Ce qui se voit : le nombre
d'objets, la taille de chacun (celle de son fichier, plus 32 octets) et sa date de dernière écriture. Se voient aussi la
taille et la date de `index.kyf` : sa taille grandit avec le nombre de fichiers et la longueur de leurs chemins, le
nombre de dossiers et la longueur de leurs chemins ; sa date est celle du dernier changement de l'arborescence (un fichier créé, déplacé, renommé ou
effacé, un dossier créé), qu'une simple réécriture ne change pas.

| Chemin | Contenu |
|---|---|
| `kyfrev-chiffre.json` | L'en-tête ci-dessus, en clair, sans rien de secret. |
| `index.kyf` | L'index, chiffré comme un fichier sous l'identifiant `index` : chaque chemin du coffre et son objet, puis les dossiers (vides compris). |
| `objets/<identifiant>.kyf` | Un fichier du coffre, chiffré sous son identifiant : 26 caractères de base32 de Crockford, tirés au hasard. |
| `.suivi/verrou`, `.suivi/verrou-info.json` | Le verrou de l'application, en clair comme pour un coffre en clair : le poste, le processus et l'heure d'ouverture, rien du contenu. |

Un objet recopié sous un autre identifiant, ou un index venu d'un autre coffre, ne s'ouvre pas : leurs données associées
les lient à leur place. Comme sous Windows, la casse ne distingue pas deux chemins, mais elle est gardée : un coffre
déchiffré revient tel qu'il était.

Chaque écriture passe par un fichier temporaire voisin, qui contient l'objet déjà chiffré, puis un remplacement : rien ne
s'écrit en clair, même un instant. Chaque opération est l'ancienne ou la nouvelle, jamais un mélange :

- **un fichier réécrit** garde son objet, remplacé d'un coup, avec un nonce neuf ;
- **un nouveau fichier** écrit son objet, puis l'index ; tant que l'index n'est pas écrit, le fichier n'existe pas ;
- **un déplacement, un renommage, un nouveau dossier** ne réécrivent que l'index ;
- **un effacement** réécrit l'index, puis efface les objets. Un objet qui ne s'efface pas (un autre programme le tient
  ouvert) reste sur le disque : l'effacement le dit et le nomme, comme un coffre en clair dit qu'un fichier n'a pas pu
  être effacé.

Si l'index ne s'écrit pas, l'index en mémoire redevient celui du disque. Un objet qu'aucune entrée ne nomme (une
écriture coupée net, un effacement empêché) est ignoré, et aucune sauvegarde ne le recopie. La taille et la date d'un fichier sont celles de son objet : un déplacement ne les change
pas, comme sur un disque.

## La session sur un coffre chiffré

La session du coffre ne lit et n'écrit ses fichiers que par une couche de stockage : le port `VaultStorage`, dans
`KyfRev.Application` (la couche que les deux infrastructures partagent, sans dépendre l'une de l'autre). Chaque lecture,
écriture, création, déplacement, effacement, énumération, vérification d'existence ou de date y passe.
`DiskStorage.Instance` (dans `KyfRev.Infrastructure.Vault`) est le disque tel quel ; `EncryptedStorage` (dans
`KyfRev.Infrastructure.Crypto`) fait d'un coffre chiffré un disque : chaque chemin du coffre est une entrée de l'index, chaque
fichier un objet, et il pose ses propres fichiers par le disque qu'on lui donne. La session, les sauvegardes et l'analyse en
lecture seule s'y ouvrent sans rien savoir du chiffrement ; la racine de composition choisit le stockage.

La règle « rien ne s'efface » tient toujours : le stockage n'efface que par `Purge`, à la demande expresse depuis la
corbeille, et pour le dossier temporaire d'une conversion et son marqueur. Le coffre chiffré y retire l'entrée de son
index, puis ses objets ; c'est le seul endroit où il efface un objet. La règle des effacements tient deux comptes exacts,
fichier par fichier : les effacements directs du disque, et chaque purge (`.Purge`), quoi qui la reçoive (un stockage, ou
l'effacement définitif d'un cas d'usage). Un effacement direct ne peut donc pas prendre la place d'un appel `Purge` sans
qu'elle le voie.

Un objet qui ne s'efface pas (un antivirus le tient ouvert) n'arrête pas l'effacement d'une fiche ou d'un dossier : le
stockage le dit par une `LeftOnDiskException` (le port de `KyfRev.Application`), qui signifie « effacé du coffre, resté
sur le disque », et non « rien n'a changé ». La session continue chaque effacement restant, puis dit en une fois ce qui
reste : la fiche ne reste jamais à moitié effacée, introuvable dans la corbeille mais encore sauvegardée.

Le stockage chiffré lit son index à l'ouverture, avant que la session prenne le verrou du coffre : un autre processus a
pu écrire entre-temps. La session le relit donc une fois le verrou pris (`VaultStorage.Reload`, sans effet pour le
disque) ; s'il ne s'ouvre plus, elle rend le verrou et refuse d'ouvrir le coffre.

Restent sur le disque, hors du coffre chiffré : le verrou, les dossiers des sauvegardes et des exports, les notes à
importer. Le stockage chiffré reste à celui qui l'a ouvert, qui le libère après la session : sa clé de données est alors
effacée de la mémoire.

## Les sauvegardes

- **Une sauvegarde** recopie les fichiers tels qu'ils sont posés sur le disque : pour un coffre chiffré, son en-tête, son
  index et les objets que l'index nomme, sans le verrou. Aucun titre, aucun dossier, aucun texte ne s'y lit. Le nombre de
  fichiers annoncé est celui du coffre. Un fichier dont l'objet manque sur le disque manque aussi à la sauvegarde, comme
  pour un coffre en clair : la sauvegarde réussit, et la fiche manquante ne s'y trouve pas.
- **Elle se restaure** telle quelle, en un coffre chiffré qui s'ouvre avec le même mot de passe : rien n'est déchiffré pour
  cela.
- **Une fiche manquante** revient d'une sauvegarde chiffrée par son index, ouvert avec la clé du coffre ; ou d'une
  sauvegarde faite avant le chiffrement, lue telle quelle. Celle d'un autre coffre, ou un objet altéré, est une sauvegarde
  inutilisable : rien n'est écrit.
- **Les exports et les partages** sont en clair : ils sont faits pour être lus ailleurs, et seulement à la demande.

## L'outil `kyfrev-coffre`

```
kyfrev-coffre chiffrer <coffre en clair> <nouveau dossier> [--pbkdf2]
kyfrev-coffre dechiffrer <coffre chiffré> <nouveau dossier>
```

- **Toujours vers un nouveau dossier** : la destination ne doit pas exister, ni se trouver dans la source ou autour d'elle.
  La source reste intacte, pas même son verrou n'est écrit.
- **Le mot de passe** se tape deux fois, sans écho. Il fait au moins 12 caractères. `--pbkdf2` choisit PBKDF2 au lieu
  d'Argon2id.
- **La clé de récupération** s'affiche une seule fois, à la fin du chiffrement. `dechiffrer` accepte le mot de passe ou
  cette clé.
- **Le coffre ouvert dans kyfRev** est refusé : l'application tient son verrou.
- **La vérification** : chaque fichier écrit est relu et comparé à l'original par son SHA-256, et le chiffrement repart du
  mot de passe pour rouvrir l'en-tête écrit. Tout s'écrit d'abord dans un dossier temporaire voisin,
  `.<destination>.conversion-<nom tiré au hasard>`, qui ne prend le nom de la destination qu'une fois vérifié. Une
  conversion refusée ou coupée ne laisse jamais de coffre à moitié écrit.
- **Le marqueur** : tant qu'elle travaille, une conversion tient dans son dossier temporaire le fichier
  `.kyfrev-conversion`, créé et ouvert d'un même geste, que personne d'autre ne peut ouvrir. Elle le retire juste avant
  de donner au dossier le nom de la destination.
- **Une conversion refusée** retire son dossier temporaire, son marqueur en dernier : tenu (ou reposé, s'il avait déjà été
  retiré) pendant que tout le reste s'efface, il n'est relâché qu'ensuite, et effacé avec le dossier. Si un fichier ne
  s'efface pas (un aperçu de l'Explorateur, un logiciel de synchronisation le tient ouvert), le marqueur reste, et le
  message le dit : la conversion suivante vers la même destination finira le travail. S'il n'a pas pu être reposé, le
  message nomme le dossier et demande de le retirer soi-même.
- **Une conversion coupée net** (arrêt brutal, disque perdu) peut laisser son dossier temporaire ; la conversion suivante
  vers la même destination le retire s'il a son nom exact et son marqueur, que plus personne ne tient. Un dossier sans
  marqueur, ou dont le marqueur est tenu (une conversion en cours), ou qu'on a ouvert dans kyfRev depuis (il a son verrou,
  qu'une conversion n'écrit jamais), reste. Un arrêt brutal avant la pose du marqueur (le temps de créer le dossier et,
  pour un chiffrement, l'en-tête d'un coffre vide) laisse un dossier qui ne contient rien du coffre ; un arrêt brutal entre
  le retrait du marqueur et le renommage, un instant, laisse le coffre converti entier, sans marqueur (après un
  déchiffrement, il est en clair). Ni l'un ni l'autre n'est retiré par la suite : il faut le faire soi-même. C'est le prix
  de la sûreté : un dossier sans marqueur n'est jamais pris pour un reste.
- **Une source dans un dossier temporaire** est refusée. La source, ou l'un de ses dossiers parents, porte le marqueur :
  le disque le dit, quel que soit le chemin pris (une jonction, un nom court 8.3, un lecteur substitué sur le dossier
  temporaire), et le chemin réel, liens résolus, est remonté lui aussi. Un lecteur substitué ou un partage qui mène dans
  un sous-dossier du reste n'est ni un lien ni un parent : avant de retirer un reste, la conversion compare chacun de ses
  dossiers à la source par leur identité sur le disque (numéro du volume, identifiant du dossier), et refuse si l'un est la
  source. Pour garder un tel coffre, il faut retirer son marqueur, une fois sûr qu'aucune conversion ne tourne, puis le
  renommer.
- **Le verrou** (`.suivi/verrou`, `.suivi/verrou-info.json`) n'est pas recopié : c'est l'état de l'application, pas du
  contenu.

L'application n'ouvre pas encore un coffre chiffré : son écran de déverrouillage vient avec le lot suivant.

## Ce que les tests vérifient

- **Les vecteurs publiés** : AES-256-GCM (McGrew et Viega, cas 13 et 14) et PBKDF2-HMAC-SHA256 (RFC 7914, §11).
- **Argon2id** : recalculé par une seconde implémentation, indépendante de libsodium, qui doit rendre les mêmes octets.
- **Un fichier chiffré** : chaque octet modifié, un à un, est refusé, comme un fichier tronqué, un autre identifiant, un
  autre coffre ou une autre clé.
- **L'en-tête** :
  - un mauvais mot de passe, une autre clé de récupération ou une faute de frappe sont refusés ;
  - un réglage ou l'identifiant changés dans le fichier, comme un en-tête illisible, sont refusés ;
  - un nouveau mot de passe garde la même clé de données et retire l'ancien ;
  - une nouvelle clé de récupération retire l'ancienne.
- **La clé retenue** ne s'ouvre que pour son coffre, dans cette session.
- **Le coffre sur le disque** : rien ne nomme ni ne montre une fiche. Un objet altéré, un objet recopié sous un autre
  identifiant, un index d'un autre coffre ou une mauvaise clé sont refusés, et un chemin qui sortirait du coffre aussi. Un
  index qui nomme deux fois le même fichier, d'un format inconnu ou qui désigne un objet invalide est refusé.
- **Le stockage chiffré face au disque** : chaque opération (création, remplacement, lecture, taille, date, déplacement,
  renommage de casse, dossier, effacement, énumération, motif) donne le même résultat ou la même erreur sur le disque et
  dans un coffre chiffré, et les mêmes fichiers relus. Une suite d'écritures est coupée à chaque écriture, une fois puis
  net : après chacune, l'index en mémoire est celui du disque, et tout ce qu'il nomme se relit.
- **La session sur les deux stockages** : ses essais tournent sur le disque puis sur un coffre chiffré. Les treize
  scénarios comparés à la 0.x y donnent le même coffre, à l'octet près, et le disque d'un coffre chiffré n'y montre que
  son en-tête, son index, un objet par fichier et le verrou. Les exports du coffre de référence chiffré sont ceux de la 0.x.
- **Les sauvegardes** : celle du coffre de référence chiffré ne laisse lire ni titre, ni dossier, ni texte, se restaure en un coffre
  chiffré identique, et rend une fiche manquante ; une sauvegarde d'un autre coffre ou altérée ne rend rien. Un objet
  manquant n'empêche ni la sauvegarde, ni la restauration des autres fiches ; un objet qu'un effacement n'a pas pu retirer
  est nommé, et ne part dans aucune sauvegarde.
- **L'index relu** : une session ouverte sur un stockage qui a lu l'index avant l'écriture d'un autre processus voit cette
  écriture et ne la perd pas ; un index qui ne s'ouvre plus laisse le verrou libre.
- **La conversion** : le coffre de référence, chiffré puis déchiffré par le mot de passe et par la clé de récupération,
  revient à l'octet près, et la source n'a pas changé. Deux saisies différentes, un mot de passe trop court, une
  destination qui existe ou dans la source, un dossier qui n'est pas un coffre, un coffre ouvert dans kyfRev, un mauvais
  secret ou un objet altéré : rien n'est écrit, et aucun dossier temporaire ne reste.
- **La conversion coupée** : dans chaque sens, chaque écriture échoue à son tour, une fois puis net. La source n'a pas
  changé, aucune destination n'existe, et la conversion suivante réussit sans laisser de dossier temporaire. Un fichier
  tenu dans le dossier temporaire après la vérification fait échouer le renommage : le marqueur reste, le message le dit,
  et la conversion suivante retire le dossier. Un dossier qui n'a que le début du nom d'un dossier temporaire n'est jamais
  retiré, ni un dossier qui en a le nom sans le marqueur, ni celui d'une conversion en cours (deux conversions vers la même
  destination ne se gênent pas), ni un dossier ouvert dans kyfRev depuis. Une source dans un dossier marqué est refusée,
  atteinte par son chemin, par une jonction, par un nom court ou par un lecteur substitué, sur le dossier temporaire comme
  sur un sous-dossier. La clé qui relit le coffre écrit est effacée, même quand la relecture échoue.
- **L'effacement empêché** : un objet tenu par un autre programme, qu'il soit celui du suivi, des annotations ou du texte
  d'une fiche, ou celui d'une fiche d'un dossier, n'arrête pas l'effacement : tout le reste est effacé, et l'erreur nomme
  ce qui est resté.
- **La casse des objets** : un objet renommé en minuscules garde sa taille, sa date, et sa place dans les sauvegardes. Dans
  un dossier sensible à la casse, un jumeau en minuscules qu'aucune entrée ne nomme reste hors des sauvegardes, et une
  archive qui contient les deux se lit par l'écriture exacte.
