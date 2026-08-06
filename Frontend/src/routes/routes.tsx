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
