// Registers the @testing-library/jest-dom matcher augmentations (toBeInTheDocument,
// toHaveTextContent, …) on Vitest's `expect`, so both `vitest run` and the production
// `svelte-check` pass see the same assertion types.
import "@testing-library/jest-dom/vitest";
