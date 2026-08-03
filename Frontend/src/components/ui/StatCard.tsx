import type { ReactElement } from 'react';
import { Icon } from './Icon';

interface StatCardProps {
  icon: string;
  /** The big number, already formatted — "12,480 kg". */
  value: string;
  label: string;
  /** The small line underneath, such as "across all categories". */
  hint?: string;
  /** Change against the previous period, e.g. 2.4 or -0.2. Omit when unknown. */
  change?: number;
  /** True when a fall is the good direction, as with waste. */
  lowerIsBetter?: boolean;
}

export function StatCard({
  icon,
  value,
  label,
  hint,
  change,
  lowerIsBetter = false,
}: StatCardProps): ReactElement {
  const isGood = change === undefined ? true : lowerIsBetter ? change <= 0 : change >= 0;

  return (
    <div className="card p-5">
      <div className="mb-4 flex items-start justify-between gap-2">
        <span className="grid size-10 place-items-center rounded-control bg-brand-50 text-brand-600">
          <Icon name={icon} />
        </span>

        {change !== undefined && (
          <span
            className={[
              'rounded-full px-2 py-0.5 text-xs font-semibold',
              isGood ? 'bg-ok-soft text-ok' : 'bg-bad-soft text-bad',
            ].join(' ')}
          >
            {change > 0 ? '+' : ''}
            {change}%
          </span>
        )}
      </div>

      <p className="text-2xl font-bold text-ink lg:text-3xl">{value}</p>
      <p className="mt-1 text-sm font-medium text-ink-soft">{label}</p>
      {hint !== undefined && <p className="mt-0.5 text-xs text-ink-muted">{hint}</p>}
    </div>
  );
}
