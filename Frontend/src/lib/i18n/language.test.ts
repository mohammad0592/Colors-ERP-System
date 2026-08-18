import { describe, expect, it } from 'vitest';
import { ar } from './ar';
import { en } from './en';
import { dictionaries, directions, initialLanguage } from './language';

describe('initialLanguage', () => {
  it('honours what the man chose last time', () => {
    expect(initialLanguage('ar', 'en-GB')).toBe('ar');
    expect(initialLanguage('en', 'ar-JO')).toBe('en');
  });

  it('falls back to the browser only when nothing was chosen', () => {
    expect(initialLanguage(null, 'ar-JO')).toBe('ar');
    expect(initialLanguage(null, 'en-GB')).toBe('en');
  });

  it('reads any flavour of Arabic as Arabic', () => {
    for (const tag of ['ar', 'ar-JO', 'ar-EG', 'AR-SA']) {
      expect(initialLanguage(null, tag)).toBe('ar');
    }
  });

  it('lands on English when there is nothing to go on', () => {
    expect(initialLanguage(null, undefined)).toBe('en');
  });

  it('ignores a stored value that is not a language we have', () => {
    // Somebody editing local storage by hand, or a value left by an older version.
    expect(initialLanguage('fr', 'en-GB')).toBe('en');
    expect(initialLanguage('', 'ar-JO')).toBe('ar');
  });
});

describe('directions', () => {
  it('runs Arabic right to left and English left to right', () => {
    expect(directions.ar).toBe('rtl');
    expect(directions.en).toBe('ltr');
  });
});

describe('the dictionaries', () => {
  it('say the same things in both languages', () => {
    // The type system already enforces this. The test is here because a missing key
    // would show a worker an empty screen element, which is worth more than one guard.
    expect(Object.keys(ar).sort()).toEqual(Object.keys(en).sort());
  });

  it('leaves nothing blank', () => {
    for (const [key, value] of Object.entries(ar)) {
      expect(value.trim(), `ar is empty for ${key}`).not.toBe('');
    }
  });

  it('actually says something different in Arabic', () => {
    // Catches a key copied across and never translated. The brand name is the one
    // deliberate exception -- it is what is painted on the building.
    const keys = Object.keys(en) as (keyof typeof en)[];
    const untranslated = keys.filter(
      (key) => ar[key] === en[key] && key !== 'app.name',
    );

    expect(untranslated).toEqual([]);
  });

  it('offers each language from the other one', () => {
    // The button shows the language it switches to, so English offers Arabic.
    expect(dictionaries.en['top.language']).toBe('العربية');
    expect(dictionaries.ar['top.language']).toBe('English');
  });
});
