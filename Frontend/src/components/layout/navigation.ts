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
/** A run of items under a sub-heading — a line, inside Production (section 19.18). */
export interface NavSection {
  /** Null for a group that has no sub-headings. */
  heading: TranslationKey | null;
  items: NavItem[];
}

export interface NavGroup {
  heading: TranslationKey;
  /** What the sidebar draws: the items in their sub-sections. */
  sections: NavSection[];
  /** Every item of the group in one list, for whoever only needs to find one. */
  items: NavItem[];
}

type Item = Omit<NavItem, 'roles'>;

/**
 * The menu as it is written: wording, order and icons only. A group is either a plain
 * list of items or a list of sub-sections, each under its own heading.
 */
const layout: {
  heading: TranslationKey;
  items?: Item[];
  sections?: { heading: TranslationKey; items: Item[] }[];
}[] = [
  {
    heading: 'nav.main',
    items: [{ label: 'nav.dashboard', path: '/', icon: 'dashboard' }],
  },
  {
    heading: 'nav.operations',
    items: [
      { label: 'nav.inventory', path: '/inventory', icon: 'inventory' },
      { label: 'nav.trace', path: '/trace', icon: 'search' },
      { label: 'nav.printQueue', path: '/print-queue', icon: 'print' },
      { label: 'nav.receive', path: '/inventory/receive', icon: 'receive' },
      { label: 'nav.issue', path: '/inventory/issue', icon: 'issue' },
      // Sending a pallet out of the factory is a stock movement, not line work.
      { label: 'nav.dispatch', path: '/production/dispatch', icon: 'pallet' },
    ],
  },
  {
    // One sub-section per line, in the order the material goes through the factory,
    // each ending with that line's shift configuration (section 19.18).
    heading: 'nav.production',
    sections: [
      {
        heading: 'nav.extruder',
        items: [
          { label: 'nav.rolls', path: '/production/rolls', icon: 'roll' },
          { label: 'nav.rollTests', path: '/production/roll-tests', icon: 'test' },
          {
            label: 'shiftCfg.extruder',
            path: '/production/shift-config/extruder',
            icon: 'shift',
          },
        ],
      },
      {
        heading: 'nav.thermoforming',
        items: [
          { label: 'nav.thermo', path: '/production/thermo', icon: 'thermo' },
          { label: 'nav.thermoTests', path: '/production/thermo-tests', icon: 'test' },
          { label: 'nav.pallets', path: '/production/pallets', icon: 'pallet' },
          { label: 'nav.packaging', path: '/production/packaging', icon: 'packaging' },
          {
            label: 'shiftCfg.thermo',
            path: '/production/shift-config/thermo',
            icon: 'shift',
          },
        ],
      },
      {
        heading: 'nav.recycle',
        items: [
          { label: 'nav.recycler', path: '/production/recycler', icon: 'recycler' },
          {
            label: 'shiftCfg.recycler',
            path: '/production/shift-config/recycler',
            icon: 'shift',
          },
        ],
      },
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
const withRoles = (item: Item): NavItem => ({ ...item, roles: rolesFor(item.path) });

export const navigation: NavGroup[] = layout.map((group) => {
  const sections: NavSection[] =
    group.sections === undefined
      ? [{ heading: null, items: (group.items ?? []).map(withRoles) }]
      : group.sections.map((section) => ({
          heading: section.heading,
          items: section.items.map(withRoles),
        }));

  return {
    heading: group.heading,
    sections,
    items: sections.flatMap((section) => section.items),
  };
});
