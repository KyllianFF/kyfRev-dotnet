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
