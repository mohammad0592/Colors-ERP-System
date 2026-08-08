import { describe, expect, it } from 'vitest';
import { formatDate, orDash, toField, toNumberOrNull } from './shiftFormat';

/**
 * The small conversions between what a text box holds and what the API wants.
 *
 * They look too simple to test until you remember what they carry: an empty downtime
 * box that becomes 0 instead of null says the line ran all shift with no stoppage,
 * which is a different claim from "nobody wrote it down".
 */

describe('formatDate', () => {
  it('writes the day first, the way the factory writes it', () => {
    // The roll codes and the paper forms are all day/month/year. A screen that showed
    // 08/03 for the 3rd of August would be read as the 8th of March.
    expect(formatDate('2026-08-03')).toBe('03/08/2026');
  });

  it('leaves anything that is not a date alone', () => {
    // Better a raw value on screen than a mangled one that looks right.
    expect(formatDate('')).toBe('');
    expect(formatDate('not a date')).toBe('not a date');
  });
});

describe('toNumberOrNull', () => {
  it('reads a number', () => {
    expect(toNumberOrNull('12.5')).toBe(12.5);
    expect(toNumberOrNull('  7 ')).toBe(7);
  });

  it('turns an empty box into nothing recorded, not zero', () => {
    // Zero downtime and no downtime written down are different facts, and the shift
    // report shows them differently.
    expect(toNumberOrNull('')).toBeNull();
    expect(toNumberOrNull('   ')).toBeNull();
  });

  it('keeps a real zero', () => {
    // He did write it down, and what he wrote was nought.
    expect(toNumberOrNull('0')).toBe(0);
  });

  it('refuses rubbish rather than sending NaN to the server', () => {
    expect(toNumberOrNull('abc')).toBeNull();
    expect(toNumberOrNull('1.2.3')).toBeNull();
  });
});

describe('toField', () => {
  it('puts a number in the box and leaves nothing as blank', () => {
    expect(toField(9.5)).toBe('9.5');
    expect(toField(null)).toBe('');
  });

  it('shows a zero rather than an empty box', () => {
    // The round trip has to hold: a recorded zero must come back as "0", or opening
    // the shift and saving it again would quietly erase the reading.
    expect(toField(0)).toBe('0');
    expect(toNumberOrNull(toField(0))).toBe(0);
    expect(toNumberOrNull(toField(null))).toBeNull();
  });
});

describe('orDash', () => {
  it('shows a dash when the reading has not been taken', () => {
    expect(orDash(null)).toBe('—');
    expect(orDash(null, ' kg')).toBe('—');
  });

  it('shows the number with its unit when it has', () => {
    expect(orDash(12, ' kg')).toBe('12 kg');
    expect(orDash(0, ' h')).toBe('0 h');
  });
});
