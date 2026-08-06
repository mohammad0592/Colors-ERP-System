import type { ReactElement } from 'react';

/**
 * Screens that are planned but not built.
 *
 * The list is empty: every screen in the menu is real. It is kept because the next thing
 * the factory asks for will want somewhere to say "this is coming", and a placeholder
 * that says so beats a menu item that goes nowhere.
 */
const planned: { path: string; title: string; phase: string; description: string }[] = [];

export const plannedRoutes: { path: string; element: ReactElement }[] = planned.map(
  () => {
    throw new Error('Unreachable: the planned list is empty.');
  },
);
