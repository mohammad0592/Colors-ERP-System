import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

/**
 * Runs before every test file.
 *
 * Two jobs, both about tests not lying to each other:
 *
 * - `jest-dom` adds the checks that read like the thing being asked — `toBeVisible`,
 *   `toHaveTextContent` — instead of poking at DOM properties by hand.
 * - `cleanup` empties the page after each test. Without it the second test in a file
 *   sees the first one's screen still there, and `getByText` finds two matches and
 *   fails for a reason that has nothing to do with the test.
 */
afterEach(() => {
  cleanup();
});
