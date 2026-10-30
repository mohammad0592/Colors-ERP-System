import { useState, type ReactElement } from 'react';
import { Modal } from '../../components/ui/Modal';
import { useTranslation } from '../../hooks/useTranslation';
import { ApiError } from '../../lib/apiClient';
import { thermoApi, type ThermoRunDto } from './api';

interface LeaveUnfinishedDialogProps {
  runId: number;
  rollCode: string;
  onClose: () => void;
  onLeft: (run: ThermoRunDto) => void;
}

/**
 * Takes a roll out of the thermo before it is finished (specification section 19.23).
 *
 * The reason is required: the roll goes back to stock half used, and whoever picks it up
 * next — maybe on another shift — has to know why. What it made so far is counted next,
 * on Thermo Tests, as for any run.
 */
export function LeaveUnfinishedDialog({
  runId,
  rollCode,
  onClose,
  onLeft,
}: LeaveUnfinishedDialogProps): ReactElement {
  const { t } = useTranslation();
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      onLeft(await thermoApi.leaveUnfinished(runId, reason.trim()));
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
    <Modal title={`${t('thermo.notFinished')} — ${rollCode}`} onClose={onClose}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <p className="mb-4 text-sm text-ink-soft">{t('thermo.notFinishedExplain')}</p>

        <div className="mb-4">
          <label className="field-label" htmlFor="unfinished-reason">
            {t('thermo.whyNotFinished')}
          </label>
          <textarea
            id="unfinished-reason"
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
          className="btn-primary"
          disabled={isSaving || reason.trim() === ''}
        >
          {isSaving ? t('common.saving') : t('thermo.takeOutUnfinished')}
        </button>
      </form>
    </Modal>
  );
}
