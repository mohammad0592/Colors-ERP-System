import { today } from '../../lib/dates';

/**
 * Small shared helpers for the shift screens.
 *
 * The factory writes dates day/month/year — that is what the roll codes and the paper
 * forms use — so every date shown here follows the same order.
 */

/** "2026-08-03" to "03/08/2026". Never uses the browser's locale: the order matters. */
export function formatDate(isoDate: string): string {
  const parts = isoDate.split('-');
  if (parts.length !== 3) {
    return isoDate;
  }
  return `${parts[2] ?? ''}/${parts[1] ?? ''}/${parts[0] ?? ''}`;
}

/**
 * Today, as the "yyyy-MM-dd" the API expects, on the clock the worker is looking at.
 *
 * This was right, and the reports screen's copy of it was not. Now there is one copy
 * (see `lib/dates.ts`) and the question cannot be answered two ways again.
 */
export function todayIso(): string {
  return today();
}

/** A number for the screen, or a dash when the reading has not been taken yet. */
export function orDash(value: number | null, suffix = ''): string {
  return value === null ? '—' : `${String(value)}${suffix}`;
}

/**
 * Turns a text box into the number the API wants.
 *
 * An empty box means "not recorded", which is null — not zero. Zero downtime and
 * no downtime recorded are different facts.
 */
export function toNumberOrNull(value: string): number | null {
  const trimmed = value.trim();
  if (trimmed === '') {
    return null;
  }
  const parsed = Number(trimmed);
  return Number.isFinite(parsed) ? parsed : null;
}

/** The other direction: a number into the text box, blank when nothing is recorded. */
export function toField(value: number | null): string {
  return value === null ? '' : String(value);
}
