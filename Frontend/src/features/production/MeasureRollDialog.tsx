import { useState, type ReactElement } from 'react';
import { Modal } from '../../components/ui/Modal';
import { useTranslation } from '../../hooks/useTranslation';
import { ApiError } from '../../lib/apiClient';
import { productionApi, type RollDto, type RollSummaryDto } from './api';

/** A roll weighs between these. 350 means the length went into the weight box. */
const MinWeight = 50;
const MaxWeight = 150;

interface MeasureRollDialogProps {
  /** From the list, or the roll just logged. */
  roll: Pick<
    RollSummaryDto,
    'id' | 'rollCode' | 'colorName' | 'recipeFamilyName' | 'weight' | 'length'
  >;
  onClose: () => void;
  onSaved: (roll: RollDto) => void;
}

/**
 * The roll's weight and length, taken where it comes off the extruder
 * (specification section 19.12).
 *
 * Moved here from the roll test, which is now the plate and the thickness. The roll goes
 * to the thermo once it has both, in either order. Opening it again on a roll already
 * measured corrects the figures, until the roll goes into the thermo.
 *
 * The label is printed after this, not when the roll is logged: it carries the weight and
 * length (section 19.15).
 */
export function MeasureRollDialog({
  roll,
  onClose,
  onSaved,
}: MeasureRollDialogProps): ReactElement {
  const { t } = useTranslation();
  const [weight, setWeight] = useState(roll.weight === null ? '' : String(roll.weight));
  const [length, setLength] = useState(roll.length === null ? '' : String(roll.length));
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const weightNumber = Number(weight);
  const weightTyped = weight.trim() !== '' && Number.isFinite(weightNumber);
  // The mistake that is already in the real Roll Log: the length typed into the weight
  // box. Caught while he is still at the machine.
  const weightLooksWrong =
    weightTyped && (weightNumber < MinWeight || weightNumber > MaxWeight);
  const complete = weightTyped && !weightLooksWrong && Number(length) > 0;

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      onSaved(
        await productionApi.measureRoll(roll.id, {
          weight: weightNumber,
          length: Number(length),
        }),
      );
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
    <Modal title={`${t('rolls.measure')} — ${roll.rollCode}`} onClose={onClose}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <div className="mb-5 border-b border-line pb-4 text-sm text-ink-muted">
          <span className="font-mono font-semibold text-ink">{roll.rollCode}</span> ·{' '}
          {roll.colorName} · {roll.recipeFamilyName}
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <div className="mb-4">
            <label className="field-label" htmlFor="measure-weight">
              {t('field.weightKg')}
            </label>
            <input
              id="measure-weight"
              type="number"
              inputMode="decimal"
              step="0.001"
              className="field-input"
              value={weight}
              disabled={isSaving}
              onChange={(event) => {
                setWeight(event.target.value);
              }}
            />
          </div>
          <div className="mb-4">
            <label className="field-label" htmlFor="measure-length">
              {t('field.length')}
            </label>
            <input
              id="measure-length"
              type="number"
              inputMode="decimal"
              step="0.001"
              className="field-input"
              value={length}
              disabled={isSaving}
              onChange={(event) => {
                setLength(event.target.value);
              }}
            />
          </div>
        </div>

        {weightLooksWrong && (
          <p className="mb-4 rounded-control border border-s-4 border-warn/30 border-s-warn bg-warn-soft px-4 py-3 text-sm font-medium text-warn">
            {t('rolls.weightLooksWrong')}
          </p>
        )}

        {error !== null && (
          <p
            role="alert"
            className="mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
          >
            {error}
          </p>
        )}

        <button type="submit" className="btn-primary" disabled={isSaving || !complete}>
          {isSaving ? t('common.saving') : t('common.save')}
        </button>
        <p className="mt-2 text-xs text-ink-muted">{t('rolls.measureNote')}</p>
      </form>
    </Modal>
  );
}
