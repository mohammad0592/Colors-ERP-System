import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type ReactElement,
  type ReactNode,
} from 'react';
import type { TranslationKey } from './en';
import {
  dictionaries,
  directions,
  initialLanguage,
  LanguageContext,
  setCurrentLanguage,
  STORAGE_KEY,
  type Language,
} from './language';

/**
 * Holds which language the screens are in, and keeps the document in step with it.
 *
 * Two things have to move together and are easy to get half right. `lang` is what makes
 * a screen reader pronounce Arabic as Arabic rather than spelling it out in English, and
 * `dir` is what makes the browser mirror the whole layout — the sidebar to the right, the
 * scrollbar to the left, text starting where the eye starts.
 *
 * Setting `dir` on the document rather than on a wrapping element is deliberate: dialogs
 * are rendered into the body, outside the app's own tree, and a dir set inside the tree
 * would leave every one of them running the wrong way.
 */
export function LanguageProvider({ children }: { children: ReactNode }): ReactElement {
  const [language, setLanguageState] = useState<Language>(() =>
    initialLanguage(
      typeof localStorage === 'undefined' ? null : localStorage.getItem(STORAGE_KEY),
      typeof navigator === 'undefined' ? undefined : navigator.language,
    ),
  );

  const direction = directions[language];

  useEffect(() => {
    document.documentElement.lang = language;
    document.documentElement.dir = direction;

    // Keep the copy the plain modules read in step. Set here rather than in the setter
    // so it is also right on the very first render, before anybody has switched.
    setCurrentLanguage(language);
  }, [language, direction]);

  const setLanguage = useCallback((next: Language) => {
    setLanguageState(next);
    try {
      localStorage.setItem(STORAGE_KEY, next);
    } catch {
      // A browser with storage switched off still gets the language it asked for; it
      // simply forgets by the next visit. Not a reason to fail.
    }
  }, []);

  const value = useMemo(() => {
    const dictionary = dictionaries[language];

    return {
      language,
      direction,
      setLanguage,
      t: (key: TranslationKey): string => dictionary[key],
    };
  }, [language, direction, setLanguage]);

  return <LanguageContext value={value}>{children}</LanguageContext>;
}
