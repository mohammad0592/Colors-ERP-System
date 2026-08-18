import type { ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import type { TranslationKey } from '../../lib/i18n/en';
import type { ShiftReportStatus } from './api';

const styles: Record<ShiftReportStatus, { label: TranslationKey; className: string }> = {
  // Still running — production goes here, and readings can still be entered.
  Open: { label: 'status.shift.open', className: 'bg-ok-soft text-ok' },
  // Finished. Nothing more may be posted to it.
  Closed: { label: 'status.shift.closed', className: 'bg-line text-ink-muted' },
  // Reopened to fix its record while another shift runs. Its readings, times and crew
  // can be corrected; no new rolls, bags or pallets belong to it.
  Correcting: { label: 'status.shift.correcting', className: 'bg-warn-soft text-warn' },
};

export function ShiftStatusBadge({
  status,
}: {
  status: ShiftReportStatus;
}): ReactElement {
  const { t } = useTranslation();
  const tone = styles[status];

  return (
    <span
      className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${tone.className}`}
    >
      {t(tone.label)}
    </span>
  );
}
