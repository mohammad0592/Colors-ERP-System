import { useState, type ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import { palletsApi, type PalletDto } from '../pallets/api';

interface UnshipDialogProps {
  palletId: number;
  palletNumber: number;
  onClose: () => void;
  onReversed: (pallet: PalletDto) => void;
}

/**
 * Undoing a shipping (specification section 10).
 *
 * The reason is required, exactly as taking a bag off a pallet needs one: the pallet is
 * going back into the factory's stock and somebody has to say why. The pallet becomes
 * Full again, which is what it was the moment before it went.
 */
export function UnshipDialog({
  palletId,
  palletNumber,
  onClose,
  onReversed,
}: UnshipDialogProps): ReactElement {
  const { t } = useTranslation();
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      const pallet = await palletsApi.unship(palletId, reason.trim());
      onReversed(pallet);
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
    <Modal title={`Pallet ${String(palletNumber)} did not go`} onClose={onClose}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <div className="mb-4">
          <label className="field-label" htmlFor="unship-reason">
            {t('dispatch.whyComingBack')}
          </label>
          <input
            id="unship-reason"
            className="field-input"
            maxLength={300}
            placeholder={t('dispatch.wrongAtLorry')}
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
          {isSaving ? 'Saving…' : t('dispatch.backToList')}
        </button>
        <p className="mt-2 text-xs text-ink-muted">
          {t('dispatch.backToList')}
        </p>
      </form>
    </Modal>
  );
}
