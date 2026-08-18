/**
 * The English wording, and the shape every other language must match.
 *
 * This file is the **source of truth for the keys**. `ar.ts` is typed against it, so a
 * key added here and forgotten there is a build error rather than an Arabic screen with
 * an English word sitting in the middle of it.
 *
 * Keys are grouped by where the words appear, and named for what they say rather than
 * where they sit — `nav.pallets`, not `sidebar.item4`. A key that names a position stops
 * being true the first time somebody reorders the menu.
 */
export const en = {
  // The sidebar. Headings first, then the screens under them.
  'nav.main': 'Main',
  'nav.operations': 'Operations',
  'nav.production': 'Production',
  'nav.analytics': 'Analytics',
  'nav.management': 'Management',

  'nav.dashboard': 'Dashboard',
  'nav.inventory': 'Inventory',
  'nav.trace': 'Trace a label',
  'nav.receive': 'Receive Materials',
  'nav.issue': 'Material Issue',
  'nav.rolls': 'Roll Production',
  'nav.rollTests': 'Roll Tests',
  'nav.thermo': 'Thermoforming',
  'nav.thermoTests': 'Thermo Tests',
  'nav.pallets': 'Pallets',
  'nav.packaging': 'Packaging',
  'nav.dispatch': 'Dispatch',
  'nav.recycler': 'Recycler',
  'nav.reports': 'Reports',
  'nav.audit': 'Audit log',
  'nav.recipes': 'Recipes',
  'nav.shifts': 'Shifts',
  'nav.masterData': 'Master Data',
  'nav.users': 'Users',
  'nav.collapse': 'Collapse',

  // The bar across the top.
  'app.name': 'Colors ERP',
  'app.tagline': 'Styrofoam Factory',
  'top.openMenu': 'Open menu',
  'top.signOut': 'Sign out',
  'top.language': 'العربية',
  'top.languageLabel': 'Switch to Arabic',

  // Page titles, and the one line under each.
  'page.dashboard.title': 'Dashboard',
  'page.inventory.title': 'Inventory',
  'page.inventory.subtitle': "What the store holds, in each material's own unit.",
  'page.trace.title': 'Where did this come from?',
  'page.trace.subtitle': 'Scan a roll, a bag or a pallet to see every step behind it.',
  'page.receive.title': 'Receive Materials',
  'page.receive.subtitle': 'Book a delivery into the store.',
  'page.issue.title': 'Material Issue',
  'page.issue.subtitle': 'Material out, leftover back, and what was really used.',
  'page.rolls.title': 'Roll Production',
  'page.rolls.subtitle': 'Rolls off the extruder, each with its own recipe and colour.',
  'page.rollTests.title': 'Roll Tests',
  'page.rollTests.subtitle': 'Weight, length, plate weight and four thickness readings.',
  'page.thermo.title': 'Thermoforming',
  'page.thermo.subtitle': 'One roll goes in whole.',
  'page.thermoTests.title': 'Thermo Tests',
  'page.thermoTests.subtitle': 'Bags, piece weight and bag weight, counted after the run.',
  'page.pallets.title': 'Pallets',
  'page.pallets.subtitle': 'Pallets being built, and the bags on them.',
  'page.packaging.title': 'Packaging',
  'page.packaging.subtitle': 'What each line used for packing.',
  'page.dispatch.title': 'Dispatch',
  'page.dispatch.subtitle': 'Finished pallets leaving the factory.',
  'page.recycler.title': 'Recycler',
  'page.recycler.subtitle': 'How much recycled material the shift produced.',
  'page.reports.title': 'Reports',
  'page.reports.subtitle': 'Worked out from what the shifts recorded.',
  'page.audit.title': 'Audit log',
  'page.audit.subtitle': 'Who changed what, and what was refused.',
  'page.recipes.title': 'Recipes',
  'page.recipes.subtitle': 'The four families and every version.',
  'page.shifts.title': 'Shifts',
  'page.shifts.subtitle': 'One record per shift. Close it when the work is finished.',
  'page.masterData.title': 'Master Data',
  'page.users.title': 'Users',
  'page.users.subtitle': 'Who may sign in, and what each of them may do.',

  // Words that appear on button after button, all over the system.
  'common.save': 'Save',
  'common.cancel': 'Cancel',
  'common.close': 'Close',
  'common.saving': 'Saving…',
  'common.loading': 'Loading…',
  'common.search': 'Search',
  'common.somethingWentWrong': 'Something went wrong. Try again.',
} as const;

/** Every key the screens may ask for. */
export type TranslationKey = keyof typeof en;
