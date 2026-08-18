import { useEffect, useRef, type ReactElement, type ReactNode } from 'react';
import { useTranslation } from '../../hooks/useTranslation';

export interface ConfirmRequest {
  title: string;
  /** What will happen, in plain words. Shown as the body of the dialog. */
  message: ReactNode;
  /** The word on the button that goes ahead — "Delete", "Put in production". */
  confirmLabel: string;
  /** Red for anything that removes or freezes something. */
  tone?: 'primary' | 'danger';
  onConfirm: () => void;
}

interface ConfirmDialogProps {
  request: ConfirmRequest;
  onCancel: () => void;
}

/**
 * Asks before doing something that cannot be undone.
 *
 * Replaces the browser's own confirm box, which cannot be styled, says
 * "localhost:5173 says", and blocks the whole browser while it is open — on a
 * factory tablet that looks like the system has crashed.
 *
 * Focus starts on Cancel, so a stray Enter never deletes anything.
 */
export function ConfirmDialog({ request, onCancel }: ConfirmDialogProps): ReactElement {
  const { t } = useTranslation();
  const cancelRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    cancelRef.current?.focus();
  }, []);

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent): void {
      if (event.key === 'Escape') {
        onCancel();
      }
    }

    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [onCancel]);

  const danger = request.tone !== 'primary';

  return (
    <div className="fixed inset-0 z-50 grid place-items-center p-4">
      <button
        type="button"
        aria-label={t('common.cancel')}
        onClick={onCancel}
        className="absolute inset-0 cursor-default bg-ink/50"
      />

      <div
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="confirm-title"
        aria-describedby="confirm-body"
        className="relative w-full max-w-md rounded-card bg-surface p-6 shadow-raised"
      >
        <h2 id="confirm-title" className="text-lg font-bold text-ink">
          {request.title}
        </h2>

        <div id="confirm-body" className="mt-2 text-sm leading-relaxed text-ink-soft">
          {request.message}
        </div>

        <div className="mt-6 flex justify-end gap-3">
          <button
            ref={cancelRef}
            type="button"
            onClick={onCancel}
            className="min-h-touch rounded-control border border-line px-5 text-sm font-semibold text-ink-soft transition-colors hover:bg-canvas"
          >
            {t('common.cancel')}
          </button>
          <button
            type="button"
            onClick={() => {
              request.onConfirm();
              onCancel();
            }}
            className={[
              'min-h-touch rounded-control px-5 text-sm font-semibold text-white transition-colors',
              danger ? 'bg-bad hover:brightness-90' : 'bg-brand-600 hover:bg-brand-700',
            ].join(' ')}
          >
            {request.confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
