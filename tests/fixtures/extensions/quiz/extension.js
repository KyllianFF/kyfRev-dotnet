// Extension d'exemple « Quiz » pour kyfRev (D62). À copier pour écrire la
// vôtre : voir docs/extensions.md.
//
// Dans une fiche :
//
//   ```quiz
//   Q: Qui convoque les états généraux en 1789 ?
//   R: Louis XVI
//   Q: Combien d'ordres compte la société d'Ancien Régime ?
//   R: Trois
//   ```
"use strict";

/** Le HTML rendu est nettoyé par kyfRev, mais le texte doit rester du texte. */
const echapper = (texte) =>
  String(texte).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);

/** Questions d'un bloc : des paires « Q: … » / « R: … ». */
function questions(source) {
  const liste = [];
  for (const ligne of source.split("\n")) {
    const q = /^\s*Q\s*:\s*(.+)$/i.exec(ligne);
    const r = /^\s*R\s*:\s*(.+)$/i.exec(ligne);
    if (q) liste.push({ question: q[1].trim(), reponse: "" });
    else if (r && liste.length > 0) liste[liste.length - 1].reponse = r[1].trim();
  }
  return liste;
}

// --- Le bloc dans la fiche : chaque réponse se dévoile au clic (<details>). ---
kyfrev.bloc("quiz", (source) => {
  const liste = questions(source);
  if (liste.length === 0) return '<p class="secondaire">Quiz vide : écrivez des lignes « Q: … » puis « R: … ».</p>';
  const items = liste
    .map(
      (q) =>
        `<li><details><summary>${echapper(q.question)}</summary><p>${echapper(q.reponse || "(pas de réponse)")}</p></details></li>`,
    )
    .join("");
  return `<p class="titre-extension">Quiz · ${liste.length} question${liste.length > 1 ? "s" : ""}</p><ol class="quiz">${items}</ol>`;
});

// --- La vue : toutes les questions du coffre, une à la fois. ---
let toutes = null;
let position = 0;
let reponseVisible = false;
let score = { sues: 0, aRevoir: 0 };

async function chargerToutes() {
  const fiches = await kyfrev.fiches.lister();
  const trouvees = [];
  for (const fiche of fiches.filter((f) => !f.archivee)) {
    const { texte } = await kyfrev.fiches.lire(fiche.id);
    for (const bloc of texte.matchAll(/```quiz\n([\s\S]*?)```/g)) {
      for (const q of questions(bloc[1])) trouvees.push({ ...q, fiche: fiche.titre });
    }
  }
  toutes = trouvees;
  score = (await kyfrev.stockage.lire()) ?? score;
}

kyfrev.vue(async () => {
  if (toutes === null) await chargerToutes();
  const bilan = `<p class="secondaire">${score.sues} sue(s) · ${score.aRevoir} à revoir, depuis le début</p>`;
  if (toutes.length === 0) {
    return `<h1>Quiz</h1><p>Aucune question pour l'instant : ajoutez un bloc <code>quiz</code> à une fiche.</p>${bilan}`;
  }
  const q = toutes[position % toutes.length];
  return `
    <h1>Quiz</h1>
    <p class="secondaire">Question ${(position % toutes.length) + 1} sur ${toutes.length} · ${echapper(q.fiche)}</p>
    <div class="carte">
      <p class="question">${echapper(q.question)}</p>
      ${reponseVisible ? `<p class="reponse">${echapper(q.reponse || "(pas de réponse)")}</p>` : ""}
    </div>
    <p class="actions">
      ${
        reponseVisible
          ? `<button class="bouton principal" data-action="resultat" data-valeur="sue">Je savais</button>
             <button class="bouton" data-action="resultat" data-valeur="a-revoir">À revoir</button>`
          : `<button class="bouton principal" data-action="afficher">Afficher la réponse</button>`
      }
      <button class="bouton discret" data-action="recharger">Relire les fiches</button>
    </p>
    ${bilan}`;
});

kyfrev.action("afficher", () => {
  reponseVisible = true;
});

kyfrev.action("resultat", async (valeur) => {
  if (valeur === "sue") score.sues += 1;
  else score.aRevoir += 1;
  await kyfrev.stockage.ecrire(score);
  position += 1;
  reponseVisible = false;
});

kyfrev.action("recharger", async () => {
  toutes = null;
  position = 0;
  reponseVisible = false;
});
