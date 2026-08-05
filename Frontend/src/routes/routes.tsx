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
    path: '/production/thermo',
    title: 'Thermoforming',
    phase: 'Phase 9',
    description:
      'Scan a roll to start forming. Bags and their barcodes are created when the run is recorded.',
  },
  {
    path: '/production/thermo-tests',
    title: 'Thermo Tests',
    phase: 'Phase 9',
    description:
      'Time in the machine, plate size, counts, plate weight, bag weight and absorbent percentage.',
  },
  {
    path: '/production/pallets',
    title: 'Pallets',
    phase: 'Phase 10',
    description:
      'Scan bags onto a pallet. The first bag sets the colour, size and type; every later bag must match.',
  },
  {
    path: '/production/packaging',
    title: 'Packaging',
    phase: 'Phase 11',
    description:
      'End-of-shift packaging materials. Bags and pallets are counted by the system; tape and shrink are weighed.',
  },
  {
    path: '/production/recycler',
    title: 'Recycler',
    phase: 'Phase 12',
    description:
      'Scrap weighed in, recycled material weighed out, and the result added back to the store.',
  },
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
