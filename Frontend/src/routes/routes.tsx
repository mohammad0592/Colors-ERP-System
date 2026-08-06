import type { ReactElement } from 'react';
import { ComingSoon } from '../components/ui/ComingSoon';

/**
 * Screens that are planned but not built.
 *
 * Listed here rather than as fifteen almost-identical files. Each is replaced by a
 * real screen as its phase is built (specification section 17).
 */
const planned: { path: string; title: string; phase: string; description: string }[] = [
  {
    path: '/reports',
    title: 'Reports',
    phase: 'Phase 13',
    description:
      'Material waste against what the recipe requires, production by shift, stock, and full traceability from a pallet back to its materials.',
  },
  {
    path: '/users',
    title: 'Users',
    phase: 'Phase 1',
    description:
      'Add a worker, give them an employee number, and set which of the nine roles they hold.',
  },
];

export const plannedRoutes: { path: string; element: ReactElement }[] = planned.map(
  ({ path, title, phase, description }) => ({
    path,
    element: <ComingSoon title={title} phase={phase} description={description} />,
  }),
);
