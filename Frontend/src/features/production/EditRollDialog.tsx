import { useState, type ReactElement } from 'react';
import { Modal } from '../../components/ui/Modal';
import { useTranslation } from '../../hooks/useTranslation';
import { ApiError } from '../../lib/apiClient';
import { productionApi, type RollDto, type RollSummaryDto } from './api';
import { fromTimeField, toOffsetIso, toTimeField } from './outTime';

interface EditRollDialogProps {
  roll: RollSummaryDto;
  onClose: () => void;
  /** With whether the code changed, so the screen can offer a new label. */
  onSaved: (roll: RollDto, codeChanged: boolean) => void;
}

/**
 * Corrects a roll's code or out time after it was logged — a code mistyped on the label,
 * a roll logged late and stamped with the wrong time (specification section 8).
 *
 * For a roll whose barcode is its code, the barcode follows the code, and a new label is
 * printed. An older roll with a separate R-barcode keeps it.
 */
export function EditRollDialog({
  roll,
  onClose,
  onSaved,
}: EditRollDialogProps): ReactElement {
  const { t } = useTranslation();
  const [rollCode, setRollCode] = useState(roll.rollCode);
  // The time only; the date stays the day the roll came off (section 19.12).
  const [outAt, setOutAt] = useState(() => toTimeField(new Date(roll.producedAt)));
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  async function save(): Promise<void> {
    const moment = fromTimeField(outAt, new Date(roll.producedAt));
    const producedAt = moment === null ? null : toOffsetIso(moment);
    if (producedAt === null) {
      setError(t('rolls.outAtHint'));
      return;
    }

    setError(null);
    setIsSaving(true);
    try {
      const saved = await productionApi.updateRoll(roll.id, {
        rollCode: rollCode.trim(),
        producedAt,
      });
      onSaved(saved, saved.rollCode !== roll.rollCode);
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
    <Modal title={`${t('rolls.editTitle')} — ${roll.rollCode}`} onClose={onClose}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <div className="mb-4">
          <label className="field-label" htmlFor="edit-roll-code">
            {t('term.rollCode')}
          </label>
          <input
            id="edit-roll-code"
            className="field-input font-mono"
            maxLength={30}
            autoComplete="off"
            value={rollCode}
            disabled={isSaving}
            onChange={(event) => {
              setRollCode(event.target.value);
            }}
          />
        </div>

        <div className="mb-4">
          <label className="field-label" htmlFor="edit-roll-out">
            {t('rolls.outAt')}
          </label>
          <input
            id="edit-roll-out"
            type="time"
            className="field-input"
            value={outAt}
            disabled={isSaving}
            onChange={(event) => {
              setOutAt(event.target.value);
            }}
          />
        </div>

        <p className="mb-4 rounded-control bg-canvas px-4 py-3 text-sm text-ink-soft">
          {t('rolls.editNote')}
        </p>

        {error !== null && (
          <p
            role="alert"
            className="mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
          >
            {error}
          </p>
        )}

        <button type="submit" className="btn-primary" disabled={isSaving}>
          {isSaving ? t('common.saving') : t('common.save')}
        </button>
      </form>
    </Modal>
  );
}
