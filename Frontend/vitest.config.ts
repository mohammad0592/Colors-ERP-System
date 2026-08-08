import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

/**
 * The test run, kept apart from `vite.config.ts` on purpose.
 *
 * The dev server config is about serving the factory floor — a fixed port, every
 * network card, Tailwind. None of that has anything to do with running tests, and
 * mixing the two would mean every change to one risks the other.
 *
 * Tailwind is deliberately absent: a test asserts what a screen *says* and *does*,
 * never how it looks. Leaving the stylesheet out keeps the run fast and stops anyone
 * writing a test that passes or fails on a colour.
 */
export default defineConfig({
  plugins: [react()],
  test: {
    // The screens are built for a browser, so they are tested in one.
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    // No globals. `describe` and `expect` are imported like anything else, so a
    // reader can see where they come from and TypeScript checks them without a
    // special entry in tsconfig.
    globals: false,
    coverage: {
      provider: 'v8',
      include: ['src/**/*.{ts,tsx}'],
      // Generated shapes and wiring — there is nothing in them to get wrong.
      exclude: [
        'src/**/*.test.{ts,tsx}',
        'src/test/**',
        'src/lib/apiTypes.ts',
        'src/main.tsx',
      ],
    },
  },
});
