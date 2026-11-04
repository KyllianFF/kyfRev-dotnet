import { defineConfig } from "vite";

// Le dessin seul, d'un seul tenant : la page cachée des dessins le charge comme un script classique, à côté de Mermaid.
export default defineConfig({
  publicDir: false,
  define: { "process.env.NODE_ENV": JSON.stringify("production") },
  build: {
    target: "es2022",
    outDir: "../../src/KyfRev.Presentation.Wpf/Schemas/Web",
    emptyOutDir: false,
    sourcemap: false,
    reportCompressedSize: false,
    lib: {
      entry: "src/reseau.ts",
      formats: ["iife"],
      name: "kyfRevDessinReseau",
      fileName: () => "reseau.js",
    },
  },
});
