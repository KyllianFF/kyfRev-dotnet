// Écrit les avis de tiers de l'éditeur à côté de son paquet, depuis le verrou : la CI vérifie que le fichier y correspond.
import { readFileSync, readdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const SORTIE = "../../src/KyfRev.Presentation.Wpf/Schemas/THIRD-PARTY-NOTICES.md";

const ENTETE = `# Bibliothèques reprises

Le paquet de l'îlot des schémas réseau (\`Web/\`) est construit à partir des paquets npm ci-dessous, et leurs licences s'appliquent à ces
parties. La liste compte toutes les dépendances de production du verrou : quelques-unes, comme des outils en ligne de
commande, n'entrent pas dans le paquet.

Ce fichier est écrit par \`tools/schema-editor/avis-de-tiers.js\` : il ne se modifie pas à la main.
`;

function licenceDeclaree(paquet) {
  const licence = paquet.license ?? paquet.licenses?.[0];
  return typeof licence === "object" ? licence?.type : licence;
}

// Les fichiers de licence, et l'avis NOTICE que la licence Apache demande de conserver.
function textes(dossier) {
  return readdirSync(dossier)
    .filter((nom) => /^(licen[cs]e|copying|notice)/i.test(nom))
    .sort()
    .map((nom) => readFileSync(join(dossier, nom), "utf8").replace(/^﻿/, "").replace(/\r\n?/g, "\n").trim());
}

function avis(dossier) {
  const paquet = JSON.parse(readFileSync(join(dossier, "package.json"), "utf8"));
  const declaree = licenceDeclaree(paquet);
  const lignes = [`## ${paquet.name} ${paquet.version}`, ""];
  if (declaree) lignes.push(`Licence : ${declaree}.`, "");
  for (const texte of textes(dossier)) lignes.push("```text", texte, "```", "");
  return { cle: `${paquet.name}@${paquet.version}`, texte: lignes.join("\n") };
}

const verrou = JSON.parse(readFileSync("package-lock.json", "utf8"));
const parCle = new Map(
  Object.entries(verrou.packages)
    .filter(([dossier, paquet]) => dossier !== "" && !paquet.dev)
    .map(([dossier]) => avis(dossier))
    .map(({ cle, texte }) => [cle, texte]),
);
const cles = [...parCle.keys()].sort();

writeFileSync(SORTIE, [ENTETE, ...cles.map((cle) => parCle.get(cle))].join("\n").trimEnd() + "\n");
