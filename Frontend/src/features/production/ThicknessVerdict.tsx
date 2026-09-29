import type { ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';

/**
 * Whether a roll came out at the thickness its product asks for (specification section
 * 19.1).
 *
 * <b>Nothing at all when there is no verdict.</b> Most products have no range until the
 * factory measures them, and a grey "not judged" beside every roll would read as a
 * problem to a man scanning the list. Absence says it better.
 *
 * Out of spec is shown, never hidden, and never stopped: a setup roll at 4 mm is still a
 * plate roll and is still used. The point is that somebody can see how many there were.
 */
export function ThicknessVerdict({ inSpec }: { inSpec: boolean | null }): ReactElement | null {
  const { t } = useTranslation();

  if (inSpec === null) {
    return null;
  }

  return (
    <span
      className={`ms-2 rounded-full px-2 py-0.5 text-xs font-semibold whitespace-nowrap ${
        inSpec ? 'bg-ok-soft text-ok' : 'bg-bad-soft text-bad'
      }`}
    >
      {inSpec ? t('rolls.inSpec') : t('rolls.outOfSpec')}
    </span>
  );
}
