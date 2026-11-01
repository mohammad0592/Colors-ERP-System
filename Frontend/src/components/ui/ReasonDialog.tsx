import { useState, type ReactElement } from 'react';
import { Modal } from './Modal';
import { useTranslation } from '../../hooks/useTranslation';
import { ApiError } from '../../lib/apiClient';

interface ReasonDialogProps {
  /** "Cancel roll 04N2309260844-White". */
  title: string;
  /** What will happen, in a sentence or two, so nobody presses it to find out. */
  explanation: string;
  /** The question above the box: "Why is it being cancelled?" */
  question: string;
  confirmLabel: string;
  /** Red for what takes something away, blue for a correction. */
  tone?: 'danger' | 'primary';
  /** Does the work. A refusal thrown from here is shown in the window, which stays open. */
  onConfirm: (reason: string) => Promise<void>;
  onClose: () => void;
}

/**
 * Asks why before a correction is made — cancelling, scrapping, reversing, taking back
 * (specification section 19.27).
 *
 * One window for all of them, because they are one idea: the record stays, and what was
 * done to it is written beside it with the reason. The button stays off until a reason
 * is typed, as the server would refuse it anyway.
 */
export function ReasonDialog({
  title,
  explanation,
  question,
  confirmLabel,
  tone = 'danger',
  onConfirm,
  onClose,
}: ReasonDialogProps): ReactElement {
  const { t } = useTranslation();
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      await onConfirm(reason.trim());
      onClose();
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : t('common.somethingWentWrong'),
      );
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <Modal title={title} onClose={onClose}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <p className="mb-4 text-sm text-ink-soft">{explanation}</p>

        <div className="mb-4">
          <label className="field-label" htmlFor="correction-reason">
            {question}
          </label>
          <textarea
            id="correction-reason"
            className="field-input min-h-24"
            maxLength={500}
            value={reason}
            disabled={isSaving}
            onChange={(event) => {
              setReason(event.target.value);
            }}
          />
        </div>

        {error !== null && (
          <p
            role="alert"
            className="mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
          >
            {error}
          </p>
        )}

        <button
          type="submit"
          className={
            tone === 'danger'
              ? 'min-h-touch w-full rounded-control bg-bad px-6 text-base font-semibold text-white transition-colors hover:bg-bad/90 disabled:opacity-50'
              : 'btn-primary'
          }
          disabled={isSaving || reason.trim() === ''}
        >
          {isSaving ? t('common.saving') : confirmLabel}
        </button>
      </form>
    </Modal>
  );
}
