import { RoleNames } from '../../lib/roles';

/**
 * The sidebar, grouped exactly as the Figma design shows.
 *
 * `roles` decides who sees an item. This only tidies the menu — every endpoint is
 * protected on the server as well. Hiding a link is not security.
 *
 * A screen that is not built yet still appears in the menu, showing what is coming,
 * so nobody wonders where a page went.
 */

export interface NavItem {
  label: string;
  path: string;
  /** Name from the icon set in components/ui/Icon.tsx. */
  icon: string;
  roles?: string[];
}

export interface NavGroup {
  heading: string;
  items: NavItem[];
}

export const navigation: NavGroup[] = [
  {
    heading: 'Main',
    items: [{ label: 'Dashboard', path: '/', icon: 'dashboard' }],
  },
  {
    heading: 'Operations',
    items: [
      { label: 'Inventory', path: '/inventory', icon: 'inventory' },
      // No roles: it writes nothing, and whoever is holding a label may need to know
      // what is behind it (specification section 13).
      { label: 'Trace a label', path: '/trace', icon: 'search' },
      {
        label: 'Receive Materials',
        path: '/inventory/receive',
        icon: 'receive',
        roles: [RoleNames.Administrator, RoleNames.InventoryManager],
      },
      {
        label: 'Material Issue',
        path: '/inventory/issue',
        icon: 'issue',
        roles: [RoleNames.Administrator, RoleNames.InventoryManager],
      },
    ],
  },
  {
    heading: 'Production',
    items: [
      {
        label: 'Roll Production',
        path: '/production/rolls',
        icon: 'roll',
        roles: [
          RoleNames.Administrator,
          RoleNames.Supervisor,
          RoleNames.ExtruderOperator,
        ],
      },
      {
        label: 'Roll Tests',
        path: '/production/roll-tests',
        icon: 'test',
        roles: [
          RoleNames.Administrator,
          RoleNames.Supervisor,
          RoleNames.ExtruderTestPerson,
        ],
      },
      {
        label: 'Thermoforming',
        path: '/production/thermo',
        icon: 'thermo',
        roles: [RoleNames.Administrator, RoleNames.Supervisor, RoleNames.ThermoOperator],
      },
      {
        label: 'Thermo Tests',
        path: '/production/thermo-tests',
        icon: 'test',
        roles: [
          RoleNames.Administrator,
          RoleNames.Supervisor,
          RoleNames.ThermoTestPerson,
        ],
      },
      {
        label: 'Pallets',
        path: '/production/pallets',
        icon: 'pallet',
        roles: [
          RoleNames.Administrator,
          RoleNames.Supervisor,
          RoleNames.PackagingOperator,
        ],
      },
      {
        label: 'Packaging',
        path: '/production/packaging',
        icon: 'packaging',
        roles: [
          RoleNames.Administrator,
          RoleNames.Supervisor,
          RoleNames.PackagingOperator,
        ],
      },
      {
        label: 'Recycler',
        path: '/production/recycler',
        icon: 'recycler',
        roles: [
          RoleNames.Administrator,
          RoleNames.Supervisor,
          RoleNames.RecyclerOperator,
        ],
      },
    ],
  },
  {
    heading: 'Analytics',
    items: [
      {
        label: 'Reports',
        path: '/reports',
        icon: 'reports',
        roles: [RoleNames.Administrator, RoleNames.Supervisor],
      },
    ],
  },
  {
    heading: 'Management',
    items: [
      {
        label: 'Recipes',
        path: '/recipes',
        icon: 'recipe',
        roles: [RoleNames.Administrator, RoleNames.Supervisor],
      },
      {
        label: 'Shifts',
        path: '/shifts',
        icon: 'shift',
        roles: [RoleNames.Administrator, RoleNames.Supervisor],
      },
      {
        label: 'Master Data',
        path: '/master-data',
        icon: 'settings',
        roles: [RoleNames.Administrator],
      },
      { label: 'Users', path: '/users', icon: 'users', roles: [RoleNames.Administrator] },
    ],
  },
];
