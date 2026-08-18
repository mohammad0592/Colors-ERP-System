import { describe, expect, it } from 'vitest';
import { daysWaiting, isStale, waitingLabel } from './dispatchFormat';

const now = new Date('2026-08-18T10:00:00Z');

describe('daysWaiting', () => {
  it('counts whole days', () => {
    expect(daysWaiting('2026-08-15T10:00:00Z', now)).toBe(3);
  });

  it('is zero on the day the pallet was finished', () => {
    expect(daysWaiting('2026-08-18T02:00:00Z', now)).toBe(0);
  });

  it('never goes negative when the tablet clock is behind the server', () => {
    expect(daysWaiting('2026-08-19T10:00:00Z', now)).toBe(0);
  });

  it('reads a date it cannot parse as no wait at all', () => {
    expect(daysWaiting('not a date', now)).toBe(0);
  });
});

describe('waitingLabel', () => {
  it('names today rather than counting it', () => {
    expect(waitingLabel('2026-08-18T02:00:00Z', now)).toBe('Finished today');
  });

  it('names yesterday too', () => {
    expect(waitingLabel('2026-08-17T02:00:00Z', now)).toBe('Waiting since yesterday');
  });

  it('counts anything older', () => {
    expect(waitingLabel('2026-08-04T10:00:00Z', now)).toBe('Waiting 14 days');
  });
});

describe('isStale', () => {
  it('leaves a pallet finished this week alone', () => {
    expect(isStale('2026-08-15T10:00:00Z', now)).toBe(false);
  });

  it('points at one that has stood for sixty days', () => {
    expect(isStale('2026-06-19T10:00:00Z', now)).toBe(true);
  });
});
