import type { ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import type { TranslationKey } from '../../lib/i18n/en';
import type { RollStatus } from './api';

const styles: Record<RollStatus, string> = {
  // Made, but not yet measured — the thermo will refuse it.
  NeedsTest: 'bg-warn-soft text-warn',
  // Measured and in stock. May sit here for weeks; rolls are used to order.
  Available: 'bg-ok-soft text-ok',
  InThermo: 'bg-brand-50 text-brand-700',
  Processed: 'bg-line text-ink-muted',
  Scrapped: 'bg-bad-soft text-bad',
};

const labels: Record<RollStatus, TranslationKey> = {
  NeedsTest: 'status.roll.needsTest',
  Available: 'status.roll.available',
  InThermo: 'status.roll.inThermo',
  Processed: 'status.roll.processed',
  Scrapped: 'status.roll.scrapped',
};

/**
 * A roll's state. Before it is ready it says what is still missing — the test, the
 * measuring, or both — since the two are done separately, in either order (19.12).
 */
export function RollStatusBadge({
  status,
  needsTest = true,
  needsMeasure = false,
}: {
  status: RollStatus;
  needsTest?: boolean;
  needsMeasure?: boolean;
}): ReactElement {
  const { t } = useTranslation();
  const label: TranslationKey =
    status !== 'NeedsTest'
      ? labels[status]
      : needsTest && needsMeasure
        ? 'status.roll.needsTestAndMeasure'
        : needsMeasure
          ? 'status.roll.needsMeasure'
          : 'status.roll.needsTest';

  return (
    <span
      className={`rounded-full px-2.5 py-0.5 text-xs font-semibold whitespace-nowrap ${styles[status]}`}
    >
      {t(label)}
    </span>
  );
}
