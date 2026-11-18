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
 * So this list is deliberately the **wider** one. The supervisor may open Roll
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
const { ExtruderOperator, ThermoOperator, RecyclerOperator } = RoleNames;
const { LabelPrinter } = RoleNames;

export const screenAccess = {
  '/': undefined,

  // Anyone signed in may see the store — an operator about to start a batch needs to
  // know the material is there. Receiving it is the inventory manager's job, and that
  // is a different screen (specification section 3).
  '/inventory': undefined,

  // Where did this label come from. Open to anyone signed in: it writes nothing, and
  // whoever is holding a label may need to know what is behind it (section 13).
  '/trace': undefined,

  // The label printer's queue, on the computer it hangs off. The tablets have no printer
  // and send their labels here (section 19.13). The supervisor and administrator may open
  // it to see what is waiting.
  '/print-queue': [Administrator, Supervisor, LabelPrinter],

  '/inventory/receive': [Administrator, InventoryManager],

  // The store's own business. The supervisor once had it too, to see the tickets that
  // stop a shift closing; the close names them, and the store is the storekeeper's
  // (section 19.31).
  '/inventory/issue': [Administrator, InventoryManager],

  // Line 1. One role for the line (section 19.31): the man who makes the rolls tests them.
  '/production/rolls': [Administrator, Supervisor, ExtruderOperator],
  '/production/roll-tests': [Administrator, Supervisor, ExtruderOperator],

  // Line 2, the same: forming, counting what came out, and packing it are one job.
  '/production/thermo': [Administrator, Supervisor, ThermoOperator],
  '/production/thermo-tests': [Administrator, Supervisor, ThermoOperator],

  // Packing. The supervisor is here because he answers for what is still part-built at
  // the end of his shift, and he is the one who takes a wrongly scanned bag back off.
  '/production/pallets': [Administrator, Supervisor, ThermoOperator],
  '/production/packaging': [Administrator, Supervisor, ThermoOperator],

  // Sending a finished pallet out is not floor work. It takes the pallet out of the
  // factory's stock for good, so it sits with the supervisor — the same fence as taking
  // a wrongly scanned bag back off (section 10).
  '/production/dispatch': [Administrator, Supervisor],

  // Line 3, the recycler (section 11). The supervisor reads it because what it produced
  // is part of the shift he answers for.
  '/production/recycler': [Administrator, Supervisor, RecyclerOperator],

  // Each line's times, crew, notes and machine settings, one screen per line, opened by
  // that line's operator (section 19.18). The factory's one meter is on all three, so a
  // reading entered on any of them is seen on the others. The supervisor and the
  // administrator answer for every line and open all three. The server still lets each
  // operator change only the line he was put on (section 2).
  '/production/shift-config/extruder': [Administrator, Supervisor, ExtruderOperator],
  '/production/shift-config/thermo': [Administrator, Supervisor, ThermoOperator],
  '/production/shift-config/recycler': [Administrator, Supervisor, RecyclerOperator],

  // Reports account for the shift, so they are for the people who answer for it. The
  // inventory manager is here for the waste control report, which is his (section 7).
  '/reports': [Administrator, Supervisor, InventoryManager],

  // Who changed what, and what was refused. The administrator answers for the system
  // (sections 15 and 19.31).
  '/audit': [Administrator],

  // A recipe decides what every roll is made of; the factory keeps it with one person
  // (section 19.31).
  '/recipes': [Administrator],

  // Opening and closing shifts is the supervisor's job. Reopening a closed one is the
  // administrator's, which the screen and the server both enforce (section 2).
  '/shifts': [Administrator, Supervisor],

  // Planning tomorrow's shifts is opening them in advance, so it is the same people's
  // job (section 19.30).
  '/shift-schedule': [Administrator, Supervisor],

  // Master data changes affect every screen, and who may sign in affects everything.
  // The administrator's alone (section 3).
  '/master-data': [Administrator],
  '/users': [Administrator],
} as const satisfies Record<string, ScreenRoles>;

export type ScreenPath = keyof typeof screenAccess;

/**
 * Who sees what on the Inventory screen (section 19.32). The server keeps to the same
 * lists, so a hidden tab is also a refused request.
 *
 * Raw material goes into the extruder's mixer, so the thermo operator does not see it;
 * he does see the packing, which he uses. The extruder operator's line makes rolls and
 * never handles a bag, so he does not see the bags and pallets.
 */
export const rawMaterialRoles = [
  Administrator,
  Supervisor,
  InventoryManager,
  ExtruderOperator,
  RecyclerOperator,
] as const;

export const packingMaterialRoles = [...rawMaterialRoles, ThermoOperator] as const;

export const bagAndPalletRoles = [
  Administrator,
  Supervisor,
  InventoryManager,
  ThermoOperator,
  RecyclerOperator,
] as const;

/**
 * The roles for one screen.
 *
 * Typed against the list above, so a path that does not exist — or one renamed on only
 * one side — is a build error rather than a screen that quietly lets everybody in.
 */
export function rolesFor(path: ScreenPath): ScreenRoles {
  return screenAccess[path];
}
