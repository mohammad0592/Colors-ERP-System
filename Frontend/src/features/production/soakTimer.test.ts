import { describe, expect, it } from 'vitest';
import { formatCountdown } from './soakTimer';

describe('formatCountdown', () => {
  it('shows minutes and seconds left', () => {
    expect(formatCountdown(10 * 60 * 1000)).toBe('10:00');
    expect(formatCountdown(65 * 1000)).toBe('1:05');
  });

  it('rounds a part second up, so it never says 0:00 too soon', () => {
    expect(formatCountdown(400)).toBe('0:01');
  });

  it('stops at zero rather than counting past it', () => {
    expect(formatCountdown(-5000)).toBe('0:00');
  });
});
