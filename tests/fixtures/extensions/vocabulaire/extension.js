// Extension « Vocabulaire » pour kyfRev (D63).
//
// Dans une fiche :
//
//   ```vocabulaire
//   the cat = le chat
//   to run = courir
//   the weather = le temps (qu'il fait)
//   ```
//
// Chaque ligne : un terme, « = » (ou « | »), sa traduction. Dans la fiche, les
// traductions se dévoilent au clic. La vue tire un mot au hasard, dans un sens
// ou dans l'autre ; les mots « à revoir » reviennent plus souvent. Les
// résultats sont gardés dans le coffre (permission « stockage »).
"use strict";

const echapper = (texte) =>
  String(texte).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);

function paires(source) {
  const liste = [];
  for (const ligne of source.split("\n")) {
    const morceaux = ligne.split(/\s[=|]\s/);
    if (morceaux.length < 2) continue;
    const terme = morceaux[0].trim();
    const traduction = morceaux.slice(1).join(" = ").trim();
    if (terme && traduction) liste.push({ terme, traduction });
  }
  return liste;
}

kyfrev.bloc("vocabulaire", (source) => {
  const liste = paires(source);
  if (liste.length === 0) return '<p class="secondaire">Liste vide : une ligne par mot, « the cat = le chat ».</p>';
  const lignes = liste
    .map(
      (p) =>
        `<tr><td>${echapper(p.terme)}</td><td><details><summary>Afficher</summary>${echapper(p.traduction)}</details></td></tr>`,
    )
    .join("");
  return `<table><thead><tr><th>Terme</th><th>Traduction</th></tr></thead><tbody>${lignes}</tbody></table>`;
});

// --- La vue : une carte à la fois. ---
let mots = null;
let carte = null;
let reponseVisible = false;
let sens = "terme"; // « terme » : on voit le terme, on cherche la traduction ; « traduction » : l'inverse.
/** { « terme » : { sus, aRevoir } } */
let resultats = {};

async function chargerMots() {
  const trouves = [];
  for (const fiche of (await kyfrev.fiches.lister()).filter((f) => !f.archivee)) {
    const { texte } = await kyfrev.fiches.lire(fiche.id);
    for (const bloc of texte.matchAll(/```vocabulaire\n([\s\S]*?)```/g)) {
      for (const p of paires(bloc[1])) trouves.push({ ...p, fiche: fiche.titre });
    }
  }
  mots = trouves;
  resultats = (await kyfrev.stockage.lire()) ?? {};
}

/** Tirage pondéré : un mot souvent « à revoir » pèse plus lourd. */
function tirer() {
  if (mots.length === 0) return null;
  const poids = mots.map((m) => {
    const r = resultats[m.terme] ?? { sus: 0, aRevoir: 0 };
    return 1 + 2 * r.aRevoir / (1 + r.sus);
  });
  let reste = Math.random() * poids.reduce((a, b) => a + b, 0);
  for (let i = 0; i < mots.length; i++) {
    reste -= poids[i];
    if (reste <= 0) return mots[i];
  }
  return mots[mots.length - 1];
}

kyfrev.vue(async () => {
  if (mots === null) await chargerMots();
  if (mots.length === 0) {
    return "<h1>Vocabulaire</h1><p>Aucun mot pour l'instant : ajoutez un bloc <code>vocabulaire</code> à une fiche.</p>";
  }
  carte ??= tirer();
  const question = sens === "terme" ? carte.terme : carte.traduction;
  const reponse = sens === "terme" ? carte.traduction : carte.terme;
  const r = resultats[carte.terme] ?? { sus: 0, aRevoir: 0 };
  return `
    <h1>Vocabulaire</h1>
    <p class="secondaire">${mots.length} mot(s) · ${echapper(carte.fiche)} · su ${r.sus} fois, à revoir ${r.aRevoir} fois</p>
    <div class="carte">
      <p class="question">${echapper(question)}</p>
      ${reponseVisible ? `<p class="reponse">${echapper(reponse)}</p>` : ""}
    </div>
    <p class="actions">
      ${
        reponseVisible
          ? `<button class="bouton principal" data-action="resultat" data-valeur="su">Je savais</button>
             <button class="bouton" data-action="resultat" data-valeur="a-revoir">À revoir</button>`
          : `<button class="bouton principal" data-action="afficher">Afficher la réponse</button>`
      }
      <button class="bouton discret" data-action="sens">${sens === "terme" ? "Sens : terme → traduction" : "Sens : traduction → terme"}</button>
      <button class="bouton discret" data-action="recharger">Relire les fiches</button>
    </p>`;
});

kyfrev.action("afficher", () => {
  reponseVisible = true;
});

kyfrev.action("resultat", async (valeur) => {
  const r = resultats[carte.terme] ?? { sus: 0, aRevoir: 0 };
  if (valeur === "su") r.sus += 1;
  else r.aRevoir += 1;
  resultats[carte.terme] = r;
  await kyfrev.stockage.ecrire(resultats);
  carte = tirer();
  reponseVisible = false;
});

kyfrev.action("sens", () => {
  sens = sens === "terme" ? "traduction" : "terme";
  reponseVisible = false;
});

kyfrev.action("recharger", () => {
  mots = null;
  carte = null;
  reponseVisible = false;
});
