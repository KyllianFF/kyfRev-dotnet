# Journal des modifications

Toutes les modifications notables de kyfRev (génération .NET) sont consignées ici.

Le format suit [Keep a Changelog 1.1.0](https://keepachangelog.com/fr/1.1.0/) et le projet
respecte le [versionnement sémantique](https://semver.org/lang/fr/spec/v2.0.0.html).

## [Non publié]

### Ajouté

- « J'avais oublié », à côté de « Valider », pour une fiche, un passage ou une définition. La révision compte, et l'étape
  recule d'un cran au lieu d'avancer : après un oubli à J7, c'est J3 qui revient trois jours plus tard, jamais en dessous de
  J1, et un oubli ne termine pas un cycle. La frise remet à faire les étapes revenues. Seul l'oubli s'écrit dans le coffre
  (`"souvenir": "oublie"` sur l'événement) : une validation ordinaire garde sa forme d'avant. Les statistiques comptent les
  révisions oubliées, et l'export de l'historique les signale. « Recommencer à J0 » reste, pour repartir de zéro.
- Rappel partie par partie : en révision, une fiche sans gras ni passage ne montre d'abord que ses titres et ce qui les
  précède. Entrée, Espace ou un clic affichent la partie suivante ; « Tout afficher » sert à relire seulement. Une fiche qui a
  du gras ou des passages garde son texte à trous. Les images et les schémas d'une partie masquée restent visibles.
- « Étaler… », sur Aujourd'hui. Après une absence : les révisions les plus fragiles restent aujourd'hui (celles dont le retard
  pèse le plus face à leur étape), les autres partent sur les jours suivants, sans dépasser le nombre par jour choisi, ce qui
  tombe déjà ces jours-là compris. Pour une pause annoncée, de 90 jours au plus : ce qui tombait pendant ces jours part après
  le retour. Le plan s'affiche avant d'être accepté et se recalcule à chaque changement. Ce sont des reports ordinaires,
  inscrits dans l'historique : rien ne recommence à J0, rien n'est validé.
- Aujourd'hui : une liste « dossier ou étiquette » restreint ce qui est à réviser, donc la séance que « Commencer » lance.
  Elle ne propose que les dossiers et les étiquettes de ce qui est dû ou à venir.
- « Suspendre », dans les autres actions de la barre de révision : la fiche ou le passage garde son étape et son échéance,
  mais ne se présente plus à réviser, ni dans la liste du jour, ni dans la charge, ni dans une séance, un étalement ou un
  resserrement. Il attend dans le groupe « Suspendues » d'Aujourd'hui, replié, et « Reprendre » le remet où il en était. Rien
  ne s'ajoute à l'historique ; dans le coffre, seule une clé `suspendueLe` paraît sur le cycle d'un élément suspendu.
- Paramètres › Révisions : « Lisser les échéances », coupé par défaut. Actif, à partir de J7, la prochaine étape peut se
  poser un jour avant ou après sa date exacte (deux à partir de J14, trois à partir de J30), sur le jour le moins chargé.
  Coupé, l'écart entre deux révisions vaut exactement le J, et rien ne s'écrit dans le coffre.
- Examens : une révision faite avant un examen ne place plus l'étape suivante après sa veille. La ligne d'un examen dit
  combien de fiches du dossier sont archivées, et « Resserrer… » propose, par une case jamais cochée d'office, de les
  remettre en révision.
- Importer un dossier de notes : ce qu'Obsidian écrit à sa façon est converti. Un encadré `> [!note] Titre` devient une
  citation qui commence par « Note : Titre » (les types courants sont traduits). Un surlignage `==texte==` devient du gras,
  que la révision masque comme un trou. Un commentaire `%%…%%`, qu'Obsidian ne montre pas à la lecture, n'est pas repris, et
  le bilan de l'import le dit note par note. Rien n'est touché dans le code, ni à la source.
- Formules : l'écriture chimique `\ce{…}` se dessine, en lecture comme au partage : symboles en romain, indices, charges,
  hydrates et flèches de réaction (`\ce{2H2 + O2 -> 2H2O}`, `\ce{SO4^2-}`, `\ce{N2 + 3H2 <=> 2NH3}`). C'est le cœur de
  mhchem, traduit pour le moteur de formules de kyfRev, pas tout mhchem.
- Sous le titre d'une fiche : sa date de création, son cycle à partir du deuxième, et son temps de lecture dès une minute.
- Paramètres › Données : « Passages et définitions pour Anki », un fichier à importer dans Anki ou AnkiDroid pour réviser
  en déplacement. Une définition donne son terme au recto, un passage son contexte troué ; chaque carte porte l'identifiant
  du passage, si bien qu'un nouvel import la met à jour sans la doubler. Ce qui est fait dans Anki ne revient pas.
- Paramètres › Données : « Échéances pour un agenda (.ics) ». Un événement d'une journée par jour de révision, qui dit
  combien de révisions tombent et lesquelles, et un par examen à venir. Les retards sont comptés aujourd'hui, les éléments
  suspendus n'y figurent pas, et un nouvel import met l'agenda à jour. Le texte des passages n'y est jamais écrit, mais
  les titres des fiches le sont : dans un agenda en ligne, ils quittent l'ordinateur, et le bouton le dit.
- Fiches : un filtre par étiquettes, comme la 0.x. Chaque pastille dit son nombre de fiches, on peut en choisir plusieurs, et
  « Tout afficher » les retire ; sur une fenêtre étroite, il se replie avec les autres filtres.
- Écrire : les boutons « Code dans le texte » et « Citation », dans le ruban et dans le clic droit.
- Mode concentration, comme la 0.x : un bouton dans la fiche, un autre dans la barre de la fenêtre pour en sortir, le texte
  garde la largeur de lecture même si la pleine largeur est choisie, et le mode est retenu sur l'ordinateur.

### Modifié

- L'application installée est précompilée (ReadyToRun) : au démarrage, son code n'attend plus le compilateur à la volée.
  L'installation pèse 7 Mo de plus (185 Mo au lieu de 178).
- Statistiques : « Série en cours » laisse la place à « Cette semaine », le nombre de révisions depuis lundi, avec la
  moyenne des quatre semaines d'avant. Un jour sans révision ne fait plus rien perdre.
- Une passe de finition sur toute l'interface. Les notifications perdent leur trait de couleur pour un filet discret, l'icône
  gardant la couleur de la gravité ; en thème sombre, les cartes se détachent par leur surface, sans ombre. Les espacements
  arbitraires passent sur la grille de 4 px, dans les vues comme dans l'éditeur visuel. Plus d'italique pour signaler un passage
  caché ou un aperçu vide : la couleur secondaire suffit. Le compteur d'une entrée du menu devient un nombre discret, et une
  permission nouvelle d'extension un mot plutôt qu'une pastille. Les chiffres des statistiques prennent la police à chasse fixe
  des autres compteurs, et le bilan de séance aligne ses chiffres à droite. Le texte des boutons des cartes Définitions et
  Citations s'aligne sur celui de la carte.

### Corrigé

- `tools/design-captures` compile de nouveau, et une attente qui n'aboutit pas ne bloque plus l'outil : elle le dit au bout de
  20 secondes et passe à la suite.

## [1.0.0-beta.1] - 2026-11-08

### Ajouté

- Paramètres › À propos dit le jour où la version a été publiée ; une construction hors publication n'affiche que son numéro.
- L'installateur installe WebView2 s'il manque, après avoir vérifié que son programme d'amorçage est signé par Microsoft ; sans
  réseau, l'installation finit quand même et le dit. Il livre `kyfrev-coffre`, et une nouvelle installation refuse le dossier de la
  0.x, dont la désinstallation y effacerait `kyfRev.exe`. Paramètres › À propos signale la 0.x encore installée, avertit si elle
  partage le dossier de cette version, et ouvre la page des applications de Windows pour la retirer ; rien n'est retiré d'office.
- La publication d'une version : `tools/publier-version.ps1` vérifie que l'étiquette, la version et le CHANGELOG concordent, reprend les
  fichiers construits par la CI et publie sous l'identité de la personne qui le lance. `docs/publier-une-version.md` en donne les
  étapes, et le workflow « Étiquette » refuse une étiquette qui ne concorde pas. `docs/guide-utilisateur.md` : le guide de kyfRev.
- Rechercher une mise à jour, dans Paramètres › À propos, seulement à la demande. kyfRev lit dans le dépôt une annonce signée par les
  clés de publication, dit si une version plus récente existe et peut ouvrir sa page ; rien n'est téléchargé ni installé.
  `tools/catalogue/catalogue.cs version` écrit et signe cette annonce. Le jeton GitHub de la 0.x n'est pas repris : le dépôt est public.
- La version portable : `tools/portable.ps1` écrit `kyfRev-<version>-portable.zip`, avec `MesRévisions\Application` et son
  `portable.json`, un `LISEZMOI.txt` et l'empreinte SHA-256 de l'archive. Elle n'écrit rien dans le profil : un mot accepté
  reste en mémoire au lieu d'aller dans le dictionnaire de Windows, et les extensions téléchargées attendent dans ses
  données locales, comme en version installée, au lieu du dossier temporaire.
- Recherche dans le coffre, comme la 0.x. La vue Fiches cherche aussi dans le texte, avec un extrait où les mots sont
  surlignés. Ctrl+K donne, sous les commandes et à partir de deux lettres, les fiches, les passages, les définitions et les
  citations qui contiennent tous les mots, sans tenir compte de la casse, des accents ni des ligatures ; un résultat ouvre
  la fiche, le passage ou la citation. La recherche relit le coffre par son stockage, en clair comme chiffré : aucun index
  n'est écrit, les mots cherchés ne vont jamais au journal, et le verrouillage arrête la recherche en cours puis oublie ce
  qui a été cherché et trouvé. Une fiche ouverte en écriture par un autre programme est cherchée comme les autres, et le
  lecteur d'écran dit quand rien ne correspond.
- Partager une fiche : au clic droit dans la vue « Fiches » et dans le texte, et par le bouton « Partager » de la fiche, sous « ⋯ » quand
  la fenêtre est étroite. Page web d'un seul fichier, images et formules comprises ; PDF au format A4, enregistré directement dans le
  dossier choisi ; Markdown ; Markdown avec ses images (.zip) ; texte brut ; ou presse-papiers, avec la mise en forme. Le contenu est
  préparé comme la lecture, et rien n'y est chargé d'ailleurs. Le fichier créé est donné. Sur un coffre chiffré, la fenêtre dit que ce
  qui sort est en clair, et la fiche copiée ne va ni dans l'historique du presse-papiers ni sur les autres appareils.
- « Enregistrer comme modèle… » : au clic droit d'une fiche ou de son texte, sous le nom proposé, celui de la fiche.
- Importer un dossier de notes (Obsidian, Typora, VS Code…) : un bouton dans la vue « Fiches », le clic droit sur la place libre de la
  liste, et la liste vide. On choisit d'entrer ou non en révision ; le bilan dit les fiches, les images, les liens et ce qui n'a pas été repris.
  Fermer la fenêtre laisse l'import finir : un refus est dit dans la vue, et son bilan attend la prochaine ouverture.
- Rétroliens et aperçu d'une fiche liée, comme dans la 0.x. Dans la vue d'une fiche, en lecture, le panneau « Fiches qui
  mènent ici » suit les passages : chaque fiche, son dossier, le texte autour de son lien, et un clic pour l'ouvrir. Faute
  de place, il s'ouvre par-dessus le texte, depuis le bouton « Rétroliens » ou le menu « ⋯ ». Au survol d'un lien vers une
  fiche, ou quand il reçoit le focus, un aperçu dit son dossier, son titre, ses étiquettes, le début de son texte et son
  étape ; Échap le referme, et un clic dessus ouvre la fiche. Un lien encore masqué par un trou, en révision, n'en montre
  aucun. Seules les fiches qui mènent ici sont lues, rien n'est enregistré, et le tout se relit quand on revient sur la
  fiche : un texte modifié, un renommage ou un déplacement se voient. Un disque devenu inaccessible se dit dans le panneau.
- Les examens arrivent sur « Aujourd'hui » : un examen par dossier (ou pour tout le coffre), avec son compte à rebours, le
  nombre d'éléments actifs et ceux prévus après lui ; « Examen… » en fixe un, « Modifier… » et « Retirer l'examen… » suivent,
  et « Resserrer… » ramène, après confirmation, les échéances avant sa veille. Les lignes se pilotent au clavier : Entrée sur
  un examen ouvre ses actions.
- La charge des 30 prochains jours, depuis le bouton « Charge » d'« Aujourd'hui » : une colonne par jour, les échéances
  connues pleines sous les étapes projetées dessinées, la moyenne, le jour le plus chargé. Un jour choisi montre ses
  révisions ; « Déplacer… » reporte une échéance, sur le jour le moins chargé à trois jours au plus, comme dans la 0.x. À
  l'étroit, les colonnes se rangent sur deux rangées pour rester visables.
- La barre de révision propose « Valider à une date passée… », entre le lendemain de la dernière révision et aujourd'hui : le
  calendrier repart de ce jour-là, et une révision en avance sur ce jour demande confirmation. « Reporter à une date… »
  vient s'ajouter à demain et à dans 3 jours, comme dans la 0.x.
- Un champ de date dans le design system (`DateField`) : une date qui se tape ou se choisit dans un calendrier en français,
  aux couleurs de chaque thème, nommé pour le lecteur d'écran ; hors des jours permis, elle est refusée : le champ se vide,
  dit pourquoi, par écrit et au lecteur d'écran, et aucune action ne part sur la date d'avant.
- Un dialogue refermé rend le focus à ce qui le tenait, et changer de coffre efface tout ce que les vues avaient lu de l'ancien.
- Coffre chiffré, lot 4 : la preuve qu'aucun texte en clair ne reste sur le disque. Sur un coffre chiffré, le menu d'un mot
  propose « Accepter jusqu'à la fermeture de kyfRev », dans le code Markdown, au lieu d'écrire dans le dictionnaire de Windows. Le journal masque tout chemin
  qui entre dans le coffre, et ne nomme plus la citation trouvée. Les moteurs web ne téléversent plus de rapport de plantage,
  et le moteur des extensions ignore à son tour les variables d'environnement `WEBVIEW2_*`. L'aide des exports dit lesquels
  sont en clair. `kyfrev-coffre` gagne `changer-mot-de-passe` et `cle-de-recuperation`, sur un coffre fermé. `docs/chiffrement.md`
  écrit le modèle de menace.
- Coffre chiffré, lot 3 : « Chiffrer ce coffre… », dans Paramètres › Données, pour un coffre en clair. Une page en trois
  étapes : le mot de passe, deux fois ; la clé de récupération, montrée une seule fois avant la conversion ; puis la
  conversion, coffre fermé, vers un nouveau dossier voisin (« Coffre chiffré »), avec les fichiers écrits puis relus.
  Le coffre en clair n'est pas touché, et c'est à l'utilisateur de le retirer. « Déchiffrer vers un dossier… », dans la
  carte Chiffrement, fait le chemin inverse, dans le dossier choisi. La conversion de `kyfrev-coffre` passe dans
  `KyfRev.Conversion`, avec sa progression et un chiffrement préparé en deux temps ; `kyfrev-coffre verifier` lit aussi
  un coffre chiffré, avec son mot de passe ou sa clé de récupération, sans rien écrire.
- Coffre chiffré, lot 3 : la carte Chiffrement, dans Paramètres › Données, pour un coffre chiffré. « Changer le mot de
  passe… » ne réécrit aucune fiche : seule l'enveloppe de la clé change, et la clé de récupération comme la clé retenue
  restent valables. « Créer une nouvelle clé de récupération… » la prépare sans rien écrire et la montre une seule fois :
  elle ne remplace l'ancienne qu'une fois rangée et enregistrée ; annulée, ou interrompue par un verrouillage, elle ne
  change rien. Enregistrée, l'ancienne n'ouvre plus le coffre, mais les sauvegardes et les copies faites avant s'ouvrent
  encore avec elle. « Se souvenir sur cet ordinateur » se coche (avec le mot de passe) ou se décoche. Chaque fenêtre
  accepte aussi la clé de récupération, quand le mot de passe est perdu. « Verrouillage automatique » se règle : 5, 15,
  30 minutes, une heure ou jamais ; le réglage est dans le coffre (`verrouillage.apresMinutes` de `.suivi/coffre.json`,
  écrit seulement s'il diffère des 15 minutes par défaut), et le verrouillage de lui-même le suit sans relire le coffre.
  « Verrouiller maintenant » fait comme Ctrl+Maj+L. La clé copiée ne va ni dans l'historique du presse-papiers ni sur les
  autres appareils. Un mot de passe changé pendant un verrouillage se dit à la prochaine lecture de ce coffre.
- L'installateur livre `vcruntime140.dll` à côté de l'application : sur un poste sans le Redistribuable Visual C++, la
  bibliothèque de chiffrement ne se chargeait pas, et aucun coffre chiffré ne pouvait se créer ni s'ouvrir.
- Les dialogues donnent le focus à leur premier champ, puis à leurs boutons : « Annuler » ne passe plus devant. Entrée
  sur une case à cocher choisit le bouton par défaut au lieu de la cocher ; Espace la coche.
- Coffre chiffré, lot 3 : le coffre chiffré se verrouille. Ctrl+Maj+L (ou « Verrouiller le coffre chiffré » dans la
  recherche) le fait à la demande ; il se verrouille de lui-même après 15 minutes sans rien toucher dans toute la session
  de Windows, ce qui se tape dans le texte mis en forme compris, et dès que Windows se verrouille ou que le poste se met en
  veille, même derrière l'écran de déverrouillage d'un autre coffre ; un départ pendant une ouverture reste dû. Ce qui
  s'écrit s'enregistre d'abord : le texte de la fiche (le texte mis en forme compris, jusqu'à sa dernière frappe), le
  commentaire ou la définition en cours, un schéma (jusqu'au champ qu'on n'a pas quitté), un dessin, une formule ou un
  lien en cours d'insertion, une définition en cours de modification (abandonnée si elle ne peut pas l'être) ; une
  séance en cours se garde pour être reprise.
  Si ce qui s'écrit n'a pas pu l'être, le coffre reste ouvert. Ce qui n'est qu'un formulaire (le titre d'une nouvelle
  fiche, un renommage, les réglages non enregistrés, la réponse d'une sélection guidée) est abandonné. Puis l'écran de
  déverrouillage remplace les vues, le coffre se ferme et sa clé est effacée, avec son index en clair, et chaque vue
  oublie ce qu'elle avait lu (listes, aperçus, définitions, corbeille, réglages, journal et pages des extensions, dessins
  des schémas). Seul son mot de passe ou sa clé de récupération le rouvre : la clé retenue sur ce poste ne sert qu'au
  démarrage, et plus à ce coffre une fois verrouillé, jusqu'à ce qu'on le déverrouille.
- Coffre chiffré, lot 3 : l'application ouvre un coffre chiffré. Au démarrage, depuis l'accueil ou depuis Paramètres ›
  Données, il attend son mot de passe sur un écran de déverrouillage, sans menu ni recherche : rien de lui n'est lu
  avant. Perdu, sa clé de récupération l'ouvre. « Se souvenir sur cet ordinateur », décoché par défaut, laisse Windows
  garder sa clé pour la session : le coffre s'ouvre ensuite sans rien demander, sur ce poste et dans ce dossier
  seulement, et une clé qui ne l'ouvre plus est oubliée. Fermé, le coffre efface la copie de sa clé. Un autre coffre peut
  être ouvert à la place, ou celui qui restait ouvert retrouvé.
- Coffre chiffré, lot 2 : la session du coffre, ses sauvegardes et son analyse passent par une couche de stockage. Sur un
  coffre chiffré, chaque fichier devient un objet chiffré et l'index chiffré tient les chemins et les dossiers : rien en
  clair sur le disque, pas même un fichier temporaire. Ses sauvegardes restent chiffrées, se restaurent en un coffre
  chiffré et rendent une fiche manquante. Les essais de la session, dont les treize scénarios comparés à la 0.x, tournent
  sur les deux stockages.
- Coffre chiffré : une conversion coupée à n'importe quelle écriture laisse la source intacte et aucune destination. Elle
  n'efface que son propre dossier temporaire, celui qu'elle a créé et trouvé vide ; le reste d'une autre conversion,
  jamais. Tant qu'un dossier qui porte le nom exact d'un dossier temporaire de la destination est à côté d'elle, marqué ou
  non, la conversion refuse sans rien écrire, le nomme et demande de le retirer à la main. Une source qui porte le
  marqueur d'une conversion, ou dont un dossier parent le porte, est refusée, par une jonction, un nom court 8.3 ou un
  lecteur substitué sur ce dossier. Un dossier temporaire qui ne s'efface qu'en partie reste, et le message le nomme.
- Coffre chiffré : la session relit l'index une fois le verrou pris, et ne perd plus ce qu'un autre processus a écrit
  entre-temps. Un effacement définitif qui ne peut pas retirer un objet va jusqu'au bout, puis nomme ce qui est resté ;
  les sauvegardes ne recopient que les objets que l'index nomme, un seul par objet, quelle que soit la casse de leur nom.
  Un objet manquant n'empêche plus la sauvegarde, ni la restauration des autres fiches.
- Lecture : un terme défini dans une fiche se marque dans toutes les fiches qui le contiennent, d'un pointillé discret, distinct
  de celui d'une définition posée à sa place. Il se trouve en mot entier, sans tenir compte des majuscules, au singulier ou au
  pluriel, une fois par partie, ni dans un titre, ni dans du code, ni dans un lien. Le survol montre sa définition ; un clic
  ou le clic droit ouvre sa fiche sur elle, ou cesse de le marquer. Rien n'est écrit dans les fiches, et rien ne se marque en
  révision, où le mot donnerait la réponse. Le réglage est dans Paramètres › Général, et chaque définition a sa case dans
  l'onglet Définitions, pour un mot trop courant (champ `partout` de la définition, écrit seulement quand il vaut faux).
- Extensions : le catalogue signé. « Voir le catalogue », dans Paramètres › Modules et extensions, lit à la demande le
  catalogue publié dans le dépôt, et ne le prend que si sa signature (ECDSA P-256) est bonne, par une clé que
  l'application connaît, et si son numéro n'est pas plus ancien que le dernier vu. Chaque fichier d'une extension est
  vérifié par sa taille et son empreinte SHA-256 avant l'installation, qui montre ce qu'elle demande. La clé privée reste
  sur le poste de la personne qui publie ; `tools/catalogue/catalogue.cs` la crée, signe et vérifie
  (`docs/extensions/catalogue.md`). L'application connaît les clés publiques `kyfrev-1` et `kyfrev-2` (la seconde est celle
  de secours).
- Écrire : un onglet « Tableau » apparaît dans le ruban quand le curseur est dans un tableau, et disparaît quand il en sort.
  Il règle l'en-tête (première ligne, première colonne, ou les deux), la dernière ligne qui ressort (une synthèse, un total),
  les bandes (lignes ou colonnes, qui suivent l'en-tête), les traits (tous, horizontaux, verticaux, contour, sous l'en-tête
  ou aucun), le thème (Sobre, Forêt, Ambre, Ardoise, Prune, Encre, lisibles en clair comme en sombre) et la largeur (pleine
  page par défaut, ou ajustée au contenu). Pour la colonne du curseur : son alignement, la cacher en révision (ses cases deviennent des
  trous, l'en-tête reste lisible) et trier les lignes de A à Z puis de Z à A. Il reprend aussi l'ajout et le retrait des
  lignes et des colonnes. L'apparence s'écrit dans une seule ligne au-dessus du tableau, `<!-- kyfrev tableau … -->`, que
  GitHub, Obsidian et VS Code n'affichent pas ; un tableau sans cette ligne garde son apparence, et la lecture, l'aperçu,
  la révision et l'éditeur dessinent la même chose. Les flèches passent d'une case à l'autre, ↑ et ↓ comprises, sans jamais
  poser le curseur entre deux cases. Un tableau prend toute la largeur du texte (« Ajustée au contenu » reste un choix), et
  ses colonnes gardent leurs mots entiers tant qu'il le permet, à l'écriture comme en lecture.
- Coffre chiffré : `kyfrev-coffre chiffrer` et `dechiffrer` convertissent un coffre vers un nouveau dossier, la source
  intacte. Chaque fichier est relu et vérifié par son SHA-256, et rien n'est écrit si un seul ne correspond pas. Le
  coffre chiffré ne garde aucun nom en clair : des objets opaques et un index chiffré. La clé de récupération s'affiche
  une seule fois.
- Coffre chiffré, lot 1 : le noyau (`KyfRev.Infrastructure.Crypto`, sans écran pour l'instant). Une clé de données de
  256 bits par coffre, enveloppée en AES-256-GCM par la clé du mot de passe (Argon2id par libsodium, réglé sur le poste,
  64 Mio et 3 passes au moins ; PBKDF2-HMAC-SHA256 en option) et par une clé de récupération en 14 groupes base32 qui
  refuse les fautes de frappe. Chaque fichier en AES-256-GCM, nonce neuf à chaque écriture, lié au coffre et à son
  identifiant. La clé peut être retenue par Windows pour la session (DPAPI). Formats décrits dans `docs/chiffrement.md`.
- Fenêtre : l'application dessine sa barre de titre, aux couleurs du thème, avec le nom, la recherche (Ctrl+K), le thème clair
  ou sombre en un clic, les raccourcis clavier et les boutons de la fenêtre. Windows garde le déplacement, le redimensionnement,
  son menu et ses dispositions sur « Agrandir ».
- Recherche (Ctrl+K) : on tape directement dans la barre de titre, et la liste des commandes s'ouvre juste dessous, comme un
  menu : un clic à côté la ferme. En plein écran, la barre revient le temps d'une recherche.
- Clic droit, avec les actions rapides en tête du menu : texte d'une fiche et passages (« Écouter d'ici » compris), lignes
  d'« Aujourd'hui », fiches archivées, définitions, lignes de « Santé du coffre », champs de texte et éditeurs, dont les
  corrections restent dans le menu.
- Suppression définitive : dans « Éléments supprimés », chaque élément peut être effacé pour de bon, après confirmation.
- Vue « Fiches » : clic droit sur les fiches (ouvrir, réviser, modifier, renommer, étiquettes, déplacer, archiver, supprimer)
  et sur les dossiers (nouvelle fiche, nouveau dossier, renommer, déplacer, supprimer). Une suppression peut aussi être
  définitive, en le cochant.
- Vue « Fiches » : une vraie arborescence, avec des dossiers dans les dossiers, qui se plient et se déplient, y compris les
  dossiers vides ; un avertissement au-delà de trois sous-dossiers.
- Extensions : elles s'installent depuis un dossier, s'activent et se retirent dans « Modules et extensions », après avoir montré
  ce qu'elles demandent ; celles de la 0.x sont reprises au premier lancement. Leur code tourne à part, sans réseau. Leur vue a son
  entrée dans le menu et s'affiche en éléments natifs : ni style, ni lien, ni image ne passent.
- Extensions, contrat 2 : une extension peut déclarer son icône et un compteur pour son entrée du menu, des commandes dans la
  recherche (Ctrl+K), des entrées au clic droit des fiches et des dossiers, et un encart sur « Aujourd'hui ». Sa vue gagne les
  formulaires (les champs partent ensemble à l'envoi) et les barres de progression. L'installation montre chaque permission sur sa
  ligne, avec son poids : une alerte pour celles qui voient le contenu des fiches, une alerte rouge pour l'écriture, et une mise à
  jour marque celles qui sont nouvelles. Chaque permission se retire ensuite d'une case, et le reste après une mise à jour. Les
  types de l'objet `kyfrev` sont publiés dans `docs/extensions/kyfrev.d.ts`. Les extensions de la 0.x fonctionnent sans changement.
- Extensions : leurs blocs se dessinent dans les fiches, en lecture, en révision et dans l'aperçu, en éléments natifs comme leurs
  vues ; un bouton d'un bloc passe par l'extension, puis le bloc se redessine. Dans l'éditeur, le bloc s'affiche en image, et son
  langage se choisit dans la liste. Une extension qui ne répond pas laisse le bloc lisible, sous sa forme de texte, avec la raison.
- Extensions : la sélection guidée. Une extension demande un texte dans une fiche (une question, puis sa réponse) : une barre montre
  sa consigne au-dessus de la fiche, et l'on prend la sélection, l'écrit à la main s'il n'y est pas, passe ou termine ; le temps ne
  court pas pendant qu'on répond. Elle peut aussi ouvrir une fiche à un passage, et ajouter ses entrées au clic droit d'un texte
  sélectionné, avec la permission « selection ».
- Extension Quiz, version 2 (dossier `extensions/quiz`) : on crée un quiz depuis son menu, Ctrl+K ou le clic droit d'une fiche, puis
  on prend chaque question et sa réponse dans la fiche, à la chaîne, ou on les écrit à la main. Un texte sélectionné en lecture
  s'ajoute au quiz par le clic droit. En partie, les questions ratées reviennent en premier, « Voir dans la fiche » rouvre le
  passage d'origine, et le menu comme « Aujourd'hui » disent combien sont à revoir. Les blocs ` ```quiz ` restent lisibles et
  deviennent des quiz en un clic ; le score de la version 1 est gardé.
- Extension Chronologie, version 2 (dossier `extensions/chronologie`) : des frises nommées. Un texte sélectionné s'y ajoute par le
  clic droit, sa date déjà proposée ; le clic droit d'une fiche en extrait les dates, à cocher. Les dates se rangent d'elles-mêmes,
  avant J.-C. comprises, chacune rouvre son passage, et « Remettre dans l'ordre » en fait un jeu. Sur « Aujourd'hui », « Ce
  jour-là » rappelle un événement d'un jour comme celui-ci. Les blocs ` ```chronologie ` restent lisibles et font une frise en un
  clic.
- Extension Vocabulaire, version 2 (dossier `extensions/vocabulaire`) : un carnet par langue. Un mot sélectionné s'y ajoute par le
  clic droit, sa traduction demandée et sa phrase d'origine gardée. Les mots reviennent à J1, J3, J7, J14 et J30, comme les fiches,
  un mot raté le lendemain ; la réponse se dévoile ou s'écrit (sans compter casse, accents ni parenthèses), dans un sens ou dans
  l'autre. Le menu et « Aujourd'hui » comptent les mots à revoir. Les mots des blocs ` ```vocabulaire ` entrent dans un carnet en
  un clic, et les résultats de la version 1 sont gardés.
- Extension Bilan du coffre, version 2 (dossier `extensions/bilan`) : il aide à corriger, plus seulement à constater. Chaque fiche
  à étoffer, sans étiquette ou sans intertitre s'ouvre d'un clic ; une étiquette s'ajoute sur place ; une fiche peut être ignorée,
  puis reprise. Le bilan de chaque semaine est gardé pour en montrer l'évolution, et « Aujourd'hui » dit combien de fiches sont à
  étoffer. Il change des étiquettes, jamais le texte.
- Extensions, pour en écrire : « Créer une extension… » (Paramètres, « Modules et extensions ») crée un dossier prêt à modifier,
  au contrat 2, avec un script d'exemple, les types de l'objet `kyfrev` pour l'éditeur et un mode d'emploi, puis l'installe après
  confirmation. Le mode développeur, retenu sur ce poste, montre sous chaque extension le dossier d'où elle vient, « Recharger »,
  qui reprend ses fichiers (une permission nouvelle passe par la confirmation), et ses vingt dernières erreurs. Le guide est
  dans `docs/extensions/guide.md`.
- Extensions : avec la permission « fiches:creation », une extension crée des fiches (titre, dossier, texte, étiquettes) et change
  leurs étiquettes ; avec « fiches:ecriture », elle remplace leur texte, l'en-tête gardé. Aucune ne peut supprimer une fiche.
  Chaque texte remplacé reste dans les versions de la fiche, d'où il se remet, et chaque écriture est notée au journal de
  l'extension, dans le coffre : la carte « Extensions » montre les trente dernières. Une extension écrit au plus soixante fois
  par minute.
- Schéma réseau : le switch de cœur et le switch de distribution rejoignent la bibliothèque, avec leur icône.
- Écrire : tous les dessins passent par le même éditeur, après le choix du modèle dans la galerie : les SmartArt en liste, la carte
  mentale sur plusieurs niveaux, les schémas Mermaid en liste, les graphiques en tableau de valeurs. Tout dessin inséré depuis la
  galerie se rouvre pour être modifié et peut changer de modèle ; un schéma écrit ou retouché à la main reste dans son code.
- Écrire : un vrai éditeur d'organigramme. Les éléments se composent dans une liste, sur autant de niveaux qu'il faut, et un
  élément peut en avoir sous lui sans toucher aux autres ; le dessin suit la frappe, et un clic sur une boîte la choisit.
  Le bloc reste du texte lisible partout : une ligne par élément, décalée de deux espaces par niveau.
- Écrire : clic droit sur un schéma, un graphique ou un SmartArt pour le modifier, voir son code ou le supprimer.
- Écrire : la feuille se détache du fond, comme dans un traitement de texte, dans les deux thèmes.
- Module Schémas réseau : la vue et l'éditeur de topologie de la 0.x (équipements, ports, câbles, VLAN, plan d'adressage,
  exports et import), dans un îlot du moteur web. Il s'active par coffre dans Paramètres, onglet « Modules et extensions ».
- Un bloc ` ```reseau ` se dessine dans les fiches, en lecture comme en écriture. Il s'insère depuis le ruban (Insertion, Réseau),
  et un double clic ou « Modifier » ouvre l'éditeur, qui revient à la fiche une fois fermé.
- Vue « Fiches » : glisser-déposer d'une fiche ou d'un dossier vers un autre dossier, avec un défilement automatique près
  du haut et du bas de la liste.
- Module Citations : la vue « Citations » (recherche, étiquettes, tri, ajout, modification, corbeille), la citation du jour
  sur « Aujourd'hui », cherchée sur Wikiquote si ce réglage est activé, l'onglet « Modules et extensions » de « Paramètres » pour
  l'activer sans redémarrer, et, dans l'onglet « Données », les citations supprimées et leur export.
- Vue « Paramètres », onglet « Général » : le thème, la taille de l'interface, le confort de lecture des fiches (largeur,
  taille du texte, interligne, police), les animations, le contraste élevé, les petits
  boutons agrandis, la voix et la vitesse de lecture. Ces réglages sont retenus sur l'ordinateur, hors du coffre, et le zoom
  réglé au clavier aussi.
- Vue « Paramètres » : elle passe en onglets par thème. « Révisions » reprend la séquence et les rappels ; « Données »
  regroupe le coffre, les sauvegardes, les éléments supprimés et les exports ; « À propos » dit la version, le mode, le coffre
  et la licence.
- Les listes de choix suivent le thème, y compris le thème sombre.
- Vue « Santé du coffre », comme dans la 0.x : chaque problème attend une décision et rien n'est réparé sans vous. Fiches
  manquantes à restaurer depuis une sauvegarde, fichiers en double, fichiers non suivis, liens cassés, passages à vérifier,
  et le diagnostic à copier pour un signalement.
- Vue « Statistiques », comme dans la 0.x : la part des révisions faites à l'heure, les révisions validées, les jours
  actifs, la série en cours et son record, le calendrier d'activité sur 52 semaines, puis le détail par mois et par dossier.
  Rien n'y est enregistré : tout vient de l'historique du suivi.
- L'installation dépose la licence de kyfRev, les avis de tiers, ceux des paquets NuGet publiés (runtime .NET compris) et les
  licences des polices dans le dossier `licences` de l'application ; la désinstallation les retire avec elle.
- La fenêtre d'insertion d'une formule montre son aperçu pendant la saisie, et propose des exemples à reprendre.
- Les images SVG d'une fiche s'affichent en lecture, comme images : aucun script ni aucune adresse qu'elles contiennent n'agit.
- Changer la place d'un passage depuis son menu, même perdu : on sélectionne le nouveau texte, puis on garde sa progression
  ou on repart de J0. La corbeille de la fiche est au bas du volet des passages, d'où tout se restaure.
- Le code d'une fiche est coloré en lecture, selon son langage, avec les couleurs du thème : C#, C et C++, CSS, HTML, Java,
  JavaScript et TypeScript, JSON, PHP, PowerShell, Python, SQL, TeX, Visual Basic, XML et les différences de fichiers.
- Écouter une fiche : le bouton « Écouter » de la lecture fait lire la fiche par les voix de Windows, hors ligne, bloc par
  bloc. Le bloc lu ressort et vient au milieu de la vue. Pause, bloc voisin, vitesse et voix se règlent pendant la lecture.
  Une formule et une image sont nommées, le code et les dessins sont passés. Pendant une révision à trous, la voix se tait :
  elle dirait ce qui est masqué.
- Vue « Paramètres », première partie : la séquence des étapes, le rappel actif et les rappels de consolidation. La vue
  montre aussitôt les dates que donne la séquence saisie, et dit avant l'enregistrement que les cycles en cours gardent
  leur calendrier. Ce qui ne se lit pas ou que le coffre refuse est expliqué, et la saisie reste.
- Vue « Définitions » : le glossaire du coffre, par dossier, avec une recherche qui ignore les accents et la casse, sur le
  terme comme sur l'explication. Chaque définition dit d'où elle vient et quand elle revient en révision, se modifie sur
  place, et « Voir dans la fiche » ouvre la fiche sur le passage. Une position à vérifier ou un passage perdu est signalé.
- Historique d'une fiche : le bouton « Historique » du ruban montre chaque version en face du texte actuel, ce qui diffère
  marqué d'une couleur et d'un signe, et permet de la remettre. Le texte actuel est d'abord gardé : rien n'est jamais
  effacé. Quand une fiche a changé sur le disque, « Comparer » montre les deux versions avant de trancher.
- Fiche en révision : le bouton « Réviser », ou une fiche ouverte depuis Aujourd'hui ou une séance. Avec le rappel actif,
  ses passages et son gras sont masqués jusqu'au clic, ou jusqu'à Entrée, pour se les rappeler avant de les relire. Le trou
  garde la largeur du texte, le volet tait aussi ce qui est masqué, et « Tout afficher » ou « Remasquer » le permettent d'un
  coup.
- Galerie de l'onglet Insertion, comme celle d'Office : des catégories, une vignette par modèle, un aperçu qui suit le
  texte saisi. 17 SmartArt que kyfRev dessine lui-même, en un bloc de code lisible partout (`smartart cycle`, puis un
  élément par ligne). 9 schémas et 6 graphiques en Mermaid. Un SmartArt se rouvre dans la galerie par « Modifier… ».
- Les schémas et les graphiques Mermaid se dessinent enfin, en lecture et à l'écriture, avec les couleurs du thème, dans
  une page cachée fermée, sans accès au coffre. Un dessin n'est qu'une image : aucun lien ni script n'y agit. Ce qui ne
  se dessine pas se lit en code.
- Mise en forme étendue : couleur et taille du texte, alignement (gauche, centre, droite, justifié) et listes de
  tâches, à la manière d'un traitement de texte. Ctrl+L, Ctrl+R et Ctrl+J alignent. Une tâche se coche à sa place, et
  « [ ] » suivi d'une espace en crée une. Une couleur choisie reste lisible dans tous les thèmes : sa teinte reste, sa
  clarté s'adapte, et le contraste élevé la remplace par celle du système. Les fiches restent en Markdown : couleur et
  taille s'écrivent `<span style="…">`, l'alignement `<div align="…">`, les tâches `- [ ]`. Seules ces formes exactes
  sont reconnues, tout autre HTML reste du texte. kyfRev 0.8.0 les affiche comme du texte, sans rien perdre.
- Écriture d'une fiche : le bouton « Écrire » ou Ctrl+E ouvre le texte mis en forme, qui se modifie
  directement à sa place, comme dans un traitement de texte. Le bouton « Afficher le Markdown » ouvre le code
  à gauche et son rendu à droite. Ce que l'on ne touche pas reste identique à l'octet dans le fichier. Un ruban
  à la manière d'Office accompagne les deux. L'onglet Accueil met en forme (gras, italique, souligné, barré,
  exposant, indice, casse, titres, listes), l'onglet Insertion ajoute un tableau à la taille choisie, un bloc
  de code avec son langage, un lien, des images, une formule ou une ligne. Le texte s'enregistre 1,5 s après
  la dernière frappe, et aussi en quittant l'écriture ou en fermant la fenêtre. Si la fiche change sur le
  disque, rien n'est écrit et aucune version n'est perdue : on choisit celle à garder, l'autre va dans
  l'historique. `[[` propose les autres fiches et écrit un lien ordinaire. Les images se collent, se déposent
  ou se choisissent. Le correcteur de Windows souligne les fautes et propose ses corrections au clic droit.
- Surlignage dans le texte : les passages paraissent en ambre et les définitions en pointillé vert, à leur
  place, même si la fiche a changé. On sélectionne du texte, puis la touche H surligne, et le clic droit
  propose Annoter et Définir. Un passage dont le texte a changé se confirme ou se garde sans position.
  Pendant un rappel, un passage surligné compte comme à revoir.
- Lecture native d'une fiche : le Markdown s'affiche enfin mis en forme, dans la fiche et dans son aperçu,
  avec ses titres, listes, citations, tableaux, formules, notes, liens et images du coffre. La fiche se
  lit en trois volets : son plan, son texte et ses passages. Quand la fenêtre rétrécit, le plan puis les
  passages se replient et s'ouvrent à la demande par-dessus le texte, avec les mêmes boutons. Un lien
  mène à une autre fiche ou au navigateur du système, sans jamais charger quoi que ce soit du web. Une
  fiche de 20 000 mots paraît aussitôt : son début d'abord, la suite juste après.
- Moteur de rendu des fiches (`KyfRev.Rendering`) : il transforme le
  Markdown en un arbre de contenu, en tire le texte affiché et y retrouve les passages. Il reproduit à
  l'identique le texte, les positions et le ré-ancrage de la version 0.x sur les 230 documents et les
  116 modifications du corpus d'ancrage, et sur 1 149 cas limites calculés par la 0.x elle-même.
- Vue « Fiches » : toutes les fiches du coffre, par dossier, à chercher par titre ou étiquette, à filtrer
  par dossier et par état, et à trier. La fiche choisie s'affiche à côté de la liste quand la place le
  permet ; sinon, un clic l'ouvre. « Nouvelle fiche » la crée dans un dossier, existant ou nouveau, vide
  ou d'après un modèle, puis l'ouvre.
- Vue « Archives », comme dans la version 0.x : les fiches archivées, avec leur état de rappel, leurs
  cycles terminés et leur dernier rappel. On les cherche par titre ou étiquette, on les filtre par
  dossier, état de rappel, date d'archivage et nombre de cycles, et on les trie. Une fiche peut y être
  relue, exclue des rappels ou réincluse, et remise en révision ; rien n'y est supprimé. Sur une
  fenêtre étroite, les filtres se replient derrière un bouton, et ceux qui sont choisis restent
  visibles en étiquettes.
- Icône de kyfRev : un éventail de fiches du vert à l'ambre, sur l'exécutable, ses fenêtres et
  raccourcis, et l'installateur, dont l'assistant a aussi ses images. Elle reste nette de 16 à
  256 px et sur les fonds clairs comme sombres.
- Installateur Inno Setup 6 (`tools/installer.ps1`), avec l'application autonome : rien à
  installer d'autre, pas même le runtime .NET. Sans droits administrateur, il installe kyfRev
  pour l'utilisateur en cours (`%LOCALAPPDATA%\Programs\kyfRev`) ou, au choix, pour tous. Il
  propose les raccourcis dans le menu Démarrer et sur le Bureau, et l'emplacement des fiches de
  chaque personne (Documents ou AppData). Son accueil donne la version et sa date de
  publication. Comme celui de la version 0.x, il met à jour sur place en gardant les choix de la
  première installation et refuse de remplacer une version plus récente (code de sortie 7). Il
  attend que kyfRev soit fermé, et sa désinstallation ne touche à aucun coffre ni réglage. En
  installation silencieuse, `/COFFRE=documents|donnees` et `/NOICONS` fixent ces choix.
  L'intégration continue construit l'installateur et l'essaie à chaque envoi : installation,
  lancement, mise à jour, retour en arrière refusé, désinstallation.
- Séance d'étude, repensée : « Commencer (N) » sur la liste du jour (en retard, puis
  aujourd'hui, filtrée et triée), la progression visible (« Séance : 5 / 12 »), « Passer » tant
  que rien n'a été fait puis « Suivant » (Ctrl+→), « Terminer la séance », et le mode révision
  plein écran (F11, Échap pour en sortir). La séance est gardée sur ce poste à chaque pas :
  quittée ou interrompue, elle se reprend le même jour (« Reprendre la séance (3 restants) »).
  Le bilan de fin donne la durée, les éléments faits, reportés et restants. Au style Veillée, la
  progression occupe toute la largeur en haut de la page, et les boutons passent sous elle sur
  fenêtre étroite.
- Journal de diagnostic `kyfRev.log`, comme celui de la version 0.x : il s'allonge d'un démarrage
  à l'autre (chaque démarrage marqué, avec la version, le mode et le système), consigne les
  arrêts inattendus et les erreurs des tâches de fond, et devient `kyfRev.precedent.log`
  au-delà de 1 Mo : deux fichiers au plus. Comme le prévoit le plan, la rotation se fait aussi en
  cours d'exécution, et le dossier du profil de l'utilisateur y est masqué (`%USERPROFILE%`).
  Installé, il est rangé dans `%LOCALAPPDATA%\kyfRev\v1`, à part de celui de la 0.x.
- Réponse aux rappels de consolidation, comme dans la version 0.x : une fiche dont le rappel
  attend sa réponse propose « Fiche connue », « Certains points sont à revoir » (les passages à
  revoir se remettent à J0, et il en faut au moins un) ou « Fiche à revoir entièrement… », avec
  confirmation. Rien n'est enregistré tant qu'aucune réponse n'est choisie : quitter la fiche ne
  vaut pas réponse. Surligner un nouveau passage viendra avec la lecture native.
- Fiche et passage en révision, au centre de la page, sur 720 px au plus, avec la barre de révision
  toujours visible en bas : la frise des étapes, l'étape attendue, « Reporter » (à demain ou de
  3 jours), les autres actions et « Valider ». Ouverts d'un clic (ou d'Entrée) depuis la vue « Aujourd'hui »,
  comme dans la version 0.x. La barre de révision montre l'étape attendue et son échéance, la
  frise des étapes du cycle, « Valider la révision » (Ctrl+Entrée), une confirmation avant une
  révision en avance, le report à demain ou de 3 jours, « Recommencer à J0… » et « Archiver… »
  avec confirmation ; un élément archivé se remet en révision, une fiche archivée s'exclut des
  rappels ou y revient. Un passage est une carte mémoire : l'explication d'une définition reste
  cachée jusqu'à ce qu'on la demande ; avec le rappel actif, le passage aussi, remplacé par un
  trou dans le texte qui l'entoure, son commentaire servant d'indice. « Voir dans la fiche » et
  « Retour » ramènent d'où l'on vient. Le texte d'une fiche est encore montré tel qu'il est
  écrit : son rendu natif vient avec la lecture native.
- Vue « Aujourd'hui », au style Veillée : le résumé du jour (ce qu'il y a à réviser, en retard,
  aujourd'hui, en rappel, et de quoi commencer la séance) puis la liste, en retard d'abord, puis
  aujourd'hui, les rappels de consolidation et les sept jours à venir, qui ne sont plus repliés
  d'emblée. À côté de la liste si la page fait au moins 880 px, à sa suite sinon : les échéances
  des sept prochains jours, les révisions validées ces sept derniers jours et les encarts des
  modules, ancrés en bas. Ces cartes n'apparaissent que si elles ont quelque chose à montrer. À
  partir de 1 550 px de page, la ligne choisie se lit dans un aperçu (son texte, les étapes de son
  cycle, ce qui a été révisé), sans rien dévoiler d'un passage caché ou d'une définition : un clic
  choisit la ligne, un double clic ou Entrée l'ouvre. À partir de 2 150 px, la liste se partage en
  deux cartes et les échéances portent sur 14 jours.
  Chaque ligne montre la nature de l'élément (fiche, passage, définition), son dossier, ses
  étiquettes, son cycle, sa dernière révision, son étape et son retard ; avec le rappel actif, un
  passage reste caché et seul son commentaire sert d'indice. Filtre (tout, fiches, passages), tri
  (échéance, étape J, nombre de cycles, dernière révision, titre) dans un menu, avertissements de
  « Santé du coffre » et de rappels en souffrance, états vides. Sous 600 px, la liste passe avant
  le reste : le tri se réduit à son icône, le pied de liste disparaît, le retard passe sous le
  titre. Les groupes se replient à la souris ou au clavier (Entrée, Espace),
  le focus restant sur l'en-tête. La vue est relue à chaque ouverture ; sa liste est
  virtualisée : 2 000 fiches en retard s'affichent en moins de 150 ms, calcul non compris.
- Ouverture du coffre au démarrage et accueil, comme dans la version 0.x : le coffre du mode
  portable (`portable.json`), sinon le dernier ouvert, sinon celui que prévoit le mode installé
  (`installation.json` : Documents ou données d'application). Sans coffre ouvert, l'accueil
  propose de créer le coffre prévu (ou, en mode installé, d'en reprendre un existant), ou de
  choisir un dossier pour créer ou ouvrir un coffre ; un coffre qui n'a pas pu être ouvert y est
  signalé. Le menu et les vues n'apparaissent qu'avec un coffre ouvert.
- Reprise de la version 0.x installée : son dernier coffre ouvert
  (`%LOCALAPPDATA%\kyfRev\config-locale.json`) est rouvert au premier lancement, sans que ce
  fichier soit jamais modifié ; kyfRev retient désormais le sien dans `%LOCALAPPDATA%\kyfRev\v1`.
- Tableau de bord et archives, identiques à la version 0.x (treizième scénario comparé,
  50 opérations) : retards du plus ancien au plus récent, révisions du jour (fiches d'abord),
  rappels en attente, les sept jours à venir, charge des 30 jours (échéances connues et étapes
  projetées), examens, prochaine échéance, compteurs et problèmes de « Santé du coffre » ;
  archives avec l'état des rappels (au repos, éligible, en attente, exclue) et le dernier rappel.
  La journée est préparée de nouveau si l'application est restée ouverte après minuit. Sur un
  coffre de 2 000 fiches, le calcul prend moins de 150 ms.
- Coquille de l'application (`KyfRev.Presentation.Wpf`, `KyfRev.App`) : menu latéral dans
  l'ordre de la 0.x (Aujourd'hui, Fiches, Définitions, Archives, Statistiques, modules actifs ;
  Santé du coffre et Paramètres en bas), recherche de commandes (Ctrl+K, sans tenir compte des
  accents), aide des raccourcis (F1), mode concentration (Ctrl+Maj+F), zoom de 80 à 200 %
  (Ctrl+plus, Ctrl+moins, Ctrl+0), Ctrl+1 à Ctrl+9 pour les vues. Navigation complète au
  clavier, focus toujours visible, focus rendu à sa place à la fermeture de la recherche. Les
  vues encore à porter présentent ce qu'elles feront.
- Coquille adaptative, du téléphone à l'écran large : le menu suit la largeur de la fenêtre
  (divisée par le zoom). Sous 600 px, les trois premières vues en onglets en bas et les autres
  sous « Plus », la recherche et l'aide en haut ; de 600 à 1 399 px, une colonne d'icônes
  libellées ; à partir de 1 400 px, la barre latérale. Rien ne défile horizontalement : les
  largeurs fixes des vues deviennent des maximums, et un dialogue trop haut fait défiler son
  contenu entre son titre et ses boutons.
- Apparence : le thème suit Windows (barre de titre comprise) ou le choix de l'utilisateur ; le
  contraste élevé et la réduction des animations de Windows s'appliquent sans redémarrage
  (`KyfRev.Infrastructure.Windows`).
- Galerie des composants dans l'application (`kyfRev --galerie`), et captures de la coquille.
- Accessibilité vérifiée par les règles automatiques d'Accessibility Insights (Axe.Windows) sur
  l'application lancée : chaque vue, l'aide et la recherche, en clair et en sombre, sans erreur.
  Les composants se présentent aux lecteurs d'écran (dialogue, notification annoncée, pastille,
  état vide, squelette) ; les icônes décoratives sont ignorées.
- Design system (`KyfRev.DesignSystem`, `docs/design-system.md`) à l'identité « Veillée »,
  choisie parmi plusieurs propositions : vert forêt et ambre, thème clair « Jour » teinté de
  sauge, thème sombre « Veillée » chaud, et contraste élevé de Windows ; 38 rôles de couleur.
  Polices Atkinson Hyperlegible Next et Mono, conçues pour se lire sans effort, livrées avec
  l'application et leur licence (SIL OFL 1.1). Échelles régulières (typographie et espacements
  sur une grille de 4 px, rayons doux de 12 à 24 px, ombres légères, contrôles de 40 px de haut
  au moins, durées de 220 ms au plus, aucune si Windows demande de réduire les animations).
  Les 51 paires de couleurs employées ensemble atteignent 4,5:1 (texte) ou 3:1 (contours et
  repères) dans chaque thème, vérifiées par les tests ; le contour des champs et boutons dépasse
  3:1, contre 1,29:1 dans la 0.x. Un retard est ambre, une erreur brique. Aucune couleur n'est
  écrite hors des jetons.
- Composants : boutons, champ avec indication, carte, pastille (icône et libellé), menu,
  dialogue, notification, état vide et squelette de chargement. Focus clavier toujours visible,
  textes dans des ressources traduisibles. Captures des trois thèmes dans `docs/captures/j5`.
- Modularité : contrat `IKyfRevModule` et catalogue des modules (`ModuleCatalog`). Un module est
  compilé avec l'application, sans aucun chargement dynamique ; ses services ne sont déclarés
  que si le coffre l'active, et son absence ne touche pas au cœur. Points d'extension : entrée
  du menu, encart du tableau de bord, remarque de « Santé du coffre », recherche, format
  d'export, rendu d'un bloc.
- Module Citations (`KyfRev.Modules.Citations`) : citations du coffre (liste, ajout,
  modification, corbeille), citation du jour, encart « Citation du jour » et signalement des
  fichiers de citation illisibles. Citation du jour, pages à lire et réglages identiques à la
  version 0.x (douzième scénario comparé, 82 opérations).
- Citation du jour sur le Wikiquote francophone, comme en 0.x : seulement si le module et la
  recherche en ligne sont actifs, trois pages au plus par jour, nouvel essai une heure après un
  échec, rien de plus une fois la journée sans nouveauté. Lecture des pages comparée à la 0.x
  sur 806 wikitextes (modèles, liens, notes, entités, références incomplètes) et 6 080 tirages
  de pages du jour. Ces comparaisons ont révélé un défaut de la 0.x, corrigé depuis : une
  lettre accentuée placée peu après un « & » y arrêtait la lecture de la page.
- Lecteur HTTPS durci (`KyfRev.Infrastructure.Online`), seul accès au réseau : adresse en HTTPS
  seulement, ni redirection ni cookie, réponse limitée à 2 Mo et 20 secondes, UTF-8 strict,
  agent identifié (politique de Wikimedia). Seul le titre de la page demandée part.
- Couche Application (`KyfRev.Application`) : 56 cas d'usage, un par commande de la version 0.x,
  regroupés par domaine (coffre, fiches, édition, dossiers, corbeille, révision, rappels,
  annotations, examens, réglages, modèles, santé, sauvegardes, exports). Ils passent par des ports
  (le coffre, les sauvegardes, la mémoire des coffres) et prennent la date du jour d'une horloge
  injectée (`TimeProvider`), dans le fuseau local. Un refus rend le message de la 0.x.
- À l'ouverture d'un coffre, comme en 0.x : la journée est préparée, une sauvegarde est faite si
  aucune ne l'a été ce jour-là, puis la rotation ; un échec est journalisé sans bloquer
  l'ouverture. Le coffre précédent n'est fermé qu'une fois le nouveau ouvert.
- Vue d'une fiche ouverte et état de révision d'une fiche ou d'un passage, identiques à la 0.x
  (onzième scénario comparé, 49 opérations).
- Tableau de correspondance des 97 commandes de la 0.x (`docs/correspondance-commandes.md`),
  vérifié par les tests.
- Tests d'architecture : dépendances entre couches, aucune opération publique au nom de
  suppression, effacement de fichiers limité aux trois cas prévus (rotation des sauvegardes,
  fichiers temporaires).

- Corpus d'ancrage « Markdown → texte affiché → positions » (`tests/fixtures/ancrage`), calculé
  par les sources mêmes de la version 0.x dans Microsoft Edge : 230 documents (fiches de
  démonstration, cas ciblés, documents tirés au hasard), 2 355 passages ancrés et 1 024
  ré-ancrages après modification. La lecture native (J7) devra le reproduire à l'identique ;
  l'outil `tools/anchoring-corpus` le régénère, à l'octet près.
- Les exports du coffre de référence (suivi, historique, annotations, citations) sont comparés à
  l'octet à ceux de la 0.x : chaque sorte d'événement de suivi y figure.
- Coffre, sauvegardes, exports et santé, identiques à la version 0.x :
  - sauvegardes `.zip` vérifiées après écriture, hors du coffre, sans le verrou ; sauvegarde
    quotidienne ; rotation (14 jours, 8 semaines, 24 mois) qui ne touche que les copies nommées
    exactement comme des sauvegardes, jamais un dossier de coffre ;
  - restauration du coffre entier dans un nouveau dossier, ou d'une fiche manquante avec ses
    ressources, sans jamais écraser ; archive refusée avant toute écriture si un nom sort du
    coffre, si la taille annoncée est démesurée ou si une entrée ment sur sa taille ou son
    contenu (CRC-32) ;
  - exports : suivi et historique en CSV pour Excel (protégés contre l'injection de formules),
    annotations en Markdown, citations en Markdown et CSV, coffre entier ;
  - partage d'une fiche : Markdown, archive avec ses images, texte, page web aux images intégrées ;
  - import d'un dossier de notes (Obsidian, Typora…) : liens `[[…]]` convertis, pièces jointes
    copiées sous un nom sûr, dossiers cachés et types exécutables écartés, rien d'écrasé ;
  - « Santé du coffre » détaillée (liens cassés, passages à vérifier), doublon résolu en gardant
    l'original, suivi recréé, annotations supprimées.
- Un dixième scénario (114 opérations) comparé à la 0.x, et le script
  `tools/interop/sauvegardes.ps1` : chaque version restaure à l'identique les sauvegardes de
  l'autre.
- Coffre, écritures complémentaires, identiques à la version 0.x :
  - annotations : passages et définitions, commentaire et définition modifiés, ré-ancrage,
    passage changé (avec ou sans retour à J0), corbeille, méthode des J propre à chaque passage ;
    l'analyse signale les fichiers d'annotations illisibles (jamais réécrits) et les annotations
    orphelines ;
  - rappels : tirage du jour (une seule fois par jour), réponses aux rappels, exclusion et
    réinclusion ;
  - réglages et examens : réglages vérifiés, module Schémas, examens par dossier, échéances
    ramenées avant l'examen ;
  - citations : ajout, modification qui garde les champs d'autres outils, corbeille, citation du
    jour trouvée en ligne retenue dans le coffre ;
  - schémas réseau : création, enregistrement avec détection de conflit, copie ;
  - modèles de fiche (intégrés et du coffre) et données d'extension.
- Trois scénarios de plus (178 opérations) comparés à la 0.x : les neuf scénarios donnent des
  coffres identiques à l'octet près.
- Coffre, écriture des fiches (`VaultSession`), identique à la version 0.x :
  - ouverture : verrou, reprise d'une opération interrompue, analyse qui suit les fiches
    déplacées, modifiées, renommées ou étiquetées hors de l'application ;
  - fiches : création, adoption d'un fichier existant, enregistrement avec versions une fois
    par session et conflits, corps seul avec en-tête conservé, versions gardées et restaurées,
    étiquettes, opérations de la méthode des J ;
  - organisation : changement de titre, déplacement, ressources, dossiers, avec réécriture des
    liens sous la protection du journal d'opération ;
  - corbeille : fiche ou dossier entier, restauration à côté d'une place reprise ;
  - confinement au coffre, y compris face aux liens symboliques et aux jonctions.
- Noms de fichiers, éditions d'en-tête et liens Markdown comparés à la 0.x sur 9 000 cas, et six
  scénarios (195 opérations) rejoués avec les mêmes identifiants : coffres identiques à l'octet près.
- Script `tools/interop/ecriture.ps1` : un coffre écrit par cette version se rouvre dans la 0.x
  sans aucun signalement.
- Coffre (`KyfRev.Infrastructure.Vault`), lecture fidèle du format 1 :
  - chemins validés selon les mêmes règles que la version 0.x ;
  - empreintes SHA-256 des octets bruts ;
  - écriture atomique (temporaire, écriture forcée sur le disque, mise en place sans écrasement) ;
  - verrou du coffre compatible avec la version 0.x, dans les deux sens ;
  - lecture et réécriture à l'octet près de tous les fichiers JSON du coffre ;
  - lecture de l'en-tête YAML des fiches.
- Les champs inconnus sont désormais conservés à tous les niveaux des fichiers.
- Un en-tête YAML qui contient plusieurs documents est refusé, comme en 0.x.
- Outil `kyfrev-coffre verifier` : analyse un coffre en lecture seule (fiches déplacées,
  modifiées, manquantes, en double ou sans suivi, fichiers illisibles, format plus récent,
  opération interrompue, annotations orphelines) et produit un rapport en texte ou en JSON.
- Coffre de référence produit par la version 0.x (chaque sorte de fichier, d'événement de
  suivi, de résultat de rappel et d'état d'ancrage) et script d'interopération du verrou
  (`tools/interop/verrou.ps1`).
- Domaine (`KyfRev.Domain`) : méthode des J (séquence, cycles, échéances depuis la révision
  réellement faite, report, antidatation, redémarrage), archivage, rappels de consolidation
  (éligibilité en mois calendaires, repos progressif, tirage quotidien reproductible avec
  priorité anti-oubli), contrôle d'intégrité d'un suivi relu et graine du jour. Le suivi est
  immuable : une opération refusée ne modifie jamais rien.
- Tests du domaine : scénarios datés de la conception, propriétés sur des suites d'opérations
  aléatoires, et comparaison avec le cœur de la version 0.x sur 10 000 suites d'opérations,
  3 000 tirages, des flux ChaCha8 et 5 000 calculs de dates (`tools/reference-vectors`).
- Tests sous MSTest et Microsoft Testing Platform ; couverture minimale de 95 % des lignes
  exigée par l'intégration continue pour chaque module, mesurée par ses propres tests ; tests de mutation (Stryker.NET, score minimal de 80 %)
  par `tools/mutation.ps1` ou par le workflow « Mutation » lancé à la demande.
- Socle du dépôt : solution .NET 10 (`KyfRev.slnx`), SDK figé, configuration commune de
  compilation et d'analyse (avertissements bloquants, style imposé), gestion centralisée des
  paquets avec fichiers de verrouillage et audit des vulnérabilités.
- Gouvernance : crochets git et intégration continue qui vérifient l'identité des commits, le
  nom des branches, les fusions sur `main` et l'absence de secrets ; détection des raccourcis
  interdits (tests désactivés, avertissements masqués, exceptions avalées).
- Documentation : plan de refonte validé, glossaire du domaine, règles de contribution,
  politique de sécurité.

### Modifié

- Fenêtre : 470 × 540 au minimum à la taille normale, la disposition à onglets en bas gardée. Le minimum grandit avec le
  zoom de l'interface, qui s'arrête à ce que l'écran peut tenir et revient au zoom choisi sur un écran plus grand.
- Écrire : quand la place manque, le ruban laisse place à une barre courte (annuler, style, gras, italique, et selon la
  place la liste, rétablir et le lien). « Mise en forme » ouvre le reste dans un volet qui défile, avec ses onglets ; dans un
  tableau, un bouton « Tableau » ouvre ses réglages. L'historique, le Markdown, l'aperçu, le plan et les passages passent
  sous « ⋯ ». L'en-tête de la fiche tient alors sur une ligne : le retour, l'enregistrement en icône, les trois modes groupés
  avec leur icône. Plus aucun bouton ne passe sous un autre.
- L'application est compilée pour Windows x64 (`win-x64`), comme elle est livrée ; les
  bibliothèques restent indépendantes de la plateforme.
- Un dialogue ou une notification qui se ferme de lui-même garde la liaison de son ouverture :
  il peut se rouvrir quand le modèle de vue le demande.
- Grands coffres plus rapides : les liens cassés sont relevés sans relire les fiches qui n'ont
  pas changé (date et taille du fichier), et la vérification qu'un chemin reste dans le coffre
  n'examine plus que ses composants sous la racine réelle du coffre. Un chemin qui passe par un
  lien symbolique ou une jonction est toujours vérifié chemin par chemin.
- Le texte aux règles de Rust (minuscules, ordre des octets UTF-8) et le modèle des citations
  passent dans `KyfRev.Application`, partagés par le coffre et les modules.
- L'examen d'un dossier se « retire » (`WithdrawExam`) : plus aucun nom de suppression dans les
  opérations publiques.

### Sécurité

- Outil de référence (`tools/reference-vectors`, jamais livré) : bibliothèque `zip` passée en
  9.0.5, qui corrige une faille critique de lecture des archives (GHSA-m665-v7pm-7grj), comme
  dans la version 0.x.

### Corrigé

- Extensions : après une sélection guidée, une extension a de nouveau toute sa patience pour finir. Elle n'avait que le
  reste de celle qui courait, et pouvait être arrêtée à tort si la réponse tombait à la fin.
- Écrire : un processus graphique ou utilitaire du moteur web, relancé par Windows, n'arrête plus le texte mis en forme ;
  sans moteur WebView2 sur l'ordinateur, le message le dit, et le journal de diagnostic garde la raison de chaque arrêt.
- Fenêtre redimensionnée : un texte n'est plus réduit à quelques lettres par ligne entre des voisins qui gardent toute
  leur largeur. La barre de révision, les fiches archivées, la corbeille, la santé du coffre et la consigne d'une sélection
  guidée passent l'un sous l'autre dès que le texte perdrait sa place, mesurée, et non plus à partir d'une largeur fixe.
- Schémas réseau et éditeur visuel : plus d'éclair blanc à l'ouverture, en thème sombre surtout ; le moteur prend le fond du
  thème avant sa première page. L'îlot des schémas ne s'arrête plus quand il se ferme pendant qu'il répond.
- Recherche (Ctrl+K) : la liste reste sous le champ quand la fenêtre change de taille, et un clic ailleurs, même dans la barre
  de titre, la ferme ; l'icône de l'application accompagne son nom dans la barre de titre.
- Extensions : désactivée, une extension n'a plus aucune permission, et la carte le montre ; la réactiver repose la question,
  avec la même confirmation qu'à l'installation.
- Lecture : le plan marque la partie qui est en haut de la vue. En haut d'une fiche, il ne saute plus au titre suivant quand
  celui-ci est proche ; tout en bas, le dernier titre, qui ne peut plus monter, est marqué.
- Lecture : le numéro d'une liste n'est plus coupé à gauche, quels que soient sa longueur, la taille du texte et la police.
- Le curseur devient une main sur tout ce qui se clique, y compris les trous et les passages du texte.
- En révision, « Afficher le passage » et un clic dans le volet découvrent le passage masqué.
- Le passage visé depuis le volet ressort nettement dans le texte, dans les deux thèmes.
- L'indicateur de sélection au clavier reste dans le volet des passages et dans le plan.
- Les onglets ont un survol arrondi, comme le reste de l'application.
- Écrire : un document caché ne se construit plus (le rendu masqué se refaisait à chaque pause de frappe), et les images
  du coffre se lisent hors du fil de l'interface. Le passage en écriture bloque moins l'interface.
- Les dialogues de suppression définitive montrent un bouton distinct, qu'Entrée ne déclenche pas.
- Un clic sur un trou du texte l'affiche, et un clic sur un passage surligné le fait ressortir : le relâchement de la souris
  n'arrivait plus sur le texte visé.
- L'aperçu des fiches et d'« Aujourd'hui » replie son historique : il ne coupe plus le bas du texte.
- Archives : le survol couvre toute la ligne, et l'état et les actions tiennent sur la ligne du titre.
- Dans un champ de saisie, le texte commençait 12 pixels plus loin que le texte indicatif : il sautait à la première frappe.
- Sur une fenêtre étroite, les boutons d'une fiche passaient les uns sous les autres : ils vont à la ligne.
- Le texte d'un champ de plusieurs lignes ne colle plus au bord haut.
- Le libellé long d'une case à cocher était coupé : il passe maintenant à la ligne, en face de sa case.

[Non publié]: https://github.com/KyllianFF/kyfRev-dotnet/compare/v1.0.0-beta.1...HEAD
[1.0.0-beta.1]: https://github.com/KyllianFF/kyfRev-dotnet/releases/tag/v1.0.0-beta.1
