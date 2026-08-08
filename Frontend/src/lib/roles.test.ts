import { describe, expect, it } from 'vitest';
import { labelForRole, roleLabels, RoleNames, type RoleName } from './roles';

/**
 * The nine roles, and the words for them.
 *
 * The names themselves have to match the server exactly. A typo would not throw
 * anything — it would quietly hide a menu item and refuse a screen, with nothing in the
 * console and nothing to search for.
 */

describe('the role names', () => {
  it('are the nine the specification lists', () => {
    // Section 3: two testing stages, so two testing roles, and packaging is its own job
    // even though one man does it alongside the thermo today.
    expect(Object.values(RoleNames).sort()).toEqual(
      [
        'Administrator',
        'ExtruderOperator',
        'ExtruderTestPerson',
        'InventoryManager',
        'PackagingOperator',
        'RecyclerOperator',
        'Supervisor',
        'ThermoOperator',
        'ThermoTestPerson',
      ].sort(),
    );
  });

  it('are spelled the same as their keys', () => {
    // The constant and the string the server sends are the same thing. If they ever
    // drift, every check against that role silently stops matching.
    for (const [key, value] of Object.entries(RoleNames)) {
      expect(value).toBe(key);
    }
  });

  it('all have a label for the screen', () => {
    for (const role of Object.values(RoleNames)) {
      expect(roleLabels[role]).toBeTruthy();
    }
  });
});

describe('labelForRole', () => {
  it('spaces out the run-together names', () => {
    expect(labelForRole(RoleNames.InventoryManager)).toBe('Inventory Manager');
    expect(labelForRole(RoleNames.ExtruderTestPerson)).toBe('Extruder Test Person');
  });

  it('shows a role it has never heard of rather than "undefined"', () => {
    // A role added on the server before the screens know about it must still read as
    // something. This is the reason the lookup is widened to plain strings.
    expect(labelForRole('SomeNewRole')).toBe('SomeNewRole');
    expect(labelForRole('')).toBe('');
  });

  it('does not fall for a name borrowed from Object', () => {
    // roleLabels is a plain object, so "constructor" and "toString" are inherited names
    // that a careless lookup would return a function for.
    expect(labelForRole('constructor')).toBe('constructor');
    expect(labelForRole('toString')).toBe('toString');
  });
});

describe('RoleName', () => {
  it('is the type of every value in the list', () => {
    // A compile-time check written as a test so it is not deleted as unused.
    const everyRole: RoleName[] = Object.values(RoleNames);
    expect(everyRole).toHaveLength(9);
  });
});
