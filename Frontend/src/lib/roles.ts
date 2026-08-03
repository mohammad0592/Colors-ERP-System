/**
 * The nine roles, mirroring Colors.Domain.Constants.RoleNames on the server.
 *
 * Written as constants, never as loose strings: a typo in a role name would hide a
 * menu item with no error and nothing to search for. The `as const` makes any typo
 * a build error instead.
 */
export const RoleNames = {
  Administrator: 'Administrator',
  Supervisor: 'Supervisor',
  InventoryManager: 'InventoryManager',
  ExtruderOperator: 'ExtruderOperator',
  ExtruderTestPerson: 'ExtruderTestPerson',
  ThermoOperator: 'ThermoOperator',
  ThermoTestPerson: 'ThermoTestPerson',
  PackagingOperator: 'PackagingOperator',
  RecyclerOperator: 'RecyclerOperator',
} as const;

export type RoleName = (typeof RoleNames)[keyof typeof RoleNames];

/** What each role is called on screen, for the header and the user list. */
export const roleLabels: Record<RoleName, string> = {
  Administrator: 'Administrator',
  Supervisor: 'Supervisor',
  InventoryManager: 'Inventory Manager',
  ExtruderOperator: 'Extruder Operator',
  ExtruderTestPerson: 'Extruder Test Person',
  ThermoOperator: 'Thermo Operator',
  ThermoTestPerson: 'Thermo Test Person',
  PackagingOperator: 'Packaging Operator',
  RecyclerOperator: 'Recycler Operator',
};

/**
 * Turns a stored role name into something readable, unchanged if unknown.
 *
 * Widened on purpose: the role arrives from the server as a plain string, so a role
 * added there before the client knows about it must show its raw name rather than
 * crash or render "undefined".
 */
const lookup: Partial<Record<string, string>> = roleLabels;

export function labelForRole(role: string): string {
  return lookup[role] ?? role;
}
