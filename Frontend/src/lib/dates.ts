/**
 * The day, written the way the API writes it.
 *
 * One function, in one place, because the two screens that needed it had each written
 * their own and the two did not agree. The shift screen read the clock on the wall; the
 * reports screen read UTC. In this factory that is a three-hour difference, so between
 * midnight and three in the morning they named different days.
 *
 * That is not a corner case here. It is the night shift.
 */

/**
 * A `Date` as the `yyyy-mm-dd` that date inputs and the API both use.
 *
 * Deliberately built from the local parts rather than `toISOString()`. `toISOString`
 * converts to UTC first, so at 01:00 in a UTC+3 country it reports yesterday — the
 * screen would ask for the wrong day and show a report that looked merely empty.
 */
export function isoDay(date: Date): string {
  const year = String(date.getFullYear());
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/** Today, on the clock the worker is looking at. */
export function today(): string {
  return isoDay(new Date());
}

/**
 * A day this many days from today. Negative counts backwards.
 *
 * Uses `setDate`, which carries across month and year ends by itself — the 3rd less
 * thirty days is the 4th of the month before, and no arithmetic here has to know it.
 */
export function dayFromNow(days: number): string {
  const date = new Date();
  date.setDate(date.getDate() + days);
  return isoDay(date);
}
