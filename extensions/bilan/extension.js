// Bilan du coffre, version 2, pour kyfRev (contrat 2).
//
// Le point sur les fiches actives, pour les améliorer plutôt que seulement constater : chaque fiche signalée s'ouvre d'un
// clic, une étiquette s'ajoute sur place (permission « fiches:creation » : il change des étiquettes, jamais le texte), et une
// fiche peut être ignorée pour ne plus la voir. Le bilan de chaque semaine est gardé, pour en voir l'évolution.
"use strict";

const FORMAT = 2;
const SEUIL_COURTE = 80;
const MONTRES = 20;
const SEMAINES = 12;

const echapper = (texte) =>
  String(texte ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);

const pluriel = (n, un, plusieurs) => `${n.toLocaleString("fr-FR")} ${n > 1 ? plusieurs : un}`;
const nombre = (n) => n.toLocaleString("fr-FR");

/** Mots du texte, sans les blocs de code ni la syntaxe Markdown. */
function compterMots(texte) {
  const sansCode = texte.replace(/```[\s\S]*?```/g, " ").replace(/`[^`]*`/g, " ");
  const sansLiens = sansCode.replace(/!?\[([^\]]*)\]\([^)]*\)/g, "$1");
  return (sansLiens.match(/[\p{L}\p{N}][\p{L}\p{N}'’-]*/gu) ?? []).length;
}

/** La semaine ISO d'une date : « 2026-S45 ». */
function semaine(date = new Date()) {
  const d = new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()));
  const jour = d.getUTCDay() || 7;
  d.setUTCDate(d.getUTCDate() + 4 - jour);
  const debut = new Date(Date.UTC(d.getUTCFullYear(), 0, 1));
  const numero = Math.ceil(((d - debut) / 86400000 + 1) / 7);
  return `${d.getUTCFullYear()}-S${String(numero).padStart(2, "0")}`;
}

// --- Les données : les fiches ignorées et le bilan de chaque semaine. ---

let donnees = null;

async function charger() {
  if (donnees) return donnees;
  const lu = await kyfrev.stockage.lire();
  donnees = lu && lu.format === FORMAT ? lu : { format: FORMAT, ignorees: [], semaines: [] };
  return donnees;
}

const garder = () => kyfrev.stockage.ecrire(donnees);

// --- Le bilan. ---

let bilan = null;
let message = "";
let voirIgnorees = false;

async function calculer() {
  const fiches = [];
  for (const fiche of (await kyfrev.fiches.lister()).filter((f) => !f.archivee)) {
    const { texte } = await kyfrev.fiches.lire(fiche.id);
    fiches.push({ ...fiche, mots: compterMots(texte), intertitres: (texte.match(/^#{2,6}\s/gm) ?? []).length });
  }
  const parDossier = new Map();
  for (const f of fiches) {
    const nom = f.dossier || "Racine";
    const d = parDossier.get(nom) ?? { fiches: 0, mots: 0 };
    d.fiches += 1;
    d.mots += f.mots;
    parDossier.set(nom, d);
  }
  bilan = {
    fiches,
    total: fiches.reduce((t, f) => t + f.mots, 0),
    parDossier: [...parDossier.entries()].sort((a, b) => b[1].mots - a[1].mots),
  };
  const ici = semaine();
  const entree = { semaine: ici, fiches: fiches.length, mots: bilan.total };
  const avant = donnees.semaines.filter((s) => s.semaine !== ici);
  const changee = JSON.stringify(donnees.semaines.find((s) => s.semaine === ici)) !== JSON.stringify(entree);
  donnees.semaines = [...avant, entree].sort((a, b) => a.semaine.localeCompare(b.semaine)).slice(-SEMAINES);
  if (changee) await garder();
}

const ignoree = (f) => donnees.ignorees.includes(f.id);
const visibles = () => bilan.fiches.filter((f) => !ignoree(f));
const courtes = () => visibles().filter((f) => f.mots < SEUIL_COURTE).sort((a, b) => a.mots - b.mots);

// --- La commande, et l'encart d'« Aujourd'hui ». ---

kyfrev.commande("bilan", async () => {
  await charger();
  bilan = null;
  message = "";
  await kyfrev.ouvrirVue();
});

kyfrev.encart(async () => {
  await charger();
  if (bilan === null) await calculer();
  const liste = courtes();
  if (liste.length === 0) return null;
  return {
    texte: `${pluriel(liste.length, "fiche à étoffer", "fiches à étoffer")}`,
    detail: `La plus courte : « ${liste[0].titre} », ${pluriel(liste[0].mots, "mot", "mots")}.`,
  };
});

// --- Les actions. ---

const action = (nom, faire) =>
  kyfrev.action(nom, async (valeur, d) => {
    await charger();
    try {
      await faire(valeur, d ?? {});
    } catch (erreur) {
      message = `Impossible : ${erreur?.message ?? erreur}`;
    }
  });

action("recalculer", () => {
  bilan = null;
  message = "";
});
action("ouvrir", async (_valeur, d) => {
  await kyfrev.fiches.ouvrir(d.fiche);
});
action("ignorer", async (_valeur, d) => {
  if (!donnees.ignorees.includes(d.fiche)) donnees.ignorees.push(d.fiche);
  await garder();
  const fiche = bilan?.fiches.find((f) => f.id === d.fiche);
  message = fiche ? `« ${fiche.titre} » est ignorée : elle n'apparaît plus dans les listes.` : "";
});
action("ne-plus-ignorer", async (_valeur, d) => {
  donnees.ignorees = donnees.ignorees.filter((id) => id !== d.fiche);
  await garder();
  message = "";
});
action("voir-ignorees", () => {
  voirIgnorees = !voirIgnorees;
});
action("etiqueter", async (valeur, d) => {
  const etiquette = String(valeur ?? "").trim().replace(/^#/, "");
  const fiche = bilan?.fiches.find((f) => f.id === d.fiche);
  if (!etiquette || !fiche) return;
  fiche.etiquettes = await kyfrev.fiches.etiqueter(fiche.id, [...fiche.etiquettes, etiquette]);
  message = `Étiquette « ${etiquette} » ajoutée à « ${fiche.titre} ».`;
});

// --- La vue. ---

const boutons = (f) =>
  `<button class="discret" data-action="ouvrir" data-fiche="${echapper(f.id)}" aria-label="Ouvrir ${echapper(f.titre)}">Ouvrir</button><button class="discret" data-action="ignorer" data-fiche="${echapper(f.id)}" aria-label="Ignorer ${echapper(f.titre)}">Ignorer</button>`;

function liste(fiches, detail, avecEtiquette = false) {
  if (fiches.length === 0) return '<p class="secondaire">Aucune.</p>';
  const lignes = fiches
    .slice(0, MONTRES)
    .map(
      (f) => `<li><strong>${echapper(f.titre)}</strong> <span class="secondaire">· ${echapper(detail(f))}</span>
        ${avecEtiquette ? `<input data-action="etiqueter" data-fiche="${echapper(f.id)}" placeholder="Ajouter une étiquette" aria-label="Ajouter une étiquette à ${echapper(f.titre)}">` : ""}
        ${boutons(f)}</li>`,
    )
    .join("");
  const reste = fiches.length - MONTRES;
  return `<ul>${lignes}</ul>${reste > 0 ? `<p class="secondaire">Et ${pluriel(reste, "autre", "autres")}.</p>` : ""}`;
}

function evolution() {
  if (donnees.semaines.length < 2) return "";
  const max = Math.max(...donnees.semaines.map((s) => s.mots), 1);
  const lignes = donnees.semaines
    .map((s) => `<tr><td>${echapper(s.semaine.replace("-S", ", semaine "))}</td><td>${nombre(s.fiches)}</td><td>${nombre(s.mots)}</td>
      <td><progress value="${s.mots}" max="${max}" aria-label="${nombre(s.mots)} mots"></progress></td></tr>`)
    .join("");
  return `<h2>Évolution</h2><table><thead><tr><th>Semaine</th><th>Fiches</th><th>Mots</th><th>Volume</th></tr></thead><tbody>${lignes}</tbody></table>`;
}

kyfrev.vue(async () => {
  await charger();
  if (bilan === null) await calculer();
  const { fiches, total, parDossier } = bilan;
  if (fiches.length === 0) return "<h1>Bilan du coffre</h1><p>Aucune fiche active pour l'instant.</p>";
  const actives = visibles();
  const sansEtiquette = actives.filter((f) => f.etiquettes.length === 0);
  const sansIntertitre = actives.filter((f) => f.intertitres === 0 && f.mots >= SEUIL_COURTE);
  const longues = [...fiches].sort((a, b) => b.mots - a.mots).slice(0, 5);
  const ignorees = fiches.filter(ignoree);
  const dossiers = parDossier
    .map(([nom, d]) => `<tr><td>${echapper(nom.replaceAll("/", " › "))}</td><td>${nombre(d.fiches)}</td><td>${nombre(d.mots)}</td><td>${nombre(Math.round(d.mots / d.fiches))}</td></tr>`)
    .join("");
  return `
    <h1>Bilan du coffre</h1>
    <p class="secondaire">${pluriel(fiches.length, "fiche active", "fiches actives")} · ${pluriel(total, "mot", "mots")} · ${nombre(Math.round(total / fiches.length))} mots par fiche en moyenne</p>
    ${message ? `<p><strong>${echapper(message)}</strong></p>` : ""}
    <p class="actions"><button class="discret" data-action="recalculer">Recalculer</button></p>
    ${evolution()}
    <h2>À étoffer (moins de ${SEUIL_COURTE} mots)</h2>
    ${liste(courtes(), (f) => pluriel(f.mots, "mot", "mots"))}
    <h2>Sans étiquette</h2>
    ${liste(sansEtiquette, (f) => f.dossier || "Racine", true)}
    <h2>Longues, sans intertitre</h2>
    ${liste(sansIntertitre, (f) => `${pluriel(f.mots, "mot", "mots")} : des intertitres aideraient à s'y retrouver`)}
    <h2>Par dossier</h2>
    <table><thead><tr><th>Dossier</th><th>Fiches</th><th>Mots</th><th>Mots par fiche</th></tr></thead><tbody>${dossiers}</tbody></table>
    <h2>Les plus longues</h2>
    <ul>${longues.map((f) => `<li>${echapper(f.titre)} <span class="secondaire">· ${pluriel(f.mots, "mot", "mots")}</span></li>`).join("")}</ul>
    ${ignorees.length
      ? `<p class="actions"><button class="discret" data-action="voir-ignorees">${voirIgnorees ? "Cacher" : "Montrer"} les fiches ignorées (${ignorees.length})</button></p>
         ${voirIgnorees ? `<ul>${ignorees.map((f) => `<li>${echapper(f.titre)} <button class="discret" data-action="ne-plus-ignorer" data-fiche="${echapper(f.id)}">Ne plus ignorer</button></li>`).join("")}</ul>` : ""}`
      : ""}`;
});
