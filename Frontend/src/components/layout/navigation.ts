import type { TranslationKey } from '../../lib/i18n/en';
import { rolesFor, type ScreenPath, type ScreenRoles } from '../../routes/access';

/**
 * The sidebar, grouped exactly as the Figma design shows.
 *
 * This file decides the **order and the grouping** — which screen sits under which
 * heading, and with which icon. The words themselves are keys, looked up in whichever
 * language the man chose; this is a plain module and cannot ask a React hook, so the
 * sidebar does the looking up.
 *
 * It deliberately does not decide who sees what: that comes from `routes/access.ts`,
 * the same list the route guard uses, so a link can never be hidden from someone the
 * guard lets in.
 *
 * A screen that is not built yet still appears in the menu, showing what is coming,
 * so nobody wonders where a page went.
 */

export interface NavItem {
  /** A key, not a word. The sidebar translates it.  */
  label: TranslationKey;
  path: ScreenPath;
  /** Name from the icon set in components/ui/Icon.tsx. */
  icon: string;
  /** Filled in from `routes/access.ts` — never written here. */
  roles: ScreenRoles;
}
export interface NavGroup {
  heading: TranslationKey;
  items: NavItem[];
}

/** The menu as it is written: wording, order and icons only. */
const layout: { heading: TranslationKey; items: Omit<NavItem, 'roles'>[] }[] = [
  {
    heading: 'nav.main',
    items: [{ label: 'nav.dashboard', path: '/', icon: 'dashboard' }],
  },
  {
    heading: 'nav.operations',
    items: [
      { label: 'nav.inventory', path: '/inventory', icon: 'inventory' },
      { label: 'nav.trace', path: '/trace', icon: 'search' },
      { label: 'nav.receive', path: '/inventory/receive', icon: 'receive' },
      { label: 'nav.issue', path: '/inventory/issue', icon: 'issue' },
    ],
  },
  {
    heading: 'nav.production',
    items: [
      { label: 'nav.rolls', path: '/production/rolls', icon: 'roll' },
      { label: 'nav.rollTests', path: '/production/roll-tests', icon: 'test' },
      { label: 'nav.thermo', path: '/production/thermo', icon: 'thermo' },
      { label: 'nav.thermoTests', path: '/production/thermo-tests', icon: 'test' },
      { label: 'nav.pallets', path: '/production/pallets', icon: 'pallet' },
      { label: 'nav.packaging', path: '/production/packaging', icon: 'packaging' },
      { label: 'nav.dispatch', path: '/production/dispatch', icon: 'pallet' },
      { label: 'nav.recycler', path: '/production/recycler', icon: 'recycler' },
    ],
  },
  {
    heading: 'nav.analytics',
    items: [
      { label: 'nav.reports', path: '/reports', icon: 'reports' },
      { label: 'nav.audit', path: '/audit', icon: 'search' },
    ],
  },
  {
    heading: 'nav.management',
    items: [
      { label: 'nav.recipes', path: '/recipes', icon: 'recipe' },
      { label: 'nav.shifts', path: '/shifts', icon: 'shift' },
      { label: 'nav.masterData', path: '/master-data', icon: 'settings' },
      { label: 'nav.users', path: '/users', icon: 'users' },
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
