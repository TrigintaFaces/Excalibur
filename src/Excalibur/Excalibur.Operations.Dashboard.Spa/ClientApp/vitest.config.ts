import { defineConfig } from "vitest/config";
import { svelte } from "@sveltejs/vite-plugin-svelte";
import { svelteTesting } from "@testing-library/svelte/vite";

// Vitest harness for the operations-dashboard SPA. Runs the Svelte 5 components and
// the shared TypeScript logic (status/api/panels/poller) under jsdom. `svelteTesting()`
// wires the browser resolve condition + auto-cleanup between tests. Kept separate from
// vite.config.ts so the production build stays test-free.
export default defineConfig({
  plugins: [svelte(), svelteTesting()],
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./vitest.setup.ts"],
    include: ["src/**/*.{test,spec}.{ts,svelte.ts}"],
    css: false,
  },
});
