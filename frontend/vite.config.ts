import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": "http://localhost:8080",
      "/health": "http://localhost:8080"
    }
  },
  test: {
    exclude: ["tests/e2e/**", "node_modules/**", "dist/**"],
    environment: "jsdom",
    setupFiles: "./tests/setup.ts"
  }
});
