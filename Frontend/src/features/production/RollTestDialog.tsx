import { useState, type ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import { productionApi, type RollDto, type RollSummaryDto } from './api';

interface RollTestDialogProps {
  roll: RollSummaryDto;
  onClose: () => void;
  onSaved: (roll: RollDto) => void;
}

/**
 * The roll test, taken once as the roll leaves the extruder (specification section 8):
 * the sample plate and the four thickness readings. Weight and length are measured on
 * the production screen instead (section 19.12).
 *
 * The four thickness boxes are named by position — RS, RM, LM, LS — because that is
 * what the gauge shows and what the man reads. Their average is displayed as he types
 * but never typed itself.
 *
 * With the measuring, this is what lets the thermo use the roll. It is not approval:
 * nothing is compared against a limit, and no roll is rejected for its numbers.
 */
export function RollTestDialog({
  roll,
  onClose,
  onSaved,
}: RollTestDialogProps): ReactElement {
  const { t } = useTranslation();
  const [plateWeight, setPlateWeight] = useState('');
  const [rs, setRs] = useState('');
  const [rm, setRm] = useState('');
  const [lm, setLm] = useState('');
  const [ls, setLs] = useState('');
  const [notes, setNotes] = useState('');
  // Absorbent rolls only (section 19.14): a piece weighed dry, then after ten minutes in
  // water. The percentage is shown as he types, never typed.
  const [dry, setDry] = useState('');
  const [wet, setWet] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const readings = [rs, rm, lm, ls].map(Number);
  const allReadings =
    [rs, rm, lm, ls].every((v) => v.trim() !== '') &&
    readings.every((v) => Number.isFinite(v) && v > 0);

  const average = allReadings
    ? Math.round((readings.reduce((sum, v) => sum + v, 0) / 4) * 1000) / 1000
    : null;

  const dryNumber = Number(dry);
  const wetNumber = Number(wet);
  const absorbencyTyped = dryNumber > 0 && wetNumber > 0;
  const absorbencyBackwards = absorbencyTyped && wetNumber < dryNumber;
  const absorbency = absorbencyTyped
    ? Math.round(((wetNumber - dryNumber) / dryNumber) * 100 * 100) / 100
    : null;

  const complete =
    Number(plateWeight) > 0 &&
    allReadings &&
    (!roll.isAbsorbent || (absorbencyTyped && !absorbencyBackwards));

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      const saved = await productionApi.saveTest(roll.id, {
        plateWeight: Number(plateWeight),
        thicknessRs: readings[0] ?? 0,
        thicknessRm: readings[1] ?? 0,
        thicknessLm: readings[2] ?? 0,
        thicknessLs: readings[3] ?? 0,
        notes: notes.trim() === '' ? null : notes.trim(),
        ...(roll.isAbsorbent
          ? { absorbencyDryWeight: dryNumber, absorbencyWetWeight: wetNumber }
          : {}),
      });
      onSaved(saved);
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
    <Modal title={`${t('rolls.testTitle')} — ${roll.rollCode}`} onClose={onClose}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <div className="mb-5 flex flex-wrap items-center gap-3 border-b border-line pb-4 text-sm text-ink-muted">
          <span className="font-mono font-semibold text-ink">{roll.rollCode}</span>
          <span>
            · {roll.colorName} · recipe {roll.recipeNumber} {roll.recipeFamilyName}
          </span>
        </div>

        <Field label={t('rolls.plateWeightG')} htmlFor="test-plate">
          <input
            id="test-plate"
            type="number"
            inputMode="decimal"
            step="0.001"
            className="field-input"
            value={plateWeight}
            disabled={isSaving}
            onChange={(event) => {
              setPlateWeight(event.target.value);
            }}
          />
        </Field>

        <p className="field-label">{t('rolls.fourReadings')}</p>
        <div className="mb-4 grid grid-cols-2 gap-3 sm:grid-cols-4">
          <Reading
            id="test-rs"
            label="RS"
            value={rs}
            onChange={setRs}
            disabled={isSaving}
          />
          <Reading
            id="test-rm"
            label="RM"
            value={rm}
            onChange={setRm}
            disabled={isSaving}
          />
          <Reading
            id="test-lm"
            label="LM"
            value={lm}
            onChange={setLm}
            disabled={isSaving}
          />
          <Reading
            id="test-ls"
            label="LS"
            value={ls}
            onChange={setLs}
            disabled={isSaving}
          />
        </div>

        <div className="mb-4 rounded-control bg-canvas px-4 py-3">
          <div className="flex items-baseline justify-between gap-3">
            <span className="text-sm font-medium text-ink-soft">
              {t('rolls.averageThickness')}
            </span>
            <span className="text-lg font-bold text-ink">{average ?? '—'}</span>
          </div>
          <p className="mt-1 text-xs text-ink-muted">{t('rolls.meanOfFour')}</p>
        </div>

        {roll.isAbsorbent && (
          <>
            <p className="field-label">{t('rolls.absorbencyTitle')}</p>
            <div className="mb-4 grid gap-3 sm:grid-cols-2">
              <Field label={t('rolls.absorbencyDry')} htmlFor="test-dry">
                <input
                  id="test-dry"
                  type="number"
                  inputMode="decimal"
                  step="0.001"
                  min="0"
                  className="field-input"
                  value={dry}
                  disabled={isSaving}
                  onChange={(event) => {
                    setDry(event.target.value);
                  }}
                />
              </Field>
              <Field label={t('rolls.absorbencyWet')} htmlFor="test-wet">
                <input
                  id="test-wet"
                  type="number"
                  inputMode="decimal"
                  step="0.001"
                  min="0"
                  className="field-input"
                  value={wet}
                  disabled={isSaving}
                  onChange={(event) => {
                    setWet(event.target.value);
                  }}
                />
              </Field>
            </div>

            {absorbencyBackwards && (
              <p className="mb-4 rounded-control border border-s-4 border-warn/30 border-s-warn bg-warn-soft px-4 py-3 text-sm font-medium text-warn">
                {t('rolls.absorbencyBackwards')}
              </p>
            )}

            <div className="mb-4 rounded-control bg-canvas px-4 py-3">
              <div className="flex items-baseline justify-between gap-3">
                <span className="text-sm font-medium text-ink-soft">
                  {t('rolls.absorbencyPercent')}
                </span>
                <span className="text-lg font-bold text-ink">
                  {absorbency === null || absorbencyBackwards
                    ? '—'
                    : `${String(absorbency)}%`}
                </span>
              </div>
              <p className="mt-1 text-xs text-ink-muted">{t('rolls.absorbencyNote')}</p>
            </div>
          </>
        )}

        <div className="mb-4">
          <label className="field-label" htmlFor="test-notes">
            {t('field.note')}{' '}
            <span className="font-normal text-ink-muted">(optional)</span>
          </label>
          <input
            id="test-notes"
            className="field-input"
            maxLength={300}
            value={notes}
            disabled={isSaving}
            onChange={(event) => {
              setNotes(event.target.value);
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

        <button type="submit" className="btn-primary" disabled={isSaving || !complete}>
          {isSaving ? 'Saving…' : t('rolls.saveAndRelease')}
        </button>
        <p className="mt-2 text-xs text-ink-muted">{t('rolls.thermoCanUse')}</p>
      </form>
    </Modal>
  );
}

function Field({
  label,
  htmlFor,
  children,
}: {
  label: string;
  htmlFor: string;
  children: ReactElement;
}): ReactElement {
  return (
    <div className="mb-4">
      <label className="field-label" htmlFor={htmlFor}>
        {label}
      </label>
      {children}
    </div>
  );
}

function Reading({
  id,
  label,
  value,
  onChange,
  disabled,
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  disabled: boolean;
}): ReactElement {
  return (
    <div>
      <label className="field-label" htmlFor={id}>
        {label}
      </label>
      <input
        id={id}
        type="number"
        step="0.001"
        min="0"
        className="field-input text-center"
        value={value}
        disabled={disabled}
        onChange={(event) => {
          onChange(event.target.value);
        }}
      />
    </div>
  );
}
