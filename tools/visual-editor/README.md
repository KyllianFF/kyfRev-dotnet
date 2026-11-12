# Éditeur visuel des fiches

Le texte mis en forme qui se modifie à sa place, comme dans un traitement de texte. C'est ProseMirror, repris de la 0.x, dans WebView2 : la fiche reste un fichier Markdown, et ce que l'utilisateur ne touche pas reste identique à l'octet.

Le paquet est écrit dans `src/KyfRev.Presentation.Wpf/Editing/Web` et embarqué dans l'application. Il est versionné : compiler kyfRev ne demande pas Node. La CI reconstruit ce paquet et vérifie qu'il correspond aux sources.

```bash
npm ci --ignore-scripts
```

```bash
npm run check
```

```bash
npm test
```

```bash
npm run build
```

`src/messages.ts` est généré depuis `VisualMessages.cs`. Un test de la présentation vérifie qu'il est à jour et donne le texte attendu s'il ne l'est pas.

## La page d'essai

`essai.html` et `essai/essai.ts` montrent l'éditeur hors de l'application : la page joue l'hôte, charge une fiche d'exemple et
envoie les messages `theme`, `page`, `paper` et `load` d'après son adresse. Elle n'entre pas dans le paquet.

```bash
npm run dev
```

Puis, par exemple, `http://localhost:5173/essai.html?papier=grands-carreaux&page=a5&police=manuscrite&fiche=longue`. Les
réglages (papier, pas, page, marge, police, taille, thème, fiche, traits) sont décrits en tête de `essai/essai.ts` ; dans la
console, `essai.verifier()` dit de combien chaque bloc de texte s'écarte des lignes du papier.

## La page des dessins

`src/diagrammes.ts` et `public/diagrammes.html` sont la page cachée qui dessine les schémas et les graphiques : Mermaid en mode
strict, tel qu'il est publié (`mermaid.min.js`), à part de l'éditeur, qui n'a pas à le charger. `npm run build` écrit les deux
paquets, puis leurs avis de tiers, depuis le verrou (`avis-de-tiers.js`).
