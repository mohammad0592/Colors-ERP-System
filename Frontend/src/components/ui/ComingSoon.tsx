import type { ReactElement } from 'react';
import { PageHeader } from './PageHeader';

interface ComingSoonProps {
  title: string;
  /** Which phase of the build order this screen belongs to. */
  phase: string;
  /** What the screen will do, in plain words. */
  description: string;
}

/**
 * A screen that is planned but not built.
 *
 * Shown instead of a blank page or a dead link, so the factory can walk the whole
 * menu and see what is coming — and so nobody wonders whether a page is broken.
 */
export function ComingSoon({ title, phase, description }: ComingSoonProps): ReactElement {
  return (
    <>
      <PageHeader title={title} />

      <div className="card mx-auto max-w-xl p-8 text-center">
        <span className="inline-block rounded-full bg-warn-soft px-3 py-1 text-xs font-semibold text-warn">
          {phase}
        </span>
        <p className="mt-4 text-ink-soft">{description}</p>
        <p className="mt-4 text-sm text-ink-muted">
          This screen is not built yet. The order of work is in the specification, section
          17.
        </p>
      </div>
    </>
  );
}
