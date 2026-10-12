import { describe, expect, it } from 'vitest';
import { fromOutTimeField, toOutTimeField } from './outTime';

describe('the out-time box', () => {
  it('writes a moment in local time, to the minute', () => {
    const moment = new Date(2026, 9, 12, 8, 5, 42);
    expect(toOutTimeField(moment)).toBe('2026-10-12T08:05');
  });

  it('reads back the same moment it wrote', () => {
    const moment = new Date(2026, 9, 12, 14, 35);
    expect(fromOutTimeField(toOutTimeField(moment))).toBe(moment.toISOString());
  });

  it('has no moment for an empty or broken box', () => {
    expect(fromOutTimeField('')).toBeNull();
    expect(fromOutTimeField('not a time')).toBeNull();
  });
});
