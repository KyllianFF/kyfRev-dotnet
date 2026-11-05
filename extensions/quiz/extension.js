// Quiz, version 2, pour kyfRev (contrat 2).
//
// On crée un quiz depuis son menu, puis on prend chaque question et sa réponse dans une fiche, à la chaîne : c'est la
// sélection guidée de kyfRev. Ce qui manque dans la fiche s'écrit à la main. On peut aussi sélectionner un texte en lecture,
// puis clic droit : « Ajouter au quiz ». Les questions ratées reviennent en premier, et « Voir dans la fiche » rouvre le
// passage d'où vient une question.
//
// Les blocs ```quiz de la version 1 restent lisibles dans les fiches, et chacun peut devenir un quiz en un clic :
//
//   ```quiz
//   Q: Qui convoque les états généraux en 1789 ?
//   R: Louis XVI
//   ```
"use strict";

const FORMAT = 2;

/** Le HTML rendu est nettoyé par kyfRev, mais le texte doit rester du texte. */
const echapper = (texte) =>
  String(texte ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);

const pluriel = (n, un, plusieurs) => `${n} ${n > 1 ? plusieurs : un}`;
const aujourdHui = () => new Date().toISOString().slice(0, 10);
const enDate = (iso) => (iso ? iso.split("-").reverse().join("/") : "");
const etiquettesDe = (texte) => [...new Set(String(texte ?? "").split(",").map((e) => e.trim().replace(/^#/, "")).filter(Boolean))];

// --- Les blocs ```quiz des fiches, comme en version 1. ---

/** Questions d'un bloc : des paires « Q: … » / « R: … ». */
function questionsDuBloc(source) {
  const liste = [];
  for (const ligne of source.split("\n")) {
    const q = /^\s*Q\s*:\s*(.+)$/i.exec(ligne);
    const r = /^\s*R\s*:\s*(.+)$/i.exec(ligne);
    if (q) liste.push({ question: q[1].trim(), reponse: "" });
    else if (r && liste.length > 0) liste[liste.length - 1].reponse = r[1].trim();
  }
  return liste;
}

kyfrev.bloc("quiz", (source) => {
  const liste = questionsDuBloc(source);
  if (liste.length === 0) return '<p class="secondaire">Quiz vide : écrivez des lignes « Q: … » puis « R: … ».</p>';
  const items = liste
    .map((q) => `<li><details><summary>${echapper(q.question)}</summary><p>${echapper(q.reponse || "(pas de réponse)")}</p></details></li>`)
    .join("");
  return `<p class="titre-extension">Quiz · ${pluriel(liste.length, "question", "questions")}</p><ol class="quiz">${items}</ol>`;
});

// --- Les données, gardées dans le coffre. ---

let donnees = null;

/** Lit les quiz ; les données de la version 1 (un score) sont gardées à part. */
async function charger() {
  if (donnees) return donnees;
  const lu = await kyfrev.stockage.lire();
  if (lu && lu.format === FORMAT && Array.isArray(lu.quiz)) {
    donnees = lu;
  } else {
    donnees = { format: FORMAT, prochain: 1, quiz: [] };
    if (lu && typeof lu === "object") donnees.ancien = lu;
  }
  return donnees;
}

const garder = () => kyfrev.stockage.ecrire(donnees);
const nouvelId = (prefixe) => `${prefixe}${donnees.prochain++}`;
const trouver = (id) => donnees.quiz.find((q) => q.id === id);
const aRevoir = (quiz) => quiz.questions.filter((q) => q.aRevoir).length;
const toutARevoir = () => donnees.quiz.reduce((total, q) => total + aRevoir(q), 0);

function creerQuiz(nom, etiquettes, ordre) {
  const quiz = { id: nouvelId("q"), nom, etiquettes, ordre: ordre === "fiche" ? "fiche" : "hasard", cree: aujourdHui(), questions: [], dernier: null };
  donnees.quiz.push(quiz);
  return quiz;
}

function ajouterQuestion(quiz, question, reponse, origine) {
  quiz.questions.push({
    id: nouvelId("k"),
    question: question.trim(),
    reponse: (reponse ?? "").trim(),
    fiche: origine?.fiche ?? null,
    titre: origine?.titre ?? null,
    debut: Number.isFinite(origine?.debut) ? origine.debut : null,
    fin: Number.isFinite(origine?.fin) ? origine.fin : null,
    aRevoir: false,
  });
}

// --- L'état de la vue. ---

let ecran = { nom: "liste" };
let message = "";
let recherche = "";
let etiquette = "";
let trouves = null;
let fiches = null;

async function listerFiches() {
  fiches = (await kyfrev.fiches.lister()).filter((f) => !f.archivee);
  return fiches;
}

/** Les fiches qui ont des blocs ```quiz et pas encore leur quiz. */
async function chercherBlocs() {
  const deja = new Set(donnees.quiz.map((q) => q.depuisBloc).filter(Boolean));
  const liste = [];
  for (const fiche of await listerFiches()) {
    if (deja.has(fiche.id)) continue;
    const { texte } = await kyfrev.fiches.lire(fiche.id);
    if (!texte.includes("```quiz")) continue;
    const questions = [];
    for (const bloc of texte.matchAll(/```quiz[^\n]*\n([\s\S]*?)```/g)) questions.push(...questionsDuBloc(bloc[1]));
    if (questions.length > 0) liste.push({ id: fiche.id, titre: fiche.titre, etiquettes: fiche.etiquettes, questions });
  }
  trouves = liste;
}

function versListe(texte) {
  ecran = { nom: "liste" };
  message = texte ?? "";
}

// --- Prendre les questions dans une fiche, à la chaîne. ---

/** La sélection guidée : la question, puis sa réponse, jusqu'à « Terminer ». Rend le nombre de questions prises. */
async function prendre(quiz, fiche) {
  let prises = 0;
  for (;;) {
    const numero = quiz.questions.length + 1;
    const question = await kyfrev.selection.demander(`${quiz.nom} · question ${numero} : sélectionne la question`, { fiche });
    if (question.choix === "terminer") break;
    if (question.choix === "passer" || !question.texte) continue;
    const reponse = await kyfrev.selection.demander(`${quiz.nom} · question ${numero} : sélectionne la réponse, ou écris-la`, { fiche: question.fiche ?? fiche });
    if (reponse.choix === "terminer") break;
    ajouterQuestion(quiz, question.texte, reponse.choix === "passer" ? "" : reponse.texte, question);
    prises++;
    await garder();
  }
  return prises;
}

async function commencer(quiz, fiche, mode) {
  if (mode === "ici") {
    ecran = { nom: "ecrire", quiz: quiz.id };
    message = "";
    return;
  }
  const prises = await prendre(quiz, fiche);
  ecran = { nom: "modifier", quiz: quiz.id };
  message = `${pluriel(prises, "question prise", "questions prises")} dans la fiche. Tu peux les corriger ici.`;
  await kyfrev.ouvrirVue();
}

// --- Jouer. ---

function melanger(liste) {
  const copie = [...liste];
  for (let i = copie.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [copie[i], copie[j]] = [copie[j], copie[i]];
  }
  return copie;
}

/** Une partie : les questions ratées d'abord, puis les autres, au hasard ou dans l'ordre du quiz. */
function lancer(quizListe, titre, seulementRatees) {
  const paires = [];
  for (const quiz of quizListe) {
    const choisies = quiz.questions.filter((q) => !seulementRatees || q.aRevoir);
    const ranger = (liste) => (quiz.ordre === "fiche" ? liste : melanger(liste));
    paires.push(...ranger(choisies.filter((q) => q.aRevoir)).map((q) => [quiz.id, q.id]), ...ranger(choisies.filter((q) => !q.aRevoir)).map((q) => [quiz.id, q.id]));
  }
  ecran = { nom: "jeu", titre, quiz: quizListe.length === 1 ? quizListe[0].id : null, suite: paires, position: 0, visible: false, bonnes: 0, ratees: [] };
  message = "";
}

const questionDe = ([quizId, id]) => {
  const quiz = trouver(quizId);
  return { quiz, question: quiz?.questions.find((q) => q.id === id) };
};

async function repondre(su) {
  const { quiz, question } = questionDe(ecran.suite[ecran.position]);
  if (question) {
    question.aRevoir = !su;
    if (su) ecran.bonnes++;
    else ecran.ratees.push(question.question);
  }
  ecran.position++;
  ecran.visible = false;
  if (ecran.position >= ecran.suite.length) {
    if (ecran.quiz && quiz) quiz.dernier = { bonnes: ecran.bonnes, total: ecran.suite.length, date: aujourdHui() };
    ecran = { ...ecran, nom: "resultat" };
  }
  await garder();
}

// --- Les commandes : Ctrl+K, le clic droit d'une fiche, et celui d'un texte sélectionné. ---

kyfrev.commande("nouveau", async () => {
  await charger();
  await listerFiches();
  ecran = { nom: "nouveau" };
  message = "";
  await kyfrev.ouvrirVue();
});

kyfrev.commande("depuis-fiche", async (contexte) => {
  await charger();
  await listerFiches();
  ecran = { nom: "nouveau", fiche: contexte.id ?? null, nomPropose: contexte.titre ?? "" };
  message = "";
  await kyfrev.ouvrirVue();
});

kyfrev.commande("revoir", async () => {
  await charger();
  const avecRatees = donnees.quiz.filter((q) => aRevoir(q) > 0);
  if (avecRatees.length === 0) versListe("Aucune question à revoir : tout est à jour.");
  else lancer(avecRatees, "Questions à revoir", true);
  await kyfrev.ouvrirVue();
});

kyfrev.commande("ajouter", async (contexte) => {
  await charger();
  if (!contexte.texte) {
    versListe("Sélectionne un texte dans une fiche, en lecture, puis fais un clic droit dessus : « Ajouter au quiz ».");
  } else {
    ecran = {
      nom: "ajout",
      origine: { fiche: contexte.id, titre: contexte.titre, debut: Number(contexte.debut), fin: Number(contexte.fin) },
      texte: contexte.texte,
    };
    message = "";
  }
  await kyfrev.ouvrirVue();
});

// --- Le compteur du menu et l'encart d'« Aujourd'hui ». ---

kyfrev.compteur(async () => {
  await charger();
  return toutARevoir();
});

kyfrev.encart(async () => {
  await charger();
  const total = toutARevoir();
  if (total === 0) return null;
  const premier = [...donnees.quiz].sort((a, b) => aRevoir(b) - aRevoir(a))[0];
  const autres = donnees.quiz.filter((q) => q !== premier && aRevoir(q) > 0).length;
  return {
    texte: `${pluriel(total, "question à revoir", "questions à revoir")}`,
    detail: `Dans « ${premier.nom} »${autres > 0 ? ` et ${pluriel(autres, "autre quiz", "autres quiz")}` : ""}.`,
  };
});

// --- Les actions de la vue. ---

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
action("chercher", (valeur) => {
  recherche = String(valeur ?? "").trim();
});
action("etiquette", (valeur) => {
  etiquette = valeur === etiquette ? "" : String(valeur ?? "");
});
action("relire", async () => {
  trouves = null;
  message = "";
});
action("nouveau", async () => {
  await listerFiches();
  ecran = { nom: "nouveau" };
  message = "";
});
action("creer", async (champs) => {
  const quiz = creerQuiz(String(champs.nom).trim(), etiquettesDe(champs.etiquettes), champs.ordre);
  await garder();
  await commencer(quiz, champs.fiche, champs.mode);
});
action("ajouter-questions", async (_valeur, d) => {
  await listerFiches();
  ecran = { nom: "ajouter-questions", quiz: d.quiz };
  message = "";
});
action("continuer", async (champs, d) => {
  const quiz = trouver(d.quiz);
  if (quiz) await commencer(quiz, champs.fiche, champs.mode);
});
action("ecrire", async (champs, d) => {
  const quiz = trouver(d.quiz);
  if (!quiz) return;
  ajouterQuestion(quiz, String(champs.question), String(champs.reponse ?? ""), null);
  await garder();
  message = `Question ajoutée : ${pluriel(quiz.questions.length, "question", "questions")} dans « ${quiz.nom} ».`;
});
action("modifier", (_valeur, d) => {
  ecran = { nom: "modifier", quiz: d.quiz };
  message = "";
});
action("enregistrer", async (champs, d) => {
  const quiz = trouver(d.quiz);
  if (!quiz) return;
  quiz.nom = String(champs.nom).trim() || quiz.nom;
  quiz.etiquettes = etiquettesDe(champs.etiquettes);
  quiz.ordre = champs.ordre === "fiche" ? "fiche" : "hasard";
  const gardees = [];
  quiz.questions.forEach((q, i) => {
    if (champs[`x${i}`] === true) return;
    q.question = String(champs[`q${i}`] ?? q.question).trim() || q.question;
    q.reponse = String(champs[`r${i}`] ?? q.reponse).trim();
    gardees.push(q);
  });
  const retirees = quiz.questions.length - gardees.length;
  quiz.questions = gardees;
  await garder();
  message = `Enregistré${retirees > 0 ? `, ${pluriel(retirees, "question retirée", "questions retirées")}` : ""}.`;
});
action("supprimer", (_valeur, d) => {
  ecran = { nom: "supprimer", quiz: d.quiz };
  message = "";
});
action("confirmer-suppression", async (_valeur, d) => {
  const quiz = trouver(d.quiz);
  donnees.quiz = donnees.quiz.filter((q) => q.id !== d.quiz);
  await garder();
  versListe(quiz ? `Quiz « ${quiz.nom} » supprimé. Les fiches n'ont pas changé.` : "");
});
action("importer", async (_valeur, d) => {
  const trouve = trouves?.find((t) => t.id === d.fiche);
  if (!trouve) return;
  const quiz = creerQuiz(trouve.titre, [...trouve.etiquettes], "fiche");
  quiz.depuisBloc = trouve.id;
  for (const q of trouve.questions) ajouterQuestion(quiz, q.question, q.reponse, { fiche: trouve.id, titre: trouve.titre });
  await garder();
  trouves = trouves.filter((t) => t.id !== trouve.id);
  message = `Quiz « ${quiz.nom} » créé, avec ${pluriel(quiz.questions.length, "question", "questions")}.`;
});
action("ajout", async (champs) => {
  const { origine } = ecran;
  let quiz = trouver(champs.quiz);
  if (!quiz) {
    quiz = creerQuiz(origine.titre ?? "Mon quiz", [], "fiche");
  }
  let reponse = String(champs.reponse ?? "").trim();
  if (!reponse) {
    const prise = await kyfrev.selection.demander(`${quiz.nom} : sélectionne la réponse à « ${champs.question} », ou écris-la`, { fiche: origine.fiche });
    if (prise.choix === "terminer") {
      versListe("Rien n'a été ajouté.");
      await kyfrev.ouvrirVue();
      return;
    }
    reponse = prise.choix === "passer" ? "" : prise.texte ?? "";
  }
  ajouterQuestion(quiz, String(champs.question), reponse, origine);
  await garder();
  ecran = { nom: "liste" };
  message = `Question ajoutée au quiz « ${quiz.nom} » (${pluriel(quiz.questions.length, "question", "questions")}).`;
  await kyfrev.ouvrirVue();
});
action("lancer", (_valeur, d) => {
  const quiz = trouver(d.quiz);
  if (quiz) lancer([quiz], quiz.nom, false);
});
action("afficher", () => {
  ecran.visible = true;
});
action("savais", () => repondre(true));
action("a-revoir", () => repondre(false));
action("quitter", () => versListe());
action("rejouer", () => {
  const quiz = trouver(ecran.quiz);
  if (quiz) lancer([quiz], quiz.nom, false);
  else versListe();
});
action("rejouer-ratees", () => {
  const quiz = trouver(ecran.quiz);
  const liste = quiz ? [quiz] : donnees.quiz.filter((q) => aRevoir(q) > 0);
  lancer(liste, quiz ? quiz.nom : "Questions à revoir", true);
});
action("voir", async (_valeur, d) => {
  const question = trouver(d.quiz)?.questions.find((q) => q.id === d.question);
  if (!question?.fiche) return;
  await kyfrev.fiches.ouvrir(question.fiche, question.debut === null ? {} : { debut: question.debut, fin: question.fin });
});

// --- Les écrans. ---

const options = (liste, choisie) =>
  liste.map((f) => `<option value="${echapper(f.id)}"${f.id === choisie ? " selected" : ""}>${echapper(f.titre)}</option>`).join("");

const ordres = (choisi) => `
  <option value="hasard"${choisi !== "fiche" ? " selected" : ""}>Au hasard</option>
  <option value="fiche"${choisi === "fiche" ? " selected" : ""}>Dans l'ordre où elles ont été prises</option>`;

const modes = `
  <fieldset><legend>Comment écrire les questions ?</legend>
    <label><input type="radio" name="mode" value="fiche" checked> En les prenant dans la fiche : la question, puis sa réponse, à la chaîne</label>
    <label><input type="radio" name="mode" value="ici"> En les écrivant ici, sans ouvrir de fiche</label>
  </fieldset>`;

const bandeau = () => (message ? `<p><strong>${echapper(message)}</strong></p>` : "");

function carteQuiz(quiz) {
  const ratees = aRevoir(quiz);
  const etat = quiz.questions.length === 0 ? "" : ratees > 0 ? `<span class="pastille">${ratees} à revoir</span> ` : `<span class="pastille">À jour</span> `;
  const dernier = quiz.dernier ? ` · dernière fois : ${quiz.dernier.bonnes} sur ${quiz.dernier.total}, le ${enDate(quiz.dernier.date)}` : "";
  const etiquettes = quiz.etiquettes.length ? ` · ${quiz.etiquettes.map(echapper).join(", ")}` : "";
  return `
    <div class="carte">
      <h2>${echapper(quiz.nom)}</h2>
      <p class="secondaire">${etat}${pluriel(quiz.questions.length, "question", "questions")}${etiquettes}${dernier}</p>
      ${quiz.dernier ? `<progress value="${quiz.dernier.bonnes}" max="${quiz.dernier.total}" aria-label="Dernier score : ${quiz.dernier.bonnes} sur ${quiz.dernier.total}"></progress>` : ""}
      <p class="actions">
        <button class="principal" data-action="lancer" data-quiz="${quiz.id}"${quiz.questions.length === 0 ? " disabled" : ""}>Lancer</button>
        <button class="discret" data-action="modifier" data-quiz="${quiz.id}">Modifier</button>
        <button class="discret" data-action="ajouter-questions" data-quiz="${quiz.id}">Ajouter des questions</button>
      </p>
    </div>`;
}

async function vueListe() {
  if (trouves === null) await chercherBlocs();
  const toutesEtiquettes = [...new Set(donnees.quiz.flatMap((q) => q.etiquettes))].sort((a, b) => a.localeCompare(b, "fr"));
  const cherche = recherche.toLocaleLowerCase("fr");
  const visibles = donnees.quiz.filter(
    (q) => (!etiquette || q.etiquettes.includes(etiquette)) && (!cherche || q.nom.toLocaleLowerCase("fr").includes(cherche) || q.etiquettes.some((e) => e.toLocaleLowerCase("fr").includes(cherche))),
  );
  const filtres = toutesEtiquettes.length
    ? `<p class="actions"><button class="${etiquette ? "discret" : "principal"}" data-action="etiquette" data-valeur="">Tout</button>${toutesEtiquettes
        .map((e) => `<button class="${e === etiquette ? "principal" : "discret"}" data-action="etiquette" data-valeur="${echapper(e)}">${echapper(e)}</button>`)
        .join("")}</p>`
    : "";
  const blocs = trouves.length
    ? `<div class="carte">
        <h2>Trouvés dans tes fiches</h2>
        <p class="secondaire">${pluriel(trouves.length, "fiche a", "fiches ont")} déjà des questions dans un bloc quiz. Elles restent lisibles ; chacune peut devenir un quiz.</p>
        <ul>${trouves
          .map((t) => `<li>${echapper(t.titre)} · ${pluriel(t.questions.length, "question", "questions")} <button class="discret" data-action="importer" data-fiche="${echapper(t.id)}">Créer ce quiz</button></li>`)
          .join("")}</ul>
      </div>`
    : "";
  const vide = donnees.quiz.length === 0
    ? `<p class="secondaire">Aucun quiz pour l'instant. Crée le premier ici, par Ctrl+K « Nouveau quiz », par le clic droit d'une fiche, ou en sélectionnant un texte en lecture : clic droit, « Ajouter au quiz ».</p>`
    : visibles.length === 0 ? `<p class="secondaire">Aucun quiz ne correspond.</p>` : "";
  return `
    <h1>Quiz</h1>
    ${bandeau()}
    <p class="actions"><button class="principal" data-action="nouveau">Nouveau quiz</button><button class="discret" data-action="relire">Relire les fiches</button></p>
    ${donnees.quiz.length ? `<input type="search" data-action="chercher" value="${echapper(recherche)}" placeholder="Rechercher un quiz" aria-label="Rechercher un quiz">` : ""}
    ${filtres}
    ${visibles.map(carteQuiz).join("")}
    ${vide}
    ${blocs}`;
}

function vueNouveau() {
  const choisie = ecran.fiche ?? fiches[0]?.id ?? "";
  return `
    <h1>Nouveau quiz</h1>
    ${bandeau()}
    <form data-action="creer">
      <label>Nom <input name="nom" required value="${echapper(ecran.nomPropose ?? "")}" placeholder="La notion de l'autorité"></label>
      <label>Étiquettes <input name="etiquettes" placeholder="séparées par des virgules"></label>
      <label>Fiche de départ <select name="fiche" aria-label="Fiche de départ">${options(fiches, choisie)}</select></label>
      <label>Ordre des questions <select name="ordre" aria-label="Ordre des questions">${ordres("hasard")}</select></label>
      ${modes}
      <p class="actions"><button data-action="liste">Annuler</button><button class="principal">Commencer</button></p>
    </form>`;
}

function vueAjouterQuestions() {
  const quiz = trouver(ecran.quiz);
  if (!quiz) return vueListe();
  const choisie = quiz.questions.at(-1)?.fiche ?? fiches[0]?.id ?? "";
  return `
    <h1>Ajouter des questions</h1>
    <p class="secondaire">Au quiz « ${echapper(quiz.nom)} », qui en a ${quiz.questions.length}.</p>
    <form data-action="continuer" data-quiz="${quiz.id}">
      <label>Fiche <select name="fiche" aria-label="Fiche où prendre les questions">${options(fiches, choisie)}</select></label>
      ${modes}
      <p class="actions"><button data-action="liste">Annuler</button><button class="principal">Commencer</button></p>
    </form>`;
}

function vueEcrire() {
  const quiz = trouver(ecran.quiz);
  if (!quiz) return vueListe();
  return `
    <h1>${echapper(quiz.nom)}</h1>
    <p class="secondaire">${pluriel(quiz.questions.length, "question", "questions")} pour l'instant.</p>
    ${bandeau()}
    <form data-action="ecrire" data-quiz="${quiz.id}">
      <label>Question <textarea name="question" required></textarea></label>
      <label>Réponse <textarea name="reponse"></textarea></label>
      <p class="actions"><button class="principal">Ajouter la question</button><button data-action="modifier" data-quiz="${quiz.id}">Terminer</button></p>
    </form>`;
}

function vueModifier() {
  const quiz = trouver(ecran.quiz);
  if (!quiz) return vueListe();
  const questions = quiz.questions
    .map(
      (q, i) => `
      <div class="carte">
        <label>Question ${i + 1} <textarea name="q${i}">${echapper(q.question)}</textarea></label>
        <label>Réponse <textarea name="r${i}">${echapper(q.reponse)}</textarea></label>
        ${q.titre ? `<p class="secondaire">Vient de « ${echapper(q.titre)} »${q.aRevoir ? " · à revoir" : ""}</p>` : q.aRevoir ? `<p class="secondaire">À revoir</p>` : ""}
        <label><input type="checkbox" name="x${i}"> Retirer cette question</label>
        ${q.fiche ? `<p class="actions"><button class="discret" data-action="voir" data-quiz="${quiz.id}" data-question="${q.id}">Voir dans la fiche</button></p>` : ""}
      </div>`,
    )
    .join("");
  return `
    <p class="actions"><button class="discret" data-action="liste">Retour aux quiz</button></p>
    <h1>${echapper(quiz.nom)}</h1>
    ${bandeau()}
    <form data-action="enregistrer" data-quiz="${quiz.id}">
      <label>Nom <input name="nom" required value="${echapper(quiz.nom)}"></label>
      <label>Étiquettes <input name="etiquettes" value="${echapper(quiz.etiquettes.join(", "))}" placeholder="séparées par des virgules"></label>
      <label>Ordre des questions <select name="ordre" aria-label="Ordre des questions">${ordres(quiz.ordre)}</select></label>
      <h2>${pluriel(quiz.questions.length, "question", "questions")}</h2>
      ${questions || `<p class="secondaire">Aucune question : ajoute-en depuis une fiche, ou écris-les.</p>`}
      <p class="actions"><button class="principal">Enregistrer</button><button data-action="ajouter-questions" data-quiz="${quiz.id}">Ajouter des questions</button></p>
    </form>
    <p class="actions"><button class="discret" data-action="supprimer" data-quiz="${quiz.id}">Supprimer ce quiz</button></p>`;
}

function vueSupprimer() {
  const quiz = trouver(ecran.quiz);
  if (!quiz) return vueListe();
  return `
    <h1>Supprimer « ${echapper(quiz.nom)} » ?</h1>
    <p>Ses ${pluriel(quiz.questions.length, "question disparaît", "questions disparaissent")} avec lui. Les fiches ne changent pas.</p>
    <p class="actions"><button class="principal" data-action="confirmer-suppression" data-quiz="${quiz.id}">Supprimer</button><button data-action="modifier" data-quiz="${quiz.id}">Annuler</button></p>`;
}

function vueAjout() {
  const choisi = donnees.quiz.at(-1)?.id ?? "nouveau";
  const liste = donnees.quiz.map((q) => `<option value="${q.id}"${q.id === choisi ? " selected" : ""}>${echapper(q.nom)}</option>`).join("");
  return `
    <h1>Ajouter au quiz</h1>
    <p class="secondaire">Depuis « ${echapper(ecran.origine.titre ?? "")} ».</p>
    <form data-action="ajout">
      <label>Quiz <select name="quiz" aria-label="Quiz">${liste}<option value="nouveau"${choisi === "nouveau" ? " selected" : ""}>Un nouveau quiz : « ${echapper(ecran.origine.titre ?? "Mon quiz")} »</option></select></label>
      <label>Question <textarea name="question" required>${echapper(ecran.texte)}</textarea></label>
      <label>Réponse <textarea name="reponse" placeholder="Laisse vide pour la sélectionner ensuite dans la fiche"></textarea></label>
      <p class="actions"><button data-action="liste">Annuler</button><button class="principal">Ajouter</button></p>
    </form>`;
}

function vueJeu() {
  const total = ecran.suite.length;
  const { quiz, question } = questionDe(ecran.suite[ecran.position]);
  if (!question) {
    ecran.position++;
    return ecran.position >= total ? vueListe() : vueJeu();
  }
  const numero = ecran.position + 1;
  return `
    <h1>${echapper(ecran.titre)}</h1>
    <p class="secondaire">Question ${numero} sur ${total}${ecran.quiz ? "" : ` · ${echapper(quiz.nom)}`}${question.aRevoir ? " · ratée la dernière fois" : ""}</p>
    <progress value="${ecran.position}" max="${total}" aria-label="Question ${numero} sur ${total}"></progress>
    <div class="carte">
      <p class="question">${echapper(question.question)}</p>
      ${ecran.visible ? `<p class="reponse">${echapper(question.reponse || "(pas de réponse)")}</p>${question.titre ? `<p class="secondaire">Vient de « ${echapper(question.titre)} »</p>` : ""}` : ""}
    </div>
    <p class="actions">
      ${ecran.visible
        ? `<button class="principal" data-action="savais">Je savais</button><button data-action="a-revoir">À revoir</button>${question.fiche ? `<button class="discret" data-action="voir" data-quiz="${quiz.id}" data-question="${question.id}">Voir dans la fiche</button>` : ""}`
        : `<button class="principal" data-action="afficher">Afficher la réponse</button>`}
      <button class="discret" data-action="quitter">Quitter</button>
    </p>`;
}

function vueResultat() {
  const total = ecran.suite.length;
  const ratees = ecran.ratees;
  return `
    <h1>${echapper(ecran.titre)}</h1>
    <p><strong>Terminé : ${ecran.bonnes} sur ${total}</strong></p>
    <progress value="${ecran.bonnes}" max="${total}" aria-label="${ecran.bonnes} sur ${total}"></progress>
    ${ratees.length
      ? `<p class="secondaire">${pluriel(ratees.length, "question à revoir", "questions à revoir")} : elle${ratees.length > 1 ? "s" : ""} reviendr${ratees.length > 1 ? "ont" : "a"} en premier.</p><ul>${ratees.map((r) => `<li>${echapper(r)}</li>`).join("")}</ul>`
      : `<p class="secondaire">Tout est su.</p>`}
    <p class="actions">
      ${ratees.length ? `<button class="principal" data-action="rejouer-ratees">Revoir ${ratees.length > 1 ? `ces ${ratees.length}` : "celle-ci"}</button>` : ""}
      ${ecran.quiz ? `<button${ratees.length ? "" : ' class="principal"'} data-action="rejouer">Tout recommencer</button>` : ""}
      <button class="discret" data-action="liste">Retour aux quiz</button>
    </p>`;
}

kyfrev.vue(async () => {
  await charger();
  switch (ecran.nom) {
    case "nouveau":
      if (fiches === null) await listerFiches();
      return vueNouveau();
    case "ajouter-questions":
      if (fiches === null) await listerFiches();
      return vueAjouterQuestions();
    case "ecrire":
      return vueEcrire();
    case "modifier":
      return vueModifier();
    case "supprimer":
      return vueSupprimer();
    case "ajout":
      return vueAjout();
    case "jeu":
      return vueJeu();
    case "resultat":
      return vueResultat();
    default:
      return vueListe();
  }
});
