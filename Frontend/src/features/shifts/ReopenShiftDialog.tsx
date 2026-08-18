import { useState, type ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import { shiftReportsApi, type ShiftReportSummaryDto } from './api';
import { formatDate } from './shiftFormat';

interface ReopenShiftDialogProps {
  report: ShiftReportSummaryDto;
  onClose: () => void;
  onReopened: () => void;
}

/**
 * Reopens a closed shift.
 *
 * The reason is required and is kept in the shift's notes, because reopening changes
 * figures somebody may already have read. Administrator only — the server enforces it
 * as well.
 */
export function ReopenShiftDialog({
  report,
  onClose,
  onReopened,
}: ReopenShiftDialogProps): ReactElement {
  const { t } = useTranslation();
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      await shiftReportsApi.reopen(report.id, reason);
      onReopened();
      onClose();
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : 'Something went wrong. Try again.',
      );
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <Modal title="Reopen this shift?" onClose={onClose}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <p className="mb-4 text-sm leading-relaxed text-ink-soft">
          {t('term.shift')} <strong>{report.shiftName}</strong> on {formatDate(report.productionDate)}{' '}
          will accept changes again, with all {report.lineCount} line
          {report.lineCount === 1 ? '' : 's'} on it. The reason is kept on the shift for
          good.
        </p>

        <div className="mb-4">
          <label className="field-label" htmlFor="reopen-reason">
            Why is it being reopened?
          </label>
          <textarea
            id="reopen-reason"
            rows={3}
            maxLength={400}
            className="field-input"
            value={reason}
            disabled={isSaving}
            placeholder="The end meter reading was written down wrong."
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
          className="btn-primary"
          disabled={isSaving || reason.trim() === ''}
        >
          {isSaving ? 'Reopening…' : 'Reopen shift'}
        </button>
      </form>
    </Modal>
  );
}
