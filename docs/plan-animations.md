# Animations d'interface : plan et liste des éléments

État au 21 novembre 2026. Les maquettes animées sont sur le canevas, rangée 19
(19A les trois cas prioritaires, 19B les blocs, 19C les nombres et la révision,
19D les réglages). La rangée 18 montre ce qui est déjà construit.

## Règles

1. **Deux durées** : rapide 120 ms, normale 220 ms (80 ms pour l'appui). Décélération
   douce, jamais de rebond, jamais de boucle.
2. **Le rendu seul est animé** : opacité, translation, échelle, découpe. La mise en page
   change une fois, avant ou après ; elle n'est jamais recalculée à chaque image.
3. **Ce qu'on lit ne bouge pas sans raison.** Entre Lire et Réviser, le texte est le
   même : rien ne s'estompe. Un bloc qui fait de la place à un autre bouge une fois.
4. **Un fondu de départ est plus court que celui d'arrivée** (120 ms contre 220 ms). Un bloc qui
   glisse pour se cacher met le même temps qu'à venir (220 ms) : on doit le voir partir.
5. **Les fenêtres natives ne s'animent pas** : l'éditeur d'Écrire et l'îlot des Schémas
   sont des pages web posées dans la fenêtre, que l'application ne peut ni estomper ni
   faire glisser. Ce qui les entoure s'anime. L'éditeur habille lui-même son texte de sa feuille, en fondu,
   sur la durée que l'application lui donne avec le thème (« --duree-entree »).
6. **Une seule porte** : toute animation passe par `Transitions.Moves`, avec sa famille. C'est là
   que se lisent le choix « Aucune », le réglage de Windows et les familles coupées.
7. **Une transition lancée pendant que l'application travaille part quand la nouvelle vue est
   affichée** (`Transitions.WhenShown`) : partie au clic, elle serait finie avant la première image ;
   attendre que l'application soit au repos la retarderait de plusieurs secondes sur une longue fiche.
8. **Redimensionner la fenêtre n'anime rien** : un bloc qui passe sous un autre parce
   que la place manque change d'un coup, comme aujourd'hui.

## Réglages (maquette 19D) : faits

Dans Paramètres › Apparence, la carte « Animations » remplace la case « Réduire les animations » :

- un choix en tête : **Toutes**, **Essentielles** (retour au toucher et sélection),
  **Aucune** ; « Aucune » est l'ancien réglage, et le fichier de préférences existant
  (`animationsReduites`) se relit tel quel ;
- si Windows demande moins d'animations, aucune ne joue : c'est dit dans la carte, sans interrupteur
  de plus ;
- **un interrupteur par famille** qui a déjà des animations, sept aujourd'hui (`animationsCoupees`
  dans les préférences). Chaque animation appartient à une famille et se coupe avec elle, sans
  redémarrer. « Nombres et jauges » et « Révision » auront le leur quand leurs animations existeront.

| Famille | Ce qu'elle couvre |
| --- | --- |
| Retour au toucher | survol et appui des boutons, coches, interrupteurs, état « Enregistré » |
| Sélection qui glisse | menu de gauche, onglets, sections des Paramètres, modes |
| Changement de vue et de mode | page qui arrive, Lire / Réviser / Écrire, ruban, élément suivant en séance |
| Menu et volets | menu réduit ou élargi, volets Plan, Passages et Liens, groupes et dossiers repliés, barres |
| Menus, fenêtres et avis | menus, listes déroulantes, dialogues, palette Ctrl+K, cartes de mot et de lien, avis |
| Listes | ligne ajoutée ou retirée, aperçu qui change, résultats de recherche |
| Nombres et jauges | compteurs, barres de la semaine et des statistiques, progression |
| Révision | trous révélés, étape validée, surlignage |
| Animation d'ouverture | les fiches du logo au lancement |

Construction : `Moves` reçoit la famille (`MotionKinds`) ; `DesignResources.MotionOff` garde les
familles coupées à côté de `ReducedMotion`, et les pose dans les ressources (`Kyf.Motion.Off`).
Une famille coupée donne une durée nulle, donc le chemin « sans animation » déjà essayé partout. Pas d'interrupteur par animation :
il y en aurait une quarantaine, et la page deviendrait illisible.

## Liste des éléments à animer

Priorité 1 : vu à chaque séance. Priorité 2 : vu chaque jour. Priorité 3 : finition.
« Fait » : dans main, ou dans la demande de fusion des correctifs.

### Retour au toucher

| Élément | Aujourd'hui | Proposé | Priorité |
| --- | --- | --- | --- |
| Boutons, coches, interrupteurs | animés | | fait |
| État « Enregistré » en écriture | texte et icône échangés | fondu (120 ms) | 2 |
| Capture rapide : « Noter » | le dialogue se ferme, rien ne confirme | un avis « Noté » | 2 |
| Pastilles Tout / Révision / Notes du Carnet | fond sec | fond lissé (120 ms) | 3 |
| Survol des lignes des Archives, des colonnes de la Charge à venir | sec | fond lissé | 3 |
| Bordure des champs au survol et au focus, bouton « Filtres » | sèche | lissée | 3 |

### Sélection qui glisse

| Élément | Aujourd'hui | Proposé | Priorité |
| --- | --- | --- | --- |
| Cadre du choix du menu et des sections des Paramètres | absent à la première ouverture, ne suit pas la largeur | présent, suit le menu, glisse | fait |
| Trait des onglets du ruban | ne glissait que dans un sens | glisse dans les deux | fait |
| Choix Lire / Réviser / Écrire | sautait | le cadre glisse d'un mode à l'autre | fait |
| Choix dans la liste du Carnet et d'Aujourd'hui | saute | le cadre glisse (19B) | 2 |

### Changement de vue et de mode

| Élément | Aujourd'hui | Proposé | Priorité |
| --- | --- | --- | --- |
| Lire ↔ Réviser | le texte clignote | rien ne bouge | fait |
| Ruban à l'entrée en Écrire | d'un coup | descend de 8 px en fondu, son trait d'onglet avec lui | fait |
| Feuille de l'éditeur à l'entrée en Écrire | surgissait après un blanc | la fiche lue reste à l'écran jusqu'à ce que l'éditeur ait son texte ; sa feuille vient alors l'habiller en fondu | fait |
| Élément suivant en séance | toute la fiche change d'un coup | l'ancienne s'efface, la nouvelle arrive en fondu | 1 |
| Bilan de fin de séance | surgit | monte de 8 px en fondu | 2 |
| « Ouverture… », squelettes → contenu (fiche, Aujourd'hui, Statistiques, Santé) | échange sec | fondu | 2 |
| Paramètres : titre de la section | change avant le contenu | entre avec le contenu | 2 |
| Aide F1 : contenu d'un onglet, texte d'une section du guide | échange sec | fondu | 3 |
| Onglets Tableau et Image du ruban | apparaissent et disparaissent sec | fondu de l'onglet | 3 |
| Aller à un titre, à un passage, au trou suivant | saut | défilement accompagné (220 ms), à décider | 3 |

### Menu et volets

| Élément | Aujourd'hui | Proposé | Priorité |
| --- | --- | --- | --- |
| Menu réduit, élargi, masqué | d'un coup | le bord glisse, la page suit | fait |
| Volets Plan, Passages, Liens (en colonne et par-dessus le texte) | d'un coup | glissent depuis leur bord et y retournent ; le texte se recentre une fois | fait |
| Barre de révision en passant à Écrire, et au retour | d'un coup | descend pour se cacher, remonte pour revenir | fait |
| Groupes d'Aujourd'hui, dossiers du Carnet | lignes d'un coup, chevron échangé | le chevron tourne, les lignes se replient | 1 |
| Barres des trous, de l'écoute, de la révision guidée | surgissent | glissent depuis leur bord, comme la barre de révision | 2 |
| Historique dans l'aperçu, filtres, étiquettes, diagnostic, options d'un module | se déplient d'un coup | se déplient (220 ms) | 2 |
| Carte « Nouvelle définition », carte en cours d'édition | pousse la grille d'un coup | se déplie | 2 |
| Bandeaux et alertes dans une page | d'un coup | se déplient en poussant la suite | 2 |
| Menu montré au survol en mode concentration | d'un coup | fondu | 3 |

### Menus, fenêtres et avis

| Élément | Aujourd'hui | Proposé | Priorité |
| --- | --- | --- | --- |
| Menus, listes déroulantes, dialogues, avis | animés | | fait |
| Palette Ctrl+K | carte d'un coup, hauteur qui saute à chaque lettre | carte en fondu, hauteur gardée pendant la frappe | 1 |
| Carte d'un mot défini, carte d'un lien survolé, suggestions après « [[ » | d'un coup | comme les menus (92 % → 100 %, 220 ms) | 2 |
| Cadre du réglage trouvé par la recherche | posé puis retiré sec | fondu à l'arrivée et au départ | 3 |
| Avis « Passage surligné » dans la fiche | texte remplacé | fondu, puis s'efface | 3 |

### Listes

| Élément | Aujourd'hui | Proposé | Priorité |
| --- | --- | --- | --- |
| Fiche ajoutée | animée (trois au plus) | | fait |
| Ligne reportée, archivée, supprimée, mise à la corbeille | part d'un coup | s'efface, la liste se referme | 1 |
| Aperçu du Carnet et d'Aujourd'hui | remplacé d'un coup | fondu | 1 |
| Résultats de la recherche (palette, réglages) | liste remplacée | fondu | 3 |
| Entrée de menu d'un module activé | s'insère d'un coup | arrive en fondu | 3 |
| Fiche déposée dans un dossier | disparaît, reparaît ailleurs | la ligne arrive en fondu à sa place | 3 |

### Nombres et jauges

| Élément | Aujourd'hui | Proposé | Priorité |
| --- | --- | --- | --- |
| « 17 éléments à réviser », pastilles, « 3 / 14 » | changent sec | l'ancien sort par le haut, le nouveau arrive par le bas | 1 |
| Progression de séance, téléchargement de la mise à jour | saute | la jauge avance | 1 |
| Barres de la semaine, des 7 jours, de la Charge, des Statistiques | sautent | poussent à l'arrivée, grandissent au changement | 2 |

### Révision

| Élément | Aujourd'hui | Proposé | Priorité |
| --- | --- | --- | --- |
| Trou découvert, « Tout afficher », définition affichée | d'un coup | fondu (120 ms) | 1 |
| Valider : étape de la frise, coche, « Révisé aujourd'hui » | échanges secs | l'étape se remplit, la coche se dessine, le trait avance | 1 |
| « Passer » qui devient « Suivant » | échange sec | fondu | 2 |
| Surlignage d'un passage, passage actif | fond posé sec | la couleur s'étend de gauche à droite | 2 |
| Coches des premiers pas, des échantillons de papier | sèches | la coche arrive comme les autres | 3 |

### Ce que je propose de ne pas animer

- L'éditeur d'Écrire et l'îlot des Schémas, et leur échange avec le Markdown ou l'aperçu.
- Les blocs posés au-dessus de l'éditeur (conflit, avis, ruban réduit) : le bloc s'estompe,
  l'éditeur prend sa place d'un coup.
- Tout ce qui change parce que la fenêtre change de taille.
- La barre de titre en plein écran : la fenêtre change d'état au même instant.
- Le zoom, le papier d'une fiche, l'aperçu d'impression. (Le changement de thème, d'abord écarté, bascule en fondu : rang 10 de la liste « Fluidité ».)

## Fluidité : ce qui reste à gagner (maquettes, rangée 20)

Relevé après l'essai 11. Les animations d'arrivée sont en place ; ce qui donne encore une impression de coupure, c'est ce
qui part d'un coup, ce qui saute pour faire de la place, et l'application occupée pendant qu'elle anime.

| Rang | Point relevé | Modification proposée | Maquette |
| --- | --- | --- | --- |
| 1 (fait, à la demande : quand le pointeur ou le clavier arrive sur « Écrire ») | L'éditeur se crée à chaque entrée en écriture : 170 à 430 ms d'attente, pendant lesquels l'application est occupée et les animations sautent des images | Le créer d'avance, caché, dès que la fiche s'ouvre en lecture ; le garder d'une entrée à l'autre | 20A |
| 2 (fait, l'alignement seulement en haut de la fiche) | Le ruban pousse le texte d'un coup en paraissant ; au relais entre la fiche lue et l'éditeur, le texte change de place | Le texte descend avec le ruban (220 ms) ; la lecture et l'éditeur posent le premier bloc au même endroit, le titre de la fiche s'efface vers le haut | 20A |
| 3 (fait, sauf les menus : Windows retire leur fenêtre dès qu'ils se ferment, il n'y a plus rien à estomper ; sauf la vue quittée, qui disparaît d'un coup : seule la vue qui arrive bouge (elle monte de 28 px et se pose en 300 ms, sur la courbe des pages de Windows, mesurée sur PowerToys : 0,1 ; 0,9 ; 0,2 ; 1) ; et sauf le ruban, qui part de nouveau d'un coup en quittant l'écriture : resté à l'écran le temps de s'effacer, il se superposait aux barres de la lecture et gardait un éditeur qui n'existait plus) | Rien de ce qui part n'est animé : vue quittée, dialogue, menu, avis, ruban en quittant l'écriture | Un départ en 120 ms, sur une courbe qui accélère ; une vue arrive du côté où l'on va. Un avis garde son texte en s'effaçant, le ruban ce qu'il montrait ; rien de ce qui part ne reçoit plus de clic. L'image de la vue quittée est bornée à 1,5 million de points, pour ne pas retarder le changement sur un très grand écran | 20B |
| 4 (fait, en lecture et en révision ; en écriture, la fenêtre de l'éditeur ne peut pas glisser) | Un volet glisse, mais le texte saute à sa nouvelle place | Le texte glisse en même temps (position affichée animée, une seule mise en page) | 20C |
| 5 (fait pour le Carnet, les Définitions et les Citations : une à trois lignes visibles retirées, rien d'autre de changé) | Une ligne retirée disparaît et la liste remonte d'un coup | Elle s'efface (120 ms), la liste se referme (220 ms) | 20C |
| 6 (fait en lecture et en révision : titre du plan, passage, trou suivant, lecture à voix haute ; de loin, la vue saute d'abord tout près) | Aller à un titre, à un passage, au trou suivant : un saut | Défilement accompagné de 220 ms au plus | 20D |
| 7 (fait : le squelette rend sa place et s'efface sous le contenu, qui paraît en fondu ; huit pages) | Squelette remplacé d'un coup par le contenu | Fondu croisé de 220 ms | 20D |
| 8 (fait : à chaque fois qu'une vue montre sa liste pour la première fois, sauf si une ligne nouvelle y arrive ; les six premières lignes l'une après l'autre, les suivantes avec la sixième, pour que le bas de la liste ne soit pas là avant le haut ; pas pendant que la page de la liste arrive : un seul mouvement à l'écran) | Une liste entière paraît en bloc | Courte cascade : 35 ms d'écart, six lignes au plus | 20C |
| 9 (fait : deux courbes ; la troisième durée attend l'essai) | Une seule courbe pour tout, deux durées | Une courbe d'arrivée et une de départ ; une troisième durée (320 ms) pour les grandes surfaces, si l'essai le demande | 20B |
| 10 (fait : l'image de la fenêtre dans l'ancien thème s'efface par-dessus le nouveau ; l'éditeur et les schémas, fenêtres natives, changent d'eux-mêmes) | Le thème bascule d'un coup | Fondu des couleurs en 120 ms | 20D |
| 11 (fait : jauges et progression de séance qui avancent, compteurs dont l'ancienne valeur sort par le haut, barres de la semaine, de la Charge et des Statistiques qui poussent, étape validée qui se remplit avec sa coche et son trait, « Passer » qui devient « Suivant » en fondu ; les trous découverts et les passages surlignés changent d'un coup : animer la couleur d'un morceau de texte refait la mise en page de toute la fiche à chaque image ; la définition demandée et « Révisé aujourd'hui » paraissent en fondu, les coches des premiers pas et des papiers grandissent comme les autres) | Compteurs, jauges, étape validée, trous révélés : secs | Voir « Nombres et jauges » et « Révision » plus haut | 19C |
| 12 (pas fait : il faut d'abord une mesure sur un vrai écran, que je n'ai pas) | Les blocs qui glissent sont redessinés à chaque image | Les mettre en image le temps du trajet (`BitmapCache`), si une mesure montre que c'est utile | — |

Les rangs 1 et 2 comptent le plus : ce sont eux qui séparent encore l'entrée en écriture d'un seul mouvement continu.

## Plan d'action

Une demande de fusion par lot, chacune avec ses essais (`InterfaceMotionTests`),
ses captures de 470 px à l'ultra-large en clair et en sombre, et un installateur d'essai.

1. **Correctifs** : cadre du choix, glissement réel, Lire ↔ Réviser, ruban, bord du menu.
2. **Réglages par famille** (fait) : la porte `Moves` par famille, les préférences, la page
   Apparence. Tout ce qui suit naît avec son interrupteur.
3. **Quatre outils communs**, petits et sans mise en page par image : montrer ou cacher
   un bloc en le faisant glisser (`Transitions.Shown`, fait), le déplier ou le replier (découpe), remplacer un contenu (fondu), retirer une ligne, faire changer un
   nombre ou une jauge. Chacun arrête proprement l'animation précédente s'il est relancé.
4. **Menu et volets** : volets de la fiche, groupes, dossiers, barres, bandeaux.
5. **Listes, aperçus, palette, cartes flottantes.**
6. **Nombres, jauges, révision, séance.**
7. **Finitions** : retours au toucher manquants, et le défilement accompagné s'il est retenu.

### Garde-fous

- Rien ne s'anime sur un élément qui n'est pas à l'écran ; ce qui est lancé pendant que
  l'application est occupée attend d'être affiché (`EnterOnceShown`).
- Chaque animation se termine sans rester accrochée (`FillBehavior.Stop`) : l'état final
  est celui de la mise en page, pas celui de l'animation.
- Dans une liste, trois lignes animées au plus à la fois ; au-delà, tout paraît d'un coup.
- Durée nulle (réglage, Windows, famille éteinte, essais) : le même code, sans animation.
- Chaque outil a un essai qui le relance en cours de route et un essai à durée nulle.
- Aucune animation ne part d'un gestionnaire de mise en page sans condition qui l'arrête : une
  mise en page qui relance une animation, laquelle redemande une mise en page, fait fermer
  l'application par WPF à la cent cinquante-troisième passe. Par sûreté, un élément qui lance
  plus de trente animations d'affilée, sans que l'application ait repris la main, est coupé
  (`Transitions.LoopCut`) et le journal dit lequel.
- Aucune animation par image sur le texte d'une fiche (couleur d'un `Run`) : chaque image
  referait la mise en page du document.
- L'image de la fenêtre (ancien thème) est prise sur la place que la mise en page lui
  donne, pas sur le contour de ce qu'elle dessine : un essai compare ses points à ceux de la fenêtre.
- Une page qui arrive est seule à bouger, et ne passe jamais sur la barre de titre, le menu ou les
  onglets : un essai le vérifie à 470, 1000 et 1500 px, en clair et en sombre.
