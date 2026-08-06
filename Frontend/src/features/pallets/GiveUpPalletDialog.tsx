import { useState, type ReactElement } from 'react';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import { palletsApi, type PalletDto } from './api';

interface GiveUpPalletDialogProps {
  palletId: number;
  palletNumber: number;
  onClose: () => void;
  onGivenUp: (pallet: PalletDto) => void;
}

/**
 * Giving up on a pallet started by mistake (specification section 10).
 *
 * The wooden pallet went out of the store the moment this one was started, so giving up
 * puts it back. Only ever offered on an empty pallet — once a bag is on it the wood is
 * under the bags, and taking the bags off is the way back.
 */
export function GiveUpPalletDialog({
  palletId,
  palletNumber,
  onClose,
  onGivenUp,
}: GiveUpPalletDialogProps): ReactElement {
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      const pallet = await palletsApi.cancel(palletId, reason.trim());
      onGivenUp(pallet);
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
    <Modal title={`Give up on pallet ${String(palletNumber)}`} onClose={onClose}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <div className="mb-4">
          <label className="field-label" htmlFor="give-up-reason">
            Why is it being given up on?
          </label>
          <input
            id="give-up-reason"
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
          {isSaving ? 'Saving…' : 'Give it up'}
        </button>
        <p className="mt-2 text-xs text-ink-muted">
          The wooden pallet goes back to the store and can be used for the next one. The
          pallet itself stays in the record with your reason — nothing is deleted.
        </p>
      </form>
    </Modal>
  );
}
