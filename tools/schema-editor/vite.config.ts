import { svelte } from "@sveltejs/vite-plugin-svelte";
import { defineConfig } from "vitest/config";

// Le paquet est écrit à côté du code qui l'héberge, et la CI vérifie qu'il correspond aux sources.
export default defineConfig({
  base: "./",
  plugins: [svelte()],
  build: {
    target: "es2022",
    outDir: "../../src/KyfRev.Presentation.Wpf/Schemas/Web",
    emptyOutDir: true,
    assetsInlineLimit: 0,
    cssCodeSplit: false,
    sourcemap: false,
    rollupOptions: {
      input: "src/main.ts",
      output: {
        format: "iife",
        entryFileNames: "schemas.js",
        assetFileNames: "schemas.css",
      },
    },
  },
  test: { include: ["src/**/*.test.ts"] },
});
