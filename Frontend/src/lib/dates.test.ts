import { afterEach, describe, expect, it, vi } from 'vitest';
import { dayFromNow, isoDay, today } from './dates';

/**
 * The day helpers, and the night-shift bug they were written to end.
 *
 * The reports screen used to build its dates with `toISOString()`, which converts to UTC
 * first. The factory runs at UTC+3, so for the first three hours of every day it named
 * the day before — and those three hours are the middle of the night shift.
 *
 * Every test here that mentions a time uses a **fake clock**, so the result does not
 * depend on when the suite happens to run. Without that, a test like this passes all day
 * and fails at one in the morning, which is worse than having no test at all.
 */

afterEach(() => {
  vi.useRealTimers();
});

/** Freezes the clock at a moment given in the factory's own time, UTC+3. */
const atFactoryTime = (localIso: string): void => {
  vi.useFakeTimers();
  vi.setSystemTime(new Date(`${localIso}+03:00`));
};

describe('isoDay', () => {
  it('writes the day the way the API reads it', () => {
    expect(isoDay(new Date(2026, 7, 8))).toBe('2026-08-08');
  });

  it('pads the month and the day', () => {
    // 3 January, not 3-1. A date input rejects "2026-1-3" outright.
    expect(isoDay(new Date(2026, 0, 3))).toBe('2026-01-03');
  });

  it('reads the clock on the wall, not UTC', () => {
    // Half past one in the morning in the factory. In UTC it is still yesterday
    // evening, and this is exactly where the old code went wrong.
    const nightShift = new Date('2026-08-08T01:30:00+03:00');

    expect(nightShift.toISOString().slice(0, 10)).toBe('2026-08-07');
    expect(isoDay(nightShift)).toBe('2026-08-08');
  });
});

describe('today', () => {
  it('is the day it is here, even in the small hours', () => {
    atFactoryTime('2026-08-08T00:15:00');
    expect(today()).toBe('2026-08-08');
  });

  it('is still today late in the evening', () => {
    atFactoryTime('2026-08-08T23:45:00');
    expect(today()).toBe('2026-08-08');
  });
});

describe('dayFromNow', () => {
  it('gives tomorrow during the night shift', () => {
    // The report range ends tomorrow on purpose: a night shift that starts this evening
    // carries tomorrow's production date. The old UTC version returned 2026-08-08 here
    // — today — and hid the very shift the reader was looking for.
    atFactoryTime('2026-08-08T01:00:00');

    expect(dayFromNow(1)).toBe('2026-08-09');
    expect(dayFromNow(0)).toBe('2026-08-08');
  });

  it('counts backwards for the start of the range', () => {
    atFactoryTime('2026-08-08T09:00:00');
    expect(dayFromNow(-30)).toBe('2026-07-09');
  });

  it('carries across the end of a month', () => {
    atFactoryTime('2026-03-01T09:00:00');
    expect(dayFromNow(-1)).toBe('2026-02-28');
  });

  it('carries across the end of a year', () => {
    atFactoryTime('2026-01-01T02:00:00');
    expect(dayFromNow(-1)).toBe('2025-12-31');
    expect(dayFromNow(1)).toBe('2026-01-02');
  });

  it('knows February has 29 days in a leap year', () => {
    atFactoryTime('2028-03-01T09:00:00');
    expect(dayFromNow(-1)).toBe('2028-02-29');
  });
});
