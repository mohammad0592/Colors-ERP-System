import { useState, type ReactElement } from 'react';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import { palletsApi, type PalletDto } from './api';

interface ReverseScanDialogProps {
  assignmentId: number;
  barcode: string;
  onClose: () => void;
  onReversed: (pallet: PalletDto) => void;
}

/**
 * Taking a bag back off a pallet (specification section 10).
 *
 * The scan is never deleted. It stays on the pallet with the reason it was undone, and
 * the bag goes back to the store where it can be scanned onto the right pallet. That is
 * why the reason is required — a reversal without one is not a correction.
 */
export function ReverseScanDialog({
  assignmentId,
  barcode,
  onClose,
  onReversed,
}: ReverseScanDialogProps): ReactElement {
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      const pallet = await palletsApi.reverse(assignmentId, reason.trim());
      onReversed(pallet);
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
    <Modal title={`Take bag ${barcode} off the pallet`} onClose={onClose}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <div className="mb-4">
          <label className="field-label" htmlFor="reverse-reason">
            Why is it coming off?
          </label>
          <input
            id="reverse-reason"
            className="field-input"
            maxLength={300}
            placeholder="Scanned onto the wrong pallet"
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
          className="btn-primary"
          disabled={isSaving || reason.trim() === ''}
        >
          {isSaving ? 'Saving…' : 'Take it off'}
        </button>
        <p className="mt-2 text-xs text-ink-muted">
          The bag goes back to the store.
        </p>
      </form>
    </Modal>
  );
}
