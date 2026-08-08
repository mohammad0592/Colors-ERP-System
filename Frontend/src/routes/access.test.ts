import { describe, expect, it } from 'vitest';
import { navigation } from '../components/layout/navigation';
import { RoleNames, type RoleName } from '../lib/roles';
import { rolesFor, screenAccess, type ScreenPath } from './access';

/**
 * The menu and the route guard must offer the same screens to the same people.
 *
 * This is the test that pays for itself. The two lists used to be written by hand in
 * separate files, and they had drifted apart in six places — an inventory manager was
 * allowed into Reports with no link to click, a supervisor into Material Issue the same
 * way. Nothing on screen said so; you had to read two files side by side to see it.
 */

const everyRole: RoleName[] = Object.values(RoleNames);
const menuItems = navigation.flatMap((group) => group.items);

/** The sidebar's own rule, copied from Sidebar.tsx, and the dashboard's shortcuts. */
const menuShows = (roles: readonly string[] | undefined, held: RoleName): boolean =>
  roles === undefined || roles.includes(held);

/** The guard's own rule, copied from ProtectedRoute.tsx. */
const guardAllows = (path: ScreenPath, held: RoleName): boolean => {
  const roles = rolesFor(path);
  return roles === undefined || roles.includes(held);
};

describe('the menu and the route guard', () => {
  it('agree for every screen and every role', () => {
    const disagreements: string[] = [];

    for (const item of menuItems) {
      for (const role of everyRole) {
        const shown = menuShows(item.roles, role);
        const allowed = guardAllows(item.path, role);

        if (shown !== allowed) {
          disagreements.push(
            `${item.path} — ${role}: menu ${shown ? 'shows' : 'hides'} it, ` +
              `guard ${allowed ? 'allows' : 'blocks'} it`,
          );
        }
      }
    }

    expect(disagreements).toEqual([]);
  });

  it('offers every screen in the list to somebody', () => {
    // A screen nobody can reach is either a mistake in its role list or a screen that
    // should have been deleted. Either way it should not sit there unnoticed.
    const unreachable = (Object.keys(screenAccess) as ScreenPath[]).filter(
      (path) => !everyRole.some((role) => guardAllows(path, role)),
    );

    expect(unreachable).toEqual([]);
  });

  it('puts every menu item on a real screen', () => {
    // `path` is typed as ScreenPath so this cannot compile otherwise — but the check
    // stays, because the type would not survive somebody widening it to `string`.
    const orphans = menuItems.filter((item) => !(item.path in screenAccess));

    expect(orphans.map((item) => item.label)).toEqual([]);
  });
});

describe('who may open what', () => {
  /** Every screen a role can reach, by path, sorted. */
  const screensFor = (role: RoleName): string[] =>
    (Object.keys(screenAccess) as ScreenPath[])
      .filter((path) => guardAllows(path, role))
      .sort();

  it('lets the administrator everywhere', () => {
    // He answers for the system, and he is the one who fixes a shift somebody closed
    // by mistake at two in the morning (specification section 3).
    expect(screensFor(RoleNames.Administrator)).toEqual(Object.keys(screenAccess).sort());
  });

  it('keeps master data and users to the administrator alone', () => {
    // Master data changes every screen at once, and user management decides who may
    // sign in at all. Nobody else, not even the supervisor.
    for (const path of ['/master-data', '/users'] as const) {
      const allowed = everyRole.filter((role) => guardAllows(path, role));
      expect(allowed).toEqual([RoleNames.Administrator]);
    }
  });

  it('gives the inventory manager the reports he answers for', () => {
    // The waste control report is his (specification section 7). This was the bug:
    // the guard let him in and the menu gave him no way to get there.
    expect(guardAllows('/reports', RoleNames.InventoryManager)).toBe(true);

    const reports = menuItems.find((item) => item.path === '/reports');
    expect(reports).toBeDefined();
    expect(menuShows(reports?.roles, RoleNames.InventoryManager)).toBe(true);
  });

  it('lets the supervisor see the tickets that stop him closing the shift', () => {
    // A ticket still outstanding blocks the close (specification section 2), so he has
    // to be able to look. He still cannot issue one — the screen decides that.
    expect(guardAllows('/inventory/issue', RoleNames.Supervisor)).toBe(true);

    const issue = menuItems.find((item) => item.path === '/inventory/issue');
    expect(menuShows(issue?.roles, RoleNames.Supervisor)).toBe(true);
  });

  it('keeps an operator off the lines that are not his', () => {
    // The recycler operator has no business on the extruder or the thermo screens.
    for (const path of [
      '/production/rolls',
      '/production/roll-tests',
      '/production/thermo',
      '/production/thermo-tests',
      '/production/pallets',
    ] as const) {
      expect(guardAllows(path, RoleNames.RecyclerOperator)).toBe(false);
    }

    expect(guardAllows('/production/recycler', RoleNames.RecyclerOperator)).toBe(true);
  });

  it('opens the store and the label lookup to everyone signed in', () => {
    // An operator about to start a batch needs to know the material is there, and
    // whoever is holding a label needs to know what is behind it. Neither writes
    // anything (specification sections 6 and 13).
    for (const role of everyRole) {
      expect(guardAllows('/inventory', role)).toBe(true);
      expect(guardAllows('/trace', role)).toBe(true);
      expect(guardAllows('/', role)).toBe(true);
    }
  });

  it('does not let a role with no production job onto a production screen', () => {
    // The inventory manager receives and issues. He does not run a machine.
    for (const path of [
      '/production/rolls',
      '/production/thermo',
      '/production/pallets',
      '/production/recycler',
    ] as const) {
      expect(guardAllows(path, RoleNames.InventoryManager)).toBe(false);
    }
  });
});
