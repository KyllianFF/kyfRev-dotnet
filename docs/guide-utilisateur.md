# Guide de kyfRev

kyfRev range vos fiches de révision en Markdown et vous les présente au bon moment, selon la méthode des J : J0, J1, J3, J7,
J14 et J30. Chaque étape tombe un nombre de jours après la révision réellement faite. Une fiche terminée est archivée, puis
rappelée de temps en temps pour la consolider.

Tout se passe sur votre ordinateur. kyfRev ne se connecte que si vous le lui demandez.

## Installer

Deux possibilités, sans droits administrateur :

- **L'installateur**, `kyfRev-<version>-installation.exe`. Installez pour vous seul : c'est le choix proposé. Vos fiches vont
  dans `Documents\kyfRev`, ou dans vos données d'application si vous préférez.
- **La version portable**, `kyfRev-<version>-portable.zip`. Décompressez le dossier `MesRévisions` où vous voulez, clé USB
  comprise, et ouvrez `Application\kyfRev.exe`. Vos fiches sont dans `MesRévisions\Coffre`. kyfRev n'écrit rien dans votre profil.

Ces fichiers ne sont pas signés : Windows peut afficher un avertissement SmartScreen. Choisissez alors « Informations
complémentaires », puis « Exécuter quand même ».

WebView2, le moteur du texte mis en forme, est déjà présent sur Windows 11. S'il manque, l'installateur l'ajoute ; sans lui,
l'écriture s'ouvre en Markdown.

### Mettre à jour

Paramètres › À propos › « Rechercher une mise à jour » regarde si une version plus récente est publiée. Installé, kyfRev la
télécharge aussitôt et vérifie qu'elle est bien celle annoncée et signée, puis demande quand l'installer : « Redémarrer
maintenant » (kyfRev se ferme, s'installe sans rien vous demander et se rouvre) ou « Plus tard » (elle s'installe quand vous
fermez kyfRev). Vos fiches, vos réglages, le dossier et les raccourcis ne changent pas. Installé pour tous, Windows demande
l'autorisation d'un administrateur.

Avec « Rechercher une mise à jour au démarrage », coupé au départ, kyfRev regarde à chaque ouverture et vous propose
« Mettre à jour » ou « Retarder à la prochaine ouverture » ; rien ne se télécharge sans votre accord. La version portable,
elle, ne se met pas à jour seule : kyfRev ouvre la page de la nouvelle version.

### Désinstaller

Windows › Paramètres › Applications retire kyfRev. Vos fiches et vos sauvegardes ne sont jamais supprimées : le coffre reste
où il est. Le désinstallateur demande ensuite ce qu'il faut faire de vos réglages, c'est-à-dire ce que kyfRev retient sur
cet ordinateur hors du coffre (le dernier coffre ouvert, l'apparence, les raccourcis, les extensions installées, la clé
retenue d'un coffre chiffré) :

- **Supprimer mes réglages**, proposé d'abord : une prochaine installation repart de zéro. Elle ne rouvre pas votre coffre
  d'elle-même (rouvrez-le par « Ouvrir un coffre existant »), et un coffre chiffré redemande son mot de passe.
- **Conserver mes réglages** : une prochaine installation retrouve votre coffre et vos choix.

Seuls vos réglages sont concernés : ceux des autres personnes de l'ordinateur restent dans leur profil. Une désinstallation
sans question (silencieuse) les conserve, sauf avec l'option `/REGLAGES=supprimer`.

### Depuis une version 0.x

kyfRev reprend votre coffre sans conversion. Au premier lancement, il ouvre le dernier coffre de la 0.x et y écrit aussitôt.
**Faites d'abord une copie du dossier du coffre.** Une fois satisfait, Paramètres › À propos vous propose de retirer la 0.x ;
votre coffre n'est pas touché. N'installez pas « pour tous les utilisateurs » dans le dossier de la 0.x : l'installateur le refuse.

## Le coffre

À l'ouverture, kyfRev montre brièvement son logotype : les fiches se feuillettent, la dernière s'écrit, le nom arrive.
La toute première fois l'animation prend quelques secondes, ensuite un peu moins de trois ; un clic ou une touche la passe, et
elle ne joue pas quand les animations sont réduites (Paramètres › Apparence).

Pour voir kyfRev en marche avant d'écrire, l'accueil propose « Essayer avec un coffre d'exemple » : dix fiches inventées
sur six matières, déjà en cours de révision. Il se crée dans son propre dossier « Exemple », à côté du coffre prévu, et
ne touche jamais à vos fiches. Pour créer ou rouvrir votre coffre ensuite : Paramètres › Coffre et chiffrement. Dans un
coffre neuf, la carte « Premiers pas » d'Aujourd'hui propose aussi « Ouvrir un coffre d'exemple » : le même, au même
endroit ; votre coffre est seulement fermé, et se rouvre par Paramètres › Coffre et chiffrement › « Ouvrir un autre coffre… ».

Un coffre est un dossier : des fichiers Markdown et, dans `.suivi`, le calendrier de vos révisions. Ne modifiez pas `.suivi` à la
main. Paramètres › Coffre et chiffrement permet d'en ouvrir ou d'en créer un autre.

## Réviser

- **Aujourd'hui** liste ce qui est à réviser : fiches, passages, définitions, rappels. « Commencer » enchaîne le tout.
- En révision, le texte surligné et les mots en gras peuvent être masqués, pour vous forcer à les retrouver. Validez une fois la
  fiche relue, ou reportez-la.
- Une fiche sans gras ni passage ne montre d'abord que ses titres : rappelez-vous chaque partie, puis affichez-la (Entrée,
  Espace ou un clic). « Tout afficher » sert à relire seulement.
- **« J'avais oublié »**, à côté de « Valider » : la révision compte, mais l'étape recule d'un cran au lieu d'avancer (après un
  oubli à J7, la fiche revient à J3 trois jours plus tard). « Recommencer à J0 » reste pour repartir de zéro.
- **Surlignez un passage** ou **créez une définition** depuis le texte : ils ont leur propre calendrier.
- **Masquez des zones d'une image** : clic droit sur l'image, en lecture, puis « Masquer des zones… ». Tracez un rectangle
  sur chaque zone à retrouver ; en révision, elles sont cachées et se dévoilent une à une. Au clavier : Inser ajoute une
  zone, les flèches la déplacent, Maj et les flèches changent sa taille, Suppr la retire. Le même clic droit existe en
  écriture, dans le texte mis en forme.
- **Réglez une image** : en écriture, cliquez sur l'image ; tirez ses poignées pour la redimensionner, ou passez par
  l'onglet « Image » : « Rogner », hauteur et largeur en centimètres, place, zones masquées et description. Le fichier de l'image n'est pas modifié : « Rétablir » retire ces réglages.
- La barre de révision permet aussi de valider à une date passée, ou de reporter à une date choisie.
- La première fois qu'une fiche s'ouvre en révision, une bulle au-dessus de « Valider » dit ce qu'il fait. « Compris » la
  ferme pour de bon ; « Ne plus montrer d'aide » coupe toutes les aides de la première fois.
- **Examens** : sur Aujourd'hui, « Examen… » fixe une date pour un dossier. « Resserrer… » ramène les échéances avant la veille,
  et peut remettre en révision les fiches archivées du dossier. Tant que l'examen n'est pas passé, une révision ne place
  jamais l'étape suivante après sa veille.
- **Dossier ou étiquette** : sur Aujourd'hui, la liste à côté de « Tout / Fiches / Passages » restreint ce qui est à réviser,
  et la séance avec.
- **Suspendre** (dans « ⋯ » de la barre de révision) met une fiche ou un passage de côté sans rien perdre : il ne se présente
  plus à réviser, et attend dans « Suspendues », sur Aujourd'hui, jusqu'à « Reprendre ».
- **Zoom de la fiche** : le curseur en bas à droite, de 50 à 200 %. La taille de toute l'interface se règle à part, dans
  Paramètres › Apparence.
- **Nouvelle fiche** : dans la vue Carnet, un volet s'ouvre à droite (par le bas en fenêtre étroite), le Carnet restant
  visible. Le titre en haut ; « Dans … » pour choisir le dossier ; fiche de révision ou note ; ses étiquettes ; puis un modèle, dont la
  vignette montre l'allure (son contenu se lit au survol). Entrée crée la fiche, Échap ou un clic à côté referme le volet.
- **Capture rapide** : Ctrl+Alt+N (à activer dans Paramètres › Raccourcis), où que l'on soit, ou Ctrl+Maj+N dans kyfRev :
  une palette s'ouvre au-dessus de tout. Entrée note l'idée dans la Boîte de réception, Ctrl+Entrée en fait une fiche ;
  les flèches choisissent une autre action (note, recherche, réviser, ouvrir kyfRev). Échap la ferme.
- **Barre d'état** : Paramètres › Démarrage et barre d'état. Fermer peut réduire kyfRev près de l'horloge, et kyfRev peut
  se lancer avec Windows. Pour le quitter vraiment : clic droit sur son icône, « Quitter kyfRev ».
- **Confirmations** : ce qui a abouti se dit dans une pastille au bas de la page, trois secondes ; la souris dessus la
  retient, Échap la ferme.
- **Une simple note** : « Nouvelle note », dans la vue Carnet. Elle s'écrit et se range comme une fiche, mais
  n'entre pas dans les révisions. La pastille « Notes » de la vue Carnet les montre ; « Mettre en révision », sur la note, en
  fait une fiche à apprendre, et « En faire une note » (dans « ⋯ ») fait l'inverse.
- **Lisser les échéances** (Paramètres › Révisions, coupé par défaut) pose les étapes longues sur un jour voisin moins chargé.
- **Charge** montre les 30 prochains jours, et permet de déplacer une échéance vers un jour moins chargé.
- **Étaler…**, sur Aujourd'hui : après une absence, gardez pour aujourd'hui les révisions les plus fragiles et répartissez les
  autres sur les jours suivants, sans dépasser le nombre par jour que vous donnez ; ou annoncez une pause, et ce qui tombait
  pendant ces jours part après votre retour. Le plan s'affiche avant d'être appliqué. Ce sont des reports : rien ne recommence.

## Écrire

Dans une fiche, « Écrire » ouvre le texte mis en forme ; le code Markdown s'affiche à côté si vous le demandez. L'enregistrement est
automatique, et l'historique garde les versions précédentes. Le ruban insère images, tableaux, formules, dessins et schémas.
- Dans une formule, `\ce{…}` écrit de la chimie : `$\ce{2H2 + O2 -> 2H2O}$`, `$\ce{SO4^2-}$`.
- Les tailles sont en points, comme dans un traitement de texte : 12 pour le texte, 20, 16 et 14 pour les titres 1 à 3 ;
  le titre 4 a la taille du texte, en italique. « Taille du texte des fiches », dans les Paramètres, grandit le tout.
- Une flèche dans la marge, à côté d'un titre, replie ou déplie tout ce qui est sous lui, sous-titres compris
  (Alt+Maj+« - » et Alt+Maj+« + »). Le repli n'est pas enregistré dans la fiche.

L'onglet « Mise en page » du ruban donne une feuille à la fiche : sans fin (par défaut), A4, A5 ou A6, en portrait ou en
paysage, avec des marges étroites, normales, larges ou sur mesure. Le choix vaut pour toutes les fiches ou pour la fiche
ouverte seule, et il est retenu sur cet ordinateur. Avec une feuille, la fiche se lit et s'écrit en pages : un bloc qui
dépasserait le bas d'une page commence la suivante, un tableau court sur plusieurs pages sans qu'une ligne soit coupée, et
une image ou un schéma trop haut rétrécit pour tenir sur une page. Sous la fiche, « Ajuster à la largeur » règle le zoom
pour que la feuille tienne dans la fenêtre. Avec une feuille, l'onglet Insertion propose « Saut de page » (la suite commence la
page suivante) et « Page blanche » (une page entière reste vide) ; un clic droit sur leur marque propose de la supprimer ; un tableau coupé répète son en-tête en haut de chaque
page, et une citation coupée dit « suite ».

Ctrl+P ouvre « Imprimer » : l'imprimante, le nombre de copies, les feuilles (toutes, ou certaines par leurs numéros :
« 1-2, 4 »), le recto verso (bord long ou bord court), le format du papier (A4, A5 ou A6, par défaut celui de la fiche),
l'orientation, les marges (par défaut celles de la fiche) et les numéros de page en bas, au format 1/4. L'aperçu montre
chaque feuille telle qu'elle sortira, et se refait quand vous changez de papier. Une fiche restée en feuille sans fin
s'imprime en A4. L'impression est toujours en thème clair, sur fond blanc.

Le groupe « Papier » de l'onglet Mise en page pose une feuille de papier derrière le texte : unie, à petits carreaux, à
grands carreaux, lignée, à points ou millimétrée. Le choix vaut pour toutes les fiches du même type de feuille (sans
fin, A4, A5, A6) ; le bouton à côté du zoom l'allume et l'éteint. C'est un décor de l'écran : il ne s'imprime pas.

Sur un papier réglé, le texte se pose sur les lignes, en lecture, en révision et en écriture : une ligne de texte par
ligne du papier (une sur deux sur les petits carreaux, les points et le millimétré). **Entrée** laisse une ligne vide,
**Maj+Entrée** passe à la ligne du dessous. Un tableau, une image ou un bloc de code prennent la place qu'il leur faut ;
le texte reprend ensuite sur la première ligne entière. Un tableau a un fond plein, pour rester lisible.

Paramètres › Feuilles règle tout cela pour toutes les fiches : la feuille, l'orientation, les marges, le papier de chaque
type de feuille, le zoom des fiches et la **police d'écriture** (sans empattements, avec empattements, manuscrite, au
feutre, à chasse fixe). Chaque papier a sa police conseillée, qu'un bouton applique. Un aperçu montre ce que cela donne.

Dans Paramètres, les sections sont à gauche, rangées par portée : ce qui est retenu sur cet ordinateur (Apparence,
Lecture, Feuilles, Menu, Raccourcis), ce qui appartient au coffre ouvert (Révisions, Modules et extensions, Coffre et
chiffrement, Sauvegardes et exports), et À propos. Apparence règle le thème, la taille de l'interface, le texte des fiches
(taille, interligne, police, largeur de la colonne) et l'accessibilité ; Lecture, les mots définis et la voix. Raccourcis change dix raccourcis
clavier ; Menu change l'ordre des vues du menu, et celles qu'il montre : décochez une vue pour la retirer du menu, elle
s'ouvre toujours par la recherche (Ctrl+K) et par son raccourci Ctrl+chiffre, qui suit ceux des vues montrées ; Aujourd'hui
reste toujours. « Chercher un réglage », en haut de la liste, trouve un réglage par quelques lettres, ouvre sa section et l'y entoure. Dans une
section retenue sur l'ordinateur, « Rétablir les réglages d'origine » remet ceux de cette section ; « Annuler » les rend tant
que vous y restez.

## Retrouver

- **Carnet** montre vos dossiers à gauche et les fiches du dossier choisi au milieu, rangées sous le nom de chaque
  sous-dossier ; « À réviser » réunit celles du jour et celles en retard. Glissez une fiche sur un dossier pour l'y ranger.
  En fenêtre étroite, le bouton au-dessus de la liste ouvre les dossiers. La recherche porte sur tout le coffre, dans le
  texte, sans tenir compte des accents ni de la casse.
- **Ctrl+K** cherche partout : commandes, fiches, passages, définitions, citations.
- Le panneau **« Fiches qui mènent ici »** montre les liens entrants ; survolez un lien pour voir un aperçu de sa fiche.

## Échanger

- **Partager** une fiche : page web, PDF, Markdown, archive avec images, texte, presse-papiers.
- **Importer** un dossier de notes, Obsidian par exemple, depuis la vue Fiches. Les liens `[[…]]`, les encadrés `> [!note]` et le
  surlignage `==…==` sont convertis ; les commentaires `%%…%%` ne sont pas repris, et le bilan le dit.
- **Exporter** le suivi, les annotations, les citations ou le coffre complet depuis Paramètres › Sauvegardes et exports.
- **Réviser ailleurs** : au même endroit, « Passages et définitions pour Anki » donne un fichier à importer dans Anki ou
  AnkiDroid, et « Échéances pour un agenda » un fichier `.ics`. Ce sont des copies à sens unique : ce que vous faites
  ailleurs ne revient pas dans kyfRev. L'agenda contient les titres de vos fiches.

Ce qui sort est en clair, même depuis un coffre chiffré, sauf le coffre complet.

## Protéger

- **La corbeille** garde ce que vous supprimez. Seule une suppression définitive, confirmée, l'efface.
- **Les sauvegardes** se font chaque jour à l'ouverture, dans le dossier `Sauvegardes` à côté du coffre.
- **Santé du coffre** signale les fichiers déplacés, les liens cassés et les annotations à vérifier.
- **Chiffrer** : Paramètres › Coffre et chiffrement › « Chiffrer ce coffre… » crée une copie chiffrée dans un nouveau dossier. **Notez la clé de
  récupération** : sans elle et sans le mot de passe, le coffre est perdu. Le coffre en clair reste en place tant que vous ne le
  retirez pas. Ctrl+Maj+L verrouille à tout moment. Ce que le chiffrement protège, et ce qu'il ne protège pas, est écrit dans
  [le modèle de menace](chiffrement.md#le-modèle-de-menace).

## Ajouter

Paramètres › Modules et extensions active les citations du jour et les schémas réseau, et installe des extensions, depuis un dossier
ou depuis le catalogue signé. Une extension n'a jamais le réseau, et demande chaque permission.

La vue Schémas réseau montre chaque schéma en carte : son aperçu, son nom, une ligne (« 10 équipements · 4 pages ·
modifié hier ») et ses étiquettes ; « Cité dans 3 fiches » et « 1 point à vérifier » ne paraissent que s'il y a lieu. Un
clic sur la carte ouvre son détail. Au survol, ou quand la carte a le clavier, « Modifier » ouvre l'éditeur et « ⋯ » le
reste des actions, les mêmes qu'au clic droit (ou touche Menu) : Renommer, Épingler, Étiqueter…, Dupliquer, Ajouter à un
schéma…, Enregistrer comme modèle…, Copier l'image, Exporter…, Imprimer…, Archiver, Supprimer…. Les petits traits au bas
de la vignette sont les pages du schéma : au survol, la vignette les montre l'une après l'autre. Un double clic sur le
nom (ou F2) le renomme sur place : Entrée garde, Échap annule. Un schéma épinglé reste en tête, sous « Épinglés ».

Au-dessus des cartes, la recherche trouve un schéma par son nom, ses équipements, leurs adresses, ses étiquettes et les
fiches qui le citent. Les puces filtrent par étiquette (un schéma qui porte l'une des étiquettes choisies) et par « À
vérifier » (le diagnostic y trouve un point) ; « Trier » range par date de modification, par nom ou par taille ;
« Grille » et « Liste » changent d'affichage, et le choix est retenu. La liste est un tableau (pages, équipements,
étiquettes, diagnostic, fiches, date) : un clic sur un en-tête trie par cette colonne, un second dans l'autre sens.
« Étiqueter… » pose et retire des étiquettes ; celles déjà en usage se proposent en tapant. Elles sont écrites dans le
schéma lui-même, comme l'épingle : une version précédente de kyfRev les perd si elle réécrit le schéma.

Plusieurs schémas se choisissent : la case au coin de la vignette (au survol), Ctrl+clic pour en ajouter un, Maj+clic pour
prendre toute une suite, Ctrl+A pour tout ce qui est affiché, Échap pour lâcher. Une barre paraît en bas : réunir en un
schéma, ajouter à un schéma, étiqueter, exporter (un fichier par schéma, dans le dossier choisi une fois), archiver,
supprimer (après une question). « Nouveau schéma ▾ » crée un schéma vide, depuis un modèle, depuis un fichier draw.io, ou
en réunissant des schémas. Dans une fenêtre étroite, les cartes vont par deux, « Importer » est sous « ⋯ » et le reste
des filtres, le tri et l'affichage sous « Filtres ».

Un schéma s'archive ou se supprime au clic droit sur sa carte, sous « ⋯ », ou depuis la barre des schémas choisis.
Archivé, il quitte la liste mais reste montré par les fiches qui l'insèrent ; supprimé, il va à la corbeille des schémas.
Les deux se retrouvent sous la liste et se restaurent : rien n'est effacé. Dans l'éditeur d'un schéma, plusieurs éléments choisis
(Maj ou Ctrl + clic, ou un cadre tracé à la souris) se groupent avec Ctrl+G et se manipulent alors ensemble ; un second clic
sur l'un d'eux le choisit seul, et Ctrl+Maj+G les dégroupe.

L'éditeur d'un schéma a deux modes, à choisir dans son en-tête. « Simplifié » va vite : on glisse un équipement depuis le
plateau du bas, on tire un câble d'un équipement à un autre sans choisir de port, on donne un nom et, si l'on veut, une
adresse IP à un équipement, un type et un nombre de câbles à une liaison, et l'on pose du texte où l'on veut. « Avancé »
règle le reste : interfaces, VLAN, vues, diagnostic. C'est le même schéma dans les deux. Sur une liaison, « Agrégat de
liens » regroupe plusieurs câbles (ils se voient tous, sous un cercle) et « Trunk » demande ses VLAN, non tagué et tagués,
écrits sur le câble (« Trunk · U 1 · T 10,20,30 ») ; les deux se cumulent. Au clic droit sur la carte d'un schéma, ou dans son Détail : « Exporter… », « Partager » (son image
va dans le presse-papiers) et « Imprimer… ». Un export draw.io montre le schéma comme kyfRev, dans la vue montrée et
avec l'affichage choisi : noms et adresses, étiquettes des ports et des câbles, câbles parallèles côte à côte, agrégats
sous leur cercle ; avec les icônes de kyfRev ou les formes réseau de draw.io. Il porte aussi le modèle complet : c'est lui
qui revient à l'import, avec ce qui a changé dans draw.io.

Un schéma peut avoir plusieurs pages, comme dans draw.io : Étage 1, Étage 2, Salle serveurs… Les onglets sont sous le plan,
avec le nombre d'équipements de chaque page ; « + », « Nouvelle page » en ajoute une. Au clic droit sur un onglet : renommer, dupliquer,
déplacer, supprimer (après une question ; Ctrl+Z la rend, et elle reste dans les versions du schéma). Un double clic
renomme, glisser un onglet le déplace. Dans une fenêtre étroite, les onglets se replient en un menu. Un câble entre deux
pages se crée par la boîte de connexion détaillée (les équipements y sont rangés par page), ou à la main : prenez le câble
(« Brancher un câble » dans la petite barre d'un équipement, ou l'outil Câble), choisissez son premier port, puis cliquez
l'onglet d'une autre page (ou choisissez-la dans le menu des pages) : le câble reste en main, et la consigne le rappelle
(« Câble en main depuis Cœur (Étage 1) : choisissez un équipement, sur cette page ou une autre »). Choisissez l'équipement
et son port, comme d'habitude ; Échap repose le câble. Un câble entre deux pages se dessine sur chacune en renvoi, en
pointillé : « → Étage 2 · SW-Étage2 · 1/48 », la page, l'équipement et le port d'en face. Un clic sur le renvoi mène à
l'autre page ; tirée, la pastille se déplace (au clavier : les flèches, Maj pour aller plus vite), et sa place est gardée
pour chaque bout du câble ; « Remettre à sa place », sous la pastille choisie, la remet à côté de son équipement. Les vues
valent pour la page montrée ; le plan IP et le diagnostic regardent tout le schéma. Une image exportée ou imprimée montre la
page à l'écran ; un fichier draw.io garde toutes les pages, une page draw.io par page, et revient avec elles.

Les pages se parcourent aussi sans ouvrir l'éditeur. Dans le Détail d'un schéma, de petits onglets, au-dessus de
l'aperçu, montrent chaque page (au clavier : les flèches, Début et Fin) ; un renvoi cliqué dans l'aperçu mène à sa page,
et ce qui s'exporte, se partage ou s'imprime depuis le Détail est la page montrée. Dans une fiche, l'aperçu montre la
première page, et un petit sélecteur, juste au-dessus, à droite, en choisit une autre (au clavier aussi, avec les
flèches) : elle reste choisie, et s'imprime, jusqu'à la fermeture de kyfRev. Un schéma à plusieurs pages s'écrit au
format 3 : une version de kyfRev qui ne connaît pas les pages refuse de l'ouvrir plutôt que de le relire en une seule.

Des pages passent d'un schéma à l'autre. Dans l'éditeur, « + » sous le plan propose « Nouvelle page » ou « Pages d'un autre
schéma… » : choisissez le schéma, puis ses pages sur leurs vignettes, et « Les copier » (le schéma d'origine ne change pas)
ou « Les déplacer » (elles le quittent). Elles arrivent après les vôtres, la première montrée, et Ctrl+Z les retire toutes
en une fois ; le schéma d'origine, lui, garde ses versions. Le chemin inverse est au clic droit sur un onglet : « Copier
vers un autre schéma… », « Déplacer vers un autre schéma… » (Ctrl+Z la rend ici), « Copier dans un nouveau schéma »
(il prend le nom de la page ; celle-ci reste ici) et « Détacher en un nouveau schéma » (la page devient un schéma à part
entière et quitte celui-ci ; Ctrl+Z l'y rend, le nouveau schéma restant dans la liste ; un schéma d'une seule page ne se
détache pas). Un câble ne va pas d'un schéma à l'autre : entre une page qui part et une page qui
reste, il ne suit pas, et le compte rendu le dit ; entre deux pages qui partent ensemble, il suit. Un nom de page déjà
pris est précédé du titre du schéma d'où elle vient (« SAMES Meylan · Stains »). Les VLAN se retrouvent par numéro (en cas
de noms différents, celui du schéma qui reçoit est gardé, et le compte rendu le dit) et les modèles de matériel par
référence.

« Réunir des schémas » en fait un seul, nouveau, à plusieurs pages (dans l'éditeur : « Plus », « Réunir avec d'autres
schémas… »). Chaque schéma d'une page y devient une page à son titre ; un schéma à plusieurs pages garde les siennes.
L'ordre des pages se glisse (au clavier : Alt+flèche haut ou bas). Un équipement qu'on retrouve dans deux schémas, de
même nom et de même adresse, reste en double par défaut, un sur chaque page ; sur demande, il n'en fait qu'un, et ses
câbles vers l'autre page deviennent des renvois. Les VLAN se réunissent par numéro (en cas de noms différents, le premier
schéma l'emporte, et la fenêtre le dit), les modèles de matériel par référence. Les originaux ne sont jamais modifiés ;
« Archiver les schémas d'origine », coché par défaut, les range aux archives, et les fiches qui les insèrent les montrent
toujours. Pour revenir en arrière : supprimer le nouveau et désarchiver les autres.

La recherche cherche aussi dans tous les schémas du coffre (les archivés compris, marqués « archivé » ; pas la corbeille),
à partir de deux caractères : un équipement par son nom ou son nom d'hôte, une adresse IP entière, son début
(« 10.0.30. ») ou un réseau (« 10.0.30.0/24 »), un VLAN par son numéro (« 30 », « vlan 30 ») ou son nom. Les résultats
sont rangés en équipements, adresses et VLAN, six par groupe (« N de plus » montre le reste) ; chacun a son icône, son
adresse et « Schéma › Page ». Un clic, ou Entrée, l'ouvre, l'équipement choisi. Au clavier, flèche bas depuis le champ
entre dans les résultats, les flèches s'y déplacent, flèche haut sur le premier revient au champ. Une même adresse portée
par deux équipements, dans deux schémas ou dans un seul, est marquée « même adresse », avec un conseil (« 10.0.30.5 sert
dans deux schémas : à vérifier s'ils sont sur le même réseau. ») : rien n'est bloqué, ce peut être deux réseaux distincts.
Dans l'éditeur, la recherche (Ctrl+F) a deux portées : « Ce schéma » et « Tous les schémas » ; un résultat d'un autre
schéma ouvre celui-ci à la place, enregistré d'abord.

Un nouveau schéma peut partir d'un modèle : « Petit bureau » (box, switch, Wi-Fi, quatre postes), « Étage type » (switch
d'étage, bornes, prises), « Site distant » (routeur VPN, switch, NAS), « Lab TP » (deux routeurs, deux switchs, VLAN 10 et
20) et « Datacenter » (cœur, pare-feux en haute disponibilité, serveurs) sont déjà câblés port à port, adressés et
rangés en VLAN. Chaque vignette est dessinée comme les aperçus ; le nom du modèle est proposé, à changer, et « Créer » fait
un schéma neuf, sans lien avec le modèle. Ses propres modèles s'ajoutent sous « Mes modèles » : « Enregistrer comme
modèle… » garde un schéma entier, et, dans l'éditeur, le clic droit sur l'onglet d'une page propose « Enregistrer cette
page comme modèle… » (la page seule, ses câbles et les VLAN du schéma), avec un nom et une description. Les modèles sont
rangés à part dans le coffre (`schemas/modeles/`) : ils ne sont ni dans la liste des schémas ni dans les fiches. Au clic
droit sur la vignette d'un de ses modèles, ou par « ⋯ », « Supprimer le modèle… » le retire après une question ; son
fichier est rangé dans `schemas/modeles/.supprimes/`, pas effacé. Dans l'éditeur, une fenêtre de taille moyenne ou
étroite propose aussi « Enregistrer comme modèle… » et « Nouveau schéma depuis un modèle… » dans « Plus ». Un modèle sert aussi dans un schéma déjà commencé : « + » sous le
plan, « Pages depuis un modèle… », ajoute ses pages après les vôtres, sous leur nom, et Ctrl+Z les retire en une fois.

En mode avancé, un équipement choisi ouvre son volet : en tête, son icône, son nom, son type (un clic ouvre la grille
des types, avec une recherche et les types récents d'abord ; changer de type garde le nom, les ports et les connexions),
son statut et « ⋯ » (Connecter, Dupliquer, Copier, Verrouiller la place, Copier la configuration, Coller sur…, Retirer…).
Le clic droit sur un équipement du plan ouvre le même menu, au pointeur, en le choisissant d'abord ; au clavier, la
touche Menu ou Maj+F10 sur l'équipement qui a la main. Avec plusieurs équipements choisis, il garde ce qui agit sur eux
tous : Dupliquer, Copier, « Coller la configuration de … », Retirer…. En mode simplifié, il garde Dupliquer, Copier,
Verrouiller la place et Retirer…. Quatre onglets suivent, le dernier choisi restant choisi d'un équipement à l'autre : Général (nom, rôle, propriétés
propres au type), Ports, Réseau (gestion, adressage, interfaces de routage par VLAN, conseils) et Inventaire. L'onglet
Ports montre la façade, un carré par port, plein s'il est relié : un clic choisit un port, Maj+clic une suite, Ctrl+clic
en ajoute un ; les ports choisis passent ensemble en accès, en trunk, en PoE ou sont désactivés. « Ajouter des ports »
ajoute une plage d'un coup, au nommage des autres ports. « Copier la configuration » puis « Coller sur… » reprend le
réglage des ports de même nom (mode, VLAN, activation, PoE) et le VLAN de gestion sur d'autres équipements, pratique
pour plusieurs switchs d'étage pareils. La fenêtre dit, pour chaque équipement, combien de ports seront repris. Si les
noms diffèrent (« 1/0/1 » d'un côté, « Gi1/0/1 » de l'autre), elle propose de reprendre les ports par leur place : même
membre, même numéro ; « Par leur nom seulement » s'en tient aux noms. Après le collage, une annonce dit ce qui a été
repris (« 28 ports repris, VLAN de gestion 99 »), ou « Aucun port de même nom sur … : rien n'a changé ». Ctrl+Z annule
le collage en une fois. En fenêtre étroite, le volet devient un tiroir par le bas.

## Noter une idée

Ctrl+Maj+N ouvre la capture rapide depuis n'importe quelle vue. Votre note s'ajoute, datée, à la fiche « Boîte de
réception », qui reste hors de vos révisions : triez-la quand vous voulez. Dans Paramètres › Raccourcis, vous pouvez activer
Ctrl+Alt+N, qui fait la même chose depuis une autre application tant que kyfRev est ouvert.

## Raccourcis utiles

| Touche | Effet |
|---|---|
| Ctrl+K | Chercher partout |
| Ctrl+E | Passer de la lecture à l'écriture |
| Ctrl+P | Imprimer la fiche : aperçu feuille par feuille, recto verso, copies |
| Ctrl+Maj+P | Partager la fiche : PDF, page web à imprimer |
| Ctrl+Maj+N | Noter une idée dans la boîte de réception |
| Entrée, Espace | En révision, afficher le trou ou la partie suivante |
| Ctrl+Maj+F | Mode concentration : masquer ou rendre le menu |
| Ctrl+Maj+L | Verrouiller un coffre chiffré |
| F1 | Aide |

## En cas de problème

**Coffre dans un dossier synchronisé** (Dropbox, OneDrive, Syncthing, Nextcloud) : si deux postes modifient la même fiche
sans se voir, l'outil garde deux fichiers. « Santé du coffre » les liste sous « Copies de conflit » : comparez-les dans
l'Explorateur, reportez ce qui manque, puis supprimez la copie. N'ouvrez pas le même coffre sur deux postes à la fois.

Le journal `kyfRev.log` garde la trace des derniers démarrages : dans `%LOCALAPPDATA%\kyfRev\v1` pour une version installée, à côté
de `kyfRev.exe` pour la version portable. Il ne contient pas le texte de vos fiches.

L'outil `kyfrev-coffre verifier <dossier>`, livré avec l'application, analyse un coffre sans rien y écrire.
