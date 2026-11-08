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
main. Paramètres › Données permet d'en ouvrir ou d'en créer un autre.

## Réviser

- **Aujourd'hui** liste ce qui est à réviser : fiches, passages, définitions, rappels. « Commencer » enchaîne le tout.
- En révision, le texte surligné et les mots en gras peuvent être masqués, pour vous forcer à les retrouver. Validez une fois la
  fiche relue, ou reportez-la.
- **Surlignez un passage** ou **créez une définition** depuis le texte : ils ont leur propre calendrier.
- La barre de révision permet aussi de valider à une date passée, ou de reporter à une date choisie.
- **Examens** : sur Aujourd'hui, « Examen… » fixe une date pour un dossier. « Resserrer… » ramène les échéances avant la veille.
- **Charge** montre les 30 prochains jours, et permet de déplacer une échéance vers un jour moins chargé.

## Écrire

Dans une fiche, « Écrire » ouvre le texte mis en forme ; le code Markdown s'affiche à côté si vous le demandez. L'enregistrement est
automatique, et l'historique garde les versions précédentes. Le ruban insère images, tableaux, formules, dessins et schémas.

## Retrouver

- **Fiches** liste vos dossiers et cherche dans le texte, sans tenir compte des accents ni de la casse.
- **Ctrl+K** cherche partout : commandes, fiches, passages, définitions, citations.
- Le panneau **« Fiches qui mènent ici »** montre les liens entrants ; survolez un lien pour voir un aperçu de sa fiche.

## Échanger

- **Partager** une fiche : page web, PDF, Markdown, archive avec images, texte, presse-papiers.
- **Importer** un dossier de notes, Obsidian par exemple, depuis la vue Fiches.
- **Exporter** le suivi, les annotations, les citations ou le coffre complet depuis Paramètres › Données.

Ce qui sort est en clair, même depuis un coffre chiffré, sauf le coffre complet.

## Protéger

- **La corbeille** garde ce que vous supprimez. Seule une suppression définitive, confirmée, l'efface.
- **Les sauvegardes** se font chaque jour à l'ouverture, dans le dossier `Sauvegardes` à côté du coffre.
- **Santé du coffre** signale les fichiers déplacés, les liens cassés et les annotations à vérifier.
- **Chiffrer** : Paramètres › Données › « Chiffrer ce coffre… » crée une copie chiffrée dans un nouveau dossier. **Notez la clé de
  récupération** : sans elle et sans le mot de passe, le coffre est perdu. Le coffre en clair reste en place tant que vous ne le
  retirez pas. Ctrl+Maj+L verrouille à tout moment. Ce que le chiffrement protège, et ce qu'il ne protège pas, est écrit dans
  [le modèle de menace](chiffrement.md#le-modèle-de-menace).

## Ajouter

Paramètres › Modules et extensions active les citations du jour et les schémas réseau, et installe des extensions, depuis un dossier
ou depuis le catalogue signé. Une extension n'a jamais le réseau, et demande chaque permission.

## Raccourcis utiles

| Touche | Effet |
|---|---|
| Ctrl+K | Chercher partout |
| Ctrl+E | Passer de la lecture à l'écriture |
| Ctrl+Maj+L | Verrouiller un coffre chiffré |
| F1 | Aide |

## En cas de problème

Le journal `kyfRev.log` garde la trace des derniers démarrages : dans `%LOCALAPPDATA%\kyfRev\v1` pour une version installée, à côté
de `kyfRev.exe` pour la version portable. Il ne contient pas le texte de vos fiches.

L'outil `kyfrev-coffre verifier <dossier>`, livré avec l'application, analyse un coffre sans rien y écrire.
