import { useContext } from 'react';
import { LanguageContext, type LanguageContextValue } from '../lib/i18n/language';

/** The words for the current language, and the switch between them. */
export function useTranslation(): LanguageContextValue {
  const context = useContext(LanguageContext);

  if (context === null) {
    throw new Error('useTranslation must be used inside <LanguageProvider>.');
  }

  return context;
}
