// Extension « Chronologie » pour kyfRev (D63).
//
// Dans une fiche :
//
//   ```chronologie
//   1789 : Prise de la Bastille
//   1792 : Proclamation de la République
//   9 novembre 1799 : Coup d'État du 18 Brumaire
//   52 av. J.-C. : Bataille d'Alésia
//   ```
//
// Chaque ligne : une date, « : », un événement. La vue réunit les dates de
// toutes les fiches, de la plus ancienne à la plus récente.
"use strict";

const echapper = (texte) =>
  String(texte).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);

/** Lignes « date : événement » d'un bloc. */
function evenements(source) {
  const liste = [];
  for (const ligne of source.split("\n")) {
    const separateur = ligne.indexOf(" : ");
    if (separateur <= 0) continue;
    const date = ligne.slice(0, separateur).trim();
    const texte = ligne.slice(separateur + 3).trim();
    if (date && texte) liste.push({ date, texte });
  }
  return liste;
}

/**
 * Année d'une date écrite librement, pour trier : le premier nombre de 3 ou
 * 4 chiffres (« 9 novembre 1799 » → 1799), sinon le premier nombre
 * (« 52 av. J.-C. » → 52) ; « av. J.-C. » ou « avant » la rend négative.
 */
function annee(date) {
  const nombres = (date.match(/\d{1,4}/g) ?? []).map(Number);
  if (nombres.length === 0) return Number.POSITIVE_INFINITY;
  const valeur = nombres.find((n) => n >= 100) ?? nombres[0];
  return /av\.?\s*J\.?-?C|avant/i.test(date) ? -valeur : valeur;
}

const frise = (liste, avecFiche) =>
  `<ol class="frise">${liste
    .map(
      (e) =>
        `<li><time>${echapper(e.date)}</time><span>${echapper(e.texte)}${
          avecFiche ? ` <span class="secondaire">· ${echapper(e.fiche)}</span>` : ""
        }</span></li>`,
    )
    .join("")}</ol>`;

kyfrev.bloc("chronologie", (source) => {
  const liste = evenements(source);
  if (liste.length === 0) {
    return '<p class="secondaire">Chronologie vide : une ligne par date, « 1789 : Prise de la Bastille ».</p>';
  }
  return frise(liste, false);
});

// --- La vue : toutes les dates du coffre. ---
let toutes = null;
let filtre = "";

async function chargerToutes() {
  const trouvees = [];
  for (const fiche of (await kyfrev.fiches.lister()).filter((f) => !f.archivee)) {
    const { texte } = await kyfrev.fiches.lire(fiche.id);
    for (const bloc of texte.matchAll(/```chronologie\n([\s\S]*?)```/g)) {
      for (const e of evenements(bloc[1])) trouvees.push({ ...e, fiche: fiche.titre, annee: annee(e.date) });
    }
  }
  trouvees.sort((a, b) => a.annee - b.annee);
  toutes = trouvees;
}

kyfrev.vue(async () => {
  if (toutes === null) await chargerToutes();
  const recherche = filtre.trim().toLowerCase();
  const visibles = recherche
    ? toutes.filter((e) => `${e.date} ${e.texte} ${e.fiche}`.toLowerCase().includes(recherche))
    : toutes;
  const entete = `
    <h1>Chronologie</h1>
    <p class="secondaire">${toutes.length} date(s), de toutes vos fiches actives, de la plus ancienne à la plus récente.</p>
    <p class="actions">
      <input class="champ" type="search" placeholder="Filtrer (date, événement, fiche)" value="${echapper(filtre)}" data-action="filtrer" aria-label="Filtrer la chronologie" />
      <button class="bouton discret" data-action="recharger">Relire les fiches</button>
    </p>`;
  if (toutes.length === 0) {
    return `${entete}<p>Aucune date pour l'instant : ajoutez un bloc <code>chronologie</code> à une fiche.</p>`;
  }
  return `${entete}${visibles.length ? frise(visibles, true) : '<p class="secondaire">Aucune date ne correspond.</p>'}`;
});

kyfrev.action("filtrer", (valeur) => {
  filtre = String(valeur ?? "");
});

kyfrev.action("recharger", () => {
  toutes = null;
});
