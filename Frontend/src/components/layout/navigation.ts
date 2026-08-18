import { rolesFor, type ScreenPath, type ScreenRoles } from '../../routes/access';

/**
 * The sidebar, grouped exactly as the Figma design shows.
 *
 * This file decides the **wording and the order** — the headings, the labels, the
 * icons, which screen sits under which group. It deliberately does not decide who
 * sees what: that comes from `routes/access.ts`, the same list the route guard uses,
 * so a link can never be hidden from someone the guard lets in.
 *
 * A screen that is not built yet still appears in the menu, showing what is coming,
 * so nobody wonders where a page went.
 */

export interface NavItem {
  label: string;
  path: ScreenPath;
  /** Name from the icon set in components/ui/Icon.tsx. */
  icon: string;
  /** Filled in from `routes/access.ts` — never written here. */
  roles: ScreenRoles;
}
export interface NavGroup {
  heading: string;
  items: NavItem[];
}

/** The menu as it is written: wording, order and icons only. */
const layout: { heading: string; items: Omit<NavItem, 'roles'>[] }[] = [
  {
    heading: 'Main',
    items: [{ label: 'Dashboard', path: '/', icon: 'dashboard' }],
  },
  {
    heading: 'Operations',
    items: [
      { label: 'Inventory', path: '/inventory', icon: 'inventory' },
      { label: 'Trace a label', path: '/trace', icon: 'search' },
      { label: 'Receive Materials', path: '/inventory/receive', icon: 'receive' },
      { label: 'Material Issue', path: '/inventory/issue', icon: 'issue' },
    ],
  },
  {
    heading: 'Production',
    items: [
      { label: 'Roll Production', path: '/production/rolls', icon: 'roll' },
      { label: 'Roll Tests', path: '/production/roll-tests', icon: 'test' },
      { label: 'Thermoforming', path: '/production/thermo', icon: 'thermo' },
      { label: 'Thermo Tests', path: '/production/thermo-tests', icon: 'test' },
      { label: 'Pallets', path: '/production/pallets', icon: 'pallet' },
      { label: 'Packaging', path: '/production/packaging', icon: 'packaging' },
      { label: 'Dispatch', path: '/production/dispatch', icon: 'pallet' },
      { label: 'Recycler', path: '/production/recycler', icon: 'recycler' },
    ],
  },
  {
    heading: 'Analytics',
    items: [
      { label: 'Reports', path: '/reports', icon: 'reports' },
      { label: 'Audit log', path: '/audit', icon: 'search' },
    ],
  },
  {
    heading: 'Management',
    items: [
      { label: 'Recipes', path: '/recipes', icon: 'recipe' },
      { label: 'Shifts', path: '/shifts', icon: 'shift' },
      { label: 'Master Data', path: '/master-data', icon: 'settings' },
      { label: 'Users', path: '/users', icon: 'users' },
    ],
  },
];

/**
 * The menu the app uses, with each item's roles filled in from the one list.
 *
 * Done here rather than by hand on every row so the two can never disagree again.
 */
export const navigation: NavGroup[] = layout.map((group) => ({
  heading: group.heading,
  items: group.items.map((item) => ({ ...item, roles: rolesFor(item.path) })),
}));
