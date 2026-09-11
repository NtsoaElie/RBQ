import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// /api is proxied to the C# query pipeline, so the app always calls relative URLs.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: { "/api": "http://localhost:5080" },
  },
});
