import type { ReactElement } from 'react';
import type { PalletStatus } from './api';

/**
 * None of these is stored. They are read off two dates and the bags on the pallet
 * (specification section 10).
 */
const tones: Record<PalletStatus, { label: string; className: string }> = {
  Empty: { label: 'Empty', className: 'bg-line text-ink-muted' },
  Opened: { label: 'Building', className: 'bg-brand-50 text-brand-700' },
  Completed: { label: 'Full', className: 'bg-ok-soft text-ok' },
  Shipped: { label: 'Shipped', className: 'bg-canvas text-ink-soft' },
};

export function PalletStatusBadge({ status }: { status: PalletStatus }): ReactElement {
  const tone = tones[status];

  return (
    <span className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${tone.className}`}>
      {tone.label}
    </span>
  );
}
