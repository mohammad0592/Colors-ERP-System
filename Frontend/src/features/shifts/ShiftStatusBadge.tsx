import type { ReactElement } from 'react';
import type { ShiftReportStatus } from './api';

const styles: Record<ShiftReportStatus, string> = {
  // Still running — readings can still be entered.
  Open: 'bg-ok-soft text-ok',
  // Finished. Nothing more may be posted to it.
  Closed: 'bg-line text-ink-muted',
};

export function ShiftStatusBadge({ status }: { status: ShiftReportStatus }): ReactElement {
  return (
    <span
      className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${styles[status]}`}
    >
      {status}
    </span>
  );
}
