/**
 * A roll's "out at" time as the box shows it — the time only, "14:35" — and back to the
 * full moment the server keeps (specification section 19.12).
 *
 * The date is filled in behind the box: the day the roll was made on, or for a new roll,
 * today. A time later than now is taken to be yesterday's — a roll logged at 00:10 that
 * came off at 23:50 came off before midnight, not in the future.
 *
 * Sent with the tablet's own offset, "2026-10-13T14:35:00+03:00", because the date and
 * time inside the roll code are the factory's clock and the server knows no time zone.
 */

function pad(value: number): string {
  return String(value).padStart(2, '0');
}

/** The time of a moment as the box writes it, in the tablet's local time. */
export function toTimeField(moment: Date): string {
  return `${pad(moment.getHours())}:${pad(moment.getMinutes())}`;
}

/** A moment written with the tablet's own offset rather than as UTC. */
export function toOffsetIso(moment: Date): string {
  const offset = -moment.getTimezoneOffset();
  const sign = offset >= 0 ? '+' : '-';
  const hours = pad(Math.floor(Math.abs(offset) / 60));
  const minutes = pad(Math.abs(offset) % 60);

  return (
    `${String(moment.getFullYear())}-${pad(moment.getMonth() + 1)}-${pad(moment.getDate())}` +
    `T${pad(moment.getHours())}:${pad(moment.getMinutes())}:00${sign}${hours}:${minutes}`
  );
}

/**
 * The box's "HH:mm" on the day of `day`, or the day before when that would be in the
 * future. Null when the box does not hold a time.
 */
export function fromTimeField(
  value: string,
  day: Date,
  now: Date = new Date(),
): Date | null {
  const match = /^(\d{1,2}):(\d{2})$/.exec(value.trim());
  if (match === null) {
    return null;
  }

  const hours = Number(match[1]);
  const minutes = Number(match[2]);
  if (hours > 23 || minutes > 59) {
    return null;
  }

  const moment = new Date(
    day.getFullYear(),
    day.getMonth(),
    day.getDate(),
    hours,
    minutes,
  );

  // Five minutes of slack, as the server allows, for two tablets' clocks disagreeing.
  if (moment.getTime() > now.getTime() + 5 * 60 * 1000) {
    moment.setDate(moment.getDate() - 1);
  }

  return moment;
}
