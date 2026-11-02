import { copyFileSync } from "node:fs";
import { resolve } from "node:path";
import type { Plugin } from "vite";
import { defineConfig } from "vite";

const SORTIE = "../../src/KyfRev.Presentation.Wpf/Editing/Web";

// Mermaid tel qu'il est publié : son paquet tout fait, à côté de la petite page qui s'en sert.
function mermaidTelQuel(): Plugin {
  return {
    name: "kyfrev-mermaid-tel-quel",
    closeBundle() {
      copyFileSync(resolve("node_modules/mermaid/dist/mermaid.min.js"), resolve(SORTIE, "mermaid.min.js"));
    },
  };
}

// La page cachée qui dessine les schémas : à part de l'éditeur, qui n'a pas à charger Mermaid.
export default defineConfig({
  publicDir: false,
  plugins: [mermaidTelQuel()],
  build: {
    target: "es2022",
    outDir: SORTIE,
    emptyOutDir: false,
    sourcemap: false,
    rollupOptions: {
      input: "src/diagrammes.ts",
      output: { format: "iife", entryFileNames: "diagrammes.js" },
    },
  },
});
