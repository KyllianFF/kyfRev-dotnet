// Coloration du code dans l'éditeur visuel : highlight.js (le même qu'en
// lecture) découpe le code en morceaux, que ProseMirror colore sans toucher
// au texte (décorations).

import hljs from "highlight.js/lib/common";

export type Morceau = { debut: number; fin: number; classes: string };

/** Noms lisibles des langages reconnus (les autres gardent leur identifiant). */
const NOMS: Record<string, string> = {
  bash: "Bash",
  c: "C",
  cpp: "C++",
  csharp: "C#",
  css: "CSS",
  diff: "Diff",
  go: "Go",
  graphql: "GraphQL",
  ini: "INI / TOML",
  java: "Java",
  javascript: "JavaScript",
  json: "JSON",
  kotlin: "Kotlin",
  less: "Less",
  lua: "Lua",
  makefile: "Makefile",
  markdown: "Markdown",
  objectivec: "Objective-C",
  perl: "Perl",
  php: "PHP",
  "php-template": "PHP (gabarit)",
  plaintext: "Texte",
  python: "Python",
  "python-repl": "Python (console)",
  r: "R",
  ruby: "Ruby",
  rust: "Rust",
  scss: "SCSS",
  shell: "Console",
  sql: "SQL",
  swift: "Swift",
  typescript: "TypeScript",
  vbnet: "VB.NET",
  wasm: "WebAssembly",
  xml: "HTML / XML",
  yaml: "YAML",
};

/** Langages proposés pour un bloc de code, par nom lisible. */
export function langages(): { id: string; nom: string }[] {
  return hljs
    .listLanguages()
    .filter((id) => id !== "plaintext")
    .map((id) => ({ id, nom: NOMS[id] ?? id }))
    .sort((a, b) => a.nom.localeCompare(b.nom, "fr", { sensitivity: "base" }));
}

/** Le langage (ou l'un de ses alias, « js », « py »…) est-il reconnu ? */
export function langageConnu(langue: string): boolean {
  return langue !== "" && hljs.getLanguage(langue) !== undefined;
}

const ENTITES: Record<string, string> = {
  "&amp;": "&",
  "&lt;": "<",
  "&gt;": ">",
  "&quot;": '"',
  "&#x27;": "'",
};

/**
 * Morceaux colorés du code : positions dans le texte d'origine et classes
 * highlight.js (« hljs-keyword »…). Rien si le langage est inconnu.
 */
export function colorer(code: string, langue: string): Morceau[] {
  if (!langageConnu(langue) || code === "") return [];
  const html = hljs.highlight(code, { language: langue, ignoreIllegals: true }).value;
  const morceaux: Morceau[] = [];
  const pile: string[] = [];
  let position = 0;
  const balise = /<span class="([^"]+)">|<\/span>|&(?:amp|lt|gt|quot|#x27);|[^<&]+/g;
  for (const trouve of html.matchAll(balise)) {
    const [jeton, classe] = trouve;
    if (classe !== undefined) {
      pile.push(classe);
    } else if (jeton === "</span>") {
      pile.pop();
    } else {
      const longueur = (ENTITES[jeton] ?? jeton).length;
      if (pile.length > 0) {
        const classes = pile.join(" ");
        const precedent = morceaux.at(-1);
        if (precedent && precedent.fin === position && precedent.classes === classes)
          precedent.fin += longueur;
        else morceaux.push({ debut: position, fin: position + longueur, classes });
      }
      position += longueur;
    }
  }
  return morceaux;
}
