import { defineConfig } from "vite";
import { svelte } from "@sveltejs/vite-plugin-svelte";

// Build the SPA to ../wwwroot, which the .NET project embeds via
// ManifestEmbeddedFileProvider. `base: "./"` makes every asset URL relative so
// the app works under any server path prefix (e.g. /dashboard) with no rewrite.
// A single JS + single CSS bundle keeps the embedded payload minimal and lets a
// strict CSP (no unsafe-inline / no eval) apply cleanly.
export default defineConfig({
  plugins: [svelte()],
  base: "./",
  build: {
    outDir: "../wwwroot",
    emptyOutDir: true,
    sourcemap: false,
    target: "es2022",
    cssCodeSplit: false,
    modulePreload: { polyfill: false },
    rollupOptions: {
      output: {
        entryFileNames: "assets/[name].[hash].js",
        chunkFileNames: "assets/[name].[hash].js",
        assetFileNames: "assets/[name].[hash][extname]",
      },
    },
  },
});
