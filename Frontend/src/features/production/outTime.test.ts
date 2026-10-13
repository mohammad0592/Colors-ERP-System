import { describe, expect, it } from 'vitest';
import { fromTimeField, toOffsetIso, toTimeField } from './outTime';

describe('the out-time box', () => {
  it('shows the time only, in local time', () => {
    expect(toTimeField(new Date(2026, 9, 12, 8, 5, 42))).toBe('08:05');
  });

  it('puts the time on the given day', () => {
    const now = new Date(2026, 9, 12, 15, 0);
    const moment = fromTimeField('14:35', now, now);

    expect(moment).toEqual(new Date(2026, 9, 12, 14, 35));
  });

  it('takes a time later than now to be yesterday', () => {
    // Logged at ten past midnight; it came off at ten to.
    const now = new Date(2026, 9, 13, 0, 10);
    const moment = fromTimeField('23:50', now, now);

    expect(moment).toEqual(new Date(2026, 9, 12, 23, 50));
  });

  it('has no moment for a box that is not a time', () => {
    const now = new Date(2026, 9, 12, 15, 0);
    expect(fromTimeField('', now, now)).toBeNull();
    expect(fromTimeField('25:00', now, now)).toBeNull();
    expect(fromTimeField('half past', now, now)).toBeNull();
  });

  it('writes the moment with the tablet own offset', () => {
    const moment = new Date(2026, 9, 12, 14, 35);
    const written = toOffsetIso(moment);

    expect(written.startsWith('2026-10-12T14:35:00')).toBe(true);
    expect(new Date(written).getTime()).toBe(moment.getTime());
  });
});
