// Vocabulaire, version 2, pour kyfRev (contrat 2).
//
// Un carnet par langue. En lecture, sélectionne un mot, clic droit, « Ajouter au vocabulaire » : l'extension demande sa
// traduction et garde la phrase de la fiche d'où il vient. Les mots reviennent selon le cycle des fiches (J1, J3, J7, J14,
// J30) ; un mot raté revient le lendemain. On répond en dévoilant la traduction ou en l'écrivant, dans un sens ou dans
// l'autre.
//
// Les blocs de la version 1 restent lisibles, et leurs mots entrent dans un carnet en un clic :
//
//   ```vocabulaire
//   the cat = le chat
//   to run = courir
//   ```
"use strict";

const FORMAT = 2;
const CYCLE = [1, 3, 7, 14, 30];

const echapper = (texte) =>
  String(texte ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);

const pluriel = (n, un, plusieurs) => `${n} ${n > 1 ? plusieurs : un}`;

/** La date du jour, ici, en AAAA-MM-JJ ; décalée de quelques jours si demandé. */
function jour(decalage = 0) {
  const d = new Date();
  d.setDate(d.getDate() + decalage);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

const enDate = (iso) => (iso ? iso.split("-").reverse().join("/") : "");

/** Une réponse écrite comparée sans casse, sans accents, sans ponctuation ni article entre parenthèses. */
const normaliser = (texte) =>
  String(texte ?? "")
    .toLocaleLowerCase("fr")
    .normalize("NFD")
    .replace(/\p{M}/gu, "")
    .replace(/\([^)]*\)/g, " ")
    .replace(/[^\p{L}\p{N}]+/gu, " ")
    .trim();

const justes = (ecrite, attendue) => attendue.split(/[;,/]/).some((une) => normaliser(une) !== "" && normaliser(une) === normaliser(ecrite));

// --- Les blocs ```vocabulaire, comme en version 1. ---

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
    .map((p) => `<tr><td>${echapper(p.terme)}</td><td><details><summary>Afficher</summary>${echapper(p.traduction)}</details></td></tr>`)
    .join("");
  return `<table><thead><tr><th>Terme</th><th>Traduction</th></tr></thead><tbody>${lignes}</tbody></table>`;
});

// --- Les données. ---

let donnees = null;

async function charger() {
  if (donnees) return donnees;
  const lu = await kyfrev.stockage.lire();
  if (lu && lu.format === FORMAT && Array.isArray(lu.carnets)) {
    donnees = lu;
  } else {
    donnees = { format: FORMAT, prochain: 1, carnets: [] };
    if (lu && typeof lu === "object") donnees.ancien = lu;
  }
  return donnees;
}

const garder = () => kyfrev.stockage.ecrire(donnees);
const nouvelId = (prefixe) => `${prefixe}${donnees.prochain++}`;
const trouver = (id) => donnees.carnets.find((c) => c.id === id);
const dus = (carnet) => carnet.mots.filter((m) => m.prochaine <= jour());
const tousDus = () => donnees.carnets.reduce((total, c) => total + dus(c).length, 0);

function creerCarnet(nom) {
  const carnet = { id: nouvelId("c"), nom: nom.trim() || "Mon carnet", sens: "terme", mode: "devoiler", mots: [] };
  donnees.carnets.push(carnet);
  return carnet;
}

function ajouterMot(carnet, terme, traduction, origine) {
  carnet.mots.push({
    id: nouvelId("m"),
    terme: terme.trim(),
    traduction: traduction.trim(),
    phrase: (origine?.phrase ?? "").trim(),
    fiche: origine?.fiche ?? null,
    titre: origine?.titre ?? null,
    debut: Number.isFinite(origine?.debut) ? origine.debut : null,
    fin: Number.isFinite(origine?.fin) ? origine.fin : null,
    etape: 0,
    prochaine: jour(),
    sus: 0,
    rates: 0,
  });
}

/** Le mot su passe à l'étape suivante du cycle ; raté, il revient demain. */
function noter(mot, su) {
  if (su) {
    mot.sus++;
    mot.prochaine = jour(CYCLE[Math.min(mot.etape, CYCLE.length - 1)]);
    mot.etape = Math.min(mot.etape + 1, CYCLE.length);
  } else {
    mot.rates++;
    mot.etape = 0;
    mot.prochaine = jour(1);
  }
}

/** La phrase de la fiche où se trouve le mot, sans la mise en forme Markdown. */
async function phraseDe(fiche, terme) {
  try {
    const { texte } = await kyfrev.fiches.lire(fiche);
    const propre = texte.replace(/```[\s\S]*?```/g, " ").replace(/[*_`#>]/g, "");
    const phrase = propre.split(/(?<=[.!?])\s+|\n+/).find((p) => p.includes(terme));
    return phrase ? phrase.trim().slice(0, 240) : "";
  } catch {
    return "";
  }
}

// --- L'état de la vue. ---

let ecran = { nom: "liste" };
let message = "";
let blocs = null;

const versListe = (texte) => {
  ecran = { nom: "liste" };
  message = texte ?? "";
};

async function lireBlocs() {
  const trouves = [];
  for (const fiche of (await kyfrev.fiches.lister()).filter((f) => !f.archivee)) {
    const { texte } = await kyfrev.fiches.lire(fiche.id);
    if (!texte.includes("```vocabulaire")) continue;
    for (const bloc of texte.matchAll(/```vocabulaire[^\n]*\n([\s\S]*?)```/g)) {
      for (const p of paires(bloc[1])) trouves.push({ ...p, fiche: fiche.id, titre: fiche.titre });
    }
  }
  blocs = trouves;
}

/** Une révision : les mots dus, d'un carnet ou de tous, les plus en retard d'abord. */
function reviser(carnets, titre) {
  const suite = carnets.flatMap((c) => dus(c).map((m) => [c.id, m.id, m.prochaine]));
  suite.sort((a, b) => a[2].localeCompare(b[2]));
  ecran = { nom: "revision", titre, carnet: carnets.length === 1 ? carnets[0].id : null, suite: suite.map(([c, m]) => [c, m]), position: 0, etat: "question", ecrite: "", sus: 0, rates: [] };
  message = "";
}

const motDe = ([carnetId, id]) => {
  const carnet = trouver(carnetId);
  return { carnet, mot: carnet?.mots.find((m) => m.id === id) };
};

async function suivant(su) {
  const { mot } = motDe(ecran.suite[ecran.position]);
  if (mot) {
    noter(mot, su);
    if (su) ecran.sus++;
    else ecran.rates.push(mot.terme);
  }
  ecran.position++;
  ecran.etat = "question";
  ecran.ecrite = "";
  if (ecran.position >= ecran.suite.length) ecran = { ...ecran, nom: "bilan" };
  await garder();
}

// --- Les commandes. ---

kyfrev.commande("reviser", async () => {
  await charger();
  if (tousDus() === 0) versListe("Aucun mot à revoir aujourd'hui.");
  else reviser(donnees.carnets.filter((c) => dus(c).length > 0), "Mots à revoir");
  await kyfrev.ouvrirVue();
});

kyfrev.commande("nouveau", async () => {
  await charger();
  ecran = { nom: "nouveau" };
  message = "";
  await kyfrev.ouvrirVue();
});

kyfrev.commande("ajouter", async (contexte) => {
  await charger();
  if (!contexte.texte) {
    versListe("Sélectionne un mot dans une fiche, en lecture, puis fais un clic droit dessus : « Ajouter au vocabulaire ».");
  } else {
    const terme = contexte.texte.trim().slice(0, 120);
    ecran = {
      nom: "ajout",
      terme,
      origine: { fiche: contexte.id, titre: contexte.titre, debut: Number(contexte.debut), fin: Number(contexte.fin), phrase: contexte.id ? await phraseDe(contexte.id, terme) : "" },
    };
    message = "";
  }
  await kyfrev.ouvrirVue();
});

// --- Le compteur du menu et l'encart d'« Aujourd'hui ». ---

kyfrev.compteur(async () => {
  await charger();
  return tousDus();
});

kyfrev.encart(async () => {
  await charger();
  const total = tousDus();
  if (total === 0) return null;
  const avec = donnees.carnets.filter((c) => dus(c).length > 0);
  return { texte: pluriel(total, "mot à revoir", "mots à revoir"), detail: `Dans ${avec.map((c) => `« ${c.nom} »`).join(", ")}.` };
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
action("nouveau", () => {
  ecran = { nom: "nouveau" };
  message = "";
});
action("creer", async (champs) => {
  const carnet = creerCarnet(String(champs.nom ?? ""));
  await garder();
  ecran = { nom: "carnet", carnet: carnet.id };
  message = "Carnet créé. Ajoute des mots ici, ou sélectionne un mot dans une fiche : clic droit, « Ajouter au vocabulaire ».";
});
action("ouvrir", (_valeur, d) => {
  ecran = { nom: "carnet", carnet: d.carnet };
  message = "";
});
action("ajouter-mot", async (champs, d) => {
  const carnet = trouver(d.carnet);
  if (!carnet) return;
  ajouterMot(carnet, String(champs.terme), String(champs.traduction), { phrase: String(champs.phrase ?? "") });
  await garder();
  message = `« ${String(champs.terme).trim()} » ajouté : ${pluriel(carnet.mots.length, "mot", "mots")} dans le carnet.`;
});
action("retirer", async (_valeur, d) => {
  const carnet = trouver(d.carnet);
  if (!carnet) return;
  carnet.mots = carnet.mots.filter((m) => m.id !== d.mot);
  await garder();
  message = "Mot retiré.";
});
action("sens", async (_valeur, d) => {
  const carnet = trouver(d.carnet);
  if (!carnet) return;
  carnet.sens = carnet.sens === "terme" ? "traduction" : "terme";
  await garder();
});
action("mode", async (_valeur, d) => {
  const carnet = trouver(d.carnet);
  if (!carnet) return;
  carnet.mode = carnet.mode === "ecrire" ? "devoiler" : "ecrire";
  await garder();
});
action("renommer", async (valeur, d) => {
  const carnet = trouver(d.carnet);
  if (carnet && String(valeur ?? "").trim()) {
    carnet.nom = String(valeur).trim();
    await garder();
  }
});
action("supprimer", (_valeur, d) => {
  ecran = { nom: "supprimer", carnet: d.carnet };
  message = "";
});
action("confirmer-suppression", async (_valeur, d) => {
  const carnet = trouver(d.carnet);
  donnees.carnets = donnees.carnets.filter((c) => c.id !== d.carnet);
  await garder();
  versListe(carnet ? `Carnet « ${carnet.nom} » supprimé. Les fiches n'ont pas changé.` : "");
});
action("ajout", async (champs) => {
  const { origine, terme } = ecran;
  const carnet = trouver(champs.carnet) ?? creerCarnet(String(champs.nouveau ?? ""));
  ajouterMot(carnet, String(champs.terme ?? terme), String(champs.traduction), { ...origine, phrase: String(champs.phrase ?? origine.phrase) });
  await garder();
  ecran = { nom: "carnet", carnet: carnet.id };
  message = `« ${String(champs.terme ?? terme).trim()} » ajouté au carnet « ${carnet.nom} ».`;
});
action("blocs", async () => {
  await lireBlocs();
  ecran = { nom: "blocs" };
  message = "";
});
action("importer", async (champs) => {
  if (blocs === null) await lireBlocs();
  const carnet = trouver(champs.carnet) ?? creerCarnet(String(champs.nouveau ?? "") || "Mots des blocs");
  const deja = new Set(carnet.mots.map((m) => normaliser(m.terme)));
  let ajoutes = 0;
  for (const p of blocs) {
    if (deja.has(normaliser(p.terme))) continue;
    ajouterMot(carnet, p.terme, p.traduction, { fiche: p.fiche, titre: p.titre });
    deja.add(normaliser(p.terme));
    ajoutes++;
  }
  await garder();
  ecran = { nom: "carnet", carnet: carnet.id };
  message = `${pluriel(ajoutes, "mot ajouté", "mots ajoutés")} depuis les blocs.`;
});
action("reviser", (_valeur, d) => {
  const carnet = trouver(d.carnet);
  if (!carnet) return;
  if (dus(carnet).length === 0) {
    message = "Aucun mot à revoir aujourd'hui dans ce carnet.";
    return;
  }
  reviser([carnet], carnet.nom);
});
action("afficher", () => {
  ecran.etat = "reponse";
});
action("verifier", (champs) => {
  const ecrite = String((typeof champs === "object" && champs !== null ? champs.reponse : champs) ?? "");
  const { carnet, mot } = motDe(ecran.suite[ecran.position]);
  if (!mot) return;
  const attendue = carnet.sens === "terme" ? mot.traduction : mot.terme;
  ecran.ecrite = ecrite;
  ecran.etat = justes(ecrite, attendue) ? "juste" : "faux";
});
action("su", () => suivant(true));
action("a-revoir", () => suivant(false));
action("suivant", () => suivant(ecran.etat === "juste"));
action("quitter", () => versListe());
action("voir", async (_valeur, d) => {
  const mot = trouver(d.carnet)?.mots.find((m) => m.id === d.mot);
  if (!mot?.fiche) return;
  await kyfrev.fiches.ouvrir(mot.fiche, mot.debut === null ? {} : { debut: mot.debut, fin: mot.fin });
});

// --- Les écrans. ---

const bandeau = () => (message ? `<p><strong>${echapper(message)}</strong></p>` : "");

const choixDeCarnet = (choisi) => `
  <label>Carnet <select name="carnet" aria-label="Carnet">${donnees.carnets
    .map((c) => `<option value="${c.id}"${c.id === choisi ? " selected" : ""}>${echapper(c.nom)}</option>`)
    .join("")}<option value="nouveau"${choisi ? "" : " selected"}>Un nouveau carnet</option></select></label>
  <label>Nom du nouveau carnet <input name="nouveau" placeholder="Anglais"></label>`;

function vueListe() {
  const cartes = donnees.carnets
    .map((c) => {
      const aRevoir = dus(c).length;
      return `
      <div class="carte">
        <h2>${echapper(c.nom)}</h2>
        <p class="secondaire">${aRevoir ? `<span class="pastille">${aRevoir} à revoir</span> ` : `<span class="pastille">À jour</span> `}${pluriel(c.mots.length, "mot", "mots")}</p>
        <p class="actions">
          <button class="principal" data-action="reviser" data-carnet="${c.id}"${aRevoir ? "" : " disabled"}>Réviser</button>
          <button class="discret" data-action="ouvrir" data-carnet="${c.id}">Ouvrir</button>
        </p>
      </div>`;
    })
    .join("");
  return `
    <h1>Vocabulaire</h1>
    ${bandeau()}
    <p class="actions"><button class="principal" data-action="nouveau">Nouveau carnet</button><button class="discret" data-action="blocs">Mots des blocs</button></p>
    ${cartes || `<p class="secondaire">Aucun carnet pour l'instant. Crée le premier ici, ou sélectionne un mot dans une fiche : clic droit, « Ajouter au vocabulaire ».</p>`}`;
}

function vueNouveau() {
  return `
    <h1>Nouveau carnet</h1>
    <form data-action="creer">
      <label>Nom <input name="nom" required placeholder="Anglais"></label>
      <p class="actions"><button data-action="liste">Annuler</button><button class="principal">Créer</button></p>
    </form>`;
}

function vueCarnet() {
  const carnet = trouver(ecran.carnet);
  if (!carnet) return vueListe();
  const aRevoir = dus(carnet).length;
  const lignes = carnet.mots
    .map(
      (m) => `<tr><td>${echapper(m.terme)}</td><td>${echapper(m.traduction)}</td><td>${m.prochaine <= jour() ? "aujourd'hui" : enDate(m.prochaine)}</td>
        <td>${m.fiche ? `<button class="discret" data-action="voir" data-carnet="${carnet.id}" data-mot="${m.id}">Voir dans la fiche</button>` : ""}<button class="discret" data-action="retirer" data-carnet="${carnet.id}" data-mot="${m.id}" aria-label="Retirer ${echapper(m.terme)}">Retirer</button></td></tr>`,
    )
    .join("");
  return `
    <p class="actions"><button class="discret" data-action="liste">Retour aux carnets</button></p>
    <h1>${echapper(carnet.nom)}</h1>
    <p class="secondaire">${pluriel(carnet.mots.length, "mot", "mots")} · ${aRevoir ? `${aRevoir} à revoir aujourd'hui` : "rien à revoir aujourd'hui"} · révisions à J1, J3, J7, J14 et J30</p>
    ${bandeau()}
    <p class="actions">
      <button class="principal" data-action="reviser" data-carnet="${carnet.id}"${aRevoir ? "" : " disabled"}>Réviser (${aRevoir})</button>
      <button class="discret" data-action="sens" data-carnet="${carnet.id}">${carnet.sens === "terme" ? "Sens : terme → traduction" : "Sens : traduction → terme"}</button>
      <button class="discret" data-action="mode" data-carnet="${carnet.id}">${carnet.mode === "ecrire" ? "Réponse : à écrire" : "Réponse : à dévoiler"}</button>
    </p>
    ${carnet.mots.length ? `<table><thead><tr><th>Terme</th><th>Traduction</th><th>Prochaine révision</th><th>Actions</th></tr></thead><tbody>${lignes}</tbody></table>` : `<p class="secondaire">Aucun mot pour l'instant.</p>`}
    <h2>Ajouter un mot</h2>
    <form data-action="ajouter-mot" data-carnet="${carnet.id}">
      <label>Terme <input name="terme" required placeholder="the weather"></label>
      <label>Traduction <input name="traduction" required placeholder="le temps (qu'il fait)"></label>
      <label>Phrase d'exemple <input name="phrase" placeholder="facultative"></label>
      <p class="actions"><button class="principal">Ajouter</button></p>
    </form>
    <label>Renommer le carnet <input data-action="renommer" data-carnet="${carnet.id}" value="${echapper(carnet.nom)}" aria-label="Nom du carnet"></label>
    <p class="actions"><button class="discret" data-action="supprimer" data-carnet="${carnet.id}">Supprimer ce carnet</button></p>`;
}

function vueAjout() {
  return `
    <h1>Ajouter au vocabulaire</h1>
    <p class="secondaire">Depuis « ${echapper(ecran.origine.titre ?? "")} ».</p>
    <form data-action="ajout">
      ${choixDeCarnet(donnees.carnets.at(-1)?.id ?? null)}
      <label>Terme <input name="terme" required value="${echapper(ecran.terme)}"></label>
      <label>Traduction <input name="traduction" required placeholder="Sa traduction"></label>
      <label>Phrase d'origine <textarea name="phrase">${echapper(ecran.origine.phrase)}</textarea></label>
      <p class="actions"><button data-action="liste">Annuler</button><button class="principal">Ajouter</button></p>
    </form>`;
}

function vueBlocs() {
  return `
    <p class="actions"><button class="discret" data-action="liste">Retour aux carnets</button></p>
    <h1>Mots des blocs</h1>
    ${blocs.length
      ? `<p class="secondaire">${pluriel(blocs.length, "mot", "mots")} dans les blocs vocabulaire de tes fiches.</p>
         <table><thead><tr><th>Terme</th><th>Traduction</th><th>Fiche</th></tr></thead><tbody>${blocs
           .map((p) => `<tr><td>${echapper(p.terme)}</td><td>${echapper(p.traduction)}</td><td>${echapper(p.titre)}</td></tr>`)
           .join("")}</tbody></table>
         <form data-action="importer">
           ${choixDeCarnet(donnees.carnets.at(-1)?.id ?? null)}
           <p class="actions"><button class="principal">Les ajouter au carnet</button></p>
         </form>`
      : `<p class="secondaire">Aucun bloc vocabulaire dans tes fiches.</p>`}`;
}

function vueSupprimer() {
  const carnet = trouver(ecran.carnet);
  if (!carnet) return vueListe();
  return `
    <h1>Supprimer « ${echapper(carnet.nom)} » ?</h1>
    <p>Ses ${pluriel(carnet.mots.length, "mot disparaît", "mots disparaissent")} avec lui. Les fiches ne changent pas.</p>
    <p class="actions"><button class="principal" data-action="confirmer-suppression" data-carnet="${carnet.id}">Supprimer</button><button data-action="ouvrir" data-carnet="${carnet.id}">Annuler</button></p>`;
}

function vueRevision() {
  const total = ecran.suite.length;
  const { carnet, mot } = motDe(ecran.suite[ecran.position]);
  if (!mot) {
    ecran.position++;
    return ecran.position >= total ? vueListe() : vueRevision();
  }
  const question = carnet.sens === "terme" ? mot.terme : mot.traduction;
  const reponse = carnet.sens === "terme" ? mot.traduction : mot.terme;
  const numero = ecran.position + 1;
  const origine = `${mot.phrase ? `<p class="secondaire">« ${echapper(mot.phrase)} »</p>` : ""}${mot.titre ? `<p class="secondaire">Vient de « ${echapper(mot.titre)} »</p>` : ""}`;
  let corps;
  if (ecran.etat === "question" && carnet.mode === "ecrire") {
    corps = `
      <form data-action="verifier">
        <label>${carnet.sens === "terme" ? "Traduction" : "Terme"} <input name="reponse" required placeholder="Écris la réponse"></label>
        <p class="actions"><button class="principal">Vérifier</button><button data-action="a-revoir">Je ne sais pas</button></p>
      </form>`;
  } else if (ecran.etat === "question") {
    corps = `<p class="actions"><button class="principal" data-action="afficher">Afficher la réponse</button><button class="discret" data-action="quitter">Quitter</button></p>`;
  } else if (ecran.etat === "reponse") {
    corps = `<p class="reponse">${echapper(reponse)}</p>${origine}
      <p class="actions"><button class="principal" data-action="su">Je savais</button><button data-action="a-revoir">À revoir</button></p>`;
  } else {
    corps = `<p class="reponse">${echapper(reponse)}</p>
      <p>${ecran.etat === "juste" ? "<strong>Juste.</strong>" : `<strong>Pas tout à fait :</strong> tu as écrit « ${echapper(ecran.ecrite)} ».`}</p>${origine}
      <p class="actions"><button class="principal" data-action="suivant">Suivant</button>${ecran.etat === "faux" ? `<button data-action="su">J'avais juste</button>` : ""}</p>`;
  }
  return `
    <h1>${echapper(ecran.titre)}</h1>
    <p class="secondaire">Mot ${numero} sur ${total}${ecran.carnet ? "" : ` · ${echapper(carnet.nom)}`}</p>
    <progress value="${ecran.position}" max="${total}" aria-label="Mot ${numero} sur ${total}"></progress>
    <div class="carte"><p class="question">${echapper(question)}</p></div>
    ${corps}`;
}

function vueBilan() {
  const total = ecran.suite.length;
  return `
    <h1>${echapper(ecran.titre)}</h1>
    <p><strong>Terminé : ${ecran.sus} sur ${total}</strong></p>
    <progress value="${ecran.sus}" max="${total}" aria-label="${ecran.sus} sur ${total}"></progress>
    ${ecran.rates.length
      ? `<p class="secondaire">${pluriel(ecran.rates.length, "mot revient", "mots reviennent")} demain : ${ecran.rates.map(echapper).join(", ")}.</p>`
      : `<p class="secondaire">Tout est su : chaque mot reviendra à sa prochaine étape.</p>`}
    <p class="actions"><button class="principal" data-action="liste">Retour aux carnets</button></p>`;
}

kyfrev.vue(async () => {
  await charger();
  switch (ecran.nom) {
    case "nouveau":
      return vueNouveau();
    case "carnet":
      return vueCarnet();
    case "ajout":
      return vueAjout();
    case "blocs":
      if (blocs === null) await lireBlocs();
      return vueBlocs();
    case "supprimer":
      return vueSupprimer();
    case "revision":
      return vueRevision();
    case "bilan":
      return vueBilan();
    default:
      return vueListe();
  }
});
