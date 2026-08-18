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

export function RollStatusBadge({ status }: { status: RollStatus }): ReactElement {
  const { t } = useTranslation();

  return (
    <span
      className={`rounded-full px-2.5 py-0.5 text-xs font-semibold whitespace-nowrap ${styles[status]}`}
    >
      {t(labels[status])}
    </span>
  );
}
