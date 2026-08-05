import type { ReactElement } from 'react';
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

const labels: Record<RollStatus, string> = {
  NeedsTest: 'Needs test',
  Available: 'Available',
  InThermo: 'In thermo',
  Processed: 'Processed',
  Scrapped: 'Scrapped',
};

export function RollStatusBadge({ status }: { status: RollStatus }): ReactElement {
  return (
    <span
      className={`rounded-full px-2.5 py-0.5 text-xs font-semibold whitespace-nowrap ${styles[status]}`}
    >
      {labels[status]}
    </span>
  );
}
