import { RoleNames } from '../lib/roles';

/**
 * Who may **open** each screen — one list, read by the menu and by the route guard.
 *
 * Before this existed the two kept separate lists and drifted apart: six screens let a
 * role in through the address bar while hiding the link from that same role, so an
 * inventory manager allowed into Reports had no way to click there. Nobody would ever
 * find that by using the system — you would have to compare two files side by side.
 *
 * ### Opening a screen is not the same as acting on it
 *
 * There are three separate questions, and they have three separate answers:
 *
 * | Question | Answered by |
 * |---|---|
 * | May he open the screen? | this file |
 * | May he press the buttons on it? | the screen's own `canProduce` / `canTest` / … |
 * | May the change be saved? | the server, on every endpoint |
 *
 * So this list is deliberately the **wider** one. The extruder test person may open Roll
 * Production and see what has been made — he simply gets no "New Roll" button, because
 * the screen asks the second question separately. Locking him out of the screen entirely
 * would hide information he needs to do his own job.
 *
 * None of this is security. It only keeps a man off a screen he cannot use. The server
 * is the real check (specification section 15).
 */

/** `undefined` means every signed-in worker. */
export type ScreenRoles = readonly string[] | undefined;

const { Administrator, Supervisor, InventoryManager } = RoleNames;
const { ExtruderOperator, ExtruderTestPerson } = RoleNames;
const { ThermoOperator, ThermoTestPerson } = RoleNames;
const { PackagingOperator, RecyclerOperator } = RoleNames;

export const screenAccess = {
  '/': undefined,

  // Anyone signed in may see the store — an operator about to start a batch needs to
  // know the material is there. Receiving it is the inventory manager's job, and that
  // is a different screen (specification section 3).
  '/inventory': undefined,

  // Where did this label come from. Open to anyone signed in: it writes nothing, and
  // whoever is holding a label may need to know what is behind it (section 13).
  '/trace': undefined,

  '/inventory/receive': [Administrator, InventoryManager],

  // The supervisor is here because he closes the shift, and a ticket still outstanding
  // is one of the things that stops him (section 2). He cannot issue one.
  '/inventory/issue': [Administrator, InventoryManager, Supervisor],

  // Line 1. Making rolls and measuring them are different jobs, so they are different
  // screens — even though one man holds both roles today (section 3). Both roles may
  // open both screens: the man about to measure needs to see what was made, and the man
  // making them needs to see what has been measured.
  '/production/rolls': [Administrator, Supervisor, ExtruderOperator, ExtruderTestPerson],
  '/production/roll-tests': [
    Administrator,
    Supervisor,
    ExtruderOperator,
    ExtruderTestPerson,
  ],

  // Line 2, split the same way: forming is one job, counting what came out is another.
  '/production/thermo': [Administrator, Supervisor, ThermoOperator, ThermoTestPerson],
  '/production/thermo-tests': [
    Administrator,
    Supervisor,
    ThermoOperator,
    ThermoTestPerson,
  ],

  // Packing. The supervisor is here because he answers for what is still part-built at
  // the end of his shift, and he is the one who takes a wrongly scanned bag back off.
  '/production/pallets': [Administrator, Supervisor, PackagingOperator],
  '/production/packaging': [Administrator, Supervisor, PackagingOperator],

  // Sending a finished pallet out is not floor work. It takes the pallet out of the
  // factory's stock for good, so it sits with the supervisor — the same fence as taking
  // a wrongly scanned bag back off (section 10).
  '/production/dispatch': [Administrator, Supervisor],

  // Line 3, the recycler (section 11). The supervisor reads it because what it produced
  // is part of the shift he answers for.
  '/production/recycler': [Administrator, Supervisor, RecyclerOperator],

  // Reports account for the shift, so they are for the people who answer for it. The
  // inventory manager is here for the waste control report, which is his (section 7).
  '/reports': [Administrator, Supervisor, InventoryManager],

  // Who changed what, and what was refused. The administrator answers for the system,
  // the supervisor for the shift (section 15).
  '/audit': [Administrator, Supervisor],

  // The supervisor adjusts the percentages, so recipes are his job too (section 3).
  '/recipes': [Administrator, Supervisor],

  // Opening and closing shifts is the supervisor's job. Reopening a closed one is the
  // administrator's, which the screen and the server both enforce (section 2).
  '/shifts': [Administrator, Supervisor],

  // Master data changes affect every screen, and who may sign in affects everything.
  // The administrator's alone (section 3).
  '/master-data': [Administrator],
  '/users': [Administrator],
} as const satisfies Record<string, ScreenRoles>;

export type ScreenPath = keyof typeof screenAccess;

/**
 * The roles for one screen.
 *
 * Typed against the list above, so a path that does not exist — or one renamed on only
 * one side — is a build error rather than a screen that quietly lets everybody in.
 */
export function rolesFor(path: ScreenPath): ScreenRoles {
  return screenAccess[path];
}
