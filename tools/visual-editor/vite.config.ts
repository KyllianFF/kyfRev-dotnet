import type { Plugin } from "vite";
import { defineConfig } from "vitest/config";

// KaTeX déclare ses polices en trois formats : WebView2 n'a besoin que du WOFF2.
function woff2Seulement(): Plugin {
  return {
    name: "kyfrev-woff2-seulement",
    enforce: "pre",
    transform(code, id) {
      if (!id.includes("katex.min.css")) return null;
      return code.replace(/,url\([^)]*\.woff\) format\("woff"\)|,url\([^)]*\.ttf\) format\("truetype"\)/g, "");
    },
  };
}

// Le paquet est écrit à côté du code qui l'héberge, et la CI vérifie qu'il correspond aux sources.
export default defineConfig({
  base: "./",
  plugins: [woff2Seulement()],
  build: {
    target: "es2022",
    outDir: "../../src/KyfRev.Presentation.Wpf/Editing/Web",
    emptyOutDir: true,
    assetsInlineLimit: 0,
    cssCodeSplit: false,
    sourcemap: false,
    rollupOptions: {
      input: "src/main.ts",
      output: {
        format: "iife",
        entryFileNames: "editor.js",
        assetFileNames: (fichier) =>
          fichier.names?.some((nom) => nom.endsWith(".css")) ? "editor.css" : "polices/[name][extname]",
      },
    },
  },
  test: { include: ["src/**/*.test.ts"] },
});
