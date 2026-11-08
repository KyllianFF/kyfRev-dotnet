# Le coffre chiffré

Le chiffrement au repos est une option par coffre (plan de refonte, §2.8 et étape J8). Ce document décrit les formats du
noyau (`src/KyfRev.Infrastructure.Crypto`) : ce qui est écrit sur le disque, et ce que chaque vérification refuse. Ce
contre quoi il protège, et ce contre quoi il ne protège pas, se lit dans [Le modèle de menace](#le-modèle-de-menace).

## Les choix

- **Argon2id**, par libsodium (paquet NSec). Il est réglé sur le poste à la création pour durer environ 0,5 s, sans
  descendre sous 64 Mio de mémoire et 3 passes. libsodium ne calcule Argon2id que sur un seul fil, là où la RFC 9106
  en conseille quatre ; les passes ajoutées par le réglage compensent.
- **PBKDF2-HMAC-SHA256**, 600 000 itérations au moins, reste lisible en option.
- **Verrouillage automatique** après 15 minutes d'inactivité par défaut.
- **« Se souvenir sur cet ordinateur »** est proposé, et décoché par défaut.

## Les clés

- **La clé de données** : 256 bits aléatoires, une par coffre. Elle chiffre tous les fichiers et ne change jamais. Changer
  le mot de passe ou la clé de récupération ne réécrit donc aucun fichier. Il s'ensuit qu'une copie plus ancienne de
  l'en-tête (une sauvegarde, l'historique d'un dossier synchronisé) s'ouvre encore avec l'ancien secret, et en tire la
  clé qui lit aussi le coffre d'aujourd'hui : après la fuite d'un secret, en changer ne suffit pas, il faut déchiffrer le
  coffre puis le chiffrer de nouveau, ce qui tire une nouvelle clé de données.
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
corbeille, et pour le dossier temporaire qu'une conversion vient de créer, et son marqueur. Le coffre chiffré y retire
l'entrée de son index, puis ses objets ; c'est le seul endroit où il efface un objet. La règle des effacements tient trois
comptes exacts, fichier par fichier, sur le code seul (un commentaire, qui finit à toute fin de ligne, n'efface rien) : les
effacements directs du disque, appelés ou passés comme délégués (`File.Delete`, `info.Delete`, `DeleteOnClose`) ; chaque
purge (`.Purge`), quoi qui la reçoive (un stockage, ou l'effacement définitif d'un cas d'usage) ; et chaque `.Delete` qui
n'efface rien, d'une liste fermée d'écritures exactes (`FileShare.Delete`, `Key.Delete`, `SheetActionsText.Delete`),
nommé : `backupText.Delete()` ou `cacheKey.Delete()` comptent comme des effacements. Un effacement direct ne peut donc pas
prendre la place d'un appel `Purge`, ni se glisser sous un nom qui n'efface rien, sans qu'elle le voie ; et ses contrôles
lisent le code seul, qu'un appel mis en commentaire ne satisfait pas.

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
- **Les exports et les partages** sont en clair : ils sont faits pour être lus ailleurs, et seulement à la demande. Seul
  « Coffre complet (.zip) », qui est une sauvegarde, garde le chiffrement du coffre.

## L'outil `kyfrev-coffre`

```
kyfrev-coffre chiffrer <coffre en clair> <nouveau dossier> [--pbkdf2]
kyfrev-coffre dechiffrer <coffre chiffré> <nouveau dossier>
kyfrev-coffre verifier <coffre> [--json]
kyfrev-coffre changer-mot-de-passe <coffre chiffré> [--pbkdf2]
kyfrev-coffre cle-de-recuperation <coffre chiffré>
```

- **`changer-mot-de-passe`** et **`cle-de-recuperation`** ne réécrivent que l'en-tête, d'un seul geste, coffre fermé : un
  coffre ouvert dans kyfRev est refusé. Elles tiennent le verrou du coffre tout du long, en créant `.suivi/verrou` s'il n'existe
  pas encore : l'application ne l'ouvre pas pendant ce temps. Les deux acceptent le mot de passe ou la clé de récupération
  actuels ; si les réglages du mot de passe ne se lisent plus, seule la clé de récupération est essayée, et le dit. La nouvelle
  clé s'affiche une fois et ne remplace l'ancienne qu'après la réponse « oui » : une clé que personne n'a notée ne remplace
  jamais l'ancienne. Sur un refus, le code de sortie est 3 et l'en-tête n'a pas changé. Une sauvegarde faite avant garde son
  ancien en-tête, donc ses anciens secrets.

- **`verifier`** lit un coffre en clair comme un coffre chiffré. Pour un coffre chiffré, il demande son mot de passe ou sa
  clé de récupération (une ligne, sans écho), lit le coffre par sa clé et fait les mêmes contrôles qu'en clair ; il
  n'écrit aucun octet, pas même le verrou. Un mauvais secret est refusé, avec le code de sortie 3.

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
- **Le marqueur** : une conversion crée son dossier temporaire, vérifie qu'il est vide (sinon ce n'est pas le sien, et elle
  refuse sans y toucher), puis y pose le fichier `.kyfrev-conversion`, créé et ouvert d'un même geste, que personne d'autre
  ne peut ouvrir. Elle le retire juste avant de donner au dossier le nom de la destination. Deux fenêtres restent donc sans
  marqueur : avant sa pose (pour un chiffrement, le temps de poser l'en-tête d'un coffre vide), et entre son retrait et le
  renommage. Un déchiffrement coupé net dans la seconde laisse le coffre entier en clair, sans marqueur.
- **Une conversion n'efface que son propre dossier temporaire**, celui qu'elle a créé et trouvé vide. Refusée, elle le
  retire, son marqueur en dernier : tenu (ou repris, ou reposé s'il avait déjà été retiré) pendant que tout le reste
  s'efface, il n'est relâché qu'ensuite, et effacé avec le dossier. S'il ne peut être ni repris ni reposé, le dossier est
  vidé tout de même : il n'appartient qu'à elle. Si un fichier ne s'efface pas (un aperçu de l'Explorateur, un logiciel de
  synchronisation le tient ouvert), le dossier reste, et le message le nomme et demande de le retirer à la main. Une erreur
  imprévue (ni le disque, ni les droits, ni le chiffrement, ni un chemin refusé) remonte telle quelle et laisse le dossier
  en place, marqué : la conversion suivante le nomme.
- **Le reste d'une autre conversion n'est jamais effacé.** Une conversion coupée net (arrêt brutal, disque perdu) peut
  laisser son dossier temporaire, et rien ne le retire ensuite. Tant qu'un dossier qui porte le nom exact d'un dossier
  temporaire de la destination est à côté d'elle, marqué ou non (une conversion coupée, même dans une fenêtre sans marqueur,
  ou une conversion en cours), la conversion vers elle refuse, sans rien écrire : le message nomme le dossier, dit ce qu'il
  est (après un déchiffrement, il contient le coffre en clair) et demande de le retirer à la main. Le nom se compare sans la
  casse, comme sous Windows. Deux conversions vers la même destination ne se gênent donc pas : la seconde refuse. Une
  conversion coupée net est rare, et retirer un dossier nommé reste simple ; aucun nettoyage n'a ainsi à deviner ce qui lui
  appartient. Un dossier dont le nom n'a que le début de celui d'un dossier temporaire n'est ni touché, ni une raison de
  refuser. Un dossier voisin qui ne se lit pas fait refuser aussi.
- **Une source dans un dossier temporaire** est refusée : la source, ou l'un de ses dossiers parents, porte le marqueur.
  Le disque le dit, quel que soit le chemin pris (une jonction, un nom court 8.3, un lecteur substitué sur le dossier
  temporaire), et le chemin réel, liens résolus, est remonté lui aussi. Un lecteur substitué ou un partage qui mène dans un
  sous-dossier n'est ni un lien ni un parent : il n'est pas refusé, mais rien n'est effacé pour autant. Pour garder un tel
  coffre, il faut retirer son marqueur, une fois sûr qu'aucune conversion ne tourne, puis le renommer.
- **Le verrou** (`.suivi/verrou`, `.suivi/verrou-info.json`) n'est pas recopié : c'est l'état de l'application, pas du
  contenu.

## L'ouverture dans l'application

- **Un coffre chiffré se reconnaît à son en-tête** (`kyfrev-chiffre.json`) : à son ouverture, au démarrage, depuis
  l'accueil ou depuis Paramètres › Données, l'application lui demande sa clé avant de rien lire de lui. Le port
  `IVaultKeys` (`KyfRev.Application`) reconnaît le coffre, tire sa clé et la retient ; `EncryptedVaults`
  (`KyfRev.Infrastructure.Crypto`) l'implémente, et ouvre aussi les coffres en clair, sur le disque, comme avant. Le
  coffre prévu du mode portable ou installé qui n'a que son en-tête (tout juste chiffré, son dossier de suivi n'est encore
  que dans son index) est reconnu comme un coffre existant, et non proposé à la création.
- **L'écran de déverrouillage** ne montre ni le menu ni la recherche. Il demande le mot de passe ou, s'il est perdu, la
  clé de récupération, telle qu'elle a été recopiée (tirets, espaces et majuscules indifférents). Le mot de passe passe
  du champ à la commande, et le champ est vidé aussitôt. La dérivation se fait hors du fil de l'interface. Un refus
  (mauvais mot de passe, faute de frappe, index illisible) laisse le coffre en attente, avec sa raison. Un autre coffre
  peut être ouvert à la place ; si le changement venait des paramètres, le coffre qui était ouvert le reste jusqu'au
  déverrouillage, et l'écran propose d'y revenir.
- **La session prend le stockage chiffré** : elle le libère en se fermant, et la copie de la clé qu'il gardait est
  effacée. Une session qui ne s'ouvre pas le libère tout de suite.
- **« Se souvenir sur cet ordinateur »**, décoché par défaut. Coché, la clé de données est protégée par Windows pour la
  session de l'utilisateur (DPAPI) et rangée dans `cles-retenues.json`, dans le dossier des données du poste (celui de
  l'application en mode portable) : l'identifiant du coffre, son dossier et la clé protégée, jamais la clé en clair. Elle
  ne sert qu'à ce coffre, dans ce dossier, dans cette session de Windows : une copie du coffre ailleurs, un autre compte
  ou un autre poste redemandent le mot de passe. Elle est vérifiée sur l'index avant de servir ; une clé qui ne l'ouvre
  plus est oubliée. Déverrouiller sans cocher oublie celle qui était retenue. Oublier réécrit le fichier, sans l'effacer.
  Coché, le coffre ne protège plus contre quelqu'un qui utilise la session de l'utilisateur, seulement contre un vol du
  disque.

## Le verrouillage

- **À la demande** : Ctrl+Maj+L, ou « Verrouiller le coffre chiffré » dans la recherche (Ctrl+K), tant qu'un coffre
  chiffré est ouvert. Un coffre en clair ne se verrouille pas.
- **De lui-même** : après 15 minutes sans rien toucher dans toute la session de Windows (`GetLastInputInfo` : ce qui se
  tape dans le texte mis en forme compte aussi), et dès que la session de Windows se verrouille ou que le poste se met en
  veille (`IUserPresence`, `AutoLock`). Un départ qui tombe pendant une ouverture reste dû : le coffre se verrouille dès
  qu'il le peut, sauf si l'utilisateur est revenu entre-temps.
- **Même derrière un autre écran** : un coffre chiffré resté ouvert derrière l'écran de déverrouillage d'un autre coffre
  (choisi depuis les paramètres) se verrouille aussi, et c'est son écran qui s'affiche. Rien ne se verrouille pendant
  qu'une ouverture est en cours.
- **Dans cet ordre** (`VaultLocking`), pour que rien ne se perde ni ne reste :
  1. ce qui s'écrit s'enregistre (`ILeaving`) : le texte de la fiche, jusqu'à la dernière frappe du texte mis en forme
     (une frappe qui n'a pas été relue garde le coffre ouvert) ; le commentaire ou la définition en cours dans une fiche
     (s'il a changé depuis son ouverture) ; un dessin, une formule ou un lien en cours d'insertion ; un schéma, jusqu'au
     champ qu'on n'a pas encore quitté ; une définition en cours de modification dans la vue « Définitions », même si elle
     n'est pas affichée (incomplète, refusée ou changée ailleurs depuis, elle est abandonnée : un brouillon que personne
     ne voit ne garde pas le coffre ouvert). Une séance en cours se garde pour être reprise. Si ce qui s'écrit n'a pas pu l'être (un conflit,
     un refus, un disque plein), le coffre reste ouvert, et le verrouillage de lui-même réessaie. Ce qui n'est qu'un
     formulaire est abandonné : le titre d'une nouvelle fiche ou d'un nouveau schéma, un renommage ou un déplacement, les
     réglages non enregistrés, la réponse tapée pour une sélection guidée, les recherches ;
  2. l'écran de déverrouillage remplace les vues, qui se ferment : le texte mis en forme et les schémas, avec leur moteur
     web ;
  3. le coffre se ferme, une fois finie la lecture en cours : son verrou est libéré, sa clé effacée, et son index en clair
     (les chemins, donc les titres) oublié ;
  4. chaque vue oublie ce qu'elle avait lu (`IForgetsVault`) : « Aujourd'hui », les fiches et leur aperçu, les
     définitions, les archives, les statistiques, la santé du coffre, la corbeille et les données des paramètres, le
     journal, les erreurs et les pages des extensions (leur moteur repart à vide), les dessins des schémas ; les modules se
     déchargent. Une lecture partie avant n'y remet rien.
- **Seul son mot de passe ou sa clé de récupération le rouvre** : la clé retenue sur ce poste ne sert qu'au démarrage,
  sinon verrouiller ne protégerait de rien. Une fois verrouillé, il n'est plus ouvert par elle, ni par « Ouvrir un autre
  coffre… » ni par l'accueil, jusqu'à ce qu'on le déverrouille.
- **Ce que le verrouillage ne peut pas effacer** : les chaînes de .NET ne s'effacent pas (le mot de passe tapé, le texte
  lu), seulement leurs références ; la mémoire libérée, le fichier d'échange et la mise en veille prolongée peuvent en
  garder des traces, et ce qui a été copié reste dans le presse-papiers, et dans son historique s'il est activé. La clé de
  récupération copiée depuis la carte Chiffrement (par « Copier », le clavier ou le menu) est exclue de l'historique de
  Windows et du partage entre appareils, et signalée aux programmes qui surveillent le presse-papiers, qui peuvent
  l'ignorer ; elle reste jusqu'à la copie suivante. BitLocker reste recommandé.

## La carte Chiffrement

Dans Paramètres › Données, pour un coffre chiffré ouvert seulement. Chaque fenêtre qui demande le mot de passe propose
« J'utilise la clé de récupération » : la clé se saisit alors dans un champ qui la montre, comme sur l'écran de
déverrouillage. Ce qui a été tapé est vidé dès que la fenêtre se ferme ou se cache.

- **Changer le mot de passe** : le mot de passe actuel (ou la clé de récupération, s'il est perdu), puis le nouveau deux
  fois, de 12 caractères au moins. Seule l'enveloppe de la clé de données change, dans l'en-tête écrit de façon
  atomique : aucune fiche n'est réécrite, la clé de récupération et la clé retenue sur ce poste restent valables, et
  l'ancien mot de passe n'ouvre plus le coffre tel qu'il est ; les sauvegardes et les copies faites avant s'ouvrent encore
  avec lui (voir « Les clés »). La dérivation est réglée sur le poste (Argon2id), hors du fil de l'interface.
- **Créer une nouvelle clé de récupération** : demande le mot de passe (ou la clé de récupération actuelle), puis prépare
  la nouvelle clé sans rien écrire et la montre une seule fois, à copier et à ranger. Elle ne remplace l'ancienne qu'une
  fois « J'ai rangé ma clé de récupération » coché et « Enregistrer cette clé » choisi : l'en-tête est alors réécrit,
  s'il est toujours celui qu'elle a lu, et la clé est oubliée de l'écran. « Annuler », Échap, un verrouillage ou
  l'application fermée la laissent de côté : rien n'a changé, l'ancienne reste la seule qui ouvre le coffre. Ainsi, une
  clé que personne n'a notée ne remplace jamais celle qui l'est, même quand le mot de passe est perdu. Enregistrée,
  l'ancienne n'ouvre plus le coffre tel qu'il est ; les sauvegardes et les copies faites avant, qui gardent l'ancien
  en-tête, s'ouvrent encore avec elle.
- **Se souvenir sur cet ordinateur** : coché, le mot de passe (ou la clé de récupération) est demandé et la clé retenue
  (voir plus haut) ; décoché, elle est oubliée tout de suite.
- **Verrouillage automatique** : après 5, 15, 30 minutes, une heure, ou jamais (l'inactivité ne verrouille plus, le départ de
  l'utilisateur si). Le réglage voyage avec le coffre : `"verrouillage": { "apresMinutes": … }` dans `.suivi/coffre.json`
  (0 pour jamais), écrit seulement s'il diffère des 15 minutes par défaut, si bien qu'un coffre qui ne l'a jamais changé
  reste identique, octet pour octet, à celui de la 0.x. Chiffré, ce fichier ne se lit ni ne se change sans la clé. Le
  délai est lu une fois à l'ouverture du coffre, puis repris de la carte à chaque réglage : le minuteur, qui regarde
  l'inactivité toutes les 20 secondes, ne touche pas au coffre, et ne regarde rien pour un coffre en clair. Un réglage
  refusé (le fichier tenu par un autre programme) le dit, chaque fois, et la liste remontre le délai toujours en
  vigueur.
- **Verrouiller maintenant** : comme Ctrl+Maj+L.

Chaque fenêtre donne le focus à son premier champ ; un refus se dit chaque fois, même s'il est le même que le précédent.

Un verrouillage peut tomber pendant une fenêtre de la carte : la carte oublie tout dès qu'il commence, avant que l'écran de
déverrouillage ne remplace les vues. Ce qu'il interrompt après coup se dit à la prochaine lecture de la carte pour ce même
coffre, et seulement pour lui, tant que l'application reste ouverte : un mot de passe changé pendant la dérivation (c'est
le nouveau qui ouvre le coffre), une nouvelle clé de récupération enregistrée pendant la fermeture, ou que le
verrouillage a empêché d'enregistrer (l'ancienne ouvre toujours le coffre).

## Chiffrer et déchiffrer depuis l'application

La conversion de l'outil sert aussi à l'application, sans changer de règles : toujours vers un nouveau dossier, la source
intacte, chaque fichier relu et comparé à l'original, un dossier temporaire voisin qui ne prend son nom qu'une fois
vérifié. Elle vit dans `KyfRev.Conversion`, le seul endroit, avec l'outil et la racine de composition, qui relie le coffre
en clair et le chiffrement ; la présentation n'y accède que par le port `IVaultConversion`.

- **« Chiffrer ce coffre… »**, dans la carte Coffre de Paramètres › Données, pour un coffre en clair. Une page remplace les
  vues, le coffre restant ouvert derrière elle tant que la conversion n'a pas commencé (« Annuler » y revient) :
  1. **le mot de passe**, deux fois, de 12 caractères au moins. Le chiffrement est alors préparé, sans rien écrire : la
     clé de données est tirée au hasard et enveloppée par le mot de passe (Argon2id, réglé sur le poste) et par une clé
     de récupération neuve ;
  2. **la clé de récupération**, montrée une seule fois, à copier (hors de l'historique du presse-papiers) et à ranger.
     « Retour » l'oublie : une autre viendra ;
  3. **la conversion**, une fois « J'ai rangé ma clé de récupération » coché. Le coffre se ferme d'abord, comme à un
     verrouillage (ce qui s'écrit s'enregistre, puis les vues oublient) : rien ne s'y écrit pendant la copie. La copie
     s'écrit dans « Coffre chiffré », à côté du coffre (« Coffre chiffré (2) » si le nom est pris), et la page montre
     les fichiers écrits puis relus.

  Ensuite, aucun coffre n'est ouvert : « Ouvrir le coffre chiffré » montre l'écran de déverrouillage, qui demande le mot
  de passe ; « Revenir au coffre en clair » le rouvre, intact. C'est à l'utilisateur de retirer le coffre en clair, une
  fois satisfait, **ainsi que ses sauvegardes d'avant** : le dossier `Sauvegardes`, à côté du coffre, garde des copies en
  clair du coffre d'avant, et la page le nomme. Ce dossier sert aussi au nouveau coffre et aux autres coffres rangés à
  côté : il ne se retire pas en entier. Sur un disque SSD, un fichier effacé peut encore se retrouver, et BitLocker
  reste recommandé. En mode portable, kyfRev rouvre toujours le même dossier au démarrage : pour que le nouveau coffre
  soit celui-là, il faut retirer l'ancien dossier, puis donner son nom au nouveau, et la page le dit.
- **« Déchiffrer vers un dossier… »**, dans la carte Chiffrement. La page demande le mot de passe ou la clé de
  récupération, et le dossier où écrire le coffre en clair (à côté du coffre chiffré par défaut). Le secret est vérifié
  avant que le coffre ne se ferme ; la copie s'écrit dans « Coffre (en clair) » (« (2) » si le nom est pris). Ensuite,
  « Ouvrir le coffre en clair » l'ouvre, et « Revenir au coffre chiffré » montre l'écran de déverrouillage, sauf si ce
  poste a retenu sa clé. Le coffre en clair se lit sans mot de passe, ses sauvegardes aussi : la page le dit.
- **Le verrou du coffre source** est tenu pendant toute la conversion, sans y rien écrire : une autre fenêtre de kyfRev le
  trouve pris, comme s'il était ouvert.
- **Ce qui reste comme à l'outil** : un dossier temporaire laissé par une conversion coupée (l'application fermée pendant
  la copie, par exemple) n'est jamais effacé, et une conversion vers le même nom refuse en le nommant, pour qu'on le
  retire à la main ; le coffre de départ, lui, n'a pas changé. Le dossier temporaire porte un nom qui commence par un
  point, à côté de la destination. La fenêtre ne se ferme pas pendant la conversion. Un coffre qui s'ouvre au même moment,
  ou un brouillon qui ne s'enregistre pas (une définition incomplète, par exemple), garde le coffre ouvert : rien n'est
  converti, et, à la différence d'un verrouillage, aucun brouillon n'est abandonné.
- **Si le coffre ne se rouvre pas** après la conversion (un disque retiré, par exemple), l'accueil dit pourquoi et propose
  d'en ouvrir un autre.

## Le modèle de menace

Le chiffrement au repos protège **un coffre lu hors de l'application** : un disque volé, une copie dans le nuage ou sur une
clé USB, une sauvegarde égarée, un poste dont on a retiré le disque. Celui qui n'a ni le mot de passe, ni la clé de
récupération, ni la session Windows qui a retenu la clé, ne lit ni les titres, ni les textes, ni les annotations, ni les
noms des dossiers et des fiches.

### Ce qui se voit quand même

- **Le nombre et la taille des fichiers** : un objet par fichier du coffre, de la taille de ce fichier plus 32 octets, ni
  rembourré ni regroupé ; la taille de `index.kyf` grandit avec le nombre de chemins et leur longueur.
- **Les dates d'écriture** des objets et de l'index : on voit quand le coffre a changé, pas ce qui a changé.
- **L'en-tête** `kyfrev-chiffre.json` : l'identifiant du coffre, les réglages de la dérivation, les enveloppes de la clé.
  Rien qui permette de l'ouvrir sans secret.
- **Le verrou** `.suivi/verrou-info.json` : le poste, le processus et l'heure de la dernière ouverture.

### Ce contre quoi il ne protège pas

- **Un poste compromis pendant que le coffre est ouvert** : un logiciel espion, un administrateur, un enregistreur de frappe.
  Le texte déchiffré est en mémoire, et le mot de passe passe par le clavier.
- **La mémoire de l'application.** La clé de données et les textes y sont tant que le coffre est ouvert ; le verrouillage
  fait oublier les vues et efface la clé, mais le moteur web et le ramasse-miettes ne promettent pas d'effacer leurs copies.
  L'hibernation et le fichier d'échange de Windows peuvent en écrire une partie : seul **BitLocker** couvre ce cas.
- **Un mot de passe faible.** Argon2id en ralentit l'essai, il ne le rend pas impossible.
- **Celui qui peut écrire dans le dossier.** Un objet modifié, ou recopié sous un autre identifiant, est refusé, pas réparé.
  Mais un objet est lié à son coffre et à son identifiant, pas à une version : un objet, ou un index, plus ancien du même
  coffre remis à sa place s'ouvre sans erreur. Et rien n'empêche d'effacer le dossier : le chiffrement ne remplace pas les
  sauvegardes.
- **Ce que l'utilisateur sort lui-même du coffre** : un export, un copier-coller (le presse-papiers et son historique), le
  coffre déchiffré. Le suivi, les annotations, les citations et un schéma exportés sont en clair, et l'application le dit ;
  « Coffre complet (.zip) » est une sauvegarde, il garde le chiffrement du coffre.
- **Ce que le coffre en clair a laissé derrière lui.** Après « Chiffrer ce coffre… », le coffre d'origine et ses sauvegardes
  restent en clair jusqu'à ce que l'utilisateur les retire ; sur un SSD, un fichier effacé peut encore se retrouver.
- **Les anciens secrets dans les sauvegardes.** Une sauvegarde garde l'en-tête de son jour : l'ancien mot de passe et
  l'ancienne clé de récupération l'ouvrent encore. Après la fuite d'un secret, les changer ne suffit pas : il faut déchiffrer
  puis chiffrer de nouveau, ce qui tire une nouvelle clé de données. Cela ne protège que le nouveau coffre : l'ancien coffre
  chiffré et les sauvegardes d'avant s'ouvrent toujours avec le secret fuité, et le dossier déchiffré reste en clair. Il faut
  les retirer aussi.

### Ce qui reste sur le poste, hors du coffre

| Emplacement | Ce qu'on y trouve | Ce qui y est fait |
|---|---|---|
| Le journal, `kyfRev.log` et `kyfRev.precedent.log` | Des événements, des erreurs | Le dossier du profil est masqué, et tout chemin qui entre dans le coffre s'arrête à son premier dossier (`fiches/…`). |
| Le profil du moteur web, `WebView2` | Ses réglages, ses caches de scripts | Le texte d'une fiche lui parvient par la mémoire, en `no-store`, jamais par un fichier ni par le stockage d'une page. Aucun rapport de plantage ne part chez Microsoft ; un plantage du moteur peut en laisser un dans ce dossier, avec un morceau de sa mémoire. |
| Le profil du moteur des extensions, `WebView2-extensions` | Ses réglages, un éventuel rapport de plantage | Comme `WebView2` : un plantage peut y laisser un morceau de sa mémoire, donc du texte donné à une extension. |
| Le dictionnaire de Windows, `%APPDATA%\Microsoft\Spelling` | Les mots ajoutés par l'utilisateur | Sur un coffre chiffré, le menu d'un mot, dans le code Markdown, propose « Accepter jusqu'à la fermeture de kyfRev », qui n'écrit rien : le mot reste accepté dans tous les coffres jusqu'à ce que l'application se ferme. Le texte mis en forme, souligné par le moteur web, ne le propose pas. Ce qui a été ajouté depuis un coffre en clair y reste. |
| `cles-retenues.json` | La clé retenue, si l'utilisateur l'a demandé | Protégée par Windows (DPAPI) : elle s'ouvre dans sa session, sur ce poste ; quiconque ouvre cette session ouvre donc le coffre. |
| `seance.json` | Le chemin du coffre et des identifiants, pendant une séance | Aucun texte de fiche. |
| `preferences.json` | L'apparence | Aucun texte de fiche. |
| `config-locale.json` | Le chemin du dernier coffre ouvert | Aucun texte de fiche. |
| Le dossier temporaire | Le cache du catalogue d'extensions | Aucun texte de fiche. |

Le correcteur d'orthographe de Windows reçoit le texte par la mémoire, à la demande, sur ce poste. La lecture à voix haute
aussi, par le moteur de voix de Windows.

Un dessin et un schéma passent par le moteur web comme le reste. Une extension installée lit ce que ses permissions lui
donnent, dans son propre profil, le réseau coupé : lui accorder la lecture d'un coffre chiffré, c'est lui faire confiance.

### La preuve, et ses limites

Le scénario `J8_nothing_of_an_encrypted_vault_stays_readable_on_the_disk` lance la vraie application sur un coffre chiffré
qui porte un marqueur unique par endroit (titre, texte, dessin, passage, commentaire, terme, explication, frappe). Elle
lit, écrit, cherche, verrouille, se ferme. Avant le lancement, fiche écrite, coffre verrouillé et application fermée, les octets
du dossier d'essai (coffre, sauvegardes, journal, profil du moteur web, dossier temporaire) sont cherchés : en UTF-8 et en
UTF-16 sans tenir compte de la casse, et en base64 aux trois alignements. Les noms de fichiers sont cherchés tels quels, sans
tenir compte de la casse. Un fichier que le moteur tient est passé tant que l'application tourne ; le dernier passage attend
que tout soit lâché, et exige de tout avoir lu. Un témoin laissé en clair doit être trouvé, faute de quoi la preuve serait vide.

Elle ne voit pas ce qui est compressé (un marqueur court reste en général entier dans le format des caches de Chromium, sans
garantie), ni ce qui est écrit hors du dossier d'essai : le dictionnaire de Windows a son propre essai, et le fichier
d'échange reste du ressort de BitLocker.

## Ce que les tests vérifient

- **Le verrouillage** : il montre l'écran de déverrouillage, ferme le coffre (plus rien ne s'y lit ni ne s'y écrit, son
  verrou est libéré) et fait tout oublier aux vues ; la clé retenue ne le rouvre pas, même par « Ouvrir un autre
  coffre… », son mot de passe si. Il se fait aussi derrière l'écran d'un autre coffre, jamais pendant une ouverture. Rien
  ne se verrouille sans coffre chiffré ouvert, ni deux fois à la fois ; ce qui s'écrit et n'a pas pu être enregistré,
  sur la page affichée ou dans une vue qui garde un brouillon, garde le coffre ouvert ; une séance en cours est gardée.
  Le commentaire, la définition, le dessin et le lien en cours sont enregistrés, une frappe du texte mis en forme qui
  n'a pas été relue garde le coffre ouvert, un schéma s'enregistre jusqu'au champ qu'on n'a pas quitté. Un départ
  pendant une ouverture verrouille dès que possible, sauf retour de l'utilisateur. La composition fait oublier toutes
  les vues qui gardent ce qu'elles ont lu. L'inactivité verrouille après le délai (jamais, sans délai), le départ
  de l'utilisateur tout de suite ; la session verrouillée ou le poste en veille, et rien d'autre, comptent comme un
  départ. Chaque vue, chargée sur un vrai coffre, n'en garde rien une fois oubliée, et se remplit de nouveau à sa
  prochaine ouverture ; une lecture partie avant l'oubli n'y remet rien. Le coffre chiffré libéré n'a plus d'index en clair.

- **Les vecteurs publiés** : AES-256-GCM (McGrew et Viega, cas 13 et 14) et PBKDF2-HMAC-SHA256 (RFC 7914, §11).
- **Argon2id** : recalculé par une seconde implémentation, indépendante de libsodium, qui doit rendre les mêmes octets.
- **Un fichier chiffré** : chaque octet modifié, un à un, est refusé, comme un fichier tronqué, un autre identifiant, un
  autre coffre ou une autre clé.
- **L'en-tête** :
  - un mauvais mot de passe, une autre clé de récupération ou une faute de frappe sont refusés ;
  - un réglage ou l'identifiant changés dans le fichier, comme un en-tête illisible, sont refusés ;
  - un nouveau mot de passe garde la même clé de données et retire l'ancien ;
  - une nouvelle clé de récupération ne change rien tant qu'elle n'est pas enregistrée, ne s'enregistre que sur l'en-tête
    qu'elle a lu, et retire alors l'ancienne.
- **La clé retenue** ne s'ouvre que pour son coffre, dans cette session. Rangée par l'application, elle n'est écrite que
  protégée ; elle ne sert qu'à son dossier, sous toute écriture du même chemin, et ni à une copie ni sur un autre poste ;
  une clé que Windows ne rend pas, un en-tête illisible ou une clé qui n'ouvre plus l'index ne rendent rien, et la
  dernière est oubliée. Un fichier des clés illisible ne retient rien, puis se réécrit.
- **L'ouverture d'un coffre chiffré** : sans clé retenue, il attend sa clé et rien n'est ouvert ; un mauvais mot de passe
  ou un index absent le laissent en attente avec la raison ; déverrouillé, il est le coffre ouvert comme un autre (sa
  journée, sa sauvegarde du jour, retenu pour le démarrage suivant) et rien de lui n'est en clair sur le disque. Retenue,
  sa clé l'ouvre au démarrage suivant sans rien demander ; une clé qui n'a pas pu être retenue n'empêche pas l'ouverture.
  Fermée, la session a libéré son stockage chiffré ; une session qui ne s'ouvre pas aussi.
- **L'écran de déverrouillage**, au démarrage, depuis l'accueil et depuis les paramètres : sans menu ni recherche, son
  champ vidé après chaque essai, la clé de récupération à la place du mot de passe, un autre coffre (chiffré ou non) à
  la place, et le retour au coffre resté ouvert. Il tient dans une fenêtre de 470 pixels de large, en clair et en
  sombre, et chaque champ et bouton a son nom.
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
  changé, aucune destination n'existe. Coupée une fois, la conversion ne laisse rien ; coupée net, elle laisse son dossier
  temporaire : la suivante refuse, le nomme et n'y touche pas, puis réussit une fois qu'on l'a retiré à la main. Un fichier
  tenu dans le dossier temporaire après la vérification fait échouer le renommage : le dossier reste avec son marqueur, et
  le message le nomme. Un dossier qui porte le nom exact d'un dossier temporaire de la destination, marqué ou non, quelle
  que soit la casse, laissé, tenu par une conversion en cours ou ouvert dans kyfRev depuis, fait refuser et n'est pas
  touché ; deux conversions vers la même destination : la seconde refuse, la première réussit. Un dossier qui n'a que le
  début de ce nom est laissé tel quel. Un dossier temporaire qu'un autre programme a pris ou rempli juste avant n'est pas
  celui de la conversion : elle refuse sans le vider. Un dossier voisin qui ne se lit pas fait refuser ; une erreur
  d'argument refuse et retire le dossier ; une erreur imprévue remonte, et la conversion suivante nomme le dossier resté ;
  une destination apparue avant le renommage final n'est pas touchée. Une source dans un dossier marqué est refusée,
  atteinte par son chemin, par une jonction, par un nom court ou par un lecteur substitué ; une source atteinte par un
  lecteur substitué sur un sous-dossier n'est pas touchée. La clé qui relit le coffre écrit est effacée, même quand la
  relecture échoue.
- **L'effacement empêché** : un objet tenu par un autre programme, qu'il soit celui du suivi, des annotations ou du texte
  d'une fiche, ou celui d'une fiche d'un dossier, n'arrête pas l'effacement : tout le reste est effacé, et l'erreur nomme
  ce qui est resté.
- **La casse des objets** : un objet renommé en minuscules garde sa taille, sa date, et sa place dans les sauvegardes. Dans
  un dossier sensible à la casse, un jumeau qui ne diffère que par la casse du nom, de l'extension ou du dossier reste hors
  des sauvegardes tant que l'objet écrit exactement `objets/<identifiant>.kyf` est là, et une archive qui contient les deux
  se lit par l'écriture exacte.
