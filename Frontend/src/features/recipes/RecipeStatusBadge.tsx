import type { ReactElement } from 'react';
import type { RecipeStatus } from './api';

const styles: Record<RecipeStatus, string> = {
  // In production — the one an operator will pick.
  Current: 'bg-ok-soft text-ok',
  // Being written, editable, not usable yet.
  Draft: 'bg-warn-soft text-warn',
  // Replaced, kept for ever so old rolls keep their formula.
  Archived: 'bg-line text-ink-muted',
};

export function RecipeStatusBadge({ status }: { status: RecipeStatus }): ReactElement {
  return (
    <span
      className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${styles[status]}`}
    >
      {status}
    </span>
  );
}
