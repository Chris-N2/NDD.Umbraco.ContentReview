import { defineConfig } from "vite";

export default defineConfig({
  build: {
    lib: {
      entry: "src/bundle.manifests.ts", // The bundle registers every manifest in the extension
      formats: ["es"],
      fileName: "ndd-content-review",
    },
    outDir: "../wwwroot/App_Plugins/NDDContentReview",
    emptyOutDir: true,
    // Source maps ship in the package: the source is MIT and public, and they make our
    // extensions debuggable on a consumer's site.
    sourcemap: true,
    rollupOptions: {
      external: [/^@umbraco/],
    },
  },
});
