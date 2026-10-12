/**
 * A roll's "out at" moment as the date-and-time box shows it, and back.
 *
 * The box speaks the tablet's own local time, "2026-10-12T14:35"; the server keeps a
 * moment in time. Kept apart from the dialogs so both read and write it the same way.
 */

function pad(value: number): string {
  return String(value).padStart(2, '0');
}

/** A moment as the box writes it, in the tablet's local time, to the minute. */
export function toOutTimeField(moment: Date): string {
  return (
    `${String(moment.getFullYear())}-${pad(moment.getMonth() + 1)}-${pad(moment.getDate())}` +
    `T${pad(moment.getHours())}:${pad(moment.getMinutes())}`
  );
}

/** What the box holds as a moment for the server, or null when it is not a time. */
export function fromOutTimeField(value: string): string | null {
  if (value.trim() === '') {
    return null;
  }

  const moment = new Date(value);
  return Number.isNaN(moment.getTime()) ? null : moment.toISOString();
}
