// Génère le corpus d'ancrage « Markdown → texte affiché → positions » depuis kyfRev 0.x.
// Usage : node tools/anchoring-corpus/generer.mjs [dossier de sortie]
// Prérequis : le dépôt kyfRev 0.x cloné à côté de ce dépôt, ses dépendances installées
// (pnpm install), et Microsoft Edge. Voir README.md.

import { execFileSync } from "node:child_process";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { analyse } from "./analyse.mjs";
import { documents, documentsAuHasard, modifications, modificationsAuHasard } from "./cas.mjs";

const ici = path.dirname(fileURLToPath(import.meta.url));
const racine = path.resolve(ici, "../..");
const ancienne = process.env.KYFREV_0X ?? path.resolve(racine, "../kyfRev");
const sortie = path.resolve(process.argv[2] ?? path.join(racine, "tests/fixtures/ancrage"));
const depuis = (relatif) => import(pathToFileURL(path.join(ancienne, "node_modules", relatif)).href);

const { build } = await depuis("vite/dist/node/index.js");
const { svelte } = await depuis("@sveltejs/vite-plugin-svelte/src/index.js");
const { chromium } = await depuis("@playwright/test/index.mjs");

// 1. Les sources de la 0.x, assemblées pour le navigateur.
const [resultat] = await build({
  configFile: false,
  root: ancienne,
  logLevel: "warn",
  plugins: [svelte({ configFile: path.join(ancienne, "svelte.config.js") })],
  build: {
    write: false,
    minify: false,
    lib: { entry: path.join(ici, "entree.ts"), formats: ["iife"], name: "Corpus", fileName: "corpus" },
  },
});
const code = resultat.output[0].code;

// 2. Microsoft Edge, sans fenêtre : le moteur de WebView2, celui de la 0.x.
const navigateur = await chromium.launch({ channel: "msedge", headless: true });
const page = await navigateur.newPage();
await page.route("http://corpus.kyfrev/**", (route) =>
  route.fulfill({ contentType: "text/html; charset=utf-8", body: "<!doctype html><html lang=\"fr\"><head><meta charset=\"utf-8\"></head><body></body></html>" }),
);
await page.goto("http://corpus.kyfrev/");
await page.addScriptTag({ content: code });

const projeter = (d) => page.evaluate((d) => Corpus.projeter(d.corps, d.titre, d.premiereLigneCorps, d.passages), d);
const reancrer = (m) =>
  page.evaluate((m) => Corpus.reancrer(m.avant, m.apres, m.titre, { auto: m.auto, exacts: m.exacts }), m);

const tires = documentsAuHasard(200, 20261031);
const cas = [];
for (const document of [...documents, ...tires]) {
  cas.push({ nom: document.nom, titre: document.titre, premiereLigneCorps: document.premiereLigneCorps, corps: document.corps, ...(await projeter(document)) });
}
const reancrages = [];
for (const modification of [...modifications, ...modificationsAuHasard(tires.slice(0, 100), 1789)]) {
  reancrages.push({ nom: modification.nom, titre: modification.titre, avant: modification.avant, apres: modification.apres, ...(await reancrer(modification)) });
}
const analyses = [];
for (const document of analyse) {
  // Le HTML n'apprend rien ici, et prendrait la moitié du fichier.
  const { html, ...projection } = await projeter(document);
  analyses.push({ nom: document.nom, titre: document.titre, premiereLigneCorps: document.premiereLigneCorps, corps: document.corps, ...projection });
}
const moteur = `Microsoft Edge ${navigateur.version()}`;
await navigateur.close();

// 3. Provenance et fichiers.
const paquet = JSON.parse(readFileSync(path.join(ancienne, "package.json"), "utf8"));
const version = (nom) => paquet.dependencies[nom];
const source = {
  depot: "KyllianFF/kyfRev",
  commit: execFileSync("git", ["-C", ancienne, "rev-parse", "HEAD"], { encoding: "utf8" }).trim(),
  moteur,
  bibliotheques: Object.fromEntries(
    ["markdown-it", "markdown-it-footnote", "@mdit/plugin-katex", "katex", "highlight.js", "dompurify", "approx-string-match"].map((nom) => [nom, version(nom)]),
  ),
};
const ecrire = (nom, contenu) => writeFileSync(path.join(sortie, nom), `${JSON.stringify(contenu, null, 1)}\n`);
mkdirSync(sortie, { recursive: true });
ecrire("projection.json", {
  description: "Markdown d'une fiche → texte affiché (positions en unités UTF-16) → passages ancrés, calculés par kyfRev 0.x.",
  source,
  cas,
});
ecrire("reancrage.json", {
  description: "Passages ancrés dans un texte, puis retrouvés après modification de la fiche, par kyfRev 0.x.",
  source,
  cas: reancrages,
});
ecrire("analyse.json", {
  description: "Cas limites de l'analyse Markdown → texte affiché → passages, calculés par kyfRev 0.x (branche par branche du moteur natif).",
  source,
  cas: analyses,
});
const passages = cas.reduce((n, c) => n + c.passages.length, 0);
const retrouves = reancrages.reduce((n, c) => n + c.passages.length, 0);
console.log(`${cas.length} documents, ${passages} passages ; ${reancrages.length} modifications, ${retrouves} ré-ancrages ; ${analyses.length} cas limites (${moteur}).`);
