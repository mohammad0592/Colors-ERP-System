import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    // Fail loudly instead of moving to the next free port. The API allows this exact
    // origin, so a silent move to 5174 would break every request with a CORS error
    // that looks like a bug in the code.
    strictPort: true,
    // Listen on every network card, not only the loopback, so the screens can be opened
    // on a phone or a tablet on the same network — which is how the factory will
    // actually use them. Development only; in production one Windows Server serves the
    // built files and the API together (specification section 15).
    host: true,
  },
});
