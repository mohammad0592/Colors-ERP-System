/**
 * How long a finished pallet has been standing in the factory.
 *
 * The dispatch list is oldest first, which is the order the factory should load in —
 * but "2026-03-14" tells a man nothing at a glance, and the whole point of the list is
 * that he can see at a glance which pallet has been waiting.
 *
 * Kept apart from the screen so it can be tested without rendering anything.
 */

/** Whole days between the two moments, never negative. */
export function daysWaiting(completedAt: string, now: Date): number {
  const completed = new Date(completedAt);
  if (Number.isNaN(completed.getTime())) {
    return 0;
  }

  const days = Math.floor((now.getTime() - completed.getTime()) / 86_400_000);

  // A clock a few minutes behind the server would otherwise read "-1 days".
  return days < 0 ? 0 : days;
}

/**
 * The same number in words. Today and yesterday are named rather than counted, because
 * that is how the floor says it.
 */
export function waitingLabel(completedAt: string, now: Date): string {
  const days = daysWaiting(completedAt, now);

  if (days === 0) {
    return 'Finished today';
  }

  if (days === 1) {
    return 'Waiting since yesterday';
  }

  return `Waiting ${String(days)} days`;
}

/**
 * A pallet that has stood long enough to be worth pointing at.
 *
 * Sixty days is the figure the specification already uses for rolls left in stock
 * (section 18, the proposed dashboard), so the same one is used here rather than
 * inventing a second answer to the same question.
 */
export function isStale(completedAt: string, now: Date): boolean {
  return daysWaiting(completedAt, now) >= 60;
}
