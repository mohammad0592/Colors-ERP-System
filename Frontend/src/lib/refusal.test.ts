import { describe, expect, it } from 'vitest';
import { ar } from './i18n/ar';
import { en } from './i18n/en';
import { isTranslationKey } from './i18n/language';

/**
 * The refusals the server sends, and the words the screens put in their place.
 *
 * The two halves are written in different languages, in different repositories of the
 * mind, months apart. Nothing but a test notices when a code the server sends has no
 * wording here — the screen simply falls back to English and nobody reports it, because
 * an English refusal looks like a refusal rather than like a bug.
 */

/** Every refusal code the backend can send, as the dictionary files them. */
const refusalKeys = Object.keys(en).filter((key) => key.startsWith('refusal.'));

describe('refusal wording', () => {
  it('has Arabic for every refusal', () => {
    for (const key of refusalKeys) {
      expect(ar[key as keyof typeof ar], `no Arabic for ${key}`).toBeTruthy();
    }
  });

  it('uses the same numbered slots in both languages', () => {
    // "Pallet {0} has already gone" must not become "المنصة {1}" — the value would land
    // in the wrong hole, or in no hole at all.
    for (const key of refusalKeys) {
      const slots = (text: string): string[] =>
        [...text.matchAll(/\{(\d+)\}/g)].map((m) => m[1]).sort();

      expect(
        slots(ar[key as keyof typeof ar]),
        `${key} does not use the same values in both languages`,
      ).toEqual(slots(en[key as keyof typeof en]));
    }
  });

  it('recognises a namespaced code as a key', () => {
    // What apiClient does with what the server sends.
    expect(isTranslationKey('refusal.pallet.alreadyGone')).toBe(true);
  });

  it('does not recognise the bare code the server sends', () => {
    // The namespace is added by the client. If this ever passes, the two have been
    // wired together twice and one of them is now wrong.
    expect(isTranslationKey('pallet.alreadyGone')).toBe(false);
  });

  it('does not recognise a code nobody has written wording for', () => {
    expect(isTranslationKey('refusal.pallet.somethingNew')).toBe(false);
  });
});
