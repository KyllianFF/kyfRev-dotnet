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

### Depuis une version 0.x

kyfRev reprend votre coffre sans conversion. Au premier lancement, il ouvre le dernier coffre de la 0.x et y écrit aussitôt.
**Faites d'abord une copie du dossier du coffre.** Une fois satisfait, Paramètres › À propos vous propose de retirer la 0.x ;
votre coffre n'est pas touché. N'installez pas « pour tous les utilisateurs » dans le dossier de la 0.x : l'installateur le refuse.

## Le coffre

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
- **Examens** : sur Aujourd'hui, « Examen… » fixe une date pour un dossier. « Resserrer… » ramène les échéances avant la veille,
  et peut remettre en révision les fiches archivées du dossier. Tant que l'examen n'est pas passé, une révision ne place
  jamais l'étape suivante après sa veille.
- **Dossier ou étiquette** : sur Aujourd'hui, la liste à côté de « Tout / Fiches / Passages » restreint ce qui est à réviser,
  et la séance avec.
- **Suspendre** (dans « ⋯ » de la barre de révision) met une fiche ou un passage de côté sans rien perdre : il ne se présente
  plus à réviser, et attend dans « Suspendues », sur Aujourd'hui, jusqu'à « Reprendre ».
- **Zoom de la fiche** : le curseur en bas à droite, de 50 à 200 %. La taille de toute l'interface se règle à part, dans
  Paramètres › Apparence.
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

L'onglet « Mise en page » du ruban donne une feuille à la fiche : sans fin (par défaut), A4, A5 ou A6, en portrait ou en
paysage, avec des marges étroites, normales, larges ou sur mesure. Le choix vaut pour toutes les fiches ou pour la fiche
ouverte seule, et il est retenu sur cet ordinateur. Avec une feuille, la fiche se lit et s'écrit en pages : un bloc qui
dépasserait le bas d'une page commence la suivante, un tableau court sur plusieurs pages sans qu'une ligne soit coupée, et
une image ou un schéma trop haut rétrécit pour tenir sur une page. Sous la fiche, « Ajuster à la largeur » règle le zoom
pour que la feuille tienne dans la fenêtre. Avec une feuille, l'onglet Insertion propose « Saut de page » (la suite commence la
page suivante) et « Page blanche » (une page entière reste vide) ; un tableau coupé répète son en-tête en haut de chaque
page, et une citation coupée dit « suite ».

Ctrl+P ouvre « Imprimer » : l'imprimante, le nombre de copies, les feuilles (toutes, ou certaines par leurs numéros :
« 1-2, 4 »), le recto verso (bord long ou bord court), l'orientation, les marges (par défaut celles de la fiche) et les
numéros de page en bas, au format 1/4. L'aperçu montre chaque feuille telle qu'elle sortira. Les pages sont celles de la
mise en page de la fiche ; une fiche restée en feuille sans fin s'imprime en A4. L'impression est toujours en thème clair,
sur fond blanc.

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
chiffrement, Sauvegardes et exports), et À propos. Raccourcis change dix raccourcis clavier ; Menu change l'ordre des vues
du menu. « Chercher un réglage », en haut de la liste, trouve un réglage par quelques lettres et ouvre sa section. Dans une
section retenue sur l'ordinateur, « Rétablir les réglages d'origine » remet ceux de cette section ; « Annuler » les rend tant
que vous y restez.

## Retrouver

- **Fiches** liste vos dossiers et cherche dans le texte, sans tenir compte des accents ni de la casse.
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

Dans la vue Schémas réseau, chaque schéma s'archive ou se supprime par ses boutons ou au clic droit sur sa carte. Archivé,
il quitte la liste mais reste montré par les fiches qui l'insèrent ; supprimé, il va à la corbeille des schémas. Les deux
se retrouvent sous la liste et se restaurent : rien n'est effacé. Dans l'éditeur d'un schéma, plusieurs éléments choisis
(Maj ou Ctrl + clic, ou un cadre tracé à la souris) se groupent avec Ctrl+G et se manipulent alors ensemble ; un second clic
sur l'un d'eux le choisit seul, et Ctrl+Maj+G les dégroupe.

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
