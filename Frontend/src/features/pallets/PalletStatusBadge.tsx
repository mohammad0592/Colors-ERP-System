import type { ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import type { TranslationKey } from '../../lib/i18n/en';
import type { PalletStatus } from './api';

/**
 * None of these is stored. They are read off three dates and the bags on the pallet
 * (specification section 10).
 */
const tones: Record<PalletStatus, { label: TranslationKey; className: string }> = {
  Empty: { label: 'status.pallet.empty', className: 'bg-line text-ink-muted' },
  Opened: { label: 'status.pallet.building', className: 'bg-brand-50 text-brand-700' },
  Completed: { label: 'status.pallet.full', className: 'bg-ok-soft text-ok' },
  Shipped: { label: 'status.pallet.shipped', className: 'bg-canvas text-ink-soft' },
  Cancelled: { label: 'status.pallet.cancelled', className: 'bg-bad-soft text-bad' },
};

export function PalletStatusBadge({ status }: { status: PalletStatus }): ReactElement {
  const { t } = useTranslation();
  const tone = tones[status];

  return (
    <span
      className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${tone.className}`}
    >
      {t(tone.label)}
    </span>
  );
}
