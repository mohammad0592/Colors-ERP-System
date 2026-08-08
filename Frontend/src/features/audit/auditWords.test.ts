import { describe as group, expect, it } from 'vitest';
import { describe, filterableThings } from './auditWords';

/**
 * The audit screen's wording and its filter.
 *
 * The filter is the part that has been wrong before. Successes are logged against the
 * table that changed, refusals against the shape the endpoint returns — two different
 * names for the same subject. A group that lists only one of them shows the
 * corrections and hides the refusals, which is the half a supervisor came to see.
 */

const groupNames = (value: string): string[] => value.split(',');
const allNames = filterableThings.flatMap((thing) => groupNames(thing.value));

group('describe', () => {
  it('reads a change as what was done and to what', () => {
    expect(describe('Modified', 'Color', 4, false)).toBe('Changed a colour (#4)');
  });

  it('names the three deeds in words a supervisor uses', () => {
    expect(describe('Added', 'RecipeVersion', 2, false)).toBe(
      'Created a recipe version (#2)',
    );
    expect(describe('Deleted', 'Material', 9, false)).toBe('Removed a material (#9)');
  });

  it('leaves off the number when there is not one', () => {
    // A refusal has no row, and neither does a delete once it is gone.
    expect(describe('Modified', 'ShiftReport', null, false)).toBe('Changed a shift');
  });

  it('reads a refusal as the thing that was attempted', () => {
    // Not "Changed a PalletDto" — nothing was changed. Somebody tried and was stopped.
    expect(describe('Pallets.ScanBag', 'PalletDto', null, true)).toBe(
      'Scanning a bag onto a pallet',
    );
  });

  it('shows a name it does not know rather than hiding the line', () => {
    // A kind of record added on the server must appear in the log the day it exists,
    // even before anybody has written a sentence for it.
    expect(describe('Modified', 'SomethingNew', 3, false)).toBe(
      'Changed SomethingNew (#3)',
    );
    expect(describe('Feature.NewThing', 'WhateverDto', null, true)).toBe(
      'Feature.NewThing',
    );
  });

  it('is not fooled by the names every object carries', () => {
    // The three tables are plain objects, so a lookup of "constructor" answers with a
    // function unless it is asked properly. See lib/words.ts.
    expect(describe('constructor', 'toString', null, false)).toBe('constructor toString');
    expect(describe('constructor', 'Color', null, true)).toBe('constructor');
  });

  it('has a sentence for a refused sign-in and a refused renewal', () => {
    // Both appear in the live log. The renewal happens without anybody pressing
    // anything, so it needs saying plainly or it reads like a fault.
    expect(describe('Auth.Login', 'AuthenticationResult', null, true)).toBe('Signing in');
    expect(describe('Auth.Refresh', 'AuthenticationResult', null, true)).toBe(
      'Renewing a sign-in',
    );
  });
});

group('the filter', () => {
  it('names every kind of record the log can hold', () => {
    // Taken from the server: every type a write endpoint returns, which is what a
    // refusal is recorded against. A name missing here is a line nobody can find.
    const loggedTypes = [
      'PalletDto',
      'RollDto',
      'ThermoRunDto',
      'PackagingConsumptionDto',
      'RecyclerProductionDto',
      'ShiftReportDto',
      'IssueTicketDto',
      'MaterialStockDto',
      'RecipeFamilyDto',
      'RecipeVersionDto',
      'UserDto',
      'AuthenticationResult',
      'MaterialDto',
      'ProductDto',
      'LookupDto',
      'ColorDto',
      'UnitDto',
      'ProductionLineDto',
      'ShiftDto',
    ];

    const missing = loggedTypes.filter((type) => !allNames.includes(type));
    expect(missing).toEqual([]);
  });

  it('covers the tables that changes are recorded against', () => {
    const auditedTables = [
      'WoodenPallet',
      'BagPalletAssignment',
      'ShiftReport',
      'MaterialIssueTicket',
      'RecipeFamily',
      'RecipeVersion',
      'RecipeIngredient',
      'ApplicationUser',
      'ApplicationRole',
      'Material',
      'MaterialCategory',
      'MaterialPackaging',
      'Product',
      'ProductType',
      'Mould',
      'Color',
      'Unit',
      'ProductionLine',
      'Shift',
      'MovementType',
    ];

    const missing = auditedTables.filter((table) => !allNames.includes(table));
    expect(missing).toEqual([]);
  });

  it('puts each name in one group only', () => {
    // A name in two groups means two options showing the same lines, and a reader who
    // picks the wrong one concludes there is nothing there.
    const seen = new Set<string>();
    const twice = allNames.filter((name) => {
      if (seen.has(name)) {
        return true;
      }
      seen.add(name);
      return false;
    });

    expect(twice).toEqual([]);
  });

  it('has no blank names and no stray spaces', () => {
    // The value goes onto the query string as-is and is split on the comma by the
    // server. A space would be part of the name and would match nothing.
    for (const name of allNames) {
      expect(name).not.toBe('');
      expect(name).toBe(name.trim());
    }
  });

  it('gives every group a label somebody would recognise', () => {
    for (const thing of filterableThings) {
      expect(thing.label.length).toBeGreaterThan(2);
      // No type names on screen. "Pallets and bags on them", not "PalletDto".
      expect(thing.label).not.toMatch(/Dto\b/);
    }
  });
});
