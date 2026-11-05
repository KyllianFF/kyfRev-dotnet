// Extension « Bilan du coffre » pour kyfRev (D63) : une vue en lecture seule
// sur l'ensemble des fiches actives. Rien n'est modifié, rien n'est gardé.
"use strict";

const echapper = (texte) =>
  String(texte).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);

/** Mots du texte, sans les blocs de code ni la syntaxe Markdown. */
function compterMots(texte) {
  const sansCode = texte.replace(/```[\s\S]*?```/g, " ").replace(/`[^`]*`/g, " ");
  const sansLiens = sansCode.replace(/!?\[([^\]]*)\]\([^)]*\)/g, "$1");
  return (sansLiens.match(/[\p{L}\p{N}][\p{L}\p{N}'’-]*/gu) ?? []).length;
}

const SEUIL_COURTE = 80;
let bilan = null;

async function calculer() {
  const fiches = [];
  for (const fiche of (await kyfrev.fiches.lister()).filter((f) => !f.archivee)) {
    const { texte } = await kyfrev.fiches.lire(fiche.id);
    fiches.push({
      ...fiche,
      mots: compterMots(texte),
      intertitres: (texte.match(/^#{2,6}\s/gm) ?? []).length,
    });
  }
  const parDossier = new Map();
  for (const f of fiches) {
    const d = parDossier.get(f.dossier || "Racine") ?? { fiches: 0, mots: 0 };
    d.fiches += 1;
    d.mots += f.mots;
    parDossier.set(f.dossier || "Racine", d);
  }
  bilan = {
    fiches,
    total: fiches.reduce((t, f) => t + f.mots, 0),
    parDossier: [...parDossier.entries()].sort((a, b) => b[1].mots - a[1].mots),
  };
}

const liste = (fiches, detail) =>
  fiches.length === 0
    ? '<p class="secondaire">Aucune.</p>'
    : `<ul>${fiches.map((f) => `<li>${echapper(f.titre)} <span class="secondaire">· ${echapper(detail(f))}</span></li>`).join("")}</ul>`;

kyfrev.vue(async () => {
  if (bilan === null) await calculer();
  const { fiches, total, parDossier } = bilan;
  if (fiches.length === 0) return "<h1>Bilan du coffre</h1><p>Aucune fiche active pour l'instant.</p>";
  const nombre = (n) => n.toLocaleString("fr-FR");
  const courtes = fiches.filter((f) => f.mots < SEUIL_COURTE).sort((a, b) => a.mots - b.mots);
  const sansEtiquette = fiches.filter((f) => f.etiquettes.length === 0);
  const sansIntertitre = fiches.filter((f) => f.intertitres === 0 && f.mots >= SEUIL_COURTE);
  const longues = [...fiches].sort((a, b) => b.mots - a.mots).slice(0, 5);
  const dossiers = parDossier
    .map(
      ([nom, d]) =>
        `<tr><td>${echapper(nom.replaceAll("/", " › "))}</td><td>${nombre(d.fiches)}</td><td>${nombre(d.mots)}</td><td>${nombre(Math.round(d.mots / d.fiches))}</td></tr>`,
    )
    .join("");
  return `
    <h1>Bilan du coffre</h1>
    <p class="secondaire">${nombre(fiches.length)} fiche(s) active(s) · ${nombre(total)} mots · ${nombre(Math.round(total / fiches.length))} mots par fiche en moyenne</p>
    <p class="actions"><button class="bouton discret" data-action="recalculer">Recalculer</button></p>
    <h2>Par dossier</h2>
    <table><thead><tr><th>Dossier</th><th>Fiches</th><th>Mots</th><th>Mots par fiche</th></tr></thead><tbody>${dossiers}</tbody></table>
    <h2>À étoffer (moins de ${SEUIL_COURTE} mots)</h2>
    ${liste(courtes, (f) => `${f.mots} mots`)}
    <h2>Sans étiquette</h2>
    ${liste(sansEtiquette, (f) => f.dossier || "Racine")}
    <h2>Longues, sans intertitre</h2>
    ${liste(sansIntertitre, (f) => `${nombre(f.mots)} mots : des intertitres aideraient à s'y retrouver`)}
    <h2>Les plus longues</h2>
    ${liste(longues, (f) => `${nombre(f.mots)} mots`)}`;
});

kyfrev.action("recalculer", () => {
  bilan = null;
});
