// Chronologie, version 2, pour kyfRev (contrat 2).
//
// Des frises nommées, que l'on compose depuis les fiches : sélectionne un texte en lecture, clic droit, « Ajouter à la frise » ;
// la date est déjà proposée. Ou clic droit sur une fiche : « Extraire les dates de cette fiche », puis coche celles à garder.
// Chaque date rouvre le passage d'où elle vient, et « Remettre dans l'ordre » en fait un jeu.
//
// Les blocs de la version 1 restent lisibles, et toutes leurs dates se retrouvent dans « Dates des blocs » :
//
//   ```chronologie
//   1789 : Prise de la Bastille
//   9 novembre 1799 : Coup d'État du 18 Brumaire
//   52 av. J.-C. : Bataille d'Alésia
//   ```
"use strict";

const FORMAT = 2;

const echapper = (texte) =>
  String(texte ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);

const pluriel = (n, un, plusieurs) => `${n} ${n > 1 ? plusieurs : un}`;

// --- Les dates écrites librement. ---

const MOIS = [
  ["janv", 1], ["févr", 2], ["fevr", 2], ["mars", 3], ["avr", 4], ["mai", 5], ["juin", 6],
  ["juil", 7], ["août", 8], ["aout", 8], ["sept", 9], ["oct", 10], ["nov", 11], ["déc", 12], ["dec", 12],
];
const NOMS_DE_MOIS = "janvier|février|fevrier|mars|avril|mai|juin|juillet|août|aout|septembre|octobre|novembre|décembre|decembre|janv\\.?|févr\\.?|fevr\\.?|avr\\.?|juil\\.?|sept\\.?|oct\\.?|nov\\.?|déc\\.?|dec\\.?";
const DATE_EN_TOUTES_LETTRES = new RegExp(`\\b(1er|[12]?\\d|3[01])\\s+(${NOMS_DE_MOIS})\\s+(\\d{1,4})(\\s*av\\.?\\s*J\\.?-?C\\.?)?`, "iu");
const DATE_EN_CHIFFRES = /\b(\d{1,2})\/(\d{1,2})\/(\d{3,4})\b/u;
const ANNEE = /(?<![\d,.])\b(\d{3,4})\b(?![\d,.]\d)(\s*av\.?\s*J\.?-?C\.?)?/u;

/** L'année (négative avant J.-C.), le mois et le jour d'une date écrite librement ; 0 pour ce qu'elle ne dit pas. */
function decomposer(date) {
  const nombres = (date.match(/\d{1,4}/g) ?? []).map(Number);
  if (nombres.length === 0) return null;
  const annee = nombres.find((n) => n >= 100) ?? nombres.at(-1);
  let mois = 0;
  let jour = 0;
  const enLettres = DATE_EN_TOUTES_LETTRES.exec(date);
  const enChiffres = DATE_EN_CHIFFRES.exec(date);
  if (enLettres) {
    jour = enLettres[1] === "1er" ? 1 : Number(enLettres[1]);
    mois = MOIS.find(([debut]) => enLettres[2].toLowerCase().startsWith(debut))?.[1] ?? 0;
  } else if (enChiffres) {
    jour = Number(enChiffres[1]);
    mois = Number(enChiffres[2]);
  }
  return { annee: /av\.?\s*J\.?-?C|avant/i.test(date) ? -annee : annee, mois, jour };
}

/** Une clé de tri : l'année, puis le mois et le jour s'ils y sont. */
function cle(date) {
  const d = decomposer(date);
  return d ? d.annee * 10000 + d.mois * 100 + d.jour : Number.POSITIVE_INFINITY;
}

/** La première date d'un texte, et le texte sans elle ; rien s'il n'en a pas. */
function dateDans(texte) {
  for (const motif of [DATE_EN_TOUTES_LETTRES, DATE_EN_CHIFFRES, ANNEE]) {
    const trouvee = motif.exec(texte);
    if (!trouvee) continue;
    const annee = Number(motif === ANNEE ? trouvee[1] : trouvee[3]);
    if (motif === ANNEE && (annee < 100 || annee > 2100)) continue;
    const date = trouvee[0].trim();
    const reste = (texte.slice(0, trouvee.index) + texte.slice(trouvee.index + trouvee[0].length))
      .replace(/^\s*(en|le|du|de|dès|vers|à|au)\s+/i, "")
      .replace(/^[\s,:;–—-]+|[\s,:;–—-]+$/g, "")
      .replace(/\s{2,}/g, " ");
    return { date, reste };
  }
  return null;
}

/** Les phrases d'une fiche qui ont une date, hors blocs de code, chacune avec sa date. */
function datesDeLaFiche(texte) {
  const trouvees = [];
  let dansCode = false;
  for (const ligne of texte.split("\n")) {
    if (/^\s*(```|~~~)/.test(ligne)) {
      dansCode = !dansCode;
      continue;
    }
    if (dansCode) continue;
    const propre = ligne.replace(/^\s*(#+|[-*+]|\d+\.)\s+/, "").replace(/[*_`]/g, "").trim();
    for (const phrase of propre.split(/(?<=[.!?])\s+/)) {
      const trouvee = dateDans(phrase);
      if (trouvee && trouvee.reste.length > 0) trouvees.push({ date: trouvee.date, texte: trouvee.reste.slice(0, 160) });
    }
  }
  return trouvees;
}

// --- Les blocs ```chronologie, comme en version 1. ---

function evenementsDuBloc(source) {
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

const frise = (liste, details) =>
  `<ol class="frise">${liste
    .map((e) => `<li><time>${echapper(e.date)}</time><span>${echapper(e.texte)}${details ? details(e) : ""}</span></li>`)
    .join("")}</ol>`;

kyfrev.bloc("chronologie", (source) => {
  const liste = evenementsDuBloc(source);
  if (liste.length === 0) return '<p class="secondaire">Chronologie vide : une ligne par date, « 1789 : Prise de la Bastille ».</p>';
  return frise(liste);
});

// --- Les données. ---

let donnees = null;

async function charger() {
  if (donnees) return donnees;
  const lu = await kyfrev.stockage.lire();
  donnees = lu && lu.format === FORMAT && Array.isArray(lu.frises) ? lu : { format: FORMAT, prochain: 1, frises: [] };
  return donnees;
}

const garder = () => kyfrev.stockage.ecrire(donnees);
const nouvelId = (prefixe) => `${prefixe}${donnees.prochain++}`;
const trouver = (id) => donnees.frises.find((f) => f.id === id);
const ranger = (f) => f.evenements.sort((a, b) => cle(a.date) - cle(b.date));

function creerFrise(nom) {
  const nouvelle = { id: nouvelId("f"), nom: nom.trim() || "Ma frise", evenements: [] };
  donnees.frises.push(nouvelle);
  return nouvelle;
}

function ajouterDate(f, date, texte, origine) {
  f.evenements.push({
    id: nouvelId("d"),
    date: date.trim(),
    texte: texte.trim(),
    fiche: origine?.fiche ?? null,
    titre: origine?.titre ?? null,
    debut: Number.isFinite(origine?.debut) ? origine.debut : null,
    fin: Number.isFinite(origine?.fin) ? origine.fin : null,
  });
  ranger(f);
}

function etendue(f) {
  if (f.evenements.length === 0) return "vide";
  const premiere = f.evenements[0].date;
  const derniere = f.evenements.at(-1).date;
  return premiere === derniere ? premiere : `de ${premiere} à ${derniere}`;
}

// --- L'état de la vue. ---

let ecran = { nom: "liste" };
let message = "";
let fiches = null;
let blocs = null;
let filtre = "";

const versListe = (texte) => {
  ecran = { nom: "liste" };
  message = texte ?? "";
};

async function listerFiches() {
  fiches = (await kyfrev.fiches.lister()).filter((f) => !f.archivee);
  return fiches;
}

async function lireBlocs() {
  const trouvees = [];
  for (const fiche of await listerFiches()) {
    const { texte } = await kyfrev.fiches.lire(fiche.id);
    if (!texte.includes("```chronologie")) continue;
    for (const bloc of texte.matchAll(/```chronologie[^\n]*\n([\s\S]*?)```/g)) {
      for (const e of evenementsDuBloc(bloc[1])) trouvees.push({ ...e, fiche: fiche.id, titre: fiche.titre });
    }
  }
  trouvees.sort((a, b) => cle(a.date) - cle(b.date));
  blocs = trouvees;
}

async function extraire(id, titre) {
  const { texte } = await kyfrev.fiches.lire(id);
  ecran = { nom: "extraire", fiche: id, titre, candidates: datesDeLaFiche(texte) };
  message = "";
}

// --- Le jeu : remettre dans l'ordre. ---

function melanger(liste) {
  const copie = [...liste];
  for (let i = copie.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [copie[i], copie[j]] = [copie[j], copie[i]];
  }
  return copie;
}

function jouer(f) {
  const ordre = f.evenements.map((e) => e.id);
  ecran = { nom: "jeu", frise: f.id, ordre, reste: melanger(ordre), places: [], bonnes: 0 };
  message = "";
}

// --- Les commandes. ---

kyfrev.commande("nouvelle", async () => {
  await charger();
  ecran = { nom: "nouvelle" };
  message = "";
  await kyfrev.ouvrirVue();
});

kyfrev.commande("ajouter", async (contexte) => {
  await charger();
  if (!contexte.texte) {
    versListe("Sélectionne un texte dans une fiche, en lecture, puis fais un clic droit dessus : « Ajouter à la frise ».");
  } else {
    const trouvee = dateDans(contexte.texte);
    ecran = {
      nom: "ajout",
      date: trouvee?.date ?? "",
      texte: trouvee?.reste || contexte.texte,
      origine: { fiche: contexte.id, titre: contexte.titre, debut: Number(contexte.debut), fin: Number(contexte.fin) },
    };
    message = trouvee ? "" : "Aucune date dans ce texte : écris-la.";
  }
  await kyfrev.ouvrirVue();
});

kyfrev.commande("extraire", async (contexte) => {
  await charger();
  if (contexte.id) {
    await extraire(contexte.id, contexte.titre ?? "");
  } else {
    await listerFiches();
    ecran = { nom: "choisir-fiche" };
    message = "";
  }
  await kyfrev.ouvrirVue();
});

// --- L'encart d'« Aujourd'hui » : ce qui s'est passé un jour comme celui-ci. ---

kyfrev.encart(async () => {
  await charger();
  const maintenant = new Date();
  const jour = maintenant.getDate();
  const mois = maintenant.getMonth() + 1;
  for (const f of donnees.frises) {
    for (const e of f.evenements) {
      const d = decomposer(e.date);
      if (d && d.mois === mois && d.jour === jour) {
        return { texte: `Il y a ${maintenant.getFullYear() - d.annee} ans : ${e.texte}`, detail: `${e.date}, dans la frise « ${f.nom} ».` };
      }
    }
  }
  return null;
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

action("liste", () => versListe());
action("nouvelle", () => {
  ecran = { nom: "nouvelle" };
  message = "";
});
action("creer", async (champs) => {
  const f = creerFrise(String(champs.nom ?? ""));
  await garder();
  ecran = { nom: "frise", frise: f.id };
  message = "Frise créée. Ajoute des dates depuis tes fiches : sélectionne un texte, clic droit, « Ajouter à la frise ».";
});
action("ouvrir", (_valeur, d) => {
  ecran = { nom: "frise", frise: d.frise };
  message = "";
});
action("renommer", async (valeur, d) => {
  const f = trouver(d.frise);
  if (f && String(valeur ?? "").trim()) {
    f.nom = String(valeur).trim();
    await garder();
  }
});
action("ajouter-date", async (champs, d) => {
  const f = trouver(d.frise);
  if (!f) return;
  ajouterDate(f, String(champs.date), String(champs.texte), null);
  await garder();
  message = `Date ajoutée : ${pluriel(f.evenements.length, "date", "dates")} dans la frise.`;
});
action("retirer", async (_valeur, d) => {
  const f = trouver(d.frise);
  if (!f) return;
  f.evenements = f.evenements.filter((e) => e.id !== d.date);
  await garder();
  message = "Date retirée.";
});
action("voir", async (_valeur, d) => {
  const e = trouver(d.frise)?.evenements.find((x) => x.id === d.date) ?? blocs?.[Number(d.bloc)];
  if (!e?.fiche) return;
  await kyfrev.fiches.ouvrir(e.fiche, Number.isFinite(e.debut) && e.debut !== null ? { debut: e.debut, fin: e.fin } : {});
});
action("supprimer", (_valeur, d) => {
  ecran = { nom: "supprimer", frise: d.frise };
  message = "";
});
action("confirmer-suppression", async (_valeur, d) => {
  const f = trouver(d.frise);
  donnees.frises = donnees.frises.filter((x) => x.id !== d.frise);
  await garder();
  versListe(f ? `Frise « ${f.nom} » supprimée. Les fiches n'ont pas changé.` : "");
});
action("ajout", async (champs) => {
  const { origine } = ecran;
  const f = trouver(champs.frise) ?? creerFrise(String(champs.nouvelle ?? "") || origine.titre || "Ma frise");
  ajouterDate(f, String(champs.date), String(champs.texte), origine);
  await garder();
  ecran = { nom: "frise", frise: f.id };
  message = `Date ajoutée à « ${f.nom} ».`;
});
action("extraire", async (champs) => {
  const fiche = fiches?.find((x) => x.id === champs.fiche);
  await extraire(String(champs.fiche), fiche?.titre ?? "");
});
action("garder", async (champs) => {
  const { candidates, fiche, titre } = ecran;
  const f = trouver(champs.frise) ?? creerFrise(String(champs.nouvelle ?? "") || titre);
  let ajoutees = 0;
  candidates.forEach((c, i) => {
    if (champs[`c${i}`] !== true) return;
    ajouterDate(f, String(champs[`d${i}`] ?? c.date), String(champs[`t${i}`] ?? c.texte), { fiche, titre });
    ajoutees++;
  });
  await garder();
  ecran = { nom: "frise", frise: f.id };
  message = `${pluriel(ajoutees, "date ajoutée", "dates ajoutées")} depuis « ${titre} ».`;
});
action("filtrer", (valeur) => {
  filtre = String(valeur ?? "");
});
action("blocs", async () => {
  await lireBlocs();
  ecran = { nom: "blocs" };
  message = "";
});
action("frise-des-blocs", async () => {
  if (blocs === null) await lireBlocs();
  const f = creerFrise("Dates des blocs");
  for (const e of blocs) ajouterDate(f, e.date, e.texte, { fiche: e.fiche, titre: e.titre });
  await garder();
  ecran = { nom: "frise", frise: f.id };
  message = `Frise créée avec ${pluriel(f.evenements.length, "date", "dates")} des blocs.`;
});
action("jouer", (_valeur, d) => {
  const f = trouver(d.frise);
  if (f) jouer(f);
});
action("placer", async (_valeur, d) => {
  const f = trouver(ecran.frise);
  if (!f) return versListe();
  const juste = ecran.ordre[ecran.places.length] === d.date;
  ecran.reste = ecran.reste.filter((id) => id !== d.date);
  ecran.places.push({ id: d.date, juste });
  if (juste) ecran.bonnes++;
  if (ecran.reste.length === 0) ecran = { ...ecran, nom: "fin-du-jeu" };
});

// --- Les écrans. ---

const bandeau = () => (message ? `<p><strong>${echapper(message)}</strong></p>` : "");

const choixDeFrise = (choisie, proposition) => `
  <label>Frise <select name="frise" aria-label="Frise">${donnees.frises
    .map((f) => `<option value="${f.id}"${f.id === choisie ? " selected" : ""}>${echapper(f.nom)}</option>`)
    .join("")}<option value="nouvelle"${choisie ? "" : " selected"}>Une nouvelle frise</option></select></label>
  <label>Nom de la nouvelle frise <input name="nouvelle" value="${echapper(proposition)}" placeholder="Utilisé seulement pour une nouvelle frise"></label>`;

function vueListe() {
  const cartes = donnees.frises
    .map(
      (f) => `
      <div class="carte">
        <h2>${echapper(f.nom)}</h2>
        <p class="secondaire">${pluriel(f.evenements.length, "date", "dates")} · ${echapper(etendue(f))}</p>
        <p class="actions">
          <button class="principal" data-action="ouvrir" data-frise="${f.id}">Ouvrir</button>
          <button class="discret" data-action="jouer" data-frise="${f.id}"${f.evenements.length < 2 ? " disabled" : ""}>Remettre dans l'ordre</button>
        </p>
      </div>`,
    )
    .join("");
  return `
    <h1>Chronologie</h1>
    ${bandeau()}
    <p class="actions"><button class="principal" data-action="nouvelle">Nouvelle frise</button><button class="discret" data-action="blocs">Dates des blocs</button></p>
    ${cartes || `<p class="secondaire">Aucune frise pour l'instant. Crée la première ici, ou sélectionne un texte daté dans une fiche : clic droit, « Ajouter à la frise ». Le clic droit d'une fiche propose aussi d'en extraire les dates.</p>`}`;
}

function vueNouvelle() {
  return `
    <h1>Nouvelle frise</h1>
    <form data-action="creer">
      <label>Nom <input name="nom" required placeholder="Révolution française"></label>
      <p class="actions"><button data-action="liste">Annuler</button><button class="principal">Créer</button></p>
    </form>`;
}

function vueFrise() {
  const f = trouver(ecran.frise);
  if (!f) return vueListe();
  const details = (e) => `${e.titre ? ` <span class="secondaire">· ${echapper(e.titre)}</span>` : ""}`;
  const lignes = f.evenements.length
    ? `<ol class="frise">${f.evenements
        .map(
          (e) => `<li><time>${echapper(e.date)}</time><span>${echapper(e.texte)}${details(e)}
            ${e.fiche ? `<button class="discret" data-action="voir" data-frise="${f.id}" data-date="${e.id}">Voir dans la fiche</button>` : ""}
            <button class="discret" data-action="retirer" data-frise="${f.id}" data-date="${e.id}" aria-label="Retirer ${echapper(e.date)}">Retirer</button></span></li>`,
        )
        .join("")}</ol>`
    : `<p class="secondaire">Aucune date pour l'instant.</p>`;
  return `
    <p class="actions"><button class="discret" data-action="liste">Retour aux frises</button></p>
    <h1>${echapper(f.nom)}</h1>
    <p class="secondaire">${pluriel(f.evenements.length, "date", "dates")} · ${echapper(etendue(f))}</p>
    ${bandeau()}
    <p class="actions"><button class="principal" data-action="jouer" data-frise="${f.id}"${f.evenements.length < 2 ? " disabled" : ""}>Remettre dans l'ordre</button></p>
    ${lignes}
    <h2>Ajouter une date</h2>
    <form data-action="ajouter-date" data-frise="${f.id}">
      <label>Date <input name="date" required placeholder="14 juillet 1789"></label>
      <label>Événement <input name="texte" required placeholder="Prise de la Bastille"></label>
      <p class="actions"><button class="principal">Ajouter</button></p>
    </form>
    <label>Renommer la frise <input data-action="renommer" data-frise="${f.id}" value="${echapper(f.nom)}" aria-label="Nom de la frise"></label>
    <p class="actions"><button class="discret" data-action="supprimer" data-frise="${f.id}">Supprimer cette frise</button></p>`;
}

function vueAjout() {
  const choisie = donnees.frises.at(-1)?.id ?? null;
  return `
    <h1>Ajouter à la frise</h1>
    <p class="secondaire">Depuis « ${echapper(ecran.origine.titre ?? "")} ».</p>
    ${bandeau()}
    <form data-action="ajout">
      ${choixDeFrise(choisie, ecran.origine.titre ?? "")}
      <label>Date <input name="date" required value="${echapper(ecran.date)}" placeholder="14 juillet 1789"></label>
      <label>Événement <textarea name="texte" required>${echapper(ecran.texte)}</textarea></label>
      <p class="actions"><button data-action="liste">Annuler</button><button class="principal">Ajouter</button></p>
    </form>`;
}

function vueChoisirFiche() {
  return `
    <h1>Extraire des dates</h1>
    <form data-action="extraire">
      <label>Fiche <select name="fiche" aria-label="Fiche">${fiches.map((f) => `<option value="${echapper(f.id)}">${echapper(f.titre)}</option>`).join("")}</select></label>
      <p class="actions"><button data-action="liste">Annuler</button><button class="principal">Chercher les dates</button></p>
    </form>`;
}

function vueExtraire() {
  const { candidates, titre } = ecran;
  if (candidates.length === 0) {
    return `
      <h1>Aucune date dans « ${echapper(titre)} »</h1>
      <p class="secondaire">Une date s'écrit « 1789 », « 14 juillet 1789 » ou « 14/07/1789 », dans une phrase.</p>
      <p class="actions"><button data-action="liste">Retour aux frises</button></p>`;
  }
  const lignes = candidates
    .map(
      (c, i) => `
      <div class="carte">
        <label><input type="checkbox" name="c${i}" checked> Garder</label>
        <label>Date <input name="d${i}" value="${echapper(c.date)}"></label>
        <label>Événement <input name="t${i}" value="${echapper(c.texte)}"></label>
      </div>`,
    )
    .join("");
  return `
    <h1>${pluriel(candidates.length, "date trouvée", "dates trouvées")} dans « ${echapper(titre)} »</h1>
    <p class="secondaire">Décoche celles à laisser, corrige les autres si besoin.</p>
    <form data-action="garder">
      ${choixDeFrise(donnees.frises.at(-1)?.id ?? null, titre)}
      ${lignes}
      <p class="actions"><button data-action="liste">Annuler</button><button class="principal">Ajouter les dates cochées</button></p>
    </form>`;
}

function vueBlocs() {
  const recherche = filtre.trim().toLowerCase();
  const visibles = recherche ? blocs.filter((e) => `${e.date} ${e.texte} ${e.titre}`.toLowerCase().includes(recherche)) : blocs;
  return `
    <p class="actions"><button class="discret" data-action="liste">Retour aux frises</button></p>
    <h1>Dates des blocs</h1>
    <p class="secondaire">${pluriel(blocs.length, "date", "dates")} des blocs chronologie de tes fiches, de la plus ancienne à la plus récente.</p>
    ${blocs.length
      ? `<input type="search" placeholder="Filtrer (date, événement, fiche)" value="${echapper(filtre)}" data-action="filtrer" aria-label="Filtrer les dates">
         <p class="actions"><button class="principal" data-action="frise-des-blocs">En faire une frise</button></p>
         ${visibles.length
           ? `<ol class="frise">${visibles
               .map((e) => `<li><time>${echapper(e.date)}</time><span>${echapper(e.texte)} <span class="secondaire">· ${echapper(e.titre)}</span> <button class="discret" data-action="voir" data-bloc="${blocs.indexOf(e)}">Voir dans la fiche</button></span></li>`)
               .join("")}</ol>`
           : `<p class="secondaire">Aucune date ne correspond.</p>`}`
      : `<p class="secondaire">Aucun bloc chronologie dans tes fiches.</p>`}`;
}

function vueSupprimer() {
  const f = trouver(ecran.frise);
  if (!f) return vueListe();
  return `
    <h1>Supprimer « ${echapper(f.nom)} » ?</h1>
    <p>Ses ${pluriel(f.evenements.length, "date disparaît", "dates disparaissent")} avec elle. Les fiches ne changent pas.</p>
    <p class="actions"><button class="principal" data-action="confirmer-suppression" data-frise="${f.id}">Supprimer</button><button data-action="ouvrir" data-frise="${f.id}">Annuler</button></p>`;
}

function vueJeu() {
  const f = trouver(ecran.frise);
  if (!f) return vueListe();
  const parId = (id) => f.evenements.find((e) => e.id === id);
  const places = ecran.places.map(({ id }) => parId(id)).filter(Boolean);
  const total = ecran.reste.length + ecran.places.length;
  const fin = ecran.nom === "fin-du-jeu";
  return `
    <h1>${echapper(f.nom)} : remettre dans l'ordre</h1>
    <p class="secondaire">${fin ? `Terminé : ${ecran.bonnes} sur ${total} à la bonne place.` : "Clique les événements du plus ancien au plus récent."}</p>
    <progress value="${ecran.places.length}" max="${total}" aria-label="${ecran.places.length} sur ${total} placés"></progress>
    ${places.length
      ? `<ol class="frise">${ecran.places
          .map(({ id, juste }) => {
            const e = parId(id);
            return e ? `<li><time>${echapper(e.date)}</time><span>${echapper(e.texte)} <span class="secondaire">· ${juste ? "à sa place" : "pas à sa place"}</span></span></li>` : "";
          })
          .join("")}</ol>`
      : ""}
    ${fin
      ? `${frise(f.evenements)}<p class="actions"><button class="principal" data-action="jouer" data-frise="${f.id}">Rejouer</button><button data-action="ouvrir" data-frise="${f.id}">Retour à la frise</button></p>`
      : `<p class="actions">${ecran.reste.map((id) => parId(id)).filter(Boolean).map((e) => `<button data-action="placer" data-date="${e.id}">${echapper(e.texte)}</button>`).join("")}</p>
         <p class="actions"><button class="discret" data-action="ouvrir" data-frise="${f.id}">Abandonner</button></p>`}`;
}

kyfrev.vue(async () => {
  await charger();
  switch (ecran.nom) {
    case "nouvelle":
      return vueNouvelle();
    case "frise":
      return vueFrise();
    case "ajout":
      return vueAjout();
    case "choisir-fiche":
      if (fiches === null) await listerFiches();
      return vueChoisirFiche();
    case "extraire":
      return vueExtraire();
    case "blocs":
      if (blocs === null) await lireBlocs();
      return vueBlocs();
    case "supprimer":
      return vueSupprimer();
    case "jeu":
    case "fin-du-jeu":
      return vueJeu();
    default:
      return vueListe();
  }
});
