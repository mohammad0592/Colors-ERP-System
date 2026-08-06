import { useState, type ReactElement } from 'react';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import { palletsApi, type PalletDto } from './api';

interface CancelPalletDialogProps {
  palletId: number;
  palletNumber: number;
  onClose: () => void;
  onCancelled: (pallet: PalletDto) => void;
}

/**
 * Cancelling a pallet started by mistake (specification section 10).
 *
 * The wooden pallet came out of the store the moment this one was started, so cancelling
 * puts it back. Only ever offered on an empty pallet — once a bag is on it the wood is
 * under the bags, and taking the bags off is the way back.
 */
export function CancelPalletDialog({
  palletId,
  palletNumber,
  onClose,
  onCancelled,
}: CancelPalletDialogProps): ReactElement {
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      const pallet = await palletsApi.cancel(palletId, reason.trim());
      onCancelled(pallet);
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
    <Modal title={`Cancel pallet ${String(palletNumber)}`} onClose={onClose}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <div className="mb-4">
          <label className="field-label" htmlFor="cancel-reason">
            Why is it being cancelled?
          </label>
          <input
            id="cancel-reason"
            className="field-input"
            maxLength={300}
            placeholder="Started on the wrong line"
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
            className="mb-4 rounded-control border border-l-4 border-bad/30 border-l-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
          >
            {error}
          </p>
        )}

        <button
          type="submit"
          className="btn-primary"
          disabled={isSaving || reason.trim() === ''}
        >
          {isSaving ? 'Saving…' : 'Cancel this pallet'}
        </button>
        <p className="mt-2 text-xs text-ink-muted">
          The wooden pallet goes back to the store and can be used for the next one. The
          pallet itself stays in the record with your reason — nothing is deleted.
        </p>
      </form>
    </Modal>
  );
}
