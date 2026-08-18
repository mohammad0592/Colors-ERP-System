import { useState, type ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
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
  const { t } = useTranslation();
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
        caught instanceof ApiError ? caught.message : t('common.somethingWentWrong'),
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
            {t('pallets.whyCancel')}
          </label>
          <input
            id="cancel-reason"
            className="field-input"
            maxLength={300}
            placeholder={t('pallets.wrongLine')}
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
          {isSaving ? 'Saving…' : t('pallets.cancelThis')}
        </button>
        <p className="mt-2 text-xs text-ink-muted">
          {t('pallets.woodGoesBack')}
        </p>
      </form>
    </Modal>
  );
}
