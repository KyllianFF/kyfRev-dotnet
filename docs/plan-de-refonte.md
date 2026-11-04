# kyfRev : audit de l'existant et plan de refonte en C# .NET

| | |
|---|---|
| **Date** | 31 octobre 2026 |
| **Statut** | Plan validé le 31 octobre 2026 : les 13 décisions du §7 sont arrêtées sur l'option recommandée (décision 2 : option C, §2.6). J1, J2, J3 et J4 validés et fusionnés le 31 octobre 2026 (J3 en cinq demandes de fusion : lecture fidèle, écriture des fiches, écritures complémentaires, sauvegardes et exports, corpus d'ancrage ; J4 en deux : couche Application et garde-fous, modularité et module Citations). J5 validé le 1er novembre 2026 sur ses captures (`docs/captures/j5`), en deux demandes de fusion : design system, coquille WPF et galerie de composants ; puis rouvert le même jour pour une nouvelle identité visuelle, « Veillée », que j'ai choisie parmi plusieurs pistes : J5.3 jetons, polices et composants Veillée ; J5.4 coquille adaptative (onglets sous 600 px, colonne d'icônes jusqu'à 1 399 px, barre latérale au-delà, jamais de défilement horizontal) ; J6.7 vue « Aujourd'hui » au nouveau style (résumé du jour, colonne latérale, révisions de la semaine) ; J6.8 fiche, passage, séance et accueil au nouveau style ; J6.9 aperçu et liste en deux sur grands écrans ; J5 revalidé le 1er novembre 2026 sur ces captures (`docs/captures/j5`, `docs/captures/j6`) ; J6.10 réponses aux rappels de consolidation (marquer un passage existant ; surligner un nouveau passage viendra avec J7). J6 validé le 1er novembre 2026, après mon essai sur un coffre généré (`tools/coffre-essai`), avec J6.1 tableau de bord et données des archives ; J6.2 emplacements, ouverture au démarrage et accueil ; J6.3 vue « Aujourd'hui » ; J6.4 fiche et passage en révision ; J6.5 séance d'étude ; journal de diagnostic ; J6.6 squelette de l'installateur (Inno Setup 6.7.3) ; scénarios d'acceptation T2 à T6, T9, T10, T11 et T18 (`tests/KyfRev.Acceptance.Tests`). Ajoutée ensuite : J6.11 vue « Archives », livrable de J6 qui manquait à la validation. Restent ouverts : l'assistant d'installation n'a pas encore été vu sur le poste (la CI l'installe et le désinstalle) ; la préversion `1.0.0-alpha.1` n'est pas publiée. Vues encore vides réparties le 1er novembre 2026 : Fiches, Définitions et début de Paramètres à J7, Citations à J9, le reste de Paramètres au fil des jalons. Jalon en cours : J7, avec J7.1 vue « Fiches » (liste, aperçu et création) J7.2 moteur de rendu `KyfRev.Rendering`, sans effet visible : texte affiché, positions et ré-ancrage identiques à la 0.x sur le corpus d'ancrage (T12) et sur 1 149 cas limites calculés par la 0.x ; et J7.3 lecture native d'une fiche et de son aperçu, en trois volets (plan, texte, passages), avec formules, tableaux, liens et images du coffre. J7.4 surlignage dans le texte : passages et définitions replacés à l'ouverture, créés par le clic droit ou la touche H, et T5 complété (un passage surligné pendant un rappel compte comme à revoir). J7.5 écriture d'une fiche : le texte mis en forme se modifie à sa place (ProseMirror dans WebView2, durci), un bouton affiche le Markdown natif à gauche et son rendu à droite, ruban Accueil et Insertion, enregistrement automatique, conflit avec le disque sans perte (T14, sans l'écran de comparaison), `[[`, images et correcteur de Windows ; T17 couvert. J7.6 le reste de l'écriture et de la lecture : format étendu (couleur, taille, alignement, tâches), galerie des schémas, graphiques et SmartArt dessinés, texte à trous, versions et comparaison, vues Définitions et Paramètres (première partie), écoute à voix haute, code coloré, images SVG, changement de place d'un passage et corbeille du volet. Reste à valider à J7 : un essai à la main, écran déverrouillé (son des voix, Narrateur, souris). Les demandes de fusion 47 à 56 sont entrées sur `main` sans CI le 2 novembre, GitHub refusant de démarrer ses jobs ; je les ai vérifiées localement, puis dans Windows Sandbox (`tools/verification-sandbox.ps1`), qui refait les étapes de la CI. Le dépôt est public depuis ce jour, et la CI est de nouveau verte sur `main` (`f707bcd`). Après avoir essayé l'installateur de J7, le 3 novembre 2026, j'ai fait passer avant J8 des livrables de J9 et J10 : statistiques, santé du coffre, vue « Paramètres » en onglets et module Citations sont fusionnés ou en cours de fusion ; schémas réseau, extensions et clic droit restent à faire. |
| **Périmètre audité** | Dépôt `KyllianFF/kyfRev` à `87f291c` (v0.8.0, 133 commits) ; application installée `C:\Program Files\kyfRev` (0.7.0) ; coffre `Documents\kyfRev\Coffre` et ses sauvegardes. |
| **Méthode** | Lecture intégrale du cœur métier (`crates/noyau`), lecture du dossier de conception (64 décisions D1 à D64), analyse du crate `coffre` (persistance), de `src-tauri` (commandes, extensions, sécurité, livraison) et de l'interface Svelte. Aucune modification de l'existant. |

---

## Synthèse

**Ce qu'est kyfRev aujourd'hui.** Une application de bureau Windows (Tauri 2 : Rust + Svelte dans WebView2) d'environ 73 000 lignes. Elle gère des fiches de révision en Markdown rangées dans un **coffre** (dossier ordinaire). Elle les fait réviser selon la **méthode des J** : J0, J1, J3, J7, J14, J30, chaque Jn tombant n jours après la révision réellement faite. Puis elle les archive et les rappelle au hasard pour consolidation. Autour de ce cœur se sont greffés :
- des passages annotés et des définitions, révisés eux aussi par la méthode des J ;
- deux éditeurs : un visuel (ProseMirror) et un Markdown (CodeMirror) ;
- un éditeur de schémas réseau d'environ 15 000 lignes ;
- les citations du jour (Wikiquote) ;
- les statistiques, les examens, la recherche globale ;
- le partage et l'import Obsidian ;
- les extensions isolées, avec un catalogue.

**Constats principaux**

1. **Le cœur métier est d'excellente qualité et se porte presque à l'identique.** Le crate `noyau` (1 300 lignes, sans aucune entrée-sortie) est sans état caché et couvert par des scénarios datés et des tests de propriétés. Sa traduction en C# est directe (`DateOnly`, records, unions fermées).
2. **Le coffre est lisible nativement depuis C#.** Il est fait de fichiers Markdown et JSON ouverts, sans chiffrement ni base de données. Il n'a **pas besoin d'outil de conversion** pour être repris : la nouvelle application peut le lire et l'écrire sur place, au format 1. Il y a toutefois 19 pièges de fidélité (ordre JSON, dates à 7 ou 9 décimales, YAML édité ligne à ligne, champs inconnus perdus…). Ils imposent une couche de compatibilité testée sur un corpus de référence.
3. **Le vrai risque de perte de données n'est pas dans le JSON, mais dans les annotations.** Leurs positions sont calculées sur le texte produit par le moteur de rendu actuel (markdown-it). Un autre moteur ferait glisser les ancres, puis les réécrirait à l'ouverture. Ce point guide le choix de la surface « document » (§2.6).
4. **Le coffre réel actuel est presque vide** (créé le 31 octobre 2026 : 0 fiche, 1 citation, 1 sauvegarde). Le risque immédiat est donc faible. Le plan reste conçu pour un coffre plein, puisque l'application actuelle continuera de servir pendant la refonte.
5. **Dette à ne pas reporter :**
   - **Sécurité :** catalogue d'extensions non signé ; fuite possible de données par une extension ; contrôle de permission fait seulement dans l'interface.
   - **Données :** champs inconnus perdus à la réécriture ; numéro de `format` jamais contrôlé.
   - **Accessibilité :** bordures à 1,29:1, aucun mode contraste élevé.
   - **Gouvernance :** aucune CI sur les demandes de fusion ; version recopiée à la main à 3 endroits ; une partie de l'historique n'est pas signée KyllianFF.

**Recommandations**

| Sujet | Recommandation |
|---|---|
| Interface | **WPF sur .NET 10 (LTS)**, design system propre aux couleurs de kyfRev. MAUI est déconseillé. Avalonia reste l'alternative si le multiplateforme devient un objectif (§2.5). |
| Surface « document » | **Lecture, révision et annotations 100 % natives dès la 1.0** : l'ancrage est calculé en C# et validé par un corpus de référence. Seuls l'éditeur visuel et un moteur web caché (Mermaid, schémas, code des extensions) restent dans WebView2 (§2.6). |
| Architecture | Clean Architecture : `Domain` (le noyau des J), `Application` (cas d'usage), `Infrastructure` (coffre, chiffrement, Windows, réseau), `Presentation` (WPF). Modules internes branchés par injection de dépendances ; extensions tierces isolées (§2.7). |
| Coffre | Lecture et écriture natives du format 1, compatibles dans les deux sens avec l'application actuelle pendant la transition. Coffre chiffré en option (AES-256-GCM, Argon2id), avec un outil de conversion vérifié (§4). |
| Dépôt | `kyfRev-dotnet` (§3). |
| Installateur | **Inno Setup 6** : installation par utilisateur (`%LocalAppData%\Programs\kyfRev`) par défaut, option « tous les utilisateurs » (`Program Files`), plus une version portable (§5.5). |
| Versions | `1.0.0-alpha.N`, puis `1.0.0` à la parité fonctionnelle avec la 0.8.0 (§5.3). |

Les **décisions** sont regroupées au §7.

---

## 1. Analyse fonctionnelle et cartographie de la logique métier

### 1.1 Inventaire fonctionnel

| Domaine | Fonctions | Décisions |
|---|---|---|
| **Révision (cœur)** | Calendrier des J par élément ; valider (y compris antidaté ou en avance), reporter, recommencer à J0, archiver ; séance du jour enchaînée ; rappel actif (texte à trous, passage caché, carte mémoire) | D2, D3, D5, D7, D29, D32 |
| **Éléments révisables** | Fiche entière ; passage annoté (surlignage ± commentaire) ; définition (terme + explication). Chacun a son propre calendrier. | D3, D10, D18 |
| **Archives et consolidation** | Archivage en fin de cycle ou manuel ; rappels tirés au hasard après 2 mois calendaires ; repos progressif 2 → 4 → 8 → 12 mois | D5, D11, §7.5 |
| **Tableau de bord** | En retard, Aujourd'hui, Rappels, À venir (7 j) ; charge des 30 prochains jours (échéances connues et projetées, déplacement d'une échéance) ; examens par dossier avec « Resserrer » | D27, D36 |
| **Fiches et organisation** | Arborescence de dossiers, étiquettes (`etiquettes` / `tags`), glisser-déposer, renommer ou déplacer avec réécriture des liens entrants, corbeille restaurable jamais purgée, modèles de fiche | D9, D19, D20, D25, D35 |
| **Écriture** | Éditeur visuel (aller-retour Markdown fidèle à l'octet) et éditeur Markdown ; liens `[[` ; images ; formules ; galerie Mermaid ; versions et conflits ; enregistrement automatique | D13, D15, D16, D30 |
| **Lecture** | Rendu Markdown, KaTeX, Mermaid isolé ; plan ; rétroliens ; aperçu au survol ; mode concentration ; confort de lecture ; lecture à voix haute | D34, D37, D38 |
| **Recherche** | Dans les fiches (tous les mots, sans casse ni accents) ; recherche globale Ctrl+K | D26, D28 |
| **Statistiques** | Ponctualité, série, carte de chaleur sur 52 semaines, par mois et par dossier ; tout est recalculé depuis l'historique, rien n'est stocké | D33 |
| **Citations** (module) | Fichiers `.md` ; citation du jour depuis Wikiquote, stable dans la journée | D14, D24 |
| **Schémas réseau** (module) | Éditeur de topologie (équipements, ports, VLAN, câbles routés, plan IP, exports SVG, PNG et JSON) dans `schemas/*.json`, cité par les fiches | D39 à D55 |
| **Échanges** | Partage d'une fiche (HTML autonome, PDF, Markdown, zip, texte) ; exports CSV, Markdown et zip ; import d'un dossier Obsidian | D22, D58 |
| **Coffre** | Sauvegarde quotidienne zip vérifiée, rotation 14 j / 8 sem. / 24 mois, restauration dans un nouveau dossier ; panneau Santé (rien n'est réparé en silence) ; verrou entre instances | D11, §9 |
| **Extensions** | Blocs et vues ajoutés par des extensions HTML/JS isolées ; catalogue vérifié par empreintes SHA-256 ; jeton GitHub chiffré par DPAPI | D61 à D64 |
| **Distribution** | Version portable (`MesRévisions\`) et installeur NSIS ; signature de code facultative ; recherche de mise à jour à la demande | D23, D59 |

### 1.2 L'algorithme des J (source : `crates/noyau`)

**Séquence.** `S = [s₀, s₁, …, sₙ]` avec `s₀ = 0`, au moins 2 valeurs, strictement croissante, chaque valeur ≤ 3650. Par défaut : `[0, 1, 3, 7, 14, 30]`.

- La séquence est **copiée dans chaque cycle** à son démarrage : la modifier dans les réglages ne touche pas les cycles en cours.
- Un cycle ponctuel dure `Σ sᵢ` jours, soit 55 jours par défaut.

**Règle de calcul (décisions D2 et D7).**

> **Échéance de l'étape suivante = date de la révision réellement validée + s(étape suivante).**

Un retard décale donc tout le reste du calendrier. L'écart entre deux révisions vaut toujours exactement le nombre du J.

**Opérations et transitions** (chacune vérifie tout avant d'agir ; une opération refusée laisse l'état strictement inchangé) :

| Opération | Préconditions | Effet |
|---|---|---|
| **Démarrer** (création, adoption d'un `.md`, rappel « à revoir entièrement », redémarrage) | — | Nouveau cycle k : `etape = 1`, `derniereRevision = d`, `echeance = d + s₁`. Événement `revision` J0 avec `origine`. |
| **Valider** au jour `v` (aujourd'hui par défaut, ou antidaté) | actif ; `v ≤ aujourd'hui` ; `v > derniereRevision` (une seule révision par jour) | Événement `revision` (`prevuLe` = échéance, `retardJours = v − échéance`, négatif en avance). Si c'était la dernière étape : archivage `fin_de_cycle`, `cyclesTermines + 1`. Sinon : `etape + 1`, `echeance = v + s[etape]`. |
| **Reporter** vers `r` | actif ; `r > aujourd'hui` ; `r ≠ échéance` | `echeance = r` ; événement `report`. Ce n'est pas une révision. |
| **Recommencer** | actif ou archivé | Si actif : `interruption` (`cyclesInterrompus + 1`) ; si archivé : `reactivation`. Puis nouveau J0 (`redemarrage`) avec la séquence **actuelle** des réglages. |
| **Archiver manuellement** | actif | `interruption`, puis `archivage` `manuelle`. |
| **Tirer pour un rappel** | archivé, inclus, pas de rappel en attente, `eligibleLe ≤ jour` | `rappelTireLe = jour`. |
| **Traiter un rappel** | un rappel en attente | voir §1.3. |
| **Inclure ou exclure des rappels** | archivé | Bascule. Exclure efface le rappel en attente. Événement `inclusion_rappels`. |

**Dates.**
- Les échéances sont des **dates civiles locales** (`AAAA-MM-JJ`) : l'heure d'été n'a aucun effet.
- Chaque événement porte `le` (instant UTC) et `jour` (date locale où l'action compte).
- Les mois sont **calendaires**, le jour étant ramené au dernier jour du mois s'il n'existe pas : 31/12 + 2 mois = 28/02, et 29/02 les années bissextiles.
- `DateOnly.AddMonths` de .NET a exactement cette sémantique.

**Invariants**, contrôlés à chaque relecture du disque (`Suivi::verifier`) :
- l'historique ne fait que grandir et commence par un J0 ;
- les compteurs se recalculent depuis l'historique ;
- `commencés = terminés + interrompus + (1 si actif)` ;
- pour un élément actif : `1 ≤ etape ≤ n` et `demarreLe ≤ derniereRevision < echeance` ;
- pour un élément archivé : `eligibleLe ≥ archiveeLe`.

Un suivi qui viole un invariant est déclaré **illisible**, signalé, et **jamais réécrit**.

**Exemple de référence** (scénarios T2, T3, T4 et T10, à reproduire tels quels en C#). Fiche créée le vendredi 9 octobre 2026, révisée ponctuellement :

| | J1 | J3 | J7 | J14 | J30 → archivée |
|---|---|---|---|---|---|
| Ponctuel | 10/10 | 13/10 | 20/10 | 03/11 | **03/12/2026** |
| J3 fait le 18/10 | 10/10 | (18/10) | **25/10** | 08/11 | 08/12 |

La suite pour la ligne « Ponctuel » :
- éligible au rappel le 03/02/2027 ;
- tirée le 08/02/2027 et déclarée « connue » : éligible le **08/06/2027** (repos de 4 mois) ;
- « connue » de nouveau : éligible le **08/02/2028** (repos de 8 mois) ;
- variante, « certains points à revoir » le 08/02/2027 : éligible le **08/04/2027**.

### 1.3 Cycle de vie des révisions

```
   créer / adopter (= J0)
          │
          ▼
     ┌──────────┐ valider la dernière étape  ┌──────────────────────┐
     │  Active  │───────────────────────────▶│  Archivée (au repos) │
     └──────────┘   ou archiver (manuel)     └──────────┬───────────┘
        ▲   ▲                                           │ eligibleLe atteinte
        │   │ recommencer (J0)                          ▼
        │   └───────────────────────────────  Archivée (éligible)
        │                                               │ tirage du jour
        │ « à revoir entièrement »                      ▼
        └────────────────────────────────────  Rappel en attente
                    « connue » : repos niveau + 1 ──┐   │ « certains points » :
                                                    ▼   ▼ repos niveau 0 + passages à J0
                                            Archivée (au repos)
```

**Résultats d'un rappel.** Le résultat est inscrit dans l'historique (`evt: rappel`). Fermer sans choisir n'enregistre rien.

| Choix | Effet |
|---|---|
| **Fiche connue** | `niveauRepos = min(niveau + 1, dernier)`, `eligibleLe = aujourd'hui + repos[niveau]` mois ; la fiche reste archivée. |
| **Certains points à revoir** | Il faut **au moins un passage marqué**. Niveau remis à 0, `eligibleLe = aujourd'hui + repos[0]`. Chaque passage marqué démarre (ou reprend) à J0, indépendamment de la fiche. |
| **À revoir entièrement** | Événement `reactivation`, nouveau cycle J0 (`origine: rappel`), J1 le lendemain. |

**Archivage** :
- d'une **fiche** : `eligibleLe = archiveeLe + delaiMinimalMois` (2) ;
- d'un **passage** ou d'une **définition** : jamais de rappel (`inclureDansRappels = false`, `eligibleLe = jour`). Le surlignage reste visible.

**Annotations** (passage ou définition). Elles sont stockées hors du `.md`, qui n'est **jamais modifié** par une annotation.
- Le texte du passage et le commentaire sont **copiés** dans l'annotation : elle reste révisable même si la fiche disparaît.
- **Ancrage** selon le modèle W3C Web Annotation : citation exacte, avec 32 caractères de contexte de chaque côté, et position en unités UTF-16 dans le *texte rendu*.
- **Ré-ancrage** à chaque ouverture, dans cet ordre :
  1. position intacte : `ancree` ;
  2. occurrence exacte unique, ou départagée par le contexte : `ancree` ;
  3. sinon recherche approchée (≤ 1/3 d'écart) : `a_verifier`, avec un candidat proposé, **jamais adopté seul** ;
  4. sinon : `introuvable`.
- **Modifier le passage** demande « Conserver la progression » ou « Recommencer à J0 ». Supprimer une annotation la met à la corbeille ; elle est restaurable et jamais purgée.

**Règle « rien ne s'efface ».** Aucune commande ne supprime une fiche. « Supprimer » range la fiche, ses ressources, son suivi et ses annotations dans une corbeille restaurable, jamais purgée. La seule suppression automatique de toute l'application est la **rotation des sauvegardes** zip, hors du coffre. Des tests vérifient dans le code source qu'aucun autre appel d'effacement n'existe ; ce garde-fou est à transposer en C# (§2.7).

### 1.4 Tirage quotidien des rappels de consolidation

Le tirage a lieu **une fois par jour**, à la première ouverture ; il est inscrit dans `.suivi/rappels.json` et n'est jamais refait.

1. `places = maxParJour (2) − rappels déjà en attente`. Les rappels non traités occupent une place : ils ne s'accumulent pas après une absence.
2. **Éligibles** : fiches archivées, incluses dans les rappels, sans rappel en attente, avec `eligibleLe ≤ aujourd'hui`.
3. **Priorité anti-oubli** : les fiches éligibles depuis au moins `attenteMaxJours` (30) passent d'abord, de la plus ancienne à la plus récente. Celles qui restent faute de place sont **signalées** sur le tableau de bord (`prioritairesNonServis`).
4. **Hasard uniforme** pour les places restantes : mélange de Fisher-Yates partiel, avec un générateur ChaCha8 initialisé par la graine FNV-1a 64 bits de `"<id du coffre>|AAAA-MM-JJ"`, et un tirage sans biais par rejet. Le résultat ne dépend pas de l'ordre des candidats.

**Conséquences pour le portage :**
- `coffre.json.id` ne doit **jamais** être régénéré.
- Reproduire à l'identique ChaCha8 et l'initialisation `seed_from_u64` de `rand_core` n'est pas indispensable, puisque le tirage du jour est persisté. C'est en revanche **recommandé** : on pourra alors comparer automatiquement les deux implémentations (scénario T18), avec des vecteurs de test générés depuis la version Rust.

### 1.5 Autres règles métier notables

- **Charge à 30 jours** : les étapes projetées supposent que chaque révision sera faite le jour de son échéance (`etapes_projetees`).
- **Examens** : un seul par dossier, enregistré dans `coffre.json`. « Resserrer » ramène les échéances avant la date d'examen au moyen d'événements `report` ordinaires.
- **Statistiques** : calculées à l'ouverture depuis les historiques ; les J0 ne comptent pas comme activité.
- **Citation du jour** : celle trouvée sur Wikiquote le jour même si elle existe encore ; sinon une citation locale tirée par la même graine FNV-1a.
- **Analyse du coffre à l'ouverture** :
  - la fiche est rattachée à son suivi **par identifiant** (`id: f_<ULID>` dans l'en-tête YAML), jamais par son chemin. Un fichier déplacé dans l'Explorateur est donc retrouvé ;
  - l'analyse réécrit le suivi si le chemin, l'empreinte, le titre ou les étiquettes ont changé ;
  - elle repère les doublons, les fichiers non suivis et les fiches manquantes, qui sont signalés dans Santé.
- **Journal d'opération** (`operation.json`) : un renommage ou un déplacement touche plusieurs fichiers ; il est journalisé, puis rejoué de façon idempotente à l'ouverture s'il a été interrompu.

### 1.6 Mécanisme d'extensions

Il existe **deux niveaux**.

**Modules internes (D61).** Citations et Schémas réseau sont compilés dans l'application. Ils sont activés pour chaque coffre dans `coffre.json` (`citations.active`, `modules.schemas`). Les désactiver masque le module sans rien supprimer.

**Extensions tierces (D62 à D64)**

- **Manifeste `extension.json`**, aux champs fermés :
  - `id` (de 2 à 40 caractères, `[a-z0-9-]`), `nom`, `version`, `entree` ;
  - `permissions`, parmi `fiches:lecture` et `stockage` ;
  - `blocs` (10 au plus ; `mermaid`, `reseau` et `katex` sont réservés) ;
  - `vue`.
- **Apport** : de nouveaux types de blocs de code dans les fiches (```` ```quiz ````) et une vue dans la barre latérale.
- **Exécution** dans un `iframe sandbox="allow-scripts"` d'origine opaque, avec une CSP `default-src 'none'; connect-src 'none'` : ni réseau, ni accès au coffre ou à l'application.
- **Protocole `postMessage`** en 10 messages (`pret`, `charger`, `rendre-bloc`, `rendre-vue`, `action`, `requete`, `reponse`, `resultat`, `rafraichir`, `erreur`), avec des délais de 10 s.
- **API exposée** : `kyfrev.bloc`, `vue`, `action`, `fiches.lister` / `lire` (lecture seule), `stockage.lire` / `ecrire` (JSON ≤ 1 Mo dans `.suivi/extensions/<id>.json`).
  - Le HTML produit est nettoyé par DOMPurify.
  - Aucune écriture de fiche n'est possible.
- **Installation** :
  - depuis un dossier (200 fichiers et 10 Mo au plus, types autorisés seulement) ;
  - ou depuis le catalogue du dépôt : chaque fichier est vérifié par sa taille et son SHA-256. Le catalogue est lu avec un jeton GitHub fine-grained, chiffré par DPAPI.
- **Stockage** :
  - le code est rangé **sur l'ordinateur**, jamais dans le coffre ;
  - les anciennes versions sont rangées dans `.anciennes`, jamais effacées ;
  - les données de l'extension sont rangées **dans le coffre**.
- **Quatre extensions publiées** : Quiz, Chronologie, Vocabulaire, Bilan. Ce sont toutes des fonctions pures « texte → HTML » qui n'utilisent ni le DOM ni le réseau.

**Faiblesses relevées** (à corriger dans la refonte, §2.3) :
- un bloc d'extension peut produire un lien `data-externe` qui exfiltre le contenu des fiches vers une URL ;
- `style` est autorisé, ce qui permet de faux dialogues ;
- les réponses peuvent être usurpées d'un cadre à l'autre ;
- `fiches:lecture` n'est contrôlé que dans l'interface ;
- le protocole `extension:` ne pose ni CSP ni `sandbox` ;
- le catalogue est lu sur la branche `main`, sans signature.

---

## 2. Diagnostic technique

### 2.1 Architecture actuelle

```
┌──────────────────────── Interface Svelte 5 (WebView2), ~44 500 lignes ───────────────┐
│  écrans · éditeurs ProseMirror/CodeMirror · rendu markdown-it/KaTeX/Mermaid ·        │
│  ancrage des annotations · éditeur de topologie (~15 000 l.) · extensions (iframes)  │
└──────────────────────────────┬────────────────────────────────────────────────────────┘
                               │ 97 commandes IPC (liste fermée, modèle d'isolation Tauri)
┌──────────────────────────────┴──── src-tauri (câblage, réseau, Windows) ──────────────┐
│ emplacements portable/installé · Wikiquote · GitHub · DPAPI · orthographe COM · NSIS │
└──────────────────────────────┬────────────────────────────────────────────────────────┘
┌──────────────────────────────┴──── crate coffre (persistance), ~13 000 lignes ────────┐
│ écriture atomique · verrou · journal · versions · corbeille · sauvegardes · import…  │
└──────────────────────────────┬────────────────────────────────────────────────────────┘
┌──────────────────────────────┴──── crate noyau (métier pur), ~1 300 l. + tests ───────┐
│ séquence · suivi · cycles · événements · rappels · tirage                            │
└───────────────────────────────────────────────────────────────────────────────────────┘
```

**Points forts à préserver**
- La séparation est stricte : le noyau est pur ; l'interface n'a aucun accès direct au disque ; la liste de commandes est fermée.
- Écritures atomiques avec réessais, contrôle d'écriture optimiste par empreinte, journal d'opération, et « rien ne s'efface » vérifié par des tests.
- Les tests (`cargo test`, `proptest`, Vitest, Playwright sur la vraie application) et le dossier de conception forment un **cahier de recette réutilisable**.
- L'interface est pensée pour l'étude : une action principale par écran, rappel actif, retours explicites, clavier d'abord.

### 2.2 Dette et défauts relevés

| Gravité | Domaine | Constat | Traitement dans la refonte |
|---|---|---|---|
| Moyenne | Sécurité | Exfiltration par un `data-externe` produit par une extension ; `style` autorisé dans le HTML d'extension | HTML d'extension converti en éléments natifs depuis une liste blanche stricte : ni `style`, ni lien externe produit par une extension |
| Moyenne | Sécurité | Catalogue sans signature, lu sur `main` (branche mutable) | Catalogue **signé Ed25519**, clé publique dans l'exécutable, lu sur une étiquette de version |
| Moyenne | Sécurité | Jeton DPAPI sans entropie, transmis une fois en clair à l'interface, dépôt codé en dur | Entropie propre à l'application ; saisie dans un champ natif (jamais dans le web) ; dépôt configurable |
| Faible | Sécurité | `fiches:lecture` contrôlé seulement dans l'interface ; réponses usurpables d'un cadre à l'autre ; pas de CSP sur `extension:` | Contrôle par l'hôte C#, jeton de session par cadre, CSP et `sandbox` sur toutes les réponses |
| Faible | Sécurité | Chemins absolus acceptés de l'interface (export, import) | L'hôte ouvre lui-même la boîte de dialogue : aucun chemin ne vient de la couche web |
| Faible | Sécurité | Journal contenant le nom d'utilisateur Windows ; rotation seulement au démarrage | Masquage de `%USERPROFILE%`, rotation pendant l'exécution |
| Faible | Sécurité | Mise à jour sans contrôle d'intégrité ; WebView2 téléchargé par l'installeur sans vérification de signature | Manifeste de version signé ; vérification Authenticode du programme d'amorçage |
| Moyenne | Données | Champs JSON inconnus **perdus** à la réécriture, sauf au premier niveau de `coffre.json` | Champs inconnus conservés à tous les niveaux |
| Moyenne | Données | `format` jamais contrôlé : une version future du coffre serait écrasée silencieusement | Contrôle de version : un coffre plus récent que l'application s'ouvre en lecture seule |
| Faible | Données | Une liste `tags: [2024, bio]` rend la fiche illisible ; un titre de 120 caractères CJK dépasse 255 octets ; `recreer_suivi` échoue si le JSON illisible existe ; la restauration d'une fiche peut prendre la copie de la corbeille ; le renommage en `.corrompu` décrit par la conception n'existe pas | Corrigé, avec un test de non-régression pour chacun |
| Moyenne | Accessibilité | Bordures à 1,29:1 (WCAG 1.4.11 non respecté) ; aucun mode contraste élevé ; onglets sans panneau ; radios sans navigation aux flèches | Jetons recalculés, thème contraste élevé natif, audit Accessibility Insights |
| Moyenne | UX | Séance sans progression, sans « passer » ni raccourci « suivant », fin silencieuse ; liste à réviser placée sous la citation sous 1 600 px ; échelle typographique et espacements ad hoc | Voir le jalon J6 (§6) |
| Moyenne | Livraison | Aucune CI sur les demandes de fusion ; version recopiée à la main à 3 endroits ; l'essai de bout en bout n'est pas en CI | CI sur chaque push et chaque demande de fusion ; source de version unique |
| — | Hygiène | Une partie de l'historique a un auteur autre que KyllianFF, et des trailers exposent des adresses de session ; dossiers propres à des outils versionnés ; chemins personnels dans la documentation | Nouveau dépôt à l'historique vierge, contrôle d'identité en CI (§5.2) |

### 2.3 Ce qui est portable, à réécrire ou à abandonner

**Strictement portable** (logique traduite presque ligne à ligne, tests compris)

| Élément | Source | Remarques |
|---|---|---|
| Algorithme des J, rappels, tirage, dates | `crates/noyau` (+ `tests/scenarios.rs`, `proptest`) | `DateOnly`, `TimeProvider`, unions fermées ; ChaCha8 et FNV-1a portés pour comparer les deux implémentations |
| Règles de chemins et de nommage | `coffre/chemins.rs` | `CheminRelatif`, noms réservés Windows, 255 octets UTF-8, suffixes `(n)` |
| En-tête YAML (édition ligne à ligne, jamais re-sérialisé) | `coffre/entete.rs` | BOM et CRLF conservés ; clôture `...` acceptée |
| Liens Markdown (repérage, encodage, réécriture) | `coffre/liens.rs` | Indispensable aux renommages |
| Journal d'opération, corbeille, versions, conflits | `operation.rs`, `corbeille.rs`, `edition.rs` | Rejeu idempotent |
| Sauvegardes, rotation, restauration (protections contre le zip-slip et les bombes) | `sauvegardes.rs` | Semaine ISO, périodes réellement présentes |
| Import Obsidian, exports CSV (anti-injection), partage zip et HTML | `import.rs`, `exports.rs`, `partage.rs` | |
| Citations, extraction Wikiquote, examens, statistiques, santé | crates `coffre` | |
| Contrats d'extension (manifeste, protocole, catalogue format 1) | `src-tauri/extensions.rs`, `catalogue.rs` | Compatibles, avec les durcissements |
| `portable.json`, `installation.json`, `config-locale.json`, `jeton-github.bin` | `emplacements.rs`, `github.rs` | `ProtectedData.Unprotect` relit le jeton existant |
| Logique pure du module topologie (~3 500 l. TypeScript) | `src/lib/topologie` | Si le module passe en natif (§6, J10) |
| Identité visuelle : 3 gammes OKLCH, rôles, contrastes | Conception §12, `theme.css` | Rôles et exigences de contraste repris ; couleurs et polices remplacées par l'identité « Veillée », choisie en novembre 2026 |

**À réécrire**
- Câblage Tauri et 97 commandes IPC : cas d'usage de la couche Application, appelés directement par les ViewModels.
- Toute l'interface Svelte : WPF + MVVM (CommunityToolkit.Mvvm), avec un design system propre.
- Surface de lecture, de révision et d'annotation : native (analyse Markdown vers FlowDocument), avec la projection du « texte rendu » réécrite en C# (§2.6).
- Clients réseau, verrou, écriture atomique, correcteur COM, voix, instance unique : équivalents .NET.
- Installeur NSIS : Inno Setup. Workflows CI.

**À abandonner**
- Spécifique à Tauri : `permissions/autogenerated` (97 fichiers), `capabilities/`, `isolation/`, `build.rs`, le test `coherence.rs` (remplacé par un test d'architecture par réflexion), `.cargo/`, `rust-toolchain.toml`, `deny.toml` (remplacé par l'audit NuGet et une politique de licences), la relance pour purger les variables `WEBVIEW2_*` (désormais faite dans le processus).
- `gestes.ts` (blocage de F5 et Ctrl+R, inutile hors navigateur), dépendance morte `minisearch`, faux backend `demo.ts` (son **contenu** devient un coffre d'exemple pour les tests).
- L'historique git (le nouveau dépôt repart de zéro), les dossiers de configuration d'outils et les chemins personnels.
- **Réutiliser les crates Rust par FFI depuis C# : écarté.** Cela imposerait une double chaîne d'outils, contraire à l'objectif. En revanche, l'implémentation Rust sert d'**oracle de test** : des vecteurs de référence sont générés une fois depuis l'ancien dépôt, puis versionnés comme fixtures synthétiques.

### 2.4 Plateforme cible

- **.NET 10 LTS** (support jusqu'en novembre 2028), SDK figé par `global.json`.
- Cible `net10.0-windows10.0.19041.0`, pour les API Windows : voix, correcteur, barre de titre.
- Windows 11 est la cible principale.
- Windows 10 22H2 reste techniquement pris en charge, mais il n'est plus maintenu par Microsoft pour le grand public depuis octobre 2026 : il est testé au mieux.

### 2.5 Choix de la technologie d'interface

| Critère | **WPF (.NET 10)** | **Avalonia UI 11+** | **.NET MAUI** |
|---|---|---|---|
| Adéquation à une application de bureau dense, au clavier | Excellente : maturité, commandes, raccourcis, menus, arbres, `DataGrid` | Très bonne | **Faible** : conçu d'abord pour le mobile, contrôles de bureau limités |
| WebView2 (rendu Markdown, Mermaid, extensions) | **Contrôle officiel Microsoft** | Pas dans le cœur libre (contrôles tiers ou offre commerciale) | Oui, via WinUI |
| Correcteur d'orthographe Windows | **Natif** (`SpellCheck.IsEnabled`) | Absent (interop COM à écrire) | Natif via WinUI |
| Accessibilité (UI Automation) | **La plus mature** | Correcte, plus récente | Correcte (WinUI) |
| Style « sur mesure », clair/sombre/contraste élevé | Très bon (thème Fluent natif depuis .NET 9, styles et templates complets) | Excellent (styles de type CSS) | Limité |
| Tests d'interface automatisés | FlaUI (UIA3) | **Avalonia.Headless** (meilleur) | Faible |
| Multiplateforme | Non (Windows seulement) | **Oui** | Oui (mobile surtout) |
| Empaquetage | Simple (exe autonome, Inno Setup) | Simple | Complexe (MSIX par défaut sous Windows) |

**Recommandation : WPF.**
- La décision D1 cible Windows seulement, et l'application dépend déjà de Windows (DPAPI, correcteur, voix, WebView2) : le multiplateforme d'Avalonia n'apporterait rien aujourd'hui.
- En revanche, son absence de WebView2 libre et de correcteur natif coûterait cher.
- **MAUI est écarté** : expérience de bureau limitée, empaquetage MSIX contraignant.
- **WinUI 3**, hors de la liste demandée, a été examiné. Ses contrôles Fluent sont plus modernes, mais son outillage est moins mûr (pas de concepteur, déploiement plus fragile) ; WPF reste préférable.
- **Garde-fou** : le coffre et les couches Domain, Application et Infrastructure.Vault ne dépendent pas de WPF. Un passage futur à Avalonia ne toucherait que la présentation.

### 2.6 Surface « document » (lecture, révision, annotation, édition)

**Contraintes**
- **Ancres** : les positions des annotations sont comptées dans le « texte rendu » : textes des blocs, un saut de ligne entre deux blocs, formules remplacées par leur source TeX, diagrammes exclus.
  - Ce qui compte n'est pas le moteur d'affichage, mais **cette projection**. Si C# la calcule à l'identique depuis le Markdown, les annotations existantes restent ancrées.
  - À défaut, la recherche par citation exacte les rattache presque toujours ; seuls des cas limites passeraient en `a_verifier`.
- **Mermaid** n'a aucun équivalent natif.
- L'éditeur visuel fidèle à l'octet coûterait **30 à 40 jours ou plus** à réécrire en natif.
- **Exposition réelle aujourd'hui** : mon coffre ne contient **aucune annotation**.

| | A. Hybride | B. 100 % natif, éditeur compris | **C. Lecture native, éditeur visuel en WebView2 (recommandée)** |
|---|---|---|---|
| Lecture, révision, texte à trous, annotations | Module web | **Natif** | **Natif** |
| Calcul de l'ancrage | JavaScript (existant) | **C#** | **C#** |
| Éditeur | ProseMirror et CodeMirror dans WebView2 | Éditeur Markdown natif avec aperçu ; **pas d'éditeur visuel en 1.0** | Éditeur Markdown **natif** (AvalonEdit) ; éditeur **visuel** ProseMirror dans WebView2 |
| Mermaid, schémas, code des extensions | Module web | Moteur web caché | Moteur web caché |
| Travail à refaire plus tard | Toute la lecture, si l'on vise le natif | Aucun | Aucun d'imposé : remplacer l'éditeur visuel reste facultatif |
| Coût en 1.0 | Le plus faible | Le plus élevé (+30 à 40 j par rapport à C) | Intermédiaire (+15 à 25 j par rapport à A) |

**Pourquoi l'option C**
- **Une seule implémentation de l'ancrage**, en C# et testée, sans migration ultérieure. Toutes les interactions de lecture (sélection, surlignage, texte à trous, panneau d'annotations) sont natives.
- **Accessibilité et thème natifs** : un lecteur d'écran lit le document par UI Automation ; contraste élevé de Windows ; surlignage du bloc lu à voix haute.
- **Surface d'attaque réduite** : la lecture n'exécute jamais de contenu web. Une fiche piégée (scénario T16) est neutralisée par construction.
- **L'éditeur visuel ne touche pas aux ancres** : il n'écrit que du Markdown, et les ancres sont recalculées en C# à la lecture suivante. Le garder dans WebView2 ne crée donc aucune dette de compatibilité.
- **WebView2 reste nécessaire de toute façon** pour Mermaid, les schémas (décision 3) et le code des extensions (décision 4). Un « 100 % natif » au sens strict n'est pas atteignable sans y renoncer.

**Mise en œuvre de la lecture native**
- **Analyse** : markdown-it et ses extensions (notes, `$…$`, `<u>`, `<sup>`, `<sub>`, liens automatiques), repris en C# règle par règle dans `KyfRev.Rendering`. Markdig, d'abord retenu, ne pouvait pas se comporter comme eux : il analyse les tableaux à l'intérieur des paragraphes, sans pouvoir lire la ligne suivante, si bien qu'un tableau qui interrompt un paragraphe ou un `---` qui le suit donnent un autre texte, donc d'autres positions (constat de J7). Reprendre l'analyse donne l'identité par construction, et retire une dépendance.
- **Affichage** : rendu en `FlowDocument`, qui gère nativement la sélection sur plusieurs blocs ; `TextPointer` donne les positions dans la projection. Tableaux à la largeur du texte, sans défilement horizontal ; code coloré (TextMateSharp) avec bouton « Copier » ; images du coffre seulement. Le document se construit par morceaux : le début paraît tout de suite, la suite s'ajoute juste après.
- **Formules** : XAML-Math en natif, avec repli sur le moteur web caché pour les commandes qu'il ne couvre pas.
- **Diagrammes Mermaid et schémas** : rendus en SVG par le moteur web caché (isolé, sans réseau), nettoyés, mis en cache par empreinte, puis affichés en natif (Svg.Skia).
- **Blocs et vues d'extensions** : le code de l'extension tourne dans le bac à sable du moteur caché. Le HTML qu'il renvoie est converti en éléments natifs depuis une **liste blanche stricte** : paragraphes, listes, tableaux, emphase, `details`, boutons `data-action`, champs. Les faiblesses `data-externe` et `style` disparaissent ainsi par construction.
- **Risque** : `FlowDocument` ne virtualise pas l'affichage. C'est pourquoi le jalon J7 fixe un critère de performance sur une longue fiche.

**Condition de réussite** : le **corpus de référence de l'ancrage** (« Markdown → texte rendu → positions »), généré depuis l'implémentation actuelle, est écrit dès le jalon J3. La projection C# doit le reproduire à l'identique avant toute écriture d'ancre par la nouvelle application.

### 2.7 Architecture cible

```
kyfRev-dotnet/
├── src/
│   ├── KyfRev.Domain/                 algorithme des J, suivi, rappels, tirage — aucune dépendance
│   ├── KyfRev.Application/            cas d'usage, ports (interfaces), contrats de modules
│   ├── KyfRev.Infrastructure.Vault/   coffre format 1 : JSON fidèle, YAML ligne à ligne, atomicité,
│   │                                  verrou, journal, versions, corbeille, sauvegardes, import/export
│   ├── KyfRev.Infrastructure.Crypto/  coffre chiffré (AES-256-GCM, Argon2id/PBKDF2), DPAPI
│   ├── KyfRev.Infrastructure.Windows/ correcteur, voix, instance unique, barre de titre
│   ├── KyfRev.Infrastructure.Online/  Wikiquote, GitHub (catalogue, mises à jour), HttpClient durci
│   ├── KyfRev.DesignSystem/           jetons (gammes, rôles), styles et contrôles WPF de kyfRev
│   ├── KyfRev.Presentation.Wpf/       coquille, vues, ViewModels (MVVM), hôte WebView2
│   ├── KyfRev.Rendering/              Markdown → document natif, projection du texte rendu, ancrage
│   ├── KyfRev.WebEngine/              moteur web caché (Mermaid, schémas, extensions) et éditeur visuel
│   ├── KyfRev.Modules.Citations/      module interne (injection de dépendances)
│   ├── KyfRev.Modules.Schemas/        module interne (îlot WebView2 en 1.0)
│   ├── KyfRev.Extensions.Host/        extensions tierces HTML/JS isolées
│   └── KyfRev.Cli/                    outil « kyfrev-coffre » : vérifier, chiffrer, déchiffrer, exporter
├── tests/                             Domain, Application, Vault (corpus), Crypto, Architecture,
│                                      Compat (vecteurs Rust), UI (FlaUI)
├── installer/                         script Inno Setup, images générées depuis le SVG
├── assets/icon/kyfrev.svg             source vectorielle unique de l'icône
└── Directory.Build.props, Directory.Packages.props, global.json, KyfRev.slnx
```

**Règles de dépendance**, imposées par des tests d'architecture (ArchUnitNET ou NetArchTest) :
- `Domain` ne dépend de rien ;
- `Application` ne dépend que de `Domain` ;
- `Infrastructure.*` implémente les ports d'`Application` ;
- `Presentation` ne référence aucune `Infrastructure` concrète : seule la racine de composition (`App`) les relie ;
- **aucun module ne référence un autre module**.

**Modularité par injection de dépendances** (sans toucher au cœur de l'exécutable) :
- **Contrat** : `IKyfRevModule` (`Id`, `ConfigureServices(IServiceCollection)`, activation selon le coffre).
- **Points d'extension typés** : `IBlockRenderer` (langages de blocs), `INavigationContributor`, `IDashboardWidget`, `ISearchProvider`, `IExportFormat`, `IVaultHealthCheck`.
- Les **modules internes** (Citations, Schémas) sont des assemblys compilés, branchés par `Microsoft.Extensions.DependencyInjection` et activés par `coffre.json`.
- Les **extensions tierces** passent par `Extensions.Host`, lui-même un module qui implémente ces points d'extension **par procuration**, dans le bac à sable WebView2.
- **Aucun chargement dynamique de code .NET tiers.** Un `AssemblyLoadContext` n'est pas une frontière de sécurité : un plugin .NET aurait tous les droits de l'utilisateur, jusqu'au déchiffrement du jeton.

**Garde-fous transposés de l'existant**
- Test par réflexion : aucun cas d'usage public ne porte un nom de suppression.
- Analyse statique : `File.Delete` et `Directory.Delete` ne sont autorisés que dans les modules prévus (temporaires, verrou, rotation des sauvegardes).
- La date du jour vient toujours de `TimeProvider`, injecté, ce qui remplace les variables d'environnement de test.

**Bibliothèques pressenties** (licences permissives, versions figées, gestion centralisée des paquets) :

| Usage | Bibliothèques |
|---|---|
| MVVM et hôte d'application | CommunityToolkit.Mvvm, Microsoft.Extensions.Hosting, DependencyInjection, Logging |
| Rendu et données | XAML-Math, Svg.Skia, TextMateSharp, AvalonEdit, Microsoft.Web.WebView2, System.Text.Json (avec convertisseurs dédiés), YamlDotNet (lecture seule, aligné sur YAML 1.2) |
| Comparaison de texte | DiffPlex |
| Cryptographie | AesGcm et Rfc2898DeriveBytes (bibliothèque standard), NSec ou libsodium (Argon2id), ProtectedData |
| Tests | MSTest sur Microsoft Testing Platform, CsCheck (propriétés), Verify (instantanés), Stryker.NET (mutations), FlaUI, ArchUnitNET. MSTest remplace xUnit v3, envisagé d'abord : Stryker.NET ne sait pas encore activer ses mutants dans les tests xUnit v3, exécutés hors du processus de test (constaté au jalon J2). |

Chaque bibliothèque sera validée au jalon qui l'introduit.

### 2.8 Sécurité dès la conception

**Modèle de menace, en résumé**

| Rang | Menace | Réponse |
|---|---|---|
| 1 | Vol ou perte du disque, de la clé USB ou d'une sauvegarde | Chiffrement au repos en option |
| 2 | Contenu de fiche malveillant (Markdown, SVG, Mermaid) | Rendu nettoyé, CSP stricte, aucun script, Mermaid isolé |
| 3 | Extension malveillante | Bac à sable, permissions contrôlées par l'hôte, catalogue signé |
| 4 | Corruption (coupure, antivirus, double instance) | Écriture atomique, verrou, journal, sauvegardes vérifiées |
| 5 | Chaîne d'approvisionnement | Dépendances figées, délai de 14 jours avant adoption, audit NuGet, actions épinglées par SHA |

**Hors périmètre, assumé et documenté** : un logiciel malveillant qui tourne sous la session de l'utilisateur pendant que le coffre est déverrouillé.

**Chiffrement au repos (coffre chiffré, en option)**
- **Clés** : une clé de données (DEK) aléatoire de 256 bits par coffre. Elle est enveloppée par une clé dérivée du mot de passe maître :
  - **Argon2id** par défaut, avec des paramètres calibrés à environ 0,5 à 1 s à la création et enregistrés dans l'en-tête (au minimum m = 64 Mio, t = 3, p = 4, selon la RFC 9106) ;
  - **PBKDF2-HMAC-SHA256** (600 000 itérations) disponible en option.
- **Clé de récupération** : 256 bits, affichée une seule fois en groupes base32 à imprimer. Elle enveloppe aussi la DEK. Option « se souvenir sur cet ordinateur » : la DEK enveloppée par DPAPI.
- **Changer de mot de passe** revient à envelopper de nouveau la DEK : rapide, aucune donnée réécrite.
- **Chaque fichier** est chiffré en **AES-256-GCM** :
  - nonce aléatoire de 96 bits à chaque écriture (largement sous la limite de 2³² chiffrements par clé) ;
  - données associées : magique, version, identifiant logique du fichier, ce qui empêche d'échanger des fichiers entre eux.
- **Noms de fichiers** : chiffrés eux aussi. Le coffre chiffré stocke des objets opaques (`objets/<ULID>.kyf`) et un index chiffré. C'est le prix à payer pour que les titres ne fuient pas.
- **Hygiène** :
  - les sauvegardes d'un coffre chiffré restent chiffrées ;
  - aucun fichier temporaire en clair ;
  - WebView2 sert le contenu depuis la mémoire, avec `Cache-Control: no-store` ;
  - journal sans contenu ;
  - clés effacées de la mémoire (`CryptographicOperations.ZeroMemory`) ;
  - verrouillage automatique après une inactivité réglable.
- **Compromis explicite** : un coffre chiffré n'est plus lisible par un éditeur de texte ni par l'application Tauri 0.x. C'est pourquoi le chiffrement reste **une option par coffre**, réversible par l'outil de conversion (§4.3). BitLocker est recommandé en complément (fichier d'échange, mise en veille prolongée).

---

## 3. Nom du nouveau dépôt GitHub privé

| Proposition | Avis |
|---|---|
| **`kyfRev-dotnet`** (recommandé) | Garde le nom du produit, indique sans ambiguïté la plateforme, ne dépend ni du framework d'interface ni d'un numéro de version. Il se distingue clairement de `kyfRev` (Tauri). |
| `kyfRev-desktop` | Ambigu : l'application actuelle est aussi une application de bureau. |
| `kyfRev-wpf` | Lié au framework d'interface ; trompeur si la présentation change un jour. |
| `kyfRev2`, `kyfRev-next`, `kyfRev-new` | Ambigus avec les numéros de version, et vieillissent mal. |

**À ne pas faire :**
- **renommer l'ancien dépôt** tant que la 0.x est en service. Son adresse `KyllianFF/kyfRev` est codée en dur pour le catalogue et les mises à jour, et son client HTTP refuse toute redirection : un renommage les casserait ;
- **archiver** l'ancien dépôt avant la parité. Il pourra l'être une fois la 1.0.0 adoptée.

Le produit, l'exécutable (`kyfRev.exe`), le nom affiché et l'installateur restent **kyfRev**. Les espaces de noms C# sont `KyfRev.*` (convention PascalCase de .NET).

---

## 4. Plan de migration du coffre

### 4.1 Faisabilité : lecture native, pas de conversion obligatoire

Le format 1 est ouvert et documenté : des Markdown avec en-tête YAML, et des JSON dans `.suivi/`. La nouvelle application **lit et écrit le coffre sur place, au format 1**. Aucun import n'est nécessaire pour un coffre en clair.

Elle reste **compatible dans les deux sens** avec Tauri 0.8.0 pendant la transition :
- même verrou système sur `.suivi/verrou`, donc pas d'ouverture simultanée ;
- mêmes noms de fichiers temporaires (`.ecriture-*.tmp`), ignorés par les deux implémentations ;
- même journal d'opération.

### 4.2 Couche de compatibilité (`KyfRev.Infrastructure.Vault`)

L'audit du format a relevé 19 pièges, tous traités et testés.

**Lecture tolérante**
- Champs absents remplacés par les mêmes valeurs par défaut que Rust.
- Discriminants (`statut`, `evt`) acceptés dans n'importe quel ordre.
- Horodatages jusqu'à 9 décimales, avec n'importe quel décalage.
- `null` accepté là où Rust attend un tableau.
- **Aucun fichier illisible n'est jamais réécrit.**

**Écriture fidèle**
- JSON indenté de 2 espaces, LF et `\n` final ; ordre des propriétés identique à Rust.
- Non-ASCII écrit tel quel ; horodatages en UTC `Z`, sans zéros finaux.
- `rappelTireLe: null` toujours écrit, les autres options omises.
- **Champs inconnus conservés à tous les niveaux** (amélioration par rapport à l'existant).

**En-tête YAML**
- Jamais re-sérialisé : on reproduit exactement l'édition ligne à ligne (BOM, CRLF, `id` en première ligne, valeurs en chaînes JSON entre guillemets doubles).

**Empreintes**
- `sha256:` calculé sur les octets bruts, BOM et CRLF compris. Le contrôle d'écriture optimiste et le journal en dépendent.

**Chemins et noms**
- Règles `CheminRelatif` à l'identique : 255 octets UTF-8, noms réservés Windows, aucune normalisation Unicode, ordre par octets UTF-8.

**Effets de l'ouverture**
- Rejouer `operation.json` **avant** l'analyse.
- Ne jamais régénérer `coffre.json.id`.
- Un coffre dont le `format` dépasse la version prise en charge s'ouvre en **lecture seule**.

**Schémas réseau**
- Lire le format 2 (`schemas/*.json`) et le format 1 écrit en ligne dans un bloc ```` ```reseau ````.

### 4.3 Outil dédié `kyfrev-coffre` (CLI, livré avec l'application)

| Commande | Rôle |
|---|---|
| `verifier <coffre>` | Lecture seule intégrale : chaque fichier parsé, invariants des suivis, ancres, liens, doublons. Produit un rapport (texte ou JSON) **sans rien écrire**, pas même le verrou-info. |
| `chiffrer <source> <destination>` | Copie chiffrée dans un **nouveau** dossier, jamais sur place. Puis vérification complète : chaque fichier est déchiffré et son SHA-256 comparé à l'original. La source reste intacte. |
| `dechiffrer <source> <destination>` | Opération inverse, avec la même vérification ; produit un coffre format 1 lisible par Tauri 0.x. |
| `exporter <coffre> <zip>` | Archive en clair ou chiffrée, vérifiée entrée par entrée. |
| `changer-mot-de-passe`, `cle-de-recuperation` | Gestion des clés d'un coffre chiffré. |

**Reprise sur interruption** : toute conversion écrit d'abord dans un dossier temporaire voisin, puis renomme ce dossier en une seule opération. Une conversion coupée ne laisse donc jamais de coffre à moitié écrit.

### 4.4 Déroulé de la migration sur ce poste

1. **Avant toute écriture par la nouvelle application** :
   - fermer kyfRev 0.x ;
   - sauvegarde zip vérifiée du coffre ;
   - `kyfrev-coffre verifier Documents\kyfRev\Coffre`, avec un rapport de comptage par entité.
2. **Premier lancement de kyfRev .NET**. Il importe les réglages propres à la machine, sans les modifier :
   - `config-locale.json` (dernier coffre) ;
   - `jeton-github.bin` (relu par DPAPI, puis protégé de nouveau avec l'entropie de l'application) ;
   - l'état des extensions.
   
   Ses propres fichiers locaux (journal, données WebView2) vont dans des sous-dossiers distincts de `%LOCALAPPDATA%\kyfRev`, pour ne pas entrer en collision avec la 0.x : `%LOCALAPPDATA%\kyfRev\v1`, où elle retient aussi son dernier coffre (J6.2).
3. **Contrôle croisé** : le coffre écrit par la nouvelle application est rouvert par Tauri 0.8.0. Son panneau Santé ne doit signaler **aucun** problème.
4. **Chiffrement (facultatif)** : `chiffrer` vers un nouveau dossier, puis bascule de l'application vers ce dossier. Le dossier en clair est conservé jusqu'à confirmation explicite, puis retiré **par l'utilisateur**, avec un avertissement : sur un SSD, l'effacement sûr n'est pas garanti, d'où la recommandation BitLocker.
5. **Retrait de la 0.x** : l'installateur détecte kyfRev 0.x (clé de désinstallation, éditeur « kyfRev ») et propose de le désinstaller **après** un premier lancement réussi, jamais d'office. Le coffre n'est jamais touché par une désinstallation.

### 4.5 Corpus de référence (preuve du « zéro perte »)

Un **coffre de référence synthétique**, sans aucune donnée personnelle, est fabriqué avec Tauri 0.8.0 à partir des contenus de `demo.ts` et des cas limites des tests Rust. Il couvre :
- les fiches actives et archivées, les rappels dans tous les états ;
- les passages et définitions dans les 3 états d'ancrage ;
- la corbeille, les versions, un journal interrompu ;
- les citations (y compris étrangères), les schémas en format 1 et 2, les extensions ;
- les CRLF et BOM, les noms limites, les chemins de plus de 260 caractères.

Ce corpus est versionné dans `tests/fixtures/`. Critères :
- **lecture puis réécriture sans modification** : les fichiers sont identiques à l'octet ;
- **opérations scénarisées** : le résultat est identique à celui de Rust ;
- **ré-ancrage** : les positions sont identiques à celles du moteur actuel.

---

## 5. Gouvernance git, versions et livraison

### 5.1 Branches

| Branche | Usage |
|---|---|
| `main` | Toujours livrable. On n'y travaille jamais directement. |
| `feature/…` | Nouvelle fonction. |
| `fix/…` | Correctif ordinaire. |
| `hotfix/…` | Correctif urgent d'une version publiée, tiré de l'étiquette. |
| `release/x.y.z` | Gel, mise à jour du CHANGELOG et de la version, préversions. |
| `chore/…` | Outillage, dépendances, CI. |
| `refactor/…` | Restructuration sans changement de comportement. |
| `docs/…` | Documentation. |
| `test/…` | Tests seuls. |

- Un contrôle de CI refuse toute demande de fusion dont la branche ne suit pas ce motif.
- **Limite à connaître** : sur GitHub Free, un dépôt **privé** n'a ni règles de protection de branche ni rulesets ; il faut GitHub Pro. Sans Pro, la discipline repose sur la CI et sur un crochet local `pre-push` qui refuse toute poussée directe sur `main`.

### 5.2 Identité des commits : uniquement KyllianFF

- **Configuration locale du dépôt** (aucune identité globale n'est définie sur ce poste) : `user.name = KyllianFF`, `user.email = 113480351+KyllianFF@users.noreply.github.com`. C'est l'adresse « noreply » déjà utilisée par les 103 commits existants de KyllianFF : elle ne révèle aucune adresse personnelle.
- **Fusions faites en local** (`git merge --no-ff`, puis `git push`) : GitHub marque alors la demande de fusion comme fusionnée. Une fusion depuis l'interface web (merge, squash ou rebase) inscrit **GitHub comme committer**, contraire à l'exigence.
- **Contrôles** : la CI vérifie que chaque commit d'une demande de fusion a pour auteur **et** committer KyllianFF, sans trailer `Co-Authored-By` ni lien de session. Un crochet local `commit-msg` fait le même contrôle avant chaque commit.
- **Dépendances** : Dependabot sert uniquement aux **alertes**. Les mises à jour sont commitées par KyllianFF, puisque les commits automatiques auraient un autre auteur.
- **Étiquettes et versions publiées** :
  - les étiquettes `vX.Y.Z` sont créées **en local** par KyllianFF (annotées, signables par clé SSH) ;
  - la CI construit les artefacts ;
  - la version GitHub est publiée sous l'identité de KyllianFF, soit par un script local, soit par un jeton fine-grained de KyllianFF en secret du dépôt.
- **`.gitignore`** : il exclut aussi les dossiers et fichiers de configuration locale des éditeurs et des outils de développement.

### 5.3 Versionnement sémantique et journal des modifications

- **SemVer 2.0.0 strict.**
  - **Source unique** : `<Version>` dans `Directory.Build.props`, propagée à l'assembly, au module web, à l'installateur et à l'écran À propos.
  - La CI vérifie l'égalité **étiquette = version = section datée du CHANGELOG**.
- **CHANGELOG.md** au format *Keep a Changelog 1.1.0*, en français :
  - sections Ajouté, Modifié, Obsolète, Retiré, Corrigé, Sécurité ;
  - rubrique `[Non publié]` tenue à jour dans chaque branche ;
  - liens de comparaison entre versions.
- **Numérotation proposée** :
  - préversions `1.0.0-alpha.N` (jalons J6 à J8), puis `1.0.0-beta.N` (J9, J10) ;
  - **`1.0.0` à la parité fonctionnelle avec Tauri 0.8.0**. La numérotation reste supérieure à 0.8.0, ce qui garde cohérentes la détection de mise à jour et celle de l'installateur ;
  - ensuite `1.x` pour les ajouts et `2.0.0` pour tout changement incompatible du format du coffre.

### 5.4 Hygiène du dépôt

**`.gitignore`** exhaustif :
- artefacts et caches : `bin/`, `obj/`, `artifacts/`, `publish/`, `*.user`, `.vs/`, `TestResults/`, `coverage*/`, `node_modules/`, `dist/`, sortie de l'installateur ;
- environnement et secrets : `*.pfx`, `*.snk` privés, `.env*`, `appsettings.*.local.json` ;
- fichiers système : `Thumbs.db`, `desktop.ini`, `.DS_Store` ;
- coffres d'essai locaux et configurations d'outils locaux.

**Fichiers de configuration du dépôt**
- `.gitattributes` : `* text=auto eol=lf`, avec des exceptions (`*.iss`, `*.ps1`, `*.bat`) en CRLF ;
- `.editorconfig` avec les règles d'analyse ;
- `Directory.Build.props` : `Nullable`, `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`, `Deterministic`, `ContinuousIntegrationBuild` en CI.

**Secrets et données personnelles**
- Gitleaks en crochet `pre-commit` et en CI ; aucune donnée personnelle dans les fixtures, qui sont toutes synthétiques.

**CI GitHub Actions**, sur chaque demande de fusion prête et sur `main` (décidé le 2 novembre 2026, après l'épuisement du quota d'Actions ; sur chaque push, seuls les contrôles d'identité, de nom de branche et de secrets tournent) :
- compilation et formatage (`dotnet format --verify-no-changes`) ;
- tests et couverture ;
- `dotnet list package --vulnerable --include-transitive` ;
- licences des dépendances ;
- contrôles d'identité et de nom de branche ;
- Gitleaks.

La livraison n'est déclenchée que sur étiquette. Toutes les actions sont épinglées par SHA.

### 5.5 Installateur et distribution

**Choix : Inno Setup 6** (version figée en CI).

| Option | Verdict |
|---|---|
| **Inno Setup** | Assistant professionnel en français, scriptable, fiable en CI. Installation par utilisateur **et** pour tous les utilisateurs dans un même paquet (`PrivilegesRequiredOverridesAllowed=dialog`). Signature intégrée. |
| WiX (MSI) | Pertinent pour un déploiement en entreprise par GPO, mais nettement plus complexe. Les MSI à double usage, par utilisateur ou par machine, sont délicats. |
| MSIX | Exige un certificat approuvé même pour un usage personnel. La virtualisation d'AppData est incompatible avec les modes portable et coffre. Écarté. |

**Spécifications de l'installateur**

| Exigence | Réalisation |
|---|---|
| Nom officiel | `AppName=kyfRev`, raccourcis « kyfRev », exécutable `kyfRev.exe` |
| Éditeur | `AppPublisher=KyllianFF` ; mêmes valeurs dans les métadonnées de l'exécutable (`Company`, `Product`, `Copyright © 2026 KyllianFF`) |
| Version exacte | `AppVersion` et `VersionInfoVersion` injectées depuis la source unique ; affichées dans l'assistant et dans Paramètres › Applications |
| Date de publication | Injectée par la CI depuis la section du CHANGELOG. Affichée sur la page d'accueil de l'assistant (« Version 1.0.0 publiée le … ») ; inscrite dans les métadonnées de l'exécutable (`AssemblyMetadata("ReleaseDate")`) et dans l'écran À propos |
| Icône vectorielle dédiée | Source unique `assets/icon/kyfrev.svg` (un éventail de fiches, redessiné pour Veillée). Le `.ico` (16 à 256 px) et les images de l'assistant à chaque échelle d'affichage (100 à 250 %) sont **générés** par `tools/icon/icon.cs` et versionnés, car la compilation en a besoin sans lancer WPF : aucune image matricielle n'est éditée à la main. |
| Chemins standard | **Par utilisateur (par défaut, sans droits administrateur)** : `%LocalAppData%\Programs\kyfRev`. **Pour tous les utilisateurs (option)** : `C:\Program Files\kyfRev`. Coffre par défaut dans `Documents\kyfRev\Coffre` ; données propres à la machine dans `%LocalAppData%\kyfRev` |
| Sémantique reprise de la 0.x | Installation silencieuse (`/VERYSILENT`, paramètres `/COFFRE=documents\|donnees`, `/NOICONS`) ; mise à jour sur place ; rétrogradation refusée avec code de sortie dédié ; la désinstallation ne touche jamais aux coffres ni aux profils ; détection de l'application ouverte |
| Prérequis | Exécutable autonome (runtime .NET inclus, aucun prérequis) ; WebView2 installé si absent, avec vérification Authenticode du programme d'amorçage avant lancement |
| Signature | Point d'entrée de signature prévu (`SignTool`) |

> **Attention : signature.** Un certificat de signature de code (Azure Trusted Signing, par exemple) affiche le nom **légal** vérifié, et non « KyllianFF ». Sans signature, SmartScreen avertit au premier lancement.

**Version portable** : `kyfRev-<version>-portable.zip`, avec la structure `MesRévisions\Application\kyfRev.exe` et `portable.json` de la 0.x. Rien n'est écrit hors du dossier.

---

## 6. Feuille de route

Les jalons sont **séquentiels**. Chacun est livré par une ou plusieurs branches conformes au §5.1 et se clôt par **ma validation** sur ses critères. Taille indicative : S < 5 j, M = 5 à 15 j, L = 15 à 30 j, XL > 30 j de travail. La parité complète représente plusieurs mois à temps plein, dont environ 40 % pour l'interface.

### J0. Validation du plan (ce document)
- **Livrable** : décisions du §7 arrêtées.
- **Critère** : mon accord explicite.

### J1. Socle du dépôt et gouvernance (S)
- **Livrables** :
  - dépôt privé `kyfRev-dotnet`, identité git locale, crochets `commit-msg`, `pre-commit` (Gitleaks) et `pre-push` ;
  - fichiers de configuration : `.gitignore`, `.gitattributes`, `.editorconfig`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, solution `.slnx` vide structurée selon §2.7 ;
  - CI de vérification ;
  - CHANGELOG, README, SECURITY.md, LICENSE ;
  - glossaire français ↔ code.
- **Critères** :
  - CI verte sur une solution vide ;
  - un commit d'essai sous une autre identité est **refusé** par le crochet et par la CI ;
  - un faux secret est **bloqué** ;
  - `git log --format='%an %ae %cn %ce'` ne montre que KyllianFF.

### J2. Domaine : algorithme des J (M)
- **Livrables** :
  - `KyfRev.Domain` (séquence, suivi, cycles, événements, rappels, tirage, ChaCha8 et FNV-1a, dates) ;
  - portage de **tous** les scénarios datés (T2 à T6, T10, T11, T18, exemples §7) et des propriétés (invariants sur des suites d'opérations aléatoires) ;
  - vecteurs de référence générés depuis l'implémentation Rust.
- **Critères** :
  - 100 % des scénarios Rust passent en C# ;
  - mêmes états JSON que Rust sur 10 000 suites d'opérations aléatoires ;
  - mêmes tirages que Rust pour des graines données ;
  - couverture ≥ 95 % et score de mutation ≥ 80 % sur `Domain`.

### J3. Compatibilité du coffre format 1 (L)
- **Livrables** :
  - `KyfRev.Infrastructure.Vault` : lecture et écriture, atomicité, verrou compatible, journal, versions, corbeille, sauvegardes et rotation, import, exports ;
  - corpus de référence (§4.5) ;
  - corpus d'ancrage « Markdown → texte rendu → positions » ;
  - outil `kyfrev-coffre verifier`.
- **Critères** :
  - le corpus est relu et réécrit **identique à l'octet** ;
  - les opérations donnent les mêmes résultats que Rust ;
  - un coffre écrit par C#, rouvert par Tauri 0.8.0, n'a **aucun** signalement dans Santé ;
  - une coupure simulée pendant l'écriture laisse l'ancienne ou la nouvelle version (T15) ;
  - le verrou est respecté entre C# et Rust ;
  - `verifier` sur mon coffre réel n'écrit **aucun** octet (empreintes avant et après).

### J4. Application, modularité, garde-fous (M)
- **Livrables** :
  - cas d'usage (un par commande de l'existant, regroupés par domaine) ;
  - ports ;
  - `IKyfRevModule` et points d'extension ;
  - modules Citations (+ Wikiquote) ;
  - tests d'architecture ;
  - test par réflexion « aucune commande de suppression ».
- **Critères** :
  - les dépendances entre couches sont imposées par les tests ;
  - un module désactivé n'est pas chargé, et son absence ne casse rien ;
  - les scénarios applicatifs passent avec une horloge simulée (`TimeProvider`), y compris le passage de minuit.

### J5. Design system et coquille WPF (M)
- **Livrables** :
  - `KyfRev.DesignSystem` :
    - jetons de l'identité « Veillée » (2 gammes, forêt et ambre, et leurs rôles), choisie en novembre 2026 parmi plusieurs pistes ; polices Atkinson Hyperlegible Next et Mono livrées avec l'application ;
    - échelles **régulières** : typographie, espacements sur une grille de 4 px, rayons, élévations, durées ;
    - thèmes clair, sombre et **contraste élevé** Windows ;
    - composants : bouton, champ, carte, pastille, menu, dialogue, notification, état vide, squelette de chargement ;
  - coquille : navigation qui s'adapte à la largeur de la fenêtre (barre latérale, colonne d'icônes ou onglets en bas, jamais de défilement horizontal), Ctrl+K, F1, mode concentration, zoom ;
  - réduction des animations respectée ;
  - galerie de composants pour la revue visuelle.
- **Critères** :
  - un test automatique vérifie les contrastes de **toutes** les paires de jetons (≥ 4,5:1 pour le texte, ≥ 3:1 pour les contours et repères, ce qui corrige le 1,29:1 actuel) ;
  - aucune couleur codée en dur hors des jetons (test) ;
  - navigation complète au clavier avec focus toujours visible ;
  - Accessibility Insights (FastPass) sans erreur ;
  - captures claires et sombres validées par moi.

### J6. Parcours de révision : `1.0.0-alpha.1` (L)
- **Livrables** :
  - tableau de bord (En retard, Aujourd'hui, Rappels, À venir), avec la liste à réviser **toujours en premier** ;
  - **séance d'étude repensée** :
    - progression visible ;
    - raccourcis Valider, Suivant et Passer ;
    - reprise d'une séance interrompue ;
    - bilan de fin (temps, faits, reportés, restants) ;
    - mode révision plein écran ;
  - fiche en Lire et Réviser ;
  - archives et rappels (3 issues) ;
  - squelette de l'installateur Inno Setup (métadonnées du §5.5).
- **Critères** :
  - scénarios d'acceptation T2 à T6, T9, T10, T11 et T18 automatisés de bout en bout (FlaUI, horloge simulée) ;
  - tableau de bord affiché en moins de 300 ms sur un coffre de 2 000 fiches ;
  - préversion installable et désinstallable sans droits administrateur ;
  - essai sur une **copie** de mon coffre.

### J7. Lecture native, annotations et édition : `alpha.2` (XL)
Ce jalon se découpe en sous-parties validées une à une, comme J6.
- **Livrables** :
  - vue **Fiches** : la liste et la création d'une fiche (l'arborescence et le glisser-déposer restent à J9) ;
  - vue **Définitions** ;
  - vue **Paramètres**, première partie : séquence des étapes et rappels de consolidation ;
  - `KyfRev.Rendering` : Markdown → document natif, projection du texte rendu, ré-ancrage en C# ;
  - lecture et révision natives : texte à trous, surlignage, passages, définitions, panneau d'annotations, plan, lecture à voix haute avec surlignage du bloc lu ;
  - moteur web caché et durci (CSP, aucune navigation, outils de développement coupés, variables `WEBVIEW2_*` neutralisées) : Mermaid et schémas rendus en SVG ;
  - éditeur Markdown natif (AvalonEdit, `[[`, images, correcteur Windows), avec son ruban ;
  - éditeur visuel ProseMirror dans WebView2, relié par un bus de messages typé et généré ;
  - versions et conflits (comparaison).
- **Décisions de l'écriture, arrêtées sur maquettes le 1er novembre** :
  - en écriture, le texte mis en forme se modifie à sa place : c'est l'éditeur visuel, le mode par défaut dès qu'il existera. Un bouton affiche le code Markdown dans un volet à part, **à gauche** du texte ;
  - le ruban ressemble à ceux d'Office : un onglet **Accueil** (gras, italique, souligné, barré, couleur, exposant et indice, listes à puces, numérotées et de tâches, taille, alignement, casse) et un onglet **Insertion** (tableau à la taille choisie, bloc de code avec son langage, schéma, graphique, SmartArt, lien, images). Annuler, Rétablir, les titres, la formule et la ligne s'y ajoutent, repris de la 0.x ;
  - la casse propose aussi « iNVERSER lA cASSE », absente de la 0.x ;
  - **le format s'étend**, sur ma décision : couleur et taille en `<span style="…">`, alignement en `<div align="…">`, tâches en `- [ ]`. Seules ces formes exactes sont reconnues, et tout autre HTML reste du texte. La 0.8.0 les affiche comme du texte, sans rien perdre : une fiche qui les emploie n'y a plus la même mise en forme, ni les mêmes positions de passages ;
  - couleur, taille, alignement, tâches : les choix de réalisation. Une couleur lue dans une fiche reste **lisible partout** : sa teinte et sa saturation restent, sa clarté (OKLCH) s'arrête à 0,45 sur fond clair et à 0,78 sur fond sombre, bornes calculées sur tout le cube de couleurs pour garantir 4,5:1, y compris sur la surface des tableaux. La même règle vaut à la lecture (`TokenColor.ReadableOn`) et à l'écriture (CSS). En contraste élevé, le thème l'emporte. Une couleur collée d'ailleurs n'entre pas dans la fiche : une page web fixe la couleur de tout. La taille se choisit en pixels et s'écrit relative au texte (`1.18em`), de 0,5 à 3 fois, pour suivre un titre. Un passage aligné est un conteneur de blocs, sur ses propres lignes, sans interrompre un paragraphe ; Markdown n'aligne un tableau que par colonne, et une liste s'aligne entière. En lecture, la case d'une tâche se dessine, mais « [ ] » compte encore dans le texte : les positions des surlignages restent celles de la 0.x. Cocher se fait en écriture ;
  - les règles du format sont vérifiées **des deux côtés sur les mêmes cas** (`tests/fixtures/format-etendu/cas.json`), lus par la lecture native et par l'éditeur. Une fiche piégée qui ouvre des passages sans fin ne fait pas s'effondrer la lecture : la recherche des fermetures a un budget de lignes, le même des deux côtés ;
  - schéma, graphique et SmartArt ouvrent une **galerie comme celle d'Office** : des catégories, une vignette par modèle, un aperçu qui suit le texte saisi ;
  - **kyfRev dessine lui-même les SmartArt**, en un bloc de code lisible partout : Mermaid n'y donne ni vrai cycle ni vraie pyramide (essai du 1er novembre sur Mermaid 12.2.0). Les schémas et les graphiques restent en Mermaid ;
  - un SmartArt est décrit une seule fois (`KyfRev.Rendering/Diagrams`) : une liste de formes, sans thème ni affichage, que WPF dessine en lecture et que l'éditeur reçoit en SVG. Les deux restent donc identiques ;
  - Mermaid dessine dans **une page cachée** (`MermaidEngine`) : le même moteur que l'éditeur, aussi fermé, qui ne voit jamais le coffre et ne reçoit que du code. Il rend une image, et un dessin qui ne finit pas (20 s) ferme la page, qui repart pour le suivant. Mermaid 12.2.0 pèse 5,5 Mo, chargés seulement si une fiche a un schéma. Sans étiquettes HTML : elles empêcheraient d'en faire une image ;
  - un schéma écrit à la main reste modifiable en code : seule un SmartArt se rouvre dans la galerie, car rien ne permet de retrouver un modèle dans du code Mermaid ;
  - sur ma décision, avant toute fusion, l'éditeur visuel arrive en même temps que le ruban et le Markdown natif ; puis le format étendu ; puis la galerie ;
  - contrôle retenu : `WebView2`, et non `WebView2CompositionControl`, qui demande le SDK Windows et passe par une capture de l'écran, au détriment de la frappe. Le moteur a sa propre fenêtre : pendant qu'un dialogue ou un volet le recouvre, son image prend sa place ;
  - le bus de messages est typé et généré : `VisualMessages.cs` donne `tools/visual-editor/src/messages.ts`, et un test vérifie qu'il est à jour. Le paquet de l'éditeur est versionné, et la CI vérifie qu'il correspond à ses sources : compiler kyfRev ne demande pas Node.
- **Décisions de la lecture, arrêtées le 2 novembre** :
  - **texte à trous** : mêmes règles que la 0.x. Le texte reste dans le document, dans la couleur de son fond : rien ne bouge à l'affichage. Le mode « Réviser » s'ajoute à « Lire », car la lecture native n'en avait pas ;
  - **écouter** : les voix SAPI de Windows par `System.Speech`, hors ligne, sans changer la cible de la solution pour le SDK Windows. Pendant une révision à trous, la voix se tait : elle dirait ce qui est masqué ;
  - **code coloré** : les règles d'AvalonEdit, déjà présent, dont seuls les noms servent : quatre rôles du design system donnent les couleurs, au contraste vérifié. Moins de langages que highlight.js ;
  - **images SVG** : par la page cachée de Mermaid, montrées comme images puis rendues en PNG : ni script ni adresse n'y agissent (T16) ;
  - **20 000 mots** : mesuré sur le poste de développement, à froid, à 490 ms (55 ms d'analyse, 430 ms d'affichage), avec des morceaux de moins de 60 ms et un défilement de 9 ms en médiane. Un test le mesure et garde contre une dégradation grossière ; ses bornes sont larges, car un serveur de vérification peut être très lent ;
  - **vue Paramètres** : un aperçu des dates de la séquence saisie, et l'avertissement sur les cycles en cours affiché avant d'enregistrer. Le reste de la vue vient aux jalons qui l'apportent.
- **Critères** :
  - corpus d'ancrage : positions identiques à celles du moteur actuel dans 100 % des cas (T12) ;
  - T14 (modification externe sans perte) ;
  - T16 : aucun contenu piégé ne s'exécute (`<script>`, `onerror`, `javascript:`, SVG, `click` Mermaid) ;
  - T17 : aucun port de débogage, même avec les variables d'environnement ;
  - aller-retour Markdown de l'éditeur visuel fidèle à l'octet (19 cas existants) ;
  - fiche de 20 000 mots affichée en moins de 500 ms, avec un défilement fluide ;
  - fiche lue par le Narrateur de Windows : texte, titres et surlignages annoncés ;
  - scénarios d'acceptation : la fiche se crée et le passage se surligne dans l'application, T10 change la séquence dans « Paramètres », et T5 surligne un **nouveau** passage pendant le rappel.

### J8. Chiffrement au repos : `alpha.3` (M)
- **Livrables** :
  - `KyfRev.Infrastructure.Crypto` : coffre chiffré, clé de récupération, DPAPI « se souvenir », verrouillage automatique ;
  - commandes `chiffrer`, `dechiffrer` et `changer-mot-de-passe` ;
  - vue « Paramètres » : le chiffrement du coffre ;
  - documentation du modèle de menace.
- **Critères** :
  - vecteurs de test connus (AES-GCM, Argon2id, PBKDF2) ;
  - toute altération d'un octet, tout échange de fichiers ou tout mauvais mot de passe **échoue proprement**, sans rien écrire ;
  - aller-retour clair → chiffré → clair identique à l'octet ;
  - conversion interrompue à n'importe quelle étape : source intacte et reprise possible ;
  - aucun texte en clair trouvé sur le disque (balayage du profil, du dossier temporaire et des données WebView2 pendant et après une séance).

### J9. Fonctions complémentaires : `1.0.0-beta.1` (L)
- **Livrables** :
  - recherche dans les fiches et recherche globale, statistiques, charge à 30 jours, examens et « Resserrer » ;
  - modèles, rétroliens et aperçu au survol, arborescence (glisser-déposer, renommer ou déplacer avec réécriture des liens) ;
  - corbeille et éléments supprimés, Santé, sauvegardes et restauration ;
  - partage et exports, import Obsidian ;
  - vue **Citations**, et module Citations branché dans l'application (citation du jour sur « Aujourd'hui ») ;
  - vue « Paramètres » : sauvegardes et citations.
- **Critères** :
  - T1 (aucune action n'efface), T7 (déplacement du coffre), T13, T19, T20 automatisés ;
  - tests du crate `coffre` portés (import, partage, exports, maintenance, sauvegardes) au vert.

### J10. Extensions et schémas réseau : `beta.2` (L)
- **Livrables** :
  - `Extensions.Host` : contrat identique ; code exécuté dans le moteur web caché ; HTML rendu en éléments natifs depuis une liste blanche stricte ; durcissements du §2.2 ;
  - catalogue **signé** ; jeton GitHub saisi dans un champ natif, avec entropie ;
  - vérification de mise à jour avec manifeste signé ;
  - module Schémas en îlot WebView2 : compatibilité des formats 1 et 2, plan IP, exports ;
  - vue « Paramètres » : extensions et module Schémas.
- **Critères** :
  - les 4 extensions existantes fonctionnent **sans modification** ;
  - essai « sonde » porté et enrichi : fuite `data-externe`, usurpation entre cadres, faux dialogue ;
  - un catalogue altéré est refusé ;
  - les schémas existants s'ouvrent et se réenregistrent sans perte.

### J11. Livraison `1.0.0` (M)
- **Livrables** :
  - branche `release/1.0.0` ;
  - installateur final et version portable ;
  - signature (si un certificat est disponible) ;
  - migration depuis la 0.x (§4.4) ;
  - documentation utilisateur ;
  - CHANGELOG daté ; étiquette `v1.0.0`.
- **Critères** :
  - installation, mise à jour et désinstallation, par utilisateur et pour tous, testées dans **Windows Sandbox** sur Windows 11 et Windows 10 22H2 ;
  - en installation silencieuse, codes de sortie conformes ;
  - nom, éditeur, version, date et icône visibles dans l'assistant et dans Paramètres › Applications ;
  - coffre intact après désinstallation ;
  - version portable : rien d'écrit hors du dossier (comparaison du registre et d'AppData avant et après) ;
  - parité fonctionnelle avec 0.8.0 vérifiée sur la liste du §1.1.

### Après la 1.0
- Remplacement éventuel de l'éditeur visuel par un éditeur natif. C'est facultatif : il ne touche pas aux annotations.
- Portage natif de l'éditeur de schémas, si son usage le justifie.

---

## 7. Décisions

L'option que j'ai retenue est en gras.

| # | Décision | Options |
|---|---|---|
| 1 | **Framework d'interface** | **A. WPF .NET 10** (recommandé) · B. Avalonia UI · C. .NET MAUI (déconseillé) |
| 2 | **Surface « document »** | **C. Lecture, révision et annotations natives ; éditeur visuel dans WebView2** (recommandé, révisé le 31 octobre) · A. Hybride · B. 100 % natif, sans éditeur visuel en 1.0 |
| 3 | **Module Schémas réseau** | **A. Îlot WebView2 en 1.0, natif plus tard** (recommandé) · B. Natif dès la 1.0 (+30 à 45 j) · C. Abandonné |
| 4 | **Extensions tierces** | **A. Contrat HTML/JS actuel conservé et durci** (recommandé) · B. Seulement des modules .NET internes · C. Abandonnées |
| 5 | **Nom du dépôt** | **A. `kyfRev-dotnet`** (recommandé) · B. un autre nom |
| 6 | **Chiffrement** | **A. Option par coffre, coffre en clair par défaut** (recommandé) · B. Obligatoire pour tout nouveau coffre |
| 7 | **Installateur** | **A. Inno Setup, par utilisateur par défaut avec option tous les utilisateurs** (recommandé) · B. Inno Setup, pour tous les utilisateurs seulement (comme la 0.x) · C. WiX (MSI) |
| 8 | **Exécutable** | **A. Autonome (runtime inclus)** (recommandé) · B. Dépendant du runtime .NET installé |
| 9 | **Numérotation** | **A. `1.0.0-alpha.N`, puis `1.0.0` à la parité** (recommandé) · B. Suite de la 0.x (`0.9.0`…) |
| 10 | **Langue du code** | **A. Identifiants en anglais, glossaire français ↔ code, interface en français (ressources localisables)** (recommandé) · B. Identifiants en français, comme l'existant |
| 11 | **Fusions** | **A. En local, pour que le committer reste KyllianFF** (recommandé) · B. Depuis GitHub (le committer devient GitHub) |
| 12 | **Licence du nouveau dépôt** | **A. MIT, comme l'existant** · B. Tous droits réservés (dépôt privé) |
| 13 | **GitHub Pro** (protection de branche sur dépôt privé) | A. Oui · **B. Non : discipline par la CI et un crochet local** (suffisant) |

---

## Annexe A : correspondance entre les commandes existantes et les futurs cas d'usage

| Domaine (nombre de commandes Tauri) | Futur service applicatif |
|---|---|
| Application et coffre (7) | `VaultSessionService`, `DiagnosticsService` |
| Tableau de bord et examens (5) | `DashboardQueries`, `ExamService` |
| Création, modèles, dossiers (7) | `SheetCreationService`, `TemplateService`, `FolderService` |
| Organisation et partage (8) | `SheetOrganizationService`, `ShareService`, `ImportService` |
| Lecture et recherche (8) | `SheetQueries`, `SearchService`, `BacklinkQueries` |
| Révision des J (4) | `RevisionService` (le noyau) |
| Annotations et définitions (8) | `AnnotationService` |
| Édition et versions (6) | `EditingService`, `VersionService` |
| Archives et rappels (3) | `ArchiveService`, `ReminderService` |
| Santé (7), réglages (2), sauvegardes et exports (4) | `HealthService`, `SettingsService`, `BackupService`, `ExportService` |
| Citations (6) | module Citations |
| Orthographe (2) | `ISpellChecker` (Windows) |
| Schémas réseau (7) | module Schémas |
| Extensions (9), GitHub et mises à jour (4) | `Extensions.Host`, `UpdateService`, `ISecretStore` |

## Annexe B : scénarios d'acceptation repris de la conception

| Domaine | Scénarios |
|---|---|
| Rien ne s'efface | T1 |
| Calendrier des J et rappels | T2 à T6, T10, T11, T18 |
| Coffre et fichiers | T7, T9, T13, T14, T15, T20 |
| Ancrage | T12 |
| Sécurité | T16, T17 |
| Citations | T19 |
| Accessibilité | T21 |

T8, qui portait sur le processus d'installation des outils de développement, devient : « l'application ne télécharge ni n'installe rien sans action explicite ».

Ces scénarios constituent la base des critères de validation des jalons J2 à J11.
