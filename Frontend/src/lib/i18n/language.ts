import { createContext } from 'react';
import { ar } from './ar';
import { en, type TranslationKey } from './en';

/**
 * The two languages, and which way each one runs.
 *
 * Kept apart from the provider component so the hook, the provider and the tests can
 * each import what they need without pulling a React component into a unit test.
 */

export type Language = 'en' | 'ar';

export const dictionaries: Record<Language, Record<TranslationKey, string>> = { en, ar };

/** Arabic runs right to left, and the whole layout mirrors with it. */
export const directions: Record<Language, 'ltr' | 'rtl'> = { en: 'ltr', ar: 'rtl' };

/** What the browser remembers between visits. */
export const STORAGE_KEY = 'colors.language';

/**
 * The language to start in.
 *
 * The saved choice wins, because a man who switched to Arabic yesterday meant it. Only
 * when there is no choice yet does the browser's own language get a say, and anything
 * other than Arabic lands on English.
 */
export function initialLanguage(
  stored: string | null,
  browserLanguage: string | undefined,
): Language {
  if (stored === 'ar' || stored === 'en') {
    return stored;
  }

  return browserLanguage?.toLowerCase().startsWith('ar') === true ? 'ar' : 'en';
}

export interface LanguageContextValue {
  language: Language;
  direction: 'ltr' | 'rtl';
  setLanguage: (language: Language) => void;
  /** The word for this key in the current language. */
  t: (key: TranslationKey) => string;
}

export const LanguageContext = createContext<LanguageContextValue | null>(null);
