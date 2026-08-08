import { describe, expect, it } from 'vitest';
import { wordFor } from './words';

describe('wordFor', () => {
  const words: Record<string, string> = { Modified: 'Changed', Added: 'Created' };

  it('gives the word when it knows it', () => {
    expect(wordFor(words, 'Modified')).toBe('Changed');
  });

  it('gives the raw name when it does not', () => {
    // The line still has to appear. Hiding what the system cannot name is how a log
    // ends up not showing the one thing somebody is looking for.
    expect(wordFor(words, 'Reopened')).toBe('Reopened');
  });

  it('is not fooled by the names every object carries', () => {
    // This is the whole reason the function exists. `words['constructor']` is a
    // function, not undefined, so `?? key` never runs and a function reaches the
    // screen — where React draws nothing and says nothing.
    expect(wordFor(words, 'constructor')).toBe('constructor');
    expect(wordFor(words, 'toString')).toBe('toString');
    expect(wordFor(words, 'hasOwnProperty')).toBe('hasOwnProperty');
    expect(wordFor(words, '__proto__')).toBe('__proto__');
  });

  it('handles an empty name and an empty table', () => {
    expect(wordFor(words, '')).toBe('');
    expect(wordFor({}, 'anything')).toBe('anything');
  });
});
