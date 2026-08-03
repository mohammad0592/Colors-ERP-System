import type { ReactElement, ReactNode } from 'react';

interface PageHeaderProps {
  title: string;
  /** The line under the title — date, shift, line. */
  subtitle?: string;
  /** Buttons on the right, such as "New roll". */
  actions?: ReactNode;
  /** A status pill on the right, such as "All systems normal". */
  badge?: ReactNode;
}

export function PageHeader({
  title,
  subtitle,
  actions,
  badge,
}: PageHeaderProps): ReactElement {
  return (
    <div className="mb-6 flex flex-wrap items-start justify-between gap-4">
      <div className="min-w-0">
        <h1 className="text-2xl font-bold text-ink lg:text-3xl">{title}</h1>
        {subtitle !== undefined && (
          <p className="mt-1 text-sm text-ink-muted">{subtitle}</p>
        )}
      </div>

      {(actions !== undefined || badge !== undefined) && (
        <div className="flex shrink-0 items-center gap-3">
          {badge}
          {actions}
        </div>
      )}
    </div>
  );
}
