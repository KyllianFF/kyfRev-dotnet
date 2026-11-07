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

Un coffre chiffré ne garde aucun nom en clair : ni les titres, ni les dossiers, ni les tailles exactes.

| Chemin | Contenu |
|---|---|
| `kyfrev-chiffre.json` | L'en-tête ci-dessus, en clair, sans rien de secret. |
| `index.kyf` | L'index, chiffré comme un fichier sous l'identifiant `index` : chaque chemin du coffre, son objet et sa taille en clair, puis les dossiers (vides compris). |
| `objets/<identifiant>.kyf` | Un fichier du coffre, chiffré sous son identifiant : 26 caractères de base32 de Crockford, tirés au hasard. |

Un fichier réécrit garde son objet, avec un nonce neuf. Un objet recopié sous un autre identifiant, ou un index venu d'un
autre coffre, ne s'ouvre pas : leurs données associées les lient à leur place. Chaque écriture passe par un fichier
temporaire voisin, puis un remplacement.

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
  mot de passe pour rouvrir l'en-tête écrit. Tout s'écrit d'abord dans un dossier temporaire voisin, qui ne prend le nom
  de la destination qu'une fois vérifié. Une conversion refusée ou coupée ne laisse jamais de coffre à moitié écrit.
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
  identifiant, un index d'un autre coffre ou une mauvaise clé sont refusés, et un chemin qui sortirait du coffre aussi.
- **La conversion** : le coffre de référence, chiffré puis déchiffré par le mot de passe et par la clé de récupération,
  revient à l'octet près, et la source n'a pas changé. Deux saisies différentes, un mot de passe trop court, une
  destination qui existe ou dans la source, un dossier qui n'est pas un coffre, un coffre ouvert dans kyfRev, un mauvais
  secret ou un objet altéré : rien n'est écrit, et aucun dossier temporaire ne reste.
